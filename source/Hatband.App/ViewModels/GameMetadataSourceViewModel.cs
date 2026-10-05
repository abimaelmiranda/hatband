using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;

namespace Hatband.App.ViewModels;

public partial class GameMetadataSourceViewModel : ObservableObject
{
    private readonly IGameMetadataProvider _provider;
    private readonly Action<GameMetadata> _applyMetadata;
    private Game? _game;
    private string _languageTag = string.Empty;
    private string _region = string.Empty;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<GameMetadata> SearchResults { get; set; } = [];

    [ObservableProperty]
    public partial GameMetadata? SelectedSearchResult { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial bool IsCustomSearchOpen { get; set; }

    [ObservableProperty]
    public partial bool HasSearched { get; set; }

    public GameMetadataSourceViewModel(IGameMetadataProvider provider, Action<GameMetadata> applyMetadata)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(applyMetadata);
        _provider = provider;
        _applyMetadata = applyMetadata;
    }

    public string ProviderId => _provider.ProviderId;

    public string DisplayName => _provider.DisplayName;

    public bool HasLookup => SelectedSearchResult is not null;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasSearchResult => SelectedSearchResult is not null;

    public bool IsResultViewVisible => !IsCustomSearchOpen;

    public bool CanSearch => !IsSearching && !string.IsNullOrWhiteSpace(SearchQuery);

    public bool IsLanguageMismatch => false;

    public string? SelectedResultName => SelectedSearchResult?.StoreName;

    public void Initialize(Game selectedGame, string preferredLanguageTag, string lookupRegion)
    {
        ArgumentNullException.ThrowIfNull(selectedGame);
        _game = selectedGame;
        SearchQuery = selectedGame.Name;
        _languageTag = preferredLanguageTag;
        _region = lookupRegion;
        SearchResults.Clear();
        SelectedSearchResult = null;
        StatusMessage = null;
        IsCustomSearchOpen = false;
        HasSearched = false;
    }

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnIsSearchingChanged(bool value) => OnPropertyChanged(nameof(CanSearch));

    partial void OnSelectedSearchResultChanged(GameMetadata? value)
    {
        OnPropertyChanged(nameof(HasLookup));
        OnPropertyChanged(nameof(HasSearchResult));
        OnPropertyChanged(nameof(SelectedResultName));
    }

    partial void OnIsCustomSearchOpenChanged(bool value) => OnPropertyChanged(nameof(IsResultViewVisible));

    [RelayCommand]
    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSearch)
        {
            return;
        }

        IsSearching = true;
        StatusMessage = null;
        SelectedSearchResult = null;
        SearchResults.Clear();
        try
        {
            var sourceGame = _game ?? throw new InvalidOperationException("The metadata source has not been initialized.");
            var queryGame = new Game
            {
                Name = SearchQuery.Trim(),
                SourceId = sourceGame.SourceId,
                SourceGameId = sourceGame.SourceGameId,
                Metadata = sourceGame.Metadata
            };
            var results = await _provider.SearchAsync(queryGame, _languageTag, _region, cancellationToken);
            SearchResults = new ObservableCollection<GameMetadata>(results);
            SelectedSearchResult = SearchResults.FirstOrDefault();
            if (results.Count == 0)
            {
                StatusMessage = Resources.MetadataSourceNoResults;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsSearching = false;
            HasSearched = true;
        }
    }

    [RelayCommand]
    private void OpenCustomSearch() => IsCustomSearchOpen = true;

    [RelayCommand]
    private void CloseCustomSearch() => IsCustomSearchOpen = false;

    [RelayCommand]
    private void Apply()
    {
        if (IsSearching || SelectedSearchResult is not { } metadata)
        {
            return;
        }

        _applyMetadata(metadata);
        StatusMessage = Resources.MetadataSourceApplied;
    }
}
