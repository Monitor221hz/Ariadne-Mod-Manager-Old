using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Ariadne.ModManager.GUI.ViewModels;

public abstract class TreeNodeViewModel : ViewModelBase
{
    private bool _isEditing;
    private bool _isExpanded;
    private bool _isReadOnly;
    private string _committedName = string.Empty;
    private static readonly Subject<TreeNodeViewModel> _fileOpenRequested = new();
    public static IObservable<TreeNodeViewModel> FileOpenRequested => _fileOpenRequested;
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
    public abstract string SizeText { get; }
    public abstract long SizeBytes { get; }
    public virtual SelectedModVerdict? ConflictVerdict { get; set; }
    private static readonly IReadOnlyList<TreeNodeViewModel> NoChildren = [];

    public virtual IEnumerable<TreeNodeViewModel> Children => NoChildren;
    public virtual bool HasChildren => false;
    public virtual bool ExpanderVisible => true;
    public abstract bool RenameAllowed { get; }

    public bool IsEditing
    {
        get => _isEditing;
        private set => this.RaiseAndSetIfChanged(ref _isEditing, value);
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => this.RaiseAndSetIfChanged(ref _isReadOnly, value);
    }

    public ReactiveCommand<Unit, Unit> StartRenameCommand { get; }
    public ReactiveCommand<Unit, Unit> FinishRenameCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    protected TreeNodeViewModel()
    {
        OpenCommand = ReactiveCommand.Create(() => _fileOpenRequested.OnNext(this));
        StartRenameCommand = ReactiveCommand.Create(
            () =>
            {
                if (!RenameAllowed || IsEditing)
                {
                    return;
                }
                _committedName = DisplayName;
                IsEditing = true;
            },
            this.WhenAnyValue(x => x.IsReadOnly).Select(readOnly => !readOnly)
        );

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
