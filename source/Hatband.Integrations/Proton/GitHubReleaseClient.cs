using System.Net.Http.Headers;
using System.Text.Json;
using Hatband.Integrations.Proton.Models;

namespace Hatband.Integrations.Proton;

internal sealed class GitHubReleaseClient(IHttpClientFactory httpClientFactory)
{
    public async Task<IReadOnlyList<GitHubRelease>> GetLatestReleasesAsync(
        Uri releasesUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, releasesUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Hatband", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var httpClient = httpClientFactory.CreateClient();
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var releases = await JsonSerializer.DeserializeAsync(
            content,
            GitHubReleaseJsonSerializerContext.Default.GitHubReleaseArray,
            cancellationToken);

        return releases ?? throw new JsonException("The GitHub release response did not contain a release list.");
    }
}
