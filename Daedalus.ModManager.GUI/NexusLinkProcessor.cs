using System.Collections.Concurrent;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.Downloads;
using Daedalus.Security;
using Daedalus.WebProtocol;
using Daedalus.WebProtocol.Nexus;

namespace Daedalus.ModManager.GUI;

public sealed class NexusLinkProcessor : IDisposable
{
    private const string ApiKeySecretName = "nexus-api-key";

    private readonly WebLinkBuffer _buffer;
    private readonly IGameCatalog _catalog;
    private readonly IInstanceService _instances;
    private readonly IModManagerPaths _paths;
    private readonly ISecretStore _secrets;
    private readonly INexusAccountCache _accountCache;
    private readonly INexusFileClient _files;
    private readonly INexusDownloadResolver _resolver;
    private readonly IDownloadQueue _downloads;
    private readonly ConcurrentDictionary<Guid, PendingProvenance> _pendingProvenance = new();

    private sealed record PendingProvenance(
        NxmModLink Link,
        NexusFileMetadata? Metadata,
        NexusDownloadLink Resolved
    );

    public event EventHandler<string>? Notification;

    public NexusLinkProcessor(
        WebLinkBuffer buffer,
        IGameCatalog catalog,
        IInstanceService instances,
        IModManagerPaths paths,
        ISecretStore secrets,
        INexusAccountCache accountCache,
        INexusFileClient files,
        INexusDownloadResolver resolver,
        IDownloadQueue downloads
    )
    {
        _buffer = buffer;
        _catalog = catalog;
        _instances = instances;
        _paths = paths;
        _secrets = secrets;
        _accountCache = accountCache;
        _files = files;
        _resolver = resolver;
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
        if (link is NxmLink)
        {
            _ = ProcessGuardedAsync(link);
        }
    }

    private async Task ProcessGuardedAsync(ISchemeLink link)
    {
        try
        {
            await ProcessAsync((NxmLink)link);
        }
        catch (Exception exception)
        {
            Notify($"Download for {link} failed to start: {exception.Message}");
        }
    }

    private async Task ProcessAsync(NxmLink link)
    {
        if (link is not NxmModLink modLink)
        {
            return;
        }

        var game = FindGameForDomain(modLink.GameDomain);
        if (game is null)
        {
            Notify(
                $"Skipped {modLink.DisplayName()}: no game handles domain \"{modLink.GameDomain}\"."
            );
            return;
        }

        var currentName = _instances.Current?.Game?.Configuration.Name;
        if (
            currentName is null
            || !string.Equals(currentName, game.Name, StringComparison.OrdinalIgnoreCase)
        )
        {
            Notify(
                $"Skipped {modLink.DisplayName()}: it targets {game.Name}, but the active instance is not attached to it."
            );
            return;
        }

        var apiKey = await GetApiKeyAsync();
        if (apiKey is null)
        {
            Notify("Skipped a Nexus Mods download: not signed in.");
            return;
        }

        var account = _accountCache.Read(apiKey);
        if (account is null)
        {
            Notify(
                "Skipped a Nexus Mods download: sign in under Sources → Nexus Mods so your account is known."
            );
            return;
        }

        var resolved = await _resolver.ResolveDownloadAsync(
            modLink,
            apiKey,
            account.IsPremium,
            serverName: null
        );
        if (resolved is null)
        {
            Notify($"Could not resolve a download link for {modLink.DisplayName()}.");
            return;
        }

        var metadata = await _files.GetFileAsync(modLink, apiKey);
        var fileName = DeriveFileName(modLink, resolved.Uri, metadata);
        var destination = new FileInfo(Path.Join(_paths.DownloadsFolder.FullName, fileName));

        var jobId = _downloads.Enqueue(new DownloadRequest(resolved.Uri, destination));
        _pendingProvenance[jobId] = new PendingProvenance(modLink, metadata, resolved);
    }

    private void OnDownloadCompleted(object? sender, DownloadCompletion completion)
    {
        _pendingProvenance.TryRemove(completion.Job.Id, out var pending);
        if (pending is null || completion.Result.State is not DownloadState.Completed)
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
                    Repository = ProtocolSchemes.Nxm,
                    Game = pending.Link.GameDomain,
                    ModId = pending.Link.ModId,
                    FileId = pending.Link.FileId,
                    ModName = pending.Metadata?.Name,
                    Version = pending.Metadata?.Version,
                    FileName = pending.Metadata?.FileName ?? file.Name,
                    SizeInBytes = pending.Metadata?.SizeInBytes,
                    SourceLink = pending.Link.ToString(),
                    ResolvedUrl = pending.Resolved.Uri.AbsoluteUri,
                    DownloadedUtc = completion.Job.FinishedUtc ?? DateTimeOffset.UtcNow,
                }
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Notify($"Downloaded archive saved, but writing its metadata failed: {file.FullName}");
        }
    }

    private ISupportedGame? FindGameForDomain(string domain)
    {
        return _catalog.Games.FirstOrDefault(game =>
            game.ProtocolGameIds.TryGetValue(ProtocolSchemes.Nxm, out var gameDomain)
            && string.Equals(gameDomain, domain, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static string DeriveFileName(
        NxmModLink link,
        Uri resolvedUri,
        NexusFileMetadata? metadata
    )
    {
        var name = metadata?.FileName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileName(resolvedUri.LocalPath);
        }
        if (string.IsNullOrEmpty(name) || !Path.HasExtension(name))
        {
            name = $"nexus-mod-{link.ModId}-file-{link.FileId}.bin";
        }
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }
        return name;
    }

    private async Task<string?> GetApiKeyAsync()
    {
#if DEBUG
        var devKey = Environment.GetEnvironmentVariable(
            ViewModels.SourcesMenuViewModel.DevKeyEnvironmentVariable
        );
        if (!string.IsNullOrWhiteSpace(devKey))
        {
            return devKey;
        }
#endif
        return await _secrets.GetAsync(ApiKeySecretName);
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

internal static class NxmModLinkDisplayFormat
{
    public static string DisplayName(this NxmModLink link)
    {
        return $"mod {link.ModId} file {link.FileId} ({link.GameDomain})";
    }
}
