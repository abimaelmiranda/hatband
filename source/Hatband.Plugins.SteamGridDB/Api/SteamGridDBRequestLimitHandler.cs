namespace Hatband.Plugins.SteamGridDB.Api;

internal sealed class SteamGridDBRequestLimitHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim RequestLimit = new(2, 2);

    public SteamGridDBRequestLimitHandler()
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await RequestLimit.WaitAsync(cancellationToken);
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            RequestLimit.Release();
        }
    }
}
