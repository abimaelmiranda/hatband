namespace Hatband.Core.Models.Games;

public sealed record GameInstallationInfo
{
    public required string InstallDirectory { get; init; }
}
