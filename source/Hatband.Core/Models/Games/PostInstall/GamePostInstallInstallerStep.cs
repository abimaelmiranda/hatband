namespace Hatband.Core.Models.Games.PostInstall;

public sealed record GamePostInstallInstallerStep : GamePostInstallStep
{
    public required IReadOnlyList<GamePostInstallProcess> Processes { get; init; }

    public string? HasRunKey { get; init; }

    public GamePostInstallRegistryView RegistryView { get; init; } = GamePostInstallRegistryView.Registry32;

    public uint MinimumHasRunValue { get; init; } = 1;

    public bool IgnoreExitCode { get; init; }

    public bool NoCleanUp { get; init; }
}

public sealed record GamePostInstallProcess
{
    public required string ExecutableTemplate { get; init; }

    public string? Arguments { get; init; }
}
