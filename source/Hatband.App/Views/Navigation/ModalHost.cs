using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Hatband.App;
using Hatband.App.Navigation;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.Views.Navigation;

/// <summary>Displays a mounted layer for each modal owner and routes input to the top layer only.</summary>
public sealed class ModalHost : UserControl
{
    public static readonly StyledProperty<NavigationCoordinator?> CoordinatorProperty =
        AvaloniaProperty.Register<ModalHost, NavigationCoordinator?>(nameof(Coordinator));

    private readonly Grid _layerRoot = new();
    private readonly Border _backdrop = new()
    {
        Background = new SolidColorBrush(Color.FromArgb(190, 8, 12, 17))
    };
    private readonly Dictionary<IModalViewModel, (Border Layer, Control View)> _viewCache = [];
    private INavigationView? _activeView;
    private NavigationCoordinator? _subscribedCoordinator;
    private bool _isAttached;
    private bool _isActiveViewActivated;

    /// <summary>Creates a hidden host ready to bind to the application's navigation coordinator.</summary>
    public ModalHost()
    {
        _layerRoot.Children.Add(_backdrop);
        Content = _layerRoot;
        IsVisible = false;
        PropertyChanged += OnHostPropertyChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    /// <summary>Coordinator whose current modal owner chain is presented by this host.</summary>
    public NavigationCoordinator? Coordinator
    {
        get => GetValue(CoordinatorProperty);
        set => SetValue(CoordinatorProperty, value);
    }

    /// <summary>The topmost view that receives modal navigation input.</summary>
    public INavigationView? ActiveView
    {
        get => _activeView;
        private set => _activeView = value;
    }

    private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != CoordinatorProperty)
        {
            return;
        }

        UpdateCoordinatorSubscriptions();
        UpdateActiveView();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _isAttached = true;
        UpdateCoordinatorSubscriptions();
        UpdateActiveView();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isActiveViewActivated && _activeView is ModalView activeModalView)
        {
            activeModalView.DeactivateView();
            _isActiveViewActivated = false;
        }

        _isAttached = false;
        UpdateCoordinatorSubscriptions();
    }

    private void UpdateCoordinatorSubscriptions()
    {
        if (_subscribedCoordinator is not null)
        {
            _subscribedCoordinator.PropertyChanged -= OnCoordinatorPropertyChanged;
            ((INotifyCollectionChanged)_subscribedCoordinator.ModalStack).CollectionChanged -= OnModalStackChanged;
        }

        _subscribedCoordinator = null;
        if (!_isAttached || Coordinator is null)
        {
            return;
        }

        _subscribedCoordinator = Coordinator;
        _subscribedCoordinator.PropertyChanged += OnCoordinatorPropertyChanged;
        ((INotifyCollectionChanged)_subscribedCoordinator.ModalStack).CollectionChanged += OnModalStackChanged;
    }

    private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NavigationCoordinator.ActiveModal) or nameof(NavigationCoordinator.HasOpenModals))
        {
            UpdateActiveView();
        }
    }

    private void OnModalStackChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PruneViewCache();
        UpdateActiveView();
    }

    private void UpdateActiveView()
    {
        var modalStack = Coordinator?.ModalStack;
        if (modalStack is null || modalStack.Count == 0)
        {
            if (_isActiveViewActivated && _activeView is ModalView previousModalView)
            {
                previousModalView.DeactivateView();
                _isActiveViewActivated = false;
            }

            ActiveView = null;
            IsVisible = false;
            return;
        }

        var topModal = modalStack[^1];
        var topLayer = GetOrCreateView(topModal);
        var activeViewChanged = !ReferenceEquals(ActiveView, topLayer.View);
        if (activeViewChanged && _isActiveViewActivated && ActiveView is ModalView previousView)
        {
            previousView.DeactivateView();
            _isActiveViewActivated = false;
        }

        for (var index = 0; index < modalStack.Count; index++)
        {
            var entry = _viewCache[modalStack[index]];
            entry.Layer.IsVisible = true;
            entry.Layer.IsEnabled = index == modalStack.Count - 1;
            entry.Layer.ZIndex = index + 1;
        }

        ActiveView = topLayer.View as INavigationView
            ?? throw new InvalidOperationException($"The view for {topModal.GetType().Name} must implement {nameof(INavigationView)}.");
        IsVisible = true;

        if (_isAttached && !_isActiveViewActivated && topLayer.View is ModalView activeModalView)
        {
            activeModalView.ActivateView();
            _isActiveViewActivated = true;
        }
    }

    private (Border Layer, Control View) GetOrCreateView(IModalViewModel modal)
    {
        if (_viewCache.TryGetValue(modal, out var existingEntry))
        {
            return existingEntry;
        }

        var template = GetTemplates().FirstOrDefault(candidate => candidate is not ViewLocator && candidate.Match(modal));
        if (template is null)
        {
            throw new InvalidOperationException($"No explicit data template is registered for {modal.GetType().Name}.");
        }

        var view = template.Build(modal)
            ?? throw new InvalidOperationException($"The data template for {modal.GetType().Name} returned no view.");
        if (view is not ModalView)
        {
            throw new InvalidOperationException($"The view for {modal.GetType().Name} must derive from {nameof(ModalView)}.");
        }

        view.DataContext = modal;
        var layer = new Border
        {
            Background = _viewCache.Count == 0
                ? Brushes.Transparent
                : new SolidColorBrush(Color.FromArgb(105, 8, 12, 17)),
            Child = view,
            IsVisible = false
        };
        _viewCache.Add(modal, (layer, view));
        _layerRoot.Children.Add(layer);
        return (layer, view);
    }

    private static IEnumerable<IDataTemplate> GetTemplates()
    {
        return Application.Current?.DataTemplates
            ?? throw new InvalidOperationException("ModalHost requires explicit view templates.");
    }

    private void PruneViewCache()
    {
        var currentStack = Coordinator?.ModalStack.ToHashSet() ?? new HashSet<IModalViewModel>();
        foreach (var modal in _viewCache.Keys.Where(modal => !currentStack.Contains(modal)).ToArray())
        {
            var layer = _viewCache[modal].Layer;
            if (!ReferenceEquals(_viewCache[modal].View, ActiveView))
            {
                ((ModalView)_viewCache[modal].View).DeactivateView();
            }

            _layerRoot.Children.Remove(layer);
            _viewCache.Remove(modal);
        }
    }
}
