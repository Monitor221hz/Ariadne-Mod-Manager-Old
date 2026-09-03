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

    public virtual IEnumerable<TreeNodeViewModel> Children => Enumerable.Empty<TreeNodeViewModel>();
    public virtual bool HasChildren => false;

    public virtual void Unload() { }
}
