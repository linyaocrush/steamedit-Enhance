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
