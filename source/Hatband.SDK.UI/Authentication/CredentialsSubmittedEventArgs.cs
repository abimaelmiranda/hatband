namespace Hatband.SDK.UI.Authentication;

public sealed class CredentialsSubmittedEventArgs(string userName, string password) : EventArgs
{
    public string UserName { get; } = userName;

    public string Password { get; } = password;
}
