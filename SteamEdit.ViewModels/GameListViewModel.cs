#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Common;
using SteamEdit.Data;

namespace SteamEdit.ViewModels;

internal sealed class GameListViewModel : ViewModelBase
{
    private readonly IAppInfoDataStore _dataStore;
    public IAppInfoDataStore DataStore => _dataStore;
    private readonly Dictionary<int, string> _displayNames = new();

    private AppInfoEntryType _filterFlags = Settings.Default.Filters;
    private ShowEntriesMode _showMode = Settings.Default.FiltersShowEntries;
    private string _sortField = Settings.Default.SortField;
    private bool _sortDescending = Settings.Default.SortDescending;
    private string _searchText = "";
    private int _totalEntryCount;
    private int _visibleCount;
    private int _modifiedCount;
    private GameItemViewModel? _selectedGame;
    private int _tooltipCachedId = -1;
    private string _tooltipCachedText = "";

    public ObservableCollection<GameItemViewModel> AllGames { get; } = new();
    public ObservableCollection<GameItemViewModel> VisibleGames { get; } = new();

    public AppInfoEntryType FilterFlags
    {
        get => _filterFlags;
        set
        {
            if (SetProperty(ref _filterFlags, value))
            {
                Settings.Default.Filters = value;
                Refresh();
            }
        }
    }

    public ShowEntriesMode ShowMode
    {
        get => _showMode;
        set
        {
            if (SetProperty(ref _showMode, value))
            {
                Settings.Default.FiltersShowEntries = value;
                Refresh();
            }
        }
    }

    public string SortField
    {
        get => _sortField;
        set
        {
            if (SetProperty(ref _sortField, value))
            {
                Settings.Default.SortField = value;
                Settings.Default.Save();
                ApplySort();
            }
        }
    }

    public bool SortDescending
    {
        get => _sortDescending;
        set
        {
            if (SetProperty(ref _sortDescending, value))
            {
                Settings.Default.SortDescending = value;
                Settings.Default.Save();
                ApplySort();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                Filter();
        }
    }

    public int TotalEntryCount
    {
        get => _totalEntryCount;
        private set => SetProperty(ref _totalEntryCount, value);
    }

    public int VisibleCount
    {
        get => _visibleCount;
        private set => SetProperty(ref _visibleCount, value);
    }

    public int ModifiedCount
    {
        get => _modifiedCount;
        private set => SetProperty(ref _modifiedCount, value);
    }

    public GameItemViewModel? SelectedGame
    {
        get => _selectedGame;
        set => SetProperty(ref _selectedGame, value);
    }

    public GameListViewModel(IAppInfoDataStore dataStore)
    {
        _dataStore = dataStore;
        _dataStore.AppInfo.Modified += OnEntryModified;
    }

    public string GetDisplayName(IAppInfoEntry entry)
    {
        if (_displayNames.TryGetValue(entry.ID, out var name))
            return name;
        name = entry.ToString() ?? "";
        _displayNames[entry.ID] = name;
        return name;
    }

    public void InvalidateDisplayName(IAppInfoEntry entry) => _displayNames.Remove(entry.ID);

    public void ClearDisplayNameCache() => _displayNames.Clear();

    public GameItemViewModel? FindByEntry(IAppInfoEntry entry) =>
        AllGames.FirstOrDefault(g => g.ID == entry.ID);

    public int IndexOfVisible(GameItemViewModel item) => VisibleGames.IndexOf(item);

    public void Refresh()
    {
        ClearDisplayNameCache();

        int totalCount = 0;
        int modifiedCount = 0;
        var all = new List<IAppInfoEntry>();
        foreach (var ent in _dataStore.AppInfo.Entries)
        {
            if (ent.Name == null) continue;
            totalCount++;
            if (ent.IsModified) modifiedCount++;
            all.Add(ent);
        }

        TotalEntryCount = totalCount;
        ModifiedCount = modifiedCount;

        AllGames.Clear();
        foreach (var ent in all)
            AllGames.Add(new GameItemViewModel(ent, GetDisplayName(ent)));

        Filter();
    }

    public void Filter()
    {
        string trimmed = _searchText.Trim();
        bool isNumeric = int.TryParse(trimmed, out _);
        string[]? patternParts = string.IsNullOrEmpty(trimmed) ? null : trimmed.Split(' ', '\t', '　');

        var filtered = AllGames.Where(g =>
        {
            if ((g.Type & _filterFlags) == 0) return false;
            if (_showMode == ShowEntriesMode.OnlyModified && !g.IsModified) return false;
            if (_showMode == ShowEntriesMode.OnlyUnmodified && g.IsModified) return false;
            if (string.IsNullOrEmpty(trimmed)) return true;
            if (isNumeric) return g.ID.ToString().StartsWith(trimmed);
            return IsFuzzyMatch(g.DisplayName, patternParts!);
        }).ToList();

        ApplySortTo(filtered);

        VisibleGames.Clear();
        foreach (var g in filtered)
            VisibleGames.Add(g);
        VisibleCount = VisibleGames.Count;
    }

    public void ApplySort()
    {
        var list = VisibleGames.ToList();
        ApplySortTo(list);
        VisibleGames.Clear();
        foreach (var g in list)
            VisibleGames.Add(g);
    }

    private void ApplySortTo(List<GameItemViewModel> list)
    {
        if (_sortField == "AppId")
        {
            list.Sort((a, b) =>
            {
                int n = a.ID.CompareTo(b.ID);
                if (_sortDescending) n = -n;
                if (n == 0) n = string.Compare(a.NormalizedSortAs, b.NormalizedSortAs, StringComparison.CurrentCulture);
                return n;
            });
        }
        else
        {
            list.Sort((a, b) =>
            {
                int n = string.Compare(a.NormalizedSortAs, b.NormalizedSortAs, StringComparison.CurrentCulture);
                if (_sortDescending) n = -n;
                if (n == 0) n = a.ID - b.ID;
                return n;
            });
        }
    }

    public void ResortEntry(GameItemViewModel item)
    {
        int i = AllGames.IndexOf(item);
        if (i < 0) return;
        AllGames.RemoveAt(i);
        int insertAt = 0;
        for (int j = 0; j < AllGames.Count; j++)
        {
            int cmp = StringTitleComparer.CompareStrings(item.SortAs, AllGames[j].SortAs);
            if (_sortDescending ? cmp >= 0 : cmp <= 0)
            {
                insertAt = j;
                break;
            }
            insertAt = j + 1;
        }
        AllGames.Insert(insertAt, item);
        Filter();
    }

    public GameItemViewModel? SelectById(int id) =>
        VisibleGames.FirstOrDefault(g => g.ID == id);

    public string GetEntryToolTip(IAppInfoEntry entry)
    {
        if (entry.ID == _tooltipCachedId) return _tooltipCachedText;
        var sb = new System.Text.StringBuilder();
        sb.AppendFormat("{0}: {1}", entry.ID, entry.Name);
        string? oslist = entry.GetPropertyValue<string>(null!, AppInfoPaths.OsList);
        if (oslist != null) sb.AppendFormat(" [{0}]", oslist);
        _tooltipCachedId = entry.ID;
        _tooltipCachedText = sb.ToString();
        return _tooltipCachedText;
    }

    private static bool IsFuzzyMatch(string testString, string[] patternParts)
    {
        foreach (string patt in patternParts)
        {
            if (!testString.Contains(patt, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private void OnEntryModified(object? sender, EventArgs e)
    {
        ModifiedCount = AllGames.Count(g => g.IsModified);
    }
}
