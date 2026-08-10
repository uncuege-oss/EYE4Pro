using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Geometry;

public enum M50FarkNesnesi { Kat, Mekan, Aciklik, DuseyBirlesim }
public enum M50FarkTuru { Eklendi, Silindi, Degisti }

public sealed record M50RevizyonFarki(
    M50FarkNesnesi Nesne,
    M50FarkTuru Tur,
    string Kimlik,
    string Baslik,
    string Aciklama,
    string? EskiKatId = null,
    string? YeniKatId = null);

/// <summary>Kimliği değişmiş olabilecek bir eski/yeni oda için geometrik eşleme adayı.</summary>
public sealed record M50OdaEslestirmeAdayi(
    string EskiOdaId,
    string YeniOdaId,
    string EskiAd,
    string YeniAd,
    double OrtusmeOrani,
    double AlanBenzerligi,
    string EskiKatId,
    string YeniKatId)
{
    public double Guven => Math.Clamp(0.75 * OrtusmeOrani + 0.25 * AlanBenzerligi, 0, 1);
}

public sealed class M50RevizyonFarkRaporu
{
    public required int EskiRevizyon { get; init; }
    public required int YeniRevizyon { get; init; }
    public required IReadOnlyList<M50RevizyonFarki> Farklar { get; init; }
    public required IReadOnlyList<M50OdaEslestirmeAdayi> OdaEslestirmeAdaylari { get; init; }

    public int Eklenen => Farklar.Count(x => x.Tur == M50FarkTuru.Eklendi);
    public int Silinen => Farklar.Count(x => x.Tur == M50FarkTuru.Silindi);
    public int Degisen => Farklar.Count(x => x.Tur == M50FarkTuru.Degisti);
    public bool FarkVar => Farklar.Count > 0;
}

/// <summary>
/// İki M50 artifact'ını değiştirmeden karşılaştırır. Önce kararlı kimlikler kullanılır;
/// silinen/eklenen odalar için dünya koordinatındaki poligon örtüşmesinden yalnız bir
/// eşleme ÖNERİSİ üretilir. Öneri hiçbir zaman kendiliğinden uygulanmaz.
/// </summary>
public sealed class M50RevizyonFarkServisi
{
    private const double GeometriDegisimEsigi = 0.005;

    public M50RevizyonFarkRaporu Karsilastir(BinaGeometri eski, BinaGeometri yeni)
    {
        ArgumentNullException.ThrowIfNull(eski);
        ArgumentNullException.ThrowIfNull(yeni);

        var farklar = new List<M50RevizyonFarki>();
        KatFarklari(eski, yeni, farklar);
        MekanFarklari(eski, yeni, farklar);
        AciklikFarklari(eski, yeni, farklar);
        BirlesimFarklari(eski, yeni, farklar);

        return new M50RevizyonFarkRaporu
        {
            EskiRevizyon = eski.GeometriRevizyon,
            YeniRevizyon = yeni.GeometriRevizyon,
            Farklar = farklar,
            OdaEslestirmeAdaylari = EslestirmeAdaylari(eski, yeni),
        };
    }

    private static void KatFarklari(BinaGeometri eski, BinaGeometri yeni, List<M50RevizyonFarki> hedef)
    {
        var e = eski.Katlar.ToDictionary(k => k.Id, StringComparer.Ordinal);
        var y = yeni.Katlar.ToDictionary(k => k.Id, StringComparer.Ordinal);
        foreach (var kat in e.Values.Where(k => !y.ContainsKey(k.Id)))
            hedef.Add(new(M50FarkNesnesi.Kat, M50FarkTuru.Silindi, kat.Id, kat.Ad,
                $"Kat kaldırıldı ({kat.Odalar.Count} mekan, {kat.Acikliklar.Count} açıklık).", kat.Id));
        foreach (var kat in y.Values.Where(k => !e.ContainsKey(k.Id)))
            hedef.Add(new(M50FarkNesnesi.Kat, M50FarkTuru.Eklendi, kat.Id, kat.Ad,
                $"Yeni kat eklendi ({kat.Odalar.Count} mekan, {kat.Acikliklar.Count} açıklık).", null, kat.Id));
        foreach (var id in e.Keys.Intersect(y.Keys))
        {
            var a = e[id]; var b = y[id];
            var neden = new List<string>();
            if (!string.Equals(a.Ad, b.Ad, StringComparison.Ordinal)) neden.Add($"ad: {a.Ad} → {b.Ad}");
            if (a.KatNo != b.KatNo) neden.Add($"kat no: {a.KatNo} → {b.KatNo}");
            if (Math.Abs(a.KatYuksekligiM - b.KatYuksekligiM) > 0.001)
                neden.Add($"yükseklik: {a.KatYuksekligiM:F2} → {b.KatYuksekligiM:F2} m");
            if (Mesafe(a.OriginOffset, b.OriginOffset) > 0.001)
                neden.Add("hizalama ofseti değişti");
            if (neden.Count > 0)
                hedef.Add(new(M50FarkNesnesi.Kat, M50FarkTuru.Degisti, id, b.Ad,
                    string.Join("; ", neden), a.Id, b.Id));
        }
    }

    private static void MekanFarklari(BinaGeometri eski, BinaGeometri yeni, List<M50RevizyonFarki> hedef)
    {
        var e = OdaIndeksi(eski);
        var y = OdaIndeksi(yeni);
        foreach (var (id, x) in e.Where(x => !y.ContainsKey(x.Key)))
            hedef.Add(new(M50FarkNesnesi.Mekan, M50FarkTuru.Silindi, id, x.Oda.Ad,
                $"Mekan kaldırıldı ({x.Oda.AlanM2:F1} m²).", x.Kat.Id));
        foreach (var (id, x) in y.Where(x => !e.ContainsKey(x.Key)))
            hedef.Add(new(M50FarkNesnesi.Mekan, M50FarkTuru.Eklendi, id, x.Oda.Ad,
                $"Yeni mekan eklendi ({x.Oda.AlanM2:F1} m²).", null, x.Kat.Id));

        foreach (var id in e.Keys.Intersect(y.Keys))
        {
            var a = e[id]; var b = y[id];
            var neden = new List<string>();
            if (!string.Equals(a.Oda.Ad, b.Oda.Ad, StringComparison.Ordinal))
                neden.Add($"ad: {a.Oda.Ad} → {b.Oda.Ad}");
            if (a.Kat.Id != b.Kat.Id) neden.Add($"kat: {a.Kat.Ad} → {b.Kat.Ad}");
            if (Math.Abs(a.Oda.AlanM2 - b.Oda.AlanM2) > 0.01)
                neden.Add($"alan: {a.Oda.AlanM2:F1} → {b.Oda.AlanM2:F1} m²");
            var ortusme = OrtusmeOrani(DunyaPoligonu(a), DunyaPoligonu(b));
            if (ortusme < 1 - GeometriDegisimEsigi)
                neden.Add($"plan geometrisi değişti (örtüşme %{100 * ortusme:F0})");
            var ah = a.Oda.YukseklikOverrideM ?? a.Kat.KatYuksekligiM;
            var bh = b.Oda.YukseklikOverrideM ?? b.Kat.KatYuksekligiM;
            if (Math.Abs(ah - bh) > 0.001) neden.Add($"yükseklik: {ah:F2} → {bh:F2} m");
            if (neden.Count > 0)
                hedef.Add(new(M50FarkNesnesi.Mekan, M50FarkTuru.Degisti, id, b.Oda.Ad,
                    string.Join("; ", neden), a.Kat.Id, b.Kat.Id));
        }
    }

    private static void AciklikFarklari(BinaGeometri eski, BinaGeometri yeni, List<M50RevizyonFarki> hedef)
    {
        var e = AciklikIndeksi(eski);
        var y = AciklikIndeksi(yeni);
        foreach (var (id, x) in e.Where(x => !y.ContainsKey(x.Key)))
            hedef.Add(new(M50FarkNesnesi.Aciklik, M50FarkTuru.Silindi, id, x.Aciklik.Tip.ToString(),
                $"Açıklık kaldırıldı ({x.Aciklik.GenislikM:F2}×{x.Aciklik.YukseklikM:F2} m).", x.Kat.Id));
        foreach (var (id, x) in y.Where(x => !e.ContainsKey(x.Key)))
            hedef.Add(new(M50FarkNesnesi.Aciklik, M50FarkTuru.Eklendi, id, x.Aciklik.Tip.ToString(),
                $"Yeni açıklık eklendi ({x.Aciklik.GenislikM:F2}×{x.Aciklik.YukseklikM:F2} m).", null, x.Kat.Id));

        foreach (var id in e.Keys.Intersect(y.Keys))
        {
            var a = e[id]; var b = y[id];
            var neden = new List<string>();
            if (a.Kat.Id != b.Kat.Id) neden.Add($"kat: {a.Kat.Ad} → {b.Kat.Ad}");
            if (a.Aciklik.Tip != b.Aciklik.Tip) neden.Add($"tip: {a.Aciklik.Tip} → {b.Aciklik.Tip}");
            if (Math.Abs(a.Aciklik.GenislikM - b.Aciklik.GenislikM) > 0.001 ||
                Math.Abs(a.Aciklik.YukseklikM - b.Aciklik.YukseklikM) > 0.001)
                neden.Add($"boyut: {a.Aciklik.GenislikM:F2}×{a.Aciklik.YukseklikM:F2} → {b.Aciklik.GenislikM:F2}×{b.Aciklik.YukseklikM:F2} m");
            if (Mesafe(DunyaNoktasi(a.Kat, a.Aciklik.Bas), DunyaNoktasi(b.Kat, b.Aciklik.Bas)) > 0.01 ||
                Mesafe(DunyaNoktasi(a.Kat, a.Aciklik.Bit), DunyaNoktasi(b.Kat, b.Aciklik.Bit)) > 0.01)
                neden.Add("konum değişti");
            if (a.Aciklik.IptalEdildi != b.Aciklik.IptalEdildi) neden.Add("iptal durumu değişti");
            if (neden.Count > 0)
                hedef.Add(new(M50FarkNesnesi.Aciklik, M50FarkTuru.Degisti, id, b.Aciklik.Tip.ToString(),
                    string.Join("; ", neden), a.Kat.Id, b.Kat.Id));
        }
    }

    private static void BirlesimFarklari(BinaGeometri eski, BinaGeometri yeni, List<M50RevizyonFarki> hedef)
    {
        var e = eski.DuseyBirlesimleri.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var y = yeni.DuseyBirlesimleri.ToDictionary(x => x.Id, StringComparer.Ordinal);
        foreach (var x in e.Values.Where(x => !y.ContainsKey(x.Id)))
            hedef.Add(new(M50FarkNesnesi.DuseyBirlesim, M50FarkTuru.Silindi, x.Id, "Düşey birleşim",
                $"Düşey birleşim kaldırıldı ({x.Uyeler.Count} üye)."));
        foreach (var x in y.Values.Where(x => !e.ContainsKey(x.Id)))
            hedef.Add(new(M50FarkNesnesi.DuseyBirlesim, M50FarkTuru.Eklendi, x.Id, "Düşey birleşim",
                $"Yeni düşey birleşim eklendi ({x.Uyeler.Count} üye)."));
        foreach (var id in e.Keys.Intersect(y.Keys))
        {
            var a = e[id]; var b = y[id];
            var ak = a.Uyeler.Select(u => $"{u.KatId}/{u.OdaId}/{u.TavanAcik}/{u.DosemeAcik}").Order().ToArray();
            var bk = b.Uyeler.Select(u => $"{u.KatId}/{u.OdaId}/{u.TavanAcik}/{u.DosemeAcik}").Order().ToArray();
            if (a.AktifMi != b.AktifMi || !ak.SequenceEqual(bk))
                hedef.Add(new(M50FarkNesnesi.DuseyBirlesim, M50FarkTuru.Degisti, id, "Düşey birleşim",
                    "Üyeler veya açık döşeme/tavan durumu değişti."));
        }
    }

    private static IReadOnlyList<M50OdaEslestirmeAdayi> EslestirmeAdaylari(BinaGeometri eski, BinaGeometri yeni)
    {
        var e = OdaIndeksi(eski);
        var y = OdaIndeksi(yeni);
        var silinen = e.Where(x => !y.ContainsKey(x.Key)).Select(x => x.Value).ToList();
        var eklenen = y.Where(x => !e.ContainsKey(x.Key)).Select(x => x.Value).ToList();
        var adaylar = new List<M50OdaEslestirmeAdayi>();
        foreach (var a in silinen)
        foreach (var b in eklenen)
        {
            var ortusme = OrtusmeOrani(DunyaPoligonu(a), DunyaPoligonu(b));
            if (ortusme < 0.20) continue;
            var maxAlan = Math.Max(Math.Abs(a.Oda.AlanM2), Math.Abs(b.Oda.AlanM2));
            var alanBenzerligi = maxAlan > 0
                ? 1 - Math.Min(1, Math.Abs(a.Oda.AlanM2 - b.Oda.AlanM2) / maxAlan)
                : 0;
            adaylar.Add(new(a.Oda.Id, b.Oda.Id, a.Oda.Ad, b.Oda.Ad,
                ortusme, alanBenzerligi, a.Kat.Id, b.Kat.Id));
        }
        return adaylar.OrderByDescending(x => x.Guven).ToList();
    }

    private static Dictionary<string, (BinaKat Kat, BinaOda Oda)> OdaIndeksi(BinaGeometri geo) =>
        geo.Katlar.SelectMany(k => k.Odalar.Select(o => (k, o)))
            .ToDictionary(x => x.o.Id, x => (x.k, x.o), StringComparer.Ordinal);

    private static Dictionary<string, (BinaKat Kat, BinaAciklik Aciklik)> AciklikIndeksi(BinaGeometri geo) =>
        geo.Katlar.SelectMany(k => k.Acikliklar.Select(a => (k, a)))
            .ToDictionary(x => x.a.Id, x => (x.k, x.a), StringComparer.Ordinal);

    private static List<Point2D> DunyaPoligonu((BinaKat Kat, BinaOda Oda) x) =>
        x.Oda.Poligon.Select(p => DunyaNoktasi(x.Kat, p)).ToList();

    private static Point2D DunyaNoktasi(BinaKat kat, Point2D p) =>
        new(p.X + kat.OriginOffset.X, p.Y + kat.OriginOffset.Y);

    private static double OrtusmeOrani(IReadOnlyList<Point2D> a, IReadOnlyList<Point2D> b)
    {
        if (a.Count < 3 || b.Count < 3) return 0;
        var aa = Math.Abs(PoligonAlani(a));
        var ba = Math.Abs(PoligonAlani(b));
        var birlik = aa + ba;
        if (birlik <= 1e-9) return 0;
        var kesisim = ClipperAdaptor.NetAlan(ClipperAdaptor.Kesisim([a], [b]));
        var union = aa + ba - kesisim;
        return union > 0 ? Math.Clamp(kesisim / union, 0, 1) : 0;
    }

    private static double PoligonAlani(IReadOnlyList<Point2D> p)
    {
        double a = 0;
        for (var i = 0; i < p.Count; i++)
        {
            var q = p[(i + 1) % p.Count];
            a += p[i].X * q.Y - q.X * p[i].Y;
        }
        return a * 0.5;
    }

    private static double Mesafe(Point2D a, Point2D b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public enum M08RevizyonEtkiDuzeyi { Bilgi, Inceleme, Kritik }

public sealed record M08RevizyonEtkiKaydi(
    M08RevizyonEtkiDuzeyi Duzey,
    string Baslik,
    string Aciklama,
    string? EskiOdaId = null,
    string? YeniOdaId = null,
    Guid? MekanId = null);

public sealed record M08RevizyonEtkiRaporu(
    M50RevizyonFarkRaporu GeometriFarki,
    IReadOnlyList<M08RevizyonEtkiKaydi> Etkiler)
{
    public int KritikSayi => Etkiler.Count(x => x.Duzey == M08RevizyonEtkiDuzeyi.Kritik);
    public bool OnayGerekli => KritikSayi > 0 || GeometriFarki.OdaEslestirmeAdaylari.Count > 0;
}

/// <summary>M50 farklarını M08'in malzeme/sınıf/tesisat verileri açısından yorumlar.</summary>
public sealed class M08RevizyonEtkiServisi
{
    public M08RevizyonEtkiRaporu Degerlendir(BinaProje proje, BinaGeometri yeniGeometri)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(yeniGeometri);
        var eski = proje.BinaGeometriKopya ?? BinaGeometriM08ExportServisi.GeometriyeCevir(proje);
        var fark = new M50RevizyonFarkServisi().Karsilastir(eski, yeniGeometri);
        var etkiler = new List<M08RevizyonEtkiKaydi>();
        var mekanlar = proje.Katlar.SelectMany(k => k.Mekanlar).ToDictionary(m => m.Id);

        foreach (var f in fark.Farklar.Where(x => x.Nesne == M50FarkNesnesi.Mekan))
        {
            var mekanId = BinaGeometriMekanAdapteri.OdaIdToGuid(f.Kimlik);
            mekanlar.TryGetValue(mekanId, out var mekan);
            if (f.Tur == M50FarkTuru.Silindi && mekan is not null && AkustikVeriVar(mekan))
            {
                var aday = fark.OdaEslestirmeAdaylari.FirstOrDefault(x => x.EskiOdaId == f.Kimlik);
                etkiler.Add(new(M08RevizyonEtkiDuzeyi.Kritik, $"{mekan.Ad}: akustik veri riskte",
                    aday is null
                        ? "Mekan yeni geometride yok; malzeme, sınıf, tesisat ve emilim verileri eşleştirilmezse kaybolur."
                        : $"Mekan kimliği değişmiş olabilir. '{aday.YeniAd}' ile %{100 * aday.Guven:F0} güvenli eşleme adayı bulundu; kullanıcı onayı gerekir.",
                    f.Kimlik, aday?.YeniOdaId, mekan.Id));
            }
            else if (f.Tur == M50FarkTuru.Degisti && mekan is not null && AkustikVeriVar(mekan))
            {
                etkiler.Add(new(M08RevizyonEtkiDuzeyi.Inceleme, $"{mekan.Ad}: geometri değişti",
                    "Oda düzeyi akustik alanlar kimlikle korunur; değişen duvar topolojisindeki malzeme, açıklık ve junction aktarımları gözden geçirilmelidir.",
                    f.Kimlik, f.Kimlik, mekan.Id));
            }
            else if (f.Tur == M50FarkTuru.Eklendi)
            {
                etkiler.Add(new(M08RevizyonEtkiDuzeyi.Inceleme, $"{f.Baslik}: yeni mekan",
                    "Kullanım sınıfı, bağımsız bölüm ve yapı elemanı atamaları tamamlanmalıdır.", null, f.Kimlik));
            }
        }

        foreach (var f in fark.Farklar.Where(x => x.Nesne == M50FarkNesnesi.Aciklik && x.Tur == M50FarkTuru.Silindi))
        {
            var atamaVar = proje.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler)
                .SelectMany(b => b.Acikliklar).Any(a => a.AciklikId == f.Kimlik && a.YapiElemani is not null);
            if (atamaVar)
                etkiler.Add(new(M08RevizyonEtkiDuzeyi.Kritik, "Açıklık malzeme ataması riskte",
                    $"Silinen {f.Baslik.ToLowerInvariant()} '{f.Kimlik}' bir M08 malzeme ataması taşıyor.", null, null));
        }

        if (fark.Farklar.Any(x => x.Nesne == M50FarkNesnesi.DuseyBirlesim))
            etkiler.Add(new(M08RevizyonEtkiDuzeyi.Inceleme, "Darbe ve hacim yolları değişebilir",
                "Düşey birleşim değişikliği katlar arası darbe yollarını ve birleşik hacim normalizasyonunu etkiler."));

        return new(fark, etkiler);
    }

    private static bool AkustikVeriVar(Mekan m) =>
        m.BagimsizBolumId is not null || m.Sinif != MekanSinifi.Tanimsiz ||
        !string.IsNullOrWhiteSpace(m.KaynakDerecesi) || !string.IsNullOrWhiteSpace(m.AliciSinifi) ||
        m.RT60 > 0 || m.EmilimAlani > 0 || m.TesisatSurekliDb > 0 || m.TesisatKesikliDb > 0 ||
        m.ZeminYapiElemani is not null || m.TavanYapiElemani is not null ||
        m.Bolmeler.Any(b => b.YapiElemani is not null || b.Acikliklar.Any(a => a.YapiElemani is not null) ||
                           b.Menfezler.Count > 0 || b.Junctions.Count > 0);
}
