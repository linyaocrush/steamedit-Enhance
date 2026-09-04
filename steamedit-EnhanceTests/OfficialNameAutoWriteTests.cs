#nullable enable
using System;
using System.Globalization;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;
using steamedit_Enhance.Data.Impl;

namespace steamedit_EnhanceTests;

/// <summary>
/// 保存 appinfo 时“自动写入官方本地化名但不计为已编辑”的行为测试。
/// </summary>
[TestClass]
public class OfficialNameAutoWriteTests
{
	private sealed class TestEntry : AppInfoEntryBase
	{
		public override uint AppInfoVersion => 0x07564428u;

		public TestEntry(string name, PropertyTable? nameLocalized = null, bool completed = false)
		{
			DisplaySettings = new DisabledDisplaySettings();
			ID = 999;
			_properties = new PropertyTable();
			var appinfo = new PropertyTable();
			var common = new PropertyTable();
			common.AddPropertyValue("name", PropertyType.String, name);
			common.AddPropertyValue("type", PropertyType.String, "game");
			if (nameLocalized != null)
				common.AddPropertyValue("name_localized", PropertyType.Table, nameLocalized);
			appinfo.AddPropertyValue("common", PropertyType.Table, common);
			_properties.AddPropertyValue("appinfo", PropertyType.Table, appinfo);
			if (completed)
				_properties.SetPropertyValue(PropertyType.Int32, 1, AppInfoPaths.IsCompleted);
		}

		public PropertyTable PrepareWriteTableForTest(out PropertyTable common)
			=> PrepareWriteTable(out common);

		public void MarkUserRenamed(string newName)
		{
			var changes = new PropertyTable();
			var appinfo = new PropertyTable();
			var common = new PropertyTable();
			common.AddPropertyValue("name", PropertyType.String, newName);
			appinfo.AddPropertyValue("common", PropertyType.Table, common);
			changes.AddPropertyValue("appinfo", PropertyType.Table, appinfo);
			Modification = new ModifiedEntry(ID) { Changes = changes };
		}
	}

	private class DisabledDisplaySettings : IEntryDisplaySettings
	{
		public string SteamDir => @"C:\Program Files (x86)\Steam";
		public bool UseCompleted => false;
		public string CompletedSuffix => " ✓";
		public bool UseHidden => false;
	}

	private sealed class AllEnabledDisplaySettings : IEntryDisplaySettings
	{
		public string SteamDir => @"C:\Program Files (x86)\Steam";
		public bool UseCompleted => true;
		public string CompletedSuffix => " ✓";
		public bool UseHidden => false;
	}

	private static PropertyTable Localized(params (string Lang, string Name)[] entries)
	{
		var t = new PropertyTable();
		foreach (var (lang, name) in entries)
			t.AddPropertyValue(lang, PropertyType.String, name);
		return t;
	}

	private static void WithUi(string culture, Action action)
	{
		CultureInfo old = CultureInfo.CurrentUICulture;
		try
		{
			CultureInfo.CurrentUICulture = new CultureInfo(culture);
			action();
		}
		finally { CultureInfo.CurrentUICulture = old; }
	}

	[TestMethod]
	public void Write_ChineseUi_Unmodified_WritesOfficialNameWithoutModification()
	{
		WithUi("zh-CN", () =>
		{
			var entry = new TestEntry("Counter-Strike 2", Localized(("english", "Counter-Strike 2"), ("schinese", "反恐精英2")));
			var table = entry.PrepareWriteTableForTest(out var common);

			Assert.AreEqual("反恐精英2", common.GetPropertyValue("name", ""));
			// base_name 仍是原名（作为基线保留，不计为编辑）
			Assert.AreEqual("Counter-Strike 2", table.GetPropertyValue<string>(null!, AppInfoPaths.BaseName));
			Assert.IsNull(entry.Modification);
			Assert.IsFalse(entry.IsModified);
		});
	}

	[TestMethod]
	public void Write_ChineseUi_UserRenamed_KeepsCustomName()
	{
		WithUi("zh-CN", () =>
		{
			var entry = new TestEntry("Counter-Strike 2", Localized(("schinese", "反恐精英2")));
			entry.MarkUserRenamed("我的CS2");

			entry.PrepareWriteTableForTest(out var common);
			Assert.AreEqual("我的CS2", common.GetPropertyValue("name", ""));
		});
	}

	[TestMethod]
	public void Write_EnglishUi_NoAutoRewrite()
	{
		WithUi("en-US", () =>
		{
			var entry = new TestEntry("Counter-Strike 2", Localized(("schinese", "反恐精英2")));
			entry.PrepareWriteTableForTest(out var common);
			Assert.AreEqual("Counter-Strike 2", common.GetPropertyValue("name", ""));
		});
	}

	[TestMethod]
	public void Write_ChineseUi_NoLocalizedName_KeepsCurrentName()
	{
		WithUi("zh-CN", () =>
		{
			var entry = new TestEntry("Some English Title");
			entry.PrepareWriteTableForTest(out var common);
			Assert.AreEqual("Some English Title", common.GetPropertyValue("name", ""));
		});
	}

	[TestMethod]
	public void Write_ChineseUi_Completed_AppendsCheckmarkToOfficialName()
	{
		WithUi("zh-CN", () =>
		{
			var entry = new TestEntry("Game", Localized(("schinese", "官方中文")), completed: true);
			entry.DisplaySettings = new AllEnabledDisplaySettings();

			entry.PrepareWriteTableForTest(out var common);
			Assert.AreEqual("官方中文 ✓", common.GetPropertyValue("name", ""));
		});
	}
}
