using System.Security.Cryptography;
using System.Text;
using Hatband.Core.Abstractions.FileSystem;
using Hatband.Core.Abstractions.Games;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Hatband.Infrastructure.Storage.Artwork;

public sealed class GameLogoNormalizationService : IGameLogoNormalizationService
{
    private const string AlgorithmVersion = "v2";
    private const uint CanvasWidth = 600;
    private const uint CanvasHeight = 240;
    private const uint HorizontalPadding = 24;
    private const uint VerticalPadding = 16;

    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IGameRepository gameRepository;
    private readonly IGameArtworkStorage gameArtworkStorage;
    private readonly ILogger<GameLogoNormalizationService> logger;
    private readonly Lock gameLocksGate = new();
    private readonly Dictionary<Guid, GameLockEntry> gameLocks = [];

    public GameLogoNormalizationService(
        IAppDataFileSystem appDataFileSystem,
        IGameRepository gameRepository,
        IGameArtworkStorage gameArtworkStorage,
        ILogger<GameLogoNormalizationService> logger)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(gameRepository);
        ArgumentNullException.ThrowIfNull(gameArtworkStorage);
        ArgumentNullException.ThrowIfNull(logger);
        this.appDataFileSystem = appDataFileSystem;
        this.gameRepository = gameRepository;
        this.gameArtworkStorage = gameArtworkStorage;
        this.logger = logger;
    }

    public async Task<string?> TryGetNormalizedPathAsync(
        Guid gameId,
        string originalPath,
        CancellationToken cancellationToken = default)
    {
        ValidateSource(gameId, originalPath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSourceAvailable(originalPath))
        {
            return null;
        }

        byte[] sourceBytes;
        try
        {
            sourceBytes = await ReadSourceBytesAsync(originalPath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var relativePath = GetCacheRelativePath(gameId, sourceBytes);
        var fullPath = appDataFileSystem.GetPath(relativePath);
        return appDataFileSystem.FileExists(relativePath) && new FileInfo(fullPath).Length > 0
            ? relativePath
            : null;
    }

    public async Task<string?> GetNormalizedPathAsync(
        Guid gameId,
        string originalPath,
        CancellationToken cancellationToken = default)
    {
        ValidateSource(gameId, originalPath);
        cancellationToken.ThrowIfCancellationRequested();

        var gameLock = await AcquireGameLockAsync(gameId, cancellationToken);
        try
        {
            var sourceBytes = await ReadSourceBytesAsync(originalPath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = GetCacheRelativePath(gameId, sourceBytes);
            var normalizedPath = appDataFileSystem.GetPath(relativePath);
            if (appDataFileSystem.FileExists(relativePath) && new FileInfo(normalizedPath).Length > 0)
            {
                return relativePath;
            }

            var normalizedBytes = await Task.Run(
                () => NormalizeLogo(sourceBytes, cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (normalizedBytes is null)
            {
                return null;
            }

            await appDataFileSystem.WriteAllBytesAtomicallyAsync(relativePath, normalizedBytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return relativePath;
        }
        finally
        {
            ReleaseGameLock(gameId, gameLock);
        }
    }

    public async Task NormalizeLibraryAsync(
        IProgress<GameLogoNormalizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var games = await gameRepository.GetAllAsync(cancellationToken);
        var gamesToNormalize = games
            .Where(game => !game.IsHidden &&
                           !string.IsNullOrWhiteSpace(game.Artwork.LogoPath) &&
                           gameArtworkStorage.IsAvailable(game.Artwork.LogoPath))
            .ToArray();

        var completedCount = 0;
        foreach (var game in gamesToNormalize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var originalPath = game.Artwork.LogoPath;
            if (string.IsNullOrWhiteSpace(originalPath))
            {
                continue;
            }

            string? normalizedPath = null;
            string? errorMessage = null;
            try
            {
                normalizedPath = await GetNormalizedPathAsync(
                    game.Id,
                    originalPath,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                logger.LogWarning(
                    exception,
                    "Could not normalize library logo for game {GameId}.",
                    game.Id);
            }

            completedCount++;
            progress?.Report(new GameLogoNormalizationProgress(
                game.Id,
                completedCount,
                gamesToNormalize.Length,
                normalizedPath,
                errorMessage));
        }
    }

    private static void ValidateSource(Guid gameId, string originalPath)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("A game ID is required.", nameof(gameId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
    }

    private bool IsSourceAvailable(string path)
    {
        return Path.IsPathRooted(path)
            ? File.Exists(path)
            : appDataFileSystem.FileExists(path);
    }

    private static string GetCacheRelativePath(Guid gameId, byte[] sourceBytes)
    {
        var hash = CreateIdentityHash(sourceBytes);
        return Path.Combine(
                "games",
                gameId.ToString("D"),
                "artwork",
                $"normalized-logo-{AlgorithmVersion}-{hash}.png")
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private async Task<byte[]> ReadSourceBytesAsync(string originalPath, CancellationToken cancellationToken)
    {
        if (Path.IsPathRooted(originalPath))
        {
            return await File.ReadAllBytesAsync(originalPath, cancellationToken);
        }

        return await appDataFileSystem.ReadAllBytesAsync(originalPath, cancellationToken);
    }

    private static string CreateIdentityHash(byte[] sourceBytes)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes($"hatband-game-logo-normalization-{AlgorithmVersion}\0"));
        hash.AppendData(sourceBytes);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static byte[]? NormalizeLogo(byte[] sourceBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(sourceBytes);
        image.AutoOrient();
        cancellationToken.ThrowIfCancellationRequested();
        image.ColorSpace = ColorSpace.sRGB;

        var bounds = FindVisibleBounds(image, cancellationToken);
        if (bounds is null)
        {
            return null;
        }

        CropToVisibleBounds(image, bounds, cancellationToken);
        image.Resize(new MagickGeometry(CanvasWidth, CanvasHeight));
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedWidth = checked(image.Width + HorizontalPadding);
        var normalizedHeight = checked(image.Height + VerticalPadding);
        using var canvas = new MagickImage(MagickColors.Transparent, normalizedWidth, normalizedHeight)
        {
            ColorSpace = ColorSpace.sRGB
        };
        canvas.Composite(image, (int)(HorizontalPadding / 2), (int)(VerticalPadding / 2), CompositeOperator.Over);
        var normalizedBytes = canvas.ToByteArray(MagickFormat.Png);
        cancellationToken.ThrowIfCancellationRequested();
        return normalizedBytes;
    }

    private static IMagickGeometry? FindVisibleBounds(MagickImage image, CancellationToken cancellationToken)
    {
        using var alphaMask = image.Clone();
        if (!alphaMask.HasAlpha)
        {
            alphaMask.Alpha(AlphaAction.Activate);
        }

        alphaMask.Alpha(AlphaAction.Extract);
        cancellationToken.ThrowIfCancellationRequested();
        alphaMask.Threshold(new Percentage(1));
        var redChannelStatistics = alphaMask.Statistics(Channels.Red).GetChannel(PixelChannel.Red);
        if (redChannelStatistics is null)
        {
            throw new InvalidOperationException("The logo alpha mask has no red channel statistics.");
        }

        if (redChannelStatistics.Maximum == 0)
        {
            return null;
        }

        alphaMask.BorderColor = MagickColors.Black;
        alphaMask.Border(1);
        alphaMask.ColorFuzz = new Percentage(0);

        var maskBounds = alphaMask.BoundingBox;
        cancellationToken.ThrowIfCancellationRequested();
        if (maskBounds is null || maskBounds.Width == 0 || maskBounds.Height == 0)
        {
            return null;
        }

        var x = Math.Max(maskBounds.X - 1, 0);
        var y = Math.Max(maskBounds.Y - 1, 0);
        var right = Math.Min((long)x + maskBounds.Width, image.Width);
        var bottom = Math.Min((long)y + maskBounds.Height, image.Height);
        if (right <= x || bottom <= y)
        {
            return null;
        }

        return new MagickGeometry(x, y, (uint)(right - x), (uint)(bottom - y));
    }

    private static void CropToVisibleBounds(
        MagickImage image,
        IMagickGeometry bounds,
        CancellationToken cancellationToken)
    {
        var padding = Math.Max(4, (int)Math.Round(Math.Min(bounds.Width, bounds.Height) * 0.04));
        var left = Math.Max(bounds.X - padding, 0);
        var top = Math.Max(bounds.Y - padding, 0);
        var right = Math.Min((long)bounds.X + bounds.Width + padding, image.Width);
        var bottom = Math.Min((long)bounds.Y + bounds.Height + padding, image.Height);
        var width = (uint)(right - left);
        var height = (uint)(bottom - top);

        image.Crop(new MagickGeometry(left, top, width, height));
        image.ResetPage();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<GameLockEntry> AcquireGameLockAsync(Guid gameId, CancellationToken cancellationToken)
    {
        GameLockEntry entry;
        lock (gameLocksGate)
        {
            if (gameLocks.TryGetValue(gameId, out var existingEntry))
            {
                entry = existingEntry;
            }
            else
            {
                entry = new GameLockEntry();
                gameLocks.Add(gameId, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            return entry;
        }
        catch
        {
            RemoveGameLockReference(gameId, entry, semaphoreAcquired: false);
            throw;
        }
    }

    private void ReleaseGameLock(Guid gameId, GameLockEntry entry)
    {
        RemoveGameLockReference(gameId, entry, semaphoreAcquired: true);
    }

    private void RemoveGameLockReference(Guid gameId, GameLockEntry entry, bool semaphoreAcquired)
    {
        lock (gameLocksGate)
        {
            if (semaphoreAcquired)
            {
                entry.Semaphore.Release();
            }

            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0)
            {
                gameLocks.Remove(gameId);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class GameLockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }
}
