#nullable enable
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

[TestClass]
public class SteamCoverResolverTests
{
    private static string _root = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "steamedit-cover-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        catch { /* ignore */ }
    }

    private static void WriteFile(string relativePath, string content = "x")
    {
        string full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    // 写入仅含合法 PNG 文件头的字节（尺寸真实，CRC/数据随意），用于模糊扫描按尺寸识别。
    private static void WritePngHeader(string relativePath, int width, int height)
    {
        string full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        byte[] b = new byte[24];
        b[0] = 0x89; b[1] = 0x50; b[2] = 0x4E; b[3] = 0x47; b[4] = 0x0D; b[5] = 0x0A; b[6] = 0x1A; b[7] = 0x0A;
        b[12] = (byte)'I'; b[13] = (byte)'H'; b[14] = (byte)'D'; b[15] = (byte)'R';
        b[16] = (byte)(width >> 24); b[17] = (byte)(width >> 16); b[18] = (byte)(width >> 8); b[19] = (byte)width;
        b[20] = (byte)(height >> 24); b[21] = (byte)(height >> 16); b[22] = (byte)(height >> 8); b[23] = (byte)height;
        File.WriteAllBytes(full, b);
    }

    private static SteamCoverResolver NewResolver()
        => new SteamCoverResolver(_root);

    private static string Abs(string relativePath)
        => Path.Combine(_root, relativePath);

    // ── 竖版 (GetPortraitPath) ──

    [TestMethod]
    public void Portrait_NoSteamFolder_ReturnsNull()
    {
        Assert.IsNull(NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_EmptyLibraryCache_ReturnsNull()
    {
        Directory.CreateDirectory(Path.Combine(_root, "appcache", "librarycache"));
        Assert.IsNull(NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_FolderLayout_Found()
    {
        WriteFile(@"appcache\librarycache\730\library_600x900.jpg");
        Assert.AreEqual(Abs(@"appcache\librarycache\730\library_600x900.jpg"), NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_FlatLayout_Found()
    {
        WriteFile(@"appcache\librarycache\730_library_600x900.png");
        Assert.AreEqual(Abs(@"appcache\librarycache\730_library_600x900.png"), NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_CustomGrid_OverridesOfficial()
    {
        WriteFile(@"appcache\librarycache\730\library_600x900.jpg");
        WriteFile(@"userdata\76561198000000000\config\grid\730p.png");
        Assert.AreEqual(Abs(@"userdata\76561198000000000\config\grid\730p.png"), NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_OnlyWidePresent_ReturnsNull()
    {
        WriteFile(@"appcache\librarycache\730\library_capsule.jpg");
        WriteFile(@"userdata\76561198000000000\config\grid\730.png");
        Assert.IsNull(NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_NonNumericUserDataDir_IsSkipped()
    {
        WriteFile(@"userdata\notanumber\config\grid\730p.png");
        Assert.IsNull(NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_NonPositiveAppId_ReturnsNull()
    {
        WriteFile(@"appcache\librarycache\730\library_600x900.jpg");
        Assert.IsNull(NewResolver().GetPortraitPath(0));
        Assert.IsNull(NewResolver().GetPortraitPath(-5));
    }

    [TestMethod]
    public void Portrait_NegativeCacheWorks()
    {
        var resolver = NewResolver();
        Assert.IsNull(resolver.GetPortraitPath(730));
        WriteFile(@"appcache\librarycache\730\library_600x900.jpg");
        Assert.IsNull(resolver.GetPortraitPath(730)); // 会话内不重扫
    }

    // ── 模糊扫描（hash 命名 / 官方固定名不存在）──

    [TestMethod]
    public void Portrait_HashedExtensionlessFile_Found()
    {
        // Steam 部分游戏竖封面是 hash 命名、无扩展名的文件
        WritePngHeader(@"appcache\librarycache\730\af9ec9ab5d6a7a2818799f340e28a020b8ef8f52", 600, 900);
        Assert.AreEqual(
            Abs(@"appcache\librarycache\730\af9ec9ab5d6a7a2818799f340e28a020b8ef8f52"),
            NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_HashedImages_PicksClosestToPortraitAspect()
    {
        string hashPortrait = @"appcache\librarycache\730\aaaa0000000000000000000000000000000001";
        string hashOtherPortrait = @"appcache\librarycache\730\aaaa0000000000000000000000000000000002";
        string hashLandscape = @"appcache\librarycache\730\bbbb0000000000000000000000000000000001";
        string hashSquare = @"appcache\librarycache\730\cccc0000000000000000000000000000000001";
        WritePngHeader(hashPortrait, 600, 900);      // 0.667
        WritePngHeader(hashOtherPortrait, 960, 1280); // 0.75，稍差
        WritePngHeader(hashLandscape, 920, 430);
        WritePngHeader(hashSquare, 184, 184); // 方形 logo，应被跳过
        Assert.AreEqual(Abs(hashPortrait), NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_HashScan_IgnoresNonImageFile()
    {
        // 目录里有真实图片 + 纯文本伪文件，伪文件必须被跳过
        WritePngHeader(@"appcache\librarycache\730\hashreal00000000000000000000000000000001", 600, 900);
        WriteFile(@"appcache\librarycache\730\hashfake00000000000000000000000000000001");
        Assert.AreEqual(
            Abs(@"appcache\librarycache\730\hashreal00000000000000000000000000000001"),
            NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_HashScan_OnlyLandscape_ReturnsNull()
    {
        WritePngHeader(@"appcache\librarycache\730\hash0000000000000000000000000000000000001", 920, 430);
        Assert.IsNull(NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Portrait_HashScan_SameAspect_PrefersHigherResolution()
    {
        // 官方竖版同 2:3 比例多档（300x450 / 600x900），宽高比打平时应选更高分辨率
        string small = @"appcache\librarycache\730\aaaa0000000000000000000000000000000001";
        string big = @"appcache\librarycache\730\bbbb0000000000000000000000000000000001";
        WritePngHeader(small, 300, 450);
        WritePngHeader(big, 600, 900);
        Assert.AreEqual(Abs(big), NewResolver().GetPortraitPath(730));
    }

    [TestMethod]
    public void Wide_HashedImages_PicksWide()
    {
        string hashPortrait = @"appcache\librarycache\730\aaaa0000000000000000000000000000000001";
        string hashCapsule = @"appcache\librarycache\730\bbbb0000000000000000000000000000000001";
        string hashHero = @"appcache\librarycache\730\bbbb0000000000000000000000000000000002";
        WritePngHeader(hashPortrait, 600, 900);
        WritePngHeader(hashHero, 3840, 1240);
        WritePngHeader(hashCapsule, 920, 430); // 最接近胶囊宽高比，应优先
        Assert.AreEqual(Abs(hashCapsule), NewResolver().GetWidePath(730));
    }

    // ── 横版/后备 (GetWidePath) ──

    [TestMethod]
    public void Wide_CustomGridWide_Found()
    {
        WriteFile(@"userdata\76561198000000000\config\grid\730.png");
        Assert.AreEqual(Abs(@"userdata\76561198000000000\config\grid\730.png"), NewResolver().GetWidePath(730));
    }

    [TestMethod]
    public void Wide_OfficialCapsuleFolder_Found()
    {
        WriteFile(@"appcache\librarycache\730\library_capsule.jpg");
        Assert.AreEqual(Abs(@"appcache\librarycache\730\library_capsule.jpg"), NewResolver().GetWidePath(730));
    }

    [TestMethod]
    public void Wide_HeroFallback_Found()
    {
        WriteFile(@"appcache\librarycache\730_library_hero.jpg");
        Assert.AreEqual(Abs(@"appcache\librarycache\730_library_hero.jpg"), NewResolver().GetWidePath(730));
    }

    [TestMethod]
    public void Wide_OnlyPortraitPresent_ReturnsNull()
    {
        WriteFile(@"appcache\librarycache\730\library_600x900.jpg");
        Assert.IsNull(NewResolver().GetWidePath(730));
    }

    [TestMethod]
    public void Wide_PortraitAndWideCoexist_PortraitPreferred()
    {
        WriteFile(@"appcache\librarycache\730\library_600x900.jpg");
        WriteFile(@"appcache\librarycache\730\library_capsule.jpg");
        var resolver = NewResolver();
        Assert.AreEqual(Abs(@"appcache\librarycache\730\library_600x900.jpg"), resolver.GetPortraitPath(730));
        Assert.AreEqual(Abs(@"appcache\librarycache\730\library_capsule.jpg"), resolver.GetWidePath(730));
    }
}
