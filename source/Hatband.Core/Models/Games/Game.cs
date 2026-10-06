using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Host;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Core.Models.Games;

/// <summary>
/// A game entry in a user's library.
/// </summary>
public sealed class Game : ModelBase
{
    public required string Name { get; set; }

    /// <summary>
    /// Source that owns this game. Manual games have no source game ID.
    /// </summary>
    public required GameSourceId SourceId { get; set; }

    /// <summary>
    /// Identifier assigned to this game by its source. Required for non-manual sources.
    /// </summary>
    public string? SourceGameId { get; set; }

    public GameMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Compares this game's declared native platforms with the host operating system.
    /// </summary>
    public HostPlatformCompatibilityStatus GetHostPlatformCompatibilityStatus(
        HostOperatingSystem hostOperatingSystem)
    {
        if (Metadata.NativePlatforms is not GamePlatform nativePlatforms ||
            hostOperatingSystem == HostOperatingSystem.Unknown)
        {
            return HostPlatformCompatibilityStatus.Unknown;
        }

        return hostOperatingSystem switch
        {
            HostOperatingSystem.Windows => nativePlatforms.HasFlag(GamePlatform.Windows)
                ? HostPlatformCompatibilityStatus.Native
                : HostPlatformCompatibilityStatus.Unsupported,
            HostOperatingSystem.MacOS => nativePlatforms.HasFlag(GamePlatform.MacOS)
                ? HostPlatformCompatibilityStatus.Native
                : HostPlatformCompatibilityStatus.Unsupported,
            HostOperatingSystem.Linux => nativePlatforms.HasFlag(GamePlatform.Linux)
                ? HostPlatformCompatibilityStatus.Native
                : nativePlatforms.HasFlag(GamePlatform.Windows)
                    ? HostPlatformCompatibilityStatus.RequiresCompatibilityTool
                    : HostPlatformCompatibilityStatus.Unsupported,
            _ => HostPlatformCompatibilityStatus.Unknown
        };
    }

    public GameArtwork Artwork { get; set; } = new();

    public GameArtwork? DefaultArtwork { get; set; }

    /// <summary>
    /// Compatibility runtime selected for this game. When null, the platform's default behavior is used.
    /// </summary>
    public CompatibilityTool? CompatibilityTool { get; set; }

    /// <summary>
    /// Configured compatibility prefix directory. Null means no prefix has been configured.
    /// </summary>
    public GameCompatibilityPrefix? CompatibilityPrefix { get; set; }

    public GameTimeToBeat? TimeToBeat { get; set; }

    public GameInstallationInfo? InstallationInfo { get; set; }

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

    /// <summary>
    /// Installed size, in bytes, when known.
    /// </summary>
    public long? InstallSizeBytes { get; set; }

    public List<GameAction> GameActions { get; set; } = [];

}
