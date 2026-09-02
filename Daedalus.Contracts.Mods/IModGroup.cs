using System.Drawing;

namespace Daedalus.Contracts.Mods;

public interface IModGroup : IList<IModInfo>
{
    string Name { get; }
    Color HeaderColor { get; set; }
}
