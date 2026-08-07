using System.Collections.Generic;

namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// Kompanzasyon panosu hesap sonuçları.
/// Kondansatör kademeleri (6/12/18) ve isteğe bağlı 3 reaktör kademesi (SVC) ayrı tutulur.
/// </summary>
public class KompanzasyonSonucu
{
    public double ToplamAktifGucKW { get; set; }
    public double MevcutCosF { get; set; } = 0.85;
    public double HedefCosF { get; set; } = 0.99;
    public double GerekliQkVar { get; set; }

    /// <summary>Kondansatör kademe sayısı: 6, 12 veya 18 (röle desteği ile uyumlu).</summary>
    public int KondansatorKademeSayisi { get; set; } = 6;

    /// <summary>Kondansatör kademeleri — KondansatorKademeSayisi kadar.</summary>
    public List<KompanzasyonKademe> KondansatorKademeleri { get; set; } = new();

    /// <summary>True ise reaktör (SVC) kademeleri aktif — daima 3 kademe.</summary>
    public bool ReaktorAktif { get; set; }

    /// <summary>Reaktör akort yüzdesi (p): tipik 5.67 (210Hz), 7 (189Hz) veya 14 (134Hz).
    /// Kademe reaktörü kütüphaneden bu akorda en yakın olana göre seçilir.</summary>
    public double ReaktorAkortYuzde { get; set; } = 7.0;

    /// <summary>Reaktör kademeleri — ReaktorAktif true ise 3 kademe.</summary>
    public List<KompanzasyonReaktorKademe> ReaktorKademeleri { get; set; } = new();

    /// <summary>SVC yük sürücü (reaktör toplam kVAr'dan büyük olmalı).</summary>
    public SvcSurucuTanim? SecilenYukSurucu { get; set; }

    public double ToplamKondansatorKVar { get; set; }
    public double ToplamReaktorKVar { get; set; }

    /// <summary>Toplam kademe sayısı = kondansatör + (reaktör aktifse 3).</summary>
    public int ToplamKademeSayisi => KondansatorKademeleri.Count + ReaktorKademeleri.Count;

    /// <summary>
    /// True ise Hesapla kademe listelerini yeniden üretmez; sadece toplamları günceller.
    /// </summary>
    public bool KademelerElleAyarlandi { get; set; }
}

/// <summary>
/// Tek bir kondansatör kademesi.
/// </summary>
public class KompanzasyonKademe
{
    public int KademeNo { get; set; }
    public double KVar { get; set; }
    public string KondansatorAd { get; set; } = "";
    public string FazTipi { get; set; } = "Trifaze";
}

/// <summary>
/// Tek bir reaktör (SVC) kademesi — kondansatörden ayrı bir devre kademesidir.
/// </summary>
public class KompanzasyonReaktorKademe
{
    public int KademeNo { get; set; }
    public string ReaktorAd { get; set; } = "";
    public double KVar { get; set; }
    public double L_mH { get; set; }
    public double AkortFrekansHz { get; set; }
}
