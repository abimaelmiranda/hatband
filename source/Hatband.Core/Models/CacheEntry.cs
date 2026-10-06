namespace Hatband.Core.Models;

public sealed record CacheEntry(string Key, string Value, DateOnly ExpiresAt);
