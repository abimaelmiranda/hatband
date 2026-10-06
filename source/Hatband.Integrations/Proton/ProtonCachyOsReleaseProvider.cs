using Hatband.Core.Abstractions.Compatibility;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Abstractions.Services;
using Hatband.Core.Enums.Host;
using Hatband.Core.Models.Compatibility;
using Hatband.Integrations.Proton.Models;

namespace Hatband.Integrations.Proton;

public sealed class ProtonCachyOsReleaseProvider : ICompatibilityToolReleaseProvider
{
    private static readonly Uri ReleasesUri = new("https://api.github.com/repos/CachyOS/proton-cachyos/releases?per_page=20");
    private readonly GitHubReleaseClient releaseClient;
    private readonly IHostSystemInfo hostSystemInfo;

    public ProtonCachyOsReleaseProvider(
        IHttpClientFactory httpClientFactory,
        IHostSystemInfo hostSystemInfo,
        ICacheService cacheService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        releaseClient = new GitHubReleaseClient(httpClientFactory, cacheService, timeProvider);
        this.hostSystemInfo = hostSystemInfo;
    }

    public string Id => "proton-cachyos";

    public string DisplayName => "Proton-CachyOS";

    public async Task<IReadOnlyList<CompatibilityToolRelease>> GetLatestReleasesAsync(
        CancellationToken cancellationToken = default)
    {
        var architectureName = ProtonArchitecture.GetAssetArchitectureName(hostSystemInfo.OperatingSystemArchitecture);
        var releases = await releaseClient.GetLatestReleasesAsync(ReleasesUri, cancellationToken);
        var protonReleases = new List<CompatibilityToolRelease>(releases.Count);

        foreach (var release in releases)
        {
            var protonRelease = ToCompatibilityToolRelease(release, architectureName);
            if (protonRelease is not null)
            {
                protonReleases.Add(protonRelease);
            }
        }

        return protonReleases;
    }

    private static CompatibilityToolRelease? ToCompatibilityToolRelease(GitHubRelease release, string architectureName)
    {
        var archive = release.Assets.FirstOrDefault(asset =>
            asset.Name.Contains("-slr-", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.EndsWith($"-{architectureName}.tar.xz", StringComparison.OrdinalIgnoreCase));
        if (archive is null)
        {
            return null;
        }

        var displayName = string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name;
        return new CompatibilityToolRelease(
            $"proton-cachyos:{release.TagName}:{architectureName}-slr",
            "proton-cachyos",
            "Proton-CachyOS",
            release.TagName,
            displayName,
            $"Steam Linux Runtime · {architectureName}",
            release.PublishedAt,
            archive.DownloadUrl,
            archive.Name);
    }
}
