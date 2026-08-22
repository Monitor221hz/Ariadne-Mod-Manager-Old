using System.Diagnostics;
using Daedalus.VFS;
using Daedalus.WinFsp;
using Fsp;

string? logPath = Environment.GetEnvironmentVariable("DAEDALUS_LOG");
if (!string.IsNullOrEmpty(logPath))
{
    Trace.Listeners.Add(new TextWriterTraceListener(logPath));
    Trace.AutoFlush = true;
    Trace.WriteLine($"log start {DateTime.Now:o}");
}

if (args.Length < 3)
{
    Console.WriteLine(
        "usage: Daedalus.WinFsp.Demo <overlay|overlay-nocow|passthrough> <source-dir> <mount-point> [overwrite-dir] [process=dir ...]"
    );
    return 1;
}

string mode = args[0].ToLowerInvariant();
string source = Path.GetFullPath(args[1]);
string mountPoint = Path.GetFullPath(args[2]);
string overwrite =
    args.Length > 3
        ? Path.GetFullPath(args[3])
        : Path.Combine(
            Path.GetTempPath(),
            "DaedalusDemo-overwrite-" + Guid.NewGuid().ToString("N")
        );

if (!Directory.Exists(source))
{
    Console.Error.WriteLine($"source directory not found: {source}");
    return 1;
}
if (Directory.Exists(mountPoint))
{
    // winfsp creates the junction itself; the mount point must not exist
    if (Directory.EnumerateFileSystemEntries(mountPoint).Any())
    {
        Console.Error.WriteLine($"mount point must be empty or nonexistent: {mountPoint}");
        return 1;
    }
    Directory.Delete(mountPoint);
}
Directory.CreateDirectory(overwrite);

var outputRules = new List<OutputRule>();
foreach (var rule in args.Skip(4))
{
    int eq = rule.IndexOf('=');
    if (eq <= 0)
    {
        Console.Error.WriteLine($"ignoring malformed rule: {rule}");
        continue;
    }
    outputRules.Add(new OutputRule(rule[..eq], rule[(eq + 1)..]));
}

FileSystemHost host;
switch (mode)
{
    case "passthrough":
        host = new FileSystemHost(new PassthroughFileSystem(source));
        break;
    case "overlay":
    case "overlay-nocow":
        var root = new VirtualNode<BackedEntry>("", NodeFlags.Directory, null, default);
        root.LinkDirectory(source, "");
        root.LinkDirectory(
            overwrite,
            "",
            LinkFlags.Recursive | LinkFlags.CreateTarget | LinkFlags.Whiteouts
        );
        foreach (var rule in outputRules)
        {
            Directory.CreateDirectory(rule.OutputDirectory);
            root.LinkDirectory(rule.OutputDirectory, "", LinkFlags.Recursive | LinkFlags.Whiteouts);
        }
        host = new FileSystemHost(
            new OverlayFileSystem(
                root,
                new OverlayFileSystemOptions
                {
                    CopyUpEnabled = mode == "overlay",
                    OutputRules = outputRules,
                    ProcessTracker = new ProcessTracker(),
                    PhysicalMountRoot = mountPoint,
                }
            )
        );
        break;
    default:
        Console.Error.WriteLine($"unknown mode: {mode}");
        return 1;
}

int status = host.Mount(mountPoint, null, false, 0);
if (status < 0)
{
    Console.Error.WriteLine($"mount failed: 0x{unchecked((uint)status):X8}");
    host.Dispose();
    return 1;
}

Console.WriteLine($"mounted ({mode}) {source} at {host.MountPoint()}");
if (mode == "overlay")
{
    Console.WriteLine($"runtime writes go to {overwrite}");
}
Console.WriteLine("press any key to unmount...");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    host.Unmount();
};

try
{
    Console.ReadKey(intercept: true);
}
finally
{
    host.Unmount();
}

host.Dispose();
return 0;
