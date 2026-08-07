using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// `.e4salon.json` dosyasının kaydet / aç / migrasyon servisi.
/// M50 <c>BinaGeometriServisi</c> ile aynı desen.
public static class SalonGeometriServisi
{
    /// Geçerli şema sürümü — migrasyon tetikleyicisi.
    public const int GuncelSemaSurum = 1;

    private static readonly JsonSerializerOptions JsonAyar = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// Geometriyi kaydet. `GeometriRevizyon` her kayıtta artırılır.
    /// **Atomik yazım:** önce geçici dosyaya yazılır, sonra hedefin üzerine taşınır —
    /// yazım yarıda kesilirse mevcut dosya bozulmaz. Yazım başarısız olursa revizyon
    /// artışı geri alınır (bellek ↔ disk revizyonu ayrışmaz).
    public static void Kaydet(SalonGeometri salon, string dosyaYolu)
    {
        ArgumentNullException.ThrowIfNull(salon);
        ArgumentException.ThrowIfNullOrWhiteSpace(dosyaYolu);
        Normalize(salon);

        salon.GeometriRevizyon++;
        var tmp = dosyaYolu + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(salon, JsonAyar);
            File.WriteAllText(tmp, json);
            File.Move(tmp, dosyaYolu, overwrite: true);
            salon.DosyaYolu = dosyaYolu;
        }
        catch
        {
            salon.GeometriRevizyon--;
            try
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            catch
            {
                // Asıl kayıt hatasını gölgelememek için geçici dosya temizleme hatası yutulur.
            }
            throw;
        }
    }

    /// Geometrinin derin kopyasını JSON olarak verir (undo/redo anlık görüntüsü).
    public static string KopyaJson(SalonGeometri salon)
        => JsonSerializer.Serialize(salon, JsonAyar);

    /// <see cref="KopyaJson"/> çıktısından geometriyi geri yükler.
    public static SalonGeometri KopyadanYukle(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var salon = JsonSerializer.Deserialize<SalonGeometri>(json, JsonAyar)
                    ?? throw new InvalidDataException("Anlık görüntü okunamadı.");
        Migrate(salon);
        Normalize(salon);
        return salon;
    }

    /// Dosyadan yükle ve gerekiyorsa migrasyon uygula.
    public static SalonGeometri Ac(string dosyaYolu)
    {
        if (!File.Exists(dosyaYolu))
            throw new FileNotFoundException($"Salon geometrisi dosyası bulunamadı: {dosyaYolu}");

        var json  = File.ReadAllText(dosyaYolu);
        var salon = JsonSerializer.Deserialize<SalonGeometri>(json, JsonAyar)
                    ?? throw new InvalidDataException("Dosya okunamadı veya boş.");
        salon.DosyaYolu = dosyaYolu;
        Migrate(salon);
        Normalize(salon);
        return salon;
    }

    /// Yeni boş salon geometrisi oluşturur; standart katmanları tohumlar.
    public static SalonGeometri YeniOlustur(string salonAdi = "")
    {
        var salon = new SalonGeometri
        {
            SalonAdi         = salonAdi,
            SemaSurum        = GuncelSemaSurum,
            GeometriRevizyon = 0
        };
        VarsayilanKatmanlariEkle(salon);
        return salon;
    }

    /// Standart salon katmanlarını (zemin, sahne, duvarlar, tavan, seyirci, korniş, balkon)
    /// ekler. Korniş/Balkon 2D'de çizilmese de seyirci kademesi ve balkon platformlarının
    /// 3D kategorileri olduğundan katman listesinde görünür (M01 kategori seti tutarlı kalır).
    public static void VarsayilanKatmanlariEkle(SalonGeometri salon)
    {
        ArgumentNullException.ThrowIfNull(salon);
        salon.Katmanlar ??= new ObservableCollection<SalonKatman>();

        SalonKatmanTipi[] tipler =
        {
            SalonKatmanTipi.Zemin,
            SalonKatmanTipi.Sahne,
            SalonKatmanTipi.DuvarSahne,
            SalonKatmanTipi.DuvarSeyirci,
            SalonKatmanTipi.Tavan,
            SalonKatmanTipi.Seyirci,
            SalonKatmanTipi.Kornis,
            SalonKatmanTipi.Balkon,
            SalonKatmanTipi.Orkestra,
            SalonKatmanTipi.Kapi,
            SalonKatmanTipi.Pencere
        };
        foreach (var t in tipler)
            salon.Katmanlar.Add(SalonKatman.Olustur(t));
    }

    // ── Migrasyon ─────────────────────────────────────────────────────────────

    private static void Migrate(SalonGeometri salon)
    {
        if (salon.SemaSurum > GuncelSemaSurum)
        {
            throw new InvalidDataException(
                $"Salon dosyası daha yeni bir şema sürümüyle oluşturulmuş (sürüm {salon.SemaSurum}); "
                + $"bu uygulama en fazla sürüm {GuncelSemaSurum} açabilir.");
        }

        if (salon.SemaSurum < 0)
        {
            throw new InvalidDataException($"Salon dosyasının şema sürümü geçersiz: {salon.SemaSurum}.");
        }

        if (salon.SemaSurum >= GuncelSemaSurum) return;

        // Sürüm 0 → 1: ilk resmi şema; özel dönüşüm gerekmez.
        salon.SemaSurum = GuncelSemaSurum;
    }

    /// <summary>Eksik/eski JSON alanlarını domain'in null taşımayan sözleşmesine getirir.</summary>
    internal static void Normalize(SalonGeometri salon)
    {
        ArgumentNullException.ThrowIfNull(salon);

        salon.SalonAdi ??= string.Empty;
        salon.Katmanlar = new ObservableCollection<SalonKatman>(
            (salon.Katmanlar ?? []).Where(item => item is not null));
        salon.CizimOgeleri = new ObservableCollection<SalonCizimOgesi>(
            (salon.CizimOgeleri ?? []).Where(item => item is not null));
        salon.SeyirciSekciyonlari = new ObservableCollection<SeyirciSekciyonu>(
            (salon.SeyirciSekciyonlari ?? []).Where(item => item is not null));
        salon.Balkonlar = new ObservableCollection<BalkonGeometri>(
            (salon.Balkonlar ?? []).Where(item => item is not null));
        salon.Acikliklar = new ObservableCollection<DuvarAcikligi>(
            (salon.Acikliklar ?? []).Where(item => item is not null));
        salon.TavanProfili = new ObservableCollection<TavanKirilimNoktasi>(
            (salon.TavanProfili ?? []).Where(item => item is not null));

        foreach (var item in salon.Katmanlar)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(item.Ad)) item.Ad = SalonKatman.VarsayilanAd(item.Tip);
            if (string.IsNullOrWhiteSpace(item.RenkHex)) item.RenkHex = SalonKatman.VarsayilanRenk(item.Tip);
        }

        foreach (var item in salon.CizimOgeleri)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            item.KatmanId ??= string.Empty;
            item.Noktalar ??= [];
        }

        foreach (var item in salon.SeyirciSekciyonlari)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            item.Ad ??= "Seyirci";
        }

        foreach (var item in salon.Balkonlar)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            item.Ad ??= "Balkon";
        }

        foreach (var item in salon.Acikliklar)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            item.Ad ??= item.Tip == AciklikTipi.Kapi ? "Kapı" : "Pencere";
        }

        salon.FasetKenarSayisi = Math.Clamp(salon.FasetKenarSayisi <= 0 ? 48 : salon.FasetKenarSayisi, 12, 128);
    }
}
