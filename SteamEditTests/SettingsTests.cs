using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Common;
using SteamEdit.Data;

namespace SteamEditTests;

[TestClass]
public class SettingsTests
{
    [TestMethod]
    public void SaveAndReload_PreservesValues()
    {
        var settings = Settings.Default;
        string originalLang = settings.Language;
        string originalModel = settings.AiModel;

        try
        {
            settings.Language = "en";
            settings.AiModel = "test-model";
            settings.Save();

            string jsonPath = Path.Combine(AppHelper.GetAppDirectory(), "Settings.json");
            Assert.IsTrue(File.Exists(jsonPath), "Settings.json should exist after save");

            string json = File.ReadAllText(jsonPath);
            Assert.IsTrue(json.Contains("\"Language\": \"en\""), "JSON should contain Language=en");
            Assert.IsTrue(json.Contains("\"AiModel\": \"test-model\""), "JSON should contain AiModel=test-model");
        }
        finally
        {
            settings.Language = originalLang;
            settings.AiModel = originalModel;
            settings.Save();
        }
    }

    [TestMethod]
    public void Save_SkipsWhenNotDirty()
    {
        var settings = Settings.Default;
        string jsonPath = Path.Combine(AppHelper.GetAppDirectory(), "Settings.json");

        // 先保存一次确保文件存在且清除脏标记
        settings.Language = settings.Language;
        settings.Save();

        if (File.Exists(jsonPath))
        {
            DateTime lastWrite = File.GetLastWriteTime(jsonPath);
            Thread.Sleep(100);
            settings.Save();

            if (File.Exists(jsonPath))
            {
                DateTime newWrite = File.GetLastWriteTime(jsonPath);
                Assert.AreEqual(lastWrite, newWrite, "Save should skip writing when not dirty");
            }
        }
    }

    [TestMethod]
    public void EnumProperty_FlagsRoundtrip()
    {
        var settings = Settings.Default;
        var originalFilters = settings.Filters;

        try
        {
            settings.Filters = AppInfoEntryType.Game | AppInfoEntryType.DLC;
            settings.Save();

            string jsonPath = Path.Combine(AppHelper.GetAppDirectory(), "Settings.json");
            string json = File.ReadAllText(jsonPath);
            Assert.IsTrue(json.Contains("Game"), "JSON should contain 'Game' enum name");
            Assert.IsTrue(json.Contains("DLC"), "JSON should contain 'DLC' enum name");
        }
        finally
        {
            settings.Filters = originalFilters;
            settings.Save();
        }
    }

    [TestMethod]
    public void CorruptedJson_DoesNotCrash()
    {
        string jsonPath = Path.Combine(AppHelper.GetAppDirectory(), "Settings.json");
        string backup = null;

        try
        {
            if (File.Exists(jsonPath))
            {
                backup = jsonPath + ".testbak";
                File.Copy(jsonPath, backup, overwrite: true);
            }

            // 写入损坏的 JSON
            File.WriteAllText(jsonPath, "{ invalid json content }");

            // 使用反射创建新实例来测试损坏 JSON 的加载（Settings.Default 是已初始化的单例）
            var ctor = typeof(Settings).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.IsNotNull(ctor, "Settings should have a private parameterless constructor");
            var freshSettings = ctor.Invoke(null);

            // 损坏的 JSON 不应导致异常，属性应保留默认值
            var langProp = typeof(Settings).GetProperty("Language");
            Assert.IsNotNull(langProp, "Language property should exist");
            string lang = (string)langProp.GetValue(freshSettings);
            Assert.IsNotNull(lang, "Language should have a default value (empty string)");
        }
        finally
        {
            if (backup != null && File.Exists(backup))
            {
                File.Copy(backup, jsonPath, overwrite: true);
                File.Delete(backup);
            }
        }
    }
}
