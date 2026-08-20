using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Fsp;

[assembly: DisableRuntimeMarshalling]

namespace Daedalus.WinFsp;

public static partial class Win32
{
    public static void ThrowIoExceptionWithHResult(int HResult)
    {
        throw new IOException(null, HResult);
    }

    public static void ThrowIoExceptionWithWin32(int Error)
    {
        ThrowIoExceptionWithHResult(unchecked((int)(0x80070000 | Error)));
    }

    public static void ThrowIoExceptionWithNtStatus(int Status)
    {
        ThrowIoExceptionWithWin32((int)FileSystemBase.Win32FromNtStatus(Status));
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FileBasicInfo
    {
        public ulong CreationTime;
        public ulong LastAccessTime;
        public ulong LastWriteTime;
        public ulong ChangeTime;
        public uint FileAttributes;

        public FileBasicInfo(
            FileAttributes fileAttributes,
            DateTime? creationTime,
            DateTime? lastAccessTime,
            DateTime? lastWriteTime,
            DateTime? changeTime
        )
        {
            uint attrs = unchecked((uint)fileAttributes);
            // -1 means "leave attributes unchanged" (a zero FILE_BASIC_INFO
            // field does exactly that); 0 means FILE_ATTRIBUTE_NORMAL.
            FileAttributes =
                attrs == unchecked((uint)-1)
                    ? 0u
                    : attrs == 0u
                        ? (uint)System.IO.FileAttributes.Normal
                        : attrs;
            LastAccessTime = lastAccessTime.HasValue
                ? (ulong)lastAccessTime.Value.ToFileTimeUtc()
                : 0;
            LastWriteTime = lastWriteTime.HasValue ? (ulong)lastWriteTime.Value.ToFileTimeUtc() : 0;
            ChangeTime = changeTime.HasValue ? (ulong)changeTime.Value.ToFileTimeUtc() : 0;
            CreationTime = creationTime.HasValue ? (ulong)creationTime.Value.ToFileTimeUtc() : 0;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FileInfoByHandle
    {
        public uint dwFileAttributes;
        public ulong ftCreationTime;
        public ulong ftLastAccessTime;
        public ulong ftLastWriteTime;
        public uint dwVolumeSerialNumber;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint nNumberOfLinks;
        public uint nFileIndexHigh;
        public uint nFileIndexLow;

        public readonly FileAttributes FileAttributes => (FileAttributes)dwFileAttributes;
        public readonly DateTime CreationTime => DateTime.FromFileTimeUtc((long)ftCreationTime);
        public readonly DateTime LastAccessTime => DateTime.FromFileTimeUtc((long)ftLastAccessTime);
        public readonly DateTime LastWriteTime => DateTime.FromFileTimeUtc((long)ftLastWriteTime);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FileDispositionInfo
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool DeleteFile;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetFileInformationByHandle(
        IntPtr hFile,
        out FileInfoByHandle lpFileInformation
    );

    public static bool GetFileInformationByHandle(
        FileStream stream,
        out FileInfoByHandle lpFileInformation
    )
    {
        return GetFileInformationByHandle(
            stream.SafeFileHandle.DangerousGetHandle(),
            out lpFileInformation
        );
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetFileInformationByHandle(
        IntPtr hFile,
        int FileInformationClass,
        ref FileDispositionInfo lpFileInformation,
        uint dwBufferSize
    );

    public static bool SetFileInformationByHandle(
        IntPtr hFile,
        ref FileDispositionInfo lpFileInformation
    )
    {
        return SetFileInformationByHandle(
            hFile,
            4,
            ref lpFileInformation,
            (uint)Marshal.SizeOf<FileDispositionInfo>()
        );
    }

    public static bool SetFileInformationByHandle(
        FileStream stream,
        ref FileDispositionInfo lpFileInformation
    )
    {
        return SetFileInformationByHandle(
            stream.SafeFileHandle.DangerousGetHandle(),
            ref lpFileInformation
        );
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetFileInformationByHandle(
        IntPtr hFile,
        int FileInformationClass,
        ref FileBasicInfo lpFileInformation,
        uint dwBufferSize
    );

    public static bool SetFileInformationByHandle(IntPtr hFile, ref FileBasicInfo lpFileInformation)
    {
        return SetFileInformationByHandle(
            hFile,
            0,
            ref lpFileInformation,
            (uint)Marshal.SizeOf<FileBasicInfo>()
        );
    }

    public static bool SetFileInformationByHandle(
        FileStream stream,
        ref FileBasicInfo lpFileInformation
    )
    {
        return SetFileInformationByHandle(
            stream.SafeFileHandle.DangerousGetHandle(),
            ref lpFileInformation
        );
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MoveFileExW(
        [MarshalAs(UnmanagedType.LPWStr)] string lpExistingFileName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpNewFileName,
        uint dwFlags
    );

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetFileSecurityW(
        [MarshalAs(UnmanagedType.LPWStr)] string fileName,
        int securityInformation,
        byte[] securityDescriptor
    );

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetKernelObjectSecurity(
        IntPtr handle,
        int securityInformation,
        byte[] securityDescriptor
    );
}
