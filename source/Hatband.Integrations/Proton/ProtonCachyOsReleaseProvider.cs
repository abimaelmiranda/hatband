using Hatband.Core.Abstractions;
using Hatband.Core.Models;
using Hatband.Integrations.Proton.Models;

namespace Hatband.Integrations.Proton;

public sealed class ProtonCachyOsReleaseProvider : IProtonReleaseProvider
{
    private static readonly Uri ReleasesUri = new("https://api.github.com/repos/CachyOS/proton-cachyos/releases?per_page=20");
    private readonly GitHubReleaseClient releaseClient;

    public ProtonCachyOsReleaseProvider(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        releaseClient = new GitHubReleaseClient(httpClient);
    }

    public string Id => "proton-cachyos";

    public string DisplayName => "Proton-CachyOS";

    public async Task<IReadOnlyList<ProtonRelease>> GetLatestReleasesAsync(
        CancellationToken cancellationToken = default)
    {
        var releases = await releaseClient.GetLatestReleasesAsync(ReleasesUri, cancellationToken);
        var protonReleases = new List<ProtonRelease>(releases.Count);

        foreach (var release in releases)
        {
            var protonRelease = ToProtonRelease(release);
            if (protonRelease is not null)
            {
                protonReleases.Add(protonRelease);
            }
        }

        return protonReleases;
    }

    private static ProtonRelease? ToProtonRelease(GitHubRelease release)
    {
        var archive = release.Assets.FirstOrDefault(asset =>
            asset.Name.Contains("-slr-", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.EndsWith("-x86_64.tar.xz", StringComparison.OrdinalIgnoreCase));
        if (archive is null)
        {
            return null;
        }

        var displayName = string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name;
        return new ProtonRelease(
            $"proton-cachyos:{release.TagName}:x86_64-slr",
            "proton-cachyos",
            "Proton-CachyOS",
            release.TagName,
            displayName,
            "Steam Linux Runtime · x86_64",
            release.PublishedAt,
            archive.DownloadUrl,
            archive.Name);
    }
}
