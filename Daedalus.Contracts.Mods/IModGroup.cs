using System.Drawing;

namespace Daedalus.Contracts.Mods;

public interface IModGroup : IList<ILibraryMod>
{
    string Name { get; }
    Color HeaderColor { get; set; }
}
