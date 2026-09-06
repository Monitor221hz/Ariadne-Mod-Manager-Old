using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Daedalus.Contracts.Mods;
using Noggog;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class ModEntryNodeViewModel : TreeNodeViewModel
{
    private readonly ILibraryMod _mod;
    private readonly Subject<Unit> _removeRequested = new();
    private uint _priorityValue;
    private bool _active;
    private string _sizeText;

    private string _displayName;
    public ILibraryMod Model => _mod;
    public override uint? PriorityValue => _priorityValue;
    public override string? VersionText => _mod.Info.Version;
    public bool Active
    {
        get => _active;
        set
        {
            _mod.Info.Active = value;
            this.RaiseAndSetIfChanged(ref _active, value);
        }
    }
    public override string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }
    public override bool RenameAllowed => true;

    public override long SizeBytes => Children.Sum(c => c.SizeBytes);
    public override string SizeText
    {
        get => _sizeText;
        set => this.RaiseAndSetIfChanged(ref _sizeText, value);
    }

    public override IEnumerable<TreeNodeViewModel> Children =>
        _mod.Content.Children.Select(ContentNodeViewModel.Wrap);
    public override bool HasChildren => _mod.Content.Children.Count > 0;

    public ModEntryNodeViewModel(ILibraryMod mod)
    {
        _displayName = mod.Name;
        _mod = mod;
        _priorityValue = mod.Info.Priority;
        _sizeText = DiskSize.Format(SizeBytes);
        _active = mod.Info.Active;
        RemoveCommand = ReactiveCommand.Create(() => _removeRequested.OnNext(Unit.Default));
    }

    public IObservable<Unit> RemoveRequested => _removeRequested;
    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }

    public void RefreshFromModel()
    {
        this.RaiseAndSetIfChanged(ref _priorityValue, _mod.Info.Priority, nameof(PriorityValue));
        // this.RaiseAndSetIfChanged(ref _active, _mod.Info.Active, nameof(Active));
    }

    protected override string ApplyRename(string name)
    {
        _mod.RenameTo(name);
        return _mod.Name;
    }
}
