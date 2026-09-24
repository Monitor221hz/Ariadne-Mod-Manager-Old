namespace Daedalus.WebProtocol.Nexus;

public sealed record NxmRegistrationOptions
{
    public required string ApplicationName { get; init; }
    public required string ApplicationDescription { get; init; }
    public required string ExecutablePath { get; init; }

    public string ProgId => $"{ApplicationName}.nxm";
    public string SoftwarePath => $@"Software\{ApplicationName}";
    public string Command => $"\"{ExecutablePath}\" --nxm \"%1\"";
    public string Icon => $"\"{ExecutablePath}\",0";
}
