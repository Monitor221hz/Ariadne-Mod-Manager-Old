using System.Globalization;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager;
using Ariadne.ModManager.GUI.Converters;
using Ariadne.ModManager.GUI.ViewModels;
using FluentIcons.Common;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public class FileIconConverterTests
{
    private sealed class FakeMod : ILibraryMod
    {
        public IModInfo Info { get; } = new ModInfo(0, SourceType.Local, "1.0", [], "");
        public string Name => "M";
        public DirectoryInfo Directory => new(".");
        public Ariadne.VFS.VirtualNode<ModFileEntry> Content { get; } =
            new("", Ariadne.VFS.NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void ReplaceInfo(IModInfo info) { }

        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => obj.GetHashCode();

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    private static readonly IModInfo Origin = new ModInfo(0, SourceType.Local, "1.0", [], "");

    private static readonly ModEntryNodeViewModel Owner = new(
        new ModListEntry(new FakeMod(), true)
    );

    private static FileLeafNodeViewModel FileNode(string name)
    {
        var entry = new ModFileEntry(
            name,
            ModEntryKind.File,
            Origin,
            $"/tmp/{name}",
            1,
            DateTimeOffset.Now
        );
        return new FileLeafNodeViewModel(
            new Ariadne.VFS.VirtualNode<ModFileEntry>(
                name,
                Ariadne.VFS.NodeFlags.None,
                null,
                entry
            ),
            Owner
        );
    }

    private static Icon IconFor(FileLeafNodeViewModel node) =>
        (Icon)
            new FileIconConverter().Convert(
                node,
                typeof(Icon),
                null,
                CultureInfo.InvariantCulture
            )!;

    [Theory]
    [InlineData("settings.json", Icon.DocumentText)]
    [InlineData("config.toml", Icon.DocumentText)]
    [InlineData("skse.toml", Icon.DocumentText)]
    [InlineData("plugin.yaml", Icon.DocumentText)]
    [InlineData("mod.ini", Icon.DocumentText)]
    [InlineData("readme.md", Icon.DocumentText)]
    [InlineData("notes.txt", Icon.DocumentText)]
    [InlineData("texture.dds", Icon.Image)]
    [InlineData("screenshot.png", Icon.Image)]
    [InlineData("mesh.nif", Icon.Cube)]
    [InlineData("plugin.esp", Icon.PlugConnected)]
    [InlineData("wine.wav", Icon.MusicNote1)]
    [InlineData("script.pex", Icon.Code)]
    [InlineData("unknown.xyz", Icon.Document)]
    public void Extension_Map_(string name, Icon expected) =>
        Assert.Equal(expected, IconFor(FileNode(name)));

    [Fact]
    public void Archive_Inner_File_Uses_Inner_Name_Extension()
    {
        var entry = new ModFileEntry(
            "meshes/sword.nif",
            ModEntryKind.File,
            Origin,
            "/tmp/pack.zip",
            1,
            DateTimeOffset.Now
        );
        var node = new FileLeafNodeViewModel(
            new Ariadne.VFS.VirtualNode<ModFileEntry>(
                "meshes/sword.nif",
                Ariadne.VFS.NodeFlags.None,
                null,
                entry
            ),
            Owner
        );

        Assert.Equal(Icon.Cube, IconFor(node));
    }
}
