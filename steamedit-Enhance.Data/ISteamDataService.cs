#nullable enable
using System;
using Common;

namespace steamedit_Enhance.Data;

internal interface ISteamDataService : ISteamDataReadService
{
    new bool UseCompleted { get; set; }
    new bool UseHidden { get; set; }
    void Initialize(ref SteamDataInitProps props);
    void Shutdown();
    void Save();
    void Apply();
    new IFileSystem FileSystem { get; set; }
}
