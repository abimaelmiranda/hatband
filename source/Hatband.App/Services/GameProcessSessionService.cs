using Avalonia.Threading;
using Hatband.Core.Enums.Games;
using Hatband.Core.Models;
using Microsoft.Extensions.Logging;

namespace Hatband.App.Services;

public sealed class GameProcessSessionService
{
    private readonly IGameProcessMonitor _gameProcessMonitor;
    private readonly ILogger<GameProcessSessionService> _logger;
    private readonly Dictionary<Guid, CancellationTokenSource> _watchers = [];
    private bool _stopped;

    public GameProcessSessionService(
        IGameProcessMonitor gameProcessMonitor,
        ILogger<GameProcessSessionService> logger)
    {
        _gameProcessMonitor = gameProcessMonitor;
        _logger = logger;
    }

    public void Watch(
        Guid gameId,
        GameProcessWatchTarget target,
        Action<GameProcessMonitorEvent> monitorEventHandler)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(monitorEventHandler);

        if (_stopped || _watchers.ContainsKey(gameId))
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _watchers.Add(gameId, cancellation);
        _ = Task.Run(() => ObserveAsync(gameId, target, monitorEventHandler, cancellation));
    }

    public void Stop()
    {
        _stopped = true;
        foreach (var cancellation in _watchers.Values)
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
            await foreach (var monitorEvent in _gameProcessMonitor.WatchAsync(target, cancellation.Token))
            {
                await PublishAsync(monitorEventHandler, monitorEvent);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not monitor the process for game {GameId}.", gameId);
            await PublishAsync(monitorEventHandler, GameProcessMonitorEvent.Failed);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_watchers.TryGetValue(gameId, out var currentCancellation) &&
                    ReferenceEquals(currentCancellation, cancellation))
                {
                    _watchers.Remove(gameId);
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
