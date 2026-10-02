using Hatband.Core.Abstractions;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Persistence;

public sealed class GameLibraryService : IGameLibraryService
{
    private readonly DbContextOptions<HatbandDbContext> options;

    public GameLibraryService(DbContextOptions<HatbandDbContext> options)
    {
        this.options = options;
    }

    public void InitializeDatabase()
    {
        using var context = new HatbandDbContext(options);
        context.Database.Migrate();

        if (!context.Libraries.Any())
        {
            context.Libraries.Add(new GameLibrary());
            context.SaveChanges();
        }
    }

    public async Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = new HatbandDbContext(options);

        return await context.Games
            .AsNoTracking()
            .Include(game => game.LaunchActions)
            .OrderBy(game => game.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddGameAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(game.Name);

        await using var context = new HatbandDbContext(options);
        var library = await context.Libraries
            .OrderBy(item => item.Id)
            .FirstAsync(cancellationToken);

        // ModelBase assigns the GUID before EF tracks the entity. Add it explicitly
        // so EF doesn't interpret that non-empty generated key as an existing row
        // when it discovers the game through the tracked library navigation.
        context.Games.Add(game);
        library.Games.Add(game);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateGameMetadataAsync(
        Guid gameId,
        GameMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        await using var context = new HatbandDbContext(options);
        var game = await context.Games.SingleAsync(item => item.Id == gameId, cancellationToken);
        game.Metadata = metadata;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateGameDetailsAsync(
        Guid gameId,
        string name,
        bool isNameCustomized,
        GameMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(metadata);

        await using var context = new HatbandDbContext(options);
        var game = await context.Games.SingleAsync(item => item.Id == gameId, cancellationToken);
        game.Name = name.Trim();
        game.IsNameCustomized = isNameCustomized;
        game.Metadata = metadata;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateGameTimeToBeatAsync(
        Guid gameId,
        GameTimeToBeat? timeToBeat,
        CancellationToken cancellationToken = default)
    {
        await using var context = new HatbandDbContext(options);
        var game = await context.Games.SingleAsync(item => item.Id == gameId, cancellationToken);
        game.TimeToBeat = timeToBeat;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetGameHiddenAsync(
        Guid gameId,
        bool isHidden,
        CancellationToken cancellationToken = default)
    {
        await using var context = new HatbandDbContext(options);
        var game = await context.Games.SingleAsync(item => item.Id == gameId, cancellationToken);
        game.IsHidden = isHidden;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SynchronizeGamesAsync(
        GameSourceId sourceId,
        IReadOnlyList<Game> importedGames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(importedGames);

        await using var context = new HatbandDbContext(options);
        var existingGames = await context.Games
            .Where(game => game.SourceId == sourceId)
            .ToListAsync(cancellationToken);
        var existingBySourceId = new Dictionary<string, Game>(StringComparer.Ordinal);
        foreach (var existingGame in existingGames)
        {
            if (existingGame.SourceGameId is string sourceGameId)
            {
                existingBySourceId.TryAdd(sourceGameId, existingGame);
            }
        }
        var library = await context.Libraries
            .OrderBy(item => item.Id)
            .FirstAsync(cancellationToken);

        foreach (var importedGame in importedGames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(importedGame.Name);
            importedGame.SourceId = sourceId;

            if (importedGame.SourceGameId is string sourceGameId &&
                existingBySourceId.TryGetValue(sourceGameId, out var existingGame))
            {
                existingGame.ApplyLibraryImport(importedGame);
                continue;
            }

            context.Games.Add(importedGame);
            library.Games.Add(importedGame);
            if (importedGame.SourceGameId is string newSourceGameId)
            {
                existingBySourceId.TryAdd(newSourceGameId, importedGame);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RefreshInstallationStatesAsync(
        GameSourceId sourceId,
        IReadOnlyList<GameInstallationInfo> installedGames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installedGames);
        var installedGamesBySourceId = installedGames.ToDictionary(
            game => game.SourceGameId,
            StringComparer.Ordinal);

        await using var context = new HatbandDbContext(options);
        var games = await context.Games
            .Where(game => game.SourceId == sourceId)
            .ToListAsync(cancellationToken);

        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (game.SourceGameId is not null && installedGamesBySourceId.TryGetValue(game.SourceGameId, out var installation))
            {
                game.IsInstalled = true;
                game.InstallDirectory = installation.InstallDirectory;
                continue;
            }

            game.IsInstalled = false;
            game.InstallDirectory = null;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

}
