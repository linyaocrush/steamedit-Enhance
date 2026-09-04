#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Common;

namespace steamedit_Enhance.Data;

/// <summary>
/// “官方本地化名称”的显示缓存（纯显示层，绝不写入 appinfo）。
/// 结构：{ "schinese": { "730": "反恐精英：全球攻势" }, ... }，JSON 存于 exe 同目录 cache 文件夹。
/// </summary>
internal sealed class LocalizedNameCache
{
	private const string FileName = "official_names.json";
	private const int MaxSavedCount = 200000;

	private static LocalizedNameCache? _default;
	private static readonly object _defaultLock = new();

	/// <summary>进程内共享实例（应用/批量获取共用；懒加载磁盘缓存）。</summary>
	public static LocalizedNameCache Default
	{
		get
		{
			lock (_defaultLock)
			{
				return _default ??= new LocalizedNameCache(DefaultFilePath());
			}
		}
	}

	internal static void ResetDefault() { lock (_defaultLock) { _default = null; } }

	private static string DefaultFilePath()
		=> Path.Combine(AppHelper.GetAppDirectory(), "cache", FileName);

	private readonly object _lock = new();
	private readonly string _filePath;
	private Dictionary<string, Dictionary<int, string>> _byLocale = new(StringComparer.OrdinalIgnoreCase);
	private bool _loaded;
	private bool _dirty;

	public LocalizedNameCache(string filePath)
	{
		_filePath = filePath;
	}

	/// <summary>读取某语言的某 appid 官方名；找不到返回 null。</summary>
	public string? Get(int appId, string locale)
	{
		if (appId <= 0 || string.IsNullOrEmpty(locale)) return null;
		lock (_lock)
		{
			EnsureLoadedLocked();
			if (_byLocale.TryGetValue(locale, out var map) && map.TryGetValue(appId, out var name))
				return name;
			return null;
		}
	}

	public bool Contains(int appId, string locale)
		=> Get(appId, locale) != null;

	/// <summary>写入一项并标记脏（不落盘，需调用 Save）。返回是否真的改变了值。</summary>
	public bool Set(int appId, string locale, string name)
	{
		if (appId <= 0 || string.IsNullOrEmpty(locale) || string.IsNullOrWhiteSpace(name)) return false;
		name = name.Trim();
		if (name.Length == 0) return false;
		lock (_lock)
		{
			EnsureLoadedLocked();
			if (!_byLocale.TryGetValue(locale, out var map))
			{
				map = new Dictionary<int, string>();
				_byLocale[locale] = map;
			}
			if (map.TryGetValue(appId, out var existing) && existing == name)
				return false;
			map[appId] = name;
			_dirty = true;
			return true;
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_byLocale = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
			_loaded = true;
			_dirty = true;
		}
	}

	public int TotalCount
	{
		get
		{
			lock (_lock)
			{
				EnsureLoadedLocked();
				int n = 0;
				foreach (var map in _byLocale.Values) n += map.Count;
				return n;
			}
		}
	}

	/// <summary>有脏数据则原子写盘。异常只记日志，不影响主流程。</summary>
	public void Save()
	{
		Dictionary<string, Dictionary<int, string>> snapshot;
		lock (_lock)
		{
			if (!_dirty) return;
			snapshot = _byLocale;
			_dirty = false;
		}
		try
		{
			int n = 0;
			foreach (var map in snapshot.Values) n += map.Count;
			if (n > MaxSavedCount)
			{
				// 防御：内存数据异常膨胀时不把超大文件写坏磁盘，丢弃本次落盘但保留内存
				Log.WriteLine("LocalizedNameCache: skip save, {0} entries exceeds {1}", n, MaxSavedCount);
				return;
			}
			string? dir = Path.GetDirectoryName(_filePath);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			string tmp = _filePath + ".tmp";
			File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot));
			File.Move(tmp, _filePath, overwrite: true);
		}
		catch (Exception ex)
		{
			Log.WriteLine("LocalizedNameCache: save failed: {0}", ex.Message);
		}
	}

	private void EnsureLoadedLocked()
	{
		if (_loaded) return;
		_loaded = true;
		try
		{
			if (!File.Exists(_filePath)) return;
			string json = File.ReadAllText(_filePath);
			var loaded = JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, string>>>(json);
			if (loaded != null) _byLocale = loaded;
		}
		catch (Exception ex)
		{
			Log.WriteLine("LocalizedNameCache: load failed ({0}): {1}", _filePath, ex.Message);
			_byLocale = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
		}
	}
}
