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

	public GameListManager(
		GameDataContext ctx,
		GameListViewModel viewModel,
		ListView appSelection,
		ContextMenuStrip filterSelectorMenu,
		ContextMenuStrip sortSelectorMenu,
		ToolTip toolTips,
		ClearableTextBox appFilterText)
	{
		_ctx = ctx;
		_viewModel = viewModel;
		_dataStore = _viewModel.DataStore;
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
		InputHandler = new GameListInputHandler(Drawer, appSelection, toolTips, idx => GetEntryToolTip(_ctx.Entries[idx]), _ctx.Entries);
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

		MenuBuilder.CopyNameRequested = () => ContextMenuCopyName?.Invoke(this, EventArgs.Empty);
		MenuBuilder.CopyAppIdRequested = () => ContextMenuCopyAppId?.Invoke(this, EventArgs.Empty);
		MenuBuilder.OpenInSteamRequested = () => ContextMenuOpenInSteam?.Invoke(this, EventArgs.Empty);
		MenuBuilder.OpenInSteamDBRequested = () => ContextMenuOpenInSteamDB?.Invoke(this, EventArgs.Empty);

		MenuBuilder.BatchRenameMenuItem.Click += (_, _) => BatchRename?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchChangeTypeMenuItem.Click += (_, _) => BatchChangeType?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchMarkHiddenMenuItem.Click += (_, _) => BatchMarkAsHidden?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchMarkVisibleMenuItem.Click += (_, _) => BatchMarkAsVisible?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchMarkCompletedMenuItem.Click += (_, _) => BatchMarkAsCompleted?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchMarkIncompleteMenuItem.Click += (_, _) => BatchMarkAsIncomplete?.Invoke(this, EventArgs.Empty);
		MenuBuilder.BatchResetMenuItem.Click += (_, _) => BatchReset?.Invoke(this, EventArgs.Empty);

		// 定时器
		_filterDebounceTimer.Interval = 500;
		_filterDebounceTimer.Tick += (_, _) =>
		{
			_filterDebounceTimer.Stop();
			OnSearchTextChanged();
		};

		// 列表事件
		appSelection.SelectedIndexChanged += OnAppSelection_SelectedIndexChanged;
		appSelection.SizeChanged += (_, _) =>
		{
			if (appSelection.Columns.Count > 0)
				appSelection.Columns[0].Width = appSelection.ClientSize.Width - 4;
			_appSelection.Invalidate();
		};
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
	public bool RefreshingAfterEdit { get => _refreshingGamesListAfterEdit; set => _refreshingGamesListAfterEdit = value; }
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
	public void InvalidateDisplayName(IAppInfoEntry entry) => _viewModel.InvalidateDisplayName(entry);
	public void ClearDisplayNameCache() => _viewModel.ClearDisplayNameCache();

	public void RefreshGameList()
	{
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
		int idx = _ctx._entries.IndexOf(entry);
		if (idx >= 0)
			Drawer.InvalidateItem(idx);
	}

	public void InvalidateAll() => Drawer.InvalidateAll();

	public void NotifyEntryEdited(IAppInfoEntry entry)
	{
		InvalidateDisplayName(entry);
		_refreshingGamesListAfterEdit = true;
		ResortEntry(entry);
		_refreshingGamesListAfterEdit = false;
		InvalidateItem(entry);
	}

	public void NotifyStatusChanged() => InvalidateAll();

	public void NotifyEntryReverted(IAppInfoEntry entry)
	{
		ClearDisplayNameCache();
		_viewModel.Refresh();
		SyncFromViewModel();
		int idx = _ctx._entries.IndexOf(entry);
		if (idx >= 0) SelectIndex(idx);
	}

	// --- 私有方法 ---

	private void SyncFromViewModel()
	{
		_ctx._entries = new List<IAppInfoEntry>(_viewModel.VisibleGames.Count);
		_ctx._baseEntries = new List<IAppInfoEntry>(_viewModel.AllGames.Count);
		foreach (var g in _viewModel.AllGames)
			_ctx._baseEntries.Add(g.Entry);
		foreach (var g in _viewModel.VisibleGames)
			_ctx._entries.Add(g.Entry);
		_ctx.TotalEntryCount = _viewModel.TotalEntryCount;
		_ctx.CurrentSearchTerm = _viewModel.SearchText;
		RebuildListBoxItems();
	}

	private void RebuildListBoxItems()
	{
		_appSelection.VirtualListSize = _ctx.Entries.Count;
		_appSelection.Invalidate();
	}

	private void RestoreSelection(IAppInfoEntry? selected)
	{
		if (selected != null)
		{
			int newIdx = _ctx._entries.IndexOf(selected);
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
		if (!_refreshingGamesListAfterEdit)
			SelectedEntryChanged?.Invoke(this, GetSelectedEntry()!);
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
	}

	private void OnEntryModified(object? sender, EventArgs e)
	{
		ClearDisplayNameCache();
		_viewModel.Refresh();
		SyncFromViewModel();
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
