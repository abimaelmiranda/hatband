namespace Hatband.Core.Models.Games;

public sealed record GameLaunchCommand(
    string ExecutablePath,
    string? Arguments,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string> EnvironmentVariables);
