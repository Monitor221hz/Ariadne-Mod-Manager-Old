using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using Fsp;
using Fsp.Interop;
using FileInfo = Fsp.Interop.FileInfo;
using SysFileInfo = System.IO.FileInfo;

namespace Ariadne.VFS.WinFsp;

public class OverlayFileSystem : FileSystem<FileSystemNode, FileSystemDescription>
{
    private const int ALLOCATION_UNIT = 4096;
    private readonly VirtualNode<BackedEntry> _root;
    private readonly object _sync = new();
    private readonly string _createTargetRootPath;
    private readonly bool _copyUpEnabled;
    private readonly uint _fileInfoTimeout;
    private readonly uint _dirInfoTimeout;
    private readonly IReadOnlyList<OutputRule> _outputRules;
    private readonly OutputRuleRouter? _router;

    private sealed class CopyState
    {
        public readonly ManualResetEventSlim Completed = new();
        public string? ReplacementPath;
        public bool Faulted;
    }

    private readonly Dictionary<VirtualNode<BackedEntry>, CopyState> _copiesInProgress = new(
        ReferenceEqualityComparer.Instance
    );

    private readonly Dictionary<VirtualNode<BackedEntry>, FileSystemNode> _nodes = new(
        ReferenceEqualityComparer.Instance
    );

    public OverlayFileSystem(VirtualNode<BackedEntry> root)
        : this(root, new OverlayFileSystemOptions()) { }

    public OverlayFileSystem(VirtualNode<BackedEntry> root, OverlayFileSystemOptions options)
    {
        _root = root;
        _copyUpEnabled = options.CopyUpEnabled;
        _fileInfoTimeout = options.FileInfoTimeout;
        _dirInfoTimeout = options.DirInfoTimeout;
        _outputRules = options.OutputRules ?? Array.Empty<OutputRule>();

        if (_outputRules.Count > 0)
        {
            _router = new OutputRuleRouter(
                _outputRules,
                options.ProcessTracker ?? new WMProcessObserver(),
                root,
                options.PhysicalMountRoot
            );
        }
        _createTargetRootPath = ComputeCreateTargetRootPath(root);
    }

    public override int ExceptionHandler(Exception ex)
    {
        Debug.WriteLine(
            $"ExceptionHandler: {ex.GetType().Name} hresult=0x{ex.HResult:X8} {ex.Message}"
        );
        int hresult = ex.HResult;
        if (0x80070000 == unchecked((uint)hresult & 0xFFFF0000))
        {
            return NtStatusFromWin32((uint)hresult & 0xFFFF);
        }
        return STATUS_UNEXPECTED_IO_ERROR;
    }

    public override int Init(FileSystemHost host)
    {
        host.SectorSize = ALLOCATION_UNIT;
        host.SectorsPerAllocationUnit = 1;
        host.MaxComponentLength = 255;
        host.FileInfoTimeout = _fileInfoTimeout;
        host.DirInfoTimeout = _dirInfoTimeout;
        host.CaseSensitiveSearch = false;
        host.CasePreservedNames = true;
        host.UnicodeOnDisk = true;
        host.PersistentAcls = true;
        host.PostCleanupWhenModifiedOnly = true;
        host.PassQueryDirectoryPattern = true;
        host.FlushAndPurgeOnCleanup = true;
        host.VolumeCreationTime = (ulong)
            File.GetCreationTimeUtc(GetCreateTargetRootPath()).ToFileTimeUtc();
        host.VolumeSerialNumber = 0;
        return STATUS_SUCCESS;
    }

    private FileSystemNode Wrap(VirtualNode<BackedEntry> node)
    {
        if (!_nodes.TryGetValue(node, out var wrapped))
        {
            wrapped = new FileSystemNode(node);
            _nodes[node] = wrapped;
        }
        return wrapped;
    }

    public override int Create(
        string fileName,
        FileCreateOptions createOptions,
        FileSystemRights grantedAccess,
        FileAttributes fileAttributes,
        byte[]? securityDescriptor,
        ulong allocationSize,
        out FileSystemNode? fileNode,
        out FileSystemDescription? fileDesc,
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
            lock (_sync)
            {
                // Debug.WriteLine($"Create '{fileName}' opts={createOptions} attrs={fileAttributes}");
                if (_root.FindNode(fileName) != null)
                {
                    return STATUS_OBJECT_NAME_COLLISION;
                }
                int pid = _router != null ? GetOperationProcessId() : 0;
                var targetPath = ResolveOutputTarget(fileName, pid);
                if (!createOptions.HasFlag(FileCreateOptions.DirectoryFile))
                {
                    var newFile = _root.LinkFile(targetPath, fileName);
                    FileSecurity? fileSecurity = null;
                    if (securityDescriptor != null)
                    {
                        fileSecurity = new FileSecurity();
                        fileSecurity.SetSecurityDescriptorBinaryForm(securityDescriptor);
                    }

                    var fileShare = FileShare.Read | FileShare.Write | FileShare.Delete;
                    Directory.CreateDirectory(Path.GetDirectoryName(newFile.Data.PhysicalPath)!);
                    ClearStaleMarker(newFile.Data.PhysicalPath);
                    fileDesc = new(
                        newFile,
                        new SysFileInfo(newFile.Data.PhysicalPath).Create(
                            // revival of a former hidden file
                            File.Exists(newFile.Data.PhysicalPath)
                                ? FileMode.Create
                                : FileMode.CreateNew,
                            grantedAccess | FileSystemRights.WriteAttributes,
                            fileShare,
                            ALLOCATION_UNIT,
                            FileOptions.RandomAccess,
                            fileSecurity
                        ),
                        grantedAccess,
                        fileShare
                    );
                    fileDesc.SetBasicInfo(fileAttributes | FileAttributes.Archive);
                    fileNode = Wrap(newFile);
                }
                else
                {
                    if (Directory.Exists(targetPath))
                    {
                        return STATUS_OBJECT_NAME_COLLISION;
                    }
                    DirectorySecurity? dirSecurity = null;
                    if (securityDescriptor != null)
                    {
                        dirSecurity = new();
                        dirSecurity.SetSecurityDescriptorBinaryForm(securityDescriptor);
                    }

                    var createdDir =
                        dirSecurity != null
                            ? dirSecurity.CreateDirectory(targetPath)
                            : Directory.CreateDirectory(targetPath);
                    ClearStaleMarker(targetPath);
                    var newFolder = _root.LinkDirectory(targetPath, fileName);
                    fileDesc = new(newFolder, createdDir);
                    fileNode = Wrap(newFolder);
                }
                fileNode.AddOpen();
                if (pid != 0 && TryResolveRuleTarget(pid, out string? ruleTarget))
                {
                    fileDesc.OutputTargetOverride = ruleTarget;
                }
                fileInfo = fileDesc.GetFileInfo();
                return STATUS_SUCCESS;
            }
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
        out FileSystemNode? fileNode,
        out FileSystemDescription? fileDesc,
        out FileInfo fileInfo,
        out string? normalizedName
    )
    {
        fileNode = null;
        fileDesc = null;
        fileInfo = default;
        normalizedName = null;
        lock (_sync)
        {
            try
            {
                var node = _root.FindNode(fileName);
                if (node == null)
                {
                    return STATUS_OBJECT_NAME_NOT_FOUND;
                }

                if (node.IsDirectory)
                {
                    // placeholder dirs open as virtual-only, physical lazy by ensurwritable
                    fileDesc = node.Data.PhysicalPath is string dirPath
                        ? new FileSystemDescription(node, new DirectoryInfo(dirPath))
                        : new FileSystemDescription(node);
                }
                else
                {
                    string physical = node.Data.PhysicalPath;
                    var share = FileShare.Read | FileShare.Write | FileShare.Delete;
                    fileDesc = new FileSystemDescription(
                        node,
                        new SysFileInfo(physical).Create(
                            FileMode.Open,
                            grantedAccess,
                            share,
                            ALLOCATION_UNIT,
                            FileOptions.RandomAccess,
                            null
                        ),
                        grantedAccess,
                        share
                    );
                }
                fileNode = Wrap(node);
                fileNode.AddOpen();
                if (
                    _router != null
                    && TryResolveRuleTarget(GetOperationProcessId(), out string? ruleTarget)
                )
                {
                    fileDesc.OutputTargetOverride = ruleTarget;
                }
                fileInfo = fileDesc.GetFileInfo();
                return STATUS_SUCCESS;
            }
            catch
            {
                fileDesc?.Dispose();
                throw;
            }
        }
    }

    internal void EnsureWritable(FileSystemDescription fileDesc)
    {
        if (!_copyUpEnabled)
        {
            return;
        }

        var node = fileDesc.Owner;
        CopyState? state;
        bool isCopier;
        string physical;
        string targetPhysical;

        lock (_sync)
        {
            if (!fileDesc.IsFile)
            {
                MaterializePlaceholderIfNeeded(node, fileDesc);
                return;
            }

            physical = node.Data.PhysicalPath!;
            if (_copiesInProgress.TryGetValue(node, out state))
            {
                isCopier = false;
                targetPhysical = null!; // unused on the waiter path
                if (state.ReplacementPath != null && !fileDesc.RedirectsTo(state.ReplacementPath))
                {
                    SwapDescTo(fileDesc, state.ReplacementPath);
                    return;
                }
            }
            else
            {
                if (IsSinkPath(physical))
                {
                    return;
                }
                state = new CopyState();
                _copiesInProgress[node] = state;
                isCopier = true;
                targetPhysical = ResolveCopyUpTarget(node, fileDesc);
            }
        }

        if (!isCopier)
        {
            state.Completed.Wait();
            lock (_sync)
            {
                if (state.Faulted)
                {
                    _copiesInProgress.Remove(node);
                    EnsureWritable(fileDesc);
                    return;
                }
                bool alreadyBound = fileDesc.RedirectsTo(state.ReplacementPath!);
                if (!alreadyBound)
                {
                    SwapDescTo(fileDesc, state.ReplacementPath!);
                }
            }
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPhysical)!);
            File.Copy(physical, targetPhysical);
            ClearStaleMarker(targetPhysical);
        }
        catch
        {
            lock (_sync)
            {
                state.Faulted = true;
                _copiesInProgress.Remove(node);
                state.Completed.Set();
            }
            throw;
        }

        lock (_sync)
        {
            state.ReplacementPath = targetPhysical;
            SwapDescTo(fileDesc, targetPhysical);
            _root.LinkFile(targetPhysical, node.GetPath());
            state.Completed.Set();
        }
    }

    private void MaterializePlaceholderIfNeeded(
        VirtualNode<BackedEntry> node,
        FileSystemDescription fileDesc
    )
    {
        if (node.Data.PhysicalPath is null)
        {
            string targetDir = GetCreateTargetPath(node.GetPath());
            Directory.CreateDirectory(targetDir);
            node.Data = new BackedEntry(targetDir);
            fileDesc.DirectoryInfo = new DirectoryInfo(targetDir);
        }
    }

    private string ResolveCopyUpTarget(
        VirtualNode<BackedEntry> node,
        FileSystemDescription fileDesc
    )
    {
        string virtualPath = node.GetPath();
        string target = fileDesc.OutputTargetOverride is string ruleTarget
            ? Path.Join(ruleTarget.AsSpan(), virtualPath.AsSpan())
            : GetCreateTargetPath(virtualPath);
        // Debug.WriteLine($"CopyUp '{virtualPath}' -> '{target}'");
        return target;
    }

    private bool IsSinkPath(string physical)
    {
        string targetRoot = GetCreateTargetRootPath();
        if (!targetRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            targetRoot += Path.DirectorySeparatorChar;
        }
        return physical.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase);
    }

    private void SwapDescTo(FileSystemDescription fileDesc, string physicalPath)
    {
        var replacement = new SysFileInfo(physicalPath).Create(
            FileMode.Open,
            fileDesc.Access,
            fileDesc.Share,
            4096,
            FileOptions.RandomAccess,
            null
        );
        fileDesc.SwapStream(replacement);
        fileDesc.MarkRedirectsTo(physicalPath);
    }

    private string GetCreateTargetPath(string virtualPath)
    {
        VirtualNode<BackedEntry>? deepest = _root.HasFlag(NodeFlags.CreateTarget) ? _root : null;
        _root.VisitPath(
            virtualPath,
            node =>
            {
                if (node.HasFlag(NodeFlags.CreateTarget))
                {
                    deepest = node;
                }
            }
        );
        if (deepest == null)
        {
            Win32.ThrowIoExceptionWithNtStatus(STATUS_ACCESS_DENIED);
        }
        string remainder =
            deepest!.Name.Length == 0
                ? virtualPath.TrimStart('\\', '/')
                : Path.GetRelativePath(deepest.GetPath(), virtualPath.TrimStart('\\', '/'));
        return Path.Combine(deepest.Data.PhysicalPath, remainder);
    }

    private static void ClearStaleMarker(string physicalPath)
    {
        string marker = physicalPath + ".daehidden";
        if (File.Exists(marker))
        {
            File.Delete(marker);
        }
    }

    private string GetCreateTargetRootPath() => _createTargetRootPath;

    private string ResolveOutputTarget(string virtualPath, int originPid)
    {
        if (TryResolveRuleTarget(originPid, out string? ruleTarget))
        {
            return Path.Join(ruleTarget.AsSpan(), virtualPath.AsSpan().TrimStart(['\\', '/']));
        }
        return GetCreateTargetPath(virtualPath);
    }

    private bool TryResolveRuleTarget(int pid, [NotNullWhen(true)] out string? outputDir)
    {
        outputDir = null;
        return _router != null && _router.TryResolve(pid, out outputDir!);
    }

    private static string ComputeCreateTargetRootPath(VirtualNode<BackedEntry> root)
    {
        VirtualNode<BackedEntry>? deepest = null;
        if (root.HasFlag(NodeFlags.CreateTarget))
        {
            deepest = root;
        }
        foreach (var child in root.Children)
        {
            FindDeepestCreateTarget(child, ref deepest);
        }
        if (deepest == null)
        {
            Win32.ThrowIoExceptionWithNtStatus(FileSystemBase.STATUS_ACCESS_DENIED);
        }
        return deepest!.Data.PhysicalPath;
    }

    private bool SubtreeInSink(VirtualNode<BackedEntry> node)
    {
        string root = GetCreateTargetRootPath();
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }
        var pending = new Stack<VirtualNode<BackedEntry>>();
        pending.Push(node);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (
                current.Data.PhysicalPath is string path
                && !path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }
            foreach (var child in current.Children)
            {
                pending.Push(child);
            }
        }
        return true;
    }

    private static void MoveDirectory(string srcDir, string destDir, bool replaceIfExists)
    {
        if (Directory.Exists(destDir))
        {
            if (!replaceIfExists)
            {
                Win32.ThrowIoExceptionWithNtStatus(STATUS_OBJECT_NAME_COLLISION);
            }
            Directory.Delete(destDir, true);
        }
        Directory.Move(srcDir, destDir);
    }

    private static void FindDeepestCreateTarget(
        VirtualNode<BackedEntry> node,
        ref VirtualNode<BackedEntry>? found
    )
    {
        if (node.HasFlag(NodeFlags.CreateTarget))
        {
            found = node;
            return;
        }
        foreach (var child in node.Children)
        {
            FindDeepestCreateTarget(child, ref found);
        }
    }

    public override int CanDelete(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        string fileName
    )
    {
        lock (_sync)
        {
            var node = _root.FindNode(fileName);
            if (node == null)
            {
                return STATUS_CANNOT_DELETE;
            }
            if (node.IsDirectory && node.CountRecursive > 1)
            {
                return STATUS_DIRECTORY_NOT_EMPTY;
            }
            return STATUS_SUCCESS;
        }
    }

    public override void Cleanup(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        string? fileName,
        CleanupFlags flags
    )
    {
        lock (_sync)
        {
            // Debug.WriteLine($"Cleanup '{fileName}' flags={flags}");
            if (flags.HasFlag(CleanupFlags.Delete) && fileName != null)
            {
                var whiteoutPath = GetCreateTargetPath(fileName + ".daehidden");
                Directory.CreateDirectory(Path.GetDirectoryName(whiteoutPath)!);
                File.Create(whiteoutPath).Dispose();
                fileNode.PendingDelete = true;
            }
        }
    }

    public override void Close(FileSystemNode fileNode, FileSystemDescription fileDesc)
    {
        lock (_sync)
        {
            // Debug.WriteLine(
            //     $"Close '{fileNode.Node.GetPath()}' pendingDelete={fileNode.PendingDelete}"
            // );
            if (fileDesc.IsFile)
            {
                fileDesc.Dispose();
            }
            if (fileNode.RemoveOpen() == 0 && fileNode.PendingDelete)
            {
                fileNode.Node.RemoveFromParent();
                _nodes.Remove(fileNode.Node);
            }
        }
    }

    public override int Flush(
        FileSystemNode? fileNode,
        FileSystemDescription? fileDesc,
        out FileInfo fileInfo
    )
    {
        lock (_sync)
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
    }

    public override int GetFileInfo(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        out FileInfo fileInfo
    )
    {
        fileInfo = fileDesc.GetFileInfo();
        return STATUS_SUCCESS;
    }

    public override int GetSecurity(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        ref byte[]? securityDescriptor
    )
    {
        if (fileDesc.IsFile)
        {
            securityDescriptor = new SysFileInfo(fileDesc.Owner.Data.PhysicalPath)
                .GetAccessControl()
                .GetSecurityDescriptorBinaryForm();
            return STATUS_SUCCESS;
        }
        EnsureWritable(fileDesc);
        securityDescriptor = fileDesc.SecurityDescriptor;
        return STATUS_SUCCESS;
    }

    public override int GetSecurityByName(
        string fileName,
        out FileAttributes fileAttributes,
        ref byte[]? securityDescriptor
    )
    {
        var node = _root.FindNode(fileName);
        if (node == null)
        {
            fileAttributes = 0;
            return STATUS_OBJECT_NAME_NOT_FOUND;
        }

        if (node.Data.PhysicalPath is not string physical)
        {
            // placeholder: virtual directory with no single physical backing
            fileAttributes = System.IO.FileAttributes.Directory;
            return STATUS_SUCCESS;
        }

        fileAttributes = File.GetAttributes(physical);
        if (securityDescriptor != null)
        {
            securityDescriptor = node.IsDirectory
                ? new DirectoryInfo(physical).GetAccessControl().GetSecurityDescriptorBinaryForm()
                : new SysFileInfo(physical).GetAccessControl().GetSecurityDescriptorBinaryForm();
        }
        return STATUS_SUCCESS;
    }

    public override int GetVolumeInfo(out VolumeInfo VolumeInfo)
    {
        VolumeInfo = default(VolumeInfo);
        try
        {
            DriveInfo info = new DriveInfo(GetCreateTargetRootPath());
            VolumeInfo.TotalSize = (ulong)info.TotalSize;
            VolumeInfo.FreeSize = (ulong)info.AvailableFreeSpace;
        }
        catch (ArgumentException)
        {
            // DriveInfo only supports drives and not UNC paths;
            // better to use GetDiskFreeSpaceEx here? report zeros for now
        }
        return STATUS_SUCCESS;
    }

    public override int Overwrite(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        FileAttributes fileAttributes,
        bool replaceFileAttributes,
        ulong allocationSize,
        out FileInfo fileInfo
    )
    {
        if (fileDesc.IsFile)
        {
            EnsureWritable(fileDesc);
        }
        lock (_sync)
        {
            // Debug.WriteLine($"Overwrite '{fileDesc.Owner.GetPath()}'");
            EnsureWritable(fileDesc);
            fileInfo = fileDesc.GetFileInfo();
            if (replaceFileAttributes)
            {
                fileDesc.SetBasicInfo(fileAttributes | FileAttributes.Archive);
            }
            else if (fileAttributes != 0)
            {
                fileDesc.SetBasicInfo(
                    (FileAttributes)fileInfo.FileAttributes
                        | fileAttributes
                        | FileAttributes.Archive
                );
            }
            if (fileDesc.IsFile)
            {
                fileDesc.Stream.SetLength(0);
            }
            fileInfo = fileDesc.GetFileInfo();
            return STATUS_SUCCESS;
        }
    }

    public override int Read(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        Span<byte> buffer,
        ulong offset,
        uint length,
        out uint pBytesTransferred
    )
    {
        pBytesTransferred = 0;
        if (!fileDesc.IsFile)
        {
            return STATUS_FILE_IS_A_DIRECTORY;
        }
        var stream = fileDesc.BeginTransfer();
        try
        {
            if (offset >= (ulong)stream.Length)
            {
                return STATUS_END_OF_FILE;
            }
            pBytesTransferred = (uint)
                RandomAccess.Read(stream.SafeFileHandle, buffer[..(int)length], (long)offset);
            return STATUS_SUCCESS;
        }
        finally
        {
            fileDesc.EndTransfer();
        }
    }

    public override bool ReadDirectoryEntry(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        string? pattern,
        string? marker,
        ref object? context,
        out string? fileName,
        out FileInfo fileInfo
    )
    {
        lock (_sync)
        {
            var children = fileNode.Node.Children;
            var matchPattern = pattern == null ? null : pattern.Replace('"', '.');

            const int dotEntries = 2; // single dot double dot ntfs compliance
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
                        int found = BinarySearchChildren(children, marker);
                        index = (found >= 0 ? found + 1 : ~found) + dotEntries;
                    }
                }
            }
            else
            {
                index = (int)context;
            }

            while (index < children.Count + dotEntries)
            {
                context = index + 1;
                if (index == 0)
                {
                    fileName = ".";
                    fileInfo = GetInfoFor(fileNode.Node);
                }
                else if (index == 1)
                {
                    fileName = "..";
                    // .. can be root itself, NTFS
                    fileInfo = GetInfoFor(fileNode.Node.Parent ?? fileNode.Node);
                }
                else
                {
                    var child = children[index - dotEntries];
                    if (
                        matchPattern != null
                        && !VirtualNode<BackedEntry>.Wildcard.Match(child.Name, matchPattern)
                    )
                    {
                        index++;
                        continue;
                    }
                    fileName = child.Name;
                    fileInfo = GetInfoFor(child);
                }
                return true;
            }

            fileName = null;
            fileInfo = default;
            return false;
        }
    }

    private static int BinarySearchChildren(
        IReadOnlyList<VirtualNode<BackedEntry>> children,
        string name
    )
    {
        int lo = 0;
        int hi = children.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >>> 1;
            int cmp = string.Compare(children[mid].Name, name, StringComparison.OrdinalIgnoreCase);
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

    private static FileInfo GetInfoFor(VirtualNode<BackedEntry> child)
    {
        if (child.Data.PhysicalPath is string physical)
        {
            FileSystemInfo info = child.IsDirectory
                ? new DirectoryInfo(physical)
                : new SysFileInfo(physical);
            return info.GetFileInfo(ALLOCATION_UNIT);
        }

        // placeholder dir
        var fileInfo = new FileInfo
        {
            FileAttributes = (uint)System.IO.FileAttributes.Directory,
            ReparseTag = 0,
            FileSize = 0,
            AllocationSize = 0,
            IndexNumber = 0,
            HardLinks = 0,
        };
        ulong epoch = (ulong)new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();
        fileInfo.CreationTime =
            fileInfo.LastAccessTime =
            fileInfo.LastWriteTime =
            fileInfo.ChangeTime =
                epoch;
        return fileInfo;
    }

    public override int Rename(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        string fileName,
        string newFileName,
        bool replaceIfExists
    )
    {
        if (fileDesc.IsFile)
        {
            try
            {
                EnsureWritable(fileDesc);
            }
            catch (Exception ex)
            {
                return ExceptionHandler(ex);
            }
        }
        lock (_sync)
        {
            // Debug.WriteLine($"Rename '{fileName}' -> '{newFileName}'");
            var srcNode = _root.FindNode(fileName);
            if (srcNode == null)
            {
                return STATUS_OBJECT_NAME_NOT_FOUND;
            }
            try
            {
                if (srcNode.IsDirectory)
                {
                    if (!SubtreeInSink(srcNode))
                    {
                        return STATUS_NOT_SUPPORTED;
                    }
                    string srcDir = srcNode.Data.PhysicalPath!;
                    string destDir = GetCreateTargetPath(newFileName);
                    MoveDirectory(srcDir, destDir, replaceIfExists);
                    srcNode.RemoveFromParent();
                    _nodes.Remove(srcNode);
                    var destDirNode = _root.LinkDirectory(
                        destDir,
                        newFileName,
                        LinkFlags.Recursive | LinkFlags.Whiteouts
                    );
                    fileNode.Node = destDirNode;
                    fileDesc.Owner = destDirNode;
                    _nodes[destDirNode] = fileNode;
                    return STATUS_SUCCESS;
                }
                EnsureWritable(fileDesc);
                var srcPath = srcNode.Data.PhysicalPath;
                var targetPath = GetCreateTargetPath(newFileName);

                FileDescription.Rename(srcPath, targetPath, replaceIfExists);
                srcNode.RemoveFromParent();
                _nodes.Remove(srcNode);

                var destNode = _root.LinkFile(targetPath, newFileName);
                fileNode.Node = destNode;
                fileDesc.Owner = destNode;
                _nodes[destNode] = fileNode;
                return STATUS_SUCCESS;
            }
            catch (Exception ex)
            {
                return ExceptionHandler(ex);
            }
        }
    }

    public override int SetBasicInfo(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        FileAttributes fileAttributes,
        DateTime? creationTime,
        DateTime? lastAccessTime,
        DateTime? lastWriteTime,
        DateTime? changeTime,
        out FileInfo fileInfo
    )
    {
        lock (_sync)
        {
            // Debug.WriteLine($"SetBasicInfo '{fileDesc.Owner.GetPath()}'");
            if (!fileDesc.IsFile && fileDesc.Owner.Data.PhysicalPath is null)
            {
                EnsureWritable(fileDesc); // materialize placeholder dir
            }
            fileDesc.SetBasicInfo(fileAttributes, creationTime, lastAccessTime, lastWriteTime);
            fileInfo = fileDesc.GetFileInfo();
            return STATUS_SUCCESS;
        }
    }

    public override int SetFileSize(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        ulong newSize,
        bool setAllocationSize,
        out FileInfo fileInfo
    )
    {
        if (fileDesc.IsFile)
        {
            EnsureWritable(fileDesc);
        }
        lock (_sync)
        {
            if (!fileDesc.IsFile)
            {
                Win32.ThrowIoExceptionWithNtStatus(STATUS_FILE_IS_A_DIRECTORY);
            }
            // Debug.WriteLine($"SetFileSize '{fileDesc.Owner.GetPath()}' newSize={newSize}");
            EnsureWritable(fileDesc);
            if (!setAllocationSize || (ulong)fileDesc.Stream.Length > newSize)
            {
                fileDesc.Stream.SetLength((long)newSize);
            }
            fileInfo = fileDesc.GetFileInfo();
            return STATUS_SUCCESS;
        }
    }

    public override int SetSecurity(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        AccessControlSections sections,
        byte[] securityDescriptor
    )
    {
        lock (_sync)
        {
            // Debug.WriteLine($"SetSecurity '{fileDesc.Owner.GetPath()}'");
            // metadata-only change: no file content, so no copy-up
            if (!fileDesc.IsFile && fileDesc.Owner.Data.PhysicalPath is null)
            {
                EnsureWritable(fileDesc); // materialize a placeholder directory
            }
            fileDesc.SetSecurityDescriptor(sections, securityDescriptor);
            return STATUS_SUCCESS;
        }
    }

    public override int Write(
        FileSystemNode fileNode,
        FileSystemDescription fileDesc,
        ReadOnlySpan<byte> buffer,
        ulong offset,
        uint length,
        bool writeToEOF,
        bool constraintedIO,
        out uint pBytesTransferred,
        out FileInfo fileInfo
    )
    {
        // Debug.WriteLine($"Write '{fileDesc.Owner.GetPath()}' offset={offset} length={length}");
        EnsureWritable(fileDesc);
        var stream = fileDesc.BeginTransfer();
        try
        {
            var handle = stream.SafeFileHandle;
            long streamLength = stream.Length;

            if (constraintedIO)
            {
                if (offset >= (ulong)streamLength)
                {
                    pBytesTransferred = 0;
                    fileInfo = default;
                    return STATUS_SUCCESS;
                }
                if (offset + length > (ulong)streamLength)
                {
                    length = (uint)((ulong)streamLength - offset);
                }
            }

            long target = writeToEOF ? streamLength : (long)offset;
            RandomAccess.Write(handle, buffer[..(int)length], target);
            pBytesTransferred = length;
            fileInfo = fileDesc.GetFileInfo();
            return STATUS_SUCCESS;
        }
        finally
        {
            fileDesc.EndTransfer();
        }
    }
}
