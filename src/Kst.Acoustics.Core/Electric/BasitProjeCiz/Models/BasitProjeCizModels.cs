using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

/// Armatür sembolünü oluşturan tek bir çizgi parçası (normalize edilmiş koordinatlar).
/// Koordinat aralığı tipik olarak ±1.0 civarındadır; canvas'a ölçeklenerek çizilir.
public readonly record struct BpcSembolCizgi(double Ax, double Ay, double Bx, double By);

public class BpcDxfSegment
{
    public Point2D A { get; set; }
    public Point2D B { get; set; }
    public string Layer { get; set; } = "";
}

public class BasitProjeCizProje
{
    /// Dosya şema sürümü. 0 = sürümsüz (eski) dosya. Yükleme sırasında
    /// migrasyonların tetiklenmesi için kullanılır; kayıtta güncel sürüm yazılır.
    public int SemaSurum { get; set; }
    /// Projenin ait olduğu modül disiplini: M12 = KuvvetliAkim (.e4p12),
    /// M16 = ZayifAkim (.e4p16). Eski dosyalarda alan yoktur → KuvvetliAkim okunur.
    public BpcDisiplin ModulDisiplini { get; set; } = BpcDisiplin.KuvvetliAkim;
    public string ProjeAdi { get; set; } = "Basit Proje";
    public ObservableCollection<BasitProjeKat> Katlar { get; set; } = new();
    public string? DosyaYolu { get; set; }

    /// Son çalışma oturumundan seçili kat numarası. 0 = kaydedilmemiş (ilk kata düş).
    public int SeciliKatNo { get; set; }

    // ── M50 bina geometrisi bağlantısı ────────────────────────────────────
    /// Bağlı .e4bina dosyasının yolu (varsa). Dosya taşınmışsa gömülü kopya kullanılır.
    public string? BinaGeometriYolu { get; set; }
    /// Yüklenen geometrinin revizyon damgası — .e4bina'daki değerden küçükse
    /// "geometri güncellenmiş" uyarısı gösterilir.
    public int? BinaGeometriRevizyon { get; set; }
    /// Yüklenen geometrinin gömülü kopyası — proje .e4bina olmadan da açılabilir.
    public Kst.Acoustics.Core.Bina.Models.BinaGeometri? BinaGeometriKopya { get; set; }
}

public class BasitProjeKat : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Kat 1";
    public int KatNo { get; set; } = 1;
    public double KatYuksekligiM { get; set; } = 3.0;
    public string DxfFilePath { get; set; } = "";
    public string? UnitWarning { get; set; }

    private bool _dxfGoster = true;
    private bool _bloklariGoster = true;

    /// Bu katın görünüm penceresinde DXF arka plan çizgilerinin gösterilip gösterilmediği.
    public bool DxfGoster
    {
        get => _dxfGoster;
        set => Set(ref _dxfGoster, value);
    }

    /// Bu katın oda blokları (poligon dolgusu + ad) viewport'ta gösterilsin mi.
    public bool BloklariGoster
    {
        get => _bloklariGoster;
        set => Set(ref _bloklariGoster, value);
    }

    /// Farklı DXF orijinleri için manuel hizalama ofseti (m).
    public Point2D OriginOffset { get; set; } = new(0, 0);

    public ObservableCollection<BasitProjeOda> Odalar { get; set; } = new();
    public ObservableCollection<BpcDxfSegment> DxfSegmentleri { get; set; } = new();
    public ObservableCollection<BasitProjeEleman> Elemanlar { get; set; } = new();

    // ─── Kablo sistemi ───────────────────────────────────────────────────
    public List<BpcDevre>      Devreler      { get; set; } = new();
    public List<BpcVavienGrup> VavienGruplari { get; set; } = new();
    public List<BpcKablo>      Kablolar      { get; set; } = new();
    public List<BpcBuatPanoBaglanti> BuatPanoBaglantilari { get; set; } = new();
    public List<BpcPrizDevre>  PrizDevreleri { get; set; } = new();
    public List<BpcMekanikDevre> MekanikDevreleri { get; set; } = new();
    public List<BpcKabloTavasi> KabloTavalari { get; set; } = new();
    public List<BpcPanoBeslemeHatti> PanoBeslemeHatlari { get; set; } = new();
    public BpcTopraklama Topraklama { get; set; } = new();
    /// Zayıf akım (Data/TV/Telefon/CCTV/Anons/Yangın) devreleri — genel model.
    public List<BpcSistemDevre> SistemDevreleri { get; set; } = new();

    /// Mahal listesi: kullanıcının elle girdiği oda × malzeme adet matrisi satırları.
    public List<BpcMahalSatiri> MahalListesi { get; set; } = new();

    /// DXF'ten okunan ham ARC entity'leri (kapı tespiti için). Aynı koordinat çerçevesi
    /// (ölçek + origin-normalize) RawDxfSegments ile aynıdır.
    public List<BpcArc> DxfArclari { get; set; } = new();

    /// Tespit edilmiş veya manuel eklenmiş açıklıklar (kapı + pencere). Otomatik
    /// priz/anahtar yerleştirme bu listeyi "no-go bölgesi" olarak kullanır.
    public List<BpcAciklik> Acikliklar { get; set; } = new();

    public override string ToString() => $"{Ad} ({KatYuksekligiM:N2} m)";
}

/// Mahal listesi tablosunda bir satır = bir malzeme tipi. Sayilar oda Id → adet.
public class BpcMahalHucreAyar : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private bool _aktif = false;
    private bool _genelAyarKullan = true;
    private string _yerlesimTipi = "Adet";
    private string _montajTipi = "Tavan";
    private double _gridAraligiM = 3.0;
    private double? _gridAraligiYM;
    private double _duvarAraligiM = 3.0;
    private int _adet = 0;
    private bool _sensorlu = false;

    public bool Aktif { get => _aktif; set => Set(ref _aktif, value); }
    public bool GenelAyarKullan { get => _genelAyarKullan; set => Set(ref _genelAyarKullan, value); }
    /// Yalnız armatür satırlarında ve oda-özel ayar modunda anlamlıdır. true ise bu
    /// odaya yerleştirilen her armatüre ayrı bir sensör eklenir ve armatürler duvar
    /// anahtarı yerine doğrudan oda buatına (sensör üzerinden) bağlanır.
    public bool Sensorlu { get => _sensorlu; set => Set(ref _sensorlu, value); }
    public string YerlesimTipi { get => _yerlesimTipi; set => Set(ref _yerlesimTipi, value); }
    public string MontajTipi { get => _montajTipi; set => Set(ref _montajTipi, value); }
    public double GridAraligiM { get => _gridAraligiM; set => Set(ref _gridAraligiM, value); }
    /// Grid Y aralığı (m). null = X ile aynı (kare grid).
    public double? GridAraligiYM { get => _gridAraligiYM; set => Set(ref _gridAraligiYM, value); }
    public double DuvarAraligiM { get => _duvarAraligiM; set => Set(ref _duvarAraligiM, value); }
    public int Adet { get => _adet; set => Set(ref _adet, value); }

    // ── Oda-özel eleman özellikleri (boş/null = sembol varsayılanını kullan) ──
    private string _ozelAd = "";
    private double? _ozelGucW;
    private int? _ozelMonofazeAdet;
    private int? _ozelTrifazeAdet;
    private double? _ozelMegapiksel;
    private int? _ozelCihazAdet;

    /// Bu odadaki elemanlara verilecek özel ad (boşsa sembol adı kullanılır).
    public string OzelAd { get => _ozelAd; set => Set(ref _ozelAd, value); }
    /// Bu odadaki elemanların güç değeri (W). null = sembol varsayılanı.
    public double? OzelGucW { get => _ozelGucW; set => Set(ref _ozelGucW, value); }
    public int? OzelMonofazeAdet { get => _ozelMonofazeAdet; set => Set(ref _ozelMonofazeAdet, value); }
    public int? OzelTrifazeAdet { get => _ozelTrifazeAdet; set => Set(ref _ozelTrifazeAdet, value); }
    public double? OzelMegapiksel { get => _ozelMegapiksel; set => Set(ref _ozelMegapiksel, value); }
    /// Tek sembolün temsil ettiği cihaz adedi (OzelAlanTuru == "Adet").
    public int? OzelCihazAdet { get => _ozelCihazAdet; set => Set(ref _ozelCihazAdet, value); }
}

public class BpcMahalSatiri : INotifyPropertyChanged
{
    // Genel ayarlar INPC bildirir (Set eşitlik korumalı) — UI, gerçek değişiklikleri
    // IsDirty'ye bağlayabilsin diye. JSON serileştirme etkilenmez.
    public event PropertyChangedEventHandler? PropertyChanged;

    /// Aynı sembolü kullanan mahal satırlarını birbirinden ayıran kararlı kimlik.
    public string Id { get; set; } = Guid.NewGuid().ToString();

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public BpcElemanTipi Tip { get; set; }
    public Dictionary<string, int> Sayilar { get; set; } = new(); // Backwards compatibility & general manual count
    public Dictionary<string, BpcMahalHucreAyar> HucreAyarlari { get; set; } = new();

    /// Yerleştirme için kullanılacak spesifik sembol (BpcArmatürTanim.Id). Null ise otomatik yerleştirme atlanır.
    public string? ArmatürTanimId { get; set; }
    /// Kütüphane yüklü değilken görüntüleme için önbelleğe alınmış sembol adı.
    public string? ArmatürTanimAd { get; set; }

    private string _yerlesimTipi = "Adet";
    private string _montajTipi = "Tavan";
    private double _gridAraligiM = 3.0;
    private double? _gridAraligiYM;
    private double _duvarAraligiM = 3.0;
    private double? _gelenGucW;

    public string YerlesimTipi { get => _yerlesimTipi; set => Set(ref _yerlesimTipi, value); }
    // Geçerli değerler:
    //   "Grid"          — tavan grid (armatür)
    //   "Adet"          — belirli adet, konum otomatik
    //   "Duvar aralıklı"— tüm duvarlara eşit aralık
    //   "Açıklık Yanı"  — kapı açıklığından itibaren, anahtar+buat sonrası (priz)
    //   "Homojen Duvar" — tüm duvarlara homojen dağılım (priz)
    public string MontajTipi { get => _montajTipi; set => Set(ref _montajTipi, value); } // "Tavan", "Duvar"
    public double GridAraligiM { get => _gridAraligiM; set => Set(ref _gridAraligiM, value); }
    /// Grid Y aralığı (m). null = X ile aynı (kare grid).
    public double? GridAraligiYM { get => _gridAraligiYM; set => Set(ref _gridAraligiYM, value); }
    public double DuvarAraligiM { get => _duvarAraligiM; set => Set(ref _duvarAraligiM, value); }
    /// Tüm odalar için genel güç değeri (W). null = sembol varsayılanı.
    public double? GelenGucW { get => _gelenGucW; set => Set(ref _gelenGucW, value); }

    /// Efektif güç (W): oda-özel mod ve OzelGucW doluysa onu, aksi halde GelenGucW döner.
    public double? EfektifGucW(string odaId)
    {
        var ayar = HucreGetirVeyaOlustur(odaId);
        if (!ayar.GenelAyarKullan && ayar.OzelGucW.HasValue) return ayar.OzelGucW;
        return GelenGucW;
    }
    
    public BpcMahalHucreAyar HucreGetirVeyaOlustur(string odaId)
    {
        if (!HucreAyarlari.TryGetValue(odaId, out var ayar))
        {
            ayar = new BpcMahalHucreAyar();
            // Migrate old Sayilar to new logic if present
            if (Sayilar.TryGetValue(odaId, out int adet) && adet > 0)
            {
                ayar.Aktif = true;
                ayar.Adet = adet;
            }
            HucreAyarlari[odaId] = ayar;
        }
        return ayar;
    }

    public (string yerlesim, string montaj, double grid, double duvar) EfektifAyarlar(string odaId)
    {
        var ayar = HucreGetirVeyaOlustur(odaId);
        if (ayar.GenelAyarKullan)
            return (YerlesimTipi, MontajTipi, GridAraligiM, DuvarAraligiM);
        return (ayar.YerlesimTipi, ayar.MontajTipi, ayar.GridAraligiM, ayar.DuvarAraligiM);
    }

    /// Efektif grid Y aralığı. null = X ile aynı (kare grid).
    public double? EfektifGridY(string odaId)
    {
        var ayar = HucreGetirVeyaOlustur(odaId);
        return ayar.GenelAyarKullan ? GridAraligiYM : ayar.GridAraligiYM;
    }

    public bool EfektifAktif(string odaId)
    {
        // Hücrenin aktifliği yalnız kullanıcının o oda+malzeme kesişimini açıkça
        // etkinleştirmesiyle belirlenir. Yerleşim tipi (Grid/Duvar/Adet) yalnız
        // NASIL yerleşeceğini değiştirir; HANGİ odalara yerleşeceğini değil.
        return HucreGetirVeyaOlustur(odaId).Aktif;
    }

    /// Sensör yalnız oda-özel ayar modunda (genel ayar kullanılmıyorken) geçerlidir.
    public bool EfektifSensorlu(string odaId)
    {
        var ayar = HucreGetirVeyaOlustur(odaId);
        return !ayar.GenelAyarKullan && ayar.Sensorlu;
    }
}

/// DXF'ten okunan bir ARC entity (kapı yayı tespiti için). Açılar derece cinsindendir.
public class BpcArc
{
    public Point2D Merkez { get; set; }
    public double R { get; set; }
    public double BasAciDeg { get; set; }
    public double BitAciDeg { get; set; }
}

/// Duvar üzerinde "açıklık" (kapı veya pencere) sayılan segment. Otomatik yerleştirme
/// için no-go bölgesi olarak kullanılır.
public enum BpcAciklikTipi
{
    Kapı,
    Pencere
}

public class BpcAciklik
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Point2D Bas { get; set; }
    public Point2D Bit { get; set; }
    public BpcAciklikTipi Tip { get; set; } = BpcAciklikTipi.Kapı;
    /// Kullanıcı "bu açıklık değil" derse true. Yeniden taramada korunur; görselde soluk gri.
    public bool IptalEdildi { get; set; }
    /// Kullanıcı elle ekledi mi? true ise yeniden tarama bu kaydı silmez.
    public bool ManuelEklendi { get; set; }
    /// Debug için: "arc" (kapı yayı), "gap" (duvar boşluk), "manuel".
    public string? Kaynak { get; set; }
}

// ─── Kablo veri modelleri ─────────────────────────────────────────────────

public enum BpcKabloTipi { GucKablosu, GucKablosu2, SeyahatKablosu, KontrolKablosu, PanoKablosu, PrizKablosu, MekanikKablosu, ZayifAkimKablosu }
public enum BpcRotaTipi  { DaisyChain, DogruCizgi }
public enum BpcKabloTavaTipi { DelikliTava, MerdivenTava, SacKanal, PvcKanal }

/// Kuvvetli akım paftalarında ortak görünen, rotalı kablo taşıyıcı sistemi.
public class BpcKabloTavasi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "KT";
    public BpcKabloTavaTipi Tip { get; set; } = BpcKabloTavaTipi.DelikliTava;
    public int Adet { get; set; } = 1;
    public double GenislikMm { get; set; } = 100;
    public double YukseklikMm { get; set; } = 50;
    public double EtKalinligiMm { get; set; } = 1.0;
    public bool AgirHizmet { get; set; }
    public double KotM { get; set; } = 2.70;
    public bool EtiketGoster { get; set; } = true;
    public List<Point2D> Rota { get; set; } = new();
    public List<BpcKabloTavasiTaliHat> TaliHatlar { get; set; } = new();
}

public class BpcKabloTavasiTaliHat
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public double GenislikMm { get; set; } = 100;
    public List<Point2D> Rota { get; set; } = new();
}

public class BpcTopraklama
{
    public List<Point2D> SecimNoktalari { get; set; } = new();
    public List<BpcTopraklamaSerit> Seritler { get; set; } = new();
    public List<Point2D> Kaziklar { get; set; } = new();
    public bool EkTopraklamaVar { get; set; }
}

public class BpcTopraklamaSerit
{
    public Point2D A { get; set; } = new();
    public Point2D B { get; set; } = new();
    public BpcTopraklamaSeritTipi Tip { get; set; } = BpcTopraklamaSeritTipi.GalvanizSerit;
}

public enum BpcTopraklamaSeritTipi
{
    GalvanizSerit,
    OrguBakir
}

public class BpcPanoBeslemeHatti
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AnaPanoId { get; set; } = "";
    public string TaliPanoId { get; set; } = "";
    public int AnaKatNo { get; set; }
    public int TaliKatNo { get; set; }
    public BpcPanoBeslemeUygulamaTipi UygulamaTipi { get; set; } = BpcPanoBeslemeUygulamaTipi.Kablo;
    public List<Point2D> Rota { get; set; } = new();
    public string Etiket { get; set; } = "";
}

public enum BpcPanoBeslemeUygulamaTipi
{
    Kablo,
    Busbar
}

/// Bir anahtarın kontrol ettiği armatür grubu (devre).
public class BpcDevre
{
    public string  Id             { get; set; } = Guid.NewGuid().ToString();
    public string? AnahtarElemanId { get; set; }
    public string? BuatId          { get; set; }
    public int     DevreNo        { get; set; } = 1;  // Komütatör: 1 veya 2
    public List<string> ArmaturElemanIds { get; set; } = new();
}

/// İki Vavien anahtarını ve paylaştıkları devreyi tanımlar.
public class BpcVavienGrup
{
    public string Id         { get; set; } = Guid.NewGuid().ToString();
    public string Anahtar1Id { get; set; } = "";
    public string Anahtar2Id { get; set; } = "";
    public string DevreId    { get; set; } = "";
}

/// Çizilen kablo rotası.
public class BpcKablo
{
    public string       Id      { get; set; } = Guid.NewGuid().ToString();
    public string       DevreId { get; set; } = "";
    public BpcKabloTipi Tip     { get; set; } = BpcKabloTipi.GucKablosu;
    public int?         PanoSira { get; set; }
    /// Zayıf akım kablolarında kabloyu hangi sisteme (Data/TV/…) ait olduğunu belirtir.
    /// Renk ve linye prefix'i bu değer üzerinden katalogdan okunur.
    public BpcPaftaTipi? SistemAnahtar { get; set; }
    public List<Point2D> Rota  { get; set; } = new();
}

public class BpcBuatPanoBaglanti
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BuatId { get; set; } = "";
    public string PanoId { get; set; } = "";
    public List<string> GecisOdaIds { get; set; } = new();
    /// "A" = aydınlatma linyesi, "P" = priz linyesi
    public string LinyePrefix { get; set; } = "A";
}

/// Bir buattaki priz grubunu tanımlar (anahtar gerekmez).
public class BpcPrizDevre
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? BuatId { get; set; }
    public List<string> PrizElemanIds { get; set; } = new();
}

/// Bir klima ünitesinin panoya olan doğrudan bağlantısını tanımlar.
public class BpcMekanikDevre
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KlimaElemanId { get; set; } = "";
    public string PanoId { get; set; } = "";
}

/// Zayıf akım sistemleri için genel devre modeli (Data/TV/Telefon/CCTV/Anons/Yangın).
/// Topolojiye göre: cihazlar → (ara toplayıcı) → merkez.
public class BpcSistemDevre
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// Hangi sisteme ait (katalog anahtarı).
    public BpcPaftaTipi Sistem { get; set; }
    /// Sistemin sonlandığı merkez elemanı (rack, santral, NVR, amfi…).
    public string? MerkezId { get; set; }
    /// Opsiyonel ara toplayıcı (kat dağıtım kutusu, splitter…).
    public string? AraToplayiciId { get; set; }
    /// Bu devredeki cihazlar (priz, kamera, dedektör…).
    public List<string> CihazIds { get; set; } = new();
}

public class BasitProjeOda : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();

    private string _ad = "Oda";
    public string Ad
    {
        get => _ad;
        set => Set(ref _ad, value);
    }

    public double AlanM2 { get; set; }
    public bool GecisSecili { get; set; }
    public bool AsmaTavan { get; set; }
    
    private bool _mahalListesineDahil = true;
    public bool MahalListesineDahil
    {
        get => _mahalListesineDahil;
        set => Set(ref _mahalListesineDahil, value);
    }
    
    public List<Point2D> Poligon { get; set; } = new();

    /// Odanın alan-ağırlıklı ağırlık merkezi. Köşe ortalaması değil; bir kenarda
    /// yoğunlaşan köşelerin merkezi kaydırması (yerleşim/etiket konumu bozulması)
    /// engellenir. Konkav odada centroid poligon dışına düşebilir; içeride olması
    /// gereken çağrılar ayrıca IsPointInside ile kontrol eder.
    public Point2D Merkez => PoligonYardimci.Centroid(Poligon);

    public override string ToString() => $"{Ad} ({AlanM2:N1} m2)";
}

public enum BpcArmaturlestirmeMantigi
{
    OdaMerkezineBirAdet,
    AlanBazliAdet,
    GridDizilim,
    KoridorEkseni
}

public class BpcArmatürTanim
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public BpcKutuphaneGrubu KutuphaneGrubu { get; set; } = BpcKutuphaneGrubu.KuvvetliAkim;
    public BpcKutuphaneKategorisi KutuphaneKategorisi { get; set; } = BpcKutuphaneKategorisi.Aydinlatma;
    public BpcElemanTipi? ElemanTipi { get; set; }
    public string Ad { get; set; } = "";
    /// DXF çıktısında blok yanına yazılan kısa etiket. Boşsa Ad kullanılır.
    public string EkranAdi { get; set; } = "";
    public string BlokAdi { get; set; } = "";
    public string SembolAdi { get; set; } = "";
    public string MontajTipi { get; set; } = "Tavan";
    public double GucW { get; set; }
    public BpcArmaturlestirmeMantigi YerlesimMantigi { get; set; } = BpcArmaturlestirmeMantigi.GridDizilim;
    public double AlanBasinaAdetM2 { get; set; } = 12.0;
    public double GridAraligiM { get; set; } = 3.0;
    /// Grid Y aralığı (m). null = X ile aynı (kare grid). Eski kayıtlarla uyumlu.
    public double? GridAraligiYM { get; set; }
    public int VarsayilanAdet { get; set; } = 1;
    public int VarsayilanMonofazeAdet { get; set; }
    public int VarsayilanTrifazeAdet { get; set; }
    public string OzelAlanTuru { get; set; } = "";

    /// DXF blok sembolünü oluşturan çizgi parçaları. Normalize koordinatlar (±1 aralığı).
    /// DXF çıktıda BLOCK tanımı olarak kullanılır; kütüphane penceresinde önizleme çizer.
    public List<BpcSembolCizgi> SembolCizgiler { get; set; } = new();

    public override string ToString() => $"{Ad} ({GucW:N0} W)";
}

public enum BpcKutuphaneGrubu
{
    KuvvetliAkim,
    ZayifAkim
}

public enum BpcKutuphaneKategorisi
{
    Aydinlatma,
    Priz,
    Anahtar,
    Pano,
    Mekanik,
    Data,
    Tv,
    Telefon,
    Cctv,
    AcilAnons,
    Diafon,
    YanginAlgilama
}

public enum BpcElemanTipi
{
    Armatür,
    SarkitArmatur,
    GlopArmatur,
    CiplakLamba,
    BantArmatur,
    Avize,
    WallWasher,
    Anahtar,
    Komütatör,
    UcluKomutator,
    DarbeAnahtari,
    Vavien,
    KomütatörVavien,
    Deviatör,
    Dimmer,
    IkiKutupluAnahtar,
    KatPanosu,
    Sensör,
    Buat,
    // ─── Priz tipleri ────────────────────────────
    Priz,
    KapakliPriz,
    SivaUstuPriz,
    TrifazePriz,
    KombinePrizKutusu,
    TavanPrizi,
    UsbliMasaPrizi,
    UpsPrizi,
    KapakliUpsPrizi,
    SivaUstuUpsPrizi,
    IkiliPriz,
    UcluPriz,
    // ─── Mekanik tipleri ─────────────────────────
    KlimaIcUnite,
    KlimaDisUnite,
    // ─── Zayıf akım: Data ────────────────────────
    DataPriz,
    DataRack,
    KatDagitimKutusu,
    // ─── Zayıf akım: TV ──────────────────────────
    TvPriz,
    TvMerkezi,
    // ─── Zayıf akım: Telefon ─────────────────────
    TelefonPriz,
    TelefonDagitim,
    // ─── Zayıf akım: CCTV ────────────────────────
    Kamera,
    CctvKayit,
    AgAnahtari,      // PoE switch — katta NVR yoksa kameralar burada toplanır
    // ─── Zayıf akım: Acil Anons ──────────────────
    Hoparlor,
    AnonsAmfi,
    // ─── Zayıf akım: Diafon / Kapı Zili ──────────
    Diafon,
    KapiZili,
    DiafonSantrali,
    // ─── Zayıf akım: Yangın Algılama ─────────────
    DumanDedektor,
    IsiDedektor,
    KombineDedektor, // optik + ısı kombine dedektör
    AsmaTavanDedektorGrubu, // asma tavan içi ded. + normal ded. + ikaz lambası (tek blok)
    YanginButonu,
    Siren,
    Flasor,
    YanginSantrali
}

/// Proje paftaları = sistemler. Kuvvetli ve zayıf akım disiplinlerine ayrılır.
public enum BpcPaftaTipi
{
    // Kuvvetli akım
    Aydinlatma,
    Priz,
    Mekanik,
    KabloTavasi,
    Topraklama,
    BusbarKablo,
    // Zayıf akım
    Data,
    Tv,
    Telefon,
    Cctv,
    Anons,
    Diafon,
    Yangin
}

/// Sistemlerin gruplandığı üst disiplin.
public enum BpcDisiplin { KuvvetliAkim, ZayifAkim }

/// Sistemin kablolama topolojisi.
public enum BpcSistemTopoloji
{
    AnahtarliDevre, // aydınlatma: anahtar armatürleri kontrol eder
    BuatDaisy,      // priz/data/tv: cihazlar buat/ara toplayıcıda toplanıp merkeze gider
    YildizMerkez,   // mekanik/cctv/anons: her cihaz doğrudan merkeze
    Loop,           // yangın: kapalı çevrim
    TasiyiciRota    // kablo tavası: kullanıcı çizimli taşıyıcı rota
}

/// Bir proje sisteminin (paftasının) tüm metaverisi. Kataloğdan okunur;
/// renk, prefix, merkez tipi, cihaz tipleri tek kaynaktan gelir.
public sealed class BpcSistemTanim
{
    public required BpcPaftaTipi Sistem { get; init; }
    public required string Ad { get; init; }
    public required string Ikon { get; init; }
    public required BpcDisiplin Disiplin { get; init; }
    public required string LinyePrefix { get; init; }
    public required string KabloRengi { get; init; }
    /// Üst bar aktif buton/çerçeve rengi.
    public required string VurguRengi { get; init; }
    public BpcSistemTopoloji Topoloji { get; init; }
    public BpcElemanTipi MerkezTipi { get; init; }
    /// Ana merkez dışında hattın sonlanabileceği ek merkez tipleri (ör. CCTV'de ağ switch'i).
    public IReadOnlyList<BpcElemanTipi> EkMerkezTipleri { get; init; } = new List<BpcElemanTipi>();
    public IReadOnlyList<BpcElemanTipi> CihazTipleri { get; init; } = new List<BpcElemanTipi>();
    public IReadOnlyList<BpcElemanTipi> AraToplayiciTipleri { get; init; } = new List<BpcElemanTipi>();

    /// Verilen tip bu sistemin merkezi mi (ana veya ek)?
    public bool MerkezMi(BpcElemanTipi tip) => tip == MerkezTipi || EkMerkezTipleri.Contains(tip);

    public string Anahtar => Sistem.ToString();
    public override string ToString() => $"{Ikon} {Ad}";
}

/// Tüm sistem tanımlarının tek kaynağı.
public static class BpcSistemKatalogu
{
    public static IReadOnlyList<BpcSistemTanim> Tumu { get; } = new List<BpcSistemTanim>
    {
        new() {
            Sistem = BpcPaftaTipi.Aydinlatma, Ad = "Aydınlatma", Ikon = "💡",
            Disiplin = BpcDisiplin.KuvvetliAkim, LinyePrefix = "A",
            KabloRengi = "#E0C84E", VurguRengi = "#4EE09A",
            Topoloji = BpcSistemTopoloji.AnahtarliDevre, MerkezTipi = BpcElemanTipi.KatPanosu,
            CihazTipleri = new[] { BpcElemanTipi.Armatür, BpcElemanTipi.Anahtar,
                BpcElemanTipi.Komütatör, BpcElemanTipi.UcluKomutator, BpcElemanTipi.DarbeAnahtari, BpcElemanTipi.Vavien,
                BpcElemanTipi.Sensör, BpcElemanTipi.Buat }
        },
        new() {
            Sistem = BpcPaftaTipi.Priz, Ad = "Priz", Ikon = "🔌",
            Disiplin = BpcDisiplin.KuvvetliAkim, LinyePrefix = "P",
            KabloRengi = "#E07840", VurguRengi = "#E0A840",
            Topoloji = BpcSistemTopoloji.BuatDaisy, MerkezTipi = BpcElemanTipi.KatPanosu,
            CihazTipleri = new[] { BpcElemanTipi.Priz, BpcElemanTipi.KapakliPriz,
                BpcElemanTipi.SivaUstuPriz, BpcElemanTipi.TrifazePriz,
                BpcElemanTipi.KombinePrizKutusu, BpcElemanTipi.TavanPrizi,
                BpcElemanTipi.UsbliMasaPrizi, BpcElemanTipi.Buat },
            AraToplayiciTipleri = new[] { BpcElemanTipi.Buat }
        },
        new() {
            Sistem = BpcPaftaTipi.Mekanik, Ad = "Mekanik", Ikon = "⚙️",
            Disiplin = BpcDisiplin.KuvvetliAkim, LinyePrefix = "M",
            KabloRengi = "#80D8FF", VurguRengi = "#40B7E0",
            Topoloji = BpcSistemTopoloji.YildizMerkez, MerkezTipi = BpcElemanTipi.KatPanosu,
            CihazTipleri = new[] { BpcElemanTipi.KlimaIcUnite, BpcElemanTipi.KlimaDisUnite }
        },
        new() {
            Sistem = BpcPaftaTipi.KabloTavasi, Ad = "Kablo Tavası", Ikon = "▤",
            Disiplin = BpcDisiplin.KuvvetliAkim, LinyePrefix = "KT",
            KabloRengi = "#B8C0B0", VurguRengi = "#B8C0B0",
            Topoloji = BpcSistemTopoloji.TasiyiciRota, MerkezTipi = BpcElemanTipi.KatPanosu
        },
        new() {
            Sistem = BpcPaftaTipi.Topraklama, Ad = "Topraklama", Ikon = "⏚",
            Disiplin = BpcDisiplin.KuvvetliAkim, LinyePrefix = "T",
            KabloRengi = "#7EE070", VurguRengi = "#7EE070",
            Topoloji = BpcSistemTopoloji.TasiyiciRota, MerkezTipi = BpcElemanTipi.KatPanosu
        },
        new() {
            Sistem = BpcPaftaTipi.BusbarKablo, Ad = "Busbar / Kablo", Ikon = "▰",
            Disiplin = BpcDisiplin.KuvvetliAkim, LinyePrefix = "BK",
            KabloRengi = "#FFB860", VurguRengi = "#FFB860",
            Topoloji = BpcSistemTopoloji.TasiyiciRota, MerkezTipi = BpcElemanTipi.KatPanosu
        },
        new() {
            Sistem = BpcPaftaTipi.Data, Ad = "Data", Ikon = "🖧",
            Disiplin = BpcDisiplin.ZayifAkim, LinyePrefix = "D",
            KabloRengi = "#4ED0E0", VurguRengi = "#4ED0E0",
            Topoloji = BpcSistemTopoloji.BuatDaisy, MerkezTipi = BpcElemanTipi.DataRack,
            CihazTipleri = new[] { BpcElemanTipi.DataPriz },
            AraToplayiciTipleri = new[] { BpcElemanTipi.KatDagitimKutusu }
        },
        new() {
            Sistem = BpcPaftaTipi.Tv, Ad = "TV", Ikon = "📺",
            Disiplin = BpcDisiplin.ZayifAkim, LinyePrefix = "TV",
            KabloRengi = "#C080E0", VurguRengi = "#C080E0",
            Topoloji = BpcSistemTopoloji.BuatDaisy, MerkezTipi = BpcElemanTipi.TvMerkezi,
            CihazTipleri = new[] { BpcElemanTipi.TvPriz },
            AraToplayiciTipleri = new[] { BpcElemanTipi.KatDagitimKutusu }
        },
        new() {
            Sistem = BpcPaftaTipi.Telefon, Ad = "Telefon", Ikon = "☎️",
            Disiplin = BpcDisiplin.ZayifAkim, LinyePrefix = "T",
            KabloRengi = "#E0B040", VurguRengi = "#E0B040",
            Topoloji = BpcSistemTopoloji.BuatDaisy, MerkezTipi = BpcElemanTipi.TelefonDagitim,
            CihazTipleri = new[] { BpcElemanTipi.TelefonPriz },
            AraToplayiciTipleri = new[] { BpcElemanTipi.KatDagitimKutusu }
        },
        new() {
            Sistem = BpcPaftaTipi.Cctv, Ad = "CCTV", Ikon = "📹",
            Disiplin = BpcDisiplin.ZayifAkim, LinyePrefix = "C",
            KabloRengi = "#E04E8C", VurguRengi = "#E04E8C",
            Topoloji = BpcSistemTopoloji.YildizMerkez, MerkezTipi = BpcElemanTipi.CctvKayit,
            EkMerkezTipleri = new[] { BpcElemanTipi.AgAnahtari },
            CihazTipleri = new[] { BpcElemanTipi.Kamera }
        },
        new() {
            Sistem = BpcPaftaTipi.Anons, Ad = "Acil Anons", Ikon = "📢",
            Disiplin = BpcDisiplin.ZayifAkim, LinyePrefix = "S",
            KabloRengi = "#60D080", VurguRengi = "#60D080",
            Topoloji = BpcSistemTopoloji.YildizMerkez, MerkezTipi = BpcElemanTipi.AnonsAmfi,
            CihazTipleri = new[] { BpcElemanTipi.Hoparlor }
        },
        new() {
            Sistem = BpcPaftaTipi.Diafon, Ad = "Diafon", Ikon = "☎",
            Disiplin = BpcDisiplin.ZayifAkim, LinyePrefix = "DF",
            KabloRengi = "#58C8A8", VurguRengi = "#58C8A8",
            Topoloji = BpcSistemTopoloji.YildizMerkez, MerkezTipi = BpcElemanTipi.DiafonSantrali,
            CihazTipleri = new[] { BpcElemanTipi.Diafon, BpcElemanTipi.KapiZili }
        },
        new() {
            Sistem = BpcPaftaTipi.Yangin, Ad = "Yangın Algılama", Ikon = "🔥",
            Disiplin = BpcDisiplin.ZayifAkim, LinyePrefix = "Y",
            KabloRengi = "#E05050", VurguRengi = "#E05050",
            Topoloji = BpcSistemTopoloji.Loop, MerkezTipi = BpcElemanTipi.YanginSantrali,
            CihazTipleri = new[] { BpcElemanTipi.DumanDedektor, BpcElemanTipi.IsiDedektor,
                BpcElemanTipi.KombineDedektor, BpcElemanTipi.AsmaTavanDedektorGrubu,
                BpcElemanTipi.YanginButonu, BpcElemanTipi.Siren, BpcElemanTipi.Flasor }
        },
    };

    public static BpcSistemTanim Get(BpcPaftaTipi sistem)
        => Tumu.First(s => s.Sistem == sistem);

    public static IEnumerable<BpcSistemTanim> Disipline(BpcDisiplin disiplin)
        => Tumu.Where(s => s.Disiplin == disiplin);

    /// Verilen eleman tipi herhangi bir sistemin merkezi mi (pano, rack, santral…)?
    public static bool MerkezTipiMi(BpcElemanTipi tip)
        => Tumu.Any(s => s.MerkezTipi == tip);

    // ─── Disiplin yardımcıları (M12/M16 ayrımının tek kaynağı) ───────────

    /// Zayıf akım sistemlerinin tüm eleman tipleri (cihaz + merkez + ek merkez +
    /// ara toplayıcı). Katalogdan türetilir; listede olmayan her tip kuvvetli akımdır.
    private static readonly HashSet<BpcElemanTipi> ZayifAkimElemanTipleri =
        Tumu.Where(s => s.Disiplin == BpcDisiplin.ZayifAkim)
            .SelectMany(s => s.CihazTipleri
                .Concat(s.AraToplayiciTipleri)
                .Concat(s.EkMerkezTipleri)
                .Append(s.MerkezTipi))
            .ToHashSet();

    public static BpcDisiplin Disiplini(BpcPaftaTipi sistem) => Get(sistem).Disiplin;

    public static BpcDisiplin Disiplini(BpcElemanTipi tip)
        => ZayifAkimElemanTipleri.Contains(tip) ? BpcDisiplin.ZayifAkim : BpcDisiplin.KuvvetliAkim;

    public static BpcDisiplin Disiplini(BpcKablo kablo)
        => kablo.Tip == BpcKabloTipi.ZayifAkimKablosu ? BpcDisiplin.ZayifAkim : BpcDisiplin.KuvvetliAkim;
}

/// JSON kütüphane dosyasının kök nesnesi. Geriye uyumluluk için Armatürler boşsa
/// eski List<BpcArmatürTanim> formatından yükleme yapılır.
public class BpcKutuphaneData
{
    public int SembolSetVersion { get; set; }
    /// Uygulamayla gelen (gömülü) DXF sembol şablonunun uygulanmış sürümü.
    /// Bu değer koddaki güncel sürümden küçükse, açılışta gömülü şablon yeniden uygulanır.
    public int GomuluSablonSurum { get; set; }
    public List<BpcArmatürTanim> Malzemeler { get; set; } = new();
    public List<BpcArmatürTanim> Armatürler { get; set; } = new();
    public List<BpcArmatürTanim> Anahtarlar { get; set; } = new();
    public List<BpcArmatürTanim> Panolar    { get; set; } = new();
    public List<BpcArmatürTanim> Prizler    { get; set; } = new();
    public List<BpcArmatürTanim> Klimalar   { get; set; } = new();

    /// Zayıf akım sembolleri için kullanıcı override haritası — BlokAdi → sembol çizgileri.
    /// Boş bırakılırsa varsayılan (kod-içi) zayıf akım sembolleri kullanılır.
    public Dictionary<string, List<BpcSembolCizgi>> ZayifAkimSemboller { get; set; } = new();
}

public class BasitProjeEleman
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public BpcElemanTipi Tip { get; set; }
    public string Ad { get; set; } = "";
    public string EkranAdi { get; set; } = "";
    public string? OdaId { get; set; }
    public string? ArmatürTanimId { get; set; }
    public string BlokAdi { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public BpcPaftaTipi PaftaTipi { get; set; } = BpcPaftaTipi.Aydinlatma;
    public string Ozellikler { get; set; } = "";
    public double? CustomGucW { get; set; }
    public string MontajTipi { get; set; } = "";
    public string YerlesimTipi { get; set; } = "";
    public double? GridAraligiM { get; set; }
    /// Grid Y aralığı (m). null = X ile aynı (kare grid).
    public double? GridAraligiYM { get; set; }
    public double? AlanBasinaAdetM2 { get; set; }
    public double? DuvarAraligiM { get; set; }
    public int? CustomAdet { get; set; }
    public int? MonofazeAdet { get; set; }
    public int? TrifazeAdet { get; set; }
    public double? Megapiksel { get; set; }
    public string OzelAlanTuru { get; set; } = "";
    /// Eleman otomatik üretilmişse hangi akıştan geldiğini belirtir.
    /// Boş/null değer manuel veya eski proje kaydı kabul edilir.
    public string OtomatikYerlesimKaynak { get; set; } = "";

    /// Mahal listesinden üretildiyse bağlı olduğu satır kimlikleri. Buat gibi
    /// ortak elemanlar birden fazla satıra hizmet edebilir.
    public List<string> OtomatikYerlesimGrupIds { get; set; } = new();

    /// Sensör kontrollü grup işareti. Mahal listesinde "Sensör" işaretli hücreden
    /// üretilen armatürlerde ve onlara eşlik eden sensör elemanlarında true olur.
    /// Bu armatürler duvar anahtarı almaz; her biri kendi sensörü üzerinden doğrudan
    /// oda buatına bağlanır. Kablolama bu armatür/sensörleri özel işler.
    public bool SensorKontrollu { get; set; }

    /// Yalnız Kat Panosu için: bu panonun beslediği oda Id'leri. Buat→pano
    /// otomatik yönlendirmesinde, odası bu listede olan buatlar bu panoya gider.
    public List<string> BagliOdaIds { get; set; } = new();
    /// Kat Panosu oda atamasında diğer katların odalarının da seçilebilmesi
    /// (varsayılan kapalı). Yalnız UI seçeneklerini etkiler.
    public bool KatArasiSecim { get; set; }

    /// Yalnız Tali Pano için: kullanıcının açıkça seçtiği üst kat panosu Id'si.
    /// null = otomatik çözümleme (odanın BagliOdaIds atamasına bakılır).
    public string? TaliPanoHedefPanoId { get; set; }

    public override string ToString() => $"{Tip} {Ad} ({X:N2}, {Y:N2})";
}
