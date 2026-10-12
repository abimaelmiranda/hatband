namespace Hatband.SDK.UI.Authentication;

public sealed class AuthorizationCodeSubmittedEventArgs(string code) : EventArgs
{
    public string Code { get; } = code;
}
