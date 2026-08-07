using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma;

/// <summary>
/// Çevre aydınlatma projesini (M15) Tek Hat Şeması (M11) <see cref="ElektrikProje"/> modeline
/// dönüştürür. Tek bir Aydınlatma Panosu kurulur; her güzergâh (aydınlatma devresi) panonun bir
/// çıkış sigortası (Linye) olur — sorti = direk sayısı, güç = armatür güçleri toplamı, kablo boyu =
/// gerçek güzergâh uzunluğu. Sigorta/kesit/ΔU M11 tarafında yeniden hesaplanır (M12 aktarım deseni).
/// </summary>
public static class CevreAydinlatmaTekHatAktarimServisi
{
    public sealed class Sonuc
    {
        public ElektrikProje? Proje { get; init; }
        public string? Hata { get; init; }
        public List<string> Uyarilar { get; } = new();
        public bool Basarili => Proje != null;
    }

    public static Sonuc Donustur(CevreAydinlatmaProje kaynak)
    {
        if (kaynak.Guzergahlar.Count == 0)
            return new Sonuc { Hata = "Projede güzergâh (aydınlatma devresi) yok; aktarım yapılamaz." };

        var armaturById = kaynak.ArmaturKutuphanesi
            .GroupBy(a => a.Id)
            .ToDictionary(g => g.Key, g => g.Last());

        var pano = new Pano
        {
            Ad = "Aydınlatma Panosu",
            PanoTipi = PanoTipi.AydinlatmaPanosu,
            UstPanoAd = null
        };

        var uyarilar = new List<string>();
        int sira = 1;

        foreach (var g in kaynak.Guzergahlar)
        {
            var direkler = kaynak.Direkler.Where(d => d.GuzergahId == g.Id).ToList();
            if (direkler.Count == 0)
            {
                uyarilar.Add($"'{g.Ad}' güzergâhında direk yok; linye atlandı.");
                continue;
            }

            double toplamW = 0;
            foreach (var d in direkler)
            {
                int? armaturId = d.ArmaturTanimId ?? g.ArmaturTanimId;
                if (armaturId is int id && armaturById.TryGetValue(id, out var armatur))
                    toplamW += armatur.GucW;
                else
                    uyarilar.Add($"'{g.Ad}' güzergâhında armatür tanımı bulunamayan direk var; 0 W sayıldı.");
            }

            int sorti = direkler.Count;
            double boy = PolylineUzunlugu(g.Rota);

            pano.Linyeler.Add(new Linye
            {
                LinyeNo = $"A{sira++}",
                YukTipiAd = "Aydınlatma",
                FazTipi = FazTipi.Monofaze,
                SortiSayisi = sorti,
                BirimGucW = toplamW / sorti,
                TalepFaktoru = 1.0,
                SigortaCinsi = SigortaCinsi.B,
                Aciklama = g.Ad,
                KabloMesafeM = boy > 0 ? boy : 10
            });
        }

        if (pano.Linyeler.Count == 0)
            return new Sonuc { Hata = "Hiçbir güzergâhta direk yok; aktarım yapılamaz." };

        pano.ToplamGucW = pano.Linyeler.Sum(l => l.ToplamGucW);

        var proje = new ElektrikProje { ProjeAdi = kaynak.Ad };
        proje.KokPano = pano;
        proje.TumPanolar.Add(pano);

        var sonuc = new Sonuc { Proje = proje };
        sonuc.Uyarilar.AddRange(uyarilar);
        return sonuc;
    }

    private static double PolylineUzunlugu(IReadOnlyList<Point2D> rota)
    {
        double d = 0;
        for (int i = 0; i + 1 < rota.Count; i++)
        {
            double dx = rota[i + 1].X - rota[i].X;
            double dy = rota[i + 1].Y - rota[i].Y;
            d += Math.Sqrt(dx * dx + dy * dy);
        }
        return d;
    }
}
