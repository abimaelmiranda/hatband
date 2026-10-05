using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;
using Hatband.App.Navigation;

namespace Hatband.App.Views;

/// <summary>
/// Hosts screen and modal contexts, dispatches global navigation intent, and handles window-level
/// game-session events. Concrete screens are responsible for their own input and focus rules.
/// </summary>
public partial class MainWindow : Window
{
    private WindowState _previousWindowState;
    private bool _isMinimizedForGame;
    private bool _isTemporarilyTopmost;
    private bool _wasTopmostBeforeRequest;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.ExitRequested += OnExitRequested;
        viewModel.Session.GameSessionStarted += OnGameSessionStarted;
        viewModel.Session.GameSessionEnded += OnGameSessionEnded;
        viewModel.Session.GameInstallationCompleted += OnGameInstallationCompleted;
        viewModel.Session.GameUninstallationCompleted += OnGameUninstallationCompleted;
        viewModel.Session.SteamFallbackRequested += OnSteamFallbackRequested;
        viewModel.Session.WindowTopmostRequested += OnWindowTopmostRequested;
        await viewModel.InitializeAsync();
        ScreenHost.ActiveView?.FocusInitial();
    }

    private async void OnWindowActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.Session.RefreshPendingInstallationStatesAsync();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ExitRequested -= OnExitRequested;
            viewModel.Session.GameSessionStarted -= OnGameSessionStarted;
            viewModel.Session.GameSessionEnded -= OnGameSessionEnded;
            viewModel.Session.GameInstallationCompleted -= OnGameInstallationCompleted;
            viewModel.Session.GameUninstallationCompleted -= OnGameUninstallationCompleted;
            viewModel.Session.SteamFallbackRequested -= OnSteamFallbackRequested;
            viewModel.Session.WindowTopmostRequested -= OnWindowTopmostRequested;
            viewModel.Session.StopPendingInstallationPolling();
            viewModel.Session.StopGameProcessMonitoring();
        }
    }

    private void OnGameSessionStarted(object? sender, EventArgs e)
    {
        if (_isMinimizedForGame)
        {
            return;
        }

        _previousWindowState = WindowState;
        _isMinimizedForGame = true;
        WindowState = WindowState.Minimized;
    }

    private void OnGameSessionEnded(object? sender, EventArgs e)
    {
        if (!_isMinimizedForGame)
        {
            return;
        }

        WindowState = _previousWindowState;
        _isMinimizedForGame = false;
        Activate();
    }

    private void OnGameUninstallationCompleted(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(Activate);
    }

    private void OnGameInstallationCompleted(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(Activate);
    }

    private async void OnSteamFallbackRequested(object? sender, EventArgs e)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(700));
        if (IsVisible)
        {
            Dispatcher.UIThread.Post(Activate);
        }
    }

    private void OnWindowTopmostRequested(bool isTopmostRequested)
    {
        if (isTopmostRequested)
        {
            if (_isTemporarilyTopmost)
            {
                return;
            }

            _wasTopmostBeforeRequest = Topmost;
            _isTemporarilyTopmost = true;
            Topmost = true;
            Activate();
            return;
        }

        if (!_isTemporarilyTopmost)
        {
            return;
        }

        Topmost = _wasTopmostBeforeRequest;
        _isTemporarilyTopmost = false;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.Session.IsGameSessionActive)
        {
            e.Handled = true;
            return;
        }

        var action = GetNavigationAction(e);
        if (action is null)
        {
            return;
        }

        var target = ModalHost.ActiveView ?? ScreenHost.ActiveView;
        if (target is null)
        {
            return;
        }

        if (action == NavigationAction.Back)
        {
            if (target.TryHandleBack(e))
            {
                e.Handled = true;
                return;
            }

            e.Handled = viewModel.Navigation.HasOpenModals
                ? viewModel.Navigation.DismissTopModal()
                : viewModel.Navigation.GoBack();
            return;
        }

        var handling = target.HandleNavigationAction(action.Value, e);
        if (handling == NavigationActionHandling.Handled)
        {
            e.Handled = true;
            return;
        }

        if (handling == NavigationActionHandling.Native)
        {
            return;
        }

        if (action == NavigationAction.OpenMenu && !viewModel.Navigation.HasOpenModals)
        {
            e.Handled = true;
            viewModel.OpenMenuCommand.Execute(null);
        }
    }

    private static NavigationAction? GetNavigationAction(KeyEventArgs e)
    {
        var textInput = e.Source as TextBox;
        if (textInput is null && e.Source is Control sourceControl)
        {
            textInput = sourceControl.GetVisualAncestors().OfType<TextBox>().FirstOrDefault();
        }

        if (textInput is not null && e.Key is Key.M or Key.Left or Key.Right)
        {
            return null;
        }

        if (textInput is { AcceptsReturn: true } && e.Key is Key.Up or Key.Down)
        {
            return null;
        }

        return e.Key switch
        {
            Key.Up => NavigationAction.Up,
            Key.Down => NavigationAction.Down,
            Key.Left => NavigationAction.Left,
            Key.Right => NavigationAction.Right,
            Key.Enter => NavigationAction.Confirm,
            Key.Escape => NavigationAction.Back,
            Key.M => NavigationAction.OpenMenu,
            _ => null
        };
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
