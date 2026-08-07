using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Rules;

/// <summary>
/// M34 on kontrol kurallarinin dayandigi mevzuat veya ic veri kaynagini tanimlar.
/// </summary>
public class YonetmelikKaynakProfili
{
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public string YayinlayanKurum { get; set; } = "";
    public DateOnly SurumTarihi { get; set; } = DateOnly.MinValue;
    public string ResmiGazeteSayisi { get; set; } = "";
    public string ResmiKaynakUrl { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public bool ResmiKaynakMi { get; set; } = true;
    public bool GuncelMetinTekrarKontrolGerektirir { get; set; } = true;
    public DateOnly SonDogrulamaTarihi { get; set; } = DateOnly.MinValue;
    public List<string> KuralKodlari { get; set; } = new();

    public bool TemelBilgilerVar =>
        !string.IsNullOrWhiteSpace(Kod) &&
        !string.IsNullOrWhiteSpace(Ad) &&
        !string.IsNullOrWhiteSpace(YayinlayanKurum) &&
        SurumTarihi != DateOnly.MinValue;
}
