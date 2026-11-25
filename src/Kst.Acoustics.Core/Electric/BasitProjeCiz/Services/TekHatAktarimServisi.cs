using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// <summary>
/// BasitProjeCiz projesini (DXF planı, panolar, linyeler, besleme hatları) Tek Hat
/// Şeması (M11) <see cref="ElektrikProje"/> modeline dönüştürür. Faz 0 sözleşmesi:
/// sadece kuvvetli akım; her pano hattı bir çıkış sigortası (Linye); sigorta amperi
/// Tek Hat tarafında yeniden hesaplanır; kablo boyu gerçek güzergahtan gelir; her
/// aktarım sıfırdan yeni proje üretir. Kompanzasyon/jeneratör Tek Hat'ta sonradan.
/// </summary>
public static class TekHatAktarimServisi
{
    public sealed class Sonuc
    {
        public ElektrikProje? Proje { get; init; }
        public string? Hata { get; init; }
        public List<string> Uyarilar { get; } = new();
        public bool Basarili => Proje != null;
    }

    public static Sonuc Donustur(BasitProjeCizProje kaynak, IEnumerable<BpcArmatürTanim> armaturTanimlari)
    {
        var tanimlar = armaturTanimlari?.ToList() ?? new List<BpcArmatürTanim>();

        // 1) Tüm kat panolarını topla
        var panoKayitlari = new List<(BasitProjeEleman El, BasitProjeKat Kat)>();
        foreach (var kat in kaynak.Katlar)
            foreach (var el in kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu))
                panoKayitlari.Add((el, kat));

        if (panoKayitlari.Count == 0)
            return new Sonuc { Hata = "Projede kat panosu yok; aktarım yapılamaz." };

        // 2) Ana pano tespiti — eleman rolü ve besleme grafiğinin kökü birlikte
        // değerlendirilir. Listeye ilk eklenen AnaPanoId ara kat panosu olabilir.
        var anaPanoId = AnaPanoIdBul(kaynak, panoKayitlari, out var anaPanoHatasi);
        if (string.IsNullOrEmpty(anaPanoId))
            return new Sonuc { Hata = anaPanoHatasi ?? "Ana pano belirlenemedi." };

        // 3) Pano içerik özetleri (eleman Id -> özet); tüm A/P/M linyeler (aktifPafta=null)
        var ozetById = BasitProjeCizPanoServisi.ProjePanoOzetleri(kaynak, tanimlar).ToDictionary(o => o.Pano.Id);

        // 4) Bina konfigürasyonu (kat numaralarından)
        var katNolar = panoKayitlari.Select(p => p.Kat.KatNo).Distinct().ToList();
        var bina = new BinaKonfigurasyonu
        {
            BodrumSayisi = katNolar.Where(n => n < 0).Select(n => -n).DefaultIfEmpty(0).Max(),
            ZeminVar = katNolar.Contains(0),
            NormalKatSayisi = katNolar.Where(n => n > 0).DefaultIfEmpty(0).Max(),
            CatiKati = 0
        };

        // 5) Panoları oluştur
        var proje = new ElektrikProje { ProjeAdi = kaynak.ProjeAdi, Bina = bina };
        var sonuc = new Sonuc { Proje = proje };
        var kullanilanAdlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ana Pano" };
        var panoById = new Dictionary<string, Pano>();
        var parentIdByPanoId = ParentHaritasi(kaynak, anaPanoId);
        Pano? anaPano = null;

        foreach (var (el, kat) in panoKayitlari)
        {
            bool ana = el.Id == anaPanoId;
            string katLabel = KatLabel(kat.KatNo);
            string ad = ana ? "Ana Pano" : BenzersizAd(el, katLabel, kullanilanAdlar);

            var pano = new Pano
            {
                Ad = ad,
                Kat = katLabel,
                PanoTipi = ana ? PanoTipi.AnaPano : PanoTipi.TaliPano,
                UstPanoAd = null
            };

            if (ozetById.TryGetValue(el.Id, out var ozet))
            {
                pano.GirisSigortaA = ozet.GirisSigortaA;
                if (el.AydinlatmaSigortalari?.GetValueOrDefault(AydinlatmaTasarimServisi.Giris) is { Manuel: true } giris)
                    pano.ManuelGirisSigortaA = giris.Amper;
                // BK linyeleri M12 pano özetinde görünür, ancak M11'de AltPanolar
                // hiyerarşisi aynı yükü zaten toplar. Normal linye gibi aktarılırsa
                // alt pano gücü her seviyede ikinci kez eklenir.
                foreach (var lo in ozet.Linyeler.Where(l => l.LinyePrefix != "BK"))
                {
                    var linye = LinyeYap(lo);
                    if (el.AydinlatmaSigortalari?.GetValueOrDefault(lo.BaglantiId) is { Manuel: true } elle)
                        linye.ManuelSigortaAmper = elle.Amper;
                    pano.Linyeler.Add(linye);
                }
            }

            if (!ana)
            {
                // Kablo boyu, panonun SEÇİLEN elektriksel ebeveynine giden hattan
                // hesaplanır; eski projelerde kalan ikinci (tali→ana) hattı çift
                // saymaya sokmaz.
                var parentId = parentIdByPanoId.TryGetValue(el.Id, out var pid) ? pid : anaPanoId;
                var hat = kaynak.Katlar.SelectMany(k => k.PanoBeslemeHatlari)
                    .FirstOrDefault(h => h.AnaPanoId == parentId && h.TaliPanoId == el.Id && h.UygulamaTipi == BpcPanoBeslemeUygulamaTipi.Kablo);
                if (hat != null && ozetById.TryGetValue(parentId, out var ust) &&
                    ust.Pano.AydinlatmaSigortalari?.GetValueOrDefault(hat.Id) is { } cikis)
                {
                    pano.UstPanoCikisSigortaA = cikis.Amper;
                    if (cikis.Manuel) pano.ManuelUstPanoCikisSigortaA = cikis.Amper;
                }
                var boy = BeslemeUzunlugu(kaynak, el.Id, parentId);
                pano.KabloMesafeM = boy ?? 10.0;
                if (boy is null)
                    sonuc.Uyarilar.Add($"'{ad}' için besleme hattı yok; kablo boyu 10 m varsayıldı.");
            }

            proje.TumPanolar.Add(pano);
            panoById[el.Id] = pano;
            if (ana) anaPano = pano;
        }

        proje.KokPano = anaPano;

        // 6) Hiyerarşi (besleme hatlarına göre tali→üst) ve ToplamGucW toplaması
        foreach (var pano in proje.TumPanolar)
            pano.ToplamGucW = pano.Linyeler.Sum(l => l.ToplamGucW);

        foreach (var (panoId, pano) in panoById)
        {
            if (pano == anaPano) continue;

            var parentId = parentIdByPanoId.TryGetValue(panoId, out var pid) ? pid : anaPanoId;
            if (string.IsNullOrWhiteSpace(parentId) || !panoById.TryGetValue(parentId, out var parent) || parent == pano)
                parent = anaPano;
            if (parent == null) continue;

            pano.UstPano = parent;
            pano.UstPanoAd = parent.Ad;
            if (!parent.AltPanolar.Contains(pano))
                parent.AltPanolar.Add(pano);
        }

        if (anaPano != null)
            HesaplaToplamGuc(anaPano, new HashSet<Pano>());

        return sonuc;
    }

    private static string? AnaPanoIdBul(
        BasitProjeCizProje kaynak,
        IReadOnlyList<(BasitProjeEleman El, BasitProjeKat Kat)> panoKayitlari,
        out string? hata)
    {
        hata = null;
        if (panoKayitlari.Count == 1) return panoKayitlari[0].El.Id;

        var panoIdleri = panoKayitlari.Select(p => p.El.Id).ToHashSet();
        var hatlar = kaynak.Katlar
            .SelectMany(k => k.PanoBeslemeHatlari)
            .Where(h => panoIdleri.Contains(h.AnaPanoId) && panoIdleri.Contains(h.TaliPanoId) &&
                        h.AnaPanoId != h.TaliPanoId)
            .ToList();

        var rolAnaIdleri = panoKayitlari
            .Where(p => BasitProjeCizKabloServisi.PanoAnaMi(p.El))
            .Select(p => p.El.Id)
            .Distinct()
            .ToList();
        var taliIdleri = hatlar.Select(h => h.TaliPanoId).ToHashSet();
        var grafKokleri = hatlar
            .Select(h => h.AnaPanoId)
            .Where(id => !taliIdleri.Contains(id))
            .Distinct()
            .ToList();

        if (rolAnaIdleri.Count == 1) return rolAnaIdleri[0];

        var rolVeGrafKoku = rolAnaIdleri.Where(grafKokleri.Contains).ToList();
        if (rolVeGrafKoku.Count == 1) return rolVeGrafKoku[0];
        if (rolAnaIdleri.Count > 1)
        {
            hata = "Birden fazla ana pano işaretli; tek hat aktarımı için bir kök pano bırakın.";
            return null;
        }

        if (grafKokleri.Count == 1) return grafKokleri[0];
        hata = grafKokleri.Count > 1
            ? "Besleme grafiğinde birden fazla kök pano var; ana pano rolünü açıkça belirleyin."
            : "Ana pano belirlenemedi. Besleme grafiğini ve olası çevrimleri kontrol edin.";
        return null;
    }

    private static Linye LinyeYap(PanoLinyeOzet lo)
    {
        int sorti = Math.Max(1, lo.ElemanSayisi);
        double birim = lo.ElemanSayisi > 0 ? lo.GucW / lo.ElemanSayisi : lo.GucW;
        return new Linye
        {
            LinyeNo = lo.Etiket,
            YukTipiAd = YukTipiAd(lo.LinyePrefix),
            FazTipi = lo.Faz,
            SortiSayisi = sorti,
            BirimGucW = birim,
            TalepFaktoru = 1.0,
            SigortaCinsi = SigortaCinsiCevir(lo.SigortaCinsi),
            SigortaAmper = lo.SigortaA
        };
    }

    private static string YukTipiAd(string prefix) => prefix switch
    {
        "A" => "Aydınlatma",
        "P" => "Priz",
        "M" => "Mekanik",
        "Y" => "Yedek",
        _ => "Özel Yük"
    };

    private static SigortaCinsi SigortaCinsiCevir(string? cins) => cins?.ToUpperInvariant() switch
    {
        "C" => SigortaCinsi.C,
        "D" => SigortaCinsi.D,
        _ => SigortaCinsi.B
    };

    private static string KatLabel(int katNo) =>
        katNo < 0 ? $"-{-katNo}" : katNo == 0 ? "Z" : katNo.ToString();

    private static string BenzersizAd(BasitProjeEleman el, string katLabel, HashSet<string> kullanilan)
    {
        string baseAd = !string.IsNullOrWhiteSpace(el.EkranAdi) ? el.EkranAdi
            : !string.IsNullOrWhiteSpace(el.Ad) ? el.Ad : "Pano";
        string ad = $"{baseAd} ({katLabel})";
        string sonuc = ad;
        int i = 2;
        while (kullanilan.Contains(sonuc)) sonuc = $"{ad} #{i++}";
        kullanilan.Add(sonuc);
        return sonuc;
    }

    /// Bir tali panonun besleme boyu. <paramref name="parentId"/> verilirse yalnız
    /// o ebeveyne giden hat(lar) toplanır (katlar arası riser iki parçadan oluşabilir);
    /// null ise tüm hatlar toplanır (eski davranış).
    private static double? BeslemeUzunlugu(BasitProjeCizProje kaynak, string taliPanoId, string? parentId = null)
    {
        double toplam = 0;
        bool bulundu = false;
        foreach (var kat in kaynak.Katlar)
            foreach (var h in kat.PanoBeslemeHatlari.Where(h => h.TaliPanoId == taliPanoId
                         && (parentId == null || h.AnaPanoId == parentId)))
            {
                bulundu = true;
                toplam += RotaUzunlugu(h.Rota);
            }
        return bulundu ? toplam : null;
    }

    /// Tali pano → üst pano ebeveyn haritası. Bir tali pano birden çok besleme hattı
    /// taşıyabilir (eski projeler hem tali→kat hem tali→ana çizmiş olabilir). Elektriksel
    /// ebeveyn tektir: ara (kat) panosu, doğrudan ana panoya tercih edilir → böylece
    /// tali → kat → ana zinciri deterministik kurulur.
    private static Dictionary<string, string> ParentHaritasi(BasitProjeCizProje kaynak, string anaPanoId)
    {
        var adaylar = new Dictionary<string, List<string>>();
        foreach (var hat in kaynak.Katlar.SelectMany(k => k.PanoBeslemeHatlari))
        {
            if (string.IsNullOrWhiteSpace(hat.TaliPanoId) || string.IsNullOrWhiteSpace(hat.AnaPanoId))
                continue;
            if (hat.TaliPanoId == hat.AnaPanoId) continue; // kendine besleme yok say
            if (!adaylar.TryGetValue(hat.TaliPanoId, out var list))
                adaylar[hat.TaliPanoId] = list = new List<string>();
            if (!list.Contains(hat.AnaPanoId)) list.Add(hat.AnaPanoId);
        }

        var map = new Dictionary<string, string>();
        foreach (var (tali, list) in adaylar)
        {
            // Ana pano dışı bir ebeveyn (ara kat panosu) varsa onu seç; yoksa ana pano.
            var araEbeveyn = list.FirstOrDefault(id => id != anaPanoId);
            map[tali] = araEbeveyn ?? list[0];
        }
        return map;
    }

    private static double HesaplaToplamGuc(Pano pano, HashSet<Pano> ziyaret)
    {
        // Cevrim koruması: kullanıcı besleme hatlarını A→B ve B→A çizerse
        // sınırsız özyineleme oluşmasın (StackOverflow).
        if (!ziyaret.Add(pano))
            return pano.Linyeler.Sum(l => l.ToplamGucW);

        pano.ToplamGucW = pano.Linyeler.Sum(l => l.ToplamGucW)
            + pano.AltPanolar.Sum(alt => HesaplaToplamGuc(alt, ziyaret));
        return pano.ToplamGucW;
    }

    private static double RotaUzunlugu(List<Point2D> rota)
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
