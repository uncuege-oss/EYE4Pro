using System.Text.RegularExpressions;

namespace Kst.Acoustics.Core.Bina.Services;

/// <summary>Mahalleri tarif eden etiketleri malzeme, eleman, ölçü ve pafta notlarından ayırır.</summary>
public static class MimariOdaEtiketServisi
{
    public sealed record OdaEtiketi(string Ad, int Puan);

    private static readonly Regex RoomType = new(
        @"\b(?:ODA(?:SI|LARI)?|SALON(?:U)?|MUTFAK|BANYO|WC|TUVALET(?:I)?|YATAK|KORIDOR|BALKON|HOL(?:U)?|OFIS|BURO|HALL|KITCHEN|BATH(?:ROOM)?|BEDROOM|LIVING|ROOM|DUKKAN|OKUMA|KUTUPHANE|DEPO(?:SU)?|GOREVLI|MESCIT|MESCID|ABDEST(?:HANE)?|FUAYE|TERAS(?:I)?|SIGINAK|DERSLIK|TOPLANTI|YEMEKHANE|TAZIYE|ATOLYE(?:SI)?|LABORATUVAR|ARSIV|MERDIVEN(?:I)?|ASANSOR|RESEPSIYON|DANISMA|BEKLEME|KAZAN\s+DAIRESI|SIHHI\s+TESISAT|TEKNIK\s+HACIM|GUVENLIK|REVI[Rİ]|SAGLIK|POLIKLINIK|SOYUNMA|DUS|GIYINME|SISTEM\s+MERKEZI)\b", RegexOptions.Compiled);
    private static readonly Regex Annotation = new(
        @"\b(?:PLANI|PLANLARI|KESIT(?:I)?|GORUNUS(?:U)?|CEPHE(?:SI)?|OLCEK|PAFTA|LEJANT|DETAY(?:I)?|DVR|DVS|KC|KOD|NOT(?:LAR)?|YAPILACAKTIR|OLACAKTIR|YAPILMASI|KONULMASI|UYGULANACAKTIR|TASINACAKTIR)\b|\b(?:BRUT|NET)\s+(?:KAT\s+)?ALAN|OTURUM\s+IZI", RegexOptions.Compiled);
    private static readonly Regex Construction = new(
        @"\b(?:BOYA|SIVA|KAPLAMA(?:SI)?|YALITIM(?:I|LI)?|KONSTRUKSIYON|BETONARME|KENETLI|ORTU(?:SU)?|HARPUSTA|DENIZLIK|KORKULUK|DOGRAMA|DOGRAMA\s*SI|TEMPERLI|KORUMA\s+BANDI|BUHAR\s+DENGELEYICI|ISI\s+CAM|KEPENK|KEYPENK|OLUK|TEZGAH|PERVAZ|ASMA\s+TAVAN|KAPI(?:SI)?|PENCERE(?:SI)?)\b", RegexOptions.Compiled);
    private static readonly Regex Material = new(
        @"\b(?:LAMINAT|ALUMINYUM|ALCIPAN|SERAMIK|FAYANS|MERMER|GRANIT|CELIK|DEMIR|PASLANMAZ|SAC|TASYUNU|XPS|BIMS|OSB|DOSEME|TAVAN|AKRILIK|MDF(?:LAM)?|DOLAP(?:LARI|I)?)\b", RegexOptions.Compiled);
    private static readonly Regex RoomPurpose = new(@"\b(?:ODASI|ODA|DEPO(?:SU)?|ATOLYE(?:SI)?|LABORATUVAR|DERSLIK|SALON(?:U)?)\b", RegexOptions.Compiled);
    private static readonly Regex CodeOnly = new(@"^[A-Z]{1,6}\s*[-.]?\s*\d{1,4}[A-Z]?$", RegexOptions.Compiled);
    private static readonly Regex Dimension = new(@"\d\s*[/X×*]\s*\d|\d[\d.,]*\s*(?:MM|CM|M2|M²|METRE)\b|^[+±-]\s*\d|\bH\s*[:=]|\b(?:KOT|KOTU)\b", RegexOptions.Compiled);

    public static bool TryName(string? text, out string name)
    {
        var result = Degerlendir(text);
        name = result?.Ad ?? "";
        return result is not null;
    }

    public static bool ContainsRoomName(string? text) => Degerlendir(text) is { Puan: >= 100 };

    public static OdaEtiketi? Degerlendir(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string name = text.Replace("\\P", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
        name = Regex.Replace(name, @"\s+", " ");
        // Birleşik mahal adı/alanı etiketi: alan bilgisi oda adı değildir.
        name = Regex.Replace(name, @"\s+(?:A(?:LAN)?\s*[:=]\s*)?\d+(?:[.,]\d+)?\s*[mM](?:2|²)\s*$", "").Trim();
        if (name.Length is < 2 or > 70 || name.Count(char.IsLetter) < 2 || name[0] is '-' or '*' or '•') return null;
        string normalized = Normalize(name);
        if (CodeOnly.IsMatch(normalized) || Annotation.IsMatch(normalized) || Dimension.IsMatch(normalized)) return null;
        if (Regex.IsMatch(normalized, @"^(?:[A-Z]\s*[-/]\s*)*[A-Z]$|^(?:AKS|DVR|DVS|KC|AL\.?\s*DOG\.?|ALAN|KAT|CATI|DUVAR|TABAN|TAS|CAM)$")) return null;
        bool purpose = RoomPurpose.IsMatch(normalized);
        // 'Seramik Atölyesi' ve 'Asma Tavan Deposu' gibi gerçek mahal adları korunur.
        if ((Construction.IsMatch(normalized) || Material.IsMatch(normalized)) && !purpose) return null;
        if (Regex.IsMatch(normalized, @"\b(?:YAPILMASI|KAPLAMASI|YALITIMLI|KADAR|USTU|ALTINA|ARASI|ENINDE)\b")) return null;
        if (RoomType.IsMatch(normalized)) return new(name, 100);
        // Tanınmayan kısa adlar da geçerli olabilir; kelime listesi zorunluluğu yoktur.
        if (name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 5 || name.Contains(':') || name.Contains('=') || name.Contains(';')) return null;
        if (Regex.IsMatch(normalized, @"\d") && !Regex.IsMatch(normalized, @"^\d{1,4}\s+[A-Z ]+$")) return null;
        return new(name, 20);
    }

    private static string Normalize(string text) => text.ToUpperInvariant().Replace('ı', 'I')
        .Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G').Replace('Ü', 'U')
        .Replace('Ö', 'O').Replace('Ç', 'C').Replace('Â', 'A').Replace('Î', 'I');
}
