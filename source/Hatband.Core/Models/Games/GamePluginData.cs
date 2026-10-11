namespace Hatband.Core.Models.Games;

/// <summary>
/// Versioned JSON data owned by a plugin and attached to one game.
/// </summary>
public sealed class GamePluginData : ModelBase
{
    public Guid GameId { get; set; }

    public required string PluginId { get; set; }

    public required string DataKey { get; set; }

    public int SchemaVersion { get; set; }

    public required string JsonPayload { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
