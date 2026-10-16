#nullable enable
using System.Threading;
using System.Threading.Tasks;
using steamedit_Enhance.Data.Native;

namespace steamedit_Enhance.Data;

/// <summary>
/// 下载竖版封面（library_600x900.jpg）的抽象，供批量预加载注入替换/测试。
/// </summary>
internal interface ICoverDownloader
{
	/// <summary>下载竖封面到 destFile。成功返回 true；失败（404/网络/无效图片）返回 false。</summary>
	Task<bool> DownloadPortraitAsync(int appId, string destFile, CancellationToken cancellationToken = default);
}

/// <summary>
/// 从 Steam 官方 CDN 下载竖版封面（library_600x900.jpg）并写入目标文件的薄封装。
/// 真正的 HTTP、多源兜底、图片头校验与原子写入全部由 Rust 内核
/// (<c>ste_net_cover_download</c>) 完成；本类只负责把调用搬到线程池上并转发取消。
/// </summary>
internal sealed class CoverDownloader : ICoverDownloader
{
	public async Task<bool> DownloadPortraitAsync(int appId, string destFile, CancellationToken cancellationToken = default)
	{
		if (appId <= 0 || string.IsNullOrEmpty(destFile))
			return false;

		cancellationToken.ThrowIfCancellationRequested();
		// 内核调用是同步阻塞的，放到线程池执行；取消只能在进入前生效。
		return await Task.Run(
			() => SteameditNative.CoverDownload(appId, destFile),
			cancellationToken).ConfigureAwait(false);
	}
}
