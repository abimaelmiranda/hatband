namespace Hatband.Core.Models.Compatibility;

public sealed record CompatibilityToolReleaseCatalog(
    string ProviderId,
    string ProviderName,
    IReadOnlyList<CompatibilityToolRelease> Releases,
    string? ErrorMessage);
