#nullable enable
using System;
using steamedit_Enhance.Data.Native;

namespace steamedit_Enhance.Data;

/// <summary>
/// 在本地 Steam 缓存中查找某个 App 的封面图（只做路径解析，不负责解码/下载）。
/// 全部解析逻辑（自定义 grid → 官方固定名 → 模糊扫描按宽高比识别；含按 AppId 的正/负缓存）
/// 由 Rust 内核 (<c>ste_cover_resolver_*</c>) 完成，本类只是句柄封装。
/// 优先级约定：竖版封面（2:3）优先，横版/横幅仅作为没有竖版时的后备。
/// </summary>
internal sealed class SteamCoverResolver : IDisposable
{
	private readonly object _lock = new();
	private IntPtr _handle;

	public SteamCoverResolver(string steamDir)
	{
		_handle = SteameditNative.CoverResolverNew(steamDir ?? "");
	}

	/// <summary>竖版封面路径；找不到返回 null。结果（含未找到）按 AppId 缓存。</summary>
	public string? GetPortraitPath(int appId)
	{
		lock (_lock) { return SteameditNative.CoverResolverPortrait(_handle, appId); }
	}

	/// <summary>横版/横幅后备路径；找不到返回 null。</summary>
	public string? GetWidePath(int appId)
	{
		lock (_lock) { return SteameditNative.CoverResolverWide(_handle, appId); }
	}

	public void Dispose()
	{
		lock (_lock)
		{
			if (_handle != IntPtr.Zero)
			{
				SteameditNative.CoverResolverFree(_handle);
				_handle = IntPtr.Zero;
			}
		}
		GC.SuppressFinalize(this);
	}

	~SteamCoverResolver() => Dispose();
}
