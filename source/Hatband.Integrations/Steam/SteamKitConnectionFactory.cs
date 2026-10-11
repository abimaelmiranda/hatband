using System.Collections.Concurrent;
using System.Diagnostics;
using Hatband.Core.Abstractions.Host;
using Microsoft.Extensions.Logging;
using SteamKit2;
using SteamKit2.Discovery;

namespace Hatband.Integrations.Steam;

public sealed class SteamKitConnectionFactory
{
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];
    private static readonly ProtocolTypes[] LinuxProtocols =
    [
        ProtocolTypes.WebSocket,
        ProtocolTypes.WebSocket,
        ProtocolTypes.Tcp
    ];

    private readonly IHostSystemInfo hostSystemInfo;
    private readonly ILogger<SteamKitConnectionFactory> logger;

    public SteamKitConnectionFactory(
        IHostSystemInfo hostSystemInfo,
        ILogger<SteamKitConnectionFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(logger);
        this.hostSystemInfo = hostSystemInfo;
        this.logger = logger;
    }

    internal async Task<SteamKitConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var configuration = SteamConfiguration.Create(builder => builder
            .WithConnectionTimeout(AttemptTimeout)
            .WithProtocolTypes(ProtocolTypes.All));
        var protocols = hostSystemInfo.IsLinux
            ? LinuxProtocols
            : [ProtocolTypes.All, ProtocolTypes.All, ProtocolTypes.All];
        Exception? lastFailure = null;

        for (var attempt = 0; attempt < protocols.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attempt > 0)
            {
                await Task.Delay(RetryDelays[attempt - 1], cancellationToken);
            }

            var protocol = protocols[attempt];
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "SteamKit server selection";
            SteamKitConnection? connection = null;
            Action<string, string>? debugListener = null;
            var transportDiagnostics = new ConcurrentQueue<string>();
            using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptTimeout.CancelAfter(AttemptTimeout);
            try
            {
                var client = new SteamClient(configuration);
                var callbackManager = new CallbackManager(client);
                var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                callbackManager.Subscribe<SteamClient.ConnectedCallback>(_ => connected.TrySetResult());
                connection = new SteamKitConnection(client, callbackManager);
                debugListener = (category, message) =>
                {
                    if (IsTransportCategory(category))
                    {
                        transportDiagnostics.Enqueue($"[{category}] {message}");
                        while (transportDiagnostics.Count > 20)
                        {
                            transportDiagnostics.TryDequeue(out _);
                        }
                    }
                };
                DebugLog.Enabled = true;
                DebugLog.AddListener(debugListener);

                if (hostSystemInfo.IsLinux)
                {
                    var server = await client.Servers.GetNextServerCandidateAsync(protocol)
                        .WaitAsync(attemptTimeout.Token);
                    if (server is null)
                    {
                        throw new InvalidOperationException($"Steam returned no server supporting {protocol}.");
                    }

                    endpoint = server.EndPoint.ToString();
                    var serverRecord = ServerRecord.CreateServer(
                        server.GetHost().ToString(),
                        server.GetPort(),
                        protocol);
                    client.Connect(serverRecord);
                }
                else
                {
                    client.Connect();
                }

                var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, attemptTimeout.Token);
                var completedTask = await Task.WhenAny(connected.Task, connection.Disconnected.Task, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    await timeoutTask;
                }

                if (completedTask == connection.Disconnected.Task)
                {
                    var disconnection = await connection.Disconnected.Task;
                    throw new InvalidOperationException(
                        $"Steam disconnected during connection setup (user initiated: {disconnection.UserInitiated}).");
                }

                await connected.Task.WaitAsync(attemptTimeout.Token);
                stopwatch.Stop();
                logger.LogInformation(
                    "Connected to Steam using {Protocol} at {Endpoint} on attempt {Attempt} in {DurationMs} ms.",
                    protocol,
                    endpoint,
                    attempt + 1,
                    stopwatch.ElapsedMilliseconds);
                return connection;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (connection is not null)
                {
                    await connection.DisposeAsync();
                }

                throw;
            }
            catch (Exception exception)
            {
                lastFailure = exception;
                stopwatch.Stop();
                logger.LogWarning(
                    exception,
                    "Steam connection attempt {Attempt}/{AttemptCount} using {Protocol} at {Endpoint} failed after {DurationMs} ms.",
                    attempt + 1,
                    protocols.Length,
                    protocol,
                    endpoint,
                    stopwatch.ElapsedMilliseconds);
                foreach (var diagnostic in transportDiagnostics)
                {
                    logger.LogWarning("SteamKit transport diagnostic: {Diagnostic}", diagnostic);
                }

                if (connection is not null)
                {
                    await connection.DisposeAsync();
                }
            }
            finally
            {
                if (debugListener is not null)
                {
                    DebugLog.RemoveListener(debugListener);
                }
            }
        }

        throw new InvalidOperationException("Could not connect to Steam after the configured attempts.", lastFailure);
    }

    private static bool IsTransportCategory(string category) =>
        category.Contains("CMClient", StringComparison.OrdinalIgnoreCase)
        || category.Contains("TcpConnection", StringComparison.OrdinalIgnoreCase)
        || category.Contains("WebSocketConnection", StringComparison.OrdinalIgnoreCase)
        || category.Contains("WebSocketContext", StringComparison.OrdinalIgnoreCase);
}
