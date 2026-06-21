#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Common;
using SteamEdit.Data;

namespace SteamEdit;

internal class EditController
{
	private static readonly string[] ControllerRawValues = { "none", "partial", "full" };

	private readonly GameDataContext _ctx;
	private readonly IAppInfoDataStore _dataStore;
	private readonly GameListManager _listManager;

	private readonly ClearableTextBox _textGameName;
	private readonly ClearableTextBox _textSortAs;
	private readonly CheckBox _checkHidden;
	private readonly CheckBox _checkCompleted;
	private readonly ComboBox _listControllerSupport;
	private readonly ComboBox _listVR;
	private readonly ComboBox _listEntryType;

	private readonly UndoManager _undoManager = new();
	private readonly Dictionary<string, object> _snapshotValues = new();

	private bool _isSettingUpEntry;
	private Color _defaultSortAsColor;

	public EditController(
		GameDataContext ctx,
		IAppInfoDataStore dataStore,
		GameListManager listManager,
		ClearableTextBox textGameName,
		ClearableTextBox textSortAs,
		CheckBox checkHidden,
		CheckBox checkCompleted,
		ComboBox listControllerSupport,
		ComboBox listVR,
		ComboBox listEntryType)
	{
		_ctx = ctx;
		_dataStore = dataStore;
		_listManager = listManager;
		_textGameName = textGameName;
		_textSortAs = textSortAs;
		_checkHidden = checkHidden;
		_checkCompleted = checkCompleted;
		_listControllerSupport = listControllerSupport;
		_listVR = listVR;
		_listEntryType = listEntryType;

		_defaultSortAsColor = _textSortAs.ForeColor;

		// 构建下拉框
		_listControllerSupport.Items.Add("none");
		_listControllerSupport.Items.Add("partial");
		_listControllerSupport.Items.Add("full");
		_listVR.Items.Add("default");
		_listVR.Items.Add("removed");

		// 订阅事件
		_textGameName.TextChanged += OnTextGameName_TextChanged;
		_textGameName.Enter += OnEditControl_Enter;
		_textGameName.ClearXClicked += OnTextGameName_ClearXClicked;
		_textSortAs.TextChanged += OnTextSortAs_TextChanged;
		_textSortAs.Enter += OnEditControl_Enter;
		_textSortAs.ClearXClicked += OnTextSortAs_ClearXClicked;
		_checkHidden.CheckedChanged += OnCheckHidden_CheckedChanged;
		_checkHidden.Enter += OnEditControl_Enter;
		_checkCompleted.CheckedChanged += OnCheckCompleted_CheckedChanged;
		_checkCompleted.Enter += OnEditControl_Enter;
		_listControllerSupport.SelectedIndexChanged += OnListControllerSupport_SelectedIndexChanged;
		_listControllerSupport.KeyDown += OnGeneric_KeyDown;
		_listControllerSupport.Enter += OnEditControl_Enter;
		_listVR.SelectedIndexChanged += OnListVR_SelectedIndexChanged;
		_listVR.KeyDown += OnGeneric_KeyDown;
		_listVR.Enter += OnEditControl_Enter;
		_listEntryType.SelectedIndexChanged += OnListEntryType_SelectedIndexChanged;
		_listEntryType.Enter += OnEditControl_Enter;
	}

	public bool IsSettingUpEntry => _isSettingUpEntry;
	public UndoManager UndoManager => _undoManager;

	public void LoadEntry(IAppInfoEntry entry)
	{
		bool hasEntry = entry != null;
		_textGameName.Enabled = hasEntry;
		_textSortAs.Enabled = hasEntry;
		_checkHidden.Enabled = hasEntry;
		_checkCompleted.Enabled = hasEntry;
		_listControllerSupport.Enabled = hasEntry;
		_listVR.Enabled = hasEntry;
		_listEntryType.Enabled = hasEntry;
		if (!hasEntry) return;

		_isSettingUpEntry = true;
		_snapshotValues.Clear();

		_textGameName.Text = entry.Name;
		_textGameName.ClearXVisible = entry.IsModified && entry.Modification?.Changes?.HasProperty(AppInfoPaths.Name) == true;
		_textSortAs.Text = entry.SortAs;
		_textSortAs.ForeColor = entry.HasSortAs ? _defaultSortAsColor : Color.Gray;
		_textSortAs.ClearXVisible = entry.IsModified && entry.Modification?.Changes?.HasProperty(AppInfoPaths.SortAs) == true;
		_checkHidden.Checked = entry.IsHidden;
		_checkCompleted.Checked = entry.IsCompleted;

		string csRaw = entry.GetPropertyValue("none", AppInfoPaths.ControllerSupport);
		int csIdx = Array.IndexOf(ControllerRawValues, csRaw);
		_listControllerSupport.SelectedIndex = csIdx >= 0 ? csIdx : 0;

		_listEntryType.SelectedItem = entry.Type;

		if (entry.GetPropertyValue(int.MaxValue, AppInfoPaths.OpenVrSupport) == int.MaxValue)
		{
			_listVR.Enabled = false;
			_listVR.SelectedItem = null;
		}
		else
		{
			_listVR.SelectedIndex = entry.GetPropertyValue(int.MaxValue, AppInfoPaths.BaseOpenVrSupport) != int.MaxValue ? 1 : 0;
		}

		_isSettingUpEntry = false;
	}

	public void PerformUndo()
	{
		if (!_undoManager.CanUndo) return;
		var record = _undoManager.Pop();

		if (_listManager.GetSelectedEntry() != record.Entry)
		{
			int idx = _ctx.Entries.IndexOf(record.Entry);
			if (idx >= 0) _listManager.SelectIndex(idx);
		}

		_isSettingUpEntry = true;
		var origType = record.Entry.GetOriginalPropertyType(record.PropertyPath);
		var origValue = record.Entry.GetOriginalPropertyValue(record.PropertyPath);
		bool isOriginal = origType == record.Type && Equals(record.PreviousValue, origValue);

		if (isOriginal)
		{
			record.Entry.RevertModifiedProperty(record.PropertyPath);
			_listManager.InvalidateDisplayName(record.Entry);
		}
		else
		{
			record.Entry.SetPropertyValue(record.Type, record.PreviousValue, record.PropertyPath);
			_listManager.InvalidateDisplayName(record.Entry);
		}

		_isSettingUpEntry = false;
		LoadEntry(record.Entry);
		_listManager.InvalidateItem(record.Entry);
	}

	public void RevertEntry(IAppInfoEntry entry)
	{
		if (entry == null) return;
		if (_dataStore.AppInfo.RevertEntry(entry))
		{
			_listManager.NotifyEntryReverted(entry);
			LoadEntry(entry);
		}
		else
		{
			if (MessageBox.Show(null, string.Format(Strings.Msg_FailedRevert, entry.ToString()), Strings.Msg_FailedRevertTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
			{
				_dataStore.AppInfo.RemoveEntry(entry);
				_listManager.RefreshGameList();
			}
		}
	}

	public void ApplyTranslation(IAppInfoEntry entry, string translated, string previousName)
	{
		_isSettingUpEntry = true;
		try
		{
			_textGameName.Text = translated;
			entry.Name = translated;
			_listManager.NotifyEntryEdited(entry);
			UpdateGlossaryForEntry(entry, previousName);
			if (!entry.HasSortAs)
			{
				_textSortAs.Text = entry.SortAs;
			}
		}
		finally { _isSettingUpEntry = false; }
	}

	public void ApplyLanguage()
	{
		_isSettingUpEntry = true;
		int csIdx = _listControllerSupport.SelectedIndex;
		_listControllerSupport.Items.Clear();
		_listControllerSupport.Items.Add(Strings.MainWindow_Controller_None);
		_listControllerSupport.Items.Add(Strings.MainWindow_Controller_Partial);
		_listControllerSupport.Items.Add(Strings.MainWindow_Controller_Full);
		if (csIdx >= 0 && csIdx < _listControllerSupport.Items.Count)
			_listControllerSupport.SelectedIndex = csIdx;

		int vrIdx = _listVR.SelectedIndex;
		_listVR.Items.Clear();
		_listVR.Items.Add(Strings.MainWindow_VR_Default);
		_listVR.Items.Add(Strings.MainWindow_VR_Removed);
		if (vrIdx >= 0 && vrIdx < _listVR.Items.Count)
			_listVR.SelectedIndex = vrIdx;
		_isSettingUpEntry = false;

		_listEntryType.Refresh();
	}

	public void UpdateGlossaryForEntry(IAppInfoEntry entry, string previousName)
	{
		try
		{
			string originalName = entry.Modification?.GetOriginalName();
			if (string.IsNullOrWhiteSpace(originalName))
				originalName = entry.GetPropertyValue<string>(null, AppInfoPaths.BaseName);
			if (string.IsNullOrWhiteSpace(originalName))
				originalName = previousName;
			TranslationGlossary.UpdateEntry(entry.ID, originalName, entry.Name);
			TranslationGlossary.Flush();
		}
		catch (Exception ex)
		{
			Log.WriteLine("Glossary update failed for {0:X8}: {1}", entry.ID, ex.Message);
		}
	}

	private void SetEntryVR(IAppInfoEntry entry, bool remove)
	{
		if (remove)
		{
			int val = entry.GetPropertyValue(int.MaxValue, AppInfoPaths.OpenVrSupport);
			if (val != int.MaxValue)
			{
				entry.SetPropertyValue(PropertyType.Int32, val, AppInfoPaths.BaseOpenVrSupport);
				entry.SetPropertyValue(PropertyType.Int32, 0, AppInfoPaths.OpenVrSupport);
			}
		}
		else
		{
			int val = entry.GetPropertyValue(int.MaxValue, AppInfoPaths.BaseOpenVrSupport);
			if (val != int.MaxValue)
			{
				entry.SetPropertyValue(PropertyType.Int32, val, AppInfoPaths.OpenVrSupport);
				entry.SetPropertyValue(PropertyType.Int32, int.MaxValue, AppInfoPaths.BaseOpenVrSupport);
			}
		}
	}

	// --- 事件处理 ---

	private void OnGeneric_KeyDown(object sender, KeyEventArgs e)
	{
		if ((uint)(e.KeyCode - 33) <= 1u)
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			if (sender is ComboBox cb) cb.DroppedDown = false;
		}
	}

	private void OnEditControl_Enter(object sender, EventArgs e)
	{
		if (_isSettingUpEntry) return;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;

		string key = null;
		object value = null;
		if (sender == _textGameName) { key = "name"; value = entry.GetPropertyValue<string>(null, AppInfoPaths.Name); }
		else if (sender == _textSortAs) { key = "sortas"; value = entry.GetPropertyValue<string>(null, AppInfoPaths.SortAs); }
		else if (sender == _checkHidden) { key = "hidden"; value = entry.GetPropertyValue(0, AppInfoPaths.IsHidden); }
		else if (sender == _checkCompleted) { key = "completed"; value = entry.GetPropertyValue(0, AppInfoPaths.IsCompleted); }
		else if (sender == _listEntryType) { key = "type"; value = entry.GetPropertyValue<string>(null, AppInfoPaths.Type); }
		else if (sender == _listControllerSupport) { key = "controller"; value = entry.GetPropertyValue<string>(null, AppInfoPaths.ControllerSupport); }
		else if (sender == _listVR)
		{
			key = "vr";
			value = entry.GetPropertyValue(int.MaxValue, AppInfoPaths.OpenVrSupport);
			_snapshotValues["vr_base"] = entry.GetPropertyValue(int.MaxValue, AppInfoPaths.BaseOpenVrSupport);
		}

		if (key != null) _snapshotValues[key] = value;
	}

	private void OnTextGameName_TextChanged(object sender, EventArgs e)
	{
		if (_isSettingUpEntry) return;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		if (_textGameName.Text != entry.Name)
		{
			string prev = entry.Name;
			_undoManager.Push(entry, AppInfoPaths.Name, PropertyType.String, prev);
			entry.Name = _textGameName.Text;
			_listManager.NotifyEntryEdited(entry);
			UpdateGlossaryForEntry(entry, prev);
			if (!entry.HasSortAs)
			{
				bool wasSettingUp = _isSettingUpEntry;
				_isSettingUpEntry = true;
				_textSortAs.Text = entry.SortAs;
				_isSettingUpEntry = wasSettingUp;
			}
		}
	}

	private void OnTextSortAs_TextChanged(object sender, EventArgs e)
	{
		if (_isSettingUpEntry) return;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		if (_textSortAs.Text != entry.SortAs)
		{
			_undoManager.Push(entry, AppInfoPaths.SortAs, PropertyType.String, entry.SortAs);
			entry.SortAs = _textSortAs.Text;
			_listManager.NotifyEntryEdited(entry);
			int idx = _ctx.Entries.IndexOf(entry);
			if (idx >= 0) _listManager.SelectIndex(idx);
			_textSortAs.ForeColor = entry.HasSortAs ? _defaultSortAsColor : Color.Gray;
		}
	}

	private void OnCheckHidden_CheckedChanged(object sender, EventArgs e)
	{
		if (_isSettingUpEntry) return;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		if (_snapshotValues.TryGetValue("hidden", out var prev) && prev is int p)
			_undoManager.Push(entry, AppInfoPaths.IsHidden, PropertyType.Int32, p);
		entry.IsHidden = _checkHidden.Checked;
		_listManager.NotifyStatusChanged();
	}

	private void OnCheckCompleted_CheckedChanged(object sender, EventArgs e)
	{
		if (_isSettingUpEntry) return;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		if (_snapshotValues.TryGetValue("completed", out var prev) && prev is int p)
			_undoManager.Push(entry, AppInfoPaths.IsCompleted, PropertyType.Int32, p);
		entry.IsCompleted = _checkCompleted.Checked;
		_listManager.NotifyStatusChanged();
	}

	private void OnListControllerSupport_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (_isSettingUpEntry) return;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		int idx = _listControllerSupport.SelectedIndex;
		string raw = idx >= 0 && idx < ControllerRawValues.Length ? ControllerRawValues[idx] : "none";
		if (_snapshotValues.TryGetValue("controller", out var prev) && prev is string p)
			_undoManager.Push(entry, AppInfoPaths.ControllerSupport, PropertyType.String, p);
		entry.SetPropertyValue(PropertyType.String, raw, AppInfoPaths.ControllerSupport);
	}

	private void OnListVR_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (_isSettingUpEntry) return;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		if (_snapshotValues.TryGetValue("vr", out var prev) && prev is int p)
		{
			_undoManager.Push(entry, AppInfoPaths.OpenVrSupport, PropertyType.Int32, p);
			if (_snapshotValues.TryGetValue("vr_base", out var basePrev) && basePrev is int bp)
				_undoManager.Push(entry, AppInfoPaths.BaseOpenVrSupport, PropertyType.Int32, bp);
		}
		SetEntryVR(entry, _listVR.SelectedIndex != 0);
	}

	private void OnListEntryType_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (_isSettingUpEntry) return;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		if (_snapshotValues.TryGetValue("type", out var prev) && prev is string p)
			_undoManager.Push(entry, AppInfoPaths.Type, PropertyType.String, p);
		entry.Type = (AppInfoEntryType)_listEntryType.SelectedItem;
		_listManager.RefreshGameList();
	}

	private void OnTextGameName_ClearXClicked(object sender, HandledEventArgs e)
	{
		e.Handled = true;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		entry.RevertModifiedProperty(AppInfoPaths.Name);
		_listManager.NotifyEntryEdited(entry);
		_listManager.RefreshGameList();
		LoadEntry(entry);
	}

	private void OnTextSortAs_ClearXClicked(object sender, HandledEventArgs e)
	{
		e.Handled = true;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		entry.RevertModifiedProperty(AppInfoPaths.SortAs);
		_listManager.NotifyEntryEdited(entry);
		LoadEntry(entry);
	}
}
