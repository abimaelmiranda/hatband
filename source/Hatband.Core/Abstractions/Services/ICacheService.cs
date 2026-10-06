namespace Hatband.Core.Abstractions.Services;

public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        DateOnly expiresAt,
        Func<Task<T>> callback,
        CancellationToken cancellationToken = default);

    Task<int> RemoveExpiredEntriesAsync(CancellationToken cancellationToken = default);
}
