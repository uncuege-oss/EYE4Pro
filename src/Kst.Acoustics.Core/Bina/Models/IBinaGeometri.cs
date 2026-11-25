using System.Collections.Generic;

namespace Kst.Acoustics.Core.Bina.Models;

/// Modüllere (M08, M12, M15…) verilen salt-okunur bina geometrisi görünümü.
public interface IBinaGeometri
{
    int SemaSurum { get; }
    int GeometriRevizyon { get; }
    string BinaAdi { get; }
    IReadOnlyList<IBinaKat> Katlar { get; }
    IReadOnlyList<IBinaDuseyBirlesim> DuseyBirlesimleri { get; }
}

public interface IBinaKat
{
    string Id { get; }
    string Ad { get; }
    int KatNo { get; }
    double KatYuksekligiM { get; }
    double? TabanKotuM { get; }
    IReadOnlyList<IBinaOda> Odalar { get; }
    IReadOnlyList<IBinaAciklik> Acikliklar { get; }
}

public interface IBinaOda
{
    string Id { get; }
    string Ad { get; }
    double AlanM2 { get; }
    /// Oda bazlı yükseklik override. null = kat yüksekliği kullanılır.
    double? YukseklikOverrideM { get; }
    double? TabanKotuOverrideM { get; }
    IReadOnlyList<string> KaynakMahalAdlari { get; }
}

public interface IBinaAciklik
{
    string Id { get; }
    BinaAciklikTipi Tip { get; }
    double GenislikM { get; }
    double YukseklikM { get; }
    double EsikYuksekligiM { get; }
    bool EsikYuksekligiBelirtildi { get; }
    double EtkinEsikYuksekligiM { get; }
    bool IptalEdildi { get; }
}

public interface IBinaDuseyBirlesim
{
    string Id { get; }
    bool AktifMi { get; }
    IReadOnlyList<IBinaBirlesimUyesi> Uyeler { get; }
}

public interface IBinaBirlesimUyesi
{
    /// Üyenin bulunduğu katın kararlı kimliği (BinaKat.Id).
    string KatId { get; }
    int KatNo { get; }
    string OdaId { get; }
    bool TavanAcik { get; }
    bool DosemeAcik { get; }
}
