using Avalonia.Layout;
using Avalonia.Media;
using System.ComponentModel;
using Hatband.App.Localization;
using Hatband.App.Navigation;
using Hatband.App.Services;
using Hatband.App.ViewModels.Navigation;
using Hatband.Core.Models.Settings;
using LibraryTitleModeOption = Hatband.Core.Models.Settings.LibraryTitleMode;

namespace Hatband.App.ViewModels;

/// <summary>
/// Presents the shared library and emits workflow requests without owning a separate navigation history.
/// </summary>
public sealed class LibraryScreenViewModel : ScreenViewModel
{
    private VerticalAlignment _libraryAlignment = VerticalAlignment.Bottom;
    private bool _showCoverTitles = true;
    private LibraryTitleModeOption _libraryTitleMode = LibraryTitleModeOption.None;
    private FontFamily _titleFontFamily = LibraryTitleTypography.GetFontFamily(LibraryTitleFont.Cinema);
    private FontWeight _titleFontWeight = LibraryTitleTypography.GetFontWeight(LibraryTitleFont.Cinema);
    private int _titleRow;
    private int _carouselRow = 1;
    private IBrush _backgroundDimmingBrush = new SolidColorBrush(Color.FromArgb(36, 0, 0, 0));

    public LibraryScreenViewModel(LibrarySessionViewModel session)
    {
        Session = session;
        Session.PropertyChanged += OnSessionPropertyChanged;
        AttachSelectedCard(Session.SelectedGameCard);
    }

    public LibrarySessionViewModel Session { get; }

    public VerticalAlignment LibraryAlignment => _libraryAlignment;
    public bool ShowCoverTitles => _showCoverTitles;
    public LibraryTitleModeOption LibraryTitleMode => _libraryTitleMode;
    public bool IsTextTitleMode => _libraryTitleMode == LibraryTitleModeOption.Text;
    public bool IsLogoTitleMode => _libraryTitleMode == LibraryTitleModeOption.Logo;
    public bool HasVisibleSelectedGameLogo => IsLogoTitleMode && Session.SelectedGameCard?.LogoImage is not null;
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
        SetProperty(ref _libraryTitleMode, settings.LibraryTitleMode, nameof(LibraryTitleMode));
        OnPropertyChanged(nameof(IsTextTitleMode));
        OnPropertyChanged(nameof(IsLogoTitleMode));
        OnPropertyChanged(nameof(HasVisibleSelectedGameLogo));
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

    protected override void OnActivated()
    {
        Session.RefreshSelectedGameLogo();
    }

    protected override void OnDeactivated()
    {
        Session.CancelSelectedGameLogoLoad();
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(LibrarySessionViewModel.SelectedGameCard))
        {
            AttachSelectedCard(Session.SelectedGameCard);
            OnPropertyChanged(nameof(HasVisibleSelectedGameLogo));
        }
    }

    private void AttachSelectedCard(GameCardViewModel? card)
    {
        if (selectedCard is not null)
        {
            selectedCard.PropertyChanged -= OnSelectedCardPropertyChanged;
        }

        selectedCard = card;
        if (selectedCard is not null)
        {
            selectedCard.PropertyChanged += OnSelectedCardPropertyChanged;
        }
    }

    private GameCardViewModel? selectedCard;

    private void OnSelectedCardPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(GameCardViewModel.LogoImage))
        {
            OnPropertyChanged(nameof(HasVisibleSelectedGameLogo));
        }
    }

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
