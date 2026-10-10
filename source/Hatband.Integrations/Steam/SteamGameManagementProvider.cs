using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Abstractions.Settings;
using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Host;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;
using Hatband.Core.Models.Compatibility;
using Hatband.Core.Models.Games;
using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Steam.Models;
using Hatband.Integrations.Settings;

namespace Hatband.Integrations.Steam;

public sealed class SteamGameManagementProvider : IGameManagementProvider
{
    private readonly IHostApplicationLauncher _hostApplicationLauncher;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly ISteamInstallationService _steamInstallationService;
    private readonly ISettingsApi _settingsApi;
    private readonly SteamGameLaunchOptionsProvider _launchOptionsProvider;

    public SteamGameManagementProvider(
        IHostApplicationLauncher hostApplicationLauncher,
        IHostSystemInfo hostSystemInfo,
        ISteamInstallationService steamInstallationService,
        ISettingsApi settingsApi,
        SteamGameLaunchOptionsProvider launchOptionsProvider)
    {
        ArgumentNullException.ThrowIfNull(hostApplicationLauncher);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(steamInstallationService);
        ArgumentNullException.ThrowIfNull(settingsApi);
        ArgumentNullException.ThrowIfNull(launchOptionsProvider);
        _hostApplicationLauncher = hostApplicationLauncher;
        _hostSystemInfo = hostSystemInfo;
        _steamInstallationService = steamInstallationService;
        _settingsApi = settingsApi;
        _launchOptionsProvider = launchOptionsProvider;
    }

    public GameSourceId SourceId => GameSourceId.Steam;

    public async Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        if (game.SourceId != GameSourceId.Steam || !await IsSilentModeEnabledAsync(cancellationToken))
        {
            return [];
        }

        var installations = await _steamInstallationService.GetInstallationsAsync(cancellationToken);
        if (installations.Count == 0)
        {
            return [];
        }

        var libraries = installations[0].Libraries;
        return libraries
            .Select(library => new GameInstallLocation(
                library.VolumeIndex.ToString(CultureInfo.InvariantCulture),
                $"{Path.GetFileName(library.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))} — {library.Path}"))
            .ToArray();
    }

    public async Task<GameManagementResult> InstallAsync(
        Game game,
        GameInstallLocation? location = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        if (!await IsSilentModeEnabledAsync(cancellationToken))
        {
            return await OpenSteamActionAsync(game, "install", cancellationToken);
        }

        if (location is not null && TryGetSteamAppId(game, out var appId) &&
            int.TryParse(location.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var volumeIndex) &&
            await TryCloseSteamClientGracefullyAsync(cancellationToken) &&
            await TryRunSteamCommandAsync("+app_install", appId, volumeIndex, cancellationToken))
        {
            return GameManagementResult.SilentCommandStarted;
        }

        return await OpenSteamActionAsync(game, "install", cancellationToken, restoreHostAfterFallback: true);
    }

    public Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default)
    {
        return UninstallSteamGameAsync(game, cancellationToken);
    }

    public async Task<GameLaunchResult> LaunchOriginalAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var result = await LaunchSteamGameAsync(game, cancellationToken);
        var installDirectory = game.InstallationInfo?.InstallDirectory;
        var watchTarget = result is not (GameManagementResult.Unavailable or GameManagementResult.Unsupported or GameManagementResult.FallbackUnavailable) &&
            !string.IsNullOrWhiteSpace(installDirectory) && Directory.Exists(installDirectory)
                ? new GameProcessWatchTarget(installDirectory)
                : null;
        return new GameLaunchResult(result, watchTarget);
    }

    public Task<IReadOnlyList<GameLaunchOption>> GetLaunchOptionsAsync(
        Game game,
        CancellationToken cancellationToken = default) =>
        _launchOptionsProvider.GetLaunchOptionsAsync(game, cancellationToken);

    public async Task<GameLaunchCommand> PrepareCustomLaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        if (game.SourceId != GameSourceId.Steam)
        {
            throw new InvalidOperationException("The Steam launch provider can only prepare Steam games.");
        }

        if (!uint.TryParse(game.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
        {
            throw new InvalidOperationException($"Steam game '{game.Name}' has no valid Steam app ID.");
        }

        var layer = game.CompatibilityLayer
            ?? throw new InvalidOperationException($"Custom compatibility is not configured for '{game.Name}'.");
        var installDirectory = game.InstallationInfo?.InstallDirectory
            ?? throw new InvalidOperationException($"Steam game '{game.Name}' has no installation directory.");
        if (!Directory.Exists(installDirectory))
        {
            throw new DirectoryNotFoundException($"Steam game installation directory does not exist: {installDirectory}");
        }

        var command = await ResolveCustomLaunchCommandAsync(game, layer, installDirectory, cancellationToken);

        var installation = await FindSteamInstallationAsync(installDirectory, cancellationToken);
        if (!await EnsureSteamClientRunningAsync(cancellationToken))
        {
            throw new InvalidOperationException("The Steam client could not be started for this custom Proton launch.");
        }

        return command with { EnvironmentVariables = CreateSteamEnvironmentVariables(appId, installation, installDirectory) };
    }

    private async Task<GameLaunchCommand> ResolveCustomLaunchCommandAsync(
        Game game,
        CompatibilityLayer layer,
        string installDirectory,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(layer.LaunchOptionId))
        {
            var options = await _launchOptionsProvider.GetLaunchOptionsAsync(game, cancellationToken);
            var selectedOption = options.FirstOrDefault(option => option.Id == layer.LaunchOptionId)
                ?? throw new InvalidOperationException($"The selected Steam launch option for '{game.Name}' is no longer available.");
            return CreateCustomLaunchCommand(selectedOption.ExecutablePath, selectedOption.Arguments, selectedOption.WorkingDirectory);
        }

        if (!string.IsNullOrWhiteSpace(layer.ExecutablePath))
        {
            return CreateCustomLaunchCommand(
                layer.ExecutablePath,
                layer.LaunchArguments,
                layer.WorkingDirectory ?? installDirectory);
        }

        var availableOptions = await _launchOptionsProvider.GetLaunchOptionsAsync(game, cancellationToken);
        if (availableOptions.Count == 1)
        {
            var option = availableOptions[0];
            return CreateCustomLaunchCommand(option.ExecutablePath, option.Arguments, option.WorkingDirectory);
        }

        if (availableOptions.Count == 0)
        {
            throw new InvalidOperationException($"No usable Steam launch option was found for '{game.Name}'.");
        }

        throw new InvalidOperationException($"Select a Steam launch option for '{game.Name}' before launching it.");
    }

    private static GameLaunchCommand CreateCustomLaunchCommand(string executablePath, string? arguments, string workingDirectory)
    {
        if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("The selected Steam executable is no longer available.", executablePath);
        }

        if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"The selected Steam working directory is unavailable: {workingDirectory}");
        }

        return new GameLaunchCommand(executablePath, arguments, workingDirectory, new Dictionary<string, string>());
    }

    private static Dictionary<string, string> CreateSteamEnvironmentVariables(
        uint appId,
        SteamInstallation installation,
        string installDirectory) => new(StringComparer.Ordinal)
        {
            ["GAMEID"] = $"umu-{appId.ToString(CultureInfo.InvariantCulture)}",
            ["STORE"] = "steam",
            ["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = installation.RootPath,
            ["STEAM_COMPAT_LIBRARY_PATHS"] = Path.Combine(FindLibraryPath(installation, installDirectory), "steamapps")
        };

    private async Task<GameManagementResult> OpenSteamActionAsync(
        Game game,
        string action,
        CancellationToken cancellationToken,
        bool restoreHostAfterFallback = false)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetSteamAppId(game, out var appId))
        {
            return GameManagementResult.Unavailable;
        }

        var steamUri = new Uri($"steam://{action}/{appId.ToString(CultureInfo.InvariantCulture)}");
        if (await _hostApplicationLauncher.TryOpenUriAsync(steamUri, cancellationToken))
        {
            return restoreHostAfterFallback
                ? GameManagementResult.FallbackProtocolOpened
                : GameManagementResult.ProtocolOpened;
        }

        if (!IsSteamClientRunning() && await TryLaunchSteamClientAsync([], cancellationToken))
        {
            return restoreHostAfterFallback
                ? GameManagementResult.FallbackStoreClientOpened
                : GameManagementResult.StoreClientOpened;
        }

        return restoreHostAfterFallback
            ? GameManagementResult.FallbackUnavailable
            : GameManagementResult.Unavailable;
    }

    private async Task<GameManagementResult> LaunchSteamGameAsync(Game game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetSteamAppId(game, out var appId))
        {
            return GameManagementResult.Unavailable;
        }

        if (!await IsSilentModeEnabledAsync(cancellationToken) || IsSteamClientRunning())
        {
            return await OpenSteamActionAsync(game, "run", cancellationToken);
        }

        if (await TryRunSteamCommandAsync("-applaunch", appId, null, cancellationToken))
        {
            return GameManagementResult.SilentCommandStarted;
        }

        return await OpenSteamActionAsync(game, "run", cancellationToken);
    }

    private async Task<GameManagementResult> UninstallSteamGameAsync(
        Game game,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetSteamAppId(game, out var appId))
        {
            return GameManagementResult.Unavailable;
        }

        if (!await IsSilentModeEnabledAsync(cancellationToken))
        {
            return await OpenSteamActionAsync(game, "uninstall", cancellationToken);
        }

        if (await TryCloseSteamClientGracefullyAsync(cancellationToken) &&
            await TryRunSteamCommandAsync("+app_uninstall", appId, null, cancellationToken))
        {
            return GameManagementResult.SilentCommandStarted;
        }

        return await OpenSteamActionAsync(game, "uninstall", cancellationToken, restoreHostAfterFallback: true);
    }

    private async Task<bool> IsSilentModeEnabledAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsApi.GetSectionAsync<ConnectorsSettings>(cancellationToken);
        return settings.Steam.SilentModeEnabled;
    }

    private bool IsSteamClientRunning()
    {
        var processNames = _hostSystemInfo.Platform == HostOperatingSystem.MacOS
            ? new[] { "steam_osx", "Steam" }
            : new[] { "steam" };
        return processNames.Any(_hostApplicationLauncher.IsProcessRunning);
    }

    private async Task<bool> TryCloseSteamClientGracefullyAsync(CancellationToken cancellationToken)
    {
        if (!IsSteamClientRunning())
        {
            return true;
        }

        if (!await _hostApplicationLauncher.TryOpenUriAsync(new Uri("steam://exit"), cancellationToken))
        {
            return false;
        }

        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSteamClientRunning())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return !IsSteamClientRunning();
    }

    private async Task<bool> TryRunSteamCommandAsync(
        string command,
        uint appId,
        int? volumeIndex,
        CancellationToken cancellationToken)
    {
        if (IsSteamClientRunning())
        {
            return false;
        }

        var steamArguments = new List<string> { "-silent", command, appId.ToString(CultureInfo.InvariantCulture) };
        if (volumeIndex is not null)
        {
            steamArguments.Add(volumeIndex.Value.ToString(CultureInfo.InvariantCulture));
        }

        return await TryLaunchSteamClientAsync(steamArguments, cancellationToken);
    }

    private async Task<bool> TryLaunchSteamClientAsync(
        IReadOnlyList<string> steamArguments,
        CancellationToken cancellationToken)
    {
        switch (_hostSystemInfo.Platform)
        {
            case HostOperatingSystem.Linux:
                return await TryLaunchSteamOnLinuxAsync(steamArguments, cancellationToken);
            case HostOperatingSystem.MacOS when OperatingSystem.IsMacOS():
                return await TryLaunchSteamOnMacOSAsync(steamArguments, cancellationToken);
            case HostOperatingSystem.Windows when OperatingSystem.IsWindows():
                return await TryLaunchSteamOnWindowsAsync(steamArguments, cancellationToken);
            default:
                return false;
        }
    }

    private async Task<SteamInstallation> FindSteamInstallationAsync(string gameInstallDirectory, CancellationToken cancellationToken)
    {
        var installations = await _steamInstallationService.GetInstallationsAsync(cancellationToken);
        foreach (var installation in installations)
        {
            if (installation.Libraries.Any(library => IsDirectoryWithin(gameInstallDirectory, library.Path)))
            {
                return installation;
            }
        }

        throw new InvalidOperationException($"Could not find the Steam library containing '{gameInstallDirectory}'.");
    }

    private static string FindLibraryPath(SteamInstallation installation, string gameInstallDirectory)
    {
        return installation.Libraries
            .FirstOrDefault(library => IsDirectoryWithin(gameInstallDirectory, library.Path))?.Path
            ?? throw new InvalidOperationException($"Could not find the Steam library containing '{gameInstallDirectory}'.");
    }

    private static bool IsDirectoryWithin(string candidateDirectory, string parentDirectory)
    {
        var candidatePath = Path.GetFullPath(candidateDirectory);
        var parentPath = Path.GetFullPath(parentDirectory);
        var relativePath = Path.GetRelativePath(parentPath, candidatePath);
        return relativePath != ".." &&
            !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !Path.IsPathRooted(relativePath);
    }

    private async Task<bool> EnsureSteamClientRunningAsync(CancellationToken cancellationToken)
    {
        if (IsSteamClientRunning())
        {
            return true;
        }

        if (!await TryLaunchSteamClientAsync(["-silent"], cancellationToken))
        {
            return false;
        }

        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSteamClientRunning())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return false;
    }

    private async Task<bool> TryLaunchSteamOnLinuxAsync(
        IReadOnlyList<string> steamArguments,
        CancellationToken cancellationToken)
    {
        if (await _hostApplicationLauncher.TryLaunchApplicationAsync("steam", steamArguments, cancellationToken))
        {
            return true;
        }

        var arguments = new List<string> { "run", "com.valvesoftware.Steam" };
        arguments.AddRange(steamArguments);
        return await _hostApplicationLauncher.TryLaunchApplicationAsync("flatpak", arguments, cancellationToken);
    }

    private Task<bool> TryLaunchSteamOnMacOSAsync(
        IReadOnlyList<string> steamArguments,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "-a", "Steam" };
        if (steamArguments.Count > 0)
        {
            arguments.Insert(0, "-g");
            arguments.Add("--args");
            arguments.AddRange(steamArguments);
        }

        return _hostApplicationLauncher.TryLaunchApplicationAsync("open", arguments, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    private Task<bool> TryLaunchSteamOnWindowsAsync(
        IReadOnlyList<string> steamArguments,
        CancellationToken cancellationToken)
    {
        var executable = GetWindowsSteamExecutable();
        if (executable is null)
        {
            return Task.FromResult(false);
        }

        return _hostApplicationLauncher.TryLaunchApplicationAsync(executable, steamArguments, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    private static string? GetWindowsSteamExecutable()
    {
        var path = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            "SteamExe",
            null) as string;
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }

    private static bool TryGetSteamAppId(Game game, out uint appId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.SourceId != GameSourceId.Steam)
        {
            appId = default;
            return false;
        }

        return uint.TryParse(game.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out appId);
    }
}
