using System.Runtime.CompilerServices;
using ReactiveUI;
using ReactiveUI.Builder;

namespace Ariadne.ModManager.GUI.Tests;

internal static class ModuleInit
{
    [ModuleInitializer]
    internal static void Init()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
    }
}
