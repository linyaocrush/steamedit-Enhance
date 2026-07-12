#nullable enable
using System;
using System.Collections.Generic;
using Common;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

internal class FakeEntryBase : IAppInfoEntry
{
    public int ID { get; set; } = 1;
    public uint AppInfoVersion { get; set; }
    public uint ChangeNumber { get; set; }
    public ModifiedEntry? Modification { get; set; }
    public bool IsModified => Modification?.Changes != null && Modification.Changes.Count > 0;
    public bool IsHidden { get; set; }
    public bool IsCompleted { get; set; }
    public string Name { get; set; } = "";
    public bool HasSortAs => !string.IsNullOrEmpty(SortAs);
    public string SortAs { get; set; } = "";
    public string NormalizedSortAs => StringTitleComparer.Normalize(SortAs);
    public string InstallPath => "";
    public AppInfoEntryType Type { get; set; } = AppInfoEntryType.Game;
    public LaunchTable Launches { get; set; } = new LaunchTable();
    public event EventHandler? Modified;

    public virtual byte[] GetOriginalDataCopy() => Array.Empty<byte>();
    public virtual T GetPropertyValue<T>(T def, params string[] path) => def;
    public virtual void SetPropertyValue(PropertyType type, object? value, params string[] path) { }
    public virtual void RevertModifiedProperty(params string[] path) { }
    public virtual bool Revert() => false;
    public virtual PropertyType? GetOriginalPropertyType(params string[] path) => null;
    public virtual object? GetOriginalPropertyValue(params string[] path) => null;

    public override string ToString() => Name;
}
