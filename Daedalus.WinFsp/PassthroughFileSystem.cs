using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Fsp;
using Fsp.Interop;
using FileInfo = Fsp.Interop.FileInfo;
using SysFileInfo = System.IO.FileInfo;

namespace Daedalus.WinFsp;

// adapted from https://github.com/winfsp/winfsp/blob/master/tst/passthrough-dotnet/Program.cs
public class FileDescription : IDisposable
{
    protected const int ALLOCATION_UNIT = 4096;

    public FileDescription(FileStream stream)
    {
        Stream = stream;
    }

    public FileDescription(DirectoryInfo directoryInfo)
    {
        DirectoryInfo = directoryInfo;
    }

    public FileStream? Stream { get; private set; }

    public DirectoryInfo? DirectoryInfo { get; private set; }
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
        if (IsFile)
        {
            var fileInfo = new FileInfo();
            if (!Win32.GetFileInformationByHandle(Stream, out var handleFileInfo))
            {
                Win32.ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
            }
            fileInfo.FileAttributes = handleFileInfo.dwFileAttributes;
            fileInfo.ReparseTag = 0;
            fileInfo.FileSize = (ulong)Stream.Length;
            fileInfo.AllocationSize =
                (fileInfo.FileSize + ALLOCATION_UNIT - 1) / ALLOCATION_UNIT * ALLOCATION_UNIT;
            fileInfo.CreationTime = handleFileInfo.ftCreationTime;
            fileInfo.LastAccessTime = handleFileInfo.ftLastAccessTime;
            fileInfo.LastWriteTime = handleFileInfo.ftLastWriteTime;
            fileInfo.ChangeTime = handleFileInfo.ftLastWriteTime;
            fileInfo.IndexNumber = 0;
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

[StructLayout(LayoutKind.Sequential)]
public class FileNode
{
    private static long s_nextIndexNumber;

    public FileNode(string fileName)
    {
        FileName = fileName;
        ulong now = (ulong)DateTime.UtcNow.ToFileTimeUtc();
        FileInfo.CreationTime =
            FileInfo.LastAccessTime =
            FileInfo.LastWriteTime =
            FileInfo.ChangeTime =
                now;
        FileInfo.IndexNumber = (ulong)System.Threading.Interlocked.Increment(ref s_nextIndexNumber);
    }

    public string FileName;
    public FileInfo FileInfo;
    public byte[]? FileSecurity;
    public byte[]? FileData;
    public byte[]? ReparseData;
    public FileNode? MainFileNode;
    public int OpenCount;
}

public class PassthroughFileSystem : FileSystem<FileNode, FileDescription>
{
    protected const int ALLOCATION_UNIT = 4096;
    private readonly string _rootPath;

    public PassthroughFileSystem(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath).TrimEnd('\\');
    }

    // WinFsp file names are rooted at the mount point ("\dir\file.txt");
    // Path.Combine would discard the root in that case, hence plain concatenation.
    private string ConcatPath(string fileName) => _rootPath + fileName;

    public override int Init(FileSystemHost host)
    {
        host.SectorSize = ALLOCATION_UNIT;
        host.SectorsPerAllocationUnit = 1;
        host.MaxComponentLength = 255;
        host.FileInfoTimeout = 1000;
        host.CaseSensitiveSearch = false;
        host.CasePreservedNames = true;
        host.UnicodeOnDisk = true;
        host.PersistentAcls = true;
        host.PostCleanupWhenModifiedOnly = true;
        host.PassQueryDirectoryPattern = true;
        host.FlushAndPurgeOnCleanup = true;
        host.VolumeCreationTime = (ulong)File.GetCreationTimeUtc(_rootPath).ToFileTimeUtc();
        host.VolumeSerialNumber = 0;
        return STATUS_SUCCESS;
    }

    public override int ExceptionHandler(Exception ex)
    {
        int hresult = ex.HResult;
        if (0x80070000 == unchecked((uint)hresult & 0xFFFF0000))
        {
            return NtStatusFromWin32((uint)hresult & 0xFFFF);
        }
        return STATUS_UNEXPECTED_IO_ERROR;
    }

    public override int GetVolumeInfo(out VolumeInfo VolumeInfo)
    {
        VolumeInfo = default(VolumeInfo);
        try
        {
            DriveInfo info = new DriveInfo(_rootPath);
            VolumeInfo.TotalSize = (ulong)info.TotalSize;
            VolumeInfo.FreeSize = (ulong)info.AvailableFreeSpace;
        }
        catch (ArgumentException)
        {
            // DriveInfo only supports drives and not UNC paths;
            // better to use GetDiskFreeSpaceEx here. Until then, report zeros
            // instead of failing the query.
        }
        return STATUS_SUCCESS;
    }

    public override int GetSecurityByName(
        string fileName,
        out FileAttributes fileAttributes,
        ref byte[]? securityDescriptor
    )
    {
        fileName = ConcatPath(fileName);
        SysFileInfo fileInfo = new SysFileInfo(fileName);
        fileAttributes = fileInfo.Attributes;
        if (securityDescriptor == null)
        {
            return STATUS_SUCCESS;
        }
        securityDescriptor = fileInfo.GetAccessControl().GetSecurityDescriptorBinaryForm();
        return STATUS_SUCCESS;
    }

    public override int Create(
        string fileName,
        FileCreateOptions createdOptions,
        FileSystemRights grantedAccess,
        FileAttributes fileAttributes,
        byte[]? securityDescriptor,
        ulong allocationSize,
        out FileNode? fileNode,
        out FileDescription? fileDesc,
        out FileInfo fileInfo,
        out string? normalizedName
    )
    {
        fileNode = null;
        fileDesc = null;
        fileInfo = default;
        normalizedName = null;
        try
        {
            fileName = ConcatPath(fileName);
            if (!createdOptions.HasFlag(FileCreateOptions.DirectoryFile))
            {
                FileSecurity? fileSecurity = null;
                if (securityDescriptor != null)
                {
                    fileSecurity = new FileSecurity();
                    fileSecurity.SetSecurityDescriptorBinaryForm(securityDescriptor);
                }
                fileDesc = new FileDescription(
                    new SysFileInfo(fileName).Create(
                        FileMode.CreateNew,
                        grantedAccess | FileSystemRights.WriteAttributes,
                        FileShare.Read | FileShare.Write | FileShare.Delete,
                        4096,
                        FileOptions.None,
                        fileSecurity
                    )
                );
                fileDesc.SetBasicInfo(fileAttributes | FileAttributes.Archive);
            }
            else
            {
                if (Directory.Exists(fileName))
                {
                    Win32.ThrowIoExceptionWithNtStatus(STATUS_OBJECT_NAME_COLLISION);
                }
                DirectorySecurity? dirSecurity = null;
                if (securityDescriptor != null)
                {
                    dirSecurity = new();
                    dirSecurity.SetSecurityDescriptorBinaryForm(securityDescriptor);
                }
                fileDesc = new(
                    dirSecurity != null
                        ? dirSecurity.CreateDirectory(fileName)
                        : Directory.CreateDirectory(fileName)
                );
                fileDesc.SetBasicInfo(fileAttributes);
            }
            fileNode = null;
            normalizedName = null;
            fileInfo = fileDesc.GetFileInfo();
            return STATUS_SUCCESS;
        }
        catch
        {
            if (fileDesc != null && fileDesc.Stream != null)
            {
                fileDesc.Dispose();
            }
            throw;
        }
    }

    public override int Open(
        string fileName,
        FileCreateOptions options,
        FileSystemRights grantedAccess,
        out FileNode? fileNode,
        out FileDescription? fileDesc,
        out FileInfo fileInfo,
        out string? normalizedName
    )
    {
        fileNode = null;
        fileDesc = null;
        fileInfo = default;
        normalizedName = null;
        try
        {
            fileName = ConcatPath(fileName);
            if (!Directory.Exists(fileName))
            {
                fileDesc = new FileDescription(
                    new SysFileInfo(fileName).Create(
                        FileMode.Open,
                        grantedAccess,
                        FileShare.Read | FileShare.Write | FileShare.Delete,
                        ALLOCATION_UNIT,
                        FileOptions.None,
                        null
                    )
                );
            }
            else
            {
                fileDesc = new FileDescription(new DirectoryInfo(fileName));
            }
            fileNode = null;
            normalizedName = null;
            fileInfo = fileDesc.GetFileInfo();
            return STATUS_SUCCESS;
        }
        catch
        {
            if (fileDesc != null && fileDesc.Stream != null)
            {
                fileDesc.Dispose();
            }
            throw;
        }
    }

    public override int Overwrite(
        FileNode fileNode,
        FileDescription fileDesc,
        FileAttributes fileAttributes,
        bool replaceFileAttributes,
        ulong allocationSize,
        out FileInfo fileInfo
    )
    {
        fileInfo = fileDesc.GetFileInfo();
        if (replaceFileAttributes)
        {
            fileDesc.SetBasicInfo(fileAttributes | FileAttributes.Archive);
        }
        else if (fileAttributes != 0)
        {
            fileDesc.SetBasicInfo(
                (FileAttributes)fileInfo.FileAttributes | fileAttributes | FileAttributes.Archive
            );
        }
        if (fileDesc.IsFile)
        {
            fileDesc.Stream.SetLength(0);
        }
        fileInfo = fileDesc.GetFileInfo();
        return STATUS_SUCCESS;
    }

    public override void Cleanup(
        FileNode fileNode,
        FileDescription fileDesc,
        string? fileName,
        CleanupFlags flags
    )
    {
        if (flags.HasFlag(CleanupFlags.Delete))
        {
            fileDesc.SetDisposition(true);
            if (fileDesc.IsFile)
            {
                fileDesc.Dispose();
            }
        }
    }

    public override int Read(
        FileNode fileNode,
        FileDescription fileDesc,
        Span<byte> buffer,
        ulong offset,
        uint length,
        out uint pBytesTransferred
    )
    {
        if (offset >= (ulong)fileDesc.Stream.Length)
        {
            Win32.ThrowIoExceptionWithNtStatus(STATUS_END_OF_FILE);
        }
        fileDesc.Stream.Seek((long)offset, SeekOrigin.Begin);
        pBytesTransferred = (uint)fileDesc.Stream.Read(buffer[..(int)length]);
        return STATUS_SUCCESS;
    }

    public override int Write(
        FileNode fileNode,
        FileDescription fileDesc,
        ReadOnlySpan<byte> buffer,
        ulong offset,
        uint length,
        bool writeToEOF,
        bool constrainedIO,
        out uint pBytesTransferred,
        out FileInfo fileInfo
    )
    {
        if (!fileDesc.IsFile)
        {
            pBytesTransferred = 0;
            fileInfo = default;
            return STATUS_INVALID_PARAMETER;
        }
        if (constrainedIO)
        {
            if (offset >= (ulong)fileDesc.Stream.Length)
            {
                pBytesTransferred = 0;
                fileInfo = default;
                return STATUS_SUCCESS;
            }
            if (offset + length > (ulong)fileDesc.Stream.Length)
            {
                length = (uint)((ulong)fileDesc.Stream.Length - offset);
            }
        }
        if (writeToEOF)
        {
            fileDesc.Stream.Seek(0, SeekOrigin.End);
        }
        else
        {
            fileDesc.Stream.Seek((long)offset, SeekOrigin.Begin);
        }
        long start = fileDesc.Stream.Position;
        fileDesc.Stream.Write(buffer[..(int)length]);
        pBytesTransferred = (uint)(fileDesc.Stream.Position - start);
        fileInfo = fileDesc.GetFileInfo();
        return STATUS_SUCCESS;
    }

    public override void Close(FileNode fileNode, FileDescription fileDesc)
    {
        if (fileDesc.IsFile)
        {
            fileDesc.Dispose();
        }
    }

    public override int Flush(FileNode? fileNode, FileDescription? fileDesc, out FileInfo fileInfo)
    {
        if (fileDesc == null)
        {
            // we do not flush the whole volume, so just return success.
            fileInfo = default;
            return STATUS_SUCCESS;
        }
        if (fileDesc.IsFile)
        {
            fileDesc.Stream.Flush(true);
        }
        fileInfo = fileDesc.GetFileInfo();
        return STATUS_SUCCESS;
    }

    public override int GetFileInfo(
        FileNode fileNode,
        FileDescription fileDesc,
        out FileInfo fileInfo
    )
    {
        fileInfo = fileDesc.GetFileInfo();
        return STATUS_SUCCESS;
    }

    public override int SetBasicInfo(
        FileNode fileNode,
        FileDescription fileDesc,
        FileAttributes fileAttributes,
        DateTime? creationTime,
        DateTime? lastAccessTime,
        DateTime? lastWriteTime,
        DateTime? changeTime,
        out FileInfo fileInfo
    )
    {
        fileDesc.SetBasicInfo(fileAttributes, creationTime, lastAccessTime, lastWriteTime);
        fileInfo = fileDesc.GetFileInfo();
        return STATUS_SUCCESS;
    }

    public override int SetFileSize(
        FileNode fileNode,
        FileDescription fileDesc,
        ulong newSize,
        bool setAllocationSize,
        out FileInfo fileInfo
    )
    {
        if (!setAllocationSize || (ulong)fileDesc.Stream.Length > newSize)
        {
            fileDesc.Stream.SetLength((long)newSize);
        }
        fileInfo = fileDesc.GetFileInfo();
        return STATUS_SUCCESS;
    }

    public override int CanDelete(FileNode fileNode, FileDescription fileDesc, string fileName)
    {
        fileDesc.SetDisposition(false);
        return STATUS_SUCCESS;
    }

    public override int Rename(
        FileNode fileNode,
        FileDescription fileDesc,
        string fileName,
        string newFileName,
        bool replaceIfExists
    )
    {
        fileName = ConcatPath(fileName);
        newFileName = ConcatPath(newFileName);
        FileDescription.Rename(fileName, newFileName, replaceIfExists);
        return STATUS_SUCCESS;
    }

    public override int GetSecurity(
        FileNode fileNode,
        FileDescription fileDesc,
        ref byte[]? securityDescriptor
    )
    {
        securityDescriptor = fileDesc.SecurityDescriptor;
        return STATUS_SUCCESS;
    }

    public override int SetSecurity(
        FileNode fileNode,
        FileDescription fileDesc,
        AccessControlSections sections,
        byte[] securityDescriptor
    )
    {
        fileDesc.SetSecurityDescriptor(sections, securityDescriptor);
        return STATUS_SUCCESS;
    }

    public override bool ReadDirectoryEntry(
        FileNode fileNode,
        FileDescription fileDesc,
        string? pattern,
        string? marker,
        ref object? context,
        out string? fileName,
        out FileInfo fileInfo
    )
    {
        if (fileDesc.FileSystemInfos == null)
        {
            string searchPattern =
                pattern == null
                    ? "*"
                    : pattern.Replace('<', '*').Replace('>', '?').Replace('"', '.');
            var infos = new List<FileSystemInfo>();
            foreach (var info in fileDesc.DirectoryInfo!.EnumerateFileSystemInfos(searchPattern))
            {
                infos.Add(info);
            }
            infos.Sort(
                static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
            );
            fileDesc.FileSystemInfos = infos;
        }

        var fileSystemInfos = fileDesc.FileSystemInfos;
        const int dotEntries = 2;
        int index;
        if (context == null)
        {
            index = 0;
            if (marker != null)
            {
                if (marker == ".")
                {
                    index = 1;
                }
                else if (marker == "..")
                {
                    index = 2;
                }
                else
                {
                    int found = BinarySearchByName(fileSystemInfos, marker);

                    index = (found >= 0 ? found + 1 : ~found) + dotEntries;
                }
            }
        }
        else
        {
            index = (int)context;
        }

        if (index >= fileSystemInfos.Count + dotEntries)
        {
            fileName = null;
            fileInfo = default;
            return false;
        }

        context = index + 1;
        if (index == 0)
        {
            fileName = ".";
            fileInfo = fileDesc.DirectoryInfo!.GetFileInfo(ALLOCATION_UNIT);
        }
        else if (index == 1)
        {
            fileName = "..";
            // for a drive root ".." is the directory itself, like on NTFS
            fileInfo = (fileDesc.DirectoryInfo!.Parent ?? fileDesc.DirectoryInfo).GetFileInfo(
                ALLOCATION_UNIT
            );
        }
        else
        {
            var info = fileSystemInfos[index - dotEntries];
            fileName = info.Name;
            fileInfo = info.GetFileInfo(ALLOCATION_UNIT);
        }
        return true;
    }

    private static int BinarySearchByName(List<FileSystemInfo> infos, string name)
    {
        int lo = 0;
        int hi = infos.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >>> 1;
            int cmp = string.Compare(infos[mid].Name, name, StringComparison.OrdinalIgnoreCase);
            if (cmp < 0)
            {
                lo = mid + 1;
            }
            else if (cmp > 0)
            {
                hi = mid - 1;
            }
            else
            {
                return mid;
            }
        }
        return ~lo;
    }
}
