#nullable enable
using System;

namespace steamedit_Enhance.Data;

internal interface IPersistenceService
{
    bool NeedsSave { get; }
    bool NeedsApply { get; }
    event EventHandler? OnNeedsSave;
    event EventHandler? OnNeedsApply;
    void Initialize(ref SteamDataInitProps props);
    void Shutdown();
    void Save();
    void Apply();
}
