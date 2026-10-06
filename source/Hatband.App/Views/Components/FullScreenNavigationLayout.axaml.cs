using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Hatband.App.Views.Components;

public partial class FullScreenNavigationLayout : UserControl
{
    private readonly Dictionary<Control, (bool Focusable, bool IsTabStop)> navigationFocusStates = [];

    public static readonly StyledProperty<object?> NavigationContentProperty =
        AvaloniaProperty.Register<FullScreenNavigationLayout, object?>(nameof(NavigationContent));

    public static readonly StyledProperty<object?> MainContentProperty =
        AvaloniaProperty.Register<FullScreenNavigationLayout, object?>(nameof(MainContent));

    public static readonly StyledProperty<object?> FooterContentProperty =
        AvaloniaProperty.Register<FullScreenNavigationLayout, object?>(nameof(FooterContent));

    public static readonly StyledProperty<bool> IsMainContentActiveProperty =
        AvaloniaProperty.Register<FullScreenNavigationLayout, bool>(nameof(IsMainContentActive));

    public FullScreenNavigationLayout()
    {
        InitializeComponent();
        PropertyChanged += OnLayoutPropertyChanged;
        NavigationContentPresenter.PropertyChanged += OnNavigationPresenterPropertyChanged;
        UpdateNavigationFocusability();
    }

    public object? NavigationContent
    {
        get => GetValue(NavigationContentProperty);
        set => SetValue(NavigationContentProperty, value);
    }

    public object? MainContent
    {
        get => GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
    }

    public object? FooterContent
    {
        get => GetValue(FooterContentProperty);
        set => SetValue(FooterContentProperty, value);
    }

    public bool IsMainContentActive
    {
        get => GetValue(IsMainContentActiveProperty);
        set => SetValue(IsMainContentActiveProperty, value);
    }

    public Control? NavigationContentRoot => NavigationContentPresenter.Content as Control;

    public Control? MainContentRoot => MainContentPresenter.Content as Control;

    public Control? FooterContentRoot => FooterContentPresenter.Content as Control;

    public void ActivateMainContent() => IsMainContentActive = true;

    public void DeactivateMainContent() => IsMainContentActive = false;

    private void OnLayoutPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsMainContentActiveProperty)
        {
            UpdateNavigationFocusability();
        }
    }

    private void OnNavigationPresenterPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (IsMainContentActive && e.Property == ContentControl.ContentProperty)
        {
            UpdateNavigationFocusability();
        }
    }

    private void UpdateNavigationFocusability()
    {
        foreach (var (control, focusState) in navigationFocusStates)
        {
            control.Focusable = focusState.Focusable;
            control.IsTabStop = focusState.IsTabStop;
        }

        navigationFocusStates.Clear();
        if (!IsMainContentActive)
        {
            return;
        }

        foreach (var control in NavigationContentPresenter.GetVisualDescendants().OfType<Control>())
        {
            navigationFocusStates.Add(control, (control.Focusable, control.IsTabStop));
            control.Focusable = false;
            control.IsTabStop = false;
        }
    }
}
