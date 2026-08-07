using netDxf;
using netDxf.Collections;
using netDxf.Entities;
using netDxf.Objects;
using netDxf.Tables;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public static class ZayifAkimTekHatDxfServisi
{
    public static void DxfCiktiAl(ZayifAkimTekHatProje proje, string hedefDosya)
    {
        var doc = new DxfDocument();

        // DXF katmanları oluştur
        var katmanEleman = new Layer("ZA-ELEMAN") { Color = AciColor.Green };
        var katmanKablo = new Layer("ZA-KABLO") { Color = AciColor.Cyan };
        var katmanMetin = new Layer("ZA-METIN") { Color = AciColor.Default };
        var katmanKat = new Layer("ZA-KAT-CIZGI") { Color = AciColor.DarkGray };

        doc.Layers.Add(katmanEleman);
        doc.Layers.Add(katmanKablo);
        doc.Layers.Add(katmanMetin);
        doc.Layers.Add(katmanKat);

        // Her sistem sayfası yan yana ayrı bir X bölgesine çizilir; üst üste binmez.
        double xOffset = 0;
        foreach (var sayfa in proje.Sayfalar)
        {
            // Hiç yerleşim yapılmamış sayfada bloklar (0,0)'da yığılır; önce otomatik yerleştir.
            if (sayfa.Bloklar.Count > 0 && sayfa.Bloklar.All(b => b.LayoutX == 0 && b.LayoutY == 0))
                ZayifAkimTekHatLayoutServisi.OtomatikYerlestir(sayfa, proje.Katlar);

            DxfSayfaOlustur(doc, sayfa, proje.Katlar, katmanEleman, katmanKablo, katmanMetin, katmanKat, xOffset);
            double sayfaGenislik = sayfa.Bloklar.Count > 0 ? sayfa.Bloklar.Max(b => b.LayoutX) + 200 : 1000;
            xOffset += sayfaGenislik + 200;
        }

        doc.Save(hedefDosya);
    }

    private static void DxfSayfaOlustur(DxfDocument doc, ELVSayfa sayfa, List<ELVKatInfo> katlar,
        Layer layerEleman, Layer layerKablo, Layer layerMetin, Layer katmanKat, double xOffset)
    {
        var (blokW, blokH) = ZayifAkimTekHatLayoutServisi.BlokBoyutu();
        var kabloRenk = HexToAciColor(sayfa.KabloRengi);

        var katlarList = katlar;
        if (katlarList == null || katlarList.Count == 0)
        {
            var inferredKats = sayfa.Bloklar
                .Where(b => b.Kat.HasValue)
                .Select(b => b.Kat ?? 0)
                .Distinct()
                .OrderByDescending(k => k)
                .ToList();
            if (inferredKats.Count == 0)
            {
                inferredKats.Add(0);
            }
            katlarList = inferredKats.Select(k => new ELVKatInfo { KatNo = k, Ad = $"Kat {k}" }).ToList();
        }

        double floorTopMargin = 50;
        double floorHeight = 120;
        double totalHeight = floorTopMargin + katlarList.Count * floorHeight + 20;

        // Local helper to invert Y-axis
        double ToCadY(double screenY) => totalHeight - screenY;

        // Sayfa başlığı — tekst bloğu olarak
        var baslik = new Text(sayfa.SistemAdi, new Vector3(xOffset + 50, ToCadY(25), 0), 15)
        {
            Layer = layerMetin
        };
        doc.Entities.Add(baslik);

        double maxBlockX = sayfa.Bloklar.Count > 0 ? sayfa.Bloklar.Max(b => b.LayoutX) + 200 : 1000;

        for (int floorIdx = 0; floorIdx < katlarList.Count; floorIdx++)
        {
            var katInfo = katlarList[floorIdx];
            double yStart = floorTopMargin + floorIdx * floorHeight;
            double yEnd = yStart + floorHeight;

            double cadFloorBottom = ToCadY(yEnd);
            double cadFloorTop = ToCadY(yStart);
            double cadFloorCenterY = (cadFloorTop + cadFloorBottom) / 2;

            // Kat çizgisini çiz
            var line = new Line(new Vector2(xOffset, cadFloorBottom), new Vector2(xOffset + maxBlockX, cadFloorBottom))
            {
                Layer = katmanKat
            };
            doc.Entities.Add(line);

            // Kat etiketini çiz
            var floorText = new Text(katInfo.Ad, new Vector3(xOffset + 10, cadFloorCenterY - 4, 0), 10)
            {
                Layer = layerMetin,
                Color = AciColor.Yellow
            };
            doc.Entities.Add(floorText);
        }

        // Blokları çiz
        foreach (var blok in sayfa.Bloklar)
        {
            var x = xOffset + blok.LayoutX;
            var y = blok.LayoutY;

            double screenLeft = x;
            double screenRight = x + blokW;
            double screenTop = y;
            double screenBottom = y + blokH;

            double cadLeft = screenLeft;
            double cadRight = screenRight;
            double cadTop = ToCadY(screenTop);
            double cadBottom = ToCadY(screenBottom);

            // Blok dikdörtgeni
            var vertices = new[]
            {
                new Polyline2DVertex(cadLeft, cadBottom),
                new Polyline2DVertex(cadRight, cadBottom),
                new Polyline2DVertex(cadRight, cadTop),
                new Polyline2DVertex(cadLeft, cadTop)
            };

            var rect = new Polyline2D(vertices, true)
            {
                Layer = layerEleman,
                Color = HexToAciColor(GetRenkFromRol(blok.Rol))
            };

            doc.Entities.Add(rect);

            // Blok metni
            string displayLabel = blok.Rol switch
            {
                ELVBlokRol.Merkez => $"M:{blok.ElemanAdi}",
                ELVBlokRol.AraToplayici => $"A:{blok.ElemanAdi}",
                ELVBlokRol.Cihaz => $"{blok.Miktar}x {blok.ElemanAdi}",
                _ => blok.ElemanAdi
            };

            var metin = new Text(displayLabel, new Vector3(cadLeft + 5, (cadTop + cadBottom) / 2 - 4, 0), 8)
            {
                Layer = layerMetin,
                Color = HexToAciColor("#8CDA50")
            };
            doc.Entities.Add(metin);
        }

        // Bağlantıları çiz
        foreach (var ebeveyn in sayfa.Bloklar.Where(b => b.BagliCocukBlokIds.Count > 0))
        {
            foreach (var cocukId in ebeveyn.BagliCocukBlokIds)
            {
                var cocuk = sayfa.Bloklar.FirstOrDefault(b => b.Id == cocukId);
                if (cocuk == null) continue;

                var x1 = xOffset + ebeveyn.LayoutX + blokW / 2;
                var y1 = ebeveyn.LayoutY + blokH;
                var x2 = xOffset + cocuk.LayoutX + blokW / 2;
                var y2 = cocuk.LayoutY;

                // L-şekilli polyline
                double ortaY = (y1 + y2) / 2;
                var baglantiBilesen = new[]
                {
                    new Polyline2DVertex(x1, ToCadY(y1)),
                    new Polyline2DVertex(x1, ToCadY(ortaY)),
                    new Polyline2DVertex(x2, ToCadY(ortaY)),
                    new Polyline2DVertex(x2, ToCadY(y2))
                };

                var baglanti = new Polyline2D(baglantiBilesen, false)
                {
                    Layer = layerKablo,
                    Color = kabloRenk
                };

                doc.Entities.Add(baglanti);
            }
        }
    }

    /// <summary>
    /// Data sistemi (rack kabin → ana merkez) için DXF tek hat şeması üretir.
    /// </summary>
    public static void DataDxfCiktiAl(ElvDataSistemi data, string hedefDosya)
        => RackliSistemDxfCiktiAl(data.AnaMerkez, data.RackKabinler, hedefDosya, "ZA-DATA", DataRackEtiket);

    /// <summary>Data rack DXF etiketi: temel hatlar + varsa ek uç birim sayıları.</summary>
    private static string DataRackEtiket(ElvRackKabin r)
    {
        var sb = new System.Text.StringBuilder($"Data:{r.DataHatSayisi} WiFi:{r.WirelessSayisi}");
        if (r.IpTelefonSayisi > 0) sb.Append($" Tel:{r.IpTelefonSayisi}");
        if (r.YaziciSayisi > 0) sb.Append($" Yzc:{r.YaziciSayisi}");
        if (r.ErisimKontrolSayisi > 0) sb.Append($" Erş:{r.ErisimKontrolSayisi}");
        if (r.DiafonSayisi > 0) sb.Append($" Dfn:{r.DiafonSayisi}");
        return sb.ToString();
    }

    /// <summary>
    /// Kamera sistemi (kat rack/PoE switch → CCTV ana merkez) için DXF tek hat şeması üretir.
    /// </summary>
    public static void KameraDxfCiktiAl(ElvKameraSistemi kamera, string hedefDosya)
        => RackliSistemDxfCiktiAl(kamera.AnaMerkez, kamera.RackKabinler, hedefDosya, "ZA-KAMERA",
            r => $"Kamera:{r.KameraSayisi}", kamera.IzlemeNoktalari);

    /// <summary>
    /// Acil anons sistemi için DXF tek hat şeması üretir.
    /// </summary>
    public static void AnonsDxfCiktiAl(ElvAnonsSistemi anons, string hedefDosya)
    {
        if (anons.Teknoloji == "Analog" && anons.AnalogZonlar.Count > 0)
            AnalogAnonsDxfCiktiAl(anons, hedefDosya);
        else
            RackliSistemDxfCiktiAl(anons.AnaMerkez, anons.RackKabinler, hedefDosya, "ZA-ANONS", r =>
                anons.Teknoloji == "Dijital (PoE)"
                    ? $"Dijital PoE Ayrı uç:{r.ToplamDijitalAnonsUcu}"
                    : $"Analog Hat:{r.AnonsHatSayisi} Hoparlör:{r.ToplamAnonsHoparlor}");
    }

    private static void AnalogAnonsDxfCiktiAl(ElvAnonsSistemi anons, string hedefDosya)
    {
        var doc = new DxfDocument();
        var merkezLayer = new Layer("ZA-ANALOG-MERKEZ") { Color = new AciColor(220, 150, 50) };
        var zonLayer = new Layer("ZA-ANALOG-ZON") { Color = new AciColor(230, 175, 70) };
        var hatALayer = new Layer("ZA-ANALOG-HAT-A") { Color = new AciColor(80, 200, 110) };
        var hatBLayer = new Layer("ZA-ANALOG-HAT-B") { Color = new AciColor(80, 155, 230) };
        var metinLayer = new Layer("ZA-ANALOG-METIN") { Color = AciColor.Default };
        foreach (var layer in new[] { merkezLayer, zonLayer, hatALayer, hatBLayer, metinLayer }) doc.Layers.Add(layer);

        double CY(double y) => -y;
        void Kutu(double x, double y, double w, double h, Layer layer)
        {
            var v = new[]
            {
                new Polyline2DVertex(x, CY(y)), new Polyline2DVertex(x + w, CY(y)),
                new Polyline2DVertex(x + w, CY(y + h)), new Polyline2DVertex(x, CY(y + h))
            };
            doc.Entities.Add(new Polyline2D(v, true) { Layer = layer });
        }
        void Cizgi(double x1, double y1, double x2, double y2, Layer layer)
            => doc.Entities.Add(new Line(new Vector2(x1, CY(y1)), new Vector2(x2, CY(y2))) { Layer = layer });
        void Yazi(string t, double x, double y, double boy = 7)
            => doc.Entities.Add(new Text(t, new Vector3(x, CY(y), 0), boy) { Layer = metinLayer });

        Kutu(20, 30, 180, 60, merkezLayer);
        Yazi("ANALOG ACİL ANONS - EN 54-16 ANA MERKEZ", 28, 50, 8);
        Yazi($"Zon:{anons.AnalogZonlar.Count} Amp:{anons.AnalogAmplifikatorler.Count}", 28, 70, 6);

        double ay = 30;
        foreach (var amp in anons.AnalogAmplifikatorler)
        {
            Kutu(240, ay, 190, 45, merkezLayer);
            Yazi(amp.Ad, 248, ay + 15, 7);
            Yazi($"{amp.NominalGucW:0.#}W {amp.KanalSayisi} kanal{(amp.Yedek ? " YEDEK" : "")}", 248, ay + 31, 6);
            Cizgi(200, 60, 240, ay + 22, merkezLayer);
            ay += 55;
        }

        double zy = 30;
        foreach (var zon in anons.AnalogZonlar.OrderByDescending(z => z.KatNo))
        {
            double zonH = Math.Max(80, 55 + (zon.HatA.HoparlorGruplari.Count + (zon.HatB?.HoparlorGruplari.Count ?? 0)) * 18);
            Kutu(480, zy, 190, zonH, zonLayer);
            Yazi(zon.Ad, 488, zy + 15, 7);
            Yazi($"{zon.KatAdi} {zon.ToplamHoparlor} hop. {zon.ToplamGucW:0.#}W", 488, zy + 32, 6);
            Cizgi(430, Math.Min(ay, 55), 480, zy + 25, zonLayer);
            AnalogHattiCiz(zon.HatA, 670, zy + 48, hatALayer);
            if (zon.HatB is not null)
                AnalogHattiCiz(zon.HatB, 670, zy + 48 + Math.Max(1, zon.HatA.HoparlorGruplari.Count) * 18 + 8, hatBLayer);
            zy += zonH + 25;
        }
        doc.Save(hedefDosya);

        void AnalogHattiCiz(ElvAnalogHoparlorHatti hat, double x, double y, Layer layer)
        {
            Cizgi(x, y, x + 50, y, layer);
            Yazi($"HAT {hat.Kol} 100V {hat.ToplamGucW:0.#}W {hat.KabloTipi} {hat.SegmentToplamMetraj:0.#}m", x + 55, y + 2, 6);
            double gy = y + 15;
            foreach (var grup in hat.HoparlorGruplari)
            {
                Yazi($"{grup.Adet}x {grup.HoparlorTipi} @{grup.TapGucuW:0.##}W EN54-24:{(grup.En54_24Uygun ? "EVET" : "HAYIR")}", x + 70, gy, 5.5);
                gy += 13;
            }
            foreach (var segment in hat.KabloSegmentleri)
            {
                Yazi($"S{segment.SiraNo} {segment.Baslangic} -> {segment.Bitis}: {segment.Metraj:0.#}m", x + 90, gy, 4.5);
                gy += 11;
            }
        }
    }

    /// <summary>
    /// TV sistemi için DXF tek hat şeması üretir.
    /// </summary>
    public static void TvDxfCiktiAl(ElvTvSistemi tv, string hedefDosya)
    {
        if (tv.Teknoloji == TvDagitimTeknolojisi.DirektLnb)
            DirektLnbDxfCiktiAl(tv, hedefDosya);
        else if (tv.Teknoloji == TvDagitimTeknolojisi.IpTv)
            RackliSistemDxfCiktiAl(tv.IpTv.AnaMerkez, tv.IpTv.RackKabinler, hedefDosya,
                "ZA-TV-IP", r => $"IP-TV uç:{r.TvPrizSayisi}");
        else
            RackliSistemDxfCiktiAl(tv.AnaMerkez, tv.RackKabinler, hedefDosya,
                "ZA-TV-MERKEZI", r => $"TV priz:{r.TvPrizSayisi}");
    }

    private static void DirektLnbDxfCiktiAl(ElvTvSistemi tv, string hedefDosya)
    {
        var doc = new DxfDocument();
        var kutu = new Layer("ZA-TV-DIREKT-KUTU") { Color = AciColor.Yellow };
        var kablo = new Layer("ZA-TV-DIREKT-KABLO") { Color = AciColor.Yellow };
        var metin = new Layer("ZA-TV-DIREKT-METIN") { Color = AciColor.Default };
        doc.Layers.Add(kutu); doc.Layers.Add(kablo); doc.Layers.Add(metin);
        double H = Math.Max(220, 60 + tv.DirektLnb.Hatlar.Count * 52);
        double CY(double y) => H - y;
        void Kutu(double x, double y, double w, double h) => doc.Entities.Add(new Polyline2D(
            new Polyline2DVertex[] { new(x, CY(y + h)), new(x + w, CY(y + h)), new(x + w, CY(y)), new(x, CY(y)) }, true) { Layer = kutu });
        void Yazi(string s, double x, double y, double boy) => doc.Entities.Add(new Text(s, new Vector3(x, CY(y + boy), 0), boy) { Layer = metin });
        Kutu(24, 65, 220, 80);
        Yazi($"{tv.DirektLnb.CanakAdi} / {tv.DirektLnb.LnbTipi}", 32, 78, 8);
        Yazi($"Cikis:{tv.DirektLnb.GerekliCikis}/{tv.DirektLnb.LnbCikisSayisi * Math.Max(1, tv.DirektLnb.LnbSayisi)}", 32, 100, 7);
        double y = 35;
        foreach (var hat in tv.DirektLnb.Hatlar)
        {
            Kutu(470, y, 260, 38);
            Yazi(hat.Ad, 478, y + 7, 7);
            Yazi($"LNB:{hat.LnbCikisNo} {hat.KabloTipi} {hat.Metraj:0.#}m", 478, y + 23, 6);
            double hy = y + 19, mid = 340 + hat.HatNo * 5;
            doc.Entities.Add(new Polyline2D(
                new Polyline2DVertex[] { new(244, CY(105)), new(mid, CY(105)), new(mid, CY(hy)), new(470, CY(hy)) }, false) { Layer = kablo });
            y += 52;
        }
        doc.Save(hedefDosya);
    }

    /// <summary>
    /// Telefon sistemi (priz hatları → santral) için DXF tek hat şeması üretir.
    /// </summary>
    public static void TelefonDxfCiktiAl(ElvTelefonSistemi telefon, string hedefDosya)
        => RackliSistemDxfCiktiAl(telefon.AnaMerkez, telefon.RackKabinler, hedefDosya, "ZA-TELEFON",
            r => $"Telefon priz:{r.TelefonPrizSayisi}");

    /// <summary>
    /// Diafon sistemi (kat üniteleri → santral) için DXF tek hat şeması üretir.
    /// </summary>
    public static void DiafonDxfCiktiAl(ElvDiafonSistemi diafon, string hedefDosya)
        => RackliSistemDxfCiktiAl(diafon.AnaMerkez, diafon.RackKabinler, hedefDosya, "ZA-DIAFON",
            r => $"Diafon ünite:{r.DiafonUniteSayisi}");

    /// <summary>
    /// Yangın algılama sistemi için kat panosuz loop → santral DXF tek hat şeması üretir.
    /// </summary>
    public static void YanginDxfCiktiAl(ElvYanginSistemi yangin, string hedefDosya)
    {
        var doc = new DxfDocument();
        var layerKutu = new Layer("ZA-YANGIN-KUTU") { Color = AciColor.Red };
        var layerKablo = new Layer("ZA-YANGIN-KABLO") { Color = AciColor.Red };
        var layerMetin = new Layer("ZA-YANGIN-METIN") { Color = AciColor.Default };
        doc.Layers.Add(layerKutu);
        doc.Layers.Add(layerKablo);
        doc.Layers.Add(layerMetin);

        const double merkezX = 24, merkezY = 70, merkezW = 220, merkezH = 76;
        const double loopX = 430, loopW = 260, loopH = 40, loopBosluk = 18;
        double topH = Math.Max(220, 70 + yangin.ToplamLoop * (loopH + loopBosluk) + 40);
        double ToCad(double y) => topH - y;

        void Kutu(double x, double yTop, double w, double h, string renk)
        {
            var v = new[]
            {
                new Polyline2DVertex(x, ToCad(yTop + h)),
                new Polyline2DVertex(x + w, ToCad(yTop + h)),
                new Polyline2DVertex(x + w, ToCad(yTop)),
                new Polyline2DVertex(x, ToCad(yTop))
            };
            doc.Entities.Add(new Polyline2D(v, true) { Layer = layerKutu, Color = HexToAciColor(renk) });
        }

        void Yazi(string t, double x, double yTop, double boy, string renk)
            => doc.Entities.Add(new Text(t, new Vector3(x, ToCad(yTop + boy), 0), boy)
            { Layer = layerMetin, Color = HexToAciColor(renk) });

        Kutu(merkezX, merkezY, merkezW, merkezH, "#3A1414");
        Yazi(yangin.AnaMerkez.Ad, merkezX + 8, merkezY + 8, 9, "#FFE0E0");
        Yazi($"Loop:{yangin.ToplamLoop} Cihaz:{yangin.ToplamCihaz}", merkezX + 8, merkezY + 28, 7, "#F0B0B0");

        int toplamLoop = Math.Max(1, yangin.ToplamLoop);
        int loopIndex = 0;
        double y = 40;
        foreach (var kat in yangin.KatLoopGruplari)
        {
            foreach (var loop in kat.Looplar)
            {
                Kutu(loopX, y, loopW, loopH, "#2A1010");
                Yazi($"{kat.KatAdi} - {loop.Ad}", loopX + 8, y + 7, 8, "#FFD8D8");
                Yazi($"Cihaz:{loop.CihazSayisi}  {loop.KabloTipi}  {loop.Metraj:0.#}m", loopX + 8, y + 24, 6.5, "#E8A0A0");

                double ry = y + loopH / 2;
                double cikisY = merkezY + 10 + (loopIndex + 1) * (merkezH - 20) / (toplamLoop + 1);
                var merkezCikis = (x: merkezX + merkezW, y: cikisY);
                double midX = merkezCikis.x + 24
                              + (loopIndex + 1) * (loopX - merkezCikis.x - 48) / (toplamLoop + 1);
                var v = new[]
                {
                    new Polyline2DVertex(merkezCikis.x, ToCad(merkezCikis.y)),
                    new Polyline2DVertex(midX, ToCad(merkezCikis.y)),
                    new Polyline2DVertex(midX, ToCad(ry)),
                    new Polyline2DVertex(loopX, ToCad(ry))
                };
                doc.Entities.Add(new Polyline2D(v, false) { Layer = layerKablo });
                y += loopH + loopBosluk;
                loopIndex++;
            }
        }

        doc.Save(hedefDosya);
    }

    private static void RackliSistemDxfCiktiAl(
        ElvMerkez anaMerkez,
        IReadOnlyList<ElvRackKabin> rackKabinler,
        string hedefDosya,
        string katmanPrefix,
        Func<ElvRackKabin, string> rackOzet,
        IReadOnlyList<ElvCctvIzlemeNoktasi>? izlemeNoktalari = null)
    {
        var doc = new DxfDocument();
        var layerKutu = new Layer($"{katmanPrefix}-KUTU") { Color = AciColor.Green };
        var layerKablo = new Layer($"{katmanPrefix}-KABLO") { Color = AciColor.Cyan };
        var layerMetin = new Layer($"{katmanPrefix}-METIN") { Color = AciColor.Default };
        doc.Layers.Add(layerKutu);
        doc.Layers.Add(layerKablo);
        doc.Layers.Add(layerMetin);

        const double rackW = 240, rackX = 430, satirH = 16, rackBaseH = 46, rackBosluk = 18;
        const double merkezW = 200, merkezX = 24;

        double RackH(ElvRackKabin r) => rackBaseH + Math.Max(1, r.Cihazlar.Count) * satirH;

        // Toplam yükseklik (Y eksenini ters çevirmek için)
        double topH = 30;
        foreach (var r in rackKabinler) topH += RackH(r) + rackBosluk;
        if (izlemeNoktalari is { Count: > 0 })
        {
            double izlemeH = 30 + izlemeNoktalari.Sum(n => rackBaseH + Math.Max(1, n.Cihazlar.Count) * satirH + rackBosluk);
            topH = Math.Max(topH, izlemeH);
        }
        double merkezH = rackBaseH + Math.Max(1, anaMerkez.Cihazlar.Count) * satirH + 10;
        topH = Math.Max(topH, merkezH + 60);
        double ToCad(double y) => topH - y;

        void Kutu(double x, double yTop, double w, double h, string renk)
        {
            double cadTop = ToCad(yTop), cadBottom = ToCad(yTop + h);
            var v = new[]
            {
                new Polyline2DVertex(x, cadBottom),
                new Polyline2DVertex(x + w, cadBottom),
                new Polyline2DVertex(x + w, cadTop),
                new Polyline2DVertex(x, cadTop)
            };
            doc.Entities.Add(new Polyline2D(v, true) { Layer = layerKutu, Color = HexToAciColor(renk) });
        }

        void Yazi(string t, double x, double yTop, double boy, string renk)
            => doc.Entities.Add(new Text(t, new Vector3(x, ToCad(yTop + boy), 0), boy)
            { Layer = layerMetin, Color = HexToAciColor(renk) });

        // Rack kutuları
        var rackBaglanti = new List<(double x, double y)>();
        var rackAnchor = new Dictionary<string, (double x, double y)>();
        double yy = 30;
        foreach (var r in rackKabinler)
        {
            double h = RackH(r);
            Kutu(rackX, yy, rackW, h, "#0E2614");
            Yazi(r.Ad, rackX + 8, yy + 6, 9, "#CFF0D8");
            Yazi($"{r.KatAdi}  {rackOzet(r)}", rackX + 8, yy + 22, 7, "#A8D8B8");
            double cy = yy + rackBaseH;
            foreach (var c in r.Cihazlar)
            {
                Yazi($"- {c.Ad} x{c.Adet}", rackX + 12, cy, 7, "#9CCFAC");
                cy += satirH;
            }
            var anchor = (rackX, yy + h / 2);
            rackBaglanti.Add(anchor);
            rackAnchor[r.Id] = anchor;
            yy += h + rackBosluk;
        }

        // Ana merkez kutusu
        double merkezY = 30 + Math.Max(0, (topH - 60 - merkezH) / 2);
        Kutu(merkezX, merkezY, merkezW, merkezH, "#13361C");
        Yazi(anaMerkez.Ad, merkezX + 8, merkezY + 6, 9, "#DDF6E4");
        double my = merkezY + rackBaseH;
        foreach (var c in anaMerkez.Cihazlar)
        {
            Yazi($"- {c.Ad} x{c.Adet}", merkezX + 12, my, 7, "#AEE0BE");
            my += satirH;
        }

        // Kablolar — her rack üst düğümüne (Ana Merkez veya başka rack) + metraj etiketi
        double cikisX = merkezX + merkezW;
        double busX = rackX - 16; // rack→rack bağlantı bus hattı
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
                var v = new[]
                {
                    new Polyline2DVertex(rx, ToCad(ry)),
                    new Polyline2DVertex(busX, ToCad(ry)),
                    new Polyline2DVertex(busX, ToCad(pp.y)),
                    new Polyline2DVertex(pp.x, ToCad(pp.y))
                };
                doc.Entities.Add(new Polyline2D(v, false) { Layer = layerKablo });
                Yazi($"{r.MerkezeKabloTipi} {r.MerkezeKabloMetraj:0.#}m", busX - 2, (ry + pp.y) / 2 - 7, 7, "#AEDBF0");
            }
            else
            {
                int sira = dogrudanRackIndexleri[i];
                double cikisY = merkezY + 8 + (sira + 1) * Math.Max(6, merkezH - 16) / (dogrudanAdet + 1);
                // Canvas ile aynı şekilde her kabloyu ayrı bir düşey koridordan geçir.
                double midX = cikisX + 18 + (sira + 1) * (rx - cikisX - 36) / (dogrudanAdet + 1);
                var v = new[]
                {
                    new Polyline2DVertex(cikisX, ToCad(cikisY)),
                    new Polyline2DVertex(midX, ToCad(cikisY)),
                    new Polyline2DVertex(midX, ToCad(ry)),
                    new Polyline2DVertex(rx, ToCad(ry))
                };
                doc.Entities.Add(new Polyline2D(v, false) { Layer = layerKablo });
                Yazi($"{r.MerkezeKabloTipi} {r.MerkezeKabloMetraj:0.#}m", midX + 4, ry - 14, 7, "#B8E0C0");
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
                Kutu(izlemeX, iy, izlemeW, h, "#102636");
                Yazi(nokta.Ad, izlemeX + 8, iy + 6, 9, "#D5F2FF");
                Yazi(nokta.KatAdi, izlemeX + 8, iy + 22, 7, "#75BEDD");
                double cy = iy + rackBaseH;
                foreach (var c in nokta.Cihazlar)
                {
                    Yazi($"- {c.Ad} x{c.Adet}", izlemeX + 12, cy, 7, "#A8DCEC");
                    cy += satirH;
                }

                (double x, double y) kaynak;
                if (nokta.BagliRackId is not null && rackAnchor.TryGetValue(nokta.BagliRackId, out var rackSol))
                    kaynak = (rackX + rackW, rackSol.y);
                else
                    kaynak = (merkezX + merkezW,
                        merkezY + 10 + (i + 1) * Math.Max(8, merkezH - 20) / (izlemeNoktalari.Count + 1));
                double hedefY = iy + h / 2;
                double midX = kaynak.x + 24 + (i + 1) * Math.Max(20, izlemeX - kaynak.x - 48) / (izlemeNoktalari.Count + 1);
                var v = new[]
                {
                    new Polyline2DVertex(kaynak.x, ToCad(kaynak.y)),
                    new Polyline2DVertex(midX, ToCad(kaynak.y)),
                    new Polyline2DVertex(midX, ToCad(hedefY)),
                    new Polyline2DVertex(izlemeX, ToCad(hedefY))
                };
                doc.Entities.Add(new Polyline2D(v, false) { Layer = layerKablo, Color = AciColor.Cyan });
                Yazi($"{nokta.BaglantiKabloTipi} {nokta.BaglantiKabloMetraj:0.#}m", midX + 4, hedefY - 14, 7, "#8ED8F5");
                iy += h + rackBosluk;
            }
        }

        doc.Save(hedefDosya);
    }

    private static AciColor HexToAciColor(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor) || hexColor[0] != '#')
            return AciColor.Default;

        try
        {
            int argb = int.Parse(hexColor.Substring(1), System.Globalization.NumberStyles.HexNumber);
            int r = (argb >> 16) & 0xFF;
            int g = (argb >> 8) & 0xFF;
            int b = argb & 0xFF;
            return new AciColor(r, g, b);
        }
        catch
        {
            return AciColor.Default;
        }
    }

    private static string GetRenkFromRol(ELVBlokRol rol) => rol switch
    {
        ELVBlokRol.Merkez => "#163A10",
        ELVBlokRol.AraToplayici => "#1A5A20",
        ELVBlokRol.Cihaz => "#0F3A15",
        _ => "#0F2010"
    };
}
