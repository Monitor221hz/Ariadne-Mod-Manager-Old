namespace Daedalus.WebProtocol.Nexus;

public interface INxmProtocolRegistration
{
    bool Register();

    NxmUnregistrationResult Unregister();

    NxmAssociationState GetState();

    string GetDiagnosticReport();
}
