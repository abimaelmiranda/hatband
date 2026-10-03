namespace Hatband.Core.Models;

public sealed record ProtonReleaseCatalog(
    string ProviderId,
    string ProviderName,
    IReadOnlyList<ProtonRelease> Releases,
    string? ErrorMessage);
