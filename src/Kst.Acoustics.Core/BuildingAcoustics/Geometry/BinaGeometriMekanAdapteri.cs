using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Parsers;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Geometry;

public sealed record M08ImportSonucu(
    int YeniKat, int GuncellenenKat, int KorunanEskiKat, List<string> Uyarilar);

/// <summary>
/// M50 → M08 köprüsü (4.1): BinaGeometri'yi BinaProje'nin Kat/Mekan modeline uygular
/// ve her güncellenen katta <see cref="OdaTespitServisi.KomsuluklariTespitEt"/> koşarak
/// bölmeleri yeniden üretir.
///
/// "Mekan akustik alanlara inceltilir": adapter yalnız GEOMETRİ alanlarını yazar
/// (Ad, Poligon, Alan, Yükseklik). Akustik alanlar (Sinif, KaynakDerecesi, AliciSinifi,
/// RT60, EmilimAlani, BagimsizBolumId, zemin/tavan atamaları) Id eşleşen mekanlarda
/// AYNEN korunur; bölme YapiElemani/Acikliklar aktarımını KomsuluklariTespitEt'in
/// geometrik durum-koruması yapar.
///
/// Id kararlılığı: Mekan.Id, BinaOda.Id'den deterministik üretilir (Guid ise doğrudan,
/// değilse MD5 tabanlı kararlı Guid) — geometri yeniden yüklendiğinde eşleşme bozulmaz.
/// Kat eşleşmesi kararlı BinaKat.Id iledir; eski M08 belgelerinde yalnız ilk
/// aktarım için KatNo geri dönüşü kullanılır. Kat sırası M50 koleksiyon sırasından
/// korunur.
/// </summary>
public static class BinaGeometriMekanAdapteri
{
    public static M08ImportSonucu Uygula(BinaProje proje, BinaGeometri geo, string? dosyaYolu = null)
    {
        var uyarilar = new List<string>();
        int yeni = 0, guncellenen = 0, korunan = 0;

        // 4.3 — Bağlantı bilgisi: yol + revizyon damgası + gömülü kopya
        if (dosyaYolu is not null) proje.BinaGeometriYolu = dosyaYolu;
        proje.BinaGeometriRevizyon = geo.GeometriRevizyon;
        proje.BinaGeometriKopya = geo;

        var geoKatIdleri = geo.Katlar.Select(k => k.Id).ToHashSet(StringComparer.Ordinal);
        var geoKatNolari = new HashSet<int>(geo.Katlar.Select(k => k.KatNo));
        var geoKatSirasi = geo.Katlar
            .Select((k, index) => (k.Id, Index: index))
            .ToDictionary(x => x.Id, x => x.Index, StringComparer.Ordinal);

        // Oda bir katın KatNo'su değiştiği için başka Kat nesnesine taşınsa bile
        // M08'in akustik alanlarını aynı Mekan nesnesi üzerinden koru.
        var tumEskiMekanlar = proje.Katlar
            .SelectMany(k => k.Mekanlar)
            .GroupBy(m => m.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var odaHedefKatlari = geo.Katlar
            .SelectMany(k => k.Odalar.Select(o => (OdaId: OdaIdToGuid(o.Id), KatId: k.Id)))
            .GroupBy(x => x.OdaId)
            .ToDictionary(g => g.Key, g => g.Single().KatId);

        // Kararlı bir oda Id'si başka kata taşındıysa aynı Mekan nesnesini önce eski
        // sahibinden ayır. Aksi halde geometride silinmiş fakat atama taşıdığı için
        // korunan bir kat ile yeni hedef kat aynı nesneyi/Id'yi birlikte tutabilir.
        foreach (var eskiKat in proje.Katlar)
        {
            string? eslesenGeoKatId = !string.IsNullOrWhiteSpace(eskiKat.BinaGeometriKatId)
                ? eskiKat.BinaGeometriKatId
                : geo.Katlar.FirstOrDefault(k => k.KatNo == eskiKat.KatNo)?.Id;
            for (int i = eskiKat.Mekanlar.Count - 1; i >= 0; i--)
            {
                var mekan = eskiKat.Mekanlar[i];
                if (odaHedefKatlari.TryGetValue(mekan.Id, out var hedefKatId) &&
                    !string.Equals(eslesenGeoKatId, hedefKatId, StringComparison.Ordinal))
                    eskiKat.Mekanlar.RemoveAt(i);
            }
        }

        // Geometride olmayan eski katlar: atama taşıyan mekanı yoksa kaldır, varsa koru + uyar.
        foreach (var eski in proje.Katlar.Where(k =>
                     !string.IsNullOrWhiteSpace(k.BinaGeometriKatId)
                         ? !geoKatIdleri.Contains(k.BinaGeometriKatId)
                         : !geoKatNolari.Contains(k.KatNo)).ToList())
        {
            bool atamaVar = eski.Mekanlar.Any(m =>
                m.ZeminYapiElemani != null || m.TavanYapiElemani != null ||
                m.Bolmeler.Any(b => b.YapiElemani != null));
            if (!atamaVar)
            {
                proje.Katlar.Remove(eski);
            }
            else
            {
                // Bu kat artık güncel M50 koleksiyonunun üyesi değildir; yalnız
                // akustik/domain verisini kurtarmak için tutulur. Eski KatId'yi canlı
                // geometri bağlantısı gibi bırakmak düşey komşulukta hayalet kat üretir.
                eski.BinaGeometriKatId = "";
                korunan++;
                uyarilar.Add($"'{eski.Ad}' katı yeni geometride yok; malzeme ataması taşıdığı için korundu.");
            }
        }

        var odaServisi = new OdaTespitServisi();

        foreach (var geoKat in geo.Katlar)
        {
            var hedef = proje.Katlar.FirstOrDefault(k =>
                !string.IsNullOrWhiteSpace(k.BinaGeometriKatId) &&
                string.Equals(k.BinaGeometriKatId, geoKat.Id, StringComparison.Ordinal));
            // Eski M08 projelerinde kararlı kimlik henüz yoktur. İlk aktarımda
            // yalnız KatNo üzerinden eşleştirip hemen BinaGeometriKatId'yi damgala.
            hedef ??= proje.Katlar.FirstOrDefault(k =>
                string.IsNullOrWhiteSpace(k.BinaGeometriKatId) && k.KatNo == geoKat.KatNo);
            if (hedef is null)
            {
                hedef = new Kat { KatNo = geoKat.KatNo, BinaGeometriKatId = geoKat.Id };
                proje.Katlar.Add(hedef);
                yeni++;
            }
            else
            {
                guncellenen++;
            }

            KatiGuncelle(hedef, geoKat, tumEskiMekanlar);

            // Bölme üretimi — komşuluklar geometriden yeniden tespit edilir;
            // önceki YapiElemani/Acikliklar geometrik yakınlıkla korunur.
            odaServisi.KomsuluklariTespitEt(hedef.Mekanlar);

            // 4.2 — Açıklıklar M50'den yeniden yerleştirilir; M08'in sahiplendiği
            // tek veri olan AciklikId → YapiElemani eşlemesi korunur.
            AcikliklariEsitle(hedef, geoKat);
        }

        proje.Katlar.Sort((a, b) =>
        {
            bool aGeo = geoKatSirasi.TryGetValue(a.BinaGeometriKatId, out var aIndex);
            bool bGeo = geoKatSirasi.TryGetValue(b.BinaGeometriKatId, out var bIndex);
            if (aGeo && bGeo) return aIndex.CompareTo(bIndex);
            if (aGeo) return -1;
            if (bGeo) return 1;
            return a.KatNo.CompareTo(b.KatNo);
        });

        // 4.4 — Patlatma M50 modeline bağlanır: geometrideki düşey birleşimler
        // M08 modeline indirgenir. M50 tek geometri kaynağıdır; listede artık
        // bulunmayan birleşimler M08'de tutulmaz.
        proje.DuseyMekanBirlesimleri.Clear();
        foreach (var b in geo.DuseyBirlesimleri)
        {
            var id = OdaIdToGuid(b.Id);
            var hedefBirlesim = new DuseyMekanBirlesimi { Id = id };
            proje.DuseyMekanBirlesimleri.Add(hedefBirlesim);
            hedefBirlesim.AktifMi = b.AktifMi;
            hedefBirlesim.UstKatNo = b.UstKatNo;
            hedefBirlesim.AltKatNo = b.AltKatNo;
            hedefBirlesim.Uyeler = b.Uyeler.Select(u => new DuseyMekanBirlesimUyesi
            {
                KatId = u.KatId,
                KatNo = u.KatNo,
                MekanId = OdaIdToGuid(u.OdaId),
                TavanAcik = u.TavanAcik,
                DosemeAcik = u.DosemeAcik
            }).ToList();
        }

        return new M08ImportSonucu(yeni, guncellenen, korunan, uyarilar);
    }

    private static void KatiGuncelle(
        Kat hedef, BinaKat kaynak, IReadOnlyDictionary<Guid, Mekan> tumEskiMekanlar)
    {
        hedef.BinaGeometriKatId = kaynak.Id;
        hedef.KatNo = kaynak.KatNo;
        hedef.Ad = kaynak.Ad;
        hedef.KatYuksekligi = kaynak.KatYuksekligiM;
        hedef.DxfFilePath = kaynak.DxfFilePath;
        hedef.DxfGoster = kaynak.DxfGoster;
        hedef.BloklariGoster = kaynak.BloklariGoster;
        hedef.OriginOffset = new Point2D(kaynak.OriginOffset.X, kaynak.OriginOffset.Y);

        // DXF arka planı görüntü/snap için taşınır
        hedef.RawDxfSegments = kaynak.DxfSegmentler
            .Select(s => (s.A, s.B))
            .ToList();
        hedef.RawDxfArcs = kaynak.DxfArclar
            .Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg))
            .ToList();

        // Mekanlar: kararlı Id eşleşmesiyle birleştir — akustik alanlar korunur.
        var yeniListe = new List<Mekan>();
        foreach (var oda in kaynak.Odalar)
        {
            var id = OdaIdToGuid(oda.Id);
            if (!tumEskiMekanlar.TryGetValue(id, out var mekan))
            {
                mekan = new Mekan
                {
                    Id = id,
                    KullanimTipi = oda.Ad // sınıf otomatik türetilsin
                };
            }

            // Yalnız geometri alanları güncellenir
            mekan.Ad = oda.Ad;
            mekan.Poligon = oda.Poligon.Select(p => new Point2D(p.X, p.Y)).ToList();
            mekan.Alan = oda.AlanM2;
            mekan.Yukseklik = oda.YukseklikOverrideM ?? kaynak.KatYuksekligiM;

            yeniListe.Add(mekan);
        }

        hedef.Mekanlar.Clear();
        foreach (var m in yeniListe) hedef.Mekanlar.Add(m);
    }

    /// 4.2 — M50 açıklıklarını katın bölmelerine yerleştirir.
    /// Boyutlar (Genislik/Yukseklik/Esik) M50 geometrisinden okunur; M08'in
    /// sahiplendiği tek veri AciklikId → YapiElemani eşlemesidir ve yeniden
    /// uygulamada korunur. Elle eklenmiş (AciklikId'siz) açıklıklara dokunulmaz.
    private static void AcikliklariEsitle(Kat hedef, BinaKat kaynak)
    {
        // 1. Mevcut malzeme eşlemesini topla
        var atamalar = new Dictionary<string, YapiElemani>();
        foreach (var m in hedef.Mekanlar)
            foreach (var b in m.Bolmeler)
                foreach (var a in b.Acikliklar)
                    if (a.AciklikId is { } id && a.YapiElemani is { } ye)
                        atamalar[id] = ye;

        // 2. Geometriden gelen açıklıkları temizle (elle eklenenler kalır)
        foreach (var m in hedef.Mekanlar)
            foreach (var b in m.Bolmeler)
                b.Acikliklar.RemoveAll(a => a.GeometridenGelir);

        // 3. M50 açıklıklarını yeniden yerleştir. Poligonun üst/sağ sınırı gibi
        // ray-cast tarafından "dışarı" sayılan kenarlar da en yakın bölmeye
        // 20 cm tolerans içindeyse kabul edilir.
        foreach (var m in hedef.Mekanlar)
        {
            foreach (var a in kaynak.Acikliklar)
            {
                if (a.IptalEdildi) continue;
                var orta = new Point2D((a.Bas.X + a.Bit.X) * 0.5, (a.Bas.Y + a.Bit.Y) * 0.5);
                if (!PoligonYardimci.IsPointInside(orta, m.Poligon) &&
                    !PoligonSinirindaMi(orta, m.Poligon))
                    continue;
                var hedefBolme = m.Bolmeler
                    .Where(b => b.HasGeometry)
                    .Select(b => new { Bolme = b, D = PoligonYardimci.SegmentMesafesi(orta, b.Baslangic, b.Bitis) })
                    .OrderBy(x => x.D)
                    .FirstOrDefault();
                if (hedefBolme == null || hedefBolme.D > 0.20) continue;

                double yukseklik = a.YukseklikM > 0 ? a.YukseklikM
                    : BinaAciklik.VarsayilanYukseklik(a.Tip);

                // Terfi: aynı konumda elle eklenmiş (AciklikId'siz) açıklık varsa
                // çift kayıt oluşturma — elle kaydı düşür, malzemesini devral.
                // (Eski M08 projesinin ilk round-trip'inde elle açıklıklar M50
                // kimliği kazanır; hesaplardaki açıklık alanı değişmemiş olur.)
                var elleEski = hedefBolme.Bolme.Acikliklar.FirstOrDefault(x =>
                    !x.GeometridenGelir &&
                    KareMesafe(OrtaNokta(x.Baslangic, x.Bitis), orta) <= 0.10 * 0.10);
                YapiElemani? devralinan = null;
                if (elleEski is not null)
                {
                    devralinan = elleEski.YapiElemani;
                    hedefBolme.Bolme.Acikliklar.Remove(elleEski);
                }

                hedefBolme.Bolme.Acikliklar.Add(new BolmeAcikligi
                {
                    AciklikId = a.Id,
                    Tip = a.Tip == BinaAciklikTipi.Kapi
                        ? BolmeAcikligiTipi.Kapi
                        : BolmeAcikligiTipi.Pencere,
                    Baslangic = a.Bas,
                    Bitis = a.Bit,
                    Genislik = a.GenislikM,
                    Yukseklik = yukseklik,
                    EsikYuksekligiM = a.EsikYuksekligiM,
                    YapiElemani = atamalar.GetValueOrDefault(a.Id) ?? devralinan
                });
            }
        }
    }

    private static bool PoligonSinirindaMi(
        Point2D nokta, IReadOnlyList<Point2D> poligon, double tolerans = 1e-6)
    {
        if (poligon.Count < 2) return false;
        for (var i = 0; i < poligon.Count; i++)
        {
            var bitis = poligon[(i + 1) % poligon.Count];
            if (PoligonYardimci.SegmentMesafesi(nokta, poligon[i], bitis) <= tolerans)
                return true;
        }
        return false;
    }

    private static Point2D OrtaNokta(Point2D a, Point2D b) =>
        new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);

    private static double KareMesafe(Point2D a, Point2D b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    /// BinaOda.Id (string) → kararlı Mekan Guid'i. Guid biçimindeyse doğrudan
    /// parse edilir; değilse MD5 özetinden deterministik Guid üretilir.
    public static Guid OdaIdToGuid(string odaId)
    {
        if (Guid.TryParse(odaId, out var g)) return g;
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(odaId));
        return new Guid(hash);
    }
}
