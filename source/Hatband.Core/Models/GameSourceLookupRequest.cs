using Hatband.Core.Enums.Stores;

namespace Hatband.Core.Models;

/// <summary>
/// Context for a user-requested lookup against one of Hatband's game sources.
/// </summary>
public sealed record GameSourceLookupRequest
{
    public required string GameName { get; init; }

    public GameSourceId? SourceId { get; init; }

    public string? SourceGameId { get; init; }
}
