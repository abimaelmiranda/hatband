using System.Diagnostics;
using Avalonia.Threading;
using Hatband.App.Navigation;
using Microsoft.Extensions.Logging;
using SDL;

namespace Hatband.App.Services.Input;

/// <summary>Reads SDL gamepad state and translates it into Hatband navigation actions.</summary>
public sealed class GamepadInputService : IDisposable
{
    private const double AxisPressThreshold = 0.5;
    private const double AxisReleaseThreshold = 0.35;
    private const int InitialNavigationRepeatDelayMilliseconds = 350;
    private const int NavigationRepeatIntervalMilliseconds = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(16);

    private readonly ILogger<GamepadInputService> _logger;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<SDL_JoystickID, IntPtr> _gamepads = [];
    private bool _started;
    private bool _disposed;
    private bool _subsystemsInitialized;
    private bool _inputEnabled;
    private bool _suppressUntilNeutral;
    private GamepadState _previousState;
    private NavigationAction? _currentDirection;
    private bool _currentDirectionUsesStick;
    private long _nextNavigationRepeatTimestamp;

    public GamepadInputService(ILogger<GamepadInputService> logger)
    {
        _logger = logger;
        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += OnTimerTick;
    }

    public event Action<NavigationAction>? ActionRequested;

    public event Action<bool>? ConnectionChanged;

    public event Action<string>? InitializationFailed;

    public bool HasConnectedGamepad => _gamepads.Count > 0;

    public void Start()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            StartCore();
            return;
        }

        Dispatcher.UIThread.InvokeAsync(StartCore).GetAwaiter().GetResult();
    }

    public void SetInputEnabled(bool enabled)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            SetInputEnabledCore(enabled);
            return;
        }

        Dispatcher.UIThread.InvokeAsync(() => SetInputEnabledCore(enabled)).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            DisposeCore();
            return;
        }

        Dispatcher.UIThread.InvokeAsync(DisposeCore).GetAwaiter().GetResult();
    }

    private void StartCore()
    {
        if (_started || _disposed)
        {
            return;
        }

        _started = true;

        try
        {
            SDL3.SDL_SetHint("SDL_JOYSTICK_THREAD", "1");
            SDL3.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");

            var subsystems = SDL_InitFlags.SDL_INIT_GAMEPAD | SDL_InitFlags.SDL_INIT_EVENTS;
            if (!SDL3.SDL_InitSubSystem(subsystems))
            {
                FailInitialization(SDL3.SDL_GetError());
                return;
            }

            _subsystemsInitialized = true;
            SDL3.SDL_SetGamepadEventsEnabled(true);
            if (!OpenConnectedGamepads())
            {
                return;
            }

            ConnectionChanged?.Invoke(HasConnectedGamepad);
            _timer.Start();
            _logger.LogInformation("SDL gamepad polling started with {GamepadCount} connected gamepads.", _gamepads.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to initialize SDL gamepad input.");
            FailInitialization(exception.Message);
        }
    }

    private void SetInputEnabledCore(bool enabled)
    {
        if (_disposed || _inputEnabled == enabled)
        {
            return;
        }

        _inputEnabled = enabled;
        _logger.LogInformation("Gamepad navigation enabled: {Enabled}.", enabled);
        _suppressUntilNeutral = enabled;
        ResetNavigationRepeat();
        _previousState = default;
    }

    private void OnTimerTick(object? sender, EventArgs eventArgs)
    {
        if (!_subsystemsInitialized || _disposed)
        {
            return;
        }

        GamepadState state;
        try
        {
            PollEvents();
            SDL3.SDL_UpdateGamepads();
            state = ReadState();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed while reading SDL gamepad input.");
            FailInitialization(exception.Message);
            return;
        }

        try
        {
            ProcessGamepadState(state);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to execute gamepad navigation.");
            _suppressUntilNeutral = true;
            ResetNavigationRepeat();
        }
        finally
        {
            _previousState = state;
        }
    }

    private unsafe void PollEvents()
    {
        SDL_Event* gamepadEvent = stackalloc SDL_Event[1];
        while (SDL3.SDL_PollEvent(gamepadEvent))
        {
            switch (gamepadEvent->Type)
            {
                case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
                    OpenGamepad(gamepadEvent->gdevice.which);
                    break;
                case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
                    CloseGamepad(gamepadEvent->gdevice.which);
                    break;
            }
        }
    }

    private bool OpenConnectedGamepads()
    {
        using var connectedGamepads = SDL3.SDL_GetGamepads();
        if (connectedGamepads is null)
        {
            FailInitialization(SDL3.SDL_GetError());
            return false;
        }

        foreach (var gamepadId in connectedGamepads)
        {
            OpenGamepad(gamepadId);
        }

        return true;
    }

    private unsafe void OpenGamepad(SDL_JoystickID gamepadId)
    {
        if (_gamepads.ContainsKey(gamepadId) || !SDL3.SDL_IsGamepad(gamepadId))
        {
            return;
        }

        var gamepad = SDL3.SDL_OpenGamepad(gamepadId);
        if (gamepad == null)
        {
            _logger.LogWarning("SDL could not open gamepad {GamepadId}: {Error}", gamepadId, SDL3.SDL_GetError());
            return;
        }

        var wasConnected = HasConnectedGamepad;
        _gamepads.Add(gamepadId, (IntPtr)gamepad);
        _suppressUntilNeutral = true;

        if (!wasConnected)
        {
            ConnectionChanged?.Invoke(true);
        }
    }

    private unsafe void CloseGamepad(SDL_JoystickID gamepadId)
    {
        if (!_gamepads.Remove(gamepadId, out var gamepad))
        {
            return;
        }

        SDL3.SDL_CloseGamepad((SDL_Gamepad*)gamepad);
        _suppressUntilNeutral = true;
        ResetNavigationRepeat();

        if (!HasConnectedGamepad)
        {
            ConnectionChanged?.Invoke(false);
        }
    }

    private void ProcessGamepadState(GamepadState state)
    {
        if (!_inputEnabled)
        {
            ResetNavigationRepeat();
            return;
        }

        if (_suppressUntilNeutral)
        {
            _currentDirection = state.Direction;
            _currentDirectionUsesStick = state.DirectionUsesStick;
            _nextNavigationRepeatTimestamp = 0;

            if (!state.HasActiveInput)
            {
                _suppressUntilNeutral = false;
                ResetNavigationRepeat();
            }

            return;
        }

        DispatchButtonPresses(state);
        DispatchDirection(state);
    }

    private unsafe GamepadState ReadState()
    {
        var confirm = false;
        var back = false;
        var openMenu = false;
        var previousTab = false;
        var nextTab = false;
        var dpadDirection = (NavigationAction?)null;
        var leftStickX = 0;
        var leftStickY = 0;
        var strongestStickMagnitude = 0d;

        foreach (var gamepadHandle in _gamepads.Values)
        {
            var gamepad = (SDL_Gamepad*)gamepadHandle;
            if (!SDL3.SDL_GamepadConnected(gamepad))
            {
                continue;
            }

            confirm |= SDL3.SDL_GetGamepadButton(gamepad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH);
            back |= SDL3.SDL_GetGamepadButton(gamepad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST);
            openMenu |= SDL3.SDL_GetGamepadButton(gamepad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START);
            previousTab |= SDL3.SDL_GetGamepadButton(gamepad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER);
            nextTab |= SDL3.SDL_GetGamepadButton(gamepad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER);

            dpadDirection ??= ReadDpadDirection(gamepad);
            var stickX = SDL3.SDL_GetGamepadAxis(gamepad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX);
            var stickY = SDL3.SDL_GetGamepadAxis(gamepad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY);
            var stickMagnitude = (double)stickX * stickX + (double)stickY * stickY;
            if (stickMagnitude > strongestStickMagnitude)
            {
                leftStickX = stickX;
                leftStickY = stickY;
                strongestStickMagnitude = stickMagnitude;
            }
        }

        var direction = dpadDirection;
        var directionUsesStick = false;
        if (direction is null)
        {
            direction = ReadStickDirection(leftStickX, leftStickY);
            directionUsesStick = direction is not null;
        }

        return new GamepadState(confirm, back, openMenu, previousTab, nextTab, direction, directionUsesStick);
    }

    private static unsafe NavigationAction? ReadDpadDirection(SDL_Gamepad* gamepad)
    {
        if (SDL3.SDL_GetGamepadButton(gamepad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP))
        {
            return NavigationAction.Up;
        }

        if (SDL3.SDL_GetGamepadButton(gamepad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN))
        {
            return NavigationAction.Down;
        }

        if (SDL3.SDL_GetGamepadButton(gamepad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT))
        {
            return NavigationAction.Left;
        }

        if (SDL3.SDL_GetGamepadButton(gamepad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT))
        {
            return NavigationAction.Right;
        }

        return null;
    }

    private NavigationAction? ReadStickDirection(int x, int y)
    {
        var normalizedX = x / (double)short.MaxValue;
        var normalizedY = y / (double)short.MaxValue;
        var threshold = _currentDirectionUsesStick && _currentDirection is not null
            ? AxisReleaseThreshold
            : AxisPressThreshold;

        if (_currentDirectionUsesStick)
        {
            var currentAxisValue = _currentDirection switch
            {
                NavigationAction.Left => -normalizedX,
                NavigationAction.Right => normalizedX,
                NavigationAction.Up => -normalizedY,
                NavigationAction.Down => normalizedY,
                _ => 0
            };
            var competingAxisValue = _currentDirection is NavigationAction.Left or NavigationAction.Right
                ? Math.Abs(normalizedY)
                : Math.Abs(normalizedX);

            if (currentAxisValue >= AxisReleaseThreshold && currentAxisValue >= competingAxisValue)
            {
                return _currentDirection;
            }
        }

        if (Math.Abs(normalizedX) >= Math.Abs(normalizedY))
        {
            if (normalizedX <= -threshold)
            {
                return NavigationAction.Left;
            }

            if (normalizedX >= threshold)
            {
                return NavigationAction.Right;
            }
        }
        else
        {
            if (normalizedY <= -threshold)
            {
                return NavigationAction.Up;
            }

            if (normalizedY >= threshold)
            {
                return NavigationAction.Down;
            }
        }

        return null;
    }

    private void DispatchButtonPresses(GamepadState state)
    {
        if (state.Confirm && !_previousState.Confirm)
        {
            ActionRequested?.Invoke(NavigationAction.Confirm);
        }

        if (state.Back && !_previousState.Back)
        {
            ActionRequested?.Invoke(NavigationAction.Back);
        }

        if (state.OpenMenu && !_previousState.OpenMenu)
        {
            ActionRequested?.Invoke(NavigationAction.OpenMenu);
        }

        if (state.PreviousTab && !_previousState.PreviousTab)
        {
            ActionRequested?.Invoke(NavigationAction.PreviousTab);
        }

        if (state.NextTab && !_previousState.NextTab)
        {
            ActionRequested?.Invoke(NavigationAction.NextTab);
        }
    }

    private void DispatchDirection(GamepadState state)
    {
        if (state.Direction is null)
        {
            ResetNavigationRepeat();
            return;
        }

        if (_currentDirection != state.Direction)
        {
            _currentDirection = state.Direction;
            _currentDirectionUsesStick = state.DirectionUsesStick;
            ActionRequested?.Invoke(state.Direction.Value);
            _nextNavigationRepeatTimestamp = Stopwatch.GetTimestamp() + MillisecondsToTimestamp(InitialNavigationRepeatDelayMilliseconds);
            return;
        }

        _currentDirectionUsesStick = state.DirectionUsesStick;
        var now = Stopwatch.GetTimestamp();
        if (_nextNavigationRepeatTimestamp != 0 && now >= _nextNavigationRepeatTimestamp)
        {
            ActionRequested?.Invoke(state.Direction.Value);
            _nextNavigationRepeatTimestamp = now + MillisecondsToTimestamp(NavigationRepeatIntervalMilliseconds);
        }
    }

    private void ResetNavigationRepeat()
    {
        _currentDirection = null;
        _currentDirectionUsesStick = false;
        _nextNavigationRepeatTimestamp = 0;
    }

    private static long MillisecondsToTimestamp(int milliseconds)
    {
        return (long)(milliseconds * (double)Stopwatch.Frequency / 1000);
    }

    private void FailInitialization(string? message)
    {
        var error = string.IsNullOrWhiteSpace(message) ? "SDL gamepad input could not be initialized." : message;
        _logger.LogError("SDL gamepad input could not be initialized: {Error}", error);
        InitializationFailed?.Invoke(error);
        DisposeCore();
    }

    private void DisposeCore()
    {
        if (_disposed)
        {
            return;
        }

        var hadConnectedGamepads = HasConnectedGamepad;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;

        foreach (var gamepad in _gamepads.Values)
        {
            unsafe
            {
                SDL3.SDL_CloseGamepad((SDL_Gamepad*)gamepad);
            }
        }

        _gamepads.Clear();

        if (_subsystemsInitialized)
        {
            SDL3.SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_GAMEPAD | SDL_InitFlags.SDL_INIT_EVENTS);
            _subsystemsInitialized = false;
        }

        if (hadConnectedGamepads)
        {
            ConnectionChanged?.Invoke(false);
        }
    }

    private readonly record struct GamepadState(
        bool Confirm,
        bool Back,
        bool OpenMenu,
        bool PreviousTab,
        bool NextTab,
        NavigationAction? Direction,
        bool DirectionUsesStick)
    {
        public bool HasActiveInput => Confirm || Back || OpenMenu || PreviousTab || NextTab || Direction is not null;
    }
}
