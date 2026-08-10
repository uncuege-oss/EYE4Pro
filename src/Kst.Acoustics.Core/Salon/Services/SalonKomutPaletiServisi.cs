namespace Kst.Acoustics.Core.Salon.Services;

public sealed record SalonKomutTanimi(
    string Id,
    string Ad,
    string Grup,
    string? Kisayol = null,
    IReadOnlyList<string>? AramaKelimeleri = null)
{
    public IReadOnlyList<string> AnahtarKelimeler { get; init; } = AramaKelimeleri ?? [];
}

/// <summary>Ctrl+K komut paleti ve araç araması için tek komut kataloğu.</summary>
public static class SalonKomutPaletiServisi
{
    public static IReadOnlyList<SalonKomutTanimi> Komutlar { get; } =
    [
        new("file.new", "Yeni salon", "Dosya", "Ctrl+N", ["temiz", "new"]),
        new("file.open", "Salon aç", "Dosya", "Ctrl+O", ["yükle", "open"]),
        new("file.save", "Kaydet", "Dosya", "Ctrl+S", ["save"]),
        new("file.saveAs", "Farklı kaydet", "Dosya", "Ctrl+Shift+S", ["save as"]),
        new("edit.undo", "Geri al", "Düzenle", "Ctrl+Z"),
        new("edit.redo", "Yinele", "Düzenle", "Ctrl+Y"),
        new("draw.line", "Çizgi çiz", "Çizim", "L"),
        new("draw.polyline", "Polyline çiz", "Çizim", "P"),
        new("draw.rectangle", "Dikdörtgen çiz", "Çizim", "R"),
        new("draw.circle", "Daire çiz", "Çizim", "C"),
        new("draw.arc", "Yay çiz", "Çizim", "A"),
        new("modify.move", "Taşı / kopyala", "Düzenle", "M"),
        new("modify.rotate", "Döndür", "Düzenle", "D"),
        new("modify.mirror", "Aynala", "Düzenle", "N"),
        new("modify.offset", "Offset", "Düzenle", "O"),
        new("modify.trim", "Trim", "Düzenle", "T"),
        new("modify.extend", "Uzat", "Düzenle", "E"),
        new("modify.fillet", "Köşe yuvarla (Fillet)", "Gelişmiş CAD", null, ["radius", "yuvarla"]),
        new("modify.chamfer", "Pah kır", "Gelişmiş CAD", null, ["chamfer"]),
        new("modify.array", "Dizi oluştur", "Gelişmiş CAD", null, ["array", "çoğalt"]),
        new("modify.align", "Hizala / dağıt", "Gelişmiş CAD"),
        new("modify.group", "Grupla / grubu çöz", "Gelişmiş CAD", "Ctrl+G"),
        new("view.fit", "Tümünü sığdır", "Görünüm", "F"),
        new("view.section", "Boyuna / enine kesit", "Görünüm"),
        new("view.split3d", "2B + 3B bölünmüş görünüm", "Görünüm"),
        new("tools.quickDesign", "Tek tık salon sihirbazı", "Tasarım", null, ["preset", "koltuk"]),
        new("tools.seats", "Gerçek koltuk yerleşimi", "Tasarım"),
        new("tools.sightline", "Görüş hattı ve C-değeri", "Analiz", null, ["ısı haritası"]),
        new("tools.acoustic", "Akustik hızlı kontrol", "Analiz", null, ["rt60", "hacim"]),
        new("tools.quality", "Geometri kalite haritası", "Analiz", null, ["sızıntı", "çakışma"]),
        new("tools.reflector", "Yansıtıcı / akustik bulut dizisi", "Tasarım"),
        new("tools.underlay", "Referans altlık ekle", "Referans", null, ["dxf", "pdf", "görsel"]),
        new("tools.history", "Otomatik kayıt ve sürüm geçmişi", "Dosya", null, ["kurtar"])
    ];

    public static IReadOnlyList<SalonKomutTanimi> Ara(string? sorgu, int azami = 12)
    {
        if (string.IsNullOrWhiteSpace(sorgu)) return Komutlar.Take(azami).ToList();
        var q = sorgu.Trim();
        return Komutlar.Select(k => (K: k, Puan: Puan(k, q)))
            .Where(x => x.Puan > 0)
            .OrderByDescending(x => x.Puan)
            .ThenBy(x => x.K.Ad)
            .Take(Math.Clamp(azami, 1, 100))
            .Select(x => x.K).ToList();
    }

    private static int Puan(SalonKomutTanimi k, string q)
    {
        if (k.Ad.Equals(q, StringComparison.CurrentCultureIgnoreCase)) return 100;
        if (k.Ad.StartsWith(q, StringComparison.CurrentCultureIgnoreCase)) return 80;
        if (k.Ad.Contains(q, StringComparison.CurrentCultureIgnoreCase)) return 60;
        if (k.Grup.Contains(q, StringComparison.CurrentCultureIgnoreCase)) return 40;
        if (k.Kisayol?.Equals(q, StringComparison.OrdinalIgnoreCase) == true) return 70;
        if (k.AnahtarKelimeler.Any(x => x.Contains(q, StringComparison.CurrentCultureIgnoreCase))) return 50;
        // Harflerin sırayla geçtiği basit fuzzy eşleşme.
        int qi = 0;
        foreach (char c in k.Ad)
            if (qi < q.Length && char.ToUpperInvariant(c) == char.ToUpperInvariant(q[qi])) qi++;
        return qi == q.Length ? 20 : 0;
    }
}
