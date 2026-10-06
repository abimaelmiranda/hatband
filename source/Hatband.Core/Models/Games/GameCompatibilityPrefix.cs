using IOPath = System.IO.Path;

namespace Hatband.Core.Models.Games;

/// <summary>
/// Prefix directory configured for a game's compatibility runtime.
/// </summary>
public sealed record GameCompatibilityPrefix
{
    public GameCompatibilityPrefix(bool isManaged, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!IOPath.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The compatibility prefix path must be absolute.", nameof(path));
        }

        IsManaged = isManaged;
        Path = IOPath.GetFullPath(path);
    }

    public bool IsManaged { get; }

    /// <summary>
    /// Absolute path to the prefix directory, whether Hatband-managed or user-selected.
    /// </summary>
    public string Path { get; }
}
