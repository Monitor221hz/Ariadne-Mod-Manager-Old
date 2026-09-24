namespace Daedalus.WebProtocol.Modl;

public sealed record ModlAssociationState
{
    public required ModlAssociationStatus Status { get; init; }
    public required bool IsRegistered { get; init; }
    public required bool HasRegistrationEntries { get; init; }
    public string? UserChoiceProgId { get; init; }
    public string? UserChoiceCommand { get; init; }
    public required string HandlerName { get; init; }
}
