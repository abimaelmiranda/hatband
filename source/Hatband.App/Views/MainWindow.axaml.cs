using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;
using Hatband.App.Navigation;
using Hatband.App.Services.Input;
using Hatband.App.ViewModels.Settings;
using Hatband.App.Views.Navigation;
using Hatband.Core.Models.Settings;
using AppResources = Hatband.App.Localization.Resources;

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

    private readonly GamepadInputService _gamepadInput;
    private readonly SettingsScreenViewModel _settings;
    private bool _inputReady;
    private bool _closed;

    public MainWindow(GamepadInputService gamepadInput, SettingsScreenViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(gamepadInput);
        ArgumentNullException.ThrowIfNull(settings);
        _gamepadInput = gamepadInput;
        _settings = settings;
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Activated is raised before Avalonia updates IsActive. Read the committed
        // property value so controller navigation resumes when focus returns.
        if (_inputReady && !_closed &&
            (change.Property == IsActiveProperty || change.Property == WindowStateProperty))
        {
            UpdateGamepadInputEnabled();
        }
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _gamepadInput.ActionRequested += OnGamepadActionRequested;
        _gamepadInput.ConnectionChanged += OnGamepadConnectionChanged;
        _gamepadInput.InitializationFailed += OnGamepadInitializationFailed;
        _settings.ControllerDisplayModeChanged += OnControllerDisplayModeChanged;
        _settings.Navigation.PropertyChanged += OnNavigationContextChanged;
        viewModel.Navigation.PropertyChanged += OnNavigationContextChanged;
        viewModel.ExitRequested += OnExitRequested;
        viewModel.Session.GameSessionStarted += OnGameSessionStarted;
        viewModel.Session.GameSessionEnded += OnGameSessionEnded;
        viewModel.Session.GameInstallationCompleted += OnGameInstallationCompleted;
        viewModel.Session.GameUninstallationCompleted += OnGameUninstallationCompleted;
        viewModel.Session.SteamFallbackRequested += OnSteamFallbackRequested;
        viewModel.Session.WindowTopmostRequested += OnWindowTopmostRequested;
        await viewModel.InitializeAsync();
        if (_closed)
        {
            return;
        }

        ScreenHost.ActiveView?.FocusInitial();
        _inputReady = true;
        _gamepadInput.Start();
        NavigationHints.HasConnectedGamepad = _gamepadInput.HasConnectedGamepad;
        UpdateGamepadInputEnabled();
        RefreshInputHints();
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
        _closed = true;
        _inputReady = false;
        _gamepadInput.ActionRequested -= OnGamepadActionRequested;
        _gamepadInput.ConnectionChanged -= OnGamepadConnectionChanged;
        _gamepadInput.InitializationFailed -= OnGamepadInitializationFailed;
        _settings.ControllerDisplayModeChanged -= OnControllerDisplayModeChanged;
        _settings.Navigation.PropertyChanged -= OnNavigationContextChanged;
        _gamepadInput.Dispose();
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Navigation.PropertyChanged -= OnNavigationContextChanged;
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
        _gamepadInput.SetInputEnabled(false);
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
        UpdateGamepadInputEnabled();
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
        if (action is not null && _inputReady)
        {
            e.Handled = DispatchNavigationAction(action.Value, InputSource.Keyboard);
        }
    }

    private void OnGamepadActionRequested(NavigationAction action)
    {
        if (CanUseGamepadInput())
        {
            DispatchNavigationAction(action, InputSource.Gamepad);
        }
    }

    private bool DispatchNavigationAction(NavigationAction action, InputSource source)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return false;
        }

        var target = ModalHost.ActiveView ?? ScreenHost.ActiveView;
        if (target is null)
        {
            return false;
        }

        if ((source == InputSource.Gamepad || action == NavigationAction.Back) &&
            FocusedControlNavigationAdapter.TryHandleOpenComboBox(target.NavigationRoot, action))
        {
            return true;
        }

        bool handled;
        if (action == NavigationAction.Back)
        {
            handled = target.TryHandleBack() || (viewModel.Navigation.HasOpenModals
                ? viewModel.Navigation.DismissTopModal()
                : viewModel.Navigation.GoBack());
        }
        else
        {
            var handling = target.HandleNavigationAction(action, new NavigationInputContext(source));
            handled = handling == NavigationActionHandling.Handled;
            if (!handled && source == InputSource.Gamepad)
            {
                handled = FocusedControlNavigationAdapter.TryHandle(target.NavigationRoot, action);
            }

            if (!handled && handling != NavigationActionHandling.Native &&
                action == NavigationAction.OpenMenu && !viewModel.Navigation.HasOpenModals)
            {
                viewModel.OpenMenuCommand.Execute(null);
                handled = true;
            }
        }

        RefreshInputHints();
        return handled;
    }

    private bool CanUseGamepadInput()
    {
        return _inputReady && !_closed && IsActive && WindowState != WindowState.Minimized &&
            DataContext is MainWindowViewModel viewModel && !viewModel.Session.IsGameSessionActive;
    }

    private void UpdateGamepadInputEnabled()
    {
        _gamepadInput.SetInputEnabled(CanUseGamepadInput());
    }

    private void OnGamepadConnectionChanged(bool connected)
    {
        NavigationHints.HasConnectedGamepad = connected;
    }

    private void OnControllerDisplayModeChanged(ControllerDisplayMode mode)
    {
        NavigationHints.DisplayMode = mode;
    }

    private void OnGamepadInitializationFailed(string error)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Session.StatusMessage = string.Format(
                CultureInfo.CurrentCulture, AppResources.GamepadInitializationError, error);
        }
    }

    private void OnNavigationContextChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshInputHints);
    }

    private void RefreshInputHints()
    {
        if (!_closed)
        {
            NavigationHints.SupportsTabNavigation = (ModalHost.ActiveView ?? ScreenHost.ActiveView)?.SupportsTabNavigation == true;
        }
    }

    private static NavigationAction? GetNavigationAction(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            if (e.Key == Key.PageUp)
            {
                return NavigationAction.PreviousTab;
            }

            if (e.Key == Key.PageDown)
            {
                return NavigationAction.NextTab;
            }
        }

        var textInput = e.Source as TextBox;
        if (textInput is null && e.Source is Control sourceControl)
        {
            textInput = sourceControl.GetVisualAncestors().OfType<TextBox>().FirstOrDefault();
        }

        if (textInput is not null && e.Key is Key.M or Key.Left or Key.Right)
        {
            return null;
        }

        if (textInput is { AcceptsReturn: true } && e.Key is Key.Up or Key.Down or Key.Enter)
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
