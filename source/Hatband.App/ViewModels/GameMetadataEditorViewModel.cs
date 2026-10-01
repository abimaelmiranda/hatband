using System.Globalization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.App.Services;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums.Artwork;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Extensions;
using Hatband.Core.Models;

namespace Hatband.App.ViewModels;

public partial class GameMetadataEditorViewModel : ViewModelBase
{
    private readonly IGameLibraryService gameLibraryService;
    private readonly IGameArtworkStorage artworkStorage;
    private readonly IReadOnlyList<IGameMetadataSearchProvider> metadataSearchProviders;
    private Game? game;
    private GameMetadata initialMetadata = new();
    private string initialName = string.Empty;
    private string? selectedCoverArtworkUrl;
    private string? selectedBackgroundArtworkUrl;
    private bool restoreCover;
    private bool restoreBackground;
    private bool restoreDescription;
    private bool restoreDeveloper;
    private bool restorePublisher;
    private bool restoreGenre;
    private bool restoreReleaseDate;
    private bool restoreName;

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
        IGameLibraryService gameLibraryService,
        IGameArtworkStorage artworkStorage,
        IEnumerable<IGameMetadataSearchProvider> metadataSearchProviders,
        IEnumerable<IGameArtworkSearchProvider> artworkSearchProviders,
        ArtworkImageLoader artworkImageLoader)
    {
        this.gameLibraryService = gameLibraryService;
        this.artworkStorage = artworkStorage;
        this.metadataSearchProviders = metadataSearchProviders.ToArray();
        ArtworkPicker = new GameArtworkPickerViewModel(artworkSearchProviders, artworkImageLoader);
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

    public bool CanRestoreDescription => game?.Metadata.Overrides.Description == true && !restoreDescription;

    public bool CanRestoreDeveloper => game?.Metadata.Overrides.Developer == true && !restoreDeveloper;

    public bool CanRestorePublisher => game?.Metadata.Overrides.Publisher == true && !restorePublisher;

    public bool CanRestoreGenre => game?.Metadata.Overrides.Genre == true && !restoreGenre;

    public bool CanRestoreReleaseDate => game?.Metadata.Overrides.ReleaseDate == true && !restoreReleaseDate;

    public bool CanRestoreName => game is { IsNameCustomized: true } &&
                                  !string.IsNullOrWhiteSpace(game.Metadata.StoreName) &&
                                  !restoreName;

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnNameChanged(string value)
    {
        ArtworkPicker.InvalidateSearch();
    }

    partial void OnMetadataSourcesChanged(ObservableCollection<GameMetadataSourceViewModel> value)
    {
        OnPropertyChanged(nameof(HasMetadataSources));
        OnPropertyChanged(nameof(HasNoMetadataSources));
    }

    public void Load(Game selectedGame, string languageTag)
    {
        game = selectedGame;
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        initialName = selectedGame.Name;
        initialMetadata = selectedGame.Metadata;

        Name = selectedGame.Name;
        Description = selectedGame.Metadata.Description ?? string.Empty;
        Developer = selectedGame.Metadata.Developer ?? string.Empty;
        Publisher = selectedGame.Metadata.Publisher ?? string.Empty;
        Genre = selectedGame.Metadata.Genre ?? string.Empty;
        ReleaseDate = selectedGame.Metadata.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? string.Empty;
        selectedCoverArtworkUrl = null;
        selectedBackgroundArtworkUrl = null;
        restoreCover = false;
        restoreBackground = false;
        restoreDescription = false;
        restoreDeveloper = false;
        restorePublisher = false;
        restoreGenre = false;
        restoreReleaseDate = false;
        restoreName = false;
        OnPropertyChanged(nameof(CanRestoreDescription));
        OnPropertyChanged(nameof(CanRestoreDeveloper));
        OnPropertyChanged(nameof(CanRestorePublisher));
        OnPropertyChanged(nameof(CanRestoreGenre));
        OnPropertyChanged(nameof(CanRestoreReleaseDate));
        OnPropertyChanged(nameof(CanRestoreName));
        CoverArtworkSelection = selectedGame.Metadata.Artwork.CoverImagePath is string coverPath
            ? Path.GetFileName(coverPath)
            : null;
        BackgroundArtworkSelection = selectedGame.Metadata.Artwork.BackgroundImagePath is string backgroundPath
            ? Path.GetFileName(backgroundPath)
            : null;
        ArtworkPicker.Initialize(selectedGame, languageTag);
        ErrorMessage = null;
        var lookupRequest = CreateLookupRequest(selectedGame, selectedGame.Name);
        MetadataSources = new ObservableCollection<GameMetadataSourceViewModel>(metadataSearchProviders
            .Where(provider => provider.CanSearch(lookupRequest))
            .OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(provider =>
            {
                var source = new GameMetadataSourceViewModel(provider, ApplyMetadata);
                source.Initialize(lookupRequest, languageTag, GetRegion(languageTag));
                return source;
            }));
        SelectedMetadataSource = MetadataSources.FirstOrDefault();
    }

    public Task OpenArtworkPickerAsync(GameArtworkSlot slot, CancellationToken cancellationToken = default)
    {
        return ArtworkPicker.OpenAsync(slot, Name, cancellationToken);
    }

    public void CloseArtworkPicker()
    {
        ArtworkPicker.Close();
    }

    public void ApplySelectedArtwork()
    {
        if (ArtworkPicker.SelectedActiveArtworkOption is not { } selectedOption)
        {
            return;
        }

        switch (ArtworkPicker.ActiveArtworkSlot)
        {
            case GameArtworkSlot.Cover:
                selectedCoverArtworkUrl = selectedOption.Url;
                CoverArtworkSelection = selectedOption.DisplayName;
                restoreCover = false;
                break;
            case GameArtworkSlot.Background:
                selectedBackgroundArtworkUrl = selectedOption.Url;
                BackgroundArtworkSelection = selectedOption.DisplayName;
                restoreBackground = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ArtworkPicker.ActiveArtworkSlot));
        }

        ArtworkPicker.Close();
    }

    public void RestoreMetadataFromStore(string fieldName)
    {
        if (fieldName == "Name")
        {
            if (game?.Metadata.StoreName is not string storeName)
            {
                return;
            }

            Name = storeName;
            restoreName = true;
            OnPropertyChanged(nameof(CanRestoreName));
            return;
        }

        switch (fieldName)
        {
            case "Description":
                Description = string.Empty;
                restoreDescription = true;
                OnPropertyChanged(nameof(CanRestoreDescription));
                break;
            case "Developer":
                Developer = string.Empty;
                restoreDeveloper = true;
                OnPropertyChanged(nameof(CanRestoreDeveloper));
                break;
            case "Publisher":
                Publisher = string.Empty;
                restorePublisher = true;
                OnPropertyChanged(nameof(CanRestorePublisher));
                break;
            case "Genre":
                Genre = string.Empty;
                restoreGenre = true;
                OnPropertyChanged(nameof(CanRestoreGenre));
                break;
            case "ReleaseDate":
                ReleaseDate = string.Empty;
                restoreReleaseDate = true;
                OnPropertyChanged(nameof(CanRestoreReleaseDate));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, "Unknown metadata field.");
        }
    }

    public void RestoreArtwork(GameArtworkSlot slot)
    {
        switch (slot)
        {
            case GameArtworkSlot.Cover:
                selectedCoverArtworkUrl = null;
                CoverArtworkSelection = null;
                restoreCover = true;
                break;
            case GameArtworkSlot.Background:
                selectedBackgroundArtworkUrl = null;
                BackgroundArtworkSelection = null;
                restoreBackground = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    public async Task SearchMetadataSourcesAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedMetadataSource is not null)
        {
            await SelectedMetadataSource.SearchIfNeededAsync(cancellationToken);
        }
    }

    private void ApplyMetadata(string gameName, GameMetadata metadata)
    {
        Name = string.IsNullOrWhiteSpace(metadata.StoreName) ? gameName : metadata.StoreName;
        if (!string.IsNullOrWhiteSpace(metadata.Description))
        {
            Description = metadata.Description;
        }

        if (!string.IsNullOrWhiteSpace(metadata.Developer))
        {
            Developer = metadata.Developer;
        }

        if (!string.IsNullOrWhiteSpace(metadata.Publisher))
        {
            Publisher = metadata.Publisher;
        }

        if (!string.IsNullOrWhiteSpace(metadata.Genre))
        {
            Genre = metadata.Genre;
        }

        if (metadata.ReleaseDate is DateOnly releaseDate)
        {
            ReleaseDate = releaseDate.ToIsoDateString();
        }
    }

    private static GameSourceLookupRequest CreateLookupRequest(Game selectedGame, string gameName)
    {
        return new GameSourceLookupRequest
        {
            GameName = gameName,
            SourceId = selectedGame.SourceId,
            SourceGameId = selectedGame.SourceGameId
        };
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (game is null || IsSaving)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = Resources.EnterGameName;
            return;
        }

        if (!TryParseReleaseDate(ReleaseDate, out var parsedReleaseDate))
        {
            ErrorMessage = Resources.InvalidReleaseDate;
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var artwork = await ImportSelectedArtworkAsync(game, cancellationToken);
            var metadata = game.Metadata with
            {
                Description = Normalize(Description),
                Developer = Normalize(Developer),
                Publisher = Normalize(Publisher),
                Genre = Normalize(Genre),
                ReleaseDate = parsedReleaseDate,
                Artwork = artwork,
                Overrides = game.Metadata.Overrides with
                {
                    Description = ShouldKeepManualTextOverride(
                        Description,
                        initialMetadata.Description,
                        game.Metadata.Overrides.Description,
                        restoreDescription),
                    Developer = ShouldKeepManualTextOverride(
                        Developer,
                        initialMetadata.Developer,
                        game.Metadata.Overrides.Developer,
                        restoreDeveloper),
                    Publisher = ShouldKeepManualTextOverride(
                        Publisher,
                        initialMetadata.Publisher,
                        game.Metadata.Overrides.Publisher,
                        restorePublisher),
                    Genre = ShouldKeepManualTextOverride(
                        Genre,
                        initialMetadata.Genre,
                        game.Metadata.Overrides.Genre,
                        restoreGenre),
                    ReleaseDate = ShouldKeepManualDateOverride(
                        ReleaseDate,
                        parsedReleaseDate,
                        initialMetadata.ReleaseDate,
                        game.Metadata.Overrides.ReleaseDate,
                        restoreReleaseDate)
                }
            };
            var updatedName = Name.Trim();
            var isNameCustomized = !string.Equals(updatedName, initialName, StringComparison.Ordinal) ||
                                   game.IsNameCustomized;
            if (restoreName && string.Equals(updatedName, game.Metadata.StoreName, StringComparison.Ordinal))
            {
                isNameCustomized = false;
            }

            await gameLibraryService.UpdateGameDetailsAsync(
                game.Id,
                updatedName,
                isNameCustomized,
                metadata,
                cancellationToken);

            game.Name = updatedName;
            game.IsNameCustomized = isNameCustomized;
            game.Metadata = metadata;
            OnPropertyChanged(nameof(CanRestoreName));
            Saved?.Invoke(this, game);
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

    private async Task<GameArtwork> ImportSelectedArtworkAsync(
        Game currentGame,
        CancellationToken cancellationToken)
    {
        var artwork = currentGame.Metadata.Artwork;

        if (selectedCoverArtworkUrl is not null || selectedBackgroundArtworkUrl is not null)
        {
            var sources = new GameArtworkSources
            {
                CoverImageUrls = selectedCoverArtworkUrl is null ? Array.Empty<string>() : [selectedCoverArtworkUrl],
                BackgroundImageUrls = selectedBackgroundArtworkUrl is null ? Array.Empty<string>() : [selectedBackgroundArtworkUrl]
            };
            var artworkForStorage = artwork with
            {
                CoverImagePath = selectedCoverArtworkUrl is null ? artwork.CoverImagePath : null,
                BackgroundImagePath = selectedBackgroundArtworkUrl is null ? artwork.BackgroundImagePath : null,
            };
            artwork = await artworkStorage.StoreAsync(currentGame.Id, sources, artworkForStorage, cancellationToken);
            if (selectedCoverArtworkUrl is not null)
            {
                if (artwork.CoverImagePath is null)
                {
                    throw new InvalidOperationException(Resources.ArtworkDownloadFailed);
                }

                artwork = artwork with { IsCoverCustomized = true };
            }

            if (selectedBackgroundArtworkUrl is not null)
            {
                if (artwork.BackgroundImagePath is null)
                {
                    throw new InvalidOperationException(Resources.ArtworkDownloadFailed);
                }

                artwork = artwork with { IsBackgroundCustomized = true };
            }

        }

        if (restoreCover)
        {
            artwork = artwork with { CoverImagePath = null, IsCoverCustomized = false };
        }

        if (restoreBackground)
        {
            artwork = artwork with { BackgroundImagePath = null, IsBackgroundCustomized = false };
        }

        return artwork;
    }

    [RelayCommand]
    private void Cancel()
    {
        ErrorMessage = null;
    }

    private static string? Normalize(string value)
    {
        var trimmedValue = value.Trim();
        return trimmedValue.Length == 0 ? null : trimmedValue;
    }

    private static bool IsChanged(string value, string? originalValue)
    {
        return !string.Equals(Normalize(value), originalValue, StringComparison.Ordinal);
    }

    private static bool ShouldKeepManualTextOverride(
        string currentValue,
        string? initialValue,
        bool existingOverride,
        bool restoringFromStore)
    {
        if (restoringFromStore && string.IsNullOrWhiteSpace(currentValue))
        {
            return false;
        }

        return IsChanged(currentValue, initialValue) || existingOverride;
    }

    private static bool ShouldKeepManualDateOverride(
        string currentValue,
        DateOnly? parsedValue,
        DateOnly? initialValue,
        bool existingOverride,
        bool restoringFromStore)
    {
        if (restoringFromStore && string.IsNullOrWhiteSpace(currentValue))
        {
            return false;
        }

        return parsedValue != initialValue || existingOverride;
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

    private static string GetRegion(string languageTag)
    {
        var culture = CultureInfo.GetCultureInfo(languageTag);
        var region = new RegionInfo(culture.Name);
        return region.TwoLetterISORegionName;
    }
}
