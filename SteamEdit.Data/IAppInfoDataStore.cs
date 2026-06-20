#nullable enable
namespace SteamEdit.Data;

/// <summary>
/// 提供对 IAppInfo 的访问，解耦 UI 层对静态 SteamData 的直接依赖。
/// </summary>
internal interface IAppInfoDataStore
{
	IAppInfo AppInfo { get; }
}
