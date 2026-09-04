#nullable enable
using System;
using System.Globalization;

namespace Common;

/// <summary>
/// 软件界面语言 (CultureInfo) 与 Steam 官方名称语言码（appinfo name_localized / store API）的映射。
/// 本应用界面只支持 en / zh-CN / zh-TW；其余语言一律按英文名显示。
/// </summary>
internal static class SteamLanguages
{
	public const string English = "english";
	public const string SimplifiedChinese = "schinese";
	public const string TraditionalChinese = "tchinese";

	/// <summary>返回当前界面语言对应的 Steam 语言码，永远不为 null（非中文一律 english）。</summary>
	public static string ForCurrentUiCulture()
		=> FromCultureName(CultureInfo.CurrentUICulture.Name) ?? English;

	/// <summary>把形如 "zh-CN"/"zh-TW"/"en-US" 的 culture 名映射到 Steam 语言码；不支持时返回 null。</summary>
	public static string? FromCultureName(string? cultureName)
	{
		if (string.IsNullOrEmpty(cultureName)) return null;
		string n = cultureName.ToLowerInvariant();
		if (n.StartsWith("zh", StringComparison.Ordinal))
		{
			// zh-TW / zh-HK / zh-MO / zh-Hant → 繁体；其余中文 → 简体
			if (n.Contains("tw", StringComparison.Ordinal)
				|| n.Contains("hk", StringComparison.Ordinal)
				|| n.Contains("mo", StringComparison.Ordinal)
				|| n.Contains("hant", StringComparison.Ordinal))
			{
				return TraditionalChinese;
			}
			return SimplifiedChinese;
		}
		return null;
	}
}
