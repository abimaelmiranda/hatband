using Avalonia.Layout;
using Avalonia.Media;
using Hatband.App.Localization;
using Hatband.App.Navigation;
using Hatband.App.Services;
using Hatband.App.ViewModels.Navigation;
using Hatband.Core.Models.Settings;

namespace Hatband.App.ViewModels;

/// <summary>
/// Presents the shared library and emits workflow requests without owning a separate navigation history.
/// </summary>
public sealed class LibraryScreenViewModel : ScreenViewModel
{
    private VerticalAlignment _libraryAlignment = VerticalAlignment.Bottom;
    private bool _showCoverTitles = true;
    private bool _showSelectedGameTitle;
    private FontFamily _titleFontFamily = LibraryTitleTypography.GetFontFamily(LibraryTitleFont.Cinema);
    private FontWeight _titleFontWeight = LibraryTitleTypography.GetFontWeight(LibraryTitleFont.Cinema);
    private int _titleRow;
    private int _carouselRow = 1;
    private IBrush _backgroundDimmingBrush = new SolidColorBrush(Color.FromArgb(36, 0, 0, 0));

    public LibraryScreenViewModel(LibrarySessionViewModel session)
    {
        Session = session;
    }

    public LibrarySessionViewModel Session { get; }

    public VerticalAlignment LibraryAlignment => _libraryAlignment;
    public bool ShowCoverTitles => _showCoverTitles;
    public bool ShowSelectedGameTitle => _showSelectedGameTitle;
    public FontFamily TitleFontFamily => _titleFontFamily;
    public FontWeight TitleFontWeight => _titleFontWeight;
    public int TitleRow => _titleRow;
    public int CarouselRow => _carouselRow;
    public IBrush BackgroundDimmingBrush => _backgroundDimmingBrush;

    public void ApplyAppearance(AppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var alignment = settings.LibraryPosition switch
        {
            LibraryPosition.Top => VerticalAlignment.Top,
            LibraryPosition.Center => VerticalAlignment.Center,
            LibraryPosition.Bottom => VerticalAlignment.Bottom,
            _ => throw new ArgumentOutOfRangeException(nameof(settings))
        };

        SetProperty(ref _libraryAlignment, alignment, nameof(LibraryAlignment));
        SetProperty(ref _showCoverTitles, settings.ShowCoverTitles, nameof(ShowCoverTitles));
        SetProperty(ref _showSelectedGameTitle, settings.ShowSelectedGameTitle, nameof(ShowSelectedGameTitle));
        SetProperty(ref _titleFontFamily, LibraryTitleTypography.GetFontFamily(settings.TitleFont), nameof(TitleFontFamily));
        SetProperty(ref _titleFontWeight, LibraryTitleTypography.GetFontWeight(settings.TitleFont), nameof(TitleFontWeight));
        SetProperty(ref _titleRow, settings.LibraryPosition == LibraryPosition.Top ? 1 : 0, nameof(TitleRow));
        SetProperty(ref _carouselRow, settings.LibraryPosition == LibraryPosition.Top ? 0 : 1, nameof(CarouselRow));

        var alpha = (byte)Math.Round(settings.BackgroundDimmingPercent * 255d / 100);
        SetProperty(ref _backgroundDimmingBrush, new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0)), nameof(BackgroundDimmingBrush));
    }

    public override IReadOnlyList<InputHint> InputHints =>
    [
        new(NavigationAction.Up, Resources.InputHintNavigate),
        new(NavigationAction.Confirm, Resources.InputHintOpen),
        new(NavigationAction.OpenMenu, Resources.InputHintMenu)
    ];

    public event Action<GameCardViewModel>? GameOpened;
    public event Action<MenuAction>? MenuActionRequested;

    public void OpenSelectedGame()
    {
        if (Session.SelectedGameCard is { } game)
        {
            GameOpened?.Invoke(game);
        }
    }

    public void RequestMenuAction(MenuAction action)
    {
        MenuActionRequested?.Invoke(action);
    }
}
