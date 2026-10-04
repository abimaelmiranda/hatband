using Hatband.Core.Models.Compatibility;

namespace Hatband.Core.Abstractions.Compatibility;

public interface ICompatibilityToolReleaseProvider
{
    string Id { get; }

    string DisplayName { get; }

    Task<IReadOnlyList<CompatibilityToolRelease>> GetLatestReleasesAsync(
        CancellationToken cancellationToken = default);
}
