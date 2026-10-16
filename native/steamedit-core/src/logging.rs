//! File-logging kernel.
//!
//! Owns the log file handle, level filtering, line formatting and the "recent
//! lines" tail read. The wall-clock timestamp is produced by the caller (the
//! managed side already has the platform's local-time formatter) and passed in
//! per line, so this module needs no date/time dependency.

use std::fs::{File, OpenOptions};
use std::io::{BufRead, BufReader, BufWriter, Write};
use std::sync::Mutex;

const LEVEL_TAGS: [&str; 4] = ["DBG", "INF", "WRN", "ERR"];

struct LogState {
    writer: Option<BufWriter<File>>,
    path: String,
    enabled: [bool; 4],
}

static LOG: Mutex<Option<LogState>> = Mutex::new(None);

fn with_state<R>(f: impl FnOnce(&mut LogState) -> R) -> Option<R> {
    let mut guard = match LOG.lock() {
        Ok(g) => g,
        Err(p) => p.into_inner(),
    };
    guard.as_mut().map(f)
}

fn open(path: &str, append: bool) -> Option<BufWriter<File>> {
    OpenOptions::new()
        .create(true)
        .write(true)
        .append(append)
        .truncate(!append)
        .open(path)
        .ok()
        .map(BufWriter::new)
}

/// Opens (or reopens) `path` in append mode. Returns the previous path if a logger
/// was already active, otherwise the new path.
pub fn init(path: &str) -> Result<(), String> {
    let writer = open(path, true).ok_or_else(|| format!("cannot open log file: {path}"))?;
    let mut guard = LOG.lock().unwrap_or_else(|p| p.into_inner());
    *guard = Some(LogState {
        writer: Some(writer),
        path: path.to_string(),
        enabled: [false, true, true, true],
    });
    Ok(())
}

/// Reopens `path` with truncation and enables every level (test helper).
pub fn reset(path: &str) -> Result<(), String> {
    let writer = open(path, false).ok_or_else(|| format!("cannot open log file: {path}"))?;
    let mut guard = LOG.lock().unwrap_or_else(|p| p.into_inner());
    *guard = Some(LogState {
        writer: Some(writer),
        path: path.to_string(),
        enabled: [true, true, true, true],
    });
    Ok(())
}

pub fn configure(debug: bool, info: bool, warning: bool, error: bool) {
    with_state(|s| s.enabled = [debug, info, warning, error]);
}

/// Writes one `"{ts} [{TAG}] {line}"` record if the level is enabled. Returns
/// whether anything was written.
pub fn write(level: u32, ts: &str, line: &str) -> bool {
    if level > 3 {
        return false;
    }
    with_state(|s| {
        if !s.enabled[level as usize] {
            return false;
        }
        match s.writer.as_mut() {
            Some(w) => {
                let _ = write!(w, "{ts} [{}] {line}\r\n", LEVEL_TAGS[level as usize]);
                true
            }
            None => false,
        }
    })
    .unwrap_or(false)
}

pub fn flush() {
    with_state(|s| {
        if let Some(w) = s.writer.as_mut() {
            let _ = w.flush();
        }
    });
}

pub fn close() {
    let mut guard = LOG.lock().unwrap_or_else(|p| p.into_inner());
    if let Some(s) = guard.as_mut() {
        if let Some(mut w) = s.writer.take() {
            let _ = w.flush();
        }
    }
}

pub fn path() -> Option<String> {
    with_state(|s| s.path.clone())
}

/// Returns the last `count` lines of the log file ("\n"-joined).
pub fn recent(count: usize) -> String {
    flush();
    let path = match path() {
        Some(p) => p,
        None => return String::new(),
    };
    let file = match File::open(&path) {
        Ok(f) => f,
        Err(_) => return String::new(),
    };
    let mut tail: std::collections::VecDeque<String> = std::collections::VecDeque::with_capacity(count);
    for line in BufReader::new(file).lines().map_while(Result::ok) {
        if tail.len() == count {
            tail.pop_front();
        }
        tail.push_back(line);
    }
    tail.into_iter().collect::<Vec<_>>().join("\n")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn writes_and_reads_recent() {
        let dir = std::env::temp_dir().join(format!(
            "ste-log-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        std::fs::create_dir_all(&dir).unwrap();
        let path = dir.join("log.txt");
        let path_s = path.to_string_lossy().to_string();

        init(&path_s).unwrap();
        configure(true, true, true, true);
        assert!(write(1, "12:00:00.000", "hello"));
        assert!(write(0, "12:00:00.000", "debug line"));
        assert!(!write(9, "12:00:00.000", "bogus")); // invalid level
        configure(false, true, true, true);
        assert!(!write(0, "12:00:00.000", "suppressed"));

        let all = recent(10);
        assert!(all.contains("[INF] hello"));
        assert!(all.contains("[DBG] debug line"));

        let tail = recent(1);
        assert_eq!(tail, "12:00:00.000 [DBG] debug line");

        close();
        let _ = std::fs::remove_dir_all(&dir);
    }
}
