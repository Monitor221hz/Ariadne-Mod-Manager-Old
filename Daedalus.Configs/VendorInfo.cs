using Daedalus.Contracts.Configs;

namespace Daedalus.Configs;

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
