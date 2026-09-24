namespace Daedalus.WebProtocol.Modl;

public interface IModlProtocolRegistration
{
    bool Register();

    ModlUnregistrationResult Unregister();

    ModlAssociationState GetState();

    string GetDiagnosticReport();
}
