using System.Collections.Generic;
using System.Linq;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public abstract class TreeNodeViewModel : ViewModelBase
{
    public abstract string DisplayName { get; }
    public virtual uint? PriorityValue => null;
    public virtual string? VersionText => null;
    public virtual string SizeText => "";

    private static readonly IReadOnlyList<TreeNodeViewModel> NoChildren = [];

    public virtual IEnumerable<TreeNodeViewModel> Children => NoChildren;
    public virtual bool HasChildren => false;
}
