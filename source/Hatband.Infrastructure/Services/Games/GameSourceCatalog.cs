using Hatband.Core.Abstractions.Games;

namespace Hatband.Infrastructure.Services.Games;

public sealed class GameSourceCatalog : IGameSourceCatalog
{
    private readonly IReadOnlyDictionary<string, string> displayNames;

    public GameSourceCatalog(IEnumerable<IGameSourceInfo> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(source.DisplayName);
            if (names.TryGetValue(source.SourceId, out var existingName) && existingName != source.DisplayName)
            {
                throw new InvalidOperationException($"Source '{source.SourceId}' declares conflicting display names.");
            }

            names[source.SourceId] = source.DisplayName;
        }

        displayNames = names;
    }

    public string GetDisplayName(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (!displayNames.TryGetValue(sourceId, out var displayName))
        {
            throw new InvalidOperationException($"No source information is registered for '{sourceId}'.");
        }

        return displayName;
    }
}
