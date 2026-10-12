using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.ComponentModel;
using Hatband.Core.Abstractions.Games;
using Microsoft.Extensions.Logging;
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
    private VerticalAlignment _storeLogoVerticalAlignment = VerticalAlignment.Bottom;
    private bool _showCoverTitles = true;
    private LibraryTitleModeOption _libraryTitleMode = LibraryTitleModeOption.None;
    private FontFamily _titleFontFamily = LibraryTitleTypography.GetFontFamily(LibraryTitleFont.Cinema);
    private FontWeight _titleFontWeight = LibraryTitleTypography.GetFontWeight(LibraryTitleFont.Cinema);
    private int _titleRow;
    private int _carouselRow = 1;
    private IBrush _backgroundDimmingBrush = new SolidColorBrush(Color.FromArgb(36, 0, 0, 0));
    private readonly IGameSourceCatalog _sourceCatalog;
    private readonly ArtworkImageLoader _artworkImageLoader;
    private readonly ILogger<LibraryScreenViewModel> _logger;
    private CancellationTokenSource? _storeLogoLoadCancellation;
    private Bitmap? _selectedStoreLogoImage;
    private string? _selectedStoreLogoPath;
    private bool _showLibraryStoreLogos;
    private bool _isActive;
    private long _storeLogoLoadVersion;

    public LibraryScreenViewModel(
        LibrarySessionViewModel session,
        IGameSourceCatalog sourceCatalog,
        ArtworkImageLoader artworkImageLoader,
        ILogger<LibraryScreenViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sourceCatalog);
        ArgumentNullException.ThrowIfNull(artworkImageLoader);
        ArgumentNullException.ThrowIfNull(logger);
        Session = session;
        _sourceCatalog = sourceCatalog;
        _artworkImageLoader = artworkImageLoader;
        _logger = logger;
        Session.PropertyChanged += OnSessionPropertyChanged;
        AttachSelectedCard(Session.SelectedGameCard);
    }

    public LibrarySessionViewModel Session { get; }

    public VerticalAlignment LibraryAlignment => _libraryAlignment;
    public VerticalAlignment StoreLogoVerticalAlignment => _storeLogoVerticalAlignment;
    public bool ShowCoverTitles => _showCoverTitles;
    public LibraryTitleModeOption LibraryTitleMode => _libraryTitleMode;
    public bool IsTextTitleMode => _libraryTitleMode == LibraryTitleModeOption.Text;
    public bool IsLogoTitleMode => _libraryTitleMode == LibraryTitleModeOption.Logo;
    public bool HasVisibleSelectedGameLogo => IsLogoTitleMode && Session.SelectedGameCard?.LogoImage is not null;
    public bool ShowLibraryStoreLogos => _showLibraryStoreLogos;
    public Bitmap? SelectedStoreLogoImage => _selectedStoreLogoImage;
    public bool HasVisibleSelectedStoreLogo => _showLibraryStoreLogos && _selectedStoreLogoImage is not null;
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
        var storeLogoAlignment = settings.LibraryPosition switch
        {
            LibraryPosition.Top => VerticalAlignment.Top,
            LibraryPosition.Center => VerticalAlignment.Center,
            LibraryPosition.Bottom => VerticalAlignment.Bottom,
            _ => throw new ArgumentOutOfRangeException(nameof(settings))
        };

        SetProperty(ref _libraryAlignment, alignment, nameof(LibraryAlignment));
        SetProperty(ref _storeLogoVerticalAlignment, storeLogoAlignment, nameof(StoreLogoVerticalAlignment));
        SetProperty(ref _showCoverTitles, settings.ShowCoverTitles, nameof(ShowCoverTitles));
        SetProperty(ref _libraryTitleMode, settings.LibraryTitleMode, nameof(LibraryTitleMode));
        var showStoreLogosChanged = SetProperty(
            ref _showLibraryStoreLogos,
            settings.ShowLibraryStoreLogos,
            nameof(ShowLibraryStoreLogos));
        OnPropertyChanged(nameof(IsTextTitleMode));
        OnPropertyChanged(nameof(IsLogoTitleMode));
        OnPropertyChanged(nameof(HasVisibleSelectedGameLogo));
        OnPropertyChanged(nameof(HasVisibleSelectedStoreLogo));
        SetProperty(ref _titleFontFamily, LibraryTitleTypography.GetFontFamily(settings.TitleFont), nameof(TitleFontFamily));
        SetProperty(ref _titleFontWeight, LibraryTitleTypography.GetFontWeight(settings.TitleFont), nameof(TitleFontWeight));
        SetProperty(ref _titleRow, settings.LibraryPosition == LibraryPosition.Top ? 1 : 0, nameof(TitleRow));
        SetProperty(ref _carouselRow, settings.LibraryPosition == LibraryPosition.Top ? 0 : 1, nameof(CarouselRow));

        var alpha = (byte)Math.Round(settings.BackgroundDimmingPercent * 255d / 100);
        SetProperty(ref _backgroundDimmingBrush, new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0)), nameof(BackgroundDimmingBrush));
        if (showStoreLogosChanged)
        {
            RefreshSelectedStoreLogo();
        }
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
        _isActive = true;
        Session.RefreshSelectedGameLogo();
        RefreshSelectedStoreLogo();
    }

    protected override void OnDeactivated()
    {
        _isActive = false;
        Session.CancelSelectedGameLogoLoad();
        CancelStoreLogoLoad();
        _selectedStoreLogoPath = null;
        SetSelectedStoreLogoImage(null);
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(LibrarySessionViewModel.SelectedGameCard))
        {
            AttachSelectedCard(Session.SelectedGameCard);
            OnPropertyChanged(nameof(HasVisibleSelectedGameLogo));
            RefreshSelectedStoreLogo();
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

    private void RefreshSelectedStoreLogo()
    {
        if (!_isActive || !_showLibraryStoreLogos || Session.SelectedGameCard is not { } selectedCard)
        {
            CancelStoreLogoLoad();
            _selectedStoreLogoPath = null;
            SetSelectedStoreLogoImage(null);
            return;
        }

        var logoPath = _sourceCatalog.GetLogoPath(selectedCard.Game.SourceId);
        if (logoPath is null)
        {
            CancelStoreLogoLoad();
            _selectedStoreLogoPath = null;
            SetSelectedStoreLogoImage(null);
            return;
        }

        if (string.Equals(_selectedStoreLogoPath, logoPath, StringComparison.Ordinal))
        {
            return;
        }

        CancelStoreLogoLoad();
        SetSelectedStoreLogoImage(null);
        _selectedStoreLogoPath = logoPath;

        var version = _storeLogoLoadVersion;
        var cancellation = new CancellationTokenSource();
        _storeLogoLoadCancellation = cancellation;
        _ = LoadSelectedStoreLogoAsync(selectedCard.Game.Id, logoPath, version, cancellation.Token);
    }

    private async Task LoadSelectedStoreLogoAsync(
        Guid gameId,
        string logoPath,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            var image = await _artworkImageLoader.LoadAsync(logoPath, cancellationToken);
            if (cancellationToken.IsCancellationRequested || version != _storeLogoLoadVersion ||
                Session.SelectedGameCard?.Game.Id != gameId)
            {
                image?.Dispose();
                return;
            }

            SetSelectedStoreLogoImage(image);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not load the store logo for game {GameId}.", gameId);
        }
        finally
        {
            if (_storeLogoLoadCancellation is { } cancellation && cancellation.Token == cancellationToken)
            {
                _storeLogoLoadCancellation = null;
                cancellation.Dispose();
            }
        }
    }

    private void CancelStoreLogoLoad()
    {
        _storeLogoLoadVersion++;
        _storeLogoLoadCancellation?.Cancel();
        _storeLogoLoadCancellation?.Dispose();
        _storeLogoLoadCancellation = null;
    }

    private void SetSelectedStoreLogoImage(Bitmap? image)
    {
        var previousImage = _selectedStoreLogoImage;
        if (!SetProperty(ref _selectedStoreLogoImage, image, nameof(SelectedStoreLogoImage)))
        {
            return;
        }

        OnPropertyChanged(nameof(HasVisibleSelectedStoreLogo));
        previousImage?.Dispose();
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
