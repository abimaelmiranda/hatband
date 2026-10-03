using Hatband.App.Localization;
using Hatband.Core.Enums;
using Hatband.Core.Models;

namespace Hatband.App.ViewModels.Settings;

public sealed class ProtonToolViewModel
{
    public ProtonToolViewModel(ProtonTool protonTool)
    {
        ArgumentNullException.ThrowIfNull(protonTool);
        Name = protonTool.Name;
        SourceName = protonTool.Source switch
        {
            ProtonToolSource.Hatband => Resources.ProtonManagedSource,
            ProtonToolSource.Steam => "Steam",
            _ => throw new ArgumentOutOfRangeException(nameof(protonTool))
        };
    }

    public string Name { get; }

    public string SourceName { get; }
}
