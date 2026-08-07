using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

namespace Kst.Acoustics.App.Electric.ZayifAkimTekHat;

public partial class ZayifAkimKutuphaneViewModel : ObservableObject
{
    private readonly ZayifAkimTekHatProje proje;
    [ObservableProperty] private ElvKutuphaneAltBasligi seciliAltBaslik;
    [ObservableProperty] private ObservableCollection<KutuphaneSatiri> cihazlar = [];
    [ObservableProperty] private ObservableCollection<KutuphaneSatiri> kablolar = [];
    [ObservableProperty] private KutuphaneSatiri? seciliCihaz;
    [ObservableProperty] private KutuphaneSatiri? seciliKablo;

    public IReadOnlyList<ElvKutuphaneAltBasligi> AltBasliklar => ElvKutuphaneServisi.AltBasliklar;
    public bool Degisti { get; private set; }

    public ZayifAkimKutuphaneViewModel(ZayifAkimTekHatProje proje, string? baslangicKodu = null)
    {
        this.proje = proje;
        seciliAltBaslik = AltBasliklar.FirstOrDefault(x => x.Kod == baslangicKodu) ?? AltBasliklar[0];
        Yenile();
    }

    partial void OnSeciliAltBaslikChanged(ElvKutuphaneAltBasligi value) => Yenile();

    [RelayCommand]
    private void CihazEkle() => OzelKalemEkle(ElvKutuphaneKalemTuru.Cihaz);

    [RelayCommand]
    private void KabloEkle() => OzelKalemEkle(ElvKutuphaneKalemTuru.Kablo);

    [RelayCommand]
    private void CihazSil() => OzelKalemSil(SeciliCihaz);

    [RelayCommand]
    private void KabloSil() => OzelKalemSil(SeciliKablo);

    private void OzelKalemEkle(ElvKutuphaneKalemTuru tur)
    {
        var model = new ElvOzelKutuphaneKalemi
        {
            AltBaslikKodu = SeciliAltBaslik.Kod,
            Tur = tur,
            Ad = tur == ElvKutuphaneKalemTuru.Cihaz ? "Yeni Cihaz" : "Yeni Kablo"
        };
        proje.OzelKutuphaneKalemleri.Add(model);
        Degisti = true;
        Yenile();
        if (tur == ElvKutuphaneKalemTuru.Cihaz)
            SeciliCihaz = Cihazlar.FirstOrDefault(x => x.Id == model.Id);
        else
            SeciliKablo = Kablolar.FirstOrDefault(x => x.Id == model.Id);
    }

    private void OzelKalemSil(KutuphaneSatiri? satir)
    {
        if (satir is null || satir.Yerlesik || satir.Kaynak is null) return;
        proje.OzelKutuphaneKalemleri.Remove(satir.Kaynak);
        Degisti = true;
        Yenile();
    }

    private void Yenile()
    {
        Cihazlar = SatirlariOlustur(ElvKutuphaneKalemTuru.Cihaz,
            ElvKutuphaneServisi.YerlesikCihazlar(SeciliAltBaslik.Kod));
        Kablolar = SatirlariOlustur(ElvKutuphaneKalemTuru.Kablo,
            ElvKutuphaneServisi.YerlesikKablolar(SeciliAltBaslik.Kod));
        SeciliCihaz = Cihazlar.FirstOrDefault();
        SeciliKablo = Kablolar.FirstOrDefault();
    }

    private ObservableCollection<KutuphaneSatiri> SatirlariOlustur(
        ElvKutuphaneKalemTuru tur, IReadOnlyList<string> yerlesik)
    {
        var satirlar = yerlesik.Select(ad => new KutuphaneSatiri(ad)).ToList();
        satirlar.AddRange(proje.OzelKutuphaneKalemleri
            .Where(k => k.AltBaslikKodu == SeciliAltBaslik.Kod && k.Tur == tur)
            .Select(k => new KutuphaneSatiri(k, () => Degisti = true)));
        return new ObservableCollection<KutuphaneSatiri>(satirlar);
    }
}

public partial class KutuphaneSatiri : ObservableObject
{
    private readonly Action? degisti;
    [ObservableProperty] private string ad;

    public string Id { get; }
    public bool Yerlesik { get; }
    public string KaynakText => Yerlesik ? "Yerleşik • Salt okunur" : "Kullanıcı";
    public ElvOzelKutuphaneKalemi? Kaynak { get; }

    public KutuphaneSatiri(string yerlesikAd)
    {
        Id = $"yerlesik:{yerlesikAd}";
        ad = yerlesikAd;
        Yerlesik = true;
    }

    public KutuphaneSatiri(ElvOzelKutuphaneKalemi kaynak, Action degisti)
    {
        Kaynak = kaynak;
        Id = kaynak.Id;
        ad = kaynak.Ad;
        this.degisti = degisti;
    }

    partial void OnAdChanged(string value)
    {
        if (Yerlesik || Kaynak is null) return;
        Kaynak.Ad = value.Trim();
        degisti?.Invoke();
    }
}
