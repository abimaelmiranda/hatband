using System.IdentityModel.Tokens.Jwt;

namespace Hatband.Integrations.Authentication;

internal static class JwtTokenUtilities
{
    /// <summary>
    /// Reads a JWT expiration for refresh scheduling. This does not validate the token's signature.
    /// </summary>
    public static DateTimeOffset GetExpiration(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expiration = jwt.Payload.Expiration
            ?? throw new InvalidOperationException("The access token did not include an expiration time.");

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(expiration);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidOperationException("The access token contains an invalid expiration time.", exception);
        }
    }
}
