using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class InstanceSetupViewModel : ViewModelBase
{
    private readonly IInstanceService _instances;

    private string _instanceName = "Default";
    private string _instanceFolder = "";
    private string? _error;
    private string? _completedName;

    public IInstalledGame Game { get; }

    public string InstanceName
    {
        get => _instanceName;
        set => this.RaiseAndSetIfChanged(ref _instanceName, value);
    }

    public string InstanceFolder
    {
        get => _instanceFolder;
        set => this.RaiseAndSetIfChanged(ref _instanceFolder, value);
    }

    public string? Error
    {
        get => _error;
        private set => this.RaiseAndSetIfChanged(ref _error, value);
    }

    public string? CompletedName
    {
        get => _completedName;
        private set => this.RaiseAndSetIfChanged(ref _completedName, value);
    }

    public ReactiveCommand<Unit, Unit> CreateCommand { get; }

    public InstanceSetupViewModel(
        IInstanceService instances,
        IModManagerPaths paths,
        IInstalledGame game
    )
    {
        _instances = instances;
        Game = game;
        InstanceFolder = Path.Join(paths.AssemblyFolder.FullName, "Instances", InstanceName);

        CreateCommand = ReactiveCommand.CreateFromTask(
            CreateAsync,
            this.WhenAnyValue(
                x => x.InstanceName,
                x => x.InstanceFolder,
                (name, folder) =>
                    !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(folder)
            )
        );
    }

    private async Task CreateAsync()
    {
        try
        {
            await Task.Run(() =>
                _instances.Create(InstanceName, new DirectoryInfo(InstanceFolder), Game)
            );
            Error = null;
            CompletedName = InstanceName;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
