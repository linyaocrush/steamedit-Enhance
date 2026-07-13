#nullable enable
using System;
using Common;
using steamedit_Enhance.Data;

namespace steamedit_Enhance.ViewModels;

internal sealed class GameItemViewModel : ViewModelBase
{
    private readonly IAppInfoEntry _entry;
    private string _displayName = "";

    public GameItemViewModel(IAppInfoEntry entry, string displayName)
    {
        _entry = entry;
        _displayName = displayName;
        _entry.Modified += OnEntryModified;
    }

    public int ID => _entry.ID;
    public IAppInfoEntry Entry => _entry;
    public string Name => _entry.Name ?? "";
    public string SortAs => _entry.SortAs;
    public string NormalizedSortAs => _entry.NormalizedSortAs;
    public AppInfoEntryType Type => _entry.Type;
    public bool IsModified => _entry.IsModified;
    public bool IsHidden => _entry.IsHidden;
    public bool IsCompleted => _entry.IsCompleted;

    private void OnEntryModified(object? sender, EventArgs e)
    {
        RaisePropertyChanged(nameof(Name));
        RaisePropertyChanged(nameof(SortAs));
        RaisePropertyChanged(nameof(NormalizedSortAs));
        RaisePropertyChanged(nameof(Type));
        RaisePropertyChanged(nameof(IsModified));
        RaisePropertyChanged(nameof(IsHidden));
        RaisePropertyChanged(nameof(IsCompleted));
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public void InvalidateDisplayName() => RaisePropertyChanged(nameof(DisplayName));
}
