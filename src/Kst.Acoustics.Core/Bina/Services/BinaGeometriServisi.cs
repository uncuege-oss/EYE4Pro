using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// `.e4bina.json` dosyasının kaydet / aç / migrasyon servisi.
public static class BinaGeometriServisi
{
    /// Geçerli şema sürümü — migrasyon tetikleyicisi.
    /// 2: düşey birleşim üyelerine kararlı KatId eklendi (kat sıralama dayanıklılığı).
    /// 3: DXF ölçek/normalize ofseti/çoklu-kat kaynak bölgesi kalıcılaştırıldı.
    public const int GuncelSemaSurum = 3;
    private const double MaxYerelKoordinatM = 10_000_000;
    private const double MaxDxfDunyaKoordinatM = 1_000_000_000_000;
    private const double MaxOdaAlaniM2 = 1_000_000_000;
    private const double MaxDxfBirimOlcegiM = 1_000_000;

    private static readonly JsonSerializerOptions JsonAyar = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly ConcurrentDictionary<string, object> KayitKilitleri = new(
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

    /// Geometriyi kaydet. `GeometriRevizyon` yalnız yazım BAŞARILI olduktan sonra artırılır
    /// (aksi halde disk hatasında bellek revizyonu dosyadakinden ileri kalır ve bir sonraki
    /// başarılı kayıt damgayı atlar). Atomik yazım: önce `.tmp`'ye yazılır, sonra hedefin
    /// üzerine taşınır — yarıda kalan yazım mevcut `.e4bina`'yı bozmaz.
    public static void Kaydet(BinaGeometri geo, string dosyaYolu)
    {
        ArgumentNullException.ThrowIfNull(geo);
        ArgumentException.ThrowIfNullOrWhiteSpace(dosyaYolu);
        string kilitAnahtari = Path.GetFullPath(dosyaYolu);
        lock (KayitKilitleri.GetOrAdd(kilitAnahtari, static _ => new object()))
        {
            // ConcurrentDictionary yalnız bu süreçteki çağrıları sıralar. Hedefe
            // bitişik kalıcı kilit dosyası, ayrı M50 süreçlerinin revizyon denetimi
            // ile atomik taşıma arasına birlikte girmesini önler. Kilit dosyasını
            // silmemek kasıtlıdır: Unix'te bekleyen süreç dosyayı açtıktan sonra
            // silmek, üçüncü bir sürecin yeni inode üzerinden kilidi aşmasına yol açar.
            using var dosyaKilidi = KayitDosyasiKilidiAl(kilitAnahtari + ".lock");
            KaydetKilitli(geo, dosyaYolu);
        }
    }

    private static void KaydetKilitli(BinaGeometri geo, string dosyaYolu)
    {
        EszamanliKaydiDogrula(geo, dosyaYolu);
        Normalize(geo);
        Validate(geo);

        int yeniRevizyon = geo.GeometriRevizyon + 1;
        string eskiDosyaYolu = geo.DosyaYolu ?? "";
        geo.DosyaYolu = dosyaYolu;
        geo.GeometriRevizyon = yeniRevizyon;
        string? tmp = null;
        try
        {
            var json = DosyaIcinSerilestir(geo, dosyaYolu);
            // Sabit .tmp adı farklı süreçlerin ara dosyasını paylaşmasına neden
            // olabilirdi. Her kayıt yalnız kendi geçici dosyasını temizler.
            tmp = dosyaYolu + $".{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, dosyaYolu, overwrite: true);
            tmp = null; // Move sonrası kaynak artık yoktur.
        }
        catch
        {
            geo.GeometriRevizyon = yeniRevizyon - 1;
            geo.DosyaYolu = eskiDosyaYolu;
            try
            {
                if (tmp is not null && File.Exists(tmp)) File.Delete(tmp);
            }
            catch
            {
                // Asıl kayıt hatasını gölgelememek için tmp temizleme hatası yutulur.
            }
            throw;
        }
    }

    private static FileStream KayitDosyasiKilidiAl(string kilitYolu)
    {
        const int denemeSayisi = 8;
        const int beklemeMs = 75;
        IOException? sonHata = null;

        for (int deneme = 1; deneme <= denemeSayisi; deneme++)
        {
            try
            {
                return new FileStream(
                    kilitYolu,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);
            }
            catch (IOException ex)
            {
                sonHata = ex;
                if (deneme < denemeSayisi) Thread.Sleep(beklemeMs);
            }
        }

        throw new InvalidOperationException(
            $"Kayıt kilidi alınamadı: '{Path.GetFileNameWithoutExtension(kilitYolu)}' " +
            "başka bir M50 oturumunda kaydediliyor. Kısa bir süre sonra yeniden deneyin.",
            sonHata);
    }

    /// Dosyadan yükle ve gerekiyorsa migrasyon uygula.
    /// Yükleme sonrası `[JsonIgnore]` olan `HamSegmentler`/`HamMetinler` boş kalır;
    /// DXF arka plan segmentleri `DxfSegmentler`'den (serileştirilmiş) alınır.
    public static BinaGeometri Ac(string dosyaYolu)
    {
        if (!File.Exists(dosyaYolu))
            throw new FileNotFoundException($"Bina geometrisi dosyası bulunamadı: {dosyaYolu}");

        var json = File.ReadAllText(dosyaYolu);
        var geo  = JsonSerializer.Deserialize<BinaGeometri>(json, JsonAyar)
                   ?? throw new InvalidDataException("Dosya okunamadı veya boş.");
        geo.DosyaYolu = dosyaYolu;
        Normalize(geo);
        Migrate(geo);
        DxfYollariniCoz(geo, dosyaYolu);
        Normalize(geo);
        Validate(geo);
        return geo;
    }

    /// Yeni boş geometri oluşturur.
    public static BinaGeometri YeniOlustur(string binaAdi = "")
        => new()
        {
            BinaAdi          = binaAdi,
            SemaSurum        = GuncelSemaSurum,
            GeometriRevizyon = 0
        };

    /// Kalıcı model alanlarının bağımsız bir kopyasını üretir. Kurtarma/dışa aktarma
    /// akışları bu kopyayı kaydederek projedeki canlı gömülü geometriyi değiştirmez.
    public static BinaGeometri DerinKopyala(BinaGeometri geo)
    {
        ArgumentNullException.ThrowIfNull(geo);
        var json = JsonSerializer.Serialize(geo, JsonAyar);
        var kopya = JsonSerializer.Deserialize<BinaGeometri>(json, JsonAyar)
            ?? throw new InvalidDataException("Bina geometrisi kopyalanamadı.");
        Normalize(kopya);
        return kopya;
    }

    /// Aynı dosyadan açılmış canlı model, diskte beklenmedik bir revizyon görürse
    /// başka bir M50 oturumunun değişikliğini ezmez. Farklı Kaydet yeni bir hedef
    /// olduğundan bu denetime girmez; mevcut hedef okunamıyorsa da güvenli tarafta
    /// kalıp açık bir hata üretir.
    private static void EszamanliKaydiDogrula(BinaGeometri geo, string dosyaYolu)
    {
        if (string.IsNullOrWhiteSpace(geo.DosyaYolu) ||
            !AyniDosyaYolu(geo.DosyaYolu, dosyaYolu) ||
            !File.Exists(dosyaYolu))
            return;

        BinaGeometri diskteki;
        try
        {
            diskteki = Ac(dosyaYolu);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Kayıt güvenliği: mevcut '{Path.GetFileName(dosyaYolu)}' dosyası " +
                $"okunamadığı veya doğrulanamadığı için üzerine yazılmadı. " +
                $"Dosyayı yeniden açın ya da Farklı Kaydet kullanın. ({ex.Message})", ex);
        }

        if (diskteki.GeometriRevizyon == geo.GeometriRevizyon) return;

        throw new InvalidOperationException(
            $"Kayıt çakışması: '{Path.GetFileName(dosyaYolu)}' başka bir oturumda " +
            $"değiştirilmiş (beklenen Rev. {geo.GeometriRevizyon}, disk Rev. " +
            $"{diskteki.GeometriRevizyon}). Dosyanın üzerine yazılmadı; yeniden açın " +
            $"ya da Farklı Kaydet kullanın.");
    }

    private static bool AyniDosyaYolu(string sol, string sag)
    {
        try
        {
            var karsilastirma = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(Path.GetFullPath(sol), Path.GetFullPath(sag), karsilastirma);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    // ── Migrasyon ─────────────────────────────────────────────────────────────

    private static void Migrate(BinaGeometri geo)
    {
        if (geo.SemaSurum > GuncelSemaSurum)
        {
            throw new InvalidDataException(
                $"Bina geometrisi daha yeni bir şema sürümüyle oluşturulmuş " +
                $"(sürüm {geo.SemaSurum}); bu uygulama en fazla sürüm {GuncelSemaSurum} açabilir.");
        }
        if (geo.SemaSurum < 0)
            throw new InvalidDataException($"Bina geometrisi şema sürümü geçersiz: {geo.SemaSurum}.");

        if (geo.SemaSurum >= GuncelSemaSurum) return;

        // Sürüm 0 → 1: ilk resmi şema; özel dönüşüm gerekmez.

        // Sürüm 1 → 2: düşey birleşim üyelerine kararlı KatId doldur (KatNo eşlemesinden).
        if (geo.SemaSurum < 2)
            BinaDuseyBirlesimServisi.KatIdleriTamamla(geo);

        // Sürüm 2 → 3: kalıcı DXF ölçek/ofset/kaynak-bölgesi metadata'sı eklendi.
        // Alanların 0/null varsayılanları eski kayıtlar için güvenli geri-dönüş
        // anlamına geldiğinden veri dönüşümü gerekmez.

        geo.SemaSurum = GuncelSemaSurum;
    }

    /// <summary>Eksik/eski JSON koleksiyonlarını null taşımayan modele getirir.</summary>
    private static void Normalize(BinaGeometri geo)
    {
        geo.BinaAdi ??= "";
        geo.Katlar ??= new ObservableCollection<BinaKat>();
        geo.DuseyBirlesimleri ??= new List<BinaDuseyBirlesim>();
        NullKayitlariSil(geo.Katlar);
        NullKayitlariSil(geo.DuseyBirlesimleri);

        foreach (var kat in geo.Katlar)
        {
            kat.Ad ??= "";
            kat.DxfFilePath ??= "";
            kat.Odalar ??= new ObservableCollection<BinaOda>();
            kat.Acikliklar ??= new List<BinaAciklik>();
            kat.DxfSegmentler ??= new List<BinaDxfSegment>();
            kat.DxfArclar ??= new List<BinaDxfArc>();
            NullKayitlariSil(kat.Odalar);
            NullKayitlariSil(kat.Acikliklar);
            NullKayitlariSil(kat.DxfSegmentler);
            NullKayitlariSil(kat.DxfArclar);
            kat.HamSegmentler ??= new List<(Point2D A, Point2D B)>();
            kat.TespitSegmentler ??= new List<(Point2D A, Point2D B)>();
            kat.HamMetinler ??= new List<(Point2D Pos, string Value)>();

            foreach (var oda in kat.Odalar)
            {
                oda.Ad ??= "";
                oda.Poligon ??= new List<Point2D>();
                if (oda.Poligon.Count >= 3 && oda.Poligon.All(SonluNoktaMi) &&
                    double.IsFinite(oda.AlanM2) && oda.AlanM2 >= 0)
                {
                    // Alan geometriye ait türetilmiş veridir. Eski/harici üreticinin
                    // ciddi tutarsız değerini kaydetmeden önce poligondan düzelt.
                    oda.AlanM2 = PoligonAlani(oda.Poligon);
                }
            }
            foreach (var a in kat.Acikliklar)
            {
                if (a.GenislikM != 0 || !SonluNoktaMi(a.Bas) || !SonluNoktaMi(a.Bit)) continue;
                double dx = a.Bit.X - a.Bas.X, dy = a.Bit.Y - a.Bas.Y;
                double uzunluk = Math.Sqrt(dx * dx + dy * dy);
                if (double.IsFinite(uzunluk) && uzunluk > 1e-8)
                    a.GenislikM = uzunluk;
            }
            foreach (var s in kat.DxfSegmentler) s.Layer ??= "";
        }

        foreach (var b in geo.DuseyBirlesimleri)
        {
            b.Uyeler ??= new List<BinaBirlesimUyesi>();
            NullKayitlariSil(b.Uyeler);
        }
    }

    private static void NullKayitlariSil<T>(IList<T> liste) where T : class
    {
        for (int i = liste.Count - 1; i >= 0; i--)
            if (liste[i] is null) liste.RemoveAt(i);
    }

    /// Canlı model mutlak yolu kullanmayı sürdürür; JSON içinde DXF yolları
    /// .e4bina dosyasının klasörüne göre göreli yazılır.
    private static string DosyaIcinSerilestir(BinaGeometri geo, string dosyaYolu)
    {
        string klasor = Path.GetDirectoryName(Path.GetFullPath(dosyaYolu))
            ?? Directory.GetCurrentDirectory();
        var dosyaKopyasi = DerinKopyala(geo);
        foreach (var kat in dosyaKopyasi.Katlar)
        {
            string yol = kat.DxfFilePath;
            if (string.IsNullOrWhiteSpace(yol) || !Path.IsPathFullyQualified(yol)) continue;
            string goreli = Path.GetRelativePath(klasor, Path.GetFullPath(yol));
            if (!Path.IsPathFullyQualified(goreli)) kat.DxfFilePath = goreli;
        }
        return JsonSerializer.Serialize(dosyaKopyasi, JsonAyar);
    }

    private static void DxfYollariniCoz(BinaGeometri geo, string dosyaYolu)
    {
        string klasor;
        try
        {
            klasor = Path.GetDirectoryName(Path.GetFullPath(dosyaYolu))
                ?? Directory.GetCurrentDirectory();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidDataException("Bina geometri dosya yolu geçersiz.", ex);
        }

        foreach (var kat in geo.Katlar)
        {
            if (string.IsNullOrWhiteSpace(kat.DxfFilePath) || Path.IsPathFullyQualified(kat.DxfFilePath))
                continue;
            try
            {
                kat.DxfFilePath = Path.GetFullPath(Path.Combine(klasor, kat.DxfFilePath));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new InvalidDataException($"'{kat.Ad}' katının DXF yolu geçersiz.", ex);
            }
        }
    }

    private static void Validate(BinaGeometri geo)
    {
        if (geo.SemaSurum > GuncelSemaSurum)
            throw new InvalidDataException(
                $"Bina geometrisi şema sürümü {geo.SemaSurum}; desteklenen en yeni sürüm {GuncelSemaSurum}.");
        if (geo.SemaSurum < 0)
            throw new InvalidDataException($"Bina geometrisi şema sürümü geçersiz: {geo.SemaSurum}.");
        if (geo.GeometriRevizyon < 0)
            throw new InvalidDataException("GeometriRevizyon negatif olamaz.");
        if (geo.GeometriRevizyon == int.MaxValue)
            throw new InvalidDataException("GeometriRevizyon üst sınıra ulaştı; kayıt artırılamaz.");

        var katIdleri = new HashSet<string>(StringComparer.Ordinal);
        var odaIdleri = new HashSet<string>(StringComparer.Ordinal);
        var aciklikIdleri = new HashSet<string>(StringComparer.Ordinal);
        var birlesimIdleri = new HashSet<string>(StringComparer.Ordinal);
        var aktifBirlesimOdaIdleri = new HashSet<string>(StringComparer.Ordinal);
        var katNolari = new HashSet<int>();

        foreach (var kat in geo.Katlar)
        {
            KimlikDogrula(kat.Id, "Kat", katIdleri);
            if (!katNolari.Add(kat.KatNo))
                throw new InvalidDataException($"Yinelenen KatNo bulundu: {kat.KatNo}.");
            Aralikta(kat.KatYuksekligiM, 0.2, 100, $"'{kat.Ad}' kat yüksekliği");
            NoktaDogrula(kat.OriginOffset, $"'{kat.Ad}' OriginOffset");
            Aralikta(kat.DxfBirimOlcegiM, 0, MaxDxfBirimOlcegiM,
                $"'{kat.Ad}' DXF birim ölçeği");
            NoktaDogrula(kat.DxfNormalizeOfsetiM, $"'{kat.Ad}' DXF normalize ofseti",
                MaxDxfDunyaKoordinatM);

            if (kat.DxfKaynakBolgesiMinM.HasValue != kat.DxfKaynakBolgesiMaxM.HasValue)
                throw new InvalidDataException(
                    $"'{kat.Ad}' DXF kaynak bölgesinin min ve max noktaları birlikte verilmelidir.");
            if (kat.DxfKaynakBolgesiMinM is { } kaynakMin &&
                kat.DxfKaynakBolgesiMaxM is { } kaynakMax)
            {
                NoktaDogrula(kaynakMin, $"'{kat.Ad}' DXF kaynak bölgesi min",
                    MaxDxfDunyaKoordinatM);
                NoktaDogrula(kaynakMax, $"'{kat.Ad}' DXF kaynak bölgesi max",
                    MaxDxfDunyaKoordinatM);
                if (kaynakMin.X > kaynakMax.X || kaynakMin.Y > kaynakMax.Y)
                    throw new InvalidDataException(
                        $"'{kat.Ad}' DXF kaynak bölgesinde min değerleri max değerlerini aşamaz.");
                if (kat.DxfBirimOlcegiM <= 0)
                    throw new InvalidDataException(
                        $"'{kat.Ad}' DXF kaynak bölgesi için pozitif birim ölçeği gereklidir.");
            }

            foreach (var oda in kat.Odalar)
            {
                KimlikDogrula(oda.Id, "Oda", odaIdleri);
                Aralikta(oda.AlanM2, 0, MaxOdaAlaniM2, $"'{oda.Ad}' oda alanı");
                if (oda.YukseklikOverrideM is { } h)
                    Aralikta(h, 0.2, 100, $"'{oda.Ad}' oda yüksekliği");
                if (oda.Poligon.Count < 3)
                    throw new InvalidDataException($"'{oda.Ad}' odasının en az 3 poligon köşesi olmalı.");
                for (int i = 0; i < oda.Poligon.Count; i++)
                    NoktaDogrula(oda.Poligon[i], $"'{oda.Ad}' poligon[{i}]");
                PoligonDogrula(oda);
            }

            foreach (var a in kat.Acikliklar)
            {
                KimlikDogrula(a.Id, "Açıklık", aciklikIdleri);
                NoktaDogrula(a.Bas, $"Açıklık '{a.Id}' başlangıcı");
                NoktaDogrula(a.Bit, $"Açıklık '{a.Id}' bitişi");
                if (!Enum.IsDefined(a.Tip))
                    throw new InvalidDataException($"Açıklık '{a.Id}' tipi geçersiz: {(int)a.Tip}.");
                Aralikta(a.GenislikM, 0, 100, $"Açıklık '{a.Id}' genişliği");
                Aralikta(a.YukseklikM, 0, 100, $"Açıklık '{a.Id}' yüksekliği");
                Aralikta(a.EsikYuksekligiM, 0, 100, $"Açıklık '{a.Id}' eşik yüksekliği");
                double segmentGenisligi = Math.Sqrt(
                    (a.Bit.X - a.Bas.X) * (a.Bit.X - a.Bas.X) +
                    (a.Bit.Y - a.Bas.Y) * (a.Bit.Y - a.Bas.Y));
                if (segmentGenisligi <= 1e-8)
                    throw new InvalidDataException($"Açıklık '{a.Id}' sıfır uzunlukta olamaz.");
                // Eski üreticilerin cm düzeyinde yuvarlamasını kabul et; fiziksel
                // segmentle belirgin biçimde çelişen genişliği yine reddet.
                if (Math.Abs(a.GenislikM - segmentGenisligi) > Math.Max(0.02, segmentGenisligi * 0.05))
                    throw new InvalidDataException(
                        $"Açıklık '{a.Id}' genişliği ({a.GenislikM:G}) uç noktalarıyla ({segmentGenisligi:G}) tutarsız.");
                if (a.EtkinEsikYuksekligiM + a.YukseklikM > kat.KatYuksekligiM + 1e-9)
                    throw new InvalidDataException(
                        $"Açıklık '{a.Id}' eşik + yüksekliği kat yüksekliğini aşamaz.");
            }

            foreach (var s in kat.DxfSegmentler)
            {
                NoktaDogrula(s.A, "DXF segment başlangıcı");
                NoktaDogrula(s.B, "DXF segment bitişi");
            }
            foreach (var arc in kat.DxfArclar)
            {
                NoktaDogrula(arc.Merkez, "DXF arc merkezi");
                Aralikta(arc.R, 0, MaxYerelKoordinatM, "DXF arc yarıçapı");
                Sonlu(arc.BasAciDeg, "DXF arc başlangıç açısı");
                Sonlu(arc.BitAciDeg, "DXF arc bitiş açısı");
            }
        }

        foreach (var b in geo.DuseyBirlesimleri)
        {
            KimlikDogrula(b.Id, "Düşey birleşim", birlesimIdleri);
            var uyeAnahtarlari = new HashSet<string>(StringComparer.Ordinal);
            foreach (var u in b.Uyeler)
            {
                if (string.IsNullOrWhiteSpace(u.OdaId))
                    throw new InvalidDataException($"Düşey birleşim '{b.Id}' boş OdaId içeriyor.");
                if (!uyeAnahtarlari.Add(u.OdaId))
                    throw new InvalidDataException($"Düşey birleşim '{b.Id}' yinelenen oda içeriyor: {u.OdaId}.");
                if (b.AktifMi && !aktifBirlesimOdaIdleri.Add(u.OdaId))
                    throw new InvalidDataException(
                        $"Oda '{u.OdaId}' birden fazla aktif düşey birleşimde yer alamaz.");

                // KatId sürüm 2'de zorunludur. Eski sürüm kaydedilirken henüz
                // migrasyon uygulanmamış olabileceğinden yalnız güncel modelde denetlenir.
                if (geo.SemaSurum >= 2)
                {
                    if (string.IsNullOrWhiteSpace(u.KatId) || !katIdleri.Contains(u.KatId))
                        throw new InvalidDataException(
                            $"Düşey birleşim '{b.Id}' geçersiz KatId içeriyor: '{u.KatId}'.");
                    var kat = geo.Katlar.First(k => k.Id == u.KatId);
                    if (!kat.Odalar.Any(o => o.Id == u.OdaId))
                        throw new InvalidDataException(
                            $"Düşey birleşim '{b.Id}' odası '{u.OdaId}' kat '{u.KatId}' içinde bulunamadı.");
                }
            }
        }
    }

    private static void KimlikDogrula(string? id, string tur, HashSet<string> kullanilan)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidDataException($"{tur} kimliği boş olamaz.");
        if (!kullanilan.Add(id))
            throw new InvalidDataException($"Yinelenen {tur} kimliği bulundu: '{id}'.");
    }

    private static void PoligonDogrula(BinaOda oda)
    {
        var p = oda.Poligon;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            double dx = a.X - b.X, dy = a.Y - b.Y;
            if (dx * dx + dy * dy <= 1e-16)
                throw new InvalidDataException(
                    $"'{oda.Ad}' odasının ardışık poligon köşeleri yinelenemez (indeks {i}).");
        }

        double alan = PoligonAlani(p);
        if (!double.IsFinite(alan) || alan <= 1e-8)
            throw new InvalidDataException($"'{oda.Ad}' odasının poligon alanı sıfır olamaz.");

        // Tipik oda poligonları onlarca köşedir. Çok büyük poligonlarda O(n²)
        // kesişim taraması kaydı kilitlemesin; temel alan/duplicate invariantları
        // yine uygulanır.
        if (p.Count <= 2_000 && PoligonKendiniKesiyor(p))
            throw new InvalidDataException($"'{oda.Ad}' odasının poligonu kendi kendini kesemez.");

        double fark = Math.Abs(oda.AlanM2 - alan);
        if (fark > Math.Max(0.01, alan * 1e-6))
            throw new InvalidDataException(
                $"'{oda.Ad}' oda alanı ({oda.AlanM2:G}) poligon alanıyla ({alan:G}) tutarsız.");
    }

    private static bool PoligonKendiniKesiyor(IReadOnlyList<Point2D> p)
    {
        int n = p.Count;
        for (int i = 0; i < n; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % n];
            for (int j = i + 1; j < n; j++)
            {
                if (j == i || j == i + 1 || (i == 0 && j == n - 1)) continue;
                var c = p[j];
                var d = p[(j + 1) % n];
                if (SegmentlerKesisiyor(a, b, c, d)) return true;
            }
        }
        return false;
    }

    private static bool SegmentlerKesisiyor(Point2D a, Point2D b, Point2D c, Point2D d)
    {
        double olcek = new[]
        {
            Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y),
            Math.Abs(d.X - c.X), Math.Abs(d.Y - c.Y),
            Math.Abs(c.X - a.X), Math.Abs(c.Y - a.Y)
        }.Max();
        double eps = 1e-10 * Math.Max(1.0, olcek * olcek);
        double o1 = Yon(a, b, c), o2 = Yon(a, b, d);
        double o3 = Yon(c, d, a), o4 = Yon(c, d, b);

        if (((o1 > eps && o2 < -eps) || (o1 < -eps && o2 > eps)) &&
            ((o3 > eps && o4 < -eps) || (o3 < -eps && o4 > eps)))
            return true;
        if (Math.Abs(o1) <= eps && KutuIcinde(c, a, b, eps)) return true;
        if (Math.Abs(o2) <= eps && KutuIcinde(d, a, b, eps)) return true;
        if (Math.Abs(o3) <= eps && KutuIcinde(a, c, d, eps)) return true;
        return Math.Abs(o4) <= eps && KutuIcinde(b, c, d, eps);
    }

    private static double Yon(Point2D a, Point2D b, Point2D c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool KutuIcinde(Point2D p, Point2D a, Point2D b, double eps)
        => p.X >= Math.Min(a.X, b.X) - eps && p.X <= Math.Max(a.X, b.X) + eps &&
           p.Y >= Math.Min(a.Y, b.Y) - eps && p.Y <= Math.Max(a.Y, b.Y) + eps;

    private static bool SonluNoktaMi(Point2D p)
        => double.IsFinite(p.X) && double.IsFinite(p.Y);

    private static double PoligonAlani(IReadOnlyList<Point2D> p)
    {
        double ikiAlan = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            ikiAlan += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(ikiAlan) * 0.5;
    }

    private static void NoktaDogrula(
        Point2D p, string ad, double maxMutlak = MaxYerelKoordinatM)
    {
        Aralikta(p.X, -maxMutlak, maxMutlak, ad + " X");
        Aralikta(p.Y, -maxMutlak, maxMutlak, ad + " Y");
    }

    private static void Aralikta(double deger, double min, double max, string ad)
    {
        Sonlu(deger, ad);
        if (deger < min || deger > max)
            throw new InvalidDataException(
                $"{ad} {min:G}–{max:G} aralığında olmalı; bulunan {deger:G}.");
    }

    private static void Sonlu(double deger, string ad)
    {
        if (!double.IsFinite(deger))
            throw new InvalidDataException($"{ad} sonlu bir sayı olmalı.");
    }
}
