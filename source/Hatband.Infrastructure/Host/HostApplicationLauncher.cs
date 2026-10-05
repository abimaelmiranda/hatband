using System.ComponentModel;
using System.Diagnostics;

namespace Hatband.Infrastructure.Host;

public sealed class HostApplicationLauncher : IHostApplicationLauncher
{
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
}
