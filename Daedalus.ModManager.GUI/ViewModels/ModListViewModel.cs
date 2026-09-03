using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager;
using Daedalus.ModManager.Serialization;
using Daedalus.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class ModListViewModel : ViewModelBase
{
    private readonly IModProfileSerializer _profileSerializer;
    private readonly IModInfoSerializer _modSerializer;
    private readonly IModManagerPaths _paths;
    private readonly IInstanceService _instances;

    private ProfileViewModel? _activeProfile;
    private HierarchicalTreeDataGridSource<TreeNodeViewModel>? _modsSource;
    private string? _currentGameText;
    private string? _selectedProfileName;
    private bool _suppressProfileSwitch;

    public ProfileViewModel? ActiveProfile
    {
        get => _activeProfile;
        private set => this.RaiseAndSetIfChanged(ref _activeProfile, value);
    }

    public HierarchicalTreeDataGridSource<TreeNodeViewModel>? ModsSource
    {
        get => _modsSource;
        private set => this.RaiseAndSetIfChanged(ref _modsSource, value);
    }

    public ObservableCollection<string> ProfileNames { get; } = [];

    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProfileName, value);
            if (value is not null && !_suppressProfileSwitch && value != ActiveProfile?.Name)
            {
                LoadProfileByName(value);
            }
        }
    }

    public string? CurrentGameText
    {
        get => _currentGameText;
        private set => this.RaiseAndSetIfChanged(ref _currentGameText, value);
    }

    public ReactiveCommand<Unit, Unit> InitializeCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateModCommand { get; }

    public ModListViewModel(
        IModProfileSerializer profileSerializer,
        IModInfoSerializer modSerializer,
        IModManagerPaths paths,
        IInstanceService instances
    )
    {
        _profileSerializer = profileSerializer;
        _modSerializer = modSerializer;
        _paths = paths;
        _instances = instances;

        InitializeCommand = ReactiveCommand.CreateFromTask(InitializeAsync);
        CreateModCommand = ReactiveCommand.CreateFromTask(
            CreateEmptyModAsync,
            this.WhenAnyValue(x => x.ActiveProfile).Select(profile => profile is not null)
        );
    }

    private Task InitializeAsync() =>
        Task.Run(() =>
        {
            var profilesRoot = _paths.ProfilesFolder;
            profilesRoot.Create();
            var latestProfileFile = profilesRoot
                .EnumerateDirectories()
                .Select(dir => new FileInfo(Path.Join(dir.FullName, ModProfileSerializer.FileName)))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            var profile = latestProfileFile is not null
                ? _profileSerializer.Load(latestProfileFile)
                : CreateDefaultProfile(
                    new DirectoryInfo(Path.Join(profilesRoot.FullName, "Default"))
                );

            var currentGame = _instances.CurrentGame;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ActiveProfile = new ProfileViewModel(profile);
                ModsSource = BuildGridSource(profile.ModList);
                RefreshProfileNames();
                SyncSelectedProfile(profile.ProfileFolder.Name);
                CurrentGameText = currentGame is { } game
                    ? $"{game.Configuration.Name} — {game.InstallPath.FullName}"
                    : null;
            });
        });

    private IModProfile CreateDefaultProfile(DirectoryInfo profileFolder)
    {
        var profile = new ModProfile(
            "Default",
            new ModList([], []),
            new Version(1, 0),
            profileFolder
        );
        profile.InitializeDisk();
        _profileSerializer.Save(profile);
        return profile;
    }

    private void RefreshProfileNames()
    {
        ProfileNames.Clear();
        var profilesRoot = _paths.ProfilesFolder;
        profilesRoot.Refresh();
        if (!profilesRoot.Exists)
        {
            return;
        }
        foreach (
            var dir in profilesRoot
                .EnumerateDirectories()
                .Where(dir => File.Exists(Path.Join(dir.FullName, ModProfileSerializer.FileName)))
                .OrderBy(dir => dir.Name, StringComparer.OrdinalIgnoreCase)
        )
        {
            ProfileNames.Add(dir.Name);
        }
    }

    private void SyncSelectedProfile(string? name)
    {
        _suppressProfileSwitch = true;
        try
        {
            SelectedProfileName = name;
        }
        finally
        {
            _suppressProfileSwitch = false;
        }
    }

    private void LoadProfileByName(string name)
    {
        var profileFolder = new DirectoryInfo(Path.Join(_paths.ProfilesFolder.FullName, name));
        var profile = _profileSerializer.Load(profileFolder);
        ActiveProfile = new ProfileViewModel(profile);
        ModsSource = BuildGridSource(profile.ModList);
    }

    private Task CreateEmptyModAsync() =>
        Task.Run(() =>
        {
            var modsFolder = _paths.ModsFolder;
            var folder = UniqueModFolder(modsFolder);
            var mod = new ModInfo(0, "New Mod", folder, SourceType.Local, "1.0.0", [], "")
            {
                Priority = (uint)(ActiveProfile!.Model.ModList.Count + 1),
            };
            folder.Create();
            _modSerializer.Save(mod);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ActiveProfile!.AddLooseMod(mod);
                _profileSerializer.Save(ActiveProfile.Model);
                // root collection changes not observed so rebuild
                ModsSource = BuildGridSource(ActiveProfile.Model.ModList);
            });
        });

    private static DirectoryInfo UniqueModFolder(DirectoryInfo modsFolder)
    {
        var candidate = new DirectoryInfo(Path.Join(modsFolder.FullName, "New Mod"));
        for (var i = 2; candidate.Exists; i++)
        {
            candidate = new DirectoryInfo(Path.Join(modsFolder.FullName, $"New Mod {i}"));
        }
        return candidate;
    }

    // TreeDataGrid roots: loose mods first, group headers after.
    private static ObservableCollection<TreeNodeViewModel> BuildRoots(IModList modList) =>
        new(
            modList
                .LooseMods.Select(mod => (TreeNodeViewModel)new ModEntryNodeViewModel(mod))
                .Concat(modList.ModGroups.Select(group => new GroupHeaderNodeViewModel(group)))
        );

    private HierarchicalTreeDataGridSource<TreeNodeViewModel> BuildGridSource(IModList modList)
    {
        var roots = BuildRoots(modList);
        var source = new HierarchicalTreeDataGridSource<TreeNodeViewModel>(roots)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<TreeNodeViewModel>(
                    new TemplateColumn<TreeNodeViewModel>(
                        "Name",
                        new FuncDataTemplate<TreeNodeViewModel>(
                            (node, _) => BuildNameCell(node),
                            supportsRecycling: false
                        ),
                        width: new GridLength(3, Avalonia.Controls.GridUnitType.Star)
                    ),
                    node => node.Children,
                    node => node.HasChildren
                ),
                new TextColumn<TreeNodeViewModel, uint?>(
                    "Priority",
                    node => node.PriorityValue,
                    width: new GridLength(90)
                ),
                new TextColumn<TreeNodeViewModel, string?>(
                    "Version",
                    node => node.VersionText,
                    width: new GridLength(120)
                ),
                new TextColumn<TreeNodeViewModel, string>(
                    "Size",
                    node => node.SizeText,
                    width: new GridLength(100)
                ),
            },
        };

        // Children enumerate lazily inside the selector on expand (fork reuses
        // the selector result); collapsing a directory-backed row frees it.
        source.RowCollapsed += (_, args) => (args.Row.Model as TreeNodeViewModel)?.Unload();

        // Groups are expanded by default; expand in reverse to keep earlier indices stable.
        for (var i = roots.Count - 1; i >= 0; i--)
        {
            if (roots[i] is GroupHeaderNodeViewModel)
            {
                source.Expand(i);
            }
        }

        return source;
    }

    // The grid re-calls cell templates with a null model while unrealizing old rows.
    private static Control BuildNameCell(TreeNodeViewModel? node) =>
        node switch
        {
            null => new TextBlock(),
            GroupHeaderNodeViewModel group => new Border
            {
                Background = group.SeparatorBrush,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = new TextBlock
                {
                    Text = group.DisplayName,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brushes.White,
                },
            },
            _ => new TextBlock
            {
                Text = node.DisplayName,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 3),
            },
        };
}
