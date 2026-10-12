using Hatband.Core.Abstractions.Compatibility;
using Hatband.Core.Enums.Games;
using Hatband.Core.Models.Compatibility;
using Hatband.Plugins.Steam.Abstractions;

namespace Hatband.Plugins.Steam.Compatibility;

public sealed class SteamCompatibilityToolLocationProvider : ICompatibilityToolLocationProvider
{
    public string ProvisionerId => "proton";

    private readonly ISteamInstallationService installations;

    public SteamCompatibilityToolLocationProvider(ISteamInstallationService installations)
    {
        ArgumentNullException.ThrowIfNull(installations);
        this.installations = installations;
    }

    public async Task<IReadOnlyList<CompatibilityToolLocation>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        var steamInstallations = await installations.GetInstallationsAsync(cancellationToken);
        var locations = new List<CompatibilityToolLocation>();
        foreach (var installation in steamInstallations)
        {
            locations.Add(new CompatibilityToolLocation(
                Path.Combine(installation.RootPath, "compatibilitytools.d"),
                CompatibilityToolSource.Steam,
                LinkToManagedDirectory: true,
                CanInstall: true));

            foreach (var directory in installation.Libraries.Select(library => library.Path)
                .Prepend(installation.RootPath).Distinct(StringComparer.Ordinal))
            {
                locations.Add(new CompatibilityToolLocation(
                    Path.Combine(directory, "steamapps", "common"),
                    CompatibilityToolSource.Steam,
                    LinkToManagedDirectory: false,
                    CanInstall: false));
            }
        }

        return locations;
    }
}
