using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Common;
using SteamEdit.Data;
using SteamEdit.Properties;

namespace SteamEdit;

internal class MainWindow : Form
{
	private string _originalTitle;

	private string _originalNumShownLabelText;

	private List<IAppInfoEntry> _entries;
	private int _totalEntryCount;

	private static readonly string[] ControllerRawValues = { "none", "partial", "full" };

	private static readonly string[] VRRawValues = { "default", "removed" };

	private Dictionary<AppInfoEntryType, Image> _filterImages;

	private AppInfoEntryType _currentFilters;

	private bool _isSettingUpEntry;

	private Color _defaultNameColor;

	private Color _defaultSortAsColor;

	private Font _defaultAppSelectionFont;

	private Font _boldAppSelectionFont;

	private Font _hiddenAppSelectionFont;

	private bool _refreshingGamesListAfterEdit;

	private StringBuilder appSearchTerm = new StringBuilder();

	private Timer appSearchTermTimer = new Timer();

	private Timer _filterDebounceTimer = new Timer();

	private ImageAttributes _bgLogoAttributes;

	private int appSelection_LastHoverI = -1;

	private IContainer components;

	private ClearableTextBox textGameName;

	private CheckBox checkHidden;

	private CheckBox checkCompleted;

	private ClearableTextBox textSortAs;

	private Button btnEditLaunch;

	private Button btnRevert;

	private ToolTip toolTips;

	private ComboBox listControllerSupport;

	private Button btnControllerBatch;

	private Button btnVRBatch;

	private Button btnSettings;

	private Button btnHelp;

	private Button btnTranslate;

	private Button btnSearch;

	private Button btnBatchTranslate;

	private ComboBox listVR;

	private ContextMenuStrip filterSelectorMenu;

	private Button filterSelector;

	private SplitContainer splitContainer1;

	private ListBox appSelection;

	private ClearableTextBox appFilterText;

	private Panel panel1;

	private FlowLayoutPanel flowLayoutPanel3;

	private ComboBox listEntryType;

	private ToolStripStatusLabel lblNumAppsShown;

	private Label lblSortAs;

	private Label lblName;

	private Label lblControllerSupport;

	private Label lblVRCategory;

	private Button btnSave;

	private Button btnSaveApply;

	private Button btnSaveApplyRestart;

	public MainWindow()
	{
		_isSettingUpEntry = false;
		float[][] array = new float[3][]
		{
			new float[3] { 0.3f, 0.3f, 0.3f },
			new float[3] { 0.59f, 0.59f, 0.59f },
			new float[3] { 0.11f, 0.11f, 0.11f }
		};
		float[][] array2 = new float[3][]
		{
			new float[3] { 1f, 0f, 0f },
			new float[3] { 0f, 1f, 0f },
			new float[3] { 0f, 0f, 1f }
		};
		float num = 0.5f;
		ColorMatrix colorMatrix = new ColorMatrix();
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				colorMatrix[i, j] = num * array[i][j] + (1f - num) * array2[i][j];
			}
		}
		colorMatrix[3, 3] = 0.08f;
		_bgLogoAttributes = new ImageAttributes();
		_bgLogoAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
		InitializeComponent();
		appSearchTermTimer.Interval = 500;
		appSearchTermTimer.Tick += delegate
		{
			appSearchTerm.Clear();
			appSearchTermTimer.Stop();
		};
		_filterDebounceTimer.Interval = 500;
		_filterDebounceTimer.Tick += delegate
		{
			_filterDebounceTimer.Stop();
			RefreshGameList();
		};
		_currentFilters = Settings.Default.Filters;
		_filterImages = new Dictionary<AppInfoEntryType, Image>
		{
			{
				AppInfoEntryType.Game,
				Resources.filter_game
			},
			{
				AppInfoEntryType.Application,
				Resources.filter_application
			},
			{
				AppInfoEntryType.Video,
				Resources.filter_video
			},
			{
				AppInfoEntryType.Music,
				Resources.filter_music
			},
			{
				AppInfoEntryType.Guide,
				Resources.filter_guide
			},
			{
				AppInfoEntryType.DLC,
				Resources.filter_dlc
			},
			{
				AppInfoEntryType.Demo,
				Resources.filter_demo
			},
			{
				AppInfoEntryType.Tool,
				Resources.filter_tool
			},
			{
				AppInfoEntryType.Config,
				Resources.filter_config
			},
			{
				AppInfoEntryType.Beta,
				Resources.filter_beta
			},
			{
				AppInfoEntryType.Unknown,
				Resources.filter_unknown
			}
		};
		listEntryType.BeginUpdate();
		foreach (KeyValuePair<AppInfoEntryType, Image> filterImage in _filterImages)
		{
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(I18n.GetEnumDisplayName(filterImage.Key), filterImage.Value)
			{
				DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
				CheckOnClick = true,
				Tag = filterImage.Key,
				Checked = _currentFilters.HasFlag(filterImage.Key)
			};
			toolStripMenuItem.CheckedChanged += OnFilter_CheckedChanged;
			filterSelectorMenu.Items.Add(toolStripMenuItem);
			if (filterImage.Key != AppInfoEntryType.Unknown)
			{
				listEntryType.Items.Add(filterImage.Key);
			}
		}
		filterSelectorMenu.Items.Add(new ToolStripSeparator());
		AddShowEntriesMenuItem("All", ShowEntriesMode.All);
		AddShowEntriesMenuItem("Only Modified", ShowEntriesMode.OnlyModified);
		AddShowEntriesMenuItem("Only Unmodified", ShowEntriesMode.OnlyUnmodified);
		listEntryType.EndUpdate();
		listEntryType.Format += (s, e) =>
		{
			if (e.ListItem is AppInfoEntryType entryType)
			{
				e.Value = I18n.GetEnumDisplayName(entryType);
			}
		};
		listControllerSupport.Items.Add("none");
		listControllerSupport.Items.Add("partial");
		listControllerSupport.Items.Add("full");
		listVR.Items.Add("default");
		listVR.Items.Add("removed");
		base.Icon = Resources.logo;
		_defaultNameColor = textGameName.ForeColor;
		_defaultSortAsColor = textSortAs.ForeColor;
		_defaultAppSelectionFont = appSelection.Font;
		_boldAppSelectionFont = new Font(appSelection.Font, FontStyle.Bold);
		_hiddenAppSelectionFont = new Font(appSelection.Font, FontStyle.Strikeout);
		_originalTitle = Text;
		_originalNumShownLabelText = lblNumAppsShown.Text;
		SteamData.OnNeedsSave += SteamData_OnNeedsSaveOrApply;
		SteamData.OnNeedsApply += SteamData_OnNeedsSaveOrApply;
		void AddShowEntriesMenuItem(string name, ShowEntriesMode mode)
		{
			ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem(name)
			{
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				CheckOnClick = true,
				Tag = mode,
				Checked = (Settings.Default.FiltersShowEntries == mode)
			};
			toolStripMenuItem2.Click += OnFilter_ShowEntriesModeChanged;
			filterSelectorMenu.Items.Add(toolStripMenuItem2);
		}
	}

	private void OnGeneric_KeyDown(object sender, KeyEventArgs e)
	{
		Keys keyCode = e.KeyCode;
		if ((uint)(keyCode - 33) <= 1u)
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			if (sender is ComboBox)
			{
				(sender as ComboBox).DroppedDown = false;
			}
		}
	}

	private void SteamData_OnNeedsSaveOrApply(object sender, EventArgs e)
	{
		Text = _originalTitle;
		if (SteamData.NeedsSave)
		{
			Text += Strings.MainWindow_UnsavedSuffix;
		}
		if (SteamData.NeedsApply)
		{
			Text += Strings.MainWindow_UnappliedSuffix;
		}
	}

	private void SortEntries()
	{
		IAppInfoEntry selectedItem = appSelection.SelectedItem as IAppInfoEntry;
		var keys = new Dictionary<IAppInfoEntry, string>(_entries.Count);
		foreach (var e in _entries) keys[e] = StringTitleComparer.Normalize(e.SortAs);
		_entries.Sort(delegate(IAppInfoEntry a, IAppInfoEntry b)
		{
			int num = StringComparer.CurrentCulture.Compare(keys[a], keys[b]);
			if (num == 0)
			{
				num = a.ID - b.ID;
			}
			return num;
		});
		appSelection.BeginUpdate();
		appSelection.Items.Clear();
		ListBox.ObjectCollection items = appSelection.Items;
		object[] items2 = _entries.ToArray();
		items.AddRange(items2);
		appSelection.EndUpdate();
		appSelection.SelectedItem = selectedItem;
		if (appSelection.SelectedItem == null)
		{
			appSelection.Text = "";
		}
	}

	private void ResortEntry(IAppInfoEntry entry)
	{
		IAppInfoEntry selectedItem = appSelection.SelectedItem as IAppInfoEntry;
		int i = _entries.IndexOf(entry);
		int index = i;
		while (i > 0 && StringTitleComparer.CompareStrings(entry.SortAs, _entries[i - 1].SortAs) < 0)
		{
			_entries[i] = _entries[i - 1];
			_entries[i - 1] = entry;
			i--;
		}
		for (; i < _entries.Count - 1 && StringTitleComparer.CompareStrings(entry.SortAs, _entries[i + 1].SortAs) > 0; i++)
		{
			_entries[i] = _entries[i + 1];
			_entries[i + 1] = entry;
		}
		appSelection.BeginUpdate();
		appSelection.Items.RemoveAt(index);
		appSelection.Items.Insert(i, entry);
		appSelection.EndUpdate();
		appSelection.SelectedItem = selectedItem;
	}

	private bool IsOnScreen()
	{
		Rectangle rect = new Rectangle(base.Left, base.Top, base.Width, base.Height);
		Screen[] allScreens = Screen.AllScreens;
		for (int i = 0; i < allScreens.Length; i++)
		{
			if (allScreens[i].WorkingArea.IntersectsWith(rect))
			{
				return true;
			}
		}
		return false;
	}

	private void MainWindow_Load(object sender, EventArgs e)
	{
		Point location = base.Location;
		Point location2 = base.Location;
		if (Settings.Default.WindowLocationX != int.MaxValue)
		{
			location2.X = Settings.Default.WindowLocationX;
		}
		if (Settings.Default.WindowLocationY != int.MaxValue)
		{
			location2.Y = Settings.Default.WindowLocationY;
		}
		base.Location = location2;
		if (!IsOnScreen())
		{
			base.Location = location;
		}
		Size size = base.Size;
		Size size2 = base.Size;
		if (Settings.Default.WindowSizeX != int.MaxValue)
		{
			size2.Width = Settings.Default.WindowSizeX;
		}
		if (Settings.Default.WindowSizeY != int.MaxValue)
		{
			size2.Height = Settings.Default.WindowSizeY;
		}
		base.Size = size2;
		if (!IsOnScreen())
		{
			base.Size = size;
		}
		if (Settings.Default.MainWindowSplitter != int.MaxValue)
		{
			splitContainer1.SplitterDistance = Settings.Default.MainWindowSplitter;
		}
		SteamData_OnNeedsSaveOrApply(null, null);
		RefreshGameList();
		ReleaseNotesView releaseNotesView = new ReleaseNotesView();
		if (releaseNotesView.FillNotes(Settings.Default.ShownVersion))
		{
			releaseNotesView.Show(this);
			Settings.Default.ShownVersion = releaseNotesView.ShownVersion;
			Settings.Default.Save();
		}
		appSelection.Select();
		I18n.LanguageChanged += OnLanguageChanged;
		ApplyLanguage();
	}

	private bool IsFuzzyMatch(string testString, string pattern)
	{
		testString = testString.ToLower();
		pattern = pattern.ToLower();
		string[] array = pattern.Split(' ', '\t', '\u00a0');
		foreach (string patt in array)
		{
			if (!IsMatch(testString, patt))
			{
				return false;
			}
		}
		return true;
		static bool IsMatch(string test, string value)
		{
			if (test.Contains(value))
			{
				return true;
			}
			return false;
		}
	}

	private void RefreshGameList()
	{
		object selectedItem = appSelection.SelectedItem;
		var source = SteamData.AppInfoData.Entries.Where((IAppInfoEntry ent) => ent.Name != null).ToList();
		IEnumerable<IAppInfoEntry> source2 = source.Where((IAppInfoEntry ent) => (ent.Type & _currentFilters) != 0);
		if (Settings.Default.FiltersShowEntries == ShowEntriesMode.OnlyModified)
		{
			source2 = source2.Where((IAppInfoEntry ent) => ent.IsModified);
		}
		else if (Settings.Default.FiltersShowEntries == ShowEntriesMode.OnlyUnmodified)
		{
			source2 = source2.Where((IAppInfoEntry ent) => !ent.IsModified);
		}
		string fuzzyPattern = appFilterText.Text;
		source2 = source2.Where((IAppInfoEntry ent) => IsFuzzyMatch(ent.Name, fuzzyPattern));
		_entries = new List<IAppInfoEntry>(source2);
		btnControllerBatch.Enabled = _entries.Count > 0;
		SortEntries();
		if (selectedItem != null && appSelection.Items.Contains(selectedItem))
		{
			appSelection.SelectedItem = selectedItem;
		}
		else
		{
			appSelection.SelectedIndex = ((appSelection.Items.Count <= 0) ? (-1) : 0);
		}
		Setup();
		_totalEntryCount = source.Count();
		lblNumAppsShown.Text = string.Format(_originalNumShownLabelText, _entries.Count, _totalEntryCount);
	}

	private void OnAppSelection_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (!_refreshingGamesListAfterEdit)
		{
			Setup();
		}
	}

	private void Setup()
	{
		bool flag = appSelection.SelectedItem != null;
		textGameName.Enabled = flag;
		textSortAs.Enabled = flag;
		checkHidden.Enabled = flag;
		checkCompleted.Enabled = flag;
		btnEditLaunch.Enabled = flag;
		btnRevert.Enabled = flag;
		btnTranslate.Enabled = flag && !string.IsNullOrWhiteSpace(Settings.Default.AiApiKey);
		btnBatchTranslate.Enabled = !string.IsNullOrWhiteSpace(Settings.Default.AiApiKey);
		listControllerSupport.Enabled = flag;
		listVR.Enabled = flag;
		btnVRBatch.Enabled = flag;
		listEntryType.Enabled = flag;
		if (!flag)
		{
			return;
		}
		_isSettingUpEntry = true;
		IAppInfoEntry appInfoEntry = (IAppInfoEntry)appSelection.SelectedItem;
		if (appInfoEntry != null)
		{
			textGameName.Text = appInfoEntry.Name;
			textGameName.ClearXVisible = appInfoEntry.IsModified && appInfoEntry.Modification.Changes.HasProperty("appinfo", "common", "name");
			textSortAs.Text = appInfoEntry.SortAs;
			textSortAs.ForeColor = (appInfoEntry.HasSortAs ? _defaultSortAsColor : Color.Gray);
			textSortAs.ClearXVisible = appInfoEntry.IsModified && appInfoEntry.Modification.Changes.HasProperty("appinfo", "common", "sortas");
			checkHidden.Checked = appInfoEntry.IsHidden;
			checkCompleted.Checked = appInfoEntry.IsCompleted;
			string csRaw = appInfoEntry.GetPropertyValue("none", "appinfo", "common", "controller_support");
			int csIdx = Array.IndexOf(ControllerRawValues, csRaw);
			listControllerSupport.SelectedIndex = csIdx >= 0 ? csIdx : 0;
			listEntryType.SelectedItem = appInfoEntry.Type;
			if (appInfoEntry.GetPropertyValue(int.MaxValue, "appinfo", "common", "openvrsupport") == int.MaxValue)
			{
				listVR.Enabled = false;
				listVR.SelectedItem = null;
			}
			else
			{
				listVR.SelectedIndex = ((appInfoEntry.GetPropertyValue(int.MaxValue, "appinfo", "steam_edit", "base_openvrsupport") != int.MaxValue) ? 1 : 0);
			}
		}
		_isSettingUpEntry = false;
	}

	private void OnTextGameName_TextChanged(object sender, EventArgs e)
	{
		if (_isSettingUpEntry)
		{
			return;
		}
		IAppInfoEntry appInfoEntry = appSelection.SelectedItem as IAppInfoEntry;
		if (appInfoEntry == null) return;
		if (textGameName.Text != appInfoEntry.Name)
		{
			string previousName = appInfoEntry.Name;
			appInfoEntry.Name = textGameName.Text;
			UpdateGlossaryForEntry(appInfoEntry, previousName);
			if (!appInfoEntry.HasSortAs)
			{
				_refreshingGamesListAfterEdit = true;
				ResortEntry(appInfoEntry);
				_refreshingGamesListAfterEdit = false;
				bool isSettingUpEntry = _isSettingUpEntry;
				_isSettingUpEntry = true;
				textSortAs.Text = appInfoEntry.SortAs;
				_isSettingUpEntry = isSettingUpEntry;
			}
			Rectangle itemRectangle = appSelection.GetItemRectangle(appSelection.SelectedIndex);
			appSelection.Invalidate(itemRectangle);
		}
	}

	private void OnTextSortAs_TextChanged(object sender, EventArgs e)
	{
		if (!_isSettingUpEntry)
		{
			IAppInfoEntry appInfoEntry = appSelection.SelectedItem as IAppInfoEntry;
			if (appInfoEntry == null) return;
			if (textSortAs.Text != appInfoEntry.SortAs)
			{
				appInfoEntry.SortAs = textSortAs.Text;
				_refreshingGamesListAfterEdit = true;
				ResortEntry(appInfoEntry);
				_refreshingGamesListAfterEdit = false;
				appSelection.SelectedItem = appInfoEntry;
				textSortAs.ForeColor = (appInfoEntry.HasSortAs ? _defaultSortAsColor : Color.Gray);
			}
		}
	}

	private void OnCheckHidden_CheckedChanged(object sender, EventArgs e)
	{
		if (!_isSettingUpEntry)
		{
			if (appSelection.SelectedItem is not IAppInfoEntry entry) return;
			entry.IsHidden = checkHidden.Checked;
			appSelection.Invalidate();
		}
	}

	private void OnAppSelection_DrawItem(object sender, DrawItemEventArgs e)
	{
		e.DrawBackground();
		if (e.Index >= 0)
		{
			IAppInfoEntry appInfoEntry = (IAppInfoEntry)appSelection.Items[e.Index];
			string text = appInfoEntry.ToString();
			text = text.Replace("&", "&&");
			Color foreColor = appSelection.ForeColor;
			Font font = _defaultAppSelectionFont;
			Rectangle bounds = e.Bounds;
			int num = e.Bounds.Height - 6;
			if ((_currentFilters & (_currentFilters - 1)) != 0)
			{
				e.Graphics.DrawImage(_filterImages[appInfoEntry.Type], e.Bounds.X + 3, e.Bounds.Y + 3, e.Bounds.Height - 6, e.Bounds.Height - 6);
				bounds.X += e.Bounds.Height;
				bounds.Width -= e.Bounds.Height;
			}
			int num2 = bounds.Width;
			if (appInfoEntry.IsModified)
			{
				e.Graphics.DrawImage(Resources.icon_modified, bounds.X + num2 - (num + 6), e.Bounds.Y + 3, num, num);
				bounds.Width = num2 - (num + 6);
				font = _boldAppSelectionFont;
			}
			if (appInfoEntry.IsCompleted)
			{
				e.Graphics.DrawImage(Resources.icon_completed, bounds.X + num2 - (num + 6) * 2, e.Bounds.Y + 3, num, num);
				bounds.Width = num2 - (num + 6) * 2;
			}
			if (appInfoEntry.IsHidden)
			{
				e.Graphics.DrawImage(Resources.icon_hidden, bounds.X + num2 - (num + 6) * 3, e.Bounds.Y + 3, num, num);
				bounds.Width = num2 - (num + 6) * 3;
				foreColor = Color.FromArgb(255 - (255 - foreColor.R) / 2, 255 - (255 - foreColor.G) / 2, 255 - (255 - foreColor.B) / 2);
				font = _hiddenAppSelectionFont;
			}
			Size size = TextRenderer.MeasureText(text, font);
			bounds.Inflate(0, -((bounds.Height - size.Height) / 2));
			TextRenderer.DrawText(e.Graphics, text, font, bounds, foreColor, TextFormatFlags.EndEllipsis);
		}
	}

	private void OnMainWindow_FormClosing(object sender, FormClosingEventArgs e)
	{
		I18n.LanguageChanged -= OnLanguageChanged;
		SteamData.OnNeedsSave -= SteamData_OnNeedsSaveOrApply;
		SteamData.OnNeedsApply -= SteamData_OnNeedsSaveOrApply;
		try { Settings.Default.Save(); } catch (Exception ex) { Log.WriteLine("Settings save failed: {0}", ex.Message); }
		if (SteamData.NeedsSave)
		{
			switch (MessageBox.Show(Strings.Msg_UnsavedChanges, Strings.Msg_SaveFirst, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation))
			{
			case DialogResult.Yes:
				SteamData.Save();
				break;
			case DialogResult.Cancel:
				e.Cancel = true;
				break;
			}
		}
	}

	private void OnLanguageChanged(object sender, EventArgs e)
	{
		if (InvokeRequired)
		{
			BeginInvoke(new Action(ApplyLanguage));
			return;
		}
		ApplyLanguage();
	}

	private void ApplyLanguage()
	{
		_originalTitle = Strings.MainWindow_Title;
		Text = _originalTitle;
		if (SteamData.NeedsSave)
			Text += Strings.MainWindow_UnsavedSuffix;
		if (SteamData.NeedsApply)
			Text += Strings.MainWindow_UnappliedSuffix;

		_originalNumShownLabelText = Strings.MainWindow_StatusShown;

		checkHidden.Text = Strings.MainWindow_CheckHidden;
		checkCompleted.Text = Strings.MainWindow_CheckCompleted;

		_isSettingUpEntry = true;

		// Refresh controller combo
		int csIdx = listControllerSupport.SelectedIndex;
		listControllerSupport.Items.Clear();
		listControllerSupport.Items.Add(Strings.MainWindow_Controller_None);
		listControllerSupport.Items.Add(Strings.MainWindow_Controller_Partial);
		listControllerSupport.Items.Add(Strings.MainWindow_Controller_Full);
		if (csIdx >= 0 && csIdx < listControllerSupport.Items.Count)
			listControllerSupport.SelectedIndex = csIdx;

		// Refresh VR combo
		int vrIdx = listVR.SelectedIndex;
		listVR.Items.Clear();
		listVR.Items.Add(Strings.MainWindow_VR_Default);
		listVR.Items.Add(Strings.MainWindow_VR_Removed);
		if (vrIdx >= 0 && vrIdx < listVR.Items.Count)
			listVR.SelectedIndex = vrIdx;

		_isSettingUpEntry = false;

		// Refresh filter menu items
		foreach (object item in filterSelectorMenu.Items)
		{
			if (item is ToolStripMenuItem menuItem)
			{
				if (menuItem.Tag is ShowEntriesMode mode)
				{
					switch (mode)
					{
						case ShowEntriesMode.All:
							menuItem.Text = Strings.MainWindow_Filter_All;
							break;
						case ShowEntriesMode.OnlyModified:
							menuItem.Text = Strings.MainWindow_Filter_OnlyModified;
							break;
						case ShowEntriesMode.OnlyUnmodified:
							menuItem.Text = Strings.MainWindow_Filter_OnlyUnmodified;
							break;
					}
				}
				else if (menuItem.Tag is AppInfoEntryType entryType)
				{
					menuItem.Text = I18n.GetEnumDisplayName(entryType);
				}
			}
		}

		// Refresh entry type combo display (items are enum values, Format event handles translation)
		listEntryType.Refresh();

		// Promoted label/button text
		lblName.Text = Strings.MainWindow_LabelName;
		lblSortAs.Text = Strings.MainWindow_LabelSortAs;
		lblControllerSupport.Text = Strings.MainWindow_LabelControllerSupport;
		lblVRCategory.Text = Strings.MainWindow_LabelVRCategory;
		btnSave.Text = Strings.MainWindow_BtnSave;
		btnSaveApply.Text = Strings.MainWindow_BtnSaveApply;
		btnSaveApplyRestart.Text = Strings.MainWindow_BtnSaveApplyRestart;
		btnTranslate.Text = Strings.MainWindow_BtnTranslate;
		btnSearch.Text = Strings.MainWindow_BtnSearch;
		toolTips.SetToolTip(btnTranslate, Strings.Tooltip_Translate);
		btnBatchTranslate.Text = Strings.MainWindow_BatchTranslate;
		toolTips.SetToolTip(btnBatchTranslate, Strings.Tooltip_BatchTranslate);
		toolTips.SetToolTip(btnSearch, Strings.Tooltip_Search);
		toolTips.SetToolTip(btnSave, Strings.Tooltip_Save);
		toolTips.SetToolTip(btnSaveApply, Strings.Tooltip_SaveApply);
		toolTips.SetToolTip(btnSaveApplyRestart, Strings.Tooltip_SaveApplyRestart);
		toolTips.SetToolTip(btnSettings, Strings.Tooltip_Settings);
		toolTips.SetToolTip(btnHelp, Strings.Tooltip_About);
		toolTips.SetToolTip(btnControllerBatch, Strings.Tooltip_BatchController);
		toolTips.SetToolTip(btnVRBatch, Strings.Tooltip_BatchVR);

		// Refresh status bar
		lblNumAppsShown.Text = string.Format(_originalNumShownLabelText, _entries?.Count ?? 0, _totalEntryCount);

		Setup();
	}

	private void OnBtnEditLaunch_Click(object sender, EventArgs e)
	{
		IAppInfoEntry appInfoEntry = (IAppInfoEntry)appSelection.SelectedItem;
		using LaunchEditor launchEditor = new LaunchEditor(appInfoEntry.Launches.Items, appInfoEntry.InstallPath, appInfoEntry.Name);
		if (launchEditor.ShowDialog(this) == DialogResult.OK)
		{
			appInfoEntry.Launches = new LaunchTable(launchEditor.LaunchItems);
		}
	}

	private void OnBtnSettings_Click(object sender, EventArgs e)
	{
		using (var dlg = new SettingsDlg()) dlg.ShowDialog(this);
		SteamData.UseCompleted = Settings.Default.EnableCompleted;
		SteamData.UseHidden = Settings.Default.EnableHidden;
	}

	private void OnCheckCompleted_CheckedChanged(object sender, EventArgs e)
	{
		if (!_isSettingUpEntry)
		{
			if (appSelection.SelectedItem is not IAppInfoEntry entry) return;
			entry.IsCompleted = checkCompleted.Checked;
			appSelection.Invalidate();
		}
	}

	private void OnMainWindow_KeyDown(object sender, KeyEventArgs e)
	{
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

	private void appSelection_KeyPress(object sender, KeyPressEventArgs e)
	{
		e.Handled = true;
		appSearchTerm.Append(e.KeyChar);
		appSearchTermTimer.Stop();
		appSearchTermTimer.Start();
		string text = appSearchTerm.ToString();
		int num = appSelection.SelectedIndex;
		if (text.Length == 1)
		{
			num++;
		}
		for (int i = 0; i < appSelection.Items.Count; i++)
		{
			int num2 = (num + i) % appSelection.Items.Count;
			if (appSelection.Items[num2].ToString().StartsWith(text, StringComparison.CurrentCultureIgnoreCase))
			{
				appSelection.SelectedIndex = num2;
				break;
			}
		}
	}

	private void OnBtnRevert_Click(object sender, EventArgs e)
	{
		IAppInfoEntry appInfoEntry = (IAppInfoEntry)appSelection.SelectedItem;
		try
		{
			SteamData.AppInfoData.RevertEntry(appInfoEntry);
			ResortEntry(appInfoEntry);
			appSelection.SelectedItem = appInfoEntry;
			Setup();
		}
		catch (NotImplementedException)
		{
			if (MessageBox.Show(this, string.Format(Strings.Msg_FailedRevert, appInfoEntry.ToString()), Strings.Msg_FailedRevertTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
			{
				SteamData.AppInfoData.RemoveEntry(appInfoEntry);
				RefreshGameList();
			}
		}
	}

	private void UpdateGlossaryForEntry(IAppInfoEntry entry, string previousName)
	{
		try
		{
			// Get original (unmodified) name from OriginalData or base_name
			string originalName = null;
			if (entry.Modification?.OriginalData != null)
			{
				using var reader = new BinaryReader(new MemoryStream(entry.Modification.OriginalData));
				var props = PropertyTableIO.ReadPropertyTable_Mods(reader);
				originalName = props.GetPropertyValue<string>(null, "appinfo", "common", "name");
			}
			if (string.IsNullOrWhiteSpace(originalName))
				originalName = entry.GetPropertyValue<string>(null, "appinfo", "steam_edit", "base_name");
			if (string.IsNullOrWhiteSpace(originalName))
				originalName = previousName;

			TranslationGlossary.UpdateEntry(entry.ID, originalName, entry.Name);
		}
		catch (Exception ex)
		{
			Log.WriteLine("Glossary update failed for {0:X8}: {1}", entry.ID, ex.Message);
		}
	}

	private void OnBtnSearch_Click(object sender, EventArgs e)
	{
		if (appSelection.SelectedItem is not IAppInfoEntry entry) return;
		string gameName = entry.Name;
		if (string.IsNullOrWhiteSpace(gameName)) return;

		string lang = Settings.Default.AiTargetLanguage switch
		{
			"zh-CN" => "中文名",
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
		Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
	}

	private async void OnBtnTranslate_Click(object sender, EventArgs e)
	{
		if (appSelection.SelectedItem is not IAppInfoEntry entry) return;
		string originalName = entry.Name;
		if (string.IsNullOrWhiteSpace(originalName)) return;

		string targetLang = Settings.Default.AiTargetLanguage;
		btnTranslate.Enabled = false;
		btnTranslate.Text = Strings.Msg_Translating;

		try
		{
			Log.WriteLine("AI Translate: Starting for \"{0}\"", originalName);
			string context = AiTranslator.BuildContext(entry);
			string translated = await AiTranslator.TranslateWithContextAsync(
				originalName, targetLang, null, context,
				Settings.Default.AiCustomSystemPrompt,
				Settings.Default.AiCustomUserMessage);
			Log.WriteLine("AI Translate: Got result \"{0}\"", translated);

			if (!string.IsNullOrWhiteSpace(translated) && IsHandleCreated && !IsDisposed)
			{
				BeginInvoke(() =>
				{
					if (appSelection.SelectedItem is not IAppInfoEntry currentEntry) return;
					string previousName = currentEntry.Name;
					_isSettingUpEntry = true;
					try
					{
						textGameName.Text = translated;
						currentEntry.Name = translated;
						UpdateGlossaryForEntry(currentEntry, previousName);
						if (!currentEntry.HasSortAs)
						{
							_refreshingGamesListAfterEdit = true;
							ResortEntry(currentEntry);
							_refreshingGamesListAfterEdit = false;
							textSortAs.Text = currentEntry.SortAs;
						}
						appSelection.Invalidate(appSelection.GetItemRectangle(appSelection.SelectedIndex));
					}
					finally { _isSettingUpEntry = false; }
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

	private void OnBtnBatchTranslate_Click(object sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(Settings.Default.AiApiUrl) ||
			string.IsNullOrWhiteSpace(Settings.Default.AiApiKey))
		{
			MessageBox.Show(this, Strings.Msg_AiNotConfigured, Strings.Msg_CriticalError,
				MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		// Get current filtered entries
		var entriesToTranslate = new List<IAppInfoEntry>();
		foreach (IAppInfoEntry entry in _entries)
		{
			// Skip entries where name has been modified
			if (entry.IsModified && entry.Modification?.Changes != null &&
				entry.Modification.Changes.HasProperty("appinfo", "common", "name"))
			{
				continue;
			}
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

		// Refresh the list to reflect changes
		appSelection.Invalidate();
		Setup();
	}

	private void OnBtnSave_Click(object sender, EventArgs e)
	{
		try
		{
			SteamData.Save();
		}
		catch (Exception ex)
		{
			MessageBox.Show(string.Format(Strings.Msg_SaveFailed, ex.ToString()));
		}
	}

	private void OnBtnSaveApply_Click(object sender, EventArgs e)
	{
		try
		{
			SteamData.Save();
		}
		catch (Exception ex)
		{
			MessageBox.Show(string.Format(Strings.Msg_SaveFailed, ex.ToString()));
			return;
		}
		try
		{
			SteamData.Apply();
		}
		catch (Exception ex2)
		{
			MessageBox.Show(string.Format(Strings.Msg_ApplyFailed, ex2.ToString()));
		}
	}

	private void OnBtnSaveApplyRestart_Click(object sender, EventArgs e)
	{
		try
		{
			SteamData.SaveAndRestartSteam(restartAlways: true);
		}
		catch (Exception ex)
		{
			MessageBox.Show(string.Format(Strings.Msg_SaveFailed, ex.ToString()));
		}
	}

	private void OnListControllerSupport_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (!_isSettingUpEntry)
		{
			if (appSelection.SelectedItem is not IAppInfoEntry entry) return;
			int idx = listControllerSupport.SelectedIndex;
			string rawValue = (idx >= 0 && idx < ControllerRawValues.Length) ? ControllerRawValues[idx] : "none";
			entry.SetPropertyValue(PropertyType.String, rawValue, "appinfo", "common", "controller_support");
		}
	}

	private void OnBtnControllerBatch_Click(object sender, EventArgs e)
	{
		using var controllerBatch = new ControllerBatch();
		if (controllerBatch.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		string text = controllerBatch.From;
		string to = controllerBatch.To;
		IAppInfoEntry appInfoEntry = (IAppInfoEntry)appSelection.SelectedItem;
		bool flag = false;
		foreach (IAppInfoEntry entry in _entries)
		{
			if (entry.GetPropertyValue("none", "appinfo", "common", "controller_support") == text)
			{
				entry.SetPropertyValue(PropertyType.String, to, "appinfo", "common", "controller_support");
				if (appInfoEntry == entry)
				{
					flag = true;
				}
			}
		}
		if (flag)
		{
			Setup();
		}
	}

	private void SetEntryVR(IAppInfoEntry entry, bool remove)
	{
		if (remove)
		{
			int propertyValue = entry.GetPropertyValue(int.MaxValue, "appinfo", "common", "openvrsupport");
			if (propertyValue != int.MaxValue)
			{
				entry.SetPropertyValue(PropertyType.Int32, propertyValue, "appinfo", "steam_edit", "base_openvrsupport");
				entry.SetPropertyValue(PropertyType.Int32, 0, "appinfo", "common", "openvrsupport");
			}
		}
		else
		{
			int propertyValue2 = entry.GetPropertyValue(int.MaxValue, "appinfo", "steam_edit", "base_openvrsupport");
			if (propertyValue2 != int.MaxValue)
			{
				entry.SetPropertyValue(PropertyType.Int32, propertyValue2, "appinfo", "common", "openvrsupport");
				entry.SetPropertyValue(PropertyType.Int32, int.MaxValue, "appinfo", "steam_edit", "base_openvrsupport");
			}
		}
	}

	private void OnListVR_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (!_isSettingUpEntry)
		{
			if (appSelection.SelectedItem is not IAppInfoEntry entry) return;
			SetEntryVR(entry, listVR.SelectedIndex != 0);
		}
	}

	private void OnBtnVRBatch_Click(object sender, EventArgs e)
	{
		using var vRBatch = new VRBatch();
		if (vRBatch.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		VRBatch.ValueTag value = vRBatch.Value;
		foreach (IAppInfoEntry entry in _entries)
		{
			SetEntryVR(entry, value == VRBatch.ValueTag.Remove);
		}
		Setup();
	}

	private void OnFilterSelectorMenu_Closing(object sender, ToolStripDropDownClosingEventArgs e)
	{
		if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
		{
			e.Cancel = true;
		}
	}

	private void OnFilter_CheckedChanged(object sender, EventArgs e)
	{
		ToolStripMenuItem obj = sender as ToolStripMenuItem;
		AppInfoEntryType appInfoEntryType = (AppInfoEntryType)obj.Tag;
		if (obj.Checked)
		{
			_currentFilters |= appInfoEntryType;
		}
		else
		{
			_currentFilters &= ~appInfoEntryType;
		}
		Settings.Default.Filters = _currentFilters;
		Settings.Default.Save();
		RefreshGameList();
	}

	private void OnFilter_ShowEntriesModeChanged(object sender, EventArgs e)
	{
		ShowEntriesMode showEntriesMode = (ShowEntriesMode)(sender as ToolStripMenuItem).Tag;
		foreach (object item in filterSelectorMenu.Items)
		{
			if (item is ToolStripMenuItem toolStripMenuItem && toolStripMenuItem.Tag is ShowEntriesMode)
			{
				ShowEntriesMode showEntriesMode2 = (ShowEntriesMode)toolStripMenuItem.Tag;
				toolStripMenuItem.Checked = showEntriesMode2 == showEntriesMode;
			}
		}
		Settings.Default.FiltersShowEntries = showEntriesMode;
		Settings.Default.Save();
		RefreshGameList();
	}

	private void OnFilterSelectorMenu_Click(object sender, EventArgs e)
	{
		if (!filterSelectorMenu.Visible)
		{
			Point screenLocation = filterSelector.PointToScreen(new Point(filterSelector.Size.Width, 0));
			filterSelectorMenu.Show(screenLocation);
		}
	}

	private void OnAppFilterText_TextChanged(object sender, EventArgs e)
	{
		_filterDebounceTimer.Stop();
		_filterDebounceTimer.Start();
	}

	private void OnAppSelection_SizeChanged(object sender, EventArgs e)
	{
		appSelection.Invalidate();
	}

	private void OnHelp_Click(object sender, EventArgs e)
	{
		using (var dlg = new About()) dlg.ShowDialog(this);
	}

	private void appSelection_MouseMove(object sender, MouseEventArgs e)
	{
		Point p = appSelection.PointToClient(Control.MousePosition);
		int num = appSelection.IndexFromPoint(p);
		if (num >= 0 && num < appSelection.Items.Count)
		{
			if (appSelection_LastHoverI != num)
			{
				IAppInfoEntry entry = (IAppInfoEntry)appSelection.Items[num];
				appSelection_LastHoverI = num;
				toolTips.Active = false;
				toolTips.SetToolTip(appSelection, GetEntryToolTip(entry));
				toolTips.Active = true;
			}
		}
		else
		{
			toolTips.SetToolTip(appSelection, null);
			appSelection_LastHoverI = -1;
		}
	}

	private void appSelection_MouseLeave(object sender, EventArgs e)
	{
		appSelection_LastHoverI = -1;
	}

	private string GetEntryToolTip(IAppInfoEntry entry)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendFormat("{0}: {1}", entry.ID, entry.Name);
		string propertyValue = entry.GetPropertyValue<string>(null, new string[3] { "appinfo", "common", "oslist" });
		if (propertyValue != null)
		{
			stringBuilder.AppendFormat(" [{0}]", propertyValue);
		}
		return stringBuilder.ToString();
	}

	private void panel1_Paint(object sender, PaintEventArgs e)
	{
		float dpiX = e.Graphics.DpiX;
		float num = dpiX / 72f;
		Image logo = Resources.logo1;
		Size size = new Size((int)(150f * num), (int)(150f * num));
		Point location = new Point(panel1.ClientSize - size);
		Rectangle destRect = new Rectangle(location, size);
		e.Graphics.DrawImage(Resources.logo1, destRect, 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, _bgLogoAttributes);
	}

	private void MainWindow_Move(object sender, EventArgs e)
	{
		if (base.WindowState != FormWindowState.Minimized)
		{
			Settings.Default.WindowLocationX = base.Location.X;
			Settings.Default.WindowLocationY = base.Location.Y;
		}
	}

	private void MainWindow_Resize(object sender, EventArgs e)
	{
		if (base.WindowState != FormWindowState.Minimized)
		{
			Settings.Default.WindowSizeX = base.Size.Width;
			Settings.Default.WindowSizeY = base.Size.Height;
			panel1.Invalidate();
		}
	}

	private void listEntryType_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (!_isSettingUpEntry)
		{
			if (appSelection.SelectedItem is not IAppInfoEntry entry) return;
			entry.Type = (AppInfoEntryType)listEntryType.SelectedItem;
			RefreshGameList();
		}
	}

	private void textGameName_ClearXClicked(object sender, HandledEventArgs e)
	{
		e.Handled = true;
		if (appSelection.SelectedItem is not IAppInfoEntry entry) return;
		entry.RevertModifiedProperty("appinfo", "common", "name");
		_refreshingGamesListAfterEdit = true;
		RefreshGameList();
		_refreshingGamesListAfterEdit = false;
		Setup();
	}

	private void textSortAs_ClearXClicked(object sender, HandledEventArgs e)
	{
		e.Handled = true;
		if (appSelection.SelectedItem is not IAppInfoEntry appInfoEntry) return;
		appInfoEntry.RevertModifiedProperty("appinfo", "common", "sortas");
		_refreshingGamesListAfterEdit = true;
		ResortEntry(appInfoEntry);
		_refreshingGamesListAfterEdit = false;
		Setup();
	}

	private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
	{
		Settings.Default.MainWindowSplitter = splitContainer1.SplitterDistance;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (components != null)
			{
				components.Dispose();
			}
			if (_boldAppSelectionFont != null)
			{
				_boldAppSelectionFont.Dispose();
			}
			if (_hiddenAppSelectionFont != null)
			{
				_hiddenAppSelectionFont.Dispose();
			}
		}
		base.Dispose(disposing);
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
		this.checkCompleted = new System.Windows.Forms.CheckBox();
		this.checkHidden = new System.Windows.Forms.CheckBox();
		this.toolTips = new System.Windows.Forms.ToolTip(this.components);
		this.btnControllerBatch = new System.Windows.Forms.Button();
		this.btnVRBatch = new System.Windows.Forms.Button();
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
		this.btnEditLaunch.Size = new System.Drawing.Size(75, 34);
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
		// btnBatchTranslate
		this.btnBatchTranslate.Name = "btnBatchTranslate";
		this.btnBatchTranslate.Size = new System.Drawing.Size(130, 34);
		this.btnBatchTranslate.TabIndex = 3;
		this.btnBatchTranslate.Text = "Batch Translate";
		this.toolTips.SetToolTip(this.btnBatchTranslate, "Batch translate all filtered games");
		this.btnBatchTranslate.UseVisualStyleBackColor = true;
		this.btnBatchTranslate.Click += new System.EventHandler(OnBtnBatchTranslate_Click);
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
		this.btnSaveApplyRestart.UseVisualStyleBackColor = true;
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
		this.lblVRCategory.AutoSize = true;
		this.lblVRCategory.BackColor = System.Drawing.Color.Transparent;
		this.lblVRCategory.Location = new System.Drawing.Point(79, 126);
		this.lblVRCategory.Name = "label4";
		this.lblVRCategory.Size = new System.Drawing.Size(70, 13);
		this.lblVRCategory.TabIndex = 24;
		this.lblVRCategory.Text = "VR Category:";
		statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.lblNumAppsShown });
		statusStrip.Location = new System.Drawing.Point(0, 306);
		statusStrip.Name = "statusStrip1";
		statusStrip.Size = new System.Drawing.Size(227, 22);
		statusStrip.SizingGrip = false;
		statusStrip.TabIndex = 3;
		statusStrip.Text = "statusStrip1";
		this.lblNumAppsShown.Name = "lblNumAppsShown";
		this.lblNumAppsShown.Size = new System.Drawing.Size(90, 17);
		this.lblNumAppsShown.Text = "Shown: ({0}/{1})";
		this.checkCompleted.AutoSize = true;
		this.checkCompleted.BackColor = System.Drawing.Color.Transparent;
		this.checkCompleted.Location = new System.Drawing.Point(129, 59);
		this.checkCompleted.Name = "checkCompleted";
		this.checkCompleted.Size = new System.Drawing.Size(76, 17);
		this.checkCompleted.TabIndex = 3;
		this.checkCompleted.Text = "Completed";
		this.checkCompleted.UseVisualStyleBackColor = false;
		this.checkCompleted.CheckedChanged += new System.EventHandler(OnCheckCompleted_CheckedChanged);
		this.checkHidden.AutoSize = true;
		this.checkHidden.BackColor = System.Drawing.Color.Transparent;
		this.checkHidden.Location = new System.Drawing.Point(58, 59);
		this.checkHidden.Name = "checkHidden";
		this.checkHidden.Size = new System.Drawing.Size(60, 17);
		this.checkHidden.TabIndex = 2;
		this.checkHidden.Text = "Hidden";
		this.checkHidden.UseVisualStyleBackColor = false;
		this.checkHidden.CheckedChanged += new System.EventHandler(OnCheckHidden_CheckedChanged);
		this.btnControllerBatch.Location = new System.Drawing.Point(282, 87);
		this.btnControllerBatch.Name = "btnControllerBatch";
		this.btnControllerBatch.Size = new System.Drawing.Size(29, 23);
		this.btnControllerBatch.TabIndex = 6;
		this.btnControllerBatch.Text = "...";
		this.toolTips.SetToolTip(this.btnControllerBatch, "Batch controller support");
		this.btnControllerBatch.UseVisualStyleBackColor = true;
		this.btnControllerBatch.Click += new System.EventHandler(OnBtnControllerBatch_Click);
		this.btnVRBatch.Location = new System.Drawing.Point(232, 121);
		this.btnVRBatch.Name = "btnVRBatch";
		this.btnVRBatch.Size = new System.Drawing.Size(29, 23);
		this.btnVRBatch.TabIndex = 8;
		this.btnVRBatch.Text = "...";
		this.toolTips.SetToolTip(this.btnVRBatch, "Batch VR");
		this.btnVRBatch.UseVisualStyleBackColor = true;
		this.btnVRBatch.Click += new System.EventHandler(OnBtnVRBatch_Click);
		this.listControllerSupport.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.listControllerSupport.FormattingEnabled = true;
		this.listControllerSupport.Location = new System.Drawing.Point(155, 89);
		this.listControllerSupport.Name = "listControllerSupport";
		this.listControllerSupport.Size = new System.Drawing.Size(121, 21);
		this.listControllerSupport.TabIndex = 5;
		this.listControllerSupport.SelectedIndexChanged += new System.EventHandler(OnListControllerSupport_SelectedIndexChanged);
		this.listControllerSupport.KeyDown += new System.Windows.Forms.KeyEventHandler(OnGeneric_KeyDown);
		this.listVR.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.listVR.FormattingEnabled = true;
		this.listVR.Location = new System.Drawing.Point(155, 123);
		this.listVR.Name = "listVR";
		this.listVR.Size = new System.Drawing.Size(71, 21);
		this.listVR.TabIndex = 7;
		this.listVR.SelectedIndexChanged += new System.EventHandler(OnListVR_SelectedIndexChanged);
		this.listVR.KeyDown += new System.Windows.Forms.KeyEventHandler(OnGeneric_KeyDown);
		this.filterSelectorMenu.Name = "filterSelectorMenu";
		this.filterSelectorMenu.ShowCheckMargin = true;
		this.filterSelectorMenu.Size = new System.Drawing.Size(83, 4);
		this.filterSelectorMenu.Closing += new System.Windows.Forms.ToolStripDropDownClosingEventHandler(OnFilterSelectorMenu_Closing);
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
		this.splitContainer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.splitContainer1.Location = new System.Drawing.Point(0, 43);
		this.splitContainer1.Name = "splitContainer1";
		this.splitContainer1.Panel1.Controls.Add(statusStrip);
		this.splitContainer1.Panel1.Controls.Add(this.appSelection);
		this.splitContainer1.Panel1.Controls.Add(this.appFilterText);
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
		this.appSelection.DrawItem += new System.Windows.Forms.DrawItemEventHandler(OnAppSelection_DrawItem);
		this.appSelection.SelectedIndexChanged += new System.EventHandler(OnAppSelection_SelectedIndexChanged);
		this.appSelection.SizeChanged += new System.EventHandler(OnAppSelection_SizeChanged);
		this.appSelection.KeyPress += new System.Windows.Forms.KeyPressEventHandler(appSelection_KeyPress);
		this.appSelection.MouseLeave += new System.EventHandler(appSelection_MouseLeave);
		this.appSelection.MouseMove += new System.Windows.Forms.MouseEventHandler(appSelection_MouseMove);
		this.appFilterText.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.appFilterText.AutoClearX = true;
		this.appFilterText.ClearXVisible = false;
		this.appFilterText.Location = new System.Drawing.Point(3, 3);
		this.appFilterText.Name = "appFilterText";
		this.appFilterText.Size = new System.Drawing.Size(195, 20);
		this.appFilterText.TabIndex = 1;
		this.appFilterText.TextChanged += new System.EventHandler(OnAppFilterText_TextChanged);
		this.panel1.AutoScroll = true;
		this.panel1.Controls.Add(this.listEntryType);
		this.panel1.Controls.Add(this.textGameName);
		this.panel1.Controls.Add(flowLayoutPanel);
		this.panel1.Controls.Add(this.checkCompleted);
		this.panel1.Controls.Add(this.lblName);
		this.panel1.Controls.Add(this.btnVRBatch);
		this.panel1.Controls.Add(this.lblControllerSupport);
		this.panel1.Controls.Add(this.checkHidden);
		this.panel1.Controls.Add(this.textSortAs);
		this.panel1.Controls.Add(this.listVR);
		this.panel1.Controls.Add(this.listControllerSupport);
		this.panel1.Controls.Add(this.lblVRCategory);
		this.panel1.Controls.Add(this.lblSortAs);
		this.panel1.Controls.Add(this.btnControllerBatch);
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
		this.listEntryType.SelectedIndexChanged += new System.EventHandler(listEntryType_SelectedIndexChanged);
		this.textGameName.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.textGameName.AutoClearX = false;
		this.textGameName.ClearXVisible = true;
		this.textGameName.Location = new System.Drawing.Point(58, 7);
		this.textGameName.Name = "textGameName";
		this.textGameName.Size = new System.Drawing.Size(386, 20);
		this.textGameName.TabIndex = 0;
		this.textGameName.ClearXClicked += new System.ComponentModel.HandledEventHandler(textGameName_ClearXClicked);
		this.textGameName.TextChanged += new System.EventHandler(OnTextGameName_TextChanged);
		this.textSortAs.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.textSortAs.AutoClearX = false;
		this.textSortAs.ClearXVisible = true;
		this.textSortAs.Location = new System.Drawing.Point(58, 33);
		this.textSortAs.Name = "textSortAs";
		this.textSortAs.Size = new System.Drawing.Size(386, 20);
		this.textSortAs.TabIndex = 1;
		this.textSortAs.ClearXClicked += new System.ComponentModel.HandledEventHandler(textSortAs_ClearXClicked);
		this.textSortAs.TextChanged += new System.EventHandler(OnTextSortAs_TextChanged);
		this.flowLayoutPanel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.flowLayoutPanel3.AutoSize = true;
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
		base.MaximizeBox = false;
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
