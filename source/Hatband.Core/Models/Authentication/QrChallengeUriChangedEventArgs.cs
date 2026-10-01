namespace Hatband.Core.Models.Authentication;

/// <summary>
/// Describes a rotated QR login challenge.
/// </summary>
public sealed class QrChallengeUriChangedEventArgs : EventArgs
{
    public QrChallengeUriChangedEventArgs(Uri challengeUri)
    {
        ArgumentNullException.ThrowIfNull(challengeUri);
        ChallengeUri = challengeUri;
    }

    public Uri ChallengeUri { get; }
}
