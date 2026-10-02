namespace Ariadne.Contracts.ModManager;

public interface IProfileService
{
    IReadOnlyList<string> Names { get; }

    IModProfile? Active { get; }

    void RefreshNames();

    IModProfile Create(string name);

    bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    IModProfile ActivateLatestOrDefault();

    IModProfile Switch(string name);

    IModProfile Read(string name);

    void SaveActive();
}
