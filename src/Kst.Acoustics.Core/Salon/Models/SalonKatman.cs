using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kst.Acoustics.Core.Salon.Models;

/// Salon yüzeylerinin kategori/katman türü. Katman adı (`SalonKatman.Ad`) M01'e
/// aktarılırken `ProjectSurface.Category` olur; tür ise varsayılan ad + renk + 3D
/// üretiminde anlamsal rol (zemin/duvar/tavan/seyirci…) için kullanılır.
public enum SalonKatmanTipi
{
    Genel,
    Zemin,
    Sahne,
    DuvarSahne,
    DuvarSeyirci,
    Tavan,
    Seyirci,
    Kornis,
    Balkon,
    Orkestra,
    Kapi,
    Pencere,
    Yansitici
}

/// Salon çizim/yüzey katmanı. Şablonlar bu katmanları otomatik atar; kullanıcı yeni
/// katman ekleyip adlandırabilir. `Ad` alanı M01 kategorisidir.
public class SalonKatman : ISalonKatman, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _ad = "Katman";
    /// M01 kategorisi olarak dışa aktarılan ad ("Zemin", "Duvar-Sahne"…).
    public string Ad { get => _ad; set => Set(ref _ad, value); }

    public SalonKatmanTipi Tip { get; set; } = SalonKatmanTipi.Genel;

    private string _renkHex = "#A0A0A0";
    public string RenkHex { get => _renkHex; set => Set(ref _renkHex, value); }

    private bool _gorunur = true;
    public bool Gorunur { get => _gorunur; set => Set(ref _gorunur, value); }

    private bool _kilitli;
    /// Kilitli katmandaki öğeler seçilemez, taşınamaz veya silinemez.
    public bool Kilitli { get => _kilitli; set => Set(ref _kilitli, value); }

    /// Tür için varsayılan kategori adı (M01'e gidecek string).
    public static string VarsayilanAd(SalonKatmanTipi tip) => tip switch
    {
        SalonKatmanTipi.Zemin        => "Zemin",
        SalonKatmanTipi.Sahne        => "Sahne",
        SalonKatmanTipi.DuvarSahne   => "Duvar-Sahne",
        SalonKatmanTipi.DuvarSeyirci => "Duvar-Seyirci",
        SalonKatmanTipi.Tavan        => "Tavan",
        SalonKatmanTipi.Seyirci      => "Seyirci",
        SalonKatmanTipi.Kornis       => "Korniş",
        SalonKatmanTipi.Balkon       => "Balkon",
        SalonKatmanTipi.Orkestra     => "Orkestra",
        SalonKatmanTipi.Kapi         => "Kapı",
        SalonKatmanTipi.Pencere      => "Pencere",
        SalonKatmanTipi.Yansitici    => "Yansıtıcı",
        _                            => "Genel"
    };

    /// Tür için varsayılan renk (2D çizim canvas'ı için).
    public static string VarsayilanRenk(SalonKatmanTipi tip) => tip switch
    {
        SalonKatmanTipi.Zemin        => "#6E7B8B",
        SalonKatmanTipi.Sahne        => "#C08A3E",
        SalonKatmanTipi.DuvarSahne   => "#B5694F",
        SalonKatmanTipi.DuvarSeyirci => "#5A8FB0",
        SalonKatmanTipi.Tavan        => "#8678B5",
        SalonKatmanTipi.Seyirci      => "#5AA86A",
        SalonKatmanTipi.Kornis       => "#A8A24A",
        SalonKatmanTipi.Balkon       => "#B0689A",
        SalonKatmanTipi.Orkestra     => "#4A9EA8",
        SalonKatmanTipi.Kapi         => "#C4813C",
        SalonKatmanTipi.Pencere      => "#5FB6D8",
        SalonKatmanTipi.Yansitici    => "#E0B84F",
        _                            => "#A0A0A0"
    };

    public static SalonKatman Olustur(SalonKatmanTipi tip) => new()
    {
        Tip = tip,
        Ad = VarsayilanAd(tip),
        RenkHex = VarsayilanRenk(tip)
    };

    public override string ToString() => Ad;
}
