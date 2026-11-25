using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Kst.Acoustics.Core.Electric.KesifHazirla;

/// <summary>Toplu girişteki ham metinler; doğrulanana kadar proje verisine eklenmez.</summary>
public sealed class KesifTopluPozSatiri : KesifObservable
{
    private string _pozNo = "", _birimAdi = "", _birim = "", _fiyat = "", _hata = "";
    public string PozNo { get => _pozNo; set => SetProperty(ref _pozNo, value); }
    public string BirimAdi { get => _birimAdi; set => SetProperty(ref _birimAdi, value); }
    public string Birim { get => _birim; set => SetProperty(ref _birim, value); }
    public string Fiyat { get => _fiyat; set => SetProperty(ref _fiyat, value); }
    public string Hata { get => _hata; set => SetProperty(ref _hata, value); }
    public bool BosMu => new[] { PozNo, BirimAdi, Birim, Fiyat }.All(string.IsNullOrWhiteSpace);

    public void HucreAyarla(int sutun, string deger)
    {
        switch (sutun)
        {
            case 0: PozNo = deger; break;
            case 1: BirimAdi = deger; break;
            case 2: Birim = deger; break;
            case 3: Fiyat = deger; break;
            default: throw new ArgumentOutOfRangeException(nameof(sutun));
        }
        Hata = "";
    }
}

public sealed record KesifTopluPozSonucu(IReadOnlyList<KesifPoz> Pozlar, int HataliSatirSayisi);

public static class KesifTopluPozServisi
{
    /// <summary>Excel'in TSV pano biçimi: tırnaklı çok satırlı hücreleri ve çift tırnakları korur.</summary>
    public static IReadOnlyList<string[]> PanoCoz(string metin, int baslangicSutunu = 0)
    {
        if (baslangicSutunu is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(baslangicSutunu));
        var kayitlar = new List<string[]>();
        var hucreler = new List<string>();
        var hucre = new StringBuilder();
        var tirnakli = false;
        void SatirBitir()
        {
            hucreler.Add(hucre.ToString()); hucre.Clear();
            if (hucreler.Any(x => !string.IsNullOrWhiteSpace(x))) kayitlar.Add(hucreler.ToArray());
            hucreler.Clear();
        }
        for (var i = 0; i < metin.Length; i++)
        {
            var c = metin[i];
            if (tirnakli)
            {
                if (c == '"' && i + 1 < metin.Length && metin[i + 1] == '"') { hucre.Append('"'); i++; }
                else if (c == '"') tirnakli = false;
                else hucre.Append(c);
            }
            else if (c == '"' && hucre.Length == 0) tirnakli = true;
            else if (c == '\t') { hucreler.Add(hucre.ToString()); hucre.Clear(); }
            else if (c is '\r' or '\n')
            {
                SatirBitir();
                if (c == '\r' && i + 1 < metin.Length && metin[i + 1] == '\n') i++;
            }
            else hucre.Append(c);
        }
        if (tirnakli) throw new InvalidOperationException("Panodaki tırnaklı hücre tamamlanmamış. Excel'deki hücreleri yeniden kopyalayın.");
        if (hucre.Length > 0 || hucreler.Count > 0) SatirBitir();
        if (baslangicSutunu == 0 && kayitlar.Count > 0 && BaslikMi(kayitlar[0])) kayitlar.RemoveAt(0);
        for (var i = 0; i < kayitlar.Count; i++)
        {
            var cells = kayitlar[i].ToList();
            while (cells.Count > 4 - baslangicSutunu && string.IsNullOrWhiteSpace(cells[^1])) cells.RemoveAt(cells.Count - 1);
            // Muhasebe biçimi bazen para simgesini ayrı Excel sütunu olarak kopyalar.
            var fiyatSutunu = 3 - baslangicSutunu;
            if (cells.Count == 5 - baslangicSutunu && (ParaSimgesiMi(cells[fiyatSutunu]) || ParaSimgesiMi(cells[fiyatSutunu + 1])))
            {
                cells[fiyatSutunu] += " " + cells[fiyatSutunu + 1];
                cells.RemoveAt(fiyatSutunu + 1);
            }
            if (cells.Count > 4 - baslangicSutunu)
                throw new InvalidOperationException($"{i + 1}. satırda fazla sütun var. Dört sütunluk veriyi Poz Adı / No sütunundan başlayarak yapıştırın.");
            kayitlar[i] = cells.ToArray();
        }
        return kayitlar;
    }

    private static bool ParaSimgesiMi(string text) => text.Trim().ToUpperInvariant() is "₺" or "TL" or "TRY";

    private static bool BaslikMi(string[] cells)
    {
        if (cells.Length < 4) return false;
        var a = KesifPozImportServisi.NormalizeHeader(cells[0]);
        var b = KesifPozImportServisi.NormalizeHeader(cells[1]);
        var c = KesifPozImportServisi.NormalizeHeader(cells[2]);
        var d = KesifPozImportServisi.NormalizeHeader(cells[3]);
        return a is "pozno" or "pozadi" or "pozkodu" or "pozadino"
            && b is "poztanimi" or "birimadi" or "imalatincinsi" or "tanim" or "aciklama"
            && c is "birim" or "pozbirimi"
            && d is "fiyat" or "pozfiyati" or "birimfiyat" or "birimfiyati";
    }

    public static bool FiyatCoz(string text, out decimal fiyat)
    {
        fiyat = 0;
        var clean = Regex.Replace(text, @"₺|TRY|TL|\s", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        // Hatalı para metnini sessizce sıfır kabul etme; yalnız sayısal biçimler geçerlidir.
        if (!Regex.IsMatch(clean, @"^[+-]?(?:\d+|\d{1,3}(?:\.\d{3})+(?:,\d+)?|\d+,\d+|\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+\.\d+)$")) return false;
        var virguller = clean.Count(c => c == ',');
        var noktalar = clean.Count(c => c == '.');
        if (virguller > 0 && noktalar > 0)
            clean = clean.LastIndexOf(',') > clean.LastIndexOf('.')
                ? clean.Replace(".", "").Replace(',', '.') : clean.Replace(",", "");
        else if (virguller == 1) clean = clean.Replace(',', '.');
        else if (virguller > 1) clean = clean.Replace(",", "");
        else if (noktalar > 0 && Regex.IsMatch(clean, @"^[+-]?\d{1,3}(?:\.\d{3})+$")) clean = clean.Replace(".", "");
        return decimal.TryParse(clean, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out fiyat);
    }

    /// <summary>Hatalı tek satır varsa hiçbir poz aktarılmaz. Mevcut pozlar güncellenmez.</summary>
    public static KesifTopluPozSonucu Hazirla(IEnumerable<KesifTopluPozSatiri> satirlar, IEnumerable<string> mevcutPozNolari)
    {
        var rows = satirlar.ToList();
        var mevcut = mevcutPozNolari.Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tekrarlar = rows.Where(x => !x.BosMu && !string.IsNullOrWhiteSpace(x.PozNo))
            .GroupBy(x => x.PozNo.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pozlar = new List<KesifPoz>();
        var hataSayisi = 0;
        foreach (var row in rows)
        {
            row.Hata = "";
            if (row.BosMu) continue;
            var hatalar = new List<string>();
            var kod = row.PozNo.Trim();
            if (kod.Length == 0) hatalar.Add("Poz adı / no eksik");
            if (string.IsNullOrWhiteSpace(row.BirimAdi)) hatalar.Add("Poz tanımı eksik");
            if (string.IsNullOrWhiteSpace(row.Birim)) hatalar.Add("Poz birimi eksik");
            if (!FiyatCoz(row.Fiyat, out var fiyat) || fiyat < 0) hatalar.Add("Fiyat geçersiz (0 veya pozitif sayı girin)");
            if (mevcut.Contains(kod)) hatalar.Add("Bu poz adı / no sistemde zaten var");
            if (tekrarlar.Contains(kod)) hatalar.Add("Bu poz adı / no girişte tekrarlanıyor");
            row.Hata = string.Join("; ", hatalar);
            if (hatalar.Count > 0) { hataSayisi++; continue; }
            pozlar.Add(new KesifPoz { PozNo = kod, BirimAdi = row.BirimAdi.Trim(), Birim = row.Birim.Trim(), BirimFiyat = fiyat });
        }
        return new KesifTopluPozSonucu(hataSayisi == 0 ? pozlar : Array.Empty<KesifPoz>(), hataSayisi);
    }
}
