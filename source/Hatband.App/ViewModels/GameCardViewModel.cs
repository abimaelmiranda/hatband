using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Hatband.App.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.App.ViewModels;

public partial class GameCardViewModel : ObservableObject
{
    private readonly string? coverSource;
    private readonly string? backgroundSource;
    private TimeZoneInfo displayTimeZone;

    [ObservableProperty]
    public partial Bitmap? CoverImage { get; set; }

    [ObservableProperty]
    public partial Bitmap? BackgroundImage { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public GameCardViewModel(
        Game game,
        string timeZoneId)
    {
        Game = game;
        displayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        coverSource = game.Metadata.Artwork.CoverImagePath;
        backgroundSource = game.Metadata.Artwork.BackgroundImagePath;
        AccentBrush = new SolidColorBrush(Color.Parse("#11161C"));
    }

    public Game Game { get; }

    public string Name => Game.Name;

    public string? Genre => Game.Metadata.Genre;

    public string? Developer => Game.Metadata.Developer;

    public string? Publisher => Game.Metadata.Publisher;

    public string? Description => Game.Metadata.Description;

    public string? ReleaseYear => Game.Metadata.ReleaseDate?.Year.ToString();

    public string? ReleaseDateDisplay => Game.Metadata.ReleaseDate?.ToString("d", System.Globalization.CultureInfo.CurrentCulture);

    public bool IsSteamSource => Game.SourceId == GameSourceId.Steam;

    public bool IsNonSteamSource => !IsSteamSource;

    public string SourceDisplayName
    {
        get
        {
            if (Game.SourceId is not GameSourceId sourceId)
            {
                return string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.Unavailable, Resources.Source);
            }

            return sourceId switch
            {
                GameSourceId.Manual => Resources.ManualGame,
                GameSourceId.Steam => "Steam",
                _ => string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.Unavailable, Resources.Source)
            };
        }
    }

    public string InstallStateLabel => Game.IsInstalled
        ? Resources.InstalledStatus
        : Resources.NotInstalledStatus;

    public string FavoriteLabel => Game.IsFavorite ? Resources.FavoriteStatus : Resources.NotFavoriteStatus;

    public string PlaytimeSummary
    {
        get
        {
            var duration = TimeSpan.FromSeconds(Game.PlaytimeSeconds);
            var hours = (long)duration.TotalHours;
            var minutes = duration.Minutes;
            if (hours == 0)
            {
                return string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.PlaytimeMinutes, minutes);
            }

            if (minutes == 0)
            {
                return string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.PlaytimeHours, hours);
            }

            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                Resources.PlaytimeHoursMinutes,
                hours,
                minutes);
        }
    }

    public string LastActivitySummary
    {
        get
        {
            if (Game.LastActivity is not DateTime lastActivity)
            {
                return Resources.NotPlayedYet;
            }

            return FormatDateInDisplayTimeZone(lastActivity);
        }
    }

    public bool HasTimeToBeat => Game.TimeToBeat is
    {
        MainStorySeconds: not null
    } or
    {
        MainStoryPlusExtrasSeconds: not null
    } or
    {
        CompletionistSeconds: not null
    };

    public string? TimeToBeatSummary
    {
        get
        {
            if (Game.TimeToBeat is not GameTimeToBeat timeToBeat)
            {
                return null;
            }

            var estimates = new List<string>();
            AddEstimate(estimates, Resources.HltbMainStory, timeToBeat.MainStorySeconds);
            AddEstimate(estimates, Resources.HltbMainStoryPlusExtras, timeToBeat.MainStoryPlusExtrasSeconds);
            AddEstimate(estimates, Resources.HltbCompletionist, timeToBeat.CompletionistSeconds);
            return estimates.Count == 0 ? null : string.Join("   ·   ", estimates);
        }
    }

    public string? CoverSource => coverSource;

    public string? BackgroundSource => backgroundSource;

    public IBrush AccentBrush { get; }

    public IBrush SelectionBrush => IsSelected ? Brushes.White : Brushes.Transparent;

    public Thickness SelectionBorderThickness => IsSelected ? new Thickness(4) : new Thickness(0);

    public double CardWidth => IsSelected ? 184 : 158;

    public double CardHeight => IsSelected ? 264 : 226;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectionBrush));
        OnPropertyChanged(nameof(SelectionBorderThickness));
        OnPropertyChanged(nameof(CardWidth));
        OnPropertyChanged(nameof(CardHeight));
    }

    public async Task LoadCoverAsync(Services.ArtworkImageLoader loader)
    {
        CoverImage = await loader.LoadAsync(CoverSource);
    }

    public async Task LoadBackgroundAsync(Services.ArtworkImageLoader loader)
    {
        BackgroundImage = await loader.LoadAsync(BackgroundSource);
    }

    public void UpdateTimeZone(string timeZoneId)
    {
        displayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        OnPropertyChanged(nameof(LastActivitySummary));
    }

    private static void AddEstimate(List<string> estimates, string label, long? seconds)
    {
        if (seconds is not long estimateSeconds)
        {
            return;
        }

        var duration = TimeSpan.FromSeconds(estimateSeconds);
        var totalHours = (long)duration.TotalHours;
        var minutes = duration.Minutes;
        var formattedDuration = minutes == 0
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.HltbHours, totalHours)
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.HltbHoursAndMinutes, totalHours, minutes);
        estimates.Add($"{label}: {formattedDuration}");
    }

    private string FormatDateInDisplayTimeZone(DateTime dateTime)
    {
        var utcDateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, displayTimeZone);
        return localDateTime.ToString("d", System.Globalization.CultureInfo.CurrentCulture);
    }
}
