using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Daedalus.ModManager.GUI.Views;

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
            disposables.Add(subscription);
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
