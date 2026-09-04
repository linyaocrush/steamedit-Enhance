#nullable enable
using System;
using System.Globalization;
using System.IO;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

[TestClass]
public class LocalizedNameResolverTests
{
	// 可注入 name_localized 表的假条目
	private sealed class LocalizedEntry : FakeEntryBase
	{
		public PropertyTable? NameLocalized { get; set; }

		public override T GetPropertyValue<T>(T def, params string[] path)
		{
			if (path.Length == 3 && path[0] == "appinfo" && path[1] == "common" && path[2] == "name_localized")
			{
				if (NameLocalized != null && typeof(T) == typeof(PropertyTable))
					return (T)(object)NameLocalized;
				return def;
			}
			return base.GetPropertyValue(def, path);
		}
	}

	private static PropertyTable LocalizedTable(params (string Lang, string Name)[] entries)
	{
		var t = new PropertyTable();
		foreach (var (lang, name) in entries)
			t.AddPropertyValue(lang, PropertyType.String, name);
		return t;
	}

	private static LocalizedEntry Entry(string name, PropertyTable? nameLocalized = null)
		=> new LocalizedEntry { ID = 100, Name = name, NameLocalized = nameLocalized };

	private static LocalizedNameCache NewCache()
		=> new LocalizedNameCache(Path.Combine(Path.GetTempPath(), "official_names_test_" + Guid.NewGuid().ToString("N") + ".json"));

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
	public void EnglishUi_ReturnsStoredName_EvenIfLocalizedExists()
	{
		var entry = Entry("Cyberpunk 2077", LocalizedTable(("schinese", "赛博朋克2077")));
		WithUi("en-US", () =>
			Assert.AreEqual("Cyberpunk 2077", LocalizedNameResolver.ResolveListName(entry, NewCache())));
	}

	[TestMethod]
	public void ChineseUi_UsesSchineseFromNameLocalized()
	{
		var entry = Entry("Cyberpunk 2077", LocalizedTable(("english", "Cyberpunk 2077"), ("schinese", "赛博朋克2077")));
		WithUi("zh-CN", () =>
			Assert.AreEqual("赛博朋克2077", LocalizedNameResolver.ResolveListName(entry, NewCache())));
	}

	[TestMethod]
	public void TraditionalUi_PicksTchinese()
	{
		var entry = Entry("Cyberpunk 2077", LocalizedTable(("schinese", "赛博朋克2077"), ("tchinese", "電馭叛客2077")));
		WithUi("zh-TW", () =>
			Assert.AreEqual("電馭叛客2077", LocalizedNameResolver.ResolveListName(entry, NewCache())));
	}

	[TestMethod]
	public void TraditionalUi_MissingTchinese_FallsBackToEnglishName_NotSchinese()
	{
		var entry = Entry("Cyberpunk 2077", LocalizedTable(("schinese", "赛博朋克2077")));
		WithUi("zh-TW", () =>
			Assert.AreEqual("Cyberpunk 2077", LocalizedNameResolver.ResolveListName(entry, NewCache())));
	}

	[TestMethod]
	public void ChineseUi_UserRenamed_KeepsCustomName()
	{
		var entry = Entry("Custom Name", LocalizedTable(("schinese", "官方中文名")));
		var mod = new ModifiedEntry(entry);
		mod.AddChange(PropertyType.String, "Custom Name", AppInfoPaths.Name);
		entry.Modification = mod;
		WithUi("zh-CN", () =>
			Assert.AreEqual("Custom Name", LocalizedNameResolver.ResolveListName(entry, NewCache())));
	}

	[TestMethod]
	public void ChineseUi_NoLocalized_ReturnsStoredName()
	{
		var entry = Entry("Some English Title");
		WithUi("zh-CN", () =>
			Assert.AreEqual("Some English Title", LocalizedNameResolver.ResolveListName(entry, NewCache())));
	}

	[TestMethod]
	public void ChineseUi_NoLocalized_ButCacheHas_ReturnsCachedName()
	{
		var entry = Entry("Some English Title");
		string path = Path.Combine(Path.GetTempPath(), "localized_cache_test_" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			var cache = new LocalizedNameCache(path);
			cache.Set(100, SteamLanguages.SimplifiedChinese, "某中文名");
			WithUi("zh-CN", () =>
				Assert.AreEqual("某中文名", LocalizedNameResolver.ResolveListName(entry, cache)));
		}
		finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
	}

	[TestMethod]
	public void IsUserRenamed_OnlyWhenNameInChanges()
	{
		var entry = Entry("Original");
		Assert.IsFalse(LocalizedNameResolver.IsUserRenamed(entry));
		var mod = new ModifiedEntry(entry);
		mod.AddChange(PropertyType.String, "Changed", AppInfoPaths.Name);
		entry.Modification = mod;
		Assert.IsTrue(LocalizedNameResolver.IsUserRenamed(entry));

		var other = Entry("Original");
		var mod2 = new ModifiedEntry(other);
		mod2.AddChange(PropertyType.Int32, 1, AppInfoPaths.IsHidden);
		other.Modification = mod2;
		Assert.IsFalse(LocalizedNameResolver.IsUserRenamed(other));
	}
}
