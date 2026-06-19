using System;
using System.IO;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamEditTests;

[TestClass]
public class AppHelperTests
{
    // HasArgument starts at index 1 (skipping program name at index 0),
    // so all test arrays need a dummy first element.

    [TestMethod]
    public void HasArgument_Present_ReturnsTrue()
    {
        Assert.IsTrue(AppHelper.HasArgument("autofix", new[] { "app.exe", "-autofix" }));
    }

    [TestMethod]
    public void HasArgument_Absent_ReturnsFalse()
    {
        Assert.IsFalse(AppHelper.HasArgument("autofix", new[] { "app.exe", "-other" }));
    }

    [TestMethod]
    public void HasArgument_DoubleDash_StopsSearch()
    {
        // "autofix" appears after "--", should not be found
        Assert.IsFalse(AppHelper.HasArgument("autofix", new[] { "app.exe", "--", "-autofix" }));
    }

    [TestMethod]
    public void HasArgument_CaseInsensitive()
    {
        Assert.IsTrue(AppHelper.HasArgument("autofix", new[] { "app.exe", "-AutoFix" }));
    }

    [TestMethod]
    public void HasArgument_DoubleDashPrefix_Works()
    {
        Assert.IsTrue(AppHelper.HasArgument("forcestart", new[] { "app.exe", "--forcestart" }));
    }

    [TestMethod]
    public void HasArgument_SlashPrefix_Works()
    {
        Assert.IsTrue(AppHelper.HasArgument("silent", new[] { "app.exe", "/silent" }));
    }

    [TestMethod]
    public void HasArgument_EmptyArgs_ReturnsFalse()
    {
        Assert.IsFalse(AppHelper.HasArgument("autofix", new[] { "app.exe" }));
    }

    [TestMethod]
    public void HasArgument_BareToken_NoPrefix_Skipped()
    {
        // A token with no prefix at all should be skipped
        Assert.IsFalse(AppHelper.HasArgument("autofix", new[] { "app.exe", "autofix" }));
    }

    [TestMethod]
    public void HasArgument_MultipleArgs_FindsCorrectOne()
    {
        Assert.IsTrue(AppHelper.HasArgument("forcestart", new[] { "app.exe", "-autofix", "--forcestart" }));
    }

    [TestMethod]
    public void HasArgument_DashAlone_MatchesEmptyTag()
    {
        // A single "-" with empty tag: stripping "-" yields "", which matches ""
        Assert.IsTrue(AppHelper.HasArgument("", new[] { "app.exe", "-" }));
    }

    [TestMethod]
    public void HasArgument_DashAlone_DoesNotMatchNonEmptyTag()
    {
        // A single "-" with non-empty tag: stripping "-" yields "", no match
        Assert.IsFalse(AppHelper.HasArgument("autofix", new[] { "app.exe", "-" }));
    }

    [TestMethod]
    public void MakePathRelative_SameDirectory_ReturnsFilename()
    {
        string result = AppHelper.MakePathRelative(
            @"C:\Steam\appinfo.vdf",
            @"C:\Steam");
        Assert.AreEqual("appinfo.vdf", result);
    }

    [TestMethod]
    public void MakePathRelative_SubDirectory_ReturnsRelative()
    {
        string result = AppHelper.MakePathRelative(
            @"C:\Steam\cache\appinfo.vdf",
            @"C:\Steam");
        // Uri-based relative paths use forward slashes
        string expected = "cache/appinfo.vdf";
        Assert.AreEqual(expected, result);
    }
}
