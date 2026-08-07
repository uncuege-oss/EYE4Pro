namespace Kst.Acoustics.Core.Terrain.Models;

/// Arazi verisinin geldiği DXF kaynağı.
public enum AraziVeriKaynagi
{
    SpotKot,   // POINT entity (tekil kot)
    Kontur,    // eşyükselti eğrisi (polyline)
    Yuzey,     // 3DFACE / nokta bulutu
    Manuel     // kullanıcı girişi
}

/// Bir kontur eğrisinin kotunun nasıl belirlendiği — güven düzeyini gösterir.
public enum AraziKotKaynagi
{
    Bilinmiyor,    // kot bulunamadı (kullanıcı girmeli)
    VertexZ,       // 3D polyline köşe Z'si
    DuzlemKotu,    // Polyline2D düzlem kotu (Elevation)
    KatmanAdi,     // katman adından çıkarılan sayı (örn. KONTUR_540)
    MetinEtiketi   // yakın TEXT/MTEXT etiketinden çözülen sayı
}
