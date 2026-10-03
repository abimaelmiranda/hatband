using System.Runtime.InteropServices;
using Hatband.Core.Enums;

namespace Hatband.Core.Abstractions;

public interface IHostSystemInfo
{
    HostOperatingSystem Platform { get; }

    Architecture OperatingSystemArchitecture { get; }

    string UserProfileDirectory { get; }

    string LocalApplicationDataDirectory { get; }
}
