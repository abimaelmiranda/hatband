using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.App.Services;
using Hatband.App.ViewModels.Settings;
using Microsoft.Extensions.Logging;

namespace Hatband.App.ViewModels;

/// <summary>
/// Holds library data, artwork, synchronization progress and game-operation monitoring independently
/// of the active screen. Screen navigation does not stop installations or process tracking.
/// </summary>
public partial class LibrarySessionViewModel : ViewModelBase, IDisposable
{
    private readonly IGameRepository _gameRepository;
    private readonly IGameLibrarySyncService _gameLibrarySyncService;
    private readonly IGameInstallationStateSyncService _gameInstallationStateSyncService;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly IGameManagementService _gameManagementService;
    private readonly GameProcessSessionService _gameProcessSessionService;
    private readonly ILogger<LibrarySessionViewModel> _logger;
    private readonly ArtworkImageLoader _artworkImageLoader;
    private readonly DateTimeDisplayFormatter _dateTimeDisplayFormatter;
    private readonly SettingsScreenViewModel _settingsScreen;
    private readonly LibrarySyncProgressViewModel _librarySyncProgress;
    private List<GameCardViewModel> _allGames = [];
    private readonly Dictionary<Guid, bool> _pendingInstallationStates = [];
    private readonly SemaphoreSlim _installationStateRefreshGate = new(1, 1);
    private readonly Dictionary<Guid, GameCardViewModel> _activeMonitoredGames = [];
    private CancellationTokenSource? _installationPollingCancellation;
    private CancellationTokenSource? _statusMessageTimeoutCancellation;
    private Task? _installationPollingTask;
    private Guid? _activeGameSessionId;

    public LibrarySessionViewModel(
        IGameRepository gameRepository,
        IGameLibrarySyncService gameLibrarySyncService,
        IGameInstallationStateSyncService gameInstallationStateSyncService,
        IHostSystemInfo hostSystemInfo,
        IGameManagementService gameManagementService,
        GameProcessSessionService gameProcessSessionService,
        ILogger<LibrarySessionViewModel> logger,
        ArtworkImageLoader artworkImageLoader,
        DateTimeDisplayFormatter dateTimeDisplayFormatter,
        SettingsScreenViewModel settingsScreen)
    {
        _gameRepository = gameRepository;
        _gameLibrarySyncService = gameLibrarySyncService;
        _gameInstallationStateSyncService = gameInstallationStateSyncService;
        _hostSystemInfo = hostSystemInfo;
        _gameManagementService = gameManagementService;
        _gameProcessSessionService = gameProcessSessionService;
        _logger = logger;
        _artworkImageLoader = artworkImageLoader;
        _dateTimeDisplayFormatter = dateTimeDisplayFormatter;
        _settingsScreen = settingsScreen;
        _librarySyncProgress = new LibrarySyncProgressViewModel();
        _librarySyncProgress.PropertyChanged += OnLibrarySyncProgressPropertyChanged;
        _librarySyncProgress.CompletionError += OnSyncError;
        _settingsScreen.TimeZoneChanged += OnTimeZoneChanged;
    }

    public event EventHandler? GameVisibilityChanged;
    public event EventHandler? GameSessionStarted;
    public event EventHandler? GameSessionEnded;
    public event EventHandler? GameUninstallationCompleted;
    public event EventHandler? GameInstallationCompleted;
    public event EventHandler? SteamFallbackRequested;
    public event Action<bool>? WindowTopmostRequested;

    public bool IsSteamSilentModeEnabled => _settingsScreen.IsSteamSilentModeEnabled;

    private void OnTimeZoneChanged(string timeZone)
    {
        foreach (var game in _allGames)
        {
            game.RefreshTimeZoneDisplay();
        }
    }

    private void OnSyncError(string message)
    {
        StatusMessage = message;
    }

    public void Dispose()
    {
        StopPendingInstallationPolling();
        StopGameProcessMonitoring();
        _statusMessageTimeoutCancellation?.Cancel();
        _settingsScreen.TimeZoneChanged -= OnTimeZoneChanged;
        _librarySyncProgress.PropertyChanged -= OnLibrarySyncProgressPropertyChanged;
        _librarySyncProgress.CompletionError -= OnSyncError;
    }

    [ObservableProperty]
    public partial ObservableCollection<GameCardViewModel> Games { get; set; } = [];

    [ObservableProperty]
    public partial GameCardViewModel? SelectedGameCard { get; set; }

    [ObservableProperty]
    public partial Bitmap? BackgroundImage { get; set; }

    [ObservableProperty]
    public partial Bitmap? PreviousBackgroundImage { get; set; }

    [ObservableProperty]
    public partial double BackgroundImageOpacity { get; set; } = 1;

    [ObservableProperty]
    public partial double PreviousBackgroundImageOpacity { get; set; }

    [ObservableProperty]
    public partial bool IsShowingHiddenGames { get; set; }

    [ObservableProperty]
    public partial bool IsGameSessionActive { get; set; }

    [ObservableProperty]
    public partial string? ActiveGameSessionName { get; set; }

    [ObservableProperty]
    public partial Bitmap? ActiveGameSessionBackgroundImage { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLibraryLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLibrarySyncRunning { get; set; }

    public bool CanRefreshMetadata => !IsLibrarySyncRunning && !IsLibraryEnrichmentRunning;

    [ObservableProperty]
    public partial bool HasCompletedSteamSync { get; set; }


    public bool IsLibraryEnrichmentRunning => _librarySyncProgress.IsLibraryEnrichmentRunning;

    public string? LibraryEnrichmentStatus => _librarySyncProgress.LibraryEnrichmentStatus;

    public double LibraryEnrichmentProgressPercent => _librarySyncProgress.LibraryEnrichmentProgressPercent;

    public bool IsTimeToBeatSyncRunning => _librarySyncProgress.IsTimeToBeatSyncRunning;

    public string? TimeToBeatSyncStatus => _librarySyncProgress.TimeToBeatSyncStatus;

    public double TimeToBeatSyncProgressPercent => _librarySyncProgress.TimeToBeatSyncProgressPercent;


    public bool IsLibraryEmpty => Games.Count == 0 && !IsLibraryBusy;

    public bool IsLibraryBusy => Games.Count == 0 && (IsLibraryLoading || IsLibrarySyncRunning);

    public bool HasEmptyLibraryActions => !IsShowingHiddenGames;

    public string LibraryScreenTitle => IsShowingHiddenGames
        ? Resources.HiddenGamesHeader
        : Resources.LibraryHeader;

    public string LibraryEmptyBreadcrumb => IsShowingHiddenGames
        ? Resources.HiddenGamesBreadcrumb
        : Resources.LibraryBreadcrumb;


    public bool IsPrimaryGameActionInstall => SelectedGameCard?.Game is
    {
        SourceId: GameSourceId.Steam,
        InstallationInfo: null
    };

    public string PrimaryGameActionLabel => IsPrimaryGameActionInstall ? Resources.Install : Resources.Play;

    public bool CanUninstallSelectedGame => SelectedGameCard?.Game is
    {
        SourceId: GameSourceId.Steam,
        InstallationInfo: not null
    };

    public bool IsSelectedGameUninstallPending =>
        SelectedGameCard is { } gameCard &&
        _pendingInstallationStates.TryGetValue(gameCard.Game.Id, out var isInstalled) &&
        !isInstalled;

    public bool IsSelectedGameManagementPending =>
        SelectedGameCard is { } gameCard && _pendingInstallationStates.ContainsKey(gameCard.Game.Id);

    public bool IsPrimaryGameActionEnabled => SelectedGameCard?.IsCompatibleWithHost ?? true;

    public string SelectedGameHiddenActionLabel => SelectedGameCard?.Game.IsHidden == true
        ? Resources.UnhideGame
        : Resources.HideGame;

    public bool IsSyncActivityVisible => IsLibrarySyncRunning || IsLibraryEnrichmentRunning || IsTimeToBeatSyncRunning;

    public bool IsSyncActivityIndeterminate => IsLibrarySyncRunning && !IsLibraryEnrichmentRunning;

    public string SyncActivityStatus
    {
        get
        {
            if (IsLibraryEnrichmentRunning)
            {
                return LibraryEnrichmentStatus ?? Resources.UpdateGames;
            }

            if (IsLibrarySyncRunning)
            {
                return Resources.SteamSyncActivity;
            }

            return TimeToBeatSyncStatus ?? Resources.HltbSyncActivity;
        }
    }

    public double SyncActivityProgressPercent => IsLibraryEnrichmentRunning
        ? LibraryEnrichmentProgressPercent
        : TimeToBeatSyncProgressPercent;

    public string EmptyLibraryTitle => IsShowingHiddenGames
        ? Resources.HiddenGamesEmptyTitle
        : Resources.EmptyTitle;

    public string EmptyLibraryMessage
    {
        get
        {
            if (IsShowingHiddenGames)
            {
                return Resources.HiddenGamesEmptyMessage;
            }

            if (HasCompletedSteamSync)
            {
                return Resources.EmptyAfterSync;
            }

            return Resources.EmptyBeforeSync;
        }
    }

    public string LibraryBusyTitle => IsLibrarySyncRunning
        ? Resources.LibraryBusySyncTitle
        : Resources.LibraryBusyTitle;

    public string LibraryBusyMessage => IsLibrarySyncRunning
        ? Resources.LibraryBusySyncMessage
        : Resources.LibraryBusyMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);


    public void MoveGameSelection(int direction)
    {
        if (Games.Count == 0 || direction == 0)
        {
            return;
        }

        var currentIndex = SelectedGameCard is null ? 0 : Games.IndexOf(SelectedGameCard);
        var nextIndex = Math.Clamp(currentIndex + Math.Sign(direction), 0, Games.Count - 1);
        SelectedGameCard = Games[nextIndex];
    }

    /// <summary>
    /// Persists visibility for an explicit game and updates the currently displayed library filter.
    /// </summary>
    public async Task ToggleGameHiddenAsync(GameCardViewModel gameCard, CancellationToken cancellationToken = default)
    {
        var game = gameCard.Game;
        var isHidden = !game.IsHidden;
        try
        {
            game.IsHidden = isHidden;
            await _gameRepository.UpdateAsync(game, cancellationToken);
            RefreshVisibleGames();
            StatusMessage = isHidden
                ? string.Format(CultureInfo.CurrentCulture, Resources.GameHidden, game.Name)
                : string.Format(CultureInfo.CurrentCulture, Resources.GameUnhidden, game.Name);
            GameVisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.UpdateGameVisibilityError, exception.Message);
        }
    }

    public async Task<bool> DeleteManualGameAsync(
        GameCardViewModel gameCard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameCard);
        if (gameCard.Game.SourceId != GameSourceId.Manual)
        {
            throw new InvalidOperationException("Only manually added games can be deleted from the library.");
        }

        try
        {
            await _gameRepository.DeleteAsync(gameCard.Game.Id, cancellationToken);
            _allGames.RemoveAll(game => game.Game.Id == gameCard.Game.Id);
            RefreshVisibleGames();
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.GameDeleted, gameCard.Name);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.DeleteGameError, exception.Message);
            return false;
        }
    }

    [RelayCommand]
    private async Task RefreshMetadataAsync(CancellationToken cancellationToken)
    {
        if (IsLibrarySyncRunning || IsLibraryEnrichmentRunning)
        {
            return;
        }

        IsLibrarySyncRunning = true;
        _librarySyncProgress.BeginSync(Resources.Brand);
        try
        {
            await _gameLibrarySyncService.RefreshMetadataAsync(cancellationToken);
            await LoadGamesAsync(cancellationToken);
            _librarySyncProgress.CompleteSync(Resources.Brand);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _librarySyncProgress.CompleteSync(Resources.Brand, exception);
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.LibraryEnrichmentError, exception.Message);
        }
        finally
        {
            IsLibrarySyncRunning = false;
        }
    }

    public void ShowAllGames()
    {
        IsShowingHiddenGames = false;
    }

    /// <summary>
    /// Launches the given game or begins installation. A returned location list requests user choice;
    /// null means the action finished or was reported through the shared status message.
    /// </summary>
    public async Task<IReadOnlyList<GameInstallLocation>?> ActivatePrimaryGameActionAsync(
        GameCardViewModel gameCard,
        CancellationToken cancellationToken = default)
    {
        var game = gameCard.Game;
        var isInstall = game.SourceId == GameSourceId.Steam && game.InstallationInfo is null;
        try
        {
            if (isInstall)
            {
                var installLocations = await _gameManagementService.GetInstallLocationsAsync(game, cancellationToken);
                if (installLocations.Count > 0)
                {
                    return installLocations;
                }

                await InstallGameAsync(game, location: null, cancellationToken);
                return null;
            }

            var processWatchTarget = _gameManagementService.GetProcessWatchTarget(game);
            var result = await _gameManagementService.LaunchAsync(game, cancellationToken);
            var action = GameManagementAction.Launch;
            StatusMessage = GetGameManagementStatus(result, action);

            if (processWatchTarget is not null &&
                result is GameManagementResult.ProtocolOpened or
                    GameManagementResult.SilentCommandStarted or
                    GameManagementResult.GameActionStarted)
            {
                StartGameProcessMonitoring(gameCard, processWatchTarget);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.GameManagementError, exception.Message);
        }

        return null;
    }

    /// <summary>
    /// Starts installation and monitors its resulting state. A null location selects the provider default.
    /// </summary>
    public async Task InstallGameAsync(Game game, GameInstallLocation? location, CancellationToken cancellationToken)
    {
        var shouldRequestTopmost = IsSteamSilentModeEnabled && game.SourceId == GameSourceId.Steam;
        if (shouldRequestTopmost)
        {
            WindowTopmostRequested?.Invoke(true);
        }

        GameManagementResult result;
        try
        {
            result = await _gameManagementService.InstallAsync(game, location, cancellationToken);
        }
        finally
        {
            if (shouldRequestTopmost)
            {
                WindowTopmostRequested?.Invoke(false);
            }
        }

        StatusMessage = GetGameManagementStatus(result, GameManagementAction.Install);
        if (result is GameManagementResult.FallbackProtocolOpened or
            GameManagementResult.FallbackStoreClientOpened or
            GameManagementResult.FallbackUnavailable)
        {
            SteamFallbackRequested?.Invoke(this, EventArgs.Empty);
        }

        if (result is GameManagementResult.ProtocolOpened or
            GameManagementResult.FallbackProtocolOpened or
            GameManagementResult.FallbackStoreClientOpened or
            GameManagementResult.SilentCommandStarted or
            GameManagementResult.StoreClientOpened)
        {
            StartInstallationStatePolling(game.Id, isInstalled: true);
        }
    }

    public void StopGameProcessMonitoring()
    {
        _gameProcessSessionService.Stop();
        _activeMonitoredGames.Clear();
    }

    private void StartGameProcessMonitoring(GameCardViewModel gameCard, GameProcessWatchTarget target)
    {
        _gameProcessSessionService.Watch(
            gameCard.Game.Id,
            target,
            monitorEvent => ApplyGameProcessMonitorEvent(gameCard, monitorEvent));
        _ = LoadGameSessionBackgroundAsync(gameCard);
    }

    private async Task LoadGameSessionBackgroundAsync(GameCardViewModel gameCard)
    {
        try
        {
            var image = gameCard.BackgroundImage ?? await _artworkImageLoader.LoadAsync(gameCard.BackgroundSource);
            if (image is null)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                gameCard.BackgroundImage = image;
                if (_activeGameSessionId == gameCard.Game.Id)
                {
                    ActiveGameSessionBackgroundImage = image;
                }
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load the game session background for game {GameId}.", gameCard.Game.Id);
        }
    }

    private void ApplyGameProcessMonitorEvent(GameCardViewModel gameCard, GameProcessMonitorEvent monitorEvent)
    {
        var gameId = gameCard.Game.Id;
        switch (monitorEvent)
        {
            case GameProcessMonitorEvent.Started:
                var wasAnyGameActive = _activeMonitoredGames.Count > 0;
                _activeMonitoredGames.TryAdd(gameId, gameCard);
                if (!wasAnyGameActive)
                {
                    _activeGameSessionId = gameId;
                    ActiveGameSessionName = gameCard.Name;
                    ActiveGameSessionBackgroundImage = gameCard.BackgroundImage;
                    IsGameSessionActive = true;
                    GameSessionStarted?.Invoke(this, EventArgs.Empty);
                }

                break;
            case GameProcessMonitorEvent.Stopped:
            case GameProcessMonitorEvent.Failed:
                if (_activeMonitoredGames.Remove(gameId) && _activeMonitoredGames.Count == 0)
                {
                    ActiveGameSessionName = null;
                    ActiveGameSessionBackgroundImage = null;
                    _activeGameSessionId = null;
                    IsGameSessionActive = false;
                    GameSessionEnded?.Invoke(this, EventArgs.Empty);
                }
                else if (_activeMonitoredGames.Count > 0)
                {
                    var activeGame = _activeMonitoredGames.Values.First();
                    _activeGameSessionId = activeGame.Game.Id;
                    ActiveGameSessionName = activeGame.Name;
                    ActiveGameSessionBackgroundImage = activeGame.BackgroundImage;
                }

                break;
            case GameProcessMonitorEvent.StartTimedOut:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(monitorEvent), monitorEvent, null);
        }
    }

    /// <summary>
    /// Requests removal of the explicit game and keeps monitoring until local reconciliation confirms it.
    /// </summary>
    public async Task UninstallGameAsync(GameCardViewModel gameCard, CancellationToken cancellationToken = default)
    {
        if (gameCard.Game.SourceId != GameSourceId.Steam || gameCard.Game.InstallationInfo is null)
        {
            return;
        }

        try
        {
            var game = gameCard.Game;
            var shouldRequestTopmost = IsSteamSilentModeEnabled && game.SourceId == GameSourceId.Steam;
            if (shouldRequestTopmost)
            {
                WindowTopmostRequested?.Invoke(true);
            }

            GameManagementResult result;
            try
            {
                result = await _gameManagementService.UninstallAsync(game, cancellationToken);
            }
            finally
            {
                if (shouldRequestTopmost)
                {
                    WindowTopmostRequested?.Invoke(false);
                }
            }

            StatusMessage = GetGameManagementStatus(result, GameManagementAction.Uninstall);
            if (result is GameManagementResult.FallbackProtocolOpened or
                GameManagementResult.FallbackStoreClientOpened or
                GameManagementResult.FallbackUnavailable)
            {
                SteamFallbackRequested?.Invoke(this, EventArgs.Empty);
            }

            if (result is GameManagementResult.ProtocolOpened or
                GameManagementResult.FallbackProtocolOpened or
                GameManagementResult.FallbackStoreClientOpened or
                GameManagementResult.SilentCommandStarted or
                GameManagementResult.StoreClientOpened)
            {
                StartInstallationStatePolling(game.Id, isInstalled: false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.GameManagementError, exception.Message);
        }
    }

    public async Task RefreshPendingInstallationStatesAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingInstallationStates.Count == 0)
        {
            return;
        }

        await _installationStateRefreshGate.WaitAsync(cancellationToken);
        try
        {
            if (_pendingInstallationStates.Count == 0)
            {
                return;
            }

            await _gameInstallationStateSyncService.RefreshAsync(cancellationToken);
            var refreshedGames = await _gameRepository.GetAllAsync(cancellationToken);
            var gamesById = refreshedGames.ToDictionary(game => game.Id);
            foreach (var gameCard in _allGames.ToArray())
            {
                if (gamesById.TryGetValue(gameCard.Game.Id, out var refreshedGame) &&
                    gameCard.Game.InstallationInfo != refreshedGame.InstallationInfo)
                {
                    UpdateGameCard(refreshedGame);
                }
            }

            foreach (var (gameId, isInstalled) in _pendingInstallationStates.ToArray())
            {
                if (gamesById.TryGetValue(gameId, out var refreshedGame) && (refreshedGame.InstallationInfo is not null) == isInstalled)
                {
                    _pendingInstallationStates.Remove(gameId);
                    OnPropertyChanged(nameof(IsSelectedGameUninstallPending));
                    OnPropertyChanged(nameof(IsSelectedGameManagementPending));
                    if (isInstalled)
                    {
                        GameInstallationCompleted?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        GameUninstallationCompleted?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resources.GameInstallationRefreshError,
                exception.Message);
        }
        finally
        {
            _installationStateRefreshGate.Release();
        }
    }

    public void StopPendingInstallationPolling()
    {
        _installationPollingCancellation?.Cancel();
    }

    private void StartInstallationStatePolling(Guid gameId, bool isInstalled)
    {
        _pendingInstallationStates[gameId] = isInstalled;
        OnPropertyChanged(nameof(IsSelectedGameUninstallPending));
        OnPropertyChanged(nameof(IsSelectedGameManagementPending));
        if (_installationPollingTask is { IsCompleted: false })
        {
            return;
        }

        _installationPollingCancellation?.Dispose();
        _installationPollingCancellation = new CancellationTokenSource();
        _installationPollingTask = PollInstallationStatesAsync(_installationPollingCancellation.Token);
    }

    private async Task PollInstallationStatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_pendingInstallationStates.Count > 0)
            {
                await RefreshPendingInstallationStatesAsync(cancellationToken);
                if (_pendingInstallationStates.Count == 0)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _installationPollingCancellation?.Dispose();
            _installationPollingCancellation = null;
            _installationPollingTask = null;
        }
    }

    private static string GetGameManagementStatus(GameManagementResult result, GameManagementAction action)
    {
        return result switch
        {
            GameManagementResult.GameActionStarted when action == GameManagementAction.Launch => Resources.GameLaunchStarted,
            GameManagementResult.ProtocolOpened or
            GameManagementResult.FallbackProtocolOpened or
            GameManagementResult.SilentCommandStarted => action switch
            {
                GameManagementAction.Install => Resources.SteamInstallRequested,
                GameManagementAction.Uninstall => Resources.SteamUninstallRequested,
                GameManagementAction.Launch => Resources.GameLaunchRequested,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            },
            GameManagementResult.StoreClientOpened or GameManagementResult.FallbackStoreClientOpened => Resources.StoreClientOpened,
            GameManagementResult.Unavailable or GameManagementResult.FallbackUnavailable => Resources.GameManagementUnavailable,
            GameManagementResult.Unsupported => Resources.GameManagementUnsupported,
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    /// <summary>
    /// Reconciles local installations and reads the saved catalog. Remote metadata enrichment is separate.
    /// </summary>
    public async Task LoadGamesAsync(CancellationToken cancellationToken = default)
    {
        IsLibraryLoading = true;


        try
        {
            try
            {
                await _gameInstallationStateSyncService.RefreshAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                StatusMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.GameInstallationRefreshError,
                    exception.Message);
            }

            var savedGames = await _gameRepository.GetAllAsync(cancellationToken);
            SetGames(savedGames.Select(game => new GameCardViewModel(game, _dateTimeDisplayFormatter, _hostSystemInfo)));

            await Task.WhenAll(_allGames.Select(game => game.LoadCoverAsync(_artworkImageLoader)));
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.LoadLibraryError, exception.Message);
        }
        finally
        {
            IsLibraryLoading = false;
        }
    }

    private void SetGames(IEnumerable<GameCardViewModel> games)
    {
        _allGames = games.ToList();
        RefreshVisibleGames();
    }

    private void RefreshVisibleGames()
    {
        var selectedGameId = SelectedGameCard?.Game.Id;
        var visibleGames = _allGames
            .Where(game => game.Game.IsHidden == IsShowingHiddenGames)
            .ToList();

        Games = new ObservableCollection<GameCardViewModel>(visibleGames);
        OnLibraryActivityChanged();
        var previousSelection = Games.FirstOrDefault(game => game.Game.Id == selectedGameId);
        SelectedGameCard = previousSelection ?? Games.FirstOrDefault();
    }

    private void OnLibraryActivityChanged()
    {
        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(IsLibraryBusy));
    }

    /// <summary>
    /// Reloads persisted cards and selects the new game without changing screen or menu state.
    /// </summary>
    public async Task RefreshAfterGameAddedAsync(Game game)
    {
        try
        {
            var savedGames = await _gameRepository.GetAllAsync();
            SetGames(savedGames.Select(savedGame => new GameCardViewModel(savedGame, _dateTimeDisplayFormatter, _hostSystemInfo)));
            SelectedGameCard = Games.FirstOrDefault(item => item.Game.Id == game.Id);
            await Task.WhenAll(_allGames.Select(item => item.LoadCoverAsync(_artworkImageLoader)));
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.GameAdded, game.Name);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.SaveGameError, exception.Message);
        }
    }

    /// <summary>
    /// Synchronizes the connected Steam catalog while exposing progress to every screen.
    /// </summary>
    public async Task<int> SynchronizeSteamLibraryAsync(CancellationToken cancellationToken)
    {
        IsLibrarySyncRunning = true;
        _librarySyncProgress.BeginSync("Steam");
        StatusMessage = null;
        try
        {
            var games = await _gameLibrarySyncService.SynchronizeAsync(
                GameSourceId.Steam,
                cancellationToken);
            await LoadGamesAsync(cancellationToken);
            HasCompletedSteamSync = true;
            _librarySyncProgress.CompleteSync("Steam");
            return games.Count;
        }
        catch (Exception exception)
        {
            _librarySyncProgress.CompleteSync("Steam", exception);
            throw;
        }
        finally
        {
            IsLibrarySyncRunning = false;
        }
    }

    private void OnLibrarySyncProgressPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(LibrarySyncProgressViewModel.IsLibraryEnrichmentRunning):
                OnPropertyChanged(nameof(IsLibraryEnrichmentRunning));
                OnPropertyChanged(nameof(CanRefreshMetadata));
                OnPropertyChanged(nameof(IsSyncActivityVisible));
                OnPropertyChanged(nameof(IsSyncActivityIndeterminate));
                OnPropertyChanged(nameof(SyncActivityStatus));
                OnPropertyChanged(nameof(SyncActivityProgressPercent));
                break;
            case nameof(LibrarySyncProgressViewModel.LibraryEnrichmentStatus):
                OnPropertyChanged(nameof(LibraryEnrichmentStatus));
                OnPropertyChanged(nameof(SyncActivityStatus));
                break;
            case nameof(LibrarySyncProgressViewModel.LibraryEnrichmentProgressPercent):
                OnPropertyChanged(nameof(LibraryEnrichmentProgressPercent));
                OnPropertyChanged(nameof(SyncActivityProgressPercent));
                break;
            case nameof(LibrarySyncProgressViewModel.IsTimeToBeatSyncRunning):
                OnPropertyChanged(nameof(IsTimeToBeatSyncRunning));
                OnPropertyChanged(nameof(IsSyncActivityVisible));
                OnPropertyChanged(nameof(SyncActivityStatus));
                OnPropertyChanged(nameof(SyncActivityProgressPercent));
                break;
            case nameof(LibrarySyncProgressViewModel.TimeToBeatSyncStatus):
                OnPropertyChanged(nameof(TimeToBeatSyncStatus));
                OnPropertyChanged(nameof(SyncActivityStatus));
                break;
            case nameof(LibrarySyncProgressViewModel.TimeToBeatSyncProgressPercent):
                OnPropertyChanged(nameof(TimeToBeatSyncProgressPercent));
                OnPropertyChanged(nameof(SyncActivityProgressPercent));
                break;
        }
    }

    public void UpdateGameCard(Game game)
    {
        var allGamesIndex = _allGames.FindIndex(card => card.Game.Id == game.Id);
        if (allGamesIndex < 0)
        {
            return;
        }

        var previousCard = _allGames[allGamesIndex];
        var wasSelected = ReferenceEquals(SelectedGameCard, previousCard);
        game.IsHidden = previousCard.Game.IsHidden;
        var card = new GameCardViewModel(game, _dateTimeDisplayFormatter, _hostSystemInfo)
        {
            IsSelected = wasSelected
        };
        _allGames[allGamesIndex] = card;

        var visibleGameIndex = Games.IndexOf(previousCard);
        if (visibleGameIndex >= 0)
        {
            Games[visibleGameIndex] = card;
        }

        if (wasSelected && visibleGameIndex >= 0)
        {
            SelectedGameCard = card;
        }

        _ = card.LoadCoverAsync(_artworkImageLoader);
    }

    partial void OnIsShowingHiddenGamesChanged(bool value)
    {
        RefreshVisibleGames();
        OnPropertyChanged(nameof(LibraryScreenTitle));
        OnPropertyChanged(nameof(LibraryEmptyBreadcrumb));
        OnPropertyChanged(nameof(EmptyLibraryTitle));
        OnPropertyChanged(nameof(EmptyLibraryMessage));
        OnPropertyChanged(nameof(HasEmptyLibraryActions));
        OnLibraryActivityChanged();
    }

    partial void OnIsLibraryLoadingChanged(bool value)
    {
        OnLibraryActivityChanged();
    }

    partial void OnIsLibrarySyncRunningChanged(bool value)
    {
        _settingsScreen.SetLibrarySyncRunning(value);
        OnPropertyChanged(nameof(CanRefreshMetadata));
        OnPropertyChanged(nameof(LibraryBusyTitle));
        OnPropertyChanged(nameof(LibraryBusyMessage));
        OnPropertyChanged(nameof(IsSyncActivityVisible));
        OnPropertyChanged(nameof(IsSyncActivityIndeterminate));
        OnPropertyChanged(nameof(SyncActivityStatus));
        OnLibraryActivityChanged();
    }

    partial void OnHasCompletedSteamSyncChanged(bool value)
    {
        OnPropertyChanged(nameof(EmptyLibraryTitle));
        OnPropertyChanged(nameof(EmptyLibraryMessage));
    }

    partial void OnSelectedGameCardChanged(GameCardViewModel? value)
    {
        OnPropertyChanged(nameof(IsPrimaryGameActionInstall));
        OnPropertyChanged(nameof(PrimaryGameActionLabel));
        OnPropertyChanged(nameof(IsPrimaryGameActionEnabled));
        OnPropertyChanged(nameof(CanUninstallSelectedGame));
        OnPropertyChanged(nameof(IsSelectedGameUninstallPending));
        OnPropertyChanged(nameof(IsSelectedGameManagementPending));
        OnPropertyChanged(nameof(SelectedGameHiddenActionLabel));

        foreach (var game in _allGames)
        {
            game.IsSelected = ReferenceEquals(game, value);
        }

        if (value is not null)
        {
            _ = LoadBackgroundAsync(value);
        }
    }

    private async Task LoadBackgroundAsync(GameCardViewModel game)
    {
        PreviousBackgroundImage = BackgroundImage;
        PreviousBackgroundImageOpacity = PreviousBackgroundImage is null ? 0 : 1;
        BackgroundImageOpacity = 0;

        var image = await _artworkImageLoader.LoadAsync(game.BackgroundSource);
        if (!ReferenceEquals(SelectedGameCard, game))
        {
            return;
        }

        BackgroundImage = image;
        BackgroundImageOpacity = 1;
        PreviousBackgroundImageOpacity = 0;
    }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));

        _statusMessageTimeoutCancellation?.Cancel();
        _statusMessageTimeoutCancellation?.Dispose();
        _statusMessageTimeoutCancellation = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var timeoutCancellation = new CancellationTokenSource();
        _statusMessageTimeoutCancellation = timeoutCancellation;
        _ = ClearStatusMessageAfterDelayAsync(value, timeoutCancellation);
    }

    private async Task ClearStatusMessageAfterDelayAsync(
        string message,
        CancellationTokenSource timeoutCancellation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), timeoutCancellation.Token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ReferenceEquals(_statusMessageTimeoutCancellation, timeoutCancellation) &&
                    StatusMessage == message)
                {
                    StatusMessage = null;
                }
            });
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_statusMessageTimeoutCancellation, timeoutCancellation))
            {
                _statusMessageTimeoutCancellation.Dispose();
                _statusMessageTimeoutCancellation = null;
            }
        }
    }
}
