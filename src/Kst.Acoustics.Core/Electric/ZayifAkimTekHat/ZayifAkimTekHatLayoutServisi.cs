using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public static class ZayifAkimTekHatLayoutServisi
{
    public static void OtomatikYerlestir(ELVSayfa sayfa, List<ELVKatInfo>? katlar = null)
    {
        if (sayfa == null || sayfa.Bloklar == null) return;

        double floorTopMargin = 50;
        double floorHeight = 120;
        double blokH = 50;

        // Fallback floor inferring if katlar is null or empty
        var katlarList = katlar;
        if (katlarList == null || katlarList.Count == 0)
        {
            var inferredKats = sayfa.Bloklar
                .Where(b => b.Kat.HasValue)
                .Select(b => b.Kat ?? 0)
                .Distinct()
                .OrderByDescending(k => k)
                .ToList();

            if (inferredKats.Count == 0)
            {
                inferredKats.Add(0); // fallback
            }

            katlarList = inferredKats.Select(k => new ELVKatInfo { KatNo = k, Ad = $"Kat {k}" }).ToList();
        }

        // Map blocks with missing or invalid floors to default floor
        int defaultKat = katlarList.LastOrDefault()?.KatNo ?? 0;
        foreach (var blok in sayfa.Bloklar)
        {
            if (!blok.Kat.HasValue || !katlarList.Any(k => k.KatNo == blok.Kat.Value))
            {
                blok.Kat = defaultKat;
            }
        }

        // Place blocks floor by floor
        for (int floorIdx = 0; floorIdx < katlarList.Count; floorIdx++)
        {
            var katInfo = katlarList[floorIdx];
            var katNo = katInfo.KatNo;
            double floorYStart = floorTopMargin + floorIdx * floorHeight;
            double blokY = floorYStart + (floorHeight - blokH) / 2;

            var floorBlocks = sayfa.Bloklar.Where(b => b.Kat == katNo).ToList();

            var merkezler = floorBlocks.Where(b => b.Rol == ELVBlokRol.Merkez).ToList();
            var araToplayicilar = floorBlocks.Where(b => b.Rol == ELVBlokRol.AraToplayici).ToList();
            var cihazlar = floorBlocks.Where(b => b.Rol == ELVBlokRol.Cihaz).ToList();

            for (int i = 0; i < merkezler.Count; i++)
            {
                merkezler[i].LayoutX = 80 + i * 160;
                merkezler[i].LayoutY = blokY;
            }

            for (int i = 0; i < araToplayicilar.Count; i++)
            {
                araToplayicilar[i].LayoutX = 260 + i * 160;
                araToplayicilar[i].LayoutY = blokY;
            }

            for (int i = 0; i < cihazlar.Count; i++)
            {
                cihazlar[i].LayoutX = 440 + i * 160;
                cihazlar[i].LayoutY = blokY;
            }
        }
    }

    // Overload for backward compatibility
    public static void OtomatikYerlestir(ELVSayfa sayfa, double genislik, double yukseklik)
    {
        OtomatikYerlestir(sayfa, null);
    }

    /// Bloğun çizim genişliği ve yüksekliği
    public static (double Width, double Height) BlokBoyutu() => (140, 50);

    /// İki blok arasında L-şekilli çizgi koordinatları
    public static List<(double X, double Y)> BaglantiBicimi(double x1, double y1, double x2, double y2)
    {
        var noktalar = new List<(double, double)>();

        // L-şekil: dikey → yatay
        noktalar.Add((x1, y1));
        double ortaY = (y1 + y2) / 2;
        noktalar.Add((x1, ortaY));
        noktalar.Add((x2, ortaY));
        noktalar.Add((x2, y2));

        return noktalar;
    }
}
