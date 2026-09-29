using Ariadne.Downloads;
using Xunit;

namespace Ariadne.Downloads.Tests;

public class DownloadManifestStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = new(
        Path.Combine(Path.GetTempPath(), $"Ariadne-manifest-tests-{Guid.NewGuid():N}")
    );

    public void Dispose()
    {
        _directory.Refresh();
        if (_directory.Exists)
        {
            _directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Write_ThenTryRead_RoundTripsManifest()
    {
        var downloaded = new FileInfo(Path.Combine(_directory.FullName, "SkyUI.7z"));
        var manifest = new DownloadManifest
        {
            Repository = "nxm",
            Game = "skyrimspecialedition",
            ModId = 12604,
            FileId = 35407,
            ModName = "SkyUI_5_2_SE",
            Version = "5.2SE",
            FileName = "SkyUI_5_2_SE-12604-5-2SE.7z",
            SizeInBytes = 2783417,
            SourceLink =
                "nxm://skyrimspecialedition/mods/12604/files/35407?key=abc&expires=1&user_id=1",
            ResolvedUrl = "https://cdn.nexusmods.com/skyui.7z",
            DownloadedUtc = new DateTimeOffset(2026, 9, 24, 12, 30, 0, TimeSpan.Zero),
        };

        DownloadManifestStore.Write(downloaded, manifest);

        var path = DownloadManifestStore.GetManifestPath(downloaded);
        Assert.Equal("SkyUI.7z.manifest.json", path.Name);
        Assert.True(File.Exists(path.FullName));

        var read = DownloadManifestStore.TryRead(downloaded);
        Assert.Equal(manifest, read);
    }

    [Fact]
    public void TryRead_Missing_ReturnsNull()
    {
        var downloaded = new FileInfo(Path.Combine(_directory.FullName, "ghost.7z"));

        Assert.Null(DownloadManifestStore.TryRead(downloaded));
    }

    [Fact]
    public void TryRead_Corrupted_ReturnsNull()
    {
        var downloaded = new FileInfo(Path.Combine(_directory.FullName, "corrupt.7z"));
        var path = DownloadManifestStore.GetManifestPath(downloaded);
        path.Directory!.Create();
        File.WriteAllText(path.FullName, "{ not json");

        Assert.Null(DownloadManifestStore.TryRead(downloaded));
    }
}
