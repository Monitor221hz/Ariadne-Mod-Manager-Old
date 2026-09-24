namespace Daedalus.WebProtocol.Nexus;

public sealed record NxmUnregistrationResult(
    bool Succeeded,
    bool RemovedRegistration,
    bool RetainedUserChoice
);
