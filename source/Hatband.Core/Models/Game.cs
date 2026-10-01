using Hatband.Core.Enums.Stores;

namespace Hatband.Core.Models;

/// <summary>
/// A game entry in a user's library.
/// </summary>
public sealed class Game : ModelBase
{
    public required string Name { get; set; }

    public bool IsNameCustomized { get; set; }

    /// <summary>
    /// Stable identifier of the integration that provides this game, such as "steam".
    /// </summary>
    public GameSourceId? SourceId { get; set; }

    /// <summary>
    /// Identifier assigned to this game by its source.
    /// </summary>
    public string? SourceGameId { get; set; }

    public GameMetadata Metadata { get; set; } = new();

    public GameTimeToBeat? TimeToBeat { get; set; }

    public bool IsInstalled { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsHidden { get; set; }

    public string? Notes { get; set; }

    public string? SortingName { get; set; }

    /// <summary>
    /// UTC date and time when this game was last played. Convert it to the preferred user time zone only in the presentation layer.
    /// </summary>
    public DateTime? LastActivity { get; set; }

    /// <summary>
    /// UTC date and time when this game was added to the library. Convert it to the preferred user time zone only in the presentation layer.
    /// </summary>
    public DateTime? Added { get; set; }

    /// <summary>
    /// Total play time, in seconds.
    /// </summary>
    public long PlaytimeSeconds { get; set; }

    public long PlayCount { get; set; }

    public string? Version { get; set; }

    public string? InstallDirectory { get; set; }

    /// <summary>
    /// Installed size, in bytes, when known.
    /// </summary>
    public long? InstallSizeBytes { get; set; }

    public List<GameLaunchAction> LaunchActions { get; set; } = [];

    public void ApplyLibraryImport(Game importedGame)
    {
        ArgumentNullException.ThrowIfNull(importedGame);
        ArgumentException.ThrowIfNullOrWhiteSpace(importedGame.Name);

        if (!IsNameCustomized)
        {
            if (string.IsNullOrWhiteSpace(Metadata.StoreName) &&
                !string.Equals(Name, importedGame.Name, StringComparison.Ordinal))
            {
                TimeToBeat = null;
            }

            Name = importedGame.Name;
        }

        IsInstalled = importedGame.IsInstalled;
        InstallDirectory = importedGame.InstallDirectory;
        PlaytimeSeconds = importedGame.PlaytimeSeconds;
        LastActivity = importedGame.LastActivity;
    }
}
