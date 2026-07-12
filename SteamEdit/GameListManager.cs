#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Common;
using SteamEdit.Data;
using SteamEdit.Properties;

namespace SteamEdit;

internal class GameListManager : IDisposable
{
	private const int IconSpacing = 6;

	private readonly GameDataContext _ctx;
	private readonly IAppInfoDataStore _dataStore;
	private readonly ListBox _appSelection;
	private readonly ContextMenuStrip _filterSelectorMenu;
	private readonly ContextMenuStrip _sortSelectorMenu;
	private readonly ToolTip _toolTips;
	private readonly ClearableTextBox _appFilterText;
	private readonly ToolStripStatusLabel _lblNumAppsShown;
	private readonly ToolStripStatusLabel _lblModifiedCount;

	private AppInfoEntryType _currentFilters;
	private Dictionary<AppInfoEntryType, Image> _filterImages;
	private readonly Font _defaultAppSelectionFont;
	private readonly Font _boldAppSelectionFont;
	private readonly Font _hiddenAppSelectionFont;
	private readonly ImageAttributes _bgLogoAttributes;

	private readonly SolidBrush _brushListBackColor;
	private readonly SolidBrush _brushListAlternateBackColor;
	private readonly SolidBrush _brushListSelectedBackColor;
	private readonly SolidBrush _brushListSelectedUnfocusedBackColor;
	private readonly SolidBrush _brushAccentLightAlt;
	private readonly Pen _penBorder;
	private readonly GraphicsPath _cachedSelectionPath = new();

	private readonly Timer _appSearchTermTimer = new();
	private readonly Timer _filterDebounceTimer = new();
	private readonly Timer _statusBarDebounceTimer = new();
	private readonly StringBuilder _appSearchTerm = new();
	private int _appSelectionLastHoverI = -1;
	private bool _refreshingGamesListAfterEdit;
	private int _modifiedCount;
	private bool _modifiedCountDirty = true;
	private int _tooltipCachedId = -1;
	private string _tooltipCachedText = "";

	private readonly EventHandler? _onLanguageChanged;

	private readonly ToolStripMenuItem _miBatch;
	private readonly ToolStripMenuItem _miBatchRename;
	private readonly ToolStripMenuItem _miBatchChangeType;
	private readonly ToolStripMenuItem _miBatchMark;
	private readonly ToolStripMenuItem _miMarkHidden;
	private readonly ToolStripMenuItem _miMarkVisible;
	private readonly ToolStripMenuItem _miMarkCompleted;
	private readonly ToolStripMenuItem _miMarkIncomplete;
	private readonly ToolStripMenuItem _miBatchReset;

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
		IAppInfoDataStore dataStore,
		ListBox appSelection,
		ContextMenuStrip filterSelectorMenu,
		ContextMenuStrip sortSelectorMenu,
		ToolTip toolTips,
		ClearableTextBox appFilterText,
		ToolStripStatusLabel lblNumAppsShown,
		ToolStripStatusLabel lblModifiedCount)
	{
		_ctx = ctx;
		_dataStore = dataStore;
		_appSelection = appSelection;
		_filterSelectorMenu = filterSelectorMenu;
		_sortSelectorMenu = sortSelectorMenu;
		_toolTips = toolTips;
		_appFilterText = appFilterText;
		_lblNumAppsShown = lblNumAppsShown;
		_lblModifiedCount = lblModifiedCount;

		_currentFilters = Settings.Default.Filters;

		// 构建灰度 ColorMatrix
		float[][] gray = new float[3][]
		{
			new float[3] { 0.3f, 0.3f, 0.3f },
			new float[3] { 0.59f, 0.59f, 0.59f },
			new float[3] { 0.11f, 0.11f, 0.11f }
		};
		float[][] identity = new float[3][]
		{
			new float[3] { 1f, 0f, 0f },
			new float[3] { 0f, 1f, 0f },
			new float[3] { 0f, 0f, 1f }
		};
		float blend = 0.5f;
		ColorMatrix cm = new ColorMatrix();
		for (int i = 0; i < 3; i++)
			for (int j = 0; j < 3; j++)
				cm[i, j] = blend * gray[i][j] + (1f - blend) * identity[i][j];
		cm[3, 3] = 0.08f;
		_bgLogoAttributes = new ImageAttributes();
		_bgLogoAttributes.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

		_filterImages = new Dictionary<AppInfoEntryType, Image>
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

		_defaultAppSelectionFont = _appSelection.Font;
		_boldAppSelectionFont = new Font(_appSelection.Font, FontStyle.Bold);
		_hiddenAppSelectionFont = new Font(_appSelection.Font, FontStyle.Strikeout);

		_brushListBackColor = new SolidBrush(ThemeManager.ListBackColor);
		_brushListAlternateBackColor = new SolidBrush(ThemeManager.ListAlternateBackColor);
		_brushListSelectedBackColor = new SolidBrush(ThemeManager.ListSelectedBackColor);
		_brushListSelectedUnfocusedBackColor = new SolidBrush(ThemeManager.ListSelectedUnfocusedBackColor);
		_brushAccentLightAlt = new SolidBrush(ThemeManager.AccentLightAlt);
		_penBorder = new Pen(ThemeManager.BorderColor);

		// 构建筛选器菜单
		foreach (var kv in _filterImages)
		{
			var item = CreateMenuItem(I18n.GetEnumDisplayName(kv.Key), kv.Key, _currentFilters.HasFlag(kv.Key), kv.Value);
			item.CheckedChanged += OnFilter_CheckedChanged;
			_filterSelectorMenu.Items.Add(item);
		}
		_filterSelectorMenu.Items.Add(new ToolStripSeparator());
		AddShowEntriesMenuItem(Strings.MainWindow_Filter_All, ShowEntriesMode.All);
		AddShowEntriesMenuItem(Strings.MainWindow_Filter_OnlyModified, ShowEntriesMode.OnlyModified);
		AddShowEntriesMenuItem(Strings.MainWindow_Filter_OnlyUnmodified, ShowEntriesMode.OnlyUnmodified);

		// 构建排序菜单
		{
			var sortByName = CreateMenuItem(Strings.Sort_ByName, "Name", false);
			sortByName.Click += OnSortField_Click;
			_sortSelectorMenu.Items.Add(sortByName);
			var sortById = CreateMenuItem(Strings.Sort_ById, "AppId", false);
			sortById.Click += OnSortField_Click;
			_sortSelectorMenu.Items.Add(sortById);
		}
		_sortSelectorMenu.Items.Add(new ToolStripSeparator());
		{
			var sortAsc = CreateMenuItem(Strings.Sort_Ascending, false, false);
			sortAsc.Click += OnSortDirection_Click;
			_sortSelectorMenu.Items.Add(sortAsc);
			var sortDesc = CreateMenuItem(Strings.Sort_Descending, true, false);
			sortDesc.Click += OnSortDirection_Click;
			_sortSelectorMenu.Items.Add(sortDesc);
		}

		// Timer 设置
		_appSearchTermTimer.Interval = 500;
		_appSearchTermTimer.Tick += (_, _) => { _appSearchTerm.Clear(); _appSearchTermTimer.Stop(); };
		_filterDebounceTimer.Interval = 500;
		_filterDebounceTimer.Tick += (_, _) =>
		{
			_filterDebounceTimer.Stop();
			ApplyIncrementalSearch();
			UpdateStatusBar();
		};
		_statusBarDebounceTimer.Interval = 200;
		_statusBarDebounceTimer.Tick += (_, _) => { _statusBarDebounceTimer.Stop(); UpdateStatusBar(); };

		// 事件订阅
		_appSelection.DrawItem += OnAppSelection_DrawItem;
		_appSelection.SelectionMode = SelectionMode.MultiExtended;
		_appSelection.SelectedIndexChanged += OnAppSelection_SelectedIndexChanged;
		_appSelection.SizeChanged += (_, _) => _appSelection.Invalidate();
		_appSelection.KeyPress += OnAppSelection_KeyPress;
		_appSelection.MouseMove += OnAppSelection_MouseMove;
		_appSelection.MouseLeave += (_, _) =>
		{
			if (_appSelectionLastHoverI >= 0)
				_appSelection.Invalidate(_appSelection.GetItemRectangle(_appSelectionLastHoverI));
			_appSelectionLastHoverI = -1;
		};
		_appSelection.MouseDown += OnAppSelection_MouseDown;
		_appFilterText.TextChanged += OnAppFilterText_TextChanged;

		// 右键上下文菜单
		var contextMenu = new ContextMenuStrip();
		var miCopyName = new ToolStripMenuItem(Strings.ContextMenu_CopyName);
		miCopyName.Click += (_, _) => ContextMenuCopyName?.Invoke(this, EventArgs.Empty);
		var miCopyAppId = new ToolStripMenuItem(Strings.ContextMenu_CopyAppId);
		miCopyAppId.Click += (_, _) => ContextMenuCopyAppId?.Invoke(this, EventArgs.Empty);
		var miOpenSteam = new ToolStripMenuItem(Strings.ContextMenu_OpenInSteam);
		miOpenSteam.Click += (_, _) => ContextMenuOpenInSteam?.Invoke(this, EventArgs.Empty);
		var miOpenSteamDB = new ToolStripMenuItem(Strings.ContextMenu_OpenInSteamDB);
		miOpenSteamDB.Click += (_, _) => ContextMenuOpenInSteamDB?.Invoke(this, EventArgs.Empty);
		contextMenu.Items.Add(miCopyName);
		contextMenu.Items.Add(miCopyAppId);
		contextMenu.Items.Add(new ToolStripSeparator());
		contextMenu.Items.Add(miOpenSteam);
		contextMenu.Items.Add(miOpenSteamDB);

		contextMenu.Items.Add(new ToolStripSeparator());

		_miBatch = new ToolStripMenuItem(Strings.ContextMenu_Batch);
		_miBatchRename = new ToolStripMenuItem(Strings.ContextMenu_BatchRename);
		_miBatchRename.Click += (_, _) => BatchRename?.Invoke(this, EventArgs.Empty);
		_miBatch.DropDownItems.Add(_miBatchRename);

		_miBatchChangeType = new ToolStripMenuItem(Strings.ContextMenu_BatchChangeType);
		_miBatchChangeType.Click += (_, _) => BatchChangeType?.Invoke(this, EventArgs.Empty);
		_miBatch.DropDownItems.Add(_miBatchChangeType);

		_miBatchMark = new ToolStripMenuItem(Strings.ContextMenu_BatchMark);
		_miMarkHidden = new ToolStripMenuItem(Strings.ContextMenu_BatchMarkHidden);
		_miMarkHidden.Click += (_, _) => BatchMarkAsHidden?.Invoke(this, EventArgs.Empty);
		_miBatchMark.DropDownItems.Add(_miMarkHidden);
		_miMarkVisible = new ToolStripMenuItem(Strings.ContextMenu_BatchMarkVisible);
		_miMarkVisible.Click += (_, _) => BatchMarkAsVisible?.Invoke(this, EventArgs.Empty);
		_miBatchMark.DropDownItems.Add(_miMarkVisible);
		_miBatchMark.DropDownItems.Add(new ToolStripSeparator());
		_miMarkCompleted = new ToolStripMenuItem(Strings.ContextMenu_BatchMarkCompleted);
		_miMarkCompleted.Click += (_, _) => BatchMarkAsCompleted?.Invoke(this, EventArgs.Empty);
		_miBatchMark.DropDownItems.Add(_miMarkCompleted);
		_miMarkIncomplete = new ToolStripMenuItem(Strings.ContextMenu_BatchMarkIncomplete);
		_miMarkIncomplete.Click += (_, _) => BatchMarkAsIncomplete?.Invoke(this, EventArgs.Empty);
		_miBatchMark.DropDownItems.Add(_miMarkIncomplete);
		_miBatch.DropDownItems.Add(_miBatchMark);

		_miBatchReset = new ToolStripMenuItem(Strings.ContextMenu_BatchReset);
		_miBatchReset.Click += (_, _) => BatchReset?.Invoke(this, EventArgs.Empty);
		_miBatch.DropDownItems.Add(_miBatchReset);

		contextMenu.Items.Add(_miBatch);

		_onLanguageChanged = (_, _) =>
		{
			_miBatch.Text = Strings.ContextMenu_Batch;
			_miBatchRename.Text = Strings.ContextMenu_BatchRename;
			_miBatchChangeType.Text = Strings.ContextMenu_BatchChangeType;
			_miBatchMark.Text = Strings.ContextMenu_BatchMark;
			_miMarkHidden.Text = Strings.ContextMenu_BatchMarkHidden;
			_miMarkVisible.Text = Strings.ContextMenu_BatchMarkVisible;
			_miMarkCompleted.Text = Strings.ContextMenu_BatchMarkCompleted;
			_miMarkIncomplete.Text = Strings.ContextMenu_BatchMarkIncomplete;
			_miBatchReset.Text = Strings.ContextMenu_BatchReset;
		};
		I18n.LanguageChanged += _onLanguageChanged;

		_appSelection.ContextMenuStrip = contextMenu;

		_filterSelectorMenu.Closing += (_, e) => { if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true; };
		_sortSelectorMenu.Closing += (_, e) => { if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true; };

		// 主题化上下文菜单
		ThemeManager.StyleContextMenuStrip(_filterSelectorMenu);
		ThemeManager.StyleContextMenuStrip(_sortSelectorMenu);
		ThemeManager.StyleContextMenuStrip(_appSelection.ContextMenuStrip!);

		_dataStore.AppInfo.Modified += OnEntryModified;

		void AddShowEntriesMenuItem(string name, ShowEntriesMode mode)
		{
			var mi = CreateMenuItem(name, mode, Settings.Default.FiltersShowEntries == mode);
			mi.Click += OnFilter_ShowEntriesModeChanged;
			_filterSelectorMenu.Items.Add(mi);
		}
	}

	public AppInfoEntryType CurrentFilters => _currentFilters;
	public bool RefreshingAfterEdit { get => _refreshingGamesListAfterEdit; set => _refreshingGamesListAfterEdit = value; }
	public ImageAttributes BgLogoAttributes => _bgLogoAttributes;
	public Dictionary<AppInfoEntryType, Image> GetFilterImages() => _filterImages;

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

	// --- 公开方法 ---

	public IAppInfoEntry? GetSelectedEntry()
	{
		int idx = _appSelection.SelectedIndex;
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

	public string GetDisplayName(IAppInfoEntry entry)
	{
		if (_ctx.DisplayNames.TryGetValue(entry.ID, out var name)) return name;
		name = entry.ToString()!;
		_ctx.DisplayNames[entry.ID] = name;
		return name;
	}

	public void InvalidateDisplayName(IAppInfoEntry entry) => _ctx.DisplayNames.Remove(entry.ID);
	public void ClearDisplayNameCache() => _ctx.DisplayNames.Clear();

	public void RefreshGameList()
	{
		var selected = GetSelectedEntry();
		var showMode = Settings.Default.FiltersShowEntries;
		ClearDisplayNameCache();

		int totalCount = 0;
		_modifiedCount = 0;
		_ctx._baseEntries = new List<IAppInfoEntry>();
		foreach (var ent in _dataStore.AppInfo.Entries)
		{
			if (ent.Name == null) continue;
			totalCount++;
			if (ent.IsModified) _modifiedCount++;
			if ((ent.Type & _currentFilters) == 0) continue;
			if (showMode == ShowEntriesMode.OnlyModified && !ent.IsModified) continue;
			if (showMode == ShowEntriesMode.OnlyUnmodified && ent.IsModified) continue;
			_ctx._baseEntries.Add(ent);
		}
		_modifiedCountDirty = false;
		_ctx.TotalEntryCount = totalCount;
		_ctx.CurrentSearchTerm = "";
		ApplyIncrementalSearch();

		if (selected != null)
		{
			int newIdx = _ctx._entries.IndexOf(selected);
			if (newIdx >= 0) _appSelection.SelectedIndex = newIdx;
		}
		if (_appSelection.SelectedIndex < 0 && _appSelection.Items.Count > 0)
			_appSelection.SelectedIndex = 0;

		UpdateStatusBar();
	}

	public void ResortEntry(IAppInfoEntry entry)
	{
		var selected = GetSelectedEntry();
		int i = _ctx._entries.IndexOf(entry);
		if (i < 0) return;
		int index = i;
		while (i > 0 && StringTitleComparer.CompareStrings(entry.SortAs, _ctx.Entries[i - 1].SortAs) < 0)
		{
			_ctx._entries[i] = _ctx._entries[i - 1];
			_ctx._entries[i - 1] = entry;
			i--;
		}
		for (; i < _ctx._entries.Count - 1 && StringTitleComparer.CompareStrings(entry.SortAs, _ctx._entries[i + 1].SortAs) > 0; i++)
		{
			_ctx._entries[i] = _ctx._entries[i + 1];
			_ctx._entries[i + 1] = entry;
		}
		_appSelection.BeginUpdate();
		int lo = Math.Min(index, i);
		int hi = Math.Max(index, i);
		for (int j = lo; j <= hi; j++)
			_appSelection.Items[j] = j;
		_appSelection.EndUpdate();
		if (selected != null)
		{
			int newIdx = _ctx._entries.IndexOf(selected);
			if (newIdx >= 0) _appSelection.SelectedIndex = newIdx;
		}
	}

	public void SortEntries()
	{
		var selected = GetSelectedEntry();
		string sortField = Settings.Default.SortField;
		bool descending = Settings.Default.SortDescending;

		if (sortField == "AppId")
		{
			_ctx._entries.Sort((a, b) =>
			{
				int n = a.ID.CompareTo(b.ID);
				if (descending) n = -n;
				if (n == 0) n = string.Compare(a.NormalizedSortAs, b.NormalizedSortAs, StringComparison.CurrentCulture);
				return n;
			});
		}
		else
		{
			_ctx._entries.Sort((a, b) =>
			{
				int n = string.Compare(a.NormalizedSortAs, b.NormalizedSortAs, StringComparison.CurrentCulture);
				if (descending) n = -n;
				if (n == 0) n = a.ID - b.ID;
				return n;
			});
		}
		RebuildListBoxItems();
		if (selected != null)
		{
			int newIdx = _ctx._entries.IndexOf(selected);
			if (newIdx >= 0) _appSelection.SelectedIndex = newIdx;
		}
		if (_appSelection.SelectedIndex < 0 && _appSelection.Items.Count > 0)
			_appSelection.SelectedIndex = 0;
	}

	public void UpdateStatusBar()
	{
		_lblNumAppsShown.Text = string.Format(Strings.MainWindow_StatusShown, _ctx.Entries?.Count ?? 0, _ctx.TotalEntryCount);
		if (_modifiedCountDirty)
		{
			_modifiedCount = 0;
			if (_dataStore.AppInfo != null)
			{
				foreach (var e in _dataStore.AppInfo.Entries)
					if (e.IsModified) _modifiedCount++;
			}
			_modifiedCountDirty = false;
		}
		_lblModifiedCount.Text = string.Format(Strings.MainWindow_StatusModified, _modifiedCount);
	}

	public void ApplyLanguageToFilters()
	{
		foreach (object item in _filterSelectorMenu.Items)
		{
			if (item is ToolStripMenuItem mi)
			{
				if (mi.Tag is ShowEntriesMode mode)
				{
					mi.Text = mode switch
					{
						ShowEntriesMode.All => Strings.MainWindow_Filter_All,
						ShowEntriesMode.OnlyModified => Strings.MainWindow_Filter_OnlyModified,
						ShowEntriesMode.OnlyUnmodified => Strings.MainWindow_Filter_OnlyUnmodified,
						_ => mi.Text
					};
				}
				else if (mi.Tag is AppInfoEntryType entryType)
				{
					mi.Text = I18n.GetEnumDisplayName(entryType);
				}
			}
		}
	}

	public void SelectIndex(int idx)
	{
		if (idx >= 0 && idx < _appSelection.Items.Count)
			_appSelection.SelectedIndex = idx;
	}

	public void InvalidateItem(IAppInfoEntry entry)
	{
		int idx = _ctx._entries.IndexOf(entry);
		if (idx >= 0)
			_appSelection.Invalidate(_appSelection.GetItemRectangle(idx));
	}

	public void InvalidateAll() => _appSelection.Invalidate();

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
		ResortEntry(entry);
		int idx = _ctx._entries.IndexOf(entry);
		if (idx >= 0) SelectIndex(idx);
	}

	// --- 私有方法 ---

	private void RebuildListBoxItems()
	{
		_appSelection.BeginUpdate();
		_appSelection.Items.Clear();
		for (int i = 0; i < _ctx.Entries.Count; i++)
			_appSelection.Items.Add(i);
		_appSelection.EndUpdate();
	}

	private async void ApplyIncrementalSearch()
	{
		string newText = _appFilterText.Text ?? "";
		string trimmed = newText.Trim();

		if (string.IsNullOrEmpty(trimmed))
		{
			_ctx._entries = new List<IAppInfoEntry>(_ctx.BaseEntries);
			_ctx.CurrentSearchTerm = "";
			SortEntries();
			return;
		}

		string[] patternParts = trimmed.Split(' ', '\t', '　');
		bool isNumeric = int.TryParse(trimmed, out _);
		string currentSearchTerm = _ctx.CurrentSearchTerm;
		IReadOnlyList<IAppInfoEntry> baseEntries = _ctx._baseEntries;
		IReadOnlyList<IAppInfoEntry> currentEntries = _ctx.Entries;
		string sortField = Settings.Default.SortField;
		bool descending = Settings.Default.SortDescending;

		// Snapshot display names for background thread safety
		var displayNameSnapshot = new Dictionary<int, string>(_ctx.DisplayNames);
		// Pre-warm NormalizedSortAs for all entries in source (thread-safe per-entry cache)
		IReadOnlyList<IAppInfoEntry> sourceForWarm = baseEntries;
		foreach (var ent in sourceForWarm)
		{
			// Force cache population on UI thread
			if (!string.IsNullOrEmpty(ent.Name))
			{
				if (!displayNameSnapshot.ContainsKey(ent.ID))
					displayNameSnapshot[ent.ID] = ent.Name;
				_ = ent.NormalizedSortAs;
			}
		}
		string GetDisplayNameSafe(IAppInfoEntry e) =>
			displayNameSnapshot.TryGetValue(e.ID, out var n) ? n : (e.Name ?? "");

		var result = await Task.Run(() =>
		{
			IReadOnlyList<IAppInfoEntry> source;
			if (!string.IsNullOrEmpty(currentSearchTerm) && trimmed.StartsWith(currentSearchTerm, StringComparison.OrdinalIgnoreCase))
				source = currentEntries;
			else if (!string.IsNullOrEmpty(currentSearchTerm) && currentSearchTerm.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
				source = baseEntries;
			else
				source = baseEntries;

			var filtered = new List<IAppInfoEntry>(source.Count);
			if (isNumeric)
			{
				foreach (var ent in source)
					if (ent.ID.ToString().StartsWith(trimmed))
						filtered.Add(ent);
			}
			else
			{
				foreach (var ent in source)
					if (IsFuzzyMatch(GetDisplayNameSafe(ent), patternParts))
						filtered.Add(ent);
			}

			if (sortField == "AppId")
			{
				filtered.Sort((a, b) =>
				{
					int n = a.ID.CompareTo(b.ID);
					if (descending) n = -n;
					if (n == 0) n = string.Compare(a.NormalizedSortAs, b.NormalizedSortAs, StringComparison.CurrentCulture);
					return n;
				});
			}
			else
			{
				filtered.Sort((a, b) =>
				{
					int n = string.Compare(a.NormalizedSortAs, b.NormalizedSortAs, StringComparison.CurrentCulture);
					if (descending) n = -n;
					if (n == 0) n = a.ID - b.ID;
					return n;
				});
			}
			return filtered;
		});

		_ctx._entries = result;
		_ctx.CurrentSearchTerm = trimmed;
		RebuildListBoxItems();
		RestoreSelectedEntry();
	}

	private void RestoreSelectedEntry()
	{
		if (_appSelection.SelectedIndex >= 0 && _appSelection.Items.Count > 0)
			return;
		if (_appSelection.Items.Count > 0)
			_appSelection.SelectedIndex = 0;
	}

	private static bool IsFuzzyMatch(string testString, string[] patternParts)
	{
		foreach (string patt in patternParts)
		{
			if (!testString.Contains(patt, StringComparison.OrdinalIgnoreCase)) return false;
		}
		return true;
	}

	private string GetEntryToolTip(IAppInfoEntry entry)
	{
		if (entry.ID == _tooltipCachedId) return _tooltipCachedText;
		var sb = new StringBuilder();
		sb.AppendFormat("{0}: {1}", entry.ID, entry.Name);
		string oslist = entry.GetPropertyValue<string>(null!, AppInfoPaths.OsList);
		if (oslist != null) sb.AppendFormat(" [{0}]", oslist);
		_tooltipCachedId = entry.ID;
		_tooltipCachedText = sb.ToString();
		return _tooltipCachedText;
	}

	// --- 事件处理 ---

	private void OnAppSelection_SelectedIndexChanged(object? sender, EventArgs e)
	{
		if (!_refreshingGamesListAfterEdit)
			SelectedEntryChanged?.Invoke(this, GetSelectedEntry()!);
	}

	private void OnAppSelection_DrawItem(object? sender, DrawItemEventArgs e)
	{
		if (e.Index < 0 || e.Index >= _ctx.Entries.Count) return;

		IAppInfoEntry entry = _ctx.Entries[e.Index];
		string text = GetDisplayName(entry).Replace("&", "&&");

		bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
		bool focused = _appSelection.Focused;
		bool hover = e.Index == _appSelectionLastHoverI && !selected;

		Rectangle bounds = e.Bounds;
		const int padding = 4;
		const int iconGap = 4;

		// --- Background ---
		Brush backBrush;
		if (selected)
			backBrush = focused ? _brushListSelectedBackColor : _brushListSelectedUnfocusedBackColor;
		else if (hover)
			backBrush = _brushAccentLightAlt;
		else if (e.Index % 2 == 1)
			backBrush = _brushListAlternateBackColor;
		else
			backBrush = _brushListBackColor;

		if (selected)
		{
			// Rounded selection highlight — reuse cached path
			var rowRect = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
			int radius = Math.Max(2, (int)Math.Round(4.0 * _appSelection.DeviceDpi / 96.0));
			ThemeManager.UpdateRoundedRectangle(_cachedSelectionPath, rowRect, radius);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.FillPath(backBrush, _cachedSelectionPath);
			e.Graphics.SmoothingMode = SmoothingMode.Default;

			if (focused)
				ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds);
		}
		else
		{
			e.Graphics.FillRectangle(backBrush, bounds);
		}

		// --- Row separator ---
		e.Graphics.DrawLine(_penBorder, bounds.X, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);

		// --- Icons and Text ---
		Color foreColor = _appSelection.ForeColor;
		Font font = _defaultAppSelectionFont;
		int iconSize = bounds.Height - 8;
		int leftX = bounds.X + padding;

		// Left side: filter type icon
		if ((_currentFilters & (_currentFilters - 1)) != 0)
		{
			if (_filterImages.TryGetValue(entry.Type, out var icon))
				e.Graphics.DrawImage(icon, leftX, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);
			leftX += iconSize + iconGap;
		}

		// Right side: status icons (hidden, completed, modified — from rightmost inward)
		int rightX = bounds.Right - padding - iconSize;
		bool hidden = entry.IsHidden;
		bool completed = entry.IsCompleted;
		bool modified = entry.IsModified;

		if (hidden)
		{
			e.Graphics.DrawImage(Resources.icon_hidden, rightX, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);
			rightX -= iconSize + iconGap;
			foreColor = Color.FromArgb(255 - (255 - foreColor.R) / 2, 255 - (255 - foreColor.G) / 2, 255 - (255 - foreColor.B) / 2);
			font = _hiddenAppSelectionFont;
		}
		if (completed)
		{
			e.Graphics.DrawImage(Resources.icon_completed, rightX, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);
			rightX -= iconSize + iconGap;
		}
		if (modified)
		{
			e.Graphics.DrawImage(Resources.icon_modified, rightX, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);
			font = _boldAppSelectionFont;
		}

		// Text
		int textWidth = rightX - leftX;
		if (textWidth > 0)
		{
			var textBounds = new Rectangle(leftX, bounds.Y, textWidth, bounds.Height);
			Size textSize = TextRenderer.MeasureText(text, font);
			textBounds.Inflate(0, -((textBounds.Height - textSize.Height) / 2));
			TextRenderer.DrawText(e.Graphics, text, font, textBounds, foreColor, TextFormatFlags.EndEllipsis);
		}
	}

	private void OnAppSelection_KeyPress(object? sender, KeyPressEventArgs e)
	{
		e.Handled = true;
		_appSearchTerm.Append(e.KeyChar);
		_appSearchTermTimer.Stop();
		_appSearchTermTimer.Start();
		string text = _appSearchTerm.ToString();
		int start = _appSelection.SelectedIndex;
		if (text.Length == 1) start++;
		for (int i = 0; i < _ctx.Entries.Count; i++)
		{
			int idx = (start + i) % _ctx.Entries.Count;
			if (GetDisplayName(_ctx.Entries[idx]).StartsWith(text, StringComparison.CurrentCultureIgnoreCase))
			{
				_appSelection.SelectedIndex = idx;
				break;
			}
		}
	}

	private void OnAppSelection_MouseMove(object? sender, MouseEventArgs e)
	{
		Point p = _appSelection.PointToClient(Control.MousePosition);
		int idx = _appSelection.IndexFromPoint(p);
		if (idx >= 0 && idx < _appSelection.Items.Count)
		{
			if (_appSelectionLastHoverI != idx)
			{
				// Invalidate old hovered item
				if (_appSelectionLastHoverI >= 0)
					_appSelection.Invalidate(_appSelection.GetItemRectangle(_appSelectionLastHoverI));

				_appSelectionLastHoverI = idx;

				// Invalidate new hovered item
				_appSelection.Invalidate(_appSelection.GetItemRectangle(idx));

				_toolTips.Active = false;
				_toolTips.SetToolTip(_appSelection, GetEntryToolTip(_ctx.Entries[idx]));
				_toolTips.Active = true;
			}
		}
		else
		{
			if (_appSelectionLastHoverI >= 0)
				_appSelection.Invalidate(_appSelection.GetItemRectangle(_appSelectionLastHoverI));
			_toolTips.SetToolTip(_appSelection, (string?)null);
			_appSelectionLastHoverI = -1;
		}
	}

	private void OnAppSelection_MouseDown(object? sender, MouseEventArgs e)
	{
		if (e.Button != MouseButtons.Right) return;
		int idx = _appSelection.IndexFromPoint(e.Location);
		if (idx >= 0 && idx < _appSelection.Items.Count)
		{
			if (_appSelection.SelectedIndices.Contains(idx))
				return; // Right-click on selected item → keep multi-selection
			_appSelection.SelectedIndex = idx; // Click on unselected → select only this
		}
	}

	private void OnAppFilterText_TextChanged(object? sender, EventArgs e)
	{
		_filterDebounceTimer.Stop();
		_filterDebounceTimer.Start();
	}

	private void OnFilter_CheckedChanged(object? sender, EventArgs e)
	{
		var item = (ToolStripMenuItem)sender!;
		var type = (AppInfoEntryType)item.Tag!;
		if (item.Checked) _currentFilters |= type;
		else _currentFilters &= ~type;
		Settings.Default.Filters = _currentFilters;
		RefreshGameList();
	}

	private void OnFilter_ShowEntriesModeChanged(object? sender, EventArgs e)
	{
		var mode = (ShowEntriesMode)((ToolStripMenuItem)sender!).Tag!;
		foreach (object obj in _filterSelectorMenu.Items)
		{
			if (obj is ToolStripMenuItem mi && mi.Tag is ShowEntriesMode sm)
				mi.Checked = sm == mode;
		}
		Settings.Default.FiltersShowEntries = mode;
		RefreshGameList();
	}

	private void OnSortField_Click(object? sender, EventArgs e)
	{
		if (sender is not ToolStripMenuItem item || item.Tag is not string field) return;
		Settings.Default.SortField = field;
		Settings.Default.Save();
		foreach (var obj in _sortSelectorMenu.Items)
		{
			if (obj is ToolStripMenuItem mi && mi.Tag is string && mi != item) mi.Checked = false;
		}
		item.Checked = true;
		SortEntries();
	}

	private void OnSortDirection_Click(object? sender, EventArgs e)
	{
		if (sender is not ToolStripMenuItem item || item.Tag is not bool descending) return;
		Settings.Default.SortDescending = descending;
		Settings.Default.Save();
		foreach (var obj in _sortSelectorMenu.Items)
		{
			if (obj is ToolStripMenuItem mi && mi.Tag is bool && mi != item) mi.Checked = false;
		}
		item.Checked = true;
		SortEntries();
	}

	private void OnEntryModified(object? sender, EventArgs e)
	{
		ClearDisplayNameCache();
		_modifiedCountDirty = true;
		_statusBarDebounceTimer.Stop();
		_statusBarDebounceTimer.Start();
	}

	public void Dispose()
	{
		_appSearchTermTimer?.Dispose();
		_filterDebounceTimer?.Dispose();
		_statusBarDebounceTimer?.Dispose();
		_boldAppSelectionFont?.Dispose();
		_hiddenAppSelectionFont?.Dispose();
		_bgLogoAttributes?.Dispose();
		_brushListBackColor?.Dispose();
		_brushListAlternateBackColor?.Dispose();
		_brushListSelectedBackColor?.Dispose();
		_brushListSelectedUnfocusedBackColor?.Dispose();
		_brushAccentLightAlt?.Dispose();
		_penBorder?.Dispose();
		_cachedSelectionPath?.Dispose();
		foreach (var img in _filterImages.Values)
			img.Dispose();
		_dataStore.AppInfo.Modified -= OnEntryModified;
		if (_onLanguageChanged != null)
			I18n.LanguageChanged -= _onLanguageChanged;
	}
}
