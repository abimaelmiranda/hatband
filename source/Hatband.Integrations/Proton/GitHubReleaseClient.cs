using System.Net.Http.Headers;
using System.Text.Json;
using Hatband.Core.Abstractions.Services;
using Hatband.Integrations.Proton.Models;

namespace Hatband.Integrations.Proton;

internal sealed class GitHubReleaseClient
{
    private const int CacheDurationDays = 1;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICacheService _cacheService;
    private readonly TimeProvider _timeProvider;

    public GitHubReleaseClient(
        IHttpClientFactory httpClientFactory,
        ICacheService cacheService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _httpClientFactory = httpClientFactory;
        _cacheService = cacheService;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<GitHubRelease>> GetLatestReleasesAsync(
        Uri releasesUri,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"github:releases:{releasesUri.AbsoluteUri}";
        var expiresAt = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime).AddDays(CacheDurationDays);
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            expiresAt,
            () => FetchReleasesAsync(releasesUri, cancellationToken),
            cancellationToken);
    }

    private async Task<IReadOnlyList<GitHubRelease>> FetchReleasesAsync(
        Uri releasesUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, releasesUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Hatband", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var httpClient = _httpClientFactory.CreateClient();
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
