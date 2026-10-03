using Hatband.Core.Abstractions;
using Hatband.Core.Models;
using Hatband.Integrations.Proton.Models;

namespace Hatband.Integrations.Proton;

public sealed class ProtonCachyOsReleaseProvider : IProtonReleaseProvider
{
    private static readonly Uri ReleasesUri = new("https://api.github.com/repos/CachyOS/proton-cachyos/releases?per_page=20");
    private readonly GitHubReleaseClient releaseClient;
    private readonly IHostSystemInfo hostSystemInfo;

    public ProtonCachyOsReleaseProvider(HttpClient httpClient, IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        releaseClient = new GitHubReleaseClient(httpClient);
        this.hostSystemInfo = hostSystemInfo;
    }

    public string Id => "proton-cachyos";

    public string DisplayName => "Proton-CachyOS";

    public async Task<IReadOnlyList<ProtonRelease>> GetLatestReleasesAsync(
        CancellationToken cancellationToken = default)
    {
        var architectureName = ProtonArchitecture.GetAssetArchitectureName(hostSystemInfo.OperatingSystemArchitecture);
        var releases = await releaseClient.GetLatestReleasesAsync(ReleasesUri, cancellationToken);
        var protonReleases = new List<ProtonRelease>(releases.Count);

        foreach (var release in releases)
        {
            var protonRelease = ToProtonRelease(release, architectureName);
            if (protonRelease is not null)
            {
                protonReleases.Add(protonRelease);
            }
        }

        return protonReleases;
    }

    private static ProtonRelease? ToProtonRelease(GitHubRelease release, string architectureName)
    {
        var archive = release.Assets.FirstOrDefault(asset =>
            asset.Name.Contains("-slr-", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.EndsWith($"-{architectureName}.tar.xz", StringComparison.OrdinalIgnoreCase));
        if (archive is null)
        {
            return null;
        }

        var displayName = string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name;
        return new ProtonRelease(
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
