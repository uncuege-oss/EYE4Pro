using System;
using System.Text;
using System.Linq;

namespace Kst.Acoustics.Core.SoundInsulation;

public static class SoundInsulationReportService
{
    public static string GenerateTextReport(SoundInsulationResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("====================================================");
        sb.AppendLine("      EYE4PRO ACOUSTICS - SES YALITIMI RAPORU       ");
        sb.AppendLine("====================================================");
        sb.AppendLine($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"Sistem Adı: {result.Assembly.Name}");
        sb.AppendLine("----------------------------------------------------");
        sb.AppendLine("KATMAN DETAYLARI:");
        foreach (var layer in result.Assembly.Layers)
        {
            var sourceInfo = !string.IsNullOrWhiteSpace(layer.Material.Source) ? $" [{layer.Material.Source}]" : "";
            sb.AppendLine($"- {layer.Material.Name}: {layer.Thickness * 1000:F1} mm{sourceInfo}");
            if (!string.IsNullOrWhiteSpace(layer.Material.Manufacturer))
                sb.AppendLine($"  Üretici/Referans: {layer.Material.Manufacturer}");
            if (!string.IsNullOrWhiteSpace(layer.Material.Reference))
                sb.AppendLine($"  Kaynakça: {layer.Material.Reference}");
            sb.AppendLine($"  Veri Güvenilirliği: {layer.Material.Reliability}");
        }
        sb.AppendLine("----------------------------------------------------");
        sb.AppendLine("AKUSTİK DEĞERLENDİRME (ISO 717-1):");
        if (result.RwRating is { } rating)
        {
            sb.AppendLine($"Rw (C; Ctr) = {rating.Rw} ({rating.C}; {rating.Ctr}) dB");
            if (rating.Stc is { } stc) sb.AppendLine($"Tahmini STC = {stc}");
            if (rating.Oitc is { } oitc) sb.AppendLine($"Tahmini OITC = {oitc}");
        }
        else
        {
            sb.AppendLine("Rw hesaplanamadı.");
        }
        sb.AppendLine($"Güvenilirlik Puanı: %{result.ReliabilityScore:F0}");
        if (result.MassAirMassResonance > 0)
            sb.AppendLine($"Kütle-Hava-Kütle Rezonansı: {result.MassAirMassResonance:F1} Hz");
        if (result.CriticalFrequencies is { Count: > 0 })
        {
            sb.AppendLine("Kritik Frekanslar:");
            foreach (var cf in result.CriticalFrequencies.OrderBy(x => x.Value))
                sb.AppendLine($"- {cf.Key}: {cf.Value:F0} Hz");
        }
        if (result.RwRating?.LimitingFrequencies.Count > 0)
            sb.AppendLine($"Sınırlayıcı Bantlar: {string.Join(", ", result.RwRating.LimitingFrequencies)} Hz");
        if (result.Warnings.Count > 0)
        {
            sb.AppendLine("UYARILAR:");
            foreach (var warning in result.Warnings)
                sb.AppendLine($"- {warning}");
        }
        sb.AppendLine("Model Sınırlaması: Bu çıktı mühendislik ön tasarım tahminidir; resmi laboratuvar sertifikası yerine geçmez. Flanking/yan iletim ve saha uygulama detayları ayrıca değerlendirilmelidir.");
        sb.AppendLine("----------------------------------------------------");
        sb.AppendLine("FREKANS BAZLI SES AZALTIM İNDEKSİ (R):");
        sb.AppendLine("Hz\tR (dB)");
        foreach (var pair in result.TransmissionLossValues.OrderBy(p => p.Key))
        {
            sb.AppendLine($"{pair.Key}\t{pair.Value:F1}");
        }
        sb.AppendLine("====================================================");
        return sb.ToString();
    }
}
