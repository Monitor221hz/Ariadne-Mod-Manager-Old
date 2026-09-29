using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Ariadne.ModManager.GUI.ViewModels;

namespace Ariadne.ModManager.GUI.Views;

public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }
        var name = param
            .GetType()
            .FullName!.Replace(".ViewModels.", ".Views.")
            .Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);
        return type is not null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
