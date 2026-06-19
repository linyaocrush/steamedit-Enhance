using System.IO;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamEditTests;

[TestClass]
public class TranslationGlossaryTests
{
    private string _tempDir;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "glossary_test_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        TranslationGlossary.Initialize(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [TestMethod]
    public void FindTranslated_NotFound_ReturnsNull()
    {
        Assert.IsNull(TranslationGlossary.FindTranslatedByOriginalName("NonExistent"));
    }

    [TestMethod]
    public void UpdateEntry_AddsNewEntry()
    {
        TranslationGlossary.UpdateEntry(12345, "The Witcher", "巫师");
        Assert.AreEqual("巫师", TranslationGlossary.FindTranslatedByOriginalName("The Witcher"));
    }

    [TestMethod]
    public void UpdateEntry_IgnoresSameName()
    {
        TranslationGlossary.UpdateEntry(12345, "Game", "Game");
        Assert.IsNull(TranslationGlossary.FindTranslatedByOriginalName("Game"));
    }

    [TestMethod]
    public void UpdateEntry_IgnoresBlank()
    {
        TranslationGlossary.UpdateEntry(12345, "", "Translated");
        Assert.IsNull(TranslationGlossary.FindTranslatedByOriginalName(""));
    }

    [TestMethod]
    public void FindTranslated_CaseInsensitive()
    {
        TranslationGlossary.UpdateEntry(1, "Portal", "传送门");
        Assert.AreEqual("传送门", TranslationGlossary.FindTranslatedByOriginalName("portal"));
        Assert.AreEqual("传送门", TranslationGlossary.FindTranslatedByOriginalName("PORTAL"));
    }

    [TestMethod]
    public void ToPromptString_ReturnsFormattedLines()
    {
        TranslationGlossary.UpdateEntry(1, "Game A", "游戏A");
        TranslationGlossary.UpdateEntry(2, "Game B", "游戏B");
        var result = TranslationGlossary.ToPromptString();
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("Game A -> 游戏A"));
        Assert.IsTrue(result.Contains("Game B -> 游戏B"));
    }
}
