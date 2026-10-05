using Avalonia.Threading;
using Hatband.Core.Enums.Games;
using Hatband.Core.Models;
using Microsoft.Extensions.Logging;

namespace Hatband.App.Services;

public sealed class GameProcessSessionService(
    IGameProcessMonitor gameProcessMonitor,
    ILogger<GameProcessSessionService> logger)
{
    private readonly Dictionary<Guid, CancellationTokenSource> watchers = [];
    private bool stopped;

    public void Watch(
        Guid gameId,
        GameProcessWatchTarget target,
        Action<GameProcessMonitorEvent> monitorEventHandler)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(monitorEventHandler);

        if (stopped || watchers.ContainsKey(gameId))
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        watchers.Add(gameId, cancellation);
        _ = Task.Run(() => ObserveAsync(gameId, target, monitorEventHandler, cancellation));
    }

    public void Stop()
    {
        stopped = true;
        foreach (var cancellation in watchers.Values)
        {
            cancellation.Cancel();
        }
    }

    private async Task ObserveAsync(
        Guid gameId,
        GameProcessWatchTarget target,
        Action<GameProcessMonitorEvent> monitorEventHandler,
        CancellationTokenSource cancellation)
    {
        try
        {
            await foreach (var monitorEvent in gameProcessMonitor.WatchAsync(target, cancellation.Token))
            {
                await PublishAsync(monitorEventHandler, monitorEvent);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not monitor the process for game {GameId}.", gameId);
            await PublishAsync(monitorEventHandler, GameProcessMonitorEvent.Failed);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (watchers.TryGetValue(gameId, out var currentCancellation) &&
                    ReferenceEquals(currentCancellation, cancellation))
                {
                    watchers.Remove(gameId);
                }

                cancellation.Dispose();
            });
        }
    }

    private async Task PublishAsync(
        Action<GameProcessMonitorEvent> monitorEventHandler,
        GameProcessMonitorEvent monitorEvent)
    {
        await Dispatcher.UIThread.InvokeAsync(() => monitorEventHandler(monitorEvent));
    }
}
