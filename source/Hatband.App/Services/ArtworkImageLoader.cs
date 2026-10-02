using Avalonia.Media.Imaging;
using System.Net.Http;
using Hatband.Core.Abstractions;

namespace Hatband.App.Services;

public sealed class ArtworkImageLoader
{
    private static readonly HttpClient RemoteHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private readonly IAppDataFileSystem appDataFileSystem;

    public ArtworkImageLoader(IAppDataFileSystem appDataFileSystem)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        this.appDataFileSystem = appDataFileSystem;
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
            var bytes = await RemoteHttpClient.GetByteArrayAsync(uri, cancellationToken);
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
}
