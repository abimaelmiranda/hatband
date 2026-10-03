using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

public interface IProtonToolInstallationService
{
    Task InstallAsync(ProtonRelease release, CancellationToken cancellationToken = default);
}
