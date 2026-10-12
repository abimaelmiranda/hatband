using Hatband.Core.Enums.Host;
using Hatband.Core.Enums.Games;
using Hatband.Core.Models.Games;
using Hatband.Core.Abstractions.Host;

namespace Hatband.Core.Abstractions.Games;

public interface IGameLaunchRuntime
{
    string RuntimeId { get; }

    HostOperatingSystem HostOperatingSystem { get; }

    HostOperatingSystem TargetOperatingSystem { get; }

    bool RequiresCompatibilityTool { get; }

    bool SupportsPostInstall { get; }

    GameRuntimeProcessWatchMode ProcessWatchMode { get; }

    Task<HostApplicationProcess?> LaunchAsync(GameRuntimeLaunchRequest request, CancellationToken cancellationToken = default);
}
