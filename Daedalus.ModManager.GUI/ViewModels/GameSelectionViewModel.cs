using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Daedalus.Contracts.Games;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class GameSelectionViewModel : ViewModelBase
{
    private readonly IGameCatalog _catalog;
    private readonly IGameLocator _locator;

    private DetectedGameViewModel? _selectedGame;
    private IInstalledGame? _confirmedGame;
    private bool _noGamesDetected;

    public ObservableCollection<DetectedGameViewModel> DetectedGames { get; } = [];

    public DetectedGameViewModel? SelectedGame
    {
        get => _selectedGame;
        set => this.RaiseAndSetIfChanged(ref _selectedGame, value);
    }

    public IInstalledGame? ConfirmedGame
    {
        get => _confirmedGame;
        private set => this.RaiseAndSetIfChanged(ref _confirmedGame, value);
    }

    public bool NoGamesDetected
    {
        get => _noGamesDetected;
        private set => this.RaiseAndSetIfChanged(ref _noGamesDetected, value);
    }

    public ReactiveCommand<Unit, Unit> DetectCommand { get; }
    public ReactiveCommand<Unit, Unit> ContinueCommand { get; }

    public GameSelectionViewModel(IGameCatalog catalog, IGameLocator locator)
    {
        _catalog = catalog;
        _locator = locator;

        DetectCommand = ReactiveCommand.CreateFromTask(DetectAsync);
        ContinueCommand = ReactiveCommand.Create(
            () =>
            {
                ConfirmedGame = SelectedGame!.Game;
            },
            this.WhenAnyValue(x => x.SelectedGame).Select(game => game is not null)
        );

        DetectCommand.Execute().Subscribe();
    }

    private Task DetectAsync() =>
        Task.Run(() =>
        {
            DetectedGameViewModel[] detected;
            try
            {
                detected = _locator
                    .FindInstalledGames(_catalog.Games)
                    .Select(game => new DetectedGameViewModel(game))
                    .ToArray();
            }
            catch
            {
                detected = [];
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                DetectedGames.Clear();
                foreach (var game in detected)
                {
                    DetectedGames.Add(game);
                }
                NoGamesDetected = detected.Length == 0;
            });
        });
}
