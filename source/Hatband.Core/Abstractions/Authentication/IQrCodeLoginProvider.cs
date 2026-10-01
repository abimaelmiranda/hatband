namespace Hatband.Core.Abstractions.Authentication;

/// <summary>
/// Provides QR-code authentication for a store connector.
/// </summary>
public interface IQrCodeLoginProvider : IConnectorAuthenticationCapability
{
    Task<IQrCodeLoginSession> BeginQrLoginAsync(CancellationToken cancellationToken = default);
}
