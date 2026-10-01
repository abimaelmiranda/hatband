using Avalonia;
using Avalonia.Controls;

namespace Hatband.App.Views.Components;

public partial class GameArtworkView : UserControl
{
    public static readonly StyledProperty<bool> ShowTitleProperty =
        AvaloniaProperty.Register<GameArtworkView, bool>(nameof(ShowTitle), true);

    public GameArtworkView()
    {
        InitializeComponent();
    }

    public bool ShowTitle
    {
        get => GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }
}
