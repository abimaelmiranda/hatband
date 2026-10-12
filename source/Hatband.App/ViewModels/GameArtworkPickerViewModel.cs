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
    private GameArtworkSlot? artworkOptionsSearchSlot;
    private string preferredLanguageTag = "en-US";
    private string? _storeSourceId;
    private string? _storeGameId;
    private int searchRevision;
    private int openRevision;

    [ObservableProperty]
    public partial ObservableCollection<GameArtworkSourceOption> CoverArtworkOptions { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<GameArtworkSourceOption> BackgroundArtworkOptions { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<GameArtworkSourceOption> LogoArtworkOptions { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<GameArtworkSourceOption> IconArtworkOptions { get; set; } = [];

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
        GameArtworkSlot.Logo => LogoArtworkOptions,
        GameArtworkSlot.Icon => IconArtworkOptions,
        _ => throw new ArgumentOutOfRangeException(nameof(ActiveArtworkSlot))
    };

    public bool IsCoverPickerActive => IsArtworkPickerOpen && ActiveArtworkSlot == GameArtworkSlot.Cover && !IsLoadingArtworkOptions && HasActiveArtworkOptions;

    public bool IsBackgroundPickerActive => IsArtworkPickerOpen && ActiveArtworkSlot == GameArtworkSlot.Background && !IsLoadingArtworkOptions && HasActiveArtworkOptions;

    public bool IsLogoPickerActive => IsArtworkPickerOpen && ActiveArtworkSlot == GameArtworkSlot.Logo && !IsLoadingArtworkOptions && HasActiveArtworkOptions;

    public bool IsIconPickerActive => IsArtworkPickerOpen && ActiveArtworkSlot == GameArtworkSlot.Icon && !IsLoadingArtworkOptions && HasActiveArtworkOptions;

    public string ActiveArtworkTitle => ActiveArtworkSlot switch
    {
        GameArtworkSlot.Cover => Resources.Cover,
        GameArtworkSlot.Background => Resources.Background,
        GameArtworkSlot.Logo => Resources.Logo,
        GameArtworkSlot.Icon => Resources.Icon,
        _ => throw new ArgumentOutOfRangeException(nameof(ActiveArtworkSlot))
    };

    public bool HasArtworkSourcesStatus => !string.IsNullOrWhiteSpace(ArtworkSourcesStatus);

    public bool HasActiveArtworkOptions => ActiveArtworkOptions.Count > 0;

    public bool HasNoActiveArtworkOptions => !HasActiveArtworkOptions && !IsLoadingArtworkOptions;

    public bool HasArtworkOptions => CoverArtworkOptions.Count > 0 || BackgroundArtworkOptions.Count > 0 || LogoArtworkOptions.Count > 0 || IconArtworkOptions.Count > 0;

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
        artworkOptionsSearchSlot = null;
        SelectedActiveArtworkOption = null;
        ClearOptions(CoverArtworkOptions);
        ClearOptions(BackgroundArtworkOptions);
        ClearOptions(LogoArtworkOptions);
        ClearOptions(IconArtworkOptions);
        ArtworkSourcesStatus = null;
        OnPropertyChanged(nameof(ActiveArtworkOptions));
        OnPropertyChanged(nameof(HasActiveArtworkOptions));
        OnPropertyChanged(nameof(HasArtworkOptions));
        OnPropertyChanged(nameof(HasNoActiveArtworkOptions));
    }

    public void UpdateStoreReference(string? storeSourceId, string? storeGameId)
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
        if (slot is not GameArtworkSlot.Cover and not GameArtworkSlot.Background and not GameArtworkSlot.Logo and not GameArtworkSlot.Icon)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        var currentOpenRevision = ++openRevision;
        ActiveArtworkSlot = slot;
        ArtworkSourcesStatus = Resources.LoadingArtworkSources;
        IsLoadingArtworkOptions = true;
        IsArtworkPickerOpen = true;
        try
        {
            await LoadArtworkOptionsAsync(editedGameName.Trim(), slot, currentOpenRevision, cancellationToken);
            if (currentOpenRevision != openRevision || !IsArtworkPickerOpen)
            {
                return;
            }

            var activeOptions = GetActiveArtworkOptionsCollection();
            if (activeOptions.Count > 0)
            {
                ArtworkSourcesStatus = Resources.LoadingArtworkPreviews;
                await LoadArtworkPreviewsAsync(
                    activeOptions,
                    () => currentOpenRevision == openRevision && IsArtworkPickerOpen,
                    cancellationToken);
                if (currentOpenRevision != openRevision || !IsArtworkPickerOpen)
                {
                    return;
                }
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
            if (currentOpenRevision == openRevision)
            {
                IsLoadingArtworkOptions = false;
            }
        }
    }

    public void Close()
    {
        openRevision++;
        IsLoadingArtworkOptions = false;
        IsArtworkPickerOpen = false;
    }

    private async Task LoadArtworkOptionsAsync(string searchName, GameArtworkSlot requestedSlot, int currentOpenRevision, CancellationToken cancellationToken)
    {
        if (string.Equals(artworkOptionsSearchName, searchName, StringComparison.Ordinal) && artworkOptionsSearchSlot == requestedSlot)
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
        if (selectedGame.SourceId == GameSourceIds.Manual &&
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
            var results = await Task.WhenAll(artworkProviders.Select(provider =>
                GetProviderArtworkAsync(provider, searchGame, requestedSlot, cancellationToken)));
            if (currentSearchRevision != searchRevision || currentOpenRevision != openRevision || !IsArtworkPickerOpen)
            {
                return;
            }

            SelectedActiveArtworkOption = null;
            ClearOptions(CoverArtworkOptions);
            ClearOptions(BackgroundArtworkOptions);
            ClearOptions(LogoArtworkOptions);
            ClearOptions(IconArtworkOptions);
            CoverArtworkOptions = CreateOptions(results, GameArtworkSlot.Cover, Resources.Cover);
            BackgroundArtworkOptions = CreateOptions(results, GameArtworkSlot.Background, Resources.Background);
            LogoArtworkOptions = CreateOptions(results, GameArtworkSlot.Logo, Resources.Logo);
            IconArtworkOptions = CreateOptions(results, GameArtworkSlot.Icon, Resources.Icon);
            artworkOptionsSearchName = searchName;
            artworkOptionsSearchSlot = requestedSlot;
            OnPropertyChanged(nameof(ActiveArtworkOptions));
            OnPropertyChanged(nameof(HasActiveArtworkOptions));
            OnPropertyChanged(nameof(HasArtworkOptions));
            var requestedArtworkExists = results.Any(result => result.Images.Any(image => image.Slot == requestedSlot));
            var providerError = results.FirstOrDefault(result => result.ErrorMessage is not null).ErrorMessage;
            ArtworkSourcesStatus = HasArtworkOptions || requestedArtworkExists
                ? null
                : providerError is null
                    ? Resources.NoArtworkSourcesFound
                    : string.Format(CultureInfo.CurrentCulture, Resources.ArtworkSourcesError, providerError);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (currentSearchRevision == searchRevision && currentOpenRevision == openRevision)
            {
                artworkOptionsSearchName = null;
                ArtworkSourcesStatus = string.Format(CultureInfo.CurrentCulture, Resources.ArtworkSourcesError, exception.Message);
            }
        }
    }

    private async Task<(IGameArtworkProvider Provider, IReadOnlyList<GameArtworkImage> Images, string? ErrorMessage)> GetProviderArtworkAsync(
        IGameArtworkProvider provider,
        Game searchGame,
        GameArtworkSlot requestedSlot,
        CancellationToken cancellationToken)
    {
        if (!provider.SupportedSlots.Contains(requestedSlot))
        {
            return (provider, Array.Empty<GameArtworkImage>(), null);
        }

        try
        {
            var images = await provider.GetArtworksAsync(
                searchGame,
                new HashSet<GameArtworkSlot> { requestedSlot },
                preferredLanguageTag,
                cancellationToken);
            return (provider, images, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return (provider, Array.Empty<GameArtworkImage>(), exception.Message);
        }
    }

    private static void ClearOptions(ObservableCollection<GameArtworkSourceOption> options)
    {
        foreach (var option in options)
        {
            option.PreviewImage?.Dispose();
            option.PreviewImage = null;
        }

        options.Clear();
    }

    private static ObservableCollection<GameArtworkSourceOption> CreateOptions(
        IEnumerable<(IGameArtworkProvider Provider, IReadOnlyList<GameArtworkImage> Images, string? ErrorMessage)> results,
        GameArtworkSlot slot,
        string slotName)
    {
        var options = new List<GameArtworkSourceOption>();
        foreach (var (provider, images, _) in results)
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
        GameArtworkSlot.Logo => LogoArtworkOptions,
        GameArtworkSlot.Icon => IconArtworkOptions,
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

    private async Task LoadArtworkPreviewsAsync(
        IEnumerable<GameArtworkSourceOption> options,
        Func<bool> isCurrentOpen,
        CancellationToken cancellationToken)
    {
        await Task.WhenAll(options.Select(async option =>
        {
            var preview = await artworkImageLoader.LoadAsync(option.Image);
            if (!isCurrentOpen())
            {
                preview?.Dispose();
                return;
            }

            option.PreviewImage = preview;
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
        OnPropertyChanged(nameof(IsLogoPickerActive));
        OnPropertyChanged(nameof(IsIconPickerActive));
        OnPropertyChanged(nameof(ActiveArtworkTitle));
        OnPropertyChanged(nameof(HasActiveArtworkOptions));
    }

    partial void OnIsArtworkPickerOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCoverPickerActive));
        OnPropertyChanged(nameof(IsBackgroundPickerActive));
        OnPropertyChanged(nameof(IsIconPickerActive));
        OnPropertyChanged(nameof(IsLogoPickerActive));
    }

    partial void OnIsLoadingArtworkOptionsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoActiveArtworkOptions));
        OnPropertyChanged(nameof(IsCoverPickerActive));
        OnPropertyChanged(nameof(IsBackgroundPickerActive));
        OnPropertyChanged(nameof(IsLogoPickerActive));
        OnPropertyChanged(nameof(IsIconPickerActive));
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

    partial void OnLogoArtworkOptionsChanged(ObservableCollection<GameArtworkSourceOption> value)
    {
        OnPropertyChanged(nameof(HasArtworkOptions));
        OnPropertyChanged(nameof(ActiveArtworkOptions));
        OnPropertyChanged(nameof(HasActiveArtworkOptions));
    }

    partial void OnIconArtworkOptionsChanged(ObservableCollection<GameArtworkSourceOption> value)
    {
        OnPropertyChanged(nameof(HasArtworkOptions));
        OnPropertyChanged(nameof(ActiveArtworkOptions));
        OnPropertyChanged(nameof(HasActiveArtworkOptions));
    }
}
