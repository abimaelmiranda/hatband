namespace Hatband.Integrations.Steam.Models;

/// <summary>
/// Represents a game as returned by Steam's library APIs or local install manifests.
/// </summary>
public sealed record SteamLibraryGame
{
    public required uint AppId { get; init; }

    public required string Name { get; init; }

    public bool IsInstalled { get; init; }

    public string? InstallDirectory { get; init; }

    public long PlaytimeSeconds { get; init; }

    public DateTime? LastActivity { get; init; }
}
