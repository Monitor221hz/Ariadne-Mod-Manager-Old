using System.Drawing;

namespace Ariadne.Contracts.ModManager;

public interface IModGroup : IList<IModListEntry>
{
    string Name { get; set; }
    Color HeaderColor { get; set; }
}
