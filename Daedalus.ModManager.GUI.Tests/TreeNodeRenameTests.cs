using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager.GUI.ViewModels;
using Xunit;

namespace Daedalus.ModManager.GUI.Tests;

public class TreeNodeRenameTests
{
    private sealed class FakeMod(string name) : ILibraryMod
    {
        public IModInfo Info { get; } =
            new Mods.ModInfo(0, SourceType.Local, "1.0", [], "", 0, false);
        public string Name { get; private set; } = name;
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

        public void RenameTo(string newName) => Name = newName;
    }

    [Fact]
    public async Task Successful_Rename_Emits_RenameCommitted()
    {
        var mod = new FakeMod("Old Name");
        var node = new ModEntryNodeViewModel(mod);
        var committedTask = node
            .RenameCommitted.Timeout(TimeSpan.FromSeconds(5))
            .FirstAsync()
            .ToTask();

        node.StartRenameCommand.Execute(Unit.Default).Subscribe();
        node.DisplayName = "New Name";
        node.FinishRenameCommand.Execute(Unit.Default).Subscribe();

        await committedTask;
        Assert.Equal("New Name", mod.Name);
        Assert.Equal("New Name", node.DisplayName);
    }

    [Fact]
    public async Task Unchanged_Rename_Reverts_Display_And_Does_Not_Emit()
    {
        var mod = new FakeMod("Old Name");
        var node = new ModEntryNodeViewModel(mod);
        var emitted = false;
        using var subscription = node.RenameCommitted.Subscribe(_ => emitted = true);

        node.StartRenameCommand.Execute(Unit.Default).Subscribe();
        node.DisplayName = "Old Name";
        node.FinishRenameCommand.Execute(Unit.Default).Subscribe();
        await Task.Delay(50);

        Assert.False(emitted);
        Assert.Equal("Old Name", node.DisplayName);
        Assert.False(node.IsEditing);
    }

    [Fact]
    public async Task Whitespace_Rename_Reverts_Display_And_Does_Not_Emit()
    {
        var mod = new FakeMod("Old Name");
        var node = new ModEntryNodeViewModel(mod);
        var emitted = false;
        using var subscription = node.RenameCommitted.Subscribe(_ => emitted = true);

        node.StartRenameCommand.Execute(Unit.Default).Subscribe();
        node.DisplayName = "   ";
        node.FinishRenameCommand.Execute(Unit.Default).Subscribe();
        await Task.Delay(50);

        Assert.False(emitted);
        Assert.Equal("Old Name", node.DisplayName);
        Assert.False(node.IsEditing);
    }
}
