namespace Hatband.Core.Models.Games;

/// <summary>
/// A destination offered by a game provider for installing a game.
/// </summary>
/// <param name="Id">The provider-specific identifier used to select this destination.</param>
/// <param name="DisplayName">The user-facing name of this destination.</param>
public sealed record GameInstallLocation(string Id, string DisplayName);
