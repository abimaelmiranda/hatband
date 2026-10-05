using CommunityToolkit.Mvvm.Input;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.ViewModels;

/// <summary>Adapts the shared artwork search state to a modal confirmation or cancellation result.</summary>
public partial class ArtworkPickerModalViewModel : ModalViewModel<GameArtworkSourceOption>
{
    public ArtworkPickerModalViewModel(GameArtworkPickerViewModel artworkPicker)
    {
        ArgumentNullException.ThrowIfNull(artworkPicker);
        ArtworkPicker = artworkPicker;
    }

    public GameArtworkPickerViewModel ArtworkPicker { get; }

    [RelayCommand]
    private void ApplySelectedArtwork()
    {
        if (ArtworkPicker.CanApplyActiveArtwork && ArtworkPicker.SelectedActiveArtworkOption is { } option)
        {
            Complete(option);
        }
    }

    [RelayCommand]
    private void ClosePicker() => Cancel();
}
