using System.Diagnostics.CodeAnalysis;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class ModListTests
{
    private sealed class FakeMod(string name) : ILibraryMod
    {
        public IModInfo Info { get; } = new ModInfo(0, SourceType.Local, "1.0", [], "");
        public string Name { get; } = name;
        public DirectoryInfo Directory => new(".");
        public VFS.VirtualNode<ModFileEntry> Content { get; } =
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

    private static IModListEntry Entry(string name) => new ModListEntry(new FakeMod(name), true);

    // loose [A,B], g1 [C,D,E], g2 [] (empty), g3 [F]
    private static ModList NewList(out List<IModListEntry> loose, out List<IModGroup> groups)
    {
        loose = [Entry("A"), Entry("B")];
        groups =
        [
            new ModGroup("g1", [Entry("C"), Entry("D"), Entry("E")]),
            new ModGroup("g2", []),
            new ModGroup("g3", [Entry("F")]),
        ];
        return new ModList(loose, groups);
    }

    [Fact]
    public void Count_SumsLooseModsAndGroupContents()
    {
        var list = NewList(out _, out _);

        Assert.Equal(6, list.Count);
    }

    [Fact]
    public void Indexer_FlattensLooseThenGroups()
    {
        var list = NewList(out _, out _);

        Assert.Equal(
            new[] { "A", "B", "C", "D", "E", "F" },
            Enumerable.Range(0, list.Count).Select(i => list[i].Mod.Name)
        );
    }

    [Fact]
    public void Indexer_Set_WritesToLooseBackingList()
    {
        var list = NewList(out var loose, out _);
        var x = Entry("X");

        list[0] = x;

        Assert.Same(x, loose[0]);
    }

    [Fact]
    public void Indexer_Set_WritesToCorrectGroupBackingList()
    {
        var list = NewList(out _, out var groups);
        var x = Entry("X");

        list[4] = x; // flattened index 4 = E = g1[2]

        Assert.Same(x, groups[0][2]);
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        var list = NewList(out _, out _);

        Assert.Throws<ArgumentOutOfRangeException>(() => list[6]);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[-1]);
    }

    [Fact]
    public void Insert_BeforeFirstGroupStart_GoesToLooseSection()
    {
        var list = NewList(out var loose, out _);
        var x = Entry("X");

        list.Insert(2, x); // loose/grouped boundary

        Assert.Equal(3, loose.Count);
        Assert.Same(x, loose[2]);
        Assert.Equal(7, list.Count);
    }

    [Fact]
    public void Insert_MiddleOfGroup_StaysInGroup()
    {
        var list = NewList(out var loose, out var groups);

        list.Insert(3, Entry("X")); // before D, inside g1

        Assert.Equal(new[] { "A", "B", "C", "X", "D", "E", "F" }, list.Select(m => m.Mod.Name));
        Assert.Equal(2, loose.Count);
        Assert.Equal("X", groups[0][1].Mod.Name);
    }

    [Fact]
    public void Insert_AtGroupStartAfterEmptyGroup_LandsInThatGroup()
    {
        var list = NewList(out var loose, out var groups);
        var y = Entry("Y");

        list.Insert(5, y); // F's position: start of g3, past empty g2

        Assert.Equal(new[] { "A", "B", "C", "D", "E", "Y", "F" }, list.Select(m => m.Mod.Name));
        Assert.Equal(2, loose.Count);
        Assert.Same(y, groups[2][0]);
    }

    [Fact]
    public void Insert_AtCount_AppendsToLastGroup()
    {
        var list = NewList(out _, out var groups);
        var x = Entry("X");

        list.Insert(list.Count, x);

        Assert.Equal(2, groups[2].Count);
        Assert.Same(x, groups[2][1]);
        Assert.Same(x, list[list.Count - 1]);
    }

    [Fact]
    public void Insert_OutOfRange_Throws()
    {
        var list = NewList(out _, out _);

        Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(-1, Entry("X")));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(7, Entry("X")));
    }

    [Fact]
    public void RemoveAt_LooseAndGroupedUpdateBackingLists()
    {
        var list = NewList(out var loose, out var groups);

        list.RemoveAt(0); // A from loose
        list.RemoveAt(2); // D from g1

        Assert.Single(loose);
        Assert.Equal(new[] { "C", "E" }, groups[0].Select(m => m.Mod.Name));
        Assert.Equal(new[] { "B", "C", "E", "F" }, list.Select(m => m.Mod.Name));
    }

    [Fact]
    public void RemoveAt_OutOfRange_Throws()
    {
        var list = NewList(out _, out _);

        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(-1));
    }

    [Fact]
    public void Remove_IndexOf_Contains()
    {
        var list = NewList(out var loose, out var groups);

        Assert.Equal(0, list.IndexOf(loose[0]));
        Assert.Equal(4, list.IndexOf(groups[0][2]));
        Assert.Equal(5, list.IndexOf(groups[2][0]));
        Assert.Equal(-1, list.IndexOf(Entry("X")));
        Assert.True(list.Contains(groups[0][1]));
        Assert.True(list.Contains(groups[0][1].Mod));
        Assert.False(list.Contains(Entry("X")));

        Assert.True(list.Remove(loose[0]));
        Assert.True(list.Remove(groups[2][0]));
        Assert.False(list.Remove(Entry("X")));
        Assert.Equal(new[] { "B", "C", "D", "E" }, list.Select(m => m.Mod.Name));
    }

    [Fact]
    public void Enumeration_MatchesFlattenedOrder_AndIsRepeatable()
    {
        var list = NewList(out _, out _);

        var first = list.Select(m => m.Mod.Name).ToArray();
        var second = list.Select(m => m.Mod.Name).ToArray();

        Assert.Equal(new[] { "A", "B", "C", "D", "E", "F" }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void CopyTo_CopiesFlattenedOrder()
    {
        var list = NewList(out _, out _);
        var array = new IModListEntry[list.Count];

        list.CopyTo(array, 0);

        Assert.Equal(list.Select(m => m.Mod.Name), array.Select(m => m.Mod.Name));
    }

    [Fact]
    public void Remove_GroupedMod_RemovesFromSetAndGroup()
    {
        var list = NewList(out _, out var groups);
        var groupedMod = groups[2][0];

        Assert.Contains(groupedMod, list);
        Assert.True(list.Remove(groupedMod));
        Assert.DoesNotContain(groupedMod, list);
        Assert.Empty(groups[2]);
    }

    [Fact]
    public void Add_Duplicate_StaysSingle()
    {
        var list = NewList(out var loose, out _);
        var entry = loose[0];

        list.Add(entry);

        Assert.Equal(1, list.Count(m => ReferenceEquals(m, entry)));
    }

    [Fact]
    public void Add_SameModDifferentEntry_StaysSingle()
    {
        var list = NewList(out var loose, out _);
        var entry = loose[0];

        list.Add(new ModListEntry(entry.Mod, false));

        Assert.Equal(6, list.Count);
    }

    [Fact]
    public void Remove_ThenReAdd_Works()
    {
        var list = NewList(out var loose, out _);
        var entry = loose[0];

        Assert.True(list.Remove(entry));
        list.Add(entry);

        Assert.True(list.Contains(entry));
    }

    [Fact]
    public void EmptyList_Works()
    {
        var list = new ModList([], []);

        Assert.Equal(0, list.Count);
        Assert.Empty(list);

        var x = Entry("X");
        list.Insert(0, x);
        Assert.Same(x, list[0]);
        list.RemoveAt(0);
        Assert.Empty(list);
    }
}
