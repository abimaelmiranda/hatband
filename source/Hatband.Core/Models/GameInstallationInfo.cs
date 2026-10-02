namespace Hatband.Core.Models;

public sealed record GameInstallationInfo
{
    public required string SourceGameId { get; init; }

    public required string InstallDirectory { get; init; }
}
