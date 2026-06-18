using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Common;
using SteamEdit.Data;
using SteamEdit.Properties;

namespace SteamEdit;

internal class GameListManager : IDisposable
{
	private readonly GameDataContext _ctx;
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

	private readonly Timer _appSearchTermTimer = new();
	private readonly Timer _filterDebounceTimer = new();
	private readonly Timer _statusBarDebounceTimer = new();
	private readonly StringBuilder _appSearchTerm = new();
	private int _appSelectionLastHoverI = -1;
	private bool _refreshingGamesListAfterEdit;

	public event EventHandler<IAppInfoEntry> SelectedEntryChanged;

	public GameListManager(
		GameDataContext ctx,
		ListBox appSelection,
		ContextMenuStrip filterSelectorMenu,
		ContextMenuStrip sortSelectorMenu,
		ToolTip toolTips,
		ClearableTextBox appFilterText,
		ToolStripStatusLabel lblNumAppsShown,
		ToolStripStatusLabel lblModifiedCount)
	{
		_ctx = ctx;
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

		// 构建筛选器菜单
		foreach (var kv in _filterImages)
		{
			var item = new ToolStripMenuItem(I18n.GetEnumDisplayName(kv.Key), kv.Value)
			{
				DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
				CheckOnClick = true,
				Tag = kv.Key,
				Checked = _currentFilters.HasFlag(kv.Key)
			};
			item.CheckedChanged += OnFilter_CheckedChanged;
			_filterSelectorMenu.Items.Add(item);
		}
		_filterSelectorMenu.Items.Add(new ToolStripSeparator());
		AddShowEntriesMenuItem(Strings.MainWindow_Filter_All, ShowEntriesMode.All);
		AddShowEntriesMenuItem(Strings.MainWindow_Filter_OnlyModified, ShowEntriesMode.OnlyModified);
		AddShowEntriesMenuItem(Strings.MainWindow_Filter_OnlyUnmodified, ShowEntriesMode.OnlyUnmodified);

		// 构建排序菜单
		var sortByName = new ToolStripMenuItem(Strings.Sort_ByName) { Tag = "Name", CheckOnClick = true };
		var sortById = new ToolStripMenuItem(Strings.Sort_ById) { Tag = "AppId", CheckOnClick = true };
		sortByName.Click += OnSortField_Click;
		sortById.Click += OnSortField_Click;
		_sortSelectorMenu.Items.Add(sortByName);
		_sortSelectorMenu.Items.Add(sortById);
		_sortSelectorMenu.Items.Add(new ToolStripSeparator());
		var sortAsc = new ToolStripMenuItem(Strings.Sort_Ascending) { Tag = false, CheckOnClick = true };
		var sortDesc = new ToolStripMenuItem(Strings.Sort_Descending) { Tag = true, CheckOnClick = true };
		sortAsc.Click += OnSortDirection_Click;
		sortDesc.Click += OnSortDirection_Click;
		_sortSelectorMenu.Items.Add(sortAsc);
		_sortSelectorMenu.Items.Add(sortDesc);

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
		_appSelection.SelectedIndexChanged += OnAppSelection_SelectedIndexChanged;
		_appSelection.SizeChanged += (_, _) => _appSelection.Invalidate();
		_appSelection.KeyPress += OnAppSelection_KeyPress;
		_appSelection.MouseMove += OnAppSelection_MouseMove;
		_appSelection.MouseLeave += (_, _) => _appSelectionLastHoverI = -1;
		_appFilterText.TextChanged += OnAppFilterText_TextChanged;
		_filterSelectorMenu.Closing += (_, e) => { if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true; };
		_sortSelectorMenu.Closing += (_, e) => { if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true; };

		SteamData.AppInfoData.Modified += OnEntryModified;

		void AddShowEntriesMenuItem(string name, ShowEntriesMode mode)
		{
			var mi = new ToolStripMenuItem(name)
			{
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				CheckOnClick = true,
				Tag = mode,
				Checked = Settings.Default.FiltersShowEntries == mode
			};
			mi.Click += OnFilter_ShowEntriesModeChanged;
			_filterSelectorMenu.Items.Add(mi);
		}
	}

	public AppInfoEntryType CurrentFilters => _currentFilters;
	public bool RefreshingAfterEdit { get => _refreshingGamesListAfterEdit; set => _refreshingGamesListAfterEdit = value; }
	public ImageAttributes BgLogoAttributes => _bgLogoAttributes;
	public Dictionary<AppInfoEntryType, Image> GetFilterImages() => _filterImages;

	// --- 公开方法 ---

	public IAppInfoEntry GetSelectedEntry()
	{
		int idx = _appSelection.SelectedIndex;
		return idx >= 0 && idx < _ctx.Entries.Count ? _ctx.Entries[idx] : null;
	}

	public string GetDisplayName(IAppInfoEntry entry)
	{
		if (_ctx.DisplayNames.TryGetValue(entry.ID, out var name)) return name;
		name = entry.ToString();
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
		_ctx.BaseEntries = new List<IAppInfoEntry>();
		foreach (var ent in SteamData.AppInfoData.Entries)
		{
			if (ent.Name == null) continue;
			totalCount++;
			if ((ent.Type & _currentFilters) == 0) continue;
			if (showMode == ShowEntriesMode.OnlyModified && !ent.IsModified) continue;
			if (showMode == ShowEntriesMode.OnlyUnmodified && ent.IsModified) continue;
			_ctx.BaseEntries.Add(ent);
		}
		_ctx.TotalEntryCount = totalCount;
		_ctx.CurrentSearchTerm = "";
		ApplyIncrementalSearch();

		if (selected != null)
		{
			int newIdx = _ctx.Entries.IndexOf(selected);
			if (newIdx >= 0) _appSelection.SelectedIndex = newIdx;
		}
		if (_appSelection.SelectedIndex < 0 && _appSelection.Items.Count > 0)
			_appSelection.SelectedIndex = 0;

		UpdateStatusBar();
	}

	public void ResortEntry(IAppInfoEntry entry)
	{
		var selected = GetSelectedEntry();
		int i = _ctx.Entries.IndexOf(entry);
		if (i < 0) return;
		int index = i;
		while (i > 0 && StringTitleComparer.CompareStrings(entry.SortAs, _ctx.Entries[i - 1].SortAs) < 0)
		{
			_ctx.Entries[i] = _ctx.Entries[i - 1];
			_ctx.Entries[i - 1] = entry;
			i--;
		}
		for (; i < _ctx.Entries.Count - 1 && StringTitleComparer.CompareStrings(entry.SortAs, _ctx.Entries[i + 1].SortAs) > 0; i++)
		{
			_ctx.Entries[i] = _ctx.Entries[i + 1];
			_ctx.Entries[i + 1] = entry;
		}
		_appSelection.BeginUpdate();
		int lo = Math.Min(index, i);
		int hi = Math.Max(index, i);
		for (int j = lo; j <= hi; j++)
			_appSelection.Items[j] = j;
		_appSelection.EndUpdate();
		if (selected != null)
		{
			int newIdx = _ctx.Entries.IndexOf(selected);
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
			_ctx.Entries.Sort((a, b) =>
			{
				int n = a.ID.CompareTo(b.ID);
				if (descending) n = -n;
				if (n == 0) n = StringTitleComparer.CompareStrings(a.SortAs, b.SortAs);
				return n;
			});
		}
		else
		{
			var keys = new Dictionary<IAppInfoEntry, string>(_ctx.Entries.Count);
			foreach (var e in _ctx.Entries) keys[e] = StringTitleComparer.Normalize(e.SortAs);
			_ctx.Entries.Sort((a, b) =>
			{
				int n = StringComparer.CurrentCulture.Compare(keys[a], keys[b]);
				if (descending) n = -n;
				if (n == 0) n = a.ID - b.ID;
				return n;
			});
		}
		RebuildListBoxItems();
		if (selected != null)
		{
			int newIdx = _ctx.Entries.IndexOf(selected);
			if (newIdx >= 0) _appSelection.SelectedIndex = newIdx;
		}
		if (_appSelection.SelectedIndex < 0 && _appSelection.Items.Count > 0)
			_appSelection.SelectedIndex = 0;
	}

	public void UpdateStatusBar()
	{
		_lblNumAppsShown.Text = string.Format(Strings.MainWindow_StatusShown, _ctx.Entries?.Count ?? 0, _ctx.TotalEntryCount);
		int modifiedCount = SteamData.AppInfoData?.Entries.Count(e => e.IsModified) ?? 0;
		_lblModifiedCount.Text = string.Format(Strings.MainWindow_StatusModified, modifiedCount);
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
		int idx = _ctx.Entries.IndexOf(entry);
		if (idx >= 0)
			_appSelection.Invalidate(_appSelection.GetItemRectangle(idx));
	}

	public void InvalidateAll() => _appSelection.Invalidate();

	// --- 私有方法 ---

	private void RebuildListBoxItems()
	{
		_appSelection.BeginUpdate();
		_appSelection.Items.Clear();
		for (int i = 0; i < _ctx.Entries.Count; i++)
			_appSelection.Items.Add(i);
		_appSelection.EndUpdate();
	}

	private void ApplyIncrementalSearch()
	{
		string newText = _appFilterText.Text ?? "";
		string trimmed = newText.Trim();

		if (string.IsNullOrEmpty(trimmed))
		{
			_ctx.Entries = new List<IAppInfoEntry>(_ctx.BaseEntries);
			_ctx.CurrentSearchTerm = "";
			SortEntries();
			return;
		}

		string[] patternParts = trimmed.ToLower().Split(' ', '\t', '　');
		List<IAppInfoEntry> source;
		if (!string.IsNullOrEmpty(_ctx.CurrentSearchTerm) && trimmed.StartsWith(_ctx.CurrentSearchTerm, StringComparison.OrdinalIgnoreCase))
			source = _ctx.Entries;
		else if (!string.IsNullOrEmpty(_ctx.CurrentSearchTerm) && _ctx.CurrentSearchTerm.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
			source = _ctx.BaseEntries;
		else
			source = _ctx.BaseEntries;

		_ctx.Entries = new List<IAppInfoEntry>();
		foreach (var ent in source)
		{
			if (IsFuzzyMatch(GetDisplayName(ent), patternParts))
				_ctx.Entries.Add(ent);
		}
		_ctx.CurrentSearchTerm = trimmed;
		SortEntries();
	}

	private static bool IsFuzzyMatch(string testString, string[] patternParts)
	{
		string lower = testString.ToLower();
		foreach (string patt in patternParts)
		{
			if (!lower.Contains(patt)) return false;
		}
		return true;
	}

	private string GetEntryToolTip(IAppInfoEntry entry)
	{
		var sb = new StringBuilder();
		sb.AppendFormat("{0}: {1}", entry.ID, entry.Name);
		string oslist = entry.GetPropertyValue<string>(null, "appinfo", "common", "oslist");
		if (oslist != null) sb.AppendFormat(" [{0}]", oslist);
		return sb.ToString();
	}

	// --- 事件处理 ---

	private void OnAppSelection_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (!_refreshingGamesListAfterEdit)
			SelectedEntryChanged?.Invoke(this, GetSelectedEntry());
	}

	private void OnAppSelection_DrawItem(object sender, DrawItemEventArgs e)
	{
		e.DrawBackground();
		if (e.Index < 0 || e.Index >= _ctx.Entries.Count) return;

		IAppInfoEntry entry = _ctx.Entries[e.Index];
		string text = GetDisplayName(entry).Replace("&", "&&");
		Color foreColor = _appSelection.ForeColor;
		Font font = _defaultAppSelectionFont;
		Rectangle bounds = e.Bounds;
		int iconSize = e.Bounds.Height - 6;

		if ((_currentFilters & (_currentFilters - 1)) != 0)
		{
			e.Graphics.DrawImage(_filterImages[entry.Type], e.Bounds.X + 3, e.Bounds.Y + 3, iconSize, iconSize);
			bounds.X += e.Bounds.Height;
			bounds.Width -= e.Bounds.Height;
		}
		int totalWidth = bounds.Width;
		if (entry.IsModified)
		{
			e.Graphics.DrawImage(Resources.icon_modified, bounds.X + totalWidth - (iconSize + 6), e.Bounds.Y + 3, iconSize, iconSize);
			bounds.Width = totalWidth - (iconSize + 6);
			font = _boldAppSelectionFont;
		}
		if (entry.IsCompleted)
		{
			e.Graphics.DrawImage(Resources.icon_completed, bounds.X + totalWidth - (iconSize + 6) * 2, e.Bounds.Y + 3, iconSize, iconSize);
			bounds.Width = totalWidth - (iconSize + 6) * 2;
		}
		if (entry.IsHidden)
		{
			e.Graphics.DrawImage(Resources.icon_hidden, bounds.X + totalWidth - (iconSize + 6) * 3, e.Bounds.Y + 3, iconSize, iconSize);
			bounds.Width = totalWidth - (iconSize + 6) * 3;
			foreColor = Color.FromArgb(255 - (255 - foreColor.R) / 2, 255 - (255 - foreColor.G) / 2, 255 - (255 - foreColor.B) / 2);
			font = _hiddenAppSelectionFont;
		}
		Size textSize = TextRenderer.MeasureText(text, font);
		bounds.Inflate(0, -((bounds.Height - textSize.Height) / 2));
		TextRenderer.DrawText(e.Graphics, text, font, bounds, foreColor, TextFormatFlags.EndEllipsis);
	}

	private void OnAppSelection_KeyPress(object sender, KeyPressEventArgs e)
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

	private void OnAppSelection_MouseMove(object sender, MouseEventArgs e)
	{
		Point p = _appSelection.PointToClient(Control.MousePosition);
		int idx = _appSelection.IndexFromPoint(p);
		if (idx >= 0 && idx < _appSelection.Items.Count)
		{
			if (_appSelectionLastHoverI != idx)
			{
				_appSelectionLastHoverI = idx;
				_toolTips.Active = false;
				_toolTips.SetToolTip(_appSelection, GetEntryToolTip(_ctx.Entries[idx]));
				_toolTips.Active = true;
			}
		}
		else
		{
			_toolTips.SetToolTip(_appSelection, null);
			_appSelectionLastHoverI = -1;
		}
	}

	private void OnAppFilterText_TextChanged(object sender, EventArgs e)
	{
		_filterDebounceTimer.Stop();
		_filterDebounceTimer.Start();
	}

	private void OnFilter_CheckedChanged(object sender, EventArgs e)
	{
		var item = (ToolStripMenuItem)sender;
		var type = (AppInfoEntryType)item.Tag;
		if (item.Checked) _currentFilters |= type;
		else _currentFilters &= ~type;
		Settings.Default.Filters = _currentFilters;
		Settings.Default.Save();
		RefreshGameList();
	}

	private void OnFilter_ShowEntriesModeChanged(object sender, EventArgs e)
	{
		var mode = (ShowEntriesMode)((ToolStripMenuItem)sender).Tag;
		foreach (object obj in _filterSelectorMenu.Items)
		{
			if (obj is ToolStripMenuItem mi && mi.Tag is ShowEntriesMode sm)
				mi.Checked = sm == mode;
		}
		Settings.Default.FiltersShowEntries = mode;
		Settings.Default.Save();
		RefreshGameList();
	}

	private void OnSortField_Click(object sender, EventArgs e)
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

	private void OnSortDirection_Click(object sender, EventArgs e)
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

	private void OnEntryModified(object sender, EventArgs e)
	{
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
		SteamData.AppInfoData.Modified -= OnEntryModified;
	}
}
