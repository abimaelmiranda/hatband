using System.Security.Cryptography;
using System.Text;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Plugins.SteamGridDB.Localization;

namespace Hatband.Plugins.SteamGridDB.Authentication;

public sealed class SteamGridDBCredentialService
{
    public const string SecretKey = "hatband.plugin.steamgriddb.api-key";

    private readonly ISecureSecretVault secretVault;

    public SteamGridDBCredentialService(ISecureSecretVault secretVault)
    {
        ArgumentNullException.ThrowIfNull(secretVault);
        this.secretVault = secretVault;
    }

    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await secretVault.ReadAsync(SecretKey, cancellationToken)
            ?? throw new InvalidOperationException(SteamGridDBResources.MissingKeyForApiMode);
        try
        {
            var value = Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(SteamGridDBResources.EmptyApiKey);
            }

            ValidateApiKey(value);
            return value;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await secretVault.ReadAsync(SecretKey, cancellationToken);
        if (bytes is null)
        {
            return false;
        }

        CryptographicOperations.ZeroMemory(bytes);
        return true;
    }

    public async Task SaveAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var normalizedApiKey = apiKey.Trim();
        ValidateApiKey(normalizedApiKey);
        var bytes = Encoding.UTF8.GetBytes(normalizedApiKey);
        try
        {
            await secretVault.WriteAsync(SecretKey, bytes, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default) =>
        secretVault.DeleteAsync(SecretKey, cancellationToken);

    public static void ValidateApiKey(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (apiKey.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new InvalidOperationException(SteamGridDBResources.InvalidApiKey);
        }
    }
}
