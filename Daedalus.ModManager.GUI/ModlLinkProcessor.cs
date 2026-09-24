using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.Downloads;
using Daedalus.WebProtocol;
using Daedalus.WebProtocol.Modl;

namespace Daedalus.ModManager.GUI;

public sealed class ModlLinkProcessor : IDisposable
{
    private readonly WebLinkBuffer _buffer;
    private readonly IGameCatalog _catalog;
    private readonly IInstanceService _instances;
    private readonly IModManagerPaths _paths;
    private readonly IDownloadQueue _downloads;
    private readonly ConcurrentDictionary<Guid, ModlLink> _pending = new();

    public event EventHandler<string>? Notification;

    public ModlLinkProcessor(
        WebLinkBuffer buffer,
        IGameCatalog catalog,
        IInstanceService instances,
        IModManagerPaths paths,
        IDownloadQueue downloads
    )
    {
        _buffer = buffer;
        _catalog = catalog;
        _instances = instances;
        _paths = paths;
        _downloads = downloads;

        _buffer.LinkEnqueued += OnLinkEnqueued;
        _downloads.JobCompleted += OnDownloadCompleted;
    }

    public void Dispose()
    {
        _buffer.LinkEnqueued -= OnLinkEnqueued;
        _downloads.JobCompleted -= OnDownloadCompleted;
    }

    private void OnLinkEnqueued(object? sender, ISchemeLink link)
    {
        if (link is not ModlLink modlLink)
        {
            return;
        }
        _ = ProcessGuardedAsync(modlLink);
    }

    private async Task ProcessGuardedAsync(ModlLink link)
    {
        try
        {
            await ProcessAsync(link);
        }
        catch (Exception exception)
        {
            Notify($"Download for {link} failed to start: {exception.Message}");
        }
    }

    private Task ProcessAsync(ModlLink link)
    {
        var game = FindGameForModlId(link.GameId);
        if (game is null)
        {
            Notify($"Skipped {link.DisplayName()}: no game handles id \"{link.GameId}\".");
            return Task.CompletedTask;
        }

        var currentName = _instances.Current?.Game?.Configuration.Name;
        if (
            currentName is null
            || !string.Equals(currentName, game.Name, StringComparison.OrdinalIgnoreCase)
        )
        {
            Notify(
                $"Skipped {link.DisplayName()}: it targets {game.Name}, but the active instance is not attached to it."
            );
            return Task.CompletedTask;
        }

        var fileName = DeriveFileName(link);
        var destination = new FileInfo(Path.Join(_paths.DownloadsFolder.FullName, fileName));

        var jobId = _downloads.Enqueue(new DownloadRequest(link.DownloadUri, destination));
        _pending[jobId] = link;
        return Task.CompletedTask;
    }

    private void OnDownloadCompleted(object? sender, DownloadCompletion completion)
    {
        _pending.TryRemove(completion.Job.Id, out var link);
        if (link is null || completion.Result.State is not DownloadState.Completed)
        {
            return;
        }

        var file = completion.Result.File;
        if (file is null)
        {
            return;
        }

        try
        {
            DownloadManifestStore.Write(
                file,
                new DownloadManifest
                {
                    Repository = link.Scheme,
                    Game = link.GameId,
                    FileName = file.Name,
                    SourceLink = link.ToString(),
                    ResolvedUrl = link.DownloadUri.AbsoluteUri,
                    DownloadedUtc = completion.Job.FinishedUtc ?? DateTimeOffset.UtcNow,
                }
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Notify($"Downloaded archive saved, but writing its metadata failed: {file.FullName}");
        }
    }

    private ISupportedGame? FindGameForModlId(string gameId)
    {
        return _catalog.Games.FirstOrDefault(game =>
            game.ProtocolGameIds.TryGetValue(ProtocolSchemes.Modl, out var shortName)
            && string.Equals(shortName, gameId, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static string DeriveFileName(ModlLink link)
    {
        var name = Path.GetFileName(link.DownloadUri.LocalPath);
        if (string.IsNullOrEmpty(name) || !Path.HasExtension(name))
        {
            var fingerprint = Convert
                .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(link.DownloadUri.AbsoluteUri)))[
                    ..8
                ]
                .ToLowerInvariant();
            name = $"modl-{link.GameId}-{fingerprint}.bin";
        }
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }
        return name;
    }

    private void Notify(string message)
    {
        try
        {
            Notification?.Invoke(this, message);
        }
        catch (Exception) { }
    }
}

internal static class ModlLinkDisplayFormat
{
    public static string DisplayName(this ModlLink link)
    {
        return $"modl download for {link.GameId}";
    }
}
