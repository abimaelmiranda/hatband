using Hatband.Core.Enums;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

public interface IGameProcessMonitor
{
    IAsyncEnumerable<GameProcessMonitorEvent> WatchAsync(
        GameProcessWatchTarget target,
        CancellationToken cancellationToken = default);
}
