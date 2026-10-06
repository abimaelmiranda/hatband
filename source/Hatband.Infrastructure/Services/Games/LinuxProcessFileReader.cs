using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Hatband.Infrastructure.Services.Games;

[SupportedOSPlatform("linux")]
internal static class LinuxProcessFileReader
{
    private const int CloseOnExec = 0x80000;
    private const int PermissionDenied = 13;
    private const int OperationNotPermitted = 1;
    private const int FileNotFound = 2;
    private const int ProcessNotFound = 3;
    private const int Interrupted = 4;

    public static bool TryReadAllText(string path, [NotNullWhen(true)] out string? content)
    {
        int descriptor;
        int error;
        do
        {
            // O_RDONLY is zero. Open failures are normal for protected or exited processes.
            descriptor = Open(path, CloseOnExec);
            error = descriptor < 0 ? Marshal.GetLastPInvokeError() : 0;
        }
        while (descriptor < 0 && error == Interrupted);

        if (descriptor < 0)
        {
            content = null;
            if (error is PermissionDenied or OperationNotPermitted or FileNotFound or ProcessNotFound)
            {
                return false;
            }

            throw new Win32Exception(error, $"Could not open process file '{path}'.");
        }

        using var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        using var stream = new FileStream(handle, FileAccess.Read);
        using var reader = new StreamReader(stream);
        content = reader.ReadToEnd();
        return true;
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
}
