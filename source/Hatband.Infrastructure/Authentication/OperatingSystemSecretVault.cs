using System.ComponentModel;
using System.Diagnostics;
using Hatband.Core.Abstractions.Authentication;

namespace Hatband.Infrastructure.Authentication;

/// <summary>
/// Stores application encryption keys in the host operating system's credential store.
/// </summary>
public sealed partial class OperatingSystemSecretVault : ISecureSecretVault
{
    public Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsWindows())
        {
            return Task.FromResult(ReadWindows(key));
        }

        if (OperatingSystem.IsMacOS())
        {
            return Task.Run(() => ReadMacOS(key, cancellationToken), cancellationToken);
        }

        if (OperatingSystem.IsLinux())
        {
            return ReadLinuxAsync(key, cancellationToken);
        }

        throw new PlatformNotSupportedException("Hatband secure storage is unavailable on this operating system.");
    }

    public async Task WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (value.IsEmpty)
        {
            throw new ArgumentException("A stored secret cannot be empty.", nameof(value));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
        {
            WriteWindows(key, value.Span);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            await Task.Run(() => WriteMacOS(key, value, cancellationToken), cancellationToken);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            await WriteLinuxAsync(key, value, cancellationToken);
            return;
        }

        throw new PlatformNotSupportedException("Hatband secure storage is unavailable on this operating system.");
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
        {
            DeleteWindows(key);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            await Task.Run(() => DeleteMacOS(key, cancellationToken), cancellationToken);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            await DeleteLinuxAsync(key, cancellationToken);
            return;
        }

        throw new PlatformNotSupportedException("Hatband secure storage is unavailable on this operating system.");
    }

    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Contains('\0'))
        {
            throw new ArgumentException("A credential key cannot contain a null character.", nameof(key));
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(
        ProcessStartInfo startInfo,
        string? secretInput,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("The operating system's secure credential service is unavailable.", exception);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            if (secretInput is not null)
            {
                await process.StandardInput.WriteAsync(secretInput.AsMemory(), cancellationToken);
                process.StandardInput.Close();
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The process exited between the state check and the kill request.
                }

                await process.WaitForExitAsync(CancellationToken.None);
            }

            await outputTask;
            await errorTask;
            throw;
        }

        return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
