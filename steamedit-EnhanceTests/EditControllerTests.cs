#nullable enable
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance;
using steamedit_Enhance.Data;
using steamedit_Enhance.ViewModels;

namespace steamedit_EnhanceTests;

[TestClass]
public class EditControllerTests
{
    /// <summary>
    /// EditController 强依赖 GameListManager（WinForms ListBox），
    /// 完整单元测试需先抽象 GameListManager 接口。
    /// 此处仅验证基本构造和事件订阅逻辑。
    /// </summary>
    [TestMethod]
    public void Constructor_DoesNotThrow()
    {
        var ctx = new GameDataContext();
        var dataStore = new MockDataStore();
        var viewModel = new PropertyEditorViewModel();
        using var listManager = CreateListManager(ctx, viewModel);

        var ctl = new EditController(ctx, dataStore, listManager, viewModel);

        Assert.IsNotNull(ctl);
        Assert.IsNotNull(ctl.ViewModel);
    }

    private static GameListManager CreateListManager(GameDataContext ctx, PropertyEditorViewModel viewModel)
    {
        var dataStore = new MockDataStore();
        var vm = new GameListViewModel(dataStore);
        return new GameListManager(ctx, vm, dataStore,
            new System.Windows.Forms.ListView(),
            new System.Windows.Forms.ContextMenuStrip(),
            new System.Windows.Forms.ContextMenuStrip(),
            new System.Windows.Forms.ToolTip(),
            new steamedit_Enhance.ClearableTextBox());
    }

    private sealed class MockDataStore : IAppInfoDataStore
    {
        public IAppInfo? AppInfo { get; set; } = new MockAppInfo();
        public bool IsDataLoaded => AppInfo != null;
    }

    private sealed class MockAppInfo : IAppInfo
    {
        public uint MagicNumber => 0;
        public event EventHandler? Modified { add { } remove { } }
        public System.Collections.Generic.IEnumerable<IAppInfoEntry> Entries { get; set; }
            = Array.Empty<IAppInfoEntry>();
        public IAppInfoEntry? FindEntry(uint AppID) => null;
        public bool RevertEntry(IAppInfoEntry entry) => true;
        public void RemoveEntry(IAppInfoEntry entry) { }
        public void Save(string path, IFileSystem? fileSystem = null) { }
    }
}
