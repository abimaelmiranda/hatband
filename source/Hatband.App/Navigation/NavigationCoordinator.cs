using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.Navigation;

/// <summary>Coordinates screen history, return-to-menu policy, and owned modal chains for the application shell.</summary>
public sealed class NavigationCoordinator : ObservableObject, IScreenNavigation, IModalService
{
    private sealed record ModalEntry(IModalViewModel ViewModel, object Owner);

    private readonly List<ModalEntry> _modalStack = [];
    private readonly ObservableCollection<IModalViewModel> _mutableModalStack = [];
    private readonly Dictionary<ScreenViewModel, bool> _returnToMenuPolicies = [];
    private readonly ReadOnlyObservableCollection<IModalViewModel> _modalStackView;
    private readonly ObservableCollection<ScreenViewModel> _mutableHistory = [];
    private readonly ReadOnlyObservableCollection<ScreenViewModel> _historyView;

    /// <summary>Screen instances retained for back navigation, in presentation order.</summary>
    public ReadOnlyObservableCollection<ScreenViewModel> History => _historyView;

    /// <summary>Modal invocations currently open, with the topmost modal last.</summary>
    public ReadOnlyObservableCollection<IModalViewModel> ModalStack => _modalStackView;

    /// <summary>The active screen, or null until navigation is initialized.</summary>
    public ScreenViewModel? ActiveScreen => History.Count == 0 ? null : History[^1];

    /// <summary>True while any modal prevents screen navigation.</summary>
    public bool HasOpenModals => _modalStack.Count > 0;

    /// <summary>The top modal invocation, or null when the modal stack is empty.</summary>
    public IModalViewModel? ActiveModal => _modalStack.Count == 0 ? null : _modalStack[^1].ViewModel;

    /// <summary>Semantic input guidance supplied by the top modal or active screen.</summary>
    public IReadOnlyList<InputHint> InputHints => ActiveModal?.InputHints ?? ActiveScreen?.InputHints ?? [];

    /// <summary>Raised after a back operation whose screen history entry requests reopening the shell menu.</summary>
    public event EventHandler? ReturnToMenuRequested;

    /// <summary>Creates an empty coordinator. Call <see cref="Initialize"/> before navigation or modal presentation.</summary>
    public NavigationCoordinator()
    {
        _modalStackView = new ReadOnlyObservableCollection<IModalViewModel>(_mutableModalStack);
        _historyView = new ReadOnlyObservableCollection<ScreenViewModel>(_mutableHistory);
        _mutableHistory.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ActiveScreen));
            OnPropertyChanged(nameof(InputHints));
        };
    }

    /// <summary>Installs and activates the root screen exactly once.</summary>
    public void Initialize(ScreenViewModel initialScreen)
    {
        ArgumentNullException.ThrowIfNull(initialScreen);
        if (History.Count != 0)
        {
            throw new InvalidOperationException("Navigation has already been initialized.");
        }

        _returnToMenuPolicies.Add(initialScreen, false);
        _mutableHistory.Add(initialScreen);
        initialScreen.Activate();
    }

    /// <summary>Pushes a new screen or resumes an existing entry after discarding entries above it.</summary>
    public void Navigate(ScreenViewModel screen, bool returnToMenuOnBack = false)
    {
        ArgumentNullException.ThrowIfNull(screen);
        EnsureInitialized();
        EnsureNoOpenModals();

        if (ReferenceEquals(ActiveScreen, screen))
        {
            _returnToMenuPolicies[screen] = returnToMenuOnBack;
            return;
        }

        var existingIndex = History.IndexOf(screen);
        if (existingIndex >= 0)
        {
            if (existingIndex == _mutableHistory.Count - 1)
            {
                _returnToMenuPolicies[screen] = returnToMenuOnBack;
                return;
            }

            ActiveScreen?.Deactivate();
            for (var index = _mutableHistory.Count - 1; index > existingIndex; index--)
            {
                var discardedScreen = _mutableHistory[index];
                _mutableHistory.RemoveAt(index);
                _returnToMenuPolicies.Remove(discardedScreen);
                discardedScreen.Discard();
            }

            _returnToMenuPolicies[screen] = returnToMenuOnBack;
            screen.Activate();
            return;
        }

        ActiveScreen?.Deactivate();
        _returnToMenuPolicies.Add(screen, returnToMenuOnBack);
        _mutableHistory.Add(screen);
        screen.Activate();
    }

    /// <summary>Discards all history and installs one active root screen; open modal chains must be dismissed first.</summary>
    public void Reset(ScreenViewModel screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        EnsureNoOpenModals();

        foreach (var historyScreen in History.ToArray())
        {
            historyScreen.Discard();
        }

        _mutableHistory.Clear();
        _returnToMenuPolicies.Clear();
        _returnToMenuPolicies.Add(screen, false);
        _mutableHistory.Add(screen);
        screen.Activate();
    }

    /// <summary>Pops the current screen and raises the menu event when both the argument and its stored policy allow it.</summary>
    public bool GoBack(bool reopenMenu = true)
    {
        EnsureInitialized();
        EnsureNoOpenModals();
        if (History.Count < 2)
        {
            return false;
        }

        var currentScreen = ActiveScreen ?? throw new InvalidOperationException("Navigation has no active screen.");
        var shouldReturnToMenu = reopenMenu && _returnToMenuPolicies.GetValueOrDefault(currentScreen);
        currentScreen.Deactivate();
        _mutableHistory.RemoveAt(_mutableHistory.Count - 1);
        _returnToMenuPolicies.Remove(currentScreen);
        currentScreen.Discard();
        (ActiveScreen ?? throw new InvalidOperationException("Navigation history is empty after going back.")).Activate();

        if (shouldReturnToMenu)
        {
            ReturnToMenuRequested?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    /// <summary>Shows a typed modal owned by the active modal or screen and returns its one completion.</summary>
    public Task<ModalCompletion<T>> ShowAsync<T>(ModalViewModel<T> modal, object? owner = null)
    {
        ArgumentNullException.ThrowIfNull(modal);
        var resolvedOwner = ValidateModalOwner(owner);
        modal.BeginInvocation();
        AddModal(modal, resolvedOwner);
        return modal.Completion;
    }

    /// <summary>Shows a modal without a result payload.</summary>
    public Task<ModalCompletion> ShowAsync(ModalViewModel modal, object? owner = null)
    {
        ArgumentNullException.ThrowIfNull(modal);
        var resolvedOwner = ValidateModalOwner(owner);
        modal.BeginInvocation();
        AddModal(modal, resolvedOwner);
        return modal.Completion;
    }

    /// <summary>Cancels the top modal and its owned descendants, returning false if no modal is open.</summary>
    public bool DismissTopModal()
    {
        if (ActiveModal is null)
        {
            return false;
        }

        ActiveModal.Cancel();
        return true;
    }

    /// <summary>Cancels the modal chain from top to root, allowing an explicit screen reset afterward.</summary>
    public void DismissModalChain()
    {
        while (ActiveModal is { } activeModal)
        {
            activeModal.Cancel();
        }
    }

    private object ValidateModalOwner(object? owner)
    {
        EnsureInitialized();

        object resolvedOwner = owner ?? (object?)ActiveModal ?? ActiveScreen
            ?? throw new InvalidOperationException("A modal requires an active screen owner.");
        if (resolvedOwner is not ScreenViewModel && resolvedOwner is not IModalViewModel)
        {
            throw new ArgumentException("A modal owner must be an active screen or modal view model.", nameof(owner));
        }

        if (resolvedOwner is ScreenViewModel screenOwner)
        {
            if (!ReferenceEquals(screenOwner, ActiveScreen) || HasOpenModals)
            {
                throw new InvalidOperationException("A screen can own a modal only when no other modal is open.");
            }
        }
        else if (!ReferenceEquals(resolvedOwner, ActiveModal))
        {
            throw new InvalidOperationException("A modal can only be owned by the currently active modal.");
        }

        return resolvedOwner;
    }

    private void AddModal(IModalViewModel modal, object owner)
    {
        modal.CompletionChanged += OnModalCompletionChanged;
        _modalStack.Add(new ModalEntry(modal, owner));
        _mutableModalStack.Add(modal);
        OnPropertyChanged(nameof(ActiveModal));
        OnPropertyChanged(nameof(HasOpenModals));
        OnPropertyChanged(nameof(InputHints));
    }

    private void OnModalCompletionChanged(object? sender, EventArgs e)
    {
        if (sender is not IModalViewModel completedModal)
        {
            return;
        }

        var modalIndex = _modalStack.FindIndex(entry => ReferenceEquals(entry.ViewModel, completedModal));
        if (modalIndex < 0)
        {
            return;
        }

        var removedEntries = _modalStack.Skip(modalIndex).Reverse().ToArray();
        _modalStack.RemoveRange(modalIndex, _modalStack.Count - modalIndex);
        _mutableModalStack.RemoveAt(modalIndex);

        foreach (var entry in removedEntries)
        {
            if (!ReferenceEquals(entry.ViewModel, completedModal))
            {
                _mutableModalStack.Remove(entry.ViewModel);
            }

            entry.ViewModel.CompletionChanged -= OnModalCompletionChanged;
            if (!entry.ViewModel.IsCompleted)
            {
                entry.ViewModel.Cancel();
            }
        }

        OnPropertyChanged(nameof(ActiveModal));
        OnPropertyChanged(nameof(HasOpenModals));
        OnPropertyChanged(nameof(InputHints));
    }

    private void EnsureInitialized()
    {
        if (ActiveScreen is null)
        {
            throw new InvalidOperationException("Initialize navigation with a root screen before navigating.");
        }
    }

    private void EnsureNoOpenModals()
    {
        if (HasOpenModals)
        {
            throw new InvalidOperationException("Screen navigation cannot change while a modal is open. Dismiss the modal chain first.");
        }
    }
}
