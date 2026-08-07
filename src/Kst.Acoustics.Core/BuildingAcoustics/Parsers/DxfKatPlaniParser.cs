using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;
using BinaServices = Kst.Acoustics.Core.Bina.Services;
using BinaModels = Kst.Acoustics.Core.Bina.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Parsers;

/// Gerçek DXF parse mantığı Core/Bina/Services/DxfKatPlaniParser'a taşındı.
/// Mevcut M08 çağrıcılar için BinaKat ↔ Kat dönüştürücü delegasyon.
public class DxfKatPlaniParser
{
    private readonly BinaServices.DxfKatPlaniParser _bina = new();

    public double UnitlessScaleMeters
    {
        get => _bina.UnitlessScaleMeters;
        set => _bina.UnitlessScaleMeters = value;
    }
    public string? UnitWarning => _bina.UnitWarning;

    public Kat Parse(string filePath, int katNo, double katYuksekligi, bool normalizeOrigin = true)
    {
        var binaKat = _bina.Parse(filePath, katNo, katYuksekligi, normalizeOrigin);
        return ToKat(binaKat);
    }

    public List<(Point2D A, Point2D B)> ExtractRawSegmentsFromFile(string filePath)
        => _bina.ExtractRawSegmentsFromFile(filePath);

    // ── Statik yardımcılar (M08 ViewModel tarafından doğrudan çağrılır) ───────

    public static void NormalizeOrigin(Kat kat)
    {
        var polPts = kat.Mekanlar.SelectMany(m => m.Poligon).ToList();
        if (polPts.Count == 0) return;

        var minX = polPts.Min(p => p.X);
        var minY = polPts.Min(p => p.Y);
        if (Math.Abs(minX) < 1e-6 && Math.Abs(minY) < 1e-6) return;

        // Yerel koordinatları sıfıra taşırken dünya koordinatını koru. Özellikle M50'den
        // gelen katlarda OriginOffset bina hizalamasının parçasıdır; yalnız yerel
        // noktaları kaydırmak katlar arası örtüşme/darbe yollarını bozardı.
        kat.OriginOffset = new Point2D(
            kat.OriginOffset.X + minX,
            kat.OriginOffset.Y + minY);

        foreach (var mekan in kat.Mekanlar)
        {
            mekan.Poligon = mekan.Poligon
                .Select(p => new Point2D(p.X - minX, p.Y - minY))
                .ToList();

            foreach (var bolme in mekan.Bolmeler)
            {
                bolme.Baslangic = new Point2D(bolme.Baslangic.X - minX, bolme.Baslangic.Y - minY);
                bolme.Bitis     = new Point2D(bolme.Bitis.X     - minX, bolme.Bitis.Y     - minY);

                foreach (var aciklik in bolme.Acikliklar)
                {
                    aciklik.Baslangic = new Point2D(aciklik.Baslangic.X - minX, aciklik.Baslangic.Y - minY);
                    aciklik.Bitis     = new Point2D(aciklik.Bitis.X     - minX, aciklik.Bitis.Y     - minY);
                }
            }
        }

        kat.RawDxfSegments = kat.RawDxfSegments
            .Select(s => (new Point2D(s.A.X - minX, s.A.Y - minY),
                          new Point2D(s.B.X - minX, s.B.Y - minY)))
            .ToList();

        kat.RawDxfTexts = kat.RawDxfTexts
            .Select(t => (new Point2D(t.Pos.X - minX, t.Pos.Y - minY), t.Value))
            .ToList();

        kat.RawDxfArcs = kat.RawDxfArcs
            .Select(a => (new Point2D(a.Merkez.X - minX, a.Merkez.Y - minY), a.R, a.BasDeg, a.BitDeg))
            .ToList();
    }

    public static int MekanlariBosluklardanYenidenTara(Kat kat)
    {
        var binaKat = new BinaModels.BinaKat
        {
            KatYuksekligiM = kat.KatYuksekligi,
            HamSegmentler  = kat.RawDxfSegments,
            HamMetinler    = kat.RawDxfTexts,
            DxfArclar      = kat.RawDxfArcs
                .Select(a => new BinaModels.BinaDxfArc
                    { Merkez = a.Merkez, R = a.R, BasAciDeg = a.BasDeg, BitAciDeg = a.BitDeg })
                .ToList()
        };

        int bulunan = BinaServices.DxfKatPlaniParser.OdalariBosluklardanYenidenTara(binaKat);
        if (bulunan == 0) return 0;

        kat.Mekanlar = new ObservableCollection<Mekan>(binaKat.Odalar.Select(oda =>
            OdaToMekan(oda, kat.KatYuksekligi)));
        return bulunan;
    }

    public static MekanSinifi SiniflandirMekan(string ad)
    {
        if (string.IsNullOrWhiteSpace(ad)) return MekanSinifi.Tanimsiz;
        var u = ad.ToUpperInvariant();
        if (u.Contains("BALKON") || u.Contains("TERAS")) return MekanSinifi.HesapDisi;
        if (AliciKeywords.Any(k => u.Contains(k))) return MekanSinifi.Alici;
        if (KaynakKeywords.Any(k => u.Contains(k))) return MekanSinifi.Kaynak;
        if (GecisKeywords.Any(k => u.Contains(k))) return MekanSinifi.Gecis;
        return MekanSinifi.Tanimsiz;
    }

    // ── Dönüşüm yardımcıları ─────────────────────────────────────────────────

    private static Kat ToKat(BinaModels.BinaKat binaKat)
    {
        var kat = new Kat
        {
            KatNo          = binaKat.KatNo,
            Ad             = binaKat.Ad,
            KatYuksekligi  = binaKat.KatYuksekligiM,
            DxfFilePath    = binaKat.DxfFilePath,
            DxfGoster      = binaKat.DxfGoster,
            BloklariGoster = binaKat.BloklariGoster,
            OriginOffset   = binaKat.OriginOffset,
            RawDxfSegments = binaKat.HamSegmentler,
            RawDxfTexts    = binaKat.HamMetinler,
            RawDxfArcs     = binaKat.DxfArclar
                .Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg))
                .ToList()
        };

        foreach (var oda in binaKat.Odalar)
        {
            var mekan = OdaToMekan(oda, binaKat.KatYuksekligiM);
            MapAcikliklar(mekan, binaKat.Acikliklar, binaKat.KatYuksekligiM);
            kat.Mekanlar.Add(mekan);
        }

        return kat;
    }

    private static Mekan OdaToMekan(BinaModels.BinaOda oda, double katYuksekligi)
    {
        var mekan = new Mekan
        {
            Ad       = oda.Ad,
            Alan     = oda.AlanM2,
            Yukseklik = katYuksekligi,
            Poligon  = oda.Poligon.Select(p => new Point2D(p.X, p.Y)).ToList()
        };
        mekan.Sinif = SiniflandirMekan(mekan.Ad);
        AddWallPartitions(mekan, katYuksekligi);
        return mekan;
    }

    private static void AddWallPartitions(Mekan mekan, double katYuksekligi)
    {
        for (int i = 0; i < mekan.Poligon.Count; i++)
        {
            var start = mekan.Poligon[i];
            var end   = mekan.Poligon[(i + 1) % mekan.Poligon.Count];
            double dx = end.X - start.X, dy = end.Y - start.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            mekan.Bolmeler.Add(new Bolme
            {
                Ad          = "Duvar",
                Alan        = len * katYuksekligi,
                KaynakMekan = mekan,
                Baslangic   = start,
                Bitis       = end,
                HasGeometry = true
            });
        }
    }

    /// BinaAciklik listesinden her mekanın en yakın Bolme'sine BolmeAcikligi ekler.
    private static void MapAcikliklar(
        Mekan mekan,
        IReadOnlyList<BinaModels.BinaAciklik> acikliklar,
        double katYuksekligi)
    {
        foreach (var a in acikliklar)
        {
            if (a.IptalEdildi) continue;
            var orta = new Point2D((a.Bas.X + a.Bit.X) * 0.5, (a.Bas.Y + a.Bit.Y) * 0.5);
            if (!PoligonYardimci.IsPointInside(orta, mekan.Poligon)) continue;

            var hedef = mekan.Bolmeler
                .Where(b => b.HasGeometry)
                .Select(b => new { Bolme = b, D = PoligonYardimci.SegmentMesafesi(orta, b.Baslangic, b.Bitis) })
                .OrderBy(x => x.D)
                .FirstOrDefault();

            if (hedef == null || hedef.D > 0.20) continue;

            var yukseklik = a.YukseklikM > 0 ? a.YukseklikM
                : (a.Tip == BinaModels.BinaAciklikTipi.Kapi ? 2.1 : 1.2);

            hedef.Bolme.Acikliklar.Add(new BolmeAcikligi
            {
                AciklikId     = a.Id, // 4.2 — M50 bağı: boyutlar geometriden, salt-okunur
                Tip           = a.Tip == BinaModels.BinaAciklikTipi.Kapi
                                    ? BolmeAcikligiTipi.Kapi
                                    : BolmeAcikligiTipi.Pencere,
                Baslangic     = a.Bas,
                Bitis         = a.Bit,
                Genislik      = a.GenislikM,
                Yukseklik     = yukseklik,
                EsikYuksekligiM = a.EsikYuksekligiM
            });
        }
    }

    // ── Oda sınıfı anahtar kelimeler (M08'e özgü) ────────────────────────────

    private static readonly string[] KaynakKeywords =
        { "SALON", "MUTFAK", "YEMEK", "OTURMA", "HOL", "KAT HOLU", "LIVING", "KITCHEN", "HALL", "DINING" };
    private static readonly string[] AliciKeywords =
        { "YATAK", "ÇOCUK", "EBEVEYN", "ÇALIŞMA", "BEBEK", "BEDROOM", "STUDY", "Ç.", "E." };
    private static readonly string[] GecisKeywords =
        { "KORİDOR", "KORIDOR", "KOR.", "ANTRE", "VESTİBÜL", "VESTIBUL", "MERDİVEN", "MERDIVEN",
          "WC", "BANYO", "BALKON", "ASANSÖR", "ASANSOR", "YANGIN", "DEPO", "Y.M.", "BATH" };
}
