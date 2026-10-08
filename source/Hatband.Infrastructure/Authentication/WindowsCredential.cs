using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Hatband.Infrastructure.Authentication;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsCredential
{
    public uint Flags;
    public uint Type;
    public IntPtr TargetName;
    public IntPtr Comment;
    public FILETIME LastWritten;
    public uint CredentialBlobSize;
    public IntPtr CredentialBlob;
    public uint Persist;
    public uint AttributeCount;
    public IntPtr Attributes;
    public IntPtr TargetAlias;
    public IntPtr UserName;
}
