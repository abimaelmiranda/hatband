using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.Core.Enums.Host;

namespace Hatband.App.ViewModels;

/// <summary>Shared draft state for choosing native execution or an installed Proton runtime and prefix.</summary>
public partial class CompatibilityEditorViewModel : ViewModelBase
{
    private readonly ICompatibilityToolDiscoveryService _toolDiscoveryService;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly IAppDataFileSystem _appDataFileSystem;
    private CompatibilityTool? _originalTool;

    public CompatibilityEditorViewModel(
        ICompatibilityToolDiscoveryService toolDiscoveryService,
        IHostSystemInfo hostSystemInfo,
        IAppDataFileSystem appDataFileSystem)
    {
        ArgumentNullException.ThrowIfNull(toolDiscoveryService);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        _toolDiscoveryService = toolDiscoveryService;
        _hostSystemInfo = hostSystemInfo;
        _appDataFileSystem = appDataFileSystem;
    }

    [ObservableProperty]
    public partial bool UseProton { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CompatibilityTool> InstalledTools { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<CompatibilityTool> DiscoveredTools { get; set; } = [];

    [ObservableProperty]
    public partial CompatibilityTool? SelectedTool { get; set; }

    [ObservableProperty]
    public partial bool UseManagedPrefix { get; set; } = true;

    [ObservableProperty]
    public partial string? CustomPrefixPath { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshingTools { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool IsLinux => _hostSystemInfo.Platform == HostOperatingSystem.Linux;

    public bool HasMissingSelectedTool => UseProton && SelectedTool is not null &&
        !DiscoveredTools.Any(tool => IsSameTool(tool, SelectedTool));

    public string ManagedPrefixPath => ConfiguredGameId is Guid gameId
        ? GetManagedPrefixPath(gameId)
        : _appDataFileSystem.GetPath("games");

    public Guid? ConfiguredGameId { get; private set; }

    public CompatibilityTool? ConfiguredTool => UseProton ? SelectedTool : null;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string? ValidateConfiguration()
    {
        if (!UseProton)
        {
            return null;
        }

        if (IsRefreshingTools)
        {
            return Resources.CompatibilityToolsRefreshing;
        }

        if (SelectedTool is null)
        {
            return Resources.CompatibilityToolRequired;
        }

        if (HasMissingSelectedTool)
        {
            return Resources.CompatibilityToolMissing;
        }

        if (!UseManagedPrefix)
        {
            if (string.IsNullOrWhiteSpace(CustomPrefixPath))
            {
                return Resources.CompatibilityPrefixRequired;
            }

            var customPrefixPath = CustomPrefixPath.Trim();
            if (!Path.IsPathFullyQualified(customPrefixPath))
            {
                return Resources.CompatibilityPrefixMustBeAbsolute;
            }

            if (!TryNormalizePrefixPath(customPrefixPath, out _))
            {
                return Resources.CompatibilityPrefixInvalid;
            }
        }

        return null;
    }

    public IBrush NativeButtonBackground => CreateModeBrush(!UseProton);

    public IBrush ProtonButtonBackground => CreateModeBrush(UseProton);

    public IBrush ManagedPrefixButtonBackground => CreateModeBrush(UseManagedPrefix);

    public IBrush CustomPrefixButtonBackground => CreateModeBrush(!UseManagedPrefix);

    public GameCompatibilityPrefix? CreatePrefix(Guid gameId)
    {
        if (!UseProton)
        {
            return null;
        }

        if (UseManagedPrefix)
        {
            if (ConfiguredGameId is Guid configuredGameId && configuredGameId != gameId)
            {
                throw new InvalidOperationException("The configured game ID does not match the prefix ID.");
            }

            return new GameCompatibilityPrefix(true, GetManagedPrefixPath(gameId));
        }

        if (string.IsNullOrWhiteSpace(CustomPrefixPath))
        {
            throw new InvalidOperationException(Resources.CompatibilityPrefixRequired);
        }

        var customPrefixPath = CustomPrefixPath.Trim();
        if (!TryNormalizePrefixPath(customPrefixPath, out var normalizedPrefixPath))
        {
            throw new InvalidOperationException(Resources.CompatibilityPrefixInvalid);
        }

        return new GameCompatibilityPrefix(false, normalizedPrefixPath);
    }

    public void Load(CompatibilityTool? tool, GameCompatibilityPrefix? prefix, Guid? gameId = null)
    {
        _originalTool = tool;
        ConfiguredGameId = gameId;
        UseProton = tool is not null;
        SelectedTool = tool;
        UseManagedPrefix = prefix?.IsManaged ?? true;
        CustomPrefixPath = prefix is { IsManaged: false } ? prefix.Path : null;
        ErrorMessage = null;
        OnPropertyChanged(nameof(ManagedPrefixPath));
        _ = RefreshToolsAsync();
    }

    [RelayCommand]
    private async Task RefreshToolsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLinux || IsRefreshingTools)
        {
            return;
        }

        IsRefreshingTools = true;
        ErrorMessage = null;
        DiscoveredTools = [];
        try
        {
            var tools = await _toolDiscoveryService.DiscoverInstalledToolsAsync(cancellationToken);
            var discovered = tools.ToList();
            DiscoveredTools = new ObservableCollection<CompatibilityTool>(discovered);
            var refreshed = discovered.ToList();
            if (_originalTool is not null && !refreshed.Any(tool => IsSameTool(tool, _originalTool)))
            {
                refreshed.Insert(0, _originalTool);
            }

            InstalledTools = new ObservableCollection<CompatibilityTool>(refreshed
                .OrderBy(tool => tool.Name, StringComparer.CurrentCultureIgnoreCase));
            var selectedTool = SelectedTool;
            if (selectedTool is not null)
            {
                var matchingTool = InstalledTools.FirstOrDefault(tool => IsSameTool(tool, selectedTool));
                if (matchingTool is not null)
                {
                    SelectedTool = matchingTool;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, Resources.CompatibilityToolsLoadError, exception.Message);
        }
        finally
        {
            IsRefreshingTools = false;
            OnPropertyChanged(nameof(HasMissingSelectedTool));
        }
    }

    partial void OnUseProtonChanged(bool value)
    {
        OnPropertyChanged(nameof(ConfiguredTool));
        OnPropertyChanged(nameof(HasMissingSelectedTool));
        OnPropertyChanged(nameof(NativeButtonBackground));
        OnPropertyChanged(nameof(ProtonButtonBackground));
    }

    partial void OnSelectedToolChanged(CompatibilityTool? value)
    {
        OnPropertyChanged(nameof(ConfiguredTool));
        OnPropertyChanged(nameof(HasMissingSelectedTool));
    }

    partial void OnInstalledToolsChanged(ObservableCollection<CompatibilityTool> value) =>
        OnPropertyChanged(nameof(HasMissingSelectedTool));

    partial void OnDiscoveredToolsChanged(ObservableCollection<CompatibilityTool> value) =>
        OnPropertyChanged(nameof(HasMissingSelectedTool));

    partial void OnUseManagedPrefixChanged(bool value)
    {
        OnPropertyChanged(nameof(ManagedPrefixButtonBackground));
        OnPropertyChanged(nameof(CustomPrefixButtonBackground));
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private static IBrush CreateModeBrush(bool isSelected) => new SolidColorBrush(Color.Parse(isSelected ? "#263640" : "#151C21"));

    private string GetManagedPrefixPath(Guid gameId) => _appDataFileSystem.GetPath($"games/{gameId:D}/prefix");

    private static bool TryNormalizePrefixPath(string path, [NotNullWhen(true)] out string? normalizedPath)
    {
        normalizedPath = null;
        if (!Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsSameTool(CompatibilityTool left, CompatibilityTool right) =>
        string.Equals(left.InstallationPath, right.InstallationPath, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
}
