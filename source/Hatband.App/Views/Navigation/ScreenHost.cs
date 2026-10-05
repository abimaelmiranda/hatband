using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App;
using Hatband.App.Navigation;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.Views.Navigation;

/// <summary>Mounts only the active screen view while retaining views for screens still in history.</summary>
public sealed class ScreenHost : UserControl
{
    public static readonly StyledProperty<NavigationCoordinator?> CoordinatorProperty =
        AvaloniaProperty.Register<ScreenHost, NavigationCoordinator?>(nameof(Coordinator));

    public static readonly StyledProperty<IReadOnlyList<IDataTemplate>?> ViewTemplatesProperty =
        AvaloniaProperty.Register<ScreenHost, IReadOnlyList<IDataTemplate>?>(nameof(ViewTemplates));

    private readonly ContentControl _contentPresenter = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };
    private readonly Dictionary<ScreenViewModel, Control> _viewCache = [];
    private ScreenViewModel? _subscribedScreen;
    private NavigationCoordinator? _subscribedCoordinator;
    private INotifyCollectionChanged? _subscribedHistory;
    private INotifyCollectionChanged? _subscribedModalStack;
    private INavigationView? _activeView;
    private Control? _focusedScreenControl;
    private bool _isAttached;
    private bool _isActiveViewActivated;

    /// <summary>Creates an empty host that resolves explicit application data templates when a screen is activated.</summary>
    public ScreenHost()
    {
        Content = _contentPresenter;
        PropertyChanged += OnHostPropertyChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    /// <summary>Coordinator whose active screen is presented.</summary>
    public NavigationCoordinator? Coordinator
    {
        get => GetValue(CoordinatorProperty);
        set => SetValue(CoordinatorProperty, value);
    }

    /// <summary>Optional explicit view templates; when omitted, application data templates are used.</summary>
    public IReadOnlyList<IDataTemplate>? ViewTemplates
    {
        get => GetValue(ViewTemplatesProperty);
        set => SetValue(ViewTemplatesProperty, value);
    }

    /// <summary>The mounted screen view used by the shell for input routing and focus.</summary>
    public INavigationView? ActiveView
    {
        get => _activeView;
        private set
        {
            if (ReferenceEquals(_activeView, value))
            {
                return;
            }

            _activeView = value;
        }
    }

    private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CoordinatorProperty)
        {
            SubscribeToNavigation();
            UpdateActiveView();
        }
        else if (e.Property == ViewTemplatesProperty)
        {
            _viewCache.Clear();
            UpdateActiveView();
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _isAttached = true;
        SubscribeToNavigation();
        UpdateActiveView();
        if (!_isActiveViewActivated && ActiveView is FullScreenView activeScreenView)
        {
            activeScreenView.ActivateView();
            _isActiveViewActivated = true;
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isActiveViewActivated && ActiveView is FullScreenView activeScreenView)
        {
            activeScreenView.DeactivateView();
            _isActiveViewActivated = false;
        }

        _isAttached = false;
        SubscribeToNavigation();
    }

    private void SubscribeToNavigation()
    {
        if (_subscribedCoordinator is not null)
        {
            _subscribedCoordinator.PropertyChanged -= OnNavigationPropertyChanged;
        }

        if (_subscribedHistory is not null)
        {
            _subscribedHistory.CollectionChanged -= OnHistoryChanged;
        }

        if (_subscribedModalStack is not null)
        {
            _subscribedModalStack.CollectionChanged -= OnModalStackChanged;
        }

        _subscribedScreen = null;
        _subscribedHistory = null;
        _subscribedModalStack = null;
        _subscribedCoordinator = null;
        if (!_isAttached || Coordinator is null)
        {
            return;
        }

        _subscribedCoordinator = Coordinator;
        _subscribedCoordinator.PropertyChanged += OnNavigationPropertyChanged;
        _subscribedHistory = _subscribedCoordinator.History;
        _subscribedHistory.CollectionChanged += OnHistoryChanged;
        _subscribedModalStack = (INotifyCollectionChanged)_subscribedCoordinator.ModalStack;
        _subscribedModalStack.CollectionChanged += OnModalStackChanged;
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IScreenNavigation.ActiveScreen) or nameof(IScreenNavigation.History) or nameof(NavigationCoordinator.HasOpenModals))
        {
            UpdateActiveView();
        }
    }

    private void OnHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PruneViewCache();
        UpdateActiveView();
    }

    private void OnModalStackChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Coordinator is { HasOpenModals: true } && _focusedScreenControl is null)
        {
            CaptureFocusedScreenControl();
        }
    }

    private void UpdateActiveView()
    {
        var screen = Coordinator?.ActiveScreen;
        if (screen is not null &&
            ReferenceEquals(screen, _subscribedScreen) &&
            _viewCache.TryGetValue(screen, out var cachedView) &&
            ReferenceEquals(ActiveView, cachedView))
        {
            UpdateEnabledState();
            return;
        }

        if (_isActiveViewActivated && ActiveView is FullScreenView previousScreenView)
        {
            previousScreenView.DeactivateView();
            _isActiveViewActivated = false;
        }

        _subscribedScreen = screen;
        var view = screen is null ? null : GetOrCreateView(screen);
        _contentPresenter.Content = view;
        ActiveView = view as INavigationView;
        if (view is not null && ActiveView is null)
        {
            throw new InvalidOperationException($"The view for {screen?.GetType().Name ?? "the active screen"} must implement {nameof(INavigationView)}.");
        }

        if (_isAttached && view is FullScreenView screenView)
        {
            screenView.ActivateView();
            _isActiveViewActivated = true;
        }

        UpdateEnabledState();
    }

    private Control GetOrCreateView(ScreenViewModel screen)
    {
        if (_viewCache.TryGetValue(screen, out var existingView))
        {
            return existingView;
        }

        var template = GetTemplates().FirstOrDefault(candidate => candidate is not ViewLocator && candidate.Match(screen));
        if (template is null)
        {
            throw new InvalidOperationException($"No explicit data template is registered for {screen.GetType().Name}.");
        }

        var view = template.Build(screen)
            ?? throw new InvalidOperationException($"The data template for {screen.GetType().Name} returned no view.");
        view.DataContext = screen;
        _viewCache.Add(screen, view);
        return view;
    }

    private IEnumerable<IDataTemplate> GetTemplates()
    {
        if (ViewTemplates is not null)
        {
            return ViewTemplates;
        }

        return Application.Current?.DataTemplates
            ?? throw new InvalidOperationException("ScreenHost requires explicit view templates.");
    }

    private void PruneViewCache()
    {
        if (Coordinator is null)
        {
            _viewCache.Clear();
            return;
        }

        var currentHistory = Coordinator.History.ToHashSet();
        foreach (var screen in _viewCache.Keys.Where(screen => !currentHistory.Contains(screen)).ToArray())
        {
            if (!ReferenceEquals(_viewCache[screen], ActiveView) && _viewCache[screen] is FullScreenView screenView)
            {
                screenView.DeactivateView();
            }

            _viewCache.Remove(screen);
        }
    }

    private void UpdateEnabledState()
    {
        var wasEnabled = IsEnabled;
        var shouldBeEnabled = Coordinator is { ActiveScreen: not null, HasOpenModals: false };
        if (IsEnabled && !shouldBeEnabled)
        {
            CaptureFocusedScreenControl();
        }

        IsEnabled = shouldBeEnabled;
        if (!shouldBeEnabled || wasEnabled)
        {
            return;
        }

        var controlToRestore = _focusedScreenControl;
        _focusedScreenControl = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (Coordinator is not { HasOpenModals: false })
            {
                return;
            }

            if (controlToRestore is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Focusable: true })
            {
                controlToRestore.Focus(NavigationMethod.Directional);
                return;
            }

            ActiveView?.FocusInitial();
        });
    }

    private void CaptureFocusedScreenControl()
    {
        _focusedScreenControl = ActiveView?.NavigationRoot.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.IsFocused);
    }
}
