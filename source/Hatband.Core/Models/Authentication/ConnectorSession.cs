using Hatband.Core.Enums.Stores;
using System.Text.Json.Serialization;

namespace Hatband.Core.Models.Authentication;

/// <summary>
/// Holds the profile and authentication tokens returned by a connector.
/// </summary>
public sealed class ConnectorSession
{
    public required GameSourceId SourceId { get; init; }

    public required ConnectorAccount Profile { get; init; }

    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTimeOffset AccessTokenExpiresAt { get; init; }

    [JsonIgnore]
    public bool IsPersisted { get; set; }
}
