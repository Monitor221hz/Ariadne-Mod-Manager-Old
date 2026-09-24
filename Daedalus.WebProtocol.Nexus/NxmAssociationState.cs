namespace Daedalus.WebProtocol.Nexus;

public sealed record NxmAssociationState
{
    public required NxmAssociationStatus Status { get; init; }
    public required bool IsRegistered { get; init; }
    public required bool HasRegistrationEntries { get; init; }
    public string? UserChoiceProgId { get; init; }
    public string? UserChoiceCommand { get; init; }
    public required string HandlerName { get; init; }
}
