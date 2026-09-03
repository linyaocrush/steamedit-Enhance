#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Common;

namespace steamedit_Enhance.Data;

/// <summary>
/// 在本地 Steam 缓存中查找某个 App 的封面图（只做路径解析，不负责解码/下载）。
/// 解析结果按 AppId + 类别缓存（含“未找到”的负缓存），同一 App 同一会话内不重复扫盘。
/// 优先级约定：竖版封面（600x900 / 自定义 xxxp）优先，横版/横幅仅作为没有竖版时的后备。
/// </summary>
internal sealed class SteamCoverResolver
{
	private static readonly string[] s_extensions = { ".jpg", ".png" };

	private readonly string _steamDir;
	private readonly Dictionary<int, string?> _portraitCache = new();
	private readonly Dictionary<int, string?> _wideCache = new();
	private string[] _userGridDirs = Array.Empty<string>();

	public SteamCoverResolver(string steamDir)
	{
		_steamDir = steamDir ?? "";
	}

	/// <summary>
	/// 竖版封面路径（自定义 xxxp / 官方 library_600x900）。找不到返回 null。
	/// </summary>
	public string? GetPortraitPath(int appId)
	{
		if (appId <= 0 || string.IsNullOrWhiteSpace(_steamDir))
			return null;
		if (_portraitCache.TryGetValue(appId, out string? cached))
			return cached;
		string? path = ResolvePortrait(appId);
		_portraitCache[appId] = path;
		return path;
	}

	/// <summary>
	/// 横版/横幅后备路径（自定义宽胶囊 / 官方 library_capsule / library_hero）。找不到返回 null。
	/// </summary>
	public string? GetWidePath(int appId)
	{
		if (appId <= 0 || string.IsNullOrWhiteSpace(_steamDir))
			return null;
		if (_wideCache.TryGetValue(appId, out string? cached))
			return cached;
		string? path = ResolveWide(appId);
		_wideCache[appId] = path;
		return path;
	}

	private string? ResolvePortrait(int appId)
	{
		string id = appId.ToString();
		foreach (string gridDir in GetUserGridDirs())
		{
			string? custom = FirstExisting(Path.Combine(gridDir, id + "p"));
			if (custom != null) return custom;
		}
		return FirstExistingFromLibraryCache(id, "library_600x900");
	}

	private string? ResolveWide(int appId)
	{
		string id = appId.ToString();
		foreach (string gridDir in GetUserGridDirs())
		{
			string? custom = FirstExisting(Path.Combine(gridDir, id));
			if (custom != null) return custom;
		}
		string? capsule = FirstExistingFromLibraryCache(id, "library_capsule");
		if (capsule != null) return capsule;
		return FirstExistingFromLibraryCache(id, "library_hero");
	}

	/// <summary>先试新版 {appId}\asset 布局，再试旧版 {appId}_asset 平铺布局。</summary>
	private string? FirstExistingFromLibraryCache(string id, string asset)
	{
		string libraryCache = Path.Combine(_steamDir, "appcache", "librarycache");
		if (!Directory.Exists(libraryCache))
			return null;
		string? folder = FirstExisting(Path.Combine(libraryCache, id, asset));
		if (folder != null) return folder;
		return FirstExisting(Path.Combine(libraryCache, id + "_" + asset));
	}

	private string? FirstExisting(string pathWithoutExtension)
	{
		foreach (string ext in s_extensions)
		{
			string candidate = pathWithoutExtension + ext;
			try
			{
				if (File.Exists(candidate))
					return candidate;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
			{
				// 忽略无法访问的目录，继续尝试其它候选
			}
		}
		return null;
	}

	private string[] GetUserGridDirs()
	{
		if (_userGridDirs.Length > 0)
			return _userGridDirs;

		var dirs = new List<string>();
		try
		{
			string userDataRoot = Path.Combine(_steamDir, "userdata");
			if (Directory.Exists(userDataRoot))
			{
				foreach (string userDir in Directory.GetDirectories(userDataRoot))
				{
					if (!long.TryParse(Path.GetFileName(userDir), out _))
						continue;
					string grid = Path.Combine(userDir, "config", "grid");
					if (Directory.Exists(grid))
						dirs.Add(grid);
				}
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			Log.WriteLine("SteamCoverResolver: failed to enumerate userdata: {0}", ex.Message);
		}

		dirs.Sort(StringComparer.Ordinal);
		_userGridDirs = dirs.ToArray();
		return _userGridDirs;
	}
}
