namespace Hatband.Core.Models.Authentication;

/// <summary>
/// Versioned credentials saved by a connector in the encrypted session store.
/// </summary>
public sealed record StoredConnectorCredentials<TCredentials>(int Version, TCredentials Credentials)
    where TCredentials : class;
