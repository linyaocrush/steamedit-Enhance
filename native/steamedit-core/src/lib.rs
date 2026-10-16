//! steamedit-core: native (Rust) kernels for Steamedit Enhance.
//!
//! This crate exposes a small C ABI used by the C# WinForms application via P/Invoke,
//! implementing the binary `appinfo.vdf` property-table codec for the two on-disk
//! property encodings used by Steam:
//!
//! * `0x00000000` — classic inline format: UTF-8 NUL-terminated names (`ZString`),
//!   `WString` values stored as NUL-terminated UTF-16LE.
//! * `0x07564429` — string-table format: names and `WString` values are `u32` indices
//!   into a shared string table.
//!
//! Trees cross the FFI boundary through a compact, self-describing "flat" byte stream
//! (see [`flat`]). Two decode modes are provided:
//!
//! * **full** — every property is emitted (used to materialize an entry on demand);
//! * **sparse** — only properties whose key path is selected by a [`PathSet`] are
//!   emitted, everything else is skipped without allocation. This lets the managed
//!   side load an entire `appinfo.vdf` while building small "summary" trees for the
//!   list UI, deferring full trees until an entry is actually viewed or edited.

use std::collections::HashMap;
use std::ffi::CString;
use std::os::raw::c_char;
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::sync::Mutex;

/// Maximum nesting depth, mirrors `PropertyTable.MaxNestingDepth` on the C# side.
const MAX_DEPTH: usize = 64;
/// Maximum size of a single string, mirrors `MaxZStringBuffer` on the C# side.
const MAX_STRING: usize = 1024 * 1024;

// ── flat wire format ────────────────────────────────────────────────────────
//
// A flat stream is a depth-first sequence of tokens:
//   kind:u8 [key:StrRef value]
// where `kind` reuses the on-disk PropertyType values. A `Table` token (0) has no
// value; its children follow until an `EndOfTable` token (8). All integers are
// little-endian. A `StrRef` is a mode byte followed by either length-prefixed UTF-8
// bytes (`INLINE`) or a `u32` string-table index (`INDEX`).
mod flat {
    pub const TABLE: u8 = 0;
    pub const STRING: u8 = 1;
    pub const INT32: u8 = 2;
    pub const FLOAT: u8 = 3;
    pub const WSTRING: u8 = 5;
    pub const COLOR: u8 = 6;
    pub const UINT64: u8 = 7;
    pub const END: u8 = 8;

    pub const STR_INLINE: u8 = 0;
    pub const STR_INDEX: u8 = 1;
}

/// A string reference inside the flat stream.
#[allow(dead_code)] // `Index` is produced by decode and rejected by encode
enum StrRef<'a> {
    Inline(&'a [u8]),
    Index(u32),
}

/// Cursor over a byte slice with checked, allocation-free reads.
struct Reader<'a> {
    buf: &'a [u8],
    pos: usize,
}

impl<'a> Reader<'a> {
    fn new(buf: &'a [u8]) -> Self {
        Reader { buf, pos: 0 }
    }

    fn remaining(&self) -> usize {
        self.buf.len() - self.pos
    }

    fn u8(&mut self) -> Result<u8, String> {
        if self.pos >= self.buf.len() {
            return Err("unexpected end of data while reading byte".into());
        }
        let v = self.buf[self.pos];
        self.pos += 1;
        Ok(v)
    }

    fn take(&mut self, n: usize) -> Result<&'a [u8], String> {
        if n > self.remaining() {
            return Err(format!(
                "unexpected end of data: need {n} bytes, {} remaining",
                self.remaining()
            ));
        }
        let s = &self.buf[self.pos..self.pos + n];
        self.pos += n;
        Ok(s)
    }

    fn u32(&mut self) -> Result<u32, String> {
        let b = self.take(4)?;
        Ok(u32::from_le_bytes([b[0], b[1], b[2], b[3]]))
    }

    fn i32(&mut self) -> Result<i32, String> {
        let b = self.take(4)?;
        Ok(i32::from_le_bytes([b[0], b[1], b[2], b[3]]))
    }

    fn f32(&mut self) -> Result<f32, String> {
        let b = self.take(4)?;
        Ok(f32::from_le_bytes([b[0], b[1], b[2], b[3]]))
    }

    fn u64(&mut self) -> Result<u64, String> {
        let b = self.take(8)?;
        Ok(u64::from_le_bytes([
            b[0], b[1], b[2], b[3], b[4], b[5], b[6], b[7],
        ]))
    }

    /// Reads a NUL-terminated UTF-8 string, returning its raw bytes (no allocation).
    fn zstring_bytes(&mut self) -> Result<&'a [u8], String> {
        let start = self.pos;
        let rel = self.buf[start..]
            .iter()
            .position(|&b| b == 0)
            .ok_or_else(|| "null terminator not found before end of stream".to_string())?;
        if rel > MAX_STRING {
            return Err("string exceeds maximum buffer size. File may be corrupted.".into());
        }
        self.pos = start + rel + 1;
        Ok(&self.buf[start..start + rel])
    }

    /// Advances past a NUL-terminated UTF-16LE string without decoding it.
    fn skip_wstring(&mut self) -> Result<(), String> {
        let start = self.pos;
        let mut i = 0usize;
        while start + i + 1 < self.buf.len() {
            if self.buf[start + i] == 0 && self.buf[start + i + 1] == 0 {
                self.pos = start + i + 2;
                return Ok(());
            }
            i += 2;
            if i > MAX_STRING {
                return Err(
                    "wide string exceeds maximum buffer size. File may be corrupted.".into(),
                );
            }
        }
        Err("null terminator not found".into())
    }

    /// Reads a NUL-terminated UTF-16LE string and appends its length-prefixed UTF-8
    /// form to `out` (no intermediate allocation).
    fn wstring_lp_into(&mut self, out: &mut Vec<u8>) -> Result<(), String> {
        let start = self.pos;
        let mut end: Option<usize> = None;
        let mut i = 0usize;
        while start + i + 1 < self.buf.len() {
            if self.buf[start + i] == 0 && self.buf[start + i + 1] == 0 {
                end = Some(i);
                break;
            }
            i += 2;
            if i > MAX_STRING {
                return Err(
                    "wide string exceeds maximum buffer size. File may be corrupted.".into(),
                );
            }
        }
        let end = end.ok_or_else(|| "null terminator not found".to_string())?;
        let src = &self.buf[start..start + end];
        self.pos = start + end + 2;
        out.push(flat::STR_INLINE);
        put_utf16_as_utf8_lp(out, src);
        Ok(())
    }

    /// Reads a length-prefixed flat string, returning its raw bytes (no allocation).
    fn fstring_bytes(&mut self) -> Result<&'a [u8], String> {
        let n = self.u32()? as usize;
        if n > MAX_STRING {
            return Err("flat string exceeds maximum buffer size.".into());
        }
        self.take(n)
    }

    /// Reads a length-prefixed flat string as an owned `String` (only used for interning).
    fn fstring(&mut self) -> Result<String, String> {
        Ok(String::from_utf8_lossy(self.fstring_bytes()?).into_owned())
    }

    /// Reads a `StrRef` (inline bytes or string-table index).
    fn strref(&mut self) -> Result<StrRef<'a>, String> {
        let mode = self.u8()?;
        match mode {
            flat::STR_INLINE => {
                let n = self.u32()? as usize;
                if n > MAX_STRING {
                    return Err("flat string exceeds maximum buffer size.".into());
                }
                Ok(StrRef::Inline(self.take(n)?))
            }
            flat::STR_INDEX => Ok(StrRef::Index(self.u32()?)),
            other => Err(format!("Unknown flat string mode {other}")),
        }
    }
}

fn put_len_bytes(out: &mut Vec<u8>, b: &[u8]) {
    out.extend_from_slice(&(b.len() as u32).to_le_bytes());
    out.extend_from_slice(b);
}

fn put_strref_inline(out: &mut Vec<u8>, b: &[u8]) {
    out.push(flat::STR_INLINE);
    put_len_bytes(out, b);
}

fn put_strref_index(out: &mut Vec<u8>, idx: u32) {
    out.push(flat::STR_INDEX);
    out.extend_from_slice(&idx.to_le_bytes());
}

fn put_lp_string(out: &mut Vec<u8>, s: &str) {
    put_len_bytes(out, s.as_bytes());
}

fn put_zstring_bytes(out: &mut Vec<u8>, b: &[u8]) {
    out.extend_from_slice(b);
    out.push(0);
}

/// Appends `src` (raw UTF-16LE, no terminator) as length-prefixed UTF-8, matching
/// .NET's `String::from_utf16_lossy` semantics for unpaired surrogates.
fn put_utf16_as_utf8_lp(out: &mut Vec<u8>, src: &[u8]) {
    let len_pos = out.len();
    out.extend_from_slice(&[0, 0, 0, 0]);
    let start = out.len();

    let mut i = 0usize;
    while i + 1 < src.len() {
        let u = u16::from_le_bytes([src[i], src[i + 1]]);
        i += 2;
        if u == 0 {
            break;
        }
        let mut buf = [0u8; 4];
        let encoded: Option<&str> = if (0xD800..=0xDBFF).contains(&u) {
            if i + 1 < src.len() {
                let lo = u16::from_le_bytes([src[i], src[i + 1]]);
                if (0xDC00..=0xDFFF).contains(&lo) {
                    i += 2;
                    let cp = 0x10000 + (((u as u32) - 0xD800) << 10) + ((lo as u32) - 0xDC00);
                    char::from_u32(cp).map(|c| c.encode_utf8(&mut buf) as &str)
                } else {
                    None
                }
            } else {
                None
            }
        } else if (0xDC00..=0xDFFF).contains(&u) {
            None
        } else {
            char::from_u32(u as u32).map(|c| c.encode_utf8(&mut buf) as &str)
        };
        out.extend_from_slice(encoded.unwrap_or("\u{FFFD}").as_bytes());
    }

    let n = out.len() - start;
    out[len_pos..len_pos + 4].copy_from_slice(&(n as u32).to_le_bytes());
}

fn put_wstring_utf8_bytes(out: &mut Vec<u8>, bytes: &[u8]) {
    let s = String::from_utf8_lossy(bytes);
    for u in s.encode_utf16() {
        out.extend_from_slice(&u.to_le_bytes());
    }
    out.extend_from_slice(&[0, 0]);
}

// ── string table (0x07564429) ───────────────────────────────────────────────

/// Opaque string-table handle shared with the managed side.
pub struct StringTable {
    list: Vec<String>,
    map: HashMap<String, u32>,
}

impl StringTable {
    fn new() -> Self {
        StringTable {
            list: Vec::new(),
            map: HashMap::new(),
        }
    }

    fn intern(&mut self, s: &str) -> u32 {
        if let Some(&i) = self.map.get(s) {
            return i;
        }
        let i = self.list.len() as u32;
        let owned = s.to_owned();
        self.list.push(owned.clone());
        self.map.insert(owned, i);
        i
    }

    /// Parses a `[u32 count][(u32 len)(utf8)]*` dump produced by the managed side or
    /// by [`ste_strtab_dump`].
    fn from_dump(buf: &[u8]) -> Result<Self, String> {
        let mut r = Reader::new(buf);
        let count = r.u32()? as usize;
        if count > 1_000_000 {
            return Err("string table too large".into());
        }
        let mut list = Vec::with_capacity(count);
        let mut map = HashMap::with_capacity(count);
        for i in 0..count {
            let s = r.fstring()?;
            map.insert(s.clone(), i as u32);
            list.push(s);
        }
        Ok(StringTable { list, map })
    }

    fn dump(&self) -> Vec<u8> {
        let mut out = Vec::with_capacity(self.list.len() * 8 + 4);
        out.extend_from_slice(&(self.list.len() as u32).to_le_bytes());
        for s in &self.list {
            put_lp_string(&mut out, s);
        }
        out
    }
}

// ── hot-path selection (sparse decode) ──────────────────────────────────────

/// A prefix trie of key paths. A terminal node means "include this whole subtree".
pub struct PathSet {
    root: TrieNode,
}

struct TrieNode {
    terminal: bool,
    children: HashMap<Vec<u8>, TrieNode>,
}

impl TrieNode {
    fn new() -> Self {
        TrieNode {
            terminal: false,
            children: HashMap::new(),
        }
    }
}

impl PathSet {
    /// Parses `[u32 pathCount][ [u32 segCount][ (u32 len)(utf8 bytes) ]* ]*`.
    fn parse(buf: &[u8]) -> Result<Self, String> {
        let mut r = Reader::new(buf);
        let count = r.u32()? as usize;
        if count > 10_000 {
            return Err("too many hot paths".into());
        }
        let mut root = TrieNode::new();
        for _ in 0..count {
            let segments = r.u32()? as usize;
            if segments == 0 || segments > MAX_DEPTH {
                return Err("invalid hot path segment count".into());
            }
            let mut node = &mut root;
            for _ in 0..segments {
                let len = r.u32()? as usize;
                if len > MAX_STRING {
                    return Err("hot path segment too long".into());
                }
                let key = r.take(len)?.to_vec();
                node = node.children.entry(key).or_insert_with(TrieNode::new);
            }
            node.terminal = true;
        }
        Ok(PathSet { root })
    }
}

// ── decode: on-disk VDF -> flat ─────────────────────────────────────────────

fn out_of_range(idx: u32, st: &[String]) -> String {
    format!(
        "string table index {idx} out of range (table size={})",
        st.len()
    )
}

/// Emits a property name as a `StrRef` (index for the string-table format).
fn emit_name(
    r: &mut Reader,
    out: &mut Vec<u8>,
    format29: bool,
    st: &[String],
) -> Result<(), String> {
    if format29 {
        let idx = r.u32()?;
        if st.get(idx as usize).is_none() {
            return Err(out_of_range(idx, st));
        }
        put_strref_index(out, idx);
    } else {
        let name = r.zstring_bytes()?;
        put_strref_inline(out, name);
    }
    Ok(())
}

/// Emits the value of a property of `kind` (a table recurses into `decode_table`).
fn emit_value(
    r: &mut Reader,
    out: &mut Vec<u8>,
    kind: u8,
    format29: bool,
    st: &[String],
    depth: usize,
) -> Result<(), String> {
    match kind {
        flat::TABLE => decode_table(r, out, depth + 1, format29, st),
        flat::STRING => {
            let v = r.zstring_bytes()?;
            put_strref_inline(out, v);
            Ok(())
        }
        flat::WSTRING => {
            if format29 {
                let idx = r.u32()?;
                if st.get(idx as usize).is_none() {
                    return Err(out_of_range(idx, st));
                }
                put_strref_index(out, idx);
            } else {
                r.wstring_lp_into(out)?;
            }
            Ok(())
        }
        flat::INT32 => {
            out.extend_from_slice(&r.i32()?.to_le_bytes());
            Ok(())
        }
        flat::FLOAT => {
            out.extend_from_slice(&r.f32()?.to_le_bytes());
            Ok(())
        }
        flat::UINT64 => {
            out.extend_from_slice(&r.u64()?.to_le_bytes());
            Ok(())
        }
        flat::COLOR => {
            out.extend_from_slice(r.take(3)?);
            Ok(())
        }
        other => Err(format!(
            "Unknown property type {other} at position {}. File may be corrupted.",
            r.pos
        )),
    }
}

/// Full decode of one table: emits each property (name + value) and a trailing `END`.
fn decode_table(
    r: &mut Reader,
    out: &mut Vec<u8>,
    depth: usize,
    format29: bool,
    st: &[String],
) -> Result<(), String> {
    if depth > MAX_DEPTH {
        return Err(format!("nesting depth {depth} exceeds maximum {MAX_DEPTH}"));
    }
    loop {
        let kind = r.u8()?;
        if kind == flat::END {
            out.push(flat::END);
            return Ok(());
        }
        out.push(kind);
        emit_name(r, out, format29, st)?;
        emit_value(r, out, kind, format29, st, depth)?;
    }
}

fn skip_name(r: &mut Reader, format29: bool) -> Result<(), String> {
    if format29 {
        r.u32()?;
    } else {
        r.zstring_bytes()?;
    }
    Ok(())
}

/// Consumes a property value without emitting anything.
fn skip_value(r: &mut Reader, kind: u8, format29: bool) -> Result<(), String> {
    match kind {
        flat::TABLE => loop {
            let k = r.u8()?;
            if k == flat::END {
                return Ok(());
            }
            skip_name(r, format29)?;
            skip_value(r, k, format29)?;
        },
        flat::STRING => {
            r.zstring_bytes()?;
            Ok(())
        }
        flat::WSTRING => {
            if format29 {
                r.u32()?;
            } else {
                r.skip_wstring()?;
            }
            Ok(())
        }
        flat::INT32 | flat::FLOAT => {
            r.take(4)?;
            Ok(())
        }
        flat::UINT64 => {
            r.take(8)?;
            Ok(())
        }
        flat::COLOR => {
            r.take(3)?;
            Ok(())
        }
        other => Err(format!(
            "Unknown property type {other} at position {}. File may be corrupted.",
            r.pos
        )),
    }
}

/// Sparse decode: emits only properties selected by `node`; anything else is skipped.
fn decode_table_sparse(
    r: &mut Reader,
    out: &mut Vec<u8>,
    depth: usize,
    format29: bool,
    st: &[String],
    node: &TrieNode,
) -> Result<(), String> {
    if depth > MAX_DEPTH {
        return Err(format!("nesting depth {depth} exceeds maximum {MAX_DEPTH}"));
    }
    loop {
        let kind = r.u8()?;
        if kind == flat::END {
            out.push(flat::END);
            return Ok(());
        }
        if format29 {
            let idx = r.u32()?;
            let name = match st.get(idx as usize) {
                Some(n) => n,
                None => return Err(out_of_range(idx, st)),
            };
            match node.children.get(name.as_bytes()) {
                Some(child) => {
                    out.push(kind);
                    put_strref_index(out, idx);
                    if child.terminal || kind != flat::TABLE {
                        emit_value(r, out, kind, format29, st, depth)?;
                    } else {
                        decode_table_sparse(r, out, depth + 1, format29, st, child)?;
                    }
                }
                None => skip_value(r, kind, format29)?,
            }
        } else {
            let name = r.zstring_bytes()?;
            match node.children.get(name) {
                Some(child) => {
                    out.push(kind);
                    put_strref_inline(out, name);
                    if child.terminal || kind != flat::TABLE {
                        emit_value(r, out, kind, format29, st, depth)?;
                    } else {
                        decode_table_sparse(r, out, depth + 1, format29, st, child)?;
                    }
                }
                None => skip_value(r, kind, format29)?,
            }
        }
    }
}

// ── encode: flat -> on-disk VDF ─────────────────────────────────────────────

fn encode_table(
    r: &mut Reader,
    out: &mut Vec<u8>,
    depth: usize,
    format29: bool,
    st: &mut StringTable,
) -> Result<(), String> {
    if depth > MAX_DEPTH {
        return Err(format!("nesting depth {depth} exceeds maximum {MAX_DEPTH}"));
    }
    loop {
        let kind = r.u8()?;
        if kind == flat::END {
            out.push(flat::END);
            return Ok(());
        }
        out.push(kind);
        let name_inline: &[u8] = match r.strref()? {
            StrRef::Inline(b) => b,
            StrRef::Index(_) => {
                return Err("flat string index refs are not supported while encoding".into())
            }
        };
        if format29 {
            let s = String::from_utf8_lossy(name_inline);
            let idx = st.intern(&s);
            out.extend_from_slice(&idx.to_le_bytes());
        } else {
            put_zstring_bytes(out, name_inline);
        }
        match kind {
            flat::TABLE => encode_table(r, out, depth + 1, format29, st)?,
            flat::STRING => {
                let v = match r.strref()? {
                    StrRef::Inline(b) => b,
                    StrRef::Index(_) => {
                        return Err("flat string index refs are not supported while encoding".into())
                    }
                };
                put_zstring_bytes(out, v);
            }
            flat::WSTRING => {
                let v = match r.strref()? {
                    StrRef::Inline(b) => b,
                    StrRef::Index(_) => {
                        return Err("flat string index refs are not supported while encoding".into())
                    }
                };
                if format29 {
                    let s = String::from_utf8_lossy(v);
                    let idx = st.intern(&s);
                    out.extend_from_slice(&idx.to_le_bytes());
                } else {
                    put_wstring_utf8_bytes(out, v);
                }
            }
            flat::INT32 => out.extend_from_slice(&r.i32()?.to_le_bytes()),
            flat::FLOAT => out.extend_from_slice(&r.f32()?.to_le_bytes()),
            flat::UINT64 => out.extend_from_slice(&r.u64()?.to_le_bytes()),
            flat::COLOR => out.extend_from_slice(r.take(3)?),
            other => return Err(format!("Unknown property type {other} in flat stream")),
        }
    }
}

// ── error reporting ─────────────────────────────────────────────────────────

static LAST_ERROR: Mutex<Option<CString>> = Mutex::new(None);

fn set_err(msg: &str) {
    let c = CString::new(msg).unwrap_or_else(|_| CString::new("error").unwrap());
    if let Ok(mut g) = LAST_ERROR.lock() {
        *g = Some(c);
    }
}

thread_local! {
    static LAST_ERROR_BUF: std::cell::RefCell<CString> =
        std::cell::RefCell::new(CString::new("").unwrap());
}

/// Returns the last error message as a NUL-terminated UTF-8 string, or an empty
/// string if none. The pointer stays valid until the next call on this thread.
#[no_mangle]
pub extern "C" fn ste_last_error() -> *const c_char {
    let msg = LAST_ERROR
        .lock()
        .ok()
        .and_then(|g| g.clone())
        .unwrap_or_else(|| CString::new("").unwrap());
    LAST_ERROR_BUF.with(|b| {
        *b.borrow_mut() = msg;
        b.borrow().as_ptr()
    })
}

// ── buffer ownership helpers ────────────────────────────────────────────────

/// Runs `f`, converting its result into a heap buffer whose length is written to
/// `out_len`. The caller must release the returned pointer with [`ste_free`]. On
/// error (or panic) returns null, writes 0 to `out_len`/`out_used`, and records a
/// message retrievable via [`ste_last_error`].
unsafe fn run(
    f: impl FnOnce() -> Result<(Vec<u8>, usize), String>,
    out_len: *mut usize,
    out_used: *mut usize,
) -> *mut u8 {
    let res = catch_unwind(AssertUnwindSafe(f))
        .unwrap_or_else(|_| Err("native panic crossed the FFI boundary".to_string()));
    match res {
        Ok((v, used)) => {
            if !out_len.is_null() {
                *out_len = v.len();
            }
            if !out_used.is_null() {
                *out_used = used;
            }
            let mut boxed = v.into_boxed_slice();
            let ptr = boxed.as_mut_ptr();
            std::mem::forget(boxed);
            ptr
        }
        Err(e) => {
            set_err(&e);
            if !out_len.is_null() {
                *out_len = 0;
            }
            if !out_used.is_null() {
                *out_used = 0;
            }
            std::ptr::null_mut()
        }
    }
}

/// Frees a buffer previously returned by this library.
///
/// # Safety
/// `ptr`/`len` must come from one of this library's `ste_*` functions.
#[no_mangle]
pub unsafe extern "C" fn ste_free(ptr: *mut u8, len: usize) {
    if ptr.is_null() {
        return;
    }
    drop(Vec::from_raw_parts(ptr, len, len));
}

unsafe fn slice<'a>(ptr: *const u8, len: usize) -> &'a [u8] {
    if ptr.is_null() || len == 0 {
        &[]
    } else {
        std::slice::from_raw_parts(ptr, len)
    }
}

// ── public C ABI ────────────────────────────────────────────────────────────

/// Decodes an entire inline (`0x00000000`) property table into a flat stream.
/// `out_used` receives the number of source bytes the table occupied.
///
/// # Safety
/// `src`/`src_len` must describe a readable buffer.
#[no_mangle]
pub unsafe extern "C" fn ste_vdf_to_flat(
    src: *const u8,
    src_len: usize,
    out_len: *mut usize,
    out_used: *mut usize,
) -> *mut u8 {
    let bytes = slice(src, src_len);
    run(
        || {
            let mut r = Reader::new(bytes);
            let mut out = Vec::with_capacity(bytes.len());
            decode_table(&mut r, &mut out, 0, false, &[])?;
            Ok((out, r.pos))
        },
        out_len,
        out_used,
    )
}

/// Decodes an entire string-table (`0x07564429`) property table into a flat stream.
///
/// # Safety
/// `src`/`src_len` must describe a readable buffer and `strtab` must be a valid handle.
#[no_mangle]
pub unsafe extern "C" fn ste_vdf_to_flat_07564429(
    src: *const u8,
    src_len: usize,
    strtab: *mut StringTable,
    out_len: *mut usize,
    out_used: *mut usize,
) -> *mut u8 {
    let bytes = slice(src, src_len);
    run(
        || {
            if strtab.is_null() {
                return Err("string table handle is null".into());
            }
            let st = &*strtab;
            let mut r = Reader::new(bytes);
            let mut out = Vec::with_capacity(bytes.len());
            decode_table(&mut r, &mut out, 0, true, &st.list)?;
            Ok((out, r.pos))
        },
        out_len,
        out_used,
    )
}

/// Sparse decode: emits only the properties selected by `pathset` (a handle from
/// [`ste_pathset_new`]). A null `pathset` behaves like [`ste_vdf_to_flat`].
///
/// # Safety
/// Pointers must describe readable buffers / valid handles.
#[no_mangle]
pub unsafe extern "C" fn ste_vdf_to_flat_sparse(
    src: *const u8,
    src_len: usize,
    format29: u32,
    strtab: *mut StringTable,
    pathset: *const PathSet,
    out_len: *mut usize,
    out_used: *mut usize,
) -> *mut u8 {
    let bytes = slice(src, src_len);
    run(
        || {
            let format29 = format29 != 0;
            let mut r = Reader::new(bytes);
            let mut out = Vec::with_capacity(bytes.len());
            if format29 {
                if strtab.is_null() {
                    return Err("string table handle is null".into());
                }
                let st = &*strtab;
                if pathset.is_null() {
                    decode_table(&mut r, &mut out, 0, true, &st.list)?;
                } else {
                    decode_table_sparse(&mut r, &mut out, 0, true, &st.list, &(*pathset).root)?;
                }
            } else if pathset.is_null() {
                decode_table(&mut r, &mut out, 0, false, &[])?;
            } else {
                decode_table_sparse(&mut r, &mut out, 0, false, &[], &(*pathset).root)?;
            }
            Ok((out, r.pos))
        },
        out_len,
        out_used,
    )
}

/// Encodes a flat stream back into an inline (`0x00000000`) property table.
///
/// # Safety
/// `flat`/`flat_len` must describe a readable buffer.
#[no_mangle]
pub unsafe extern "C" fn ste_flat_to_vdf(
    flat: *const u8,
    flat_len: usize,
    out_len: *mut usize,
) -> *mut u8 {
    let bytes = slice(flat, flat_len);
    run(
        || {
            let mut r = Reader::new(bytes);
            let mut out = Vec::with_capacity(bytes.len());
            let mut st = StringTable::new();
            encode_table(&mut r, &mut out, 0, false, &mut st)?;
            Ok((out, 0))
        },
        out_len,
        std::ptr::null_mut(),
    )
}

/// Encodes a flat stream into a string-table (`0x07564429`) property table, adding
/// any new names/wide-strings to `strtab`.
///
/// # Safety
/// `flat`/`flat_len` must describe a readable buffer and `strtab` must be a valid handle.
#[no_mangle]
pub unsafe extern "C" fn ste_flat_to_vdf_07564429(
    flat: *const u8,
    flat_len: usize,
    strtab: *mut StringTable,
    out_len: *mut usize,
) -> *mut u8 {
    let bytes = slice(flat, flat_len);
    run(
        || {
            if strtab.is_null() {
                return Err("string table handle is null".into());
            }
            let st = &mut *strtab;
            let mut r = Reader::new(bytes);
            let mut out = Vec::with_capacity(bytes.len());
            encode_table(&mut r, &mut out, 0, true, st)?;
            Ok((out, 0))
        },
        out_len,
        std::ptr::null_mut(),
    )
}

/// Creates an empty string table handle.
#[no_mangle]
pub extern "C" fn ste_strtab_new() -> *mut StringTable {
    Box::into_raw(Box::new(StringTable::new()))
}

/// Builds a string table handle from a `[u32 count][(u32 len)(utf8)]*` dump.
/// Returns null on failure (see [`ste_last_error`]).
///
/// # Safety
/// `dump`/`dump_len` must describe a readable buffer.
#[no_mangle]
pub unsafe extern "C" fn ste_strtab_parse(dump: *const u8, dump_len: usize) -> *mut StringTable {
    let bytes = slice(dump, dump_len);
    match catch_unwind(AssertUnwindSafe(|| StringTable::from_dump(bytes))) {
        Ok(Ok(t)) => Box::into_raw(Box::new(t)),
        Ok(Err(e)) => {
            set_err(&e);
            std::ptr::null_mut()
        }
        Err(_) => {
            set_err("native panic crossed the FFI boundary");
            std::ptr::null_mut()
        }
    }
}

/// Frees a string table handle.
///
/// # Safety
/// `st` must be a handle from [`ste_strtab_new`]/[`ste_strtab_parse`] not yet freed.
#[no_mangle]
pub unsafe extern "C" fn ste_strtab_free(st: *mut StringTable) {
    if !st.is_null() {
        drop(Box::from_raw(st));
    }
}

/// Serializes a string table to a `[u32 count][(u32 len)(utf8)]*` dump buffer.
///
/// # Safety
/// `st` must be a valid handle.
#[no_mangle]
pub unsafe extern "C" fn ste_strtab_dump(st: *mut StringTable, out_len: *mut usize) -> *mut u8 {
    run(
        || {
            if st.is_null() {
                return Err("string table handle is null".into());
            }
            Ok(((&*st).dump(), 0))
        },
        out_len,
        std::ptr::null_mut(),
    )
}

/// Builds a hot-path set from `[u32 pathCount][ [u32 segCount][ (u32 len)(utf8) ]* ]*`.
/// Returns null on failure (see [`ste_last_error`]).
///
/// # Safety
/// `paths`/`len` must describe a readable buffer.
#[no_mangle]
pub unsafe extern "C" fn ste_pathset_new(paths: *const u8, len: usize) -> *mut PathSet {
    let bytes = slice(paths, len);
    match catch_unwind(AssertUnwindSafe(|| PathSet::parse(bytes))) {
        Ok(Ok(p)) => Box::into_raw(Box::new(p)),
        Ok(Err(e)) => {
            set_err(&e);
            std::ptr::null_mut()
        }
        Err(_) => {
            set_err("native panic crossed the FFI boundary");
            std::ptr::null_mut()
        }
    }
}

/// Frees a hot-path set handle.
///
/// # Safety
/// `p` must be a handle from [`ste_pathset_new`] not yet freed.
#[no_mangle]
pub unsafe extern "C" fn ste_pathset_free(p: *mut PathSet) {
    if !p.is_null() {
        drop(Box::from_raw(p));
    }
}
