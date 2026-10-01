using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.Core.Abstractions;
using Hatband.Core.Models;

namespace Hatband.App.ViewModels;

public partial class GameMetadataSourceViewModel : ObservableObject
{
    private readonly IGameMetadataSearchProvider provider;
    private readonly Action<string, GameMetadata> applyMetadata;
    private string preferredLanguageTag = string.Empty;
    private string region = string.Empty;
    private GameSourceLookupRequest? request;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<GameMetadataSearchResult> SearchResults { get; set; } = [];

    [ObservableProperty]
    public partial GameMetadataSearchResult? SelectedSearchResult { get; set; }

    [ObservableProperty]
    public partial GameMetadataLookupResponse? Lookup { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial bool IsCustomSearchOpen { get; set; }

    [ObservableProperty]
    public partial bool HasSearched { get; set; }

    public GameMetadataSourceViewModel(
        IGameMetadataSearchProvider provider,
        Action<string, GameMetadata> applyMetadata)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(applyMetadata);
        this.provider = provider;
        this.applyMetadata = applyMetadata;
    }

    public string ProviderId => provider.ProviderId;

    public string DisplayName => provider.DisplayName;

    public bool HasLookup => Lookup?.Metadata is not null;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasSearchResult => SelectedSearchResult is not null;

    public bool IsResultViewVisible => !IsCustomSearchOpen;

    public bool CanSearch => !IsSearching;

    public bool IsLanguageMismatch => Lookup is { Metadata: not null, IsContentLanguageMatch: false };

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    partial void OnIsSearchingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSearch));
    }

    partial void OnLookupChanged(GameMetadataLookupResponse? value)
    {
        OnPropertyChanged(nameof(HasLookup));
        OnPropertyChanged(nameof(IsLanguageMismatch));
    }

    partial void OnSelectedSearchResultChanged(GameMetadataSearchResult? value)
    {
        Lookup = null;
        StatusMessage = null;
        OnPropertyChanged(nameof(HasSearchResult));
    }

    partial void OnIsCustomSearchOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsResultViewVisible));
    }

    public void Initialize(GameSourceLookupRequest lookupRequest, string languageTag, string lookupRegion)
    {
        ArgumentNullException.ThrowIfNull(lookupRequest);
        request = lookupRequest;
        SearchQuery = lookupRequest.GameName;
        preferredLanguageTag = languageTag;
        region = lookupRegion;
        SearchResults.Clear();
        SelectedSearchResult = null;
        Lookup = null;
        StatusMessage = null;
        IsCustomSearchOpen = false;
        HasSearched = false;
    }

    public Task SearchIfNeededAsync(CancellationToken cancellationToken = default)
    {
        return HasSearched ? Task.CompletedTask : SearchAsync(cancellationToken);
    }

    [RelayCommand]
    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        if (IsSearching || string.IsNullOrWhiteSpace(SearchQuery))
        {
            return;
        }

        IsSearching = true;
        StatusMessage = null;
        Lookup = null;
        try
        {
            var lookupRequest = request ?? throw new InvalidOperationException("The metadata source has not been initialized.");
            var response = await provider.SearchAsync(
                lookupRequest with { GameName = SearchQuery.Trim() },
                preferredLanguageTag,
                region,
                cancellationToken);
            SearchResults = new ObservableCollection<GameMetadataSearchResult>(response.Results);
            SelectedSearchResult = SearchResults.FirstOrDefault();
            StatusMessage = response.ErrorMessage;
            if (response.ErrorMessage is null && response.Results.Count == 0)
            {
                StatusMessage = Resources.MetadataSourceNoResults;
            }

            if (SelectedSearchResult is not null)
            {
                IsCustomSearchOpen = false;
                await LoadDetailsAsync(cancellationToken);
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
    private void OpenCustomSearch()
    {
        IsCustomSearchOpen = true;
    }

    [RelayCommand]
    private void CloseCustomSearch()
    {
        IsCustomSearchOpen = false;
    }

    private async Task LoadDetailsAsync(CancellationToken cancellationToken)
    {
        if (SelectedSearchResult is null)
        {
            return;
        }

        try
        {
            Lookup = await provider.GetDetailsAsync(
                SelectedSearchResult,
                preferredLanguageTag,
                region,
                cancellationToken);
            StatusMessage = Lookup.ErrorMessage;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void Apply()
    {
        if (Lookup?.Metadata is not GameMetadata metadata || SelectedSearchResult is null)
        {
            return;
        }

        applyMetadata(SelectedSearchResult.Name, metadata);
        StatusMessage = Resources.MetadataSourceApplied;
    }
}
