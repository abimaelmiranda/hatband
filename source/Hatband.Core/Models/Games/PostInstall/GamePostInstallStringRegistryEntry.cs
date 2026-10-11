namespace Hatband.Core.Models.Games.PostInstall;

public sealed record GamePostInstallStringRegistryEntry : GamePostInstallRegistryEntry
{
    public required string Value { get; init; }
}
