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
using Hatband.Core.Enums.Artwork;
using Hatband.Core.Models.Settings;
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
    private readonly IGameSourceCatalog _sourceCatalog;
    private readonly IReadOnlyDictionary<string, IGameManagementPresentationProvider> _presentationProviders;
    private readonly GameProcessSessionService _gameProcessSessionService;
    private readonly ILogger<LibrarySessionViewModel> _logger;
    private readonly ArtworkImageLoader _artworkImageLoader;
    private readonly DateTimeDisplayFormatter _dateTimeDisplayFormatter;
    private readonly SettingsScreenViewModel _settingsScreen;
    private readonly LibrarySyncProgressViewModel _librarySyncProgress;
    private readonly LibraryLogoNormalizationViewModel _libraryLogoNormalization;
    private List<GameCardViewModel> _allGames = [];
    private readonly Dictionary<Guid, bool> _pendingInstallationStates = [];
    private readonly HashSet<Guid> _pendingLaunchGameIds = [];
    private readonly SemaphoreSlim _installationStateRefreshGate = new(1, 1);
    private readonly SemaphoreSlim _librarySyncGate = new(1, 1);
    private readonly Dictionary<Guid, GameCardViewModel> _activeMonitoredGames = [];
    private readonly Dictionary<Guid, GamePreparationActivityViewModel> _preparations = [];
    private readonly Dictionary<Guid, CachedGameLogo> _cachedGameLogos = [];
    private readonly LinkedList<Guid> _gameLogoCacheLru = [];
    private readonly CancellationTokenSource _applicationLifetime = new();
    private CancellationTokenSource? _installationPollingCancellation;
    private CancellationTokenSource? _statusMessageTimeoutCancellation;
    private CancellationTokenSource? _selectedLogoCancellation;
    private CancellationTokenSource? _selectedBackgroundCancellation;
    private long _selectedLogoRevision;
    private long _selectedBackgroundRevision;
    private GameCardViewModel? _selectedLogoCard;
    private bool _normalizeLibraryLogos;
    private bool _showSelectedGameLogos;
    private bool _isLibraryScreenActive;
    private Task? _installationPollingTask;
    private string? _librarySyncSourceName;
    private Guid? _activeGameSessionId;
    private const int MaximumCachedGameLogos = 8;

    private sealed record CachedGameLogo(
        string? Source,
        bool NormalizationRequested,
        bool IsNormalized,
        Bitmap Image,
        LinkedListNode<Guid> Node);

    public LibrarySessionViewModel(
        IGameRepository gameRepository,
        IGameLibrarySyncService gameLibrarySyncService,
        IGameInstallationStateSyncService gameInstallationStateSyncService,
        IHostSystemInfo hostSystemInfo,
        IGameManagementService gameManagementService,
        IGameSourceCatalog sourceCatalog,
        IEnumerable<IGameManagementPresentationProvider> presentationProviders,
        GameProcessSessionService gameProcessSessionService,
        ILogger<LibrarySessionViewModel> logger,
        ILogger<LibraryLogoNormalizationViewModel> logoNormalizationLogger,
        IGameLogoNormalizationService gameLogoNormalizationService,
        ArtworkImageLoader artworkImageLoader,
        DateTimeDisplayFormatter dateTimeDisplayFormatter,
        SettingsScreenViewModel settingsScreen)
    {
        _gameRepository = gameRepository;
        _gameLibrarySyncService = gameLibrarySyncService;
        _gameInstallationStateSyncService = gameInstallationStateSyncService;
        _hostSystemInfo = hostSystemInfo;
        _gameManagementService = gameManagementService;
        _sourceCatalog = sourceCatalog;
        _presentationProviders = presentationProviders.ToDictionary(provider => provider.SourceId, StringComparer.Ordinal);
        _gameProcessSessionService = gameProcessSessionService;
        _logger = logger;
        _artworkImageLoader = artworkImageLoader;
        _dateTimeDisplayFormatter = dateTimeDisplayFormatter;
        _settingsScreen = settingsScreen;
        _librarySyncProgress = new LibrarySyncProgressViewModel();
        _libraryLogoNormalization = new LibraryLogoNormalizationViewModel(
            gameLogoNormalizationService,
            logoNormalizationLogger,
            _applicationLifetime.Token);
        _librarySyncProgress.PropertyChanged += OnLibrarySyncProgressPropertyChanged;
        _librarySyncProgress.CompletionError += OnSyncError;
        _libraryLogoNormalization.PropertyChanged += OnLibraryLogoNormalizationPropertyChanged;
        _libraryLogoNormalization.GameCompleted += OnGameLogoNormalizationCompleted;
        _settingsScreen.TimeZoneChanged += OnTimeZoneChanged;
        _settingsScreen.AppearanceSettingsChanged += OnAppearanceSettingsChanged;
    }

    public event EventHandler? GameVisibilityChanged;
    public event EventHandler? GameSessionStarted;
    public event EventHandler? GameSessionEnded;
    public event EventHandler? GameUninstallationCompleted;
    public event EventHandler? GameInstallationCompleted;
    public event EventHandler? StoreFallbackRequested;
    public event Action<bool>? WindowTopmostRequested;

    public void RefreshSelectedGameLogo()
    {
        _isLibraryScreenActive = true;
        if (SelectedGameCard is { } gameCard && _showSelectedGameLogos)
        {
            _ = LoadSelectedGameLogoAsync(gameCard);
        }
    }

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
        _applicationLifetime.Cancel();
        _libraryLogoNormalization.Dispose();
        _applicationLifetime.Dispose();
        _selectedLogoCancellation?.Cancel();
        _selectedBackgroundCancellation?.Cancel();
        ClearGameLogoCache();
        foreach (var gameCard in _allGames)
        {
            gameCard.LogoImage = null;
        }

        BackgroundImage?.Dispose();
        if (PreviousBackgroundImage is not null && !ReferenceEquals(PreviousBackgroundImage, BackgroundImage))
        {
            PreviousBackgroundImage.Dispose();
        }

        BackgroundImage = null;
        PreviousBackgroundImage = null;
        StopPendingInstallationPolling();
        StopGameProcessMonitoring();
        _statusMessageTimeoutCancellation?.Cancel();
        _settingsScreen.TimeZoneChanged -= OnTimeZoneChanged;
        _settingsScreen.AppearanceSettingsChanged -= OnAppearanceSettingsChanged;
        _librarySyncProgress.PropertyChanged -= OnLibrarySyncProgressPropertyChanged;
        _librarySyncProgress.CompletionError -= OnSyncError;
        _libraryLogoNormalization.PropertyChanged -= OnLibraryLogoNormalizationPropertyChanged;
        _libraryLogoNormalization.GameCompleted -= OnGameLogoNormalizationCompleted;
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

    public GamePreparationActivityViewModel? SelectedGamePreparationActivity
    {
        get
        {
            if (SelectedGameCard is not { } gameCard)
            {
                return null;
            }

            return _preparations.GetValueOrDefault(gameCard.Game.Id);
        }
    }

    public bool IsSelectedGamePreparing => SelectedGamePreparationActivity is not null;

    public bool IsSelectedGameManagementPendingWithoutPreparation =>
        IsSelectedGameManagementPending && !IsSelectedGamePreparing;

    private void NotifyGamePreparationChanged()
    {
        OnPropertyChanged(nameof(SelectedGamePreparationActivity));
        OnPropertyChanged(nameof(IsSelectedGamePreparing));
        OnPropertyChanged(nameof(IsSelectedGameManagementPendingWithoutPreparation));
    }

    [ObservableProperty]
    public partial bool IsLibraryLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLibrarySyncRunning { get; set; }

    public bool CanRefreshMetadata => !IsLibrarySyncRunning && !IsLibraryEnrichmentRunning;

    [ObservableProperty]
    public partial bool HasCompletedLibrarySync { get; set; }


    public bool IsLibraryEnrichmentRunning => _librarySyncProgress.IsLibraryEnrichmentRunning;

    public string? LibraryEnrichmentStatus => _librarySyncProgress.LibraryEnrichmentStatus;

    public double LibraryEnrichmentProgressPercent => _librarySyncProgress.LibraryEnrichmentProgressPercent;

    public bool IsTimeToBeatSyncRunning => _librarySyncProgress.IsTimeToBeatSyncRunning;

    public string? TimeToBeatSyncStatus => _librarySyncProgress.TimeToBeatSyncStatus;

    public double TimeToBeatSyncProgressPercent => _librarySyncProgress.TimeToBeatSyncProgressPercent;

    public bool IsLibraryLogoNormalizationRunning => _libraryLogoNormalization.IsRunning;

    public string? LibraryLogoNormalizationStatus => _libraryLogoNormalization.Status;

    public double LibraryLogoNormalizationProgressPercent => _libraryLogoNormalization.ProgressPercent;


    public bool IsLibraryEmpty => Games.Count == 0 && !IsLibraryBusy;

    public bool IsLibraryBusy => Games.Count == 0 && (IsLibraryLoading || IsLibrarySyncRunning);

    public bool HasEmptyLibraryActions => !IsShowingHiddenGames;

    public string LibraryScreenTitle => IsShowingHiddenGames
        ? Resources.HiddenGamesHeader
        : Resources.LibraryHeader;

    public string LibraryEmptyBreadcrumb => IsShowingHiddenGames
        ? Resources.HiddenGamesBreadcrumb
        : Resources.LibraryBreadcrumb;


    public bool IsPrimaryGameActionInstall => SelectedGameCard?.Game is { } game &&
        game.SourceId != GameSourceIds.Manual &&
        game.InstallationInfo is null;

    public string PrimaryGameActionLabel => IsPrimaryGameActionInstall ? Resources.Install : Resources.Play;

    public bool CanUninstallSelectedGame => SelectedGameCard?.Game is { } game &&
        game.SourceId != GameSourceIds.Manual &&
        game.InstallationInfo is not null;

    public bool IsSelectedGameUninstallPending =>
        SelectedGameCard is { } gameCard &&
        _pendingInstallationStates.TryGetValue(gameCard.Game.Id, out var isInstalled) &&
        !isInstalled;

    public bool IsSelectedGameManagementPending
    {
        get
        {
            var gameCard = SelectedGameCard;
            if (gameCard is null)
            {
                return false;
            }

            var gameId = gameCard.Game.Id;
            return _pendingInstallationStates.ContainsKey(gameId) || _pendingLaunchGameIds.Contains(gameId);
        }
    }

    public bool IsPrimaryGameActionEnabled
    {
        get
        {
            var gameCard = SelectedGameCard;
            if (gameCard is null)
            {
                return true;
            }

            if (!gameCard.IsCompatibleWithHost || IsSelectedGameManagementPending)
            {
                return false;
            }

            var game = gameCard.Game;
            if (_hostSystemInfo.Platform != HostOperatingSystem.Linux ||
                game.CompatibilityLayer is not { ExecutionMode: GameExecutionMode.Custom, Tool: not null })
            {
                return true;
            }

            return !_activeMonitoredGames.ContainsKey(game.Id);
        }
    }

    public string SelectedGameHiddenActionLabel => SelectedGameCard?.Game.IsHidden == true
        ? Resources.UnhideGame
        : Resources.HideGame;

    public bool IsSyncActivityVisible => IsLibrarySyncRunning || IsLibraryEnrichmentRunning ||
        IsTimeToBeatSyncRunning || IsLibraryLogoNormalizationRunning;

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
                return string.Format(CultureInfo.CurrentCulture, Resources.ConnectorSyncActivity, _librarySyncSourceName);
            }

            if (IsTimeToBeatSyncRunning)
            {
                return TimeToBeatSyncStatus ?? Resources.HltbSyncActivity;
            }

            return LibraryLogoNormalizationStatus ?? TimeToBeatSyncStatus ?? Resources.HltbSyncActivity;
        }
    }

    public double SyncActivityProgressPercent
    {
        get
        {
            if (IsLibraryEnrichmentRunning)
            {
                return LibraryEnrichmentProgressPercent;
            }

            if (IsLibrarySyncRunning)
            {
                return 0;
            }

            if (IsTimeToBeatSyncRunning)
            {
                return TimeToBeatSyncProgressPercent;
            }

            return IsLibraryLogoNormalizationRunning
                ? LibraryLogoNormalizationProgressPercent
                : TimeToBeatSyncProgressPercent;
        }
    }

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

            if (HasCompletedLibrarySync)
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
        if (gameCard.Game.SourceId != GameSourceIds.Manual)
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

    public async Task RefreshGameDataAsync(
        GameCardViewModel gameCard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameCard);
        if (IsLibrarySyncRunning || IsLibraryEnrichmentRunning)
        {
            return;
        }

        IsLibrarySyncRunning = true;
        _librarySyncProgress.BeginSync(gameCard.Name);
        try
        {
            await _gameLibrarySyncService.RefreshGameDataAsync(gameCard.Game.Id, cancellationToken);
            await LoadGamesAsync(cancellationToken);
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.GameDataUpdated, gameCard.Name);
            _librarySyncProgress.CompleteSync(gameCard.Name);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _librarySyncProgress.CompleteSync(gameCard.Name, exception);
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
        var isInstall = game.SourceId != GameSourceIds.Manual && game.InstallationInfo is null;
        var isCustomLinuxLaunch = _hostSystemInfo.Platform == HostOperatingSystem.Linux &&
            game.CompatibilityLayer?.ExecutionMode == GameExecutionMode.Custom;
        if (_pendingLaunchGameIds.Contains(game.Id) ||
            (isCustomLinuxLaunch && _activeMonitoredGames.ContainsKey(game.Id)))
        {
            return null;
        }

        var processMonitoringStarted = false;
        GamePreparationActivityViewModel? preparation = null;
        if (isCustomLinuxLaunch && !isInstall)
        {
            cancellationToken.ThrowIfCancellationRequested();
            preparation = new GamePreparationActivityViewModel(game.Name, _applicationLifetime.Token);
            _preparations.Add(game.Id, preparation);
            NotifyGamePreparationChanged();
            _pendingLaunchGameIds.Add(game.Id);
            NotifyGameLaunchStateChanged();
            StatusMessage = null;
        }

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

            IProgress<GamePreparationProgress>? progress = null;
            if (preparation is { } activity)
            {
                progress = new Progress<GamePreparationProgress>(update =>
                {
                    if (_preparations.TryGetValue(game.Id, out var current) && ReferenceEquals(current, activity))
                    {
                        activity.Report(update);
                    }
                });
            }

            var operationToken = preparation is null ? cancellationToken : preparation.CancellationToken;
            var launchResult = await _gameManagementService.LaunchAsync(game, progress, operationToken);
            var result = launchResult.Result;
            var action = GameManagementAction.Launch;
            StatusMessage = GetGameManagementStatus(result, action);

            if (launchResult.WatchTarget is { } processWatchTarget &&
                result is GameManagementResult.ProtocolOpened or
                    GameManagementResult.SilentCommandStarted or
                    GameManagementResult.GameActionStarted)
            {
                StartGameProcessMonitoring(gameCard, processWatchTarget);
                processMonitoringStarted = true;
            }
        }
        catch (OperationCanceledException) when (preparation?.CancellationToken.IsCancellationRequested == true)
        {
            StatusMessage = Resources.GamePreparationCanceled;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not launch game {GameId}.", game.Id);
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.GameManagementError, exception.Message);
        }
        finally
        {
            if (preparation is not null)
            {
                _preparations.Remove(game.Id);
                NotifyGamePreparationChanged();

                preparation.Dispose();
            }

            if (!processMonitoringStarted)
            {
                _pendingLaunchGameIds.Remove(game.Id);
                NotifyGameLaunchStateChanged();
            }
        }

        return null;
    }

    /// <summary>
    /// Starts installation and monitors its resulting state. A null location selects the provider default.
    /// </summary>
    public async Task InstallGameAsync(Game game, GameInstallLocation? location, CancellationToken cancellationToken)
    {
        var shouldRequestTopmost = await ShouldKeepHostInForegroundAsync(game, cancellationToken);
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
            StoreFallbackRequested?.Invoke(this, EventArgs.Empty);
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
        _pendingLaunchGameIds.Clear();
        NotifyGameLaunchStateChanged();
    }

    private void NotifyGameLaunchStateChanged()
    {
        OnPropertyChanged(nameof(IsSelectedGameManagementPending));
        NotifyGamePreparationChanged();
        OnPropertyChanged(nameof(IsPrimaryGameActionEnabled));
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
        if (monitorEvent != GameProcessMonitorEvent.StartTimedOut)
        {
            _pendingLaunchGameIds.Remove(gameId);
        }
        switch (monitorEvent)
        {
            case GameProcessMonitorEvent.Started:
                if (gameCard.Game.GetCompatibilityTool() is not null)
                {
                    StatusMessage = Resources.GameLaunchStarted;
                }
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
                if (monitorEvent == GameProcessMonitorEvent.Failed)
                {
                    StatusMessage = Resources.GameLaunchFailed;
                }
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
                StatusMessage = Resources.GameLaunchTimedOut;
                break;
            case GameProcessMonitorEvent.StartNotObserved:
                StatusMessage = Resources.GameLaunchNotObserved;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(monitorEvent), monitorEvent, null);
        }

        NotifyGameLaunchStateChanged();
    }

    /// <summary>
    /// Requests removal of the explicit game and keeps monitoring until local reconciliation confirms it.
    /// </summary>
    public async Task UninstallGameAsync(GameCardViewModel gameCard, CancellationToken cancellationToken = default)
    {
        if (gameCard.Game.SourceId == GameSourceIds.Manual || gameCard.Game.InstallationInfo is null)
        {
            return;
        }

        try
        {
            var game = gameCard.Game;
            var shouldRequestTopmost = await ShouldKeepHostInForegroundAsync(game, cancellationToken);
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
                StoreFallbackRequested?.Invoke(this, EventArgs.Empty);
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
                    NotifyGamePreparationChanged();
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
        NotifyGamePreparationChanged();
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

    private async Task<bool> ShouldKeepHostInForegroundAsync(Game game, CancellationToken cancellationToken)
    {
        if (!_presentationProviders.TryGetValue(game.SourceId, out var provider))
        {
            return false;
        }

        return await provider.KeepHostInForegroundAsync(game, cancellationToken);
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
                GameManagementAction.Install => Resources.GameInstallRequested,
                GameManagementAction.Uninstall => Resources.GameUninstallRequested,
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
        ClearGameLogoCache();
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
    /// Synchronizes a connected store catalog while exposing progress to every screen.
    /// </summary>
    public async Task<int> SynchronizeLibraryAsync(string sourceId, CancellationToken cancellationToken)
    {
        return await SynchronizeLibraryAsync(sourceId, cancellationToken, GameLibrarySyncMode.Full);
    }

    public async Task<int> RescanLibraryAsync(string sourceId, CancellationToken cancellationToken)
    {
        return await SynchronizeLibraryAsync(sourceId, cancellationToken, GameLibrarySyncMode.Incremental);
    }

    private async Task<int> SynchronizeLibraryAsync(
        string sourceId,
        CancellationToken cancellationToken,
        GameLibrarySyncMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        await _librarySyncGate.WaitAsync(cancellationToken);
        try
        {
            _librarySyncSourceName = _sourceCatalog.GetDisplayName(sourceId);
            IsLibrarySyncRunning = true;
            _librarySyncProgress.BeginLibrarySync(_librarySyncSourceName);
            StatusMessage = null;
            var progress = new CallbackProgress<GameLibrarySyncProgress>(ReportLibrarySyncProgress);
            var games = await _gameLibrarySyncService.SynchronizeAsync(
                sourceId,
                progress,
                cancellationToken,
                mode);
            await FinishLibrarySyncAsync(games);
            HasCompletedLibrarySync = true;
            _librarySyncProgress.CompleteSync(_sourceCatalog.GetDisplayName(sourceId));
            return games.Count;
        }
        catch (Exception exception)
        {
            await LoadGamesAsync(CancellationToken.None);
            _librarySyncProgress.CompleteSync(_sourceCatalog.GetDisplayName(sourceId), exception);
            throw;
        }
        finally
        {
            IsLibrarySyncRunning = false;
            _librarySyncSourceName = null;
            _librarySyncGate.Release();
        }
    }

    private async Task FinishLibrarySyncAsync(IReadOnlyList<Game> games)
    {
        _librarySyncProgress.ReportLibrarySyncProgress(new GameLibrarySyncProgress(
            GameLibrarySyncStage.RefreshingLibrary,
            games.Count,
            games.Count));

        await Dispatcher.UIThread.InvokeAsync(() => AddMissingSynchronizedGames(games));
        await Task.WhenAll(_allGames.Select(card => card.LoadCoverAsync(_artworkImageLoader)));
    }

    private void AddMissingSynchronizedGames(IReadOnlyList<Game> games)
    {
        var existingIds = _allGames.Select(card => card.Game.Id).ToHashSet();
        foreach (var game in games)
        {
            if (existingIds.Add(game.Id))
            {
                AddSynchronizedGame(game);
            }
        }
    }

    private void ReportLibrarySyncProgress(GameLibrarySyncProgress progress)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyLibrarySyncProgress(progress)).GetAwaiter().GetResult();
            return;
        }

        ApplyLibrarySyncProgress(progress);
    }

    private void ApplyLibrarySyncProgress(GameLibrarySyncProgress progress)
    {
        _librarySyncProgress.ReportLibrarySyncProgress(progress);
        if (progress.UpdatedGame is { } game)
        {
            ApplySynchronizedGame(game);
        }
    }

    private void ApplySynchronizedGame(Game game)
    {
        if (_allGames.Any(card => card.Game.Id == game.Id))
        {
            UpdateGameCard(game);
            return;
        }

        AddSynchronizedGame(game);
    }

    private void AddSynchronizedGame(Game game)
    {
        var card = new GameCardViewModel(game, _dateTimeDisplayFormatter, _hostSystemInfo);
        _allGames.Add(card);
        if (game.IsHidden == IsShowingHiddenGames)
        {
            Games.Add(card);
            SelectedGameCard ??= card;
        }

        OnLibraryActivityChanged();
        _ = card.LoadCoverAsync(_artworkImageLoader);
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
        if (_cachedGameLogos.TryGetValue(game.Id, out var cachedLogo))
        {
            if (string.Equals(cachedLogo.Source, game.Artwork.LogoPath, StringComparison.Ordinal) &&
                cachedLogo.NormalizationRequested == _normalizeLibraryLogos)
            {
                card.LogoImage = cachedLogo.Image;
                TouchCachedGameLogo(cachedLogo);
            }
            else
            {
                RemoveCachedGameLogo(game.Id);
            }
        }

        _allGames[allGamesIndex] = card;
        previousCard.LogoImage = null;

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

    partial void OnHasCompletedLibrarySyncChanged(bool value)
    {
        OnPropertyChanged(nameof(EmptyLibraryTitle));
        OnPropertyChanged(nameof(EmptyLibraryMessage));
    }

    private sealed class CallbackProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    partial void OnSelectedGameCardChanged(GameCardViewModel? oldValue, GameCardViewModel? newValue)
    {
        OnPropertyChanged(nameof(IsPrimaryGameActionInstall));
        OnPropertyChanged(nameof(PrimaryGameActionLabel));
        OnPropertyChanged(nameof(IsPrimaryGameActionEnabled));
        OnPropertyChanged(nameof(CanUninstallSelectedGame));
        OnPropertyChanged(nameof(IsSelectedGameUninstallPending));
        OnPropertyChanged(nameof(IsSelectedGameManagementPending));
        NotifyGamePreparationChanged();
        OnPropertyChanged(nameof(SelectedGameHiddenActionLabel));

        foreach (var game in _allGames)
        {
            game.IsSelected = ReferenceEquals(game, newValue);
        }

        if (newValue is not null)
        {
            _ = LoadBackgroundAsync(newValue);
            if (_isLibraryScreenActive)
            {
                _ = LoadSelectedGameLogoAsync(newValue);
            }
        }
        else
        {
            CancelSelectedGameLogoRequest();
            CancelSelectedBackgroundLoad();
        }
    }

    private void OnAppearanceSettingsChanged(AppearanceSettings settings)
    {
        var wasNormalizationEnabled = _showSelectedGameLogos && _normalizeLibraryLogos;
        var normalizationChanged = _normalizeLibraryLogos != settings.NormalizeLibraryLogos;
        var wasShowingSelectedGameLogo = _showSelectedGameLogos;
        _showSelectedGameLogos = settings.LibraryTitleMode == LibraryTitleMode.Logo;
        _normalizeLibraryLogos = settings.NormalizeLibraryLogos;
        var isNormalizationEnabled = _showSelectedGameLogos && _normalizeLibraryLogos;
        if (!isNormalizationEnabled)
        {
            if (wasShowingSelectedGameLogo)
            {
                CancelSelectedGameLogoRequest();
            }

            _libraryLogoNormalization.Cancel();
            return;
        }

        if (normalizationChanged && SelectedGameCard is { } selectedCard)
        {
            CancelSelectedGameLogoRequest();
            RemoveCachedGameLogo(selectedCard.Game.Id);
        }

        if (SelectedGameCard is { } gameCard && _isLibraryScreenActive)
        {
            _ = LoadSelectedGameLogoAsync(gameCard);
        }

        if (!wasNormalizationEnabled)
        {
            QueueLibraryLogoNormalization();
        }
    }

    private void QueueLibraryLogoNormalization()
    {
        if (_showSelectedGameLogos && _normalizeLibraryLogos)
        {
            _libraryLogoNormalization.RequestRun();
        }
    }

    public void RequestLibraryLogoNormalization() => QueueLibraryLogoNormalization();

    private void OnGameLogoNormalizationCompleted(GameLogoNormalizationProgress progress)
    {
        if (!_showSelectedGameLogos || !_normalizeLibraryLogos ||
            progress.ErrorMessage is not null || progress.NormalizedPath is null)
        {
            return;
        }

        var gameCard = _allGames.FirstOrDefault(card => card.Game.Id == progress.GameId);
        if (gameCard is null)
        {
            return;
        }

        if (_cachedGameLogos.TryGetValue(progress.GameId, out var cachedLogo) &&
            cachedLogo.IsNormalized &&
            string.Equals(cachedLogo.Source, gameCard.Game.Artwork.LogoPath, StringComparison.Ordinal))
        {
            return;
        }

        RemoveCachedGameLogo(progress.GameId);
        if (_isLibraryScreenActive && ReferenceEquals(SelectedGameCard, gameCard))
        {
            CancelSelectedGameLogoRequest();
            _ = LoadSelectedGameLogoAsync(gameCard);
        }
    }

    private void OnLibraryLogoNormalizationPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(LibraryLogoNormalizationViewModel.IsRunning):
                OnPropertyChanged(nameof(IsLibraryLogoNormalizationRunning));
                OnPropertyChanged(nameof(IsSyncActivityVisible));
                OnPropertyChanged(nameof(SyncActivityStatus));
                OnPropertyChanged(nameof(SyncActivityProgressPercent));
                break;
            case nameof(LibraryLogoNormalizationViewModel.Status):
                OnPropertyChanged(nameof(LibraryLogoNormalizationStatus));
                OnPropertyChanged(nameof(SyncActivityStatus));
                break;
            case nameof(LibraryLogoNormalizationViewModel.ProgressPercent):
                OnPropertyChanged(nameof(LibraryLogoNormalizationProgressPercent));
                OnPropertyChanged(nameof(SyncActivityProgressPercent));
                break;
        }
    }

    public void CancelSelectedGameLogoLoad()
    {
        _isLibraryScreenActive = false;
        CancelSelectedGameLogoRequest();
    }

    private void CancelSelectedGameLogoRequest()
    {
        _selectedLogoRevision++;
        _selectedLogoCancellation?.Cancel();
        _selectedLogoCard = null;
    }

    private async Task LoadSelectedGameLogoAsync(GameCardViewModel gameCard)
    {
        if (_selectedLogoCancellation is not null && !ReferenceEquals(_selectedLogoCard, gameCard))
        {
            CancelSelectedGameLogoRequest();
        }

        if (!_showSelectedGameLogos)
        {
            return;
        }

        var normalize = _normalizeLibraryLogos;
        if (TryGetCachedGameLogo(gameCard, normalize, out var cachedImage))
        {
            if (!ReferenceEquals(gameCard.LogoImage, cachedImage))
            {
                gameCard.LogoImage = cachedImage;
            }

            return;
        }

        if (_selectedLogoCancellation is not null &&
            ReferenceEquals(_selectedLogoCard, gameCard) &&
            !_selectedLogoCancellation.IsCancellationRequested)
        {
            return;
        }

        _selectedLogoCancellation?.Cancel();
        _selectedLogoCancellation = null;
        var revision = ++_selectedLogoRevision;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_applicationLifetime.Token);
        var cancellationToken = cancellation.Token;
        _selectedLogoCancellation = cancellation;
        _selectedLogoCard = gameCard;
        try
        {
            var logo = await gameCard.LoadLogoAsync(_artworkImageLoader, normalize, cancellationToken);
            if (!IsCurrentSelectedLogo(gameCard, revision, cancellation, normalize))
            {
                logo.Image?.Dispose();
                return;
            }

            if (logo.Image is not null)
            {
                CacheGameLogo(gameCard, logo.Image, normalize, logo.IsNormalized);
                return;
            }

            var updatedGame = await _gameLibrarySyncService.EnsureArtworkAsync(
                gameCard.Game.Id,
                new HashSet<GameArtworkSlot> { GameArtworkSlot.Logo },
                cancellationToken);
            if (!IsCurrentSelectedLogo(gameCard, revision, cancellation, normalize))
            {
                return;
            }

            gameCard.RefreshArtwork(updatedGame);
            logo = await gameCard.LoadLogoAsync(_artworkImageLoader, normalize, cancellationToken);
            if (!IsCurrentSelectedLogo(gameCard, revision, cancellation, normalize))
            {
                logo.Image?.Dispose();
                return;
            }

            if (logo.Image is not null)
            {
                CacheGameLogo(gameCard, logo.Image, normalize, logo.IsNormalized);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not load library logo for game {GameId}.", gameCard.Game.Id);
        }
        finally
        {
            if (ReferenceEquals(_selectedLogoCancellation, cancellation))
            {
                _selectedLogoCancellation = null;
                _selectedLogoCard = null;
            }

            cancellation.Dispose();
        }
    }

    private bool IsCurrentSelectedLogo(
        GameCardViewModel gameCard,
        long revision,
        CancellationTokenSource cancellation,
        bool normalize) =>
        !cancellation.IsCancellationRequested &&
        revision == _selectedLogoRevision &&
        ReferenceEquals(SelectedGameCard, gameCard) &&
        normalize == _normalizeLibraryLogos &&
        _showSelectedGameLogos;

    private bool TryGetCachedGameLogo(GameCardViewModel gameCard, bool normalize, out Bitmap? image)
    {
        if (!_cachedGameLogos.TryGetValue(gameCard.Game.Id, out var cachedLogo))
        {
            image = null;
            return false;
        }

        if (!string.Equals(cachedLogo.Source, gameCard.Game.Artwork.LogoPath, StringComparison.Ordinal) ||
            cachedLogo.NormalizationRequested != normalize)
        {
            RemoveCachedGameLogo(gameCard.Game.Id);
            image = null;
            return false;
        }

        TouchCachedGameLogo(cachedLogo);
        image = cachedLogo.Image;
        return true;
    }

    private void CacheGameLogo(GameCardViewModel gameCard, Bitmap image, bool normalize, bool isNormalized)
    {
        RemoveCachedGameLogo(gameCard.Game.Id);
        var node = _gameLogoCacheLru.AddFirst(gameCard.Game.Id);
        _cachedGameLogos.Add(
            gameCard.Game.Id,
            new CachedGameLogo(gameCard.Game.Artwork.LogoPath, normalize, isNormalized, image, node));
        gameCard.LogoImage = image;

        while (_cachedGameLogos.Count > MaximumCachedGameLogos)
        {
            var last = _gameLogoCacheLru.Last;
            if (last is null)
            {
                throw new InvalidOperationException("The selected game logo cache is inconsistent.");
            }

            RemoveCachedGameLogo(last.Value);
        }
    }

    private void TouchCachedGameLogo(CachedGameLogo cachedLogo)
    {
        _gameLogoCacheLru.Remove(cachedLogo.Node);
        _gameLogoCacheLru.AddFirst(cachedLogo.Node);
    }

    private void RemoveCachedGameLogo(Guid gameId)
    {
        if (!_cachedGameLogos.Remove(gameId, out var cachedLogo))
        {
            return;
        }

        _gameLogoCacheLru.Remove(cachedLogo.Node);
        foreach (var card in _allGames)
        {
            if (ReferenceEquals(card.LogoImage, cachedLogo.Image))
            {
                card.LogoImage = null;
            }
        }

        cachedLogo.Image.Dispose();
    }

    private void ClearGameLogoCache()
    {
        foreach (var cachedLogo in _cachedGameLogos.Values)
        {
            foreach (var card in _allGames)
            {
                if (ReferenceEquals(card.LogoImage, cachedLogo.Image))
                {
                    card.LogoImage = null;
                }
            }

            cachedLogo.Image.Dispose();
        }

        _cachedGameLogos.Clear();
        _gameLogoCacheLru.Clear();
    }

    private void CancelSelectedBackgroundLoad()
    {
        _selectedBackgroundRevision++;
        _selectedBackgroundCancellation?.Cancel();
    }

    private async Task LoadBackgroundAsync(GameCardViewModel game)
    {
        CancelSelectedBackgroundLoad();
        var revision = ++_selectedBackgroundRevision;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_applicationLifetime.Token);
        var cancellationToken = cancellation.Token;
        _selectedBackgroundCancellation = cancellation;
        try
        {
            var image = await _artworkImageLoader.LoadAsync(game.BackgroundSource, cancellationToken);
            if (cancellationToken.IsCancellationRequested ||
                revision != _selectedBackgroundRevision ||
                !ReferenceEquals(SelectedGameCard, game))
            {
                image?.Dispose();
                return;
            }

            var obsoletePreviousImage = PreviousBackgroundImage;
            PreviousBackgroundImage = BackgroundImage;
            PreviousBackgroundImageOpacity = PreviousBackgroundImage is null ? 0 : 1;
            BackgroundImage = image;
            BackgroundImageOpacity = 1;
            PreviousBackgroundImageOpacity = 0;
            if (obsoletePreviousImage is not null && !ReferenceEquals(obsoletePreviousImage, BackgroundImage))
            {
                obsoletePreviousImage.Dispose();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not load library background for game {GameId}.", game.Game.Id);
        }
        finally
        {
            if (ReferenceEquals(_selectedBackgroundCancellation, cancellation))
            {
                _selectedBackgroundCancellation = null;
            }

            cancellation.Dispose();
        }
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
