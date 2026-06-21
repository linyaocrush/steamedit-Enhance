#nullable enable
using System;
using System.Collections.Generic;
using SteamEdit.Data;

namespace Common;

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
    private readonly Stack<UndoRecord> _undoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public int Count => _undoStack.Count;

    public void Push(IAppInfoEntry entry, string[] propertyPath, PropertyType type, object? previousValue)
    {
        _undoStack.Push(new UndoRecord(entry, propertyPath, type, previousValue));
    }

    public void PushCompound(UndoRecord compoundRecord)
    {
        if (compoundRecord == null) throw new ArgumentNullException(nameof(compoundRecord));
        _undoStack.Push(compoundRecord);
    }

    public UndoRecord Pop()
    {
        return _undoStack.Pop();
    }

    public void Clear()
    {
        _undoStack.Clear();
    }
}
