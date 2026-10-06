using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hatband.Core.Abstractions.Services;
using Hatband.Core.Enums.Games;
using Hatband.Integrations.Steam.Models;
using Microsoft.Extensions.Logging;

namespace Hatband.Integrations.Steam.Protondb;

public class ProtondbMetadataProvider
{
    private const string ProtondbUrl = "https://www.protondb.com/api/v1/reports/summaries/{0}.json";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProtondbMetadataProvider> _logger;
    private readonly ICacheService _cacheService;
    private readonly TimeProvider _timeProvider;

    public ProtondbMetadataProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<ProtondbMetadataProvider> logger,
        ICacheService cacheService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cacheService = cacheService;
        _timeProvider = timeProvider;
    }

    public async Task<GameCompatibilityTier> GetCompatibilityTierAsync(
        string storeGameId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeGameId);
        if (!uint.TryParse(storeGameId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            throw new ArgumentException("The store game ID must be a numeric Steam app ID.", nameof(storeGameId));
        }

        try
        {
            var expiresAt = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime).AddDays(1);
            return await _cacheService.GetOrCreateAsync(
                $"protondb:compatibility:{storeGameId}",
                expiresAt,
                () => FetchCompatibilityTierAsync(storeGameId, cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(exception, "ProtonDB compatibility lookup timed out for Steam app {SteamAppId}.", storeGameId);
            return GameCompatibilityTier.Unknown;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(exception, "ProtonDB compatibility lookup failed for Steam app {SteamAppId}.", storeGameId);
            return GameCompatibilityTier.Unknown;
        }
    }

    private async Task<GameCompatibilityTier> FetchCompatibilityTierAsync(
        string storeGameId,
        CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("Protondb");
        using var response = await client.GetAsync(
            string.Format(CultureInfo.InvariantCulture, ProtondbUrl, storeGameId),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return GameCompatibilityTier.Unknown;
        }

        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<ProtondbGameSummary>(cancellationToken: cancellationToken);
        return summary?.ToGameCompatibilityTier() ?? GameCompatibilityTier.Unknown;
    }
}
