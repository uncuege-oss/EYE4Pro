using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Electric.KesifHazirla;

public abstract class KesifObservable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class KesifPoz : KesifObservable
{
    private string _pozNo = "";
    private string _birimAdi = "";
    private string _birim = "adet";
    private decimal _birimFiyat;
    private string _aciklama = "";
    private string _kategori = "";
    private bool _aktifMi = true;
    private Guid? _birimFiyatListesiId;
    private string _birimFiyatListesiAdi = "";
    private bool _listePozuMu;
    private DateTime _sonGuncellemeTarihi = DateTime.Now;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string PozNo { get => _pozNo; set => SetProperty(ref _pozNo, value); }
    public string BirimAdi { get => _birimAdi; set => SetProperty(ref _birimAdi, value); }
    public string Birim { get => _birim; set => SetProperty(ref _birim, value); }
    public decimal BirimFiyat { get => _birimFiyat; set => SetProperty(ref _birimFiyat, value); }
    public string Aciklama { get => _aciklama; set => SetProperty(ref _aciklama, value); }
    public string Kategori { get => _kategori; set => SetProperty(ref _kategori, value); }
    public bool AktifMi { get => _aktifMi; set => SetProperty(ref _aktifMi, value); }
    public Guid? BirimFiyatListesiId { get => _birimFiyatListesiId; set => SetProperty(ref _birimFiyatListesiId, value); }
    public string BirimFiyatListesiAdi { get => _birimFiyatListesiAdi; set => SetProperty(ref _birimFiyatListesiAdi, value); }
    public bool ListePozuMu { get => _listePozuMu; set => SetProperty(ref _listePozuMu, value); }
    public DateTime SonGuncellemeTarihi { get => _sonGuncellemeTarihi; set => SetProperty(ref _sonGuncellemeTarihi, value); }
}

public sealed class KesifBirimFiyatListesi : KesifObservable
{
    private string _ad = "Birim Fiyat Listesi";
    private string _kaynakDosya = "";
    private bool _aktifMi = true;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ad { get => _ad; set => SetProperty(ref _ad, value); }
    public string KaynakDosya { get => _kaynakDosya; set => SetProperty(ref _kaynakDosya, value); }
    public DateTime YuklemeTarihi { get; set; } = DateTime.Now;
    public bool AktifMi { get => _aktifMi; set => SetProperty(ref _aktifMi, value); }
    public ObservableCollection<KesifPoz> Pozlar { get; set; } = new();
    [JsonIgnore] public int PozSayisi => Pozlar.Count;
}

public enum KesifKaynakTipi
{
    Manuel,
    Otomatik,
    Karma
}

/// <summary>Birleştirilmiş otomatik keşif satırında referans bazlı miktar katkısı.</summary>
public sealed class KesifSatirOtomatikKatki
{
    public Guid KaynakReferansId { get; set; }
    public string KaynakModul { get; set; } = "";
    public decimal Miktar { get; set; }
}

public sealed class KesifSatir : KesifObservable
{
    private string _pozNo = "";
    private string _birimAdi = "";
    private string _birim = "adet";
    private decimal _birimFiyat;
    private decimal _miktar;
    private string _aciklama = "";
    private KesifKaynakTipi _kaynakTipi = KesifKaynakTipi.Manuel;
    private string _kaynakModul = "";

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PozId { get; set; }
    public string PozNo { get => _pozNo; set => SetProperty(ref _pozNo, value); }
    public string BirimAdi { get => _birimAdi; set => SetProperty(ref _birimAdi, value); }
    public string Birim { get => _birim; set => SetProperty(ref _birim, value); }
    public decimal BirimFiyat
    {
        get => _birimFiyat;
        set
        {
            if (SetProperty(ref _birimFiyat, value))
            {
                OnPropertyChanged(nameof(SatirToplam));
                OnPropertyChanged(nameof(BirimFiyatElleDegisti));
            }
        }
    }

    /// <summary>Satırın kaynağından (poz/otomatik) geldiği andaki birim fiyat.
    /// Güncel <see cref="BirimFiyat"/> bundan farklıysa fiyat elle değiştirilmiş demektir.
    /// Eski projelerde null; yüklemede güncel fiyatla baz alınır (değişmemiş sayılır).</summary>
    public decimal? OrijinalBirimFiyat { get; set; }

    /// <summary>Birim fiyat kaynağındaki değerden elle değiştirilmiş mi (renkli göstermek için).</summary>
    [JsonIgnore] public bool BirimFiyatElleDegisti
        => OrijinalBirimFiyat.HasValue && Math.Abs(BirimFiyat - OrijinalBirimFiyat.Value) > 0.0000001m;

    public decimal Miktar
    {
        get => _miktar;
        set
        {
            if (SetProperty(ref _miktar, value))
                OnPropertyChanged(nameof(SatirToplam));
        }
    }

    [JsonIgnore] public decimal SatirToplam => Miktar * BirimFiyat;
    public string Aciklama { get => _aciklama; set => SetProperty(ref _aciklama, value); }
    public KesifKaynakTipi KaynakTipi { get => _kaynakTipi; set => SetProperty(ref _kaynakTipi, value); }
    public string KaynakModul { get => _kaynakModul; set => SetProperty(ref _kaynakModul, value); }

    /// <summary>Otomatik satırın üretildiği referans metraj dosyası (<see cref="KesifReferansDosyasi"/>).
    /// Referans silinince/yeniden işlenince bu Id ile bu satırlar temizlenir. Manuel satırlarda null.</summary>
    public Guid? KaynakReferansId { get; set; }

    /// <summary>Aynı poz birden çok referanstan geldiğinde tek satır içinde kaynak miktarlarını korur.</summary>
    public ObservableCollection<KesifSatirOtomatikKatki> OtomatikKatkilar { get; set; } = new();

    private string? _referansBilgisi;
    /// <summary>Fare balonu (tooltip) için, satırın hangi referanstan ne miktar geldiğini özetleyen geçici metin.
    /// ViewModel tarafından doldurulur; serialize edilmez. Manuel satırlarda null (balon gösterilmez).</summary>
    [JsonIgnore] public string? ReferansBilgisi { get => _referansBilgisi; set => SetProperty(ref _referansBilgisi, value); }
}

public sealed class KesifTablosu : KesifObservable
{
    private string _ad = "Keşif Tablosu";
    private string _not = "";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ad { get => _ad; set => SetProperty(ref _ad, value); }
    public ObservableCollection<KesifSatir> Satirlar { get; set; } = new();

    /// <summary>Alt keşif tabloları (en fazla 3 seviye: tablo → alt tablo → alt tablonun alt tablosu).
    /// Alt tablosu olan bir tablo "başlık"tır; doğrudan satır tutmaz (satırlar yaprak tablolarda).</summary>
    public ObservableCollection<KesifTablosu> AltTablolar { get; set; } = new();

    [JsonIgnore] public bool AltTabloVarMi => AltTablolar.Count > 0;
    [JsonIgnore] public bool YaprakMi => AltTablolar.Count == 0;

    /// <summary>Tablo toplamı = kendi satırları + tüm alt tablo toplamları (özyinelemeli).</summary>
    [JsonIgnore] public decimal Toplam => Satirlar.Sum(s => s.SatirToplam) + AltTablolar.Sum(t => t.Toplam);
    public string Not { get => _not; set => SetProperty(ref _not, value); }

    /// <summary>Bu tablonun ve tüm alt tablolarının toplamını bildirir (özyinelemeli).</summary>
    public void ToplamiYenile()
    {
        foreach (var alt in AltTablolar)
            alt.ToplamiYenile();
        OnPropertyChanged(nameof(Toplam));
        OnPropertyChanged(nameof(AltTabloVarMi));
        OnPropertyChanged(nameof(YaprakMi));
    }
}

/// <summary>Keşif raporu kapağı ve genel icmal başlığında gösterilen proje künyesi.</summary>
public sealed class KesifProjeBilgileri : KesifObservable
{
    private string _isveren = "";
    private string _yuklenici = "";
    private string _ilIlce = "";
    private string _adaParsel = "";
    private string _sozlesmeNo = "";
    private string _tarih = "";
    private string _aciklama = "";

    public string Isveren { get => _isveren; set => SetProperty(ref _isveren, value); }
    public string Yuklenici { get => _yuklenici; set => SetProperty(ref _yuklenici, value); }
    public string IlIlce { get => _ilIlce; set => SetProperty(ref _ilIlce, value); }
    public string AdaParsel { get => _adaParsel; set => SetProperty(ref _adaParsel, value); }
    public string SozlesmeNo { get => _sozlesmeNo; set => SetProperty(ref _sozlesmeNo, value); }
    public string Tarih { get => _tarih; set => SetProperty(ref _tarih, value); }
    public string Aciklama { get => _aciklama; set => SetProperty(ref _aciklama, value); }
}

/// <summary>Keşif tablosu ağacı üzerinde gezinme yardımcıları (özyinelemeli düz liste, ebeveyn, derinlik).</summary>
public static class KesifTabloAgaci
{
    public const int MaksimumDerinlik = 3;

    /// <summary>Projedeki tüm tabloları (alt tablolar dahil) ön-sıralı düz liste olarak verir.</summary>
    public static IEnumerable<KesifTablosu> TumTablolar(KesifProje proje)
        => proje.Tablolar.SelectMany(TumTablolar);

    /// <summary>Bir tabloyu ve tüm alt tablolarını ön-sıralı verir.</summary>
    public static IEnumerable<KesifTablosu> TumTablolar(KesifTablosu tablo)
    {
        yield return tablo;
        foreach (var alt in tablo.AltTablolar)
            foreach (var torun in TumTablolar(alt))
                yield return torun;
    }

    /// <summary>Verilen tablonun ebeveynini bulur; en üst seviyedeyse null.</summary>
    public static KesifTablosu? Ebeveyn(KesifProje proje, KesifTablosu hedef)
        => TumTablolar(proje).FirstOrDefault(t => t.AltTablolar.Contains(hedef));

    /// <summary>1 tabanlı seviye (en üst = 1).</summary>
    public static int Derinlik(KesifProje proje, KesifTablosu hedef)
    {
        var derinlik = 1;
        var ebeveyn = Ebeveyn(proje, hedef);
        while (ebeveyn is not null)
        {
            derinlik++;
            ebeveyn = Ebeveyn(proje, ebeveyn);
        }
        return derinlik;
    }
}

public sealed class KesifProje : KesifObservable
{
    private string _ad = "Yeni Keşif";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ad { get => _ad; set => SetProperty(ref _ad, value); }

    /// <summary>Çalışma anında gösterilen birleşik kütüphane (manuel pozlar + aktif liste pozları).
    /// Liste pozları zaten <see cref="BirimFiyatListeleri"/> içinde tutulduğundan burada serialize EDİLMEZ;
    /// aksi halde aynı pozlar dosyaya iki kez yazılırdı. Yüklemede <c>PozKutuphaneYenile</c> ile yeniden kurulur.</summary>
    [JsonIgnore] public ObservableCollection<KesifPoz> PozKutuphane { get; set; } = new();

    /// <summary>Hiçbir birim fiyat listesine ait olmayan manuel pozlar — kalıcı kaynak.</summary>
    public ObservableCollection<KesifPoz> ManuelPozlar { get; set; } = new();

    /// <summary>Proje künyesi (işveren, yüklenici, yer, tarih...). Sağ paneldeki "Proje Bilgileri".</summary>
    public KesifProjeBilgileri Bilgiler { get; set; } = new();

    public ObservableCollection<KesifBirimFiyatListesi> BirimFiyatListeleri { get; set; } = new();

    /// <summary>En üst seviye keşif tabloları. Alt tablolar her tablonun <c>AltTablolar</c>'ında tutulur.</summary>
    public ObservableCollection<KesifTablosu> Tablolar { get; set; } = new();

    /// <summary>Bu keşfe metraj alınan referans excel dosyaları (M13 Metraj Özeti vb.).
    /// Sadece ayrıştırılmış metraj + dosya adı saklanır; orijinal excel gömülmez.</summary>
    public ObservableCollection<KesifReferansDosyasi> ReferansDosyalari { get; set; } = new();

    public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    public DateTime GuncellemeTarihi { get; set; } = DateTime.Now;
    [JsonIgnore] public decimal GenelToplam => Tablolar.Sum(t => t.Toplam);

    public void ToplamlariYenile() => OnPropertyChanged(nameof(GenelToplam));
}

/// <summary>Referans excel'inden ayrıştırılmış tek bir metraj satırı (örn. bir kablo tipi/kesiti
/// ya da bir cihaz). Eşleşme/öğrenme bunun <see cref="EslemeAnahtari"/> kimliği üzerinden yürür.</summary>
public sealed class KesifMetrajSatiri : KesifObservable
{
    private string _anaGrup = "";
    private string _altGrup = "";
    private string _teknikOzellik = "";
    private string _birim = "";
    private decimal _miktar;
    private string _aciklama = "";

    public string AnaGrup { get => _anaGrup; set { if (SetProperty(ref _anaGrup, value)) OnPropertyChanged(nameof(EslemeAnahtari)); } }
    public string AltGrup { get => _altGrup; set { if (SetProperty(ref _altGrup, value)) OnPropertyChanged(nameof(EslemeAnahtari)); } }
    public string TeknikOzellik { get => _teknikOzellik; set { if (SetProperty(ref _teknikOzellik, value)) OnPropertyChanged(nameof(EslemeAnahtari)); } }
    public string Birim { get => _birim; set { if (SetProperty(ref _birim, value)) OnPropertyChanged(nameof(EslemeAnahtari)); } }
    public decimal Miktar { get => _miktar; set => SetProperty(ref _miktar, value); }
    public string Aciklama { get => _aciklama; set => SetProperty(ref _aciklama, value); }

    /// <summary>Ana Grup + Alt Grup + Teknik Özellik + Birim (tam kimlik) — normalize edilmiş eşleşme anahtarı.</summary>
    [JsonIgnore] public string EslemeAnahtari => KesifEslemeAnahtar.Uret(AnaGrup, AltGrup, TeknikOzellik, Birim);
}

/// <summary>Bir keşfe metraj alınan referans excel dosyası. Ayrıştırılmış metraj satırlarını taşır.</summary>
public sealed class KesifReferansDosyasi : KesifObservable
{
    private string _dosyaAdi = "";
    private string _kaynakModul = "";
    private string _sayfaAdi = "";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string DosyaAdi { get => _dosyaAdi; set => SetProperty(ref _dosyaAdi, value); }
    public string KaynakModul { get => _kaynakModul; set => SetProperty(ref _kaynakModul, value); }
    public string SayfaAdi { get => _sayfaAdi; set => SetProperty(ref _sayfaAdi, value); }
    public DateTime YuklemeTarihi { get; set; } = DateTime.Now;
    public ObservableCollection<KesifMetrajSatiri> MetrajSatirlari { get; set; } = new();
    [JsonIgnore] public int SatirSayisi => MetrajSatirlari.Count;
}

/// <summary>Öğrenilen poz eşleşmesi: bir metraj kimliği (Ana/Alt/Teknik/Birim) → bir poz.
/// Global veritabanında (AppData) saklanır; projeden bağımsızdır.</summary>
public sealed class KesifPozEsleme : KesifObservable
{
    private string _anaGrup = "";
    private string _altGrup = "";
    private string _teknikOzellik = "";
    private string _birim = "";
    private string _pozNo = "";
    private string _birimAdi = "";
    private bool _kontrolEdildi;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string AnaGrup { get => _anaGrup; set { if (SetProperty(ref _anaGrup, value)) OnPropertyChanged(nameof(EslemeAnahtari)); } }
    public string AltGrup { get => _altGrup; set { if (SetProperty(ref _altGrup, value)) OnPropertyChanged(nameof(EslemeAnahtari)); } }
    public string TeknikOzellik { get => _teknikOzellik; set { if (SetProperty(ref _teknikOzellik, value)) OnPropertyChanged(nameof(EslemeAnahtari)); } }
    public string Birim { get => _birim; set { if (SetProperty(ref _birim, value)) OnPropertyChanged(nameof(EslemeAnahtari)); } }
    public string PozNo { get => _pozNo; set => SetProperty(ref _pozNo, value); }
    public string BirimAdi { get => _birimAdi; set => SetProperty(ref _birimAdi, value); }

    /// <summary>Kullanıcı bu atamayı gözden geçirip onayladı mı. Gözden kaçan atamaları ayırt etmek için.</summary>
    public bool KontrolEdildi { get => _kontrolEdildi; set => SetProperty(ref _kontrolEdildi, value); }

    public DateTime SonGuncellemeTarihi { get; set; } = DateTime.Now;

    [JsonIgnore] public string EslemeAnahtari => KesifEslemeAnahtar.Uret(AnaGrup, AltGrup, TeknikOzellik, Birim);
}

/// <summary>Metraj kalemi ile poz eşleşmesi için tam kimlik (Ana+Alt+Teknik+Birim) anahtarını üretir.
/// Aynı kuralı hem <see cref="KesifMetrajSatiri"/> hem <see cref="KesifPozEsleme"/> kullanır.</summary>
public static class KesifEslemeAnahtar
{
    public static string Uret(string? anaGrup, string? altGrup, string? teknikOzellik, string? birim)
        => string.Join('|', Sadelestir(anaGrup), Sadelestir(altGrup), Sadelestir(teknikOzellik), Sadelestir(birim));

    private static string Sadelestir(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var lower = value.Trim().ToLowerInvariant();
        return string.Join(' ', lower.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed class KesifIcmalKirilim
{
    public string TabloAdi { get; init; } = "";
    public decimal Miktar { get; init; }
    public decimal Tutar { get; init; }
}

public sealed class KesifIcmalSatiri
{
    public string PozNo { get; init; } = "";
    public string BirimAdi { get; init; } = "";
    public string Birim { get; init; } = "";
    public decimal ToplamMiktar { get; init; }
    public decimal OrtalamaBirimFiyat { get; init; }
    public decimal ToplamTutar { get; init; }
    public bool BirimFiyatFarkiVar { get; init; }
    public ObservableCollection<KesifIcmalKirilim> Kirilimlar { get; init; } = new();
    public string BirimFiyatFarkiMetni => BirimFiyatFarkiVar ? "Fiyat farkı var" : "";
    public string TabloOzeti => string.Join(", ", Kirilimlar.Select(k => $"{k.TabloAdi}: {k.Miktar:0.##}"));
}
