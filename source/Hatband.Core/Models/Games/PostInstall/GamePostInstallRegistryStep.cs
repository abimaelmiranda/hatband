namespace Hatband.Core.Models.Games.PostInstall;

public sealed record GamePostInstallRegistryStep : GamePostInstallStep
{
    public required string KeyPath { get; init; }

    public required GamePostInstallRegistryView View { get; init; }

    public required IReadOnlyList<GamePostInstallRegistryEntry> Values { get; init; }
}
