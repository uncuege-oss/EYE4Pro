using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// <summary>
/// M12'nin geometriye bağımlı verisini mutabakat için bildirir (3.4):
/// - Elemanlar: doğrudan `OdaId` bağı
/// - Devreler: anahtar/buat/armatür elemanları üzerinden dolaylı oda bağı
/// - Mahal listesi: satır hücreleri oda Id'siyle anahtarlanır
/// </summary>
public sealed class BasitProjeGeometriBagimliVeri : IGeometriBagimliVeri
{
    private readonly BasitProjeCizProje _proje;

    public BasitProjeGeometriBagimliVeri(BasitProjeCizProje proje) => _proje = proje;

    public IEnumerable<GeometriBagimliKayit> BagimliKayitlar()
    {
        foreach (var kat in _proje.Katlar)
        {
            var elemanlar = kat.Elemanlar.ToDictionary(e => e.Id);
            var odaAdlari = kat.Odalar.ToDictionary(o => o.Id, o => o.Ad);

            string OdaAd(string? odaId) =>
                odaId is not null && odaAdlari.TryGetValue(odaId, out var ad) ? ad : odaId ?? "?";

            // ── Elemanlar ────────────────────────────────────────────────────
            foreach (var el in kat.Elemanlar)
            {
                if (string.IsNullOrEmpty(el.OdaId)) continue;
                string etiket = !string.IsNullOrWhiteSpace(el.EkranAdi) ? el.EkranAdi
                    : !string.IsNullOrWhiteSpace(el.Ad) ? el.Ad
                    : el.Tip.ToString();
                yield return new GeometriBagimliKayit(
                    "Eleman", $"{etiket} → {OdaAd(el.OdaId)}", el.OdaId, kat.Ad, kat.Id);
            }

            // ── Devreler (elemanları üzerinden dolaylı oda bağı) ─────────────
            int devreSira = 0;
            foreach (var devre in kat.Devreler)
            {
                devreSira++;
                var iliskiliOdalar = DevreOdalari(devre, elemanlar).Distinct().ToList();
                foreach (var odaId in iliskiliOdalar)
                {
                    yield return new GeometriBagimliKayit(
                        "Devre", $"Devre {devreSira} → {OdaAd(odaId)}", odaId, kat.Ad, kat.Id);
                }
            }

            // ── Mahal listesi hücreleri ──────────────────────────────────────
            foreach (var satir in kat.MahalListesi)
            {
                var odaIdleri = satir.Sayilar.Keys
                    .Concat(satir.HucreAyarlari.Keys)
                    .Distinct();
                foreach (var odaId in odaIdleri)
                {
                    yield return new GeometriBagimliKayit(
                        "Mahal satırı", $"{satir.Tip} → {OdaAd(odaId)}", odaId, kat.Ad, kat.Id);
                }
            }

            // Oda Id'si taşımayan fakat kat silinirse kaybolacak M12 verileri.
            // Kat kimliği mutabakat servisine bu kayıtları da görünür kılar.
            if (kat.Kablolar.Count > 0)
                yield return KatKaydi("Kablo", kat.Kablolar.Count, kat);
            if (kat.VavienGruplari.Count > 0)
                yield return KatKaydi("Vavien grubu", kat.VavienGruplari.Count, kat);
            if (kat.BuatPanoBaglantilari.Count > 0)
                yield return KatKaydi("Buat-pano bağlantısı", kat.BuatPanoBaglantilari.Count, kat);
            if (kat.PrizDevreleri.Count > 0)
                yield return KatKaydi("Priz devresi", kat.PrizDevreleri.Count, kat);
            if (kat.MekanikDevreleri.Count > 0)
                yield return KatKaydi("Mekanik devre", kat.MekanikDevreleri.Count, kat);
            if (kat.KabloTavalari.Count > 0)
                yield return KatKaydi("Kablo tavası", kat.KabloTavalari.Count, kat);
            if (kat.PanoBeslemeHatlari.Count > 0)
                yield return KatKaydi("Pano besleme hattı", kat.PanoBeslemeHatlari.Count, kat);
            if (kat.SistemDevreleri.Count > 0)
                yield return KatKaydi("Sistem devresi", kat.SistemDevreleri.Count, kat);
            if (TopraklamaVerisiVar(kat.Topraklama))
                yield return new GeometriBagimliKayit(
                    "Topraklama", "Kat topraklama verisi", null, kat.Ad, kat.Id);
        }
    }

    private static GeometriBagimliKayit KatKaydi(string tur, int adet, BasitProjeKat kat)
        => new(tur, $"{adet} kayıt", null, kat.Ad, kat.Id);

    private static bool TopraklamaVerisiVar(BpcTopraklama? topraklama)
        => topraklama is not null &&
           ((topraklama.SecimNoktalari?.Count ?? 0) > 0 ||
            (topraklama.Seritler?.Count ?? 0) > 0 ||
            (topraklama.Kaziklar?.Count ?? 0) > 0 ||
            topraklama.EkTopraklamaVar);

    private static IEnumerable<string> DevreOdalari(
        BpcDevre devre, IReadOnlyDictionary<string, BasitProjeEleman> elemanlar)
    {
        string? Oda(string? elemanId) =>
            elemanId is not null && elemanlar.TryGetValue(elemanId, out var el) ? el.OdaId : null;

        if (Oda(devre.AnahtarElemanId) is { } a) yield return a;
        if (Oda(devre.BuatId) is { } b) yield return b;
        foreach (var armId in devre.ArmaturElemanIds)
            if (Oda(armId) is { } o) yield return o;
    }
}
