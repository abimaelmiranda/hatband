using System;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums;

namespace Hatband.Infrastructure.Host;

public sealed class HostSystemInfo : IHostSystemInfo
{
    private readonly HostOperatingSystem _platform;

    public HostSystemInfo()
    {
        _platform = DetectPlatform();
    }

    public HostOperatingSystem Platform => _platform;

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
