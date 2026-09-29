using System.Drawing;

namespace Ariadne.Contracts.ModManager;

public interface IModGroup : IList<ILibraryMod>
{
    string Name { get; set; }
    Color HeaderColor { get; set; }
}
