using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public class CozumOnerisi
{
    public string OneriAdi  { get; set; } = "";
    public double ArtisDb   { get; set; }
    public string Aciklama  { get; set; } = "";
    public bool   Uyari     { get; set; } // true → aksiyon değil, dikkat notu
}

public class CozumOnerisiServisi
{
    public List<CozumOnerisi> OnerileriGetir(Bolme bolme, double gerekenArtis)
    {
        var oneriler = new List<CozumOnerisi>();

        if (bolme?.YapiElemani == null)
            return oneriler;

        // M5 — Tüm aday önerileri topla, sonra gerekli artışa göre süz.
        var aday = new List<CozumOnerisi>();

        // Flanking uyarısı: zayıf bağlantı (düşük Kij) flanking iletimini sınırlandırmaz.
        double kij = bolme.Junctions.Count > 0
            ? bolme.Junctions.Min(j => j.KijOverrideDb > 0 ? j.KijOverrideDb : BaglantiKij.GetKij(j.BaglantiTipi))
            : BaglantiKij.GetKij(bolme.YapiElemani.BaglantiTipi);

        if (kij <= 5 && gerekenArtis > 0)
        {
            aday.Add(new CozumOnerisi
            {
                OneriAdi = "Flanking Yolu Kontrolü",
                ArtisDb  = 0,
                Uyari    = true,
                Aciklama = "Düşük Kij değerli rijit bağlantı flanking iletimini yeterince sınırlamıyor. " +
                           "Elastik bağlantı bant veya yüzer döşeme ile yapısal köprü kesilirse " +
                           "R'w pratik limit performansı artabilir."
            });
        }

        // Flanking-limitli durum uyarısı: direkt bölme zaten yüksek Rw ama R' düşük → yan duvar suçlu.
        if (bolme.YapiElemani.Rw >= 55 && bolme.ApparentRw > 0 &&
            (bolme.YapiElemani.Rw - bolme.ApparentRw) >= 5)
        {
            aday.Add(new CozumOnerisi
            {
                OneriAdi = "Flanking-Limitli Durum",
                ArtisDb  = 0,
                Uyari    = true,
                Aciklama = $"Direkt bölme Rw {bolme.YapiElemani.Rw:F0} dB; R' {bolme.ApparentRw:F0} dB. " +
                           "Aradaki fark flanking iletimine işaret ediyor — bu duvarı kalınlaştırmak " +
                           "tek başına yetersiz kalır; yan duvar/zemin/tavan flanking elemanlarını da güçlendirin."
            });
        }

        if (bolme.YapiElemani.Tip == YapiElemaniTipi.Duvar)
        {
            aday.Add(new CozumOnerisi
            {
                OneriAdi = "İlave Alçıpan Kaplama",
                ArtisDb  = 5.0,
                Aciklama = "Mevcut duvara profil ve taşyünü dolgulu tek kat alçıpan eklenmesi."
            });

            aday.Add(new CozumOnerisi
            {
                OneriAdi = "Çift Kat Alçıpan Kaplama",
                ArtisDb  = 8.0,
                Aciklama = "Mevcut duvara çift kat alçıpan ve ağır bariyer eklenmesi."
            });

            aday.Add(new CozumOnerisi
            {
                OneriAdi = "Bağımsız Karkas + Yün Dolgu",
                ArtisDb  = 15.0,
                Aciklama = "Mevcut duvara temas etmeyen ayrık metal karkas + çift alçıpan + 50 mm taşyünü."
            });
        }
        else if (bolme.YapiElemani.Tip == YapiElemaniTipi.Doseme)
        {
            aday.Add(new CozumOnerisi
            {
                OneriAdi = "Halı / Yumuşak Zemin Kaplaması",
                ArtisDb  = 10.0,
                Aciklama = "ΔLw ≥ 10 dB sınıfı elastik zemin kaplaması (EN ISO 10140-3)."
            });

            aday.Add(new CozumOnerisi
            {
                OneriAdi = "Yüzer Döşeme Uygulaması",
                ArtisDb  = 25.0,
                Aciklama = "Betonarme döşeme üzerine 2 cm taşyünü ve 5 cm şap uygulanması."
            });
        }

        // M5 — Uyarılar her zaman gelir; aksiyon önerilerinden yalnızca gerekli artışı sağlayanlar gelir.
        // gerekenArtis ≤ 0 ise (bölme zaten yeterli) tüm aksiyon önerileri geri tutulur.
        foreach (var o in aday)
        {
            if (o.Uyari) { oneriler.Add(o); continue; }
            if (gerekenArtis <= 0) continue;
            if (o.ArtisDb + 0.5 >= gerekenArtis) oneriler.Add(o);
        }

        // Hiçbir tek öneri yetmiyorsa, en yüksek artışlıyı yine de göster + uyarı düş.
        if (gerekenArtis > 0 && oneriler.All(o => o.Uyari))
        {
            var enIyi = aday.Where(o => !o.Uyari)
                            .OrderByDescending(o => o.ArtisDb)
                            .FirstOrDefault();
            if (enIyi != null)
            {
                oneriler.Add(enIyi);
                oneriler.Add(new CozumOnerisi
                {
                    OneriAdi = "Kombine Çözüm Gerekli",
                    ArtisDb  = 0,
                    Uyari    = true,
                    Aciklama = $"Tek bir öneri {gerekenArtis:F0} dB iyileştirmeye yetmiyor. " +
                               "Birden fazla önerinin (örn. ilave kaplama + bağımsız karkas) birleşimi gerekebilir."
                });
            }
        }

        return oneriler;
    }
}
