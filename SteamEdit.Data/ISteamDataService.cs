#nullable enable
using System;
using Common;

namespace SteamEdit.Data;

/// <summary>
/// SteamData 服务接口，用于解耦静态单例，
/// 使生命周期管理、测试隔离和未来多实例成为可能。
/// </summary>
internal interface ISteamDataService
{
    // 只读状态
    string SteamDir { get; }
    string AppInfoPath { get; }
    string ModInfoPath { get; }
    IAppInfo AppInfoData { get; }
    IAppInfoDataStore DataStore { get; }
    IEntryDisplaySettings DisplaySettings { get; }

    // 脏标志
    bool NeedsSave { get; }
    bool NeedsApply { get; }

    // 配置（可写）
    bool UseCompleted { get; set; }
    bool UseHidden { get; set; }

    // 事件
    event EventHandler? OnNeedsSave;
    event EventHandler? OnNeedsApply;

    // 生命周期
    void Initialize(ref SteamDataInitProps props);
    void Shutdown();

    // 操作
    void Save();
    void Apply();

    // 文件系统抽象（测试用）
    IFileSystem FileSystem { get; set; }
}
