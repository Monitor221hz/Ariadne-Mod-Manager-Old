using System.Text.Json.Serialization;
using Daedalus.Contracts.Games;

namespace Daedalus.Games.Serialization;

public sealed class PlatformConfigurationRecord
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlatformType Platform { get; }
    public List<ExecutableRecord> Executables { get; }
    public int DefaultIndex { get; }

    public PlatformConfigurationRecord(
        PlatformType platform,
        List<ExecutableRecord> executables,
        int defaultIndex
    )
    {
        Platform = platform;
        Executables = executables;
        DefaultIndex = defaultIndex;
    }
}
