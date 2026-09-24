using System.Diagnostics.CodeAnalysis;
using Daedalus.Contracts.ModManager;
using Daedalus.ModManager;
using Daedalus.ModManager.GUI.ViewModels;
using Xunit;

namespace Daedalus.ModManager.GUI.Tests;

public class ModOrderSyncTests
{
    private sealed class FakeMod(string name, uint priority = 0) : ILibraryMod
    {
        public IModInfo Info { get; } =
            new ModInfo(0, SourceType.Local, "1.0", [], "", priority, false);
        public string Name { get; } = name;
        public DirectoryInfo Directory => new(".");
        public Daedalus.VFS.VirtualNode<ModFileEntry> Content { get; } =
            new("", Daedalus.VFS.NodeFlags.Directory, null, default);

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

        public void RenameTo(string newName) { }
    }

    private static ModGroup Group(string name, params ILibraryMod[] mods) =>
        new(name, mods.ToList());

    [Fact]
    public void ApplyOrder_AssignsContiguousPrioritiesAcrossLooseAndGroups()
    {
        var m1 = new FakeMod("M1");
        var m2 = new FakeMod("M2");
        var m3 = new FakeMod("M3");
        var g1 = Group("G1", m3);
        var list = new ModList([m1, m2], [g1]);

        ModOrderSync.ApplyOrder(
            [m1, m2],
            [g1],
            [
                [m3],
            ],
            list
        );

        Assert.Equal(1u, m1.Info.Priority);
        Assert.Equal(2u, m2.Info.Priority);
        Assert.Equal(3u, m3.Info.Priority);
        Assert.Equal(new[] { m1, m2 }, list.LooseMods);
        Assert.Equal([g1], list.ModGroups.ToArray());
        Assert.Equal([m3], g1.ToArray());
    }

    [Fact]
    public void ApplyOrder_ModMovedAcrossBoundaries_StaysUnique()
    {
        var m1 = new FakeMod("M1");
        var m2 = new FakeMod("M2");
        var m3 = new FakeMod("M3");
        var m4 = new FakeMod("M4");
        var g1 = Group("G1");
        var list = new ModList([m1, m2], [g1]);

        ModOrderSync.ApplyOrder(
            [m1, m3],
            [g1],
            [
                [m4, m2],
            ],
            list
        );

        var priorities = new[]
        {
            m1.Info.Priority,
            m2.Info.Priority,
            m3.Info.Priority,
            m4.Info.Priority,
        };
        Assert.Equal(
            priorities.OrderBy(p => p).ToArray(),
            priorities.Distinct().OrderBy(p => p).ToArray()
        );
        Assert.Equal(4u, priorities.Max());
        Assert.Equal([m4, m2], g1.ToArray());
        Assert.Equal(new[] { m1, m3 }, list.LooseMods);
    }

    [Fact]
    public void ApplyOrder_HealsDuplicatePriorities_FromExistingState()
    {
        var m1 = new FakeMod("M1", 4);
        var m2 = new FakeMod("M2", 4);
        var m3 = new FakeMod("M3", 4);
        var list = new ModList([m1, m2], []);
        list.LooseMods.Add(m3);

        ModOrderSync.ApplyOrder([m1, m2, m3], [], [], list);

        Assert.Equal(1u, m1.Info.Priority);
        Assert.Equal(2u, m2.Info.Priority);
        Assert.Equal(3u, m3.Info.Priority);
    }

    [Fact]
    public void ApplyOrder_GroupReorder_RenumbersMembers()
    {
        var m1 = new FakeMod("M1");
        var m2 = new FakeMod("M2");
        var gA = Group("A");
        var gB = Group("B");
        var list = new ModList([], [gA, gB]);

        ModOrderSync.ApplyOrder(
            [],
            [gB, gA],
            [
                [m1],
                [m2],
            ],
            list
        );

        Assert.Equal(1u, m1.Info.Priority);
        Assert.Equal(2u, m2.Info.Priority);
        Assert.Equal([gB, gA], list.ModGroups.ToArray());
    }
}
