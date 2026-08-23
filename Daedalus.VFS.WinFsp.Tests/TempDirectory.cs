namespace Daedalus.VFS.WinFsp.Tests;

public sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "DaedalusTests-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(Path);
    }

    public string Combine(params string[] parts) =>
        System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }
        // delete-on-close and SetBasicInfo tests can leave read-only entries behind
        foreach (
            var entry in Directory.EnumerateFileSystemEntries(
                Path,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            File.SetAttributes(entry, FileAttributes.Normal);
        }
        Directory.Delete(Path, true);
    }
}
