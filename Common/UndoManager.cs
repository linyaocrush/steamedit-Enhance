using System.Collections.Generic;
using SteamEdit.Data;

namespace Common;

internal class UndoRecord
{
    public IAppInfoEntry Entry { get; }
    public string[] PropertyPath { get; }
    public PropertyType Type { get; }
    public object PreviousValue { get; }

    public UndoRecord(IAppInfoEntry entry, string[] propertyPath, PropertyType type, object previousValue)
    {
        Entry = entry;
        PropertyPath = propertyPath;
        Type = type;
        PreviousValue = previousValue;
    }
}

internal class UndoManager
{
    private readonly Stack<UndoRecord> _undoStack = new();

    public bool CanUndo => _undoStack.Count > 0;

    public void Push(IAppInfoEntry entry, string[] propertyPath, PropertyType type, object previousValue)
    {
        _undoStack.Push(new UndoRecord(entry, propertyPath, type, previousValue));
    }

    public UndoRecord Pop()
    {
        return _undoStack.Pop();
    }

    public void Undo()
    {
        if (!CanUndo) return;

        var record = _undoStack.Pop();

        // RevertModifiedProperty 会：1) 从 Changes 中移除该属性的修改
        // 2) 从原始数据备份中恢复该属性的值 3) 如果 Changes 为空则完全 Revert 条目
        record.Entry.RevertModifiedProperty(record.PropertyPath);
    }
}
