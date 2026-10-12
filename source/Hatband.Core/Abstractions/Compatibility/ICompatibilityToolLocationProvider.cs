using Hatband.Core.Models.Compatibility;

namespace Hatband.Core.Abstractions.Compatibility;

/// <summary>Contributes directories in which compatibility tools can be found or installed.</summary>
public interface ICompatibilityToolLocationProvider
{
    string ProvisionerId { get; }

    Task<IReadOnlyList<CompatibilityToolLocation>> GetLocationsAsync(CancellationToken cancellationToken = default);
}
