using System.Globalization;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Hatband.Plugins.Steam.Abstractions;
using Hatband.Plugins.Steam.Library;
using Hatband.Plugins.Steam.Models;
using Microsoft.Extensions.Logging;
using ValveKeyValue;

namespace Hatband.Plugins.Steam.Games;

public sealed class SteamGameLaunchOptionsProvider
{
    private readonly ISteamInstallationService steamInstallationService;
    private readonly ILogger<SteamGameLaunchOptionsProvider> logger;
    private readonly SteamAppInfoReader appInfoReader = new();

    public SteamGameLaunchOptionsProvider(
        ISteamInstallationService steamInstallationService,
        ILogger<SteamGameLaunchOptionsProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(steamInstallationService);
        ArgumentNullException.ThrowIfNull(logger);
        this.steamInstallationService = steamInstallationService;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<GameLaunchOption>> GetLaunchOptionsAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.SourceId != GameSourceIds.Steam ||
            !uint.TryParse(game.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId) ||
            game.InstallationInfo is not { } installationInfo)
        {
            return [];
        }

        var installations = await steamInstallationService.GetInstallationsAsync(cancellationToken);
        return await Task.Run(
            () => FindLaunchOptions(installationInfo.InstallDirectory, appId, installations, cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<GameLaunchOption> FindLaunchOptions(
        string installDirectory,
        uint appId,
        IReadOnlyList<SteamInstallation> installations,
        CancellationToken cancellationToken)
    {
        var gameInstallDirectory = ResolveExistingPath(installDirectory, isDirectory: true);
        if (gameInstallDirectory is null)
        {
            logger.LogDebug("Steam launch metadata unavailable for app {AppId}: install directory was not found.", appId);
            return [];
        }

        foreach (var installation in installations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ContainsGameInstallation(installation, gameInstallDirectory))
            {
                continue;
            }

            var appInfoPath = Path.Combine(installation.RootPath, "appcache", "appinfo.vdf");
            if (!File.Exists(appInfoPath))
            {
                continue;
            }

            try
            {
                var appInfo = appInfoReader.ReadAppInfo(appInfoPath, appId, cancellationToken);
                if (appInfo is not null)
                {
                    return ReadLaunchOptions(appInfo, appId, gameInstallDirectory, cancellationToken, logger);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsAppInfoReadException(exception))
            {
                logger.LogWarning(
                    "Could not read Steam launch metadata for app {AppId} ({ErrorType}).",
                    appId,
                    exception.GetType().Name);
            }
        }

        return [];
    }

    private static IReadOnlyList<GameLaunchOption> ReadLaunchOptions(
        KVObject appInfo,
        uint appId,
        string installDirectory,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        if (!appInfo.TryGetValue("config", out var config) ||
            !config.TryGetValue("launch", out var launchOptions))
        {
            return [];
        }

        var launchOptionsList = new List<GameLaunchOption>();
        foreach (var (key, launch) in launchOptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!launch.TryGetValue("executable", out var executableValue))
            {
                logger.LogDebug("Skipping Steam launch option {AppId}:{OptionId}: executable is missing.", appId, key);
                continue;
            }

            if (!IsWindowsLaunchOption(launch))
            {
                logger.LogDebug("Skipping Steam launch option {AppId}:{OptionId}: it is not a Windows option.", appId, key);
                continue;
            }

            var executable = executableValue.ToString()
                .Replace("%INSTALLDIR%", installDirectory, StringComparison.OrdinalIgnoreCase)
                .Trim('"', ' ')
                .Replace('\\', Path.DirectorySeparatorChar);
            var requestedExecutablePath = Path.IsPathFullyQualified(executable)
                ? executable
                : Path.GetFullPath(Path.Combine(installDirectory, executable));
            var executablePath = ResolveExistingPath(requestedExecutablePath, isDirectory: false);
            if (executablePath is null)
            {
                logger.LogDebug("Skipping Steam launch option {AppId}:{OptionId}: executable was not found.", appId, key);
                continue;
            }

            var workingDirectory = launch.TryGetValue("workingdir", out var workingDirectoryValue)
                ? workingDirectoryValue.ToString()
                    .Replace("%INSTALLDIR%", installDirectory, StringComparison.OrdinalIgnoreCase)
                    .Trim('"', ' ')
                : installDirectory;
            workingDirectory = workingDirectory.Replace('\\', Path.DirectorySeparatorChar);
            if (!Path.IsPathFullyQualified(workingDirectory))
            {
                workingDirectory = Path.GetFullPath(Path.Combine(installDirectory, workingDirectory));
            }

            var resolvedWorkingDirectory = ResolveExistingPath(workingDirectory, isDirectory: true);
            if (resolvedWorkingDirectory is null)
            {
                logger.LogDebug("Skipping Steam launch option {AppId}:{OptionId}: working directory was not found.", appId, key);
                continue;
            }

            var arguments = launch.TryGetValue("arguments", out var argumentValue)
                ? argumentValue.ToString()
                : null;
            var name = launch.TryGetValue("description", out var description) &&
                       !string.IsNullOrWhiteSpace(description.ToString())
                ? description.ToString()
                : $"Launch option {key}";

            launchOptionsList.Add(new GameLaunchOption(
                $"{appId}:{key}",
                $"{name} ({Path.GetFileName(executablePath)})",
                executablePath,
                arguments,
                resolvedWorkingDirectory));
        }

        return launchOptionsList;
    }

    private static bool IsWindowsLaunchOption(KVObject launch)
    {
        if (!launch.TryGetValue("config", out var launchConfig) ||
            !launchConfig.TryGetValue("oslist", out var osList))
        {
            return true;
        }

        return osList.ToString()
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains("windows", StringComparer.OrdinalIgnoreCase);
    }

    private static bool ContainsGameInstallation(
        SteamInstallation installation,
        string gameInstallDirectory)
    {
        return installation.Libraries.Any(library =>
        {
            var commonDirectory = Path.Combine(library.Path, "steamapps", "common");
            var resolvedCommonDirectory = ResolveExistingPath(commonDirectory, isDirectory: true);
            return resolvedCommonDirectory is not null && IsSameOrChildPath(gameInstallDirectory, resolvedCommonDirectory);
        });
    }

    private static bool IsSameOrChildPath(string candidate, string parent)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var relativePath = Path.GetRelativePath(parent, candidate);
        return relativePath == "." ||
               (!Path.IsPathRooted(relativePath) && relativePath != ".." &&
                !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", comparison));
    }

    private static string? ResolveExistingPath(string path, bool isDirectory)
    {
        var fullPath = Path.GetFullPath(path);
        if (isDirectory ? Directory.Exists(fullPath) : File.Exists(fullPath))
        {
            return ResolveFinalSymlink(fullPath, isDirectory);
        }

        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        var currentPath = root;
        var pathParts = fullPath[root.Length..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        foreach (var pathPart in pathParts)
        {
            if (!Directory.Exists(currentPath))
            {
                return null;
            }

            var matchingEntry = Directory.EnumerateFileSystemEntries(currentPath)
                .FirstOrDefault(entry => string.Equals(
                    Path.GetFileName(entry),
                    pathPart,
                    StringComparison.OrdinalIgnoreCase));
            if (matchingEntry is null)
            {
                return null;
            }

            currentPath = matchingEntry;
        }

        return isDirectory
            ? Directory.Exists(currentPath) ? ResolveFinalSymlink(currentPath, isDirectory: true) : null
            : File.Exists(currentPath) ? ResolveFinalSymlink(currentPath, isDirectory: false) : null;
    }

    private static string ResolveFinalSymlink(string path, bool isDirectory)
    {
        FileSystemInfo fileSystemInfo = isDirectory ? new DirectoryInfo(path) : new FileInfo(path);
        return fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path;
    }

    private static bool IsAppInfoReadException(Exception exception)
    {
        return exception is IOException ||
               exception is UnauthorizedAccessException ||
               exception is InvalidDataException ||
               exception is ArgumentException ||
               exception is OverflowException ||
               exception is KeyValueException ||
               exception is FormatException;
    }
}
