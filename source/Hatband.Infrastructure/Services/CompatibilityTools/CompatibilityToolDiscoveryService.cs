using Hatband.Integrations.Steam.Abstractions;

namespace Hatband.Infrastructure.Services.CompatibilityTools;

public sealed class CompatibilityToolDiscoveryService : ICompatibilityToolDiscoveryService
{
    private const string HatbandRunnersDirectory = "tools/proton";

    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IHostSystemInfo hostSystemInfo;
    private readonly ISteamInstallationService steamInstallationService;

    public CompatibilityToolDiscoveryService(
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

    public async Task<IReadOnlyList<CompatibilityTool>> DiscoverInstalledToolsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLinuxHost();

        var protonTools = new List<CompatibilityTool>();
        AddToolsFromDirectory(
            appDataFileSystem.GetPath(HatbandRunnersDirectory),
            CompatibilityToolSource.Hatband,
            protonTools);

        var installations = await steamInstallationService.GetInstallationsAsync(cancellationToken);
        var steamDirectories = installations
            .SelectMany(installation => installation.Libraries.Select(library => library.Path).Prepend(installation.RootPath))
            .Distinct(StringComparer.Ordinal);
        foreach (var steamDirectory in steamDirectories)
        {
            AddToolsFromDirectory(
                Path.Combine(steamDirectory, "steamapps", "common"),
                CompatibilityToolSource.Steam,
                protonTools);
            AddToolsFromDirectory(
                Path.Combine(steamDirectory, "compatibilitytools.d"),
                CompatibilityToolSource.Steam,
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
        CompatibilityToolSource source,
        ICollection<CompatibilityTool> protonTools)
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
            protonTools.Add(new CompatibilityTool(name, name, toolDirectory, source));
        }
    }

    private static string ResolveInstallationPath(string path)
    {
        var directory = new DirectoryInfo(path);
        return directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName;
    }
}
