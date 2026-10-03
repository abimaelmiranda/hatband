using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

public interface IProtonReleaseProvider
{
    string Id { get; }

    string DisplayName { get; }

    Task<IReadOnlyList<ProtonRelease>> GetLatestReleasesAsync(CancellationToken cancellationToken = default);
}
