using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hatband.Core.Enums.Host;

namespace Hatband.Core.Abstractions.Host;

public interface IHostSystemInfo
{
    HostOperatingSystem Platform { get; }

    [SupportedOSPlatformGuard("linux")]
    bool IsLinux { get; }

    Architecture OperatingSystemArchitecture { get; }

    string UserProfileDirectory { get; }

    string LocalApplicationDataDirectory { get; }
}
