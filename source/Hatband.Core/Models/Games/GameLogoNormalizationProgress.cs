namespace Hatband.Core.Models.Games;

public sealed record GameLogoNormalizationProgress(
    Guid GameId,
    int CompletedCount,
    int TotalCount,
    string? NormalizedPath,
    string? ErrorMessage);
