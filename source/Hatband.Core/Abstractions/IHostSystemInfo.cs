using Hatband.Core.Enums;

namespace Hatband.Core.Abstractions;

public interface IHostSystemInfo
{
    HostOperatingSystem Platform { get; }

    string UserProfileDirectory { get; }

    string LocalApplicationDataDirectory { get; }
}
