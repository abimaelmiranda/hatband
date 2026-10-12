using System.Net.Http;
using Avalonia.Media.Imaging;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Models.Games;

namespace Hatband.App.Services;

public sealed class ArtworkImageLoader
{
    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IGameLogoNormalizationService gameLogoNormalizationService;

    public ArtworkImageLoader(
        IAppDataFileSystem appDataFileSystem,
        IHttpClientFactory httpClientFactory,
        IGameLogoNormalizationService gameLogoNormalizationService)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(gameLogoNormalizationService);
        this.appDataFileSystem = appDataFileSystem;
        this.httpClientFactory = httpClientFactory;
        this.gameLogoNormalizationService = gameLogoNormalizationService;
    }

    public async Task<Bitmap?> LoadAsync(string? source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        try
        {
            if (!Path.IsPathRooted(source) && !appDataFileSystem.FileExists(source))
            {
                return null;
            }

            var imageBytes = Path.IsPathRooted(source)
                ? await File.ReadAllBytesAsync(source, cancellationToken)
                : await appDataFileSystem.ReadAllBytesAsync(source, cancellationToken);
            if (imageBytes is not { Length: > 0 })
            {
                return null;
            }

            return await Task.Run(() => DecodeBitmap(imageBytes, cancellationToken), cancellationToken);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<(Bitmap? Image, bool IsNormalized)> LoadLogoAsync(
        Guid gameId,
        string? source,
        bool normalize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return (null, false);
        }

        if (Path.IsPathRooted(source) ? !File.Exists(source) : !appDataFileSystem.FileExists(source))
        {
            return (null, false);
        }

        if (normalize)
        {
            var normalizedPath = await Task.Run(
                () => gameLogoNormalizationService.TryGetNormalizedPathAsync(gameId, source, cancellationToken),
                cancellationToken);
            if (normalizedPath is not null)
            {
                var normalizedImage = await LoadAsync(normalizedPath, cancellationToken);
                if (normalizedImage is not null)
                {
                    return (normalizedImage, true);
                }
            }
        }

        return (await LoadAsync(source, cancellationToken), false);
    }

    private static Bitmap? DecodeBitmap(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new MemoryStream(imageBytes);
        var bitmap = new Bitmap(stream);
        if (cancellationToken.IsCancellationRequested)
        {
            bitmap.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }

        return bitmap;
    }

    public async Task<Bitmap?> LoadRemoteAsync(string source, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            uri.Scheme is not "https" and not "http")
        {
            return null;
        }

        try
        {
            using var httpClient = httpClientFactory.CreateClient("Artwork");
            var bytes = await httpClient.GetByteArrayAsync(uri, cancellationToken);
            if (bytes.Length == 0)
            {
                return null;
            }

            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public Task<Bitmap?> LoadAsync(GameArtworkImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Content.Length == 0)
        {
            return Task.FromResult<Bitmap?>(null);
        }

        using var stream = new MemoryStream(image.Content);
        return Task.FromResult<Bitmap?>(new Bitmap(stream));
    }

}
