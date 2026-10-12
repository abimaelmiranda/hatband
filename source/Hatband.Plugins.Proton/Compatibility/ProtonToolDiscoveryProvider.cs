using Hatband.Core.Abstractions.Compatibility;
using Hatband.Core.Abstractions.FileSystem;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Host;
using Hatband.Core.Models.Compatibility;
using Hatband.Core.Models.Games;

namespace Hatband.Plugins.Proton.Compatibility;

public sealed class ProtonToolDiscoveryProvider : ICompatibilityToolDiscoveryProvider
{
    private const string HatbandRunnersDirectory = "tools/proton";
    private const string ProtonProvisionerId = "proton";

    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IHostSystemInfo hostSystemInfo;
    private readonly IReadOnlyList<ICompatibilityToolLocationProvider> locationProviders;

    public ProtonToolDiscoveryProvider(
        IAppDataFileSystem appDataFileSystem,
        IHostSystemInfo hostSystemInfo,
        IEnumerable<ICompatibilityToolLocationProvider> locationProviders)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(locationProviders);
        this.appDataFileSystem = appDataFileSystem;
        this.hostSystemInfo = hostSystemInfo;
        this.locationProviders = locationProviders
            .Where(provider => string.Equals(provider.ProvisionerId, ProtonProvisionerId, StringComparison.Ordinal))
            .ToArray();
    }

    public string Id => ProtonProvisionerId;

    public bool SupportsHost(HostOperatingSystem host)
    {
        return host == HostOperatingSystem.Linux;
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

        foreach (var provider in locationProviders)
        {
            var locations = await provider.GetLocationsAsync(cancellationToken);
            foreach (var location in locations)
            {
                if (location.LinkToManagedDirectory)
                {
                    AddCompatibilityTools(location, protonTools);
                }
                else
                {
                    AddToolsFromDirectory(location.DirectoryPath, location.Source, protonTools);
                }
            }
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
            if (!ContainsProtonScript(toolDirectory))
            {
                continue;
            }

            protonTools.Add(CreateTool(toolDirectory, source));
        }
    }

    private void AddCompatibilityTools(CompatibilityToolLocation location, ICollection<CompatibilityTool> protonTools)
    {
        var directoryPath = location.DirectoryPath;
        var source = location.Source;
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var toolDirectory in Directory.EnumerateDirectories(directoryPath))
        {
            if (!ContainsProtonScript(toolDirectory))
            {
                continue;
            }

            var runnerLinkPath = appDataFileSystem.GetPath(Path.Combine(HatbandRunnersDirectory, Path.GetFileName(toolDirectory)));
            if (TryCreateRunnerLink(toolDirectory, runnerLinkPath))
            {
                // TODO: Preserve the original Steam source when discovering linked runners.
                protonTools.Add(CreateTool(runnerLinkPath, CompatibilityToolSource.Hatband));
                continue;
            }

            protonTools.Add(CreateTool(toolDirectory, source));
        }
    }

    private static bool ContainsProtonScript(string toolDirectory)
    {
        return File.Exists(Path.Combine(toolDirectory, "proton"));
    }

    private static CompatibilityTool CreateTool(string toolDirectory, CompatibilityToolSource source)
    {
        var name = Path.GetFileName(toolDirectory);
        return new CompatibilityTool(name, name, toolDirectory, source);
    }

    private static bool TryCreateRunnerLink(string sourcePath, string linkPath)
    {
        var existingLink = new DirectoryInfo(linkPath);
        if (existingLink.LinkTarget is not null)
        {
            try
            {
                var existingTarget = existingLink.ResolveLinkTarget(returnFinalTarget: true);
                return existingTarget is not null && PathsEqual(existingTarget.FullName, sourcePath);
            }
            catch (IOException)
            {
                return false;
            }
        }

        if (Directory.Exists(linkPath) || File.Exists(linkPath))
        {
            return false;
        }

        try
        {
            Directory.CreateSymbolicLink(linkPath, sourcePath);
            return true;
        }
        catch (IOException) when (new DirectoryInfo(linkPath).LinkTarget is not null)
        {
            var existingTarget = new DirectoryInfo(linkPath).ResolveLinkTarget(returnFinalTarget: true);
            return existingTarget is not null && PathsEqual(existingTarget.FullName, sourcePath);
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.Ordinal);
    }

    private static string ResolveInstallationPath(string path)
    {
        var directory = new DirectoryInfo(path);
        return directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName;
    }
}
