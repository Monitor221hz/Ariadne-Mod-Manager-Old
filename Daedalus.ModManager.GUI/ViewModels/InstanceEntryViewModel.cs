namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class InstanceEntryViewModel(string name, string folder, bool isCurrent)
    : ViewModelBase
{
    public string Name { get; } = name;
    public string Folder { get; } = folder;
    public bool IsCurrent { get; } = isCurrent;

    public string DisplayHeader => IsCurrent ? $"{Name} (active)" : Name;
    public string RemoveHeader => $"Remove {Name}…";
}
