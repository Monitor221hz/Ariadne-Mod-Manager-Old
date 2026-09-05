using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Daedalus.ModManager.GUI.Views;

public partial class GroupHeaderNodeView : ReactiveUserControl<ViewModels.TreeNodeViewModel>
{
    public GroupHeaderNodeView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            var subscription = this.GetObservable(DataContextProperty)
                .Select(dataContext => dataContext as ViewModels.TreeNodeViewModel)
                .WhereNotNull()
                .Select(node =>
                    NameBox
                        .GetObservable(InputElement.IsFocusedProperty)
                        .Skip(1)
                        .Where(focused => !focused)
                        .Select(_ => node)
                )
                .Switch()
                .Subscribe(node => node.FinishRenameCommand.Execute(Unit.Default).Subscribe());

            var editSubscription = this.GetObservable(DataContextProperty)
                .Select(dataContext => dataContext as ViewModels.TreeNodeViewModel)
                .WhereNotNull()
                .Select(node => node.WhenAnyValue(n => n.IsEditing))
                .Switch()
                .Where(editing => editing)
                .Subscribe(_ =>
                {
                    Dispatcher.Post(() =>
                    {
                        NameBox.Focus();
                        NameBox.CaretIndex = NameBox.Text?.Length ?? 0;
                    });
                });
            disposables.Add(subscription);
            disposables.Add(editSubscription);
        });
    }

    private void NameBox_LostFocus(object? sender, Avalonia.Input.FocusChangedEventArgs e)
    {
        if (DataContext is ViewModels.TreeNodeViewModel node)
        {
            node.FinishRenameCommand.Execute(Unit.Default).Subscribe();
        }
    }
}
