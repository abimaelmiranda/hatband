using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.Localization;
using Hatband.App.Services;
using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Enums.Host;

namespace Hatband.App.ViewModels;

public partial class GameCardViewModel : ObservableObject
{
    private readonly string? coverSource;
    private readonly string? backgroundSource;
    private readonly DateTimeDisplayFormatter dateTimeDisplayFormatter;
    private readonly HostPlatformCompatibilityStatus platformCompatibilityStatus;

    [ObservableProperty]
    public partial Bitmap? CoverImage { get; set; }

    [ObservableProperty]
    public partial Bitmap? BackgroundImage { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public GameCardViewModel(
        Game game,
        DateTimeDisplayFormatter dateTimeDisplayFormatter,
        IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(dateTimeDisplayFormatter);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        Game = game;
        this.dateTimeDisplayFormatter = dateTimeDisplayFormatter;
        platformCompatibilityStatus = game.GetHostPlatformCompatibilityStatus(hostSystemInfo.Platform);
        coverSource = game.Artwork.CoverImagePath;
        backgroundSource = game.Artwork.BackgroundImagePath;
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

    public string? PlatformCompatibilityLabel
    {
        get
        {
            return platformCompatibilityStatus switch
            {
                HostPlatformCompatibilityStatus.Unknown => Resources.UnknownPlatformSupport,
                HostPlatformCompatibilityStatus.Native => Resources.NativePlatformSupport,
                HostPlatformCompatibilityStatus.RequiresCompatibilityTool => Resources.RequiresCompatibilityTool,
                HostPlatformCompatibilityStatus.Unsupported => Resources.UnsupportedPlatform,
                _ => throw new ArgumentOutOfRangeException(nameof(platformCompatibilityStatus))
            };
        }
    }

    public bool HasProtonCompatibility => Game.CompatibilityLayer is not null;

    public string ProtonCompatibilityLabel => Game.CompatibilityLayer?.Tier switch
    {
        GameCompatibilityTier.Platinum => Resources.ProtonTierPlatinum,
        GameCompatibilityTier.Gold => Resources.ProtonTierGold,
        GameCompatibilityTier.Silver => Resources.ProtonTierSilver,
        GameCompatibilityTier.Bronze => Resources.ProtonTierBronze,
        GameCompatibilityTier.Borked => Resources.ProtonTierBorked,
        GameCompatibilityTier.Unknown => Resources.ProtonTierUnknown,
        null => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(Game.CompatibilityLayer.Tier))
    };

    public IBrush ProtonCompatibilityBackground => Game.CompatibilityLayer?.Tier switch
    {
        GameCompatibilityTier.Platinum => new SolidColorBrush(Color.Parse("#E5E4E2")),
        GameCompatibilityTier.Gold => new SolidColorBrush(Color.Parse("#FFD700")),
        GameCompatibilityTier.Silver => new SolidColorBrush(Color.Parse("#C0C0C0")),
        GameCompatibilityTier.Bronze => new SolidColorBrush(Color.Parse("#CD7F32")),
        GameCompatibilityTier.Borked => new SolidColorBrush(Color.Parse("#FF5252")),
        GameCompatibilityTier.Unknown => new SolidColorBrush(Color.Parse("#687681")),
        null => Brushes.Transparent,
        _ => throw new ArgumentOutOfRangeException(nameof(Game.CompatibilityLayer.Tier))
    };

    public IBrush ProtonCompatibilityForeground => Game.CompatibilityLayer?.Tier is GameCompatibilityTier.Unknown
        ? Brushes.White
        : new SolidColorBrush(Color.Parse("#11161C"));

    public bool SupportsWindows => Game.Metadata.NativePlatforms?.HasFlag(GamePlatform.Windows) == true;

    public bool SupportsMacOS => Game.Metadata.NativePlatforms?.HasFlag(GamePlatform.MacOS) == true;

    public bool SupportsLinux => Game.Metadata.NativePlatforms?.HasFlag(GamePlatform.Linux) == true;

    public bool HasUnknownNativePlatforms => Game.Metadata.NativePlatforms is null;

    public bool HasNoNativePlatforms => Game.Metadata.NativePlatforms == GamePlatform.None;

    public bool IsCompatibleWithHost
    {
        get
        {
            return platformCompatibilityStatus is
                HostPlatformCompatibilityStatus.Unknown or
                HostPlatformCompatibilityStatus.Native or
                HostPlatformCompatibilityStatus.RequiresCompatibilityTool;
        }
    }

    public bool IsSteamSource => Game.SourceId == GameSourceId.Steam;

    public bool IsNonSteamSource => !IsSteamSource;

    public string SourceDisplayName
    {
        get
        {
            return Game.SourceId switch
            {
                GameSourceId.Manual => Resources.ManualGame,
                GameSourceId.Steam => "Steam",
                _ => string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.Unavailable, Resources.Source)
            };
        }
    }

    public string InstallStateLabel => Game.InstallationInfo is not null
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

            return dateTimeDisplayFormatter.FormatUtcDate(lastActivity);
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

    public void RefreshTimeZoneDisplay()
    {
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
}
