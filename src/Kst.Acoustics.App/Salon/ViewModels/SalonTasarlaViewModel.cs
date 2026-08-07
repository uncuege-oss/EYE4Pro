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
            SeciliKatman = _salon.Katmanlar.Count > 0 ? _salon.Katmanlar[0] : null;
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
        set { if (_seciliSeyirci == value) return; _seciliSeyirci = value; OnPropertyChanged(); }
    }

    public System.Collections.ObjectModel.ObservableCollection<BalkonGeometri> Balkonlar => _salon.Balkonlar;

    private BalkonGeometri? _seciliBalkon;
    public BalkonGeometri? SeciliBalkon
    {
        get => _seciliBalkon;
        set { if (_seciliBalkon == value) return; _seciliBalkon = value; OnPropertyChanged(); }
    }

    public System.Collections.ObjectModel.ObservableCollection<TavanKirilimNoktasi> TavanProfili => _salon.TavanProfili;

    private TavanKirilimNoktasi? _seciliTavanNoktasi;
    public TavanKirilimNoktasi? SeciliTavanNoktasi
    {
        get => _seciliTavanNoktasi;
        set { if (_seciliTavanNoktasi == value) return; _seciliTavanNoktasi = value; OnPropertyChanged(); }
    }

    public System.Collections.ObjectModel.ObservableCollection<DuvarAcikligi> Acikliklar => _salon.Acikliklar;

    private DuvarAcikligi? _seciliAciklik;
    public DuvarAcikligi? SeciliAciklik
    {
        get => _seciliAciklik;
        set { if (_seciliAciklik == value) return; _seciliAciklik = value; OnPropertyChanged(); }
    }

    private SalonKatman? _seciliKatman;
    /// Aktif katman — yeni çizilen öğeler buraya atanır.
    public SalonKatman? SeciliKatman
    {
        get => _seciliKatman;
        set { if (_seciliKatman == value) return; _seciliKatman = value; OnPropertyChanged(); }
    }

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

    public string PencereBasligi
    {
        get
        {
            var ad = string.IsNullOrWhiteSpace(_salon.SalonAdi) ? "Adsız Salon" : _salon.SalonAdi;
            var dosya = string.IsNullOrWhiteSpace(_salon.DosyaYolu) ? "" : $" — {Path.GetFileName(_salon.DosyaYolu)}";
            var kirli = _isDirty ? " *" : "";
            return $"Salon Tasarla — {ad}{dosya}{kirli}";
        }
    }

    public void BasligiYenile()
    {
        OnPropertyChanged(nameof(PencereBasligi));
        OnPropertyChanged(nameof(SalonYuksekligiM));
    }
}
