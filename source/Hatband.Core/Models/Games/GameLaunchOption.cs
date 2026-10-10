namespace Hatband.Core.Models.Games;

public sealed record GameLaunchOption(
    string Id,
    string Name,
    string ExecutablePath,
    string? Arguments,
    string WorkingDirectory);
