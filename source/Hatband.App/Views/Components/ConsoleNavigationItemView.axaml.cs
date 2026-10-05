using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Hatband.App.Views.Components;

public partial class ConsoleNavigationItemView : UserControl
{
    public static readonly StyledProperty<FluentIconGlyph> IconGlyphProperty =
        AvaloniaProperty.Register<ConsoleNavigationItemView, FluentIconGlyph>(nameof(IconGlyph));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ConsoleNavigationItemView, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<double> TitleFontSizeProperty =
        AvaloniaProperty.Register<ConsoleNavigationItemView, double>(nameof(TitleFontSize), 15);

    public static readonly StyledProperty<IBrush> ItemBackgroundProperty =
        AvaloniaProperty.Register<ConsoleNavigationItemView, IBrush>(nameof(ItemBackground), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> ItemBorderBrushProperty =
        AvaloniaProperty.Register<ConsoleNavigationItemView, IBrush>(nameof(ItemBorderBrush), Brushes.Transparent);

    public ConsoleNavigationItemView()
    {
        InitializeComponent();
    }

    public FluentIconGlyph IconGlyph
    {
        get => GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public double TitleFontSize
    {
        get => GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }

    public IBrush ItemBackground
    {
        get => GetValue(ItemBackgroundProperty);
        set => SetValue(ItemBackgroundProperty, value);
    }

    public IBrush ItemBorderBrush
    {
        get => GetValue(ItemBorderBrushProperty);
        set => SetValue(ItemBorderBrushProperty, value);
    }

    public event EventHandler? Activated;

    public event EventHandler? FocusEntered;

    public void FocusItem()
    {
        DirectionalFocusNavigator.Focus(NavigationButton);
    }

    public bool HasKeyboardFocus => NavigationButton.IsFocused;

    public void SetSelected(bool isSelected)
    {
        ItemBackground = isSelected
            ? new SolidColorBrush(Color.Parse("#263743"))
            : Brushes.Transparent;
        ItemBorderBrush = isSelected
            ? new SolidColorBrush(Color.Parse("#72D9FF"))
            : Brushes.Transparent;
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        Activated?.Invoke(this, EventArgs.Empty);
    }

    private void OnNavigationButtonGotFocus(object? sender, RoutedEventArgs e)
    {
        FocusEntered?.Invoke(this, EventArgs.Empty);
    }
}
