using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.App.BuildingAcoustics.ViewModels;

/// <summary>Profesyonel araçlardaki farklı Core sonuçlarını tek, sade tablo diline çevirir.</summary>
public sealed class M08AracSatiri : ObservableObject
{
    public string Baslik { get; init; } = "";
    public string AltBaslik { get; init; } = "";
    public string Deger { get; init; } = "";
    public string Aciklama { get; init; } = "";
    public string Durum { get; init; } = "";
    public Guid? HedefId { get; init; }
    public object? Tag { get; init; }
}

public sealed class M08TopluMekanSatiri : ObservableObject
{
    private readonly BinaProje _proje;
    private readonly IReadOnlyList<YapiElemani> _kutuphane;
    public Kat KatModel { get; }
    public Mekan MekanModel { get; }

    public M08TopluMekanSatiri(
        BinaProje proje,
        Kat kat,
        Mekan mekan,
        IReadOnlyList<YapiElemani> kutuphane)
    {
        _proje = proje;
        _kutuphane = kutuphane;
        KatModel = kat;
        MekanModel = mekan;
    }

    public string Kat => string.IsNullOrWhiteSpace(KatModel.Ad) ? $"Kat {KatModel.KatNo}" : KatModel.Ad;
    public string MekanAdi
    {
        get => MekanModel.Ad;
        set { MekanModel.Ad = value?.Trim() ?? ""; OnPropertyChanged(); }
    }
    public string KullanimTipi
    {
        get => MekanModel.KullanimTipi;
        set { MekanModel.KullanimTipi = value?.Trim() ?? ""; OnPropertyChanged(); }
    }
    public string KaynakDerecesi
    {
        get => MekanModel.KaynakDerecesi;
        set { MekanModel.KaynakDerecesi = NormalKaynak(value); OnPropertyChanged(); }
    }
    public string AliciSinifi
    {
        get => MekanModel.AliciSinifi;
        set { MekanModel.AliciSinifi = NormalAlici(value); OnPropertyChanged(); }
    }
    public string BagimsizBolum
    {
        get => MekanModel.BagimsizBolumId is { } id
            ? _proje.BagimsizBolumler.FirstOrDefault(x => x.Id == id)?.Ad ?? ""
            : "";
        set
        {
            var ad = value?.Trim() ?? "";
            if (ad.Length == 0) MekanModel.BagimsizBolumId = null;
            else if (_proje.BagimsizBolumler.FirstOrDefault(x =>
                         string.Equals(x.Ad, ad, StringComparison.CurrentCultureIgnoreCase)) is { } bolum)
                MekanModel.BagimsizBolumId = bolum.Id;
            OnPropertyChanged();
        }
    }
    public string ZeminMalzemesi
    {
        get => MekanModel.ZeminYapiElemani?.Ad ?? "";
        set
        {
            MekanModel.ZeminYapiElemani = MalzemeBul(value, YapiElemaniTipi.Doseme) ?? MekanModel.ZeminYapiElemani;
            OnPropertyChanged(); OnPropertyChanged(nameof(AtamaDurumu));
        }
    }
    public string TavanMalzemesi
    {
        get => MekanModel.TavanYapiElemani?.Ad ?? "";
        set
        {
            MekanModel.TavanYapiElemani = MalzemeBul(value, YapiElemaniTipi.Tavan) ?? MekanModel.TavanYapiElemani;
            OnPropertyChanged(); OnPropertyChanged(nameof(AtamaDurumu));
        }
    }
    public double TesisatSurekliDb
    {
        get => MekanModel.TesisatSurekliDb;
        set { MekanModel.TesisatSurekliDb = Math.Max(0, value); OnPropertyChanged(); }
    }
    public double TesisatKesikliDb
    {
        get => MekanModel.TesisatKesikliDb;
        set { MekanModel.TesisatKesikliDb = Math.Max(0, value); OnPropertyChanged(); }
    }
    public double EmilimAlani
    {
        get => MekanModel.EmilimAlani;
        set { MekanModel.EmilimAlani = Math.Max(0, value); OnPropertyChanged(); }
    }
    public string AtamaDurumu => MekanModel.AtamaTamamMi ? "Tamam" : "Eksik";

    public void Yenile()
    {
        OnPropertyChanged(string.Empty);
        OnPropertyChanged(nameof(AtamaDurumu));
    }

    private YapiElemani? MalzemeBul(string? ad, YapiElemaniTipi tip) =>
        _kutuphane.FirstOrDefault(x => x.Tip == tip &&
            string.Equals(x.Ad, ad?.Trim(), StringComparison.CurrentCultureIgnoreCase));

    private static string NormalKaynak(string? value) => (value ?? "").Trim().ToUpperInvariant() switch
    {
        "DG" => "DG", "OG" => "OG", "YG" => "YG", _ => "",
    };
    private static string NormalAlici(string? value) => (value ?? "").Trim().ToUpperInvariant() switch
    {
        "I" => "I", "II" => "II", "III" => "III", _ => "",
    };
}

public sealed class M08TopluBolmeSatiri : ObservableObject
{
    private readonly IReadOnlyList<YapiElemani> _kutuphane;
    public Kat KatModel { get; }
    public Bolme BolmeModel { get; }

    public M08TopluBolmeSatiri(Kat kat, Bolme bolme, IReadOnlyList<YapiElemani> kutuphane)
    {
        KatModel = kat;
        BolmeModel = bolme;
        _kutuphane = kutuphane;
    }

    public string Kat => string.IsNullOrWhiteSpace(KatModel.Ad) ? $"Kat {KatModel.KatNo}" : KatModel.Ad;
    public string KaynakMekan => BolmeModel.KaynakMekan?.Ad ?? "";
    public string AliciMekan => BolmeModel.AliciMekan?.Ad ?? "Dış cephe";
    public string BolmeAdi => BolmeModel.Ad;
    public double Alan => BolmeModel.Alan;
    public string Malzeme
    {
        get => BolmeModel.YapiElemani?.Ad ?? "";
        set
        {
            var yeni = _kutuphane.FirstOrDefault(x => x.Tip == YapiElemaniTipi.Duvar &&
                string.Equals(x.Ad, value?.Trim(), StringComparison.CurrentCultureIgnoreCase));
            if (yeni is not null) BolmeModel.YapiElemani = yeni;
            OnPropertyChanged();
        }
    }
    public string AciklikOzeti => BolmeModel.Acikliklar.Count == 0
        ? "Yok"
        : $"{BolmeModel.Acikliklar.Count} adet · {BolmeModel.AciklikAlani:F1} m²";
    public string Komsuluk => BolmeModel.KomsulukTipiEtiketi;

    public void Yenile() => OnPropertyChanged(string.Empty);
}
