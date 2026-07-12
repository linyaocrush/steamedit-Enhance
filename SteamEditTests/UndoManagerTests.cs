#nullable enable
using System;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamEdit.Data;

namespace SteamEditTests;

[TestClass]
public class UndoManagerTests
{
    private class FakeEntry : IAppInfoEntry
    {
        public int ID { get; set; }
        public string Name { get; set; } = "";
        public string SortAs { get; set; } = "";
        public string NormalizedSortAs => Common.StringTitleComparer.Normalize(SortAs);
        public bool HasSortAs => false;
        public bool IsHidden { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsModified => false;
        public AppInfoEntryType Type { get; set; }
        public uint ChangeNumber => 0;
        public uint AppInfoVersion => 0;
        public ModifiedEntry Modification { get; set; }
        public LaunchTable Launches { get; set; }
        public string InstallPath => "";
        public event EventHandler Modified;
        public T GetPropertyValue<T>(T def, params string[] path) => def;
        public void SetPropertyValue(PropertyType t, object v, params string[] path) { }
        public void RevertModifiedProperty(params string[] path) { }
        public PropertyType? GetOriginalPropertyType(params string[] path) => null;
        public object GetOriginalPropertyValue(params string[] path) => null;
        public bool Revert() => false;
        public byte[] GetOriginalDataCopy() => null;
    }

    [TestMethod]
    public void CanUndo_Empty_ReturnsFalse()
    {
        var mgr = new UndoManager();
        Assert.IsFalse(mgr.CanUndo);
    }

    [TestMethod]
    public void CanUndo_AfterPush_ReturnsTrue()
    {
        var mgr = new UndoManager();
        mgr.Push(new FakeEntry { ID = 1 }, AppInfoPaths.Name, PropertyType.String, "Old");
        Assert.IsTrue(mgr.CanUndo);
    }

    [TestMethod]
    public void Push_Pop_ReturnsCorrectRecord()
    {
        var mgr = new UndoManager();
        var entry = new FakeEntry { ID = 42 };
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, "OldName");
        var record = mgr.Pop();
        Assert.AreEqual(42, record.Entry.ID);
        Assert.AreEqual("OldName", record.PreviousValue);
        Assert.AreEqual(PropertyType.String, record.Type);
    }

    [TestMethod]
    public void Push_Pop_LIFOOrder()
    {
        var mgr = new UndoManager();
        var entry = new FakeEntry { ID = 1 };
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, "First");
        mgr.Push(entry, AppInfoPaths.SortAs, PropertyType.String, "Second");
        var record = mgr.Pop();
        Assert.AreEqual("Second", record.PreviousValue);
        record = mgr.Pop();
        Assert.AreEqual("First", record.PreviousValue);
    }

    [TestMethod]
    public void Pop_Empty_ThrowsInvalidOperation()
    {
        var mgr = new UndoManager();
        Assert.ThrowsException<InvalidOperationException>(() => mgr.Pop());
    }

    [TestMethod]
    public void Push_Pop_All_RemovesAll()
    {
        var mgr = new UndoManager();
        var entry = new FakeEntry { ID = 1 };
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, "A");
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, "B");
        mgr.Pop();
        mgr.Pop();
        Assert.IsFalse(mgr.CanUndo);
    }

    [TestMethod]
    public void Push_MultipleEntries_TracksAll()
    {
        var mgr = new UndoManager();
        var entry = new FakeEntry { ID = 1 };
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, "1");
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, "2");
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, "3");
        Assert.AreEqual("3", mgr.Pop().PreviousValue);
        Assert.AreEqual("2", mgr.Pop().PreviousValue);
        Assert.AreEqual("1", mgr.Pop().PreviousValue);
    }

    [TestMethod]
    public void UndoRecord_PropertyPath_Preserved()
    {
        var mgr = new UndoManager();
        var entry = new FakeEntry { ID = 1 };
        mgr.Push(entry, AppInfoPaths.IsHidden, PropertyType.Int32, 1);
        var record = mgr.Pop();
        CollectionAssert.AreEqual(AppInfoPaths.IsHidden, record.PropertyPath);
    }

    [TestMethod]
    public void CompoundRecord_CreateCompound_SetsIsCompound()
    {
        var entry = new FakeEntry { ID = 1 };
        var r1 = new UndoRecord(entry, AppInfoPaths.OpenVrSupport, PropertyType.Int32, 1);
        var r2 = new UndoRecord(entry, AppInfoPaths.BaseOpenVrSupport, PropertyType.Int32, 2);
        var compound = UndoRecord.CreateCompound(r1, r2);
        Assert.IsTrue(compound.IsCompound);
        Assert.AreEqual(2, compound.SubRecords!.Length);
    }

    [TestMethod]
    public void CompoundRecord_EntryFromFirstSubRecord()
    {
        var entry = new FakeEntry { ID = 42 };
        var r1 = new UndoRecord(entry, AppInfoPaths.OpenVrSupport, PropertyType.Int32, 1);
        var r2 = new UndoRecord(entry, AppInfoPaths.BaseOpenVrSupport, PropertyType.Int32, 2);
        var compound = UndoRecord.CreateCompound(r1, r2);
        Assert.AreEqual(42, compound.Entry.ID);
        CollectionAssert.AreEqual(AppInfoPaths.OpenVrSupport, compound.PropertyPath);
        Assert.AreEqual(1, (int)compound.PreviousValue!);
    }

    [TestMethod]
    public void PushCompound_PopReturnsCompound()
    {
        var mgr = new UndoManager();
        var entry = new FakeEntry { ID = 1 };
        var r1 = new UndoRecord(entry, AppInfoPaths.OpenVrSupport, PropertyType.Int32, 1);
        var r2 = new UndoRecord(entry, AppInfoPaths.BaseOpenVrSupport, PropertyType.Int32, 2);
        mgr.PushCompound(UndoRecord.CreateCompound(r1, r2));
        Assert.IsTrue(mgr.CanUndo);
        var record = mgr.Pop();
        Assert.IsTrue(record.IsCompound);
        Assert.AreEqual(2, record.SubRecords!.Length);
    }

    [TestMethod]
    public void PushCompound_MixedWithRegularPush_LIFOOrder()
    {
        var mgr = new UndoManager();
        var entry = new FakeEntry { ID = 1 };
        // 普通记录
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, "OldName");
        // 复合记录
        var r1 = new UndoRecord(entry, AppInfoPaths.OpenVrSupport, PropertyType.Int32, 1);
        var r2 = new UndoRecord(entry, AppInfoPaths.BaseOpenVrSupport, PropertyType.Int32, 2);
        mgr.PushCompound(UndoRecord.CreateCompound(r1, r2));
        // LIFO: 先弹出复合记录
        var popped = mgr.Pop();
        Assert.IsTrue(popped.IsCompound);
        // 再弹出普通记录
        popped = mgr.Pop();
        Assert.IsFalse(popped.IsCompound);
        Assert.AreEqual("OldName", popped.PreviousValue);
    }

    [TestMethod]
    public void CreateCompound_Empty_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(() => UndoRecord.CreateCompound());
    }

    [TestMethod]
    public void RegularRecord_IsCompound_ReturnsFalse()
    {
        var entry = new FakeEntry { ID = 1 };
        var record = new UndoRecord(entry, AppInfoPaths.Name, PropertyType.String, "Old");
        Assert.IsFalse(record.IsCompound);
        Assert.IsNull(record.SubRecords);
    }

    [TestMethod]
    public void MaxUndoLevel_DefaultIs500()
    {
        var mgr = new UndoManager();
        Assert.AreEqual(500, mgr.MaxUndoLevel);
    }

    [TestMethod]
    public void MaxUndoLevel_CustomValue_Applied()
    {
        var mgr = new UndoManager(10);
        Assert.AreEqual(10, mgr.MaxUndoLevel);
    }

    [TestMethod]
    public void MaxUndoLevel_MinClampedToOne()
    {
        var mgr = new UndoManager(0);
        Assert.AreEqual(1, mgr.MaxUndoLevel);
        var mgr2 = new UndoManager(-5);
        Assert.AreEqual(1, mgr2.MaxUndoLevel);
    }

    [TestMethod]
    public void Push_ExceedsLimit_TrimsOldest()
    {
        var mgr = new UndoManager(3);
        var entry = new FakeEntry { ID = 1 };
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, 'A');
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, 'B');
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, 'C');
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, 'D');
        Assert.AreEqual(3, mgr.Count);
        Assert.AreEqual('D', mgr.Pop().PreviousValue);
        Assert.AreEqual('C', mgr.Pop().PreviousValue);
        Assert.AreEqual('B', mgr.Pop().PreviousValue);
    }

    [TestMethod]
    public void PushCompound_ExceedsLimit_TrimsOldest()
    {
        var mgr = new UndoManager(2);
        var entry = new FakeEntry { ID = 1 };
        var r1 = new UndoRecord(entry, AppInfoPaths.Name, PropertyType.String, 'A');
        var r2 = new UndoRecord(entry, AppInfoPaths.Name, PropertyType.String, 'B');
        mgr.PushCompound(UndoRecord.CreateCompound(r1, r2));
        mgr.Push(entry, AppInfoPaths.Name, PropertyType.String, 'C');
        Assert.AreEqual(2, mgr.Count);
    }
}
