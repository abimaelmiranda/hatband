using Hatband.Core.Abstractions;
using Hatband.Core.Enums;
using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Steam.Models;
using Microsoft.Win32;
using SteamKit2;

namespace Hatband.Integrations.Steam;

public sealed class SteamInstalledGameScanner : ISteamInstalledGameScanner
{
    private readonly IHostSystemInfo hostSystemInfo;

    public SteamInstalledGameScanner(IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        this.hostSystemInfo = hostSystemInfo;
    }

    public Task<IReadOnlyList<SteamLibraryGame>> ScanAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanInstalledGames(cancellationToken), cancellationToken);
    }

    private IReadOnlyList<SteamLibraryGame> ScanInstalledGames(CancellationToken cancellationToken)
    {
        var gamesByAppId = new Dictionary<uint, SteamLibraryGame>();

        foreach (var libraryPath in FindLibraryPaths(FindSteamRoots(), hostSystemInfo.Platform == HostOperatingSystem.Windows))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var steamAppsPath = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamAppsPath))
            {
                continue;
            }

            foreach (var manifestPath in Directory.EnumerateFiles(steamAppsPath, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var game = ReadInstalledGame(manifestPath);
                    if (game is not null)
                    {
                        gamesByAppId.TryAdd(game.AppId, game);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
                {
                    // Steam can leave partially written manifests; continue with other games.
                }
            }
        }

        return gamesByAppId.Values
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static SteamLibraryGame? ReadInstalledGame(string manifestPath)
    {
        using var stream = File.OpenRead(manifestPath);
        var manifest = new KeyValue();
        manifest.ReadAsText(stream);
        var appState = FindChild(manifest, "AppState") ?? manifest;
        var stateFlags = GetValue(appState, "StateFlags");
        if (!int.TryParse(stateFlags, out var flags) || (flags & 4) == 0)
        {
            return null;
        }

        var appId = GetValue(appState, "appid") ?? GetValue(appState, "appID");
        var name = GetValue(appState, "name") ?? GetValue(FindChild(appState, "UserConfig"), "name");
        var installDirectoryName = GetValue(appState, "installdir");
        var steamAppsDirectory = Path.GetDirectoryName(manifestPath);
        if (!uint.TryParse(appId, out var parsedAppId) || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(installDirectoryName) || steamAppsDirectory is null)
        {
            return null;
        }

        var installDirectory = Path.Combine(steamAppsDirectory, "common", installDirectoryName);
        if (!Directory.Exists(installDirectory))
        {
            installDirectory = Path.Combine(steamAppsDirectory, "music", installDirectoryName);
            if (!Directory.Exists(installDirectory))
            {
                return null;
            }
        }

        return new SteamLibraryGame
        {
            AppId = parsedAppId,
            Name = name.Trim(),
            IsInstalled = true,
            InstallDirectory = installDirectory
        };
    }

    private IReadOnlyList<string> FindSteamRoots()
    {
        var paths = new List<string>();
        var compatPath = Environment.GetEnvironmentVariable("STEAM_COMPAT_CLIENT_INSTALL_PATH");
        if (!string.IsNullOrWhiteSpace(compatPath))
        {
            paths.Add(compatPath);
        }

        var home = hostSystemInfo.UserProfileDirectory;
        if (!string.IsNullOrWhiteSpace(home))
        {
            if (hostSystemInfo.Platform == HostOperatingSystem.MacOS)
            {
                paths.Add(Path.Combine(home, "Library", "Application Support", "Steam"));
            }
            else
            {
                paths.Add(Path.Combine(home, ".steam", "steam"));
                paths.Add(Path.Combine(home, ".steam", "root"));
                paths.Add(Path.Combine(home, ".local", "share", "Steam"));
                paths.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
            }
        }

        if (hostSystemInfo.Platform == HostOperatingSystem.Windows && OperatingSystem.IsWindows())
        {
            using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var registryPath = steamKey?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(registryPath))
            {
                paths.Add(registryPath);
            }
        }

        return DistinctExistingDirectories(paths, hostSystemInfo.Platform == HostOperatingSystem.Windows);
    }

    private static IReadOnlyList<string> FindLibraryPaths(IEnumerable<string> steamRoots, bool isWindows)
    {
        var paths = new List<string>();
        foreach (var steamRoot in steamRoots)
        {
            paths.Add(steamRoot);
            var libraryFolders = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFolders))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(libraryFolders);
                var root = new KeyValue();
                root.ReadAsText(stream);
                var folders = FindChild(root, "libraryfolders") ?? root;
                foreach (var folder in folders.Children)
                {
                    var path = GetValue(folder, "path") ?? folder.Value;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        paths.Add(path);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
            {
                // Keep the default library even if the additional-library index is malformed.
            }
        }

        return DistinctExistingDirectories(paths, isWindows);
    }

    private static IReadOnlyList<string> DistinctExistingDirectories(IEnumerable<string> paths, bool isWindows)
    {
        return paths
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
    }

    private static KeyValue? FindChild(KeyValue? parent, string name)
    {
        return parent?.Children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetValue(KeyValue? parent, string name)
    {
        var value = FindChild(parent, name)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
