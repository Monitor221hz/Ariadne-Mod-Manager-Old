using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Ariadne.VFS;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class DeploymentPreviewServiceTests
{
    private sealed class StubMod(string name, string target) : ILibraryMod
    {
        public IModInfo Info { get; } = new ModInfo(0, SourceType.Local, "1.0", [], target);
        public string Name => name;
        public DirectoryInfo Directory => new(".");
        public VirtualNode<ModFileEntry> Content { get; } =
            new(name, NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void ReplaceInfo(IModInfo info) { }

        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => obj.GetHashCode();

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);

        public ModFileEntry AddFile(string path, long size = 1)
        {
            var entry = new ModFileEntry(
                path.Split('\\', '/').Last(),
                ModEntryKind.File,
                Info,
                path,
                size,
                DateTimeOffset.UnixEpoch
            );
            Content.AddFile(path, entry);
            return entry;
        }
    }

    private static readonly ISupportedGame Config = new SupportedGame(
        "Test Game",
        [],
        new VendorInfo(0, 0),
        new GamePath("_root", "", []),
        [],
        [new GamePath("Data", "Data", []), new GamePath("Root", "", [])]
    );

    private readonly DeploymentPreviewService _service = new();

    [Fact]
    public void GroupByTarget_ResolvesKeysAgainstConfiguration()
    {
        var untargeted = new StubMod("A", "");
        var targeted = new StubMod("B", "Data");

        var groups = _service.GroupByTarget([untargeted, targeted], Config);

        Assert.Equal(2, groups.Count);
        var root = groups.Single(g => g.DisplayKey == "Root");
        Assert.Same(untargeted, Assert.Single(root.Mods));
        Assert.NotNull(root.Path);
        var data = groups.Single(g => g.DisplayKey == "Data");
        Assert.Same(targeted, Assert.Single(data.Mods));
    }

    [Fact]
    public void GroupByTarget_UnknownTarget_MarkedUnresolved()
    {
        var groups = _service.GroupByTarget([new StubMod("A", "Nowhere")], Config);

        Assert.Equal("<unresolved: Nowhere>", Assert.Single(groups).DisplayKey);
    }

    [Fact]
    public void GroupByTarget_NullConfiguration_FallsBackToRootKey()
    {
        var groups = _service.GroupByTarget([new StubMod("A", "")], null);

        var group = Assert.Single(groups);
        Assert.Equal("Root", group.DisplayKey);
        Assert.Null(group.Path);
    }

    [Fact]
    public void MergeContent_LaterModWinsConflict()
    {
        var first = new StubMod("First", "");
        first.AddFile("shared.txt", size: 1);
        var second = new StubMod("Second", "");
        var winning = second.AddFile("shared.txt", size: 2);

        var merged = _service.MergeContent([first, second]);

        var node = merged.GetNode("shared.txt");
        Assert.Same(winning, node.Data);
    }

    [Fact]
    public void MergeContent_SynthesizesDirectoryEntries()
    {
        var mod = new StubMod("A", "");
        mod.AddFile("Data\\Scripts\\script.lua");

        var merged = _service.MergeContent([mod]);

        var data = merged.GetNode("Data");
        Assert.True(data.IsDirectory);
        Assert.NotNull(data.Data);
        Assert.Equal(ModEntryKind.Directory, data.Data!.Kind);
        var scripts = data.GetNode("Scripts");
        Assert.Equal(ModEntryKind.Directory, scripts.Data!.Kind);
    }
}
