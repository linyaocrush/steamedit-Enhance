#nullable enable
using System.Threading;
using System.Threading.Tasks;
using steamedit_Enhance.Data.Native;

namespace steamedit_Enhance.Data;

/// <summary>
/// 获取某游戏某语言的官方名称的抽象，供批量预加载注入替换/测试。
/// </summary>
internal interface IOfficialNameFetcher
{
	/// <summary>取官方名称；无商店页面/网络失败/被限流均返回 null。</summary>
	Task<string?> FetchNameAsync(int appId, string locale, CancellationToken cancellationToken = default);
}

/// <summary>
/// 从 Steam 官方商店接口获取某游戏某语言的官方名称的薄封装。
/// HTTP 请求、JSON 解析全部由 Rust 内核 (<c>ste_net_official_name</c>) 完成；
/// 本类只把调用搬到线程池上并转发取消，失败一律返回 null、绝不抛给调用方。
/// </summary>
internal sealed class OfficialNameFetcher : IOfficialNameFetcher
{
	public async Task<string?> FetchNameAsync(int appId, string locale, CancellationToken cancellationToken = default)
	{
		if (appId <= 0 || string.IsNullOrEmpty(locale))
			return null;

		cancellationToken.ThrowIfCancellationRequested();
		return await Task.Run(
			() => SteameditNative.OfficialName(appId, locale),
			cancellationToken).ConfigureAwait(false);
	}
}
