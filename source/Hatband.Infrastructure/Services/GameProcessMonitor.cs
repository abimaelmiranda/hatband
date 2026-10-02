using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums;
using Hatband.Core.Models;

namespace Hatband.Infrastructure.Services;

public sealed class GameProcessMonitor : IGameProcessMonitor
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(10);
    private const int MissingPollsBeforeStop = 3;

    public async IAsyncEnumerable<GameProcessMonitorEvent> WatchAsync(
        GameProcessWatchTarget target,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(target.InstallDirectory);

        var installDirectory = Path.GetFullPath(target.InstallDirectory);
        var startedAt = Stopwatch.StartNew();
        var hasStarted = false;
        var consecutiveMissingPolls = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var processIsRunning = IsGameProcessRunning(installDirectory);
            if (processIsRunning)
            {
                consecutiveMissingPolls = 0;
                if (!hasStarted)
                {
                    hasStarted = true;
                    yield return GameProcessMonitorEvent.Started;
                }
            }
            else if (hasStarted)
            {
                consecutiveMissingPolls++;
                if (consecutiveMissingPolls >= MissingPollsBeforeStop)
                {
                    yield return GameProcessMonitorEvent.Stopped;
                    yield break;
                }
            }
            else if (startedAt.Elapsed >= StartTimeout)
            {
                yield return GameProcessMonitorEvent.StartTimedOut;
                yield break;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsGameProcessRunning(string installDirectory)
    {
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var installDirectoryPrefix = Path.EndsInDirectorySeparator(installDirectory)
            ? installDirectory
            : installDirectory + Path.DirectorySeparatorChar;

        var processes = Process.GetProcesses();
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    var executablePath = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(executablePath))
                    {
                        continue;
                    }

                    var fullExecutablePath = Path.GetFullPath(executablePath);
                    if (string.Equals(fullExecutablePath, installDirectory, pathComparison) ||
                        fullExecutablePath.StartsWith(installDirectoryPrefix, pathComparison))
                    {
                        return true;
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or
                                                   Win32Exception or
                                                   IOException or
                                                   NotSupportedException or
                                                   SecurityException or
                                                   UnauthorizedAccessException or
                                                   ArgumentException)
                {
                    // Processes can exit or deny access while the system process list is inspected.
                }
            }

            return false;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
