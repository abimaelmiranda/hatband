namespace Hatband.Core.Models.Games.PostInstall;

/// <summary>
/// Source-provided post-install actions for a game installation.
/// </summary>
public sealed record GamePostInstallPlan
{
    /// <summary>
    /// Changes whenever the source's post-install definition changes.
    /// </summary>
    public required string Revision { get; init; }

    public required IReadOnlyList<GamePostInstallStep> Steps { get; init; }

    public required IReadOnlyList<GamePostInstallDiagnostic> Diagnostics { get; init; }
}
