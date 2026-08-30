using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Daedalus.Contracts.Games;
using Daedalus.Games.Serialization;

namespace Daedalus.Games;

public sealed class GameCatalog : IGameCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<ISupportedGame> Games { get; }

    public GameCatalog(
        IEnumerable<Assembly> gameModules,
        DirectoryInfo? looseConfigDirectory = null
    )
    {
        List<ISupportedGame> games = [];

        foreach (var assembly in gameModules.Distinct())
        {
            games.AddRange(LoadEmbeddedGames(assembly));
        }

        if (looseConfigDirectory is { Exists: true })
        {
            foreach (var file in looseConfigDirectory.EnumerateFiles("*.json"))
            {
                if (TryParse(File.ReadAllText(file.FullName), out var game))
                {
                    games.Add(game);
                }
            }
        }

        Games = games;
    }

    public GameCatalog(DirectoryInfo directory)
        : this([], directory) { }

    public static ISupportedGame LoadGame(string json)
    {
        var record =
            JsonSerializer.Deserialize<SupportedGameRecord>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Game configuration deserialized to null.");
        return record.Map();
    }

    private static IEnumerable<ISupportedGame> LoadEmbeddedGames(Assembly assembly)
    {
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!IsGameConfigResource(resourceName))
            {
                continue;
            }
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }
            using var reader = new StreamReader(stream);
            if (TryParse(reader.ReadToEnd(), out var game))
            {
                yield return game;
            }
        }
    }

    private static bool IsGameConfigResource(string name) =>
        name.Contains(".Embedded.", StringComparison.Ordinal)
        && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static bool TryParse(string json, [NotNullWhen(true)] out ISupportedGame? game)
    {
        game = null;
        try
        {
            game = LoadGame(json);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
