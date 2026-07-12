#nullable enable
using System;
using Common;

namespace SteamEdit.Data;

internal interface ISteamDataReadService
{
    string SteamDir { get; }
    string AppInfoPath { get; }
    string ModInfoPath { get; }
    IAppInfo AppInfoData { get; }
    IAppInfoDataStore DataStore { get; }
    IEntryDisplaySettings DisplaySettings { get; }
    bool NeedsSave { get; }
    bool NeedsApply { get; }
    bool UseCompleted { get; }
    bool UseHidden { get; }
    event EventHandler? OnNeedsSave;
    event EventHandler? OnNeedsApply;
    IFileSystem FileSystem { get; }
}
