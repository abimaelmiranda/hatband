using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Hatband.App.ViewModels;

public sealed partial class GameArtworkSourceOption : ObservableObject
{
    public GameArtworkSourceOption(string displayName, GameArtworkImage image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(image);
        DisplayName = displayName;
        Image = image;
    }

    public string DisplayName { get; }

    public GameArtworkImage Image { get; }

    [ObservableProperty]
    public partial Bitmap? PreviewImage { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewLoaded { get; set; }

    public bool HasPreviewImage => PreviewImage is not null;

    public bool IsPreviewUnavailable => IsPreviewLoaded && !HasPreviewImage;

    partial void OnPreviewImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasPreviewImage));
        OnPropertyChanged(nameof(IsPreviewUnavailable));
    }

    partial void OnIsPreviewLoadedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPreviewUnavailable));
    }
}
