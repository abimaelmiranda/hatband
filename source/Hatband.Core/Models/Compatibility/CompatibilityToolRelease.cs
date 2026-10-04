namespace Hatband.Core.Models.Compatibility;

public sealed record CompatibilityToolRelease(
    string Id,
    string ProviderId,
    string ProviderName,
    string Version,
    string DisplayName,
    string Variant,
    DateTimeOffset PublishedAt,
    string DownloadUrl,
    string ArchiveFileName);
