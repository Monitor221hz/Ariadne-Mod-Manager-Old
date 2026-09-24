namespace Daedalus.WebProtocol.Nexus;

internal sealed class UnsupportedNxmProtocolRegistration : INxmProtocolRegistration
{
    private const string UnsupportedMessage =
        "nxm:// protocol registration is only supported on Windows.";

    private readonly string _applicationName;

    public UnsupportedNxmProtocolRegistration(string applicationName)
    {
        _applicationName = applicationName;
    }

    public bool Register()
    {
        return false;
    }

    public NxmUnregistrationResult Unregister()
    {
        return new NxmUnregistrationResult(false, false, false);
    }

    public NxmAssociationState GetState()
    {
        return new NxmAssociationState
        {
            Status = NxmAssociationStatus.Unregistered,
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
