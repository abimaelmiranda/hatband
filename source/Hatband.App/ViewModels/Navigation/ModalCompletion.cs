namespace Hatband.App.ViewModels.Navigation;

/// <summary>The final result of one modal invocation.</summary>
public enum ModalOutcome
{
    Confirmed,
    Cancelled
}

/// <summary>A typed modal result whose payload is available only after confirmation.</summary>
public readonly record struct ModalCompletion<T>
{
    private sealed record ValueBox(T Value);

    private readonly ValueBox? _valueBox;

    private ModalCompletion(ModalOutcome outcome, ValueBox? valueBox)
    {
        Outcome = outcome;
        _valueBox = valueBox;
    }

    /// <summary>The outcome that determines whether a confirmed value exists.</summary>
    public ModalOutcome Outcome { get; }

    /// <summary>Returns the confirmed payload, or throws when the modal was cancelled.</summary>
    public T GetConfirmedValue()
    {
        if (Outcome != ModalOutcome.Confirmed)
        {
            throw new InvalidOperationException("A cancelled modal has no confirmed value.");
        }

        if (_valueBox is null)
        {
            throw new InvalidOperationException("A confirmed modal completion has no value.");
        }

        return _valueBox.Value;
    }

    /// <summary>Creates a confirmed result, including when the supplied value is a valid default value.</summary>
    public static ModalCompletion<T> Confirmed(T value) => new(ModalOutcome.Confirmed, new ValueBox(value));

    /// <summary>Creates a cancelled result with no payload.</summary>
    public static ModalCompletion<T> Cancelled() => new(ModalOutcome.Cancelled, null);
}

/// <summary>A modal result that carries no value.</summary>
public readonly record struct ModalCompletion(ModalOutcome Outcome)
{
    public static ModalCompletion Confirmed() => new(ModalOutcome.Confirmed);

    public static ModalCompletion Cancelled() => new(ModalOutcome.Cancelled);
}
