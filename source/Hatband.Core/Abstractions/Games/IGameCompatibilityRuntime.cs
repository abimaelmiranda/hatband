using Hatband.Core.Models.Games;
using Hatband.Core.Abstractions.Host;

namespace Hatband.Core.Abstractions.Games;

public interface IGameCompatibilityRuntime
{
    Task<HostApplicationProcess?> LaunchAsync(GameRuntimeLaunchRequest request, CancellationToken cancellationToken = default);
}
