using Avalonia.Media.Imaging;

namespace Hatband.App.Services;

public sealed class ArtworkImageLoader
{
    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IGameLogoNormalizationService gameLogoNormalizationService;
    private readonly ArtworkBitmapCache bitmapCache;
    private readonly SemaphoreSlim decodeGate = new(2, 2);

    public ArtworkImageLoader(
        IAppDataFileSystem appDataFileSystem,
        IHttpClientFactory httpClientFactory,
        IGameLogoNormalizationService gameLogoNormalizationService,
        ArtworkBitmapCache bitmapCache)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(gameLogoNormalizationService);
        ArgumentNullException.ThrowIfNull(bitmapCache);
        this.appDataFileSystem = appDataFileSystem;
        this.httpClientFactory = httpClientFactory;
        this.gameLogoNormalizationService = gameLogoNormalizationService;
        this.bitmapCache = bitmapCache;
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

            await decodeGate.WaitAsync(cancellationToken);
            try
            {
                return await Task.Run(() => DecodeBitmap(imageBytes, null, cancellationToken), cancellationToken);
            }
            finally
            {
                decodeGate.Release();
            }
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

    public async Task<ArtworkBitmapLease?> LoadCachedAsync(
        string? source,
        int decodeWidth,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(decodeWidth);
        try
        {
            var fullPath = Path.IsPathRooted(source) ? source : appDataFileSystem.GetPath(source);
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                return null;
            }

            var key = $"{Path.GetFullPath(fullPath)}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}|{decodeWidth}";
            var cached = bitmapCache.TryAcquire(key);
            if (cached is not null)
            {
                return cached;
            }

            var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            await decodeGate.WaitAsync(cancellationToken);
            Bitmap? decoded;
            try
            {
                var racedCacheHit = bitmapCache.TryAcquire(key);
                if (racedCacheHit is not null)
                {
                    return racedCacheHit;
                }

                decoded = await Task.Run(() => DecodeBitmap(bytes, decodeWidth, cancellationToken), cancellationToken);
            }
            finally
            {
                decodeGate.Release();
            }

            if (decoded is null)
            {
                return null;
            }

            try
            {
                return bitmapCache.AddOrAcquire(key, decoded);
            }
            catch
            {
                decoded.Dispose();
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<(ArtworkBitmapLease? Image, bool IsNormalized)> LoadCachedLogoAsync(
        Guid gameId,
        string? source,
        bool normalize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
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
                var normalizedLogo = await LoadCachedAsync(normalizedPath, 600, cancellationToken);
                if (normalizedLogo is not null)
                {
                    return (normalizedLogo, true);
                }
            }
        }

        return (await LoadCachedAsync(source, 600, cancellationToken), false);
    }

    private static Bitmap? DecodeBitmap(byte[] imageBytes, int? decodeWidth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new MemoryStream(imageBytes);
        var bitmap = decodeWidth is int width
            ? Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.MediumQuality)
            : new Bitmap(stream);
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

            await decodeGate.WaitAsync(cancellationToken);
            try
            {
                return await Task.Run(() => DecodeBitmap(bytes, null, cancellationToken), cancellationToken);
            }
            finally
            {
                decodeGate.Release();
            }
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException)
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

        return DecodeArtworkImageAsync(image.Content);
    }

    private async Task<Bitmap?> DecodeArtworkImageAsync(byte[] imageBytes)
    {
        await decodeGate.WaitAsync();
        try
        {
            return await Task.Run(() => DecodeBitmap(imageBytes, null, CancellationToken.None));
        }
        finally
        {
            decodeGate.Release();
        }
    }

}
