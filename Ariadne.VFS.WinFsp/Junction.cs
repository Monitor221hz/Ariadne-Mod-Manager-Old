using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Ariadne.VFS.WinFsp;

internal static partial class Junction
{
    private const uint FsctlSetReparsePoint = 0x000900A4;
    private const uint IoReparseTagMountPoint = 0xA0000003;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;

    public static bool IsJunctionTo(string path, string target)
    {
        var info = new DirectoryInfo(path);
        info.Refresh();
        if (info.LinkTarget is null)
        {
            return false;
        }
        return string.Equals(
            Normalize(info.LinkTarget),
            Normalize(target),
            StringComparison.OrdinalIgnoreCase
        );
    }

    public static void Create(string path, string target)
    {
        Directory.CreateDirectory(path);
        var reparseData = BuildMountPointReparseBuffer(Path.GetFullPath(target));
        using var handle = CreateFileW(
            path,
            GenericWrite,
            0,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero
        );
        if (handle.IsInvalid)
        {
            Win32.ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
        }
        if (
            !DeviceIoControl(
                handle,
                FsctlSetReparsePoint,
                reparseData,
                reparseData.Length,
                IntPtr.Zero,
                0,
                out _,
                IntPtr.Zero
            )
        )
        {
            Win32.ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
        }
    }

    public static void Delete(string path)
    {
        Directory.Delete(path);
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd('\\');

    private static byte[] BuildMountPointReparseBuffer(string target)
    {
        var substituteBytes = Encoding.Unicode.GetBytes(@"\??\" + target);
        var printBytes = Encoding.Unicode.GetBytes(target);
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);
        writer.Write(IoReparseTagMountPoint);
        writer.Write((ushort)(8 + substituteBytes.Length + 2 + printBytes.Length + 2));
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)substituteBytes.Length);
        writer.Write((ushort)(substituteBytes.Length + 2));
        writer.Write((ushort)printBytes.Length);
        writer.Write(substituteBytes);
        writer.Write((ushort)0);
        writer.Write(printBytes);
        writer.Write((ushort)0);
        return buffer.ToArray();
    }

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16
    )]
    private static partial SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile
    );

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        byte[] lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped
    );
}
