using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Hatband.App.ViewModels;

public sealed class AddGameSectionViewModel : ObservableObject
{
    public AddGameSectionViewModel(string id, string title, FluentIconGlyph iconGlyph)
    {
        Id = id;
        Title = title;
        IconGlyph = iconGlyph;
    }

    public string Id { get; }

    public string Title { get; }

    public FluentIconGlyph IconGlyph { get; }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(Background));
                OnPropertyChanged(nameof(SelectionBorderBrush));
            }
        }
    }

    public IBrush Background => IsSelected
        ? new SolidColorBrush(Color.Parse("#263640"))
        : Brushes.Transparent;

    public IBrush SelectionBorderBrush => IsSelected
        ? new SolidColorBrush(Color.Parse("#72D9FF"))
        : Brushes.Transparent;
}
