using System.Text.Json;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using Daedalus.Mods;
using Daedalus.Mods.Serialization;

namespace Daedalus.ModManager.Serialization;

public sealed class ModProfileSerializer : IModProfileSerializer
{
    public const string FileName = "profile.json";

    private readonly IModInfoSerializer _modInfoSerializer;
    private readonly IModManagerPaths _paths;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public ModProfileSerializer(IModInfoSerializer modInfoSerializer, IModManagerPaths paths)
    {
        _modInfoSerializer = modInfoSerializer;
        _paths = paths;
    }

    public IModProfile Load(FileInfo profileFile)
    {
        var record =
            JsonSerializer.Deserialize<ModProfileRecord>(
                File.ReadAllText(profileFile.FullName),
                SerializerOptions
            )
            ?? throw new InvalidOperationException(
                $"Profile \"{profileFile.FullName}\" deserialized to null."
            );
        var profileFolder =
            profileFile.Directory
            ?? throw new InvalidOperationException(
                $"Profile file \"{profileFile.FullName}\" has no parent directory."
            );

        List<IModInfo> looseMods = record
            .ModList.LooseMods.Select(LoadMod)
            .Cast<IModInfo>()
            .ToList();
        List<IModGroup> groups = record
            .ModList.ModGroups.Select(group => new ModGroup(
                group.Name,
                group.Mods.Select(LoadMod).Cast<IModInfo>().ToList(),
                group.HeaderColor
            ))
            .Cast<IModGroup>()
            .ToList();

        return new ModProfile(
            record.Name,
            new ModList(looseMods, groups),
            record.Version,
            profileFolder
        );
    }

    public IModProfile Load(DirectoryInfo profileFolder) =>
        Load(new FileInfo(Path.Join(profileFolder.FullName, FileName)));

    public void Save(IModProfile profile)
    {
        var record = new ModProfileRecord(
            profile.Name,
            new ModListRecord(
                profile.ModList.LooseMods.Select(ModFolderName).ToList(),
                profile
                    .ModList.ModGroups.Select(group => new ModGroupRecord(
                        group.Name,
                        group.HeaderColor,
                        group.Select(ModFolderName).ToList()
                    ))
                    .ToList()
            ),
            profile.Version
        );
        profile.ProfileFolder.Refresh();
        if (!profile.ProfileFolder.Exists)
        {
            profile.ProfileFolder.Create();
        }
        File.WriteAllText(
            Path.Join(profile.ProfileFolder.FullName, FileName),
            JsonSerializer.Serialize(record, SerializerOptions)
        );
    }

    private static string ModFolderName(IModInfo mod) => mod.Directory.Name;

    private IModInfo LoadMod(string modFolderName) =>
        _modInfoSerializer.Load(
            new DirectoryInfo(Path.Join(_paths.ModsFolder.FullName, modFolderName))
        );
}
