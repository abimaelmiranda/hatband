using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.Navigation;
using Hatband.App.ViewModels;
using Hatband.Core.Enums.Artwork;

namespace Hatband.App.Views.Components;

/// <summary>Displays artwork search results as a reusable horizontal carousel.</summary>
public partial class ArtworkPickerCarouselView : UserControl
{
    public ArtworkPickerCarouselView()
    {
        InitializeComponent();
    }

    public bool IsArtworkOptionFocused => GetActiveArtworkList() is { } activeList &&
        (activeList.IsFocused || activeList.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Any(item => item.IsFocused));

    public bool HasActiveArtworkList => GetActiveArtworkList() is not null;

    public void FocusSelectedArtworkOption()
    {
        var activeList = GetActiveArtworkList();
        if (activeList is null)
        {
            return;
        }

        DirectionalFocusNavigator.Focus(activeList);
        Dispatcher.UIThread.Post(() =>
        {
            if (activeList.SelectedItem is not GameArtworkSourceOption selectedOption)
            {
                return;
            }

            var selectedItem = activeList.GetVisualDescendants()
                .OfType<ListBoxItem>()
                .FirstOrDefault(item => ReferenceEquals(item.DataContext, selectedOption));
            if (selectedItem is not null)
            {
                DirectionalFocusNavigator.Focus(selectedItem);
            }
        });
    }

    private ListBox? GetActiveArtworkList()
    {
        if (DataContext is not GameArtworkPickerViewModel viewModel ||
            !viewModel.IsArtworkPickerOpen)
        {
            return null;
        }

        var activeList = viewModel.ActiveArtworkSlot switch
        {
            GameArtworkSlot.Cover => CoverArtworkOptions,
            GameArtworkSlot.Background => BackgroundArtworkOptions,
            GameArtworkSlot.Icon => IconArtworkOptions,
            GameArtworkSlot.Logo => LogoArtworkOptions,
            _ => throw new ArgumentOutOfRangeException(nameof(viewModel.ActiveArtworkSlot))
        };

        return activeList.IsEffectivelyVisible ? activeList : null;
    }

    private void OnArtworkOptionGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not ListBox list || e.Source is not ListBoxItem { DataContext: GameArtworkSourceOption option })
        {
            return;
        }

        list.SelectedItem = option;
    }
}
