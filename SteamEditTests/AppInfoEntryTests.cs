using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamEdit.Data;
using SteamEdit.Data.Impl;

namespace SteamEditTests;

[TestClass]
public class AppInfoEntryTests
{
    private class TestEntry : AppInfoEntryBase
    {
        public override uint AppInfoVersion => 0x07564428u;

        public TestEntry()
        {
            _properties = new PropertyTable();
            var appinfo = new PropertyTable();
            var common = new PropertyTable();
            common.AddPropertyValue("name", PropertyType.String, "Test Game");
            common.AddPropertyValue("type", PropertyType.String, "game");
            appinfo.AddPropertyValue("common", PropertyType.Table, common);
            _properties.AddPropertyValue("appinfo", PropertyType.Table, appinfo);
            _originalData = PropertyTableIO.WritePropertyTable_Mods(_properties);
            RestoreBaseProperties();
        }
    }

    [TestMethod]
    public void Name_Get_ReturnsValue()
    {
        var entry = new TestEntry();
        Assert.AreEqual("Test Game", entry.Name);
    }

    [TestMethod]
    public void Name_Set_UpdatesValue()
    {
        var entry = new TestEntry();
        entry.Name = "Renamed";
        Assert.AreEqual("Renamed", entry.Name);
    }

    [TestMethod]
    public void SortAs_FallsBackToName()
    {
        var entry = new TestEntry();
        Assert.AreEqual("Test Game", entry.SortAs);
    }

    [TestMethod]
    public void Type_Get_ReturnsGame()
    {
        var entry = new TestEntry();
        Assert.AreEqual(AppInfoEntryType.Game, entry.Type);
    }

    [TestMethod]
    public void Type_Set_UpdatesValue()
    {
        var entry = new TestEntry();
        entry.Type = AppInfoEntryType.Tool;
        Assert.AreEqual(AppInfoEntryType.Tool, entry.Type);
    }

    [TestMethod]
    public void IsHidden_DefaultFalse()
    {
        var entry = new TestEntry();
        Assert.IsFalse(entry.IsHidden);
    }

    [TestMethod]
    public void IsHidden_SetTrue_Works()
    {
        var entry = new TestEntry();
        entry.IsHidden = true;
        Assert.IsTrue(entry.IsHidden);
    }

    [TestMethod]
    public void IsCompleted_SetTrue_Works()
    {
        var entry = new TestEntry();
        entry.IsCompleted = true;
        Assert.IsTrue(entry.IsCompleted);
    }

    [TestMethod]
    public void IsModified_DefaultFalse()
    {
        var entry = new TestEntry();
        Assert.IsFalse(entry.IsModified);
    }

    [TestMethod]
    public void ToString_ReturnsName()
    {
        var entry = new TestEntry();
        Assert.AreEqual("Test Game", entry.ToString());
    }
}
