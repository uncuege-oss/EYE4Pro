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
    public const int GuncelSemaSurum = 2;

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
        Migrate(salon);
        Normalize(salon);
        SalonModelButunlukServisi.DogrulaVeFirlat(salon);

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
        SalonModelButunlukServisi.DogrulaVeFirlat(salon);
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
        SalonModelButunlukServisi.DogrulaVeFirlat(salon);
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
            SalonKatmanTipi.Pencere,
            SalonKatmanTipi.Yansitici
        };
        foreach (var t in tipler)
            if (!salon.Katmanlar.Any(k => k is not null && k.Tip == t))
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
        if (salon.SemaSurum < 1)
            salon.SemaSurum = 1;

        // Sürüm 1 → 2: kalıcı tasarım öğeleri eklendi. Koleksiyonlar property
        // initializer/Normalize ile oluşturulur; eski veya eksik dosyaya tüm semantik
        // katmanları birer kez eklemek yeni araçların güvenli varsayılanlarını sağlar.
        if (salon.SemaSurum < 2)
        {
            VarsayilanKatmanlariEkle(salon);
            salon.SemaSurum = 2;
        }
    }

    /// <summary>Eksik/eski JSON alanlarını domain'in null taşımayan sözleşmesine getirir.</summary>
    internal static void Normalize(SalonGeometri salon)
    {
        ArgumentNullException.ThrowIfNull(salon);

        salon.SalonAdi ??= string.Empty;
        // Mevcut koleksiyon örneklerini koru: editör ItemsSource bağları bu nesnelere
        // abonedir. Her kayıtta yeni ObservableCollection atamak UI'ı eski listede bırakır.
        salon.Katmanlar = KoleksiyonuNormalizeEt(salon.Katmanlar);
        salon.CizimOgeleri = KoleksiyonuNormalizeEt(salon.CizimOgeleri);
        salon.SeyirciSekciyonlari = KoleksiyonuNormalizeEt(salon.SeyirciSekciyonlari);
        salon.Balkonlar = KoleksiyonuNormalizeEt(salon.Balkonlar);
        salon.Acikliklar = KoleksiyonuNormalizeEt(salon.Acikliklar);
        salon.TavanProfili = KoleksiyonuNormalizeEt(salon.TavanProfili);
        salon.Olculendirmeler = KoleksiyonuNormalizeEt(salon.Olculendirmeler);
        salon.GeometrikKisitlar = KoleksiyonuNormalizeEt(salon.GeometrikKisitlar);
        salon.ReferansAltliklar = KoleksiyonuNormalizeEt(salon.ReferansAltliklar);
        salon.YansiticiPaneller = KoleksiyonuNormalizeEt(salon.YansiticiPaneller);

        // Üretilen her 3B kategorinin katman listesinde bir karşılığı olmalıdır.
        // Bu aynı zamanda doğrudan `new SalonGeometri()` ile oluşturulup kaydedilen
        // boş modelleri güvenli, tam bir semantik katman setine getirir.
        VarsayilanKatmanlariEkle(salon);

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

        foreach (var item in salon.Olculendirmeler)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            item.Ad ??= "Ölçü";
            item.SonEk ??= "m";
        }

        foreach (var item in salon.GeometrikKisitlar)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            item.Ad ??= "Kısıt";
            item.OgeId ??= string.Empty;
        }

        foreach (var item in salon.ReferansAltliklar)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            item.Ad ??= "Referans";
            item.DosyaYolu ??= string.Empty;
            item.Segmentler ??= [];
            item.Segmentler.RemoveAll(segment => segment is null);
            foreach (var segment in item.Segmentler)
                segment.Katman ??= "0";
        }

        foreach (var item in salon.YansiticiPaneller)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            item.Ad ??= "Yansıtıcı";
            item.Kategori ??= "Yansıtıcı";
            if (string.IsNullOrWhiteSpace(item.KatmanId))
                item.KatmanId = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Yansitici).Id;
        }

        salon.FasetKenarSayisi = Math.Clamp(salon.FasetKenarSayisi <= 0 ? 48 : salon.FasetKenarSayisi, 12, 128);
    }

    private static ObservableCollection<T> KoleksiyonuNormalizeEt<T>(ObservableCollection<T>? koleksiyon)
        where T : class
    {
        koleksiyon ??= new ObservableCollection<T>();
        for (int i = koleksiyon.Count - 1; i >= 0; i--)
            if (koleksiyon[i] is null)
                koleksiyon.RemoveAt(i);
        return koleksiyon;
    }
}
