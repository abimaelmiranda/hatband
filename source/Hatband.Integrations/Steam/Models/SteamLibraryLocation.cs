namespace Hatband.Integrations.Steam.Models;

public sealed record SteamLibraryLocation
{
    public required int VolumeIndex { get; init; }

    public required string Path { get; init; }
}
