using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Hatband.Infrastructure.Authentication;

public sealed partial class OperatingSystemSecretVault
{
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;

    private static byte[]? ReadWindows(string key)
    {
        if (!CredRead(key, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168)
            {
                return null;
            }

            throw new Win32Exception(error, "Could not read the Hatband key from Windows Credential Manager.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<WindowsCredential>(credentialPointer);
            var bytes = new byte[checked((int)credential.CredentialBlobSize)];
            if (bytes.Length > 0)
            {
                if (credential.CredentialBlob == IntPtr.Zero)
                {
                    throw new InvalidDataException("Windows Credential Manager returned an invalid secret buffer.");
                }

                Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            }
            return bytes;
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    private static void WriteWindows(string key, ReadOnlySpan<byte> value)
    {
        if (value.Length > 2560)
        {
            throw new ArgumentException("The secret exceeds the Windows Credential Manager size limit.", nameof(value));
        }

        var secret = value.ToArray();
        var secretPointer = IntPtr.Zero;
        var targetPointer = IntPtr.Zero;
        var userPointer = IntPtr.Zero;
        try
        {
            secretPointer = Marshal.AllocHGlobal(secret.Length);
            targetPointer = Marshal.StringToHGlobalUni(key);
            userPointer = Marshal.StringToHGlobalUni(Environment.UserName);
            Marshal.Copy(secret, 0, secretPointer, secret.Length);
            var credential = new WindowsCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = targetPointer,
                CredentialBlobSize = (uint)secret.Length,
                CredentialBlob = secretPointer,
                Persist = CredentialPersistLocalMachine,
                UserName = userPointer
            };

            var credentialPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WindowsCredential>());
            try
            {
                Marshal.StructureToPtr(credential, credentialPointer, false);
                if (!CredWrite(credentialPointer, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not save the Hatband key to Windows Credential Manager.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(credentialPointer);
            }
        }
        finally
        {
            for (var index = 0; secretPointer != IntPtr.Zero && index < secret.Length; index++)
            {
                Marshal.WriteByte(secretPointer, index, 0);
            }

            Marshal.FreeHGlobal(secretPointer);
            Marshal.FreeHGlobal(targetPointer);
            Marshal.FreeHGlobal(userPointer);
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static void DeleteWindows(string key)
    {
        if (!CredDelete(key, CredentialTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1168)
            {
                throw new Win32Exception(error, "Could not remove the Hatband key from Windows Credential Manager.");
            }
        }
    }

    [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredWrite(IntPtr credential, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredDeleteW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredDelete(string target, uint type, uint flags);

    [LibraryImport("advapi32.dll")]
    private static partial void CredFree(IntPtr buffer);
}
