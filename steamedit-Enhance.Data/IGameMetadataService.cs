#nullable enable
using System;

namespace steamedit_Enhance.Data;

internal interface IGameMetadataService
{
    string SteamDir { get; }
    string AppInfoPath { get; }
    string ModInfoPath { get; }
    IAppInfo AppInfoData { get; }
    IAppInfoDataStore DataStore { get; }
    IEntryDisplaySettings DisplaySettings { get; }
}
