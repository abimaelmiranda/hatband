using Avalonia.Controls;

namespace Hatband.SDK.UI.Authentication;

/// <summary>Common browser authorization and code-entry UI. The plugin owns the URL and token exchange.</summary>
public sealed class BrowserAuthorizationView : UserControl
{
    private readonly Button openBrowser = new() { Content = "Open sign-in page" };
    private readonly TextBlock codeLabel = new() { Text = "Authorization code" };
    private readonly TextBox code = new();
    private readonly Button submit = new() { Content = "Continue" };

    public BrowserAuthorizationView()
    {
        Content = new StackPanel
        {
            Spacing = 8,
            Children = { openBrowser, codeLabel, code, submit }
        };
        openBrowser.Click += (_, _) => OpenBrowserRequested?.Invoke(this, EventArgs.Empty);
        submit.Click += (_, _) => SubmitCode();
    }

    public event EventHandler? OpenBrowserRequested;

    public event EventHandler<AuthorizationCodeSubmittedEventArgs>? Submitted;

    public string OpenBrowserLabel
    {
        get => openBrowser.Content as string ?? throw new InvalidOperationException("The browser button label must be text.");
        set => openBrowser.Content = value;
    }

    public string CodeLabel
    {
        get => codeLabel.Text ?? throw new InvalidOperationException("The field label must be text.");
        set => codeLabel.Text = value;
    }

    public string SubmitLabel
    {
        get => submit.Content as string ?? throw new InvalidOperationException("The submit label must be text.");
        set => submit.Content = value;
    }

    public bool FocusInitial() => openBrowser.Focus();

    public void ClearCode() => code.Clear();

    private void SubmitCode()
    {
        if (string.IsNullOrWhiteSpace(code.Text))
        {
            return;
        }

        var authorization = new AuthorizationCodeSubmittedEventArgs(code.Text.Trim());
        code.Clear();
        Submitted?.Invoke(this, authorization);
    }
}
