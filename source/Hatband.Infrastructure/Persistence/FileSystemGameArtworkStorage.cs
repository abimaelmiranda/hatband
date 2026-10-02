using System.Net.Http;
using Hatband.Core.Enums.Artwork;
using Hatband.Core.Abstractions;
using Hatband.Core.Models;

namespace Hatband.Infrastructure.Persistence;

public sealed class FileSystemGameArtworkStorage : IGameArtworkStorage
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly IAppDataFileSystem appDataFileSystem;

    public FileSystemGameArtworkStorage(IAppDataFileSystem appDataFileSystem)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        this.appDataFileSystem = appDataFileSystem;
    }

    public bool IsAvailable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return Path.IsPathRooted(path)
            ? File.Exists(path)
            : appDataFileSystem.FileExists(path);
    }

    public async Task<GameArtwork> StoreAsync(
        Guid gameId,
        GameArtworkSources sources,
        GameArtwork existingArtwork,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(existingArtwork);

        var coverImagePath = await StoreImageAsync(
            gameId,
            "cover",
            existingArtwork.CoverImagePath,
            sources.CoverImageUrls,
            cancellationToken);
        var backgroundImagePath = await StoreImageAsync(
            gameId,
            "background",
            existingArtwork.BackgroundImagePath,
            sources.BackgroundImageUrls,
            cancellationToken);
        var iconPath = await StoreImageAsync(
            gameId,
            "icon",
            existingArtwork.IconPath,
            sources.IconUrls,
            cancellationToken);

        return new GameArtwork
        {
            CoverImagePath = coverImagePath,
            BackgroundImagePath = backgroundImagePath,
            IconPath = iconPath,
            IsCoverCustomized = existingArtwork.IsCoverCustomized,
            IsBackgroundCustomized = existingArtwork.IsBackgroundCustomized,
            IsIconCustomized = existingArtwork.IsIconCustomized
        };
    }

    public async Task<string> ImportLocalAsync(
        Guid gameId,
        GameArtworkSlot slot,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceFullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourceFullPath))
        {
            throw new FileNotFoundException("The selected artwork file does not exist.", sourceFullPath);
        }

        var extension = Path.GetExtension(sourceFullPath).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp" and not ".bmp")
        {
            throw new InvalidOperationException("Choose a PNG, JPG, WEBP, or BMP image file.");
        }

        var assetName = GetAssetName(slot);
        var relativeDirectory = Path.Combine("artwork", gameId.ToString());
        appDataFileSystem.CreateDirectory(relativeDirectory);

        var fileName = $"custom-{assetName}-{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(relativeDirectory, fileName)
            .Replace(Path.DirectorySeparatorChar, '/');
        var fullPath = appDataFileSystem.GetPath(relativePath);
        var temporaryPath = fullPath + ".import";

        try
        {
            await using (var source = new FileStream(
                             sourceFullPath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            if (new FileInfo(temporaryPath).Length == 0)
            {
                throw new InvalidOperationException("The selected artwork file is empty.");
            }

            File.Move(temporaryPath, fullPath);
            return relativePath;
        }
        catch
        {
            DeleteIfExists(temporaryPath);
            throw;
        }
    }

    private async Task<string?> StoreImageAsync(
        Guid gameId,
        string assetName,
        string? existingPath,
        IReadOnlyList<string> sourceUrls,
        CancellationToken cancellationToken)
    {
        foreach (var sourceUrl in sourceUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri) ||
                (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            var extension = Path.GetExtension(sourceUri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 8)
            {
                extension = ".jpg";
            }

            var relativeDirectory = Path.Combine("artwork", gameId.ToString());
            appDataFileSystem.CreateDirectory(relativeDirectory);

            var normalizedExtension = extension.ToLowerInvariant();
            var sourceName = Path.GetFileNameWithoutExtension(sourceUri.AbsolutePath);
            var fileName = assetName == "background" && !string.IsNullOrWhiteSpace(sourceName)
                ? $"{assetName}-{sourceName}{normalizedExtension}"
                : assetName + normalizedExtension;
            var relativePath = Path.Combine(relativeDirectory, fileName)
                .Replace(Path.DirectorySeparatorChar, '/');
            var fullPath = appDataFileSystem.GetPath(relativePath);
            if (string.Equals(
                    existingPath?.Replace('\\', '/'),
                    relativePath,
                    StringComparison.OrdinalIgnoreCase) &&
                File.Exists(fullPath))
            {
                return existingPath;
            }

            var temporaryPath = fullPath + ".download";

            try
            {
                using var response = await HttpClient.GetAsync(
                    sourceUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using (var output = File.Create(temporaryPath))
                {
                    await response.Content.CopyToAsync(output, cancellationToken);
                }

                if (new FileInfo(temporaryPath).Length == 0)
                {
                    File.Delete(temporaryPath);
                    continue;
                }

                File.Move(temporaryPath, fullPath, true);
                return relativePath;
            }
            catch (HttpRequestException)
            {
                DeleteIfExists(temporaryPath);
            }
            catch (IOException)
            {
                DeleteIfExists(temporaryPath);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                DeleteIfExists(temporaryPath);
            }
        }

        return existingPath;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string GetAssetName(GameArtworkSlot slot)
    {
        return slot switch
        {
            GameArtworkSlot.Cover => "cover",
            GameArtworkSlot.Background => "background",
            GameArtworkSlot.Icon => "icon",
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown artwork slot.")
        };
    }
}
