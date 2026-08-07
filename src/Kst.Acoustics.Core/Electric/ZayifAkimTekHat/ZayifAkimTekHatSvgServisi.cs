using System.Globalization;
using System.Text;

namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

/// <summary>
/// Sistem şemalarının SVG çıktısı. DXF servisiyle aynı yerleşim mantığını kullanır;
/// SVG'de Y ekseni zaten yukarıdan aşağı olduğundan eksen çevrimi yoktur.
/// </summary>
public static class ZayifAkimTekHatSvgServisi
{
    public static void DataSvgCiktiAl(ElvDataSistemi data, string hedefDosya)
        => File.WriteAllText(hedefDosya, RackliSistemSvg(data.AnaMerkez, data.RackKabinler,
            r => $"Data:{r.DataHatSayisi} WiFi:{r.WirelessSayisi}"));

    public static void KameraSvgCiktiAl(ElvKameraSistemi kamera, string hedefDosya)
        => File.WriteAllText(hedefDosya, RackliSistemSvg(kamera.AnaMerkez, kamera.RackKabinler,
            r => $"Kamera:{r.KameraSayisi}", kamera.IzlemeNoktalari));

    public static void AnonsSvgCiktiAl(ElvAnonsSistemi anons, string hedefDosya)
    {
        string svg = anons.Teknoloji == "Analog" && anons.AnalogZonlar.Count > 0
            ? AnalogAnonsSvg(anons)
            : RackliSistemSvg(anons.AnaMerkez, anons.RackKabinler, r =>
                anons.Teknoloji == "Dijital (PoE)"
                    ? $"Dijital PoE Ayrı uç:{r.ToplamDijitalAnonsUcu}"
                    : $"Analog Hat:{r.AnonsHatSayisi} Hoparlör:{r.ToplamAnonsHoparlor}");
        File.WriteAllText(hedefDosya, svg);
    }

    public static void TvSvgCiktiAl(ElvTvSistemi tv, string hedefDosya)
    {
        string svg = tv.Teknoloji switch
        {
            TvDagitimTeknolojisi.DirektLnb => DirektLnbSvg(tv),
            TvDagitimTeknolojisi.IpTv => RackliSistemSvg(tv.IpTv.AnaMerkez, tv.IpTv.RackKabinler,
                r => $"IP-TV uç:{r.TvPrizSayisi}"),
            _ => RackliSistemSvg(tv.AnaMerkez, tv.RackKabinler, r => $"TV priz:{r.TvPrizSayisi}")
        };
        File.WriteAllText(hedefDosya, svg);
    }

    private static string DirektLnbSvg(ElvTvSistemi tv)
    {
        double h = Math.Max(220, 60 + tv.DirektLnb.Hatlar.Count * 52);
        var sb = SvgBaslat(780, h);
        Kutu(sb, 24, 65, 220, 80, "#30270B", "#D6B336");
        Yazi(sb, $"{tv.DirektLnb.CanakAdi} / {tv.DirektLnb.LnbTipi}", 34, 87, 12, "#FFF0B5", true);
        Yazi(sb, $"Çıkış:{tv.DirektLnb.GerekliCikis}/{tv.DirektLnb.LnbCikisSayisi * Math.Max(1, tv.DirektLnb.LnbSayisi)}", 34, 112, 10, "#E9D578");
        double y = 35;
        foreach (var hat in tv.DirektLnb.Hatlar)
        {
            Kutu(sb, 470, y, 260, 38, "#26200D", "#B89B32");
            Yazi(sb, hat.Ad, 478, y + 15, 11, "#FFF1BF", true);
            Yazi(sb, $"LNB:{hat.LnbCikisNo} {hat.KabloTipi} {hat.Metraj:0.#} m", 478, y + 31, 9, "#D8C778");
            double hy = y + 19, mid = 340 + hat.HatNo * 5;
            Cizgi(sb, [(244, 105), (mid, 105), (mid, hy), (470, hy)], "#D8B842", 1.6);
            y += 52;
        }
        return SvgBitir(sb);
    }

    public static void YanginSvgCiktiAl(ElvYanginSistemi yangin, string hedefDosya)
        => File.WriteAllText(hedefDosya, YanginSvg(yangin));

    public static void TelefonSvgCiktiAl(ElvTelefonSistemi telefon, string hedefDosya)
        => File.WriteAllText(hedefDosya, RackliSistemSvg(telefon.AnaMerkez, telefon.RackKabinler,
            r => $"Telefon priz:{r.TelefonPrizSayisi}"));

    public static void DiafonSvgCiktiAl(ElvDiafonSistemi diafon, string hedefDosya)
        => File.WriteAllText(hedefDosya, RackliSistemSvg(diafon.AnaMerkez, diafon.RackKabinler,
            r => $"Diafon ünite:{r.DiafonUniteSayisi}"));

    // ── Rack tabanlı sistemler (Data/Kamera/Anons-rack/TV) ─────────────────

    private static string RackliSistemSvg(
        ElvMerkez anaMerkez, IReadOnlyList<ElvRackKabin> rackKabinler, Func<ElvRackKabin, string> rackOzet,
        IReadOnlyList<ElvCctvIzlemeNoktasi>? izlemeNoktalari = null)
    {
        const double rackW = 240, rackX = 430, satirH = 16, rackBaseH = 46, rackBosluk = 18;
        const double merkezW = 200, merkezX = 24;

        double RackH(ElvRackKabin r) => rackBaseH + Math.Max(1, r.Cihazlar.Count) * satirH;

        double topH = 30;
        foreach (var r in rackKabinler) topH += RackH(r) + rackBosluk;
        if (izlemeNoktalari is { Count: > 0 })
            topH = Math.Max(topH, 30 + izlemeNoktalari.Sum(n => rackBaseH + Math.Max(1, n.Cihazlar.Count) * satirH + rackBosluk));
        double merkezH = rackBaseH + Math.Max(1, anaMerkez.Cihazlar.Count) * satirH + 10;
        topH = Math.Max(topH, merkezH + 60);

        var sb = SvgBaslat(izlemeNoktalari is { Count: > 0 } ? rackX + rackW + 420 : rackX + rackW + 40, topH + 20);

        var rackAnchor = new Dictionary<string, (double x, double y)>();
        var rackBaglanti = new List<(double x, double y)>();
        double yy = 30;
        foreach (var r in rackKabinler)
        {
            double h = RackH(r);
            Kutu(sb, rackX, yy, rackW, h, "#0E2614", "#2E7D46");
            Yazi(sb, r.Ad, rackX + 8, yy + 15, 12, "#CFF0D8", bold: true);
            Yazi(sb, $"{r.KatAdi}  {rackOzet(r)}", rackX + 8, yy + 32, 10, "#A8D8B8");
            double cy = yy + rackBaseH + 10;
            foreach (var c in r.Cihazlar)
            {
                Yazi(sb, $"• {c.Ad} ×{c.Adet}", rackX + 12, cy, 10, "#9CCFAC");
                cy += satirH;
            }
            var anchor = (rackX, yy + h / 2);
            rackBaglanti.Add(anchor);
            rackAnchor[r.Id] = anchor;
            yy += h + rackBosluk;
        }

        double merkezY = 30 + Math.Max(0, (topH - 60 - merkezH) / 2);
        Kutu(sb, merkezX, merkezY, merkezW, merkezH, "#13361C", "#3CA85A");
        Yazi(sb, anaMerkez.Ad, merkezX + 8, merkezY + 15, 12, "#DDF6E4", bold: true);
        double my = merkezY + rackBaseH + 10;
        foreach (var c in anaMerkez.Cihazlar)
        {
            Yazi(sb, $"• {c.Ad} ×{c.Adet}", merkezX + 12, my, 10, "#AEE0BE");
            my += satirH;
        }

        double cikisX = merkezX + merkezW;
        double busX = rackX - 16;
        var dogrudanRackIndexleri = rackKabinler
            .Select((r, index) => (r, index))
            .Where(x => x.r.UstRackId is null || !rackAnchor.ContainsKey(x.r.UstRackId))
            .Select((x, sira) => (x.index, sira))
            .ToDictionary(x => x.index, x => x.sira);
        int dogrudanAdet = dogrudanRackIndexleri.Count;
        for (int i = 0; i < rackBaglanti.Count; i++)
        {
            var (rx, ry) = rackBaglanti[i];
            var r = rackKabinler[i];
            if (r.UstRackId != null && rackAnchor.TryGetValue(r.UstRackId, out var pp))
            {
                Cizgi(sb, [(rx, ry), (busX, ry), (busX, pp.y), (pp.x, pp.y)], "#F0A83C", 2.2);
                Yazi(sb, $"{r.MerkezeKabloTipi} {r.MerkezeKabloMetraj:0.#} m", busX - 2, (ry + pp.y) / 2, 9, "#F6CE86");
            }
            else
            {
                int sira = dogrudanRackIndexleri[i];
                double cikisY = merkezY + 8 + (sira + 1) * Math.Max(6, merkezH - 16) / (dogrudanAdet + 1);
                double midX = cikisX + 18 + (sira + 1) * (rx - cikisX - 36) / (dogrudanAdet + 1);
                Cizgi(sb, [(cikisX, cikisY), (midX, cikisY), (midX, ry), (rx, ry)], "#4CAF50", 1.6);
                Yazi(sb, $"{r.MerkezeKabloTipi} {r.MerkezeKabloMetraj:0.#} m", midX + 4, ry - 6, 9, "#B8E0C0");
            }
        }

        if (izlemeNoktalari is { Count: > 0 })
        {
            const double izlemeX = rackX + rackW + 150, izlemeW = 230;
            double iy = 30;
            for (int i = 0; i < izlemeNoktalari.Count; i++)
            {
                var nokta = izlemeNoktalari[i];
                double h = rackBaseH + Math.Max(1, nokta.Cihazlar.Count) * satirH;
                Kutu(sb, izlemeX, iy, izlemeW, h, "#102636", "#3A9BC8");
                Yazi(sb, nokta.Ad, izlemeX + 8, iy + 15, 12, "#D5F2FF", bold: true);
                Yazi(sb, nokta.KatAdi, izlemeX + 8, iy + 32, 10, "#75BEDD");
                double cy = iy + rackBaseH + 10;
                foreach (var c in nokta.Cihazlar)
                {
                    Yazi(sb, $"• {c.Ad} ×{c.Adet}", izlemeX + 12, cy, 10, "#A8DCEC");
                    cy += satirH;
                }
                (double x, double y) kaynak = nokta.BagliRackId is not null && rackAnchor.TryGetValue(nokta.BagliRackId, out var rackSol)
                    ? (rackX + rackW, rackSol.y)
                    : (merkezX + merkezW, merkezY + 10 + (i + 1) * Math.Max(8, merkezH - 20) / (izlemeNoktalari.Count + 1));
                double hedefY = iy + h / 2;
                double midX = kaynak.x + 24 + (i + 1) * Math.Max(20, izlemeX - kaynak.x - 48) / (izlemeNoktalari.Count + 1);
                Cizgi(sb, [(kaynak.x, kaynak.y), (midX, kaynak.y), (midX, hedefY), (izlemeX, hedefY)], "#4DB6E8", 1.6);
                Yazi(sb, $"{nokta.BaglantiKabloTipi} {nokta.BaglantiKabloMetraj:0.#} m", midX + 4, hedefY - 6, 9, "#8ED8F5");
                iy += h + rackBosluk;
            }
        }

        return SvgBitir(sb);
    }

    // ── Yangın (loop → santral) ─────────────────────────────────────────────

    private static string YanginSvg(ElvYanginSistemi yangin)
    {
        const double merkezX = 24, merkezY = 70, merkezW = 220, merkezH = 76;
        const double loopX = 430, loopW = 260, loopH = 40, loopBosluk = 18;
        double topH = Math.Max(220, 70 + yangin.ToplamLoop * (loopH + loopBosluk) + 40);

        var sb = SvgBaslat(loopX + loopW + 40, topH);

        Kutu(sb, merkezX, merkezY, merkezW, merkezH, "#351414", "#C85050");
        Yazi(sb, yangin.AnaMerkez.Ad, merkezX + 8, merkezY + 17, 12, "#FFE0E0", bold: true);
        Yazi(sb, $"Loop:{yangin.ToplamLoop} Cihaz:{yangin.ToplamCihaz}", merkezX + 8, merkezY + 37, 10, "#F0B0B0");

        int toplamLoop = Math.Max(1, yangin.ToplamLoop);
        int loopIndex = 0;
        double y = 40;
        foreach (var kat in yangin.KatLoopGruplari)
        {
            foreach (var loop in kat.Looplar)
            {
                Kutu(sb, loopX, y, loopW, loopH, "#2A1010", "#C85050");
                Yazi(sb, $"{kat.KatAdi} - {loop.Ad}", loopX + 8, y + 14, 11, "#FFD8D8", bold: true);
                Yazi(sb, $"Cihaz:{loop.CihazSayisi}  {loop.KabloTipi}  {loop.Metraj:0.#} m", loopX + 8, y + 31, 9, "#E8A0A0");

                double ry = y + loopH / 2;
                double cikisY = merkezY + 10 + (loopIndex + 1) * (merkezH - 20) / (toplamLoop + 1);
                var merkezCikis = (x: merkezX + merkezW, y: cikisY);
                double midX = merkezCikis.x + 24
                              + (loopIndex + 1) * (loopX - merkezCikis.x - 48) / (toplamLoop + 1);
                Cizgi(sb, [(merkezCikis.x, merkezCikis.y), (midX, merkezCikis.y), (midX, ry), (loopX, ry)], "#E05050", 1.6);
                y += loopH + loopBosluk;
                loopIndex++;
            }
        }

        return SvgBitir(sb);
    }

    // ── Analog acil anons ───────────────────────────────────────────────────

    private static string AnalogAnonsSvg(ElvAnonsSistemi anons)
    {
        double toplamH = 80;
        foreach (var zon in anons.AnalogZonlar)
            toplamH += Math.Max(80, 55 + (zon.HatA.HoparlorGruplari.Count + (zon.HatB?.HoparlorGruplari.Count ?? 0)) * 18
                + (zon.HatA.KabloSegmentleri.Count + (zon.HatB?.KabloSegmentleri.Count ?? 0)) * 11) + 25;
        toplamH = Math.Max(toplamH, 80 + anons.AnalogAmplifikatorler.Count * 55);

        var sb = SvgBaslat(1180, toplamH);

        Kutu(sb, 20, 30, 180, 60, "#3A2410", "#D28A32");
        Yazi(sb, "ANALOG ACİL ANONS", 28, 50, 11, "#FFE2B8", bold: true);
        Yazi(sb, $"EN 54-16 • Zon:{anons.AnalogZonlar.Count} Amp:{anons.AnalogAmplifikatorler.Count}", 28, 70, 9, "#D8B078");

        double ay = 30;
        foreach (var amp in anons.AnalogAmplifikatorler)
        {
            Kutu(sb, 240, ay, 190, 45, amp.Yedek ? "#30233A" : "#332810", amp.Yedek ? "#B58CD8" : "#D2A33A");
            Yazi(sb, amp.Ad, 248, ay + 15, 10, "#FFF0C8", bold: true);
            Yazi(sb, $"{amp.NominalGucW:0.#} W • {amp.KanalSayisi} kanal{(amp.Yedek ? " • YEDEK" : "")}", 248, ay + 31, 9, "#D8BE88");
            Cizgi(sb, [(200, 60), (240, ay + 22)], "#B98232", 1.4);
            ay += 55;
        }

        double zy = 30;
        foreach (var zon in anons.AnalogZonlar.OrderByDescending(z => z.KatNo))
        {
            double zonH = Math.Max(80, 55 + (zon.HatA.HoparlorGruplari.Count + (zon.HatB?.HoparlorGruplari.Count ?? 0)) * 18);
            Kutu(sb, 480, zy, 190, zonH, "#2D2010", "#E2A33A");
            Yazi(sb, zon.Ad, 488, zy + 15, 10, "#FFE0A8", bold: true);
            Yazi(sb, $"{zon.KatAdi} • {zon.ToplamHoparlor} hop. • {zon.ToplamGucW:0.#} W", 488, zy + 32, 9, "#D8B878");
            Cizgi(sb, [(430, Math.Min(ay, 55)), (480, zy + 25)], "#E2A33A", 1.4);

            double hatY = AnalogHatSvg(sb, zon.HatA, 670, zy + 48, "#63C878");
            if (zon.HatB is not null)
                hatY = AnalogHatSvg(sb, zon.HatB, 670, hatY + 8, "#69AEE8");
            zy += Math.Max(zonH, hatY - zy) + 25;
        }

        return SvgBitir(sb);
    }

    /// <summary>Bir 100 V hattı ve altındaki grup/segment satırlarını çizer; kullanılan son Y'yi döndürür.</summary>
    private static double AnalogHatSvg(StringBuilder sb, ElvAnalogHoparlorHatti hat, double x, double y, string renk)
    {
        Cizgi(sb, [(x, y), (x + 50, y)], renk, 1.6);
        Yazi(sb, $"HAT {hat.Kol} • 100 V • {hat.ToplamGucW:0.#} W • {hat.KabloTipi} {hat.SegmentToplamMetraj:0.#} m",
            x + 55, y + 3, 9, renk, bold: true);
        double gy = y + 18;
        foreach (var grup in hat.HoparlorGruplari)
        {
            Yazi(sb, $"{grup.Adet}x {grup.HoparlorTipi} @{grup.TapGucuW:0.##} W EN54-24:{(grup.En54_24Uygun ? "EVET" : "HAYIR")}",
                x + 70, gy, 8.5, "#D8E8D8");
            gy += 15;
        }
        foreach (var segment in hat.KabloSegmentleri)
        {
            Yazi(sb, $"S{segment.SiraNo} {segment.Baslangic} → {segment.Bitis}: {segment.Metraj:0.#} m",
                x + 90, gy, 7.5, "#A8B8A8");
            gy += 11;
        }
        return gy;
    }

    // ── SVG yardımcıları ────────────────────────────────────────────────────

    private static StringBuilder SvgBaslat(double genislik, double yukseklik)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(genislik)}\" height=\"{F(yukseklik)}\" viewBox=\"0 0 {F(genislik)} {F(yukseklik)}\">");
        sb.AppendLine($"<rect width=\"{F(genislik)}\" height=\"{F(yukseklik)}\" fill=\"#08140C\"/>");
        return sb;
    }

    private static string SvgBitir(StringBuilder sb)
    {
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static void Kutu(StringBuilder sb, double x, double y, double w, double h, string dolgu, string kenar)
        => sb.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" rx=\"6\" fill=\"{dolgu}\" stroke=\"{kenar}\" stroke-width=\"1.6\"/>");

    private static void Cizgi(StringBuilder sb, (double x, double y)[] noktalar, string renk, double kalinlik)
    {
        var puan = string.Join(" ", noktalar.Select(n => $"{F(n.x)},{F(n.y)}"));
        sb.AppendLine($"<polyline points=\"{puan}\" fill=\"none\" stroke=\"{renk}\" stroke-width=\"{F(kalinlik)}\"/>");
    }

    private static void Yazi(StringBuilder sb, string metin, double x, double y, double boy, string renk, bool bold = false)
        => sb.AppendLine($"<text x=\"{F(x)}\" y=\"{F(y)}\" font-family=\"sans-serif\" font-size=\"{F(boy)}\""
                         + $"{(bold ? " font-weight=\"600\"" : "")} fill=\"{renk}\">{Kacir(metin)}</text>");

    private static string F(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Kacir(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
