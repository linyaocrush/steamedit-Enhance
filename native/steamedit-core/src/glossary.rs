//! Translation glossary (AI training pairs), persisted as `glossary.json`.
//!
//! Layout: `{ "version": 1, "entries": { "00003039": { "original": "...",
//! "translated": "..." } } }`. Entries are keyed by the app id in upper-case hex.
//! An in-memory, case-insensitive index maps original → translated for lookup.
//! Saves are atomic (temp file + rename) with a `.bak` fallback on load.

use std::collections::{BTreeMap, HashMap};
use std::fs;
use std::path::Path;

use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Default)]
struct GlossaryEntry {
    #[serde(default)]
    original: String,
    #[serde(default)]
    translated: String,
}

fn default_version() -> i32 {
    1
}

#[derive(Serialize, Deserialize)]
struct GlossaryData {
    #[serde(default = "default_version")]
    version: i32,
    #[serde(default)]
    entries: BTreeMap<String, GlossaryEntry>,
}

impl Default for GlossaryData {
    fn default() -> Self {
        GlossaryData {
            version: 1,
            entries: BTreeMap::new(),
        }
    }
}

fn key_for(app_id: i32) -> String {
    format!("{:08X}", app_id as u32)
}

fn ci(s: &str) -> String {
    s.to_lowercase()
}

pub struct Glossary {
    path: String,
    initialized: bool,
    dirty: bool,
    data: GlossaryData,
    index: HashMap<String, String>,
}

impl Glossary {
    pub fn new() -> Self {
        Glossary {
            path: String::new(),
            initialized: false,
            dirty: false,
            data: GlossaryData::default(),
            index: HashMap::new(),
        }
    }

    pub fn initialize(&mut self, data_directory: &str) {
        if self.initialized {
            return;
        }
        self.path = Path::new(data_directory)
            .join("glossary.json")
            .to_string_lossy()
            .into_owned();
        self.load();
        self.initialized = true;
    }

    pub fn load(&mut self) {
        self.data = GlossaryData::default();
        if let Ok(text) = fs::read_to_string(&self.path) {
            match serde_json::from_str::<GlossaryData>(&text) {
                Ok(d) => self.data = d,
                Err(_) => {
                    let bak = format!("{}.bak", self.path);
                    if let Ok(btext) = fs::read_to_string(&bak) {
                        if let Ok(d) = serde_json::from_str::<GlossaryData>(&btext) {
                            self.data = d;
                        }
                    }
                }
            }
        }
        self.rebuild_index();
    }

    fn rebuild_index(&mut self) {
        self.index.clear();
        for entry in self.data.entries.values() {
            if !entry.original.trim().is_empty() {
                self.index.insert(ci(&entry.original), entry.translated.clone());
            }
        }
    }

    /// Whether an entry already exists for `app_id` (used by bulk sync).
    pub fn has_app(&self, app_id: i32) -> bool {
        self.data.entries.contains_key(&key_for(app_id))
    }

    pub fn update(&mut self, app_id: i32, original: &str, translated: &str) -> bool {
        if original.trim().is_empty() || translated.trim().is_empty() || original == translated {
            return false;
        }
        let key = key_for(app_id);
        self.data.entries.insert(
            key,
            GlossaryEntry {
                original: original.to_string(),
                translated: translated.to_string(),
            },
        );
        self.index.insert(ci(original), translated.to_string());
        self.dirty = true;
        true
    }

    pub fn entry_count(&self) -> usize {
        self.data.entries.len()
    }

    pub fn find_by_original(&self, original: &str) -> Option<String> {
        if original.trim().is_empty() {
            return None;
        }
        self.index.get(&ci(original)).cloned()
    }

    pub fn to_prompt_string(&self, max_entries: usize) -> Option<String> {
        if self.data.entries.is_empty() {
            return None;
        }
        let mut out = String::new();
        let mut count = 0usize;
        for entry in self.data.entries.values() {
            out.push_str(&format!("{} -> {}\n", entry.original, entry.translated));
            count += 1;
            if count >= max_entries {
                break;
            }
        }
        Some(out)
    }

    pub fn flush(&mut self) {
        if !self.dirty {
            return;
        }
        if self.save() {
            self.dirty = false;
        }
    }

    fn save(&self) -> bool {
        if self.path.is_empty() {
            return false;
        }
        let json = match serde_json::to_string_pretty(&self.data) {
            Ok(j) => j,
            Err(_) => return false,
        };
        let tmp = format!("{}.tmp", self.path);
        if fs::write(&tmp, json).is_err() {
            return false;
        }
        if Path::new(&self.path).exists() {
            let _ = fs::copy(&self.path, format!("{}.bak", self.path));
        }
        fs::rename(&tmp, &self.path).is_ok()
    }
}

impl Default for Glossary {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn temp_dir(tag: &str) -> std::path::PathBuf {
        let d = std::env::temp_dir().join(format!(
            "ste-glossary-{tag}-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        fs::create_dir_all(&d).unwrap();
        d
    }

    #[test]
    fn update_and_lookup_case_insensitive() {
        let dir = temp_dir("ci");
        let mut g = Glossary::new();
        g.initialize(&dir.to_string_lossy());
        assert!(g.update(1, "Portal", "传送门"));
        assert_eq!(g.find_by_original("portal").as_deref(), Some("传送门"));
        assert_eq!(g.find_by_original("PORTAL").as_deref(), Some("传送门"));
        assert!(g.find_by_original("missing").is_none());
        assert_eq!(g.entry_count(), 1);
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn ignores_blank_and_identical() {
        let dir = temp_dir("blank");
        let mut g = Glossary::new();
        g.initialize(&dir.to_string_lossy());
        assert!(!g.update(1, "", "x"));
        assert!(!g.update(1, "Game", "Game"));
        assert_eq!(g.entry_count(), 0);
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn persists_and_reloads() {
        let dir = temp_dir("persist");
        {
            let mut g = Glossary::new();
            g.initialize(&dir.to_string_lossy());
            g.update(0x3039, "The Witcher", "巫师");
            g.flush();
        }
        {
            let mut g = Glossary::new();
            g.initialize(&dir.to_string_lossy());
            assert_eq!(g.find_by_original("The Witcher").as_deref(), Some("巫师"));
            assert!(g.has_app(0x3039));
            let prompt = g.to_prompt_string(100).unwrap();
            assert!(prompt.contains("The Witcher -> 巫师"));
        }
        let _ = fs::remove_dir_all(&dir);
    }
}
