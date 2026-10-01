using Avalonia;
using Avalonia.Controls;

namespace Hatband.App.Views.Components;

public partial class GameDetailStatView : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<GameDetailStatView, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<GameDetailStatView, string>(nameof(Value), string.Empty);

    public GameDetailStatView()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}
