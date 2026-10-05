using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.App.Services;
using Hatband.Core.Extensions;

namespace Hatband.App.ViewModels;

public partial class GameMetadataEditorViewModel : ViewModelBase
{
    private readonly IGameRepository gameRepository;
    private readonly IGameArtworkStorage artworkStorage;
    private readonly IReadOnlyList<IGameMetadataProvider> metadataProviders;
    private Game? game;
    private GameArtworkImage? selectedCoverImage;
    private GameArtworkImage? selectedBackgroundImage;
    private bool restoreCover;
    private bool restoreBackground;
    private GamePlatform? selectedNativePlatforms;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Developer { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Publisher { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Genre { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReleaseDate { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial string? CoverArtworkSelection { get; set; }

    [ObservableProperty]
    public partial string? BackgroundArtworkSelection { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<GameMetadataSourceViewModel> MetadataSources { get; set; } = [];

    [ObservableProperty]
    public partial GameMetadataSourceViewModel? SelectedMetadataSource { get; set; }

    public GameMetadataEditorViewModel(
        IGameRepository gameRepository,
        IGameArtworkStorage artworkStorage,
        IEnumerable<IGameMetadataProvider> metadataProviders,
        IEnumerable<IGameArtworkProvider> artworkProviders,
        ArtworkImageLoader artworkImageLoader)
    {
        ArgumentNullException.ThrowIfNull(gameRepository);
        ArgumentNullException.ThrowIfNull(artworkStorage);
        ArgumentNullException.ThrowIfNull(metadataProviders);
        this.gameRepository = gameRepository;
        this.artworkStorage = artworkStorage;
        this.metadataProviders = metadataProviders.ToArray();
        ArtworkPicker = new GameArtworkPickerViewModel(artworkProviders, artworkImageLoader);
        ArtworkPicker.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(GameArtworkPickerViewModel.IsArtworkPickerOpen))
            {
                OnPropertyChanged(nameof(IsArtworkPickerOpen));
            }
        };
    }

    public GameArtworkPickerViewModel ArtworkPicker { get; }

    public event EventHandler<Game>? Saved;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasMetadataSources => MetadataSources.Count > 0;

    public bool HasNoMetadataSources => !HasMetadataSources;

    public bool IsArtworkPickerOpen => ArtworkPicker.IsArtworkPickerOpen;

    public bool CanRestoreCover => game?.DefaultArtwork?.CoverImagePath is not null;

    public bool CanRestoreBackground => game?.DefaultArtwork?.BackgroundImagePath is not null;

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnNameChanged(string value) => ArtworkPicker.InvalidateSearch();

    partial void OnMetadataSourcesChanged(ObservableCollection<GameMetadataSourceViewModel> value)
    {
        OnPropertyChanged(nameof(HasMetadataSources));
        OnPropertyChanged(nameof(HasNoMetadataSources));
    }

    public void Load(Game selectedGame, string languageTag)
    {
        ArgumentNullException.ThrowIfNull(selectedGame);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        game = selectedGame;
        selectedNativePlatforms = null;
        Name = selectedGame.Name;
        Description = selectedGame.Metadata.Description ?? string.Empty;
        Developer = selectedGame.Metadata.Developer ?? string.Empty;
        Publisher = selectedGame.Metadata.Publisher ?? string.Empty;
        Genre = selectedGame.Metadata.Genre ?? string.Empty;
        ReleaseDate = selectedGame.Metadata.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
        selectedCoverImage = null;
        selectedBackgroundImage = null;
        restoreCover = false;
        restoreBackground = false;
        CoverArtworkSelection = selectedGame.Artwork.CoverImagePath is { } cover ? Path.GetFileName(cover) : null;
        BackgroundArtworkSelection = selectedGame.Artwork.BackgroundImagePath is { } background ? Path.GetFileName(background) : null;
        OnPropertyChanged(nameof(CanRestoreCover));
        OnPropertyChanged(nameof(CanRestoreBackground));
        ArtworkPicker.Initialize(selectedGame, languageTag);
        ErrorMessage = null;
        var region = GetRegion(languageTag);
        MetadataSources = new ObservableCollection<GameMetadataSourceViewModel>(metadataProviders
            .Where(provider => provider.CanSearch(selectedGame))
            .OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(provider =>
            {
                var source = new GameMetadataSourceViewModel(provider, ApplyMetadata);
                source.Initialize(selectedGame, languageTag, region);
                return source;
            }));
        SelectedMetadataSource = MetadataSources.FirstOrDefault();
    }

    public Task OpenArtworkPickerAsync(GameArtworkSlot slot, CancellationToken cancellationToken = default) =>
        ArtworkPicker.OpenAsync(slot, Name, cancellationToken);

    public void CloseArtworkPicker() => ArtworkPicker.Close();

    public void ApplySelectedArtwork()
    {
        if (ArtworkPicker.SelectedActiveArtworkOption is not { } option)
        {
            return;
        }

        switch (ArtworkPicker.ActiveArtworkSlot)
        {
            case GameArtworkSlot.Cover:
                selectedCoverImage = option.Image;
                CoverArtworkSelection = option.DisplayName;
                restoreCover = false;
                break;
            case GameArtworkSlot.Background:
                selectedBackgroundImage = option.Image;
                BackgroundArtworkSelection = option.DisplayName;
                restoreBackground = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ArtworkPicker.ActiveArtworkSlot));
        }

        ArtworkPicker.Close();
    }

    public void RestoreArtwork(GameArtworkSlot slot)
    {
        switch (slot)
        {
            case GameArtworkSlot.Cover:
                selectedCoverImage = null;
                CoverArtworkSelection = game?.DefaultArtwork?.CoverImagePath is { } cover ? Path.GetFileName(cover) : null;
                restoreCover = true;
                break;
            case GameArtworkSlot.Background:
                selectedBackgroundImage = null;
                BackgroundArtworkSelection = game?.DefaultArtwork?.BackgroundImagePath is { } background ? Path.GetFileName(background) : null;
                restoreBackground = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    public Task SearchMetadataSourcesAsync(CancellationToken cancellationToken = default) =>
        SelectedMetadataSource?.SearchAsync(cancellationToken) ?? Task.CompletedTask;

    private void ApplyMetadata(GameMetadata metadata)
    {
        selectedNativePlatforms = metadata.NativePlatforms;
        if (!string.IsNullOrWhiteSpace(metadata.StoreName)) Name = metadata.StoreName;
        if (!string.IsNullOrWhiteSpace(metadata.Description)) Description = metadata.Description;
        if (!string.IsNullOrWhiteSpace(metadata.Developer)) Developer = metadata.Developer;
        if (!string.IsNullOrWhiteSpace(metadata.Publisher)) Publisher = metadata.Publisher;
        if (!string.IsNullOrWhiteSpace(metadata.Genre)) Genre = metadata.Genre;
        if (metadata.ReleaseDate is DateOnly releaseDate) ReleaseDate = releaseDate.ToIsoDateString();
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (game is null || IsSaving) return;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = Resources.EnterGameName;
            return;
        }

        if (!TryParseReleaseDate(ReleaseDate, out var releaseDate))
        {
            ErrorMessage = Resources.InvalidReleaseDate;
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var metadata = game.Metadata with
            {
                Description = Normalize(Description),
                Developer = Normalize(Developer),
                Publisher = Normalize(Publisher),
                Genre = Normalize(Genre),
                ReleaseDate = releaseDate,
                NativePlatforms = selectedNativePlatforms ?? game.Metadata.NativePlatforms
            };
            var artwork = await ImportSelectedArtworkAsync(game, cancellationToken);
            game.Name = Name.Trim();
            game.Metadata = metadata;
            game.Artwork = artwork;
            await gameRepository.UpdateAsync(game, cancellationToken);
            Saved?.Invoke(this, game);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, Resources.SaveGameError, exception.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task<GameArtwork> ImportSelectedArtworkAsync(Game selectedGame, CancellationToken cancellationToken)
    {
        var current = selectedGame.Artwork;
        var images = new List<GameArtworkImage>();
        if (selectedCoverImage is not null) images.Add(selectedCoverImage);
        if (selectedBackgroundImage is not null) images.Add(selectedBackgroundImage);
        var stored = images.Count == 0
            ? current
            : await artworkStorage.StoreAsync(selectedGame.Id, images, current, cancellationToken);
        return stored with
        {
            CoverImagePath = restoreCover ? selectedGame.DefaultArtwork?.CoverImagePath : stored.CoverImagePath,
            BackgroundImagePath = restoreBackground ? selectedGame.DefaultArtwork?.BackgroundImagePath : stored.BackgroundImagePath
        };
    }

    [RelayCommand]
    private void Cancel() => ErrorMessage = null;

    private static string? Normalize(string value)
    {
        var trimmedValue = value.Trim();
        return trimmedValue.Length == 0 ? null : trimmedValue;
    }

    private static bool TryParseReleaseDate(string value, out DateOnly? releaseDate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            releaseDate = null;
            return true;
        }

        if (value.TryParseIsoDate(out var parsedDate))
        {
            releaseDate = parsedDate;
            return true;
        }

        releaseDate = null;
        return false;
    }

    private static string GetRegion(string languageTag) =>
        new RegionInfo(CultureInfo.GetCultureInfo(languageTag).Name).TwoLetterISORegionName;
}
