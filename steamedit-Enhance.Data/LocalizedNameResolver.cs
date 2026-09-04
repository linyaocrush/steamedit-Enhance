#nullable enable
using System;
using Common;

namespace steamedit_Enhance.Data;

/// <summary>
/// 游戏列表“默认名”的显示层解析器（纯显示，绝不修改 appinfo 数据）：
///   1. 用户手动改过名（name 属性处于已修改状态）→ 显示用户自定义名，其余规则不生效；
///   2. 界面为中文时优先取 appinfo 自带 name_localized 的官方本地化名；
///   3. 缺则取本地官方名称缓存（联网批量获取的结果）；
///   4. 仍没有 → 回退当前 appinfo 里的名字（英文默认名）。
/// 界面为英文（或其它非中文）时不做替换。
/// </summary>
internal static class LocalizedNameResolver
{
	public static string SteamLocaleForCurrentUi() => SteamLanguages.ForCurrentUiCulture();

	/// <summary>判断条目的 name 是否被用户手动修改（用于 AI 翻译/批量改名/手输改名等写盘操作）。</summary>
	public static bool IsUserRenamed(IAppInfoEntry entry)
		=> entry.Modification?.Changes != null && entry.Modification.Changes.HasProperty(AppInfoPaths.Name);

	/// <summary>从 appinfo 的 common/name_localized 读取某语言的官方本地化名；不存在返回 null。</summary>
	public static string? TryGetLocalizedFromAppInfo(IAppInfoEntry entry, string locale)
	{
		if (string.IsNullOrEmpty(locale)) return null;
		try
		{
			var table = entry.GetPropertyValue<PropertyTable>(null!, AppInfoPaths.NameLocalized);
			if (table == null) return null;
			return table.TryGetPropertyValue<string>(locale, out var name) && !string.IsNullOrWhiteSpace(name)
				? name
				: null;
		}
		catch (Exception ex)
		{
			Log.WriteLine("LocalizedNameResolver: read name_localized failed for {0:X8}: {1}", entry.ID, ex.Message);
			return null;
		}
	}

	/// <summary>解析列表应显示的名字（使用进程共享缓存）。</summary>
	public static string ResolveListName(IAppInfoEntry entry)
		=> ResolveListName(entry, LocalizedNameCache.Default);

	public static string ResolveListName(IAppInfoEntry entry, LocalizedNameCache cache)
	{
		string fallback = entry.Name ?? "";
		try
		{
			string? official = TryGetLocalizedOfficialName(entry, cache);
			return string.IsNullOrEmpty(official) ? fallback : official;
		}
		catch (Exception ex)
		{
			Log.WriteLine("LocalizedNameResolver: resolve failed for {0:X8}: {1}", entry.ID, ex.Message);
			return fallback;
		}
	}

	/// <summary>
	/// 取“应写入/显示的官方本地化默认名”：
	///   用户手动改过名 → null；界面为英文等非中文 → null（保持英文默认名）；
	///   否则依次取 appinfo name_localized、本地官方名称缓存；两者皆无 → null。
	/// </summary>
	public static string? TryGetLocalizedOfficialName(IAppInfoEntry entry, LocalizedNameCache? cache = null)
	{
		if (IsUserRenamed(entry)) return null;
		string locale = SteamLanguages.ForCurrentUiCulture();
		if (locale == SteamLanguages.English) return null;

		string? official = TryGetLocalizedFromAppInfo(entry, locale);
		if (string.IsNullOrWhiteSpace(official))
			official = (cache ?? LocalizedNameCache.Default).Get(entry.ID, locale);

		return string.IsNullOrWhiteSpace(official) ? null : official;
	}
}
