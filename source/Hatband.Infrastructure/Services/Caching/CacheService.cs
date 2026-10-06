using System.Text.Json;
using Hatband.Core.Abstractions.Services;
using Hatband.Core.Models;
using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Hatband.Infrastructure.Services.Caching;

public sealed class CacheService : ICacheService
{
    private readonly IDbContextFactory<HatbandDbContext> _dbContextFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly TimeProvider _timeProvider;

    public CacheService(
        IMemoryCache memoryCache,
        IDbContextFactory<HatbandDbContext> dbContextFactory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _memoryCache = memoryCache;
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        DateOnly expiresAt,
        Func<Task<T>> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();

        if (_memoryCache.TryGetValue<T>(key, out var memoryValue) && memoryValue is not null)
        {
            return memoryValue;
        }

        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var currentDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var entry = await dbContext.CacheEntries.SingleOrDefaultAsync(
            cacheEntry => cacheEntry.Key == key,
            cancellationToken);

        if (entry is not null && entry.ExpiresAt >= currentDate)
        {
            try
            {
                var cachedValue = JsonSerializer.Deserialize<T>(entry.Value);
                if (cachedValue is not null)
                {
                    SetMemoryCacheValue(key, cachedValue, entry.ExpiresAt);
                    return cachedValue;
                }
            }
            catch (JsonException)
            {
                // Invalid cache data can be discarded and regenerated.
            }
        }

        if (entry is not null)
        {
            dbContext.CacheEntries.Remove(entry);
        }

        var valueToCache = await callback();
        if (valueToCache is null)
        {
            if (entry is not null)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return valueToCache;
        }

        await dbContext.CacheEntries.AddAsync(
            new CacheEntry(key, JsonSerializer.Serialize(valueToCache), expiresAt),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        SetMemoryCacheValue(key, valueToCache, expiresAt);
        return valueToCache;
    }

    public async Task<int> RemoveExpiredEntriesAsync(CancellationToken cancellationToken = default)
    {
        var currentDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.CacheEntries
            .Where(entry => entry.ExpiresAt < currentDate)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private void SetMemoryCacheValue<T>(string key, T value, DateOnly expiresAt)
    {
        var expirationDate = expiresAt.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var absoluteExpiration = new DateTimeOffset(expirationDate);
        _memoryCache.Set(key, value, absoluteExpiration);
    }
}
