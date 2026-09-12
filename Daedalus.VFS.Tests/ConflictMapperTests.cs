using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Daedalus.VFS.Tests;

public class ConflictMapperTests
{
    private readonly ITestOutputHelper _output;

    public ConflictMapperTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static VirtualNode<string> Tree(params string[] paths)
    {
        var root = new VirtualNode<string>("", NodeFlags.Directory, null, null);
        foreach (var path in paths)
        {
            root.AddFile(path, path);
        }
        return root;
    }

    private static string Signature(ConflictMapper<string>.Conflict conflict)
    {
        var path = conflict.Providers[0].Node.GetPath();
        var indices = string.Join(",", conflict.Providers.Select(p => p.Index));
        return $"{conflict.ConflictType}|{conflict.WinnerIndex}|{path}|[{indices}]";
    }

    private void Dump(string label, IReadOnlyList<ConflictMapper<string>.Conflict> conflicts)
    {
        _output.WriteLine($"{label}: {conflicts.Count} conflict(s)");
        foreach (var conflict in conflicts)
        {
            _output.WriteLine("  " + Signature(conflict));
        }
    }

    [Fact]
    public void Null_Throws_Immediately_Without_Enumeration()
    {
        Assert.Throws<ArgumentNullException>(() => ConflictMapper<string>.MapConflicts(null!));
        _output.WriteLine("exception thrown at call site, before any enumeration");
    }

    [Fact]
    public void Fewer_Than_Two_Trees_Yields_Nothing()
    {
        Assert.Empty(ConflictMapper<string>.MapConflicts(new List<VirtualNode<string>>()));
        Assert.Empty(ConflictMapper<string>.MapConflicts(new[] { Tree("a.txt") }));
        _output.WriteLine("empty and single-tree inputs yield zero conflicts");
    }

    [Fact]
    public void Shared_File_Reports_Single_Overwritten_With_Higher_Index_Winning()
    {
        var a = Tree("textures\\sword.dds", "readme.txt");
        var b = Tree("textures\\sword.dds", "changelog.md");

        var conflicts = ConflictMapper<string>.MapConflicts(new[] { a, b }).ToList();
        Dump("two trees, one shared file", conflicts);

        var conflict = Assert.Single(conflicts);
        Assert.Equal(ConflictMapper<string>.ConflictType.Overwritten, conflict.ConflictType);
        Assert.Equal(1, conflict.WinnerIndex);
        Assert.Equal(new[] { 0, 1 }, conflict.Providers.Select(p => p.Index));
        Assert.All(conflict.Providers, p => Assert.Equal("textures\\sword.dds", p.Node.GetPath()));
    }

    [Fact]
    public void Three_Trees_One_Path_Providers_Ascending_Winner_Is_Last()
    {
        var a = Tree("meshes\\sword.nif");
        var b = Tree("meshes\\sword.nif");
        var c = Tree("meshes\\sword.nif");

        var conflicts = ConflictMapper<string>.MapConflicts(new[] { a, b, c }).ToList();
        Dump("three trees, one path", conflicts);

        var conflict = Assert.Single(conflicts);
        Assert.Equal(new[] { 0, 1, 2 }, conflict.Providers.Select(p => p.Index));
        Assert.Equal(2, conflict.WinnerIndex);
    }

    [Fact]
    public void Middle_Tree_Absent_From_Shared_Directory_Keeps_Original_Indices()
    {
        var a = Tree("textures\\sword.dds");
        var b = Tree("elsewhere\\thing.txt");
        var c = Tree("textures\\sword.dds");

        var conflicts = ConflictMapper<string>.MapConflicts(new[] { a, b, c }).ToList();
        Dump("A and C share, B absent", conflicts);

        var conflict = Assert.Single(conflicts);
        Assert.Equal(new[] { 0, 2 }, conflict.Providers.Select(p => p.Index));
        Assert.Equal(2, conflict.WinnerIndex);
        Assert.Equal("textures\\sword.dds", conflict.Providers[0].Node.GetPath());
    }

    [Fact]
    public void Unique_Files_And_NonOverlapping_Shared_Dirs_Produce_Nothing()
    {
        var a = Tree("textures\\a.dds", "meshes\\a.nif");
        var b = Tree("textures\\b.dds", "meshes\\b.nif");

        var conflicts = ConflictMapper<string>.MapConflicts(new[] { a, b }).ToList();
        Dump("shared dirs, disjoint contents", conflicts);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void File_Versus_Directory_Reports_FileFolderType()
    {
        var a = Tree("meshes\\sword.nif");
        var b = new VirtualNode<string>("", NodeFlags.Directory, null, null);
        b.AddFile("meshes\\sword.nif\\embedded.txt", "embedded");

        var conflicts = ConflictMapper<string>.MapConflicts(new[] { a, b }).ToList();
        Dump("file vs directory", conflicts);

        var conflict = Assert.Single(conflicts);
        Assert.Equal(ConflictMapper<string>.ConflictType.FileFolderType, conflict.ConflictType);
        Assert.Equal(1, conflict.WinnerIndex);
        Assert.Equal(2, conflict.Providers.Count);
    }

    [Fact]
    public void Names_Match_Case_Insensitively()
    {
        var a = Tree("MESHES\\Armor.NIF");
        var b = Tree("meshes\\armor.nif");

        var conflicts = ConflictMapper<string>.MapConflicts(new[] { a, b }).ToList();
        Dump("casing differs across trees", conflicts);

        var conflict = Assert.Single(conflicts);
        Assert.Equal("MESHES\\Armor.NIF", conflict.Providers[0].Node.GetPath());
        Assert.Equal("meshes\\armor.nif", conflict.Providers[1].Node.GetPath());
    }

    [Fact]
    public void Multiple_Conflicts_Across_Nested_Subtrees()
    {
        var a = Tree("meshes\\a.nif", "meshes\\weapons\\w.nif", "readme.txt");
        var b = Tree("meshes\\a.nif", "meshes\\weapons\\w.nif", "docs\\readme.md");

        var conflicts = ConflictMapper<string>.MapConflicts(new[] { a, b }).ToList();
        Dump("nested multi-conflict", conflicts);

        var paths = conflicts.Select(c => c.Providers[0].Node.GetPath()).OrderBy(p => p).ToList();
        Assert.Equal(new[] { "meshes\\a.nif", "meshes\\weapons\\w.nif" }, paths);
        Assert.All(
            conflicts,
            c => Assert.Equal(ConflictMapper<string>.ConflictType.Overwritten, c.ConflictType)
        );
    }

    [Fact]
    public void Input_Trees_Are_Not_Mutated()
    {
        var a = Tree("textures\\sword.dds", "unique_a.txt");
        var b = Tree("textures\\sword.dds", "unique_b.txt");
        int countA = a.CountRecursive;
        int countB = b.CountRecursive;

        var conflicts = ConflictMapper<string>.MapConflicts(new[] { a, b }).ToList();
        Dump("immutability check", conflicts);

        Assert.Single(conflicts);
        Assert.Equal(countA, a.CountRecursive);
        Assert.Equal(countB, b.CountRecursive);
        Assert.Equal("textures\\sword.dds", a.FindNode("textures\\sword.dds")?.Data);
        Assert.Equal("textures\\sword.dds", b.FindNode("textures\\sword.dds")?.Data);
    }

    [Fact]
    public void Stream_Is_ReEnumerable_With_Identical_Results()
    {
        var a = Tree("x\\one.txt", "x\\two.txt", "only_a.txt");
        var b = Tree("x\\one.txt", "x\\two.txt", "only_b.txt");

        var stream = ConflictMapper<string>.MapConflicts(new[] { a, b });
        var first = stream.Select(Signature).ToList();
        var second = stream.Select(Signature).ToList();

        Assert.Equal(first, second);
        foreach (var line in first)
        {
            _output.WriteLine(line);
        }
    }

    [Fact]
    public void Stress_Realistic_Profile_Of_1500_Mod_Folders()
    {
        var (mods, expected, classSizes, totalFiles, buildMs) = BuildProfile(1500, 3000, 42);
        var scanTimer = Stopwatch.StartNew();
        var conflicts = ConflictMapper<string>.MapConflicts(mods).ToList();
        scanTimer.Stop();

        Assert.Equal(expected.Count, conflicts.Count);
        foreach (var conflict in conflicts)
        {
            string path = conflict.Providers[0].Node.GetPath();
            Assert.True(expected.TryGetValue(path, out var who), $"unexpected conflict at {path}");
            Assert.Equal(who, conflict.Providers.Select(p => p.Index).ToArray());
            Assert.Equal(who[^1], conflict.WinnerIndex);
        }

        var histogram = conflicts
            .GroupBy(c =>
                c.Providers.Count switch
                {
                    2 => "2 mods",
                    < 6 => "3-5 mods",
                    < 11 => "6-10 mods",
                    < 26 => "11-25 mods",
                    _ => "26+ mods",
                }
            )
            .OrderBy(g => g.Key);

        _output.WriteLine($"mods:                    {mods.Count}");
        _output.WriteLine($"planted shared paths:    {expected.Count}");
        _output.WriteLine($"total files:             {totalFiles}");
        _output.WriteLine($"  small mods (5-20):     {classSizes[0]} files");
        _output.WriteLine($"  medium (50-300):       {classSizes[1]} files");
        _output.WriteLine($"  large (500-2000):      {classSizes[2]} files");
        _output.WriteLine($"  huge (3000-8000):      {classSizes[3]} files");
        _output.WriteLine($"conflicts found:         {conflicts.Count}");
        _output.WriteLine("provider-count histogram:");
        foreach (var bucket in histogram)
        {
            _output.WriteLine($"  {bucket.Key, -10} {bucket.Count()} conflicts");
        }
        _output.WriteLine("most crowded paths:");
        foreach (var conflict in conflicts.OrderByDescending(c => c.Providers.Count).Take(5))
        {
            _output.WriteLine(
                $"  {conflict.Providers.Count} mods claim {conflict.Providers[0].Node.GetPath()} (winner=mod{conflict.WinnerIndex})"
            );
        }
        _output.WriteLine($"tree build:              {buildMs} ms");
        _output.WriteLine($"conflict scan:           {scanTimer.ElapsedMilliseconds} ms");
    }

    private static (
        List<VirtualNode<string>> Mods,
        Dictionary<string, int[]> Expected,
        int[] ClassSizes,
        long TotalFiles,
        long BuildMs
    ) BuildProfile(int modCount, int poolCount, int seed)
    {
        var rng = new Random(seed);
        var expected = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        var perMod = new List<string>[modCount];
        for (int m = 0; m < modCount; m++)
        {
            perMod[m] = new List<string>();
        }

        for (int i = 0; i < poolCount; i++)
        {
            string path = CrowdedPath(rng, i);
            int participants = Math.Min(2 + (int)(60 * Math.Pow(rng.NextDouble(), 3)), modCount);
            var members = new HashSet<int>();
            while (members.Count < participants)
            {
                members.Add(rng.Next(modCount));
            }
            foreach (var m in members)
            {
                perMod[m].Add(path);
            }
            expected[path] = members.OrderBy(m => m).ToArray();
        }

        int[] classSizes = new int[4];
        for (int m = 0; m < modCount; m++)
        {
            double roll = rng.NextDouble();
            int sizeClass =
                roll < 0.60 ? 0
                : roll < 0.85 ? 1
                : roll < 0.97 ? 2
                : 3;
            int uniqueCount = sizeClass switch
            {
                0 => 5 + rng.Next(16),
                1 => 50 + rng.Next(251),
                2 => 500 + rng.Next(1501),
                _ => 3000 + rng.Next(5001),
            };
            classSizes[sizeClass] += uniqueCount;
            for (int u = 0; u < uniqueCount; u++)
            {
                perMod[m].Add($"solo_{m:D4}\\group_{u % 9}\\file_{u}.txt");
            }
        }

        var buildTimer = Stopwatch.StartNew();
        var mods = perMod
            .Select(paths =>
            {
                var root = new VirtualNode<string>("", NodeFlags.Directory, null, null);
                foreach (var path in paths)
                {
                    root.AddFile(path, path);
                }
                return root;
            })
            .ToList();
        buildTimer.Stop();

        long totalFiles = perMod.Sum(p => (long)p.Count);
        return (mods, expected, classSizes, totalFiles, buildTimer.ElapsedMilliseconds);
    }

    private static string CrowdedPath(Random rng, int i)
    {
        string[] dirs =
        {
            "textures",
            "meshes",
            "sound",
            "scripts",
            "interface",
            "textures\\actors\\body",
            "meshes\\weapons",
            "textures\\architecture",
            "meshes\\armor\\iron",
            "sound\\fx\\ui",
        };
        string[] exts = { "dds", "nif", "pex", "swf", "wav" };
        return $"{dirs[rng.Next(dirs.Length)]}\\shared_{i:D5}.{exts[rng.Next(exts.Length)]}";
    }

    [Fact]
    public void Stress_Deep_Path_Overlap_Traverses_Aligned_Subsets()
    {
        const int depth = 40;
        const int modsWithPath = 50;

        var mods = new List<VirtualNode<string>>(modsWithPath + 1);
        for (int m = 0; m < modsWithPath; m++)
        {
            var segments = Enumerable.Range(0, depth).Select(d => $"d{d}");
            mods.Add(Tree(string.Join("\\", segments.Append("leaf.txt"))));
        }
        mods.Add(Tree("unrelated\\file.txt"));

        var timer = Stopwatch.StartNew();
        var conflicts = ConflictMapper<string>.MapConflicts(mods).ToList();
        timer.Stop();

        var conflict = Assert.Single(conflicts);
        Assert.Equal(modsWithPath, conflict.Providers.Count);
        Assert.Equal(modsWithPath - 1, conflict.WinnerIndex);

        var expectedPath =
            string.Join("\\", Enumerable.Range(0, depth).Select(d => $"d{d}")) + "\\leaf.txt";
        Assert.Equal(expectedPath, conflict.Providers[0].Node.GetPath());

        _output.WriteLine($"depth:                {depth} directory levels");
        _output.WriteLine($"mods sharing path:    {modsWithPath} (+1 unrelated)");
        _output.WriteLine($"conflicts found:      {conflicts.Count}");
        _output.WriteLine($"scan:                 {timer.ElapsedMilliseconds} ms");
        _output.WriteLine($"path: {conflict.Providers[0].Node.GetPath()}");
    }

    [Fact]
    public void Focused_Out_Of_Range_Throws_Immediately()
    {
        var trees = new[] { Tree("a.txt"), Tree("a.txt") };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ConflictMapper<string>.MapConflictsFor(trees, -1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ConflictMapper<string>.MapConflictsFor(trees, 2)
        );
        _output.WriteLine("both out-of-range indices threw at call site, before enumeration");
    }

    [Fact]
    public void Focused_Returns_Only_Conflicts_Involving_The_Focus_Mod()
    {
        var a = Tree("shared\\x.txt", "ac_only\\both.txt");
        var b = Tree("shared\\x.txt");
        var c = Tree("shared\\x.txt", "ac_only\\both.txt");

        var full = ConflictMapper<string>.MapConflicts(new[] { a, b, c }).ToList();
        var focused = ConflictMapper<string>.MapConflictsFor(new[] { a, b, c }, 1).ToList();
        Dump("full scan", full);
        Dump("focused on mod1", focused);

        Assert.Equal(2, full.Count);
        var conflict = Assert.Single(focused);
        Assert.Equal("shared\\x.txt", conflict.Providers[0].Node.GetPath());
        Assert.Equal(new[] { 0, 1, 2 }, conflict.Providers.Select(p => p.Index));
    }

    [Fact]
    public void Focused_Prunes_Subtrees_The_Focus_Mod_Does_Not_Touch()
    {
        var focus = Tree("textures\\weapons\\mine.nif");
        var otherA = Tree("textures\\armor\\plate.nif");
        var otherB = Tree("textures\\weapons\\mine.nif", "textures\\armor\\plate.nif");

        var conflicts = ConflictMapper<string>
            .MapConflictsFor(new[] { focus, otherA, otherB }, 0)
            .ToList();
        Dump("focus=mod0, noise parked one dir away", conflicts);

        var conflict = Assert.Single(conflicts);
        Assert.Equal("textures\\weapons\\mine.nif", conflict.Providers[0].Node.GetPath());
        Assert.Equal(new[] { 0, 2 }, conflict.Providers.Select(p => p.Index));
    }

    [Fact]
    public void Focused_On_Mod_With_No_Conflicts_Yields_Nothing()
    {
        var a = Tree("alpha\\one.txt");
        var b = Tree("beta\\two.txt");

        var conflicts = ConflictMapper<string>.MapConflictsFor(new[] { a, b }, 1).ToList();
        Dump("focus on isolated mod", conflicts);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void Focused_Equals_Full_Scan_Filtered_To_Focus()
    {
        var (mods, expected, _, totalFiles, _) = BuildProfile(200, 600, 7);
        var full = ConflictMapper<string>.MapConflicts(mods).ToList();

        int hotMod = full.SelectMany(c => c.Providers)
            .GroupBy(p => p.Index)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;
        int coldMod = Enumerable
            .Range(0, mods.Count)
            .FirstOrDefault(i => full.All(c => c.Providers.All(p => p.Index != i)), -1);

        foreach (int focus in coldMod >= 0 ? new[] { hotMod, coldMod } : new[] { hotMod })
        {
            var focused = ConflictMapper<string>
                .MapConflictsFor(mods, focus)
                .Select(Signature)
                .OrderBy(s => s)
                .ToList();
            var filtered = full.Where(c => c.Providers.Any(p => p.Index == focus))
                .Select(Signature)
                .OrderBy(s => s)
                .ToList();
            Assert.Equal(filtered, focused);
            _output.WriteLine(
                $"focus=mod{focus}: focused stream {focused.Count} == filtered full {filtered.Count}"
            );
        }
        _output.WriteLine(
            $"profile: {mods.Count} mods, {totalFiles} files, {full.Count} conflicts"
        );
        _output.WriteLine($"hot mod #{hotMod} participates in the most conflicts");
        _output.WriteLine(
            coldMod >= 0
                ? $"cold mod #{coldMod} participates in none"
                : "no fully cold mod exists in this profile"
        );
    }

    [Fact]
    public void Stress_Focused_Scan_Cost_Tracks_Focus_Footprint()
    {
        var (mods, expected, _, totalFiles, _) = BuildProfile(1500, 3000, 42);

        int[] participation = new int[mods.Count];
        foreach (var members in expected.Values)
        {
            foreach (int m in members)
            {
                participation[m]++;
            }
        }
        int hotMod = participation
            .Select((count, index) => (count, index))
            .OrderByDescending(x => x.count)
            .First()
            .index;
        int coldMod = participation
            .Select((count, index) => (count, index))
            .OrderBy(x => x.count)
            .First()
            .index;

        var fullTimer = Stopwatch.StartNew();
        var full = ConflictMapper<string>.MapConflicts(mods).ToList();
        fullTimer.Stop();

        var hotTimer = Stopwatch.StartNew();
        var hotFocused = ConflictMapper<string>.MapConflictsFor(mods, hotMod).ToList();
        hotTimer.Stop();

        var coldTimer = Stopwatch.StartNew();
        var coldFocused = ConflictMapper<string>.MapConflictsFor(mods, coldMod).ToList();
        coldTimer.Stop();

        Assert.Equal(
            full.Where(c => c.Providers.Any(p => p.Index == hotMod))
                .Select(Signature)
                .OrderBy(s => s),
            hotFocused.Select(Signature).OrderBy(s => s)
        );
        Assert.Equal(participation[hotMod], hotFocused.Count);
        Assert.Equal(participation[coldMod], coldFocused.Count);

        _output.WriteLine($"profile:            {mods.Count} mods, {totalFiles} files");
        _output.WriteLine(
            $"full scan:          {fullTimer.ElapsedMilliseconds} ms -> {full.Count} conflicts"
        );
        _output.WriteLine(
            $"focused hot mod{hotMod}:  {hotTimer.ElapsedMilliseconds} ms -> {hotFocused.Count} conflicts"
        );
        _output.WriteLine(
            $"focused cold mod{coldMod}: {coldTimer.ElapsedMilliseconds} ms -> {coldFocused.Count} conflicts"
        );
    }
}
