using Ariadne.Contracts.ModManager;
using Ariadne.ModManager.Serialization;

namespace Ariadne.ModManager;

public sealed class ProfileService(IModProfileSerializer serializer, IModManagerPaths paths)
    : IProfileService
{
    private readonly List<string> _names = [];

    public IReadOnlyList<string> Names => _names;

    public IModProfile? Active { get; private set; }

    public void RefreshNames()
    {
        _names.Clear();
        var root = paths.ProfilesFolder;
        root.Refresh();
        if (!root.Exists)
        {
            return;
        }
        foreach (
            var dir in root.EnumerateDirectories()
                .Where(dir => File.Exists(Path.Join(dir.FullName, ModProfileSerializer.FileName)))
                .OrderBy(dir => dir.Name, StringComparer.OrdinalIgnoreCase)
        )
        {
            _names.Add(dir.Name);
        }
    }

    public IModProfile Create(string name)
    {
        var profile = new ModProfile(
            name,
            new ModList([], []),
            new Version(1, 0),
            new DirectoryInfo(Path.Join(paths.ProfilesFolder.FullName, name))
        );
        profile.InitializeDisk();
        serializer.Save(profile);
        RefreshNames();
        return profile;
    }

    public IModProfile ActivateLatestOrDefault()
    {
        var root = paths.ProfilesFolder;
        root.Refresh();
        if (root.Exists)
        {
            var latestProfileFile = root.EnumerateDirectories()
                .Select(dir => new FileInfo(Path.Join(dir.FullName, ModProfileSerializer.FileName)))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
            if (latestProfileFile is not null)
            {
                return Active = Normalize(serializer.Load(latestProfileFile));
            }
        }
        return Active = Create("Default");
    }

    public IModProfile Switch(string name)
    {
        var profile = serializer.Load(
            new DirectoryInfo(Path.Join(paths.ProfilesFolder.FullName, name))
        );
        return Active = Normalize(profile);
    }

    public IModProfile Read(string name)
    {
        return serializer.Load(new DirectoryInfo(Path.Join(paths.ProfilesFolder.FullName, name)));
    }

    public void SaveActive()
    {
        if (Active is not null)
        {
            serializer.Save(Active);
        }
    }

    private static IModProfile Normalize(IModProfile profile)
    {
        profile.Name = profile.ProfileFolder.Name;
        var modList = profile.ModList;
        ModOrderSync.ApplyOrder(
            modList.LooseMods.ToList(),
            modList.ModGroups.ToList(),
            modList
                .ModGroups.Select(group => (IReadOnlyList<IModListEntry>)group.ToList())
                .ToList(),
            modList
        );
        return profile;
    }
}
