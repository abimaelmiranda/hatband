using Avalonia.Media.Imaging;
using Hatband.Core.Models.Games;
using System.Net.Http;

namespace Hatband.App.Services;

public sealed class ArtworkImageLoader
{
    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IHttpClientFactory httpClientFactory;

    public ArtworkImageLoader(IAppDataFileSystem appDataFileSystem, IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        this.appDataFileSystem = appDataFileSystem;
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<Bitmap?> LoadAsync(string? source)
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
                ? await File.ReadAllBytesAsync(source)
                : await appDataFileSystem.ReadAllBytesAsync(source);
            if (imageBytes is not { Length: > 0 })
            {
                return null;
            }

            using var stream = new MemoryStream(imageBytes);
            return new Bitmap(stream);
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
