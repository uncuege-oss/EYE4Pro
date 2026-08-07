using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using netDxf;
using netDxf.Entities;
using netDxf.Units;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// M60 — DXF arazi parser. Bina kat planı parser'ından (DxfKatPlaniParser) AYRIDIR:
/// kapalı oda poligonları yerine **açık eşyükselti eğrilerini** ve **spot kotları** okur.
///
/// Kontur kotu öncelik zinciri:
///   1) 3D polyline köşe Z'si (VertexZ)
///   2) Polyline2D düzlem kotu / Elevation (DuzlemKotu)
///   3) Katman adındaki sayı, örn. "KONTUR_540" (KatmanAdi)
///   4) En yakın TEXT/MTEXT etiketi (MetinEtiketi)
///   5) Çözülemezse Bilinmiyor → kullanıcı elle girer.
///
/// Geometrik koordinatlar (X, Y, Z) `$INSUNITS` ölçeğiyle metreye çevrilir.
/// Katman/etiket kaynaklı kot sayıları metre kabul edilir (geometrik ölçek uygulanmaz).
public class DxfAraziParser
{
    /// InsUnits=Unitless olduğunda varsayılan ölçek (1 birim = 1 m). Arazi DXF'leri
    /// genellikle gerçek koordinatlı olduğundan bina parser'ından farklı olarak metre varsayar.
    public double UnitlessScaleMeters { get; set; } = 1.0;

    /// Etiket eşleştirmede konturun en yakın köşesine izin verilen azami uzaklık (metre).
    public double EtiketEslesmeToleransiM { get; set; } = 50.0;

    private static readonly Regex SayiRegex = new(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

    public AraziImportSonucu Parse(string filePath)
    {
        var dxf = DxfDocument.Load(filePath) ?? throw new Exception("DXF dosyası yüklenemedi.");
        var sonuc = Parse(dxf);
        sonuc.Arazi.DosyaYolu = filePath;
        return sonuc;
    }

    /// Test ve yeniden kullanım için doküman üzerinden ayrıştırma.
    public AraziImportSonucu Parse(DxfDocument dxf)
    {
        var sonuc = new AraziImportSonucu();
        var arazi = sonuc.Arazi;

        bool unitless = dxf.DrawingVariables.InsUnits == DrawingUnits.Unitless;
        double scale = MetrePerBirim(dxf.DrawingVariables.InsUnits);
        if (unitless)
            sonuc.Uyarilar.Add($"DXF InsUnits=Unitless. Varsayılan ölçek: 1 birim = {UnitlessScaleMeters} m kabul edildi.");

        // Etiketler (kot eşleştirme için): metin değeri sayıya çözülebilenler.
        var etiketler = MetinEtiketleriniTopla(dxf, scale);

        // Spot kotlar (POINT) → arazi noktaları.
        foreach (var pt in dxf.Entities.Points)
        {
            arazi.Noktalar.Add(new AraziNoktasi(
                pt.Position.X * scale,
                pt.Position.Y * scale,
                pt.Position.Z * scale,
                AraziVeriKaynagi.SpotKot));
        }

        // Konturlar (açık/kapalı polyline) → eşyükselti eğrileri.
        foreach (var ent in dxf.Entities.All)
        {
            switch (ent)
            {
                case Polyline3D p3:
                    KonturEkle(arazi, p3, scale, etiketler, sonuc);
                    break;
                case Polyline2D p2:
                    KonturEkle(arazi, p2, scale, etiketler, sonuc);
                    break;
            }
        }

        sonuc.KotsuzKonturSayisi = arazi.Konturlar.Count(k => k.KotKaynagi == AraziKotKaynagi.Bilinmiyor);
        if (sonuc.KotsuzKonturSayisi > 0)
            sonuc.Uyarilar.Add($"{sonuc.KotsuzKonturSayisi} konturun kotu çözülemedi; elle girilmeli.");

        if (arazi.Noktalar.Count == 0 && arazi.Konturlar.Count == 0)
            sonuc.Uyarilar.Add("DXF içinde kontur (polyline) veya spot kot (POINT) bulunamadı.");

        return sonuc;
    }

    // ── Kontur üreticiler ─────────────────────────────────────────────────────

    private void KonturEkle(
        AraziModeli arazi, Polyline3D p3, double scale,
        List<(Point2D Pos, double Deger)> etiketler, AraziImportSonucu sonuc)
    {
        var v3 = p3.Vertexes;
        if (v3 == null || v3.Count < 2) return;

        var noktalar = v3.Select(v => new Point2D(v.X * scale, v.Y * scale)).ToList();
        var zler = v3.Select(v => v.Z * scale).ToList();

        var kontur = new AraziKonturu
        {
            Noktalar = noktalar,
            Kapali = p3.IsClosed
        };

        // 1) Köşe Z'si anlamlıysa kullan.
        if (zler.Any(z => Math.Abs(z) > 1e-9))
        {
            kontur.KotM = zler.Average();
            kontur.KotKaynagi = AraziKotKaynagi.VertexZ;
        }
        else
        {
            KotuKatmanVeyaEtiketten(kontur, p3.Layer?.Name, noktalar, etiketler, sonuc);
        }

        arazi.Konturlar.Add(kontur);
    }

    private void KonturEkle(
        AraziModeli arazi, Polyline2D p2, double scale,
        List<(Point2D Pos, double Deger)> etiketler, AraziImportSonucu sonuc)
    {
        var verts = p2.Vertexes;
        if (verts == null || verts.Count < 2) return;

        var noktalar = verts.Select(v => new Point2D(v.Position.X * scale, v.Position.Y * scale)).ToList();

        var kontur = new AraziKonturu
        {
            Noktalar = noktalar,
            Kapali = p2.IsClosed
        };

        // 2) Polyline2D düzlem kotu (Elevation) anlamlıysa kullan.
        double elevation = p2.Elevation * scale;
        if (Math.Abs(elevation) > 1e-9)
        {
            kontur.KotM = elevation;
            kontur.KotKaynagi = AraziKotKaynagi.DuzlemKotu;
        }
        else
        {
            KotuKatmanVeyaEtiketten(kontur, p2.Layer?.Name, noktalar, etiketler, sonuc);
        }

        arazi.Konturlar.Add(kontur);
    }

    // 3) Katman adı, 4) en yakın etiket, 5) bilinmiyor.
    private void KotuKatmanVeyaEtiketten(
        AraziKonturu kontur, string? katmanAdi, List<Point2D> noktalar,
        List<(Point2D Pos, double Deger)> etiketler, AraziImportSonucu sonuc)
    {
        // Katman adı: varsayılan "0" katmanı atlanır; kot 0 (datum dışı) güvenilmez sayılır.
        if (!string.Equals(katmanAdi, "0", StringComparison.Ordinal)
            && TrySayiCoz(katmanAdi, out double katmanKot)
            && Math.Abs(katmanKot) > 1e-9)
        {
            kontur.KotM = katmanKot;
            kontur.KotKaynagi = AraziKotKaynagi.KatmanAdi;
            return;
        }

        var eslesme = EnYakinEtiket(noktalar, etiketler);
        if (eslesme.HasValue)
        {
            kontur.KotM = eslesme.Value;
            kontur.KotKaynagi = AraziKotKaynagi.MetinEtiketi;
            return;
        }

        kontur.KotKaynagi = AraziKotKaynagi.Bilinmiyor;
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    private double? EnYakinEtiket(List<Point2D> noktalar, List<(Point2D Pos, double Deger)> etiketler)
    {
        if (etiketler.Count == 0) return null;

        double enIyiMesafe = double.MaxValue;
        double? enIyiDeger = null;

        foreach (var (pos, deger) in etiketler)
        {
            foreach (var n in noktalar)
            {
                double dx = pos.X - n.X, dy = pos.Y - n.Y;
                double m = dx * dx + dy * dy;
                if (m < enIyiMesafe)
                {
                    enIyiMesafe = m;
                    enIyiDeger = deger;
                }
            }
        }

        if (enIyiDeger.HasValue && Math.Sqrt(enIyiMesafe) <= EtiketEslesmeToleransiM)
            return enIyiDeger;

        return null;
    }

    private List<(Point2D Pos, double Deger)> MetinEtiketleriniTopla(DxfDocument dxf, double scale)
    {
        var list = new List<(Point2D, double)>();

        void Ekle(double x, double y, string? raw)
        {
            if (TrySayiCoz(raw, out double deger))
                list.Add((new Point2D(x * scale, y * scale), deger));
        }

        foreach (var t in dxf.Entities.Texts) Ekle(t.Position.X, t.Position.Y, t.Value);
        foreach (var t in dxf.Entities.MTexts) Ekle(t.Position.X, t.Position.Y, t.Value);

        return list;
    }

    private static bool TrySayiCoz(string? metin, out double deger)
    {
        deger = 0;
        if (string.IsNullOrWhiteSpace(metin)) return false;

        var m = SayiRegex.Match(metin);
        if (!m.Success) return false;

        var s = m.Value.Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out deger);
    }

    private double MetrePerBirim(DrawingUnits units) => units switch
    {
        DrawingUnits.Unitless     => UnitlessScaleMeters,
        DrawingUnits.Millimeters  => 0.001,
        DrawingUnits.Centimeters  => 0.01,
        DrawingUnits.Decimeters   => 0.1,
        DrawingUnits.Meters       => 1.0,
        DrawingUnits.Kilometers   => 1000.0,
        DrawingUnits.Inches       => 0.0254,
        DrawingUnits.Feet         => 0.3048,
        DrawingUnits.Yards        => 0.9144,
        DrawingUnits.Miles        => 1609.344,
        DrawingUnits.USSurveyFeet => 1200.0 / 3937.0,
        _ => UnitlessScaleMeters
    };
}
