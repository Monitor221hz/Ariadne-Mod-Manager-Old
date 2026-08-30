using Daedalus.Contracts.Games;

namespace Daedalus.Games;

public class VendorInfo : IVendorInfo
{
    public uint Steam { get; }
    public int GOG { get; }

    public VendorInfo(uint steam, int gog)
    {
        Steam = steam;
        GOG = gog;
    }
}
