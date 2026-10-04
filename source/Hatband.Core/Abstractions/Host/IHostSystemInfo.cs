using System.Runtime.InteropServices;
using Hatband.Core.Enums.Host;

namespace Hatband.Core.Abstractions.Host;

public interface IHostSystemInfo
{
    HostOperatingSystem Platform { get; }

    Architecture OperatingSystemArchitecture { get; }

    string UserProfileDirectory { get; }

    string LocalApplicationDataDirectory { get; }
}
