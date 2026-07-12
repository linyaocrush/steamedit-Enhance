#nullable enable
using System;
using System.Collections.Generic;

namespace SteamEdit.Data;

internal class UndoRecord
{
    public IAppInfoEntry Entry { get; }
    public string[] PropertyPath { get; }
    public PropertyType Type { get; }
    public object? PreviousValue { get; }
    public UndoRecord[]? SubRecords { get; }

    public UndoRecord(IAppInfoEntry entry, string[] propertyPath, PropertyType type, object? previousValue)
    {
        Entry = entry;
        PropertyPath = propertyPath;
        Type = type;
        PreviousValue = previousValue;
    }

    /// <summary>
    /// 创建复合撤销记录，将多个属性变更作为一个原子操作撤销。
    /// 例如 VR 修改同时涉及 OpenVrSupport 和 BaseOpenVrSupport 两个属性。
    /// </summary>
    private UndoRecord(UndoRecord[] subRecords)
    {
        if (subRecords == null || subRecords.Length == 0)
            throw new ArgumentException("SubRecords must not be empty.", nameof(subRecords));
        Entry = subRecords[0].Entry;
        PropertyPath = subRecords[0].PropertyPath;
        Type = subRecords[0].Type;
        PreviousValue = subRecords[0].PreviousValue;
        SubRecords = subRecords;
    }

    public bool IsCompound => SubRecords != null;

    public static UndoRecord CreateCompound(params UndoRecord[] subRecords)
        => new UndoRecord(subRecords);
}

internal class UndoManager
{
    public const int DefaultMaxUndoLevel = 500;

    private readonly List<UndoRecord> _undoStack = new();
    private readonly int _maxUndoLevel;
    private readonly object _lock = new();

    public int MaxUndoLevel => _maxUndoLevel;
    public bool CanUndo { get { lock (_lock) { return _undoStack.Count > 0; } } }
    public int Count { get { lock (_lock) { return _undoStack.Count; } } }

    public UndoManager(int maxUndoLevel = DefaultMaxUndoLevel)
    {
        _maxUndoLevel = Math.Max(maxUndoLevel, 1);
    }

    public void Push(IAppInfoEntry entry, string[] propertyPath, PropertyType type, object? previousValue)
    {
        lock (_lock)
        {
            _undoStack.Add(new UndoRecord(entry, propertyPath, type, previousValue));
            TrimExcess();
        }
    }

    public void PushCompound(UndoRecord compoundRecord)
    {
        if (compoundRecord == null) throw new ArgumentNullException(nameof(compoundRecord));
        lock (_lock)
        {
            _undoStack.Add(compoundRecord);
            TrimExcess();
        }
    }

    public UndoRecord Pop()
    {
        lock (_lock)
        {
            if (_undoStack.Count == 0)
                throw new InvalidOperationException("Undo stack is empty.");
            int last = _undoStack.Count - 1;
            var item = _undoStack[last];
            _undoStack.RemoveAt(last);
            return item;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _undoStack.Clear();
        }
    }

    private void TrimExcess()
    {
        int overflow = _undoStack.Count - _maxUndoLevel;
        if (overflow > 0)
            _undoStack.RemoveRange(0, overflow);
    }
}
