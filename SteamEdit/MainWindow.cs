#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Common;
using SteamEdit.Data;
using SteamEdit.Properties;

namespace SteamEdit;

internal class MainWindow : LocalizableForm
{
	private readonly ISteamDataService _steamData;
	private string _originalTitle = null!;

	private GameDataContext _ctx = null!;
	private GameListManager _listManager = null!;
	private EditController _editController = null!;

	private IContainer components = null!;

	private ClearableTextBox textGameName = null!;
	private CheckBox checkHidden = null!;
	private CheckBox checkCompleted = null!;
	private ClearableTextBox textSortAs = null!;
	private Button btnEditLaunch = null!;
	private Button btnRevert = null!;
	private ToolTip toolTips = null!;
	private ComboBox listControllerSupport = null!;
	private Button btnSettings = null!;
	private Button btnHelp = null!;
	private Button btnShortcuts = null!;
	private Button btnTranslate = null!;
	private Button btnSearch = null!;
	private Button btnBatchTranslate = null!;
	private Button btnBatchReplace = null!;
	private ComboBox listVR = null!;
	private ContextMenuStrip filterSelectorMenu = null!;
	private Button filterSelector = null!;
	private ContextMenuStrip sortSelectorMenu = null!;
	private Button sortSelector = null!;
	private SplitContainer splitContainer1 = null!;
	private ListBox appSelection = null!;
	private ClearableTextBox appFilterText = null!;
	private Panel panel1 = null!;
	private FlowLayoutPanel flowLayoutPanel3 = null!;
	private ComboBox listEntryType = null!;
	private ToolStripStatusLabel lblNumAppsShown = null!;
	private ToolStripStatusLabel lblModifiedCount = null!;
	private ToolStripStatusLabel lblAppId = null!;
	private Label lblSortAs = null!;
	private Label lblName = null!;
	private Label lblControllerSupport = null!;
	private Label lblVRCategory = null!;
	private Button btnSave = null!;
	private Button btnSaveApply = null!;
	private Button btnSaveApplyRestart = null!;

	public MainWindow(ISteamDataService steamData)
	{
		_steamData = steamData;
		InitializeComponent();

		// 构建 EntryType 下拉框（依赖 filterImages，由 GameListManager 构建）
		listEntryType.Format += (s, e) =>
		{
			if (e.ListItem is AppInfoEntryType entryType)
				e.Value = I18n.GetEnumDisplayName(entryType);
		};

		base.Icon = Resources.logo;
		_originalTitle = Text;

		// 创建共享数据上下文
		_ctx = new GameDataContext();

		// 创建游戏列表管理器
		_listManager = new GameListManager(
			_ctx, _steamData.DataStore, appSelection, filterSelectorMenu, sortSelectorMenu,
			toolTips, appFilterText, lblNumAppsShown, lblModifiedCount);

		// 填充 EntryType 下拉框（依赖 _listManager 的 filterImages）
		listEntryType.BeginUpdate();
		foreach (var kv in _listManager.GetFilterImages())
		{
			if (kv.Key != AppInfoEntryType.Unknown)
				listEntryType.Items.Add(kv.Key);
		}
		listEntryType.EndUpdate();

		_listManager.SelectedEntryChanged += (_, entry) =>
		{
			_editController.LoadEntry(entry);
			btnEditLaunch.Enabled = entry != null;
			btnRevert.Enabled = entry != null;
			btnTranslate.Enabled = entry != null && !string.IsNullOrWhiteSpace(Settings.Default.AiApiKey);
			btnBatchTranslate.Enabled = !string.IsNullOrWhiteSpace(Settings.Default.AiApiKey);
			lblAppId.Text = entry != null ? $"App ID: {entry.ID}" : "App ID: —";
		};

		_listManager.ContextMenuCopyName += (_, _) =>
		{
			var entry = _listManager.GetSelectedEntry();
			if (entry != null) try { Clipboard.SetText(entry.Name); } catch (System.Runtime.InteropServices.ExternalException) { }
		};
		_listManager.ContextMenuCopyAppId += (_, _) =>
		{
			var entry = _listManager.GetSelectedEntry();
			if (entry != null) try { Clipboard.SetText(entry.ID.ToString()); } catch (System.Runtime.InteropServices.ExternalException) { }
		};
		_listManager.ContextMenuOpenInSteam += (_, _) =>
		{
			var entry = _listManager.GetSelectedEntry();
			if (entry != null)
				try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = $"steam://nav/games/details/{entry.ID}", UseShellExecute = true })?.Dispose(); }
				catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { MessageBox.Show(this, ex.Message, Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Error); }
		};
		_listManager.ContextMenuOpenInSteamDB += (_, _) =>
		{
			var entry = _listManager.GetSelectedEntry();
			if (entry != null)
				try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = $"https://steamdb.info/app/{entry.ID}/", UseShellExecute = true })?.Dispose(); }
				catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { MessageBox.Show(this, ex.Message, Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Error); }
		};

		// 创建编辑控制器
		_editController = new EditController(
			_ctx, _steamData.DataStore, _listManager, textGameName, textSortAs,
			checkHidden, checkCompleted, listControllerSupport,
			listVR, listEntryType);
		// 订阅 SteamData 事件
		_steamData.OnNeedsSave += SteamData_OnNeedsSaveOrApply;
		_steamData.OnNeedsApply += SteamData_OnNeedsSaveOrApply;
	}

	private void SteamData_OnNeedsSaveOrApply(object? sender, EventArgs e)
	{
		Text = _originalTitle;
		if (_steamData.NeedsSave)
			Text += Strings.MainWindow_UnsavedSuffix;
		if (_steamData.NeedsApply)
			Text += Strings.MainWindow_UnappliedSuffix;
	}

	private bool IsOnScreen()
	{
		Rectangle rect = new Rectangle(base.Left, base.Top, base.Width, base.Height);
		Screen[] allScreens = Screen.AllScreens;
		for (int i = 0; i < allScreens.Length; i++)
		{
			if (allScreens[i].WorkingArea.IntersectsWith(rect))
				return true;
		}
		return false;
	}

	private void MainWindow_Load(object? sender, EventArgs e)
	{
		Point location = base.Location;
		Point location2 = base.Location;
		if (Settings.Default.WindowLocationX != int.MaxValue)
			location2.X = Settings.Default.WindowLocationX;
		if (Settings.Default.WindowLocationY != int.MaxValue)
			location2.Y = Settings.Default.WindowLocationY;
		base.Location = location2;
		if (!IsOnScreen())
			base.Location = location;
		Size size = base.Size;
		Size size2 = base.Size;
		if (Settings.Default.WindowSizeX != int.MaxValue)
			size2.Width = Settings.Default.WindowSizeX;
		if (Settings.Default.WindowSizeY != int.MaxValue)
			size2.Height = Settings.Default.WindowSizeY;
		base.Size = size2;
		if (!IsOnScreen())
			base.Size = size;
		if (Settings.Default.MainWindowSplitter != int.MaxValue)
			splitContainer1.SplitterDistance = Settings.Default.MainWindowSplitter;
		SteamData_OnNeedsSaveOrApply(null, EventArgs.Empty);
		_listManager.RefreshGameList();
		appSelection.Select();
	}

	private void UpdateStatusBar()
	{
		_listManager.UpdateStatusBar();
	}

	protected override void ApplyLanguage()
	{
		_originalTitle = Strings.MainWindow_Title;
		SteamData_OnNeedsSaveOrApply(null, EventArgs.Empty);

		checkHidden.Text = Strings.MainWindow_CheckHidden;
		checkCompleted.Text = Strings.MainWindow_CheckCompleted;

		// 委托给 EditController 更新控制器/VR/EntryType 下拉框
		_editController.ApplyLanguage();

		// 委托给 GameListManager 更新筛选器菜单
		_listManager.ApplyLanguageToFilters();

		// 更新标签和按钮文本
		lblName.Text = Strings.MainWindow_LabelName;
		lblSortAs.Text = Strings.MainWindow_LabelSortAs;
		lblControllerSupport.Text = Strings.MainWindow_LabelControllerSupport;
		lblVRCategory.Text = Strings.MainWindow_LabelVRCategory;
		btnSave.Text = Strings.MainWindow_BtnSave;
		btnSaveApply.Text = Strings.MainWindow_BtnSaveApply;
		btnSaveApplyRestart.Text = Strings.MainWindow_BtnSaveApplyRestart;
		btnTranslate.Text = Strings.MainWindow_BtnTranslate;
		btnSearch.Text = Strings.MainWindow_BtnSearch;
		btnRevert.Text = Strings.MainWindow_BtnRevert;
		btnEditLaunch.Text = Strings.MainWindow_BtnEditLaunch;
		toolTips.SetToolTip(btnTranslate, Strings.Tooltip_Translate);
		btnBatchTranslate.Text = Strings.MainWindow_BatchTranslate;
		toolTips.SetToolTip(btnBatchTranslate, Strings.Tooltip_BatchTranslate);
		btnBatchReplace.Text = Strings.MainWindow_BatchReplace;
		toolTips.SetToolTip(btnBatchReplace, Strings.Tooltip_BatchReplace);
		toolTips.SetToolTip(btnSearch, Strings.Tooltip_Search);
		toolTips.SetToolTip(btnSave, Strings.Tooltip_Save);
		toolTips.SetToolTip(btnSaveApply, Strings.Tooltip_SaveApply);
		toolTips.SetToolTip(btnSaveApplyRestart, Strings.Tooltip_SaveApplyRestart);
		toolTips.SetToolTip(btnSettings, Strings.Tooltip_Settings);
		toolTips.SetToolTip(btnHelp, Strings.Tooltip_About);
		toolTips.SetToolTip(btnShortcuts, Strings.Tooltip_Shortcuts);
		toolTips.SetToolTip(sortSelector, Strings.Sort_Title);

		UpdateStatusBar();
		_listManager.ClearDisplayNameCache();
		_editController.LoadEntry(_listManager.GetSelectedEntry());
	}

	private void OnMainWindow_FormClosing(object? sender, FormClosingEventArgs e)
	{
		_steamData.OnNeedsSave -= SteamData_OnNeedsSaveOrApply;
		_steamData.OnNeedsApply -= SteamData_OnNeedsSaveOrApply;
		try { Settings.Default.Save(); } catch (Exception ex) { Log.WriteLine("Settings save failed: {0}", ex.Message); MessageBox.Show(this, string.Format(Strings.Msg_SettingsSaveFailed, ex.Message), Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Error); }
		if (_steamData.NeedsSave)
		{
			switch (MessageBox.Show(Strings.Msg_UnsavedChanges, Strings.Msg_SaveFirst, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation))
			{
			case DialogResult.Yes:
				_steamData.Save();
				break;
			case DialogResult.Cancel:
				e.Cancel = true;
				break;
			}
		}
	}

	private void OnMainWindow_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Control && e.KeyCode == Keys.S)
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			try { _steamData.Save(); }
			catch (Exception ex) { MessageBox.Show(string.Format(Strings.Msg_SaveFailed, ex.ToString())); }
			return;
		}
		if (e.Control && e.KeyCode == Keys.Z)
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			_editController.PerformUndo();
			return;
		}
		if (e.Control && e.KeyCode == Keys.C && !appFilterText.Focused)
		{
			var entry = _listManager.GetSelectedEntry();
			if (entry != null) { try { Clipboard.SetText(entry.Name); } catch (System.Runtime.InteropServices.ExternalException) { } }
			e.Handled = true;
			e.SuppressKeyPress = true;
			return;
		}
		if (e.Control && e.KeyCode == Keys.F)
		{
			if (appFilterText.Focused) return;
			e.Handled = true;
			e.SuppressKeyPress = true;
			appFilterText.Focus();
			appFilterText.SelectAll();
			return;
		}
		if (e.Control && e.KeyCode == Keys.Enter)
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			OnBtnSaveApply_Click(sender, EventArgs.Empty);
			return;
		}
		if (e.Control && e.KeyCode == Keys.T)
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			OnBtnTranslate_Click(sender, EventArgs.Empty);
			return;
		}
		if (e.Control && e.KeyCode == Keys.B)
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			OnBtnBatchTranslate_Click(sender, EventArgs.Empty);
			return;
		}
		if (e.Control && e.KeyCode == Keys.R)
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			OnBtnRevert_Click(sender, EventArgs.Empty);
			return;
		}
		switch (e.KeyCode)
		{
		case Keys.Next:
			appSelection.SelectedIndex = Math.Min(appSelection.SelectedIndex + 1, appSelection.Items.Count - 1);
			break;
		case Keys.Prior:
			appSelection.SelectedIndex = Math.Max(appSelection.SelectedIndex - 1, 0);
			break;
		}
	}

	// --- 对话框打开 ---

	private void OnBtnEditLaunch_Click(object? sender, EventArgs e)
	{
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		using LaunchEditor launchEditor = new LaunchEditor(_steamData.DataStore, entry.Launches.Items, entry.InstallPath, entry.Name);
		if (launchEditor.ShowDialog(this) == DialogResult.OK)
			entry.Launches = new LaunchTable(launchEditor.LaunchItems);
	}

	private void OnBtnSettings_Click(object? sender, EventArgs e)
	{
		using (var dlg = new SettingsDlg()) dlg.ShowDialog(this);
		_steamData.UseCompleted = Settings.Default.EnableCompleted;
		_steamData.UseHidden = Settings.Default.EnableHidden;
	}

	private void OnBtnRevert_Click(object? sender, EventArgs e)
	{
		_editController.RevertEntry(_listManager.GetSelectedEntry());
	}

	private void OnBtnSearch_Click(object? sender, EventArgs e)
	{
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		string gameName = entry.Name;
		if (string.IsNullOrWhiteSpace(gameName)) return;
		string lang = Settings.Default.AiTargetLanguage switch
		{
			"zh-CN" => "中文名",
			"zh-TW" => "繁體中文名",
			"en" => "English name",
			_ => "translated name"
		};
		string query = Uri.EscapeDataString($"{gameName} {lang}");
		string baseUrl = Settings.Default.SearchEngine switch
		{
			"Bing" => "https://www.bing.com/search?q=",
			"Baidu" => "https://www.baidu.com/s?wd=",
			"DuckDuckGo" => "https://duckduckgo.com/?q=",
			_ => "https://www.google.com/search?q="
		};
		string url = baseUrl + query;
		Log.WriteLine("Search: Opening {0}", url);
		try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose(); }
		catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { MessageBox.Show(this, ex.Message, Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Error); }
	}

	private async void OnBtnTranslate_Click(object? sender, EventArgs e)
	{
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		string originalName = entry.Name;
		if (string.IsNullOrWhiteSpace(originalName)) return;

		string targetLang = Settings.Default.AiTargetLanguage;
		btnTranslate.Enabled = false;
		btnTranslate.Text = Strings.Msg_Translating;

		try
		{
			Log.WriteLine("AI Translate: Starting for \"{0}\"", originalName);
			string? context = AiTranslator.BuildContext(
				entry.GetPropertyValue<string>(null!, AppInfoPaths.Developers),
				entry.GetPropertyValue<string>(null!, AppInfoPaths.Publishers),
				entry.GetPropertyValue<string>(null!, AppInfoPaths.Type));
			string translated = await AiTranslator.TranslateWithContextAsync(
				originalName, targetLang, context,
				Settings.Default.AiCustomSystemPrompt,
				Settings.Default.AiCustomUserMessage);
			Log.WriteLine("AI Translate: Got result \"{0}\"", translated);

			if (!string.IsNullOrWhiteSpace(translated) && IsHandleCreated && !IsDisposed)
			{
				BeginInvoke(() =>
				{
					var currentEntry = _listManager.GetSelectedEntry();
					if (currentEntry == null) return;
					string previousName = currentEntry.Name;
					_editController.ApplyTranslation(currentEntry, translated, previousName);
					Log.WriteLine("AI Translate: UI updated to \"{0}\"", translated);
				});
			}
			else if (string.IsNullOrWhiteSpace(translated))
			{
				Log.WriteLine("AI Translate: Result was empty, not updating");
			}
		}
		catch (Exception ex)
		{
			Log.WriteLine("AI Translate: Exception: {0}", ex);
			if (IsHandleCreated && !IsDisposed)
				MessageBox.Show(this, string.Format(Strings.Msg_TranslateFailed, ex.Message), Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		finally
		{
			btnTranslate.Text = Strings.MainWindow_BtnTranslate;
			btnTranslate.Enabled = true;
		}
	}

	private void OnBtnBatchTranslate_Click(object? sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(Settings.Default.AiApiUrl) ||
			string.IsNullOrWhiteSpace(Settings.Default.AiApiKey))
		{
			MessageBox.Show(this, Strings.Msg_AiNotConfigured, Strings.Msg_CriticalError,
				MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		var entriesToTranslate = new List<IAppInfoEntry>();
		foreach (IAppInfoEntry entry in _ctx.Entries)
		{
			if (entry.IsModified && entry.Modification?.Changes != null &&
				entry.Modification.Changes.HasProperty(AppInfoPaths.Name))
				continue;
			if (string.IsNullOrWhiteSpace(entry.Name)) continue;
			entriesToTranslate.Add(entry);
		}

		if (entriesToTranslate.Count == 0)
		{
			MessageBox.Show(this, Strings.Msg_NoTranslatableGames, Strings.MainWindow_BatchTranslate,
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		string targetLang = Settings.Default.AiTargetLanguage;
		using var dlg = new BatchTranslateDialog(entriesToTranslate, targetLang);
		dlg.ShowDialog(this);

		_listManager.InvalidateAll();
		_editController.LoadEntry(_listManager.GetSelectedEntry());
	}

	private void OnBtnBatchReplace_Click(object? sender, EventArgs e)
	{
		if (_ctx.Entries == null || _ctx.Entries.Count == 0) return;
		using var dialog = new BatchReplaceDialog(_ctx.Entries);
		if (dialog.ShowDialog(this) == DialogResult.OK)
		{
			int replaced = dialog.ReplaceAll();
			if (replaced > 0)
			{
				_listManager.ClearDisplayNameCache();
				_listManager.SortEntries();
				_editController.LoadEntry(_listManager.GetSelectedEntry());
			}
		}
	}

	private void OnBtnSave_Click(object? sender, EventArgs e)
	{
		try { _steamData.Save(); }
		catch (Exception ex) { Log.WriteLine("Save failed: {0}", ex); MessageBox.Show(string.Format(Strings.Msg_SaveFailed, ex.Message)); }
	}

	private void OnBtnSaveApply_Click(object? sender, EventArgs e)
	{
		try { _steamData.Save(); }
		catch (Exception ex) { Log.WriteLine("Save failed: {0}", ex); MessageBox.Show(string.Format(Strings.Msg_SaveFailed, ex.Message)); return; }
		try { _steamData.Apply(); }
		catch (Exception ex2) { Log.WriteLine("Apply failed: {0}", ex2); MessageBox.Show(string.Format(Strings.Msg_ApplyFailed, ex2.Message)); }
	}

	private void OnBtnSaveApplyRestart_Click(object? sender, EventArgs e)
	{
		try
		{
			SteamProcessManager.KillSteam(_steamData.SteamDir, Strings.SteamData_WaitingForClose, (msg, waitWhile) =>
			{
				using var dlg = new WaitingDialog(msg);
				dlg.ShowAndWait(() => waitWhile());
			});
		}
		catch (Exception ex) { Log.WriteLine("KillSteam failed: {0}", ex); MessageBox.Show(string.Format(Strings.Msg_KillSteamFailed, ex.Message)); return; }
		try { _steamData.Save(); }
		catch (Exception ex) { Log.WriteLine("Save failed: {0}", ex); MessageBox.Show(string.Format(Strings.Msg_SaveFailed, ex.Message)); return; }
		try { _steamData.Apply(); }
		catch (Exception ex) { Log.WriteLine("Apply failed: {0}", ex); MessageBox.Show(string.Format(Strings.Msg_ApplyFailed, ex.Message)); return; }
		try { SteamProcessManager.EnsureSteamRunning(_steamData.SteamDir, focusIfRunning: true); }
		catch (Exception ex) { Log.WriteLine("Restart Steam failed: {0}", ex); MessageBox.Show(string.Format(Strings.Msg_RestartFailed, ex.Message)); }
	}

	private void OnHelp_Click(object? sender, EventArgs e)
	{
		using (var dlg = new About()) dlg.ShowDialog(this);
	}

	private void OnBtnShortcuts_Click(object? sender, EventArgs e)
	{
		using var dlg = new Form();
		dlg.Text = Strings.Shortcuts_Title;
		dlg.StartPosition = FormStartPosition.CenterParent;
		dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
		dlg.MaximizeBox = false;
		dlg.MinimizeBox = false;
		dlg.ShowInTaskbar = false;
		dlg.ClientSize = new Size(320, 300);
		var lbl = new Label();
		lbl.Dock = DockStyle.Fill;
		lbl.Font = new Font("Consolas", 9f);
		lbl.AutoSize = false;
		lbl.Padding = new Padding(12);
		lbl.Text = Strings.Shortcuts_Content;
		var btnClose = new Button();
		btnClose.Text = Strings.About_BtnClose;
		btnClose.Dock = DockStyle.Bottom;
		btnClose.Height = 30;
		btnClose.Click += (_, _) => dlg.Close();
		dlg.AcceptButton = btnClose;
		dlg.CancelButton = btnClose;
		dlg.Controls.Add(lbl);
		dlg.Controls.Add(btnClose);
		dlg.ShowDialog(this);
	}

	private void OnFilterSelectorMenu_Click(object? sender, EventArgs e)
	{
		if (!filterSelectorMenu.Visible)
		{
			Point screenLocation = filterSelector.PointToScreen(new Point(filterSelector.Size.Width, 0));
			filterSelectorMenu.Show(screenLocation);
		}
	}

	private void OnSortSelectorMenu_Click(object? sender, EventArgs e)
	{
		if (!sortSelectorMenu.Visible)
		{
			string currentField = Settings.Default.SortField;
			bool descending = Settings.Default.SortDescending;
			foreach (var item in sortSelectorMenu.Items)
			{
				if (item is not ToolStripMenuItem menuItem) continue;
				if (menuItem.Tag is string field)
					menuItem.Checked = field == currentField;
				else if (menuItem.Tag is bool isDesc)
					menuItem.Checked = isDesc == descending;
			}
			Point screenLocation = sortSelector.PointToScreen(new Point(sortSelector.Size.Width, 0));
			sortSelectorMenu.Show(screenLocation);
		}
	}

	private void panel1_Paint(object? sender, PaintEventArgs e)
	{
		float dpiX = e.Graphics.DpiX;
		float num = dpiX / 72f;
		Image logo = Resources.logo1;
		Size size = new Size((int)(150f * num), (int)(150f * num));
		Point location = new Point(panel1.ClientSize - size);
		Rectangle destRect = new Rectangle(location, size);
		e.Graphics.DrawImage(Resources.logo1, destRect, 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, _listManager.BgLogoAttributes);
	}

	private void MainWindow_Move(object? sender, EventArgs e)
	{
		if (base.WindowState != FormWindowState.Minimized)
		{
			Settings.Default.WindowLocationX = base.Location.X;
			Settings.Default.WindowLocationY = base.Location.Y;
		}
	}

	private void MainWindow_Resize(object? sender, EventArgs e)
	{
		if (base.WindowState != FormWindowState.Minimized)
		{
			Settings.Default.WindowSizeX = base.Size.Width;
			Settings.Default.WindowSizeY = base.Size.Height;
			panel1.Invalidate();
		}
	}

	private void splitContainer1_SplitterMoved(object? sender, SplitterEventArgs e)
	{
		Settings.Default.MainWindowSplitter = splitContainer1.SplitterDistance;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_editController?.Dispose();
			_listManager?.Dispose();
			components?.Dispose();
		}
		base.Dispose(disposing);
	}

	private void OnGreenButton_Paint(object? sender, PaintEventArgs e)
	{
		var btn = (System.Windows.Forms.Button)sender!;
		var g = e.Graphics;
		g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

		int radius = 6;
		var rect = new System.Drawing.Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
		using var path = new System.Drawing.Drawing2D.GraphicsPath();
		path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
		path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
		path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
		path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
		path.CloseFigure();

		// 清除背景（用父容器颜色填充角落）
		using var bgBrush = new System.Drawing.SolidBrush(btn.Parent!.BackColor);
		g.FillRectangle(bgBrush, btn.ClientRectangle);

		// 渐变填充
		using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
			new System.Drawing.Rectangle(0, 0, btn.Width, btn.Height),
			System.Drawing.Color.FromArgb(109, 184, 43),
			System.Drawing.Color.FromArgb(76, 107, 34),
			System.Drawing.Drawing2D.LinearGradientMode.Vertical);
		g.FillPath(brush, path);

		// 绘制文字
		var textRect = btn.ClientRectangle;
		textRect.Y -= 1;
		System.Windows.Forms.TextRenderer.DrawText(g, btn.Text, btn.Font, textRect, btn.ForeColor,
			System.Windows.Forms.TextFormatFlags.HorizontalCenter | System.Windows.Forms.TextFormatFlags.VerticalCenter);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		this.btnEditLaunch = new System.Windows.Forms.Button();
		this.btnRevert = new System.Windows.Forms.Button();
		this.btnTranslate = new System.Windows.Forms.Button();
		this.btnSearch = new System.Windows.Forms.Button();
		this.btnBatchTranslate = new System.Windows.Forms.Button();
		this.lblNumAppsShown = new System.Windows.Forms.ToolStripStatusLabel();
		this.lblModifiedCount = new System.Windows.Forms.ToolStripStatusLabel();
		this.lblAppId = new System.Windows.Forms.ToolStripStatusLabel();
		this.checkCompleted = new System.Windows.Forms.CheckBox();
		this.checkHidden = new System.Windows.Forms.CheckBox();
		this.toolTips = new System.Windows.Forms.ToolTip(this.components);
		this.listControllerSupport = new System.Windows.Forms.ComboBox();
		this.listVR = new System.Windows.Forms.ComboBox();
		this.filterSelectorMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
		this.filterSelector = new System.Windows.Forms.Button();
		this.splitContainer1 = new System.Windows.Forms.SplitContainer();
		this.appSelection = new System.Windows.Forms.ListBox();
		this.appFilterText = new SteamEdit.ClearableTextBox();
		this.panel1 = new System.Windows.Forms.Panel();
		this.listEntryType = new System.Windows.Forms.ComboBox();
		this.textGameName = new SteamEdit.ClearableTextBox();
		this.textSortAs = new SteamEdit.ClearableTextBox();
		this.flowLayoutPanel3 = new System.Windows.Forms.FlowLayoutPanel();
		this.btnSave = new System.Windows.Forms.Button();
		this.lblSortAs = new System.Windows.Forms.Label();
		this.btnSaveApply = new System.Windows.Forms.Button();
		this.lblName = new System.Windows.Forms.Label();
		this.lblControllerSupport = new System.Windows.Forms.Label();
		System.Windows.Forms.FlowLayoutPanel flowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
		System.Windows.Forms.FlowLayoutPanel flowLayoutPanel2 = new System.Windows.Forms.FlowLayoutPanel();
		this.btnSaveApplyRestart = new System.Windows.Forms.Button();
		this.btnSettings = new System.Windows.Forms.Button();
		this.btnHelp = new System.Windows.Forms.Button();
		this.btnShortcuts = new System.Windows.Forms.Button();
		this.lblVRCategory = new System.Windows.Forms.Label();
		System.Windows.Forms.StatusStrip statusStrip = new System.Windows.Forms.StatusStrip();
		flowLayoutPanel.SuspendLayout();
		flowLayoutPanel2.SuspendLayout();
		statusStrip.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.splitContainer1).BeginInit();
		this.splitContainer1.Panel1.SuspendLayout();
		this.splitContainer1.Panel2.SuspendLayout();
		this.splitContainer1.SuspendLayout();
		this.panel1.SuspendLayout();
		this.flowLayoutPanel3.SuspendLayout();
		base.SuspendLayout();
		this.btnSave.Location = new System.Drawing.Point(3, 3);
		this.btnSave.Name = "btnSave";
		this.btnSave.Size = new System.Drawing.Size(90, 34);
		this.btnSave.TabIndex = 0;
		this.btnSave.Text = "Save";
		this.toolTips.SetToolTip(this.btnSave, "Save all edits to mods file");
		this.btnSave.UseVisualStyleBackColor = true;
		this.btnSave.Click += new System.EventHandler(OnBtnSave_Click);
		this.lblSortAs.AutoSize = true;
		this.lblSortAs.BackColor = System.Drawing.Color.Transparent;
		this.lblSortAs.Location = new System.Drawing.Point(8, 36);
		this.lblSortAs.Name = "label2";
		this.lblSortAs.Size = new System.Drawing.Size(44, 13);
		this.lblSortAs.TabIndex = 11;
		this.lblSortAs.Text = "Sort As:";
		this.btnSaveApply.Location = new System.Drawing.Point(84, 3);
		this.btnSaveApply.Name = "btnSaveApply";
		this.btnSaveApply.Size = new System.Drawing.Size(110, 34);
		this.btnSaveApply.TabIndex = 1;
		this.btnSaveApply.Text = "Save && Apply";
		this.toolTips.SetToolTip(this.btnSaveApply, "Save all edits to mods file, and apply to Steam.");
		this.btnSaveApply.UseVisualStyleBackColor = true;
		this.btnSaveApply.Click += new System.EventHandler(OnBtnSaveApply_Click);
		this.lblName.AutoSize = true;
		this.lblName.BackColor = System.Drawing.Color.Transparent;
		this.lblName.Location = new System.Drawing.Point(14, 10);
		this.lblName.Name = "label1";
		this.lblName.Size = new System.Drawing.Size(38, 13);
		this.lblName.TabIndex = 1;
		this.lblName.Text = "Name:";
		this.lblControllerSupport.AutoSize = true;
		this.lblControllerSupport.BackColor = System.Drawing.Color.Transparent;
		this.lblControllerSupport.Location = new System.Drawing.Point(55, 92);
		this.lblControllerSupport.Name = "label3";
		this.lblControllerSupport.Size = new System.Drawing.Size(94, 13);
		this.lblControllerSupport.TabIndex = 21;
		this.lblControllerSupport.Text = "Controller Support:";
		flowLayoutPanel.AutoSize = true;
		flowLayoutPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
		flowLayoutPanel.BackColor = System.Drawing.Color.Transparent;
		flowLayoutPanel.Controls.Add(this.btnEditLaunch);
		flowLayoutPanel.Controls.Add(this.btnRevert);
		flowLayoutPanel.Controls.Add(this.btnTranslate);
		flowLayoutPanel.Controls.Add(this.btnSearch);
		flowLayoutPanel.Location = new System.Drawing.Point(14, 151);
		flowLayoutPanel.Margin = new System.Windows.Forms.Padding(2);
		flowLayoutPanel.Name = "flowLayoutPanel2";
		flowLayoutPanel.Size = new System.Drawing.Size(195, 40);
		flowLayoutPanel.TabIndex = 9;
		flowLayoutPanel.WrapContents = false;
		this.btnEditLaunch.Location = new System.Drawing.Point(3, 3);
		this.btnEditLaunch.Name = "btnEditLaunch";
		this.btnEditLaunch.Size = new System.Drawing.Size(90, 34);
		this.btnEditLaunch.TabIndex = 0;
		this.btnEditLaunch.Text = "Edit Launch";
		this.btnEditLaunch.UseVisualStyleBackColor = true;
		this.btnEditLaunch.Click += new System.EventHandler(OnBtnEditLaunch_Click);
		this.btnRevert.Location = new System.Drawing.Point(84, 3);
		this.btnRevert.Name = "btnRevert";
		this.btnRevert.Size = new System.Drawing.Size(108, 34);
		this.btnRevert.TabIndex = 1;
		this.btnRevert.Text = "Revert Game";
		this.btnRevert.UseVisualStyleBackColor = true;
		this.btnRevert.Click += new System.EventHandler(OnBtnRevert_Click);
		this.btnTranslate.Name = "btnTranslate";
		this.btnTranslate.Size = new System.Drawing.Size(75, 34);
		this.btnTranslate.TabIndex = 2;
		this.btnTranslate.Text = "AI Translate";
		this.btnTranslate.UseVisualStyleBackColor = true;
		this.btnTranslate.Click += new System.EventHandler(OnBtnTranslate_Click);
		this.btnBatchTranslate.Name = "btnBatchTranslate";
		this.btnBatchTranslate.Size = new System.Drawing.Size(130, 34);
		this.btnBatchTranslate.TabIndex = 3;
		this.btnBatchTranslate.Text = "Batch Translate";
		this.toolTips.SetToolTip(this.btnBatchTranslate, "Batch translate all filtered games");
		this.btnBatchTranslate.UseVisualStyleBackColor = true;
		this.btnBatchTranslate.Click += new System.EventHandler(OnBtnBatchTranslate_Click);
		this.btnBatchReplace = new System.Windows.Forms.Button();
		this.btnBatchReplace.Name = "btnBatchReplace";
		this.btnBatchReplace.Size = new System.Drawing.Size(130, 34);
		this.btnBatchReplace.TabIndex = 4;
		this.btnBatchReplace.Text = "Find & Replace";
		this.btnBatchReplace.UseVisualStyleBackColor = true;
		this.btnBatchReplace.Click += new System.EventHandler(OnBtnBatchReplace_Click);
		this.btnSearch.Name = "btnSearch";
		this.btnSearch.Size = new System.Drawing.Size(75, 34);
		this.btnSearch.TabIndex = 3;
		this.btnSearch.Text = "Search";
		this.btnSearch.UseVisualStyleBackColor = true;
		this.btnSearch.Click += new System.EventHandler(OnBtnSearch_Click);
		flowLayoutPanel2.AutoSize = true;
		flowLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
		flowLayoutPanel2.Controls.Add(this.btnSave);
		flowLayoutPanel2.Controls.Add(this.btnSaveApply);
		flowLayoutPanel2.Controls.Add(this.btnSaveApplyRestart);
		flowLayoutPanel2.Controls.Add(this.btnBatchTranslate);
		flowLayoutPanel2.Controls.Add(this.btnBatchReplace);
		flowLayoutPanel2.Location = new System.Drawing.Point(0, 0);
		flowLayoutPanel2.Margin = new System.Windows.Forms.Padding(0);
		flowLayoutPanel2.Name = "flowLayoutPanel1";
		flowLayoutPanel2.AutoSize = true;
		flowLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
		flowLayoutPanel2.Size = new System.Drawing.Size(350, 40);
		flowLayoutPanel2.TabIndex = 9;
		this.btnSaveApplyRestart.Location = new System.Drawing.Point(174, 3);
		this.btnSaveApplyRestart.Name = "btnSaveApplyRestart";
		this.btnSaveApplyRestart.Size = new System.Drawing.Size(140, 34);
		this.btnSaveApplyRestart.TabIndex = 2;
		this.btnSaveApplyRestart.Text = "Save && Apply && Restart";
		this.toolTips.SetToolTip(this.btnSaveApplyRestart, "Save edits, apply to Steam, and restart Steam.");
		this.btnSaveApplyRestart.UseVisualStyleBackColor = false;
		this.btnSaveApplyRestart.BackColor = System.Drawing.Color.Transparent;
		this.btnSaveApplyRestart.ForeColor = System.Drawing.Color.White;
		this.btnSaveApplyRestart.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnSaveApplyRestart.FlatAppearance.BorderSize = 0;
		this.btnSaveApplyRestart.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
		this.btnSaveApplyRestart.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
		this.btnSaveApplyRestart.Font = new System.Drawing.Font(this.btnSaveApplyRestart.Font, System.Drawing.FontStyle.Bold);
		this.btnSaveApplyRestart.Paint += OnGreenButton_Paint;
		this.btnSaveApplyRestart.Click += new System.EventHandler(OnBtnSaveApplyRestart_Click);
		this.btnSettings.Image = SteamEdit.Properties.Resources.base_cog_32;
		this.btnSettings.Location = new System.Drawing.Point(3, 3);
		this.btnSettings.Name = "btnSettings";
		this.btnSettings.Size = new System.Drawing.Size(35, 34);
		this.btnSettings.TabIndex = 0;
		this.toolTips.SetToolTip(this.btnSettings, "Settings");
		this.btnSettings.UseVisualStyleBackColor = true;
		this.btnSettings.Click += new System.EventHandler(OnBtnSettings_Click);
		this.btnHelp.Location = new System.Drawing.Point(44, 3);
		this.btnHelp.Name = "btnHelp";
		this.btnHelp.Size = new System.Drawing.Size(34, 34);
		this.btnHelp.TabIndex = 1;
		this.btnHelp.Text = "?";
		this.toolTips.SetToolTip(this.btnHelp, "About");
		this.btnHelp.UseVisualStyleBackColor = true;
		this.btnHelp.Click += new System.EventHandler(OnHelp_Click);
		this.btnShortcuts.Location = new System.Drawing.Point(78, 3);
		this.btnShortcuts.Name = "btnShortcuts";
		this.btnShortcuts.Size = new System.Drawing.Size(34, 34);
		this.btnShortcuts.TabIndex = 28;
		this.btnShortcuts.Text = "H";
		this.toolTips.SetToolTip(this.btnShortcuts, "Keyboard Shortcuts");
		this.btnShortcuts.UseVisualStyleBackColor = true;
		this.btnShortcuts.Click += new System.EventHandler(OnBtnShortcuts_Click);
		this.lblVRCategory.AutoSize = true;
		this.lblVRCategory.BackColor = System.Drawing.Color.Transparent;
		this.lblVRCategory.Location = new System.Drawing.Point(79, 126);
		this.lblVRCategory.Name = "label4";
		this.lblVRCategory.Size = new System.Drawing.Size(70, 13);
		this.lblVRCategory.TabIndex = 24;
		this.lblVRCategory.Text = "VR Category:";
		statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[5] { this.lblNumAppsShown, new System.Windows.Forms.ToolStripSeparator(), this.lblModifiedCount, new System.Windows.Forms.ToolStripSeparator(), this.lblAppId });
		statusStrip.Location = new System.Drawing.Point(0, 306);
		statusStrip.Name = "statusStrip1";
		statusStrip.Size = new System.Drawing.Size(227, 22);
		statusStrip.SizingGrip = false;
		statusStrip.TabIndex = 3;
		statusStrip.Text = "statusStrip1";
		this.lblNumAppsShown.Name = "lblNumAppsShown";
		this.lblNumAppsShown.Size = new System.Drawing.Size(90, 17);
		this.lblNumAppsShown.Text = "Shown: ({0}/{1})";
		this.lblModifiedCount.Name = "lblModifiedCount";
		this.lblModifiedCount.Size = new System.Drawing.Size(90, 17);
		this.lblModifiedCount.Text = "Modified: {0}";
		this.lblAppId.Name = "lblAppId";
		this.lblAppId.Size = new System.Drawing.Size(80, 17);
		this.lblAppId.Text = "App ID: —";
		this.checkCompleted.AutoSize = true;
		this.checkCompleted.BackColor = System.Drawing.Color.Transparent;
		this.checkCompleted.Location = new System.Drawing.Point(129, 59);
		this.checkCompleted.Name = "checkCompleted";
		this.checkCompleted.Size = new System.Drawing.Size(76, 17);
		this.checkCompleted.TabIndex = 3;
		this.checkCompleted.Text = "Completed";
		this.checkCompleted.UseVisualStyleBackColor = false;
		this.checkHidden.AutoSize = true;
		this.checkHidden.BackColor = System.Drawing.Color.Transparent;
		this.checkHidden.Location = new System.Drawing.Point(58, 59);
		this.checkHidden.Name = "checkHidden";
		this.checkHidden.Size = new System.Drawing.Size(60, 17);
		this.checkHidden.TabIndex = 2;
		this.checkHidden.Text = "Hidden";
		this.checkHidden.UseVisualStyleBackColor = false;
		this.listControllerSupport.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.listControllerSupport.FormattingEnabled = true;
		this.listControllerSupport.Location = new System.Drawing.Point(155, 89);
		this.listControllerSupport.Name = "listControllerSupport";
		this.listControllerSupport.Size = new System.Drawing.Size(121, 21);
		this.listControllerSupport.TabIndex = 5;
		this.listVR.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.listVR.FormattingEnabled = true;
		this.listVR.Location = new System.Drawing.Point(155, 123);
		this.listVR.Name = "listVR";
		this.listVR.Size = new System.Drawing.Size(71, 21);
		this.listVR.TabIndex = 7;
		this.filterSelectorMenu.Name = "filterSelectorMenu";
		this.filterSelectorMenu.ShowCheckMargin = true;
		this.filterSelectorMenu.Size = new System.Drawing.Size(83, 4);
		this.filterSelector.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.filterSelector.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.filterSelector.Image = SteamEdit.Properties.Resources.filter;
		this.filterSelector.Location = new System.Drawing.Point(201, 0);
		this.filterSelector.Margin = new System.Windows.Forms.Padding(0);
		this.filterSelector.Name = "filterSelector";
		this.filterSelector.Size = new System.Drawing.Size(26, 26);
		this.filterSelector.TabIndex = 0;
		this.filterSelector.UseVisualStyleBackColor = true;
		this.filterSelector.Click += new System.EventHandler(OnFilterSelectorMenu_Click);
		this.sortSelectorMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
		this.sortSelector = new System.Windows.Forms.Button();
		this.sortSelectorMenu.Name = "sortSelectorMenu";
		this.sortSelectorMenu.ShowCheckMargin = true;
		this.sortSelectorMenu.Size = new System.Drawing.Size(83, 4);
		this.sortSelector.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.sortSelector.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.sortSelector.Text = "↕";
		this.sortSelector.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
		this.sortSelector.Location = new System.Drawing.Point(175, 0);
		this.sortSelector.Margin = new System.Windows.Forms.Padding(0);
		this.sortSelector.Name = "sortSelector";
		this.sortSelector.Size = new System.Drawing.Size(26, 26);
		this.sortSelector.TabIndex = 0;
		this.sortSelector.UseVisualStyleBackColor = true;
		this.sortSelector.Click += new System.EventHandler(OnSortSelectorMenu_Click);
		this.splitContainer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.splitContainer1.Location = new System.Drawing.Point(0, 43);
		this.splitContainer1.Name = "splitContainer1";
		this.splitContainer1.Panel1.Controls.Add(statusStrip);
		this.splitContainer1.Panel1.Controls.Add(this.appSelection);
		this.splitContainer1.Panel1.Controls.Add(this.appFilterText);
		this.splitContainer1.Panel1.Controls.Add(this.sortSelector);
		this.splitContainer1.Panel1.Controls.Add(this.filterSelector);
		this.splitContainer1.Panel1MinSize = 50;
		this.splitContainer1.Panel2.Controls.Add(this.panel1);
		this.splitContainer1.Size = new System.Drawing.Size(690, 330);
		this.splitContainer1.SplitterDistance = 229;
		this.splitContainer1.TabIndex = 0;
		this.splitContainer1.TabStop = false;
		this.splitContainer1.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(splitContainer1_SplitterMoved);
		this.appSelection.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.appSelection.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
		this.appSelection.FormattingEnabled = true;
		this.appSelection.IntegralHeight = false;
		this.appSelection.ItemHeight = 20;
		this.appSelection.Location = new System.Drawing.Point(2, 29);
		this.appSelection.Name = "appSelection";
		this.appSelection.Size = new System.Drawing.Size(221, 274);
		this.appSelection.TabIndex = 2;
		this.appFilterText.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.appFilterText.AutoClearX = true;
		this.appFilterText.ClearXVisible = false;
		this.appFilterText.Location = new System.Drawing.Point(3, 3);
		this.appFilterText.Name = "appFilterText";
		this.appFilterText.Size = new System.Drawing.Size(169, 20);
		this.appFilterText.TabIndex = 1;
		this.panel1.AutoScroll = true;
		this.panel1.Controls.Add(this.listEntryType);
		this.panel1.Controls.Add(this.textGameName);
		this.panel1.Controls.Add(flowLayoutPanel);
		this.panel1.Controls.Add(this.checkCompleted);
		this.panel1.Controls.Add(this.lblName);
		this.panel1.Controls.Add(this.lblControllerSupport);
		this.panel1.Controls.Add(this.checkHidden);
		this.panel1.Controls.Add(this.textSortAs);
		this.panel1.Controls.Add(this.listVR);
		this.panel1.Controls.Add(this.listControllerSupport);
		this.panel1.Controls.Add(this.lblVRCategory);
		this.panel1.Controls.Add(this.lblSortAs);
		this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.panel1.Location = new System.Drawing.Point(0, 0);
		this.panel1.Margin = new System.Windows.Forms.Padding(0);
		this.panel1.Name = "panel1";
		this.panel1.Size = new System.Drawing.Size(455, 328);
		this.panel1.TabIndex = 26;
		this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(panel1_Paint);
		this.listEntryType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.listEntryType.FormattingEnabled = true;
		this.listEntryType.Location = new System.Drawing.Point(232, 57);
		this.listEntryType.Name = "listEntryType";
		this.listEntryType.Size = new System.Drawing.Size(79, 21);
		this.listEntryType.TabIndex = 4;
		this.textGameName.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.textGameName.AutoClearX = false;
		this.textGameName.ClearXVisible = true;
		this.textGameName.Location = new System.Drawing.Point(58, 7);
		this.textGameName.Name = "textGameName";
		this.textGameName.Size = new System.Drawing.Size(386, 20);
		this.textGameName.TabIndex = 0;
		this.textSortAs.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.textSortAs.AutoClearX = false;
		this.textSortAs.ClearXVisible = true;
		this.textSortAs.Location = new System.Drawing.Point(58, 33);
		this.textSortAs.Name = "textSortAs";
		this.textSortAs.Size = new System.Drawing.Size(386, 20);
		this.textSortAs.TabIndex = 1;
		this.flowLayoutPanel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.flowLayoutPanel3.AutoSize = true;
		this.flowLayoutPanel3.Controls.Add(this.btnShortcuts);
		this.flowLayoutPanel3.Controls.Add(this.btnHelp);
		this.flowLayoutPanel3.Controls.Add(this.btnSettings);
		this.flowLayoutPanel3.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
		this.flowLayoutPanel3.Location = new System.Drawing.Point(609, 0);
		this.flowLayoutPanel3.Name = "flowLayoutPanel3";
		this.flowLayoutPanel3.Size = new System.Drawing.Size(81, 40);
		this.flowLayoutPanel3.TabIndex = 27;
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		base.ClientSize = new System.Drawing.Size(690, 373);
		base.Controls.Add(this.flowLayoutPanel3);
		base.Controls.Add(this.splitContainer1);
		base.Controls.Add(flowLayoutPanel2);
		base.KeyPreview = true;
		base.MaximizeBox = true;
		this.MinimumSize = new System.Drawing.Size(518, 289);
		base.Name = "MainWindow";
		this.Text = "SteamEdit";
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(OnMainWindow_FormClosing);
		base.Load += new System.EventHandler(MainWindow_Load);
		base.KeyDown += new System.Windows.Forms.KeyEventHandler(OnMainWindow_KeyDown);
		base.Move += new System.EventHandler(MainWindow_Move);
		base.Resize += new System.EventHandler(MainWindow_Resize);
		flowLayoutPanel.ResumeLayout(false);
		flowLayoutPanel2.ResumeLayout(false);
		statusStrip.ResumeLayout(false);
		statusStrip.PerformLayout();
		this.splitContainer1.Panel1.ResumeLayout(false);
		this.splitContainer1.Panel1.PerformLayout();
		this.splitContainer1.Panel2.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.splitContainer1).EndInit();
		this.splitContainer1.ResumeLayout(false);
		this.panel1.ResumeLayout(false);
		this.panel1.PerformLayout();
		this.flowLayoutPanel3.ResumeLayout(false);
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
