using System.Security.AccessControl;
using System.Text;
using Fsp;
using Xunit;
using FileInfo = Fsp.Interop.FileInfo;

namespace Daedalus.VFS.WinFsp.Tests;

public class PassthroughFileSystemHeadlessTests
{
    private const int STATUS_SUCCESS = 0;
    private static readonly int STATUS_END_OF_FILE = unchecked((int)0xC0000011);
    private static readonly int STATUS_UNEXPECTED_IO_ERROR = unchecked((int)0xC00000E9);

    private static bool IsWindows() => OperatingSystem.IsWindows();

    private static FileDescription CreateFile(
        PassthroughFileSystem fs,
        string name,
        FileAttributes fileAttributes = FileAttributes.Normal
    )
    {
        int status = fs.Create(
            name,
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            fileAttributes,
            null,
            0,
            out _,
            out var fileDesc,
            out _,
            out _
        );
        Assert.Equal(STATUS_SUCCESS, status);
        Assert.NotNull(fileDesc);
        Assert.True(fileDesc!.IsFile);
        return fileDesc;
    }

    private static FileDescription OpenPath(
        PassthroughFileSystem fs,
        string name,
        FileCreateOptions options = default
    )
    {
        int status = fs.Open(
            name,
            options,
            FileSystemRights.FullControl,
            out _,
            out var fileDesc,
            out _,
            out _
        );
        Assert.Equal(STATUS_SUCCESS, status);
        Assert.NotNull(fileDesc);
        return fileDesc!;
    }

    private static List<string> EnumerateAll(PassthroughFileSystem fs, FileDescription dirDesc)
    {
        var names = new List<string>();
        object? context = null;
        while (fs.ReadDirectoryEntry(null!, dirDesc, null, null, ref context, out var name, out _))
        {
            names.Add(name!);
        }
        return names;
    }

    [SkippableFact]
    public void Create_File_Persists_And_Reports_Archive()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using var desc = CreateFile(fs, "\\hello.txt");

        Assert.True(File.Exists(root.Combine("hello.txt")));
        var fileInfo = desc.GetFileInfo();
        Assert.True(((FileAttributes)fileInfo.FileAttributes).HasFlag(FileAttributes.Archive));
    }

    [SkippableFact]
    public void Create_Directory_Persists()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        int status = fs.Create(
            "\\dir1",
            FileCreateOptions.DirectoryFile,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out _,
            out var fileDesc,
            out _,
            out _
        );

        Assert.Equal(STATUS_SUCCESS, status);
        Assert.NotNull(fileDesc);
        Assert.False(fileDesc!.IsFile);
        Assert.True(Directory.Exists(root.Combine("dir1")));
    }

    [SkippableFact]
    public void Create_ExistingFile_Returns_Failure_Status()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using var first = CreateFile(fs, "\\hello.txt");
        int status = fs.Create(
            "\\hello.txt",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out _,
            out _,
            out _,
            out _
        );
        Assert.True(status < 0);
    }

    [SkippableFact]
    public void ExceptionHandler_RoundTrips_Mapped_Win32_Errors()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        // error 38 (ERROR_HANDLE_EOF) does round-trip through WinFsp's table
        var ex = new IOException(null, unchecked((int)0x80070026));
        Assert.Equal(STATUS_END_OF_FILE, fs.ExceptionHandler(ex));
    }

    [SkippableFact]
    public void Write_Then_Read_RoundTrips()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);
        byte[] payload = Encoding.UTF8.GetBytes("hello world");

        using var desc = CreateFile(fs, "\\hello.txt");
        int writeStatus = fs.Write(
            null!,
            desc,
            payload,
            0,
            (uint)payload.Length,
            false,
            false,
            out uint written,
            out var infoAfterWrite
        );
        Assert.Equal(STATUS_SUCCESS, writeStatus);
        Assert.Equal((uint)payload.Length, written);
        Assert.Equal((ulong)payload.Length, infoAfterWrite.FileSize);

        byte[] buffer = new byte[64];
        int readStatus = fs.Read(null!, desc, buffer, 0, (uint)payload.Length, out uint read);
        Assert.Equal(STATUS_SUCCESS, readStatus);
        Assert.Equal((uint)payload.Length, read);
        Assert.Equal(payload, buffer[..(int)read]);
    }

    [SkippableFact]
    public void Write_ToEndOfFile_Appends()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);
        using (var desc = CreateFile(fs, "\\log.txt"))
        {
            fs.Write(null!, desc, "ab"u8, 0, 2, false, false, out _, out _);
            fs.Write(null!, desc, "cd"u8, ulong.MaxValue, 2, true, false, out _, out _);
        }

        Assert.Equal("abcd", File.ReadAllText(root.Combine("log.txt")));
    }

    [SkippableFact]
    public void Read_Beyond_End_Of_File_Returns_StatusEndOfFile()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using var desc = CreateFile(fs, "\\empty.txt");
        byte[] buffer = new byte[16];
        int status = fs.Read(null!, desc, buffer, 100, 10, out uint transferred);
        Assert.Equal(STATUS_END_OF_FILE, status);
        Assert.Equal(0u, transferred);
    }

    [SkippableFact]
    public void SetFileSize_Extends_Then_Truncates()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using var desc = CreateFile(fs, "\\size.txt");
        fs.SetFileSize(null!, desc, 100, false, out var extended);
        Assert.Equal(100ul, extended.FileSize);
        Assert.Equal(100L, new System.IO.FileInfo(root.Combine("size.txt")).Length);

        fs.SetFileSize(null!, desc, 10, false, out var truncated);
        Assert.Equal(10ul, truncated.FileSize);
        Assert.Equal(10L, new System.IO.FileInfo(root.Combine("size.txt")).Length);
    }

    [SkippableFact]
    public void SetBasicInfo_AttributesSentinel_LeavesAttributes_But_UpdatesTimes()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using var desc = CreateFile(fs, "\\attrs.txt");
        fs.SetBasicInfo(null!, desc, FileAttributes.ReadOnly, null, null, null, null, out _);
        Assert.Equal(FileAttributes.ReadOnly, File.GetAttributes(root.Combine("attrs.txt")));

        var newWriteTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        fs.SetBasicInfo(
            null!,
            desc,
            unchecked((FileAttributes)(-1)),
            null,
            null,
            newWriteTime,
            null,
            out _
        );
        Assert.Equal(FileAttributes.ReadOnly, File.GetAttributes(root.Combine("attrs.txt")));
        Assert.Equal(newWriteTime, File.GetLastWriteTimeUtc(root.Combine("attrs.txt")));
    }

    [SkippableFact]
    public void SetBasicInfo_Directory_Applies_Attributes()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        fs.Create(
            "\\dir1",
            FileCreateOptions.DirectoryFile,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out _,
            out _,
            out _,
            out _
        );
        var dirDesc = OpenPath(fs, "\\dir1");
        fs.SetBasicInfo(
            null!,
            dirDesc,
            FileAttributes.Directory | FileAttributes.Hidden,
            null,
            null,
            null,
            null,
            out _
        );

        Assert.True(File.GetAttributes(root.Combine("dir1")).HasFlag(FileAttributes.Hidden));
    }

    [SkippableFact]
    public void ReadDirectoryEntry_Enumerates_All_Entries()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using (CreateFile(fs, "\\a.txt"))
        using (CreateFile(fs, "\\b.txt")) { }

        var dirDesc = OpenPath(fs, "\\");
        Assert.Contains(".", EnumerateAll(fs, dirDesc));
        Assert.Contains("..", EnumerateAll(fs, dirDesc));
        Assert.Contains("a.txt", EnumerateAll(fs, dirDesc));
        Assert.Contains("b.txt", EnumerateAll(fs, dirDesc));
    }

    [SkippableFact]
    public void ReadDirectoryEntry_Applies_Pattern()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using (CreateFile(fs, "\\keep.txt"))
        using (CreateFile(fs, "\\drop.md")) { }

        using var dirDesc = OpenPath(fs, "\\");
        var names = new List<string>();
        object? context = null;
        while (
            fs.ReadDirectoryEntry(null!, dirDesc, "*.txt", null, ref context, out var name, out _)
        )
        {
            names.Add(name!);
        }

        Assert.Contains("keep.txt", names);
        Assert.DoesNotContain("drop.md", names);
    }

    [SkippableFact]
    public void ReadDirectoryEntry_Resumes_After_Marker()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using (CreateFile(fs, "\\a.txt"))
        using (CreateFile(fs, "\\b.txt")) { }

        using var dirDesc = OpenPath(fs, "\\");
        // build the snapshot with a throwaway initial call, then restart at the marker
        object? context = null;
        fs.ReadDirectoryEntry(null!, dirDesc, null, null, ref context, out _, out _);

        object? restartContext = null;
        bool hasNext = fs.ReadDirectoryEntry(
            null!,
            dirDesc,
            null,
            "a.txt",
            ref restartContext,
            out var name,
            out _
        );

        Assert.True(hasNext);
        Assert.Equal("b.txt", name);
    }

    [SkippableFact]
    public void ReadDirectoryEntry_Resumes_At_InsertionPoint_When_Marker_Was_Deleted()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using (CreateFile(fs, "\\a.txt"))
        using (CreateFile(fs, "\\c.txt")) { }

        using var dirDesc = OpenPath(fs, "\\");
        object? context = null;
        fs.ReadDirectoryEntry(null!, dirDesc, null, null, ref context, out _, out _);

        object? restartContext = null;
        bool hasNext = fs.ReadDirectoryEntry(
            null!,
            dirDesc,
            null,
            "b.txt",
            ref restartContext,
            out var name,
            out _
        );

        Assert.True(hasNext);
        Assert.Equal("c.txt", name);
    }

    [SkippableFact]
    public void CanDelete_Then_Cleanup_Delete_Removes_File()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        string fullPath = root.Combine("doomed.txt");
        File.WriteAllText(fullPath, "x");
        var desc = OpenPath(fs, "\\doomed.txt");

        Assert.Equal(STATUS_SUCCESS, fs.CanDelete(null!, desc, "\\doomed.txt"));
        fs.Cleanup(null!, desc, "\\doomed.txt", CleanupFlags.Delete);

        Assert.False(File.Exists(fullPath));
    }

    [SkippableFact]
    public void Rename_Moves_File_And_Rejects_Existing_Target_Without_Replace()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using (CreateFile(fs, "\\before.txt")) { }

        Assert.Equal(STATUS_SUCCESS, fs.Rename(null!, null!, "\\before.txt", "\\after.txt", false));
        Assert.False(File.Exists(root.Combine("before.txt")));
        Assert.True(File.Exists(root.Combine("after.txt")));

        File.WriteAllText(root.Combine("other.txt"), "occupied");
        Assert.Throws<IOException>(() =>
            fs.Rename(null!, null!, "\\after.txt", "\\other.txt", false)
        );
    }

    [SkippableFact]
    public void Security_RoundTrips_Through_GetSecurity_And_SetSecurity()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        using var desc = CreateFile(fs, "\\secure.txt");
        byte[]? descriptor = null;
        Assert.Equal(STATUS_SUCCESS, fs.GetSecurity(null!, desc, ref descriptor));
        Assert.NotNull(descriptor);
        Assert.NotEmpty(descriptor!);

        Assert.Equal(
            STATUS_SUCCESS,
            fs.SetSecurity(null!, desc, AccessControlSections.Access, descriptor!)
        );

        byte[]? after = null;
        Assert.Equal(STATUS_SUCCESS, fs.GetSecurity(null!, desc, ref after));
        // the kernel normalizes control bits on write
        var beforeSD = new RawSecurityDescriptor(descriptor!, 0);
        var afterSD = new RawSecurityDescriptor(after!, 0);
        Assert.Equal(beforeSD.Owner?.Value, afterSD.Owner?.Value);
        Assert.Equal(beforeSD.Group?.Value, afterSD.Group?.Value);
        Assert.Equal(beforeSD.DiscretionaryAcl?.Count, afterSD.DiscretionaryAcl?.Count);
    }

    [SkippableFact]
    public void GetVolumeInfo_Reports_Nonzero_Total()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        Assert.Equal(STATUS_SUCCESS, fs.GetVolumeInfo(out var volumeInfo));
        Assert.True(volumeInfo.TotalSize > 0);
    }

    [SkippableFact]
    public void ExceptionHandler_Maps_NonWin32_Exceptions_To_UnexpectedIoError()
    {
        Skip.IfNot(IsWindows(), "Windows only");
        using var root = new TempDirectory();
        var fs = new PassthroughFileSystem(root.Path);

        Assert.Equal(
            STATUS_UNEXPECTED_IO_ERROR,
            fs.ExceptionHandler(new InvalidOperationException("boom"))
        );
    }
}
