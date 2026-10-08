using System.ComponentModel.DataAnnotations;

namespace Hatband.Core.Models.Settings;

public sealed class AppearanceSettings
{
    public LibraryPosition LibraryPosition { get; set; } = LibraryPosition.Bottom;

    public bool ShowCoverTitles { get; set; } = true;

    [Range(0, 100)]
    public int BackgroundDimmingPercent { get; set; } = 14;

    public bool ShowSelectedGameTitle { get; set; }

    public LibraryTitleFont TitleFont { get; set; } = LibraryTitleFont.Cinema;
}
