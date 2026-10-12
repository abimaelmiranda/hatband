namespace Hatband.Plugins.Steam.Authentication;

public interface ISteamQrCodeLoginProvider
{
    Task<ISteamQrCodeLoginSession> BeginQrLoginAsync(CancellationToken cancellationToken = default);
}
