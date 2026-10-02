using Ariadne.Contracts.ModManager;
using Ariadne.Downloads;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class DownloadInstallInfoTests
{
    private static readonly FileInfo Archive = new(Path.Combine(Path.GetTempPath(), "archive.7z"));

    [Fact]
    public void SuggestName_PrefersModFileName()
    {
        var manifest = new DownloadManifest
        {
            Repository = "nxm",
            ModFileName = "SkyUI",
            FileName = "SkyUI-12604-6-11-1778020881.zip",
            DownloadedUtc = DateTimeOffset.UtcNow,
        };

        Assert.Equal("SkyUI", DownloadInstallInfo.SuggestName(Archive, manifest));
    }

    [Fact]
    public void SuggestName_FallsBackToManifestFileNameWithoutExtension()
    {
        var manifest = new DownloadManifest
        {
            Repository = "nxm",
            FileName = "SkyUI-12604-6-11-1778020881.zip",
            DownloadedUtc = DateTimeOffset.UtcNow,
        };

        Assert.Equal(
            "SkyUI-12604-6-11-1778020881",
            DownloadInstallInfo.SuggestName(Archive, manifest)
        );
    }

    [Fact]
    public void SuggestName_WithoutManifest_UsesArchiveFileName()
    {
        Assert.Equal("archive", DownloadInstallInfo.SuggestName(Archive, null));
    }

    [Fact]
    public void Provenance_NxmManifest_MapsToNexusMods()
    {
        var manifest = new DownloadManifest
        {
            Repository = "nxm",
            ModId = 12604,
            FileId = 360415,
            DownloadedUtc = DateTimeOffset.UtcNow,
        };

        Assert.Equal(
            new ModID(12604, SourceType.NexusMods),
            DownloadInstallInfo.Provenance(manifest)
        );
    }

    [Fact]
    public void Provenance_ModlManifest_MapsToModPub()
    {
        var manifest = new DownloadManifest
        {
            Repository = "modl",
            ModId = 42,
            DownloadedUtc = DateTimeOffset.UtcNow,
        };

        Assert.Equal(new ModID(42, SourceType.ModPub), DownloadInstallInfo.Provenance(manifest));
    }

    [Fact]
    public void Provenance_WithoutModId_ReturnsNull()
    {
        var manifest = new DownloadManifest
        {
            Repository = "modl",
            ModId = null,
            DownloadedUtc = DateTimeOffset.UtcNow,
        };

        Assert.Null(DownloadInstallInfo.Provenance(manifest));
        Assert.Null(DownloadInstallInfo.Provenance(null));
    }
}
