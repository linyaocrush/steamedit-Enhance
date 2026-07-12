#nullable enable
using System;
using System.Windows.Input;
using SteamEdit.Data;

namespace SteamEdit.ViewModels;

internal sealed class MainViewModel : ViewModelBase
{
    private readonly ISteamDataService _steamData;
    private bool _canUndo;
    private string _windowTitle = "SteamEdit";
    private string _appIdText = "App ID: —";

    public GameListViewModel GameList { get; }
    public PropertyEditorViewModel PropertyEditor { get; }

    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveApplyCommand { get; }
    public RelayCommand SaveApplyRestartCommand { get; }

    public string WindowTitle
    {
        get => _windowTitle;
        private set => SetProperty(ref _windowTitle, value);
    }

    public string AppIdText
    {
        get => _appIdText;
        set => SetProperty(ref _appIdText, value);
    }

    public bool CanUndo
    {
        get => _canUndo;
        set
        {
            if (SetProperty(ref _canUndo, value))
                UndoCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand UndoCommand { get; }

    public event EventHandler<EventArgs>? SaveRequested;
    public event EventHandler<EventArgs>? SaveApplyRequested;
    public event EventHandler<EventArgs>? SaveApplyRestartRequested;
    public event EventHandler<EventArgs>? UndoRequested;

    public MainViewModel(ISteamDataService steamData, GameListViewModel gameList, PropertyEditorViewModel propertyEditor)
    {
        _steamData = steamData;
        GameList = gameList;
        PropertyEditor = propertyEditor;

        _steamData.OnNeedsSave += OnNeedsSaveOrApply;
        _steamData.OnNeedsApply += OnNeedsSaveOrApply;

        propertyEditor.EditCommandExecuted += (_, _) => UpdateCanUndo();

        SaveCommand = new RelayCommand(() => SaveRequested?.Invoke(this, EventArgs.Empty));
        SaveApplyCommand = new RelayCommand(() => SaveApplyRequested?.Invoke(this, EventArgs.Empty));
        SaveApplyRestartCommand = new RelayCommand(() => SaveApplyRestartRequested?.Invoke(this, EventArgs.Empty));
        UndoCommand = new RelayCommand(() => UndoRequested?.Invoke(this, EventArgs.Empty), () => CanUndo);

        UpdateCanUndo();
    }

    private void OnNeedsSaveOrApply(object? sender, EventArgs e)
    {
        WindowTitle = BuildWindowTitle();
    }

    public void RefreshTitle()
    {
        WindowTitle = BuildWindowTitle();
    }

    private string BuildWindowTitle()
    {
        var title = "SteamEdit";
        if (_steamData.NeedsSave)
            title += " *";
        if (_steamData.NeedsApply)
            title += " !";
        return title;
    }

    public void UpdateCanUndo()
    {
        CanUndo = PropertyEditor.UndoManager.CanUndo;
    }

    public void UpdateAppId(int id)
    {
        AppIdText = id != 0 ? $"App ID: {id}" : "App ID: —";
    }

    public void DetachEvents()
    {
        _steamData.OnNeedsSave -= OnNeedsSaveOrApply;
        _steamData.OnNeedsApply -= OnNeedsSaveOrApply;
    }
}
