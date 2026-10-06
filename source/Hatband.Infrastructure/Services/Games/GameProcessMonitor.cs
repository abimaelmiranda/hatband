using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using Hatband.Core.Enums.Games;
using Hatband.Core.Models;
using Hatband.Infrastructure.Services.CompatibilityTools;

namespace Hatband.Infrastructure.Services.Games;

public sealed class GameProcessMonitor : IGameProcessMonitor
{
    private readonly ProtonExecutionService _protonExecutionService;

    public GameProcessMonitor(ProtonExecutionService protonExecutionService)
    {
        ArgumentNullException.ThrowIfNull(protonExecutionService);
        _protonExecutionService = protonExecutionService;
    }

    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _startTimeout = TimeSpan.FromMinutes(10);
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
        var hasReportedStartTimeout = false;
        var protonProcess = target.ProtonProcess;
        if (protonProcess is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(protonProcess.ExecutablePath);
        }

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (protonProcess is not null)
                {
                    var completedExitCode = _protonExecutionService.GetCompletedExitCode(protonProcess.GameId);
                    if (completedExitCode is not null)
                    {
                        if (completedExitCode != 0)
                        {
                            yield return GameProcessMonitorEvent.Failed;
                        }
                        else if (!hasStarted)
                        {
                            yield return GameProcessMonitorEvent.StartNotObserved;
                        }
                        else
                        {
                            yield return GameProcessMonitorEvent.Stopped;
                        }

                        yield break;
                    }
                }

                var processIsRunning = IsGameProcessRunning(installDirectory, protonProcess);
                if (processIsRunning)
                {
                    consecutiveMissingPolls = 0;
                    if (!hasStarted)
                    {
                        hasStarted = true;
                        yield return GameProcessMonitorEvent.Started;
                    }
                }
                else if (hasStarted && protonProcess is null)
                {
                    consecutiveMissingPolls++;
                    if (consecutiveMissingPolls >= MissingPollsBeforeStop)
                    {
                        yield return GameProcessMonitorEvent.Stopped;
                        yield break;
                    }
                }
                else if (!hasStarted && !hasReportedStartTimeout && startedAt.Elapsed >= _startTimeout)
                {
                    hasReportedStartTimeout = true;
                    yield return GameProcessMonitorEvent.StartTimedOut;
                    if (protonProcess is null)
                    {
                        yield break;
                    }
                }

                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (protonProcess is not null)
            {
                _protonExecutionService.ForgetLaunch(protonProcess.GameId);
            }
        }
    }

    private static bool IsGameProcessRunning(string installDirectory, ProtonProcessWatchTarget? protonProcess)
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
                    if (protonProcess is not null && OperatingSystem.IsLinux())
                    {
                        if (IsProtonGameProcess(process, protonProcess))
                        {
                            return true;
                        }

                        continue;
                    }

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

    [SupportedOSPlatform("linux")]
    private static bool IsProtonGameProcess(Process process, ProtonProcessWatchTarget target)
    {
        var environmentPath = $"/proc/{process.Id}/environ";
        if (!LinuxProcessFileReader.TryReadAllText(environmentPath, out var environmentContent))
        {
            return false;
        }

        var environment = environmentContent.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var marker = $"HATBAND_GAME_ID={target.GameId:N}";
        if (!environment.Contains(marker, StringComparer.Ordinal))
        {
            return false;
        }

        var executablePath = process.MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var executableName = Path.GetFileName(executablePath);
        if (!executableName.StartsWith("wine", StringComparison.OrdinalIgnoreCase) &&
            !executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!LinuxProcessFileReader.TryReadAllText($"/proc/{process.Id}/cmdline", out var commandLineContent))
        {
            return false;
        }

        var commandLine = commandLineContent.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var targetExecutableName = Path.GetFileName(target.ExecutablePath);
        foreach (var argument in commandLine)
        {
            var name = Path.GetFileName(argument.Replace('\\', '/'));
            if (string.Equals(name, targetExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
