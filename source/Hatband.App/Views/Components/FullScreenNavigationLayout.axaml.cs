using Avalonia;
using Avalonia.Controls;

namespace Hatband.App.Views.Components;

public partial class FullScreenNavigationLayout : UserControl
{
    public static readonly StyledProperty<object?> NavigationContentProperty =
        AvaloniaProperty.Register<FullScreenNavigationLayout, object?>(nameof(NavigationContent));

    public static readonly StyledProperty<object?> MainContentProperty =
        AvaloniaProperty.Register<FullScreenNavigationLayout, object?>(nameof(MainContent));

    public static readonly StyledProperty<object?> FooterContentProperty =
        AvaloniaProperty.Register<FullScreenNavigationLayout, object?>(nameof(FooterContent));

    public FullScreenNavigationLayout()
    {
        InitializeComponent();
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
}
