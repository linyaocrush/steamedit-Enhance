#nullable enable
using System;
using Common;
using steamedit_Enhance.Data;

namespace steamedit_Enhance.ViewModels;

internal sealed class PropertyEditorViewModel : ViewModelBase
{
    public static readonly string[] ControllerRawValues = { "none", "partial", "full" };

    private readonly UndoManager _undoManager = new();
    private IAppInfoEntry? _currentEntry;
    private bool _isSettingUp;
    private string _gameName = "";
    private string _sortAs = "";
    private bool _isHidden;
    private bool _isCompleted;
    private int _controllerSupportIndex;
    private int _vrIndex = -1;
    private AppInfoEntryType _entryType;
    private bool _vrEnabled;
    private bool _gameNameClearXVisible;
    private bool _sortAsClearXVisible;
    private bool _hasSortAs;

    public IAppInfoEntry? CurrentEntry
    {
        get => _currentEntry;
        private set => SetProperty(ref _currentEntry, value);
    }

    public bool IsSettingUp
    {
        get => _isSettingUp;
        set => SetProperty(ref _isSettingUp, value);
    }

    public UndoManager UndoManager => _undoManager;

    public string GameName
    {
        get => _gameName;
        set
        {
            if (SetProperty(ref _gameName, value) && !_isSettingUp)
                OnGameNameChanged(value);
        }
    }

    public string SortAs
    {
        get => _sortAs;
        set
        {
            if (SetProperty(ref _sortAs, value) && !_isSettingUp)
                OnSortAsChanged(value);
        }
    }

    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            if (SetProperty(ref _isHidden, value) && !_isSettingUp)
                OnIsHiddenChanged(value);
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (SetProperty(ref _isCompleted, value) && !_isSettingUp)
                OnIsCompletedChanged(value);
        }
    }

    public int ControllerSupportIndex
    {
        get => _controllerSupportIndex;
        set
        {
            if (SetProperty(ref _controllerSupportIndex, value) && !_isSettingUp)
                OnControllerSupportChanged(value);
        }
    }

    public int VRIndex
    {
        get => _vrIndex;
        set
        {
            if (SetProperty(ref _vrIndex, value) && !_isSettingUp)
                OnVRChanged(value);
        }
    }

    public bool VREnabled
    {
        get => _vrEnabled;
        set => SetProperty(ref _vrEnabled, value);
    }

    public AppInfoEntryType EntryType
    {
        get => _entryType;
        set
        {
            if (SetProperty(ref _entryType, value) && !_isSettingUp)
                OnEntryTypeChanged(value);
        }
    }

    public bool GameNameClearXVisible
    {
        get => _gameNameClearXVisible;
        set => SetProperty(ref _gameNameClearXVisible, value);
    }

    public bool SortAsClearXVisible
    {
        get => _sortAsClearXVisible;
        set => SetProperty(ref _sortAsClearXVisible, value);
    }

    public bool HasSortAs
    {
        get => _hasSortAs;
        set => SetProperty(ref _hasSortAs, value);
    }

    public event EventHandler<IAppInfoEntry>? EditCommandExecuted;

    public void LoadEntry(IAppInfoEntry? entry)
    {
        CurrentEntry = entry;
        if (entry == null) return;

        _isSettingUp = true;

        GameName = entry.Name ?? "";
        SortAs = entry.SortAs;
        IsHidden = entry.IsHidden;
        IsCompleted = entry.IsCompleted;

        string csRaw = entry.GetPropertyValue("none", AppInfoPaths.ControllerSupport);
        int csIdx = Array.IndexOf(ControllerRawValues, csRaw);
        ControllerSupportIndex = csIdx >= 0 ? csIdx : 0;

        EntryType = entry.Type;

        if (entry.GetPropertyValue(int.MaxValue, AppInfoPaths.OpenVrSupport) == int.MaxValue)
        {
            VREnabled = false;
            VRIndex = -1;
        }
        else
        {
            VREnabled = true;
            VRIndex = entry.GetPropertyValue(int.MaxValue, AppInfoPaths.BaseOpenVrSupport) != int.MaxValue ? 1 : 0;
        }

        HasSortAs = entry.HasSortAs;
        GameNameClearXVisible = entry.IsModified
            && entry.Modification?.Changes?.HasProperty(AppInfoPaths.Name) == true;
        SortAsClearXVisible = entry.IsModified
            && entry.Modification?.Changes?.HasProperty(AppInfoPaths.SortAs) == true;

        _isSettingUp = false;
    }

    public void ApplyTranslation(string translated, string previousName)
    {
        if (_currentEntry == null) return;
        _isSettingUp = true;
        try
        {
            GameName = translated;
            _currentEntry.Name = translated;
            HasSortAs = _currentEntry.HasSortAs;
            EditCommandExecuted?.Invoke(this, _currentEntry);
        }
        finally { _isSettingUp = false; }
    }

    private void OnGameNameChanged(string value)
    {
        var entry = _currentEntry;
        if (entry == null) return;
        string prev = entry.Name;
        _undoManager.Push(entry, AppInfoPaths.Name, PropertyType.String, prev);
        entry.Name = value;
        EditCommandExecuted?.Invoke(this, entry);
    }

    private void OnSortAsChanged(string value)
    {
        var entry = _currentEntry;
        if (entry == null) return;
        _undoManager.Push(entry, AppInfoPaths.SortAs, PropertyType.String, entry.SortAs);
        entry.SortAs = value;
        EditCommandExecuted?.Invoke(this, entry);
    }

    private void OnIsHiddenChanged(bool value)
    {
        var entry = _currentEntry;
        if (entry == null) return;
        int prev = entry.GetPropertyValue(0, AppInfoPaths.IsHidden);
        _undoManager.Push(entry, AppInfoPaths.IsHidden, PropertyType.Int32, prev);
        entry.IsHidden = value;
        EditCommandExecuted?.Invoke(this, entry);
    }

    private void OnIsCompletedChanged(bool value)
    {
        var entry = _currentEntry;
        if (entry == null) return;
        int prev = entry.GetPropertyValue(0, AppInfoPaths.IsCompleted);
        _undoManager.Push(entry, AppInfoPaths.IsCompleted, PropertyType.Int32, prev);
        entry.IsCompleted = value;
        EditCommandExecuted?.Invoke(this, entry);
    }

    private void OnControllerSupportChanged(int index)
    {
        var entry = _currentEntry;
        if (entry == null) return;
        string raw = index >= 0 && index < ControllerRawValues.Length ? ControllerRawValues[index] : "none";
        string prev = entry.GetPropertyValue<string>(null!, AppInfoPaths.ControllerSupport);
        _undoManager.Push(entry, AppInfoPaths.ControllerSupport, PropertyType.String, prev);
        entry.SetPropertyValue(PropertyType.String, raw, AppInfoPaths.ControllerSupport);
        EditCommandExecuted?.Invoke(this, entry);
    }

    private void OnVRChanged(int index)
    {
        var entry = _currentEntry;
        if (entry == null) return;
        bool remove = index != 0;
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
        EditCommandExecuted?.Invoke(this, entry);
    }

    private void OnEntryTypeChanged(AppInfoEntryType value)
    {
        var entry = _currentEntry;
        if (entry == null) return;
        string prevType = entry.GetPropertyValue<string>(null!, AppInfoPaths.Type);
        _undoManager.Push(entry, AppInfoPaths.Type, PropertyType.String, prevType);
        entry.Type = value;
        EditCommandExecuted?.Invoke(this, entry);
    }
}
