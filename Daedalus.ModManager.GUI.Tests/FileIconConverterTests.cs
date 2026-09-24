using System.Globalization;
using Daedalus.Contracts.ModManager;
using Daedalus.ModManager;
using Daedalus.ModManager.GUI.Converters;
using Daedalus.ModManager.GUI.ViewModels;
using FluentIcons.Common;
using Xunit;

namespace Daedalus.ModManager.GUI.Tests;

public class FileIconConverterTests
{
    private static readonly IModInfo Origin = new ModInfo(
        0,
        SourceType.Local,
        "1.0",
        [],
        "",
        0,
        false
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
            new Daedalus.VFS.VirtualNode<ModFileEntry>(
                name,
                Daedalus.VFS.NodeFlags.None,
                null,
                entry
            )
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
            new Daedalus.VFS.VirtualNode<ModFileEntry>(
                "meshes/sword.nif",
                Daedalus.VFS.NodeFlags.None,
                null,
                entry
            )
        );

        Assert.Equal(Icon.Cube, IconFor(node));
    }
}
