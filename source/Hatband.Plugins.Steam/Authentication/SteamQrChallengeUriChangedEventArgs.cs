namespace Hatband.Plugins.Steam.Authentication;

public sealed class SteamQrChallengeUriChangedEventArgs(Uri challengeUri) : EventArgs
{
    public Uri ChallengeUri { get; } = challengeUri;
}
