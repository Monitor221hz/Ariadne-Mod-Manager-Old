using Ariadne.Contracts.ModManager;
using Ariadne.VFS;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class ModProfileEditorTests
{
    private sealed class StubMod(string directory) : ILibraryMod
    {
        public IModInfo Info { get; } = new ModInfo(0, SourceType.Local, "1.0", [], "");
        public string Name => directory;
        public DirectoryInfo Directory { get; } = new(directory);
        public VirtualNode<ModFileEntry> Content =>
            new(directory, NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void ReplaceInfo(IModInfo info) { }

        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => obj.GetHashCode();

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    private static ModProfile EmptyProfile() =>
        new("P", new ModList([], []), new Version(1, 0), new DirectoryInfo("."));

    private readonly ModProfileEditor _editor = new();

    [Fact]
    public void TryAddMod_AddsInactiveLooseMod()
    {
        var profile = EmptyProfile();
        var mod = new StubMod("mods/A");

        Assert.True(_editor.TryAddMod(profile, mod));

        var entry = Assert.Single(profile.ModList.LooseMods);
        Assert.Same(mod, entry.Mod);
        Assert.False(entry.Active);
    }

    [Fact]
    public void TryAddMod_SameDirectory_ReturnsFalse()
    {
        var profile = EmptyProfile();
        var first = new StubMod("mods/A");
        var second = new StubMod("mods/A");
        _editor.TryAddMod(profile, first);

        Assert.False(_editor.TryAddMod(profile, second));
        Assert.Single(profile.ModList.LooseMods);
    }

    [Fact]
    public void CreateGroup_GeneratesUniqueNames()
    {
        var profile = EmptyProfile();

        var first = _editor.CreateGroup(profile);
        var second = _editor.CreateGroup(profile);

        Assert.Equal("New Group", first.Name);
        Assert.Equal("New Group 2", second.Name);
        Assert.Equal(2, profile.ModList.ModGroups.Count);
    }

    [Fact]
    public void CreateGroup_AssignsReadableHeaderColor()
    {
        var profile = EmptyProfile();

        var group = _editor.CreateGroup(profile);

        Assert.True(
            GroupHeaderColors.RelativeLuminance(
                group.HeaderColor.R,
                group.HeaderColor.G,
                group.HeaderColor.B
            ) <= 0.17
        );
    }
}
