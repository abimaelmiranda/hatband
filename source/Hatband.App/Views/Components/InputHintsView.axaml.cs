using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Hatband.App.Navigation;
using Hatband.App.ViewModels.Navigation;
using Hatband.Core.Models.Settings;
using AppResources = Hatband.App.Localization.Resources;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace Hatband.App.Views.Components;

/// <summary>Displays semantic input hints using keyboard labels or the selected controller legend style.</summary>
public partial class InputHintsView : UserControl
{
    public static readonly StyledProperty<IReadOnlyList<InputHint>> InputHintsProperty =
        AvaloniaProperty.Register<InputHintsView, IReadOnlyList<InputHint>>(nameof(InputHints), Array.Empty<InputHint>());

    public static readonly StyledProperty<bool> HasConnectedGamepadProperty =
        AvaloniaProperty.Register<InputHintsView, bool>(nameof(HasConnectedGamepad));

    public static readonly StyledProperty<ControllerDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<InputHintsView, ControllerDisplayMode>(nameof(DisplayMode), ControllerDisplayMode.Xbox);

    public static readonly StyledProperty<bool> SupportsTabNavigationProperty =
        AvaloniaProperty.Register<InputHintsView, bool>(nameof(SupportsTabNavigation));

    public InputHintsView()
    {
        InitializeComponent();
        RenderHints();
    }

    public IReadOnlyList<InputHint> InputHints
    {
        get => GetValue(InputHintsProperty);
        set => SetValue(InputHintsProperty, value);
    }

    public bool HasConnectedGamepad
    {
        get => GetValue(HasConnectedGamepadProperty);
        set => SetValue(HasConnectedGamepadProperty, value);
    }

    public ControllerDisplayMode DisplayMode
    {
        get => GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    public bool SupportsTabNavigation
    {
        get => GetValue(SupportsTabNavigationProperty);
        set => SetValue(SupportsTabNavigationProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InputHintsProperty ||
            change.Property == HasConnectedGamepadProperty ||
            change.Property == DisplayModeProperty ||
            change.Property == SupportsTabNavigationProperty)
        {
            RenderHints();
        }
    }

    private void RenderHints()
    {
        if (HintsPanel is null)
        {
            return;
        }

        HintsPanel.Children.Clear();
        var hints = InputHints.ToList();
        if (SupportsTabNavigation)
        {
            hints.Add(new InputHint(NavigationAction.PreviousTab, AppResources.InputHintPreviousTab));
            hints.Add(new InputHint(NavigationAction.NextTab, AppResources.InputHintNextTab));
        }

        IsVisible = hints.Count > 0;
        foreach (var hint in hints)
        {
            HintsPanel.Children.Add(CreateHint(hint));
        }
    }

    private Control CreateHint(InputHint hint)
    {
        var isGamepad = HasConnectedGamepad;
        var legend = CreateLegend(hint.Action, isGamepad);
        var keyBadge = new Border
        {
            Padding = new Thickness(6, 2),
            MinWidth = 22,
            MinHeight = 20,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.Parse("#36424D")),
            Child = legend
        };
        var description = new TextBlock
        {
            Text = hint.Description,
            Foreground = new SolidColorBrush(Color.Parse("#D5DEE5")),
            Classes = { "typography-caption" },
            VerticalAlignment = VerticalAlignment.Center
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 12, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { keyBadge, description }
        };
    }

    private Control CreateLegend(NavigationAction action, bool isGamepad)
    {
        if (!isGamepad)
        {
            return CreateLegendText(action switch
            {
                NavigationAction.Up or NavigationAction.Down or NavigationAction.Left or NavigationAction.Right => "↑ ↓ ← →",
                NavigationAction.Confirm => "Enter",
                NavigationAction.Back => "Esc",
                NavigationAction.OpenMenu => "M",
                NavigationAction.PreviousTab => "Ctrl + PgUp",
                NavigationAction.NextTab => "Ctrl + PgDn",
                _ => throw new InvalidOperationException($"No keyboard legend is defined for '{action}'.")
            });
        }

        if (DisplayMode == ControllerDisplayMode.PlayStation && action is (NavigationAction.Confirm or NavigationAction.Back))
        {
            return action == NavigationAction.Confirm
                ? new ShapePath
                {
                    Data = StreamGeometry.Parse("M 1,1 L 9,9 M 9,1 L 1,9"),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.6,
                    Width = 10,
                    Height = 10,
                    Stretch = Stretch.Fill
                }
                : new Ellipse
                {
                    Stroke = Brushes.White,
                    StrokeThickness = 1.6,
                    Width = 9,
                    Height = 9
                };
        }

        return CreateLegendText(action switch
        {
            NavigationAction.Up or NavigationAction.Down or NavigationAction.Left or NavigationAction.Right => AppResources.ControllerDirectionalInput,
            NavigationAction.Confirm => DisplayMode == ControllerDisplayMode.Xbox ? "A" : "✕",
            NavigationAction.Back => DisplayMode == ControllerDisplayMode.Xbox ? "B" : "○",
            NavigationAction.OpenMenu => DisplayMode == ControllerDisplayMode.Xbox ? AppResources.ControllerMenuXbox : AppResources.ControllerMenuPlayStation,
            NavigationAction.PreviousTab => DisplayMode == ControllerDisplayMode.Xbox ? "LB" : "L1",
            NavigationAction.NextTab => DisplayMode == ControllerDisplayMode.Xbox ? "RB" : "R1",
            _ => throw new InvalidOperationException($"No gamepad legend is defined for '{action}'.")
        });
    }

    private static TextBlock CreateLegendText(string text) => new()
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        Classes = { "typography-caption" },
        Foreground = Brushes.White,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };
}
