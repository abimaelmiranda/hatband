namespace Hatband.Core.Models.Games.PostInstall;

public sealed record GamePostInstallProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
