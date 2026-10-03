using Hatband.Core.Abstractions;
using Hatband.Core.Enums;
using Hatband.Core.Models;

namespace Hatband.Infrastructure.Services;

public sealed class ProtonToolDiscoveryService : IProtonToolDiscoveryService
{
    private const string HatbandRunnersDirectory = "proton/runners";

    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IHostSystemInfo hostSystemInfo;

    public ProtonToolDiscoveryService(
        IAppDataFileSystem appDataFileSystem,
        IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        this.appDataFileSystem = appDataFileSystem;
        this.hostSystemInfo = hostSystemInfo;
    }

    public Task<IReadOnlyList<ProtonTool>> DiscoverInstalledToolsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLinuxHost();

        var protonTools = new List<ProtonTool>();
        AddToolsFromDirectory(
            appDataFileSystem.GetPath(HatbandRunnersDirectory),
            ProtonToolSource.Hatband,
            protonTools);

        foreach (var steamDirectory in GetSteamDirectories())
        {
            AddToolsFromDirectory(
                Path.Combine(steamDirectory, "steamapps", "common"),
                ProtonToolSource.Steam,
                protonTools);
            AddToolsFromDirectory(
                Path.Combine(steamDirectory, "compatibilitytools.d"),
                ProtonToolSource.Steam,
                protonTools);
        }

        var installedTools = protonTools
            .DistinctBy(tool => tool.InstallationPath, StringComparer.Ordinal)
            .OrderBy(tool => tool.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyList<ProtonTool>>(installedTools);
    }

    private IEnumerable<string> GetSteamDirectories()
    {
        var homeDirectory = hostSystemInfo.UserProfileDirectory;
        var candidates = new[]
        {
            Path.Combine(homeDirectory, ".steam", "root"),
            Path.Combine(homeDirectory, ".steam", "steam"),
            Path.Combine(homeDirectory, ".local", "share", "Steam"),
            Path.Combine(homeDirectory, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam")
        };

        var steamDirectories = candidates
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var steamDirectory in steamDirectories.ToArray())
        {
            var libraryFoldersFile = Path.Combine(steamDirectory, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersFile))
            {
                continue;
            }

            foreach (var line in File.ReadLines(libraryFoldersFile))
            {
                var segments = line.Split('"');
                if (segments.Length < 5 || !string.Equals(segments[1], "path", StringComparison.Ordinal))
                {
                    continue;
                }

                var libraryPath = segments[3].Replace("\\\\", "\\", StringComparison.Ordinal);
                if (Directory.Exists(libraryPath))
                {
                    steamDirectories.Add(Path.GetFullPath(libraryPath));
                }
            }
        }

        return steamDirectories.Distinct(StringComparer.Ordinal);
    }

    private void EnsureLinuxHost()
    {
        if (hostSystemInfo.Platform != HostOperatingSystem.Linux)
        {
            throw new PlatformNotSupportedException("Proton tools can only be managed on Linux.");
        }
    }

    private static void AddToolsFromDirectory(
        string directoryPath,
        ProtonToolSource source,
        ICollection<ProtonTool> protonTools)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var toolDirectory in Directory.EnumerateDirectories(directoryPath))
        {
            var protonScript = Path.Combine(toolDirectory, "proton");
            if (!File.Exists(protonScript))
            {
                continue;
            }

            var name = Path.GetFileName(toolDirectory);
            protonTools.Add(new ProtonTool(name, name, toolDirectory, source));
        }
    }
}
