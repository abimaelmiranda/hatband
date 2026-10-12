using System.Security.Cryptography;
using Hatband.Core.Abstractions.FileSystem;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hatband.Infrastructure.Services.Games;

public sealed class GameSourceCatalog : IGameSourceCatalog
{
    private readonly IDbContextFactory<HatbandDbContext> contextFactory;
    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IReadOnlyList<IGameSourceInfo> sources;
    private readonly ILogger<GameSourceCatalog> logger;
    private IReadOnlyDictionary<string, GameStore> stores = new Dictionary<string, GameStore>(StringComparer.Ordinal);

    public GameSourceCatalog(
        IDbContextFactory<HatbandDbContext> contextFactory,
        IAppDataFileSystem appDataFileSystem,
        IEnumerable<IGameSourceInfo> sources,
        ILogger<GameSourceCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(logger);
        this.contextFactory = contextFactory;
        this.appDataFileSystem = appDataFileSystem;
        this.sources = ValidateSources(sources);
        this.logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var storesBySourceId = await context.GameStores
            .ToDictionaryAsync(store => store.SourceId, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        var gameSourceIds = await context.Games
            .Select(game => game.SourceId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        EnsureStoreExists(context, storesBySourceId, GameSourceIds.Manual, "Manual");
        foreach (var sourceId in gameSourceIds)
        {
            EnsureStoreExists(context, storesBySourceId, sourceId, sourceId);
        }

        foreach (var source in sources)
        {
            if (!storesBySourceId.TryGetValue(source.SourceId, out var store))
            {
                store = new GameStore { SourceId = source.SourceId, Name = source.DisplayName };
                storesBySourceId.Add(source.SourceId, store);
                context.GameStores.Add(store);
            }
            else
            {
                store.Name = source.DisplayName;
            }

            if (source.LogoAssetPath is { } logoAssetPath)
            {
                await UpdateLogoAsync(store, source, logoAssetPath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                store.LogoPath = null;
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        stores = await context.GameStores.AsNoTracking()
            .ToDictionaryAsync(store => store.SourceId, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
    }

    public string GetDisplayName(string sourceId) => GetStore(sourceId).Name;

    public int GetStoreId(string sourceId) => GetStore(sourceId).Id;

    public string? GetLogoPath(string sourceId) => GetStore(sourceId).LogoPath;

    private GameStore GetStore(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (!stores.TryGetValue(sourceId, out var store))
        {
            throw new InvalidOperationException($"No store information is registered for '{sourceId}'.");
        }

        return store;
    }

    private static IReadOnlyList<IGameSourceInfo> ValidateSources(IEnumerable<IGameSourceInfo> sourceInfos)
    {
        var sourceById = new Dictionary<string, IGameSourceInfo>(StringComparer.Ordinal);
        foreach (var source in sourceInfos)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceId);
            GameSourceIds.Validate(source.SourceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(source.DisplayName);
            if (sourceById.TryGetValue(source.SourceId, out var existingSource))
            {
                if (!string.Equals(existingSource.DisplayName, source.DisplayName, StringComparison.Ordinal) ||
                    !string.Equals(existingSource.LogoAssetPath, source.LogoAssetPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Source '{source.SourceId}' declares conflicting names or logo assets.");
                }

                continue;
            }

            sourceById.Add(source.SourceId, source);
        }

        return sourceById.Values.ToArray();
    }

    private static void EnsureStoreExists(
        HatbandDbContext context,
        IDictionary<string, GameStore> storesBySourceId,
        string sourceId,
        string displayName)
    {
        if (!storesBySourceId.ContainsKey(sourceId))
        {
            var store = new GameStore { SourceId = sourceId, Name = displayName };
            storesBySourceId.Add(sourceId, store);
            context.GameStores.Add(store);
        }
    }

    private async Task UpdateLogoAsync(
        GameStore store,
        IGameSourceInfo source,
        string logoAssetPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var sourceDirectory = Path.GetDirectoryName(source.GetType().Assembly.Location)
                ?? throw new InvalidOperationException("The source plugin assembly has no location.");
            var fullSourceDirectory = Path.GetFullPath(sourceDirectory);
            var fullLogoPath = Path.GetFullPath(Path.Combine(fullSourceDirectory, logoAssetPath));
            var sourceDirectoryPrefix = fullSourceDirectory.EndsWith(Path.DirectorySeparatorChar)
                ? fullSourceDirectory
                : fullSourceDirectory + Path.DirectorySeparatorChar;
            if (!fullLogoPath.StartsWith(sourceDirectoryPrefix, StringComparison.Ordinal) ||
                !File.Exists(fullLogoPath))
            {
                logger.LogWarning(
                    "Store logo asset {LogoAssetPath} declared by {SourceId} was not found in the plugin directory.",
                    logoAssetPath,
                    source.SourceId);
                return;
            }

            var extension = Path.GetExtension(fullLogoPath).ToLowerInvariant();
            if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
            {
                logger.LogWarning(
                    "Store logo asset {LogoAssetPath} declared by {SourceId} has an unsupported file type.",
                    logoAssetPath,
                    source.SourceId);
                return;
            }

            var content = await File.ReadAllBytesAsync(fullLogoPath, cancellationToken).ConfigureAwait(false);
            var contentHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            var relativePath = $"stores/{source.SourceId}/logo-{contentHash}{extension}";
            await appDataFileSystem.WriteAllBytesAtomicallyAsync(relativePath, content, cancellationToken)
                .ConfigureAwait(false);
            store.LogoPath = relativePath;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(exception, "Could not persist the store logo for {SourceId}.", source.SourceId);
        }
    }
}
