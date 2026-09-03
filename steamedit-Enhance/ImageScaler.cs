#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace steamedit_Enhance;

/// <summary>
/// 图片缩放。放大时用 Lanczos-3 重建插值以提升画质，等大时拷贝原图，缩小时用 GDI+ 高质量双三次。
/// 返回的 Bitmap 一律是新建对象，所有权交由调用方（负责 Dispose）。
/// </summary>
internal static class ImageScaler
{
	private const double Radius = 3.0;

	/// <summary>把 source 缩放到指定像素尺寸，返回新 Bitmap（Lanczos-3 放大 / 双三次缩小 / 等大拷贝）。</summary>
	public static Bitmap Resize(Image source, int width, int height)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException(nameof(width), "目标尺寸必须为正");
		if (width == source.Width && height == source.Height)
			return new Bitmap(source);
		if (width > source.Width || height > source.Height)
			return ResampleLanczos3(source, width, height);
		return ResampleBicubic(source, width, height);
	}

	private static Bitmap ResampleBicubic(Image source, int width, int height)
	{
		var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		using (Graphics g = Graphics.FromImage(bmp))
		{
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.SmoothingMode = SmoothingMode.HighQuality;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.CompositingQuality = CompositingQuality.HighQuality;
			g.DrawImage(source, 0, 0, width, height);
		}
		return bmp;
	}

	// ── Lanczos-3 可分滤波（先横后纵两趟）──

	private static Bitmap ResampleLanczos3(Image source, int width, int height)
	{
		using Bitmap src = ToArgb(source);
		int sw = src.Width;
		int sh = src.Height;

		byte[] pixels = new byte[sw * sh * 4];
		ReadPixels(src, pixels, sw, sh);

		var htmp = new byte[width * sh * 4];
		FilterHorizontal(pixels, sw, sh, htmp, width);

		var output = new byte[width * height * 4];
		FilterVertical(htmp, width, sh, output, height);

		return BitmapFromBytes(output, width, height);
	}

	/// <summary>统一为 32 位 ARGB，保证锁位图时每像素 4 字节、行序自上而下。</summary>
	private static Bitmap ToArgb(Image source)
	{
		if (source is Bitmap b && b.PixelFormat == PixelFormat.Format32bppArgb)
			return new Bitmap(b);
		var bmp = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
		using (Graphics g = Graphics.FromImage(bmp))
			g.DrawImage(source, 0, 0, source.Width, source.Height);
		return bmp;
	}

	private static void ReadPixels(Bitmap bmp, byte[] buffer, int w, int h)
	{
		BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		try
		{
			int rowBytes = w * 4;
			int stride = Math.Abs(bd.Stride);
			for (int y = 0; y < h; y++)
			{
				IntPtr row = IntPtr.Add(bd.Scan0, y * stride);
				Marshal.Copy(row, buffer, y * rowBytes, rowBytes);
			}
		}
		finally
		{
			bmp.UnlockBits(bd);
		}
	}

	private static Bitmap BitmapFromBytes(byte[] buffer, int w, int h)
	{
		var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
		BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
		try
		{
			int rowBytes = w * 4;
			int stride = Math.Abs(bd.Stride);
			for (int y = 0; y < h; y++)
			{
				IntPtr row = IntPtr.Add(bd.Scan0, y * stride);
				Marshal.Copy(buffer, y * rowBytes, row, rowBytes);
			}
		}
		finally
		{
			bmp.UnlockBits(bd);
		}
		return bmp;
	}

	private sealed class WeightTable
	{
		public int Start;
		public double[] Weights = Array.Empty<double>();
	}

	private static WeightTable[] BuildTables(int dstLen, int srcLen)
	{
		double scale = (double)srcLen / dstLen;
		var tables = new WeightTable[dstLen];
		for (int i = 0; i < dstLen; i++)
		{
			double center = (i + 0.5) * scale - 0.5;
			int j0 = (int)Math.Ceiling(center - Radius);
			int j1 = (int)Math.Floor(center + Radius);
			if (j0 < 0) j0 = 0;
			if (j1 > srcLen - 1) j1 = srcLen - 1;
			int count = Math.Max(1, j1 - j0 + 1);

			var ws = new double[count];
			double sum = 0;
			for (int j = j0; j <= j1; j++)
			{
				double w = LanczosWeight(center - j);
				ws[j - j0] = w;
				sum += w;
			}
			if (Math.Abs(sum) < 1e-12)
			{
				// 理论上不会发生，兜底退化为最近邻
				for (int k = 0; k < count; k++) ws[k] = 0;
				ws[0] = 1;
			}
			else
			{
				for (int k = 0; k < count; k++) ws[k] /= sum;
			}

			tables[i] = new WeightTable { Start = j0, Weights = ws };
		}
		return tables;
	}

	private static double LanczosWeight(double x)
	{
		if (x < 0) x = -x;
		if (x < 1e-12) return 1.0;
		if (x >= Radius) return 0.0;
		double px = Math.PI * x;
		return Radius * Math.Sin(px) * Math.Sin(px / Radius) / (px * px);
	}

	private static void FilterHorizontal(byte[] src, int sw, int sh, byte[] dst, int dw)
	{
		WeightTable[] tables = BuildTables(dw, sw);
		for (int y = 0; y < sh; y++)
		{
			int sBase = y * sw * 4;
			int dBase = y * dw * 4;
			for (int x = 0; x < dw; x++)
			{
				WeightTable t = tables[x];
				double r = 0, g = 0, b = 0, a = 0;
				for (int k = 0; k < t.Weights.Length; k++)
				{
					int idx = sBase + (t.Start + k) * 4;
					double w = t.Weights[k];
					r += w * src[idx];
					g += w * src[idx + 1];
					b += w * src[idx + 2];
					a += w * src[idx + 3];
				}
				int o = dBase + x * 4;
				dst[o] = Clamp(r);
				dst[o + 1] = Clamp(g);
				dst[o + 2] = Clamp(b);
				dst[o + 3] = Clamp(a);
			}
		}
	}

	private static void FilterVertical(byte[] src, int dw, int sh, byte[] dst, int dh)
	{
		WeightTable[] tables = BuildTables(dh, sh);
		for (int y = 0; y < dh; y++)
		{
			WeightTable t = tables[y];
			int dBase = y * dw * 4;
			for (int x = 0; x < dw; x++)
			{
				double r = 0, g = 0, b = 0, a = 0;
				for (int k = 0; k < t.Weights.Length; k++)
				{
					int idx = ((t.Start + k) * dw + x) * 4;
					double w = t.Weights[k];
					r += w * src[idx];
					g += w * src[idx + 1];
					b += w * src[idx + 2];
					a += w * src[idx + 3];
				}
				int o = dBase + x * 4;
				dst[o] = Clamp(r);
				dst[o + 1] = Clamp(g);
				dst[o + 2] = Clamp(b);
				dst[o + 3] = Clamp(a);
			}
		}
	}

	private static byte Clamp(double v)
	{
		if (v <= 0) return 0;
		if (v >= 255) return 255;
		return (byte)Math.Round(v);
	}
}
