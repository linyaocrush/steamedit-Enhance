#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Common;
using steamedit_Enhance.Data;
using steamedit_Enhance.ViewModels;

namespace steamedit_Enhance;

internal sealed class GameListMenuBuilder
{
	private readonly GameListViewModel _viewModel;
	private readonly ContextMenuStrip _filterSelectorMenu;
	private readonly ContextMenuStrip _sortSelectorMenu;
	private readonly ContextMenuStrip _contextMenu;

	public ToolStripMenuItem CopyNameMenuItem { get; }
	public ToolStripMenuItem CopyAppIdMenuItem { get; }
	public ToolStripMenuItem OpenInSteamMenuItem { get; }
	public ToolStripMenuItem OpenInSteamDBMenuItem { get; }
	public ToolStripMenuItem BatchRenameMenuItem { get; }
	public ToolStripMenuItem BatchChangeTypeMenuItem { get; }
	public ToolStripMenuItem BatchMarkHiddenMenuItem { get; }
	public ToolStripMenuItem BatchMarkVisibleMenuItem { get; }
	public ToolStripMenuItem BatchMarkCompletedMenuItem { get; }
	public ToolStripMenuItem BatchMarkIncompleteMenuItem { get; }
	public ToolStripMenuItem BatchResetMenuItem { get; }
	public ToolStripMenuItem OfficialNamesMenu { get; }
	public ToolStripMenuItem FetchOfficialNamesMenuItem { get; }
	public ToolStripMenuItem ClearOfficialNameCacheMenuItem { get; }

	public event Action<AppInfoEntryType>? FilterTypeChanged;
	public event Action<ShowEntriesMode>? ShowModeChanged;
	public event Action<string>? SortFieldChanged;
	public event Action<bool>? SortDirectionChanged;

	public GameListMenuBuilder(
		GameListViewModel viewModel,
		Dictionary<AppInfoEntryType, Image> filterImages,
		ContextMenuStrip filterSelectorMenu,
		ContextMenuStrip sortSelectorMenu,
		ListView appSelection)
	{
		_viewModel = viewModel;
		_filterSelectorMenu = filterSelectorMenu;
		_sortSelectorMenu = sortSelectorMenu;
		_contextMenu = new ContextMenuStrip();

		// 筛选器菜单
		foreach (var kv in filterImages)
		{
			var item = CreateMenuItem(I18n.GetEnumDisplayName(kv.Key), kv.Key, _viewModel.FilterFlags.HasFlag(kv.Key), kv.Value);
			item.CheckedChanged += OnFilterCheckedChanged;
			_filterSelectorMenu.Items.Add(item);
		}
		_filterSelectorMenu.Items.Add(new ToolStripSeparator());
		AddShowEntriesMenuItem(Strings.MainWindow_Filter_All, ShowEntriesMode.All);
		AddShowEntriesMenuItem(Strings.MainWindow_Filter_OnlyModified, ShowEntriesMode.OnlyModified);
		AddShowEntriesMenuItem(Strings.MainWindow_Filter_OnlyUnmodified, ShowEntriesMode.OnlyUnmodified);

		// 排序菜单
		{
			var sortByName = CreateMenuItem(Strings.Sort_ByName, "Name", _viewModel.SortField == "Name");
			sortByName.Click += OnSortFieldClick;
			_sortSelectorMenu.Items.Add(sortByName);
			var sortById = CreateMenuItem(Strings.Sort_ById, "AppId", _viewModel.SortField == "AppId");
			sortById.Click += OnSortFieldClick;
			_sortSelectorMenu.Items.Add(sortById);
		}
		_sortSelectorMenu.Items.Add(new ToolStripSeparator());
		{
			var sortAsc = CreateMenuItem(Strings.Sort_Ascending, false, !_viewModel.SortDescending);
			sortAsc.Click += OnSortDirectionClick;
			_sortSelectorMenu.Items.Add(sortAsc);
			var sortDesc = CreateMenuItem(Strings.Sort_Descending, true, _viewModel.SortDescending);
			sortDesc.Click += OnSortDirectionClick;
			_sortSelectorMenu.Items.Add(sortDesc);
		}

		// 右键上下文菜单
		CopyNameMenuItem = new ToolStripMenuItem(Strings.ContextMenu_CopyName);
		CopyAppIdMenuItem = new ToolStripMenuItem(Strings.ContextMenu_CopyAppId);
		OpenInSteamMenuItem = new ToolStripMenuItem(Strings.ContextMenu_OpenInSteam);
		OpenInSteamDBMenuItem = new ToolStripMenuItem(Strings.ContextMenu_OpenInSteamDB);

		_contextMenu.Items.Add(CopyNameMenuItem);
		_contextMenu.Items.Add(CopyAppIdMenuItem);
		_contextMenu.Items.Add(new ToolStripSeparator());
		_contextMenu.Items.Add(OpenInSteamMenuItem);
		_contextMenu.Items.Add(OpenInSteamDBMenuItem);
		_contextMenu.Items.Add(new ToolStripSeparator());

		BatchRenameMenuItem = new ToolStripMenuItem(Strings.ContextMenu_BatchRename);
		BatchChangeTypeMenuItem = new ToolStripMenuItem(Strings.ContextMenu_BatchChangeType);
		BatchMarkHiddenMenuItem = new ToolStripMenuItem(Strings.ContextMenu_BatchMarkHidden);
		BatchMarkVisibleMenuItem = new ToolStripMenuItem(Strings.ContextMenu_BatchMarkVisible);
		BatchMarkCompletedMenuItem = new ToolStripMenuItem(Strings.ContextMenu_BatchMarkCompleted);
		BatchMarkIncompleteMenuItem = new ToolStripMenuItem(Strings.ContextMenu_BatchMarkIncomplete);
		BatchResetMenuItem = new ToolStripMenuItem(Strings.ContextMenu_BatchReset);

		var batchMenu = new ToolStripMenuItem(Strings.ContextMenu_Batch);
		batchMenu.DropDownItems.Add(BatchRenameMenuItem);
		batchMenu.DropDownItems.Add(BatchChangeTypeMenuItem);
		var markMenu = new ToolStripMenuItem(Strings.ContextMenu_BatchMark);
		markMenu.DropDownItems.Add(BatchMarkHiddenMenuItem);
		markMenu.DropDownItems.Add(BatchMarkVisibleMenuItem);
		markMenu.DropDownItems.Add(new ToolStripSeparator());
		markMenu.DropDownItems.Add(BatchMarkCompletedMenuItem);
		markMenu.DropDownItems.Add(BatchMarkIncompleteMenuItem);
		batchMenu.DropDownItems.Add(markMenu);
		batchMenu.DropDownItems.Add(BatchResetMenuItem);
		_contextMenu.Items.Add(batchMenu);
		_contextMenu.Items.Add(new ToolStripSeparator());

		FetchOfficialNamesMenuItem = new ToolStripMenuItem(Strings.ContextMenu_FetchOfficialNames);
		ClearOfficialNameCacheMenuItem = new ToolStripMenuItem(Strings.ContextMenu_ClearOfficialNameCache);
		OfficialNamesMenu = new ToolStripMenuItem(Strings.ContextMenu_OfficialNames);
		OfficialNamesMenu.DropDownItems.Add(FetchOfficialNamesMenuItem);
		OfficialNamesMenu.DropDownItems.Add(ClearOfficialNameCacheMenuItem);
		_contextMenu.Items.Add(OfficialNamesMenu);

		_filterSelectorMenu.Closing += (_, e) => { if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true; };
		_sortSelectorMenu.Closing += (_, e) => { if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true; };

		appSelection.ContextMenuStrip = _contextMenu;

		ThemeManager.StyleContextMenuStrip(_filterSelectorMenu);
		ThemeManager.StyleContextMenuStrip(_sortSelectorMenu);
		ThemeManager.StyleContextMenuStrip(_contextMenu);
	}

	private static ToolStripMenuItem CreateMenuItem<T>(string text, T tag, bool isChecked, Image? icon = null)
	{
		return new ToolStripMenuItem(text, icon)
		{
			Tag = tag,
			CheckOnClick = true,
			Checked = isChecked,
			DisplayStyle = icon != null ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Text
		};
	}

	private void AddShowEntriesMenuItem(string name, ShowEntriesMode mode)
	{
		var mi = CreateMenuItem(name, mode, _viewModel.ShowMode == mode);
		mi.Click += OnShowEntriesModeClick;
		_filterSelectorMenu.Items.Add(mi);
	}

	public void ApplyLanguage()
	{
		foreach (object item in _filterSelectorMenu.Items)
		{
			if (item is ToolStripMenuItem mi)
			{
				if (mi.Tag is ShowEntriesMode mode)
					mi.Text = mode switch
					{
						ShowEntriesMode.All => Strings.MainWindow_Filter_All,
						ShowEntriesMode.OnlyModified => Strings.MainWindow_Filter_OnlyModified,
						ShowEntriesMode.OnlyUnmodified => Strings.MainWindow_Filter_OnlyUnmodified,
						_ => mi.Text
					};
				else if (mi.Tag is AppInfoEntryType entryType)
					mi.Text = I18n.GetEnumDisplayName(entryType);
			}
		}
	}

	private void OnFilterCheckedChanged(object? sender, EventArgs e)
	{
		if (sender is not ToolStripMenuItem item) return;
		var type = (AppInfoEntryType)item.Tag!;
		FilterTypeChanged?.Invoke(type);
	}

	private void OnShowEntriesModeClick(object? sender, EventArgs e)
	{
		if (sender is not ToolStripMenuItem item) return;
		var mode = (ShowEntriesMode)item.Tag!;
		ShowModeChanged?.Invoke(mode);
	}

	private void OnSortFieldClick(object? sender, EventArgs e)
	{
		if (sender is not ToolStripMenuItem item || item.Tag is not string field) return;
		SortFieldChanged?.Invoke(field);
	}

	private void OnSortDirectionClick(object? sender, EventArgs e)
	{
		if (sender is not ToolStripMenuItem item || item.Tag is not bool descending) return;
		SortDirectionChanged?.Invoke(descending);
	}
}
