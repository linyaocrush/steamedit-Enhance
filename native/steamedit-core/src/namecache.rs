//! Display cache for official localized names.
//!
//! On-disk shape: `{ "schinese": { "730": "反恐精英：全球攻势" }, ... }`. Purely a
//! display-layer cache — it never touches `appinfo.vdf`. Loading is lazy and saving
//! is atomic (temp file + rename).

use std::collections::HashMap;
use std::fs;
use std::path::Path;

/// Defensive cap: refuse to persist absurdly large caches.
const MAX_SAVED_COUNT: usize = 200_000;

type Store = HashMap<String, HashMap<i32, String>>;

pub struct NameCache {
    path: String,
    loaded: bool,
    dirty: bool,
    by_locale: Store,
}

impl NameCache {
    pub fn new(path: &str) -> Self {
        NameCache {
            path: path.to_string(),
            loaded: false,
            dirty: false,
            by_locale: Store::new(),
        }
    }

    fn ensure_loaded(&mut self) {
        if self.loaded {
            return;
        }
        self.loaded = true;
        if let Ok(text) = fs::read_to_string(&self.path) {
            if let Ok(map) = serde_json::from_str::<Store>(&text) {
                self.by_locale = map;
            }
        }
    }

    pub fn get(&mut self, app_id: i32, locale: &str) -> Option<String> {
        if app_id <= 0 || locale.is_empty() {
            return None;
        }
        self.ensure_loaded();
        self.by_locale
            .get(locale)
            .and_then(|m| m.get(&app_id))
            .cloned()
    }

    pub fn contains(&mut self, app_id: i32, locale: &str) -> bool {
        self.get(app_id, locale).is_some()
    }

    pub fn set(&mut self, app_id: i32, locale: &str, name: &str) -> bool {
        if app_id <= 0 || locale.is_empty() {
            return false;
        }
        let name = name.trim();
        if name.is_empty() {
            return false;
        }
        self.ensure_loaded();
        let map = self.by_locale.entry(locale.to_string()).or_default();
        if map.get(&app_id).map(|s| s == name).unwrap_or(false) {
            return false;
        }
        map.insert(app_id, name.to_string());
        self.dirty = true;
        true
    }

    pub fn clear(&mut self) {
        self.by_locale.clear();
        self.loaded = true;
        self.dirty = true;
    }

    pub fn total_count(&mut self) -> usize {
        self.ensure_loaded();
        self.by_locale.values().map(|m| m.len()).sum()
    }

    pub fn save(&mut self) {
        if !self.dirty {
            return;
        }
        self.dirty = false;
        if self.total_count() > MAX_SAVED_COUNT {
            return;
        }
        if let Some(dir) = Path::new(&self.path).parent() {
            if !dir.as_os_str().is_empty() {
                let _ = fs::create_dir_all(dir);
            }
        }
        let json = match serde_json::to_string(&self.by_locale) {
            Ok(j) => j,
            Err(_) => return,
        };
        let tmp = format!("{}.tmp", self.path);
        if fs::write(&tmp, json).is_ok() {
            let _ = fs::rename(&tmp, &self.path);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn temp_path(tag: &str) -> std::path::PathBuf {
        std::env::temp_dir().join(format!(
            "ste-namecache-{tag}-{}-{}.json",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ))
    }

    #[test]
    fn set_get_and_roundtrip() {
        let path = temp_path("rt");
        let path_s = path.to_string_lossy().to_string();
        {
            let mut c = NameCache::new(&path_s);
            assert!(c.set(730, "schinese", " 反恐精英 "));
            assert!(!c.set(730, "schinese", "反恐精英")); // unchanged
            assert_eq!(c.get(730, "schinese").as_deref(), Some("反恐精英"));
            assert_eq!(c.get(730, "english"), None);
            assert_eq!(c.total_count(), 1);
            c.save();
        }
        {
            let mut c = NameCache::new(&path_s);
            assert_eq!(c.get(730, "schinese").as_deref(), Some("反恐精英"));
        }
        let _ = fs::remove_file(&path);
    }

    #[test]
    fn rejects_invalid_and_blank() {
        let mut c = NameCache::new(&temp_path("bad").to_string_lossy());
        assert!(!c.set(0, "schinese", "x"));
        assert!(!c.set(1, "", "x"));
        assert!(!c.set(1, "schinese", "   "));
        assert!(c.get(1, "schinese").is_none());
    }
}
