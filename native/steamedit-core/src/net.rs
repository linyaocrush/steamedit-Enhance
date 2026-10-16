//! Network kernel for Steamedit Enhance.
//!
//! Everything that talks to the outside world lives here and is reached from the
//! managed side through the C ABI in [`crate`]:
//!
//! * **cover download** — the portrait library cover from the Steam CDN, with
//!   multi-host fallback and atomic file replacement;
//! * **official names** — `store.steampowered.com/api/appdetails`, reduced to the
//!   localized `name` field;
//! * **AI chat completions** — OpenAI-compatible `/chat/completions` and `/models`,
//!   including retry/backoff and a per-process circuit breaker.
//!
//! The managed side performs no HTTP of its own: it only marshals arguments in and
//! results out.

use std::fs;
use std::io::Read;
use std::path::Path;
use std::sync::{Mutex, OnceLock};
use std::time::{Duration, Instant};

// ── shared HTTP plumbing ────────────────────────────────────────────────────

/// A response decoupled from the connection so it can outlive the `ureq` call.
struct Resp {
    status: u16,
    content_type: Option<String>,
    body: Vec<u8>,
}

impl Resp {
    fn body_text(&self) -> String {
        String::from_utf8_lossy(&self.body).into_owned()
    }

    fn is_json(&self) -> bool {
        match &self.content_type {
            Some(ct) => ct.to_ascii_lowercase().contains("json"),
            None => true,
        }
    }
}

static AGENT: OnceLock<ureq::Agent> = OnceLock::new();

/// Shared agent: keeps the connection pool and the DNS cache warm across calls.
/// Per-request deadlines are applied with [`ureq::Request::timeout`].
fn agent() -> &'static ureq::Agent {
    AGENT.get_or_init(|| {
        ureq::AgentBuilder::new()
            .timeout_connect(Duration::from_secs(15))
            .build()
    })
}

fn read_body(resp: ureq::Response) -> Result<Vec<u8>, String> {
    let mut buf = Vec::new();
    resp.into_reader()
        .read_to_end(&mut buf)
        .map_err(|e| format!("failed to read response body: {e}"))?;
    Ok(buf)
}

/// Normalizes a `ureq` call, treating non-2xx statuses as *responses* (not
/// transport errors) so callers can surface the status code and body like the
/// HTTP API does.
fn send(result: Result<ureq::Response, ureq::Error>) -> Result<Resp, String> {
    match result {
        Ok(resp) => {
            let content_type = resp.header("content-type").map(|s| s.to_string());
            let status = resp.status();
            Ok(Resp {
                status,
                content_type,
                body: read_body(resp)?,
            })
        }
        Err(ureq::Error::Status(code, resp)) => {
            let content_type = resp.header("content-type").map(|s| s.to_string());
            Ok(Resp {
                status: code,
                content_type,
                body: read_body(resp)?,
            })
        }
        Err(ureq::Error::Transport(t)) => Err(t.to_string()),
    }
}

// ── cover download ──────────────────────────────────────────────────────────

const COVER_HOSTS: [&str; 2] = [
    "https://cdn.cloudflare.steamstatic.com/steam/apps/{0}/library_600x900.jpg",
    "https://cdn.akamai.steamstatic.com/steam/apps/{0}/library_600x900.jpg",
];

const COVER_TIMEOUT: Duration = Duration::from_secs(10);

/// JPEG (`FF D8 FF`) or PNG (`89 50 4E 47 0D 0A 1A 0A`) magic bytes.
pub fn is_valid_image(data: &[u8]) -> bool {
    if data.len() < 8 {
        return false;
    }
    if data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF {
        return true;
    }
    data[0] == 0x89
        && data[1] == 0x50
        && data[2] == 0x4E
        && data[3] == 0x47
        && data[4] == 0x0D
        && data[5] == 0x0A
        && data[6] == 0x1A
        && data[7] == 0x0A
}

/// Returns the body of the first URL that yields a valid image, trying each URL
/// in order. Non-image bodies, non-200 statuses and transport errors all fall
/// through to the next candidate.
fn fetch_first_valid_image(urls: &[String], timeout: Duration) -> Option<Vec<u8>> {
    for url in urls {
        let req = agent()
            .get(url)
            .set("User-Agent", "steamedit-Enhance")
            .timeout(timeout);
        if let Ok(resp) = send(req.call()) {
            if resp.status == 200 && is_valid_image(&resp.body) {
                return Some(resp.body);
            }
        }
    }
    None
}

/// Downloads the portrait cover for `app_id` to `dest`, replacing it atomically.
/// Returns `true` on success; `false` for a bad app id, no valid image from any
/// host, or a filesystem failure (never leaves a partial file behind).
pub fn cover_download(app_id: i32, dest: &str) -> bool {
    if app_id <= 0 || dest.is_empty() {
        return false;
    }
    let id = app_id.to_string();
    let urls: Vec<String> = COVER_HOSTS.iter().map(|h| h.replace("{0}", &id)).collect();
    match fetch_first_valid_image(&urls, COVER_TIMEOUT) {
        Some(b) => write_atomic(dest, &b),
        None => false,
    }
}

/// Writes `bytes` to `dest` via a sibling `.tmp` file so a reader never observes
/// a half-written image.
fn write_atomic(dest: &str, bytes: &[u8]) -> bool {
    let path = Path::new(dest);
    let dir = match path.parent() {
        Some(d) if !d.as_os_str().is_empty() => d,
        _ => Path::new("."),
    };
    if fs::create_dir_all(dir).is_err() {
        return false;
    }
    let tmp = format!("{dest}.tmp");
    if fs::write(&tmp, bytes).is_err() {
        let _ = fs::remove_file(&tmp);
        return false;
    }
    match fs::rename(&tmp, dest) {
        Ok(()) => true,
        Err(_) => {
            let _ = fs::remove_file(&tmp);
            false
        }
    }
}

// ── official (store) names ──────────────────────────────────────────────────

/// Fetches the localized official name for `app_id` from the Steam store API.
/// Any failure (no store page, HTTP error, malformed JSON) yields `None`.
pub fn official_name(app_id: i32, locale: &str) -> Option<String> {
    if app_id <= 0 {
        return None;
    }
    let url = format!(
        "https://store.steampowered.com/api/appdetails?appids={app_id}&l={locale}"
    );
    let req = agent()
        .get(&url)
        .set("User-Agent", "steamedit-Enhance")
        .timeout(Duration::from_secs(20));
    let resp = match send(req.call()) {
        Ok(r) if r.status == 200 => r,
        _ => return None,
    };
    parse_official_name(&resp.body_text(), app_id)
}

fn parse_official_name(json: &str, app_id: i32) -> Option<String> {
    let root: serde_json::Value = serde_json::from_str(json).ok()?;
    let key = app_id.to_string();
    let item = root.get(key)?.as_object()?;
    if item.get("success")?.as_bool() != Some(true) {
        return None;
    }
    let data = item.get("data")?.as_object()?;
    let name = data.get("name")?.as_str()?.trim();
    if name.is_empty() {
        None
    } else {
        Some(name.to_string())
    }
}

// ── AI error classification ─────────────────────────────────────────────────

pub const AI_KIND_NONE: i32 = -1;
pub const AI_KIND_NETWORK: i32 = 0;
pub const AI_KIND_API: i32 = 1;
pub const AI_KIND_PARSE: i32 = 2;
pub const AI_KIND_CIRCUIT: i32 = 3;

static AI_ERR: Mutex<(i32, i32)> = Mutex::new((AI_KIND_NONE, 0));

fn set_ai_err(kind: i32, status: i32) {
    if let Ok(mut g) = AI_ERR.lock() {
        *g = (kind, status);
    }
}

/// `(kind, statusCode)` recorded by the most recent AI failure.
pub fn ai_error() -> (i32, i32) {
    AI_ERR.lock().map(|g| *g).unwrap_or((AI_KIND_NONE, 0))
}

// ── circuit breaker ─────────────────────────────────────────────────────────

const FAILURE_THRESHOLD: u32 = 5;
const CIRCUIT_OPEN_TIMEOUT: Duration = Duration::from_secs(30);

#[derive(Clone, Copy, PartialEq)]
enum CircuitState {
    Closed,
    Open,
    HalfOpen,
}

struct Circuit {
    state: CircuitState,
    consecutive_failures: u32,
    last_failure: Option<Instant>,
}

static CIRCUIT: Mutex<Circuit> = Mutex::new(Circuit {
    state: CircuitState::Closed,
    consecutive_failures: 0,
    last_failure: None,
});

/// Fails fast while the breaker is open; transitions Open → HalfOpen once the
/// cool-down elapses so a single trial request may go through.
fn check_circuit() -> Result<(), String> {
    let mut c = match CIRCUIT.lock() {
        Ok(g) => g,
        Err(p) => p.into_inner(),
    };
    match c.state {
        CircuitState::Closed | CircuitState::HalfOpen => Ok(()),
        CircuitState::Open => {
            let elapsed = c.last_failure.map(|t| t.elapsed()).unwrap_or(CIRCUIT_OPEN_TIMEOUT);
            if elapsed >= CIRCUIT_OPEN_TIMEOUT {
                c.state = CircuitState::HalfOpen;
                Ok(())
            } else {
                let remaining = (CIRCUIT_OPEN_TIMEOUT - elapsed).as_secs();
                set_ai_err(AI_KIND_CIRCUIT, 0);
                Err(format!(
                    "Circuit breaker is open ({remaining}s remaining). Service may be unavailable."
                ))
            }
        }
    }
}

fn record_success() {
    let mut c = match CIRCUIT.lock() {
        Ok(g) => g,
        Err(p) => p.into_inner(),
    };
    c.consecutive_failures = 0;
    c.state = CircuitState::Closed;
}

fn record_failure() {
    let mut c = match CIRCUIT.lock() {
        Ok(g) => g,
        Err(p) => p.into_inner(),
    };
    c.consecutive_failures += 1;
    c.last_failure = Some(Instant::now());
    if c.consecutive_failures >= FAILURE_THRESHOLD {
        c.state = CircuitState::Open;
    } else if c.state == CircuitState::HalfOpen {
        c.state = CircuitState::Open;
    }
}

/// Restores the breaker to its initial state so integration tests do not leak
/// failure counters into one another.
#[cfg(test)]
fn reset_circuit_for_tests() {
    let mut c = CIRCUIT.lock().unwrap_or_else(|p| p.into_inner());
    c.state = CircuitState::Closed;
    c.consecutive_failures = 0;
    c.last_failure = None;
}

// ── AI: chat completions ────────────────────────────────────────────────────

const MAX_RETRIES: u32 = 3;
const MAX_BACKOFF: Duration = Duration::from_secs(30);

fn strip_completions_suffix(url: &str) -> String {
    let trimmed = url.trim_end_matches('/');
    const SUFFIX: &str = "/chat/completions";
    if trimmed.len() >= SUFFIX.len()
        && trimmed[trimmed.len() - SUFFIX.len()..].eq_ignore_ascii_case(SUFFIX)
    {
        trimmed[..trimmed.len() - SUFFIX.len()].to_string()
    } else {
        trimmed.to_string()
    }
}

/// POSTs an OpenAI-compatible chat completion. On success returns the trimmed
/// assistant content; on failure records the classified error for
/// [`ai_error`] and returns a message.
#[allow(clippy::too_many_arguments)]
pub fn ai_chat(
    system: &str,
    user: &str,
    api_url: &str,
    api_key: &str,
    model: &str,
    max_tokens: i32,
    temperature: f32,
    timeout: Duration,
) -> Result<String, String> {
    if api_url.trim().is_empty() {
        return Err("AI API URL is not configured.".to_string());
    }
    let base = strip_completions_suffix(api_url);
    let url = if base.is_empty() {
        api_url.trim_end_matches('/').to_string()
    } else {
        base
    };
    let url = if url.to_ascii_lowercase().ends_with("/chat/completions") {
        url
    } else {
        format!("{url}/chat/completions")
    };
    let model = if model.trim().is_empty() {
        "gpt-4o-mini"
    } else {
        model
    };

    let body = serde_json::json!({
        "model": model,
        "messages": [
            { "role": "system", "content": system },
            { "role": "user", "content": user }
        ],
        "max_tokens": max_tokens,
        "temperature": temperature
    })
    .to_string();

    check_circuit()?;

    let mut attempt = 0u32;
    let resp = loop {
        if attempt > 0 {
            std::thread::sleep(Duration::from_millis(1000u64 << (attempt - 1)).min(MAX_BACKOFF));
        }
        let mut req = agent()
            .post(&url)
            .set("Content-Type", "application/json")
            .timeout(timeout);
        if !api_key.trim().is_empty() {
            req = req.set("Authorization", &format!("Bearer {api_key}"));
        }
        match send(req.send_string(&body)) {
            Ok(r) => {
                if (r.status == 429 || r.status == 503) && attempt + 1 < MAX_RETRIES {
                    attempt += 1;
                    continue;
                }
                break r;
            }
            Err(e) => {
                if attempt + 1 < MAX_RETRIES {
                    attempt += 1;
                    continue;
                }
                record_failure();
                set_ai_err(AI_KIND_NETWORK, 0);
                return Err(format!("Network error: {e}"));
            }
        }
    };

    if !(200..300).contains(&resp.status) {
        record_failure();
        set_ai_err(AI_KIND_API, resp.status as i32);
        let text = resp.body_text();
        let preview = if text.len() > 200 { &text[..200] } else { &text };
        return Err(format!("API error {}: {}", resp.status, preview));
    }
    if !resp.is_json() {
        let ct = resp.content_type.unwrap_or_default();
        set_ai_err(AI_KIND_PARSE, 0);
        return Err(format!("Expected JSON response but got {ct}"));
    }

    let content = parse_chat_content(&resp.body).map_err(|e| {
        set_ai_err(AI_KIND_PARSE, 0);
        e
    })?;
    record_success();
    set_ai_err(AI_KIND_NONE, 0);
    Ok(content)
}

fn parse_chat_content(body: &[u8]) -> Result<String, String> {
    let root: serde_json::Value =
        serde_json::from_slice(body).map_err(|e| format!("Failed to parse AI response: {e}"))?;
    let choices = root
        .get("choices")
        .and_then(|c| c.as_array())
        .ok_or_else(|| "Failed to parse AI response: missing 'choices'".to_string())?;
    let first = choices
        .first()
        .ok_or_else(|| "Failed to parse AI response: empty 'choices'".to_string())?;
    let content = first
        .get("message")
        .and_then(|m| m.get("content"))
        .and_then(|c| c.as_str())
        .or_else(|| first.get("text").and_then(|t| t.as_str()))
        .ok_or_else(|| "Failed to parse AI response: no content".to_string())?;
    Ok(content.trim().to_string())
}

// ── AI: model listing ───────────────────────────────────────────────────────

/// GETs `/models` (derived from the chat-completions URL) and returns the ids,
/// sorted case-insensitively. Failures are classified for [`ai_error`].
pub fn ai_models(api_url: &str, api_key: &str, timeout: Duration) -> Result<Vec<String>, String> {
    let base = strip_completions_suffix(api_url);
    let models_url = format!("{base}/models");

    let mut req = agent()
        .get(&models_url)
        .set("User-Agent", "steamedit-Enhance")
        .timeout(timeout);
    if !api_key.trim().is_empty() {
        req = req.set("Authorization", &format!("Bearer {api_key}"));
    }

    let resp = match send(req.call()) {
        Ok(r) => r,
        Err(e) => {
            set_ai_err(AI_KIND_NETWORK, 0);
            return Err(format!("Network error: {e}"));
        }
    };

    if !(200..300).contains(&resp.status) {
        set_ai_err(AI_KIND_API, resp.status as i32);
        return Err(format!("API error {}: {}", resp.status, resp.body_text()));
    }
    if !resp.is_json() {
        let ct = resp.content_type.unwrap_or_default();
        set_ai_err(AI_KIND_PARSE, 0);
        return Err(format!("Expected JSON response but got {ct}"));
    }

    let root: serde_json::Value = serde_json::from_slice(&resp.body).map_err(|e| {
        set_ai_err(AI_KIND_PARSE, 0);
        format!("Failed to parse models response: {e}")
    })?;

    let mut models: Vec<String> = root
        .get("data")
        .and_then(|d| d.as_array())
        .map(|arr| {
            arr.iter()
                .filter_map(|item| item.get("id").and_then(|id| id.as_str()))
                .filter(|s| !s.is_empty())
                .map(|s| s.to_string())
                .collect()
        })
        .unwrap_or_default();
    models.sort_by(|a, b| a.to_ascii_lowercase().cmp(&b.to_ascii_lowercase()));
    set_ai_err(AI_KIND_NONE, 0);
    Ok(models)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use std::net::TcpListener;
    use std::sync::Mutex as StdMutex;

    /// Serializes tests that touch the process-wide circuit breaker.
    static NET_TEST_LOCK: StdMutex<()> = StdMutex::new(());

    fn lock() -> std::sync::MutexGuard<'static, ()> {
        let guard = NET_TEST_LOCK.lock().unwrap_or_else(|p| p.into_inner());
        reset_circuit_for_tests();
        guard
    }

    /// Minimal one-request-at-a-time HTTP server; `responder` sees the raw request
    /// text and the request ordinal (0-based) and returns `(status, content_type, body)`.
    fn mock_server<F>(responder: F) -> (String, std::thread::JoinHandle<()>)
    where
        F: Fn(&str, usize) -> (u16, String, Vec<u8>) + Send + 'static,
    {
        let listener = TcpListener::bind("127.0.0.1:0").unwrap();
        let addr = listener.local_addr().unwrap();
        let handle = std::thread::spawn(move || {
            let mut ordinal = 0usize;
            for stream in listener.incoming() {
                let mut stream = match stream {
                    Ok(s) => s,
                    Err(_) => break,
                };
                let mut buf = Vec::new();
                let mut tmp = [0u8; 2048];
                // Read headers, then drain the body (Content-Length) so we never
                // reset the connection while the client is still writing.
                let mut header_end = None;
                loop {
                    match stream.read(&mut tmp) {
                        Ok(0) => break,
                        Ok(k) => {
                            buf.extend_from_slice(&tmp[..k]);
                            if let Some(p) = buf.windows(4).position(|w| w == b"\r\n\r\n") {
                                header_end = Some(p + 4);
                                break;
                            }
                        }
                        Err(_) => break,
                    }
                }
                if let Some(he) = header_end {
                    let text = String::from_utf8_lossy(&buf[..he]).to_ascii_lowercase();
                    let content_length: usize = text
                        .lines()
                        .find_map(|l| l.strip_prefix("content-length:"))
                        .and_then(|v| v.trim().parse().ok())
                        .unwrap_or(0);
                    let needed = he + content_length;
                    while buf.len() < needed {
                        match stream.read(&mut tmp) {
                            Ok(0) => break,
                            Ok(k) => buf.extend_from_slice(&tmp[..k]),
                            Err(_) => break,
                        }
                    }
                }
                let req = String::from_utf8_lossy(&buf).into_owned();
                let (status, ctype, body) = responder(&req, ordinal);
                ordinal += 1;
                let reason = if (200..300).contains(&status) { "OK" } else { "ERR" };
                let head = format!(
                    "HTTP/1.1 {status} {reason}\r\nContent-Type: {ctype}\r\nContent-Length: {}\r\nConnection: close\r\n\r\n",
                    body.len()
                );
                let _ = stream.write_all(head.as_bytes());
                let _ = stream.write_all(&body);
                let _ = stream.flush();
            }
        });
        (format!("http://127.0.0.1:{}", addr.port()), handle)
    }

    const JPEG: [u8; 8] = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0];

    #[test]
    fn cover_fetch_falls_back_past_non_image() {
        let (base, _h) = mock_server(|req, _| {
            if req.contains("/html") {
                (200, "text/html".into(), b"<html>404</html>".to_vec())
            } else {
                (200, "image/jpeg".into(), JPEG.to_vec())
            }
        });
        let urls = vec![format!("{base}/html"), format!("{base}/img")];
        assert!(fetch_first_valid_image(&urls, Duration::from_secs(5)).is_some());
    }

    #[test]
    fn cover_fetch_none_when_no_image() {
        let (base, _h) = mock_server(|_, _| (404, "text/plain".into(), b"nope".to_vec()));
        assert!(fetch_first_valid_image(&[format!("{base}/a")], Duration::from_secs(5)).is_none());
    }

    #[test]
    fn ai_chat_parses_success() {
        let _guard = lock();
        let (base, _h) = mock_server(|req, _| {
            assert!(req.starts_with("POST "));
            assert!(req.contains("Authorization: Bearer test"));
            (
                200,
                "application/json".into(),
                br#"{"choices":[{"message":{"content":"  name  "}}]}"#.to_vec(),
            )
        });
        let out = ai_chat(
            "sys",
            "user",
            &format!("{base}/v1/chat/completions"),
            "test",
            "gpt-4o-mini",
            4096,
            0.1,
            Duration::from_secs(5),
        )
        .unwrap();
        assert_eq!(out, "name");
    }

    #[test]
    fn ai_chat_retries_transient_error() {
        let _guard = lock();
        let (base, _h) = mock_server(|_, ordinal| {
            if ordinal < 2 {
                (503, "text/plain".into(), b"busy".to_vec())
            } else {
                (
                    200,
                    "application/json".into(),
                    br#"{"choices":[{"message":{"content":"retried"}}]}"#.to_vec(),
                )
            }
        });
        let out = ai_chat(
            "s",
            "u",
            &format!("{base}/chat/completions"),
            "",
            "m",
            16,
            0.1,
            Duration::from_secs(5),
        )
        .unwrap();
        assert_eq!(out, "retried");
    }

    #[test]
    fn ai_chat_reports_api_error() {
        let _guard = lock();
        let (base, _h) = mock_server(|_, _| {
            (
                401,
                "application/json".into(),
                br#"{"error":"unauthorized"}"#.to_vec(),
            )
        });
        let err = ai_chat(
            "s",
            "u",
            &format!("{base}/chat/completions"),
            "",
            "m",
            16,
            0.1,
            Duration::from_secs(5),
        )
        .unwrap_err();
        assert!(err.contains("401"), "unexpected: {err}");
        assert_eq!(ai_error(), (AI_KIND_API, 401));
    }

    #[test]
    fn ai_chat_reports_network_error() {
        let _guard = lock();
        // Bind then release a port so nothing is listening on it.
        let port = {
            let l = TcpListener::bind("127.0.0.1:0").unwrap();
            l.local_addr().unwrap().port()
        };
        let err = ai_chat(
            "s",
            "u",
            &format!("http://127.0.0.1:{port}/chat/completions"),
            "",
            "m",
            16,
            0.1,
            Duration::from_secs(5),
        )
        .unwrap_err();
        assert!(err.starts_with("Network error"), "unexpected: {err}");
        assert_eq!(ai_error().0, AI_KIND_NETWORK);
    }

    #[test]
    fn ai_models_are_sorted() {
        let _guard = lock();
        let (base, _h) = mock_server(|req, _| {
            assert!(req.starts_with("GET "));
            (
                200,
                "application/json".into(),
                br#"{"data":[{"id":"gpt-4"},{"id":"gpt-3.5-turbo"}]}"#.to_vec(),
            )
        });
        let models = ai_models(
            &format!("{base}/v1/chat/completions"),
            "k",
            Duration::from_secs(5),
        )
        .unwrap();
        assert_eq!(models, vec!["gpt-3.5-turbo".to_string(), "gpt-4".to_string()]);
    }

    #[test]
    fn image_magic_detection() {
        assert!(is_valid_image(&[0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0]));
        assert!(is_valid_image(&[
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        ]));
        assert!(!is_valid_image(b"<html>"));
        assert!(!is_valid_image(&[0xFF, 0xD8]));
    }

    #[test]
    fn official_name_parsing() {
        let json = r#"{"440":{"success":true,"data":{"name":"  Team Fortress 2  "}}}"#;
        assert_eq!(parse_official_name(json, 440).as_deref(), Some("Team Fortress 2"));
        assert!(parse_official_name(r#"{"440":{"success":false}}"#, 440).is_none());
        assert!(parse_official_name("not json", 440).is_none());
        assert!(parse_official_name(r#"{"440":{"success":true,"data":{}}}"#, 440).is_none());
    }

    #[test]
    fn chat_content_parsing() {
        let body = br#"{"choices":[{"message":{"content":"  test name  "}}]}"#;
        assert_eq!(parse_chat_content(body).unwrap(), "test name");
        let legacy = br#"{"choices":[{"text":"legacy"}]}"#;
        assert_eq!(parse_chat_content(legacy).unwrap(), "legacy");
        assert!(parse_chat_content(br#"{"choices":[]}"#).is_err());
    }

    #[test]
    fn completions_suffix_is_stripped() {
        assert_eq!(
            strip_completions_suffix("https://api.openai.com/v1/chat/completions"),
            "https://api.openai.com/v1"
        );
        assert_eq!(strip_completions_suffix("https://x/v1/"), "https://x/v1");
    }
}
