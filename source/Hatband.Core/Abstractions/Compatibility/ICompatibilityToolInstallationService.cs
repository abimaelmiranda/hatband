using Hatband.Core.Models.Compatibility;

namespace Hatband.Core.Abstractions.Compatibility;

public interface ICompatibilityToolInstallationService
{
    Task InstallAsync(
        CompatibilityToolRelease release,
        CancellationToken cancellationToken = default);
}
