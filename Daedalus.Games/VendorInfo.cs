using Daedalus.Contracts.Games;

namespace Daedalus.Games;

public class VendorInfo : IVendorInfo
{
    public int Steam { get; }
    public int GOG { get; }

    public VendorInfo(int steam, int gog)
    {
        Steam = steam;
        GOG = gog;
    }
}
