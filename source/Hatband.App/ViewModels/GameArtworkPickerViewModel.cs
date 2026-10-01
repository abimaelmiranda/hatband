using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.Localization;
using Hatband.App.Services;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums.Artwork;
using Hatband.Core.Models;

namespace Hatband.App.ViewModels;

public partial class GameArtworkPickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<IGameArtworkSearchProvider> artworkSearchProviders;
    private readonly ArtworkImageLoader artworkImageLoader;
    private Game? game;
    private string? artworkOptionsSearchName;
    private string preferredLanguageTag = "en-US";
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

    public GameArtworkPickerViewModel(
        IEnumerable<IGameArtworkSearchProvider> artworkSearchProviders,
        ArtworkImageLoader artworkImageLoader)
    {
        this.artworkSearchProviders = artworkSearchProviders.ToArray();
        this.artworkImageLoader = artworkImageLoader;
    }

    public IReadOnlyList<GameArtworkSourceOption> ActiveArtworkOptions => ActiveArtworkSlot switch
    {
        GameArtworkSlot.Cover => CoverArtworkOptions,
        GameArtworkSlot.Background => BackgroundArtworkOptions,
        _ => throw new ArgumentOutOfRangeException(nameof(ActiveArtworkSlot))
    };

    public bool IsCoverPickerActive => IsArtworkPickerOpen &&
                                       ActiveArtworkSlot == GameArtworkSlot.Cover &&
                                       !IsLoadingArtworkOptions &&
                                       HasActiveArtworkOptions;

    public bool IsBackgroundPickerActive => IsArtworkPickerOpen &&
                                            ActiveArtworkSlot == GameArtworkSlot.Background &&
                                            !IsLoadingArtworkOptions &&
                                            HasActiveArtworkOptions;

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

    public bool CanApplyActiveArtwork => SelectedActiveArtworkOption is
    {
        IsPreviewLoaded: true,
        PreviewImage: not null
    };

    public void Initialize(Game selectedGame, string languageTag)
    {
        ArgumentNullException.ThrowIfNull(selectedGame);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);

        game = selectedGame;
        preferredLanguageTag = languageTag;
        InvalidateSearch();
        CoverArtworkOptions.Clear();
        BackgroundArtworkOptions.Clear();
        ArtworkSourcesStatus = null;
        IsArtworkPickerOpen = false;
        IsLoadingArtworkOptions = false;
        SelectedActiveArtworkOption = null;
        OnPropertyChanged(nameof(ActiveArtworkOptions));
        OnPropertyChanged(nameof(HasArtworkOptions));
        OnPropertyChanged(nameof(HasActiveArtworkOptions));
        OnPropertyChanged(nameof(HasNoActiveArtworkOptions));
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

    public async Task OpenAsync(
        GameArtworkSlot slot,
        string editedGameName,
        CancellationToken cancellationToken = default)
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
            if (HasActiveArtworkOptions)
            {
                ArtworkSourcesStatus = Resources.LoadingArtworkPreviews;
                var activeOptions = GetActiveArtworkOptionsCollection();
                await LoadArtworkPreviewsAsync(activeOptions, cancellationToken);
                RemoveUnavailableArtworkPreviews(activeOptions);
                OnPropertyChanged(nameof(HasArtworkOptions));
                OnPropertyChanged(nameof(HasActiveArtworkOptions));
                SelectedActiveArtworkOption = activeOptions.FirstOrDefault();
                ArtworkSourcesStatus = HasActiveArtworkOptions
                    ? null
                    : Resources.NoUsableArtworkFound;
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

    public void Close()
    {
        IsArtworkPickerOpen = false;
    }

    private async Task LoadArtworkOptionsAsync(string searchName, CancellationToken cancellationToken)
    {
        if (string.Equals(artworkOptionsSearchName, searchName, StringComparison.Ordinal))
        {
            if (HasActiveArtworkOptions)
            {
                ArtworkSourcesStatus = null;
            }
            else if (HasArtworkOptions)
            {
                ArtworkSourcesStatus = Resources.NoUsableArtworkFound;
            }
            else
            {
                ArtworkSourcesStatus = Resources.NoArtworkSourcesFound;
            }

            return;
        }

        ArtworkSourcesStatus = Resources.LoadingArtworkSources;
        var currentSearchRevision = searchRevision;
        try
        {
            if (game is null)
            {
                throw new InvalidOperationException("The game artwork picker has not been initialized.");
            }

            var request = CreateLookupRequest(game, searchName);
            var eligibleProviders = artworkSearchProviders.Where(provider => provider.CanSearch(request)).ToArray();
            var searchResults = await Task.WhenAll(eligibleProviders.Select(async provider =>
                (Provider: provider, Response: await provider.SearchArtworkAsync(
                    request,
                    preferredLanguageTag,
                    GetRegion(preferredLanguageTag),
                    cancellationToken))));

            if (currentSearchRevision != searchRevision)
            {
                return;
            }

            var coverOptions = new List<GameArtworkSourceOption>();
            var backgroundOptions = new List<GameArtworkSourceOption>();
            var sourceErrors = new List<string>();
            foreach (var (provider, response) in searchResults)
            {
                if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
                {
                    sourceErrors.Add($"{provider.DisplayName}: {response.ErrorMessage}");
                }

                coverOptions.AddRange(CreateOptions(
                    provider.DisplayName,
                    response.Sources.CoverImageCandidates,
                    response.Sources.CoverImageUrls,
                    Resources.Cover));
                backgroundOptions.AddRange(CreateOptions(
                    provider.DisplayName,
                    response.Sources.BackgroundImageCandidates,
                    response.Sources.BackgroundImageUrls,
                    Resources.Background));
            }

            CoverArtworkOptions = DistinctOptions(coverOptions);
            BackgroundArtworkOptions = DistinctOptions(backgroundOptions);
            artworkOptionsSearchName = searchName;
            OnPropertyChanged(nameof(ActiveArtworkOptions));
            OnPropertyChanged(nameof(HasActiveArtworkOptions));
            OnPropertyChanged(nameof(HasArtworkOptions));
            ArtworkSourcesStatus = HasArtworkOptions ? null : Resources.NoArtworkSourcesFound;
            if (!HasArtworkOptions && sourceErrors.Count > 0)
            {
                artworkOptionsSearchName = null;
                ArtworkSourcesStatus = string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.ArtworkSourcesError,
                    string.Join(" ", sourceErrors));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (currentSearchRevision != searchRevision)
            {
                return;
            }

            artworkOptionsSearchName = null;
            ArtworkSourcesStatus = string.Format(
                CultureInfo.CurrentCulture,
                Resources.ArtworkSourcesError,
                exception.Message);
        }
    }

    private ObservableCollection<GameArtworkSourceOption> GetActiveArtworkOptionsCollection()
    {
        return ActiveArtworkSlot switch
        {
            GameArtworkSlot.Cover => CoverArtworkOptions,
            GameArtworkSlot.Background => BackgroundArtworkOptions,
            _ => throw new ArgumentOutOfRangeException(nameof(ActiveArtworkSlot))
        };
    }

    private static void RemoveUnavailableArtworkPreviews(
        ObservableCollection<GameArtworkSourceOption> artworkOptions)
    {
        for (var index = artworkOptions.Count - 1; index >= 0; index--)
        {
            if (artworkOptions[index].PreviewImage is null)
            {
                artworkOptions.RemoveAt(index);
            }
        }
    }

    private static IEnumerable<GameArtworkSourceOption> CreateOptions(
        string sourceName,
        IReadOnlyList<GameArtworkCandidate> candidates,
        IReadOnlyList<string> urls,
        string artworkType)
    {
        var distinctCandidates = candidates
            .DistinctBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinctCandidates.Length > 0)
        {
            return distinctCandidates
                .Select((candidate, index) => new GameArtworkSourceOption(
                    string.IsNullOrWhiteSpace(candidate.Caption)
                        ? $"{sourceName} · {artworkType} {index + 1}"
                        : $"{sourceName} · {candidate.Caption}",
                    candidate.Url));
        }

        return urls
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((url, index) => new GameArtworkSourceOption(
                $"{sourceName} · {artworkType} {index + 1}",
                url));
    }

    private static ObservableCollection<GameArtworkSourceOption> DistinctOptions(
        IEnumerable<GameArtworkSourceOption> options)
    {
        return new ObservableCollection<GameArtworkSourceOption>(options
            .DistinctBy(option => option.Url, StringComparer.OrdinalIgnoreCase));
    }

    private async Task LoadArtworkPreviewsAsync(
        IEnumerable<GameArtworkSourceOption> artworkOptions,
        CancellationToken cancellationToken)
    {
        var options = artworkOptions
            .Where(option => option.PreviewImage is null)
            .ToArray();
        using var concurrencyLimit = new SemaphoreSlim(4);
        await Task.WhenAll(options.Select(async option =>
        {
            await concurrencyLimit.WaitAsync(cancellationToken);
            try
            {
                option.PreviewImage = await artworkImageLoader.LoadRemoteAsync(option.Url, cancellationToken);
                option.IsPreviewLoaded = true;
            }
            finally
            {
                concurrencyLimit.Release();
            }
        }));
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

    private static string GetRegion(string languageTag)
    {
        var culture = CultureInfo.GetCultureInfo(languageTag);
        var region = new RegionInfo(culture.Name);
        return region.TwoLetterISORegionName;
    }

    partial void OnArtworkSourcesStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasArtworkSourcesStatus));
    }

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

    partial void OnSelectedActiveArtworkOptionChanged(GameArtworkSourceOption? value)
    {
        OnPropertyChanged(nameof(CanApplyActiveArtwork));
    }

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
