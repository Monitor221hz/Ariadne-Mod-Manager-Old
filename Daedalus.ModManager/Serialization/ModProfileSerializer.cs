using System.Diagnostics;
using System.Text.Json;
using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager.Serialization;

public sealed class ModProfileSerializer : IModProfileSerializer
{
    public const string FileName = "profile.json";

    private readonly ILibraryModSerializer _libraryModSerializer;
    private readonly IModManagerPaths _paths;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public ModProfileSerializer(ILibraryModSerializer libraryModSerializer, IModManagerPaths paths)
    {
        _libraryModSerializer = libraryModSerializer;
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

        List<ILibraryMod> looseMods = record
            .ModList.LooseMods.Select(LoadMod)
            .OfType<ILibraryMod>()
            .ToList();
        List<IModGroup> groups = record
            .ModList.ModGroups.Select(group => new ModGroup(
                group.Name,
                group.Mods.Select(LoadMod).OfType<ILibraryMod>().ToList(),
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

    private static string ModFolderName(ILibraryMod mod) => mod.Directory.Name;

    private ILibraryMod? LoadMod(string modFolderName)
    {
        try
        {
            return _libraryModSerializer.Load(
                new DirectoryInfo(Path.Join(_paths.ModsFolder.FullName, modFolderName))
            );
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"Skipping mod \"{modFolderName}\": {ex.Message}");
            return null;
        }
    }
}
