#nullable enable
using System;
using System.Collections.Generic;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;
using steamedit_Enhance.ViewModels;

namespace steamedit_EnhanceTests;

[TestClass]
public class MainViewModelTests
{
    private sealed class MockSteamDataService : ISteamDataService
    {
        public string SteamDir => "";
        public string AppInfoPath => "";
        public string ModInfoPath => "";
        public IAppInfo AppInfoData => null!;
        public IAppInfoDataStore DataStore => null!;
        public IEntryDisplaySettings DisplaySettings => null!;
        public bool NeedsSave { get; set; }
        public bool NeedsApply { get; set; }
        public bool UseCompleted { get; set; }
        public bool UseHidden { get; set; }

        bool ISteamDataReadService.UseCompleted => UseCompleted;
        bool ISteamDataReadService.UseHidden => UseHidden;

        public IFileSystem FileSystem { get; set; } = null!;

        public event EventHandler? OnNeedsSave;
        public event EventHandler? OnNeedsApply;

        public void RaiseNeedsSave() => OnNeedsSave?.Invoke(this, EventArgs.Empty);
        public void RaiseNeedsApply() => OnNeedsApply?.Invoke(this, EventArgs.Empty);

        public void Initialize(ref SteamDataInitProps props) { }
        public void Shutdown() { }
        public void Save() { }
        public void Apply() { }
    }

    private sealed class FakeEntry : IAppInfoEntry
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

    private sealed class MockAppInfo : IAppInfo
    {
        public IEnumerable<IAppInfoEntry> Entries => Array.Empty<IAppInfoEntry>();
        public event EventHandler? Modified;
        public IAppInfoEntry? FindEntry(uint AppID) => null;
        public void Save(string path) { }
        public bool RevertEntry(IAppInfoEntry entry) => false;
        public void RemoveEntry(IAppInfoEntry entry) { }
    }

    private sealed class MockDataStore : IAppInfoDataStore
    {
        public IAppInfo AppInfo { get; } = new MockAppInfo();
    }

    private static MainViewModel CreateMainViewModel()
    {
        var service = new MockSteamDataService();
        var gl = new GameListViewModel(new MockDataStore());
        var pe = new PropertyEditorViewModel();
        return new MainViewModel(service, gl, pe);
    }

    [TestMethod]
    public void Constructor_SetsWindowTitle()
    {
        var service = new MockSteamDataService();
        var vm = new MainViewModel(service, new GameListViewModel(new MockDataStore()), new PropertyEditorViewModel());
        Assert.AreEqual("steamedit-Enhance", vm.WindowTitle);
    }

    [TestMethod]
    public void WindowTitle_NeedsSave_ShowsAsterisk()
    {
        var service = new MockSteamDataService();
        var vm = new MainViewModel(service, new GameListViewModel(new MockDataStore()), new PropertyEditorViewModel());

        service.NeedsSave = true;
        service.RaiseNeedsSave();

        Assert.IsTrue(vm.WindowTitle.Contains("*"));
    }

    [TestMethod]
    public void WindowTitle_NeedsApply_ShowsExclamation()
    {
        var service = new MockSteamDataService();
        var vm = new MainViewModel(service, new GameListViewModel(new MockDataStore()), new PropertyEditorViewModel());

        service.NeedsApply = true;
        service.RaiseNeedsApply();

        Assert.IsTrue(vm.WindowTitle.Contains("!"));
    }

    [TestMethod]
    public void WindowTitle_NeedsSaveAndApply_BothShown()
    {
        var service = new MockSteamDataService();
        var vm = new MainViewModel(service, new GameListViewModel(new MockDataStore()), new PropertyEditorViewModel());

        service.NeedsSave = true;
        service.NeedsApply = true;
        service.RaiseNeedsSave();

        Assert.IsTrue(vm.WindowTitle.Contains("*"));
        Assert.IsTrue(vm.WindowTitle.Contains("!"));
    }

    [TestMethod]
    public void RefreshTitle_UpdatesTitleWithoutEvent()
    {
        var service = new MockSteamDataService { NeedsSave = true };
        var vm = new MainViewModel(service, new GameListViewModel(new MockDataStore()), new PropertyEditorViewModel());

        vm.RefreshTitle();

        Assert.IsTrue(vm.WindowTitle.Contains("*"));
    }

    [TestMethod]
    public void CanUndo_InitiallyFalse()
    {
        var service = new MockSteamDataService();
        var vm = new MainViewModel(service, new GameListViewModel(new MockDataStore()), new PropertyEditorViewModel());
        Assert.IsFalse(vm.CanUndo);
    }

    [TestMethod]
    public void CanUndo_AfterEditCommandExecuted_True()
    {
        var pe = new PropertyEditorViewModel();
        var vm = new MainViewModel(new MockSteamDataService(), new GameListViewModel(new MockDataStore()), pe);

        // Simulate an undo push via EditCommandExecuted (which PropertyEditor fires on edits)
        // We need to trigger the event through PropertyEditor
        var entry = new FakeEntry { ID = 1, Name = "Test" };
        pe.LoadEntry(entry);
        pe.GameName = "Changed";

        Assert.IsTrue(vm.CanUndo);
    }

    [TestMethod]
    public void UpdateAppId_SetsAppIdText()
    {
        var service = new MockSteamDataService();
        var vm = new MainViewModel(service, new GameListViewModel(new MockDataStore()), new PropertyEditorViewModel());

        vm.UpdateAppId(42);

        Assert.AreEqual("App ID: 42", vm.AppIdText);
    }

    [TestMethod]
    public void UpdateAppId_ZeroShowsDash()
    {
        var vm = CreateMainViewModel();

        vm.UpdateAppId(0);

        Assert.AreEqual("App ID: —", vm.AppIdText);
    }

    [TestMethod]
    public void SaveCommand_FiresSaveRequested()
    {
        var vm = CreateMainViewModel();
        bool fired = false;
        vm.SaveRequested += (_, _) => fired = true;

        vm.SaveCommand.Execute(null);

        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void SettingsCommand_FiresSettingsRequested()
    {
        var vm = CreateMainViewModel();
        bool fired = false;
        vm.SettingsRequested += (_, _) => fired = true;

        vm.SettingsCommand.Execute(null);

        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void UndoCommand_WhenCanUndo_FiresUndoRequested()
    {
        var pe = new PropertyEditorViewModel();
        var vm = new MainViewModel(new MockSteamDataService(), new GameListViewModel(new MockDataStore()), pe);
        var entry = new FakeEntry { ID = 1, Name = "Old" };
        pe.LoadEntry(entry);
        pe.GameName = "New";

        bool fired = false;
        vm.UndoRequested += (_, _) => fired = true;

        vm.UndoCommand.Execute(null);

        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void UndoCommand_WhenCannotUndo_DoesNotExecute()
    {
        var vm = CreateMainViewModel();
        bool fired = false;
        vm.UndoRequested += (_, _) => fired = true;

        Assert.IsFalse(vm.UndoCommand.CanExecute(null));
    }

    [TestMethod]
    public void RaiseNeedsSave_UpdatesWindowTitle()
    {
        var service = new MockSteamDataService { NeedsSave = true };
        var vm = new MainViewModel(service, new GameListViewModel(new MockDataStore()), new PropertyEditorViewModel());

        service.RaiseNeedsSave();

        Assert.IsTrue(vm.WindowTitle.Contains("*"));
    }

    [TestMethod]
    public void AllCommands_AreNotNull()
    {
        var vm = CreateMainViewModel();

        Assert.IsNotNull(vm.SaveCommand);
        Assert.IsNotNull(vm.SaveApplyCommand);
        Assert.IsNotNull(vm.SaveApplyRestartCommand);
        Assert.IsNotNull(vm.UndoCommand);
        Assert.IsNotNull(vm.SettingsCommand);
        Assert.IsNotNull(vm.EditLaunchCommand);
        Assert.IsNotNull(vm.RevertCommand);
        Assert.IsNotNull(vm.SearchCommand);
        Assert.IsNotNull(vm.TranslateCommand);
        Assert.IsNotNull(vm.BatchTranslateCommand);
        Assert.IsNotNull(vm.BatchReplaceCommand);
        Assert.IsNotNull(vm.AboutCommand);
        Assert.IsNotNull(vm.ShortcutsCommand);
    }

    [TestMethod]
    public void DetachEvents_StopsTitleUpdates()
    {
        var service = new MockSteamDataService();
        var vm = new MainViewModel(service, new GameListViewModel(new MockDataStore()), new PropertyEditorViewModel());

        vm.DetachEvents();

        service.NeedsSave = true;
        service.RaiseNeedsSave();

        Assert.AreEqual("steamedit-Enhance", vm.WindowTitle);
    }

    [TestMethod]
    public void GameList_SubViewModel_IsAccessible()
    {
        var vm = CreateMainViewModel();
        Assert.IsNotNull(vm.GameList);
        Assert.IsNotNull(vm.PropertyEditor);
    }
}
