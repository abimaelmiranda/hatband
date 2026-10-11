namespace Hatband.Core.Models.Games.PostInstall;

public sealed record GamePostInstallCopyFolderStep : GamePostInstallStep
{
    public required string SourceTemplate { get; init; }

    public required string DestinationTemplate { get; init; }
}
