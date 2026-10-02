using System.Diagnostics.CodeAnalysis;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public class ModOrderSyncTests
{
    private sealed class FakeMod(string name) : ILibraryMod
    {
        public IModInfo Info { get; } = new ModInfo(0, SourceType.Local, "1.0", [], "");
        public string Name { get; } = name;
        public DirectoryInfo Directory => new(".");
        public Ariadne.VFS.VirtualNode<ModFileEntry> Content { get; } =
            new("", Ariadne.VFS.NodeFlags.Directory, null, default);

        public bool Equals(ILibraryMod? x, ILibraryMod? y)
        {
            return x is not null && y is not null && x.Directory.FullName == y.Directory.FullName;
        }

        public int GetHashCode([DisallowNull] ILibraryMod obj)
        {
            return obj.Directory.FullName.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);

        public void RefreshContent() { }

        public void ReplaceInfo(IModInfo info) { }

        public void RenameTo(string newName) { }
    }

    private static IModListEntry Entry(FakeMod mod) => new ModListEntry(mod, true);

    private static ModGroup Group(string name, params IModListEntry[] entries) =>
        new(name, entries.ToList());

    [Fact]
    public void ApplyOrder_AppliesLooseAndGroupOrder()
    {
        var e1 = Entry(new FakeMod("M1"));
        var e2 = Entry(new FakeMod("M2"));
        var e3 = Entry(new FakeMod("M3"));
        var g1 = Group("G1", e3);
        var list = new ModList([e1, e2], [g1]);

        ModOrderSync.ApplyOrder(
            [e1, e2],
            [g1],
            [
                [e3],
            ],
            list
        );

        Assert.Equal([e1, e2], list.LooseMods.ToArray());
        Assert.Equal([g1], list.ModGroups.ToArray());
        Assert.Equal([e3], g1.ToArray());
    }

    [Fact]
    public void ApplyOrder_ModMovedAcrossBoundaries_LandsInNewHome()
    {
        var e1 = Entry(new FakeMod("M1"));
        var e2 = Entry(new FakeMod("M2"));
        var e3 = Entry(new FakeMod("M3"));
        var e4 = Entry(new FakeMod("M4"));
        var g1 = Group("G1");
        var list = new ModList([e1, e2], [g1]);

        ModOrderSync.ApplyOrder(
            [e1, e3],
            [g1],
            [
                [e4, e2],
            ],
            list
        );

        Assert.Equal([e4, e2], g1.ToArray());
        Assert.Equal([e1, e3], list.LooseMods.ToArray());
        Assert.Equal(4, list.Count);
    }

    [Fact]
    public void ApplyOrder_RebuildsDuplicateTracking()
    {
        var e1 = Entry(new FakeMod("M1"));
        var e2 = Entry(new FakeMod("M2"));
        var list = new ModList([e1, e2], []);

        ModOrderSync.ApplyOrder([e1], [], [], list);
        list.Add(new ModListEntry(e2.Mod, true));

        Assert.Equal(1, list.Count(entry => ReferenceEquals(entry.Mod, e2.Mod)));
    }

    [Fact]
    public void ApplyOrder_GroupReorder_RewritesGroupSequence()
    {
        var e1 = Entry(new FakeMod("M1"));
        var e2 = Entry(new FakeMod("M2"));
        var gA = Group("A");
        var gB = Group("B");
        var list = new ModList([], [gA, gB]);

        ModOrderSync.ApplyOrder(
            [],
            [gB, gA],
            [
                [e1],
                [e2],
            ],
            list
        );

        Assert.Equal([gB, gA], list.ModGroups.ToArray());
        Assert.Equal([e1], gB.ToArray());
        Assert.Equal([e2], gA.ToArray());
    }
}
