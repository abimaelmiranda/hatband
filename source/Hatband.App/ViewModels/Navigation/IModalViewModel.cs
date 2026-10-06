namespace Hatband.App.ViewModels.Navigation;

/// <summary>Common state consumed by the modal stack and modal view host.</summary>
public interface IModalViewModel
{
    bool IsCompleted { get; }

    IReadOnlyList<InputHint> InputHints { get; }

    Task CompletionTask { get; }

    event EventHandler? CompletionChanged;

    void Cancel();

    internal void BeginInvocation();
}
