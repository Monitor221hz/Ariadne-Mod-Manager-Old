namespace Ariadne.WebProtocol.Modl;

public sealed record ModlRegistrationOptions
{
    public const string ProtocolName = "modl";

    public required string ApplicationName { get; init; }
    public required string ApplicationDescription { get; init; }
    public required string ExecutablePath { get; init; }

    public string ProgId => $"{ApplicationName}.{ProtocolName}";
    public string SoftwarePath => $@"Software\{ApplicationName}";
    public string Command => $"\"{ExecutablePath}\" --modl \"%1\"";
    public string Icon => $"\"{ExecutablePath}\",0";
}
