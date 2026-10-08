using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.App.Navigation;
using Hatband.App.ViewModels.Navigation;
using Hatband.App.ViewModels.Settings;
using Hatband.Core.Models.Settings;

namespace Hatband.App.ViewModels;

/// <summary>
/// Composes the application screens and translates application workflows into global navigation.
/// Shared library operations live in <see cref="LibrarySessionViewModel"/>, not in screen history.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly LibraryScreenViewModel _library;
    private readonly GameDetailsScreenViewModel _details;
    private readonly SettingsScreenViewModel _settings;
    private readonly AddGameViewModel _addGame;
    private readonly GameMetadataEditorViewModel _editor;
    private readonly CompatibilitySettingsScreenViewModel _compatibilityEditor;
    private readonly CancellationTokenSource _startupCancellation = new();
    private Task? _steamStartupImport;

    public MainWindowViewModel(
        NavigationCoordinator navigation,
        LibrarySessionViewModel session,
        LibraryScreenViewModel library,
        GameDetailsScreenViewModel details,
        SettingsScreenViewModel settings,
        AddGameViewModel addGame,
        GameMetadataEditorViewModel editor,
        CompatibilitySettingsScreenViewModel compatibilityEditor)
    {
        Navigation = navigation;
        Session = session;
        _library = library;
        _details = details;
        _settings = settings;
        _addGame = addGame;
        _editor = editor;
        _compatibilityEditor = compatibilityEditor;
        _settings.SetSteamLibrarySyncCallback(Session.SynchronizeSteamLibraryAsync);
        _settings.SetRefreshMetadataCommand(Session.RefreshMetadataCommand);
        _settings.SetCanRefreshMetadata(Session.CanRefreshMetadata);
        _settings.SettingsSaved += OnSettingsSaved;
        _settings.SettingsError += OnSettingsError;
        Session.PropertyChanged += OnSessionPropertyChanged;
        Session.GameVisibilityChanged += OnGameVisibilityChanged;
        _library.GameOpened += OpenGame;
        _library.MenuActionRequested += ActivateMenuOption;
        _details.EditRequested += OpenEditor;
        _details.CompatibilityRequested += OpenCompatibilityEditor;
        _details.ManualGameDeleted += ReturnToLibrary;
        _addGame.CancelRequested += CancelNewGame;
        _addGame.CreationCompleted += OnAddGameCreationCompleted;
        _editor.CancelRequested += CancelGameEditing;
        _editor.Saved += OnGameMetadataEditorSaved;
        _compatibilityEditor.CancelRequested += CancelGameEditing;
        _compatibilityEditor.Saved += OnCompatibilitySettingsSaved;
        Navigation.ReturnToMenuRequested += OnReturnToMenuRequested;
        Navigation.PropertyChanged += OnNavigationPropertyChanged;
        Navigation.Initialize(_library);
    }

    public NavigationCoordinator Navigation { get; }

    public LibrarySessionViewModel Session { get; }

    public IReadOnlyList<InputHint> InputHints => Navigation.InputHints;

    public event EventHandler? ExitRequested;

    /// <summary>
    /// Loads preferences before reconciling the local library, without refreshing remote metadata.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _settings.InitializeAsync(cancellationToken);
        await Session.LoadGamesAsync(cancellationToken);
        _steamStartupImport ??= RestoreAndImportSteamLibraryAsync(_startupCancellation.Token);
    }

    private async Task RestoreAndImportSteamLibraryAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await _settings.RestoreSteamSessionAsync(cancellationToken))
            {
                await Session.RescanSteamLibraryAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _settings.ReportSteamRestoreError(exception);
        }
    }

    [RelayCommand]
    private async Task OpenMenuAsync()
    {
        if (Navigation.HasOpenModals || Session.IsGameSessionActive)
        {
            return;
        }

        var menu = new MainMenuViewModel(Session.IsShowingHiddenGames);
        var completion = await Navigation.ShowAsync(menu);
        if (completion.Outcome == ModalOutcome.Confirmed)
        {
            ActivateMenuOption(completion.GetConfirmedValue());
        }
    }

    /// <summary>
    /// Applies a built-in menu action after the menu modal has completed and released input ownership.
    /// </summary>
    public void ActivateMenuOption(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.Library:
                Session.ShowAllGames();
                Navigation.Reset(_library);
                break;
            case MenuAction.HiddenGames:
                Session.IsShowingHiddenGames = !Session.IsShowingHiddenGames;
                Navigation.Reset(_library);
                break;
            case MenuAction.AddGame:
                StartNewGame(returnToMenu: true);
                break;
            case MenuAction.Settings:
                Navigation.Navigate(_settings, returnToMenuOnBack: true);
                break;
            case MenuAction.OpenConnectorSettings:
                _settings.SelectConnectorsSection();
                _settings.RequestConnectorsPrimaryActionFocus();
                _settings.ActivateSettingsSection();
                Navigation.Navigate(_settings, returnToMenuOnBack: true);
                break;
            case MenuAction.Exit:
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private void StartNewGame(bool returnToMenu)
    {
        _addGame.Reset();
        _addGame.InitializeSearch(_settings.GetSettings<GeneralSettings>().LanguageTag);
        Session.StatusMessage = null;
        Navigation.Navigate(_addGame, returnToMenu);
    }

    /// <summary>
    /// Opens the manual editor with a prepared executable action through the global screen history.
    /// </summary>
    public void PrepareGameFromExecutable(string executablePath)
    {
        _addGame.Reset();
        _addGame.InitializeSearch(_settings.GetSettings<GeneralSettings>().LanguageTag);
        _addGame.PrepareFromExecutable(executablePath);
        Session.StatusMessage = null;
        Navigation.Navigate(_addGame);
    }

    private void OpenGame(GameCardViewModel game)
    {
        Session.SelectedGameCard = game;
        Session.StatusMessage = null;
        Navigation.Navigate(_details);
    }

    private void ReturnToLibrary()
    {
        Navigation.Reset(_library);
    }

    private void OpenEditor(GameCardViewModel game)
    {
        _editor.Load(game.Game, _settings.GetSettings<GeneralSettings>().LanguageTag);
        Navigation.Navigate(_editor);
    }

    private void OpenCompatibilityEditor(GameCardViewModel game)
    {
        _compatibilityEditor.Load(game.Game);
        Navigation.Navigate(_compatibilityEditor);
    }

    private void CancelNewGame()
    {
        _addGame.Reset();
        Navigation.GoBack();
    }

    private void CancelGameEditing()
    {
        Navigation.GoBack();
    }

    private void OnGameMetadataEditorSaved(object? sender, Game game)
    {
        Session.UpdateGameCard(game);
        Navigation.GoBack();
        Session.StatusMessage = Resources.GameDetailsSaved;
    }

    private void OnCompatibilitySettingsSaved(object? sender, Game game)
    {
        Session.UpdateGameCard(game);
        Navigation.GoBack();
    }

    private async void OnAddGameCreationCompleted(AddGameCreationResult result)
    {
        switch (result)
        {
            case AddGameCreationResult.InvalidName:
                Session.StatusMessage = Resources.EnterGameName;
                break;
            case AddGameCreationResult.InvalidReleaseDate:
                Session.StatusMessage = Resources.InvalidReleaseDate;
                break;
            case AddGameCreationResult.Failed failed:
                Session.StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.SaveGameError, failed.Exception.Message);
                break;
            case AddGameCreationResult.Saved saved:
                await Session.RefreshAfterGameAddedAsync(saved.Game);
                _addGame.Reset();
                Navigation.GoBack(reopenMenu: false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, null);
        }
    }

    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        Session.StatusMessage = Resources.SettingsSaved;
    }

    private void OnSettingsError(string message)
    {
        Session.StatusMessage = message;
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Session.CanRefreshMetadata))
        {
            _settings.SetCanRefreshMetadata(Session.CanRefreshMetadata);
        }
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Navigation.ActiveScreen) or nameof(Navigation.ActiveModal))
        {
            OnPropertyChanged(nameof(InputHints));
        }
    }

    private void OnGameVisibilityChanged(object? sender, EventArgs e)
    {
        Navigation.GoBack(reopenMenu: false);
    }

    private void OnReturnToMenuRequested(object? sender, EventArgs e)
    {
        OpenMenuCommand.Execute(null);
    }

    public void Dispose()
    {
        _startupCancellation.Cancel();
        _startupCancellation.Dispose();
        Navigation.DismissModalChain();
        _settings.SettingsSaved -= OnSettingsSaved;
        _settings.SettingsError -= OnSettingsError;
        Session.PropertyChanged -= OnSessionPropertyChanged;
        Session.GameVisibilityChanged -= OnGameVisibilityChanged;
        _library.GameOpened -= OpenGame;
        _library.MenuActionRequested -= ActivateMenuOption;
        _details.EditRequested -= OpenEditor;
        _details.CompatibilityRequested -= OpenCompatibilityEditor;
        _details.ManualGameDeleted -= ReturnToLibrary;
        _addGame.CancelRequested -= CancelNewGame;
        _addGame.CreationCompleted -= OnAddGameCreationCompleted;
        _editor.CancelRequested -= CancelGameEditing;
        _editor.Saved -= OnGameMetadataEditorSaved;
        _compatibilityEditor.CancelRequested -= CancelGameEditing;
        _compatibilityEditor.Saved -= OnCompatibilitySettingsSaved;
        Navigation.ReturnToMenuRequested -= OnReturnToMenuRequested;
        Navigation.PropertyChanged -= OnNavigationPropertyChanged;
    }
}
