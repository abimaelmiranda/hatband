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

    public async Task<ConnectorSession?> LoadAsync(GameSourceId sourceId, CancellationToken cancellationToken = default)
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

                return DecryptSession(sourceId, contents, key);
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

    public async Task SaveAsync(ConnectorSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateSource(session.SourceId);
        if (session.Profile is null || session.Profile.SourceId != session.SourceId ||
            string.IsNullOrWhiteSpace(session.Profile.AccountId) ||
            string.IsNullOrWhiteSpace(session.AccessToken) ||
            string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            throw new ArgumentException("The connector session is missing a profile or authentication token.", nameof(session));
        }

        await gate.WaitAsync(cancellationToken);
        byte[]? key = null;
        byte[]? plaintext = null;
        try
        {
            key = await secretVault.ReadAsync(GetVaultKey(session.SourceId), cancellationToken);
            if (key is null)
            {
                key = RandomNumberGenerator.GetBytes(KeySize);
                await secretVault.WriteAsync(GetVaultKey(session.SourceId), key, cancellationToken);
            }
            else if (key.Length != KeySize)
            {
                throw new InvalidDataException("The encryption key for the connector session has an invalid size.");
            }

            plaintext = JsonSerializer.SerializeToUtf8Bytes(session);
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
                await fileSystem.WriteAllBytesAtomicallyAsync(GetPath(session.SourceId), contents, cancellationToken);
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

    public async Task DeleteAsync(GameSourceId sourceId, CancellationToken cancellationToken = default)
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

    private string GetPath(GameSourceId sourceId) => $"sessions/{sourceId.ToString().ToLowerInvariant()}.enc";

    private static ConnectorSession DecryptSession(GameSourceId sourceId, byte[] contents, byte[] key)
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
            var session = JsonSerializer.Deserialize<ConnectorSession>(plaintext)
                ?? throw new InvalidDataException("The saved connector session is empty.");
            if (session.SourceId != sourceId || session.Profile is null || session.Profile.SourceId != sourceId ||
                string.IsNullOrWhiteSpace(session.Profile.AccountId) ||
                string.IsNullOrWhiteSpace(session.AccessToken) ||
                string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                throw new InvalidDataException("The saved connector session is incomplete or belongs to another connector.");
            }

            return session;
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

    private static string GetVaultKey(GameSourceId sourceId) => $"org.hatband.session-key.{sourceId.ToString().ToLowerInvariant()}";

    private static void ValidateSource(GameSourceId sourceId)
    {
        if (!Enum.IsDefined(sourceId))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceId), sourceId, "The connector source is not supported.");
        }
    }
}
