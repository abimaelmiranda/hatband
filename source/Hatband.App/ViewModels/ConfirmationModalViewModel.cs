using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.ViewModels;

/// <summary>
/// Describes a destructive-action confirmation; cancellation is distinct from an affirmative result.
/// </summary>
public sealed class ConfirmationModalViewModel : ModalViewModel<bool>
{
    public ConfirmationModalViewModel(string title, string subject, string message, string confirmLabel)
    {
        Title = title;
        Subject = subject;
        Message = message;
        ConfirmLabel = confirmLabel;
    }

    public string Title { get; }
    public string Subject { get; }
    public string Message { get; }
    public string ConfirmLabel { get; }
}
