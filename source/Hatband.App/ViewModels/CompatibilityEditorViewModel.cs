using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Host;
using Hatband.Core.Models.Compatibility;
using Hatband.Core.Models.Games;

namespace Hatband.App.ViewModels;

public partial class CompatibilityEditorViewModel : ViewModelBase
{
    private readonly ICompatibilityToolDiscoveryService toolDiscoveryService;
    private readonly IGameManagementService gameManagementService;
    private readonly IHostSystemInfo hostSystemInfo;
    private readonly IAppDataFileSystem appDataFileSystem;
    private CompatibilityLayer? originalLayer;
    private Game? game;
    private bool isApplyingLaunchOption;
    private bool isApplyingRefreshResult;
    private CancellationTokenSource? refreshCancellation;
    private long refreshGeneration;
    private int configurationRevision;
    private int launchConfigurationRevision;

    public CompatibilityEditorViewModel(
        ICompatibilityToolDiscoveryService toolDiscoveryService,
        IGameManagementService gameManagementService,
        IHostSystemInfo hostSystemInfo,
        IAppDataFileSystem appDataFileSystem)
    {
        ArgumentNullException.ThrowIfNull(toolDiscoveryService);
        ArgumentNullException.ThrowIfNull(gameManagementService);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        this.toolDiscoveryService = toolDiscoveryService;
        this.gameManagementService = gameManagementService;
        this.hostSystemInfo = hostSystemInfo;
        this.appDataFileSystem = appDataFileSystem;
        EnvironmentVariables.CollectionChanged += OnEnvironmentVariablesCollectionChanged;
    }

    [ObservableProperty]
    public partial GameExecutionMode ExecutionMode { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CompatibilityTool> InstalledTools { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<GameLaunchOption> LaunchOptions { get; set; } = [];

    [ObservableProperty]
    public partial CompatibilityTool? SelectedTool { get; set; }

    [ObservableProperty]
    public partial GameLaunchOption? SelectedLaunchOption { get; set; }

    [ObservableProperty]
    public partial string? ExecutablePath { get; set; }

    [ObservableProperty]
    public partial string? WorkingDirectory { get; set; }

    [ObservableProperty]
    public partial string LaunchArguments { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AdditionalArguments { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<EnvironmentVariableViewModel> EnvironmentVariables { get; set; } = [];

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool IsLinux => hostSystemInfo.Platform == HostOperatingSystem.Linux;

    public bool IsSteamGame => game?.SourceId == GameSourceId.Steam;

    public bool IsGameInstalled => game?.InstallationInfo is not null;

    public bool IsCustom => ExecutionMode == GameExecutionMode.Custom;

    public string? PrefixPath => game is null ? null : appDataFileSystem.GetGamePrefixPath(game.Id);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public IBrush OriginalButtonBackground => CreateModeBrush(!IsCustom);

    public IBrush CustomButtonBackground => CreateModeBrush(IsCustom);

    public string? ValidateConfiguration()
    {
        if (!IsCustom)
        {
            return null;
        }

        if (IsRefreshing)
        {
            return Resources.CompatibilityToolsRefreshing;
        }

        if (SelectedTool is null || !Directory.Exists(SelectedTool.InstallationPath))
        {
            return Resources.CompatibilityToolRequired;
        }

        if (IsSteamGame && IsGameInstalled && SelectedLaunchOption is null &&
            string.IsNullOrWhiteSpace(ExecutablePath))
        {
            return Resources.CompatibilityLaunchOptionRequired;
        }

        if (!ValidateEnvironmentVariables(out var environmentError))
        {
            return environmentError;
        }

        return null;
    }

    public CompatibilityLayer CreateCompatibilityLayer()
    {
        var layer = new CompatibilityLayer
        {
            ExecutionMode = originalLayer?.ExecutionMode ?? GameExecutionMode.Original,
            Tool = originalLayer?.Tool,
            LaunchOptionId = originalLayer?.LaunchOptionId,
            ExecutablePath = originalLayer?.ExecutablePath,
            WorkingDirectory = originalLayer?.WorkingDirectory,
            LaunchArguments = originalLayer?.LaunchArguments,
            AdditionalArguments = originalLayer?.AdditionalArguments,
            EnvironmentVariables = originalLayer?.EnvironmentVariables.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [],
            Tier = originalLayer?.Tier ?? GameCompatibilityTier.Unknown
        };
        layer.ExecutionMode = ExecutionMode;
        if (!IsCustom)
        {
            return layer;
        }

        layer.Tool = SelectedTool ?? throw new InvalidOperationException(Resources.CompatibilityToolRequired);
        layer.LaunchOptionId = SelectedLaunchOption?.Id;
        layer.ExecutablePath = NormalizeOptionalPath(ExecutablePath);
        layer.WorkingDirectory = NormalizeOptionalPath(WorkingDirectory);
        layer.LaunchArguments = NormalizeOptionalValue(LaunchArguments);
        layer.AdditionalArguments = string.IsNullOrWhiteSpace(AdditionalArguments) ? null : AdditionalArguments.Trim();
        layer.EnvironmentVariables = CreateEnvironmentVariables();
        return layer;
    }

    public void Load(Game selectedGame)
    {
        ArgumentNullException.ThrowIfNull(selectedGame);
        refreshCancellation?.Cancel();
        refreshGeneration++;
        configurationRevision = 0;
        game = selectedGame;
        originalLayer = selectedGame.CompatibilityLayer;
        ExecutionMode = originalLayer?.ExecutionMode ?? GameExecutionMode.Original;
        SelectedTool = originalLayer?.Tool;
        SelectedLaunchOption = null;
        ExecutablePath = originalLayer?.ExecutablePath;
        WorkingDirectory = originalLayer?.WorkingDirectory;
        LaunchArguments = originalLayer?.LaunchArguments ?? string.Empty;
        AdditionalArguments = originalLayer?.AdditionalArguments ?? string.Empty;
        foreach (var variable in EnvironmentVariables)
        {
            variable.PropertyChanged -= OnEnvironmentVariablePropertyChanged;
        }

        EnvironmentVariables.Clear();
        foreach (var variable in (originalLayer?.EnvironmentVariables ?? [])
            .Select(pair => new EnvironmentVariableViewModel(pair.Key, pair.Value)))
        {
            EnvironmentVariables.Add(variable);
        }

        isApplyingRefreshResult = true;
        try
        {
            LaunchOptions.Clear();
            InstalledTools.Clear();
            SelectedTool = originalLayer?.Tool;
            SelectedLaunchOption = null;
        }
        finally
        {
            isApplyingRefreshResult = false;
        }
        ErrorMessage = null;
        configurationRevision = 0;
        launchConfigurationRevision = 0;
        OnPropertyChanged(nameof(IsSteamGame));
        OnPropertyChanged(nameof(IsGameInstalled));
        NotifyConfigurationChanged();
        _ = RefreshAsync();
    }

    public void SetOriginalMode() => ExecutionMode = GameExecutionMode.Original;

    public void SetCustomMode() => ExecutionMode = GameExecutionMode.Custom;

    [RelayCommand]
    private void AddEnvironmentVariable() => EnvironmentVariables.Add(new EnvironmentVariableViewModel());

    [RelayCommand]
    private void RemoveEnvironmentVariable(EnvironmentVariableViewModel variable) => EnvironmentVariables.Remove(variable);

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLinux || game is null)
        {
            return;
        }

        refreshCancellation?.Cancel();
        var currentGame = game;
        var generation = ++refreshGeneration;
        var revision = configurationRevision;
        var launchRevision = launchConfigurationRevision;
        var refreshSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        refreshCancellation = refreshSource;
        var refreshToken = refreshSource.Token;
        IsRefreshing = true;
        ErrorMessage = null;
        try
        {
            var tools = await toolDiscoveryService.DiscoverInstalledToolsAsync(refreshToken);
            if (!IsCurrentRefresh(currentGame, generation, refreshToken))
            {
                return;
            }

            var selectedToolPath = SelectedTool?.InstallationPath;
            var selectedTool = SelectedTool;
            var draftChanged = configurationRevision != revision;
            isApplyingRefreshResult = true;
            try
            {
                ReplaceItems(InstalledTools, tools);
                if (selectedToolPath is not null)
                {
                    SelectedTool = tools.FirstOrDefault(tool => PathsEqual(tool.InstallationPath, selectedToolPath)) ?? selectedTool;
                }
                else if (!draftChanged && ExecutionMode == GameExecutionMode.Custom)
                {
                    SelectedTool = tools.FirstOrDefault();
                }
            }
            finally
            {
                isApplyingRefreshResult = false;
            }

            if (currentGame.SourceId == GameSourceId.Steam && currentGame.InstallationInfo is not null)
            {
                var options = await gameManagementService.GetLaunchOptionsAsync(CreateLaunchOptionsQuerySnapshot(currentGame), refreshToken);
                if (!IsCurrentRefresh(currentGame, generation, refreshToken))
                {
                    return;
                }

                var currentSelectedOption = SelectedLaunchOption;
                var selectedOption = ResolveRefreshedLaunchOption(
                    options,
                    currentSelectedOption,
                    launchRevision,
                    out var shouldApplyLaunchOption);
                isApplyingRefreshResult = true;
                try
                {
                    ReplaceItems(LaunchOptions, options);
                    SelectedLaunchOption = selectedOption;
                    if (shouldApplyLaunchOption && selectedOption is not null)
                    {
                        ApplyLaunchOption(selectedOption);
                    }
                }
                finally
                {
                    isApplyingRefreshResult = false;
                }
            }
        }
        catch (OperationCanceledException) when (refreshToken.IsCancellationRequested)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
        catch (Exception exception)
        {
            if (IsCurrentRefresh(currentGame, generation, refreshToken))
            {
                ErrorMessage = string.Format(CultureInfo.CurrentCulture, Resources.CompatibilityToolsLoadError, exception.Message);
            }
        }
        finally
        {
            if (generation == refreshGeneration)
            {
                IsRefreshing = false;
                NotifyConfigurationChanged();
            }

            if (ReferenceEquals(refreshCancellation, refreshSource))
            {
                refreshCancellation = null;
            }

            refreshSource.Dispose();
        }
    }

    partial void OnExecutionModeChanged(GameExecutionMode value) => MarkDraftChanged();
    partial void OnSelectedToolChanged(CompatibilityTool? value) => MarkDraftChanged();
    partial void OnSelectedLaunchOptionChanged(GameLaunchOption? value)
    {
        if (value is not null && !isApplyingRefreshResult)
        {
            ApplyLaunchOption(value);
        }

        if (!isApplyingRefreshResult)
        {
            configurationRevision++;
            launchConfigurationRevision++;
        }
        NotifyConfigurationChanged();
    }
    partial void OnExecutablePathChanged(string? value) => OnLaunchConfigurationEdited();
    partial void OnWorkingDirectoryChanged(string? value) => OnLaunchConfigurationEdited();
    partial void OnLaunchArgumentsChanged(string value) => OnLaunchConfigurationEdited();
    partial void OnAdditionalArgumentsChanged(string value) => MarkDraftChanged();
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private bool ValidateEnvironmentVariables(out string? error)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variable in EnvironmentVariables)
        {
            if (!GameLaunchEnvironmentPolicy.IsValidVariableName(variable.Name))
            {
                error = Resources.CompatibilityEnvironmentVariableInvalid;
                return false;
            }

            if (GameLaunchEnvironmentPolicy.IsReservedVariableName(variable.Name) || !names.Add(variable.Name))
            {
                error = Resources.CompatibilityEnvironmentVariableReserved;
                return false;
            }
        }

        error = null;
        return true;
    }

    private Dictionary<string, string> CreateEnvironmentVariables() =>
        EnvironmentVariables.ToDictionary(variable => variable.Name.Trim(), variable => variable.Value);

    private void NotifyConfigurationChanged()
    {
        OnPropertyChanged(nameof(IsCustom));
        OnPropertyChanged(nameof(PrefixPath));
        OnPropertyChanged(nameof(OriginalButtonBackground));
        OnPropertyChanged(nameof(CustomButtonBackground));
    }

    private void OnLaunchConfigurationEdited()
    {
        MarkDraftChanged();
        if (!isApplyingRefreshResult)
        {
            launchConfigurationRevision++;
        }

        if (!isApplyingLaunchOption && SelectedLaunchOption is not null)
        {
            SelectedLaunchOption = null;
        }

        NotifyConfigurationChanged();
    }

    private void MarkDraftChanged()
    {
        if (!isApplyingRefreshResult)
        {
            configurationRevision++;
        }

        NotifyConfigurationChanged();
    }

    private void ApplyLaunchOption(GameLaunchOption option)
    {
        isApplyingLaunchOption = true;
        try
        {
            ExecutablePath = option.ExecutablePath;
            WorkingDirectory = option.WorkingDirectory;
            LaunchArguments = option.Arguments ?? string.Empty;
        }
        finally
        {
            isApplyingLaunchOption = false;
        }
    }

    private bool IsCurrentRefresh(Game expectedGame, long generation, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && generation == refreshGeneration && ReferenceEquals(game, expectedGame);

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private static Game CreateLaunchOptionsQuerySnapshot(Game sourceGame) => new()
    {
        Id = sourceGame.Id,
        Name = sourceGame.Name,
        SourceId = sourceGame.SourceId,
        SourceGameId = sourceGame.SourceGameId,
        Metadata = sourceGame.Metadata with { },
        InstallationInfo = sourceGame.InstallationInfo is { } installationInfo
            ? new GameInstallationInfo { InstallDirectory = installationInfo.InstallDirectory }
            : null
    };

    private GameLaunchOption? ResolveRefreshedLaunchOption(
        IReadOnlyList<GameLaunchOption> options,
        GameLaunchOption? currentSelection,
        int launchRevisionAtStart,
        out bool shouldApplyOption)
    {
        shouldApplyOption = false;

        if (launchConfigurationRevision != launchRevisionAtStart)
        {
            return FindOption(options, currentSelection?.Id) ?? currentSelection;
        }

        if (currentSelection is not null)
        {
            var refreshedOption = FindOption(options, currentSelection.Id);
            shouldApplyOption = refreshedOption is not null;
            return refreshedOption ?? currentSelection;
        }

        if (launchRevisionAtStart != 0)
        {
            return null;
        }

        if (originalLayer?.LaunchOptionId is { } originalOptionId)
        {
            var originalOption = FindOption(options, originalOptionId);
            shouldApplyOption = originalOption is not null;
            return originalOption;
        }

        if (string.IsNullOrWhiteSpace(ExecutablePath) && options.Count == 1)
        {
            shouldApplyOption = true;
            return options[0];
        }

        return null;
    }

    private static GameLaunchOption? FindOption(IReadOnlyList<GameLaunchOption> options, string? optionId)
    {
        if (optionId is null)
        {
            return null;
        }

        return options.FirstOrDefault(option => option.Id == optionId);
    }

    private void OnEnvironmentVariablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.OldItems is not null)
        {
            foreach (EnvironmentVariableViewModel variable in eventArgs.OldItems)
            {
                variable.PropertyChanged -= OnEnvironmentVariablePropertyChanged;
            }
        }

        if (eventArgs.NewItems is not null)
        {
            foreach (EnvironmentVariableViewModel variable in eventArgs.NewItems)
            {
                variable.PropertyChanged += OnEnvironmentVariablePropertyChanged;
            }
        }

        MarkDraftChanged();
    }

    private void OnEnvironmentVariablePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) => MarkDraftChanged();

    private static string? NormalizeOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path.Trim());

    private static string? NormalizeOptionalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IBrush CreateModeBrush(bool isSelected) =>
        new SolidColorBrush(Color.Parse(isSelected ? "#263640" : "#151C21"));

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
}
