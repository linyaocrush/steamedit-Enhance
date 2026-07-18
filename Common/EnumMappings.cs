#nullable enable
using System.Collections.Generic;

namespace Common;

internal static class EnumMappings
{
	private static readonly Dictionary<string, string> LangSearchDisplayNames = new()
	{
		["zh-CN"] = "中文名",
		["zh-TW"] = "繁體中文名",
		["en"] = "English name",
	};

	private static readonly Dictionary<string, string> LangAiDisplayNames = new()
	{
		["zh-CN"] = "Simplified Chinese",
		["zh-TW"] = "Traditional Chinese",
		["en"] = "English",
	};

	private static readonly Dictionary<string, string> SearchEngineUrls = new()
	{
		["Google"] = "https://www.google.com/search?q=",
		["Bing"] = "https://www.bing.com/search?q=",
		["Baidu"] = "https://www.baidu.com/s?wd=",
		["DuckDuckGo"] = "https://duckduckgo.com/?q=",
		["StartPage"] = "https://www.startpage.com/do/dsearch?query=",
	};

	private static readonly Dictionary<int, string> LanguageCodeByIndex = new()
	{
		[1] = "zh-CN",
		[2] = "zh-TW",
	};

	public static string GetSearchDisplayName(string langCode)
		=> LangSearchDisplayNames.TryGetValue(langCode, out var name) ? name : "translated name";

	public static string GetAiDisplayName(string langCode)
		=> LangAiDisplayNames.TryGetValue(langCode, out var name) ? name : langCode;

	public static string GetSearchEngineUrl(string engineName)
		=> SearchEngineUrls.TryGetValue(engineName, out var url) ? url : "https://www.google.com/search?q=";

	public static string GetLanguageCodeByIndex(int index)
		=> LanguageCodeByIndex.TryGetValue(index, out var code) ? code : "en";
}
