namespace Hatband.Core.Models;

/// <summary>
/// The user's collection of games.
/// </summary>
public sealed class GameLibrary : ModelBase
{
    public string Name { get; set; } = "My Library";

    public List<Game> Games { get; set; } = [];
}
