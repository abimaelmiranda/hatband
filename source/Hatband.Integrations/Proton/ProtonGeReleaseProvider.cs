using Hatband.Core.Abstractions;
using Hatband.Core.Models;
using Hatband.Integrations.Proton.Models;

namespace Hatband.Integrations.Proton;

public sealed class ProtonGeReleaseProvider : IProtonReleaseProvider
{
    private static readonly Uri ReleasesUri = new("https://api.github.com/repos/GloriousEggroll/proton-ge-custom/releases?per_page=20");
    private readonly GitHubReleaseClient releaseClient;
    private readonly IHostSystemInfo hostSystemInfo;

    public ProtonGeReleaseProvider(HttpClient httpClient, IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        releaseClient = new GitHubReleaseClient(httpClient);
        this.hostSystemInfo = hostSystemInfo;
    }

    public string Id => "proton-ge";

    public string DisplayName => "GE-Proton";

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

        return new ProtonRelease(
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
