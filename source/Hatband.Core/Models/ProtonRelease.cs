namespace Hatband.Core.Models;

public sealed record ProtonRelease(
    string Id,
    string ProviderId,
    string ProviderName,
    string Version,
    string DisplayName,
    string Variant,
    DateTimeOffset PublishedAt,
    string DownloadUrl,
    string ArchiveFileName);
