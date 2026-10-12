using System.Security.Cryptography;
using System.Text.Json;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Abstractions.FileSystem;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Authentication;

namespace Hatband.Infrastructure.Authentication;

/// <summary>
/// Encrypts connector sessions at rest using an encryption key held by the operating system.
/// </summary>
public sealed class EncryptedConnectorSessionStore : IConnectorSessionStore
{
    private const byte FileVersion = 1;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly IAppDataFileSystem fileSystem;
    private readonly ISecureSecretVault secretVault;
    private readonly SemaphoreSlim gate = new(1, 1);

    public EncryptedConnectorSessionStore(IAppDataFileSystem fileSystem, ISecureSecretVault secretVault)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(secretVault);
        this.fileSystem = fileSystem;
        this.secretVault = secretVault;
    }

    public async Task<StoredConnectorCredentials<TCredentials>?> LoadAsync<TCredentials>(
        string sourceId,
        CancellationToken cancellationToken = default)
        where TCredentials : class
    {
        ValidateSource(sourceId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var path = GetPath(sourceId);
            if (!fileSystem.FileExists(path))
            {
                return null;
            }

            var key = await secretVault.ReadAsync(GetVaultKey(sourceId), cancellationToken)
                ?? throw new InvalidDataException("The encryption key for the saved connector session is missing from the operating system's credential store.");
            try
            {
                if (key.Length != KeySize)
                {
                    throw new InvalidDataException("The encryption key for the saved connector session has an invalid size.");
                }

                var contents = await fileSystem.ReadAllBytesAsync(path, cancellationToken);
                if (contents.Length < 1 + NonceSize + TagSize || contents[0] != FileVersion)
                {
                    throw new InvalidDataException("The saved connector session has an unsupported or invalid format.");
                }

                return DecryptCredentials<TCredentials>(contents, key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync<TCredentials>(
        string sourceId,
        int version,
        TCredentials credentials,
        CancellationToken cancellationToken = default)
        where TCredentials : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentNullException.ThrowIfNull(credentials);
        ValidateSource(sourceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        await gate.WaitAsync(cancellationToken);
        byte[]? key = null;
        byte[]? plaintext = null;
        try
        {
            key = await secretVault.ReadAsync(GetVaultKey(sourceId), cancellationToken);
            if (key is null)
            {
                key = RandomNumberGenerator.GetBytes(KeySize);
                await secretVault.WriteAsync(GetVaultKey(sourceId), key, cancellationToken);
            }
            else if (key.Length != KeySize)
            {
                throw new InvalidDataException("The encryption key for the connector session has an invalid size.");
            }

            var storedCredentials = new StoredConnectorCredentials<TCredentials>(version, credentials);
            plaintext = JsonSerializer.SerializeToUtf8Bytes(storedCredentials);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];
            var contents = new byte[1 + NonceSize + TagSize + ciphertext.Length];
            try
            {
                using (var aes = new AesGcm(key, TagSize))
                {
                    aes.Encrypt(nonce, plaintext, ciphertext, tag);
                }

                contents[0] = FileVersion;
                nonce.CopyTo(contents, 1);
                tag.CopyTo(contents, 1 + NonceSize);
                ciphertext.CopyTo(contents, 1 + NonceSize + TagSize);
                await fileSystem.WriteAllBytesAtomicallyAsync(GetPath(sourceId), contents, cancellationToken);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(contents);
                CryptographicOperations.ZeroMemory(ciphertext);
                CryptographicOperations.ZeroMemory(nonce);
                CryptographicOperations.ZeroMemory(tag);
            }
        }
        finally
        {
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            if (key is not null)
            {
                CryptographicOperations.ZeroMemory(key);
            }

            gate.Release();
        }
    }

    public async Task DeleteAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        ValidateSource(sourceId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var path = GetPath(sourceId);
            if (fileSystem.FileExists(path))
            {
                File.Delete(fileSystem.GetPath(path));
            }

            await secretVault.DeleteAsync(GetVaultKey(sourceId), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private string GetPath(string sourceId) => $"sessions/{sourceId.ToString().ToLowerInvariant()}.enc";

    private static StoredConnectorCredentials<TCredentials> DecryptCredentials<TCredentials>(byte[] contents, byte[] key)
        where TCredentials : class
    {
        var ciphertext = contents.AsSpan(1 + NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                contents.AsSpan(1, NonceSize),
                ciphertext,
                contents.AsSpan(1 + NonceSize, TagSize),
                plaintext);
            var storedCredentials = JsonSerializer.Deserialize<StoredConnectorCredentials<TCredentials>>(plaintext)
                ?? throw new InvalidDataException("The saved connector credentials are empty.");
            if (storedCredentials.Version <= 0 || storedCredentials.Credentials is null)
            {
                throw new InvalidDataException("The saved connector credentials are incomplete.");
            }

            return storedCredentials;
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("The saved connector session failed its integrity check.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static string GetVaultKey(string sourceId) => $"org.hatband.session-key.{sourceId.ToString().ToLowerInvariant()}";

    private static void ValidateSource(string sourceId)
    {
        GameSourceIds.Validate(sourceId);
    }
}
