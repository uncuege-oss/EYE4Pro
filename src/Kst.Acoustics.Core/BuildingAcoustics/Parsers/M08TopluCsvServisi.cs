using System.Globalization;
using System.Text;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Parsers;

public sealed class M08TopluCsvSonucu
{
    public int MekanGuncellemeSayisi { get; internal set; }
    public int BolmeGuncellemeSayisi { get; internal set; }
    public List<string> Uyarilar { get; } = new();
    public List<string> Hatalar { get; } = new();
    public bool Basarili => Hatalar.Count == 0;
}

/// <summary>Kimlik tabanlı oda/bölme CSV aktarımı. Tüm satırlar doğrulanmadan modeli değiştirmez.</summary>
public static class M08TopluCsvServisi
{
    private const string Header = "KayitTuru;Kat;NesneId;Ad;KullanimTipi;KaynakDerecesi;AliciSinifi;BagimsizBolumId;ZeminElemaniId;TavanElemaniId;TesisatSurekliDb;TesisatKesikliDb;EmilimAlani;BolmeElemaniId";

    public static string Olustur(BinaProje proje)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var sb = new StringBuilder().AppendLine(Header);
        foreach (var kat in proje.Katlar.OrderBy(x => x.KatNo))
        foreach (var m in kat.Mekanlar.OrderBy(x => x.Ad, StringComparer.CurrentCultureIgnoreCase))
        {
            sb.AppendLine(string.Join(';', "Mekan", M08Csv.Kacisla(Kat(kat)), m.Id,
                M08Csv.Kacisla(m.Ad), M08Csv.Kacisla(m.KullanimTipi), M08Csv.Kacisla(m.KaynakDerecesi),
                M08Csv.Kacisla(m.AliciSinifi), m.BagimsizBolumId?.ToString("D") ?? "",
                Id(m.ZeminYapiElemani), Id(m.TavanYapiElemani), N(m.TesisatSurekliDb),
                N(m.TesisatKesikliDb), N(m.EmilimAlani), ""));
            foreach (var b in m.Bolmeler.OrderBy(x => x.Ad, StringComparer.CurrentCultureIgnoreCase))
                sb.AppendLine(string.Join(';', "Bolme", M08Csv.Kacisla(Kat(kat)), b.Id,
                    M08Csv.Kacisla(b.Ad), "", "", "", "", "", "", "", "", "", Id(b.YapiElemani)));
        }
        return sb.ToString();
    }

    public static void DosyayaYaz(BinaProje proje, string yol) => M08Csv.AtomikYaz(yol, Olustur(proje));

    public static M08TopluCsvSonucu DosyadanUygula(BinaProje proje, string yol) =>
        MetindenUygula(proje, File.ReadAllText(yol));

    public static M08TopluCsvSonucu MetindenUygula(BinaProje proje, string csv)
    {
        ArgumentNullException.ThrowIfNull(proje); ArgumentNullException.ThrowIfNull(csv);
        var r = new M08TopluCsvSonucu();
        var lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0])) { r.Hatalar.Add("CSV başlığı yok."); return r; }
        var sep = M08Csv.AyiracBul(lines[0]);
        var headers = M08Csv.AlanlaraAyir(lines[0], sep).Select(M08Csv.Anahtarlastir).ToList();
        int I(string name) => headers.IndexOf(M08Csv.Anahtarlastir(name));
        var required = new[] { "KayitTuru", "NesneId", "Ad" };
        foreach (var x in required) if (I(x) < 0) r.Hatalar.Add($"Zorunlu sütun yok: {x}");
        if (!r.Basarili) return r;

        var mekanlar = proje.Katlar.SelectMany(k => k.Mekanlar).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
        var bolmeler = mekanlar.Values.SelectMany(x => x.Bolmeler).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
        var elemanlar = proje.YapiElemaniKutuphanesi.Concat(mekanlar.Values.SelectMany(m => m.Bolmeler.Select(b => b.YapiElemani)
                .Concat(m.Bolmeler.SelectMany(b => b.Acikliklar).Select(a => a.YapiElemani)).Append(m.ZeminYapiElemani).Append(m.TavanYapiElemani)))
            .Where(x => x is not null).Cast<YapiElemani>().GroupBy(EtkinId).ToDictionary(x => x.Key, x => x.First());
        var bolumler = proje.BagimsizBolumler.ToDictionary(x => x.Id);
        var actions = new List<Action>();
        var seen = new HashSet<(string, Guid)>();
        for (var ln = 1; ln < lines.Length; ln++)
        {
            if (string.IsNullOrWhiteSpace(lines[ln])) continue;
            var f = M08Csv.AlanlaraAyir(lines[ln], sep);
            string V(string name) { var i = I(name); return i >= 0 && i < f.Count ? f[i].Trim() : ""; }
            if (!Guid.TryParse(V("NesneId"), out var id)) { r.Hatalar.Add($"Satır {ln + 1}: NesneId geçersiz."); continue; }
            var tur = V("KayitTuru");
            if (!seen.Add((tur.ToUpperInvariant(), id))) { r.Hatalar.Add($"Satır {ln + 1}: yinelenen {tur}/{id}."); continue; }
            if (tur.Equals("Mekan", StringComparison.OrdinalIgnoreCase))
            {
                if (!mekanlar.TryGetValue(id, out var m)) { r.Hatalar.Add($"Satır {ln + 1}: mekan bulunamadı ({id})."); continue; }
                if (!Sayilar(V("TesisatSurekliDb"), V("TesisatKesikliDb"), V("EmilimAlani"), out var surekli, out var kesikli, out var emilim))
                { r.Hatalar.Add($"Satır {ln + 1}: tesisat/emilim sayısı geçersiz."); continue; }
                if (!OpsiyonelGuid(V("BagimsizBolumId"), bolumler, out Guid? bolum)) { r.Hatalar.Add($"Satır {ln + 1}: bağımsız bölüm bulunamadı."); continue; }
                if (!Eleman(V("ZeminElemaniId"), elemanlar, YapiElemaniTipi.Doseme, out var zemin) ||
                    !Eleman(V("TavanElemaniId"), elemanlar, YapiElemaniTipi.Tavan, out var tavan))
                { r.Hatalar.Add($"Satır {ln + 1}: zemin/tavan elemanı bulunamadı veya tipi yanlış."); continue; }
                var ad = V("Ad"); var kullanim = V("KullanimTipi"); var kaynak = V("KaynakDerecesi").ToUpperInvariant();
                var alici = V("AliciSinifi").ToUpperInvariant();
                if (kaynak is not ("" or "DG" or "OG" or "YG") || alici is not ("" or "I" or "II" or "III"))
                { r.Hatalar.Add($"Satır {ln + 1}: kaynak/alıcı sınıfı geçersiz."); continue; }
                actions.Add(() => { m.Ad = ad; m.KullanimTipi = kullanim; m.KaynakDerecesi = kaynak; m.AliciSinifi = alici;
                    m.BagimsizBolumId = bolum; m.ZeminYapiElemani = zemin; m.TavanYapiElemani = tavan;
                    m.TesisatSurekliDb = surekli; m.TesisatKesikliDb = kesikli; m.EmilimAlani = emilim; r.MekanGuncellemeSayisi++; });
            }
            else if (tur.Equals("Bolme", StringComparison.OrdinalIgnoreCase))
            {
                if (!bolmeler.TryGetValue(id, out var b)) { r.Hatalar.Add($"Satır {ln + 1}: bölme bulunamadı ({id})."); continue; }
                if (!Eleman(V("BolmeElemaniId"), elemanlar, null, out var e) || e is not null && e.Tip is not (YapiElemaniTipi.Duvar or YapiElemaniTipi.Doseme))
                { r.Hatalar.Add($"Satır {ln + 1}: bölme elemanı bulunamadı veya tipi yanlış."); continue; }
                var ad = V("Ad"); actions.Add(() => { b.Ad = ad; b.YapiElemani = e; r.BolmeGuncellemeSayisi++; });
            }
            else r.Hatalar.Add($"Satır {ln + 1}: KayitTuru 'Mekan' veya 'Bolme' olmalıdır.");
        }
        if (!r.Basarili) return r;
        foreach (var action in actions) action();
        return r;
    }

    private static bool Sayilar(string a, string b, string c, out double x, out double y, out double z)
    {
        x = y = z = 0;
        return M08Csv.DoubleOku(a, out x) && M08Csv.DoubleOku(b, out y) && M08Csv.DoubleOku(c, out z) &&
               x >= 0 && y >= 0 && z >= 0;
    }
    private static bool OpsiyonelGuid<T>(string s, IReadOnlyDictionary<Guid, T> map, out Guid? id)
    { id = null; if (string.IsNullOrWhiteSpace(s)) return true; if (!Guid.TryParse(s, out var x) || !map.ContainsKey(x)) return false; id = x; return true; }
    private static bool Eleman(string s, IReadOnlyDictionary<Guid, YapiElemani> map, YapiElemaniTipi? tip, out YapiElemani? e)
    { e = null; if (string.IsNullOrWhiteSpace(s)) return true; if (!Guid.TryParse(s, out var id) || !map.TryGetValue(id, out e)) return false; return tip is null || e.Tip == tip; }
    private static string Kat(Kat k) => string.IsNullOrWhiteSpace(k.Ad) ? k.KatNo.ToString(CultureInfo.InvariantCulture) : k.Ad;
    private static string N(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    private static string Id(YapiElemani? e) => e is null ? "" : EtkinId(e).ToString("D");
    private static Guid EtkinId(YapiElemani e) => e.Id == Guid.Empty ? M08KararliKimlik.YapiElemani(e) : e.Id;
}
