namespace Hatband.App.ViewModels.Navigation;

/// <summary>Base for a modal invocation that returns a confirmed value.</summary>
public abstract class ModalViewModel<T> : ModalViewModel
{
    private readonly TaskCompletionSource<ModalCompletion<T>> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Task containing this typed invocation's confirmed or cancelled result.</summary>
    public new Task<ModalCompletion<T>> Completion => _completionSource.Task;

    public override Task CompletionTask => Completion;

    /// <summary>Confirms this modal with its result value.</summary>
    public void Complete(T value)
    {
        var completion = ModalCompletion<T>.Confirmed(value);
        if (!_completionSource.TrySetResult(completion))
        {
            throw new InvalidOperationException("A modal invocation can only be completed once.");
        }

        PublishCompletion(ModalCompletion.Confirmed());
        OnCompleted(completion);
    }

    public override void Complete()
    {
        throw new InvalidOperationException("A typed modal must be completed with a result value.");
    }

    public override void Cancel()
    {
        var completion = ModalCompletion<T>.Cancelled();
        if (!_completionSource.TrySetResult(completion))
        {
            throw new InvalidOperationException("A modal invocation can only be completed once.");
        }

        PublishCompletion(ModalCompletion.Cancelled());
        OnCompleted(completion);
    }

    protected virtual void OnCompleted(ModalCompletion<T> completion)
    {
    }
}
