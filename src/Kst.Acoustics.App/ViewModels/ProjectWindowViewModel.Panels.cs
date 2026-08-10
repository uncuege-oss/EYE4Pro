using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed class PanelSatirViewModel : ViewModelBase
{
    public AcousticPanel Panel { get; }
    public string Etiket => $"{LayerName}-{Panel.DilimNo}";
    
    public string LayerName => Panel.Tip switch
    {
        PanelTipi.Tavan => "TAVAN-PANEL",
        PanelTipi.DuvarSol => "DUVAR-PANEL-SOL",
        PanelTipi.DuvarSag => "DUVAR-PANEL-SAG",
        _ => "PANEL"
    };

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    private bool _isSelected;

    public string TipText => Panel.Tip.ToString();
    public string PanelEniText => $"{Panel.PanelEniM:F2} m";
    public string AciText => $"{Panel.AciDerece:F1}°";
    public string SarkmaCikmaText => $"{Panel.SarkmaCikmaM:F2} m";

    public PanelSatirViewModel(AcousticPanel panel)
    {
        Panel = panel;
    }

    public void UpdateDisplay()
    {
        OnPropertyChanged(nameof(AciText));
        OnPropertyChanged(nameof(SarkmaCikmaText));
        OnPropertyChanged(nameof(PanelEniText));
    }
}

public sealed class PanelEniSatirViewModel : ViewModelBase
{
    public int Sira
    {
        get => _sira;
        set => SetProperty(ref _sira, value);
    }
    private int _sira;

    public double EnM
    {
        get => _enM;
        set => SetProperty(ref _enM, value);
    }
    private double _enM;

    public PanelEniSatirViewModel(int sira, double enM)
    {
        _sira = sira;
        _enM = enM;
    }
}

public sealed partial class ProjectWindowViewModel
{
    public AcousticPanelKonfigurasyon? PanelKonfigurasyon
    {
        get => _panelKonfigurasyon;
        set => SetProperty(ref _panelKonfigurasyon, value);
    }
    private AcousticPanelKonfigurasyon? _panelKonfigurasyon;

    public ObservableCollection<string> SahneOnuSecenekleri { get; } = new();

    public string? SeciliSahneOnu
    {
        get => _seciliSahneOnu;
        set => SetProperty(ref _seciliSahneOnu, value);
    }
    private string? _seciliSahneOnu;

    public const string HedefYuzeyOtomatikEtiket = "(Otomatik)";
    public ObservableCollection<string> HedefYuzeySecenekleri { get; } = new();

    public string? SeciliTavanYuzey
    {
        get => _seciliTavanYuzey;
        set
        {
            if (SetProperty(ref _seciliTavanYuzey, value))
                RefreshHedefYuzeyOzeti();
        }
    }
    private string? _seciliTavanYuzey = HedefYuzeyOtomatikEtiket;

    public string? SeciliSolDuvarYuzey
    {
        get => _seciliSolDuvarYuzey;
        set
        {
            if (SetProperty(ref _seciliSolDuvarYuzey, value))
                RefreshHedefYuzeyOzeti();
        }
    }
    private string? _seciliSolDuvarYuzey = HedefYuzeyOtomatikEtiket;

    public string? SeciliSagDuvarYuzey
    {
        get => _seciliSagDuvarYuzey;
        set
        {
            if (SetProperty(ref _seciliSagDuvarYuzey, value))
                RefreshHedefYuzeyOzeti();
        }
    }
    private string? _seciliSagDuvarYuzey = HedefYuzeyOtomatikEtiket;

    public string HedefYuzeyOzeti
    {
        get => _hedefYuzeyOzeti;
        private set => SetProperty(ref _hedefYuzeyOzeti, value);
    }
    private string _hedefYuzeyOzeti = "";

    private static string? NormalizeHedef(string? secim) =>
        string.IsNullOrWhiteSpace(secim) || secim == HedefYuzeyOtomatikEtiket ? null : secim;

    public double PanelEniM
    {
        get => _panelEniM;
        set => SetProperty(ref _panelEniM, value);
    }
    private double _panelEniM = 0.6;

    public bool SiraliPanelEniAktif
    {
        get => _siraliPanelEniAktif;
        set
        {
            if (!SetProperty(ref _siraliPanelEniAktif, value))
                return;

            OnPropertyChanged(nameof(TekTipPanelEniAktif));
            OnPropertyChanged(nameof(PanelEniDurumText));
        }
    }
    private bool _siraliPanelEniAktif;

    public bool TekTipPanelEniAktif => !SiraliPanelEniAktif;

    public ObservableCollection<PanelEniSatirViewModel> PanelEniSatirlari { get; } = new();

    public PanelEniSatirViewModel? SeciliPanelEniSatiri
    {
        get => _seciliPanelEniSatiri;
        set => SetProperty(ref _seciliPanelEniSatiri, value);
    }
    private PanelEniSatirViewModel? _seciliPanelEniSatiri;

    public string PanelEniDurumText
    {
        get
        {
            if (!SiraliPanelEniAktif)
                return "Tek tip panel eni kullanılacak.";

            return TryParsePanelEnleri(out var enler, out _)
                ? $"{enler.Count} panel eni tanımlandı."
                : "Panel eni satırları 0'dan büyük olmalı.";
        }
    }

    public double SarkmaCikmaM
    {
        get => _sarkmaCikmaM;
        set => SetProperty(ref _sarkmaCikmaM, value);
    }
    private double _sarkmaCikmaM = 0.5;

    public bool TavanPanelAktif
    {
        get => _tavanPanelAktif;
        set => SetProperty(ref _tavanPanelAktif, value);
    }
    private bool _tavanPanelAktif = true;

    public bool SolDuvarPanelAktif
    {
        get => _solDuvarPanelAktif;
        set
        {
            if (SetProperty(ref _solDuvarPanelAktif, value))
                SyncDikeyDuvarFlag();
        }
    }
    private bool _solDuvarPanelAktif = true;

    public bool SagDuvarPanelAktif
    {
        get => _sagDuvarPanelAktif;
        set
        {
            if (SetProperty(ref _sagDuvarPanelAktif, value))
                SyncDikeyDuvarFlag();
        }
    }
    private bool _sagDuvarPanelAktif = true;

    public bool DikeyDuvarPanelAktif
    {
        get => _dikeyDuvarPanelAktif;
        set
        {
            if (!SetProperty(ref _dikeyDuvarPanelAktif, value))
                return;

            SolDuvarPanelAktif = value;
            SagDuvarPanelAktif = value;
        }
    }
    private bool _dikeyDuvarPanelAktif = true;

    private void SyncDikeyDuvarFlag() =>
        SetProperty(ref _dikeyDuvarPanelAktif, _solDuvarPanelAktif || _sagDuvarPanelAktif, nameof(DikeyDuvarPanelAktif));

    public double SeciliPanellerAciDegeri
    {
        get => _seciliPanellerAciDegeri;
        set => SetProperty(ref _seciliPanellerAciDegeri, value);
    }
    private double _seciliPanellerAciDegeri = 0.0;
    
    public double SeciliPanellerSarkmaDegeri
    {
        get => _seciliPanellerSarkmaDegeri;
        set => SetProperty(ref _seciliPanellerSarkmaDegeri, value);
    }
    private double _seciliPanellerSarkmaDegeri = 0.5;

    public ObservableCollection<PanelSatirViewModel> PanelListesi { get; } = new();

    public PanelSatirViewModel? SeciliPanelSatiri
    {
        get => _seciliPanelSatiri;
        set
        {
            if (!SetProperty(ref _seciliPanelSatiri, value) || value is null)
                return;

            value.IsSelected = true;
            UpdateSelectedSurfaceIdsFromPanelRows();
        }
    }
    private PanelSatirViewModel? _seciliPanelSatiri;

    private void InitPanels()
    {
        // Çalışma kopyası proje snapshot'ından ayrıdır. Böylece "Kaydetmeden Çık"
        // seçeneği mutable panel nesneleri üzerinden ana projeyi geriye dönük değiştirmez.
        PanelKonfigurasyon = ClonePanelConfiguration(_project.PanelKonfigurasyon);

        var surfaceNames = GetPanelHostSurfaces().Select(s => s.Name).Distinct().OrderBy(n => n).ToList();
        SahneOnuSecenekleri.Clear();
        foreach (var name in surfaceNames)
            SahneOnuSecenekleri.Add(name);

        HedefYuzeySecenekleri.Clear();
        HedefYuzeySecenekleri.Add(HedefYuzeyOtomatikEtiket);
        foreach (var name in surfaceNames)
            HedefYuzeySecenekleri.Add(name);

        if (PanelKonfigurasyon != null)
        {
            PanelEniM = PanelKonfigurasyon.PanelEniM;
            SiraliPanelEniAktif = PanelKonfigurasyon.PanelEniModu == PanelEniModu.Sirali;
            SetPanelEniRows(PanelKonfigurasyon.PanelEnleriM.Count > 0 ? PanelKonfigurasyon.PanelEnleriM : [PanelKonfigurasyon.PanelEniM]);
            SarkmaCikmaM = PanelKonfigurasyon.SarkmaCikmaM;
            TavanPanelAktif = PanelKonfigurasyon.AktifTipler.Contains(PanelTipi.Tavan);
            SolDuvarPanelAktif = PanelKonfigurasyon.AktifTipler.Contains(PanelTipi.DuvarSol);
            SagDuvarPanelAktif = PanelKonfigurasyon.AktifTipler.Contains(PanelTipi.DuvarSag);
            DikeyDuvarPanelAktif = SolDuvarPanelAktif || SagDuvarPanelAktif;
            SeciliSahneOnu = PanelKonfigurasyon.SahneOnuYuzeyId;
            SeciliTavanYuzey = HedefYuzeySecenekleri.Contains(PanelKonfigurasyon.TavanYuzeyId ?? "") ? PanelKonfigurasyon.TavanYuzeyId : HedefYuzeyOtomatikEtiket;
            SeciliSolDuvarYuzey = HedefYuzeySecenekleri.Contains(PanelKonfigurasyon.SolDuvarYuzeyId ?? "") ? PanelKonfigurasyon.SolDuvarYuzeyId : HedefYuzeyOtomatikEtiket;
            SeciliSagDuvarYuzey = HedefYuzeySecenekleri.Contains(PanelKonfigurasyon.SagDuvarYuzeyId ?? "") ? PanelKonfigurasyon.SagDuvarYuzeyId : HedefYuzeyOtomatikEtiket;

            foreach (var panel in PanelKonfigurasyon.Paneller)
            {
                AddPanelRow(panel);
            }
        }
        else if (SahneOnuSecenekleri.Any())
        {
            SeciliSahneOnu = SahneOnuSecenekleri.First();
            SetPanelEniRows([PanelEniM]);
        }

        if (PanelEniSatirlari.Count == 0)
            SetPanelEniRows([PanelEniM]);

        RefreshHedefYuzeyOzeti();
    }

    internal static AcousticPanelKonfigurasyon? ClonePanelConfiguration(AcousticPanelKonfigurasyon? source)
    {
        if (source is null) return null;

        return new AcousticPanelKonfigurasyon
        {
            OturumId = source.OturumId,
            SahneOnuYuzeyId = source.SahneOnuYuzeyId,
            PanelEniM = source.PanelEniM,
            PanelEniModu = source.PanelEniModu,
            PanelEnleriM = source.PanelEnleriM?.ToList() ?? [],
            SarkmaCikmaM = source.SarkmaCikmaM,
            AktifTipler = source.AktifTipler?.ToArray() ?? [],
            TavanYuzeyId = source.TavanYuzeyId,
            SolDuvarYuzeyId = source.SolDuvarYuzeyId,
            SagDuvarYuzeyId = source.SagDuvarYuzeyId,
            Paneller = source.Paneller?.Select(panel => new AcousticPanel
            {
                Id = panel.Id,
                Tip = panel.Tip,
                DilimNo = panel.DilimNo,
                PanelEniM = panel.PanelEniM,
                SarkmaCikmaM = panel.SarkmaCikmaM,
                AciDerece = panel.AciDerece,
                MaterialId = panel.MaterialId,
                Vertices = panel.Vertices?.ToList() ?? []
            }).ToList() ?? []
        };
    }

    private void RefreshHedefYuzeyOzeti()
    {
        var hostSurfaces = GetPanelHostSurfaces();
        if (hostSurfaces.Count == 0)
        {
            HedefYuzeyOzeti = "";
            return;
        }

        var konfig = new AcousticPanelKonfigurasyon
        {
            SahneOnuYuzeyId = SeciliSahneOnu,
            TavanYuzeyId = NormalizeHedef(SeciliTavanYuzey),
            SolDuvarYuzeyId = NormalizeHedef(SeciliSolDuvarYuzey),
            SagDuvarYuzeyId = NormalizeHedef(SeciliSagDuvarYuzey)
        };
        var hedefler = PanelHedefDetector.Bul(hostSurfaces, konfig);

        string Etiket(HedefYuzey? h) => h?.Surface.Name ?? "—";
        HedefYuzeyOzeti =
            $"Tavan: {Etiket(hedefler.Tavan)}   " +
            $"Sol: {Etiket(hedefler.SolDuvar)}   " +
            $"Sağ: {Etiket(hedefler.SagDuvar)}";
    }

    public void PanelEniSatiriEkle()
    {
        var nextWidth = PanelEniSatirlari.LastOrDefault()?.EnM ?? PanelEniM;
        var row = new PanelEniSatirViewModel(PanelEniSatirlari.Count + 1, nextWidth);
        row.PropertyChanged += PanelEniSatiri_PropertyChanged;
        PanelEniSatirlari.Add(row);
        SeciliPanelEniSatiri = row;
        OnPropertyChanged(nameof(PanelEniDurumText));
    }

    public void SeciliPanelEniSatiriniSil()
    {
        if (SeciliPanelEniSatiri is null || PanelEniSatirlari.Count <= 1)
            return;

        SeciliPanelEniSatiri.PropertyChanged -= PanelEniSatiri_PropertyChanged;
        PanelEniSatirlari.Remove(SeciliPanelEniSatiri);
        YenidenNumaralandirPanelEnleri();
        SeciliPanelEniSatiri = PanelEniSatirlari.LastOrDefault();
        OnPropertyChanged(nameof(PanelEniDurumText));
    }

    public void PanelOlustur()
    {
        var istenenTipler = new List<PanelTipi>();
        if (TavanPanelAktif)     istenenTipler.Add(PanelTipi.Tavan);
        if (SolDuvarPanelAktif)  istenenTipler.Add(PanelTipi.DuvarSol);
        if (SagDuvarPanelAktif)  istenenTipler.Add(PanelTipi.DuvarSag);

        if (istenenTipler.Count == 0)
        {
            StatusMessage = "Panel üretmek için tavan veya dikey panel seçin.";
            return;
        }

        var mevcutPaneller = PanelKonfigurasyon?.Paneller.ToList() ?? new List<AcousticPanel>();
        var mevcutTipler = mevcutPaneller.Select(p => p.Tip).Distinct().ToHashSet();
        var uretilecekTipler = istenenTipler.Where(t => !mevcutTipler.Contains(t)).ToArray();
        if (uretilecekTipler.Length == 0)
        {
            StatusMessage = "Seçilen panel tipi zaten üretildi. Yeniden üretmek için önce panelleri temizleyin.";
            return;
        }

        var panelEniModu = SiraliPanelEniAktif ? PanelEniModu.Sirali : PanelEniModu.TekTip;
        var panelEnleri = new List<double>();
        if (SiraliPanelEniAktif)
        {
            if (!TryParsePanelEnleri(out panelEnleri, out var hata))
            {
                StatusMessage = hata;
                return;
            }
        }

        var yeniUretimConfig = new AcousticPanelKonfigurasyon
        {
            PanelEniM = PanelEniM,
            PanelEniModu = panelEniModu,
            PanelEnleriM = panelEnleri,
            SarkmaCikmaM = SarkmaCikmaM,
            SahneOnuYuzeyId = SeciliSahneOnu,
            AktifTipler = uretilecekTipler,
            TavanYuzeyId = NormalizeHedef(SeciliTavanYuzey),
            SolDuvarYuzeyId = NormalizeHedef(SeciliSolDuvarYuzey),
            SagDuvarYuzeyId = NormalizeHedef(SeciliSagDuvarYuzey)
        };

        var hostSurfaces = GetPanelHostSurfaces();
        var boyutlar = PanelYerlesimServisi.BoyutlariHesapla(hostSurfaces, SeciliSahneOnu, _project.Unit);
        var yeniPaneller = PanelYerlesimServisi.PanelleriUret(yeniUretimConfig, hostSurfaces, boyutlar);
        if (yeniPaneller.Count == 0)
        {
            StatusMessage = SiraliPanelEniAktif
                ? "Sıralı panel enleri salon derinliğine sığmadı veya geçerli panel üretmedi."
                : "Panel üretilemedi. Panel eni ve salon derinliğini kontrol edin.";
            return;
        }

        var tumTipler = mevcutPaneller.Concat(yeniPaneller)
            .Select(p => p.Tip)
            .Distinct()
            .ToArray();
        var config = PanelKonfigurasyon ?? new AcousticPanelKonfigurasyon();
        config.PanelEniM = PanelEniM;
        config.PanelEniModu = panelEniModu;
        config.PanelEnleriM = panelEnleri;
        config.SarkmaCikmaM = SarkmaCikmaM;
        config.SahneOnuYuzeyId = SeciliSahneOnu;
        config.AktifTipler = tumTipler;
        config.TavanYuzeyId = NormalizeHedef(SeciliTavanYuzey);
        config.SolDuvarYuzeyId = NormalizeHedef(SeciliSolDuvarYuzey);
        config.SagDuvarYuzeyId = NormalizeHedef(SeciliSagDuvarYuzey);
        config.Paneller = mevcutPaneller.Concat(yeniPaneller).ToList();
        PanelKonfigurasyon = config;
        SeciliPanellerAciDegeri = 0;
        SeciliPanellerSarkmaDegeri = SarkmaCikmaM;

        PanelListesi.Clear();
        foreach (var panel in PanelKonfigurasyon.Paneller)
        {
            AddPanelRow(panel);
        }

        SenkronizeYuzeyler();
        StatusMessage = $"{yeniPaneller.Count} yeni panel eklendi. Mevcut panel tipleri korunuyor.";
    }

    private bool TryParsePanelEnleri(out List<double> enler, out string hata)
    {
        enler = PanelEniSatirlari.Select(r => r.EnM).ToList();
        hata = "";

        if (enler.Count == 0)
        {
            hata = "Sıralı panel eni için en az bir değer girin.";
            return false;
        }

        if (enler.Any(value => value <= 0 || !double.IsFinite(value)))
        {
            hata = "Panel eni değerleri 0'dan büyük olmalı.";
            return false;
        }

        return true;
    }

    private void SetPanelEniRows(IEnumerable<double> enler)
    {
        foreach (var row in PanelEniSatirlari)
            row.PropertyChanged -= PanelEniSatiri_PropertyChanged;

        PanelEniSatirlari.Clear();

        var valid = enler.Where(e => e > 0 && double.IsFinite(e)).DefaultIfEmpty(PanelEniM).ToList();
        for (var i = 0; i < valid.Count; i++)
        {
            var row = new PanelEniSatirViewModel(i + 1, valid[i]);
            row.PropertyChanged += PanelEniSatiri_PropertyChanged;
            PanelEniSatirlari.Add(row);
        }

        SeciliPanelEniSatiri = PanelEniSatirlari.FirstOrDefault();
        OnPropertyChanged(nameof(PanelEniDurumText));
    }

    private void PanelEniSatiri_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PanelEniSatirViewModel.EnM))
            OnPropertyChanged(nameof(PanelEniDurumText));
    }

    private void YenidenNumaralandirPanelEnleri()
    {
        for (var i = 0; i < PanelEniSatirlari.Count; i++)
            PanelEniSatirlari[i].Sira = i + 1;
    }

    public void PanelleriTemizle()
    {
        PanelKonfigurasyon = null;
        PanelListesi.Clear();
        SenkronizeYuzeyler();
    }

    public void SeciliPanelleriGuncelle()
    {
        var secililer = PanelListesi.Where(p => p.IsSelected).ToList();
        if (!secililer.Any()) return;

        bool degisiklikVar = false;
        var aciDelta = SeciliPanellerAciDegeri;
        var hedefler = GetPanelUpdateTargets(secililer);
        foreach (var panel in hedefler)
        {
            var nextAngle = panel.Tip switch
            {
                PanelTipi.DuvarSol => panel.AciDerece - aciDelta,
                PanelTipi.DuvarSag => panel.AciDerece + aciDelta,
                _ => panel.AciDerece + aciDelta
            };

            if (Math.Abs(panel.AciDerece - nextAngle) > 0.001 ||
                Math.Abs(panel.SarkmaCikmaM - SeciliPanellerSarkmaDegeri) > 0.001)
            {
                panel.AciDerece = nextAngle;
                ApplyPanelSarkmaDelta(panel, SeciliPanellerSarkmaDegeri - panel.SarkmaCikmaM);
                panel.SarkmaCikmaM = SeciliPanellerSarkmaDegeri;
                PanelListesi.FirstOrDefault(r => r.Panel == panel)?.UpdateDisplay();
                degisiklikVar = true;
            }
        }

        if (degisiklikVar)
        {
            SenkronizeYuzeyler();
        }
    }

    private void SenkronizeYuzeyler()
    {
        // 1. Önce _workingSurfaces içinden eski panelleri temizle
        var panelLayerNames = new[] { "TAVAN-PANEL", "DUVAR-PANEL-SOL", "DUVAR-PANEL-SAG" };
        _workingSurfaces.RemoveAll(s => panelLayerNames.Contains(s.Category.ToUpperInvariant()));

        // Kategoriler değişeceği için CategoryNodes vs güncellenmeli.
        // Ancak biz NotifySurfacesChanged dediğimizde bu otomatik güncellenmiyor olabilir. ProjectWindowViewModel içinde
        // CategoryNodes sadece constructor'da oluşturulmuş. 
        // Eğer kategori ağacını da güncellemek istiyorsak, CategoryNodes'i yeniden oluşturmalıyız.

        // 2. Yeni panelleri ekle
        if (PanelKonfigurasyon != null && PanelKonfigurasyon.Paneller.Any())
        {
            var boyutlar = PanelYerlesimServisi.BoyutlariHesapla(
                GetPanelHostSurfaces(), PanelKonfigurasyon.SahneOnuYuzeyId, _project.Unit);
            int maxId = _workingSurfaces.Any() ? _workingSurfaces.Max(s => s.Id) : 0;
            var yeniYuzeyler = PanelYerlesimServisi.PanelleriYuzeyeAktar(PanelKonfigurasyon.Paneller, maxId + 1, boyutlar);
            _workingSurfaces.AddRange(yeniYuzeyler);
        }

        RebuildCategoryNodes();
        RefreshLightTracingOptions();
        NotifySurfacesChanged("Panel yerleşimi değiştirildi");
        if (PanelListesi.Any(p => p.IsSelected))
            UpdateSelectedSurfaceIdsFromPanelRows();
        StatusMessage = $"{PanelListesi.Count} panel senkronize edildi.";
    }

    private IReadOnlyList<ProjectSurface> GetPanelHostSurfaces() =>
        _workingSurfaces.Where(surface => !IsGeneratedPanelSurface(surface)).ToArray();

    private void ApplyPanelSarkmaDelta(AcousticPanel panel, double diffMetre)
    {
        if (Math.Abs(diffMetre) < 0.001)
            return;

        // diffMetre kullanıcıdan metre cinsinden gelir; vertices proje birimindedir.
        var diff = diffMetre * PanelYerlesimServisi.GetMetreCarpan(_project.Unit);
        double dx = panel.Tip == PanelTipi.DuvarSol ? diff : panel.Tip == PanelTipi.DuvarSag ? -diff : 0;
        double dz = panel.Tip == PanelTipi.Tavan ? -diff : 0;
        panel.Vertices = panel.Vertices.Select(v => new Point3D(v.X + dx, v.Y, v.Z + dz)).ToList();
    }

    private void AddPanelRow(AcousticPanel panel)
    {
        var row = new PanelSatirViewModel(panel);
        row.PropertyChanged += PanelRow_PropertyChanged;
        PanelListesi.Add(row);
    }

    private void PanelRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_panelSelectionSyncInProgress)
            return;

        if (e.PropertyName == nameof(PanelSatirViewModel.IsSelected))
            UpdateSelectedSurfaceIdsFromPanelRows();
    }

    private bool _panelSelectionSyncInProgress;

    private void UpdateSelectedSurfaceIdsFromPanelRows()
    {
        var ids = PanelListesi
            .Where(p => p.IsSelected)
            .Select(GetPanelSurfaceId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        SelectedSurfaceIds = ids;
        HighlightedSurfaceId = ids.Length > 0 ? ids[^1] : null;
    }

    private int? GetPanelSurfaceId(PanelSatirViewModel row)
    {
        return _workingSurfaces.FirstOrDefault(s =>
            s.Category.Equals(row.LayerName, StringComparison.OrdinalIgnoreCase) &&
            s.Name.Equals(row.Etiket, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private void SyncPanelRowsFromSelectedSurfaceIds(IReadOnlyList<int> selectedIds)
    {
        if (PanelListesi.Count == 0)
            return;

        var selectedSet = selectedIds.ToHashSet();
        var selectedRows = new List<PanelSatirViewModel>();
        _panelSelectionSyncInProgress = true;
        try
        {
            foreach (var row in PanelListesi)
            {
                var surfaceId = GetPanelSurfaceId(row);
                var selected = surfaceId is not null && selectedSet.Contains(surfaceId.Value);
                row.IsSelected = selected;
                if (selected)
                    selectedRows.Add(row);
            }
        }
        finally
        {
            _panelSelectionSyncInProgress = false;
        }

        SyncPanelEditFieldsFromSelection(selectedRows);
    }

    private void SyncPanelEditFieldsFromSelection(IReadOnlyList<PanelSatirViewModel> selectedRows)
    {
        if (selectedRows.Count == 0)
            return;

        // Açı delta alanı sıfırlanmıyor — kullanıcı aynı açıyı birden fazla panele uygulayabilir
        SeciliPanellerSarkmaDegeri = selectedRows[0].Panel.SarkmaCikmaM;
    }

    private IReadOnlyList<AcousticPanel> GetPanelUpdateTargets(IReadOnlyList<PanelSatirViewModel> selectedRows)
    {
        var result = new List<AcousticPanel>();
        foreach (var row in selectedRows)
        {
            AddPanelTarget(result, row.Panel);

            if (row.Panel.Tip is not (PanelTipi.DuvarSol or PanelTipi.DuvarSag))
                continue;

            var mirrorTip = row.Panel.Tip == PanelTipi.DuvarSol ? PanelTipi.DuvarSag : PanelTipi.DuvarSol;
            var mirror = PanelKonfigurasyon?.Paneller.FirstOrDefault(p =>
                p.Tip == mirrorTip && p.DilimNo == row.Panel.DilimNo);
            if (mirror is not null)
                AddPanelTarget(result, mirror);
        }

        return result;
    }

    private static void AddPanelTarget(List<AcousticPanel> result, AcousticPanel panel)
    {
        if (result.All(p => p.Id != panel.Id))
            result.Add(panel);
    }

    private void RebuildCategoryNodes()
    {
        var grouped = _workingSurfaces
            .GroupBy(s => s.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Sadece yeni kategori düğümlerini ekle, varolanların referansını koru
        var currentNodes = CategoryNodes.ToList();
        var newNodes = new List<CategoryNodeViewModel>();

        foreach (var g in grouped)
        {
            var surfaces = g.Select(s => new SurfaceNodeViewModel(s.Id, s.Name, SelectSurfaceById));
            var existingNode = currentNodes.FirstOrDefault(n => string.Equals(n.Name, g.Key, StringComparison.OrdinalIgnoreCase));
            
            if (existingNode != null)
            {
                // Aslında mevcut node'un Surfaces listesi immutable. Eğer değiştiremeyeceksek yeni node yaratmalıyız.
                // Property'si CategoryNodes read-only.
                // Değiştirebilmek için CategoryNodes'u ObservableCollection yapmalıyız.
                // Ancak ProjectWindowViewModel içinde CategoryNodes IReadOnlyList.
                // O yüzden bunu yapabilmek için IReadOnlyList referansını yenileyebiliriz.
            }
            
            var node = new CategoryNodeViewModel(g.Key, surfaces);
            if (existingNode != null)
            {
                node.IsVisible = existingNode.IsVisible;
                node.IsExpanded = existingNode.IsExpanded;
            }
            
                node.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(CategoryNodeViewModel.IsVisible))
                        NotifyViewportPerformanceChanged();
                };
            newNodes.Add(node);
        }

        CategoryNodes = newNodes;
        OnPropertyChanged(nameof(CategoryNodes));

        // Material rows'u güncelle
        // Eksik kategoriler için yeni CategoryMaterialViewModel ekle, fazlaları sil
        var existingMatCategories = CategoryMaterialRows.Select(r => r.Category).ToList();
        var newCategories = newNodes.Select(n => n.Name).ToList();

        foreach (var toRemove in existingMatCategories.Except(newCategories).ToList())
        {
            var row = CategoryMaterialRows.First(r => r.Category == toRemove);
            CategoryMaterialRows.Remove(row);
        }

        foreach (var toAdd in newCategories.Except(existingMatCategories).ToList())
        {
            var node = newNodes.First(n => n.Name == toAdd);
            _project.CategoryMaterials.TryGetValue(node.Name, out var matId);
            var resolved = matId is not null ? MaterialDatabase.Instance.FindById(matId) : null;
            var material = resolved ?? MaterialLibrary.Default;
            var row = new CategoryMaterialViewModel(
                node.Name, node.Count, material,
                unresolvedMaterialId: matId is not null && resolved is null ? matId : null);
            row.PropertyChanged += (_, _) => RefreshMaterialStatus();
            CategoryMaterialRows.Add(row);
        }
    }
}
