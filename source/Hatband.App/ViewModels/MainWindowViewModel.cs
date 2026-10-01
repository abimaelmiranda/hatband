using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Hatband.App.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Hatband.Core.Enums.Stores;
using Hatband.App.Services;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Abstractions;
using Hatband.Core.Models;
using Hatband.Core.Models.Settings;
using Hatband.App.ViewModels.Settings;
using System.Windows.Input;

namespace Hatband.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IGameLibraryService gameLibraryService;
    private readonly IGameLibrarySyncService gameLibrarySyncService;
    private readonly IGameTimeToBeatSyncService gameTimeToBeatSyncService;
    private readonly ISettingsStore settingsStore;
    private readonly ArtworkImageLoader artworkImageLoader;
    private readonly SteamConnectorLoginViewModel steamConnectorLogin;
    private readonly LibrarySyncProgressViewModel librarySyncProgress;
    private List<GameCardViewModel> allGames = [];
    private readonly Stack<HatbandScreen> navigationHistory = new();
    private bool returnToMenuOnBack;

    [ObservableProperty]
    public partial ObservableCollection<GameCardViewModel> Games { get; set; } = [];

    [ObservableProperty]
    public partial HatbandSettings Settings { get; set; } = new();

    [ObservableProperty]
    public partial SettingsOptionViewModel? SelectedLanguageOption { get; set; }

    [ObservableProperty]
    public partial SettingsOptionViewModel? SelectedTimeZoneOption { get; set; }

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
    public partial HatbandScreen CurrentScreen { get; set; } = HatbandScreen.Library;

    [ObservableProperty]
    public partial ObservableCollection<MenuOptionViewModel> MenuOptions { get; set; } = [
        new(Resources.MenuLibrary, MenuAction.Library),
        new(Resources.MenuViewHiddenGames, MenuAction.HiddenGames),
        new(Resources.MenuAddGame, MenuAction.AddGame),
        new(Resources.MenuConnectors, MenuAction.Connectors),
        new(Resources.MenuSettings, MenuAction.Settings),
        new(Resources.MenuExit, MenuAction.Exit)
    ];

    [ObservableProperty]
    public partial int SelectedMenuIndex { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ConnectorViewModel> Connectors { get; set; } = [];

    [ObservableProperty]
    public partial int SelectedConnectorIndex { get; set; }

    [ObservableProperty]
    public partial ConnectorViewModel? SelectedConnector { get; set; }

    [ObservableProperty]
    public partial bool IsMenuOpen { get; set; }

    [ObservableProperty]
    public partial bool IsGameOptionsOpen { get; set; }

    public GameMetadataEditorViewModel GameMetadataEditor { get; }

    public AddGameViewModel AddGame { get; }

    public string NewGameName
    {
        get => AddGame.Name;
        set => AddGame.Name = value;
    }

    public string? NewGameInstallDirectory
    {
        get => AddGame.InstallDirectory;
        set => AddGame.InstallDirectory = value;
    }

    public string? NewGameLaunchTarget
    {
        get => AddGame.LaunchTarget;
        set => AddGame.LaunchTarget = value;
    }

    public ICommand SaveNewGameCommand => AddGame.SaveGameCommand;

    [ObservableProperty]
    public partial bool IsShowingHiddenGames { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLibraryLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLibrarySyncRunning { get; set; }

    [ObservableProperty]
    public partial bool HasCompletedSteamSync { get; set; }

    public MainWindowViewModel(
        IGameLibraryService gameLibraryService,
        IGameTimeToBeatSyncService gameTimeToBeatSyncService,
        ISettingsStore settingsStore,
        ArtworkImageLoader artworkImageLoader,
        IEnumerable<IQrCodeLoginProvider> qrLoginProviders,
        IEnumerable<IGameStoreIntegration> storeIntegrations,
        IGameLibrarySyncService gameLibrarySyncService,
        IGameArtworkStorage artworkStorage,
        IEnumerable<IGameMetadataSearchProvider> metadataSearchProviders,
        IEnumerable<IGameArtworkSearchProvider> artworkSearchProviders)
    {
        this.gameLibraryService = gameLibraryService;
        AddGame = new AddGameViewModel(gameLibraryService);
        AddGame.PropertyChanged += OnAddGamePropertyChanged;
        AddGame.CreationCompleted += OnAddGameCreationCompleted;
        GameMetadataEditor = new GameMetadataEditorViewModel(
            gameLibraryService,
            artworkStorage,
            metadataSearchProviders,
            artworkSearchProviders,
            artworkImageLoader);
        GameMetadataEditor.Saved += OnGameMetadataEditorSaved;
        GameMetadataEditor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(GameMetadataEditor.IsArtworkPickerOpen))
            {
                OnPropertyChanged(nameof(KeyboardHelpText));
            }
        };
        this.settingsStore = settingsStore;
        SettingsNavigation.PropertyChanged += OnSettingsNavigationPropertyChanged;
        this.gameLibrarySyncService = gameLibrarySyncService;
        this.gameTimeToBeatSyncService = gameTimeToBeatSyncService;
        librarySyncProgress = new LibrarySyncProgressViewModel(gameLibrarySyncService, gameTimeToBeatSyncService);
        librarySyncProgress.PropertyChanged += OnLibrarySyncProgressPropertyChanged;
        librarySyncProgress.UpdatedGame += UpdateGameCard;
        librarySyncProgress.CompletionError += message => StatusMessage = message;
        this.artworkImageLoader = artworkImageLoader;
        LanguageOptions = CreateLanguageOptions();
        TimeZoneOptions = CreateTimeZoneOptions();
        UpdateSelectedSettingsOptions();
        var steamQrLoginProvider = qrLoginProviders.Single(provider => provider.SourceId == GameSourceId.Steam);
        var steamSessionProvider = storeIntegrations.OfType<IConnectorSessionProvider>()
            .Single(provider => provider.SourceId == GameSourceId.Steam);
        steamConnectorLogin = new SteamConnectorLoginViewModel(
            steamQrLoginProvider,
            steamSessionProvider,
            SynchronizeSteamLibraryAsync);
        steamConnectorLogin.ErrorOccurred += message => StatusMessage = message;
        steamConnectorLogin.Disconnected += message => StatusMessage = message;
        steamConnectorLogin.PropertyChanged += OnSteamConnectorLoginPropertyChanged;
        var qrLoginSourceIds = qrLoginProviders.Select(provider => provider.SourceId).ToHashSet();
        Connectors = new ObservableCollection<ConnectorViewModel>(
            storeIntegrations
                .OrderBy(integration => integration.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(integration => new ConnectorViewModel(
                    integration.SourceId,
                    integration.DisplayName,
                    qrLoginSourceIds.Contains(integration.SourceId))));
        UpdateConnectorSelection();
    }

    public event EventHandler? ExitRequested;

    public ObservableCollection<SettingsOptionViewModel> LanguageOptions { get; }

    public ObservableCollection<SettingsOptionViewModel> TimeZoneOptions { get; }

    public SettingsNavigationViewModel SettingsNavigation { get; } = new();

    public ObservableCollection<SettingsSectionOptionViewModel> SettingsSections => SettingsNavigation.Sections;

    public int SelectedSettingsSectionIndex => SettingsNavigation.SelectedSectionIndex;

    public bool IsSettingsContentActive => SettingsNavigation.IsContentActive;

    public int SelectedSettingsFieldIndex => SettingsNavigation.SelectedFieldIndex;

    public bool IsGeneralSettingsSection => SettingsNavigation.IsGeneralSection;

    public bool IsSettingsSectionPlaceholderVisible => SettingsNavigation.IsSectionPlaceholderVisible;

    public SettingsSectionOptionViewModel SelectedSettingsSection => SettingsNavigation.SelectedSection;

    public bool IsLanguageFieldSelected => SettingsNavigation.IsLanguageFieldSelected;

    public bool IsTimeZoneFieldSelected => SettingsNavigation.IsTimeZoneFieldSelected;

    public IBrush LanguageFieldBorderBrush => SettingsNavigation.LanguageFieldBorderBrush;

    public IBrush TimeZoneFieldBorderBrush => SettingsNavigation.TimeZoneFieldBorderBrush;

    public bool IsLibraryEnrichmentRunning => librarySyncProgress.IsLibraryEnrichmentRunning;

    public string? LibraryEnrichmentStatus => librarySyncProgress.LibraryEnrichmentStatus;

    public double LibraryEnrichmentProgressPercent => librarySyncProgress.LibraryEnrichmentProgressPercent;

    public bool IsTimeToBeatSyncRunning => librarySyncProgress.IsTimeToBeatSyncRunning;

    public string? TimeToBeatSyncStatus => librarySyncProgress.TimeToBeatSyncStatus;

    public double TimeToBeatSyncProgressPercent => librarySyncProgress.TimeToBeatSyncProgressPercent;

    public bool IsLibraryScreen => CurrentScreen == HatbandScreen.Library;

    public bool IsLibraryBackgroundVisible => GetScreenZIndex(HatbandScreen.Library) >= 0;

    public int LibraryScreenZIndex => GetScreenZIndex(HatbandScreen.Library);

    public bool IsDetailsScreen => CurrentScreen == HatbandScreen.Details;

    public bool IsDetailsBackgroundVisible => GetScreenZIndex(HatbandScreen.Details) >= 0;

    public int DetailsScreenZIndex => GetScreenZIndex(HatbandScreen.Details);

    public bool IsGameEditorScreen => CurrentScreen == HatbandScreen.EditGame;

    public bool IsGameEditorBackgroundVisible => GetScreenZIndex(HatbandScreen.EditGame) >= 0;

    public int GameEditorScreenZIndex => GetScreenZIndex(HatbandScreen.EditGame);

    public bool IsAddGameScreen => CurrentScreen == HatbandScreen.AddGame;

    public bool IsAddGameBackgroundVisible => GetScreenZIndex(HatbandScreen.AddGame) >= 0;

    public int AddGameScreenZIndex => GetScreenZIndex(HatbandScreen.AddGame);

    public bool IsSettingsScreen => CurrentScreen == HatbandScreen.Settings;

    public bool IsSettingsBackgroundVisible => GetScreenZIndex(HatbandScreen.Settings) >= 0;

    public int SettingsScreenZIndex => GetScreenZIndex(HatbandScreen.Settings);

    public bool IsConnectorsScreen => CurrentScreen == HatbandScreen.Connectors;

    public bool IsConnectorsBackgroundVisible => GetScreenZIndex(HatbandScreen.Connectors) >= 0;

    public int ConnectorsScreenZIndex => GetScreenZIndex(HatbandScreen.Connectors);

    public int MenuOverlayZIndex => 3;

    private bool IsDialogScreen => IsGameEditorScreen || IsAddGameScreen || IsSettingsScreen || IsConnectorsScreen;

    public bool IsConnectorLoginScreen => IsConnectorsScreen && SelectedConnector is not null;

    public bool IsConnectorConnected => ActiveConnectorAccountName is not null;

    public Bitmap? SteamQrCode => steamConnectorLogin.QrCode;

    public string SteamConnectionStatus => steamConnectorLogin.ConnectionStatus;

    public string? ActiveConnectorAccountName => steamConnectorLogin.ActiveAccountName;

    public bool IsSteamLoginPending => steamConnectorLogin.IsLoginPending;

    public bool CanStartSteamLogin => steamConnectorLogin.CanStartLogin;

    public ICommand ConnectSteamCommand => steamConnectorLogin.ConnectSteamCommand;

    public ICommand CancelSteamLoginCommand => steamConnectorLogin.CancelSteamLoginCommand;

    public ICommand DisconnectSteamCommand => steamConnectorLogin.DisconnectSteamCommand;

    public bool IsLibraryEmpty => Games.Count == 0 && !IsLibraryBusy;

    public bool IsLibraryBusy => Games.Count == 0 && (IsLibraryLoading || IsLibrarySyncRunning);

    public bool HasEmptyLibraryActions => !IsShowingHiddenGames;

    public string LibraryScreenTitle => IsShowingHiddenGames
        ? Resources.HiddenGamesHeader
        : Resources.LibraryHeader;

    public string LibraryEmptyBreadcrumb => IsShowingHiddenGames
        ? Resources.HiddenGamesBreadcrumb
        : Resources.LibraryBreadcrumb;

    public string KeyboardHelpText
    {
        get
        {
            if (IsMenuOpen)
            {
                return Resources.KeyboardMenuNavigate;
            }

            if (GameMetadataEditor.IsArtworkPickerOpen)
            {
                return Resources.KeyboardArtworkHelp;
            }

            if (IsGameEditorScreen || IsGameOptionsOpen)
            {
                return Resources.KeyboardEditorHelp;
            }

            if (IsSettingsScreen)
            {
                return Resources.KeyboardSettingsHelp;
            }

            if (IsConnectorsScreen)
            {
                return Resources.KeyboardConnectorHelp;
            }

            if (IsDetailsScreen)
            {
                return Resources.KeyboardDetailsHelp;
            }

            if (IsAddGameScreen)
            {
                return Resources.KeyboardAddGame;
            }

            return Resources.KeyboardChooseOpen;
        }
    }

    public bool CanSynchronizeSteamLibrary =>
        steamConnectorLogin.CurrentAccount is not null && !IsLibrarySyncRunning;

    public bool IsPrimaryGameActionInstall => SelectedGameCard?.Game is
    {
        SourceId: GameSourceId.Steam,
        IsInstalled: false
    };

    public string PrimaryGameActionLabel => IsPrimaryGameActionInstall ? Resources.Install : Resources.Play;

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

    public void MoveSettingsSectionSelection(int direction)
    {
        if (IsSettingsScreen)
        {
            SettingsNavigation.MoveSectionSelection(direction);
        }
    }

    public void ActivateSettingsSection() => SettingsNavigation.ActivateSection();

    public void DeactivateSettingsContent() => SettingsNavigation.DeactivateContent();

    public void MoveSettingsFieldSelection(int direction) => SettingsNavigation.MoveFieldSelection(direction);

    public void SelectSettingsField(int index) => SettingsNavigation.SelectField(index);

    public void SelectSettingsSection(SettingsSectionOptionViewModel section) => SettingsNavigation.SelectSection(section);

    private void OnSettingsNavigationPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(SettingsNavigationViewModel.Sections):
                OnPropertyChanged(nameof(SettingsSections));
                break;
            case nameof(SettingsNavigationViewModel.SelectedSectionIndex):
                OnPropertyChanged(nameof(SelectedSettingsSectionIndex));
                break;
            case nameof(SettingsNavigationViewModel.IsContentActive):
                OnPropertyChanged(nameof(IsSettingsContentActive));
                break;
            case nameof(SettingsNavigationViewModel.SelectedFieldIndex):
                OnPropertyChanged(nameof(SelectedSettingsFieldIndex));
                break;
            case nameof(SettingsNavigationViewModel.IsGeneralSection):
                OnPropertyChanged(nameof(IsGeneralSettingsSection));
                break;
            case nameof(SettingsNavigationViewModel.IsSectionPlaceholderVisible):
                OnPropertyChanged(nameof(IsSettingsSectionPlaceholderVisible));
                break;
            case nameof(SettingsNavigationViewModel.SelectedSection):
                OnPropertyChanged(nameof(SelectedSettingsSection));
                break;
            case nameof(SettingsNavigationViewModel.IsLanguageFieldSelected):
                OnPropertyChanged(nameof(IsLanguageFieldSelected));
                break;
            case nameof(SettingsNavigationViewModel.IsTimeZoneFieldSelected):
                OnPropertyChanged(nameof(IsTimeZoneFieldSelected));
                break;
            case nameof(SettingsNavigationViewModel.LanguageFieldBorderBrush):
                OnPropertyChanged(nameof(LanguageFieldBorderBrush));
                break;
            case nameof(SettingsNavigationViewModel.TimeZoneFieldBorderBrush):
                OnPropertyChanged(nameof(TimeZoneFieldBorderBrush));
                break;
        }
    }

    partial void OnSettingsChanged(HatbandSettings value)
    {
        UpdateSelectedSettingsOptions();
    }

    partial void OnIsShowingHiddenGamesChanged(bool value)
    {
        RefreshVisibleGames();
        UpdateHiddenGamesMenuTitle();
        OnPropertyChanged(nameof(LibraryScreenTitle));
        OnPropertyChanged(nameof(LibraryEmptyBreadcrumb));
        OnPropertyChanged(nameof(EmptyLibraryTitle));
        OnPropertyChanged(nameof(EmptyLibraryMessage));
        OnPropertyChanged(nameof(HasEmptyLibraryActions));
        OnLibraryActivityChanged();
    }

    partial void OnSelectedLanguageOptionChanged(SettingsOptionViewModel? value)
    {
        if (value is null || Settings.General.LanguageTag == value.Value)
        {
            return;
        }

        Settings.General.LanguageTag = value.Value;
        _ = SaveSettingsAsync(refreshMetadata: true);
    }

    partial void OnSelectedTimeZoneOptionChanged(SettingsOptionViewModel? value)
    {
        if (value is null || Settings.General.TimeZoneId == value.Value)
        {
            return;
        }

        Settings.General.TimeZoneId = value.Value;
        foreach (var game in allGames)
        {
            game.UpdateTimeZone(Settings.General.TimeZoneId);
        }

        _ = SaveSettingsAsync();
    }

    partial void OnCurrentScreenChanged(HatbandScreen value)
    {
        if (value != HatbandScreen.Details)
        {
            IsGameOptionsOpen = false;
        }

        OnPropertyChanged(nameof(IsLibraryScreen));
        OnPropertyChanged(nameof(IsLibraryBackgroundVisible));
        OnPropertyChanged(nameof(LibraryScreenZIndex));
        OnPropertyChanged(nameof(IsDetailsScreen));
        OnPropertyChanged(nameof(IsDetailsBackgroundVisible));
        OnPropertyChanged(nameof(DetailsScreenZIndex));
        OnPropertyChanged(nameof(IsGameEditorScreen));
        OnPropertyChanged(nameof(IsGameEditorBackgroundVisible));
        OnPropertyChanged(nameof(GameEditorScreenZIndex));
        OnPropertyChanged(nameof(IsAddGameScreen));
        OnPropertyChanged(nameof(IsAddGameBackgroundVisible));
        OnPropertyChanged(nameof(AddGameScreenZIndex));
        OnPropertyChanged(nameof(IsSettingsScreen));
        OnPropertyChanged(nameof(IsSettingsBackgroundVisible));
        OnPropertyChanged(nameof(SettingsScreenZIndex));
        OnPropertyChanged(nameof(IsConnectorsScreen));
        OnPropertyChanged(nameof(IsConnectorsBackgroundVisible));
        OnPropertyChanged(nameof(ConnectorsScreenZIndex));
        OnPropertyChanged(nameof(IsConnectorLoginScreen));
        OnPropertyChanged(nameof(KeyboardHelpText));
    }

    partial void OnIsMenuOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(KeyboardHelpText));
    }

    partial void OnIsGameOptionsOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(KeyboardHelpText));
    }

    partial void OnSelectedConnectorChanged(ConnectorViewModel? value)
    {
        steamConnectorLogin.SelectConnector(value?.SourceId);
        OnPropertyChanged(nameof(IsConnectorLoginScreen));
    }

    private void OnSteamConnectorLoginPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SteamConnectorLoginViewModel.ActiveAccountName))
        {
            OnPropertyChanged(nameof(ActiveConnectorAccountName));
            OnPropertyChanged(nameof(IsConnectorConnected));
            OnPropertyChanged(nameof(CanSynchronizeSteamLibrary));
            SyncConnectedSteamLibraryCommand.NotifyCanExecuteChanged();
        }
        else if (args.PropertyName == nameof(SteamConnectorLoginViewModel.ConnectionStatus))
        {
            OnPropertyChanged(nameof(SteamConnectionStatus));
        }
        else if (args.PropertyName == nameof(SteamConnectorLoginViewModel.QrCode))
        {
            OnPropertyChanged(nameof(SteamQrCode));
        }
        else if (args.PropertyName == nameof(SteamConnectorLoginViewModel.IsLoginPending))
        {
            OnPropertyChanged(nameof(IsSteamLoginPending));
            OnPropertyChanged(nameof(CanStartSteamLogin));
        }
    }

    partial void OnIsLibraryLoadingChanged(bool value)
    {
        OnLibraryActivityChanged();
    }

    partial void OnIsLibrarySyncRunningChanged(bool value)
    {
        SyncConnectedSteamLibraryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSynchronizeSteamLibrary));
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
        OnPropertyChanged(nameof(SelectedGameHiddenActionLabel));

        foreach (var game in allGames)
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

        var image = await artworkImageLoader.LoadAsync(game.BackgroundSource);
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
    }

    public async Task LoadGamesAsync(CancellationToken cancellationToken = default)
    {
        IsLibraryLoading = true;

        try
        {
            Settings = await settingsStore.LoadAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.LoadSettingsError, exception.Message);
        }

        try
        {
            var savedGames = await gameLibraryService.GetGamesAsync(cancellationToken);
            SetGames(savedGames.Select(game => new GameCardViewModel(game, Settings.General.TimeZoneId)));

            await Task.WhenAll(allGames.Select(game => game.LoadCoverAsync(artworkImageLoader)));
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.LoadLibraryError, exception.Message);
        }
        finally
        {
            IsLibraryLoading = false;
        }

        if (allGames.Count > 0)
        {
            _ = SynchronizeLibraryDataAndHowLongToBeatAsync(cancellationToken);
        }
    }

    private async Task SynchronizeLibraryDataAndHowLongToBeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            await gameLibrarySyncService.EnrichLibraryAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.LibraryEnrichmentError, exception.Message);
        }

        await SynchronizeTimeToBeatAsync(cancellationToken);
    }

    private async Task SaveSettingsAsync(bool refreshMetadata = false)
    {
        try
        {
            await settingsStore.SaveAsync(Settings);
            StatusMessage = Resources.SettingsSaved;

            if (refreshMetadata)
            {
                await gameLibrarySyncService.EnrichLibraryAsync();
            }
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.SaveSettingsError, exception.Message);
        }
    }

    private void UpdateSelectedSettingsOptions()
    {
        SelectedLanguageOption = FindOrAddLanguageOption(Settings.General.LanguageTag);
        SelectedTimeZoneOption = FindOrAddTimeZoneOption(Settings.General.TimeZoneId);
    }

    private SettingsOptionViewModel FindOrAddLanguageOption(string languageTag)
    {
        var option = LanguageOptions.FirstOrDefault(item => item.Value == languageTag);
        if (option is not null)
        {
            return option;
        }

        string label;
        try
        {
            label = CultureInfo.GetCultureInfo(languageTag).NativeName;
        }
        catch (CultureNotFoundException)
        {
            label = languageTag;
        }

        option = new SettingsOptionViewModel(languageTag, label);
        LanguageOptions.Add(option);
        return option;
    }

    private SettingsOptionViewModel FindOrAddTimeZoneOption(string timeZoneId)
    {
        var option = TimeZoneOptions.FirstOrDefault(item => item.Value == timeZoneId);
        if (option is not null)
        {
            return option;
        }

        option = new SettingsOptionViewModel(timeZoneId, string.Format(CultureInfo.CurrentCulture, Resources.Unavailable, timeZoneId));
        TimeZoneOptions.Add(option);
        return option;
    }

    private static ObservableCollection<SettingsOptionViewModel> CreateLanguageOptions()
    {
        var languageTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "en-US",
            "pt-BR",
            "es-ES"
        };

        return new ObservableCollection<SettingsOptionViewModel>(
            languageTags
                .Where(languageTag => !string.IsNullOrWhiteSpace(languageTag))
                .Select(languageTag => new SettingsOptionViewModel(
                    languageTag,
                    CultureInfo.GetCultureInfo(languageTag).NativeName))
                .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase));
    }

    private static ObservableCollection<SettingsOptionViewModel> CreateTimeZoneOptions()
    {
        return new ObservableCollection<SettingsOptionViewModel>(
            TimeZoneInfo.GetSystemTimeZones()
                .Select(timeZone => new SettingsOptionViewModel(
                    timeZone.Id,
                    $"{timeZone.DisplayName} ({timeZone.Id})"))
                .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase));
    }

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

    public void OpenSelectedGame()
    {
        if (SelectedGameCard is null)
        {
            return;
        }

        StatusMessage = null;
        NavigateTo(HatbandScreen.Details);
        IsGameOptionsOpen = false;
    }

    public void ToggleGameOptions()
    {
        if (!IsDetailsScreen)
        {
            return;
        }

        IsGameOptionsOpen = !IsGameOptionsOpen;
    }

    public void OpenSelectedGameEditor()
    {
        if (SelectedGameCard is null)
        {
            return;
        }

        GameMetadataEditor.Load(SelectedGameCard.Game, Settings.General.LanguageTag);
        IsGameOptionsOpen = false;
        NavigateTo(HatbandScreen.EditGame);
    }

    public void CancelGameEditing()
    {
        GameMetadataEditor.CancelCommand.Execute(null);
        ReturnToPreviousScreen();
    }

    private void OnGameMetadataEditorSaved(object? sender, Game game)
    {
        UpdateGameCard(game);
        ReturnToPreviousScreen();
        StatusMessage = Resources.GameDetailsSaved;
        _ = EnrichLibraryAfterGameEditAsync();
    }

    private async Task EnrichLibraryAfterGameEditAsync()
    {
        try
        {
            await gameLibrarySyncService.EnrichLibraryAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.LibraryEnrichmentError, exception.Message);
        }
    }

    [RelayCommand]
    private async Task ToggleSelectedGameHiddenAsync(CancellationToken cancellationToken)
    {
        if (SelectedGameCard is null)
        {
            return;
        }

        var game = SelectedGameCard.Game;
        var isHidden = !game.IsHidden;
        try
        {
            await gameLibraryService.SetGameHiddenAsync(game.Id, isHidden, cancellationToken);
            game.IsHidden = isHidden;
            IsGameOptionsOpen = false;
            ReturnToPreviousScreen();
            RefreshVisibleGames();
            StatusMessage = isHidden
                ? string.Format(CultureInfo.CurrentCulture, Resources.GameHidden, game.Name)
                : string.Format(CultureInfo.CurrentCulture, Resources.GameUnhidden, game.Name);
            ReturnToLibraryRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.UpdateGameVisibilityError, exception.Message);
        }
    }

    public void ShowAllGames()
    {
        IsShowingHiddenGames = false;
        CurrentScreen = HatbandScreen.Library;
        navigationHistory.Clear();
        NotifyBackgroundScreensChanged();
    }

    public event EventHandler? ReturnToLibraryRequested;

    public void ActivatePrimaryGameAction()
    {
        StatusMessage = IsPrimaryGameActionInstall
            ? Resources.SteamInstallNotImplemented
            : Resources.LaunchNotImplemented;
    }

    public void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
        if (IsMenuOpen)
        {
            UpdateMenuSelection();
        }
    }

    public void MoveMenuSelection(int direction)
    {
        if (!IsMenuOpen || MenuOptions.Count == 0 || direction == 0)
        {
            return;
        }

        SelectedMenuIndex = Math.Clamp(
            SelectedMenuIndex + Math.Sign(direction),
            0,
            MenuOptions.Count - 1);
        UpdateMenuSelection();
    }

    public void ActivateMenuOption(MenuAction action)
    {
        IsMenuOpen = false;
        switch (action)
        {
            case MenuAction.Library:
                IsShowingHiddenGames = false;
                returnToMenuOnBack = false;
                CurrentScreen = HatbandScreen.Library;
                navigationHistory.Clear();
                NotifyBackgroundScreensChanged();
                break;
            case MenuAction.HiddenGames:
                IsShowingHiddenGames = !IsShowingHiddenGames;
                returnToMenuOnBack = false;
                CurrentScreen = HatbandScreen.Library;
                navigationHistory.Clear();
                NotifyBackgroundScreensChanged();
                break;
            case MenuAction.AddGame:
                returnToMenuOnBack = true;
                StartNewGame();
                break;
            case MenuAction.Settings:
                returnToMenuOnBack = true;
                NavigateTo(HatbandScreen.Settings);
                break;
            case MenuAction.Connectors:
                returnToMenuOnBack = true;
                SelectedConnector = null;
                SelectedConnectorIndex = 0;
                NavigateTo(HatbandScreen.Connectors);
                break;
            case MenuAction.Exit:
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public void GoBack()
    {
        if (IsMenuOpen)
        {
            IsMenuOpen = false;
            return;
        }

        if (IsGameOptionsOpen)
        {
            IsGameOptionsOpen = false;
            return;
        }

        switch (CurrentScreen)
        {
            case HatbandScreen.Details:
            case HatbandScreen.AddGame:
                ReturnToPreviousScreen();
                StatusMessage = null;
                break;
            case HatbandScreen.EditGame:
                CancelGameEditing();
                break;
            case HatbandScreen.Settings:
                steamConnectorLogin.CancelLogin();
                ReturnToPreviousScreen();
                break;
            case HatbandScreen.Connectors:
                if (SelectedConnector is not null)
                {
                    steamConnectorLogin.CancelAndReset();
                    SelectedConnector = null;
                    return;
                }

                steamConnectorLogin.CancelLogin();
                ReturnToPreviousScreen();
                break;
        }
    }

    private void NavigateTo(HatbandScreen screen)
    {
        if (CurrentScreen == screen)
        {
            return;
        }

        navigationHistory.Push(CurrentScreen);
        CurrentScreen = screen;
        NotifyBackgroundScreensChanged();
    }

    private void ReturnToPreviousScreen()
    {
        if (navigationHistory.TryPop(out var previousScreen))
        {
            CurrentScreen = previousScreen;
        }
        else
        {
            CurrentScreen = HatbandScreen.Library;
        }

        NotifyBackgroundScreensChanged();
        if (returnToMenuOnBack)
        {
            returnToMenuOnBack = false;
            IsMenuOpen = true;
        }
    }

    private bool IsPreviousScreen(HatbandScreen screen)
    {
        return navigationHistory.TryPeek(out var previousScreen) && previousScreen == screen;
    }

    private void NotifyBackgroundScreensChanged()
    {
        OnPropertyChanged(nameof(IsLibraryBackgroundVisible));
        OnPropertyChanged(nameof(LibraryScreenZIndex));
        OnPropertyChanged(nameof(IsDetailsBackgroundVisible));
        OnPropertyChanged(nameof(DetailsScreenZIndex));
        OnPropertyChanged(nameof(IsGameEditorBackgroundVisible));
        OnPropertyChanged(nameof(GameEditorScreenZIndex));
        OnPropertyChanged(nameof(IsAddGameBackgroundVisible));
        OnPropertyChanged(nameof(AddGameScreenZIndex));
        OnPropertyChanged(nameof(IsSettingsBackgroundVisible));
        OnPropertyChanged(nameof(SettingsScreenZIndex));
        OnPropertyChanged(nameof(IsConnectorsBackgroundVisible));
        OnPropertyChanged(nameof(ConnectorsScreenZIndex));
    }

    private int GetScreenZIndex(HatbandScreen screen)
    {
        if (CurrentScreen == screen)
        {
            return IsDialogScreen ? 2 : 0;
        }

        return IsDialogScreen && IsPreviousScreen(screen) ? 1 : -1;
    }

    public void MoveConnectorSelection(int direction)
    {
        if (!IsConnectorsScreen || Connectors.Count == 0 || direction == 0)
        {
            return;
        }

        SelectedConnectorIndex = Math.Clamp(
            SelectedConnectorIndex + Math.Sign(direction),
            0,
            Connectors.Count - 1);
        UpdateConnectorSelection();
    }

    public void OpenConnector(ConnectorViewModel connector)
    {
        ArgumentNullException.ThrowIfNull(connector);
        SelectedConnectorIndex = Connectors.IndexOf(connector);
        UpdateConnectorSelection();
        SelectedConnector = connector;
        steamConnectorLogin.RefreshConnectionStatus();
    }

    private void UpdateConnectorSelection()
    {
        for (var index = 0; index < Connectors.Count; index++)
        {
            Connectors[index].IsSelected = index == SelectedConnectorIndex;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSynchronizeSteamLibrary))]
    private async Task SyncConnectedSteamLibraryAsync(CancellationToken cancellationToken)
    {
        var account = steamConnectorLogin.CurrentAccount;
        if (account is null)
        {
            StatusMessage = Resources.ConnectBeforeSync;
            return;
        }

        try
        {
            steamConnectorLogin.SetConnectionStatus(string.Format(CultureInfo.CurrentCulture, Resources.SyncingAccount, account.DisplayName));
            var gameCount = await SynchronizeSteamLibraryAsync(cancellationToken);
            steamConnectorLogin.SetConnectionStatus(SteamConnectorLoginViewModel.FormatSyncStatus(gameCount));
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.SteamSyncError, exception.Message);
        }
    }

    [RelayCommand]
    private void StartNewGame()
    {
        IsShowingHiddenGames = false;
        ClearNewGameForm();
        NavigateTo(HatbandScreen.AddGame);
    }

    [RelayCommand]
    private void CancelNewGame()
    {
        ClearNewGameForm();
        ReturnToPreviousScreen();
    }

    public void PrepareGameFromExecutable(string executablePath)
    {
        AddGame.PrepareFromExecutable(executablePath);
        NavigateTo(HatbandScreen.AddGame);
        StatusMessage = null;
    }

    private void SetGames(IEnumerable<GameCardViewModel> games)
    {
        allGames = games.ToList();
        RefreshVisibleGames();
    }

    private void RefreshVisibleGames()
    {
        var visibleGames = allGames
            .Where(game => game.Game.IsHidden == IsShowingHiddenGames)
            .ToList();

        Games = new ObservableCollection<GameCardViewModel>(visibleGames);
        OnLibraryActivityChanged();
        SelectedGameCard = Games.FirstOrDefault();
    }

    private void OnLibraryActivityChanged()
    {
        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(IsLibraryBusy));
    }

    private void UpdateMenuSelection()
    {
        for (var index = 0; index < MenuOptions.Count; index++)
        {
            MenuOptions[index].IsSelected = index == SelectedMenuIndex;
        }
    }

    private void UpdateHiddenGamesMenuTitle()
    {
        var hiddenGamesOption = MenuOptions.First(option => option.Action == MenuAction.HiddenGames);
        hiddenGamesOption.UpdateTitle(IsShowingHiddenGames
            ? Resources.MenuShowAllGames
            : Resources.MenuViewHiddenGames);
    }

    private void ClearNewGameForm()
    {
        AddGame.Reset();
        StatusMessage = null;
    }

    private void OnAddGamePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        var propertyName = args.PropertyName switch
        {
            nameof(AddGameViewModel.Name) => nameof(NewGameName),
            nameof(AddGameViewModel.InstallDirectory) => nameof(NewGameInstallDirectory),
            nameof(AddGameViewModel.LaunchTarget) => nameof(NewGameLaunchTarget),
            _ => null
        };

        if (propertyName is not null)
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void OnAddGameCreationCompleted(AddGameCreationResult result)
    {
        _ = HandleAddGameCreationResultAsync(result);
    }

    private async Task HandleAddGameCreationResultAsync(AddGameCreationResult result)
    {
        switch (result)
        {
            case AddGameCreationResult.InvalidName:
                StatusMessage = Resources.EnterGameName;
                return;
            case AddGameCreationResult.Failed failed:
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.SaveGameError, failed.Exception.Message);
                return;
            case AddGameCreationResult.Saved saved:
                await RefreshAfterManualGameAddedAsync(saved.Game);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown manual game creation result.");
        }
    }

    private async Task RefreshAfterManualGameAddedAsync(Game game)
    {
        try
        {
            var savedGames = await gameLibraryService.GetGamesAsync();
            SetGames(savedGames.Select(savedGame => new GameCardViewModel(savedGame, Settings.General.TimeZoneId)));
            SelectedGameCard = Games.FirstOrDefault(item => item.Game.Id == game.Id);
            await Task.WhenAll(allGames.Select(item => item.LoadCoverAsync(artworkImageLoader)));
            ClearNewGameForm();
            ReturnToPreviousScreen();
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.GameAdded, game.Name);
            _ = SynchronizeLibraryDataAndHowLongToBeatAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.SaveGameError, exception.Message);
        }
    }

    private async Task<int> SynchronizeSteamLibraryAsync(CancellationToken cancellationToken)
    {
        IsLibrarySyncRunning = true;
        StatusMessage = null;
        try
        {
            var games = await gameLibrarySyncService.SynchronizeAsync(
                GameSourceId.Steam,
                cancellationToken);
            await LoadGamesAsync(cancellationToken);
            HasCompletedSteamSync = true;
            return games.Count;
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

    private async Task SynchronizeTimeToBeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            await gameTimeToBeatSyncService.SynchronizeAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.HltbSyncError, exception.Message);
        }
    }

    private void UpdateGameCard(Game game)
    {
        var allGamesIndex = allGames.FindIndex(card => card.Game.Id == game.Id);
        if (allGamesIndex < 0)
        {
            return;
        }

        var previousCard = allGames[allGamesIndex];
        var wasSelected = ReferenceEquals(SelectedGameCard, previousCard);
        game.IsHidden = previousCard.Game.IsHidden;
        var card = new GameCardViewModel(game, Settings.General.TimeZoneId)
        {
            IsSelected = wasSelected
        };
        allGames[allGamesIndex] = card;

        var visibleGameIndex = Games.IndexOf(previousCard);
        if (visibleGameIndex >= 0)
        {
            Games[visibleGameIndex] = card;
        }

        if (wasSelected && visibleGameIndex >= 0)
        {
            SelectedGameCard = card;
        }

        _ = card.LoadCoverAsync(artworkImageLoader);
    }
}
