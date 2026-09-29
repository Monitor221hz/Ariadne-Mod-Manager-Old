using System.Security.Cryptography;
using Ariadne.Downloads;
using Xunit;

namespace Ariadne.Downloads.Tests;

public class DownloadManagerTests : IDisposable
{
    private readonly DirectoryInfo _directory = new(
        Path.Combine(Path.GetTempPath(), $"Ariadne-download-tests-{Guid.NewGuid():N}")
    );

    public void Dispose()
    {
        _directory.Refresh();
        if (_directory.Exists)
        {
            _directory.Delete(recursive: true);
        }
    }

    private static byte[] CreateContent(int size)
    {
        var content = new byte[size];
        for (var i = 0; i < size; i++)
        {
            content[i] = (byte)(i * 31 % 251);
        }
        return content;
    }

    private static string Md5Hex(byte[] content)
    {
        return Convert.ToHexString(MD5.HashData(content));
    }

    private FileInfo Destination(string name = "mod.7z")
    {
        return new FileInfo(Path.Combine(_directory.FullName, name));
    }

    [Fact]
    public async Task DownloadAsync_RoundTrip_CompletesWithMatchingContent()
    {
        var content = CreateContent(256 * 1024);
        using var server = new StubHttpServer(content);
        var destination = Destination();
        var manager = new DownloadManager();

        var result = await manager.DownloadAsync(
            new DownloadRequest(server.BaseUri, destination, Md5Hex(content))
        );

        Assert.Equal(DownloadState.Completed, result.State);
        Assert.True(destination.Exists);
        Assert.Equal(content, await File.ReadAllBytesAsync(destination.FullName));
        Assert.Equal(content.Length, result.ReceivedBytes);
        Assert.Equal(content.Length, result.TotalBytes);
        Assert.Equal(destination.FullName, result.File?.FullName);
    }

    [Fact]
    public async Task DownloadAsync_ProgressEvents_MonoTonicAndMatchingId()
    {
        var content = CreateContent(512 * 1024);
        using var server = new StubHttpServer(content);
        var manager = new DownloadManager();
        var events = new List<DownloadProgress>();
        manager.ProgressChanged += (_, progress) =>
        {
            lock (events)
            {
                events.Add(progress);
            }
        };

        var result = await manager.DownloadAsync(
            new DownloadRequest(server.BaseUri, Destination())
        );

        Assert.Equal(DownloadState.Completed, result.State);
        List<DownloadProgress> snapshot;
        lock (events)
        {
            snapshot = events.ToList();
        }
        Assert.NotEmpty(snapshot);
        Assert.All(snapshot, progress => Assert.Equal(result.Id, progress.Id));
        Assert.All(snapshot, progress => Assert.True(progress.BytesPerSecond >= 0));
        Assert.Equal(content.Length, snapshot[^1].ReceivedBytes);
    }

    [Fact]
    public async Task DownloadAsync_ChecksumMismatch_DeletesFile()
    {
        var content = CreateContent(64 * 1024);
        using var server = new StubHttpServer(content);
        var destination = Destination();
        var manager = new DownloadManager();

        var result = await manager.DownloadAsync(
            new DownloadRequest(server.BaseUri, destination, new string('0', 32))
        );

        Assert.Equal(DownloadState.IntegrityMismatch, result.State);
        Assert.Null(result.File);
        Assert.False(destination.Exists);
        Assert.Contains("checksum", result.Error);
    }

    [Fact]
    public async Task DownloadAsync_CancelledMidTransfer_ReturnsCancelled()
    {
        var content = CreateContent(64 * 1024 * 1024);
        using var server = new StubHttpServer(content) { ChunkDelayMilliseconds = 10 };
        var manager = new DownloadManager();
        using var cancellation = new CancellationTokenSource();
        manager.ProgressChanged += (_, _) => cancellation.Cancel();

        var result = await manager.DownloadAsync(
            new DownloadRequest(server.BaseUri, Destination()),
            cancellation.Token
        );

        Assert.Equal(DownloadState.Cancelled, result.State);
    }

    [Fact]
    public async Task DownloadAsync_NotFound_ReturnsFailed()
    {
        using var server = new StubHttpServer(CreateContent(1024)) { ServeNotFound = true };
        var manager = new DownloadManager();

        var result = await manager.DownloadAsync(
            new DownloadRequest(server.BaseUri, Destination())
        );

        Assert.Equal(DownloadState.Failed, result.State);
    }

    [Fact]
    public async Task DownloadAsync_ExistingDestination_IsReplaced()
    {
        var content = CreateContent(16 * 1024);
        using var server = new StubHttpServer(content);
        var destination = Destination();
        destination.Directory?.Create();
        await File.WriteAllBytesAsync(destination.FullName, new byte[8]);
        var manager = new DownloadManager();

        var result = await manager.DownloadAsync(new DownloadRequest(server.BaseUri, destination));

        Assert.Equal(DownloadState.Completed, result.State);
        Assert.Equal(content.Length, result.ReceivedBytes);
    }

    [Fact]
    public async Task DownloadAsync_ConcurrentDownloads_BothCompleteWithDistinctIds()
    {
        var content = CreateContent(128 * 1024);
        using var server = new StubHttpServer(content);
        var manager = new DownloadManager();

        var first = manager.DownloadAsync(
            new DownloadRequest(server.BaseUri, Destination("first.7z"))
        );
        var second = manager.DownloadAsync(
            new DownloadRequest(server.BaseUri, Destination("second.7z"))
        );

        var results = await Task.WhenAll(first, second);
        Assert.All(results, result => Assert.Equal(DownloadState.Completed, result.State));
        Assert.NotEqual(results[0].Id, results[1].Id);
    }
}
