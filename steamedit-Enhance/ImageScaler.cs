#nullable enable
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using steamedit_Enhance.Data.Native;

namespace steamedit_Enhance;

/// <summary>
/// 图片缩放。采样算法（放大用 Lanczos-3、缩小用双三次）由 Rust 内核
/// (<c>ste_image_resize_bgra</c>) 完成；本类只负责用 GDI+ 在 <see cref="Image"/>
/// 与原始 BGRA 像素缓冲之间转换（解码/编码是平台能力）。
/// 返回的 Bitmap 一律是新建对象，所有权交由调用方（负责 Dispose）。
/// </summary>
internal static class ImageScaler
{
	/// <summary>把 source 缩放到指定像素尺寸，返回新 Bitmap。</summary>
	public static Bitmap Resize(Image source, int width, int height)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException(nameof(width), "目标尺寸必须为正");
		if (width == source.Width && height == source.Height)
			return new Bitmap(source);
		return ResampleNative(source, width, height);
	}

	private static Bitmap ResampleNative(Image source, int width, int height)
	{
		using Bitmap src = ToArgb(source);
		int sw = src.Width;
		int sh = src.Height;

		byte[] pixels = new byte[sw * sh * 4];
		ReadPixels(src, pixels, sw, sh);

		byte[]? output = SteameditNative.ResizeBgra(pixels, sw, sh, width, height);
		if (output == null)
			throw new InvalidOperationException(
				"原生图像内核缩放失败：" + SteameditNative.LastError);

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
}
