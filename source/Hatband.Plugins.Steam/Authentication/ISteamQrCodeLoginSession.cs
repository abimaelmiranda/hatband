using Hatband.Core.Models.Authentication;

namespace Hatband.Plugins.Steam.Authentication;

public interface ISteamQrCodeLoginSession : IAsyncDisposable
{
    Uri ChallengeUri { get; }

    event EventHandler<SteamQrChallengeUriChangedEventArgs>? ChallengeUriChanged;

    Task<ConnectorAccount> WaitForAuthenticationAsync(CancellationToken cancellationToken = default);
}
