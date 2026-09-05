using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Daedalus.ModManager.GUI.Views;

public static class ButtonObservables
{
    public static IObservable<EventPattern<RoutedEventArgs>> ClicksOf(Button button) =>
        Observable.FromEventPattern<RoutedEventArgs>(
            handler => button.Click += handler,
            handler => button.Click -= handler
        );
}
