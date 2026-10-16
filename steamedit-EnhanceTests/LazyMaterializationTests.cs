#nullable enable
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Common;
using steamedit_Enhance.Data;
using steamedit_Enhance.Data.Impl;

namespace steamedit_EnhanceTests;

/// <summary>
/// 惰性物化行为测试：加载时只建“摘要表”（热路径），热路径读取不物化完整表，
/// 非热路径访问才物化；且还原 base_name/base_type、修改、原始数据读取都保持一致。
/// </summary>
[TestClass]
public class LazyMaterializationTests
{
    private sealed class ProbeEntry : AppInfoEntry_0x07564427
    {
        public bool IsMaterialized => _properties != null;
        public bool HasSummary => Summary != null;
    }

    private sealed class TestDisplaySettings : IEntryDisplaySettings
    {
        public string SteamDir => @"C:\Steam";
        public bool UseCompleted => false;
        public string CompletedSuffix => " ✓";
        public bool UseHidden => false;
    }

    private static PropertyTable BuildTable()
    {
        var appinfo = new PropertyTable();

        var common = new PropertyTable();
        common.AddPropertyValue("name", PropertyType.String, "Test Game");
        common.AddPropertyValue("type", PropertyType.String, "game");
        var localized = new PropertyTable();
        localized.AddPropertyValue("schinese", PropertyType.String, "测试游戏");
        common.AddPropertyValue("name_localized", PropertyType.Table, localized);
        appinfo.AddPropertyValue("common", PropertyType.Table, common);

        var config = new PropertyTable();
        config.AddPropertyValue("installdir", PropertyType.String, "TestGame");
        var launch = new PropertyTable();
        var launch0 = new PropertyTable();
        launch0.AddPropertyValue("executable", PropertyType.String, "game.exe");
        launch0.AddPropertyValue("arguments", PropertyType.String, "-foo");
        launch.AddPropertyValue("0", PropertyType.Table, launch0);
        config.AddPropertyValue("launch", PropertyType.Table, launch);
        appinfo.AddPropertyValue("config", PropertyType.Table, config);

        var steamEdit = new PropertyTable();
        steamEdit.AddPropertyValue("base_name", PropertyType.String, "Base Name");
        steamEdit.AddPropertyValue("is_hidden", PropertyType.Int32, 1);
        appinfo.AddPropertyValue("steam_edit", PropertyType.Table, steamEdit);

        // Not on any hot path → only reachable after full materialization.
        var extended = new PropertyTable();
        extended.AddPropertyValue("description", PropertyType.String, "Long description");
        appinfo.AddPropertyValue("extended", PropertyType.Table, extended);

        var root = new PropertyTable();
        root.AddPropertyValue("appinfo", PropertyType.Table, appinfo);
        return root;
    }

    private static ProbeEntry Load()
    {
        byte[] tableBytes = PropertyTableIO.WritePropertyTable_0x00000000(BuildTable());
        using var entryData = new MemoryStream();
        using (var w = new BinaryWriter(entryData, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(new byte[16]); // stuff before hash
            w.Write(new byte[20]); // sha1
            w.Write(1u);           // change number
            w.Write(tableBytes);
        }

        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(12345);                   // app id
            w.Write((int)entryData.Length);   // entry data size
            w.Write(entryData.ToArray());
        }
        stream.Position = 0;

        using var reader = new BinaryReader(stream);
        var entry = AppInfoEntryBase.ReadFromReader<ProbeEntry>(reader)!;
        entry.DisplaySettings = new TestDisplaySettings();
        return entry;
    }

    [TestMethod]
    public void Load_BuildsSummaryOnly()
    {
        var entry = Load();
        Assert.IsTrue(entry.HasSummary, "加载应建立摘要表。");
        Assert.IsFalse(entry.IsMaterialized, "加载不应物化完整属性表。");
    }

    [TestMethod]
    public void HotReads_DoNotMaterialize()
    {
        var entry = Load();
        Assert.AreEqual("Base Name", entry.Name);            // base_name 已还原到 name
        Assert.AreEqual(AppInfoEntryType.Game, entry.Type);
        Assert.IsTrue(entry.IsHidden);
        Assert.IsFalse(entry.IsCompleted);
        Assert.AreEqual("TestGame", entry.GetPropertyValue<string>(null!, AppInfoPaths.InstallDir));
        Assert.IsFalse(entry.IsMaterialized, "读取热路径属性不应触发物化。");
    }

    [TestMethod]
    public void ColdRead_Materializes_AndReturnsValues()
    {
        var entry = Load();
        Assert.IsFalse(entry.IsMaterialized);
        string? description = entry.GetPropertyValue<string>(null!, new[] { "appinfo", "extended", "description" });
        Assert.AreEqual("Long description", description);
        Assert.IsTrue(entry.IsMaterialized, "读取非热路径属性应物化完整表。");
        // 物化后热路径仍然一致
        Assert.AreEqual("Base Name", entry.Name);
        Assert.AreEqual(AppInfoEntryType.Game, entry.Type);
    }

    [TestMethod]
    public void OriginalData_IsRawTable_NotRestored()
    {
        var entry = Load();
        byte[] original = entry.GetOriginalDataCopy();
        using var ms = new MemoryStream(original);
        using var br = new BinaryReader(ms);
        PropertyTable raw = PropertyTableIO.ReadPropertyTable_0x00000000(br);
        // 原始数据保存的是“未还原”的解析结果：name 仍是文件里的名字，base_name 仍在。
        Assert.AreEqual("Test Game", raw.GetPropertyValue<string>(null!, AppInfoPaths.Name));
        Assert.AreEqual("Base Name", raw.GetPropertyValue<string>(null!, AppInfoPaths.BaseName));
    }

    [TestMethod]
    public void Modification_Materializes_AndIsRecorded()
    {
        var entry = Load();
        Assert.IsFalse(entry.IsMaterialized);

        entry.Name = "Renamed";

        Assert.IsTrue(entry.IsMaterialized, "修改应物化完整表。");
        Assert.IsTrue(entry.IsModified);
        Assert.AreEqual("Renamed", entry.Name);
        Assert.AreEqual("Renamed", entry.GetPropertyValue<string>(null!, AppInfoPaths.Name));
        Assert.AreEqual("Test Game", entry.GetOriginalPropertyValue(AppInfoPaths.Name));
    }

    [TestMethod]
    public void LaunchTable_ReadAfterMaterialization()
    {
        var entry = Load();
        var launches = entry.Launches.Items.ToList();
        Assert.AreEqual(1, launches.Count);
        Assert.AreEqual("game.exe", launches[0].Executable);
        Assert.AreEqual("-foo", launches[0].Arguments);
    }
}
