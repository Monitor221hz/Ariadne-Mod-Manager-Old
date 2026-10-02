using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed class ModEntryNodeViewModel : ContentHostNodeViewModel
{
    private readonly IModListEntry _entry;
    private readonly Subject<Unit> _removeRequested = new();
    private readonly Subject<Unit> _removeFromProfileRequested = new();
    private uint _priorityValue;
    private bool _active;
    private SelectedModVerdict? _conflictVerdict;
    private readonly Subject<Unit> _targetChanged = new();
    private string _displayName;
    public IModListEntry Entry => _entry;
    public override uint? PriorityValue => _priorityValue;
    public override string? VersionText => _entry.Mod.Info.Version;
    public bool Active
    {
        get => _active;
        set
        {
            _entry.Active = value;
            this.RaiseAndSetIfChanged(ref _active, value);
        }
    }
    public override string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, PathName.Filter(value));
    }

    public override SelectedModVerdict? ConflictVerdict
    {
        get => _conflictVerdict;
        set => this.RaiseAndSetIfChanged(ref _conflictVerdict, value);
    }
    public override bool RenameAllowed => true;

    public ModEntryNodeViewModel(IModListEntry entry)
        : base(entry.Mod)
    {
        var mod = entry.Mod;
        _entry = entry;
        _displayName = mod.Name;
        _active = entry.Active;
        var writable = this.WhenAnyValue(x => x.IsReadOnly).Select(readOnly => !readOnly);
        DeleteFromDiskCommand = ReactiveCommand.Create(
            () => _removeRequested.OnNext(Unit.Default),
            writable
        );
        ForgetFromProfileCommand = ReactiveCommand.Create(
            () => _removeFromProfileRequested.OnNext(Unit.Default),
            writable
        );
        SetTargetCommand = ReactiveCommand.Create<IGamePath>(
            target =>
            {
                mod.Info.Target = target.Key;
                this.RaisePropertyChanged(nameof(Target));
                _targetChanged.OnNext(Unit.Default);
            },
            writable
        );
    }

    public IObservable<Unit> RemoveRequested => _removeRequested;
    public ReactiveCommand<Unit, Unit> DeleteFromDiskCommand { get; }

    public IObservable<Unit> RemoveFromProfileRequested => _removeFromProfileRequested;
    public ReactiveCommand<Unit, Unit> ForgetFromProfileCommand { get; }

    public string Target => _entry.Mod.Info.Target;
    public IObservable<Unit> TargetChanged => _targetChanged;
    public ReactiveCommand<IGamePath, Unit> SetTargetCommand { get; }

    internal void SetPriorityDisplay(uint priority)
    {
        this.RaiseAndSetIfChanged(ref _priorityValue, priority, nameof(PriorityValue));
    }

    protected override string ApplyRename(string name)
    {
        _entry.Mod.RenameTo(name);
        return _entry.Mod.Name;
    }

    internal void RenameTo(string newName)
    {
        _entry.Mod.RenameTo(newName);
        DisplayName = newName;
        NotifyRenamed();
    }
}
