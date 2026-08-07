using System;
using System.Globalization;
using System.Text;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Exporting;

/// <summary>
/// Mimari 3D modeli (M35 `PlanTaslagi.Model3D`) bağımlılıksız Wavefront OBJ metnine çevirir.
/// Mahal hacimleri ve duvarlar kutu (box) mesh olarak yazılır; her eleman ayrı grup (`g`).
/// M39 format kararı: IFC/GLB uzun vade; OBJ pratik, taşınabilir ve bağımlılıksız çıktıdır.
/// Koordinatlar model çerçevesinde (X sağ, Y derinlik, Z kot/yukarı) doğrudan yazılır.
/// </summary>
public static class MimariObjIhracatServisi
{
    public static string Olustur(MimariProje? proje, bool duvarlariEkle = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# EYE4PRO mimari OBJ ihracatı");
        sb.Append("# Proje: ").AppendLine(string.IsNullOrWhiteSpace(proje?.ProjeAdi) ? "Mimari Proje" : proje!.ProjeAdi);

        var model = proje?.PlanTaslagi?.Model3D;
        if (model is null)
            return sb.ToString();

        var verteksSayaci = 0;
        foreach (var kat in model.Katlar)
        {
            foreach (var mahal in kat.Mahaller)
            {
                sb.Append("g ").AppendLine(GrupAdi("mahal", mahal.Ad, mahal.Id));
                KutuYaz(sb, ref verteksSayaci, mahal.X, mahal.Y, mahal.Z, mahal.GenislikM, mahal.DerinlikM, mahal.YukseklikM);
            }

            if (!duvarlariEkle)
                continue;

            foreach (var duvar in kat.Duvarlar)
            {
                sb.Append("g ").AppendLine(GrupAdi("duvar", "", duvar.Id));
                DuvarKutusuYaz(sb, ref verteksSayaci, duvar);
            }
        }

        return sb.ToString();
    }

    /// <summary>Eksen hizalı kutu: 8 köşe + 6 dörtgen yüz (OBJ 1-tabanlı, global indeks).</summary>
    private static void KutuYaz(
        StringBuilder sb, ref int taban,
        double x, double y, double z, double g, double d, double h)
    {
        g = Math.Max(0, g);
        d = Math.Max(0, d);
        h = Math.Max(0, h);

        Vertex(sb, x, y, z);
        Vertex(sb, x + g, y, z);
        Vertex(sb, x + g, y + d, z);
        Vertex(sb, x, y + d, z);
        Vertex(sb, x, y, z + h);
        Vertex(sb, x + g, y, z + h);
        Vertex(sb, x + g, y + d, z + h);
        Vertex(sb, x, y + d, z + h);

        var b = taban;
        Yuz(sb, b + 1, b + 2, b + 3, b + 4); // alt
        Yuz(sb, b + 5, b + 6, b + 7, b + 8); // üst
        Yuz(sb, b + 1, b + 2, b + 6, b + 5); // ön
        Yuz(sb, b + 2, b + 3, b + 7, b + 6); // sağ
        Yuz(sb, b + 3, b + 4, b + 8, b + 7); // arka
        Yuz(sb, b + 4, b + 1, b + 5, b + 8); // sol
        taban += 8;
    }

    private static void DuvarKutusuYaz(StringBuilder sb, ref int taban, Model3D.Duvar3D duvar)
    {
        // Duvarı doğrultusu boyunca ince bir kutuya indirger (kalınlık enine yayılır).
        var dx = duvar.BitisX - duvar.BaslangicX;
        var dy = duvar.BitisY - duvar.BaslangicY;
        var uzunluk = Math.Sqrt(dx * dx + dy * dy);
        var kalinlik = Math.Max(0.01, duvar.KalinlikM);
        var h = Math.Max(0, duvar.YukseklikM);
        if (uzunluk <= 1e-9)
            return;

        // Birim yön ve dik (normal) vektör.
        var ux = dx / uzunluk;
        var uy = dy / uzunluk;
        var nx = -uy * (kalinlik / 2.0);
        var ny = ux * (kalinlik / 2.0);
        var z = duvar.TabanKotuM;

        var ax = duvar.BaslangicX;
        var ay = duvar.BaslangicY;
        var bx = duvar.BitisX;
        var by = duvar.BitisY;

        Vertex(sb, ax + nx, ay + ny, z);
        Vertex(sb, bx + nx, by + ny, z);
        Vertex(sb, bx - nx, by - ny, z);
        Vertex(sb, ax - nx, ay - ny, z);
        Vertex(sb, ax + nx, ay + ny, z + h);
        Vertex(sb, bx + nx, by + ny, z + h);
        Vertex(sb, bx - nx, by - ny, z + h);
        Vertex(sb, ax - nx, ay - ny, z + h);

        var t = taban;
        Yuz(sb, t + 1, t + 2, t + 3, t + 4);
        Yuz(sb, t + 5, t + 6, t + 7, t + 8);
        Yuz(sb, t + 1, t + 2, t + 6, t + 5);
        Yuz(sb, t + 2, t + 3, t + 7, t + 6);
        Yuz(sb, t + 3, t + 4, t + 8, t + 7);
        Yuz(sb, t + 4, t + 1, t + 5, t + 8);
        taban += 8;
    }

    private static void Vertex(StringBuilder sb, double x, double y, double z)
        => sb.Append("v ")
             .Append(Sayi(x)).Append(' ')
             .Append(Sayi(y)).Append(' ')
             .AppendLine(Sayi(z));

    private static void Yuz(StringBuilder sb, int a, int b, int c, int d)
        => sb.Append("f ").Append(a).Append(' ').Append(b).Append(' ').Append(c).Append(' ').Append(d).AppendLine();

    private static string Sayi(double deger)
        => deger.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GrupAdi(string tur, string ad, string id)
    {
        var taban = string.IsNullOrWhiteSpace(ad) ? $"{tur}-{id}" : $"{tur}-{ad}-{id}";
        var temiz = new StringBuilder(taban.Length);
        foreach (var c in taban)
            temiz.Append(char.IsWhiteSpace(c) ? '_' : c);
        return temiz.ToString();
    }
}
