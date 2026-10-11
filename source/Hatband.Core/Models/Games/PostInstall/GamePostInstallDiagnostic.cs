namespace Hatband.Core.Models.Games.PostInstall;

public sealed record GamePostInstallDiagnostic
{
    public required string Code { get; init; }

    public required string Message { get; init; }
}
