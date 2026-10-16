#nullable enable
using System;
using System.IO;
using Common;
using steamedit_Enhance.Data.Native;

namespace steamedit_Enhance.Data;

/// <summary>
/// “官方本地化名称”的显示缓存（纯显示层，绝不写入 appinfo）。
/// 结构：{ "schinese": { "730": "反恐精英：全球攻势" }, ... }，JSON 存于 exe 同目录 cache 文件夹。
/// 合并/查询/原子落盘全部由 Rust 内核 (<c>ste_namecache_*</c>) 负责，本类只是句柄封装。
/// </summary>
internal sealed class LocalizedNameCache : IDisposable
{
	private const string FileName = "official_names.json";

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

	private readonly System.Threading.Lock _lock = new();
	private IntPtr _handle;

	public LocalizedNameCache(string filePath)
	{
		_handle = SteameditNative.NameCacheNew(filePath);
	}

	/// <summary>读取某语言的某 appid 官方名；找不到返回 null。</summary>
	public string? Get(int appId, string locale)
	{
		if (appId <= 0 || string.IsNullOrEmpty(locale)) return null;
		lock (_lock) { return SteameditNative.NameCacheGet(_handle, appId, locale); }
	}

	public bool Contains(int appId, string locale)
	{
		if (appId <= 0 || string.IsNullOrEmpty(locale)) return false;
		lock (_lock) { return SteameditNative.NameCacheContains(_handle, appId, locale); }
	}

	/// <summary>写入一项并标记脏（不落盘，需调用 Save）。返回是否真的改变了值。</summary>
	public bool Set(int appId, string locale, string name)
	{
		if (appId <= 0 || string.IsNullOrEmpty(locale) || string.IsNullOrWhiteSpace(name)) return false;
		lock (_lock) { return SteameditNative.NameCacheSet(_handle, appId, locale, name); }
	}

	public void Clear()
	{
		lock (_lock) { SteameditNative.NameCacheClear(_handle); }
	}

	public int TotalCount
	{
		get { lock (_lock) { return (int)SteameditNative.NameCacheTotal(_handle); } }
	}

	/// <summary>有脏数据则原子写盘。异常只记日志，不影响主流程。</summary>
	public void Save()
	{
		lock (_lock) { SteameditNative.NameCacheSave(_handle); }
	}

	public void Dispose()
	{
		lock (_lock)
		{
			if (_handle != IntPtr.Zero)
			{
				SteameditNative.NameCacheFree(_handle);
				_handle = IntPtr.Zero;
			}
		}
		GC.SuppressFinalize(this);
	}

	~LocalizedNameCache() => Dispose();
}
