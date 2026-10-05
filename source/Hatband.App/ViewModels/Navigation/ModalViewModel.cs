using Hatband.App.ViewModels;

namespace Hatband.App.ViewModels.Navigation;

/// <summary>Base for a modal invocation with an explicit confirmed or cancelled completion.</summary>
public abstract class ModalViewModel : ViewModelBase, IModalViewModel
{
    private readonly TaskCompletionSource<ModalCompletion> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _hasBeenInvoked;

    /// <summary>Task completed once this modal invocation is confirmed or cancelled.</summary>
    public Task<ModalCompletion> Completion => _completionSource.Task;

    public virtual Task CompletionTask => Completion;

    /// <summary>True after this modal invocation has produced its one completion.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Keyboard guidance displayed while this modal is topmost.</summary>
    public virtual string KeyboardHelpText => string.Empty;

    public event EventHandler? CompletionChanged;

    /// <summary>Confirms a modal that has no result payload.</summary>
    public virtual void Complete()
    {
        PublishCompletion(ModalCompletion.Confirmed());
    }

    /// <summary>Cancels this modal invocation. Owned descendants are cancelled by the coordinator.</summary>
    public virtual void Cancel()
    {
        PublishCompletion(ModalCompletion.Cancelled());
    }

    internal void BeginInvocation()
    {
        if (_hasBeenInvoked)
        {
            throw new InvalidOperationException("A modal view model instance can only be shown once.");
        }

        if (IsCompleted)
        {
            throw new InvalidOperationException("A completed modal view model cannot be shown.");
        }

        _hasBeenInvoked = true;
    }

    void IModalViewModel.BeginInvocation() => BeginInvocation();

    protected void PublishCompletion(ModalCompletion completion)
    {
        if (!_completionSource.TrySetResult(completion))
        {
            throw new InvalidOperationException("A modal invocation can only be completed once.");
        }

        IsCompleted = true;
        OnPropertyChanged(nameof(IsCompleted));
        CompletionChanged?.Invoke(this, EventArgs.Empty);
    }
}
