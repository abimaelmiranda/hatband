using Hatband.Core.Models;

namespace Hatband.Core.Models.Games;

/// <summary>
/// Describes one way to start a game.
/// </summary>
public sealed class GameAction : ModelBase
{
    public string Name { get; set; } = "Play";

    public GameActionType Type { get; set; } = GameActionType.Executable;

    /// <summary>
    /// Executable path or URI, depending on <see cref="Type"/>.
    /// </summary>
    public required string Target { get; set; }

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public bool IsPrimary { get; set; }
}

public enum GameActionType
{
    Executable,
    Uri
}
