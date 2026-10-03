using Hatband.Core.Abstractions;
using Hatband.Core.Enums;
using Hatband.Core.Models;
using Hatband.Integrations.Steam.Abstractions;

namespace Hatband.Infrastructure.Services;

public sealed class ProtonToolDiscoveryService : IProtonToolDiscoveryService
{
    private const string HatbandRunnersDirectory = "proton/runners";

    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IHostSystemInfo hostSystemInfo;
    private readonly ISteamInstallationService steamInstallationService;

    public ProtonToolDiscoveryService(
        IAppDataFileSystem appDataFileSystem,
        IHostSystemInfo hostSystemInfo,
        ISteamInstallationService steamInstallationService)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(steamInstallationService);
        this.appDataFileSystem = appDataFileSystem;
        this.hostSystemInfo = hostSystemInfo;
        this.steamInstallationService = steamInstallationService;
    }

    public async Task<IReadOnlyList<ProtonTool>> DiscoverInstalledToolsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLinuxHost();

        var protonTools = new List<ProtonTool>();
        AddToolsFromDirectory(
            appDataFileSystem.GetPath(HatbandRunnersDirectory),
            ProtonToolSource.Hatband,
            protonTools);

        var installations = await steamInstallationService.GetInstallationsAsync(cancellationToken);
        var steamDirectories = installations
            .SelectMany(installation => installation.Libraries.Select(library => library.Path).Prepend(installation.RootPath))
            .Distinct(StringComparer.Ordinal);
        foreach (var steamDirectory in steamDirectories)
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
            .DistinctBy(tool => ResolveInstallationPath(tool.InstallationPath), StringComparer.Ordinal)
            .OrderBy(tool => tool.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return installedTools;
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

    private static string ResolveInstallationPath(string path)
    {
        var directory = new DirectoryInfo(path);
        return directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName;
    }
}
