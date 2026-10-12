using Hatband.Core.Enums.Artwork;
using Hatband.Core.Models.Games;

namespace Hatband.Infrastructure.Storage.Artwork;

public sealed class FileSystemGameArtworkStorage : IGameArtworkStorage
{
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
        IReadOnlyList<GameArtworkImage> images,
        GameArtwork existingArtwork,
        GameArtworkStorageKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(existingArtwork);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown artwork storage kind.");
        }

        var storedPaths = new Dictionary<GameArtworkSlot, string>();
        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (storedPaths.ContainsKey(image.Slot))
            {
                continue;
            }

            storedPaths[image.Slot] = await StoreImageAsync(gameId, image, kind, cancellationToken);
        }

        var storedArtwork = existingArtwork;
        foreach (var (slot, path) in storedPaths)
        {
            storedArtwork = storedArtwork.WithPath(slot, path);
        }

        return storedArtwork;
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
        var relativeDirectory = Path.Combine("games", gameId.ToString("D"), "artwork");
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

    private async Task<string> StoreImageAsync(
        Guid gameId,
        GameArtworkImage image,
        GameArtworkStorageKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(image.ContentType);
        if (image.Content.Length == 0)
        {
            throw new ArgumentException("Artwork content cannot be empty.", nameof(image));
        }

        var extension = image.ContentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            _ => throw new InvalidOperationException($"Unsupported artwork content type '{image.ContentType}'.")
        };
        var relativeDirectory = Path.Combine("games", gameId.ToString("D"), "artwork");
        appDataFileSystem.CreateDirectory(relativeDirectory);
        var fileName = kind switch
        {
            GameArtworkStorageKind.Default => $"default-{GetAssetName(image.Slot)}{extension}",
            GameArtworkStorageKind.Custom => $"custom-{GetAssetName(image.Slot)}-{Guid.NewGuid():N}{extension}",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown artwork storage kind.")
        };
        var relativePath = Path.Combine(relativeDirectory, fileName)
            .Replace(Path.DirectorySeparatorChar, '/');
        await appDataFileSystem.WriteAllBytesAtomicallyAsync(relativePath, image.Content, cancellationToken);
        return relativePath;
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
            GameArtworkSlot.Logo => "logo",
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown artwork slot.")
        };
    }
}
