using Avalonia;

namespace Hatband.App.Services;

/// <summary>Applies the user's scale to the shared semantic typography sizes.</summary>
public static class TypographyScale
{
    public const string Micro = "Typography.Micro";
    public const string Caption = "Typography.Caption";
    public const string BodySmall = "Typography.BodySmall";
    public const string Body = "Typography.Body";
    public const string BodyEmphasis = "Typography.BodyEmphasis";
    public const string Subheading = "Typography.Subheading";
    public const string SectionTitle = "Typography.SectionTitle";
    public const string ScreenTitle = "Typography.ScreenTitle";
    public const string GameTitle = "Typography.GameTitle";
    public const string LibraryGameTitle = "Typography.LibraryGameTitle";
    public const string LibraryGameTitleLineHeight = "Typography.LibraryGameTitleLineHeight";
    public const string Display = "Typography.Display";

    public static void Apply(int scalePercent)
    {
        if (scalePercent is < 80 or > 140)
        {
            throw new ArgumentOutOfRangeException(nameof(scalePercent), "Text scale must be between 80 and 140 percent.");
        }

        var scale = scalePercent / 100d;
        var resources = (Application.Current ?? throw new InvalidOperationException("The application has not been initialized.")).Resources;
        resources[Micro] = 10 * scale;
        resources[Caption] = 12 * scale;
        resources[BodySmall] = 13 * scale;
        resources[Body] = 14 * scale;
        resources[BodyEmphasis] = 16 * scale;
        resources[Subheading] = 18 * scale;
        resources[SectionTitle] = 20 * scale;
        resources[ScreenTitle] = 24 * scale;
        resources[GameTitle] = 30 * scale;
        resources[LibraryGameTitle] = 40 * scale;
        resources[LibraryGameTitleLineHeight] = 48 * scale;
        resources[Display] = 32 * scale;
    }
}
