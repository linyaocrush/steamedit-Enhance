#nullable enable
using System.IO;
using System.Linq;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;
using steamedit_Enhance.Data.Impl;

namespace steamedit_EnhanceTests;

/// <summary>
/// 端到端保存/重载测试：覆盖 0x07564429 的原生字符串表写入路径，
/// 并验证热路径属性与非热路径属性在完整 保存 → 加载 后都正确。
/// </summary>
[TestClass]
public class AppInfoSaveRoundTripTests
{
    private sealed class TestDisplaySettings : IEntryDisplaySettings
    {
        public string SteamDir => @"C:\Steam";
        public bool UseCompleted => false;
        public string CompletedSuffix => " ✓";
        public bool UseHidden => false;
    }

    private sealed class TestEntry : AppInfoEntry_0x07564429
    {
        public TestEntry(int id, PropertyTable properties)
        {
            ID = id;
            DisplaySettings = new TestDisplaySettings();
            _stuffBeforeHash = new byte[16];
            _properties = properties;
        }
    }

    private sealed class TestAppInfo : AppInfo_0x07564429
    {
        public void Add(IAppInfoEntry entry) => _entries.Add((AppInfoEntry_0x07564429)entry);
    }

    private static PropertyTable BuildEntryTable(string name, string description)
    {
        var appinfo = new PropertyTable();
        var common = new PropertyTable();
        common.AddPropertyValue("name", PropertyType.String, name);
        common.AddPropertyValue("type", PropertyType.String, "game");
        appinfo.AddPropertyValue("common", PropertyType.Table, common);
        var config = new PropertyTable();
        config.AddPropertyValue("installdir", PropertyType.String, name.Replace(" ", ""));
        appinfo.AddPropertyValue("config", PropertyType.Table, config);
        var extended = new PropertyTable();
        extended.AddPropertyValue("description", PropertyType.String, description);
        appinfo.AddPropertyValue("extended", PropertyType.Table, extended);
        var root = new PropertyTable();
        root.AddPropertyValue("appinfo", PropertyType.Table, appinfo);
        return root;
    }

    [TestMethod]
    public void Save_And_Reload_0x07564429_RoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"steamedit-save-{System.Guid.NewGuid():N}.vdf");
        File.WriteAllBytes(path, new byte[16]); // Save 使用 File.Replace，目标需已存在
        try
        {
            var appInfo = new TestAppInfo();
            appInfo.Add(new TestEntry(730, BuildEntryTable("Counter-Strike 2", "desc-cs2")));
            appInfo.Add(new TestEntry(570, BuildEntryTable("Dota Two", "desc-dota")));
            appInfo.Save(path, null);

            var loaded = AppInfoFactory.Load(path);
            var entries = loaded.Entries.ToList();
            Assert.AreEqual(2, entries.Count);

            var cs2 = loaded.FindEntry(730);
            Assert.IsNotNull(cs2);
            Assert.AreEqual("Counter-Strike 2", cs2.Name);
            Assert.AreEqual(AppInfoEntryType.Game, cs2.Type);
            // 非热路径属性：需要物化完整表才能读到
            Assert.AreEqual("desc-cs2", cs2.GetPropertyValue<string>(null!, new[] { "appinfo", "extended", "description" }));

            var dota = loaded.FindEntry(570);
            Assert.IsNotNull(dota);
            Assert.AreEqual("Dota Two", dota.Name);
            Assert.AreEqual("desc-dota", dota.GetPropertyValue<string>(null!, new[] { "appinfo", "extended", "description" }));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
