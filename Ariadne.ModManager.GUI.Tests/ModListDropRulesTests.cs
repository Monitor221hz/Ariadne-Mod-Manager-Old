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
        public IModInfo Info { get; } = new ModManager.ModInfo(1, SourceType.Local, "1.0", [], "");
        public string Name => Directory.Name;
        public DirectoryInfo Directory { get; } = directory;
        public VirtualNode<ModFileEntry> Content { get; } = content;

        public void RefreshContent() { }

        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => 0;

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    private readonly TempDirectory _temp = new();
    private readonly ModListDropRules _rules = new(new ContentMoveService());

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
        var (ownerB, _) = CreateMod("ModB");
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
        var (ownerB, _) = CreateMod("ModB");
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
