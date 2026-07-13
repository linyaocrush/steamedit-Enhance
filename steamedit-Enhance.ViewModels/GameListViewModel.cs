#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Common;
using steamedit_Enhance.Data;

namespace steamedit_Enhance.ViewModels;

internal sealed class GameListViewModel : ViewModelBase
{
    private readonly IAppInfoDataStore _dataStore;
    private readonly Dictionary<int, string> _displayNames = new();
    private readonly Dictionary<int, string> _escapedDisplayNameCache = new();
    
    // 搜索缓存：LRU 缓存，LinkedList + Dictionary 实现 O(1) 淘汰
    private readonly Dictionary<(int GameId, string SearchPattern), (LinkedListNode<(int GameId, string SearchPattern)> Node, bool Result)> _searchCache = new();
    private readonly LinkedList<(int GameId, string SearchPattern)> _searchCacheList = new();
    private const int MaxSearchCacheSize = 1000;

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
        if (_escapedDisplayNameCache.TryGetValue(entry.ID, out var escaped))
            return escaped;
        if (!_displayNames.TryGetValue(entry.ID, out var name))
        {
            name = entry.ToString() ?? "";
            _displayNames[entry.ID] = name;
        }
        escaped = name.Replace("&", "&&");
        _escapedDisplayNameCache[entry.ID] = escaped;
        return escaped;
    }

    public void InvalidateDisplayName(IAppInfoEntry entry)
    {
        _displayNames.Remove(entry.ID);
        _escapedDisplayNameCache.Remove(entry.ID);
    }

    public void ClearDisplayNameCache()
    {
        _displayNames.Clear();
        _escapedDisplayNameCache.Clear();
    }

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

        foreach (var g in AllGames)
            g.DetachEvents();
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

		var result = new List<GameItemViewModel>(AllGames.Count);
		foreach (var g in AllGames)
		{
			if (string.IsNullOrEmpty(trimmed) ||
			    (isNumeric && g.ID.ToString().StartsWith(trimmed)) ||
			    (!isNumeric && IsFuzzyMatchCached(g, trimmed, patternParts!)))
			{
				if ((g.Type & _filterFlags) != 0 &&
				    (_showMode == ShowEntriesMode.All ||
				     (_showMode == ShowEntriesMode.OnlyModified ? g.IsModified : !g.IsModified)))
				{
					result.Add(g);
				}
			}
		}

		ApplySortTo(result);

		VisibleGames.Clear();
		foreach (var g in result)
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
            // 使用 IndexOf 代替 Contains 以获得更好的性能
            if (testString.IndexOf(patt, StringComparison.OrdinalIgnoreCase) < 0) return false;
        }
        return true;
    }
    
    private bool IsFuzzyMatchCached(GameItemViewModel game, string searchPattern, string[] patternParts)
    {
        var cacheKey = (game.ID, searchPattern);
        
        if (_searchCache.TryGetValue(cacheKey, out var entry))
        {
            _searchCacheList.Remove(entry.Node);
            _searchCacheList.AddLast(entry.Node);
            return entry.Result;
        }
            
        bool result = IsFuzzyMatch(game.DisplayName, patternParts);
        
        var newNode = _searchCacheList.AddLast(cacheKey);
        _searchCache[cacheKey] = (newNode, result);
        
        while (_searchCache.Count > MaxSearchCacheSize)
        {
            var oldest = _searchCacheList.First!;
            _searchCacheList.RemoveFirst();
            _searchCache.Remove(oldest.Value);
        }
        
        return result;
    }
    
    public void ClearSearchCache()
    {
        _searchCache.Clear();
        _searchCacheList.Clear();
    }

    private void OnEntryModified(object? sender, EventArgs e)
    {
        ModifiedCount = AllGames.Count(g => g.IsModified);
    }
}
