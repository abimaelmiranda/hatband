using Hatband.Core.Abstractions.Compatibility;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Abstractions.Services;
using Hatband.Core.Enums.Host;
using Hatband.Core.Models.Compatibility;
using Hatband.Integrations.Proton.Models;

namespace Hatband.Integrations.Proton;

public sealed class ProtonGeReleaseProvider : ICompatibilityToolReleaseProvider
{
    private static readonly Uri ReleasesUri = new("https://api.github.com/repos/GloriousEggroll/proton-ge-custom/releases?per_page=20");
    private readonly GitHubReleaseClient releaseClient;
    private readonly IHostSystemInfo hostSystemInfo;

    public ProtonGeReleaseProvider(
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

    public string Id => "proton-ge";

    public string DisplayName => "GE-Proton";

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
            asset.Name.EndsWith($"-{architectureName}.tar.gz", StringComparison.OrdinalIgnoreCase));
        if (archive is null && architectureName == "x86_64")
        {
            archive = release.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, $"{release.TagName}.tar.gz", StringComparison.OrdinalIgnoreCase));
        }

        if (archive is null)
        {
            return null;
        }

        return new CompatibilityToolRelease(
            $"proton-ge:{release.TagName}",
            "proton-ge",
            "GE-Proton",
            release.TagName,
            string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
            architectureName,
            release.PublishedAt,
            archive.DownloadUrl,
            archive.Name);
    }
}
