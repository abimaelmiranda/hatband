using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Games;

/// <summary>
/// Creates and caches a display-ready version of a game's logo without changing its source artwork.
/// </summary>
public interface IGameLogoNormalizationService
{
    Task<string?> TryGetNormalizedPathAsync(
        Guid gameId,
        string originalPath,
        CancellationToken cancellationToken = default);

    Task<string?> GetNormalizedPathAsync(
        Guid gameId,
        string originalPath,
        CancellationToken cancellationToken = default);

    Task NormalizeLibraryAsync(
        IProgress<GameLogoNormalizationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
