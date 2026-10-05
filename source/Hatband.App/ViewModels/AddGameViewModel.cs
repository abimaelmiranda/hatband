using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.App.Services;
using Hatband.Core.Enums.Artwork;
using Hatband.Core.Models.Games;
using Hatband.Core.Models.Libraries;

namespace Hatband.App.ViewModels;

public partial class AddGameViewModel : ViewModelBase
{
    public const string GameSectionId = "game";
    public const string ActionsSectionId = "actions";
    public const string ImagesSectionId = "images";

    private readonly IGameRepository _gameRepository;
    private readonly IGameLibraryRepository _libraryRepository;
    private readonly IGameArtworkStorage _artworkStorage;
    private readonly IReadOnlyList<IGameMetadataProvider> _metadataProviders;
    private string _preferredLanguageTag = "en-US";
    private string? _metadataStoreName;
    private string? _metadataStoreGameId;
    private GameSourceId? _metadataStoreSourceId;
    private GamePlatform? _nativePlatforms;
    private GameArtworkImage? _selectedCoverImage;
    private GameArtworkImage? _selectedBackgroundImage;

    public AddGameViewModel(
        IGameRepository gameRepository,
        IGameLibraryRepository libraryRepository,
        IGameArtworkStorage artworkStorage,
        IEnumerable<IGameMetadataProvider> metadataProviders,
        IEnumerable<IGameArtworkProvider> artworkProviders,
        ArtworkImageLoader artworkImageLoader)
    {
        _gameRepository = gameRepository;
        _libraryRepository = libraryRepository;
        _artworkStorage = artworkStorage;
        _metadataProviders = metadataProviders.ToArray();
        ArtworkPicker = new GameArtworkPickerViewModel(artworkProviders, artworkImageLoader);
        Sections =
        [
            new AddGameSectionViewModel(GameSectionId, Resources.ManualGameSection, "◉"),
            new AddGameSectionViewModel(ActionsSectionId, Resources.GameActionsSection, "▶"),
            new AddGameSectionViewModel(ImagesSectionId, Resources.ManualGameImagesSection, "▧")
        ];
        var gameSection = Sections.Single(section => section.Id == GameSectionId);
        SelectedSectionIndex = Sections.IndexOf(gameSection);
        gameSection.IsSelected = true;
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? InstallDirectory { get; set; }

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
    public partial string? CoverArtworkSelection { get; set; }

    [ObservableProperty]
    public partial string? BackgroundArtworkSelection { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<GameMetadataSourceViewModel> MetadataSources { get; set; } = [];

    [ObservableProperty]
    public partial string MetadataSearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int SelectedMetadataSourceIndex { get; set; }

    [ObservableProperty]
    public partial bool IsMetadataSearchOpen { get; set; }

    [ObservableProperty]
    public partial bool IsSearchingMetadata { get; set; }

    [ObservableProperty]
    public partial string SteamArtworkAssociationStatus { get; private set; } = Resources.SteamArtworkNeedsMetadata;

    [ObservableProperty]
    public partial ObservableCollection<AddGameActionViewModel> Actions { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<AddGameSectionViewModel> Sections { get; set; }

    [ObservableProperty]
    public partial int SelectedSectionIndex { get; set; }

    [ObservableProperty]
    public partial bool IsContentActive { get; set; }

    [ObservableProperty]
    public partial int SelectedFieldIndex { get; set; }

    public AddGameSectionViewModel SelectedSection => Sections[SelectedSectionIndex];

    public bool IsGameSectionSelected => SelectedSection.Id == GameSectionId;

    public bool IsActionsSectionSelected => SelectedSection.Id == ActionsSectionId;

    public bool IsImagesSectionSelected => SelectedSection.Id == ImagesSectionId;

    public bool HasMetadataSources => MetadataSources.Count > 0;

    public bool HasNoMetadataSources => !HasMetadataSources;

    public bool CanOpenMetadataSearch => HasMetadataSources;

    public bool CanSearchMetadata => !IsSearchingMetadata &&
                                     !string.IsNullOrWhiteSpace(MetadataSearchQuery) &&
                                     HasMetadataSources;

    public GameMetadataSourceViewModel? SelectedMetadataSource =>
        SelectedMetadataSourceIndex >= 0 && SelectedMetadataSourceIndex < MetadataSources.Count
            ? MetadataSources[SelectedMetadataSourceIndex]
            : null;

    public bool CanApplySelectedMetadata => !IsSearchingMetadata &&
        SelectedMetadataSource is { HasLookup: true, IsSearching: false };

    public GameArtworkPickerViewModel ArtworkPicker { get; }

    public event Action<AddGameCreationResult>? CreationCompleted;

    [RelayCommand]
    private void AddAction()
    {
        var action = new AddGameActionViewModel(
            Actions.Count == 0 ? Resources.Play : Resources.GameActionDefaultName,
            Actions.Count == 0,
            SetPrimaryAction,
            RemoveAction);
        Actions.Add(action);
        SelectedSectionIndex = Sections.IndexOf(Sections.Single(section => section.Id == ActionsSectionId));
        IsContentActive = true;
        SelectedFieldIndex = 0;
    }

    [RelayCommand]
    private void RemoveAction(AddGameActionViewModel action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!Actions.Remove(action))
        {
            return;
        }

        if (Actions.Count > 0 && Actions.All(item => !item.IsPrimary))
        {
            Actions[0].IsPrimary = true;
        }

        SelectedFieldIndex = 0;
    }

    public void SelectSection(AddGameSectionViewModel section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var sectionIndex = Sections.IndexOf(section);
        if (sectionIndex < 0)
        {
            throw new ArgumentException("The section does not belong to this view model.", nameof(section));
        }

        IsContentActive = false;
        SelectedSectionIndex = sectionIndex;
    }

    public void ActivateContent()
    {
        IsContentActive = true;
        SelectedFieldIndex = 0;
    }

    public void DeactivateContent() => IsContentActive = false;

    public void SelectField(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        IsContentActive = true;
        SelectedFieldIndex = index;
    }

    public void PrepareFromExecutable(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        Name = Path.GetFileNameWithoutExtension(executablePath);
        InstallDirectory = Path.GetDirectoryName(executablePath);
        if (Actions.Count == 0)
        {
            AddAction();
        }

        var primaryAction = Actions.FirstOrDefault(action => action.IsPrimary) ?? Actions[0];
        primaryAction.Type = GameActionType.Executable;
        primaryAction.Target = executablePath;
        primaryAction.WorkingDirectory = Path.GetDirectoryName(executablePath);
    }

    public void InitializeSearch(string languageTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        _preferredLanguageTag = languageTag;

        var searchGame = CreateSearchGame();
        ArtworkPicker.Initialize(searchGame, languageTag);
        var culture = CultureInfo.GetCultureInfo(languageTag);
        var region = new RegionInfo(culture.Name).TwoLetterISORegionName;
        var providers = _metadataProviders
            .Where(provider => provider.SupportsManualSearch)
            .OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase);
        var sources = new ObservableCollection<GameMetadataSourceViewModel>();
        foreach (var provider in providers)
        {
            var source = new GameMetadataSourceViewModel(provider, ApplyMetadata);
            source.Initialize(searchGame, languageTag, region);
            source.PropertyChanged += OnMetadataSourcePropertyChanged;
            sources.Add(source);
        }

        MetadataSources = sources;
        SelectedMetadataSourceIndex = 0;
    }

    private void OnMetadataSourcePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(GameMetadataSourceViewModel.SelectedSearchResult) or
            nameof(GameMetadataSourceViewModel.IsSearching))
        {
            OnPropertyChanged(nameof(CanApplySelectedMetadata));
        }
    }

    public void OpenMetadataSearch()
    {
        if (!CanOpenMetadataSearch)
        {
            return;
        }

        MetadataSearchQuery = Name.Trim();
        SelectedMetadataSourceIndex = 0;
        IsMetadataSearchOpen = true;
    }

    public void CloseMetadataSearch() => IsMetadataSearchOpen = false;

    public void ApplySelectedMetadata()
    {
        if (!CanApplySelectedMetadata || SelectedMetadataSource is not { } source)
        {
            return;
        }

        source.ApplyCommand.Execute(null);
    }

    public async Task SearchMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSearchMetadata)
        {
            return;
        }

        IsSearchingMetadata = true;
        try
        {
            var query = MetadataSearchQuery.Trim();
            foreach (var source in MetadataSources)
            {
                source.SearchQuery = query;
            }

            await Task.WhenAll(MetadataSources.Select(source => source.SearchAsync(cancellationToken)));
        }
        finally
        {
            IsSearchingMetadata = false;
        }
    }

    public Task SearchArtworkAsync(GameArtworkSlot slot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return Task.CompletedTask;
        }

        return ArtworkPicker.OpenAsync(slot, Name.Trim(), cancellationToken);
    }

    public void ApplySelectedArtwork()
    {
        if (ArtworkPicker.SelectedActiveArtworkOption is not { } option)
        {
            return;
        }

        switch (ArtworkPicker.ActiveArtworkSlot)
        {
            case GameArtworkSlot.Cover:
                _selectedCoverImage = option.Image;
                CoverArtworkSelection = option.DisplayName;
                break;
            case GameArtworkSlot.Background:
                _selectedBackgroundImage = option.Image;
                BackgroundArtworkSelection = option.DisplayName;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ArtworkPicker.ActiveArtworkSlot));
        }

        ArtworkPicker.Close();
    }

    public void CloseArtworkSearch() => ArtworkPicker.Close();

    public void Reset()
    {
        Name = string.Empty;
        InstallDirectory = null;
        Description = string.Empty;
        Developer = string.Empty;
        Publisher = string.Empty;
        Genre = string.Empty;
        ReleaseDate = string.Empty;
        MetadataSearchQuery = string.Empty;
        SelectedMetadataSourceIndex = 0;
        IsMetadataSearchOpen = false;
        CoverArtworkSelection = null;
        BackgroundArtworkSelection = null;
        _metadataStoreName = null;
        _metadataStoreGameId = null;
        _metadataStoreSourceId = null;
        _nativePlatforms = null;
        _selectedCoverImage = null;
        _selectedBackgroundImage = null;
        ArtworkPicker.Close();
        ArtworkPicker.InvalidateSearch();
        foreach (var source in MetadataSources)
        {
            source.PropertyChanged -= OnMetadataSourcePropertyChanged;
        }

        MetadataSources = [];
        IsSearchingMetadata = false;
        SteamArtworkAssociationStatus = Resources.SteamArtworkNeedsMetadata;
        Actions.Clear();
        SelectedSectionIndex = Sections.IndexOf(Sections.Single(section => section.Id == GameSectionId));
        IsContentActive = false;
        SelectedFieldIndex = 0;
    }

    private void SetPrimaryAction(AddGameActionViewModel action)
    {
        foreach (var candidate in Actions)
        {
            candidate.SetPrimary(candidate == action);
        }
    }

    private Game CreateSearchGame() => new()
    {
        Name = Name.Trim(),
        SourceId = GameSourceId.Manual
    };

    private Game CreateArtworkSearchGame()
    {
        return new Game
        {
            Name = Name.Trim(),
            SourceId = GameSourceId.Manual,
            Metadata = new GameMetadata
            {
                StoreSourceId = _metadataStoreSourceId,
                StoreGameId = _metadataStoreGameId
            }
        };
    }

    private void ApplyMetadata(GameMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.StoreName))
        {
            Name = metadata.StoreName;
        }

        if (metadata.StoreSourceId is { } storeSourceId && metadata.StoreGameId is { } storeGameId)
        {
            _metadataStoreName = metadata.StoreName;
            _metadataStoreSourceId = storeSourceId;
            _metadataStoreGameId = storeGameId;
            if (storeSourceId == GameSourceId.Steam)
            {
                SteamArtworkAssociationStatus = string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.SteamArtworkLinked,
                    _metadataStoreName ?? Name,
                    storeGameId);
            }
        }

        _nativePlatforms = metadata.NativePlatforms;
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
            ReleaseDate = releaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        ArtworkPicker.Initialize(CreateArtworkSearchGame(), _preferredLanguageTag);
        CloseMetadataSearch();
    }

    partial void OnSelectedSectionIndexChanged(int value)
    {
        for (var index = 0; index < Sections.Count; index++)
        {
            Sections[index].IsSelected = Sections[index] == SelectedSection;
        }

        SelectedFieldIndex = 0;
        OnPropertyChanged(nameof(SelectedSection));
        OnPropertyChanged(nameof(IsGameSectionSelected));
        OnPropertyChanged(nameof(IsActionsSectionSelected));
        OnPropertyChanged(nameof(IsImagesSectionSelected));
    }

    partial void OnNameChanged(string value)
    {
        ArtworkPicker.InvalidateSearch();
    }

    partial void OnIsSearchingMetadataChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSearchMetadata));
        OnPropertyChanged(nameof(CanApplySelectedMetadata));
    }

    partial void OnMetadataSearchQueryChanged(string value) => OnPropertyChanged(nameof(CanSearchMetadata));

    partial void OnSelectedMetadataSourceIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SelectedMetadataSource));
        OnPropertyChanged(nameof(CanApplySelectedMetadata));
    }

    partial void OnMetadataSourcesChanged(ObservableCollection<GameMetadataSourceViewModel> value)
    {
        OnPropertyChanged(nameof(HasMetadataSources));
        OnPropertyChanged(nameof(HasNoMetadataSources));
        OnPropertyChanged(nameof(CanOpenMetadataSearch));
        OnPropertyChanged(nameof(CanSearchMetadata));
        OnPropertyChanged(nameof(SelectedMetadataSource));
        OnPropertyChanged(nameof(CanApplySelectedMetadata));
    }

    [RelayCommand]
    private Task SearchMetadata() => SearchMetadataAsync();

    [RelayCommand]
    private async Task SaveGameAsync(CancellationToken cancellationToken)
    {
        var trimmedName = Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            CreationCompleted?.Invoke(new AddGameCreationResult.InvalidName());
            return;
        }

        if (!TryParseReleaseDate(ReleaseDate, out var releaseDate))
        {
            CreationCompleted?.Invoke(new AddGameCreationResult.InvalidReleaseDate());
            return;
        }

        var trimmedInstallDirectory = TrimToNull(InstallDirectory);
        var game = new Game
        {
            Name = trimmedName,
            SourceId = GameSourceId.Manual,
            Metadata = new GameMetadata
            {
                LanguageTag = _preferredLanguageTag,
                StoreName = TrimToNull(_metadataStoreName),
                StoreGameId = _metadataStoreGameId,
                StoreSourceId = _metadataStoreSourceId,
                Description = TrimToNull(Description),
                Developer = TrimToNull(Developer),
                Publisher = TrimToNull(Publisher),
                Genre = TrimToNull(Genre),
                ReleaseDate = releaseDate,
                NativePlatforms = _nativePlatforms
            },
            InstallationInfo = trimmedInstallDirectory is null
                ? null
                : new GameInstallationInfo { InstallDirectory = trimmedInstallDirectory }
        };

        var configuredActions = Actions
            .Where(action => !string.IsNullOrWhiteSpace(action.Target))
            .ToList();
        var primaryAction = configuredActions.FirstOrDefault(action => action.IsPrimary) ?? configuredActions.FirstOrDefault();
        foreach (var action in configuredActions)
        {
            game.GameActions.Add(new GameAction
            {
                Name = string.IsNullOrWhiteSpace(action.Name) ? Resources.GameActionDefaultName : action.Name.Trim(),
                Target = action.Target.Trim(),
                Type = action.Type,
                Arguments = TrimToNull(action.Arguments),
                WorkingDirectory = TrimToNull(action.WorkingDirectory),
                IsPrimary = action == primaryAction
            });
        }

        try
        {
            var libraries = await _libraryRepository.GetAllAsync(cancellationToken);
            var library = libraries.FirstOrDefault();
            if (library is null)
            {
                library = new GameLibrary();
                await _libraryRepository.AddAsync(library, cancellationToken);
            }

            var selectedArtwork = new List<GameArtworkImage>();
            if (_selectedCoverImage is not null)
            {
                selectedArtwork.Add(_selectedCoverImage);
            }

            if (_selectedBackgroundImage is not null)
            {
                selectedArtwork.Add(_selectedBackgroundImage);
            }
            if (selectedArtwork.Count > 0)
            {
                game.Artwork = await _artworkStorage.StoreAsync(game.Id, selectedArtwork, game.Artwork, cancellationToken);
            }

            await _gameRepository.AddAsync(library.Id, game, cancellationToken);
            CreationCompleted?.Invoke(new AddGameCreationResult.Saved(game));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            CreationCompleted?.Invoke(new AddGameCreationResult.Failed(exception));
        }
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseReleaseDate(string value, out DateOnly? releaseDate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            releaseDate = null;
            return true;
        }

        var parsed = DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate);
        releaseDate = parsed ? parsedDate : null;
        return parsed;
    }
}
