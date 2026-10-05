using Hatband.App.Localization;

namespace Hatband.App.ViewModels.Settings;

public sealed class ProtonToolViewModel
{
    public ProtonToolViewModel(CompatibilityTool protonTool)
    {
        ArgumentNullException.ThrowIfNull(protonTool);
        Name = protonTool.Name;
        SourceName = protonTool.Source switch
        {
            CompatibilityToolSource.Hatband => Resources.ProtonManagedSource,
            CompatibilityToolSource.Steam => "Steam",
            _ => throw new ArgumentOutOfRangeException(nameof(protonTool))
        };
    }

    public string Name { get; }

    public string SourceName { get; }
}
