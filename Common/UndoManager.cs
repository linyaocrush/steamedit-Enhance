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

        // 用 SetPropertyValue 恢复到上一步的值（而非原始值）
        // SetPropertyValue 会更新 live 属性表、更新 Changes 增量记录、触发 Modified 事件
        record.Entry.SetPropertyValue(record.Type, record.PreviousValue, record.PropertyPath);

        // 如果 Changes 为空，说明所有修改都已撤销，清除修改标记
        if (record.Entry.Modification?.Changes.Count == 0)
        {
            record.Entry.Revert();
        }
    }
}
