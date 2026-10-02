using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums;
using Hatband.Core.Enums.Stores;
using Microsoft.Win32;

namespace Hatband.Infrastructure.Host;

public sealed class HostApplicationLauncher : IHostApplicationLauncher
{
    private readonly IHostSystemInfo hostSystemInfo;

    public HostApplicationLauncher(IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        this.hostSystemInfo = hostSystemInfo;
    }

    public Task<bool> TryOpenUriAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            return Task.FromResult(process is not null);
        }
        catch (Win32Exception)
        {
            return Task.FromResult(false);
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> TryLaunchStoreClientAsync(
        GameSourceId sourceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceId != GameSourceId.Steam)
        {
            return Task.FromResult(false);
        }

        var started = hostSystemInfo.Platform switch
        {
            HostOperatingSystem.Windows when OperatingSystem.IsWindows() => TryLaunchWindowsSteamClient(),
            HostOperatingSystem.MacOS => TryLaunchMacSteamClient(),
            HostOperatingSystem.Linux => TryLaunchLinuxSteamClient(),
            _ => false
        };

        return Task.FromResult(started);
    }

    [SupportedOSPlatform("windows")]
    private static bool TryLaunchWindowsSteamClient()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var steamExecutable = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            "SteamExe",
            null) as string;

        if (string.IsNullOrWhiteSpace(steamExecutable) || !File.Exists(steamExecutable))
        {
            return false;
        }

        return TryStartProcess(steamExecutable);
    }

    private static bool TryLaunchMacSteamClient()
    {
        var userProfileDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var steamBundles = new[]
        {
            "/Applications/Steam.app",
            Path.Combine(userProfileDirectory, "Applications", "Steam.app"),
            Path.Combine(
                userProfileDirectory,
                "Library",
                "Application Support",
                "Steam",
                "Steam.AppBundle",
                "Steam.app")
        };

        if (!steamBundles.Any(Directory.Exists))
        {
            return false;
        }

        return TryStartProcess("open", "-a", "Steam");
    }

    private static bool TryLaunchLinuxSteamClient()
    {
        if (TryStartProcess("steam"))
        {
            return true;
        }

        return TryStartProcess("flatpak", "run", "com.valvesoftware.Steam");
    }

    private static bool TryStartProcess(string fileName, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            return Process.Start(startInfo) is not null;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
