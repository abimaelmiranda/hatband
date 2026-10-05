using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.Localization;
using Hatband.App.Services;
using Hatband.Core.Enums.Stores;

namespace Hatband.App.ViewModels;

public partial class GameArtworkPickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<IGameArtworkProvider> artworkProviders;
    private readonly ArtworkImageLoader artworkImageLoader;
    private Game? game;
    private string? artworkOptionsSearchName;
    private string preferredLanguageTag = "en-US";
    private GameSourceId? _storeSourceId;
    private string? _storeGameId;
    private int searchRevision;

    [ObservableProperty]
    public partial ObservableCollection<GameArtworkSourceOption> CoverArtworkOptions { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<GameArtworkSourceOption> BackgroundArtworkOptions { get; set; } = [];

    [ObservableProperty]
    public partial string? ArtworkSourcesStatus { get; set; }

    [ObservableProperty]
    public partial bool IsArtworkPickerOpen { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingArtworkOptions { get; set; }

    [ObservableProperty]
    public partial GameArtworkSlot ActiveArtworkSlot { get; set; }

    [ObservableProperty]
    public partial GameArtworkSourceOption? SelectedActiveArtworkOption { get; set; }

    public GameArtworkPickerViewModel(IEnumerable<IGameArtworkProvider> artworkProviders, ArtworkImageLoader artworkImageLoader)
    {
        ArgumentNullException.ThrowIfNull(artworkProviders);
        ArgumentNullException.ThrowIfNull(artworkImageLoader);
        this.artworkProviders = artworkProviders.ToArray();
        this.artworkImageLoader = artworkImageLoader;
    }

    public IReadOnlyList<GameArtworkSourceOption> ActiveArtworkOptions => ActiveArtworkSlot switch
    {
        GameArtworkSlot.Cover => CoverArtworkOptions,
        GameArtworkSlot.Background => BackgroundArtworkOptions,
        _ => throw new ArgumentOutOfRangeException(nameof(ActiveArtworkSlot))
    };

    public bool IsCoverPickerActive => IsArtworkPickerOpen && ActiveArtworkSlot == GameArtworkSlot.Cover && !IsLoadingArtworkOptions && HasActiveArtworkOptions;

    public bool IsBackgroundPickerActive => IsArtworkPickerOpen && ActiveArtworkSlot == GameArtworkSlot.Background && !IsLoadingArtworkOptions && HasActiveArtworkOptions;

    public string ActiveArtworkTitle => ActiveArtworkSlot switch
    {
        GameArtworkSlot.Cover => Resources.Cover,
        GameArtworkSlot.Background => Resources.Background,
        _ => throw new ArgumentOutOfRangeException(nameof(ActiveArtworkSlot))
    };

    public bool HasArtworkSourcesStatus => !string.IsNullOrWhiteSpace(ArtworkSourcesStatus);

    public bool HasActiveArtworkOptions => ActiveArtworkOptions.Count > 0;

    public bool HasNoActiveArtworkOptions => !HasActiveArtworkOptions && !IsLoadingArtworkOptions;

    public bool HasArtworkOptions => CoverArtworkOptions.Count > 0 || BackgroundArtworkOptions.Count > 0;

    public bool CanApplyActiveArtwork => SelectedActiveArtworkOption is { IsPreviewLoaded: true, PreviewImage: not null };

    public void Initialize(Game selectedGame, string languageTag)
    {
        ArgumentNullException.ThrowIfNull(selectedGame);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        game = selectedGame;
        preferredLanguageTag = languageTag;
        _storeSourceId = selectedGame.Metadata.StoreSourceId;
        _storeGameId = selectedGame.Metadata.StoreGameId;
        InvalidateSearch();
        ArtworkSourcesStatus = null;
        IsArtworkPickerOpen = false;
        IsLoadingArtworkOptions = false;
    }

    public void InvalidateSearch()
    {
        searchRevision++;
        artworkOptionsSearchName = null;
        CoverArtworkOptions.Clear();
        BackgroundArtworkOptions.Clear();
        SelectedActiveArtworkOption = null;
        ArtworkSourcesStatus = null;
        OnPropertyChanged(nameof(ActiveArtworkOptions));
        OnPropertyChanged(nameof(HasActiveArtworkOptions));
        OnPropertyChanged(nameof(HasArtworkOptions));
        OnPropertyChanged(nameof(HasNoActiveArtworkOptions));
    }

    public void UpdateStoreReference(GameSourceId? storeSourceId, string? storeGameId)
    {
        if ((storeSourceId is null) != string.IsNullOrWhiteSpace(storeGameId))
        {
            throw new ArgumentException("A store source and game ID must be provided together.");
        }

        _storeSourceId = storeSourceId;
        _storeGameId = storeGameId;
        InvalidateSearch();
    }

    public async Task OpenAsync(GameArtworkSlot slot, string editedGameName, CancellationToken cancellationToken = default)
    {
        if (slot is not GameArtworkSlot.Cover and not GameArtworkSlot.Background)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        ActiveArtworkSlot = slot;
        ArtworkSourcesStatus = Resources.LoadingArtworkSources;
        IsLoadingArtworkOptions = true;
        IsArtworkPickerOpen = true;
        try
        {
            await LoadArtworkOptionsAsync(editedGameName.Trim(), cancellationToken);
            var activeOptions = GetActiveArtworkOptionsCollection();
            if (activeOptions.Count > 0)
            {
                ArtworkSourcesStatus = Resources.LoadingArtworkPreviews;
                await LoadArtworkPreviewsAsync(activeOptions, cancellationToken);
                RemoveUnavailableArtworkPreviews(activeOptions);
                SelectedActiveArtworkOption = activeOptions.FirstOrDefault();
                ArtworkSourcesStatus = HasActiveArtworkOptions ? null : Resources.NoUsableArtworkFound;
                if (!HasActiveArtworkOptions)
                {
                    artworkOptionsSearchName = null;
                }
            }
        }
        finally
        {
            IsLoadingArtworkOptions = false;
        }
    }

    public void Close() => IsArtworkPickerOpen = false;

    private async Task LoadArtworkOptionsAsync(string searchName, CancellationToken cancellationToken)
    {
        if (string.Equals(artworkOptionsSearchName, searchName, StringComparison.Ordinal))
        {
            ArtworkSourcesStatus = HasActiveArtworkOptions ? null : HasArtworkOptions
                ? Resources.NoUsableArtworkFound
                : Resources.NoArtworkSourcesFound;
            return;
        }

        var selectedGame = game ?? throw new InvalidOperationException("The game artwork picker has not been initialized.");
        var currentSearchRevision = searchRevision;
        var searchSourceId = selectedGame.SourceId;
        var sourceGameId = selectedGame.SourceGameId;
        if (selectedGame.SourceId == GameSourceId.Manual &&
            _storeSourceId is { } storeSourceId &&
            !string.IsNullOrWhiteSpace(_storeGameId))
        {
            searchSourceId = storeSourceId;
            sourceGameId = _storeGameId;
        }

        var searchGame = new Game
        {
            Name = searchName,
            SourceId = searchSourceId,
            SourceGameId = sourceGameId,
            Metadata = selectedGame.Metadata with
            {
                StoreSourceId = _storeSourceId,
                StoreGameId = _storeGameId
            }
        };
        try
        {
            var results = await Task.WhenAll(artworkProviders.Select(async provider =>
                (Provider: provider, Images: await provider.GetArtworksAsync(searchGame, preferredLanguageTag, cancellationToken))));
            if (currentSearchRevision != searchRevision)
            {
                return;
            }

            CoverArtworkOptions = CreateOptions(results, GameArtworkSlot.Cover, Resources.Cover);
            BackgroundArtworkOptions = CreateOptions(results, GameArtworkSlot.Background, Resources.Background);
            artworkOptionsSearchName = searchName;
            OnPropertyChanged(nameof(ActiveArtworkOptions));
            OnPropertyChanged(nameof(HasActiveArtworkOptions));
            OnPropertyChanged(nameof(HasArtworkOptions));
            ArtworkSourcesStatus = HasArtworkOptions ? null : Resources.NoArtworkSourcesFound;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (currentSearchRevision == searchRevision)
            {
                artworkOptionsSearchName = null;
                ArtworkSourcesStatus = string.Format(CultureInfo.CurrentCulture, Resources.ArtworkSourcesError, exception.Message);
            }
        }
    }

    private static ObservableCollection<GameArtworkSourceOption> CreateOptions(
        IEnumerable<(IGameArtworkProvider Provider, IReadOnlyList<GameArtworkImage> Images)> results,
        GameArtworkSlot slot,
        string slotName)
    {
        var options = new List<GameArtworkSourceOption>();
        foreach (var (provider, images) in results)
        {
            var matchingImages = images.Where(image => image.Slot == slot).ToArray();
            for (var index = 0; index < matchingImages.Length; index++)
            {
                options.Add(new GameArtworkSourceOption($"{provider.DisplayName} · {slotName} {index + 1}", matchingImages[index]));
            }
        }

        return new ObservableCollection<GameArtworkSourceOption>(options
            .DistinctBy(option => Convert.ToHexString(SHA256.HashData(option.Image.Content)), StringComparer.Ordinal));
    }

    private ObservableCollection<GameArtworkSourceOption> GetActiveArtworkOptionsCollection() => ActiveArtworkSlot switch
    {
        GameArtworkSlot.Cover => CoverArtworkOptions,
        GameArtworkSlot.Background => BackgroundArtworkOptions,
        _ => throw new ArgumentOutOfRangeException(nameof(ActiveArtworkSlot))
    };

    private static void RemoveUnavailableArtworkPreviews(ObservableCollection<GameArtworkSourceOption> options)
    {
        for (var index = options.Count - 1; index >= 0; index--)
        {
            if (options[index].PreviewImage is null)
            {
                options.RemoveAt(index);
            }
        }
    }

    private async Task LoadArtworkPreviewsAsync(IEnumerable<GameArtworkSourceOption> options, CancellationToken cancellationToken)
    {
        await Task.WhenAll(options.Select(async option =>
        {
            option.PreviewImage = await artworkImageLoader.LoadAsync(option.Image);
            option.IsPreviewLoaded = true;
        }));
    }

    partial void OnArtworkSourcesStatusChanged(string? value) => OnPropertyChanged(nameof(HasArtworkSourcesStatus));

    partial void OnActiveArtworkSlotChanged(GameArtworkSlot value)
    {
        SelectedActiveArtworkOption = ActiveArtworkOptions.FirstOrDefault();
        OnPropertyChanged(nameof(ActiveArtworkOptions));
        OnPropertyChanged(nameof(IsCoverPickerActive));
        OnPropertyChanged(nameof(IsBackgroundPickerActive));
        OnPropertyChanged(nameof(ActiveArtworkTitle));
        OnPropertyChanged(nameof(HasActiveArtworkOptions));
    }

    partial void OnIsArtworkPickerOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCoverPickerActive));
        OnPropertyChanged(nameof(IsBackgroundPickerActive));
    }

    partial void OnIsLoadingArtworkOptionsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoActiveArtworkOptions));
        OnPropertyChanged(nameof(IsCoverPickerActive));
        OnPropertyChanged(nameof(IsBackgroundPickerActive));
        OnPropertyChanged(nameof(CanApplyActiveArtwork));
    }

    partial void OnSelectedActiveArtworkOptionChanged(GameArtworkSourceOption? value) => OnPropertyChanged(nameof(CanApplyActiveArtwork));

    partial void OnCoverArtworkOptionsChanged(ObservableCollection<GameArtworkSourceOption> value)
    {
        OnPropertyChanged(nameof(HasArtworkOptions));
        OnPropertyChanged(nameof(ActiveArtworkOptions));
        OnPropertyChanged(nameof(HasActiveArtworkOptions));
    }

    partial void OnBackgroundArtworkOptionsChanged(ObservableCollection<GameArtworkSourceOption> value)
    {
        OnPropertyChanged(nameof(HasArtworkOptions));
        OnPropertyChanged(nameof(ActiveArtworkOptions));
        OnPropertyChanged(nameof(HasActiveArtworkOptions));
    }
}
