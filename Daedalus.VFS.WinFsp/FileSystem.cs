using System.Diagnostics;
using System.Security.AccessControl;
using Fsp;
using Fsp.Interop;

namespace Daedalus.VFS.WinFsp;

using FileInfo = Fsp.Interop.FileInfo;
using VolumeInfo = Fsp.Interop.VolumeInfo;

public abstract class FileSystem<TNode, TDesc> : FileSystemBase
    where TNode : class
    where TDesc : class
{
    public override int ExceptionHandler(Exception ex)
    {
        Debug.WriteLine(
            $"FAIL: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}\n{ex.StackTrace}"
        );
        int hResult = ex.HResult;
        if (0x80070000 == (hResult & 0xFFFF0000))
            return NtStatusFromWin32((uint)hResult & 0xFFFF);
        return STATUS_UNEXPECTED_IO_ERROR;
    }

    public sealed override int Init(object Host)
    {
        return Init((FileSystemHost)Host);
    }

    public abstract int Init(FileSystemHost host);

    public abstract override int GetVolumeInfo(out VolumeInfo VolumeInfo);

    public sealed override int GetSecurityByName(
        string fileName,
        out uint fileAttributes,
        ref byte[]? securityDescriptor
    )
    {
        int result = GetSecurityByName(
            fileName,
            out FileAttributes fileAttributes2,
            ref securityDescriptor
        );
        fileAttributes = (uint)fileAttributes2;
        return result;
    }

    public abstract int GetSecurityByName(
        string fileName,
        out FileAttributes fileAttributes,
        ref byte[]? securityDescriptor
    );

    public sealed override int Create(
        string FileName,
        uint CreateOptions,
        uint GrantedAccess,
        uint FileAttributes,
        byte[] SecurityDescriptor,
        ulong AllocationSize,
        out object? FileNode,
        out object? FileDesc,
        out FileInfo FileInfo,
        out string? NormalizedName
    )
    {
        int result = Create(
            FileName,
            (FileCreateOptions)CreateOptions,
            (FileSystemRights)GrantedAccess,
            (FileAttributes)FileAttributes,
            SecurityDescriptor,
            AllocationSize,
            out TNode? fileNode,
            out TDesc? fileDesc,
            out FileInfo,
            out NormalizedName
        );
        FileNode = fileNode;
        FileDesc = fileDesc;
        return result;
    }

    public abstract int Create(
        string fileName,
        FileCreateOptions createOptions,
        FileSystemRights grantedAccess,
        FileAttributes fileAttributes,
        byte[]? securityDescriptor,
        ulong allocationSize,
        out TNode? fileNode,
        out TDesc? fileDesc,
        out FileInfo fileInfo,
        out string? normalizedName
    );

    public sealed override int Open(
        string FileName,
        uint CreateOptions,
        uint GrantedAccess,
        out object? FileNode,
        out object? FileDesc0,
        out FileInfo FileInfo,
        out string? NormalizedName
    )
    {
        int result = Open(
            FileName,
            (FileCreateOptions)CreateOptions,
            (FileSystemRights)GrantedAccess,
            out TNode? fileNode,
            out TDesc? fileDesc,
            out FileInfo,
            out NormalizedName
        );
        FileNode = fileNode;
        FileDesc0 = fileDesc;
        return result;
    }

    public abstract int Open(
        string fileName,
        FileCreateOptions options,
        FileSystemRights grantedAccess,
        out TNode? fileNode,
        out TDesc? fileDesc,
        out FileInfo fileInfo,
        out string? normalizedName
    );

    public sealed override int Overwrite(
        object FileNode,
        object FileDesc,
        uint FileAttributes,
        bool ReplaceFileAttributes,
        ulong AllocationSize,
        out FileInfo FileInfo
    )
    {
        return Overwrite(
            (TNode)FileNode,
            (TDesc)FileDesc,
            (FileAttributes)FileAttributes,
            ReplaceFileAttributes,
            AllocationSize,
            out FileInfo
        );
    }

    public abstract int Overwrite(
        TNode fileNode,
        TDesc fileDesc,
        FileAttributes fileAttributes,
        bool replaceFileAttributes,
        ulong allocationSize,
        out FileInfo fileInfo
    );

    public sealed override void Cleanup(
        object FileNode,
        object FileDesc,
        string FileName,
        uint Flags
    )
    {
        Cleanup((TNode)FileNode, (TDesc)FileDesc, FileName, (CleanupFlags)Flags);
    }

    public abstract void Cleanup(
        TNode fileNode,
        TDesc fileDesc,
        string? fileName,
        CleanupFlags flags
    );

    public sealed override void Close(object FileNode, object FileDesc)
    {
        Close((TNode)FileNode, (TDesc)FileDesc);
    }

    public abstract void Close(TNode fileNode, TDesc fileDesc);

    public sealed override unsafe int Read(
        object FileNode,
        object FileDesc,
        nint Buffer,
        ulong Offset,
        uint Length,
        out uint BytesTransferred
    )
    {
        return Read(
            (TNode)FileNode,
            (TDesc)FileDesc,
            new Span<byte>((void*)Buffer, (int)Length),
            Offset,
            Length,
            out BytesTransferred
        );
    }

    public abstract int Read(
        TNode fileNode,
        TDesc fileDesc,
        Span<byte> buffer,
        ulong offset,
        uint length,
        out uint pBytesTransferred
    );

    public sealed override unsafe int Write(
        object FileNode,
        object FileDesc,
        nint Buffer,
        ulong Offset,
        uint Length,
        bool WriteToEndOfFile,
        bool ConstrainedIo,
        out uint BytesTransferred,
        out FileInfo FileInfo
    )
    {
        return Write(
            (TNode)FileNode,
            (TDesc)FileDesc,
            new ReadOnlySpan<byte>((void*)Buffer, (int)Length),
            Offset,
            Length,
            WriteToEndOfFile,
            ConstrainedIo,
            out BytesTransferred,
            out FileInfo
        );
    }

    public abstract int Write(
        TNode fileNode,
        TDesc fileDesc,
        ReadOnlySpan<byte> buffer,
        ulong offset,
        uint length,
        bool writeToEOF,
        bool constraintedIO,
        out uint pBytesTransferred,
        out FileInfo fileInfo
    );

    public sealed override int Flush(object FileNode, object FileDesc, out FileInfo FileInfo)
    {
        return Flush((TNode)FileNode, (TDesc)FileDesc, out FileInfo);
    }

    public abstract int Flush(TNode? fileNode, TDesc? fileDesc, out FileInfo fileInfo);

    public sealed override int GetFileInfo(object FileNode, object FileDesc, out FileInfo FileInfo)
    {
        return GetFileInfo((TNode)FileNode, (TDesc)FileDesc, out FileInfo);
    }

    public abstract int GetFileInfo(TNode fileNode, TDesc fileDesc, out FileInfo fileInfo);

    public sealed override int SetBasicInfo(
        object FileNode,
        object FileDesc,
        uint FileAttributes,
        ulong CreationTime,
        ulong LastAccessTime,
        ulong LastWriteTime,
        ulong ChangeTime,
        out FileInfo FileInfo
    )
    {
        return SetBasicInfo(
            (TNode)FileNode,
            (TDesc)FileDesc,
            (FileAttributes)FileAttributes,
            CreationTime == 0L ? null : DateTime.FromFileTimeUtc((long)CreationTime),
            LastAccessTime == 0L ? null : DateTime.FromFileTimeUtc((long)LastAccessTime),
            LastWriteTime == 0L ? null : DateTime.FromFileTimeUtc((long)LastWriteTime),
            ChangeTime == 0L ? null : DateTime.FromFileTimeUtc((long)ChangeTime),
            out FileInfo
        );
    }

    public abstract int SetBasicInfo(
        TNode fileNode,
        TDesc fileDesc,
        FileAttributes fileAttributes,
        DateTime? creationTime,
        DateTime? lastAccessTime,
        DateTime? lastWriteTime,
        DateTime? changeTime,
        out FileInfo fileInfo
    );

    public sealed override int SetFileSize(
        object FileNode,
        object FileDesc,
        ulong NewSize,
        bool SetAllocationSize,
        out FileInfo FileInfo
    )
    {
        return SetFileSize(
            (TNode)FileNode,
            (TDesc)FileDesc,
            NewSize,
            SetAllocationSize,
            out FileInfo
        );
    }

    public abstract int SetFileSize(
        TNode fileNode,
        TDesc fileDesc,
        ulong newSize,
        bool setAllocationSize,
        out FileInfo fileInfo
    );

    public sealed override int CanDelete(object FileNode, object FileDesc, string FileName)
    {
        return CanDelete((TNode)FileNode, (TDesc)FileDesc, FileName);
    }

    public abstract int CanDelete(TNode fileNode, TDesc fileDesc, string fileName);

    public sealed override int Rename(
        object FileNode,
        object FileDesc,
        string FileName,
        string NewFileName,
        bool ReplaceIfExists
    )
    {
        return Rename((TNode)FileNode, (TDesc)FileDesc, FileName, NewFileName, ReplaceIfExists);
    }

    public abstract int Rename(
        TNode fileNode,
        TDesc fileDesc,
        string fileName,
        string newFileName,
        bool replaceIfExists
    );

    public sealed override int GetSecurity(
        object FileNode,
        object FileDesc,
        ref byte[]? SecurityDescriptor
    )
    {
        return GetSecurity((TNode)FileNode, (TDesc)FileDesc, ref SecurityDescriptor);
    }

    public abstract int GetSecurity(TNode fileNode, TDesc fileDesc, ref byte[]? securityDescriptor);

    public sealed override int SetSecurity(
        object FileNode,
        object FileDesc,
        AccessControlSections Sections,
        byte[] SecurityDescriptor
    )
    {
        return SetSecurity((TNode)FileNode, (TDesc)FileDesc, Sections, SecurityDescriptor);
    }

    public abstract int SetSecurity(
        TNode fileNode,
        TDesc fileDesc,
        AccessControlSections sections,
        byte[] securityDescriptor
    );

    public sealed override bool ReadDirectoryEntry(
        object FileNode,
        object FileDesc,
        string? Pattern,
        string? Marker,
        ref object? Context,
        out string? FileName,
        out FileInfo FileInfo
    )
    {
        return ReadDirectoryEntry(
            (TNode)FileNode,
            (TDesc)FileDesc,
            Pattern,
            Marker,
            ref Context,
            out FileName,
            out FileInfo
        );
    }

    public abstract bool ReadDirectoryEntry(
        TNode fileNode,
        TDesc fileDesc,
        string? pattern,
        string? marker,
        ref object? context,
        out string? fileName,
        out FileInfo fileInfo
    );

    public sealed override int SetVolumeLabel(string VolumeLabel, out VolumeInfo VolumeInfo)
    {
        Debug.WriteLine(
            $"unimplemented call: SetVolumeLabel('{VolumeLabel}') — implement in {GetType().Name}"
        );
        throw new NotImplementedException(nameof(SetVolumeLabel));
    }

    public sealed override int Control(
        object FileNode,
        object FileDesc,
        uint ControlCode,
        IntPtr InputBuffer,
        uint InputBufferLength,
        IntPtr OutputBuffer,
        uint OutputBufferLength,
        out uint BytesTransferred
    )
    {
        Debug.WriteLine($"unimplemented call: Control(code=0x{ControlCode:X8})");
        throw new NotImplementedException(nameof(Control));
    }

    public sealed override int GetEa(
        object FileNode,
        object FileDesc,
        IntPtr Ea,
        uint EaLength,
        out uint BytesTransferred
    )
    {
        Debug.WriteLine("unimplemented call: GetEa");
        throw new NotImplementedException(nameof(GetEa));
    }

    public sealed override bool GetEaEntry(
        object FileNode,
        object FileDesc,
        ref object Context,
        out string EaName,
        out byte[] EaValue,
        out bool NeedEa
    )
    {
        Debug.WriteLine("unimplemented call: GetEaEntry");
        throw new NotImplementedException(nameof(GetEaEntry));
    }

    public sealed override int SetEa(
        object FileNode,
        object FileDesc,
        IntPtr Ea,
        uint EaLength,
        out FileInfo FileInfo
    )
    {
        Debug.WriteLine("unimplemented call: SetEa");
        throw new NotImplementedException(nameof(SetEa));
    }

    public sealed override int SetEaEntry(
        object FileNode,
        object FileDesc,
        ref object Context,
        string EaName,
        byte[] EaValue,
        bool NeedEa
    )
    {
        Debug.WriteLine("unimplemented call: SetEaEntry");
        throw new NotImplementedException(nameof(SetEaEntry));
    }

    public sealed override int GetStreamInfo(
        object FileNode,
        object FileDesc,
        IntPtr Buffer,
        uint Length,
        out uint BytesTransferred
    )
    {
        Debug.WriteLine("unimplemented call: GetStreamInfo");
        throw new NotImplementedException(nameof(GetStreamInfo));
    }

    public sealed override bool GetStreamEntry(
        object FileNode,
        object FileDesc,
        ref object Context,
        out string StreamName,
        out ulong StreamSize,
        out ulong StreamAllocationSize
    )
    {
        Debug.WriteLine("unimplemented call: GetStreamEntry");
        throw new NotImplementedException(nameof(GetStreamEntry));
    }

    public sealed override int GetReparsePoint(
        object FileNode,
        object FileDesc,
        string FileName,
        ref byte[] ReparseData
    )
    {
        Debug.WriteLine($"unimplemented call: GetReparsePoint('{FileName}')");
        throw new NotImplementedException(nameof(GetReparsePoint));
    }

    public sealed override int SetReparsePoint(
        object FileNode,
        object FileDesc,
        string FileName,
        byte[] ReparseData
    )
    {
        Debug.WriteLine($"unimplemented call: SetReparsePoint('{FileName}')");
        throw new NotImplementedException(nameof(SetReparsePoint));
    }

    public sealed override int DeleteReparsePoint(
        object FileNode,
        object FileDesc,
        string FileName,
        byte[] ReparseData
    )
    {
        Debug.WriteLine($"unimplemented call: DeleteReparsePoint('{FileName}')");
        throw new NotImplementedException(nameof(DeleteReparsePoint));
    }

    public sealed override int GetReparsePointByName(
        string FileName,
        bool IsDirectory,
        ref byte[] ReparseData
    )
    {
        Debug.WriteLine($"unimplemented call: GetReparsePointByName('{FileName}')");
        throw new NotImplementedException(nameof(GetReparsePointByName));
    }

    public sealed override int ResolveReparsePoints(
        string FileName,
        uint ReparsePointIndex,
        bool ResolveLastPathComponent,
        out IoStatusBlock IoStatus,
        IntPtr Buffer,
        IntPtr PSize
    )
    {
        Debug.WriteLine($"unimplemented call: ResolveReparsePoints('{FileName}')");
        throw new NotImplementedException(nameof(ResolveReparsePoints));
    }
}
