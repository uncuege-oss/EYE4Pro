using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.ViewModels;

/// M51 "Salon Tasarla" pencere ViewModel'i. Model + katman listesi + aktif katman + durum.
public class SalonTasarlaViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public SalonTasarlaViewModel()
    {
        _seciliKatman = _salon.Katmanlar.FirstOrDefault();
    }

    private SalonGeometri _salon = SalonGeometriServisi.YeniOlustur();
    public SalonGeometri Salon
    {
        get => _salon;
        set
        {
            if (_salon == value) return;
            _salon = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Katmanlar));
            OnPropertyChanged(nameof(CizimOgeleri));
            OnPropertyChanged(nameof(SeyirciSekciyonlari));
            OnPropertyChanged(nameof(Balkonlar));
            OnPropertyChanged(nameof(TavanProfili));
            OnPropertyChanged(nameof(Acikliklar));
            OnPropertyChanged(nameof(PencereBasligi));
            OnPropertyChanged(nameof(SalonYuksekligiM));
            SeciliKatman = _salon.Katmanlar.FirstOrDefault(k => k.Gorunur)
                ?? _salon.Katmanlar.FirstOrDefault();
            SeciliSeyirci = null;
            SeciliBalkon = null;
            SeciliTavanNoktasi = null;
            SeciliAciklik = null;
        }
    }

    public System.Collections.ObjectModel.ObservableCollection<SalonKatman> Katmanlar => _salon.Katmanlar;
    public System.Collections.ObjectModel.ObservableCollection<SalonCizimOgesi> CizimOgeleri => _salon.CizimOgeleri;
    public System.Collections.ObjectModel.ObservableCollection<SeyirciSekciyonu> SeyirciSekciyonlari => _salon.SeyirciSekciyonlari;

    private SeyirciSekciyonu? _seciliSeyirci;
    public SeyirciSekciyonu? SeciliSeyirci
    {
        get => _seciliSeyirci;
        set { if (_seciliSeyirci == value) return; _seciliSeyirci = value; OnPropertyChanged(); OnPropertyChanged(nameof(SeciliSeyirciVar)); }
    }
    public bool SeciliSeyirciVar => _seciliSeyirci is not null;

    public System.Collections.ObjectModel.ObservableCollection<BalkonGeometri> Balkonlar => _salon.Balkonlar;

    private BalkonGeometri? _seciliBalkon;
    public BalkonGeometri? SeciliBalkon
    {
        get => _seciliBalkon;
        set { if (_seciliBalkon == value) return; _seciliBalkon = value; OnPropertyChanged(); OnPropertyChanged(nameof(SeciliBalkonVar)); }
    }
    public bool SeciliBalkonVar => _seciliBalkon is not null;

    public System.Collections.ObjectModel.ObservableCollection<TavanKirilimNoktasi> TavanProfili => _salon.TavanProfili;

    private TavanKirilimNoktasi? _seciliTavanNoktasi;
    public TavanKirilimNoktasi? SeciliTavanNoktasi
    {
        get => _seciliTavanNoktasi;
        set { if (_seciliTavanNoktasi == value) return; _seciliTavanNoktasi = value; OnPropertyChanged(); OnPropertyChanged(nameof(SeciliTavanNoktasiVar)); }
    }
    public bool SeciliTavanNoktasiVar => _seciliTavanNoktasi is not null;

    public System.Collections.ObjectModel.ObservableCollection<DuvarAcikligi> Acikliklar => _salon.Acikliklar;

    private DuvarAcikligi? _seciliAciklik;
    public DuvarAcikligi? SeciliAciklik
    {
        get => _seciliAciklik;
        set { if (_seciliAciklik == value) return; _seciliAciklik = value; OnPropertyChanged(); OnPropertyChanged(nameof(SeciliAciklikVar)); }
    }
    public bool SeciliAciklikVar => _seciliAciklik is not null;

    private SalonKatman? _seciliKatman;
    /// Aktif katman — yeni çizilen öğeler buraya atanır.
    public SalonKatman? SeciliKatman
    {
        get => _seciliKatman;
        set { if (_seciliKatman == value) return; _seciliKatman = value; OnPropertyChanged(); OnPropertyChanged(nameof(SeciliKatmanVar)); }
    }
    public bool SeciliKatmanVar => _seciliKatman is not null;

    public double SalonYuksekligiM
    {
        get => _salon.SalonYuksekligiM;
        set
        {
            if (_salon.SalonYuksekligiM == value) return;
            _salon.SalonYuksekligiM = value;
            OnPropertyChanged();
        }
    }

    private string _durumMesaji = "Yeni salon geometrisi. Bir katman seçip çizmeye başlayın (Faz 1).";
    public string DurumMesaji
    {
        get => _durumMesaji;
        set { if (_durumMesaji == value) return; _durumMesaji = value; OnPropertyChanged(); }
    }

    private bool _isDirty;
    /// Kaydedilmemiş değişiklik var mı — başlıkta `*` gösterilir (maincodex §4.2),
    /// temizken kapanış onayı sorulmaz.
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PencereBasligi));
        }
    }

    private bool _uygulanmamisTaslakVar;
    /// Panelde henüz "Oluştur / Güncelle" ile modele uygulanmamış kullanıcı girişi.
    /// Model dirty durumundan ayrı tutulur; kapanış ve belge değiştirme korumasına dahildir.
    public bool UygulanmamisTaslakVar
    {
        get => _uygulanmamisTaslakVar;
        set
        {
            if (_uygulanmamisTaslakVar == value) return;
            _uygulanmamisTaslakVar = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PencereBasligi));
        }
    }

    private bool _canUndo;
    public bool CanUndo
    {
        get => _canUndo;
        set { if (_canUndo == value) return; _canUndo = value; OnPropertyChanged(); }
    }

    private bool _canRedo;
    public bool CanRedo
    {
        get => _canRedo;
        set { if (_canRedo == value) return; _canRedo = value; OnPropertyChanged(); }
    }

    private string _koordinatMesaji = "x: 0,00 m   y: 0,00 m";
    /// Fare koordinatı ve ölçek; komut talimatından ayrı gösterilir.
    public string KoordinatMesaji
    {
        get => _koordinatMesaji;
        set { if (_koordinatMesaji == value) return; _koordinatMesaji = value; OnPropertyChanged(); }
    }

    public string PencereBasligi
    {
        get
        {
            var ad = string.IsNullOrWhiteSpace(_salon.SalonAdi) ? "Adsız Salon" : _salon.SalonAdi;
            var dosya = string.IsNullOrWhiteSpace(_salon.DosyaYolu) ? "" : $" — {Path.GetFileName(_salon.DosyaYolu)}";
            var kirli = _isDirty || _uygulanmamisTaslakVar ? " *" : "";
            return $"Salon Tasarla — {ad}{dosya}{kirli}";
        }
    }

    public void BasligiYenile()
    {
        OnPropertyChanged(nameof(PencereBasligi));
        OnPropertyChanged(nameof(SalonYuksekligiM));
    }

    /// Kaydet/normalize aynı model örneğindeki koleksiyon nesnelerini yenileyebilir;
    /// binding'lerin eski koleksiyonda kalmaması için tüm türetilmiş kaynakları bildirir.
    public void ModelBaglariniYenile()
    {
        OnPropertyChanged(nameof(Katmanlar));
        OnPropertyChanged(nameof(CizimOgeleri));
        OnPropertyChanged(nameof(SeyirciSekciyonlari));
        OnPropertyChanged(nameof(Balkonlar));
        OnPropertyChanged(nameof(TavanProfili));
        OnPropertyChanged(nameof(Acikliklar));
    }
}
