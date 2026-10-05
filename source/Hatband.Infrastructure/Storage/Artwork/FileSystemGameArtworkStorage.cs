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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(existingArtwork);

        var storedPaths = new Dictionary<GameArtworkSlot, string>();
        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (storedPaths.ContainsKey(image.Slot))
            {
                continue;
            }

            storedPaths[image.Slot] = await StoreImageAsync(gameId, image, cancellationToken);
        }

        return new GameArtwork
        {
            CoverImagePath = storedPaths.GetValueOrDefault(GameArtworkSlot.Cover) ?? existingArtwork.CoverImagePath,
            BackgroundImagePath = storedPaths.GetValueOrDefault(GameArtworkSlot.Background) ?? existingArtwork.BackgroundImagePath,
            IconPath = storedPaths.GetValueOrDefault(GameArtworkSlot.Icon) ?? existingArtwork.IconPath
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

    private async Task<string> StoreImageAsync(Guid gameId, GameArtworkImage image, CancellationToken cancellationToken)
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
        var relativeDirectory = Path.Combine("artwork", gameId.ToString());
        appDataFileSystem.CreateDirectory(relativeDirectory);
        var relativePath = Path.Combine(relativeDirectory, $"{GetAssetName(image.Slot)}{extension}")
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
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown artwork slot.")
        };
    }
}
