using System;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums;

namespace Hatband.Infrastructure.Host;

public sealed class HostSystemInfo : IHostSystemInfo
{
    public HostOperatingSystem Platform
    {
        get
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

    public string UserProfileDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string LocalApplicationDataDirectory => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
}
