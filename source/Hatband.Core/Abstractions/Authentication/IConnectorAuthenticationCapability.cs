using Hatband.Core.Enums.Stores;

namespace Hatband.Core.Abstractions.Authentication;

/// <summary>
/// Identifies an optional authentication capability provided by a store connector.
/// </summary>
public interface IConnectorAuthenticationCapability
{
    GameSourceId SourceId { get; }
}
