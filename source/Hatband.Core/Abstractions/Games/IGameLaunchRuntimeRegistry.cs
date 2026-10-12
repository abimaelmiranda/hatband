using Hatband.Core.Enums.Host;

namespace Hatband.Core.Abstractions.Games;

public interface IGameLaunchRuntimeRegistry
{
    bool Supports(HostOperatingSystem hostOperatingSystem, HostOperatingSystem targetOperatingSystem);

    IGameLaunchRuntime Resolve(
        HostOperatingSystem hostOperatingSystem,
        HostOperatingSystem targetOperatingSystem,
        string? runtimeId = null);
}
