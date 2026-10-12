using Avalonia.Controls;

namespace Hatband.SDK.UI.Authentication;

/// <summary>Presents credential fields without persisting credentials or choosing an authentication protocol.</summary>
public sealed class CredentialsFormView : UserControl
{
    private readonly TextBlock userNameLabel = new() { Text = "Username" };
    private readonly TextBlock passwordLabel = new() { Text = "Password" };
    private readonly TextBox userName = new();
    private readonly TextBox password = new() { PasswordChar = '●' };
    private readonly Button submit = new() { Content = "Sign in" };

    public CredentialsFormView()
    {
        Content = new StackPanel
        {
            Spacing = 8,
            Children = { userNameLabel, userName, passwordLabel, password, submit }
        };
        submit.Click += (_, _) => SubmitCredentials();
    }

    public event EventHandler<CredentialsSubmittedEventArgs>? Submitted;

    public string UserNameLabel
    {
        get => userNameLabel.Text ?? throw new InvalidOperationException("The field label must be text.");
        set => userNameLabel.Text = value;
    }

    public string PasswordLabel
    {
        get => passwordLabel.Text ?? throw new InvalidOperationException("The field label must be text.");
        set => passwordLabel.Text = value;
    }

    public string SubmitLabel
    {
        get => submit.Content as string ?? throw new InvalidOperationException("The submit label must be text.");
        set => submit.Content = value;
    }

    public bool FocusInitial() => userName.Focus();

    public void ClearCredentials()
    {
        userName.Clear();
        password.Clear();
    }

    private void SubmitCredentials()
    {
        if (string.IsNullOrWhiteSpace(userName.Text) || string.IsNullOrEmpty(password.Text))
        {
            return;
        }

        var credentials = new CredentialsSubmittedEventArgs(userName.Text, password.Text);
        password.Clear();
        Submitted?.Invoke(this, credentials);
    }
}
