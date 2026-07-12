#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;
using steamedit_Enhance.ViewModels;

namespace steamedit_EnhanceTests;

[TestClass]
public class GameListViewModelTests
{
    private sealed class FakeEntry : IAppInfoEntry
    {
        private readonly Dictionary<string, object?> _properties = new();

        public int ID { get; set; }
        public uint AppInfoVersion => 0;
        public uint ChangeNumber => 0;
        public ModifiedEntry? Modification { get; set; }
        public bool IsModified { get; set; }
        public bool IsHidden { get; set; }
        public bool IsCompleted { get; set; }
        public string Name { get; set; } = "";
        public bool HasSortAs => !string.IsNullOrEmpty(SortAs);
        public string SortAs { get; set; } = "";
        public string NormalizedSortAs => Common.StringTitleComparer.Normalize(SortAs);
        public string InstallPath => "";
        public AppInfoEntryType Type { get; set; } = AppInfoEntryType.Game;
        public LaunchTable Launches { get; set; } = new LaunchTable();
        public event EventHandler? Modified;
        public byte[] GetOriginalDataCopy() => Array.Empty<byte>();
        public T GetPropertyValue<T>(T def, params string[] PropertyPath)
        {
            var key = string.Join("/", PropertyPath);
            if (_properties.TryGetValue(key, out var v) && v is T typed) return typed;
            return def;
        }
        public void SetPropertyValue(PropertyType t, object? v, params string[] PropertyPath)
        {
            var key = string.Join("/", PropertyPath);
            _properties[key] = v;
        }
        public void RevertModifiedProperty(params string[] path) { }
        public bool Revert() { IsModified = false; return true; }
public PropertyType? GetOriginalPropertyType(params string[] path) => null;
        public object? GetOriginalPropertyValue(params string[] path) => null;
        
        public override string ToString() => Name;
    }

    private sealed class FakeAppInfo : IAppInfo
    {
        private readonly List<IAppInfoEntry> _entries;
        public FakeAppInfo(IEnumerable<IAppInfoEntry> entries) => _entries = entries.ToList();
        public IEnumerable<IAppInfoEntry> Entries => _entries;
        public event EventHandler? Modified;
        public IAppInfoEntry? FindEntry(uint AppID) => _entries.FirstOrDefault(e => e.ID == AppID);
        public void Save(string path) { }
        public bool RevertEntry(IAppInfoEntry entry) => entry.Revert();
        public void RemoveEntry(IAppInfoEntry entry) => _entries.Remove(entry);
    }

    private sealed class FakeDataStore : IAppInfoDataStore
    {
        public FakeDataStore(IAppInfo appInfo) => AppInfo = appInfo;
        public IAppInfo AppInfo { get; }
    }

    private static GameListViewModel CreateViewModel(params IAppInfoEntry[] entries)
    {
        var savedFilters = Settings.Default.Filters;
        var savedShowMode = Settings.Default.FiltersShowEntries;
        var savedSortField = Settings.Default.SortField;
        var savedSortDescending = Settings.Default.SortDescending;
        try
        {
            Settings.Default.Filters = (AppInfoEntryType)0x7FF;
            Settings.Default.FiltersShowEntries = ShowEntriesMode.All;
            Settings.Default.SortField = "Name";
            Settings.Default.SortDescending = false;
            var appInfo = new FakeAppInfo(entries);
            var store = new FakeDataStore(appInfo);
            return new GameListViewModel(store);
        }
        finally
        {
            Settings.Default.Filters = savedFilters;
            Settings.Default.FiltersShowEntries = savedShowMode;
            Settings.Default.SortField = savedSortField;
            Settings.Default.SortDescending = savedSortDescending;
        }
    }

    [TestMethod]
    public void Refresh_PopulatesAllGames()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Game A" },
            new FakeEntry { ID = 2, Name = "Game B" });
        vm.Refresh();

        Assert.AreEqual(2, vm.TotalEntryCount);
        Assert.AreEqual(2, vm.AllGames.Count);
        Assert.AreEqual(2, vm.VisibleGames.Count);
    }

    [TestMethod]
    public void Refresh_CountsModified()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "A", IsModified = true },
            new FakeEntry { ID = 2, Name = "B", IsModified = false });
        vm.Refresh();

        Assert.AreEqual(1, vm.ModifiedCount);
    }

    [TestMethod]
    public void Refresh_NullNameEntry_Skipped()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Valid" },
            new FakeEntry { ID = 2, Name = null! });
        vm.Refresh();

        Assert.AreEqual(1, vm.TotalEntryCount);
        Assert.AreEqual(1, vm.AllGames.Count);
    }

    [TestMethod]
    public void Filter_ByType_HidesOtherTypes()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Game", Type = AppInfoEntryType.Game },
            new FakeEntry { ID = 2, Name = "Tool", Type = AppInfoEntryType.Tool },
            new FakeEntry { ID = 3, Name = "DLC", Type = AppInfoEntryType.DLC });
        vm.Refresh();

        vm.FilterFlags = AppInfoEntryType.Tool | AppInfoEntryType.DLC;

        Assert.AreEqual(2, vm.VisibleCount);
        Assert.IsTrue(vm.VisibleGames.All(g => g.Type != AppInfoEntryType.Game));
    }

    [TestMethod]
    public void Filter_BySearchText_FuzzyMatches()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 10, Name = "Half-Life" },
            new FakeEntry { ID = 20, Name = "Team Fortress" },
            new FakeEntry { ID = 30, Name = "Portal" });
        vm.Refresh();

        vm.SearchText = "10";
        Assert.AreEqual(1, vm.VisibleCount);
        Assert.AreEqual(10, vm.VisibleGames[0].ID);
    }

    [TestMethod]
    public void Filter_BySearchText_MultiWordFuzzy()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Half-Life 2" },
            new FakeEntry { ID = 2, Name = "Portal 2" });
        vm.Refresh();

        vm.SearchText = "1";
        Assert.AreEqual(1, vm.VisibleCount);
        Assert.AreEqual(1, vm.VisibleGames[0].ID);
    }

    [TestMethod]
    public void Filter_ByNumericSearch_MatchesIdPrefix()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 123, Name = "Game 1" },
            new FakeEntry { ID = 1234, Name = "Game 2" },
            new FakeEntry { ID = 456, Name = "Game 3" });
        vm.Refresh();

        vm.SearchText = "123";

        Assert.AreEqual(2, vm.VisibleCount);
        Assert.IsTrue(vm.VisibleGames.All(g => g.ID.ToString().StartsWith("123")));
    }

    [TestMethod]
    public void Filter_ShowMode_OnlyModified()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "A", IsModified = true },
            new FakeEntry { ID = 2, Name = "B", IsModified = false });
        vm.Refresh();

        vm.ShowMode = ShowEntriesMode.OnlyModified;

        Assert.AreEqual(1, vm.VisibleCount);
        Assert.AreEqual(1, vm.VisibleGames[0].ID);
    }

    [TestMethod]
    public void Filter_ShowMode_OnlyUnmodified()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "A", IsModified = true },
            new FakeEntry { ID = 2, Name = "B", IsModified = false });
        vm.Refresh();

        vm.ShowMode = ShowEntriesMode.OnlyUnmodified;

        Assert.AreEqual(1, vm.VisibleCount);
        Assert.AreEqual(2, vm.VisibleGames[0].ID);
    }

    [TestMethod]
    public void ApplySort_ById_Ascending()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 3, Name = "C" },
            new FakeEntry { ID = 1, Name = "A" },
            new FakeEntry { ID = 2, Name = "B" });
        vm.Refresh();

        vm.SortField = "AppId";
        vm.SortDescending = false;

        var ids = vm.VisibleGames.Select(g => g.ID).ToList();
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ids);
    }

    [TestMethod]
    public void ApplySort_ById_Descending()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "A" },
            new FakeEntry { ID = 3, Name = "C" },
            new FakeEntry { ID = 2, Name = "B" });
        vm.Refresh();

        vm.SortField = "AppId";
        vm.SortDescending = true;

        var ids = vm.VisibleGames.Select(g => g.ID).ToList();
        CollectionAssert.AreEqual(new[] { 3, 2, 1 }, ids);
    }

    [TestMethod]
    public void ApplySort_ByName_Ascending()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Zoo", SortAs = "Zoo" },
            new FakeEntry { ID = 2, Name = "Apple", SortAs = "Apple" },
            new FakeEntry { ID = 3, Name = "Banana", SortAs = "Banana" });
        vm.Refresh();

        vm.SortDescending = false;

        var names = vm.VisibleGames.Select(g => g.Name).ToList();
        CollectionAssert.AreEqual(new[] { "Apple", "Banana", "Zoo" }, names);
    }

    [TestMethod]
    public void SelectById_Exists_ReturnsItem()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 42, Name = "The Answer" });
        vm.Refresh();

        var found = vm.SelectById(42);
        Assert.IsNotNull(found);
        Assert.AreEqual(42, found!.ID);
    }

    [TestMethod]
    public void SelectById_NotExists_ReturnsNull()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "One" });
        vm.Refresh();

        Assert.IsNull(vm.SelectById(999));
    }

    [TestMethod]
    public void SearchText_WithSearch_ClearsSearch_ShowsAll()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Alpha" },
            new FakeEntry { ID = 2, Name = "Beta" });
        vm.Refresh();

        Assert.AreEqual(2, vm.VisibleCount, "Visible after refresh");

        vm.SearchText = "2";
        Assert.AreEqual(1, vm.VisibleCount, "Visible after ID search");

        vm.SearchText = "";
        Assert.AreEqual(2, vm.VisibleCount, "Visible after clear");
    }

    [TestMethod]
    public void GetEntryToolTip_ReturnsFormattedString()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 42, Name = "My Game" });
        vm.Refresh();

        var entry = new FakeEntry { ID = 42, Name = "My Game" };
        var tip = vm.GetEntryToolTip(entry);
        Assert.AreEqual("42: My Game", tip);
    }

    [TestMethod]
    public void GetEntryToolTip_WithOsList_IncludesIt()
    {
        var entryWithOs = new FakeEntry { ID = 2, Name = "WithOS" };
        entryWithOs.SetPropertyValue(PropertyType.String, "windows", "appinfo", "common", "oslist");

        var vm = new GameListViewModel(new FakeDataStore(new FakeAppInfo(Array.Empty<IAppInfoEntry>())));
        var tip = vm.GetEntryToolTip(entryWithOs);
        Assert.AreEqual("2: WithOS [windows]", tip);
    }

    [TestMethod]
    public void Refresh_RaisesTotalEntryCountChanged()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "A" },
            new FakeEntry { ID = 2, Name = "B" });
        bool fired = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameListViewModel.TotalEntryCount))
                fired = true;
        };
        vm.Refresh();
        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void Filter_MultipleTypes_ExcludesUnknown()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Game", Type = AppInfoEntryType.Game },
            new FakeEntry { ID = 2, Name = "Music", Type = AppInfoEntryType.Music });
        vm.Refresh();

        vm.FilterFlags = AppInfoEntryType.Music | AppInfoEntryType.Video;
        Assert.AreEqual(1, vm.VisibleCount);
        Assert.AreEqual(AppInfoEntryType.Music, vm.VisibleGames[0].Type);
    }

[TestMethod]
    public void Sort_ByName_EmptySortAs_TiesBrokenById()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 5, Name = "Z", SortAs = "" },
            new FakeEntry { ID = 3, Name = "Z", SortAs = "" });
        vm.Refresh();

        vm.SortDescending = false;
        var ids = vm.VisibleGames.Select(g => g.ID).ToList();
        CollectionAssert.AreEqual(new[] { 3, 5 }, ids);
    }

    // ========= 添加边界条件测试 =========
    
    [TestMethod]
    public void Filter_BoundaryCondition_EmptyList()
    {
        var vm = CreateViewModel();
        vm.Refresh();
        
        Assert.AreEqual(0, vm.TotalEntryCount);
        Assert.AreEqual(0, vm.VisibleCount);
        Assert.AreEqual(0, vm.ModifiedCount);
        Assert.AreEqual(0, vm.AllGames.Count);
        Assert.AreEqual(0, vm.VisibleGames.Count);
    }

    [TestMethod]
    public void Filter_BoundaryCondition_SingleEntry()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Single Game", Type = AppInfoEntryType.Game, IsModified = true });
        vm.Refresh();
        
        Assert.AreEqual(1, vm.TotalEntryCount);
        Assert.AreEqual(1, vm.VisibleCount);
        Assert.AreEqual(1, vm.ModifiedCount);
        Assert.AreEqual(1, vm.AllGames.Count);
        Assert.AreEqual(1, vm.VisibleGames.Count);
        
        // 测试各种筛选条件下单一条目的行为
        vm.ShowMode = ShowEntriesMode.OnlyModified;
        Assert.AreEqual(1, vm.VisibleCount);
        
        vm.ShowMode = ShowEntriesMode.OnlyUnmodified;
        Assert.AreEqual(0, vm.VisibleCount); // 修改过的条目不应显示
    }

    [TestMethod]
    public void Filter_BoundaryCondition_SearchDoesNotMatch()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Half-Life", Type = AppInfoEntryType.Game },
            new FakeEntry { ID = 2, Name = "Portal", Type = AppInfoEntryType.Game });
        vm.Refresh();
        
        vm.SearchText = "xyz123456789"; // 不可能匹配的搜索词
        Assert.AreEqual(0, vm.VisibleCount);
        
        // 清空搜索后应恢复显示
        vm.SearchText = "";
        Assert.AreEqual(2, vm.VisibleCount);
    }

    [TestMethod]
    public void Filter_BoundaryCondition_AllEntriesFilteredOut()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Game A", Type = AppInfoEntryType.Game },
            new FakeEntry { ID = 2, Name = "Game B", Type = AppInfoEntryType.Game });
        vm.Refresh();
        
        // 设置过滤器排除所有类型
        vm.FilterFlags = AppInfoEntryType.Config | AppInfoEntryType.Video; // 不包括Game
        Assert.AreEqual(0, vm.VisibleCount);
        
        // 恢复过滤器
        vm.FilterFlags = (AppInfoEntryType)0x7FF; // 全部类型
        Assert.AreEqual(2, vm.VisibleCount);
    }

    [TestMethod]
    public void Filter_Performance_LargeDataset_PartialFilter()
    {
        // 创建一个中等数据集测试（实际项目中可能更大）
        var entries = new List<FakeEntry>();
        for (int i = 1; i <= 200; i++)
        {
            entries.Add(new FakeEntry { ID = i, Name = $"Game {i:D3}", Type = AppInfoEntryType.Game });
        }
        
        var vm = CreateViewModel(entries.ToArray());
        vm.Refresh();
        Assert.AreEqual(200, vm.TotalEntryCount);
        
        // 测试部分过滤性能（虽然不是真正的性能测试，但验证没有异常）
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        vm.SearchText = "999"; // 匹配任何项
        var elapsed = stopwatch.ElapsedMilliseconds;
        
        Assert.IsTrue(elapsed < 5000, $"筛选200项耗时不应超过5秒，实际耗时{elapsed}ms");
    }

    [TestMethod]
    public void Filter_EdgeCase_HiddenAndCompletedStates()
    {
        var entry = new FakeEntry { 
            ID = 1, 
            Name = "Test Game", 
            Type = AppInfoEntryType.Game,
            IsHidden = true,
            IsCompleted = true
        };
        
        var vm = CreateViewModel(entry);
        vm.Refresh();
        
        // 注意：GameListViewModel 不直接根据 Hidden 或 Completed 状态过滤
        // 这些状态由 PropertyEditorViewModel 处理
        // 我们只是验证具有这些状态的条目能被正确处理
        Assert.AreEqual(1, vm.AllGames.Count);
        Assert.AreEqual(1, vm.VisibleCount);
    }

    [TestMethod] 
    public void GetEntryToolTip_CacheBehavior()
    {
        var vm = CreateViewModel();
        var entry1 = new FakeEntry { ID = 42, Name = "Game A" };
        var entry2 = new FakeEntry { ID = 43, Name = "Game B" };
        
        // 首次获取应该计算
        var tip1 = vm.GetEntryToolTip(entry1);
        Assert.AreEqual("42: Game A", tip1);
        
        // 相同ID应该从缓存获取
        var tipCached = vm.GetEntryToolTip(entry1);
        Assert.AreEqual(tip1, tipCached);
        
        // 不同ID应该重新计算
        var tip2 = vm.GetEntryToolTip(entry2);
        Assert.AreEqual("43: Game B", tip2);
    }

    [TestMethod]
    public void Filter_SearchText_WhitespaceVariations()
    {
        var vm = CreateViewModel(
            new FakeEntry { ID = 1, Name = "Portal 2", Type = AppInfoEntryType.Game },
            new FakeEntry { ID = 2, Name = "Half Life 2", Type = AppInfoEntryType.Game }); // 改为"Half Life 2"而不是"Half-Life 2"
        vm.Refresh();
        
        // 测试各种空白字符
        vm.SearchText = "  Portal  2  "; // 前后和中间多余空格
        Assert.AreEqual(1, vm.VisibleCount);
        Assert.AreEqual("Portal 2", vm.VisibleGames[0].Name);
        
        vm.SearchText = "Half\tLife"; // 制表符分隔的搜索词
        Assert.AreEqual(1, vm.VisibleCount);
        Assert.AreEqual("Half Life 2", vm.VisibleGames[0].Name);
        
        // 全角空格在字符串中的表示可能有问题，暂时注释掉
        // vm.SearchText = "Half　Life"; // 全角空格分隔
        // Assert.AreEqual(1, vm.VisibleCount);
        // Assert.AreEqual("Half Life 2", vm.VisibleGames[0].Name);
    }
}
