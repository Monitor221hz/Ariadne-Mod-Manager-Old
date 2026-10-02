using Ariadne.Contracts.ModManager;
using Ariadne.VFS;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class ConflictAnalysisServiceTests
{
    private sealed class StubMod(string name) : ILibraryMod
    {
        public IModInfo Info { get; } = new ModInfo(0, SourceType.Local, "1.0", [], "");
        public string Name => name;
        public DirectoryInfo Directory => new(".");
        public VirtualNode<ModFileEntry> Content { get; } =
            new(name, NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => obj.GetHashCode();

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);

        public void AddFile(string path)
        {
            Content.AddFile(
                path,
                new ModFileEntry(name, ModEntryKind.File, Info, path, 1, DateTimeOffset.UnixEpoch)
            );
        }
    }

    private static ModProfile ProfileWith(params (StubMod Mod, bool Active)[] mods) =>
        new(
            "P",
            new ModList(
                mods.Select(m => (IModListEntry)new ModListEntry(m.Mod, m.Active)).ToList(),
                []
            ),
            new Version(1, 0),
            new DirectoryInfo(".")
        );

    private readonly ConflictAnalysisService _service = new();

    [Fact]
    public void EarlierMod_LosesToSelected_LaterMod_BeatsSelected()
    {
        var before = new StubMod("Before");
        var selected = new StubMod("Selected");
        var after = new StubMod("After");
        foreach (var mod in new[] { before, selected, after })
        {
            mod.AddFile("shared.txt");
        }
        var profile = ProfileWith((before, true), (selected, true), (after, true));

        var verdicts = _service.ComputeVerdicts(profile, selected);

        Assert.Equal(SelectedModVerdict.LosesToSelected, verdicts[before]);
        Assert.Equal(SelectedModVerdict.BeatsSelected, verdicts[after]);
        Assert.False(verdicts.ContainsKey(selected));
    }

    [Fact]
    public void InactiveSelectedMod_YieldsNoVerdicts()
    {
        var selected = new StubMod("Selected");
        selected.AddFile("shared.txt");
        var other = new StubMod("Other");
        other.AddFile("shared.txt");
        var profile = ProfileWith((selected, false), (other, true));

        Assert.Empty(_service.ComputeVerdicts(profile, selected));
    }

    [Fact]
    public void SingleActiveMod_YieldsNoVerdicts()
    {
        var selected = new StubMod("Selected");
        selected.AddFile("shared.txt");
        var profile = ProfileWith((selected, true));

        Assert.Empty(_service.ComputeVerdicts(profile, selected));
    }

    [Fact]
    public void ModsWithoutConflicts_YieldNoVerdicts()
    {
        var selected = new StubMod("Selected");
        selected.AddFile("a.txt");
        var other = new StubMod("Other");
        other.AddFile("b.txt");
        var profile = ProfileWith((selected, true), (other, true));

        Assert.Empty(_service.ComputeVerdicts(profile, selected));
    }
}
