using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Downloads;

namespace Ariadne.ModManager;

public static class DownloadInstallInfo
{
    public static string SuggestName(FileInfo archive, DownloadManifest? manifest)
    {
        return manifest?.ModFileName is { Length: > 0 } modFileName
            ? modFileName
            : Path.GetFileNameWithoutExtension(manifest?.FileName ?? archive.Name);
    }

    public static ModID? Provenance(DownloadManifest? manifest)
    {
        if (manifest?.ModId is not { } modId)
        {
            return null;
        }

        var source = manifest.Repository switch
        {
            ProtocolSchemes.Nxm => SourceType.NexusMods,
            ProtocolSchemes.Modl => SourceType.ModPub,
            _ => SourceType.Local,
        };
        return new ModID((ulong)modId, source);
    }
}
