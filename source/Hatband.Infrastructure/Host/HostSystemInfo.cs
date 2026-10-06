using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hatband.Core.Enums.Host;

namespace Hatband.Infrastructure.Host;

public sealed class HostSystemInfo : IHostSystemInfo
{
    private readonly HostOperatingSystem _platform;
    private readonly Architecture _operatingSystemArchitecture;

    public HostSystemInfo()
    {
        _platform = DetectPlatform();
        _operatingSystemArchitecture = RuntimeInformation.OSArchitecture;
    }

    public HostOperatingSystem Platform => _platform;

    [SupportedOSPlatformGuard("linux")]
    public bool IsLinux => _platform == HostOperatingSystem.Linux;

    public Architecture OperatingSystemArchitecture => _operatingSystemArchitecture;

    public string UserProfileDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string LocalApplicationDataDirectory => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static HostOperatingSystem DetectPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return HostOperatingSystem.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return HostOperatingSystem.MacOS;
        }

        if (OperatingSystem.IsLinux())
        {
            return HostOperatingSystem.Linux;
        }

        return HostOperatingSystem.Unknown;
    }
}
