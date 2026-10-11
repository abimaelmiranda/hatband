namespace Hatband.SDK.Plugins;

/// <summary>
/// Stores plugin-owned, versioned data for a game without adding plugin-specific columns to Games.
/// </summary>
public interface IPluginDataStore
{
    Task<PluginGameDataValue<T>?> GetGameDataAsync<T>(
        string pluginId,
        Guid gameId,
        string key,
        CancellationToken cancellationToken = default);

    Task SetGameDataAsync<T>(
        string pluginId,
        Guid gameId,
        string key,
        int schemaVersion,
        T value,
        CancellationToken cancellationToken = default);

    Task DeleteGameDataAsync(
        string pluginId,
        Guid gameId,
        string key,
        CancellationToken cancellationToken = default);
}
