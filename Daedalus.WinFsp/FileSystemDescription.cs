using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Daedalus.VFS;

namespace Daedalus.WinFsp;

using FileInfo = Fsp.Interop.FileInfo;
using SysFileInfo = System.IO.FileInfo;

public class FileSystemDescription : IDisposable
{
    protected const int ALLOCATION_UNIT = 4096;

    public FileSystemDescription(
        VirtualNode<BackedEntry> owner,
        FileStream stream,
        FileSystemRights access,
        FileShare share
    )
    {
        Owner = owner;
        Stream = stream;
        Access = access;
        Share = share;
    }

    public FileSystemDescription(VirtualNode<BackedEntry> owner, DirectoryInfo directoryInfo)
    {
        Owner = owner;
        DirectoryInfo = directoryInfo;
    }

    public FileSystemDescription(VirtualNode<BackedEntry> owner)
    {
        Owner = owner;
    }

    public VirtualNode<BackedEntry> Owner { get; set; }

    public FileStream? Stream { get; internal set; }
    public DirectoryInfo? DirectoryInfo { get; internal set; }

    internal int _activeTransfers;
    internal FileStream? _retiredStream;

    internal FileStream BeginTransfer()
    {
        lock (this)
        {
            _activeTransfers++;
            return Stream!;
        }
    }

    internal void EndTransfer()
    {
        lock (this)
        {
            if (--_activeTransfers == 0 && _retiredStream != null)
            {
                _retiredStream.Dispose();
                _retiredStream = null;
            }
        }
    }

    // caller must prevent concurrent transfers (sync lock)
    internal void SwapStream(FileStream replacement)
    {
        if (_activeTransfers == 0)
        {
            Stream?.Dispose();
        }
        else
        {
            _retiredStream = Stream;
        }
        Stream = replacement;
    }

    public FileSystemRights Access { get; } = FileSystemRights.Read;
    public FileShare Share { get; } = FileShare.Read;
    public List<FileSystemInfo>? FileSystemInfos { get; set; }

    [MemberNotNullWhen(true, nameof(Stream))]
    [MemberNotNullWhen(false, nameof(DirectoryInfo))]
    public bool IsFile => Stream != null;

    public byte[] SecurityDescriptor =>
        IsFile
            ? Stream.GetAccessControl().GetSecurityDescriptorBinaryForm()
            : DirectoryInfo!.GetAccessControl().GetSecurityDescriptorBinaryForm();

    public void SetSecurityDescriptor(AccessControlSections sections, byte[] securityDescriptor)
    {
        const int OWNER_SECURITY_INFORMATION = 1;
        const int GROUP_SECURITY_INFORMATION = 2;
        const int DACL_SECURITY_INFORMATION = 4;
        const int SACL_SECURITY_INFORMATION = 8;

        int securityInformation = 0;
        if (sections.HasFlag(AccessControlSections.Owner))
        {
            securityInformation |= OWNER_SECURITY_INFORMATION;
        }
        if (sections.HasFlag(AccessControlSections.Group))
        {
            securityInformation |= GROUP_SECURITY_INFORMATION;
        }
        if (sections.HasFlag(AccessControlSections.Access))
        {
            securityInformation |= DACL_SECURITY_INFORMATION;
        }
        if (sections.HasFlag(AccessControlSections.Audit))
        {
            securityInformation |= SACL_SECURITY_INFORMATION;
        }
        bool ok = IsFile
            ? Win32.SetKernelObjectSecurity(
                Stream.SafeFileHandle.DangerousGetHandle(),
                securityInformation,
                securityDescriptor
            )
            : Win32.SetFileSecurityW(
                DirectoryInfo!.FullName,
                securityInformation,
                securityDescriptor
            );
        if (!ok)
        {
            Win32.ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
        }
    }

    public void SetBasicInfo(
        FileAttributes fileAttributes = FileAttributes.Normal,
        DateTime? creationTime = null,
        DateTime? lastAccessTime = null,
        DateTime? lastWriteTime = null
    )
    {
        if (IsFile)
        {
            Win32.FileBasicInfo basicInfo = new(
                fileAttributes,
                creationTime,
                lastAccessTime,
                lastWriteTime,
                null
            );
            if (!Win32.SetFileInformationByHandle(Stream, ref basicInfo))
            {
                Win32.ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
            }
        }
        else
        {
            if ((int)fileAttributes != -1)
            {
                DirectoryInfo.Attributes = fileAttributes;
            }
            if (creationTime.HasValue)
            {
                DirectoryInfo.CreationTimeUtc = creationTime.Value;
            }
            if (lastAccessTime.HasValue)
            {
                DirectoryInfo.LastAccessTimeUtc = lastAccessTime.Value;
            }
            if (lastWriteTime.HasValue)
            {
                DirectoryInfo.LastWriteTimeUtc = lastWriteTime.Value;
            }
        }
    }

    public FileInfo GetFileInfo()
    {
        if (!IsFile && DirectoryInfo == null)
        {
            // placeholder directory: report a bare directory-shaped info
            var fileInfo = new FileInfo
            {
                FileAttributes = (uint)FileAttributes.Directory,
                ReparseTag = 0,
                FileSize = 0,
                AllocationSize = 0,
                IndexNumber = 0,
                HardLinks = 0,
            };
            ulong epoch = (ulong)
                new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();
            fileInfo.CreationTime =
                fileInfo.LastAccessTime =
                fileInfo.LastWriteTime =
                fileInfo.ChangeTime =
                    epoch;
            return fileInfo;
        }
        if (IsFile)
        {
            string? physical = Owner.Data.PhysicalPath;
            var fileInfo =
                physical != null && File.Exists(physical)
                    ? new SysFileInfo(physical).GetFileInfo(ALLOCATION_UNIT)
                    : new FileInfo();
            fileInfo.FileSize = (ulong)Stream.Length;
            fileInfo.AllocationSize =
                (fileInfo.FileSize + ALLOCATION_UNIT - 1) / ALLOCATION_UNIT * ALLOCATION_UNIT;
            fileInfo.HardLinks = 0;
            return fileInfo;
        }
        return DirectoryInfo!.GetFileInfo(ALLOCATION_UNIT);
    }

    public void SetDisposition(bool safe)
    {
        if (IsFile)
        {
            Win32.FileDispositionInfo info = new() { DeleteFile = true };
            if (!Win32.SetFileInformationByHandle(Stream, ref info) && !safe)
            {
                Win32.ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
            }
        }
        else
        {
            try
            {
                DirectoryInfo.Delete();
            }
            catch (Exception ex)
            {
                if (!safe)
                {
                    Win32.ThrowIoExceptionWithHResult(ex.HResult);
                }
            }
        }
    }

    public static void Rename(string fileName, string newFileName, bool replaceIfExists)
    {
        if (!Win32.MoveFileExW(fileName, newFileName, replaceIfExists ? 1u : 0u))
        {
            Win32.ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
        }
    }

    public void Dispose()
    {
        if (IsFile)
        {
            Stream.Dispose();
        }
    }
}
