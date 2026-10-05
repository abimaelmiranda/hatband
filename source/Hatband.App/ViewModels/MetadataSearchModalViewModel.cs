using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.ViewModels;

/// <summary>Owns metadata dialog query/focus state while sharing result sources with the draft screen.</summary>
public partial class MetadataSearchModalViewModel : ModalViewModel<GameMetadataSourceViewModel>
{
    private readonly Func<string, CancellationToken, Task> _searchAsync;
    private CancellationTokenSource? _searchCancellation;

    /// <summary>Creates a metadata lookup dialog that reuses the parent screen's provider results.</summary>
    public MetadataSearchModalViewModel(
        ObservableCollection<GameMetadataSourceViewModel> sources,
        string initialQuery,
        Func<string, CancellationToken, Task> searchAsync)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(searchAsync);
        Sources = sources;
        SearchQuery = initialQuery;
        _searchAsync = searchAsync;
        foreach (var source in Sources)
        {
            source.PropertyChanged += OnSourcePropertyChanged;
        }
    }

    public ObservableCollection<GameMetadataSourceViewModel> Sources { get; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; }

    [ObservableProperty]
    public partial int SelectedSourceIndex { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    public GameMetadataSourceViewModel? SelectedSource =>
        (uint)SelectedSourceIndex < (uint)Sources.Count ? Sources[SelectedSourceIndex] : null;

    public bool HasSources => Sources.Count > 0;

    public bool CanSearch => !IsSearching && !string.IsNullOrWhiteSpace(SearchQuery);

    public bool CanApplySelectedResult => SelectedSource is { HasLookup: true, IsSearching: false };

    [RelayCommand]
    private Task Search(CancellationToken cancellationToken) => SearchAsync(cancellationToken);

    /// <summary>Runs a query across the available metadata providers for this modal's lifetime.</summary>
    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSearch)
        {
            return;
        }

        using var searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _searchCancellation?.Cancel();
        _searchCancellation = searchCancellation;
        IsSearching = true;
        try
        {
            await _searchAsync(SearchQuery.Trim(), searchCancellation.Token);
        }
        catch (OperationCanceledException) when (searchCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            IsSearching = false;
            if (ReferenceEquals(_searchCancellation, searchCancellation))
            {
                _searchCancellation = null;
            }
        }
    }

    [RelayCommand]
    private void ApplySelectedResult()
    {
        if (CanApplySelectedResult && SelectedSource is { } source)
        {
            DetachFromSources();
            Complete(source);
        }
    }

    [RelayCommand]
    private void CancelSearch()
    {
        _searchCancellation?.Cancel();
        DetachFromSources();
        Cancel();
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(GameMetadataSourceViewModel.HasLookup) or
            nameof(GameMetadataSourceViewModel.IsSearching) or
            nameof(GameMetadataSourceViewModel.SelectedSearchResult))
        {
            OnPropertyChanged(nameof(CanApplySelectedResult));
        }
    }

    private void DetachFromSources()
    {
        foreach (var source in Sources)
        {
            source.PropertyChanged -= OnSourcePropertyChanged;
        }
    }

    protected override void OnCompleted(ModalCompletion<GameMetadataSourceViewModel> completion)
    {
        _searchCancellation?.Cancel();
        DetachFromSources();
        base.OnCompleted(completion);
    }

    partial void OnSelectedSourceIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SelectedSource));
        OnPropertyChanged(nameof(CanApplySelectedResult));
    }

    partial void OnSearchQueryChanged(string value) => OnPropertyChanged(nameof(CanSearch));

    partial void OnIsSearchingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(CanApplySelectedResult));
    }
}
