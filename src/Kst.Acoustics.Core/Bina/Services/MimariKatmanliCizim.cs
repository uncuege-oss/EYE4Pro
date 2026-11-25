using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

public sealed record MimariCizimKatmani(string Ad, int NesneSayisi, bool VarsayilanAcik)
{
    /// Katmanı kapatırken ölçü kanıtının da gizleneceğini önizlemede belirtir.
    public int KotYukseklikEtiketiSayisi { get; init; }
}

/// <summary>Bir kez açılmış CAD çizimi; katman düğmeleri yalnız bu veriyi süzer, oda üretmez.</summary>
public sealed class MimariKatmanliCizim
{
    public IReadOnlyList<MimariCizimKatmani> Katmanlar { get; }
    public string? UnitWarning { get; }
    public string KaynakYolu { get; }
    private readonly IReadOnlyList<KatmanliOge> _ogeler;

    internal MimariKatmanliCizim(string kaynak, string? uyari,
        IReadOnlyList<MimariCizimKatmani> katmanlar, IReadOnlyList<KatmanliOge> ogeler)
    { KaynakYolu = kaynak; UnitWarning = uyari; Katmanlar = katmanlar; _ogeler = ogeler; }

    /// <summary>Önizleme ve Üret aynı seçili veri kümesini kullanır. Dönüş nesnesi bağımsızdır.</summary>
    public BinaKat HamKatOlustur(IEnumerable<string> acikKatmanlar)
    {
        ArgumentNullException.ThrowIfNull(acikKatmanlar);
        var selected = new HashSet<string>(acikKatmanlar, StringComparer.OrdinalIgnoreCase);
        var kat = new BinaKat {
            DxfFilePath = KaynakYolu, UnitWarning = UnitWarning,
            KaynakKatmanlari = Katmanlar.Where(k => selected.Contains(k.Ad)).Select(k => k.Ad).ToList()
        };
        foreach (var oge in _ogeler)
        {
            if (!oge.GerekliKatmanlar.All(selected.Contains)) continue;
            kat.HamSegmentler.AddRange(oge.Segmentler);
            if (oge.TespitIcin) kat.HamTespitSegmentler.AddRange(oge.Segmentler);
            kat.HamMetinler.AddRange(oge.Metinler);
            // Yaylar önizlemede Segmentler üzerinden zaten çizilir. Tespit için
            // çizgilerden elenen tefriş/ölçü yayı sanal kapı duvarı üretemez.
            if (oge.TespitIcin)
                kat.DxfArclar.AddRange(oge.Arclar.Select(a => new BinaDxfArc {
                    Merkez = a.Merkez, R = a.R, BasAciDeg = a.BasAciDeg, BitAciDeg = a.BitAciDeg }));
        }
        // Önizlemede küçük geometriler de korunur. Üret sonrası gösterim sadeleştirilir.
        kat.DxfSegmentler = kat.HamSegmentler.Select(s => new BinaDxfSegment { A = s.A, B = s.B }).ToList();
        return kat;
    }

    internal sealed class KatmanliOge
    {
        public required string[] GerekliKatmanlar { get; init; }
        public bool TespitIcin { get; init; }
        public List<(Point2D A, Point2D B)> Segmentler { get; } = new();
        public List<(Point2D Pos, string Value)> Metinler { get; } = new();
        public List<BinaDxfArc> Arclar { get; } = new();
    }
}
