using Ariadne.Contracts.ModManager;
using Ariadne.ModManager.GUI.DragDrop;
using Ariadne.ModManager.GUI.ViewModels;
using Ariadne.VFS;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public class ModListDropRulesTests : IDisposable
{
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "AriadneDropTests-" + Guid.NewGuid().ToString("N")
            );

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }

    private sealed class FakeLibraryMod(DirectoryInfo directory, VirtualNode<ModFileEntry> content)
        : ILibraryMod
    {
        public IModInfo Info { get; private set; } =
            new ModManager.ModInfo(1, SourceType.Local, "1.0", [], "");
        public string Name => Directory.Name;
        public DirectoryInfo Directory { get; private set; } = directory;
        public VirtualNode<ModFileEntry> Content { get; } = content;

        public void RefreshContent() { }

        public void ReplaceInfo(IModInfo info) => Info = info;

        public void RenameTo(string newName)
        {
            var destination = Path.Join(Directory.Parent!.FullName, newName);
            Directory.MoveTo(destination);
            Directory = new DirectoryInfo(destination);
        }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => 0;

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    private readonly TempDirectory _temp = new();
    private readonly ModListDropRules _rules = new(
        new ContentMoveService(new LibraryModSerializer([]))
    );

    public void Dispose() => _temp.Dispose();

    private string ModDir(string name) => Path.Combine(_temp.Path, name);

    private (ModEntryNodeViewModel Owner, VirtualNode<ModFileEntry> Root) CreateMod(string name)
    {
        var directory = new DirectoryInfo(ModDir(name));
        directory.Create();
        var root = new VirtualNode<ModFileEntry>(name, NodeFlags.Directory, null, default);
        return (
            new ModEntryNodeViewModel(new ModListEntry(new FakeLibraryMod(directory, root), true)),
            root
        );
    }

    private VirtualNode<ModFileEntry> CreateFileNode(
        VirtualNode<ModFileEntry> root,
        string modDir,
        string relativePath,
        ModEntryKind kind = ModEntryKind.File
    )
    {
        var absolute = Path.Join(modDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(absolute, "x");
        var name = relativePath[(relativePath.LastIndexOf('/') + 1)..];
        return root.AddFile(
            relativePath,
            new ModFileEntry(name, kind, ModOrigin, absolute, 1, DateTimeOffset.UnixEpoch)
        );
    }

    private VirtualNode<ModFileEntry> CreateDirectoryNode(
        VirtualNode<ModFileEntry> root,
        string modDir,
        string relativePath
    )
    {
        Directory.CreateDirectory(
            Path.Join(modDir, relativePath.Replace('/', Path.DirectorySeparatorChar))
        );
        return root.AddDirectory(relativePath);
    }

    private static readonly IModInfo ModOrigin = new ModManager.ModInfo(
        1,
        SourceType.Local,
        "1.0",
        [],
        ""
    );

    private bool IsLegal(TreeNodeViewModel dragged, TreeNodeViewModel? target) =>
        _rules.IsLegal(
            [dragged],
            target,
            null,
            DataGridRowDropPosition.Inside,
            DragDropEffects.Move
        );

    [Fact]
    public void File_IntoFolder_MovesFile()
    {
        var (owner, root) = CreateMod("ModA");
        var folder = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        var file = CreateFileNode(root, ModDir("ModA"), "a.txt");
        var fileVm = new FileLeafNodeViewModel(file, owner);
        var folderVm = new DirectoryNodeViewModel(folder, owner);

        Assert.True(IsLegal(fileVm, folderVm));
        var affected = _rules.ExecuteContentDrop([fileVm], folderVm);

        Assert.True(File.Exists(Path.Combine(ModDir("ModA"), "stuff", "a.txt")));
        Assert.False(File.Exists(Path.Combine(ModDir("ModA"), "a.txt")));
        Assert.Equal([owner], affected);
    }

    [Fact]
    public void Folder_IntoFolder_MovesWithContents()
    {
        var (owner, root) = CreateMod("ModA");
        var stuff = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        CreateFileNode(root, ModDir("ModA"), "stuff/inner.txt");
        var misc = CreateDirectoryNode(root, ModDir("ModA"), "misc");
        var stuffVm = new DirectoryNodeViewModel(stuff, owner);
        var miscVm = new DirectoryNodeViewModel(misc, owner);

        Assert.True(IsLegal(stuffVm, miscVm));
        _rules.ExecuteContentDrop([stuffVm], miscVm);

        Assert.True(File.Exists(Path.Combine(ModDir("ModA"), "misc", "stuff", "inner.txt")));
        Assert.False(Directory.Exists(Path.Combine(ModDir("ModA"), "stuff")));
    }

    [Fact]
    public void Folder_IntoParentMod_FlattensContentsToTopLevel()
    {
        var (owner, root) = CreateMod("ModA");
        var stuff = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        CreateFileNode(root, ModDir("ModA"), "stuff/a.txt");
        CreateFileNode(root, ModDir("ModA"), "stuff/b.txt");
        var stuffVm = new DirectoryNodeViewModel(stuff, owner);

        Assert.True(IsLegal(stuffVm, owner));
        var affected = _rules.ExecuteContentDrop([stuffVm], owner);

        Assert.True(File.Exists(Path.Combine(ModDir("ModA"), "a.txt")));
        Assert.True(File.Exists(Path.Combine(ModDir("ModA"), "b.txt")));
        Assert.False(Directory.Exists(Path.Combine(ModDir("ModA"), "stuff")));
        Assert.Equal([owner], affected);
    }

    [Fact]
    public void Folder_IntoNonParentMod_MovesFolder()
    {
        var (ownerA, rootA) = CreateMod("ModA");
        var (ownerB, rootB) = CreateMod("ModB");
        CreateFileNode(rootB, ModDir("ModB"), "existing.txt");
        var stuff = CreateDirectoryNode(rootA, ModDir("ModA"), "stuff");
        CreateFileNode(rootA, ModDir("ModA"), "stuff/inner.txt");
        var stuffVm = new DirectoryNodeViewModel(stuff, ownerA);

        Assert.True(IsLegal(stuffVm, ownerB));
        var affected = _rules.ExecuteContentDrop([stuffVm], ownerB);

        Assert.True(File.Exists(Path.Combine(ModDir("ModB"), "stuff", "inner.txt")));
        Assert.False(Directory.Exists(Path.Combine(ModDir("ModA"), "stuff")));
        Assert.Equal(2, affected.Count);
        Assert.Contains(ownerA, affected);
        Assert.Contains(ownerB, affected);
    }

    [Fact]
    public void File_IntoNonParentMod_MovesFile()
    {
        var (ownerA, rootA) = CreateMod("ModA");
        var (ownerB, rootB) = CreateMod("ModB");
        CreateFileNode(rootB, ModDir("ModB"), "existing.txt");
        var file = CreateFileNode(rootA, ModDir("ModA"), "a.txt");
        var fileVm = new FileLeafNodeViewModel(file, ownerA);

        Assert.True(IsLegal(fileVm, ownerB));
        _rules.ExecuteContentDrop([fileVm], ownerB);

        Assert.True(File.Exists(Path.Combine(ModDir("ModB"), "a.txt")));
        Assert.False(File.Exists(Path.Combine(ModDir("ModA"), "a.txt")));
    }

    [Fact]
    public void File_FromSubfolder_IntoParentMod_MovesToTopLevel()
    {
        var (owner, root) = CreateMod("ModA");
        CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        var file = CreateFileNode(root, ModDir("ModA"), "stuff/a.txt");
        var fileVm = new FileLeafNodeViewModel(file, owner);

        Assert.True(IsLegal(fileVm, owner));
        _rules.ExecuteContentDrop([fileVm], owner);

        Assert.True(File.Exists(Path.Combine(ModDir("ModA"), "a.txt")));
        Assert.False(File.Exists(Path.Combine(ModDir("ModA"), "stuff", "a.txt")));
    }

    [Fact]
    public void File_IntoOwnParentFolder_IsIllegal()
    {
        var (owner, root) = CreateMod("ModA");
        var file = CreateFileNode(root, ModDir("ModA"), "a.txt");
        var fileVm = new FileLeafNodeViewModel(file, owner);

        Assert.False(IsLegal(fileVm, owner));
    }

    [Fact]
    public void Folder_IntoItself_IsIllegal()
    {
        var (owner, root) = CreateMod("ModA");
        var stuff = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        var stuffVm = new DirectoryNodeViewModel(stuff, owner);

        Assert.False(IsLegal(stuffVm, stuffVm));
    }

    [Fact]
    public void Folder_IntoOwnDescendant_IsIllegal()
    {
        var (owner, root) = CreateMod("ModA");
        var stuff = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        var inner = CreateDirectoryNode(root, ModDir("ModA"), "stuff/inner");
        var stuffVm = new DirectoryNodeViewModel(stuff, owner);
        var innerVm = new DirectoryNodeViewModel(inner, owner);

        Assert.False(IsLegal(stuffVm, innerVm));
    }

    [Fact]
    public void File_IntoFolder_WithNameCollision_IsIllegal()
    {
        var (owner, root) = CreateMod("ModA");
        var folder = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        CreateFileNode(root, ModDir("ModA"), "stuff/a.txt");
        var file = CreateFileNode(root, ModDir("ModA"), "a.txt");
        var fileVm = new FileLeafNodeViewModel(file, owner);
        var folderVm = new DirectoryNodeViewModel(folder, owner);

        Assert.False(IsLegal(fileVm, folderVm));
    }

    [Fact]
    public void Folder_IntoParentMod_WithChildCollision_IsIllegal()
    {
        var (owner, root) = CreateMod("ModA");
        var stuff = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        CreateFileNode(root, ModDir("ModA"), "stuff/a.txt");
        CreateFileNode(root, ModDir("ModA"), "a.txt");
        var stuffVm = new DirectoryNodeViewModel(stuff, owner);

        Assert.False(IsLegal(stuffVm, owner));
    }

    [Fact]
    public void File_InsideArchive_IsIllegal()
    {
        var (owner, root) = CreateMod("ModA");
        var folder = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        var archive = CreateFileNode(root, ModDir("ModA"), "pack.zip", ModEntryKind.Archive);
        var inner = archive.AddFile(
            "inner.txt",
            new ModFileEntry(
                "inner.txt",
                ModEntryKind.File,
                ModOrigin,
                "inner.txt",
                1,
                DateTimeOffset.UnixEpoch
            )
        );
        var innerVm = new FileLeafNodeViewModel(inner, owner);
        var folderVm = new DirectoryNodeViewModel(folder, owner);

        Assert.False(innerVm.IsDiskBacked);
        Assert.False(IsLegal(innerVm, folderVm));
    }

    [Fact]
    public void Archive_IntoFolder_MovesArchive()
    {
        var (owner, root) = CreateMod("ModA");
        var folder = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        var archive = CreateFileNode(root, ModDir("ModA"), "pack.zip", ModEntryKind.Archive);
        var archiveVm = new ArchiveLeafNodeViewModel(archive, owner);
        var folderVm = new DirectoryNodeViewModel(folder, owner);

        Assert.True(archiveVm.IsDiskBacked);
        Assert.True(IsLegal(archiveVm, folderVm));
        _rules.ExecuteContentDrop([archiveVm], folderVm);

        Assert.True(File.Exists(Path.Combine(ModDir("ModA"), "stuff", "pack.zip")));
    }

    private (OverwriteNodeViewModel Owner, VirtualNode<ModFileEntry> Root) CreateOverwrite(
        string name
    )
    {
        var directory = new DirectoryInfo(ModDir(name));
        directory.Create();
        var root = new VirtualNode<ModFileEntry>(name, NodeFlags.Directory, null, default);
        var mod = new LibraryMod(new ModInfo(1, SourceType.Local, "1.0", [], ""), directory, []);
        return (new OverwriteNodeViewModel(mod), root);
    }

    [Fact]
    public void File_FromOverwrite_IntoModFolder_Moves()
    {
        var (modOwner, modRoot) = CreateMod("ModA");
        var (overwriteOwner, overwriteRoot) = CreateOverwrite("Overwrite");
        var file = CreateFileNode(overwriteRoot, ModDir("Overwrite"), "runtime.ini");
        var folder = CreateDirectoryNode(modRoot, ModDir("ModA"), "stuff");
        var fileVm = new FileLeafNodeViewModel(file, overwriteOwner);
        var folderVm = new DirectoryNodeViewModel(folder, modOwner);

        Assert.True(IsLegal(fileVm, folderVm));
        _rules.ExecuteContentDrop([fileVm], folderVm);

        Assert.True(File.Exists(Path.Combine(ModDir("ModA"), "stuff", "runtime.ini")));
        Assert.False(File.Exists(Path.Combine(ModDir("Overwrite"), "runtime.ini")));
    }

    [Fact]
    public void File_FromMod_IntoOverwrite_Moves()
    {
        var (modOwner, modRoot) = CreateMod("ModA");
        var (overwriteOwner, _) = CreateOverwrite("Overwrite");
        var file = CreateFileNode(modRoot, ModDir("ModA"), "a.txt");
        var fileVm = new FileLeafNodeViewModel(file, modOwner);

        Assert.True(IsLegal(fileVm, overwriteOwner));
        _rules.ExecuteContentDrop([fileVm], overwriteOwner);

        Assert.True(File.Exists(Path.Combine(ModDir("Overwrite"), "a.txt")));
        Assert.False(File.Exists(Path.Combine(ModDir("ModA"), "a.txt")));
    }

    [Fact]
    public void File_IntoEmptyLocalMod_InheritsSourceInfo()
    {
        var (sourceOwner, sourceRoot) = CreateMod("ModA");
        sourceOwner.Model.ReplaceInfo(
            new ModManager.ModInfo(new ModID(12604, SourceType.NexusMods), "6.1", [], "Data")
        );
        var (destinationOwner, _) = CreateMod("ModB");
        var file = CreateFileNode(sourceRoot, ModDir("ModA"), "a.txt");
        var fileVm = new FileLeafNodeViewModel(file, sourceOwner);

        _rules.ExecuteContentDrop([fileVm], destinationOwner);

        Assert.Equal(new ModID(12604, SourceType.NexusMods), destinationOwner.Model.Info.ID);
        Assert.Equal("6.1", destinationOwner.Model.Info.Version);
        Assert.Equal("Data", destinationOwner.Model.Info.Target);
        Assert.Equal("ModA a", destinationOwner.DisplayName);
        Assert.True(File.Exists(Path.Combine(_temp.Path, "ModA a", "a.txt")));
        Assert.True(File.Exists(Path.Combine(_temp.Path, "ModA a", LibraryModSerializer.FileName)));
    }

    [Fact]
    public void File_IntoEmptyLocalMod_ConflictingName_AppendsNumber()
    {
        CreateMod("ModA a");
        var (sourceOwner, sourceRoot) = CreateMod("ModA");
        var (destinationOwner, _) = CreateMod("ModB");
        var file = CreateFileNode(sourceRoot, ModDir("ModA"), "a.txt");
        var fileVm = new FileLeafNodeViewModel(file, sourceOwner);

        _rules.ExecuteContentDrop([fileVm], destinationOwner);

        Assert.Equal("ModA a 2", destinationOwner.DisplayName);
        Assert.True(File.Exists(Path.Combine(_temp.Path, "ModA a 2", "a.txt")));
    }

    [Fact]
    public void Folder_IntoEmptyLocalMod_RenamesWithFolderName()
    {
        var (sourceOwner, sourceRoot) = CreateMod("ModA");
        var folder = CreateDirectoryNode(sourceRoot, ModDir("ModA"), "meshes");
        var (destinationOwner, _) = CreateMod("ModB");
        var folderVm = new DirectoryNodeViewModel(folder, sourceOwner);

        _rules.ExecuteContentDrop([folderVm], destinationOwner);

        Assert.Equal("ModA meshes", destinationOwner.DisplayName);
        Assert.True(Directory.Exists(Path.Combine(_temp.Path, "ModA meshes", "meshes")));
    }

    [Fact]
    public void File_IntoNonEmptyMod_KeepsDestinationInfo()
    {
        var (sourceOwner, sourceRoot) = CreateMod("ModA");
        sourceOwner.Model.ReplaceInfo(
            new ModManager.ModInfo(new ModID(12604, SourceType.NexusMods), "6.1", [], "Data")
        );
        var (destinationOwner, destinationRoot) = CreateMod("ModB");
        CreateFileNode(destinationRoot, ModDir("ModB"), "existing.txt");
        var file = CreateFileNode(sourceRoot, ModDir("ModA"), "a.txt");
        var fileVm = new FileLeafNodeViewModel(file, sourceOwner);

        _rules.ExecuteContentDrop([fileVm], destinationOwner);

        Assert.Equal(new ModID(1, SourceType.Local), destinationOwner.Model.Info.ID);
        Assert.True(File.Exists(Path.Combine(ModDir("ModB"), "a.txt")));
    }

    [Fact]
    public void Group_OntoOverwrite_IsIllegal()
    {
        var (owner, _) = CreateOverwrite("Overwrite");
        var group = new GroupHeaderNodeViewModel(new ModGroup("G", []));
        var (modOwner, _) = CreateMod("ModA");

        Assert.False(
            _rules.IsLegal(
                [group],
                owner,
                null,
                DataGridRowDropPosition.After,
                DragDropEffects.Move
            )
        );
        Assert.False(
            _rules.IsLegal(
                [group],
                owner,
                null,
                DataGridRowDropPosition.Before,
                DragDropEffects.Move
            )
        );
        Assert.True(
            _rules.IsLegal(
                [group],
                modOwner,
                null,
                DataGridRowDropPosition.After,
                DragDropEffects.Move
            )
        );
    }

    [Fact]
    public void File_NonMoveEffect_IsIllegal()
    {
        var (owner, root) = CreateMod("ModA");
        var folder = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        var file = CreateFileNode(root, ModDir("ModA"), "a.txt");
        var fileVm = new FileLeafNodeViewModel(file, owner);
        var folderVm = new DirectoryNodeViewModel(folder, owner);

        Assert.False(
            _rules.IsLegal(
                [fileVm],
                folderVm,
                null,
                DataGridRowDropPosition.Inside,
                DragDropEffects.Copy
            )
        );
    }

    [Fact]
    public void File_BeforeFolder_IsIllegal()
    {
        var (owner, root) = CreateMod("ModA");
        var folder = CreateDirectoryNode(root, ModDir("ModA"), "stuff");
        var file = CreateFileNode(root, ModDir("ModA"), "a.txt");
        var fileVm = new FileLeafNodeViewModel(file, owner);
        var folderVm = new DirectoryNodeViewModel(folder, owner);

        Assert.False(
            _rules.IsLegal(
                [fileVm],
                folderVm,
                null,
                DataGridRowDropPosition.Before,
                DragDropEffects.Move
            )
        );
    }
}
