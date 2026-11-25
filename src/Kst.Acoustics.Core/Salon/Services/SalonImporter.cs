using System;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// M51 → M01 köprüsü: SalonGeometri'yi M01'in tükettiği <see cref="AcousticsProject"/>'e çevirir.
/// Yüzeyler <see cref="Salon3DGorunumServisi"/>'den, hacim <see cref="SalonHacimServisi"/>'den
/// (gerçek plan geometrisi; bbox değil); kategoriler (Zemin/Duvar/Tavan/Sahne/Seyirci…)
/// M01'de malzeme atamasını sürer. `GeometriRevizyon` projeye damgalanır — M01 projeyi
/// sonradan açarken kaynak `.e4salon.json` güncellenmişse uyarı verebilir.
public static class SalonImporter
{
    public static AcousticsProject Cevir(SalonGeometri salon, string? kaynakDosya)
    {
        ArgumentNullException.ThrowIfNull(salon);
        SalonGeometriServisi.Normalize(salon);
        if (salon.SerbestTasarim)
        {
            var errors = SalonTasarimDogrulama.Kontrol(salon).Where(x=>x.Hata).ToList();
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n",errors.Select(x=>x.Mesaj)));
        }
        var surfaces = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        if (surfaces.Count == 0)
        {
            throw new InvalidDataException(
                "Salon geometrisi M01'e aktarılabilecek kapalı yüzey içermiyor.");
        }

        var volumeM3 = SalonHacimServisi.Hesapla(salon);
        if (!double.IsFinite(volumeM3) || volumeM3 <= 0)
        {
            throw new InvalidDataException(
                "Salon geometrisinden pozitif ve sonlu bir oda hacmi hesaplanamadı.");
        }

        return new AcousticsProject(
            Name: string.IsNullOrWhiteSpace(salon.SalonAdi) ? "Salon" : salon.SalonAdi,
            SourceFilePath: kaynakDosya,
            ImportedAtUtc: DateTime.UtcNow,
            ImportFormat: SurfaceImportFormat.Salon,
            Unit: SurfaceImportUnit.Meters,
            Surfaces: surfaces,
            RoomVolume: volumeM3,
            VolumeOrigin: RoomVolumeOrigin.SourceModel)
        {
            KaynakGeometriRevizyon = salon.GeometriRevizyon
        };
    }

    /// `.e4salon.json` dosyasını yükleyip M01 projesine çevirir.
    public static AcousticsProject Yukle(string dosyaYolu)
        => Cevir(SalonGeometriServisi.Ac(dosyaYolu), dosyaYolu);
}
