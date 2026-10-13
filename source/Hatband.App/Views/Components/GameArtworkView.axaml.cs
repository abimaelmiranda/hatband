using Avalonia;
using Avalonia.Controls;
using Hatband.App.ViewModels;

namespace Hatband.App.Views.Components;

public partial class GameArtworkView : UserControl
{
    private bool isAttached;
    private GameCardViewModel? coverConsumer;
    public static readonly StyledProperty<bool> ShowTitleProperty =
        AvaloniaProperty.Register<GameArtworkView, bool>(nameof(ShowTitle), true);

    public GameArtworkView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    public bool ShowTitle
    {
        get => GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (isAttached && coverConsumer is not null)
        {
            coverConsumer.DetachCoverConsumer(this);
        }

        coverConsumer = DataContext as GameCardViewModel;
        AttachCoverConsumer();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        isAttached = true;
        coverConsumer = DataContext as GameCardViewModel;
        AttachCoverConsumer();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        isAttached = false;
        coverConsumer?.DetachCoverConsumer(this);
    }

    private void AttachCoverConsumer()
    {
        if (isAttached)
        {
            var renderScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            var decodeWidth = (int)Math.Ceiling(320 * renderScaling);
            coverConsumer?.AttachCoverConsumer(this, decodeWidth);
        }
    }
}
