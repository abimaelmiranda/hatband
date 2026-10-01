using Hatband.Core.Enums.Stores;

namespace Hatband.Core.Models.Authentication;

/// <summary>
/// Describes the account authenticated with a game store connector.
/// </summary>
public sealed record ConnectorAccount
{
    public required GameSourceId SourceId { get; init; }

    public required string AccountId { get; init; }

    public required string DisplayName { get; init; }
}
