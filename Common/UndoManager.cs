#nullable enable
using System.Collections.Generic;
using SteamEdit.Data;

namespace Common;

internal class UndoRecord
{
    public IAppInfoEntry Entry { get; }
    public string[] PropertyPath { get; }
    public PropertyType Type { get; }
    public object? PreviousValue { get; }

    public UndoRecord(IAppInfoEntry entry, string[] propertyPath, PropertyType type, object? previousValue)
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
    public int Count => _undoStack.Count;

    public void Push(IAppInfoEntry entry, string[] propertyPath, PropertyType type, object? previousValue)
    {
        _undoStack.Push(new UndoRecord(entry, propertyPath, type, previousValue));
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
