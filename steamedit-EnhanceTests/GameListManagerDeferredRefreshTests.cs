#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance;
using steamedit_Enhance.Data;
using steamedit_Enhance.ViewModels;

namespace steamedit_EnhanceTests;

/// <summary>
/// 验证「修改游戏名称时左侧列表不立即刷新，点选另一条游戏后才刷新」的行为。
/// </summary>
[TestClass]
public class GameListManagerDeferredRefreshTests
{
    [TestMethod]
    public void NotifyEntryEdited_DoesNotRebuildListUntilPendingRefreshApplied()
    {
        var first = new FakeEntryBase { ID = 1, Name = "Alpha", SortAs = "Alpha" };
        var second = new FakeEntryBase { ID = 2, Name = "Beta", SortAs = "Beta" };
        using var env = new ListManagerEnv(first, second);
        env.Manager.RefreshGameList();

        int countBefore = env.Manager.ViewModel.AllGames.Count;
        string displayBefore = env.Manager.ViewModel.GetDisplayName(first);

        // 模拟改名：EditController 只会通知列表管理器，不应立即重建列表
        first.Name = "Zeta";
        env.Manager.NotifyEntryEdited(first);

        Assert.IsTrue(env.Manager.HasPendingListRefresh);
        Assert.AreEqual(countBefore, env.Manager.ViewModel.AllGames.Count);
        Assert.AreEqual(displayBefore, env.Manager.ViewModel.GetDisplayName(first), "编辑后应继续显示旧的缓存名称");

        // 点选另一条游戏 -> 应用延迟刷新
        Assert.IsTrue(env.Manager.ApplyPendingRefresh());
        Assert.IsFalse(env.Manager.HasPendingListRefresh);
        Assert.AreEqual("Zeta", env.Manager.ViewModel.GetDisplayName(first));
        Assert.IsTrue(env.Manager.ViewModel.AllGames.Any(g => g.Entry == first && g.Entry.Name == "Zeta"));
    }

    [TestMethod]
    public void ApplyPendingRefresh_WithoutEdit_DoesNothing()
    {
        using var env = new ListManagerEnv(new FakeEntryBase { ID = 1, Name = "Alpha" });
        env.Manager.RefreshGameList();

        Assert.IsFalse(env.Manager.HasPendingListRefresh);
        Assert.IsFalse(env.Manager.ApplyPendingRefresh());
    }

    [TestMethod]
    public void SelectingAnotherEntry_AppliesPendingRefreshAndRaisesSelectionChanged()
    {
        var first = new FakeEntryBase { ID = 1, Name = "Alpha", SortAs = "Alpha" };
        var second = new FakeEntryBase { ID = 2, Name = "Beta", SortAs = "Beta" };
        using var env = new ListManagerEnv(first, second);
        env.Manager.RefreshGameList();

        IAppInfoEntry? raised = null;
        env.Manager.SelectedEntryChanged += (_, entry) => raised = entry;

        first.Name = "Zeta";
        env.Manager.NotifyEntryEdited(first);
        Assert.IsTrue(env.Manager.HasPendingListRefresh);

        // 点选另一条游戏 -> 列表重建并通知选中项
        env.Select(1);

        Assert.IsFalse(env.Manager.HasPendingListRefresh);
        Assert.AreSame(second, raised);
        Assert.AreEqual("Zeta", env.Manager.ViewModel.GetDisplayName(first));
        Assert.AreSame(second, env.Manager.GetSelectedEntry());
    }

    private sealed class ListManagerEnv : IDisposable
    {
        private readonly ListView _listView = new();
        private readonly ContextMenuStrip _filterMenu = new();
        private readonly ContextMenuStrip _sortMenu = new();
        private readonly ToolTip _toolTips = new();
        private readonly ClearableTextBox _filterText = new();

        public ListManagerEnv(params IAppInfoEntry[] entries)
        {
            _listView.VirtualMode = true;
            // 触发句柄创建：虚拟模式下 SelectedIndices 的索引校验依赖真实的 VirtualListSize
            _ = _listView.Handle;
            var store = new FakeStore(entries);
            ViewModel = new GameListViewModel(store);
            Manager = new GameListManager(new GameDataContext(), ViewModel, store,
                _listView, _filterMenu, _sortMenu, _toolTips, _filterText);
        }

        public GameListViewModel ViewModel { get; }

        public GameListManager Manager { get; }

        /// <summary>
        /// 模拟用户点选列表中的某一行。
        /// </summary>
        public void Select(int index)
        {
            _listView.SelectedIndices.Clear();
            _listView.SelectedIndices.Add(index);
        }

        public void Dispose()
        {
            Manager.Dispose();
            _listView.Dispose();
            _filterMenu.Dispose();
            _sortMenu.Dispose();
            _toolTips.Dispose();
            _filterText.Dispose();
        }
    }

    private sealed class FakeStore : IAppInfoDataStore
    {
        public FakeStore(IEnumerable<IAppInfoEntry> entries) => AppInfo = new FakeAppInfo(entries);

        public IAppInfo? AppInfo { get; set; }

        public bool IsDataLoaded => AppInfo != null;
    }

    private sealed class FakeAppInfo : IAppInfo
    {
        private readonly IAppInfoEntry[] _entries;

        public FakeAppInfo(IEnumerable<IAppInfoEntry> entries) => _entries = entries.ToArray();

        public uint MagicNumber => 0;

        public event EventHandler? Modified { add { } remove { } }

        public IEnumerable<IAppInfoEntry> Entries => _entries;

        public IAppInfoEntry? FindEntry(uint AppID) => _entries.FirstOrDefault(e => e.ID == AppID);

        public bool RevertEntry(IAppInfoEntry entry) => true;

        public void RemoveEntry(IAppInfoEntry entry) { }

        public void Save(string path, IFileSystem? fileSystem = null) { }
    }
}
