#nullable enable
using System;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance;

namespace steamedit_EnhanceTests;

[TestClass]
public class ImageScalerTests
{
    private static Bitmap CreateBitmap(int w, int h, Color color)
    {
        var bmp = new Bitmap(w, h);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(color);
        }
        return bmp;
    }

    private static Color GetPixel(Bitmap bmp, int x, int y)
        => bmp.GetPixel(x, y);

    [TestMethod]
    public void Upscale_DimensionsAndFormat()
    {
        using Bitmap src = CreateBitmap(4, 6, Color.White);
        using Bitmap dst = ImageScaler.Resize(src, 8, 12);
        Assert.AreEqual(8, dst.Width);
        Assert.AreEqual(12, dst.Height);
    }

    [TestMethod]
    public void Upscale_SolidColorStaysUniform()
    {
        Color color = Color.FromArgb(40, 90, 200);
        using Bitmap src = CreateBitmap(3, 5, color);
        using Bitmap dst = ImageScaler.Resize(src, 30, 50); // 10 倍放大，走 Lanczos-3
        for (int y = 0; y < dst.Height; y += 2)
            for (int x = 0; x < dst.Width; x += 2)
            {
                Color c = GetPixel(dst, x, y);
                // 纯色放大后应保持大致一致（允许小数舍入 ±2）
                Assert.IsTrue(Math.Abs(c.R - color.R) <= 2 && Math.Abs(c.G - color.G) <= 2 &&
                              Math.Abs(c.B - color.B) <= 2, $"({c}) != {color}");
            }
    }

    [TestMethod]
    public void Upscale_KeepsContentInBounds()
    {
        using Bitmap src = CreateBitmap(2, 2, Color.FromArgb(0, 120, 240));
        using Bitmap dst = ImageScaler.Resize(src, 17, 13);
        for (int y = 0; y < dst.Height; y++)
            for (int x = 0; x < dst.Width; x++)
            {
                Color c = GetPixel(dst, x, y);
                Assert.IsTrue(c.R <= 0 && Math.Abs(c.G - 120) <= 3 && Math.Abs(c.B - 240) <= 3,
                    $"outlier at ({x},{y}): {c}");
            }
    }

    [TestMethod]
    public void Downscale_Dimensions()
    {
        using Bitmap src = CreateBitmap(50, 40, Color.Gray);
        using Bitmap dst = ImageScaler.Resize(src, 20, 16); // 缩小走双三次
        Assert.AreEqual(20, dst.Width);
        Assert.AreEqual(16, dst.Height);
    }

    [TestMethod]
    public void EqualSize_ReturnsIndependentCopy()
    {
        using Bitmap src = CreateBitmap(6, 6, Color.FromArgb(10, 20, 30));
        using Bitmap dst = ImageScaler.Resize(src, 6, 6);
        Assert.AreEqual(6, dst.Width);
        Assert.AreEqual(6, dst.Height);
        Assert.AreNotSame(src, dst);
        dst.SetPixel(0, 0, Color.Red);
        Assert.AreEqual(Color.FromArgb(10, 20, 30), GetPixel(src, 0, 0));
    }
}
