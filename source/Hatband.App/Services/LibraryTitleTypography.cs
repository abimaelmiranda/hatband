using Avalonia.Media;
using Hatband.Core.Models.Settings;

namespace Hatband.App.Services;

/// <summary>Maps library title styles to bundled font families and their available weights.</summary>
internal static class LibraryTitleTypography
{
    private static readonly FontFamily CinemaFont = new("avares://Hatband.App/Assets/Fonts/BebasNeue#Bebas Neue");
    private static readonly FontFamily FuturisticFont = new("avares://Hatband.App/Assets/Fonts/Rajdhani#Rajdhani");
    private static readonly FontFamily EditorialFont = new("avares://Hatband.App/Assets/Fonts/Spectral#Spectral");
    private static readonly FontFamily LightFont = new("avares://Hatband.App/Assets/Fonts/Lato#Lato");

    public static FontFamily GetFontFamily(LibraryTitleFont style) => style switch
    {
        LibraryTitleFont.Cinema => CinemaFont,
        LibraryTitleFont.Futuristic => FuturisticFont,
        LibraryTitleFont.Editorial => EditorialFont,
        LibraryTitleFont.Light => LightFont,
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "The title font must be a supported value.")
    };

    public static FontWeight GetFontWeight(LibraryTitleFont style) => style switch
    {
        LibraryTitleFont.Cinema => FontWeight.Normal,
        LibraryTitleFont.Light => FontWeight.Light,
        LibraryTitleFont.Futuristic or LibraryTitleFont.Editorial => FontWeight.SemiBold,
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "The title font must be a supported value.")
    };
}
