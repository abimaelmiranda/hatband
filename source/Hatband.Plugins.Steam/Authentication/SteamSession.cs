using Hatband.Core.Models.Authentication;

namespace Hatband.Plugins.Steam.Authentication;

internal sealed class SteamSession
{
    public required ConnectorAccount Account { get; init; }

    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTimeOffset AccessTokenExpiresAt { get; init; }

    public bool IsPersisted { get; set; }
}

internal sealed class SteamCredentials
{
    public required ConnectorAccount Account { get; init; }

    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTimeOffset AccessTokenExpiresAt { get; init; }
}
