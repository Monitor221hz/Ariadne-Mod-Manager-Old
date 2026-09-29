namespace Ariadne.WebProtocol.Modl;

internal sealed class UnsupportedModlProtocolRegistration : IModlProtocolRegistration
{
    private const string UnsupportedMessage =
        "modl:// protocol registration is only supported on Windows.";

    private readonly string _applicationName;

    public UnsupportedModlProtocolRegistration(string applicationName)
    {
        _applicationName = applicationName;
    }

    public bool Register()
    {
        return false;
    }

    public ModlUnregistrationResult Unregister()
    {
        return new ModlUnregistrationResult(false, false, false);
    }

    public ModlAssociationState GetState()
    {
        return new ModlAssociationState
        {
            Status = ModlAssociationStatus.Unregistered,
            IsRegistered = false,
            HasRegistrationEntries = false,
            HandlerName = _applicationName,
        };
    }

    public string GetDiagnosticReport()
    {
        return UnsupportedMessage;
    }
}
