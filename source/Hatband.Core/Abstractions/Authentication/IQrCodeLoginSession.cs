using Hatband.Core.Models.Authentication;

namespace Hatband.Core.Abstractions.Authentication;

/// <summary>
/// Represents a QR-code login attempt with a store connector.
/// </summary>
public interface IQrCodeLoginSession : IAsyncDisposable
{
    Uri ChallengeUri { get; }

    event EventHandler<QrChallengeUriChangedEventArgs>? ChallengeUriChanged;

    Task<ConnectorAccount> WaitForAuthenticationAsync(CancellationToken cancellationToken = default);
}
