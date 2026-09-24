using System.Collections.ObjectModel;
using Daedalus.Contracts.ModManager;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class LoadOrderInfoViewModel : ViewModelBase
{
    private bool _active;
    private readonly ILoadOrderInfo _info;
    public ILoadOrderInfo Model => _info;
    public string Name => _info.Name;

    public bool Active
    {
        get => _active;
        set
        {
            _info.Active = value;
            this.RaiseAndSetIfChanged(ref _active, value);
        }
    }

    public ObservableCollection<LoadOrderInfoViewModel> Dependencies { get; } = new();

    public LoadOrderInfoViewModel(ILoadOrderInfo info)
    {
        _info = info;
        _active = info.Active;
        foreach (var dep in info.Dependencies)
        {
            Dependencies.Add(new LoadOrderInfoViewModel(dep));
        }
    }
}
