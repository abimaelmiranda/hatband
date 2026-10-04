using Hatband.Core.Enums.Games;

namespace Hatband.Core.Models.Games;

/// <summary>
/// An installed compatibility runtime that can be selected for a game, such as Proton or Wine.
/// </summary>
public sealed record CompatibilityTool(
    string Name,
    string Version,
    string InstallationPath,
    CompatibilityToolSource Source);
