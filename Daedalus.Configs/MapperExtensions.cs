using Daedalus.Configs.Serialization;
using Daedalus.Contracts.Configs;
using Riok.Mapperly.Abstractions;

namespace Daedalus.Configs;

[Mapper]
public static partial class MapperExtensions
{
    public static partial Executable Map(this ExecutableRecord record);

    public static partial GamePath Map(this GamePathRecord record);

    public static partial PlatformConfiguration Map(this PlatformConfigurationRecord record);

    public static partial VendorInfo Map(this VendorInfoRecord record);

    public static partial SupportedGame Map(this SupportedGameRecord record);

    public static partial ExecutableRecord Record(this Executable executable);

    public static partial GamePathRecord Record(this GamePath path);

    public static partial PlatformConfigurationRecord Record(this PlatformConfiguration platform);

    public static partial VendorInfoRecord Record(this VendorInfo vendor);

    public static partial SupportedGameRecord Record(this SupportedGame game);

    private static IExecutable MapToIExecutable(ExecutableRecord record) => Map(record);

    private static IGamePath MapToIGamePath(GamePathRecord record) => Map(record);

    private static IPlatformConfiguration MapToIPlatformConfiguration(
        PlatformConfigurationRecord record
    ) => Map(record);

    private static IVendorInfo MapToIVendorInfo(VendorInfoRecord record) => Map(record);

    private static FileInfo MapToFileInfo(string path) => new(path);

    private static DirectoryInfo MapToDirectoryInfo(string path) => new(path);

    private static string MapToString(FileInfo file) => file.FullName;

    private static string MapToString(DirectoryInfo directory) => directory.FullName;
}
