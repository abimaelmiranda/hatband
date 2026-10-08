namespace Hatband.Core.Abstractions.Authentication;

/// <summary>
/// Reads and writes small secrets in the operating system's credential store.
/// </summary>
public interface ISecureSecretVault
{
    Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default);

    Task WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
