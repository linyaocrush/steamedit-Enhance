//! Image processing kernel.
//!
//! Operates on raw BGRA8 pixel buffers (the layout GDI+ exposes via `LockBits`), so
//! the managed side only needs the platform codec to decode/encode files — all
//! geometric resampling happens here.
//!
//! Two separable kernel resamplers are provided, selected by the caller's intent
//! (matching the previous managed behaviour):
//!
//! * **Lanczos-3** when either axis grows (upscaling — reconstruction quality);
//! * **bicubic (Catmull-Rom)** otherwise (downscaling).
//!
//! Both are implemented as scale-aware two-pass filters (horizontal then vertical),
//! so the sample footprint widens automatically as the ratio grows.

/// Lanczos kernel support radius.
const LANCZOS_RADIUS: f64 = 3.0;
/// Catmull-Rom (cubic) kernel support radius.
const CUBIC_RADIUS: f64 = 2.0;
/// Cap on the linear size of an individual dimension (defensive, ~64 KiB).
const MAX_DIM: u32 = 1 << 16;

#[derive(Clone, Copy, PartialEq, Eq)]
enum Kernel {
    Lanczos3,
    Cubic,
}

impl Kernel {
    fn radius(self) -> f64 {
        match self {
            Kernel::Lanczos3 => LANCZOS_RADIUS,
            Kernel::Cubic => CUBIC_RADIUS,
        }
    }

    fn weight(self, x: f64) -> f64 {
        let x = x.abs();
        match self {
            Kernel::Lanczos3 => {
                if x < 1e-12 {
                    1.0
                } else if x >= LANCZOS_RADIUS {
                    0.0
                } else {
                    let px = std::f64::consts::PI * x;
                    LANCZOS_RADIUS * px.sin() * (px / LANCZOS_RADIUS).sin() / (px * px)
                }
            }
            Kernel::Cubic => {
                // Catmull-Rom: a = -0.5.
                const A: f64 = -0.5;
                if x < 1.0 {
                    (A + 2.0) * x * x * x - (A + 3.0) * x * x + 1.0
                } else if x < 2.0 {
                    A * x * x * x - 5.0 * A * x * x + 8.0 * A * x - 4.0 * A
                } else {
                    0.0
                }
            }
        }
    }
}

/// Precomputed per-output-column weights: a contiguous window starting at `start`.
struct WeightTable {
    start: i64,
    weights: Vec<f64>,
}

fn build_tables(dst_len: usize, src_len: usize, kernel: Kernel) -> Vec<WeightTable> {
    let scale = src_len as f64 / dst_len as f64;
    let radius = kernel.radius();
    let mut tables = Vec::with_capacity(dst_len);
    for i in 0..dst_len {
        let center = (i as f64 + 0.5) * scale - 0.5;
        let mut j0 = (center - radius).ceil() as i64;
        let mut j1 = (center + radius).floor() as i64;
        if j0 < 0 {
            j0 = 0;
        }
        let last = src_len as i64 - 1;
        if j1 > last {
            j1 = last;
        }
        let count = ((j1 - j0 + 1).max(1)) as usize;
        let mut weights = vec![0f64; count];
        let mut sum = 0.0;
        for j in j0..=j1 {
            let w = kernel.weight(center - j as f64);
            weights[(j - j0) as usize] = w;
            sum += w;
        }
        if sum.abs() < 1e-12 {
            // Degenerate window: fall back to nearest-neighbour.
            for w in weights.iter_mut() {
                *w = 0.0;
            }
            weights[0] = 1.0;
        } else {
            for w in weights.iter_mut() {
                *w /= sum;
            }
        }
        tables.push(WeightTable {
            start: j0,
            weights,
        });
    }
    tables
}

#[inline]
fn clamp_px(v: f64) -> u8 {
    if v <= 0.0 {
        0
    } else if v >= 255.0 {
        255
    } else {
        v.round() as u8
    }
}

/// Horizontal pass: `(sw, sh)` → `(dw, sh)`.
fn filter_horizontal(src: &[u8], sw: usize, sh: usize, dw: usize, kernel: Kernel) -> Vec<u8> {
    let tables = build_tables(dw, sw, kernel);
    let mut out = vec![0u8; dw * sh * 4];
    for y in 0..sh {
        let s_base = y * sw * 4;
        let d_base = y * dw * 4;
        for x in 0..dw {
            let t = &tables[x];
            let (mut r, mut g, mut b, mut a) = (0.0f64, 0.0f64, 0.0f64, 0.0f64);
            for (k, &w) in t.weights.iter().enumerate() {
                let idx = s_base + (t.start as usize + k) * 4;
                r += w * src[idx] as f64;
                g += w * src[idx + 1] as f64;
                b += w * src[idx + 2] as f64;
                a += w * src[idx + 3] as f64;
            }
            let o = d_base + x * 4;
            out[o] = clamp_px(r);
            out[o + 1] = clamp_px(g);
            out[o + 2] = clamp_px(b);
            out[o + 3] = clamp_px(a);
        }
    }
    out
}

/// Vertical pass: `(dw, sh)` → `(dw, dh)`.
fn filter_vertical(src: &[u8], dw: usize, sh: usize, dh: usize, kernel: Kernel) -> Vec<u8> {
    let tables = build_tables(dh, sh, kernel);
    let mut out = vec![0u8; dw * dh * 4];
    for y in 0..dh {
        let t = &tables[y];
        let d_base = y * dw * 4;
        for x in 0..dw {
            let (mut r, mut g, mut b, mut a) = (0.0f64, 0.0f64, 0.0f64, 0.0f64);
            for (k, &w) in t.weights.iter().enumerate() {
                let idx = ((t.start as usize + k) * dw + x) * 4;
                r += w * src[idx] as f64;
                g += w * src[idx + 1] as f64;
                b += w * src[idx + 2] as f64;
                a += w * src[idx + 3] as f64;
            }
            let o = d_base + x * 4;
            out[o] = clamp_px(r);
            out[o + 1] = clamp_px(g);
            out[o + 2] = clamp_px(b);
            out[o + 3] = clamp_px(a);
        }
    }
    out
}

/// Resamples a straight BGRA8 buffer from `(sw, sh)` to `(dw, dh)`.
///
/// `src` must hold exactly `sw * sh * 4` bytes. The kernel is chosen by size:
/// upscaling uses Lanczos-3, downscaling uses bicubic. Returns the new buffer
/// (`dw * dh * 4` bytes).
pub fn resize_bgra(src: &[u8], sw: u32, sh: u32, dw: u32, dh: u32) -> Result<Vec<u8>, String> {
    if sw == 0 || sh == 0 || dw == 0 || dh == 0 {
        return Err("image dimensions must be positive".to_string());
    }
    if sw > MAX_DIM || sh > MAX_DIM || dw > MAX_DIM || dh > MAX_DIM {
        return Err(format!("image dimension exceeds {MAX_DIM}"));
    }
    let expected = sw as usize * sh as usize * 4;
    if src.len() != expected {
        return Err(format!(
            "source buffer is {} bytes, expected {expected} for {sw}x{sh} BGRA",
            src.len()
        ));
    }
    if sw == dw && sh == dh {
        return Ok(src.to_vec());
    }

    let (sw, sh, dw, dh) = (sw as usize, sh as usize, dw as usize, dh as usize);
    let kernel = if dw > sw || dh > sh {
        Kernel::Lanczos3
    } else {
        Kernel::Cubic
    };

    let horizontal = filter_horizontal(src, sw, sh, dw, kernel);
    Ok(filter_vertical(&horizontal, dw, sh, dh, kernel))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn solid(sw: u32, sh: u32, b: u8, g: u8, r: u8, a: u8) -> Vec<u8> {
        let mut v = Vec::with_capacity((sw * sh * 4) as usize);
        for _ in 0..sw * sh {
            v.extend_from_slice(&[b, g, r, a]);
        }
        v
    }

    #[test]
    fn rejects_bad_buffer_size() {
        assert!(resize_bgra(&[0u8; 10], 2, 2, 4, 4).is_err());
        assert!(resize_bgra(&[], 0, 2, 4, 4).is_err());
    }

    #[test]
    fn equal_size_is_a_copy() {
        let src = solid(3, 2, 1, 2, 3, 255);
        let out = resize_bgra(&src, 3, 2, 3, 2).unwrap();
        assert_eq!(out, src);
    }

    #[test]
    fn upscale_dimensions_and_uniformity() {
        let src = solid(3, 5, 200, 90, 40, 255);
        let out = resize_bgra(&src, 3, 5, 30, 50).unwrap();
        assert_eq!(out.len(), 30 * 50 * 4);
        // Solid colour must survive the filter within rounding tolerance.
        for px in out.chunks_exact(4) {
            assert!(px[0].abs_diff(200) <= 2, "b={}", px[0]);
            assert!(px[1].abs_diff(90) <= 2);
            assert!(px[2].abs_diff(40) <= 2);
        }
    }

    #[test]
    fn downscale_dimensions() {
        let src = solid(50, 40, 128, 128, 128, 255);
        let out = resize_bgra(&src, 50, 40, 20, 16).unwrap();
        assert_eq!(out.len(), 20 * 16 * 4);
    }

    #[test]
    fn mixed_axis_uses_lanczos_but_keeps_bounds() {
        // Width shrinks, height grows: still an upscale overall (Lanczos path).
        let src = solid(40, 4, 240, 120, 0, 255);
        let out = resize_bgra(&src, 40, 4, 20, 8).unwrap();
        assert_eq!(out.len(), 20 * 8 * 4);
        for px in out.chunks_exact(4) {
            assert!(px[0] <= 240 && px[1] <= 120 + 3);
        }
    }
}
