namespace Hatband.Core.Models;

/// <summary>
/// Describes one way to start a game.
/// </summary>
public sealed class GameLaunchAction : ModelBase
{
    public string Name { get; set; } = "Play";

    public GameLaunchActionType Type { get; set; } = GameLaunchActionType.Executable;

    /// <summary>
    /// Executable path or URI, depending on <see cref="Type"/>.
    /// </summary>
    public required string Target { get; set; }

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public bool IsPrimary { get; set; }
}

public enum GameLaunchActionType
{
    Executable,
    Uri
}
