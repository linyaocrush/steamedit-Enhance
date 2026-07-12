#nullable enable
using System;
using System.Collections.Generic;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance;
using steamedit_Enhance.Data;
using steamedit_Enhance.ViewModels;

namespace steamedit_EnhanceTests;

[TestClass]
public class EditControllerTests
{
    private sealed class MockDataStore : IAppInfoDataStore
    {
        public IAppInfo? AppInfo { get; set; }
        public bool IsDataLoaded => AppInfo != null;
    }

    private sealed class MockAppInfo : IAppInfo
    {
        public uint MagicNumber => 0;
        public event EventHandler? Modified { add { } remove { } }
        public IEnumerable<IAppInfoEntry> Entries { get; set; } = Array.Empty<IAppInfoEntry>();
        public bool RevertEntry(IAppInfoEntry entry) => true;
        public void RemoveEntry(IAppInfoEntry entry) { }
        public void Save(string path) { }
    }

    private sealed class MockEntry : IAppInfoEntry
    {
        private readonly Dictionary<string, object?> _props = new();
        private string _name = "TestGame";
        private string? _sortAs;

        public int ID { get; set; } = 12345;
        public uint AppInfoVersion => 0x07564429;
        public uint ChangeNumber => 1;
        public ModifiedEntry? Modification { get; set; }
        public bool IsModified => Modification?.Changes != null && Modification.Changes.Count > 0;
        public bool IsHidden { get; set; }
        public bool IsCompleted { get; set; }
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                Modified?.Invoke(this, EventArgs.Empty);
            }
        }
        public bool HasSortAs => !string.IsNullOrEmpty(_sortAs);
        public string SortAs
        {
            get => _sortAs ?? _name;
            set => _sortAs = value;
        }
        public string NormalizedSortAs => StringTitleComparer.Normalize(SortAs);
        public string InstallPath => "";
        public AppInfoEntryType Type { get; set; } = AppInfoEntryType.Game;
        public LaunchTable Launches { get; set; } = new LaunchTable();

        public event EventHandler? Modified;

        public byte[] GetOriginalDataCopy() => Array.Empty<byte>();
        public T GetPropertyValue<T>(T def, params string[] path) => def;
        public void SetPropertyValue(PropertyType type, object? value, params string[] path) { }
        public void RevertModifiedProperty(params string[] path) { }
        public bool Revert() => false;
        public PropertyType? GetOriginalPropertyType(params string[] path) => null;
        public object? GetOriginalPropertyValue(params string[] path) => null;
        public override string ToString() => Name;
    }

    private sealed class MockListManager
    {
        public IAppInfoEntry? SelectedEntry { get; set; }
        public int SelectedIndex { get; set; }
        public IAppInfoEntry? NotifiedEntry { get; private set; }
        public IAppInfoEntry? RevertedEntry { get; private set; }

        public IAppInfoEntry? GetSelectedEntry() => SelectedEntry;
        public bool SelectIndex(int idx) { SelectedIndex = idx; return true; }
        public void NotifyEntryEdited(IAppInfoEntry entry) => NotifiedEntry = entry;
        public void NotifyEntryReverted(IAppInfoEntry entry) => RevertedEntry = entry;
        public void InvalidateDisplayName(IAppInfoEntry entry) { }
        public void InvalidateItem(IAppInfoEntry entry) { }
        public void RefreshGameList() { }
    }

    private static EditController CreateController(out MockListManager listManager, out PropertyEditorViewModel viewModel)
    {
        var ctx = new GameDataContext();
        var dataStore = new MockDataStore { AppInfo = new MockAppInfo() };
        listManager = new MockListManager();
        viewModel = new PropertyEditorViewModel();
        return new EditController(ctx, dataStore, listManager, viewModel);
    }

    [TestMethod]
    public void LoadEntry_ValidEntry_SetsCurrentEntry()
    {
        var ctl = CreateController(out _, out var vm);
        var entry = new MockEntry { ID = 100 };

        ctl.LoadEntry(entry);

        Assert.AreEqual(entry, vm.CurrentEntry);
    }

    [TestMethod]
    public void LoadEntry_NullEntry_ClearsCurrentEntry()
    {
        var ctl = CreateController(out _, out var vm);

        ctl.LoadEntry(null);

        Assert.IsNull(vm.CurrentEntry);
    }

    [TestMethod]
    public void PerformUndo_NoUndo_DoesNothing()
    {
        var ctl = CreateController(out var lm, out var vm);
        var entry = new MockEntry { ID = 200 };
        lm.SelectedEntry = entry;

        ctl.PerformUndo();

        Assert.IsFalse(vm.UndoManager.CanUndo);
    }

    [TestMethod]
    public void ApplyBatchPropertyChange_RecordsUndo()
    {
        var ctl = CreateController(out var lm, out var vm);
        var entries = new List<IAppInfoEntry>
        {
            new MockEntry { ID = 1, Name = "GameA" },
            new MockEntry { ID = 2, Name = "GameB" }
        };

        int callCount = 0;
        ctl.ApplyBatchPropertyChange(entries, PropertyType.String,
            e => e.Name,
            e => { e.Name = "Renamed"; callCount++; },
            AppInfoPaths.Name);

        Assert.AreEqual(2, callCount);
        Assert.IsTrue(vm.UndoManager.CanUndo);
        var record = vm.UndoManager.Pop();
        Assert.AreEqual(2, record.SubRecords.Length);
    }

    [TestMethod]
    public void ClearProperty_ClearsAndReloads()
    {
        var ctl = CreateController(out var lm, out var vm);
        var entry = new MockEntry { ID = 300 };
        lm.SelectedEntry = entry;

        ctl.ClearProperty(AppInfoPaths.Name);

        Assert.AreEqual(entry, vm.CurrentEntry);
    }

    [TestMethod]
    public void ClearProperty_NullEntry_DoesNothing()
    {
        var ctl = CreateController(out var lm, out var vm);
        lm.SelectedEntry = null;

        ctl.ClearProperty(AppInfoPaths.Name);

        Assert.IsNull(vm.CurrentEntry);
    }
}
