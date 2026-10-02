using Avalonia.Controls;

namespace Hatband.App.Views.Components;

public partial class GameSessionOverlayView : UserControl
{
    public GameSessionOverlayView()
    {
        InitializeComponent();
        ZIndex = 10000;
    }
}
