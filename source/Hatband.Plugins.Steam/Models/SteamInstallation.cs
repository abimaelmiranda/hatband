namespace Hatband.Plugins.Steam.Models;

public sealed record SteamInstallation
{
    public required string RootPath { get; init; }

    public required IReadOnlyList<SteamLibraryLocation> Libraries { get; init; }
}
