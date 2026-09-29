using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Ariadne.ModManager.GUI.Views;

public partial class ModEntryNodeView : ReactiveUserControl<ViewModels.TreeNodeViewModel>
{
    public ModEntryNodeView()
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

    private void MaskedTextBox_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (
            sender is MaskedTextBox textBox
            && textBox.DataContext is ViewModels.TreeNodeViewModel node
        )
        {
            node.StartRenameCommand.Execute(Unit.Default).Subscribe();
            textBox.Focus();
        }
    }
}
