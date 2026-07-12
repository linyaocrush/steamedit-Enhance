#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Common;
using SteamEdit.Data;
using SteamEdit.ViewModels;

namespace SteamEdit;

internal class EditController : IDisposable
{
	private readonly GameDataContext _ctx;
	private readonly IAppInfoDataStore _dataStore;
	private readonly GameListManager _listManager;
	private readonly PropertyEditorViewModel _viewModel;

	private readonly ClearableTextBox _textGameName;
	private readonly ClearableTextBox _textSortAs;
	private readonly CheckBox _checkHidden;
	private readonly CheckBox _checkCompleted;
	private readonly ComboBox _listControllerSupport;
	private readonly ComboBox _listVR;
	private readonly ComboBox _listEntryType;

	private Color _defaultSortAsColor;

	public EditController(
		GameDataContext ctx,
		IAppInfoDataStore dataStore,
		GameListManager listManager,
		PropertyEditorViewModel viewModel,
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
		_viewModel = viewModel;
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

		_viewModel.EditCommandExecuted += OnViewModelEditExecuted;

		// 订阅事件
		_textGameName.TextChanged += OnTextGameName_TextChanged;
		_textGameName.ClearXClicked += OnTextGameName_ClearXClicked;
		_textSortAs.TextChanged += OnTextSortAs_TextChanged;
		_textSortAs.ClearXClicked += OnTextSortAs_ClearXClicked;
		_checkHidden.CheckedChanged += OnCheckHidden_CheckedChanged;
		_checkCompleted.CheckedChanged += OnCheckCompleted_CheckedChanged;
		_listControllerSupport.SelectedIndexChanged += OnListControllerSupport_SelectedIndexChanged;
		_listControllerSupport.KeyDown += OnGeneric_KeyDown;
		_listVR.SelectedIndexChanged += OnListVR_SelectedIndexChanged;
		_listVR.KeyDown += OnGeneric_KeyDown;
		_listEntryType.SelectedIndexChanged += OnListEntryType_SelectedIndexChanged;
	}

	public void Dispose()
	{
		_viewModel.EditCommandExecuted -= OnViewModelEditExecuted;
		_textGameName.TextChanged -= OnTextGameName_TextChanged;
		_textGameName.ClearXClicked -= OnTextGameName_ClearXClicked;
		_textSortAs.TextChanged -= OnTextSortAs_TextChanged;
		_textSortAs.ClearXClicked -= OnTextSortAs_ClearXClicked;
		_checkHidden.CheckedChanged -= OnCheckHidden_CheckedChanged;
		_checkCompleted.CheckedChanged -= OnCheckCompleted_CheckedChanged;
		_listControllerSupport.SelectedIndexChanged -= OnListControllerSupport_SelectedIndexChanged;
		_listControllerSupport.KeyDown -= OnGeneric_KeyDown;
		_listVR.SelectedIndexChanged -= OnListVR_SelectedIndexChanged;
		_listVR.KeyDown -= OnGeneric_KeyDown;
		_listEntryType.SelectedIndexChanged -= OnListEntryType_SelectedIndexChanged;
	}

	public PropertyEditorViewModel ViewModel => _viewModel;

	public void LoadEntry(IAppInfoEntry? entry)
	{
		bool hasEntry = entry != null;
		_textGameName.Enabled = hasEntry;
		_textSortAs.Enabled = hasEntry;
		_checkHidden.Enabled = hasEntry;
		_checkCompleted.Enabled = hasEntry;
		_listControllerSupport.Enabled = hasEntry;
		_listVR.Enabled = hasEntry;
		_listEntryType.Enabled = hasEntry;
		if (entry == null) return;

		_viewModel.LoadEntry(entry);
		SyncControlsFromViewModel();
	}

	private void SyncControlsFromViewModel()
	{
		_textGameName.Text = _viewModel.GameName;
		_textGameName.ClearXVisible = _viewModel.CurrentEntry!.IsModified
			&& _viewModel.CurrentEntry.Modification?.Changes?.HasProperty(AppInfoPaths.Name) == true;
		_textSortAs.Text = _viewModel.SortAs;
		_textSortAs.ForeColor = _viewModel.CurrentEntry!.HasSortAs ? _defaultSortAsColor : Color.Gray;
		_textSortAs.ClearXVisible = _viewModel.CurrentEntry.IsModified
			&& _viewModel.CurrentEntry.Modification?.Changes?.HasProperty(AppInfoPaths.SortAs) == true;
		_checkHidden.Checked = _viewModel.IsHidden;
		_checkCompleted.Checked = _viewModel.IsCompleted;

		int csIdx = _viewModel.ControllerSupportIndex;
		_listControllerSupport.SelectedIndex = csIdx >= 0 && csIdx < _listControllerSupport.Items.Count ? csIdx : 0;
		_listEntryType.SelectedItem = _viewModel.EntryType;

		if (!_viewModel.VREnabled)
		{
			_listVR.Enabled = false;
			_listVR.SelectedItem = null;
		}
		else
		{
			_listVR.SelectedIndex = _viewModel.VRIndex;
		}
	}

	public void PerformUndo()
	{
		var undoManager = _viewModel.UndoManager;
		if (!undoManager.CanUndo) return;
		var record = undoManager.Pop();

		// 复合记录：递归处理所有子记录
		if (record.IsCompound)
		{
			foreach (var sub in record.SubRecords!)
				ApplyUndoRecord(sub);
		}
		else
		{
			ApplyUndoRecord(record);
		}
	}

	private void ApplyUndoRecord(UndoRecord record)
	{
		if (_listManager.GetSelectedEntry() != record.Entry)
		{
			int idx = _ctx._entries.IndexOf(record.Entry);
			if (idx >= 0) _listManager.SelectIndex(idx);
		}

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

		LoadEntry(record.Entry);
		_listManager.InvalidateItem(record.Entry);
	}

	public void RevertEntry(IAppInfoEntry? entry)
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
		_viewModel.ApplyTranslation(translated, previousName);
		SyncControlsFromViewModel();
		_listManager.NotifyEntryEdited(entry);
		UpdateGlossaryForEntry(entry, previousName);
		if (!entry.HasSortAs)
		{
			_textSortAs.Text = entry.SortAs;
		}
	}

	public void ApplyLanguage()
	{
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

		_listEntryType.Refresh();
	}

	public void UpdateGlossaryForEntry(IAppInfoEntry entry, string previousName)
	{
		try
		{
			string? originalName = entry.Modification?.GetOriginalName();
			if (string.IsNullOrWhiteSpace(originalName))
				originalName = entry.GetPropertyValue<string>(null!, AppInfoPaths.BaseName);
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

	// --- 事件处理 ---

	private bool CanEdit()
	{
		return !_viewModel.IsSettingUp && _viewModel.CurrentEntry != null;
	}

	private void OnGeneric_KeyDown(object? sender, KeyEventArgs e)
	{
		if ((uint)(e.KeyCode - 33) <= 1u)
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			if (sender is ComboBox cb) cb.DroppedDown = false;
		}
	}

	private void OnViewModelEditExecuted(object? sender, IAppInfoEntry entry)
	{
		SyncControlsFromViewModel();
		_listManager.NotifyEntryEdited(entry);
	}

	private void OnTextGameName_TextChanged(object? sender, EventArgs e)
	{
		if (!CanEdit() || _textGameName.Text == _viewModel.CurrentEntry!.Name) return;
		string prev = _viewModel.CurrentEntry.Name;
		_viewModel.GameName = _textGameName.Text;
		UpdateGlossaryForEntry(_viewModel.CurrentEntry!, prev);
	}

	private void OnTextSortAs_TextChanged(object? sender, EventArgs e)
	{
		if (!CanEdit() || _textSortAs.Text == _viewModel.CurrentEntry!.SortAs) return;
		_viewModel.SortAs = _textSortAs.Text;
		_textSortAs.ForeColor = _viewModel.CurrentEntry!.HasSortAs ? _defaultSortAsColor : Color.Gray;
	}

	private void OnCheckHidden_CheckedChanged(object? sender, EventArgs e)
	{
		if (!CanEdit()) return;
		_viewModel.IsHidden = _checkHidden.Checked;
	}

	private void OnCheckCompleted_CheckedChanged(object? sender, EventArgs e)
	{
		if (!CanEdit()) return;
		_viewModel.IsCompleted = _checkCompleted.Checked;
	}

	private void OnListControllerSupport_SelectedIndexChanged(object? sender, EventArgs e)
	{
		if (!CanEdit()) return;
		_viewModel.ControllerSupportIndex = _listControllerSupport.SelectedIndex;
	}

	private void OnListVR_SelectedIndexChanged(object? sender, EventArgs e)
	{
		if (!CanEdit()) return;
		int vrIdx = _listVR.SelectedIndex;
		if (_viewModel.VREnabled)
			_viewModel.VRIndex = vrIdx;
	}

	private void OnListEntryType_SelectedIndexChanged(object? sender, EventArgs e)
	{
		if (!CanEdit()) return;
		_viewModel.EntryType = (AppInfoEntryType)_listEntryType.SelectedItem!;
	}

	private void OnTextGameName_ClearXClicked(object? sender, HandledEventArgs e)
	{
		e.Handled = true;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		entry.RevertModifiedProperty(AppInfoPaths.Name);
		_listManager.NotifyEntryEdited(entry);
		_listManager.RefreshGameList();
		LoadEntry(entry);
	}

	private void OnTextSortAs_ClearXClicked(object? sender, HandledEventArgs e)
	{
		e.Handled = true;
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		entry.RevertModifiedProperty(AppInfoPaths.SortAs);
		_listManager.NotifyEntryEdited(entry);
		LoadEntry(entry);
	}
}
