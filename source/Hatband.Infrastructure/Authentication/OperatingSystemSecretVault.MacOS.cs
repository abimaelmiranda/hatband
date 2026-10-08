using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Hatband.Infrastructure.Authentication;

public sealed partial class OperatingSystemSecretVault
{
    private const int KeychainItemNotFound = -25300;
    private static readonly Lazy<IntPtr> CoreFoundationLibrary = new(() => NativeLibrary.Load(
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation"));
    private static readonly Lazy<IntPtr> SecurityLibrary = new(() => NativeLibrary.Load(
        "/System/Library/Frameworks/Security.framework/Security"));

    private static byte[]? ReadMacOS(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = CreateKeychainQuery(key, includeReturnData: true);
        var item = IntPtr.Zero;
        try
        {
            var status = SecItemCopyMatching(query, out item);
            if (status == KeychainItemNotFound)
            {
                return null;
            }

            EnsureKeychainSuccess(status);
            if (item == IntPtr.Zero || CFGetTypeID(item) != CFDataGetTypeID())
            {
                throw new InvalidDataException("The macOS Keychain returned an invalid secret object.");
            }

            var length = checked((int)CFDataGetLength(item));
            var encodedSecret = new byte[length];
            if (length > 0)
            {
                var bytes = CFDataGetBytePtr(item);
                if (bytes == IntPtr.Zero)
                {
                    throw new InvalidDataException("The macOS Keychain returned an invalid secret buffer.");
                }

                Marshal.Copy(bytes, encodedSecret, 0, length);
            }
            try
            {
                return Convert.FromBase64String(Encoding.UTF8.GetString(encodedSecret));
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("The encryption key saved in the macOS Keychain has an invalid format.", exception);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(encodedSecret);
            }
        }
        finally
        {
            ReleaseNativeObject(item);
            CFRelease(query);
        }
    }

    private static void WriteMacOS(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var encodedSecret = Convert.ToBase64String(value.Span);
        var secretBytes = Encoding.UTF8.GetBytes(encodedSecret);
        var query = IntPtr.Zero;
        var update = IntPtr.Zero;
        var attributes = IntPtr.Zero;
        try
        {
            query = CreateKeychainQuery(key, includeReturnData: false);
            update = CreateKeychainDataAttributes(secretBytes);
            var status = SecItemUpdate(query, update);
            if (status == KeychainItemNotFound)
            {
                attributes = CreateKeychainItemAttributes(key, secretBytes);
                status = SecItemAdd(attributes, IntPtr.Zero);
            }

            EnsureKeychainSuccess(status);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            ReleaseNativeObject(attributes);
            ReleaseNativeObject(update);
            ReleaseNativeObject(query);
        }
    }

    private static void DeleteMacOS(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = CreateKeychainQuery(key, includeReturnData: false);
        try
        {
            var status = SecItemDelete(query);
            if (status != KeychainItemNotFound)
            {
                EnsureKeychainSuccess(status);
            }

        }
        finally
        {
            CFRelease(query);
        }
    }

    private static IntPtr CreateKeychainQuery(string key, bool includeReturnData)
    {
        var query = CreateNativeDictionary();
        try
        {
            CFDictionarySetValue(query, GetExportedObject(SecurityLibrary.Value, "kSecClass"), GetExportedObject(SecurityLibrary.Value, "kSecClassGenericPassword"));
            AddStringAttribute(query, "kSecAttrService", "org.hatband.session-key");
            AddStringAttribute(query, "kSecAttrAccount", key);
            if (includeReturnData)
            {
                CFDictionarySetValue(query, GetExportedObject(SecurityLibrary.Value, "kSecReturnData"), GetExportedObject(CoreFoundationLibrary.Value, "kCFBooleanTrue"));
                CFDictionarySetValue(query, GetExportedObject(SecurityLibrary.Value, "kSecMatchLimit"), GetExportedObject(SecurityLibrary.Value, "kSecMatchLimitOne"));
            }

            return query;
        }
        catch
        {
            CFRelease(query);
            throw;
        }
    }

    private static IntPtr CreateKeychainDataAttributes(byte[] secretBytes)
    {
        var attributes = CreateNativeDictionary();
        try
        {
            var data = CFDataCreate(IntPtr.Zero, secretBytes, secretBytes.Length);
            if (data == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not allocate the Keychain secret data.");
            }
            try
            {
                CFDictionarySetValue(attributes, GetExportedObject(SecurityLibrary.Value, "kSecValueData"), data);
            }
            finally
            {
                CFRelease(data);
            }

            return attributes;
        }
        catch
        {
            CFRelease(attributes);
            throw;
        }
    }

    private static IntPtr CreateKeychainItemAttributes(string key, byte[] secretBytes)
    {
        var attributes = CreateKeychainDataAttributes(secretBytes);
        try
        {
            CFDictionarySetValue(attributes, GetExportedObject(SecurityLibrary.Value, "kSecClass"), GetExportedObject(SecurityLibrary.Value, "kSecClassGenericPassword"));
            AddStringAttribute(attributes, "kSecAttrService", "org.hatband.session-key");
            AddStringAttribute(attributes, "kSecAttrAccount", key);
            return attributes;
        }
        catch
        {
            CFRelease(attributes);
            throw;
        }
    }

    private static void AddStringAttribute(IntPtr dictionary, string attributeName, string value)
    {
        var nativeValue = CFStringCreateWithCString(IntPtr.Zero, value, 0x08000100);
        if (nativeValue == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not create a Keychain attribute.");
        }

        try
        {
            CFDictionarySetValue(dictionary, GetExportedObject(SecurityLibrary.Value, attributeName), nativeValue);
        }
        finally
        {
            CFRelease(nativeValue);
        }
    }

    private static IntPtr CreateNativeDictionary()
    {
        // Callback exports are structures; CFString and CFBoolean exports hold object pointers.
        var dictionary = CFDictionaryCreateMutable(
            IntPtr.Zero,
            0,
            GetExportedStructureAddress(CoreFoundationLibrary.Value, "kCFTypeDictionaryKeyCallBacks"),
            GetExportedStructureAddress(CoreFoundationLibrary.Value, "kCFTypeDictionaryValueCallBacks"));
        if (dictionary == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not allocate a Keychain attribute dictionary.");
        }

        return dictionary;
    }

    private static IntPtr GetExportedObject(IntPtr library, string name)
    {
        var address = NativeLibrary.GetExport(library, name);
        var value = Marshal.ReadIntPtr(address);
        if (value == IntPtr.Zero)
        {
            throw new InvalidOperationException($"The native framework export {name} has no object value.");
        }

        return value;
    }

    private static IntPtr GetExportedStructureAddress(IntPtr library, string name) => NativeLibrary.GetExport(library, name);

    private static void EnsureKeychainSuccess(int status)
    {
        if (status != 0)
        {
            throw new InvalidOperationException($"Hatband could not access the macOS Keychain (OSStatus {status}). Check that the login keychain is unlocked and access is allowed.");
        }
    }

    private static void ReleaseNativeObject(IntPtr value)
    {
        if (value != IntPtr.Zero)
        {
            CFRelease(value);
        }
    }

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFDictionaryCreateMutable(IntPtr allocator, nint capacity, IntPtr keyCallbacks, IntPtr valueCallbacks);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRelease(IntPtr value);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, nint length);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nuint CFGetTypeID(IntPtr value);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nuint CFDataGetTypeID();

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFDataGetLength(IntPtr data);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFDataGetBytePtr(IntPtr data);

    [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
    private static partial int SecItemCopyMatching(IntPtr query, out IntPtr result);

    [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
    private static partial int SecItemAdd(IntPtr attributes, IntPtr result);

    [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
    private static partial int SecItemUpdate(IntPtr query, IntPtr attributes);

    [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
    private static partial int SecItemDelete(IntPtr query);
}
