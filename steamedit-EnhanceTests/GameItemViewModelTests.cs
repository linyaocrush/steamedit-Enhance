#nullable enable
using System;
using System.Collections.Generic;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;
using steamedit_Enhance.ViewModels;

namespace steamedit_EnhanceTests;

[TestClass]
public class GameItemViewModelTests
{
    private class FakeEntry : IAppInfoEntry
    {
        public int ID { get; set; }
        public uint AppInfoVersion => 0;
        public uint ChangeNumber => 0;
        public ModifiedEntry? Modification { get; set; }
        public bool IsModified { get; set; }
        public bool IsHidden { get; set; }
        public bool IsCompleted { get; set; }
        public string Name { get; set; } = "";
        public bool HasSortAs => false;
        public string SortAs { get; set; } = "";
        public string NormalizedSortAs => Common.StringTitleComparer.Normalize(SortAs);
        public string InstallPath => "";
        public AppInfoEntryType Type { get; set; }
        public LaunchTable Launches { get; set; } = new LaunchTable();
        public event EventHandler? Modified;
        public byte[] GetOriginalDataCopy() => Array.Empty<byte>();
        public T GetPropertyValue<T>(T def, params string[] path) => def;
        public void SetPropertyValue(PropertyType t, object? v, params string[] path) { }
        public void RevertModifiedProperty(params string[] path) { }
        public bool Revert() => false;
        public PropertyType? GetOriginalPropertyType(params string[] path) => null;
        public object? GetOriginalPropertyValue(params string[] path) => null;
    }

    [TestMethod]
    public void Constructor_StoresEntry()
    {
        var entry = new FakeEntry { ID = 42 };
        var vm = new GameItemViewModel(entry, "Display Name");
        Assert.AreSame(entry, vm.Entry);
        Assert.AreEqual(42, vm.ID);
    }

    [TestMethod]
    public void Constructor_InitialDisplayName()
    {
        var vm = new GameItemViewModel(new FakeEntry(), "Initial");
        Assert.AreEqual("Initial", vm.DisplayName);
    }

    [TestMethod]
    public void Name_DelegatesToEntry()
    {
        var entry = new FakeEntry { Name = "Game A" };
        var vm = new GameItemViewModel(entry, "Game A");
        Assert.AreEqual("Game A", vm.Name);
    }

    [TestMethod]
    public void SortAs_DelegatesToEntry()
    {
        var entry = new FakeEntry { SortAs = "SortKey" };
        var vm = new GameItemViewModel(entry, "Display");
        Assert.AreEqual("SortKey", vm.SortAs);
    }

    [TestMethod]
    public void Type_DelegatesToEntry()
    {
        var entry = new FakeEntry { Type = AppInfoEntryType.DLC };
        var vm = new GameItemViewModel(entry, "");
        Assert.AreEqual(AppInfoEntryType.DLC, vm.Type);
    }

    [TestMethod]
    public void IsModified_DelegatesToEntry()
    {
        var vm = new GameItemViewModel(new FakeEntry { IsModified = false }, "");
        Assert.IsFalse(vm.IsModified);

        var vm2 = new GameItemViewModel(new FakeEntry { IsModified = true }, "");
        Assert.IsTrue(vm2.IsModified);
    }

    [TestMethod]
    public void IsHidden_DelegatesToEntry()
    {
        var vm = new GameItemViewModel(new FakeEntry { IsHidden = true }, "");
        Assert.IsTrue(vm.IsHidden);
    }

    [TestMethod]
    public void IsCompleted_DelegatesToEntry()
    {
        var vm = new GameItemViewModel(new FakeEntry { IsCompleted = true }, "");
        Assert.IsTrue(vm.IsCompleted);
    }

    [TestMethod]
    public void NormalizedSortAs_DelegatesToEntry()
    {
        var vm = new GameItemViewModel(new FakeEntry { SortAs = "The Game Name" }, "");
        Assert.AreEqual(Common.StringTitleComparer.Normalize("The Game Name"), vm.NormalizedSortAs);
    }

    [TestMethod]
    public void DisplayName_Setter_StoresValue()
    {
        var vm = new GameItemViewModel(new FakeEntry(), "Initial");
        vm.DisplayName = "Updated";
        Assert.AreEqual("Updated", vm.DisplayName);
    }

    [TestMethod]
    public void DisplayName_SameValue_DoesNotRaise()
    {
        var vm = new GameItemViewModel(new FakeEntry(), "Same");
        int count = 0;
        vm.PropertyChanged += (_, _) => count++;
        vm.DisplayName = "Same";
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void DisplayName_NewValue_RaisesPropertyChanged()
    {
        var vm = new GameItemViewModel(new FakeEntry(), "Old");
        var fired = new List<string>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");
        vm.DisplayName = "New";
        CollectionAssert.AreEqual(new[] { nameof(GameItemViewModel.DisplayName) }, fired);
    }

    [TestMethod]
    public void InvalidateDisplayName_RaisesPropertyChanged()
    {
        var vm = new GameItemViewModel(new FakeEntry(), "Initial");
        bool fired = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameItemViewModel.DisplayName))
                fired = true;
        };
        vm.InvalidateDisplayName();
        Assert.IsTrue(fired);
    }
}
