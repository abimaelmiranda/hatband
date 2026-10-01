using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Hatband.App.ViewModels;

public sealed partial class GameArtworkSourceOption : ObservableObject
{
    public GameArtworkSourceOption(string displayName, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        DisplayName = displayName;
        Url = url;
    }

    public string DisplayName { get; }

    public string Url { get; }

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
