using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Models;

/// <summary>
/// Bina islevine gore ihtiyac programi uretmek icin kullanilan mahal sablonu.
/// </summary>
public class MahalSablonu
{
    public string Kod { get; set; } = "";
    public string VarsayilanAd { get; set; } = "";
    public MahalTipi MahalTipi { get; set; } = MahalTipi.Tanimsiz;
    public KullanimSinifi KullanimSinifi { get; set; } = KullanimSinifi.Tanimsiz;
    public List<BinaIslevi> UygunBinaIslevleri { get; set; } = new();

    public int VarsayilanAdet { get; set; } = 1;
    public double MinimumAlanM2 { get; set; }
    public double IdealAlanM2 { get; set; }
    public double MinimumNetYukseklikM { get; set; } = 2.6;
    public double KisiBasinaAlanM2 { get; set; }
    public bool Zorunlu { get; set; } = true;

    public List<MahalTipi> YakinOlmali { get; set; } = new();
    public List<MahalTipi> UzakDurmali { get; set; } = new();
    public bool DisCepheGerekli { get; set; }
    public bool AyniAkstaSureklilikGerekli { get; set; }

    public MahalGereksinimi GereksinimOlustur(int kapasite = 0)
    {
        // Yalniz kapasiteye gore boyutlanan (kisi basina alani olan) mahaller bina kapasitesini
        // kendi kisi kapasitesi olarak alir. WC, teknik oda gibi alani kapasiteden bagimsiz mahaller
        // 0 (tanimsiz) kalir; aksi halde her mahale tum bina kapasitesi yanlislikla yazilirdi.
        var kapasiteyeBagli = kapasite > 0 && KisiBasinaAlanM2 > 0;
        var hedefAlan = kapasiteyeBagli
            ? Math.Max(IdealAlanM2, kapasite * KisiBasinaAlanM2)
            : IdealAlanM2;

        return new MahalGereksinimi
        {
            MahalTipi = MahalTipi,
            KullanimSinifi = KullanimSinifi,
            Ad = VarsayilanAd,
            Adet = VarsayilanAdet,
            KisiKapasitesi = kapasiteyeBagli ? kapasite : 0,
            MinimumAlanM2 = MinimumAlanM2,
            IdealAlanM2 = IdealAlanM2,
            HedefAlanM2 = hedefAlan,
            MinimumNetYukseklikM = MinimumNetYukseklikM,
            // Sablon yalniz minimum net yuksekligi tasidigindan hedef = minimum (asgari serbest
            // yukseklige tasarlanir). Ileride sablona ideal yukseklik eklenirse burasi guncellenmeli.
            HedefNetYukseklikM = MinimumNetYukseklikM,
            Zorunlu = Zorunlu,
        };
    }
}
