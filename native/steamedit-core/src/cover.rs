//! Steam cover resolution kernel.
//!
//! Finds a game's cover image inside the local Steam cache (path resolution only —
//! no decoding/downloading). Priority: user grid custom art (portrait uses the
//! `<id>p` naming), then the official library asset names, then a fuzzy scan of the
//! app's `librarycache` folder that picks the image whose aspect ratio best matches
//! the requested orientation (portrait 2:3, wide 920:430).
//!
//! Results — including "not found" — are cached per (app id, orientation).

use std::collections::HashMap;
use std::path::{Path, PathBuf};

const PORTRAIT_EXTENSIONS: [&str; 2] = [".jpg", ".png"];
const FUZZY_EXTENSIONS: [&str; 6] = ["jpg", "jpeg", "png", "webp", "bmp", "gif"];

const PORTRAIT_TARGET_ASPECT: f64 = 2.0 / 3.0;
const WIDE_TARGET_ASPECT: f64 = 920.0 / 430.0;

pub struct CoverResolver {
    steam_dir: String,
    portrait_cache: HashMap<i32, Option<String>>,
    wide_cache: HashMap<i32, Option<String>>,
    user_grid_dirs: Option<Vec<String>>,
}

impl CoverResolver {
    pub fn new(steam_dir: &str) -> Self {
        CoverResolver {
            steam_dir: steam_dir.to_string(),
            portrait_cache: HashMap::new(),
            wide_cache: HashMap::new(),
            user_grid_dirs: None,
        }
    }

    /// Portrait cover path (2:3), or `None` if unresolved.
    pub fn portrait_path(&mut self, app_id: i32) -> Option<String> {
        if app_id <= 0 || self.steam_dir.trim().is_empty() {
            return None;
        }
        if let Some(cached) = self.portrait_cache.get(&app_id) {
            return cached.clone();
        }
        let path = self.resolve_portrait(app_id);
        self.portrait_cache.insert(app_id, path.clone());
        path
    }

    /// Wide/banner fallback path, or `None` if unresolved.
    pub fn wide_path(&mut self, app_id: i32) -> Option<String> {
        if app_id <= 0 || self.steam_dir.trim().is_empty() {
            return None;
        }
        if let Some(cached) = self.wide_cache.get(&app_id) {
            return cached.clone();
        }
        let path = self.resolve_wide(app_id);
        self.wide_cache.insert(app_id, path.clone());
        path
    }

    fn resolve_portrait(&mut self, app_id: i32) -> Option<String> {
        let id = app_id.to_string();
        for grid in self.user_grid_dirs() {
            if let Some(p) = first_existing(&Path::new(&grid).join(format!("{id}p"))) {
                return Some(p);
            }
        }
        if let Some(p) = self.first_from_library_cache(&id, "library_600x900") {
            return Some(p);
        }
        self.pick_best_from_library_cache(&id, true)
    }

    fn resolve_wide(&mut self, app_id: i32) -> Option<String> {
        let id = app_id.to_string();
        for grid in self.user_grid_dirs() {
            if let Some(p) = first_existing(&Path::new(&grid).join(&id)) {
                return Some(p);
            }
        }
        if let Some(p) = self.first_from_library_cache(&id, "library_capsule") {
            return Some(p);
        }
        if let Some(p) = self.first_from_library_cache(&id, "library_hero") {
            return Some(p);
        }
        self.pick_best_from_library_cache(&id, false)
    }

    /// Newer `{id}\asset` layout first, then the older flat `{id}_asset` layout.
    fn first_from_library_cache(&self, id: &str, asset: &str) -> Option<String> {
        let library_cache = Path::new(&self.steam_dir).join("appcache").join("librarycache");
        if !library_cache.is_dir() {
            return None;
        }
        if let Some(p) = first_existing(&library_cache.join(id).join(asset)) {
            return Some(p);
        }
        first_existing(&library_cache.join(format!("{id}_{asset}")))
    }

    fn pick_best_from_library_cache(&self, id: &str, want_portrait: bool) -> Option<String> {
        let app_dir = Path::new(&self.steam_dir)
            .join("appcache")
            .join("librarycache")
            .join(id);
        let entries = std::fs::read_dir(&app_dir).ok()?;
        let target = if want_portrait {
            PORTRAIT_TARGET_ASPECT
        } else {
            WIDE_TARGET_ASPECT
        };

        let mut best: Option<String> = None;
        let mut best_score = f64::MAX;
        let mut best_area: i64 = 0;

        for entry in entries.flatten() {
            let path = entry.path();
            if !path.is_file() {
                continue;
            }
            let name = entry.file_name().to_string_lossy().into_owned();
            if !is_image_name(&name) {
                continue;
            }
            let (w, h) = match image_size(&path) {
                Some(d) => d,
                None => continue,
            };
            if w == 0 || h == 0 || w == h {
                continue;
            }
            let is_portrait = h > w;
            if is_portrait != want_portrait {
                continue;
            }
            let score = (w as f64 / h as f64 - target).abs();
            let area = w as i64 * h as i64;
            let better = score < best_score - 1e-9;
            let equal_bigger = (score - best_score).abs() <= 1e-9 && area > best_area;
            if better || equal_bigger {
                best_score = score;
                best_area = area;
                best = Some(path.to_string_lossy().into_owned());
            }
        }
        best
    }

    fn user_grid_dirs(&mut self) -> Vec<String> {
        if let Some(dirs) = &self.user_grid_dirs {
            return dirs.clone();
        }
        let mut dirs: Vec<String> = Vec::new();
        let userdata = Path::new(&self.steam_dir).join("userdata");
        if let Ok(entries) = std::fs::read_dir(&userdata) {
            for entry in entries.flatten() {
                let path = entry.path();
                if !path.is_dir() {
                    continue;
                }
                let name = entry.file_name().to_string_lossy().into_owned();
                if name.parse::<i64>().is_err() {
                    continue;
                }
                let grid = path.join("config").join("grid");
                if grid.is_dir() {
                    dirs.push(grid.to_string_lossy().into_owned());
                }
            }
        }
        dirs.sort();
        self.user_grid_dirs = Some(dirs.clone());
        dirs
    }
}

/// First existing `path` + extension from the cover extension list.
fn first_existing(path_without_extension: &Path) -> Option<String> {
    for ext in PORTRAIT_EXTENSIONS {
        let mut candidate = path_without_extension.as_os_str().to_os_string();
        candidate.push(ext);
        let candidate = PathBuf::from(candidate);
        if candidate.is_file() {
            return Some(candidate.to_string_lossy().into_owned());
        }
    }
    None
}

/// Accepts known image extensions or extensionless (hash-named) files.
fn is_image_name(name: &str) -> bool {
    match Path::new(name).extension() {
        None => true,
        Some(ext) => {
            let ext = ext.to_string_lossy().to_ascii_lowercase();
            FUZZY_EXTENSIONS.contains(&ext.as_str())
        }
    }
}

fn le16(b: &[u8], off: usize) -> Option<u32> {
    let a = *b.get(off)? as u32;
    let c = *b.get(off + 1)? as u32;
    Some(a | (c << 8))
}

fn le32(b: &[u8], off: usize) -> Option<u32> {
    let a = *b.get(off)? as u32;
    let c = *b.get(off + 1)? as u32;
    let d = *b.get(off + 2)? as u32;
    let e = *b.get(off + 3)? as u32;
    Some(a | (c << 8) | (d << 16) | (e << 24))
}

/// Reads only the file header to determine image dimensions. Supports PNG/JPEG/
/// GIF/BMP/WebP; returns `None` on any failure.
pub fn image_size(path: &Path) -> Option<(u32, u32)> {
    let data = std::fs::read(path).ok()?;
    if data.len() < 12 {
        return None;
    }
    png_size(&data)
        .or_else(|| jpeg_size(&data))
        .or_else(|| gif_size(&data))
        .or_else(|| bmp_size(&data))
        .or_else(|| webp_size(&data))
}

fn png_size(d: &[u8]) -> Option<(u32, u32)> {
    const SIG: [u8; 8] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    if d.len() < 24 || d[..8] != SIG || &d[12..16] != b"IHDR" {
        return None;
    }
    let w = u32::from_be_bytes([d[16], d[17], d[18], d[19]]);
    let h = u32::from_be_bytes([d[20], d[21], d[22], d[23]]);
    Some((w, h))
}

fn jpeg_size(d: &[u8]) -> Option<(u32, u32)> {
    if d.len() < 4 || d[0] != 0xFF || d[1] != 0xD8 {
        return None;
    }
    let mut pos = 2usize;
    loop {
        // Seek the next marker (skip fill bytes).
        while d.get(pos)? != &0xFF {
            pos += 1;
        }
        let mut marker = *d.get(pos + 1)?;
        pos += 2;
        while marker == 0xFF {
            marker = *d.get(pos)?;
            pos += 1;
        }
        if marker == 0xD9 || marker == 0xDA {
            return None; // EOI / SOS before any SOF
        }
        if marker == 0xD8 || marker == 0x01 || (0xD0..=0xD7).contains(&marker) {
            continue;
        }
        let hi = *d.get(pos)? as usize;
        let lo = *d.get(pos + 1)? as usize;
        pos += 2;
        let seg_len = (hi << 8) | lo;
        if seg_len < 2 {
            return None;
        }
        let is_sof = (0xC0..=0xC3).contains(&marker)
            || (0xC5..=0xC7).contains(&marker)
            || (0xC9..=0xCB).contains(&marker)
            || (0xCD..=0xCF).contains(&marker);
        if is_sof {
            let h = ((*d.get(pos + 1)? as u32) << 8) | (*d.get(pos + 2)? as u32);
            let w = ((*d.get(pos + 3)? as u32) << 8) | (*d.get(pos + 4)? as u32);
            return if w > 0 && h > 0 { Some((w, h)) } else { None };
        }
        pos = pos.checked_add(seg_len - 2)?;
        if pos > d.len() {
            return None;
        }
    }
}

fn gif_size(d: &[u8]) -> Option<(u32, u32)> {
    if d.len() < 10
        || d[0] != b'G'
        || d[1] != b'I'
        || d[2] != b'F'
        || d[3] != b'8'
        || (d[4] != b'7' && d[4] != b'9')
        || d[5] != b'a'
    {
        return None;
    }
    let w = le16(d, 6)?;
    let h = le16(d, 8)?;
    if w > 0 && h > 0 {
        Some((w, h))
    } else {
        None
    }
}

fn bmp_size(d: &[u8]) -> Option<(u32, u32)> {
    if d.len() < 26 || d[0] != b'B' || d[1] != b'M' {
        return None;
    }
    let w = le32(d, 18)?;
    let h_raw = le32(d, 22)? as i32;
    let h = h_raw.unsigned_abs();
    if w > 0 && h > 0 {
        Some((w, h))
    } else {
        None
    }
}

fn webp_size(d: &[u8]) -> Option<(u32, u32)> {
    if d.len() < 30
        || &d[0..4] != b"RIFF"
        || &d[8..12] != b"WEBP"
    {
        return None;
    }
    let fourcc = &d[12..16];
    match fourcc {
        b"VP8L" => {
            let sig = d.get(16..21)?;
            if sig[0] != 0x2F {
                return None;
            }
            let bits = sig[1] as u32
                | ((sig[2] as u32) << 8)
                | ((sig[3] as u32) << 16)
                | ((sig[4] as u32) << 24);
            let w = (bits & 0x3FFF) + 1;
            let h = ((bits >> 14) & 0x3FFF) + 1;
            Some((w, h))
        }
        b"VP8X" => {
            let w = le24(d, 20)? + 1;
            let h = le24(d, 23)? + 1;
            Some((w, h))
        }
        b"VP8 " => {
            let sync = d.get(19..22)?;
            if sync[0] != 0x9D || sync[1] != 0x01 || sync[2] != 0x2A {
                return None;
            }
            let dims = d.get(22..26)?;
            let w = (dims[0] as u32 | ((dims[1] as u32 & 0x3F) << 8)) + 1;
            let h = (dims[2] as u32 | ((dims[3] as u32 & 0x3F) << 8)) + 1;
            Some((w, h))
        }
        _ => None,
    }
}

fn le24(b: &[u8], off: usize) -> Option<u32> {
    let a = *b.get(off)? as u32;
    let c = *b.get(off + 1)? as u32;
    let d = *b.get(off + 2)? as u32;
    Some(a | (c << 8) | (d << 16))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicU32, Ordering};

    static COUNTER: AtomicU32 = AtomicU32::new(0);

    fn temp_root() -> PathBuf {
        let n = COUNTER.fetch_add(1, Ordering::SeqCst);
        let d = std::env::temp_dir().join(format!(
            "ste-cover-{}-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos(),
            n
        ));
        std::fs::create_dir_all(&d).unwrap();
        d
    }

    fn write_file(root: &Path, rel: &str, bytes: &[u8]) {
        let full = root.join(rel);
        std::fs::create_dir_all(full.parent().unwrap()).unwrap();
        std::fs::write(full, bytes).unwrap();
    }

    fn png_header(w: u32, h: u32) -> Vec<u8> {
        let mut b = vec![0u8; 24];
        b[..8].copy_from_slice(&[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        b[12..16].copy_from_slice(b"IHDR");
        b[16..20].copy_from_slice(&w.to_be_bytes());
        b[20..24].copy_from_slice(&h.to_be_bytes());
        b
    }

    fn abs(root: &Path, rel: &str) -> String {
        let mut p = root.to_path_buf();
        for part in rel.split('/') {
            p.push(part);
        }
        p.to_string_lossy().into_owned()
    }

    #[test]
    fn portrait_folder_and_flat_layouts() {
        let root = temp_root();
        write_file(&root, "appcache/librarycache/730/library_600x900.jpg", b"x");
        assert_eq!(
            CoverResolver::new(&root.to_string_lossy()).portrait_path(730),
            Some(abs(&root, "appcache/librarycache/730/library_600x900.jpg"))
        );

        let root2 = temp_root();
        write_file(&root2, "appcache/librarycache/730_library_600x900.png", b"x");
        assert_eq!(
            CoverResolver::new(&root2.to_string_lossy()).portrait_path(730),
            Some(abs(&root2, "appcache/librarycache/730_library_600x900.png"))
        );
        let _ = std::fs::remove_dir_all(&root);
        let _ = std::fs::remove_dir_all(&root2);
    }

    #[test]
    fn custom_grid_overrides_official() {
        let root = temp_root();
        write_file(&root, "appcache/librarycache/730/library_600x900.jpg", b"x");
        write_file(&root, "userdata/76561198000000000/config/grid/730p.png", b"x");
        assert_eq!(
            CoverResolver::new(&root.to_string_lossy()).portrait_path(730),
            Some(abs(&root, "userdata/76561198000000000/config/grid/730p.png"))
        );
        let _ = std::fs::remove_dir_all(&root);
    }

    #[test]
    fn only_wide_returns_null_and_negative_cache_holds() {
        let root = temp_root();
        write_file(&root, "appcache/librarycache/730/library_capsule.jpg", b"x");
        let root_s = root.to_string_lossy().to_string();
        let mut r = CoverResolver::new(&root_s);
        assert_eq!(r.portrait_path(730), None);
        // Adding a portrait after the miss does not invalidate the negative cache.
        write_file(&root, "appcache/librarycache/730/library_600x900.jpg", b"x");
        assert_eq!(r.portrait_path(730), None);
        let _ = std::fs::remove_dir_all(&root);
    }

    #[test]
    fn fuzzy_scan_picks_closest_portrait_aspect() {
        let root = temp_root();
        write_file(&root, "appcache/librarycache/730/aaaa01", &png_header(600, 900)); // 0.667
        write_file(&root, "appcache/librarycache/730/aaaa02", &png_header(960, 1280)); // 0.75
        write_file(&root, "appcache/librarycache/730/bbbb01", &png_header(920, 430)); // landscape
        write_file(&root, "appcache/librarycache/730/cccc01", &png_header(184, 184)); // square skip
        write_file(&root, "appcache/librarycache/730/fake01", b"not an image");
        assert_eq!(
            CoverResolver::new(&root.to_string_lossy()).portrait_path(730),
            Some(abs(&root, "appcache/librarycache/730/aaaa01"))
        );
        let _ = std::fs::remove_dir_all(&root);
    }

    #[test]
    fn fuzzy_scan_same_aspect_prefers_higher_resolution() {
        let root = temp_root();
        write_file(&root, "appcache/librarycache/730/aaaa01", &png_header(300, 450));
        write_file(&root, "appcache/librarycache/730/bbbb01", &png_header(600, 900));
        assert_eq!(
            CoverResolver::new(&root.to_string_lossy()).portrait_path(730),
            Some(abs(&root, "appcache/librarycache/730/bbbb01"))
        );
        let _ = std::fs::remove_dir_all(&root);
    }

    #[test]
    fn wide_prefers_capsule_aspect() {
        let root = temp_root();
        write_file(&root, "appcache/librarycache/730/aaaa01", &png_header(600, 900));
        write_file(&root, "appcache/librarycache/730/bbbb01", &png_header(3840, 1240));
        write_file(&root, "appcache/librarycache/730/cccc01", &png_header(920, 430));
        assert_eq!(
            CoverResolver::new(&root.to_string_lossy()).wide_path(730),
            Some(abs(&root, "appcache/librarycache/730/cccc01"))
        );
        let _ = std::fs::remove_dir_all(&root);
    }

    #[test]
    fn non_numeric_userdata_is_skipped_and_bad_ids_null() {
        let root = temp_root();
        write_file(&root, "userdata/notanumber/config/grid/730p.png", b"x");
        let mut r = CoverResolver::new(&root.to_string_lossy());
        assert_eq!(r.portrait_path(730), None);
        assert_eq!(r.portrait_path(0), None);
        assert_eq!(r.portrait_path(-5), None);
        let _ = std::fs::remove_dir_all(&root);
    }
}
