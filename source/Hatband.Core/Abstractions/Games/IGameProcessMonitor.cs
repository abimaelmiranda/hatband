using Hatband.Core.Enums.Games;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions.Games;

public interface IGameProcessMonitor
{
    IAsyncEnumerable<GameProcessMonitorEvent> WatchAsync(
        GameProcessWatchTarget target,
        CancellationToken cancellationToken = default);
}
