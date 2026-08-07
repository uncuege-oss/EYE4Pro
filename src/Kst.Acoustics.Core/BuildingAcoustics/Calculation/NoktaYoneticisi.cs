using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public class NoktaYoneticisi
{
    public AkustikNokta KaynakEkle(Mekan mekan, string ad, double lw)
    {
        var nokta = new AkustikNokta
        {
            Ad = ad,
            Tip = AkustikNoktaTipi.Kaynak,
            Mekan = mekan,
            Lw = lw,
            // Varsayılan olarak mekanın merkezine yerleştir (basit yaklaşım)
            Konum = new Point3D(0, 0, 1.5) 
        };
        return nokta;
    }

    public AkustikNokta AliciEkle(Mekan mekan, string ad)
    {
        var nokta = new AkustikNokta
        {
            Ad = ad,
            Tip = AkustikNoktaTipi.Alici,
            Mekan = mekan,
            // Yönetmelik gereği genellikle döşemeden 1.2m yükseklik
            Konum = new Point3D(0, 0, 1.2)
        };
        return nokta;
    }
}
