namespace Hatband.Core.Models.Settings;

/// <summary>
/// Root configuration object for Hatband-wide user preferences.
/// Increment Version when a settings migration is required.
/// </summary>
public sealed class HatbandSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public GeneralSettings General { get; set; } = new();
}