using SteamKit2;

namespace Hatband.Plugins.Steam.Authentication;

internal sealed class SteamKitConnection : IAsyncDisposable
{
    private readonly CancellationTokenSource callbackPumpCancellation;
    private readonly Task callbackPump;
    private bool disposed;

    public SteamKitConnection(SteamClient client, CallbackManager callbackManager)
    {
        Client = client;
        CallbackManager = callbackManager;
        Disconnected = new TaskCompletionSource<SteamClient.DisconnectedCallback>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        callbackManager.Subscribe<SteamClient.DisconnectedCallback>(callback => Disconnected.TrySetResult(callback));
        callbackPumpCancellation = new CancellationTokenSource();
        callbackPump = Task.Run(() => RunCallbackPump(callbackManager, callbackPumpCancellation.Token));
    }

    public SteamClient Client { get; }

    public CallbackManager CallbackManager { get; }

    public TaskCompletionSource<SteamClient.DisconnectedCallback> Disconnected { get; }

    internal static void RunCallbackPump(CallbackManager callbackManager, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            callbackManager.RunWaitCallbacks(TimeSpan.FromMilliseconds(250));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        callbackPumpCancellation.Cancel();
        Client.Disconnect();
        await callbackPump;
        callbackPumpCancellation.Dispose();
    }
}
