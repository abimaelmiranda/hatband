namespace Hatband.Core.Models.Games;

/// <summary>
/// A persisted library source and its optional branding artwork.
/// </summary>
public sealed class GameStore
{
    public int Id { get; set; }

    public required string SourceId { get; set; }

    public required string Name { get; set; }

    public string? LogoPath { get; set; }
}
