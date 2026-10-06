namespace Hatband.Core.Models;

public sealed record GameProcessWatchTarget(string InstallDirectory, ProtonProcessWatchTarget? ProtonProcess = null);
