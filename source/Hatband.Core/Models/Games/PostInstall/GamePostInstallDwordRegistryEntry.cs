namespace Hatband.Core.Models.Games.PostInstall;

public sealed record GamePostInstallDwordRegistryEntry : GamePostInstallRegistryEntry
{
    public required uint Value { get; init; }
}
