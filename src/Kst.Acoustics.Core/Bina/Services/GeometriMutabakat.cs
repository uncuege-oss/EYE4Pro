using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Bina.Models;

namespace Kst.Acoustics.Core.Bina.Services;

/// <summary>
/// Geometriye (OdaId) bağlı tek bir modül kaydı. Mutabakat raporunda
/// "askıda" (yeni geometride karşılığı olmayan) kayıtları betimler.
/// </summary>
/// <param name="Tur">Veri türü etiketi: "Eleman", "Devre", "Mahal satırı" vb.</param>
/// <param name="Aciklama">Kullanıcıya gösterilecek kısa tanım.</param>
/// <param name="OdaId">Bağlı olunan oda Id'si; null ise KatId üzerinden karşılaştırılabilir.</param>
/// <param name="KatAd">Kaydın bulunduğu katın adı (diyalogda bağlam için).</param>
/// <param name="KatId">Oda bağı olmayan kat-seviyesi kayıtların bağlı olduğu kat.</param>
public sealed record GeometriBagimliKayit(
    string Tur, string Aciklama, string? OdaId, string KatAd, string? KatId = null);

/// <summary>
/// Geometriye bağımlı veri taşıyan her modül (M12 elemanlar/devreler/mahal,
/// Faz 4'te M08 malzeme atamaları) bu arayüzü uygular; güncellenmiş `.e4bina`
/// yüklenmeden önce mutabakat raporu bu kayıtlarla üretilir.
/// </summary>
public interface IGeometriBagimliVeri
{
    IEnumerable<GeometriBagimliKayit> BagimliKayitlar();
}

public sealed class GeometriMutabakatRaporu
{
    public IReadOnlyList<GeometriBagimliKayit> AskidaKayitlar { get; }
    public int ToplamBagimliKayit { get; }

    public GeometriMutabakatRaporu(IReadOnlyList<GeometriBagimliKayit> askida, int toplam)
    {
        AskidaKayitlar = askida;
        ToplamBagimliKayit = toplam;
    }

    public bool AskidaVeriVar => AskidaKayitlar.Count > 0;

    /// "2 Eleman, 1 Mahal satırı askıda kalacak" biçiminde özet.
    public string Ozet()
    {
        if (!AskidaVeriVar) return "Askıda veri yok.";
        var gruplar = AskidaKayitlar
            .GroupBy(k => k.Tur)
            .Select(g => $"{g.Count()} {g.Key}");
        return string.Join(", ", gruplar) + " askıda kalacak.";
    }
}

/// Yeni geometri ile modül kayıtlarını karşılaştırır. Kat bağlamı bulunan oda
/// kayıtları OdaId+KatId çiftiyle eşleşir; oda başka kata taşındığında eski kattaki
/// domain verisi sessizce geçerli sayılmaz ve kullanıcıya mutabakat olarak gösterilir.
public static class GeometriMutabakatServisi
{
    public static GeometriMutabakatRaporu Karsilastir(
        BinaGeometri yeniGeo, IEnumerable<GeometriBagimliKayit> kayitlar)
    {
        var odaIdleri = new HashSet<string>(
            yeniGeo.Katlar.SelectMany(k => k.Odalar).Select(o => o.Id));
        var katIdleri = new HashSet<string>(yeniGeo.Katlar.Select(k => k.Id));
        var katOdaCiftleri = new HashSet<(string KatId, string OdaId)>(
            yeniGeo.Katlar.SelectMany(k => k.Odalar.Select(o => (k.Id, o.Id))));

        var liste = kayitlar.Where(k =>
            !string.IsNullOrWhiteSpace(k.OdaId) || !string.IsNullOrWhiteSpace(k.KatId)).ToList();
        var askida = liste
            .Where(k => !string.IsNullOrWhiteSpace(k.OdaId)
                ? !string.IsNullOrWhiteSpace(k.KatId)
                    ? !katOdaCiftleri.Contains((k.KatId!, k.OdaId!))
                    : !odaIdleri.Contains(k.OdaId!)
                : !string.IsNullOrWhiteSpace(k.KatId) && !katIdleri.Contains(k.KatId!))
            .ToList();

        return new GeometriMutabakatRaporu(askida, liste.Count);
    }
}
