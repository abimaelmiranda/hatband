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
using Hatband.Core.Models.Games;
using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Settings;

namespace Hatband.Integrations.Steam;

public sealed class SteamGameManagementProvider : IGameManagementService
{
    private readonly IHostApplicationLauncher hostApplicationLauncher;
    private readonly IHostSystemInfo hostSystemInfo;
    private readonly ISteamInstallationService steamInstallationService;
    private readonly ISettingsApi settingsApi;

    public SteamGameManagementProvider(
        IHostApplicationLauncher hostApplicationLauncher,
        IHostSystemInfo hostSystemInfo,
        ISteamInstallationService steamInstallationService,
        ISettingsApi settingsApi)
    {
        ArgumentNullException.ThrowIfNull(hostApplicationLauncher);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(steamInstallationService);
        ArgumentNullException.ThrowIfNull(settingsApi);
        this.hostApplicationLauncher = hostApplicationLauncher;
        this.hostSystemInfo = hostSystemInfo;
        this.steamInstallationService = steamInstallationService;
        this.settingsApi = settingsApi;
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

        var installations = await steamInstallationService.GetInstallationsAsync(cancellationToken);
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

    public Task<GameManagementResult> LaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        return LaunchSteamGameAsync(game, cancellationToken);
    }

    public GameProcessWatchTarget? GetProcessWatchTarget(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (game.SourceId != GameSourceId.Steam ||
            game.InstallationInfo is not { InstallDirectory: var installDirectory } ||
            !Directory.Exists(installDirectory))
        {
            return null;
        }

        return new GameProcessWatchTarget(installDirectory);
    }

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
        if (await hostApplicationLauncher.TryOpenUriAsync(steamUri, cancellationToken))
        {
            return restoreHostAfterFallback
                ? GameManagementResult.FallbackProtocolOpened
                : GameManagementResult.ProtocolOpened;
        }

        if (!IsSteamClientRunning() && await TryLaunchSteamClientAsync(cancellationToken))
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

        if (OperatingSystem.IsMacOS() && hostSystemInfo.Platform == HostOperatingSystem.MacOS &&
            await hostApplicationLauncher.TryLaunchApplicationAsync(
                "open",
                ["-a", "Steam", "--args", "-silent", "-applaunch", appId.ToString(CultureInfo.InvariantCulture)],
                cancellationToken))
        {
            return GameManagementResult.ProtocolOpened;
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
        var settings = await settingsApi.GetSectionAsync<ConnectorsSettings>(cancellationToken);
        return settings.Steam.SilentModeEnabled;
    }

    private bool IsSteamClientRunning()
    {
        var processNames = hostSystemInfo.Platform == HostOperatingSystem.MacOS
            ? new[] { "steam_osx", "Steam" }
            : new[] { "steam" };
        return processNames.Any(hostApplicationLauncher.IsProcessRunning);
    }

    private async Task<bool> TryCloseSteamClientGracefullyAsync(CancellationToken cancellationToken)
    {
        if (!IsSteamClientRunning())
        {
            return true;
        }

        if (!await hostApplicationLauncher.TryOpenUriAsync(new Uri("steam://exit"), cancellationToken))
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

        var arguments = new List<string> { "-silent", command, appId.ToString(CultureInfo.InvariantCulture) };
        if (volumeIndex is not null)
        {
            arguments.Add(volumeIndex.Value.ToString(CultureInfo.InvariantCulture));
        }

        var executable = hostSystemInfo.Platform switch
        {
            HostOperatingSystem.Linux => "steam",
            HostOperatingSystem.MacOS when OperatingSystem.IsMacOS() => "open",
            HostOperatingSystem.Windows when OperatingSystem.IsWindows() => GetWindowsSteamExecutable(),
            _ => null
        };
        if (executable is null)
        {
            return false;
        }

        if (hostSystemInfo.Platform == HostOperatingSystem.MacOS)
        {
            arguments.InsertRange(0, ["-g", "-a", "Steam", "--args"]);
        }

        return await hostApplicationLauncher.TryLaunchApplicationAsync(executable, arguments, cancellationToken);
    }

    private async Task<bool> TryLaunchSteamClientAsync(CancellationToken cancellationToken)
    {
        var executable = hostSystemInfo.Platform switch
        {
            HostOperatingSystem.Linux => "steam",
            HostOperatingSystem.MacOS when OperatingSystem.IsMacOS() => "open",
            HostOperatingSystem.Windows when OperatingSystem.IsWindows() => GetWindowsSteamExecutable(),
            _ => null
        };
        if (executable is null)
        {
            return false;
        }

        var arguments = hostSystemInfo.Platform == HostOperatingSystem.MacOS
            ? new[] { "-a", "Steam" }
            : Array.Empty<string>();
        return await hostApplicationLauncher.TryLaunchApplicationAsync(executable, arguments, cancellationToken);
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
