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
    private bool _isBusy;

    public GameListViewModel GameList { get; }
    public PropertyEditorViewModel PropertyEditor { get; }

    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveApplyCommand { get; }
    public RelayCommand SaveApplyRestartCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand SettingsCommand { get; }
    public RelayCommand EditLaunchCommand { get; }
    public RelayCommand RevertCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand TranslateCommand { get; }
    public RelayCommand BatchTranslateCommand { get; }
    public RelayCommand BatchReplaceCommand { get; }
    public RelayCommand AboutCommand { get; }
    public RelayCommand ShortcutsCommand { get; }

    public event EventHandler<EventArgs>? SaveRequested;
    public event EventHandler<EventArgs>? SaveApplyRequested;
    public event EventHandler<EventArgs>? SaveApplyRestartRequested;
    public event EventHandler<EventArgs>? UndoRequested;
    public event EventHandler<EventArgs>? SettingsRequested;
    public event EventHandler<EventArgs>? EditLaunchRequested;
    public event EventHandler<EventArgs>? RevertRequested;
    public event EventHandler<EventArgs>? SearchRequested;
    public event EventHandler<EventArgs>? TranslateRequested;
    public event EventHandler<EventArgs>? BatchTranslateRequested;
    public event EventHandler<EventArgs>? BatchReplaceRequested;
    public event EventHandler<EventArgs>? AboutRequested;
    public event EventHandler<EventArgs>? ShortcutsRequested;
    public event EventHandler<EventArgs>? StatusChanged;

    public MainViewModel(ISteamDataService steamData, GameListViewModel gameList, PropertyEditorViewModel propertyEditor)
    {
        _steamData = steamData;
        GameList = gameList;
        PropertyEditor = propertyEditor;

        _steamData.OnNeedsSave += OnNeedsSaveOrApply;
        _steamData.OnNeedsApply += OnNeedsSaveOrApply;

        propertyEditor.EditCommandExecuted += (_, _) => UpdateCanUndo();
        gameList.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(GameListViewModel.VisibleCount):
                case nameof(GameListViewModel.TotalEntryCount):
                case nameof(GameListViewModel.ModifiedCount):
                    StatusChanged?.Invoke(this, EventArgs.Empty);
                    break;
            }
        };

        SaveCommand = new RelayCommand(() => SaveRequested?.Invoke(this, EventArgs.Empty));
        SaveApplyCommand = new RelayCommand(() => SaveApplyRequested?.Invoke(this, EventArgs.Empty));
        SaveApplyRestartCommand = new RelayCommand(() => SaveApplyRestartRequested?.Invoke(this, EventArgs.Empty));
        UndoCommand = new RelayCommand(() => UndoRequested?.Invoke(this, EventArgs.Empty), () => CanUndo);
        SettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        EditLaunchCommand = new RelayCommand(() => EditLaunchRequested?.Invoke(this, EventArgs.Empty));
        RevertCommand = new RelayCommand(() => RevertRequested?.Invoke(this, EventArgs.Empty));
        SearchCommand = new RelayCommand(() => SearchRequested?.Invoke(this, EventArgs.Empty));
        TranslateCommand = new RelayCommand(() => TranslateRequested?.Invoke(this, EventArgs.Empty));
        BatchTranslateCommand = new RelayCommand(() => BatchTranslateRequested?.Invoke(this, EventArgs.Empty));
        BatchReplaceCommand = new RelayCommand(() => BatchReplaceRequested?.Invoke(this, EventArgs.Empty));
        AboutCommand = new RelayCommand(() => AboutRequested?.Invoke(this, EventArgs.Empty));
        ShortcutsCommand = new RelayCommand(() => ShortcutsRequested?.Invoke(this, EventArgs.Empty));

        UpdateCanUndo();
    }

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

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private void OnNeedsSaveOrApply(object? sender, EventArgs e)
    {
        RefreshTitle();
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
