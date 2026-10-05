using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Hatband.App.ViewModels;

public partial class MenuOptionViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public MenuOptionViewModel(string title, MenuAction action)
    {
        Title = title;
        Action = action;
    }

    public string Title { get; private set; }

    public MenuAction Action { get; }

    public FluentIconGlyph IconGlyph => GetIconGlyph(Action);

    public IBrush HighlightBrush => IsSelected
        ? new SolidColorBrush(Color.Parse("#263640"))
        : Brushes.Transparent;

    public IBrush SelectionBorderBrush => IsSelected
        ? new SolidColorBrush(Color.Parse("#72D9FF"))
        : Brushes.Transparent;

    public void UpdateTitle(string title)
    {
        if (Title == title)
        {
            return;
        }

        Title = title;
        OnPropertyChanged(nameof(Title));
    }

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(HighlightBrush));
        OnPropertyChanged(nameof(SelectionBorderBrush));
    }

    private static FluentIconGlyph GetIconGlyph(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.Library:
            case MenuAction.HiddenGames:
                return FluentIconGlyph.Games;
            case MenuAction.AddGame:
                return FluentIconGlyph.Add;
            case MenuAction.Settings:
                return FluentIconGlyph.Settings;
            case MenuAction.Exit:
                return FluentIconGlyph.Power;
            default:
                return FluentIconGlyph.Apps;
        }
    }
}
