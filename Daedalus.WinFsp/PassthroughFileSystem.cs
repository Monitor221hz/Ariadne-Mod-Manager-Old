using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Fsp;
using Fsp.Interop;
using FileInfo = Fsp.Interop.FileInfo;

namespace Daedalus.WinFsp;

public class FileDescription
{
    public FileDescription(FileStream stream)
    {
        Stream = stream;
    }

    public FileDescription(DirectoryInfo directoryInfo)
    {
        DirectoryInfo = directoryInfo;
    }

    public FileStream? Stream;
    public DirectoryInfo? DirectoryInfo;
    public FileSystemInfo[]? FileSystemInfos;
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
    public override int CanDelete(FileNode fileNode, FileDescription fileDesc, string fileName)
    {
        throw new NotImplementedException();
    }

    public override void Cleanup(
        FileNode fileNode,
        FileDescription fileDesc,
        string? fileName,
        CleanupFlags flags
    )
    {
        throw new NotImplementedException();
    }

    public override void Close(FileNode fileNode, FileDescription fileDesc)
    {
        throw new NotImplementedException();
    }

    public override int Create(
        string fileName,
        FileCreateOptions options,
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
        throw new NotImplementedException();
    }

    public override int Flush(FileNode? fileNode, FileDescription? fileDesc, out FileInfo fileInfo)
    {
        throw new NotImplementedException();
    }

    public override int GetFileInfo(
        FileNode fileNode,
        FileDescription fileDesc,
        out FileInfo fileInfo
    )
    {
        throw new NotImplementedException();
    }

    public override int GetSecurity(
        FileNode fileNode,
        FileDescription fileDesc,
        ref byte[]? securityDescriptor
    )
    {
        throw new NotImplementedException();
    }

    public override int GetSecurityByName(
        string FileName,
        out uint FileAttributes,
        ref byte[]? SecurityDescriptor
    )
    {
        throw new NotImplementedException();
    }

    public override int GetVolumeInfo(out VolumeInfo VolumeInfo)
    {
        throw new NotImplementedException();
    }

    public override int Init(FileSystemHost host)
    {
        throw new NotImplementedException();
    }

    public override int Open(
        string fileName,
        FileCreateOptions options,
        FileSystemRights grantedAccess,
        out FileNode? fileNode,
        out FileDescription? fileDesc0,
        out FileInfo fileInfo,
        out string? normalizedName
    )
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    public override int Rename(
        FileNode fileNode,
        FileDescription fileDesc,
        string fileName,
        string newFileName,
        bool replaceIfExists
    )
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    public override int SetFileSize(
        FileNode fileNode,
        FileDescription fileDesc,
        ulong newSize,
        bool setAllocationSize,
        out FileInfo fileInfo
    )
    {
        throw new NotImplementedException();
    }

    public override int SetSecurity(
        FileNode fileNode,
        FileDescription fileDesc,
        AccessControlSections sections,
        byte[] securityDescriptor
    )
    {
        throw new NotImplementedException();
    }

    public override int Write(
        FileNode fileNode,
        FileDescription fileDesc,
        ReadOnlySpan<byte> buffer,
        ulong offset,
        uint length,
        bool writeToEOF,
        bool constraintedIO,
        out uint pBytesTransferred,
        out FileInfo fileInfo
    )
    {
        throw new NotImplementedException();
    }
}
