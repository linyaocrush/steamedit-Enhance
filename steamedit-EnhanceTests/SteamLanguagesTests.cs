#nullable enable
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace steamedit_EnhanceTests;

[TestClass]
public class SteamLanguagesTests
{
	[TestMethod]
	public void FromCultureName_SimplifiedChineseVariants_MapsToSchinese()
	{
		Assert.AreEqual(SteamLanguages.SimplifiedChinese, SteamLanguages.FromCultureName("zh-CN"));
		Assert.AreEqual(SteamLanguages.SimplifiedChinese, SteamLanguages.FromCultureName("zh-SG"));
		Assert.AreEqual(SteamLanguages.SimplifiedChinese, SteamLanguages.FromCultureName("zh"));
	}

	[TestMethod]
	public void FromCultureName_TraditionalChineseVariants_MapsToTchinese()
	{
		Assert.AreEqual(SteamLanguages.TraditionalChinese, SteamLanguages.FromCultureName("zh-TW"));
		Assert.AreEqual(SteamLanguages.TraditionalChinese, SteamLanguages.FromCultureName("zh-HK"));
		Assert.AreEqual(SteamLanguages.TraditionalChinese, SteamLanguages.FromCultureName("zh-Hant"));
	}

	[TestMethod]
	public void FromCultureName_EnglishOrOther_ReturnsNull()
	{
		Assert.IsNull(SteamLanguages.FromCultureName("en-US"));
		Assert.IsNull(SteamLanguages.FromCultureName("en"));
		Assert.IsNull(SteamLanguages.FromCultureName("de-DE"));
		Assert.IsNull(SteamLanguages.FromCultureName(""));
		Assert.IsNull(SteamLanguages.FromCultureName(null));
	}

	[TestMethod]
	public void ForCurrentUiCulture_AlwaysReturnsEnglishWhenNotChinese()
	{
		var old = System.Globalization.CultureInfo.CurrentUICulture;
		try
		{
			System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
			Assert.AreEqual(SteamLanguages.English, SteamLanguages.ForCurrentUiCulture());
		}
		finally { System.Globalization.CultureInfo.CurrentUICulture = old; }
	}
}
