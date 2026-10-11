using System.Text.Json;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Hatband.SDK.Plugins;
using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Plugins;

public sealed class PluginDataStore : IPluginDataStore
{
    private const int MaximumKeyLength = 128;
    private readonly IDbContextFactory<HatbandDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    public PluginDataStore(IDbContextFactory<HatbandDbContext> contextFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    public async Task<PluginGameDataValue<T>?> GetGameDataAsync<T>(
        string pluginId,
        Guid gameId,
        string key,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(pluginId, gameId, key);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var data = await context.GamePluginData.AsNoTracking()
            .SingleOrDefaultAsync(item => item.GameId == gameId && item.PluginId == pluginId && item.DataKey == key,
                cancellationToken);
        if (data is null)
        {
            return null;
        }

        var value = JsonSerializer.Deserialize<T>(data.JsonPayload)
            ?? throw new InvalidDataException($"Plugin data '{pluginId}/{key}' deserialized to null.");
        return new PluginGameDataValue<T>(data.SchemaVersion, value);
    }

    public async Task SetGameDataAsync<T>(
        string pluginId,
        Guid gameId,
        string key,
        int schemaVersion,
        T value,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(pluginId, gameId, key);
        ArgumentNullException.ThrowIfNull(value);
        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "A plugin data schema version must be positive.");
        }

        var jsonPayload = JsonSerializer.Serialize(value);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var data = await context.GamePluginData.SingleOrDefaultAsync(
            item => item.GameId == gameId && item.PluginId == pluginId && item.DataKey == key,
            cancellationToken);
        if (data is null)
        {
            context.GamePluginData.Add(new GamePluginData
            {
                GameId = gameId,
                PluginId = pluginId,
                DataKey = key,
                SchemaVersion = schemaVersion,
                JsonPayload = jsonPayload,
                UpdatedAt = _timeProvider.GetUtcNow(),
            });
        }
        else
        {
            data.SchemaVersion = schemaVersion;
            data.JsonPayload = jsonPayload;
            data.UpdatedAt = _timeProvider.GetUtcNow();
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGameDataAsync(
        string pluginId,
        Guid gameId,
        string key,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(pluginId, gameId, key);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.GamePluginData
            .Where(item => item.GameId == gameId && item.PluginId == pluginId && item.DataKey == key)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static void ValidateIdentity(string pluginId, Guid gameId, string key)
    {
        GameSourceIds.Validate(pluginId, nameof(pluginId));
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("A game ID is required.", nameof(gameId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Length > MaximumKeyLength)
        {
            throw new ArgumentException($"Plugin data keys cannot exceed {MaximumKeyLength} characters.", nameof(key));
        }
    }
}
