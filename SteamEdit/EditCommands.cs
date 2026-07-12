#nullable enable
using Common;
using SteamEdit.Data;

namespace SteamEdit;

internal interface IEditCommand
{
	void Execute(IAppInfoEntry entry);
}

internal sealed class NameChangeCommand : IEditCommand
{
	private readonly UndoManager _undoManager;
	private readonly GameListManager _listManager;
	private readonly string _newName;

	public NameChangeCommand(UndoManager undoManager, GameListManager listManager, string newName)
	{
		_undoManager = undoManager;
		_listManager = listManager;
		_newName = newName;
	}

	public void Execute(IAppInfoEntry entry)
	{
		string prev = entry.Name;
		_undoManager.Push(entry, AppInfoPaths.Name, PropertyType.String, prev);
		entry.Name = _newName;
		_listManager.NotifyEntryEdited(entry);
	}
}

internal sealed class SortAsChangeCommand : IEditCommand
{
	private readonly UndoManager _undoManager;
	private readonly GameListManager _listManager;
	private readonly string _newSortAs;

	public SortAsChangeCommand(UndoManager undoManager, GameListManager listManager, string newSortAs)
	{
		_undoManager = undoManager;
		_listManager = listManager;
		_newSortAs = newSortAs;
	}

	public void Execute(IAppInfoEntry entry)
	{
		_undoManager.Push(entry, AppInfoPaths.SortAs, PropertyType.String, entry.SortAs);
		entry.SortAs = _newSortAs;
		_listManager.NotifyEntryEdited(entry);
	}
}

internal sealed class HiddenToggleCommand : IEditCommand
{
	private readonly UndoManager _undoManager;
	private readonly GameListManager _listManager;
	private readonly bool _hidden;

	public HiddenToggleCommand(UndoManager undoManager, GameListManager listManager, bool hidden)
	{
		_undoManager = undoManager;
		_listManager = listManager;
		_hidden = hidden;
	}

	public void Execute(IAppInfoEntry entry)
	{
		int prev = entry.GetPropertyValue(0, AppInfoPaths.IsHidden);
		_undoManager.Push(entry, AppInfoPaths.IsHidden, PropertyType.Int32, prev);
		entry.IsHidden = _hidden;
		_listManager.NotifyStatusChanged();
	}
}

internal sealed class CompletedToggleCommand : IEditCommand
{
	private readonly UndoManager _undoManager;
	private readonly GameListManager _listManager;
	private readonly bool _completed;

	public CompletedToggleCommand(UndoManager undoManager, GameListManager listManager, bool completed)
	{
		_undoManager = undoManager;
		_listManager = listManager;
		_completed = completed;
	}

	public void Execute(IAppInfoEntry entry)
	{
		int prev = entry.GetPropertyValue(0, AppInfoPaths.IsCompleted);
		_undoManager.Push(entry, AppInfoPaths.IsCompleted, PropertyType.Int32, prev);
		entry.IsCompleted = _completed;
		_listManager.NotifyStatusChanged();
	}
}

internal sealed class ControllerChangeCommand : IEditCommand
{
	private static readonly string[] RawValues = { "none", "partial", "full" };

	private readonly UndoManager _undoManager;
	private readonly int _selectedIndex;

	public ControllerChangeCommand(UndoManager undoManager, int selectedIndex)
	{
		_undoManager = undoManager;
		_selectedIndex = selectedIndex;
	}

	public void Execute(IAppInfoEntry entry)
	{
		string raw = _selectedIndex >= 0 && _selectedIndex < RawValues.Length ? RawValues[_selectedIndex] : "none";
		string prev = entry.GetPropertyValue<string>(null!, AppInfoPaths.ControllerSupport);
		_undoManager.Push(entry, AppInfoPaths.ControllerSupport, PropertyType.String, prev);
		entry.SetPropertyValue(PropertyType.String, raw, AppInfoPaths.ControllerSupport);
	}
}

internal sealed class VrChangeCommand : IEditCommand
{
	private readonly UndoManager _undoManager;
	private readonly bool _remove;

	public VrChangeCommand(UndoManager undoManager, bool remove)
	{
		_undoManager = undoManager;
		_remove = remove;
	}

	public void Execute(IAppInfoEntry entry)
	{
		int prevVr = entry.GetPropertyValue(int.MaxValue, AppInfoPaths.OpenVrSupport);
		int prevVrBase = entry.GetPropertyValue(int.MaxValue, AppInfoPaths.BaseOpenVrSupport);
		var vrRecord = new UndoRecord(entry, AppInfoPaths.OpenVrSupport, PropertyType.Int32, prevVr);
		if (prevVrBase != int.MaxValue)
		{
			var baseRecord = new UndoRecord(entry, AppInfoPaths.BaseOpenVrSupport, PropertyType.Int32, prevVrBase);
			_undoManager.PushCompound(UndoRecord.CreateCompound(vrRecord, baseRecord));
		}
		else
		{
			_undoManager.PushCompound(vrRecord);
		}

		if (_remove)
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
}

internal sealed class EntryTypeChangeCommand : IEditCommand
{
	private readonly UndoManager _undoManager;
	private readonly GameListManager _listManager;
	private readonly AppInfoEntryType _newType;

	public EntryTypeChangeCommand(UndoManager undoManager, GameListManager listManager, AppInfoEntryType newType)
	{
		_undoManager = undoManager;
		_listManager = listManager;
		_newType = newType;
	}

	public void Execute(IAppInfoEntry entry)
	{
		string prevType = entry.GetPropertyValue<string>(null!, AppInfoPaths.Type);
		_undoManager.Push(entry, AppInfoPaths.Type, PropertyType.String, prevType);
		entry.Type = _newType;
		_listManager.RefreshGameList();
	}
}
