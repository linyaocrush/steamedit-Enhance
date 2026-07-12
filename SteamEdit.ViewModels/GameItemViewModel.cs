#nullable enable
using Common;
using SteamEdit.Data;

namespace SteamEdit.ViewModels;

internal sealed class GameItemViewModel : ViewModelBase
{
    private readonly IAppInfoEntry _entry;
    private string _displayName = "";

    public GameItemViewModel(IAppInfoEntry entry, string displayName)
    {
        _entry = entry;
        _displayName = displayName;
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

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public void InvalidateDisplayName() => RaisePropertyChanged(nameof(DisplayName));
}
