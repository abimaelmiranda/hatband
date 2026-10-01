namespace Hatband.Integrations.HowLongToBeat.Models;

internal sealed record HowLongToBeatSearchSessionToken(
    string ApiPath,
    string Token,
    string? HoneypotKey,
    string? HoneypotValue,
    DateTimeOffset CreatedAtUtc);
