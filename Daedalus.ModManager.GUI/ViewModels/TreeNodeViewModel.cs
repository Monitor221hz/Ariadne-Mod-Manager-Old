using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Daedalus.ModManager.GUI.ViewModels;

public abstract class TreeNodeViewModel : ViewModelBase
{
    private bool _isEditing;
    private bool _isExpanded;
    private string _committedName = string.Empty;
    private readonly Subject<Unit> _renameCommitted = new();
    public IObservable<Unit> RenameCommitted => _renameCommitted;

    public bool IsExpanded
    {
        get => _isExpanded;
        internal set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public abstract string DisplayName { get; set; }
    public virtual uint? PriorityValue => null;
    public virtual string? VersionText => null;
    public abstract string SizeText { get; set; }
    public abstract long SizeBytes { get; }
    private static readonly IReadOnlyList<TreeNodeViewModel> NoChildren = [];

    public virtual IEnumerable<TreeNodeViewModel> Children => NoChildren;
    public virtual bool HasChildren => false;
    public abstract bool RenameAllowed { get; }

    public bool IsEditing
    {
        get => _isEditing;
        private set => this.RaiseAndSetIfChanged(ref _isEditing, value);
    }

    public ReactiveCommand<Unit, Unit> StartRenameCommand { get; }
    public ReactiveCommand<Unit, Unit> FinishRenameCommand { get; }

    protected TreeNodeViewModel()
    {
        StartRenameCommand = ReactiveCommand.Create(() =>
        {
            if (!RenameAllowed || IsEditing)
            {
                return;
            }
            _committedName = DisplayName;
            IsEditing = true;
        });

        FinishRenameCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (!IsEditing)
            {
                return;
            }
            IsEditing = false;
            var name = DisplayName.Trim();
            if (name.Length == 0 || name == _committedName)
            {
                DisplayName = _committedName;
                return;
            }
            var committed = await Task.Run(() => ApplyRename(name));
            _committedName = committed;
            DisplayName = committed;
            _renameCommitted.OnNext(Unit.Default);
        });

        FinishRenameCommand
            .ThrownExceptions.ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(_ => DisplayName = _committedName);
    }

    protected virtual string ApplyRename(string name) => name;
}
