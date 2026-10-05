using Hatband.Core.Abstractions.Host;
using Hatband.Core.Enums.Host;
using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Steam.Models;
using Microsoft.Win32;
using SteamKit2;

namespace Hatband.Integrations.Steam;

public sealed class SteamInstallationService : ISteamInstallationService
{
    private readonly IHostSystemInfo hostSystemInfo;

    public SteamInstallationService(IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        this.hostSystemInfo = hostSystemInfo;
    }

    public Task<IReadOnlyList<SteamInstallation>> GetInstallationsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => FindInstallations(cancellationToken), cancellationToken);
    }

    private IReadOnlyList<SteamInstallation> FindInstallations(CancellationToken cancellationToken)
    {
        var installations = new List<SteamInstallation>();
        foreach (var rootPath in FindInstallationRoots(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            installations.Add(new SteamInstallation
            {
                RootPath = rootPath,
                Libraries = FindLibraries(rootPath, cancellationToken)
            });
        }

        return installations;
    }

    private IReadOnlyList<string> FindInstallationRoots(CancellationToken cancellationToken)
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
                paths.Add(Path.Combine(home, ".steam", "root"));
                paths.Add(Path.Combine(home, ".steam", "steam"));
                paths.Add(Path.Combine(home, ".local", "share", "Steam"));
                paths.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
                paths.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"));
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

        return DistinctExistingDirectories(paths, hostSystemInfo.Platform == HostOperatingSystem.Windows)
            .Where(path =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Directory.Exists(Path.Combine(path, "steamapps"));
            })
            .ToArray();
    }

    private IReadOnlyList<SteamLibraryLocation> FindLibraries(
        string steamRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rootLocations = new Dictionary<int, string>();
        var libraryFolders = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFolders))
        {
            rootLocations.TryAdd(0, steamRoot);
        }
        else
        {
            try
            {
                using var stream = File.OpenRead(libraryFolders);
                var root = new KeyValue();
                root.ReadAsText(stream);
                var folders = FindChild(root, "libraryfolders") ?? root;
                foreach (var folder in folders.Children)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!int.TryParse(folder.Name, out var volumeIndex))
                    {
                        continue;
                    }

                    var path = GetValue(folder, "path") ?? folder.Value;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        rootLocations.TryAdd(volumeIndex, path);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
            {
                // Use the default library if the additional-library index is malformed.
            }
        }

        if (rootLocations.Count == 0)
        {
            rootLocations.TryAdd(0, steamRoot);
        }

        var seenPaths = new HashSet<string>(hostSystemInfo.Platform == HostOperatingSystem.Windows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        var libraries = new List<SteamLibraryLocation>();
        foreach (var (volumeIndex, path) in rootLocations.OrderBy(location => location.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = ResolveDirectoryPath(path);
            if (Directory.Exists(fullPath) && seenPaths.Add(fullPath))
            {
                libraries.Add(new SteamLibraryLocation
                {
                    VolumeIndex = volumeIndex,
                    Path = fullPath
                });
            }
        }

        return libraries;
    }

    private static IReadOnlyList<string> DistinctExistingDirectories(IEnumerable<string> paths, bool isWindows)
    {
        return paths
            .Where(Directory.Exists)
            .Select(ResolveDirectoryPath)
            .Distinct(isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveDirectoryPath(string path)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        return directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName;
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
