using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hatband.Core.Abstractions.Services;

namespace Hatband.Integrations.IGN;

internal sealed class IgnGraphQlClient
{
    private const string Endpoint = "https://mollusk.apis.ign.com/graphql";
    private const string SearchOperation = "SearchObjectsByName";
    private const string SearchHash = "e1c2e012a21b4a98aaa618ef1b43eb0cafe9136303274a34f5d9ea4f2446e884";
    private const string DetailsOperation = "ObjectSelectByTypeAndSlug";
    private const string DetailsQuery = """
        query ObjectSelectByTypeAndSlug($objectType: ObjectType!, $slug: String!, $state: State) {
          objectSelectByTypeAndSlug(type: $objectType, slug: $slug, state: $state) {
            slug
            metadata {
              descriptions {
                long
                short
              }
            }
            genres {
              name
            }
            producers {
              name
            }
            publishers {
              name
            }
            objectRegions {
              region
              releases {
                date
                platformAttributes {
                  name
                }
              }
            }
          }
        }
        """;
    private const string ImagesOperation = "ObjectImageGallery";
    private const string ImagesHash = "06204b0f0871f8382e3adab7d1c59399e6c17ac94bff575c20a12ebf9d880b86";
    private static readonly Uri IgnReferer = new("https://www.ign.com/reviews/games");
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ICacheService cacheService;
    private readonly TimeProvider timeProvider;

    public IgnGraphQlClient(
        IHttpClientFactory httpClientFactory,
        ICacheService cacheService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.httpClientFactory = httpClientFactory;
        this.cacheService = cacheService;
        this.timeProvider = timeProvider;
    }

    public async Task<IgnGraphQlResult<IReadOnlyList<IgnGameSearchEntry>>> SearchGamesAsync(
        string title,
        string region,
        string preferredLanguageTag,
        CancellationToken cancellationToken)
    {
        var response = await CallAsync(
            SearchOperation,
            new { term = title.Trim(), count = 20, objectType = "Game" },
            SearchHash,
            preferredLanguageTag,
            cancellationToken);
        if (response.ErrorMessage is not null)
        {
            return IgnGraphQlResult<IReadOnlyList<IgnGameSearchEntry>>.Failed(response.ErrorMessage);
        }

        using var document = RequireDocument(response);
        try
        {
            return IgnGraphQlResult<IReadOnlyList<IgnGameSearchEntry>>.Succeeded(
                IgnGraphQlParser.ReadSearchResults(document.RootElement, region));
        }
        catch (JsonException)
        {
            return IgnGraphQlResult<IReadOnlyList<IgnGameSearchEntry>>.Failed(
                "IGN returned a response without search results.");
        }
    }

    public async Task<IgnGraphQlResult<IgnGameDetails>> GetGameDetailsAsync(
        string slug,
        string region,
        string preferredLanguageTag,
        CancellationToken cancellationToken)
    {
        var response = await CallAsync(
            DetailsOperation,
            new { slug, objectType = "Game", state = "Published" },
            hash: null,
            preferredLanguageTag,
            cancellationToken,
            DetailsQuery);
        if (response.ErrorMessage is not null)
        {
            return IgnGraphQlResult<IgnGameDetails>.Failed(response.ErrorMessage);
        }

        using var document = RequireDocument(response);
        var details = IgnGraphQlParser.ReadDetails(document.RootElement, region);
        return details is null
            ? IgnGraphQlResult<IgnGameDetails>.Failed("IGN did not return details for the selected game.")
            : IgnGraphQlResult<IgnGameDetails>.Succeeded(details);
    }

    public async Task<IgnGraphQlResult<JsonDocument>> GetArtworkDocumentAsync(
        string slug,
        string preferredLanguageTag,
        CancellationToken cancellationToken)
    {
        return await CallAsync(
            ImagesOperation,
            new { slug, objectType = "Game", count = 10 },
            ImagesHash,
            preferredLanguageTag,
            cancellationToken);
    }

    private async Task<IgnGraphQlResult<JsonDocument>> CallAsync(
        string operationName,
        object variables,
        string? hash,
        string preferredLanguageTag,
        CancellationToken cancellationToken,
        string? queryText = null)
    {
        var query = new Dictionary<string, string>
        {
            ["operationName"] = operationName,
            ["variables"] = JsonSerializer.Serialize(variables)
        };
        if (queryText is null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hash);
            query["extensions"] = JsonSerializer.Serialize(new
            {
                persistedQuery = new { version = 1, sha256Hash = hash }
            });
        }
        else
        {
            query["query"] = queryText;
        }

        var uri = new Uri($"{Endpoint}?{string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))}");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Referrer = IgnReferer;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("apollographql-client-name", "kraken");
        request.Headers.TryAddWithoutValidation("apollographql-client-version", "v0.67.0");
        request.Headers.TryAddWithoutValidation("Accept-Language", preferredLanguageTag);
        request.Headers.TryAddWithoutValidation("apollo-require-preflight", "true");

        var cacheKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{uri.AbsoluteUri}|{preferredLanguageTag}")));
        var cacheExpiration = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(7);

        try
        {
            var json = await cacheService.GetOrCreateAsync(
                $"ign:graphql:{cacheKeyHash}",
                cacheExpiration,
                () => FetchDocumentJsonAsync(request, cancellationToken),
                cancellationToken);
            var document = JsonDocument.Parse(json);
            return IgnGraphQlResult<JsonDocument>.Succeeded(document);
        }
        catch (IgnGraphQlRequestException exception)
        {
            return IgnGraphQlResult<JsonDocument>.Failed(exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return IgnGraphQlResult<JsonDocument>.Failed("IGN request timed out. Try again later.");
        }
        catch (HttpRequestException)
        {
            return IgnGraphQlResult<JsonDocument>.Failed("IGN could not be reached. Try again later.");
        }
        catch (JsonException)
        {
            return IgnGraphQlResult<JsonDocument>.Failed("IGN returned an unreadable metadata response.");
        }
    }

    private async Task<string> FetchDocumentJsonAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiError = IgnGraphQlParser.ReadApiError(responseBody);
                var status = $"HTTP {(int)response.StatusCode} ({response.StatusCode})";
                throw new IgnGraphQlRequestException(apiError is null
                    ? $"IGN request failed with {status}."
                    : $"IGN request failed with {status}: {apiError}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var apiErrorFromResponse = IgnGraphQlParser.ReadApiError(document.RootElement);
            if (apiErrorFromResponse is not null)
            {
                throw new IgnGraphQlRequestException(
                    $"IGN rejected the metadata request: {apiErrorFromResponse}");
            }

            return document.RootElement.GetRawText();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new IgnGraphQlRequestException("IGN request timed out. Try again later.");
        }
        catch (HttpRequestException)
        {
            throw new IgnGraphQlRequestException("IGN could not be reached. Try again later.");
        }
        catch (JsonException)
        {
            throw new IgnGraphQlRequestException("IGN returned an unreadable metadata response.");
        }
    }

    private sealed class IgnGraphQlRequestException(string message) : Exception(message);

    private static JsonDocument RequireDocument(IgnGraphQlResult<JsonDocument> result)
    {
        return result.Value ?? throw new InvalidOperationException("IGN did not return a metadata document.");
    }
}
