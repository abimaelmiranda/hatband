namespace Hatband.Core.Abstractions.Games;

/// <summary>Identifies a source independently of its library, authentication or UI capabilities.</summary>
public interface IGameSourceInfo
{
    string SourceId { get; }

    string DisplayName { get; }

    /// <summary>
    /// Optional plugin-relative path to a source logo asset.
    /// </summary>
    string? LogoAssetPath => null;
}
