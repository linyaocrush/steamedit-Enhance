#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance;

namespace steamedit_EnhanceTests;

[TestClass]
public class CoverLayoutTests
{
    [TestMethod]
    public void PortraitCover_UsesFullAvailableHeight_AndProportionalWidth()
    {
        // 600x900 竖封面
        var size = MainWindow.FitCoverSize(600, 900, 427, 123);
        Assert.AreEqual(123, size.Height);       // 占满可用高度
        Assert.AreEqual(82, size.Width);         // 82 = 123 * 600/900
        Assert.IsTrue(size.Width < size.Height); // 保持竖版
    }

    [TestMethod]
    public void LandscapeCapsule_FitsWithinBounds_HeightLimited()
    {
        // 920x430 横版胶囊在矮可用区内受高度限制
        var size = MainWindow.FitCoverSize(920, 430, 427, 123);
        Assert.AreEqual(123, size.Height);
        Assert.AreEqual(263, size.Width);
        Assert.IsTrue(size.Width <= 427);         // 不超出可用宽度
        Assert.IsTrue(size.Width > size.Height);   // 保持横版
    }

    [TestMethod]
    public void LandscapeWide_FitsWithinBounds_NoOverflow()
    {
        // hero 图 1920x620，宽远大于可用区域
        var size = MainWindow.FitCoverSize(1920, 620, 427, 123);
        Assert.IsTrue(size.Width <= 427);
        Assert.IsTrue(size.Height <= 123);
    }

    [TestMethod]
    public void SquareImage_SizesToAvailableHeight()
    {
        var size = MainWindow.FitCoverSize(512, 512, 427, 123);
        Assert.AreEqual(123, size.Width);
        Assert.AreEqual(123, size.Height);
    }

    [TestMethod]
    public void TinyAvailableSpace_StillYieldsUsableMinimum()
    {
        var size = MainWindow.FitCoverSize(600, 900, 30, 30);
        Assert.IsTrue(size.Width >= 24 && size.Height >= 24);
        Assert.IsTrue(size.Width <= 30 && size.Height <= 30);
    }

    [TestMethod]
    public void WideTallAvailableSpace_LandscapeUsesWidth()
    {
        // 非常大的面板高度下横图宽度受 availW 限制
        var size = MainWindow.FitCoverSize(920, 430, 300, 800);
        Assert.AreEqual(300, size.Width);
        Assert.AreEqual(140, size.Height);
    }
}
