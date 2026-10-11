using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Hatband.Infrastructure.Host;

public sealed class HostApplicationLauncher : IHostApplicationLauncher
{
    private readonly ILogger<HostApplicationLauncher> _logger;

    public HostApplicationLauncher(ILogger<HostApplicationLauncher> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task<bool> TryOpenUriAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }) is not null);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> TryLaunchApplicationAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            return Task.FromResult(Process.Start(startInfo) is not null);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return Task.FromResult(false);
        }
    }

    public Task<HostApplicationProcess?> StartApplicationAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string> environmentVariables,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environmentVariables);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            foreach (var (name, value) in environmentVariables)
            {
                startInfo.Environment[name] = value;
            }

            var process = Process.Start(startInfo);
            if (process is null)
            {
                return Task.FromResult<HostApplicationProcess?>(null);
            }

            return Task.FromResult<HostApplicationProcess?>(new HostApplicationProcess(ObserveProcessAsync(process, executable)));
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or ArgumentException)
        {
            _logger.LogError(exception, "Could not start host application {Executable}.", executable);
            return Task.FromResult<HostApplicationProcess?>(null);
        }
    }

    public Task<bool> TryLaunchApplicationAsync(
        string executable,
        string? arguments,
        string? workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                Arguments = arguments ?? string.Empty,
                UseShellExecute = false
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            return Task.FromResult(Process.Start(startInfo) is not null);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or ArgumentException)
        {
            return Task.FromResult(false);
        }
    }

    public async Task<HostApplicationResult> RunApplicationAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string> environmentVariables,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environmentVariables);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var (name, value) in environmentVariables)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start host command '{executable}'.");
        using var cancellationRegistration = cancellationToken.Register(() => StopCommand(process, executable));
        var outputTask = ReadCapturedOutputAsync(process.StandardOutput, executable, cancellationToken);
        var errorTask = ReadCapturedOutputAsync(process.StandardError, executable, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Both output readers observe cancellation before their streams are disposed.
            }

            throw;
        }
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        return new HostApplicationResult(process.ExitCode, output, error);
    }

    private void StopCommand(Process process, string executable)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            _logger.LogDebug(exception, "Could not stop canceled host command {Executable}.", executable);
        }
    }

    private async Task<string> ReadCapturedOutputAsync(
        StreamReader reader,
        string executable,
        CancellationToken cancellationToken)
    {
        const int maximumCapturedCharacters = 65_536;
        var output = new StringBuilder();
        var buffer = new char[4096];
        var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        while (count > 0)
        {
            var chunk = new string(buffer, 0, count);
            _logger.LogDebug("{Executable}: {Output}", executable, chunk);
            output.Append(chunk);
            if (output.Length > maximumCapturedCharacters)
            {
                output.Remove(0, output.Length - maximumCapturedCharacters);
            }

            count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return output.ToString();
    }

    public bool IsProcessRunning(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    public async Task<bool> TryCloseProcessGracefullyAsync(
        string processName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        cancellationToken.ThrowIfCancellationRequested();
        var processes = Process.GetProcessesByName(processName);
        try
        {
            foreach (var process in processes)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    process.CloseMainWindow();
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15) && IsProcessRunning(processName))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return !IsProcessRunning(processName);
    }

    private async Task<int> ObserveProcessAsync(Process process, string executable)
    {
        using (process)
        {
            var standardOutputTask = LogOutputAsync(process.StandardOutput, executable, isError: false);
            var standardErrorTask = LogOutputAsync(process.StandardError, executable, isError: true);
            try
            {
                await process.WaitForExitAsync();
                await Task.WhenAll(standardOutputTask, standardErrorTask);
                var exitCode = process.ExitCode;
                if (exitCode != 0)
                {
                    _logger.LogWarning("Host application {Executable} exited with code {ExitCode}.", executable, exitCode);
                }

                return exitCode;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed while observing host application {Executable}.", executable);
                throw;
            }
        }
    }

    private async Task LogOutputAsync(StreamReader reader, string executable, bool isError)
    {
        var line = await reader.ReadLineAsync();
        while (line is not null)
        {
            if (isError)
            {
                _logger.LogWarning("{Executable}: {Output}", executable, line);
            }
            else
            {
                _logger.LogDebug("{Executable}: {Output}", executable, line);
            }

            line = await reader.ReadLineAsync();
        }
    }
}
