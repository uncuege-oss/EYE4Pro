using Kst.Acoustics.Core.Exporting;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// M51 salon geometrisini DXF (3DFACE) olarak dışa aktarır. Mevcut <see cref="DxfExportService"/>
/// kullanılır; her yüzeyin kategorisi DXF katmanı olur. Üretilen DXF M01'in mevcut DXF import
/// yolundan da okunabilir.
public static class SalonDxfExportServisi
{
    public static void DisaAktar(SalonGeometri salon, string dosyaYolu)
    {
        var surfaces = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        new DxfExportService().Export(dosyaYolu, surfaces, SurfaceImportUnit.Meters);
    }
}
