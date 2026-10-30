#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Common;
using steamedit_Enhance.Data;
using steamedit_Enhance.Properties;
using steamedit_Enhance.ViewModels;

namespace steamedit_Enhance;

internal class GameListManager : IDisposable
{
	private readonly GameDataContext _ctx;
	private readonly GameListViewModel _viewModel;
	private readonly IAppInfoDataStore _dataStore;
	private readonly ListView _appSelection;
	private readonly ClearableTextBox _appFilterText;
	private readonly Timer _filterDebounceTimer = new();
	private readonly EventHandler _onLanguageChangedHandler;
	private bool _refreshingGamesListAfterEdit;
	private bool _pendingListRefresh;

	public GameListDrawer Drawer { get; }
	public GameListInputHandler InputHandler { get; }
	public GameListMenuBuilder MenuBuilder { get; }

	public event EventHandler<IAppInfoEntry>? SelectedEntryChanged;
	public event EventHandler? ContextMenuCopyName;
	public event EventHandler? ContextMenuCopyAppId;
	public event EventHandler? ContextMenuOpenInSteam;
	public event EventHandler? ContextMenuOpenInSteamDB;
	public event EventHandler? BatchRename;
	public event EventHandler? BatchChangeType;
	public event EventHandler? BatchMarkAsHidden;
	public event EventHandler? BatchMarkAsVisible;
	public event EventHandler? BatchMarkAsCompleted;
	public event EventHandler? BatchMarkAsIncomplete;
	public event EventHandler? BatchReset;
	public event EventHandler? FetchOfficialNames;
	public event EventHandler? ClearOfficialNameCache;

	public GameListManager(
		GameDataContext ctx,
		GameListViewModel viewModel,
		IAppInfoDataStore dataStore,
		ListView appSelection,
		ContextMenuStrip filterSelectorMenu,
		ContextMenuStrip sortSelectorMenu,
		ToolTip toolTips,
		ClearableTextBox appFilterText)
	{
		_ctx = ctx;
		_viewModel = viewModel;
		_dataStore = dataStore;
		_appSelection = appSelection;
		_appFilterText = appFilterText;

		// 构建筛选器图标字典
		var filterImages = new Dictionary<AppInfoEntryType, Image>
		{
			{ AppInfoEntryType.Game, Resources.filter_game },
			{ AppInfoEntryType.Application, Resources.filter_application },
			{ AppInfoEntryType.Video, Resources.filter_video },
			{ AppInfoEntryType.Music, Resources.filter_music },
			{ AppInfoEntryType.Guide, Resources.filter_guide },
			{ AppInfoEntryType.DLC, Resources.filter_dlc },
			{ AppInfoEntryType.Demo, Resources.filter_demo },
			{ AppInfoEntryType.Tool, Resources.filter_tool },
			{ AppInfoEntryType.Config, Resources.filter_config },
			{ AppInfoEntryType.Beta, Resources.filter_beta },
			{ AppInfoEntryType.Unknown, Resources.filter_unknown }
		};

		// 创建子组件
		Drawer = new GameListDrawer(viewModel, appSelection, _ctx, filterImages);
		InputHandler = new GameListInputHandler(Drawer, appSelection, toolTips, idx => GetEntryToolTip(_ctx.Entries[idx]));
		MenuBuilder = new GameListMenuBuilder(viewModel, filterImages, filterSelectorMenu, sortSelectorMenu, appSelection);

		// 菜单事件转发
		MenuBuilder.FilterTypeChanged += type =>
		{
			AppInfoEntryType flags = _viewModel.FilterFlags;
			flags ^= type;
			_viewModel.FilterFlags = flags;
			SyncFromViewModel();
		};
		MenuBuilder.ShowModeChanged += mode =>
		{
			_viewModel.ShowMode = mode;
			SyncFromViewModel();
		};
		MenuBuilder.SortFieldChanged += field =>
		{
			_viewModel.SortField = field;
			SyncFromViewModel();
		};
		MenuBuilder.SortDirectionChanged += descending =>
		{
			_viewModel.SortDescending = descending;
			SyncFromViewModel();
		};

		MenuBuilder.CopyNameMenuItem.Click += (_, _) => ContextMenuCopyName?.Invoke(this, EventArgs.Empty);
		MenuBuilder.CopyAppIdMenuItem.Click += (_, _) => ContextMenuCopyAppId?.Invoke(this, EventArgs.Empty);
		MenuBuilder.OpenInSteamMenuItem.Click += (_, _) => ContextMenuOpenInSteam?.Invoke(this, EventArgs.Empty);
		MenuBuilder.OpenInSteamDBMenuItem.Click += (_, _) => ContextMenuOpenInSteamDB?.Invoke(this, EventArgs.Empty);

		MenuBuilder.BatchRenameMenuItem.Click += (_, _) => BatchRename?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchChangeTypeMenuItem.Click += (_, _) => BatchChangeType?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchMarkHiddenMenuItem.Click += (_, _) => BatchMarkAsHidden?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchMarkVisibleMenuItem.Click += (_, _) => BatchMarkAsVisible?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchMarkCompletedMenuItem.Click += (_, _) => BatchMarkAsCompleted?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchMarkIncompleteMenuItem.Click += (_, _) => BatchMarkAsIncomplete?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchResetMenuItem.Click += (_, _) => BatchReset?.Invoke(this, EventArgs.Empty);
		MenuBuilder.FetchOfficialNamesMenuItem.Click += (_, _) => FetchOfficialNames?.Invoke(this, EventArgs.Empty);
		MenuBuilder.ClearOfficialNameCacheMenuItem.Click += (_, _) => ClearOfficialNameCache?.Invoke(this, EventArgs.Empty);

		// 定时器
		_filterDebounceTimer.Interval = 500;
		_filterDebounceTimer.Tick += (_, _) =>
		{
			_filterDebounceTimer.Stop();
			OnSearchTextChanged();
		};

		// 列表事件
		appSelection.SelectedIndexChanged += OnAppSelection_SelectedIndexChanged;
		// 监听 ClientSizeChanged：滚动条出现/消失时控件 Size 可能不变而 ClientSize 变化，
		// 只看 SizeChanged 会让列宽停留在旧值上。
		appSelection.ClientSizeChanged += (_, _) => UpdateListColumnWidth();
		appSelection.RetrieveVirtualItem += (_, e) => e.Item = new ListViewItem();

		// 搜索文本
		appFilterText.TextChanged += (_, _) =>
		{
			_filterDebounceTimer.Stop();
			_filterDebounceTimer.Start();
		};

		// 批处理菜单语言变更
		_onLanguageChangedHandler = (_, _) => ApplyBatchLanguage();
		I18n.LanguageChanged += _onLanguageChangedHandler;

		_dataStore.AppInfo.Modified += OnEntryModified;
	}

	public AppInfoEntryType CurrentFilters => _viewModel.FilterFlags;
	public GameListViewModel ViewModel => _viewModel;
	public bool RefreshingAfterEdit => _refreshingGamesListAfterEdit;
	public ImageAttributes BgLogoAttributes => Drawer.BgLogoAttributes;
	public Dictionary<AppInfoEntryType, Image> GetFilterImages() => Drawer.FilterImages;

	// --- 公共方法 ---

	public IAppInfoEntry? GetSelectedEntry()
	{
		int idx = GetSelectedIndex();
		return idx >= 0 && idx < _ctx.Entries.Count ? _ctx.Entries[idx] : null;
	}

	public IReadOnlyList<IAppInfoEntry> GetSelectedEntries()
	{
		var entries = new List<IAppInfoEntry>();
		foreach (int idx in _appSelection.SelectedIndices)
		{
			if (idx >= 0 && idx < _ctx.Entries.Count)
				entries.Add(_ctx.Entries[idx]);
		}
		return entries.AsReadOnly();
	}

	public string GetDisplayName(IAppInfoEntry entry) => _viewModel.GetDisplayName(entry);
	public string GetCopyableDisplayName(IAppInfoEntry entry) => _viewModel.GetRawDisplayName(entry);
	public void InvalidateDisplayName(IAppInfoEntry entry) => _viewModel.InvalidateDisplayName(entry);
	public void ClearDisplayNameCache() => _viewModel.ClearDisplayNameCache();

	public void RefreshGameList()
	{
		_pendingListRefresh = false;
		var selected = GetSelectedEntry();
		_viewModel.Refresh();
		SyncFromViewModel();
		RestoreSelection(selected);
	}

	public void ResortEntry(IAppInfoEntry entry)
	{
		var selected = GetSelectedEntry();
		var item = _viewModel.FindByEntry(entry);
		if (item == null) return;
		_viewModel.ResortEntry(item);
		SyncFromViewModel();
		RestoreSelection(selected);
	}

	public void SortEntries()
	{
		var selected = GetSelectedEntry();
		_viewModel.ApplySort();
		SyncFromViewModel();
		RestoreSelection(selected);
	}

	public void ApplyLanguageToFilters() => MenuBuilder.ApplyLanguage();

	public void SelectIndex(int idx)
	{
		if (idx >= 0 && idx < _ctx.Entries.Count)
			SetSelectedIndex(idx);
	}

	public void InvalidateItem(IAppInfoEntry entry)
	{
		int idx = _ctx.IndexOf(entry);
		if (idx >= 0)
			Drawer.InvalidateItem(idx);
	}

	public void InvalidateAll() => Drawer.InvalidateAll();

	public void NotifyEntryEdited(IAppInfoEntry entry)
	{
		// 编辑（例如修改游戏名称）时不立即重建列表：保留显示名缓存与当前选中项，
		// 等用户点选另一条游戏后再统一刷新（见 OnAppSelection_SelectedIndexChanged）。
		_pendingListRefresh = true;
		InvalidateItem(entry);
	}

	public void NotifyStatusChanged() => InvalidateAll();

	public void NotifyEntryReverted(IAppInfoEntry entry)
	{
		_pendingListRefresh = false;
		ClearDisplayNameCache();
		_viewModel.Refresh();
		SyncFromViewModel();
		int idx = _ctx.IndexOf(entry);
		if (idx >= 0) SelectIndex(idx);
	}

	// --- 私有方法 ---

	private void SyncFromViewModel()
	{
		// 列表已经整体重建，编辑后遗留的延迟刷新标记不再需要
		_pendingListRefresh = false;
		var allEntries = new List<IAppInfoEntry>(_viewModel.AllGames.Count);
		var visibleEntries = new List<IAppInfoEntry>(_viewModel.VisibleGames.Count);
		foreach (var g in _viewModel.AllGames)
			allEntries.Add(g.Entry);
		foreach (var g in _viewModel.VisibleGames)
			visibleEntries.Add(g.Entry);
		_ctx.ReloadEntries(visibleEntries, allEntries);
		_ctx.TotalEntryCount = _viewModel.TotalEntryCount;
		_ctx.CurrentSearchTerm = _viewModel.SearchText;
		RebuildListBoxItems();
	}

	/// <summary>
	/// 让列宽跟随客户区宽度变化，避免窗口缩放（或分栏拖动）后列宽与可视区域失配。
	/// </summary>
	private void UpdateListColumnWidth()
	{
		if (_appSelection.Columns.Count == 0) return;
		int width = Math.Max(0, _appSelection.ClientSize.Width - 4);
		if (_appSelection.Columns[0].Width != width)
			_appSelection.Columns[0].Width = width;
	}

	private void RebuildListBoxItems()
	{
		int count = _ctx.Entries.Count;
		if (_appSelection.VirtualListSize != count)
			_appSelection.VirtualListSize = count;
		UpdateListColumnWidth();
		// 虚拟列表重建后必须整块重绘：只调用 Invalidate() 时，WinForms 可能只重绘局部区域，
		// 未被重绘的行会残留旧像素——表现为切换筛选后大量空白行、点击到某行后才显示名称。
		_appSelection.Refresh();
	}

	private void RestoreSelection(IAppInfoEntry? selected)
	{
		if (selected != null)
		{
			int newIdx = _ctx.IndexOf(selected);
			if (newIdx >= 0) SetSelectedIndex(newIdx);
		}
		if (GetSelectedIndex() < 0 && _ctx.Entries.Count > 0)
			SetSelectedIndex(0);
	}

	private int GetSelectedIndex() => _appSelection.SelectedIndices.Count > 0 ? _appSelection.SelectedIndices[0] : -1;

	private void SetSelectedIndex(int idx)
	{
		_appSelection.SelectedIndices.Clear();
		if (idx >= 0) _appSelection.SelectedIndices.Add(idx);
	}

	private void OnSearchTextChanged()
	{
		string text = _appFilterText.Text ?? "";
		if (text.Trim() != _viewModel.SearchText.Trim())
		{
			_viewModel.SearchText = text;
			SyncFromViewModel();
		}
	}

	private string GetEntryToolTip(IAppInfoEntry entry) => _viewModel.GetEntryToolTip(entry);

	private void OnAppSelection_SelectedIndexChanged(object? sender, EventArgs e)
	{
		if (_refreshingGamesListAfterEdit) return;

		ApplyPendingRefresh();

		var entry = GetSelectedEntry();
		if (entry != null)
			SelectedEntryChanged?.Invoke(this, entry);
	}

	/// <summary>
	/// 是否还有编辑后待应用的延迟刷新（点选另一条游戏时才应用）。
	/// </summary>
	internal bool HasPendingListRefresh => _pendingListRefresh;

	/// <summary>
	/// 编辑后延迟刷新：在用户点选另一条游戏时重建列表，并保持当前选中的条目。
	/// </summary>
	/// <returns>本次是否执行了刷新。</returns>
	internal bool ApplyPendingRefresh()
	{
		if (!_pendingListRefresh) return false;
		_pendingListRefresh = false;
		RefreshAfterDeferredEdit();
		return true;
	}

	private void RefreshAfterDeferredEdit()
	{
		var selected = GetSelectedEntry();
		_refreshingGamesListAfterEdit = true;
		try
		{
			ClearDisplayNameCache();
			_viewModel.Refresh();
			SyncFromViewModel();
			RestoreSelection(selected);
		}
		finally
		{
			_refreshingGamesListAfterEdit = false;
		}
	}

	private void ApplyBatchLanguage()
	{
		MenuBuilder.BatchRenameMenuItem.Text = Strings.ContextMenu_BatchRename;
		MenuBuilder.BatchChangeTypeMenuItem.Text = Strings.ContextMenu_BatchChangeType;
		MenuBuilder.BatchMarkHiddenMenuItem.Text = Strings.ContextMenu_BatchMarkHidden;
		MenuBuilder.BatchMarkVisibleMenuItem.Text = Strings.ContextMenu_BatchMarkVisible;
		MenuBuilder.BatchMarkCompletedMenuItem.Text = Strings.ContextMenu_BatchMarkCompleted;
		MenuBuilder.BatchMarkIncompleteMenuItem.Text = Strings.ContextMenu_BatchMarkIncomplete;
		MenuBuilder.BatchResetMenuItem.Text = Strings.ContextMenu_BatchReset;
		MenuBuilder.OfficialNamesMenu.Text = Strings.ContextMenu_OfficialNames;
		MenuBuilder.FetchOfficialNamesMenuItem.Text = Strings.ContextMenu_FetchOfficialNames;
		MenuBuilder.ClearOfficialNameCacheMenuItem.Text = Strings.ContextMenu_ClearOfficialNameCache;
	}

	private void OnEntryModified(object? sender, EventArgs e)
	{
		// AppInfo.Modified 会在每次属性变化时触发。这里只标记待刷新，
		// 避免修改游戏名称时左侧列表立即重建。
		_pendingListRefresh = true;
	}

	public void Dispose()
	{
		I18n.LanguageChanged -= _onLanguageChangedHandler;
		_filterDebounceTimer?.Dispose();
		Drawer?.Dispose();
		InputHandler?.Dispose();
		_dataStore.AppInfo.Modified -= OnEntryModified;
	}
}
