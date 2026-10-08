using System.ComponentModel;
using System.Diagnostics;

namespace Hatband.Infrastructure.Authentication;

public sealed partial class OperatingSystemSecretVault
{
    private static async Task<byte[]?> ReadLinuxAsync(string key, CancellationToken cancellationToken)
    {
        var result = await RunSecretToolAsync("lookup", key, null, cancellationToken);
        if (result.ExitCode == 1 && (string.IsNullOrWhiteSpace(result.Error) ||
            result.Error.Contains("no such secret", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        EnsureLinuxSuccess(result);
        try
        {
            return Convert.FromBase64String(result.Output.TrimEnd('\r', '\n'));
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The encryption key saved in the Linux Secret Service has an invalid format.", exception);
        }
    }

    private static async Task WriteLinuxAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken)
    {
        var result = await RunSecretToolAsync("store", key, Convert.ToBase64String(value.Span), cancellationToken);
        EnsureLinuxSuccess(result);
    }

    private static async Task DeleteLinuxAsync(string key, CancellationToken cancellationToken)
    {
        var result = await RunSecretToolAsync("clear", key, null, cancellationToken);
        if (result.ExitCode == 1 && (string.IsNullOrWhiteSpace(result.Error) ||
            result.Error.Contains("no such secret", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        EnsureLinuxSuccess(result);
    }

    private static Task<ProcessResult> RunSecretToolAsync(
        string operation,
        string key,
        string? value,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "secret-tool",
            RedirectStandardInput = value is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(operation);
        if (operation == "store")
        {
            startInfo.ArgumentList.Add("--label=Hatband session encryption key");
        }

        startInfo.ArgumentList.Add("application");
        startInfo.ArgumentList.Add("Hatband");
        startInfo.ArgumentList.Add("identifier");
        startInfo.ArgumentList.Add(key);
        return RunSecretToolProcessAsync(startInfo, value, cancellationToken);
    }

    private static async Task<ProcessResult> RunSecretToolProcessAsync(
        ProcessStartInfo startInfo,
        string? value,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RunProcessAsync(startInfo, value, cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.InnerException is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("Install libsecret-tools and enable a Secret Service provider such as GNOME Keyring or KWallet.", exception);
        }
    }

    private static void EnsureLinuxSuccess(ProcessResult result)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        var message = result.Error.Contains("org.freedesktop.DBus.Error.ServiceUnknown", StringComparison.Ordinal)
            ? "Install a Secret Service provider such as GNOME Keyring or KWallet, then sign in again."
            : "Hatband could not access the Linux Secret Service. Check that its keyring is installed and unlocked.";
        throw new InvalidOperationException(message);
    }
}
