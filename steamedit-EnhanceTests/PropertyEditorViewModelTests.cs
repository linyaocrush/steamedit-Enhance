#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;
using steamedit_Enhance.ViewModels;

namespace steamedit_EnhanceTests;

[TestClass]
public class PropertyEditorViewModelTests
{
    private sealed class FakeEntry : IAppInfoEntry
    {
        private readonly Dictionary<string, object?> _properties = new();

        public int ID { get; set; } = 1;
        public uint AppInfoVersion => 0;
        public uint ChangeNumber => 0;
        public ModifiedEntry? Modification { get; set; }
        public bool IsModified => Modification?.Changes != null;
        public bool IsHidden { get; set; }
        public bool IsCompleted { get; set; }
        public string Name { get; set; } = "";
        public bool HasSortAs => !string.IsNullOrEmpty(SortAs);
        public string SortAs { get; set; } = "";
        public string NormalizedSortAs => Common.StringTitleComparer.Normalize(SortAs);
        public string InstallPath => "";
        public AppInfoEntryType Type { get; set; } = AppInfoEntryType.Game;
        public LaunchTable Launches { get; set; } = new LaunchTable();
        public PropertyTable? NameLocalized { get; set; }

        public event EventHandler? Modified;

        public byte[] GetOriginalDataCopy() => Array.Empty<byte>();

        public T GetPropertyValue<T>(T def, params string[] PropertyPath)
        {
            if (PropertyPath.Length == 3 && PropertyPath[0] == "appinfo" && PropertyPath[1] == "common" && PropertyPath[2] == "name_localized")
            {
                if (NameLocalized != null && typeof(T) == typeof(PropertyTable))
                    return (T)(object)NameLocalized;
                return def;
            }
            var key = string.Join("/", PropertyPath);
            if (_properties.TryGetValue(key, out var v) && v is T typed) return typed;
            return def;
        }

        public void SetPropertyValue(PropertyType t, object? Value, params string[] PropertyPath)
        {
            var key = string.Join("/", PropertyPath);
            _properties[key] = Value;
        }

        public void RevertModifiedProperty(params string[] PropertyPath)
        {
            var key = string.Join("/", PropertyPath);
            if (_properties.ContainsKey(key))
                _properties.Remove(key);
        }

        public bool Revert() => true;

        public PropertyType? GetOriginalPropertyType(params string[] PropertyPath) => null;
        public object? GetOriginalPropertyValue(params string[] PropertyPath) => null;

        public T GetPropOrDefault<T>(string path, T defaultValue) => GetPropertyValue(defaultValue, path);
    }

    [TestMethod]
    public void LoadEntry_NullParams_CurrentEntryBecomesNull()
    {
        var vm = new PropertyEditorViewModel();
        vm.LoadEntry(null);
        Assert.IsNull(vm.CurrentEntry);
    }

    [TestMethod]
    public void LoadEntry_LoadsAllEditableFields()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry
        {
            Name = "Game A",
            SortAs = "A-Sort",
            IsHidden = true,
            IsCompleted = true,
            Type = AppInfoEntryType.DLC,
        };
        entry.SetPropertyValue(PropertyType.String, "partial", "appinfo", "common", "controller_support");
        entry.SetPropertyValue(PropertyType.Int32, 1, "appinfo", "common", "openvrsupport");

        vm.LoadEntry(entry);

        Assert.AreEqual("Game A", vm.GameName);
        Assert.AreEqual("A-Sort", vm.SortAs);
        Assert.IsTrue(vm.IsHidden);
        Assert.IsTrue(vm.IsCompleted);
        Assert.AreEqual(AppInfoEntryType.DLC, vm.EntryType);
        Assert.AreEqual(1, vm.ControllerSupportIndex);  // partial
        Assert.IsTrue(vm.VREnabled);
        Assert.AreEqual(0, vm.VRIndex);
        Assert.IsNotNull(vm.CurrentEntry);
    }

    [TestMethod]
    public void LoadEntry_AccessesProperties_OfCurrentEntry_Works()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { Name = "Game A" };
        vm.LoadEntry(entry);
        Assert.AreEqual("Game A", vm.GameName);
        Assert.IsNotNull(vm.CurrentEntry);
        Assert.AreEqual(entry, vm.CurrentEntry);
    }

    private static PropertyTable LocalizedTable(params (string Lang, string Name)[] entries)
    {
        var t = new PropertyTable();
        foreach (var (lang, name) in entries)
            t.AddPropertyValue(lang, PropertyType.String, name);
        return t;
    }

    private static void WithUi(string culture, Action action)
    {
        CultureInfo old = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            action();
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }

    [TestMethod]
    public void LoadEntry_ChineseUi_Unmodified_GameNameShowsLocalizedOfficialName()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry
        {
            Name = "Cyberpunk 2077",
            NameLocalized = LocalizedTable(("english", "Cyberpunk 2077"), ("schinese", "赛博朋克2077")),
        };
        WithUi("zh-CN", () => vm.LoadEntry(entry));
        Assert.AreEqual("赛博朋克2077", vm.GameName);
    }

    [TestMethod]
    public void LoadEntry_EnglishUi_GameNameKeepsStoredEnglishName()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry
        {
            Name = "Cyberpunk 2077",
            NameLocalized = LocalizedTable(("schinese", "赛博朋克2077")),
        };
        WithUi("en-US", () => vm.LoadEntry(entry));
        Assert.AreEqual("Cyberpunk 2077", vm.GameName);
    }

    [TestMethod]
    public void LoadEntry_ChineseUi_UserRenamed_GameNameShowsCustomName()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry
        {
            Name = "My Custom Name",
            NameLocalized = LocalizedTable(("schinese", "赛博朋克2077")),
        };
        var mod = new ModifiedEntry(entry);
        mod.AddChange(PropertyType.String, "My Custom Name", AppInfoPaths.Name);
        entry.Modification = mod;

        WithUi("zh-CN", () => vm.LoadEntry(entry));
        Assert.AreEqual("My Custom Name", vm.GameName);
    }

    [TestMethod]
    public void GameName_EditDuringLoad_DoesNotPushUndo()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { Name = "OldName" };
        vm.LoadEntry(entry);
        var recordsBefore = vm.UndoManager.Count;
        Assert.AreEqual(0, recordsBefore);
    }

    [TestMethod]
    public void GameName_SetAfterLoad_PushesUndoAndUpdatesEntry()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { Name = "OldName" };
        vm.LoadEntry(entry);

        vm.GameName = "NewName";
        Assert.AreEqual("NewName", entry.Name);
        Assert.AreEqual(1, vm.UndoManager.Count);
        Assert.AreEqual("OldName", vm.UndoManager.Pop().PreviousValue);
    }

    [TestMethod]
    public void GameName_SetSameValue_DoesNotPushUndo()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { Name = "SameName" };
        vm.LoadEntry(entry);

        vm.GameName = "SameName";
        Assert.AreEqual(0, vm.UndoManager.Count);
    }

    [TestMethod]
    public void IsHidden_Change_PushesUndo()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { IsHidden = false };
        vm.LoadEntry(entry);

        vm.IsHidden = true;
        Assert.IsTrue(entry.IsHidden);
        Assert.AreEqual(1, vm.UndoManager.Count);
        Assert.AreEqual(0, (int)vm.UndoManager.Pop().PreviousValue!);
    }

    [TestMethod]
    public void IsCompleted_Change_PushesUndo()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { IsCompleted = false };
        vm.LoadEntry(entry);

        vm.IsCompleted = true;
        Assert.IsTrue(entry.IsCompleted);
        Assert.AreEqual(1, vm.UndoManager.Count);
    }

    [TestMethod]
    public void SortAs_Change_PushesUndoAndUpdatesEntry()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { SortAs = "" };
        vm.LoadEntry(entry);

        vm.SortAs = "New Sort";
        Assert.AreEqual("New Sort", entry.SortAs);
        Assert.AreEqual(1, vm.UndoManager.Count);
    }

    [TestMethod]
    public void ControllerSupportIndex_SetValidIndex_WritesRawValue()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry();
        entry.SetPropertyValue(PropertyType.String, "none", "appinfo", "common", "controller_support");
        vm.LoadEntry(entry);

        vm.ControllerSupportIndex = 2;  // full
        var written = entry.GetPropertyValue<string>(null!, "appinfo", "common", "controller_support");
        Assert.AreEqual("full", written);
        Assert.AreEqual(1, vm.UndoManager.Count);
    }

    [TestMethod]
    public void ControllerSupportIndex_OutOfRange_WritesNone()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry();
        entry.SetPropertyValue(PropertyType.String, "full", "appinfo", "common", "controller_support");
        vm.LoadEntry(entry);

        vm.ControllerSupportIndex = 10;
        var written = entry.GetPropertyValue<string>(null!, "appinfo", "common", "controller_support");
        Assert.AreEqual("none", written);
    }

    [TestMethod]
    public void EntryType_Change_PushesUndo()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { Type = AppInfoEntryType.Game };
        vm.LoadEntry(entry);

        vm.EntryType = AppInfoEntryType.DLC;
        Assert.AreEqual(AppInfoEntryType.DLC, entry.Type);
        Assert.AreEqual(1, vm.UndoManager.Count);
    }

    [TestMethod]
    public void VREnabled_OpenVrSupportDefault_StaysDisabled()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry();
        // Without OpenVrSupport set, default is int.MaxValue (disabled)
        vm.LoadEntry(entry);
        Assert.IsFalse(vm.VREnabled);
        Assert.AreEqual(-1, vm.VRIndex);
    }

    [TestMethod]
    public void VREnabled_OpenVrSupportSet_EnablesVR()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry();
        entry.SetPropertyValue(PropertyType.Int32, 1, "appinfo", "common", "openvrsupport");
        vm.LoadEntry(entry);

        Assert.IsTrue(vm.VREnabled);
        Assert.AreEqual(0, vm.VRIndex);
    }

    [TestMethod]
    public void VREnabled_BaseOpenVrSupportSet_ShowsRemoved()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry();
        entry.SetPropertyValue(PropertyType.Int32, 1, "appinfo", "common", "openvrsupport");
        entry.SetPropertyValue(PropertyType.Int32, 1, "appinfo", "steam_edit", "base_openvrsupport");
        vm.LoadEntry(entry);

        Assert.IsTrue(vm.VREnabled);
        Assert.AreEqual(1, vm.VRIndex);  // "removed"
    }

    [TestMethod]
    public void ApplyTranslation_SetsNameAndUpdatesHasSortAs()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { Name = "Old", SortAs = "" };
        vm.LoadEntry(entry);

        vm.ApplyTranslation("Translated", "Old");

        Assert.AreEqual("Translated", vm.GameName);
        Assert.IsFalse(vm.HasSortAs);
    }

    [TestMethod]
    public void LoadEntry_RaisesPropertyChangedForGameName()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { Name = "Test" };
        vm.LoadEntry(entry);

        var fired = new List<string>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        var entry2 = new FakeEntry { Name = "Updated" };
        vm.LoadEntry(entry2);

        Assert.IsTrue(fired.Count > 0);
        Assert.IsTrue(fired.Contains(nameof(PropertyEditorViewModel.GameName)));
    }

    [TestMethod]
    public void UndoManager_DifferentInstances_IndependentRecords()
    {
        var vm1 = new PropertyEditorViewModel();
        var vm2 = new PropertyEditorViewModel();
        Assert.AreNotSame(vm1.UndoManager, vm2.UndoManager);
    }

    [TestMethod]
    public void EditCommandExecuted_RaisedOnPropertyChange()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { Name = "Old" };
        vm.LoadEntry(entry);

        IAppInfoEntry? captured = null;
        vm.EditCommandExecuted += (_, e) => captured = e;

        vm.GameName = "New";

        Assert.IsNotNull(captured);
        Assert.AreEqual(entry, captured);
    }

    [TestMethod]
    public void Multiple_PropertyChanges_DistinctUndoRecords()
    {
        var vm = new PropertyEditorViewModel();
        var entry = new FakeEntry { Name = "A", SortAs = "" };
        vm.LoadEntry(entry);

        vm.GameName = "B";
        vm.SortAs = "S";

        Assert.AreEqual(2, vm.UndoManager.Count);
    }
}
