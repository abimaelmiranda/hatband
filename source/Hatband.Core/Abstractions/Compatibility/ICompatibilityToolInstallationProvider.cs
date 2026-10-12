using Hatband.Core.Models.Compatibility;

namespace Hatband.Core.Abstractions.Compatibility;

/// <summary>Installs releases published by one or more compatibility tool providers.</summary>
public interface ICompatibilityToolInstallationProvider
{
    string Id { get; }

    IReadOnlyCollection<string> ReleaseProviderIds { get; }

    Task InstallAsync(CompatibilityToolRelease release, CancellationToken cancellationToken = default);
}
