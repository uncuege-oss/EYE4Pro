using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// <summary>Dosyaya yazılacak M51 modelinin şema ve sayısal bütünlük kapısı.</summary>
public static class SalonModelButunlukServisi
{
    public static IReadOnlyList<string> Dogrula(SalonGeometri s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var e = new List<string>();
        if (s.SemaSurum != SalonGeometriServisi.GuncelSemaSurum)
            e.Add($"Şema sürümü {SalonGeometriServisi.GuncelSemaSurum} olmalıdır.");
        if (s.GeometriRevizyon < 0) e.Add("Geometri revizyonu negatif olamaz.");
        PozitifSonlu(s.SalonYuksekligiM, "Salon yüksekliği", e);
        if (s.FasetKenarSayisi is < 12 or > 128) e.Add("Faset sayısı 12–128 aralığında olmalıdır.");

        Kimlikler(s.Katmanlar.Select(x => x.Id), "katman", e);
        Kimlikler(s.CizimOgeleri.Select(x => x.Id), "çizim öğesi", e);
        Kimlikler(s.SeyirciSekciyonlari.Select(x => x.Id), "seyirci bölümü", e);
        Kimlikler(s.Balkonlar.Select(x => x.Id), "balkon", e);
        Kimlikler(s.Acikliklar.Select(x => x.Id), "açıklık", e);
        Kimlikler(s.Olculendirmeler.Select(x => x.Id), "ölçü", e);
        Kimlikler(s.GeometrikKisitlar.Select(x => x.Id), "kısıt", e);
        Kimlikler(s.ReferansAltliklar.Select(x => x.Id), "referans altlık", e);
        Kimlikler(s.YansiticiPaneller.Select(x => x.Id), "yansıtıcı", e);

        var katmanlar = s.Katmanlar.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var k in s.Katmanlar)
        {
            if (!Enum.IsDefined(k.Tip)) e.Add($"'{k.Ad}' katman türü geçersiz.");
            if (string.IsNullOrWhiteSpace(k.Ad)) e.Add("Katman adı boş olamaz.");
            if (!RenkMi(k.RenkHex)) e.Add($"'{k.Ad}' katman rengi #RRGGBB biçiminde olmalıdır.");
        }
        foreach (var o in s.CizimOgeleri)
        {
            if (!Enum.IsDefined(o.Tip)) e.Add($"'{o.Id}' çizim türü geçersiz.");
            if (!string.IsNullOrWhiteSpace(o.KatmanId) && !katmanlar.Contains(o.KatmanId))
                e.Add($"'{o.Id}' çizim öğesinin katmanı bulunamadı.");
            if (o.Noktalar.Any(p => !Sonlu(p))) e.Add($"'{o.Id}' sonlu olmayan koordinat içeriyor.");
            if (!Yeterli(o)) e.Add($"'{o.Id}' {o.Tip} geometrisi eksik veya dejenere.");
            if (!double.IsFinite(o.AciBasDeg) || !double.IsFinite(o.AciBitisDeg))
                e.Add($"'{o.Id}' yay açıları sonlu olmalıdır.");
        }

        if (s.Sahne is { } sh)
        {
            if (!Enum.IsDefined(sh.Tip) || !Enum.IsDefined(sh.Konum)) e.Add("Sahne türü/konumu geçersiz.");
            PozitifSonlu(sh.GenislikM, "Sahne genişliği", e);
            PozitifSonlu(sh.Tip == SahneTipi.Dairesel ? sh.GenislikM : sh.DerinlikM, "Sahne derinliği", e);
            NegatifOlmayanSonlu(sh.YukseklikM, "Sahne yüksekliği", e);
            NegatifOlmayanSonlu(sh.TabanKotuM, "Sahne taban kotu", e);
            Koordinat(sh.MerkezX, sh.MerkezY, "Sahne merkezi", e);
        }
        if (s.OrkestraCukuru is { } oc)
        {
            PozitifSonlu(oc.GenislikM, "Orkestra çukuru genişliği", e);
            PozitifSonlu(oc.DerinlikM, "Orkestra çukuru derinliği", e);
            PozitifSonlu(oc.CukurDerinligiM, "Orkestra çukuru derin kotu", e);
            Koordinat(oc.MerkezX, oc.MerkezY, "Orkestra çukuru merkezi", e);
        }
        foreach (var x in s.SeyirciSekciyonlari)
        {
            PozitifSonlu(x.GenislikM, $"'{x.Ad}' genişliği", e);
            PozitifSonlu(x.DerinlikM, $"'{x.Ad}' derinliği", e);
            if (x.KademeSayisi is < 1 or > 1000) e.Add($"'{x.Ad}' kademe sayısı 1–1000 olmalıdır.");
            NegatifOlmayanSonlu(x.KademeYuksekligiM, $"'{x.Ad}' kademe yüksekliği", e);
            NegatifOlmayanSonlu(x.TabanKotuM, $"'{x.Ad}' taban kotu", e);
            PozitifSonlu(x.KoltukGenisligiM, $"'{x.Ad}' koltuk genişliği", e);
            PozitifSonlu(x.SiraAraligiM, $"'{x.Ad}' sıra aralığı", e);
            NegatifOlmayanSonlu(x.YanKoridorGenisligiM, $"'{x.Ad}' yan koridoru", e);
            NegatifOlmayanSonlu(x.OrtaKoridorGenisligiM, $"'{x.Ad}' orta koridoru", e);
            if (x.OrtaKoridorSayisi is < 0 or > 100 || x.TekerlekliSandalyeYeri is < 0 or > 1000)
                e.Add($"'{x.Ad}' koridor/erişilebilir yer sayısı geçersiz.");
            Koordinat(x.MerkezX, x.MerkezY, $"'{x.Ad}' merkezi", e);
        }
        foreach (var x in s.Balkonlar)
        {
            if (!Enum.IsDefined(x.Tip)) e.Add($"'{x.Ad}' balkon türü geçersiz.");
            PozitifSonlu(x.GenislikM, $"'{x.Ad}' genişliği", e);
            PozitifSonlu(x.DerinlikM, $"'{x.Ad}' derinliği", e);
            PozitifSonlu(x.KalinlikM, $"'{x.Ad}' kalınlığı", e);
            NegatifOlmayanSonlu(x.TabanKotuM, $"'{x.Ad}' kotu", e);
            NegatifOlmayanSonlu(x.ParapetM, $"'{x.Ad}' parapeti", e);
            Koordinat(x.MerkezX, x.MerkezY, $"'{x.Ad}' merkezi", e);
        }
        foreach (var x in s.Acikliklar)
        {
            if (!Enum.IsDefined(x.Tip)) e.Add($"'{x.Ad}' açıklık türü geçersiz.");
            PozitifSonlu(x.GenislikM, $"'{x.Ad}' genişliği", e);
            PozitifSonlu(x.YukseklikM, $"'{x.Ad}' yüksekliği", e);
            NegatifOlmayanSonlu(x.AltKotM, $"'{x.Ad}' alt kotu", e);
            Koordinat(x.MerkezX, x.MerkezY, $"'{x.Ad}' merkezi", e);
        }
        if (s.TavanProfili.Any(x => !double.IsFinite(x.YM) || !double.IsFinite(x.KotM) || x.KotM <= 0))
            e.Add("Tavan profili sonlu Y ve pozitif kot değerleri içermelidir.");
        if (s.TavanProfili.Count == 1)
            e.Add("Tavan profili boş veya en az iki noktadan oluşmalıdır.");
        if (s.TavanProfili.GroupBy(x => Math.Round(x.YM, 9)).Any(g => g.Count() > 1))
            e.Add("Tavan profilinde aynı Y konumunda birden fazla nokta olamaz.");

        foreach (var x in s.Olculendirmeler)
        {
            if (!Enum.IsDefined(x.Tip)) e.Add($"'{x.Ad}' ölçü türü geçersiz.");
            if (!Sonlu(x.Baslangic) || !Sonlu(x.Bitis) || !double.IsFinite(x.OfsetM))
                e.Add($"'{x.Ad}' ölçüsü sonlu olmayan değer içeriyor.");
            if (!string.IsNullOrWhiteSpace(x.OgeId))
            {
                var oge = s.CizimOgeleri.FirstOrDefault(o => o.Id == x.OgeId);
                if (oge is null)
                    e.Add($"'{x.Ad}' ölçüsünün bağlı olduğu çizim öğesi bulunamadı.");
                else if (x.BaslangicNoktaIndeksi < 0 || x.BitisNoktaIndeksi < 0 ||
                         x.BaslangicNoktaIndeksi >= oge.Noktalar.Count ||
                         x.BitisNoktaIndeksi >= oge.Noktalar.Count)
                    e.Add($"'{x.Ad}' ölçüsünün bağlı köşe indisleri geçersiz.");
            }
        }
        var ogeId = s.CizimOgeleri.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var x in s.GeometrikKisitlar)
        {
            if (!Enum.IsDefined(x.Tip)) e.Add($"'{x.Ad}' kısıt türü geçersiz.");
            if (!ogeId.Contains(x.OgeId)) e.Add($"'{x.Ad}' birinci çizim öğesini bulamıyor.");
            if (!string.IsNullOrWhiteSpace(x.IkinciOgeId) && !ogeId.Contains(x.IkinciOgeId))
                e.Add($"'{x.Ad}' ikinci çizim öğesini bulamıyor.");
            if (!double.IsFinite(x.Deger)) e.Add($"'{x.Ad}' kısıt değeri sonlu olmalıdır.");
            if (ogeId.Contains(x.OgeId))
            {
                var a = s.CizimOgeleri.First(o => o.Id == x.OgeId);
                if (x.NoktaIndeksi < 0 || x.NoktaIndeksi >= a.Noktalar.Count)
                    e.Add($"'{x.Ad}' birinci köşe indisi geçersiz.");
                if (x.Tip is SalonKisitTipi.Yatay or SalonKisitTipi.Dikey or SalonKisitTipi.Uzunluk
                    && (x.IkinciNoktaIndeksi < 0 || x.IkinciNoktaIndeksi >= a.Noktalar.Count ||
                        x.IkinciNoktaIndeksi == x.NoktaIndeksi))
                    e.Add($"'{x.Ad}' ikinci köşe indisi geçersiz.");
            }
            if (!string.IsNullOrWhiteSpace(x.IkinciOgeId) && ogeId.Contains(x.IkinciOgeId))
            {
                var b = s.CizimOgeleri.First(o => o.Id == x.IkinciOgeId);
                int gerekli = x.Tip == SalonKisitTipi.Cakistir ? x.IkinciNoktaIndeksi : x.NoktaIndeksi;
                if (gerekli < 0 || gerekli >= b.Noktalar.Count)
                    e.Add($"'{x.Ad}' ikinci öğe köşe indisi geçersiz.");
                if (x.Tip is SalonKisitTipi.Paralel or SalonKisitTipi.Dik or SalonKisitTipi.EsitUzunluk
                    && (x.IkinciNoktaIndeksi < 0 || x.IkinciNoktaIndeksi >= b.Noktalar.Count ||
                        x.IkinciNoktaIndeksi == x.NoktaIndeksi))
                    e.Add($"'{x.Ad}' ikinci öğe segment indisleri geçersiz.");
            }
        }
        foreach (var x in s.ReferansAltliklar)
        {
            if (!Enum.IsDefined(x.Tip)) e.Add($"'{x.Ad}' altlık türü geçersiz.");
            PozitifSonlu(x.Olcek, $"'{x.Ad}' altlık ölçeği", e);
            if (!double.IsFinite(x.DonusDerece + x.OfsetX + x.OfsetY) || x.Saydamlik is < 0 or > 1 || !double.IsFinite(x.Saydamlik))
                e.Add($"'{x.Ad}' altlık dönüşüm/saydamlık değeri geçersiz.");
            if (x.Segmentler.Any(y => !Sonlu(y.Baslangic) || !Sonlu(y.Bitis)))
                e.Add($"'{x.Ad}' altlığı sonlu olmayan segment içeriyor.");
        }
        foreach (var x in s.YansiticiPaneller)
        {
            PozitifSonlu(x.GenislikM, $"'{x.Ad}' genişliği", e);
            PozitifSonlu(x.DerinlikM, $"'{x.Ad}' derinliği", e);
            if (!double.IsFinite(x.MerkezX + x.MerkezY + x.MerkezZ + x.XEgimDerece + x.YEgimDerece))
                e.Add($"'{x.Ad}' yansıtıcısı sonlu olmayan konum/açı içeriyor.");
            if (string.IsNullOrWhiteSpace(x.KatmanId) || !katmanlar.Contains(x.KatmanId))
                e.Add($"'{x.Ad}' yansıtıcısının katmanı bulunamadı.");
        }
        return e.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static void DogrulaVeFirlat(SalonGeometri salon)
    {
        var e = Dogrula(salon);
        if (e.Count > 0)
            throw new InvalidDataException("Salon model bütünlüğü geçersiz:\n• " + string.Join("\n• ", e));
    }

    private static void Kimlikler(IEnumerable<string?> ids, string ad, List<string> e)
    {
        var l = ids.ToList();
        if (l.Any(string.IsNullOrWhiteSpace)) e.Add($"Boş {ad} kimliği var.");
        foreach (var g in l.Where(x => !string.IsNullOrWhiteSpace(x)).GroupBy(x => x!, StringComparer.Ordinal).Where(g => g.Count() > 1))
            e.Add($"'{g.Key}' {ad} kimliği yineleniyor.");
    }
    private static bool Yeterli(SalonCizimOgesi o) => o.Tip switch
    {
        SalonCizimTipi.Cizgi => o.Noktalar.Count >= 2 && Uzaklik(o.Noktalar[0], o.Noktalar[1]) > 1e-9,
        SalonCizimTipi.Polyline => o.Noktalar.Distinct().Count() >= (o.Kapali ? 3 : 2),
        SalonCizimTipi.Dikdortgen => o.Noktalar.Distinct().Count() >= 3,
        SalonCizimTipi.Daire or SalonCizimTipi.Yay => o.Noktalar.Count > 0 && double.IsFinite(o.YariCapM) && o.YariCapM > 0,
        _ => false
    };
    private static bool RenkMi(string? s) => s is { Length: 7 } && s[0] == '#' && s[1..].All(Uri.IsHexDigit);
    private static bool Sonlu(Point2D p) => double.IsFinite(p.X) && double.IsFinite(p.Y);
    private static void Koordinat(double x, double y, string ad, List<string> e)
    { if (!double.IsFinite(x) || !double.IsFinite(y)) e.Add($"{ad} sonlu olmalıdır."); }
    private static void PozitifSonlu(double x, string ad, List<string> e)
    { if (!double.IsFinite(x) || x <= 0) e.Add($"{ad} pozitif ve sonlu olmalıdır."); }
    private static void NegatifOlmayanSonlu(double x, string ad, List<string> e)
    { if (!double.IsFinite(x) || x < 0) e.Add($"{ad} negatif olmayan sonlu değer olmalıdır."); }
    private static double Uzaklik(Point2D a, Point2D b)
        => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
