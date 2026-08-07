using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.ImpactSurface;

/// <summary>
/// EN 1177 / ASTM F1292 prosedürlerine uygun bir uyum raporu.
/// Birden çok düşüş yüksekliğinde simülasyon sonuçlarını ve bin aramayla
/// hassas bulunmuş "sertifikalı" CFH'yi içerir.
/// </summary>
public class StandardComplianceReport
{
    public string StandardLabel { get; set; } = "EN 1177:2018 + ASTM F1292-22 (motor tahmini)";
    public string HeadFormProfile { get; set; } = "Yarı küresel rijit baş (4.6 kg, EN 1177 tipik)";
    public double HeadMassKg { get; set; }
    public double ContactAreaM2 { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    /// <summary>Tarama yüksekliklerinde tek tek deneme sonuçları.</summary>
    public List<StandardComplianceTrial> Trials { get; set; } = new();

    /// <summary>Bin aramayla bulunan kritik düşüş yüksekliği (m).</summary>
    public double CertifiedCriticalFallHeightM { get; set; }

    /// <summary>Hesap genel olarak tutarlı tamamlandı mı.</summary>
    public bool IsValid { get; set; } = true;

    public List<string> Notes { get; set; } = new();
}

public class StandardComplianceTrial
{
    public double DropHeightM { get; set; }
    public double GmaxG { get; set; }
    public double Hic { get; set; }
    public bool IsPassed { get; set; }

    /// <summary>"GEÇTİ" / "Gmax aştı" / "HIC aştı" gibi okunaklı durum.</summary>
    public string Verdict { get; set; } = string.Empty;
}
