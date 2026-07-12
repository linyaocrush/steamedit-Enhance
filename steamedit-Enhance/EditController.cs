#nullable enable
using System;
using Common;
using steamedit_Enhance.Data;
using steamedit_Enhance.ViewModels;

namespace steamedit_Enhance;

internal class EditController
{
	private readonly GameDataContext _ctx;
	private readonly IAppInfoDataStore _dataStore;
	private readonly GameListManager _listManager;
	private readonly PropertyEditorViewModel _viewModel;

	public EditController(
		GameDataContext ctx,
		IAppInfoDataStore dataStore,
		GameListManager listManager,
		PropertyEditorViewModel viewModel)
	{
		_ctx = ctx;
		_dataStore = dataStore;
		_listManager = listManager;
		_viewModel = viewModel;

		_viewModel.EditCommandExecuted += OnViewModelEditExecuted;
	}

	public PropertyEditorViewModel ViewModel => _viewModel;

	public void LoadEntry(IAppInfoEntry? entry)
	{
		_viewModel.LoadEntry(entry);
	}

	public void PerformUndo()
	{
		var undoManager = _viewModel.UndoManager;
		if (!undoManager.CanUndo) return;
		var record = undoManager.Pop();

		foreach (var sub in record.SubRecords)
			ApplyUndoRecord(sub);
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
			if (System.Windows.Forms.MessageBox.Show(null,
				string.Format(Strings.Msg_FailedRevert, entry.ToString()),
				Strings.Msg_FailedRevertTitle,
				System.Windows.Forms.MessageBoxButtons.OKCancel,
				System.Windows.Forms.MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.OK)
			{
				_dataStore.AppInfo.RemoveEntry(entry);
				_listManager.RefreshGameList();
			}
		}
	}

	public void ApplyTranslation(IAppInfoEntry entry, string translated, string previousName)
	{
		_viewModel.ApplyTranslation(translated, previousName);
		_listManager.NotifyEntryEdited(entry);
		UpdateGlossaryForEntry(entry, previousName);
		if (!entry.HasSortAs)
		{
			_viewModel.SortAs = entry.SortAs;
		}
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

	private void OnViewModelEditExecuted(object? sender, IAppInfoEntry entry)
	{
		_listManager.NotifyEntryEdited(entry);
	}

	public void ClearProperty(string[] propertyPath, bool refreshList = false)
	{
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		entry.RevertModifiedProperty(propertyPath);
		_listManager.NotifyEntryEdited(entry);
		if (refreshList)
			_listManager.RefreshGameList();
		LoadEntry(entry);
	}
}
