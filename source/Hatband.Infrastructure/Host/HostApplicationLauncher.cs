using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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

    public Task<bool> TryLaunchSteamGameAsync(uint appId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (hostSystemInfo.Platform != HostOperatingSystem.MacOS || !OperatingSystem.IsMacOS())
        {
            return Task.FromResult(false);
        }

        var steamBundle = FindMacSteamBundle();
        if (steamBundle is null)
        {
            return Task.FromResult(false);
        }

        var started = TryStartProcess(
            "open",
            "-a",
            steamBundle,
            "--args",
            "-silent",
            "-applaunch",
            appId.ToString(CultureInfo.InvariantCulture));
        return Task.FromResult(started);
    }

    public bool IsSteamClientRunning()
    {
        var processNames = hostSystemInfo.Platform switch
        {
            HostOperatingSystem.MacOS => new[] { "steam_osx", "Steam" },
            HostOperatingSystem.Windows or HostOperatingSystem.Linux => ["steam"],
            _ => []
        };

        return processNames.Any(HasProcess);
    }

    public async Task<bool> TryCloseSteamClientGracefullyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSteamClientRunning())
        {
            return true;
        }

        if (!await TryOpenUriAsync(new Uri("steam://exit"), cancellationToken))
        {
            return false;
        }

        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSteamClientRunning())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return !IsSteamClientRunning();
    }

    public Task<bool> TryInstallSteamGameAsync(
        uint appId,
        int volumeIndex,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (volumeIndex < 0 || IsSteamClientRunning())
        {
            return Task.FromResult(false);
        }

        var appIdArgument = appId.ToString(CultureInfo.InvariantCulture);
        var volumeArgument = volumeIndex.ToString(CultureInfo.InvariantCulture);
        var started = hostSystemInfo.Platform switch
        {
            HostOperatingSystem.Windows when OperatingSystem.IsWindows() => TryStartWindowsSteamCommand(
                "-silent", "+app_install", appIdArgument, volumeArgument),
            HostOperatingSystem.MacOS => TryStartMacSteamCommand(
                "-silent", "+app_install", appIdArgument, volumeArgument),
            HostOperatingSystem.Linux => TryStartLinuxSteamCommand(
                "-silent", "+app_install", appIdArgument, volumeArgument),
            _ => false
        };

        return Task.FromResult(started);
    }

    public Task<bool> TryUninstallSteamGameAsync(
        uint appId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsSteamClientRunning())
        {
            return Task.FromResult(false);
        }

        var appIdArgument = appId.ToString(CultureInfo.InvariantCulture);
        var started = hostSystemInfo.Platform switch
        {
            HostOperatingSystem.Windows when OperatingSystem.IsWindows() => TryStartWindowsSteamCommand(
                "-silent", "+app_uninstall", appIdArgument),
            HostOperatingSystem.MacOS => TryStartMacSteamCommand(
                "-silent", "+app_uninstall", appIdArgument),
            HostOperatingSystem.Linux => TryStartLinuxSteamCommand(
                "-silent", "+app_uninstall", appIdArgument),
            _ => false
        };
        return Task.FromResult(started);
    }

    private static bool HasProcess(string processName)
    {
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

    [SupportedOSPlatform("windows")]
    private static bool TryStartWindowsSteamCommand(params string[] arguments)
    {
        var steamExecutable = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            "SteamExe",
            null) as string;
        return !string.IsNullOrWhiteSpace(steamExecutable) &&
            File.Exists(steamExecutable) &&
            TryStartProcess(steamExecutable, arguments);
    }

    private bool TryStartMacSteamCommand(params string[] arguments)
    {
        var steamBundle = FindMacSteamBundle();
        if (steamBundle is null)
        {
            return false;
        }

        var commandArguments = new List<string> { "-g", "-a", steamBundle, "--args" };
        commandArguments.AddRange(arguments);
        return TryStartProcess("open", [.. commandArguments]);
    }

    private static bool TryStartLinuxSteamCommand(params string[] arguments)
    {
        if (TryStartProcess("steam", arguments))
        {
            return true;
        }

        var flatpakArguments = new List<string> { "run", "com.valvesoftware.Steam" };
        flatpakArguments.AddRange(arguments);
        return TryStartProcess("flatpak", [.. flatpakArguments]);
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

    private bool TryLaunchMacSteamClient()
    {
        var steamBundle = FindMacSteamBundle();
        if (steamBundle is null)
        {
            return false;
        }

        return TryStartProcess("open", "-a", steamBundle);
    }

    private string? FindMacSteamBundle()
    {
        var steamBundles = new[]
        {
            "/Applications/Steam.app",
            Path.Combine(hostSystemInfo.UserProfileDirectory, "Applications", "Steam.app"),
            Path.Combine(
                hostSystemInfo.UserProfileDirectory,
                "Library",
                "Application Support",
                "Steam",
                "Steam.AppBundle",
                "Steam.app")
        };

        return steamBundles.FirstOrDefault(Directory.Exists);
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
