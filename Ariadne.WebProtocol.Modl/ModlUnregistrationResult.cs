namespace Ariadne.WebProtocol.Modl;

public sealed record ModlUnregistrationResult(
    bool Succeeded,
    bool RemovedRegistration,
    bool RetainedUserChoice
);
