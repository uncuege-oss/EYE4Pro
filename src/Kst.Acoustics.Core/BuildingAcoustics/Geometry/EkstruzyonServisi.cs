using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Geometry;

public class EkstruzyonServisi
{
    /// <summary>
    /// Mekanı 3D yüzeylere çevirir.
    /// B4: Pencere ve kapı açıklıkları duvar yüzeyinden çıkarılır (CSG).
    /// B5: Kat OriginOffset koordinatlara uygulanır.
    /// </summary>
    public List<BinaYuzeyi> Mekani3DyeCevir(Mekan mekan, double tabanZ, Point2D? originOffset = null)
    {
        var yuzeyler = new List<BinaYuzeyi>();
        double tavanZ = tabanZ + mekan.Yukseklik;
        var off = originOffset ?? new Point2D(0, 0);

        foreach (var bolme in mekan.Bolmeler)
        {
            if (!bolme.HasGeometry) continue;

            // B4 — CSG: açıklık yoksa tek panel, varsa yanlar + üst/alt şeritler
            var (duvarPanelleri, aciklikYuzeyleri) = CreateWallPanelsWithOpenings(bolme, tabanZ, tavanZ, off);
            foreach (var panel in duvarPanelleri)
                yuzeyler.Add(new BinaYuzeyi { Tip = "Duvar", IlgiliBolme = bolme, Koseler = panel });
            // B4 — Açıklığın kendisi (cam/ahşap) ayrı yüzey olarak — 3D'de renkli görünür
            foreach (var (aciklikTip, koseler) in aciklikYuzeyleri)
                yuzeyler.Add(new BinaYuzeyi
                {
                    Tip = aciklikTip == BolmeAcikligiTipi.Kapi ? "Kapi" : "Pencere",
                    IlgiliBolme = bolme,
                    Koseler = koseler
                });
        }

        if (mekan.Poligon.Count >= 3)
        {
            yuzeyler.Add(new BinaYuzeyi
            {
                Tip = "Doseme",
                Koseler = mekan.Poligon.Select(p => new Point3D(p.X + off.X, p.Y + off.Y, tabanZ)).ToList()
            });
            yuzeyler.Add(new BinaYuzeyi
            {
                Tip = "Tavan",
                Koseler = mekan.Poligon.Select(p => new Point3D(p.X + off.X, p.Y + off.Y, tavanZ)).ToList()
            });
        }

        return yuzeyler;
    }

    /// B4 — Duvar yüzeyini açıklıklara göre parçalara böler.
    /// Koordinat sistemi: duvar boyunca parametrik (u=0..duvar uzunluğu), dikey (z=taban..tavan).
    /// Döner: (katı duvar panelleri, açıklık yüzeyleri tip+köşe).
    private static (List<List<Point3D>> Panels, List<(BolmeAcikligiTipi, List<Point3D>)> Openings)
        CreateWallPanelsWithOpenings(Bolme bolme, double tabanZ, double tavanZ, Point2D off)
    {
        var panels = new List<List<Point3D>>();
        var openingSurfaces = new List<(BolmeAcikligiTipi, List<Point3D>)>();
        double wallLen = Math.Sqrt(
            Math.Pow(bolme.Bitis.X - bolme.Baslangic.X, 2) +
            Math.Pow(bolme.Bitis.Y - bolme.Baslangic.Y, 2));

        if (wallLen <= 0) return (panels, openingSurfaces);

        // Açıklık yoksa tek dikdörtgen panel
        if (!bolme.Acikliklar.Any())
        {
            panels.Add(WallRect(bolme, off, 0, wallLen, tabanZ, tavanZ));
            return (panels, openingSurfaces);
        }

        double wallH = tavanZ - tabanZ;

        // Açıklıkları duvara göre parametrik pozisyona dönüştür — gerçek Yukseklik ve EsikYuksekligi
        var openings = new List<(double U0, double U1, double Z0, double Z1, BolmeAcikligiTipi Tip)>();
        foreach (var aciklik in bolme.Acikliklar)
        {
            // Açıklığın duvar üzerindeki u konumu: başlangıç noktasına göre projeksiyon
            double u0 = ProjectOntoWall(bolme, aciklik.Baslangic, wallLen);
            double openingH = aciklik.Yukseklik > 0
                ? aciklik.Yukseklik
                : (aciklik.Tip == BolmeAcikligiTipi.Kapi ? 2.10 : 1.20);
            double sillH = aciklik.EtkinEsikYuksekligiM;
            double u1 = Math.Min(wallLen, u0 + aciklik.Genislik);
            // Açıklık üst kenarı tavanı aşmamalı — eşiği gerekirse aşağı çek
            double z0 = tabanZ + Math.Max(0, Math.Min(sillH, wallH - openingH));
            double z1 = Math.Min(tavanZ, z0 + openingH);
            if (u1 > u0 && z1 > z0)
                openings.Add((u0, u1, z0, z1, aciklik.Tip));
        }

        openings.Sort((a, b) => a.U0.CompareTo(b.U0));

        // Her açıklık etrafında kalan katı panelleri üret
        double uCur = 0;
        foreach (var (u0, u1, z0, z1, tip) in openings)
        {
            if (u0 > uCur)
                panels.Add(WallRect(bolme, off, uCur, u0, tabanZ, tavanZ));

            // Açıklığın altı (eşik / denizlik) — kapıda eşik 0 ise atla
            if (z0 > tabanZ + 1e-6)
                panels.Add(WallRect(bolme, off, u0, u1, tabanZ, z0));
            // Açıklığın üstü (lento)
            if (z1 < tavanZ - 1e-6)
                panels.Add(WallRect(bolme, off, u0, u1, z1, tavanZ));

            // B4 — Açıklığın kendisi (delik değil, cam/kapı yüzeyi)
            openingSurfaces.Add((tip, WallRect(bolme, off, u0, u1, z0, z1)));

            uCur = u1;
        }

        if (uCur < wallLen)
            panels.Add(WallRect(bolme, off, uCur, wallLen, tabanZ, tavanZ));

        return (panels, openingSurfaces);
    }

    /// Duvar üzerinde u=u0..u1, z=z0..z1 dikdörtgen 4 köşe noktası
    private static List<Point3D> WallRect(Bolme bolme, Point2D off, double u0, double u1, double z0, double z1)
    {
        double wallLen = Math.Sqrt(
            Math.Pow(bolme.Bitis.X - bolme.Baslangic.X, 2) +
            Math.Pow(bolme.Bitis.Y - bolme.Baslangic.Y, 2));
        if (wallLen <= 0) return new List<Point3D>();

        double dx = (bolme.Bitis.X - bolme.Baslangic.X) / wallLen;
        double dy = (bolme.Bitis.Y - bolme.Baslangic.Y) / wallLen;

        Point3D P(double u, double z) => new(
            bolme.Baslangic.X + off.X + dx * u,
            bolme.Baslangic.Y + off.Y + dy * u,
            z);

        return [P(u0, z0), P(u1, z0), P(u1, z1), P(u0, z1)];
    }

    private static double ProjectOntoWall(Bolme bolme, Point2D p, double wallLen)
    {
        if (wallLen <= 0) return 0;
        double dx = (bolme.Bitis.X - bolme.Baslangic.X) / wallLen;
        double dy = (bolme.Bitis.Y - bolme.Baslangic.Y) / wallLen;
        double t = (p.X - bolme.Baslangic.X) * dx + (p.Y - bolme.Baslangic.Y) * dy;
        return Math.Clamp(t, 0, wallLen);
    }
}

public class BinaYuzeyi
{
    public List<Point3D> Koseler    { get; set; } = new();
    public string         Tip        { get; set; } = "";
    public Bolme?         IlgiliBolme { get; set; }
}
