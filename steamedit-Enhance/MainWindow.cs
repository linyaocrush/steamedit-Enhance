#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Common;
using steamedit_Enhance.Data;
using steamedit_Enhance.Properties;
using steamedit_Enhance.ViewModels;

namespace steamedit_Enhance;

internal partial class MainWindow : LocalizableForm
{
	private readonly ISteamDataService _steamData;

	private GameDataContext _ctx = null!;
	private MainViewModel _mainViewModel = null!;
	private GameListViewModel _gameListViewModel = null!;
	private PropertyEditorViewModel _propertyEditorViewModel = null!;
	private GameListManager _listManager = null!;
	private EditController _editController = null!;

	private readonly ShortcutHandler _shortcutHandler;
	private readonly List<(Control Control, Func<string> Text)> _tooltipMappings = new();

	public MainWindow(ISteamDataService steamData)
	{
		_steamData = steamData;
		InitializeComponent();
		ApplyTheme();
		SetupServices();
		_shortcutHandler = new ShortcutHandler();
		RegisterShortcuts();
		StartSteamMonitor();
	}

	private void SetupServices()
	{
		listEntryType.Format += ComboBoxFormatter.FormatEntryType;

		_tooltipMappings.Add((btnTranslate, () => Strings.Tooltip_Translate));
		_tooltipMappings.Add((btnBatchTranslate, () => Strings.Tooltip_BatchTranslate));
		_tooltipMappings.Add((btnBatchReplace, () => Strings.Tooltip_BatchReplace));
		_tooltipMappings.Add((btnSearch, () => Strings.Tooltip_Search));
		_tooltipMappings.Add((btnSave, () => Strings.Tooltip_Save));
		_tooltipMappings.Add((btnSaveApply, () => Strings.Tooltip_SaveApply));
		_tooltipMappings.Add((btnSaveApplyRestart, () => Strings.Tooltip_SaveApplyRestart));
		_tooltipMappings.Add((btnSettings, () => Strings.Tooltip_Settings));
		_tooltipMappings.Add((btnHelp, () => Strings.Tooltip_About));
		_tooltipMappings.Add((btnShortcuts, () => Strings.Tooltip_Shortcuts));
		_tooltipMappings.Add((sortSelector, () => Strings.Sort_Title));

		base.Icon = Resources.logo;

		_ctx = new GameDataContext();
		_gameListViewModel = new GameListViewModel(_steamData.DataStore);
		_propertyEditorViewModel = new PropertyEditorViewModel();
		_mainViewModel = new MainViewModel(_steamData, _gameListViewModel, _propertyEditorViewModel);

		listControllerSupport.Items.Add("none");
		listControllerSupport.Items.Add("partial");
		listControllerSupport.Items.Add("full");
		listControllerSupport.Format += ComboBoxFormatter.FormatControllerSupport;
		listVR.Items.Add("default");
		listVR.Items.Add("removed");
		listVR.Format += ComboBoxFormatter.FormatVR;

		BindingConfigurator.Configure(
			_propertyEditorViewModel,
			textGameName, textSortAs,
			checkHidden, checkCompleted,
			listControllerSupport, listVR, listEntryType);

		textGameName.ClearXClicked += (_, e) =>
		{
			e.Handled = true;
			_editController.ClearProperty(AppInfoPaths.Name, refreshList: true);
		};
		textSortAs.ClearXClicked += (_, e) =>
		{
			e.Handled = true;
			_editController.ClearProperty(AppInfoPaths.SortAs);
		};

		_mainViewModel.PropertyChanged += (_, e) =>
		{
			switch (e.PropertyName)
			{
				case nameof(MainViewModel.WindowTitle):
					Text = _mainViewModel.WindowTitle;
					break;
				case nameof(MainViewModel.AppIdText):
					lblAppId.Text = _mainViewModel.AppIdText;
					break;
			}
		};

		_mainViewModel.StatusChanged += (_, _) =>
		{
			lblNumAppsShown.Text = string.Format(Strings.MainWindow_StatusShown, _mainViewModel.GameList.VisibleCount, _mainViewModel.GameList.TotalEntryCount);
			lblModifiedCount.Text = string.Format(Strings.MainWindow_StatusModified, _mainViewModel.GameList.ModifiedCount);
		};

		_listManager = new GameListManager(
			_ctx, _gameListViewModel, appSelection, filterSelectorMenu, sortSelectorMenu,
			toolTips, appFilterText);

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
			_mainViewModel.UpdateAppId(entry?.ID ?? 0);
		};

		appSelection.DoubleClick += (_, _) => textGameName.Focus();

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

		_listManager.BatchRename += OnBatchRename;
		_listManager.BatchChangeType += OnBatchChangeType;
		_listManager.BatchMarkAsHidden += OnBatchMarkAsHidden;
		_listManager.BatchMarkAsVisible += OnBatchMarkAsVisible;
		_listManager.BatchMarkAsCompleted += OnBatchMarkAsCompleted;
		_listManager.BatchMarkAsIncomplete += OnBatchMarkAsIncomplete;
		_listManager.BatchReset += OnBatchReset;

		_mainViewModel.SaveRequested += (_, _) => OnBtnSave_Click(null, EventArgs.Empty);
		_mainViewModel.SaveApplyRequested += (_, _) => OnBtnSaveApply_Click(null, EventArgs.Empty);
		_mainViewModel.SaveApplyRestartRequested += (_, _) => OnBtnSaveApplyRestart_Click(null, EventArgs.Empty);
		_mainViewModel.UndoRequested += (_, _) => _editController.PerformUndo();
		_mainViewModel.SettingsRequested += (_, _) => OnBtnSettings_Click(null, EventArgs.Empty);
		_mainViewModel.EditLaunchRequested += (_, _) => OnBtnEditLaunch_Click(null, EventArgs.Empty);
		_mainViewModel.RevertRequested += (_, _) => OnBtnRevert_Click(null, EventArgs.Empty);
		_mainViewModel.SearchRequested += (_, _) => OnBtnSearch_Click(null, EventArgs.Empty);
		_mainViewModel.TranslateRequested += (_, _) => OnBtnTranslate_Click(null, EventArgs.Empty);
		_mainViewModel.BatchTranslateRequested += (_, _) => OnBtnBatchTranslate_Click(null, EventArgs.Empty);
		_mainViewModel.BatchReplaceRequested += (_, _) => OnBtnBatchReplace_Click(null, EventArgs.Empty);
		_mainViewModel.AboutRequested += (_, _) => OnHelp_Click(null, EventArgs.Empty);
		_mainViewModel.ShortcutsRequested += (_, _) => OnBtnShortcuts_Click(null, EventArgs.Empty);

		_editController = new EditController(
			_ctx, _steamData.DataStore, _listManager, _propertyEditorViewModel);
	}

	private void StartSteamMonitor()
	{
		UpdateSteamStatus();
		steamStatusTimer.Start();
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
		_mainViewModel.RefreshTitle();
		_listManager.RefreshGameList();
		appSelection.Select();
	}

	private void UpdateStatusBar()
	{
		lblNumAppsShown.Text = string.Format(Strings.MainWindow_StatusShown, _mainViewModel.GameList.VisibleCount, _mainViewModel.GameList.TotalEntryCount);
		lblModifiedCount.Text = string.Format(Strings.MainWindow_StatusModified, _mainViewModel.GameList.ModifiedCount);
	}

	private void UpdateSteamStatus()
	{
		bool running = SteamProcessManager.IsSteamRunning(_steamData.SteamDir);
		lblSteamStatus.Text = running ? Strings.SteamStatus_Running : Strings.SteamStatus_NotRunning;
		lblSteamStatus.ForeColor = running ? System.Drawing.Color.FromArgb(56, 163, 82) : System.Drawing.Color.Gray;

		string version = GetSteamVersion();
		lblSteamVersion.Text = version.Length > 0 ? $"v{version}" : "";
		lblSteamVersion.ForeColor = System.Drawing.Color.Gray;
	}

	private void SetSteamStatusLinkStyle(bool isHover)
	{
		if (isHover)
		{
			lblSteamStatus.ForeColor = System.Drawing.Color.FromArgb(0, 102, 204);
			lblSteamStatus.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Underline);
			lblSteamStatus.Cursor = System.Windows.Forms.Cursors.Hand;
		}
		else
		{
			bool running = SteamProcessManager.IsSteamRunning(_steamData.SteamDir);
			lblSteamStatus.ForeColor = running ? System.Drawing.Color.FromArgb(56, 163, 82) : System.Drawing.Color.Gray;
			lblSteamStatus.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular);
			lblSteamStatus.Cursor = System.Windows.Forms.Cursors.Default;
		}
	}

	private static string GetSteamVersion()
	{
		try
		{
			using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
			if (key == null) return "";
			string? exePath = key.GetValue("SteamExe") as string;
			if (string.IsNullOrEmpty(exePath) || !System.IO.File.Exists(exePath)) return "";
			var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
			return versionInfo.FileVersion ?? "";
		}
		catch
		{
			return "";
		}
	}

	protected override void ApplyLanguage()
	{
		_mainViewModel.RefreshTitle();

		checkHidden.Text = Strings.MainWindow_CheckHidden;
		checkCompleted.Text = Strings.MainWindow_CheckCompleted;

		listControllerSupport.Refresh();
		listVR.Refresh();
		listEntryType.Refresh();

		_listManager.ApplyLanguageToFilters();

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
		foreach (var (control, text) in _tooltipMappings)
			toolTips.SetToolTip(control, text());
		btnBatchTranslate.Text = Strings.MainWindow_BatchTranslate;
		btnBatchReplace.Text = Strings.MainWindow_BatchReplace;

		UpdateStatusBar();
		UpdateSteamStatus();
		_listManager.ClearDisplayNameCache();
		_editController.LoadEntry(_listManager.GetSelectedEntry());
	}

	private void OnMainWindow_FormClosing(object? sender, FormClosingEventArgs e)
	{
		_mainViewModel.DetachEvents();
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
		if (_shortcutHandler.TryHandle(e.KeyCode, e.Control, e.Shift))
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			return;
		}
		switch (e.KeyCode)
		{
		case Keys.Next:
			{
				int cur = appSelection.SelectedIndices.Count > 0 ? appSelection.SelectedIndices[0] : -1;
				int next = Math.Min(cur + 1, _ctx.Entries.Count - 1);
				appSelection.SelectedIndices.Clear();
				if (next >= 0) appSelection.SelectedIndices.Add(next);
			}
			break;
		case Keys.Prior:
			{
				int cur = appSelection.SelectedIndices.Count > 0 ? appSelection.SelectedIndices[0] : -1;
				int prev = Math.Max(cur - 1, 0);
				appSelection.SelectedIndices.Clear();
				appSelection.SelectedIndices.Add(prev);
			}
			break;
		}
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

	private void ApplyTheme()
	{
		ThemeManager.ApplyToForm(this);

		splitContainer1.BackColor = ThemeManager.BorderColor;
		splitContainer1.Panel1.BackColor = ThemeManager.PanelBackColor;
		splitContainer1.Panel2.BackColor = ThemeManager.BackColor;

		panel1.BackColor = ThemeManager.PanelBackColor;

		foreach (Control c in splitContainer1.Panel1.Controls)
		{
			if (c is StatusStrip ss)
			{
				ThemeManager.StyleToolStrip(ss);
				break;
			}
		}

		ThemeManager.StyleButton(btnSave, isPrimary: true);
		ThemeManager.StyleButton(btnSaveApply, isPrimary: true);

		ThemeManager.StyleButton(btnEditLaunch);
		ThemeManager.StyleButton(btnRevert);
		ThemeManager.StyleButton(btnTranslate);
		ThemeManager.StyleButton(btnSearch);
		ThemeManager.StyleButton(btnBatchTranslate);
		ThemeManager.StyleButton(btnBatchReplace);
		ThemeManager.StyleButton(btnSettings);
		ThemeManager.StyleButton(btnHelp);
		ThemeManager.StyleButton(btnShortcuts);
		ThemeManager.StyleButton(filterSelector);
		ThemeManager.StyleButton(sortSelector);

		appSelection.BackColor = ThemeManager.ListBackColor;
		appSelection.BorderStyle = BorderStyle.None;

		ThemeManager.StyleTextBox(textGameName);
		ThemeManager.StyleTextBox(textSortAs);
		ThemeManager.StyleTextBox(appFilterText);

		ThemeManager.StyleComboBox(listControllerSupport);
		ThemeManager.StyleComboBox(listVR);
		ThemeManager.StyleComboBox(listEntryType);

		ThemeManager.StyleCheckBox(checkHidden);
		ThemeManager.StyleCheckBox(checkCompleted);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_listManager?.Dispose();
			components?.Dispose();
		}
		base.Dispose(disposing);
	}

	private static void ShowShortcutsDialog(IWin32Window owner)
	{
		using var dlg = new Form();
		dlg.Text = Strings.Shortcuts_Title;
		dlg.StartPosition = FormStartPosition.CenterParent;
		dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
		dlg.MaximizeBox = false;
		dlg.MinimizeBox = false;
		dlg.ShowInTaskbar = false;
		dlg.ClientSize = new Size(380, 340);
		ThemeManager.ApplyToForm(dlg);
		var lbl = new Label();
		lbl.Dock = DockStyle.Fill;
		lbl.Font = new Font("Consolas", 9f);
		lbl.AutoSize = false;
		lbl.Padding = new Padding(16, 12, 12, 12);
		lbl.Text = Strings.Shortcuts_Content;
		lbl.BackColor = ThemeManager.PanelBackColor;
		var btnClose = new Button();
		btnClose.Text = Strings.About_BtnClose;
		btnClose.Dock = DockStyle.Bottom;
		btnClose.Height = 36;
		btnClose.Click += (_, _) => dlg.Close();
		dlg.AcceptButton = btnClose;
		dlg.CancelButton = btnClose;
		ThemeManager.StyleButton(btnClose);
		dlg.Controls.Add(lbl);
		dlg.Controls.Add(btnClose);
		dlg.ShowDialog(owner);
	}
}
