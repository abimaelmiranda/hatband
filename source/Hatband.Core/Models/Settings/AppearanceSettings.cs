using System.ComponentModel.DataAnnotations;
using LibraryTitleModeOption = Hatband.Core.Models.Settings.LibraryTitleMode;

namespace Hatband.Core.Models.Settings;

public sealed class AppearanceSettings
{
    public LibraryPosition LibraryPosition { get; set; } = LibraryPosition.Bottom;

    public bool ShowCoverTitles { get; set; } = true;

    [Range(0, 100)]
    public int BackgroundDimmingPercent { get; set; } = 14;

    public LibraryTitleMode LibraryTitleMode { get; set; } = LibraryTitleModeOption.None;

    public LibraryTitleFont TitleFont { get; set; } = LibraryTitleFont.Cinema;

    public bool NormalizeLibraryLogos { get; set; }

    public bool ShowLibraryStoreLogos { get; set; }
}
