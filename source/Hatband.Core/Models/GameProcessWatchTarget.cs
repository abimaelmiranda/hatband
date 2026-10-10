namespace Hatband.Core.Models;

public sealed record GameProcessWatchTarget(string InstallDirectory, GameRuntimeProcessWatchTarget? RuntimeProcess = null);
