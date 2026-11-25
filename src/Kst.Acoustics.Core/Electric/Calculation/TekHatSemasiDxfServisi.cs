using System;
using System.Collections.Generic;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// M11 Tek Hat Şeması DXF ihracatı. UI Canvas'ından bağımsız; panoları KAT bantlarına
/// (üstten alta bina sırası) yerleştirir — ana pano alt katta kaldığından şema aşağıdan
/// yukarı okunur. Kat ayrım çizgileri + etiketleri çizer; her pano için giriş şalteri/KAR
/// sembolü, bara, çıkış linyeleri (sigorta + kesit), busbar ve jeneratör/transfer bloğunu
/// ekler. Parent→child bağlantıları yön bağımsızdır (çocuk üst/alt katta olabilir).
/// netDxf ile LINE/CIRCLE/TEXT varlıkları üretir (IEC 60617 tarzı basit semboller).
/// </summary>
public static class TekHatSemasiDxfServisi
{
    // Yerleşim ölçüleri (CAD birimi)
    private const double KutuW = 72;
    private const double KutuH = 26;
    private const double KolonAdim = 150;  // yatay yaprak adımı
    private const double SatirAdim = 165;  // dikey kat/derinlik adımı
    private const double SatirYuksek = 5.8;
    private const double DxfBranchGap = 12.0;
    private const double DxfBusbarFeederGap = 12.0;

    public static void DxfCiktiAl(ElektrikProje proje, string hedefDosya,
        ElektrikKutuphaneServisi? kutuphane = null)
    {
        var doc = new DxfDocument();
        var layPano = Katman(doc, "M11-PANO", AciColor.Green);
        var layKablo = Katman(doc, "M11-KABLO", AciColor.Cyan);
        var laySembol = Katman(doc, "M11-SEMBOL", AciColor.Yellow);
        var layMetin = Katman(doc, "M11-METIN", AciColor.Default);
        var layBusbar = Katman(doc, "M11-BUSBAR", AciColor.Green);
        var layKontrol = Katman(doc, "M11-KONTROL", AciColor.Magenta);
        var layJen = Katman(doc, "M11-JEN", AciColor.Magenta);
        var layUps = Katman(doc, "M11-UPS", AciColor.Cyan);
        var layOg = Katman(doc, "M11-OG", AciColor.Red);
        var layTrafo = Katman(doc, "M11-TRAFO", AciColor.Magenta);
        var layHucre = Katman(doc, "M11-HUCRE", AciColor.Red);

        // 1) X yerleşimi (yaprak tabanlı). Dikey konum sonradan KATA göre atanır.
        var pos = new Dictionary<Pano, (double X, double Y)>();
        double leafX = 0;
        var kokler = proje.TumPanolar
            .Where(p => p.UstPano == null && !TransferPanosuMu(proje, p))
            .ToList();
        foreach (var kok in kokler)
            Yerlestir(kok, 0, proje, pos, ref leafX);

        if (pos.Count == 0)
        {
            // En azından başlık + boş çerçeve üret
            doc.Entities.Add(new Text("Tek Hat Şeması (boş proje)", new Vector3(0, 0, 0), 6) { Layer = layMetin });
            doc.Save(hedefDosya);
            return;
        }

        // 1b) Kat-bazlı dikey yerleşim — katlar üstten alta (bina sırası), ana pano alt katta kalır;
        //     CAD'de Y yukarı olduğundan şema AŞAĞIDAN YUKARI okunur (kanvasla aynı). Her pano kendi
        //     kat bandına, kat içinde besleme derinliğine göre satıra oturur (kardeşler aynı satır).
        static int Derinlik(Pano p) { int d = 0; var x = p; while (x.UstPano != null) { d++; x = x.UstPano; } return d; }
        static string KatAdi(Pano p) => string.IsNullOrWhiteSpace(p.Kat) ? "Z" : p.Kat;
        // Fiziksel kat sırası: Çatı en üstte, sonra pozitif katlar, Zemin, bodrumlar en altta.
        static double KatSiraAnahtari(string k)
        {
            if (k == "Çatı") return 9999;
            if (k == "Z") return 0;
            return int.TryParse(k, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;
        }
        var mevcutKatlar = pos.Keys.Select(KatAdi).Distinct()
            .OrderByDescending(KatSiraAnahtari)   // üst kat → küçük yDown → CAD üstte; ana pano altta kalır
            .ThenBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var katBandUst = new Dictionary<string, double>(); // her katın yDown band üstü
        double yBirikim = 0;
        foreach (var kat in mevcutKatlar)
        {
            var katPanolari = pos.Keys.Where(p => KatAdi(p) == kat).ToList();
            int minDerinlik = katPanolari.Min(Derinlik);
            int maxSatir = katPanolari.Max(p => Derinlik(p) - minDerinlik);
            katBandUst[kat] = yBirikim;
            foreach (var pp in katPanolari)
                pos[pp] = (pos[pp].X, yBirikim + 46 + (Derinlik(pp) - minDerinlik) * SatirAdim);
            yBirikim += 46 + (maxSatir + 1) * SatirAdim + 26;
        }
        double bandToplam = yBirikim;

        double maxY = pos.Values.Max(v => v.Y) + 40;
        double ToCad(double yDown) => maxY - yDown; // ekran-aşağı → CAD-yukarı

        // 2) Başlık
        doc.Entities.Add(new Text(
            $"{proje.ProjeAdi} — Tek Hat Şeması",
            new Vector3(0, ToCad(-SatirAdim * 0.6) + KutuH, 0), 8)
        { Layer = layMetin });

        // 3) Kablolar: parent bazlı grupla; kablo ve busbar çıkışlarını ayrı hatlara ayırır.
        // Aynı üst panodan çıkan farklı busbarlar kendi yatay güzergahını alır.
        var parentGruplari = pos.Keys
            .Where(p => p.UstPano != null && pos.ContainsKey(p.UstPano))
            .GroupBy(p => p.UstPano!);

        // Merkezi UPS dağıtım panosu ana panodan AYRI KOLON ile beslenir → M11-UPS katmanında vurgulanır.
        string? merkeziDagitimAd = proje.Upsler.FirstOrDefault(u => u.Merkezi)?.KritikPanoAd;

        // 3a) Kat ayrım çizgileri + etiketleri (yatay bantlar)
        double katXMin = pos.Values.Min(v => v.X) - 110;
        double katXMax = pos.Values.Max(v => v.X) + 260;
        foreach (var kat in mevcutKatlar)
        {
            double bandCadUst = ToCad(katBandUst[kat]);
            Cizgi(doc, katXMin, bandCadUst, katXMax, bandCadUst, layMetin);
            doc.Entities.Add(new Text($"Kat: {kat}", new Vector3(katXMin + 2, bandCadUst - 9, 0), 4) { Layer = layMetin });
        }
        Cizgi(doc, katXMin, ToCad(bandToplam), katXMax, ToCad(bandToplam), layMetin);

        foreach (var grp in parentGruplari)
        {
            var parent = grp.Key;
            var (upX, upY) = pos[parent];
            double upCadY = ToCad(upY);

            var cocuklar = grp.OrderBy(c => pos[c].X).ToList();
            var kabloluCocuklar = cocuklar
                .Where(c => c.BaglantiTipi != BaglantiTipi.Busbar || string.IsNullOrWhiteSpace(c.BaglandigiBusbarId))
                .ToList();
            var busbarGruplari = cocuklar
                .Where(c => c.BaglantiTipi == BaglantiTipi.Busbar && !string.IsNullOrWhiteSpace(c.BaglandigiBusbarId))
                .GroupBy(c => c.BaglandigiBusbarId!)
                .OrderBy(g => g.Min(c => pos[c].X))
                .ToList();

            // Kablolu çocuklar — yön bağımsız ortogonal bağlantı (çocuk üst veya alt katta olabilir).
            for (int i = 0; i < kabloluCocuklar.Count; i++)
            {
                var pano = kabloluCocuklar[i];
                var (pX, pY) = pos[pano];
                double cCadY = ToCad(pY);
                var panoUps = proje.Upsler.FirstOrDefault(u =>
                    string.Equals(u.KritikPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase));
                // UPS'li çıkışta üst pano hattının hedefi kritik pano değil UPS girişidir.
                // UpsCiz aşağıda UPS çıkışını kritik panoya bağlayarak seri topolojiyi tamamlar.
                double hedefEdge = panoUps == null
                    ? cCadY
                    : cCadY + KutuH / 2.0 + 18 + KutuH;
                int yon = hedefEdge >= upCadY ? 1 : -1;         // hedef yukarıda (+) / aşağıda (−)
                double pEdge = upCadY + yon * (KutuH / 2.0);    // ebeveyn kenarı
                double cEdge = panoUps == null
                    ? cCadY - yon * (KutuH / 2.0)                // çocuk kenarı
                    : hedefEdge;                                 // UPS giriş kenarı
                double dalY = pEdge + yon * (14 + (i + 1) * DxfBranchGap);
                if (yon > 0 && dalY > cEdge - 6) dalY = cEdge - 6;
                if (yon < 0 && dalY < cEdge + 6) dalY = cEdge + 6;

                bool merkeziKolon = pano.Ad == merkeziDagitimAd;
                var dalKatman = panoUps != null ? layUps : merkeziKolon ? layUps : layKablo;
                Cizgi(doc, upX, pEdge, upX, dalY, dalKatman);
                Cizgi(doc, upX, dalY, pX, dalY, dalKatman);
                Cizgi(doc, pX, dalY, pX, cEdge, dalKatman);

                double sembY = (dalY + cEdge) / 2;
                SigortaSembol(doc, pX, sembY, laySembol);
                string dalEtiket = merkeziKolon
                    ? "UPS Kolonu (Ayrı)"
                    : panoUps != null ? $"UPS Girişi • {CikisEtiket(pano)}" : CikisEtiket(pano);
                doc.Entities.Add(new Text(dalEtiket,
                    new Vector3(pX + 5, sembY + 1, 0), 3) { Layer = panoUps != null || merkeziKolon ? layUps : layMetin });
            }

            // Busbar grupları — feeder kolonu çocukların Y aralığını kapsar (yön bağımsız).
            for (int gi = 0; gi < busbarGruplari.Count; gi++)
            {
                var busbarGrubu = busbarGruplari[gi].OrderBy(c => pos[c].X).ToList();
                if (busbarGrubu.Count == 0) continue;

                var busbar = proje.TumBusbarlar.FirstOrDefault(b => b.Id == busbarGrubu[0].BaglandigiBusbarId);
                double feederX = BusbarFeederX(upX, gi, busbarGruplari.Count);
                var cocukCadY = busbarGrubu.Select(c => ToCad(pos[c].Y)).ToList();

                int byon = cocukCadY.Average() >= upCadY ? 1 : -1;
                double pEdgeB = upCadY + byon * (KutuH / 2.0);
                double feedY = pEdgeB + byon * 16;
                Cizgi(doc, upX, pEdgeB, upX, feedY, layBusbar);
                Cizgi(doc, upX, feedY, feederX, feedY, layBusbar);

                double colTop = Math.Max(feedY, cocukCadY.Max());
                double colBot = Math.Min(feedY, cocukCadY.Min());
                Cizgi(doc, feederX, colBot, feederX, colTop, layBusbar);
                doc.Entities.Add(new Text(BusbarDxfMetni(busbar, busbarGrubu[0].BaglandigiBusbarId),
                    new Vector3(feederX + 3, colTop + 4, 0), 2.8) { Layer = layMetin });

                foreach (var pano in busbarGrubu)
                {
                    var (pX, pY) = pos[pano];
                    double cCadY = ToCad(pY);
                    Dikdortgen(doc, feederX - 5, cCadY - 5, feederX + 5, cCadY + 5, layBusbar); // çıkış kutusu
                    double cEdgeX = feederX <= pX ? pX - KutuW / 2.0 : pX + KutuW / 2.0;
                    Cizgi(doc, feederX, cCadY, cEdgeX, cCadY, layBusbar);

                    var kutu = BusbarKutu(proje, pano);
                    int sigA = kutu?.SigortaA > 0 ? kutu.SigortaA : pano.CikisKutusuSigortaA;
                    double sembX = (feederX + cEdgeX) / 2;
                    SigortaSembol(doc, sembX, cCadY, laySembol);
                    string kutuCihaz = pano.CikisKorumaCihaziTipi == "TMŞ" ? " TMŞ" : "";
                    string kutuKa = string.IsNullOrWhiteSpace(pano.CikisKesmeKapasitesi) ? "" : $"  {pano.CikisKesmeKapasitesi}";
                    var kutuEtiket = kutu == null
                        ? $"{sigA}A{kutuCihaz}  {pano.KabloKesiti}{kutuKa}"
                        : $"{sigA}A{kutuCihaz}  {pano.KabloKesiti}  {kutu.PozisyonM:0.#}m{kutuKa}";
                    doc.Entities.Add(new Text(kutuEtiket,
                        new Vector3(sembX - 6, cCadY + 4, 0), 2.6) { Layer = layMetin });
                }
            }
        }

        // 3b) Kontrol panosu → kaynak pano sinyal kabloları. Kanvasta gösterilmez; DXF'te
        // gerçek saha kablosu olarak damar, kesit, tip ve metraj etiketiyle çizilir.
        foreach (var kontrol in pos.Keys.Where(p => p.PanoTipi == PanoTipi.KontrolPanosu))
        {
            var (kontrolX, kontrolY) = pos[kontrol];
            double kontrolCadY = ToCad(kontrolY);
            for (int i = 0; i < kontrol.KontrolSinyalKablolari.Count; i++)
            {
                var kablo = kontrol.KontrolSinyalKablolari[i];
                var kaynak = pos.Keys.FirstOrDefault(p =>
                    string.Equals(p.Ad, kablo.KaynakPanoAd, StringComparison.OrdinalIgnoreCase));
                if (kaynak == null || !pos.TryGetValue(kaynak, out var kaynakPos)) continue;

                double kaynakCadY = ToCad(kaynakPos.Y);
                int yon = kaynakPos.X >= kontrolX ? 1 : -1;
                double kontrolKenarX = kontrolX + yon * KutuW / 2.0;
                double hatX = kontrolKenarX + yon * (18 + i * 10);
                double kaynakKenarX = hatX <= kaynakPos.X
                    ? kaynakPos.X - KutuW / 2.0
                    : kaynakPos.X + KutuW / 2.0;

                Cizgi(doc, kontrolKenarX, kontrolCadY, hatX, kontrolCadY, layKontrol);
                Cizgi(doc, hatX, kontrolCadY, hatX, kaynakCadY, layKontrol);
                Cizgi(doc, hatX, kaynakCadY, kaynakKenarX, kaynakCadY, layKontrol);

                doc.Entities.Add(new Text(KontrolSinyalKabloMetni(kablo),
                    new Vector3(hatX + 3, (kontrolCadY + kaynakCadY) / 2.0, 0), 2.6)
                { Layer = layKontrol });
            }
        }

        // 4) Panolar + giriş sembolleri + çıkış linyeleri
        foreach (var (pano, p) in pos)
            PanoCiz(doc, proje, pano, p.X, ToCad(p.Y), layPano, laySembol, layMetin, layKablo);

        // 4.5) UPS sembolleri — kritik panoların üstüne UPS + akü
        foreach (var ups in proje.Upsler)
        {
            var kritik = proje.TumPanolar.FirstOrDefault(p => p.Ad == ups.KritikPanoAd);
            if (kritik != null && pos.TryGetValue(kritik, out var kp))
                UpsCiz(doc, ups, kp.X, ToCad(kp.Y), layUps, layMetin, kutuphane);
        }

        // 5) Jeneratör + senkron/transfer (varsa, hedef pano sağına). Hedef pano adı eski/boş
        // kalmış projelerde hesap servisindeki fallback ile aynı hedefe çiz; sessizce atlama.
        var aktifJeneratorGruplari = AktifJeneratorGruplari(proje);
        if (kutuphane != null)
        {
            foreach (var jenGrup in aktifJeneratorGruplari)
            {
                var hedef = JeneratorHedefPanoBul(proje, jenGrup, pos);
                if (hedef != null)
                    JeneratorHesapServisi.Hesapla(jenGrup, hedef, proje, kutuphane);
            }
        }

        for (int i = 0; i < aktifJeneratorGruplari.Count; i++)
        {
            var jenGrup = aktifJeneratorGruplari[i];
            var hedef = JeneratorHedefPanoBul(proje, jenGrup, pos);
            if (hedef != null && pos.TryGetValue(hedef, out var ap))
            {
                // Jeneratör katı verilmişse o kat bandının CAD-Y'sine yerleştir; yoksa hedef pano seviyesi.
                double jenCadY = ToCad(ap.Y);
                string jk = (jenGrup.Kat ?? "").Trim();
                if (!string.IsNullOrEmpty(jk) && katBandUst.TryGetValue(jk, out var jbUst))
                    jenCadY = ToCad(jbUst + 46);
                JeneratorCiz(doc, proje, jenGrup, hedef, ap.X, ToCad(ap.Y), jenCadY, pos, i, layJen, laySembol, layMetin, kutuphane);
            }
        }

        // 6) Orta gerilim (şebeke → OG hücreleri → trafo → AG bağlantı) — ana panonun üstüne
        var og = proje.OrtaGerilim;
        if (og != null)
        {
            var anaOg = proje.TumPanolar.FirstOrDefault(p => p.Ad == og.AnaPanoAd)
                        ?? proje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano && pos.ContainsKey(p))
                        ?? proje.TumPanolar.FirstOrDefault(p => p.UstPano == null && pos.ContainsKey(p));
            if (anaOg != null && pos.TryGetValue(anaOg, out var op))
                OrtaGerilimCiz(doc, og, op.X, ToCad(op.Y), anaOg.GirisSalteriA, layOg, layTrafo, layHucre, laySembol, layMetin);

            // Köşk fiziksel yerleşimi (plan) — ana çizimin soluna ayrı blok (kütüphane verilmişse)
            if (kutuphane != null)
            {
                double minX = pos.Values.Min(v => v.X);
                KoskBlokCiz(doc, og, kutuphane, minX - 160, ToCad(0), layHucre, layTrafo, layMetin);
            }
        }

        // 7) Pano metraj özeti — şemanın sağına ayrı tablo bloğu. Duvar tipi standart ön-yüz alanına
        //    (0,30 m²), dikili tip derinliğe (90 cm) göre tek satırda toplanır (Excel metrajıyla aynı).
        PanoMetrajOzetiCiz(doc, proje, katXMax + 40, maxY, layPano, layMetin);

        doc.Save(hedefDosya);
    }

    /// <summary>Şemanın sağına pano metraj özeti tablosu çizer: duvar tipi standart alana,
    /// dikili tip derinliğe göre tek satırda toplanmış adetler.</summary>
    private static void PanoMetrajOzetiCiz(DxfDocument doc, ElektrikProje proje,
        double x, double yTop, Layer layPano, Layer layMetin)
    {
        var gruplar = proje.TumPanolar
            .GroupBy(PanoBoyutlandirmaServisi.MetrajAnahtari)
            .OrderBy(g => g.Key.Govde).ThenBy(g => g.Key.StandartAlanM2).ThenBy(g => g.Key.DerinlikCm)
            .ToList();
        if (gruplar.Count == 0) return;

        const double satirY = 11;
        const double genislik = 165;
        double y = yTop;

        doc.Entities.Add(new Text("PANO METRAJ ÖZETİ", new Vector3(x + 3, y, 0), 5) { Layer = layMetin });
        y -= satirY * 1.4;

        foreach (var g in gruplar)
        {
            string etiket = PanoBoyutlandirmaServisi.MetrajEtiketi(g.First());
            doc.Entities.Add(new Text($"{g.Count()} adet    {etiket}",
                new Vector3(x + 3, y, 0), 3.4) { Layer = layMetin });
            y -= satirY;
        }

        Dikdortgen(doc, x, y - 2, x + genislik, yTop + 9, layPano);
    }

    // ── Yerleşim ──
    private static (double Sol, double Sag) Yerlestir(Pano pano, int derinlik, ElektrikProje proje,
        Dictionary<Pano, (double, double)> pos, ref double leafX)
    {
        var cocuklar = pano.AltPanolar.Where(c => !TransferPanosuMu(proje, c)).ToList();
        double y = derinlik * SatirAdim;
        double kendiPayi = PanoYatayPay(pano);
        if (cocuklar.Count == 0)
        {
            double yaprakSol = leafX;
            double yaprakSag = leafX + kendiPayi;
            pos[pano] = ((yaprakSol + yaprakSag) / 2, y);
            leafX = yaprakSag;
            return (yaprakSol, yaprakSag);
        }
        double solCocuk = double.PositiveInfinity;
        double sagCocuk = double.NegativeInfinity;
        foreach (var c in cocuklar)
        {
            var aralik = Yerlestir(c, derinlik + 1, proje, pos, ref leafX);
            solCocuk = Math.Min(solCocuk, aralik.Sol);
            sagCocuk = Math.Max(sagCocuk, aralik.Sag);
        }
        double ortX = cocuklar.Where(pos.ContainsKey).Average(c => pos[c].Item1);
        pos[pano] = (ortX, y);
        double sol = Math.Min(solCocuk, ortX - kendiPayi / 2);
        double sag = Math.Max(sagCocuk, ortX + kendiPayi / 2);
        leafX = Math.Max(leafX, sag);
        return (sol, sag);
    }

    // ── Pano kutusu + giriş sembolleri + çıkış linyeleri ──
    private static void PanoCiz(DxfDocument doc, ElektrikProje proje, Pano pano, double cx, double cy,
        Layer layPano, Layer laySembol, Layer layMetin, Layer layKablo)
    {
        double sol = cx - KutuW / 2, sag = cx + KutuW / 2;
        double ust = cy + KutuH / 2, alt = cy - KutuH / 2;
        Dikdortgen(doc, sol, alt, sag, ust, layPano);

        bool komp = pano.PanoTipi == PanoTipi.Kompanzasyon;
        doc.Entities.Add(new Text(pano.Ad, new Vector3(sol + 3, cy + 3, 0), 4) { Layer = layMetin });
        // Kompanzasyon panosu aktif güç taşımaz → kVAr göster (kW yerine).
        string gucEtiket = komp
            ? $"{pano.ToplamReaktifVAR / 1000.0:N1}kVAr"
            : $"{pano.ToplamGucW / 1000.0:N1}kW";
        doc.Entities.Add(new Text(
            $"{gucEtiket}  {pano.GirisSigortaA}A  Ik={pano.KisaDevreAkimiKA:N1}kA",
            new Vector3(sol + 3, cy - 6, 0), 2.6) { Layer = layMetin });

        // Giriş şalteri (kutu üstünde) + KAR
        if (pano.UstPano != null || pano.SayacEkle)
        {
            SalterSembol(doc, cx, ust + 6, laySembol);
            string kar = KarDxfEtiketi(pano);
            if (kar.Length > 0)
                doc.Entities.Add(new Text(kar, new Vector3(cx + 6, ust + 5, 0), 2.6) { Layer = layMetin });
        }

        // Yangın panosu: ana şalter ÖNCESİNDEN beslenir, ana pano yüküne katılmaz — DXF notu.
        if (pano.PanoTipi == PanoTipi.YanginPanosu)
            doc.Entities.Add(new Text("YANGIN — ana şalter öncesi",
                new Vector3(sol + 3, alt - 4, 0), 2.6) { Layer = layMetin });

        // Ana pano + trafo YOKSA: kutu üstüne ana giriş şalteri + anma akımı (trafo varsa OG bloğunda çizilir).
        if (pano.PanoTipi == PanoTipi.AnaPano && proje.OrtaGerilim == null)
        {
            Cizgi(doc, cx, ust + 12, cx, ust + 30, layKablo);
            SalterSembol(doc, cx, ust + 22, laySembol);
            doc.Entities.Add(new Text(
                pano.GirisSalteriA > 0 ? $"Giriş Şalteri {pano.GirisSalteriA}A" : "Giriş Şalteri",
                new Vector3(cx + 6, ust + 21, 0), 2.6) { Layer = layMetin });
        }

        if (komp)
        {
            KompanzasyonCikislariCiz(doc, proje, pano, cx, alt, sag, laySembol, layMetin, layKablo);
            return;
        }

        // Çıkış linyeleri — kutunun altından sağa doğru liste
        var linyeler = pano.Linyeler;
        for (int i = 0; i < linyeler.Count; i++)
        {
            var l = linyeler[i];
            double ly = alt - YukseklikEk(pano) * (i + 0.5) / Math.Max(1, linyeler.Count) - 3;
            // bara → çıkış stub
            double stubX = sag + 6;
            Cizgi(doc, cx, alt, cx, ly, layKablo);
            Cizgi(doc, cx, ly, stubX, ly, layKablo);
            SigortaSembol(doc, stubX + 5, ly, laySembol);
            doc.Entities.Add(new Text(
                LinyeDxfMetni(l),
                new Vector3(stubX + 12, ly - 1, 0), 2.6) { Layer = layMetin });
        }
    }

    /// <summary>
    /// Kompanzasyon panosunun içeriğini çizer: giriş NH bıçaklı sigorta + röle bilgisi, ardından
    /// her kondansatör kademesi (kademe sigortası + kondansatör sembolü + kVAr) ve reaktör (SVC)
    /// kademeleri (reaktör sembolü) kutunun altından sağa doğru dal olarak listelenir.
    /// </summary>
    private static void KompanzasyonCikislariCiz(DxfDocument doc, ElektrikProje proje, Pano pano,
        double cx, double alt, double sag, Layer laySembol, Layer layMetin, Layer layKablo)
    {
        double hatV = proje.SistemGerilimV;
        var sonuc = proje.Kompanzasyon;
        var kondKademeler = sonuc?.KondansatorKademeleri ?? new List<KompanzasyonKademe>();
        var reakKademeler = (sonuc?.ReaktorAktif == true ? sonuc?.ReaktorKademeleri : null)
            ?? new List<KompanzasyonReaktorKademe>();

        // Üst bilgi satırı: röle + NH bıçaklı giriş sigortası + (varsa) SVC sürücü + harmonik filtre
        var bilgi = new List<string>();
        if (pano.SeciliKompanzasyonRolesi != null)
            bilgi.Add($"Röle {pano.SeciliKompanzasyonRolesi.KademeSayisi} kademe");
        if (pano.GirisBicakliSigorta != null)
            bilgi.Add($"NH {pano.GirisBicakliSigorta.AnmaAkimA}A");
        if (pano.SeciliSvcSurucu != null)
            bilgi.Add($"SVC {pano.SeciliSvcSurucu.Ad}");
        if (pano.SeciliHarmonikFiltre != null)
            bilgi.Add($"Filtre {pano.SeciliHarmonikFiltre.Ad}");
        if (bilgi.Count > 0)
            doc.Entities.Add(new Text(string.Join("  ", bilgi),
                new Vector3(cx + KutuW / 2 + 6, alt - 2, 0), 2.6) { Layer = layMetin });

        // Kademe dalları (kondansatörler önce, reaktörler sonra)
        int toplam = kondKademeler.Count + reakKademeler.Count;
        if (toplam == 0) return;

        double stubX = sag + 6;
        double y0 = alt - 8;          // ilk kademe biraz aşağıda
        double satir = SatirYuksek + 2;
        int idx = 0;

        foreach (var k in kondKademeler)
        {
            double ly = y0 - idx * satir;
            Cizgi(doc, cx, alt, cx, ly, layKablo);
            Cizgi(doc, cx, ly, stubX, ly, layKablo);
            SigortaSembol(doc, stubX + 5, ly, laySembol);                 // kademe sigortası
            KondansatorSembol(doc, stubX + 14, ly, laySembol);           // kondansatör sembolü
            int sigA = PanoHesapServisi.KondansatorKademeSigortaA(k.KVar, hatV);
            doc.Entities.Add(new Text(
                $"K{k.KademeNo}  {k.KVar:N1}kVAr  {sigA}A",
                new Vector3(stubX + 22, ly - 1, 0), 2.6) { Layer = layMetin });
            idx++;
        }

        foreach (var r in reakKademeler)
        {
            double ly = y0 - idx * satir;
            Cizgi(doc, cx, alt, cx, ly, layKablo);
            Cizgi(doc, cx, ly, stubX, ly, layKablo);
            SigortaSembol(doc, stubX + 5, ly, laySembol);
            ReaktorSembol(doc, stubX + 14, ly, laySembol);
            KondansatorSembol(doc, stubX + 22, ly, laySembol);           // reaktör + kondansatör (detuned)
            doc.Entities.Add(new Text(
                $"R{r.KademeNo}  {r.KVar:N1}kVAr  %{sonuc?.ReaktorAkortYuzde:N2}",
                new Vector3(stubX + 30, ly - 1, 0), 2.6) { Layer = layMetin });
            idx++;
        }
    }

    // Çıkış linye listesi için kutu altında ayrılan dikey yer
    private static double YukseklikEk(Pano pano)
        => Math.Max(0, pano.Linyeler.Count) * SatirYuksek + 6;

    private static double PanoYatayPay(Pano pano)
    {
        var maxMetin = pano.Linyeler.Select(LinyeDxfMetni).DefaultIfEmpty("").Max(t => t.Length);
        double metinPayi = maxMetin == 0 ? 0 : 32 + Math.Min(180, maxMetin * 1.6);
        return Math.Max(KolonAdim, KutuW + metinPayi);
    }

    private static string LinyeDxfMetni(Linye l)
    {
        string faz = l.FazAtamasiGoster;   // trifaze → "RST", monofaze → "R"/"S"/"T"
        // Koruma cihazı: TMŞ ise "{A} TMŞ", sigorta ise "{A}A/{karakteristik}"
        string cihaz = l.KorumaCihaziTipi == "TMŞ"
            ? $"{l.SigortaAmper}A TMŞ"
            : $"{l.SigortaAmper}A/{l.SigortaCinsi}";
        var ekler = new List<string>();
        if (l.Kontaktor != null) ekler.Add($"K{l.Kontaktor.AnmaAkimA}A");
        if (l.DarbeKesici != null) ekler.Add($"DK{l.DarbeKesici.AnmaAkimA}A");
        string ek = ekler.Count > 0 ? "  " + string.Join(" ", ekler) : "";
        string metin = $"{l.LinyeNo}  {cihaz}  {faz}  {l.KabloKesiti}  {l.KesmeKapasitesi}{ek}";
        var aciklama = DxfMetinKisalt(l.Aciklama, 36);
        return string.IsNullOrWhiteSpace(aciklama) ? metin : $"{metin}  |  {aciklama}";
    }

    private static string KontrolSinyalKabloMetni(KontrolSinyalKablosu kablo)
    {
        string tip = string.IsNullOrWhiteSpace(kablo.KabloTipi) ? "Sinyal" : kablo.KabloTipi.Trim();
        int damar = Math.Max(1, kablo.DamarSayisi);
        double kesit = kablo.KesitMm2 > 0 ? kablo.KesitMm2 : 1.5;
        double metraj = Math.Max(0, kablo.MetrajM);
        return FormattableString.Invariant($"{tip} • {damar}x{kesit:0.##} mm² • L={metraj:0.##}m");
    }

    /// <summary>Bir alt panoya giden çıkış cihazının DXF etiketi: anma akımı + (TMŞ ise) cihaz tipi
    /// + kablo kesiti + kesme kapasitesi.</summary>
    private static string CikisEtiket(Pano alt)
    {
        string cihaz = alt.CikisKorumaCihaziTipi == "TMŞ" ? " TMŞ" : "";
        string ka = string.IsNullOrWhiteSpace(alt.CikisKesmeKapasitesi) ? "" : "  " + alt.CikisKesmeKapasitesi;
        return $"{alt.CikisKorumaAkimA}A{cihaz}  {alt.KabloKesiti}{ka}";
    }

    private static string KarDxfEtiketi(Pano pano)
    {
        var kar = pano.KacakAkimRolesi;
        if (kar == null) return "";
        if (!kar.TroidTipiMi)
            return $"KAR {kar.SensitiviteMa}mA";

        var at = pano.KacakAkimAkimTrafosu;
        string atEtiket = at == null ? "AT ?" : $"{at.PrimerA}/{at.SekonderA}A";
        return $"Troid KAR {kar.SensitiviteMa}mA {atEtiket}";
    }

    private static string DxfMetinKisalt(string? metin, int max)
    {
        if (string.IsNullOrWhiteSpace(metin)) return "";
        var temiz = metin.Trim().Replace('\r', ' ').Replace('\n', ' ');
        while (temiz.Contains("  ", StringComparison.Ordinal))
            temiz = temiz.Replace("  ", " ", StringComparison.Ordinal);
        return temiz.Length <= max ? temiz : temiz[..Math.Max(0, max - 3)] + "...";
    }

    private static BusbarCikisKutusu? BusbarKutu(ElektrikProje proje, Pano pano)
    {
        var busbar = proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
        return busbar?.CikisKutulari.FirstOrDefault(k => string.Equals(k.BagliPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase));
    }

    private static string BusbarDxfMetni(BusbarHatti? busbar, string? busbarId)
    {
        if (busbar == null)
            return string.IsNullOrWhiteSpace(busbarId) ? "Busbar" : $"Busbar {busbarId[..Math.Min(8, busbarId.Length)]}";

        var parcalar = new List<string> { string.IsNullOrWhiteSpace(busbar.Ad) ? "Busbar" : busbar.Ad };
        if (!string.IsNullOrWhiteSpace(busbar.BusbarTipiAd))
            parcalar.Add(busbar.BusbarTipiAd);
        if (busbar.ToplamAkimA > 0)
            parcalar.Add($"{busbar.ToplamAkimA:N0}A");
        if (busbar.ToplamUzunlukM > 0)
            parcalar.Add($"{busbar.ToplamUzunlukM:0.#}m");
        return string.Join("  ", parcalar.Select(p => DxfMetinKisalt(p, 34)));
    }

    private static double BusbarFeederX(double parentX, int index, int count)
    {
        if (count <= 1) return parentX;

        double raw = index - (count - 1) / 2.0;
        if (Math.Abs(raw) < 0.01)
            raw = 0.5;
        else if (raw > 0)
            raw += 0.5;
        else
            raw -= 0.5;

        return parentX + raw * DxfBusbarFeederGap;
    }

    // ── Jeneratör + transfer bloğu ──
    private static void JeneratorCiz(DxfDocument doc, ElektrikProje proje, JeneratorGrubu grup, Pano ana,
        double anaX, double anaY, double jenY, Dictionary<Pano, (double X, double Y)> pos,
        int grupIndex, Layer layJen, Layer laySembol, Layer layMetin, ElektrikKutuphaneServisi? kutuphane)
    {
        double saparX = pos.Values.Max(v => v.X) + KolonAdim + grupIndex * 210;
        double y = jenY;   // jeneratör bloğu kendi katının seviyesinde

        // Ana pano → senkron panosu kablosu (kat farklıysa ortogonal güzergah)
        double anaCikis = anaX + KutuW / 2;
        if (Math.Abs(jenY - anaY) < 0.5)
            Cizgi(doc, anaCikis, anaY, saparX - 20, anaY, layJen);
        else
        {
            double midX = (anaCikis + saparX - 20) / 2;
            Cizgi(doc, anaCikis, anaY, midX, anaY, layJen);
            Cizgi(doc, midX, anaY, midX, jenY, layJen);
            Cizgi(doc, midX, jenY, saparX - 20, jenY, layJen);
        }
        // ATS / senkron şalter sembolü (ana pano çıkışında)
        var salter = grup.TransferSalterAd;
        doc.Entities.Add(new Text(string.IsNullOrWhiteSpace(salter) ? "ATS" : salter,
            new Vector3(anaCikis + 4, anaY + 3, 0), 2.6) { Layer = layJen });
        string hedefKabloEtiketi = string.IsNullOrWhiteSpace(grup.SenkronHedefKabloKesiti)
            ? ""
            : $"  {grup.SenkronHedefKabloKesiti}";
        doc.Entities.Add(new Text(
            FormattableString.Invariant($"L={grup.SenkronHedefPanoKabloMesafeM:0.##}m") + hedefKabloEtiketi,
            new Vector3(anaCikis + 4, anaY - 5, 0), 2.4) { Layer = layJen });

        // Senkron kutusu
        Dikdortgen(doc, saparX - 24, y - 12, saparX + 24, y + 12, layJen);
        doc.Entities.Add(new Text("JENERATOR", new Vector3(saparX - 24, y + 18, 0), 3.2) { Layer = layJen });
        doc.Entities.Add(new Text("Senkron", new Vector3(saparX - 18, y + 1, 0), 3) { Layer = layJen });
        doc.Entities.Add(new Text(string.IsNullOrWhiteSpace(grup.SenkronPanoAd) ? "Senkron Pano" : grup.SenkronPanoAd,
            new Vector3(saparX - 22, y - 7, 0), 2.2) { Layer = layJen });
        doc.Entities.Add(new Text(
            grup.BeslemeKapsami == JeneratorBeslemeKapsami.LokalBolge ? $"Lokal: {ana.Ad}" : "Tesis geneli",
            new Vector3(saparX - 24, y + 13, 0), 2.2) { Layer = layJen });

        var uniteler = grup.Jeneratorler.Count > 0 ? grup.Jeneratorler : new List<Jenerator> { new() };
        double genX = saparX + 82;
        double firstY = y - (uniteler.Count - 1) * 18;
        double lastY = y + (uniteler.Count - 1) * 18;
        Cizgi(doc, saparX + 24, y, genX - 20, y, layJen);
        if (uniteler.Count > 1)
            Cizgi(doc, genX - 20, firstY, genX - 20, lastY, layJen);

        for (int i = 0; i < uniteler.Count; i++)
        {
            var jen = uniteler[i];
            double genY = y + (i - (uniteler.Count - 1) / 2.0) * 36;
            Cizgi(doc, genX - 20, genY, genX - 14, genY, layJen);

            doc.Entities.Add(new Circle(new Vector3(genX, genY, 0), 14) { Layer = layJen });
            doc.Entities.Add(new Text("G", new Vector3(genX - 3, genY - 3, 0), 8) { Layer = layJen });

            var tanim = kutuphane?.Jeneratorler.FirstOrDefault(t => t.Ad == jen.JeneratorTanimAd);
            string mod = grup.Mod == JeneratorMod.Prime ? "PRP" : "ESP";
            string guc = tanim == null
                ? ""
                : $"{JeneratorHesapServisi.JeneratorGucKVA(tanim, grup.Mod):N0}kVA ";
            string uniteKabloEtiketi = string.IsNullOrWhiteSpace(jen.SenkronKabloKesiti)
                ? ""
                : $"  {jen.SenkronKabloKesiti}";
            doc.Entities.Add(new Text(
                $"{i + 1}. {(string.IsNullOrWhiteSpace(jen.JeneratorTanimAd) ? "Otomatik jeneratör" : jen.JeneratorTanimAd)}  {guc}{mod}  " +
                FormattableString.Invariant($"L={jen.SenkronPanoKabloMesafeM:0.##}m") + uniteKabloEtiketi,
                new Vector3(genX + 18, genY - 3, 0), 2.6) { Layer = layJen });
        }

        string katMetin = string.IsNullOrWhiteSpace(grup.Kat) ? "" : $"  Kat: {grup.Kat}";
        doc.Entities.Add(new Text(
            $"Toplam: {grup.ToplamGucKVA:N0}kVA  Yük≈{grup.HesaplananYukKVA:N0}kVA{katMetin}",
            new Vector3(saparX - 24, y - 22, 0), 2.4) { Layer = layJen });
    }

    // ── Orta gerilim bloğu: şebeke → OG hücreleri → trafo → AG bağlantı (ana panonun üstüne) ──
    private static void OrtaGerilimCiz(DxfDocument doc, OrtaGerilimSistemi og,
        double rootX, double rootCadY, int girisSalteriA, Layer layOg, Layer layTrafo, Layer layHucre,
        Layer laySembol, Layer layMetin)
    {
        double rootUst = rootCadY + KutuH / 2;     // ana pano üst kenarı (CAD)
        double trafoY = rootUst + 40;              // trafo merkez
        double busY = trafoY + 55;                 // OG bara
        double cellAlt = busY + 8;                 // hücre kutusu alt
        double cellH = 16;
        double sebekeY = cellAlt + cellH + 26;     // şebeke kutusu merkez

        // ── Trafo AG çıkışı → ana pano (dikey) + bağlantı etiketi ──
        Cizgi(doc, rootX, rootUst, rootX, trafoY - 14, layOg);
        doc.Entities.Add(new Text(OgBaglantiMetin(og.CikisBaglanti),
            new Vector3(rootX + 4, (rootUst + trafoY) / 2, 0), 2.6) { Layer = layMetin });
        // Giriş şalteri (ana giriş şalteri) — trafo AG çıkış akımına göre seçili.
        double salterY = (rootUst + (trafoY - 14)) / 2;
        SalterSembol(doc, rootX, salterY, laySembol);
        doc.Entities.Add(new Text(
            girisSalteriA > 0 ? $"Giriş Şalteri {girisSalteriA}A" : "Giriş Şalteri",
            new Vector3(rootX + 6, salterY + 5, 0), 2.6) { Layer = layMetin });

        // ── Trafo (IEC iki daire) ──
        doc.Entities.Add(new Circle(new Vector3(rootX, trafoY - 6, 0), 8) { Layer = layTrafo });
        doc.Entities.Add(new Circle(new Vector3(rootX, trafoY + 6, 0), 8) { Layer = layTrafo });
        doc.Entities.Add(new Text(
            string.IsNullOrEmpty(og.TrafoTanimAd) ? "TRAFO" : og.TrafoTanimAd,
            new Vector3(rootX + 12, trafoY + 2, 0), 3) { Layer = layMetin });
        if (og.TrafoAgAkimA > 0)
            doc.Entities.Add(new Text($"AG {og.TrafoAgAkimA:N0}A  Ik={og.AgBaraIkKA:N1}kA",
                new Vector3(rootX + 12, trafoY - 6, 0), 2.4) { Layer = layMetin });

        // ── Trafo → OG bara (dikey) ──
        Cizgi(doc, rootX, trafoY + 14, rootX, busY, layOg);
        doc.Entities.Add(new Text(
            OrtaGerilimHesapServisi.XlpeKabloEtiketi(og.HucreTrafoXlpeKablosu),
            new Vector3(rootX + 4, trafoY + 21, 0), 2.2) { Layer = layMetin });

        // ── OG bara (yatay) + hücreler ──
        var hucreler = og.Hucreler;
        int n = hucreler.Count;
        double pitch = KolonAdim;
        double lineupW = n * pitch;
        double xStart = rootX - lineupW / 2;
        double busLeft = Math.Min(xStart, rootX - 30);
        double busRight = Math.Max(xStart + lineupW, rootX + 30);
        Cizgi(doc, busLeft, busY, busRight, busY, layOg);

        for (int i = 0; i < n; i++)
        {
            var h = hucreler[i];
            double xi = xStart + pitch * (i + 0.5);
            double cellW = 56;
            Cizgi(doc, xi, busY, xi, cellAlt, layHucre);
            Dikdortgen(doc, xi - cellW / 2, cellAlt, xi + cellW / 2, cellAlt + cellH, layHucre);
            doc.Entities.Add(new Text(OgHucreKisaAdDxf(h.HucreTipi),
                new Vector3(xi - cellW / 2 + 2, cellAlt + cellH / 2 - 1, 0), 2.4) { Layer = layMetin });
        }

        // ── Şebeke kutusu (en üst) ──
        Cizgi(doc, rootX, busY, rootX, sebekeY - 8, layOg);
        doc.Entities.Add(new Text(
            OrtaGerilimHesapServisi.XlpeKabloEtiketi(og.SebekeHucreXlpeKablosu),
            new Vector3(rootX + 4, busY + 4, 0), 2.2) { Layer = layMetin });
        Dikdortgen(doc, rootX - 30, sebekeY - 8, rootX + 30, sebekeY + 8, layOg);
        doc.Entities.Add(new Text($"ŞEBEKE {og.OgGerilimKV:0.#}kV",
            new Vector3(rootX - 27, sebekeY - 1, 0), 2.6) { Layer = layMetin });

        // Köşk + trafo katı etiketi
        string koskKat = string.IsNullOrWhiteSpace(og.TrafoKat) ? "" : $"  •  Kat: {og.TrafoKat}";
        doc.Entities.Add(new Text($"Köşk: {OgKoskMetin(og.KoskTipi)}{koskKat}",
            new Vector3(busLeft, sebekeY + 14, 0), 2.6) { Layer = layMetin });
    }

    private static string OgHucreKisaAdDxf(OgHucreTipi t) => t switch
    {
        OgHucreTipi.Giris => "GIRIS",
        OgHucreTipi.Olcu => "OLCU",
        OgHucreTipi.BaraBaglama => "BARA BAG.",
        OgHucreTipi.Cikis => "CIKIS",
        OgHucreTipi.TrafoKoruma => "TR.KORUMA",
        _ => t.ToString()
    };

    private static string OgKoskMetin(KoskTipi k) => k switch
    {
        KoskTipi.BinaAlti => "Bina Alti",
        KoskTipi.DisMekanBetonKosk => "Dis Mekan Beton Kosk",
        KoskTipi.HariciYapi => "Harici Yapi",
        _ => k.ToString()
    };

    // ── Köşk fiziksel yerleşim bloğu (plan görünümü, ayrı bölge) ──
    private static void KoskBlokCiz(DxfDocument doc, OrtaGerilimSistemi og,
        ElektrikKutuphaneServisi kutuphane, double sagUstSagX, double ustY,
        Layer layHucre, Layer layTrafo, Layer layMetin)
    {
        var y = KoskYerlesimServisi.Hesapla(og, kutuphane);
        if (y.GenislikMm <= 0 || y.DerinlikMm <= 0) return;

        const double olcek = 0.02; // mm → CAD birimi
        double kw = y.GenislikMm * olcek;
        double kd = y.DerinlikMm * olcek;
        // Bloğun sağ kenarı sagUstSagX'te; plan aşağı doğru çizilir (Y küçülerek).
        double ox = sagUstSagX - kw;
        double oyUst = ustY;

        doc.Entities.Add(new Text("Kösk Yerlesimi (plan)", new Vector3(ox, oyUst + 8, 0), 4) { Layer = layMetin });
        // Köşk dış duvarı
        Dikdortgen(doc, ox, oyUst - kd, ox + kw, oyUst, layTrafo);
        doc.Entities.Add(new Text($"{y.GenislikMm:N0}x{y.DerinlikMm:N0}mm",
            new Vector3(ox, oyUst - kd - 6, 0), 2.6) { Layer = layMetin });

        foreach (var el in y.Elemanlar)
        {
            // Model Y (önden artar) → CAD (yukarıdan aşağı): üst = oyUst - el.Y*olcek
            double ex = ox + el.X * olcek;
            double eUst = oyUst - el.Y * olcek;
            double eAlt = eUst - el.Derinlik * olcek;
            double esag = ex + el.Genislik * olcek;
            var lay = el.Tur == "Trafo" ? layTrafo : layHucre;
            Dikdortgen(doc, ex, eAlt, esag, eUst, lay);
            doc.Entities.Add(new Text(el.Etiket, new Vector3(ex + 1, (eUst + eAlt) / 2 - 1, 0), 2.2) { Layer = layMetin });
        }
    }

    private static string OgBaglantiMetin(TrafoCikisBaglanti b)
    {
        string tip = b.Tip switch
        {
            TrafoCikisBaglantiTipi.Kablo => $"Kablo {b.KesitMm2}".Trim(),
            TrafoCikisBaglantiTipi.Bar24 => $"Bar-24 {b.ReferansAd}".Trim(),
            TrafoCikisBaglantiTipi.Hucre => "AG Hucre",
            _ => "Baglanti"
        };
        return $"{tip} {b.UzunlukM:0.#}m";
    }

    // ── UPS + akü bloğu (kritik panonun üstünde) ──
    private static void UpsCiz(DxfDocument doc, Ups ups,
        double cx, double cy, Layer layUps, Layer layMetin,
        ElektrikKutuphaneServisi? kutuphane = null)
    {
        double kutuUst = cy + KutuH / 2;
        double upsAlt = kutuUst + 18;
        double upsUst = upsAlt + KutuH;
        double sol = cx - KutuW / 2, sag = cx + KutuW / 2;

        // UPS → kritik pano besleme çizgisi
        Cizgi(doc, cx, kutuUst, cx, upsAlt, layUps);

        // UPS kutusu + çapraz (çift dönüşüm: ~ / =)
        Dikdortgen(doc, sol, upsAlt, sag, upsUst, layUps);
        Cizgi(doc, sol, upsAlt, sag, upsUst, layUps); // köşegen — dönüştürücü simgesi

        // Anma gücü hesap sırasında UPS kaydına yazılır; böylece DXF kütüphane parametresi
        // verilmeden üretildiğinde de kanvasla aynı değeri gösterir. Kütüphane yalnız eski
        // proje kayıtları için geriye dönük çözümlemedir.
        var upsTanim = kutuphane?.Upsler?.FirstOrDefault(u => u.Ad == ups.UpsTanimAd);
        double anmaGucuKVA = ups.SeciliUpsGucKVA > 0 ? ups.SeciliUpsGucKVA : upsTanim?.GucKVA ?? 0;
        string kvaTxt = anmaGucuKVA > 0 ? $"{anmaGucuKVA:N0}kVA"
            : ups.GerekliKVA > 0 ? $"~{ups.GerekliKVA:N0}kVA" : "UPS";
        doc.Entities.Add(new Text($"UPS {kvaTxt}", new Vector3(sol + 2, (upsAlt + upsUst) / 2 - 1, 0), 2.6) { Layer = layMetin });

        // Akü sembolü (UPS sağına) — uzun/kısa çizgi çiftleri
        double akuX = sag + 14;
        double akuY = (upsAlt + upsUst) / 2;
        // hücre 1
        Cizgi(doc, akuX, akuY + 5, akuX, akuY - 5, layUps);          // uzun (+)
        Cizgi(doc, akuX + 4, akuY + 2.5, akuX + 4, akuY - 2.5, layUps); // kısa (−)
        // hücre 2
        Cizgi(doc, akuX + 8, akuY + 5, akuX + 8, akuY - 5, layUps);
        Cizgi(doc, akuX + 12, akuY + 2.5, akuX + 12, akuY - 2.5, layUps);
        // UPS → akü bağlantısı
        Cizgi(doc, sag, akuY, akuX, akuY, layUps);

        doc.Entities.Add(new Text(
            $"{ups.SeriBlokSayisi}S×{ups.ParalelStringSayisi}P  {ups.ToplamAh:N0}Ah  {ups.OtonomiDk}dk",
            new Vector3(akuX, akuY - 9, 0), 2.4) { Layer = layMetin });
    }

    // ── Semboller ──
    private static void SalterSembol(DxfDocument doc, double x, double y, Layer lay)
    {
        // Açık şalter: kısa eğik çizgi + iki uç nokta
        Cizgi(doc, x, y - 5, x, y - 2, lay);
        Cizgi(doc, x, y - 2, x + 4, y + 4, lay);
        Cizgi(doc, x, y + 4, x, y + 7, lay);
    }

    private static void SigortaSembol(DxfDocument doc, double x, double y, Layer lay)
    {
        // Sigorta: küçük dikdörtgen + orta çizgi (IEC 60617)
        Dikdortgen(doc, x - 4, y - 2.2, x + 4, y + 2.2, lay);
        Cizgi(doc, x - 4, y, x + 4, y, lay);
    }

    private static void KondansatorSembol(DxfDocument doc, double x, double y, Layer lay)
    {
        // Kondansatör (IEC 60617): iki paralel plaka + giriş/çıkış uçları (yatay hat üzerinde).
        Cizgi(doc, x - 5, y, x - 1.2, y, lay);     // sol uç
        Cizgi(doc, x - 1.2, y - 3, x - 1.2, y + 3, lay); // sol plaka
        Cizgi(doc, x + 1.2, y - 3, x + 1.2, y + 3, lay); // sağ plaka
        Cizgi(doc, x + 1.2, y, x + 5, y, lay);     // sağ uç
    }

    private static void ReaktorSembol(DxfDocument doc, double x, double y, Layer lay)
    {
        // Reaktör/bobin (detuned filtre): küçük dikdörtgen sargı kutusu + uçlar.
        Cizgi(doc, x - 5, y, x - 3, y, lay);
        Dikdortgen(doc, x - 3, y - 2.4, x + 3, y + 2.4, lay);
        Cizgi(doc, x + 3, y, x + 5, y, lay);
    }

    // ── Yardımcılar ──
    private static Layer Katman(DxfDocument doc, string ad, AciColor renk)
    {
        var lay = new Layer(ad) { Color = renk };
        doc.Layers.Add(lay);
        return lay;
    }

    private static void Cizgi(DxfDocument doc, double x1, double y1, double x2, double y2, Layer lay)
        => doc.Entities.Add(new Line(new Vector3(x1, y1, 0), new Vector3(x2, y2, 0)) { Layer = lay });

    private static void Dikdortgen(DxfDocument doc, double sol, double alt, double sag, double ust, Layer lay)
    {
        Cizgi(doc, sol, alt, sag, alt, lay);
        Cizgi(doc, sag, alt, sag, ust, lay);
        Cizgi(doc, sag, ust, sol, ust, lay);
        Cizgi(doc, sol, ust, sol, alt, lay);
    }

    private static bool TransferPanosuMu(ElektrikProje proje, Pano pano)
        => pano.PanoTipi == PanoTipi.TransferPanosu &&
           (proje.JeneratorGruplari.Any(g => g.SenkronPanoAd == pano.Ad)
            || proje.Jenerator?.TransferPanoAd == pano.Ad);

    private static List<JeneratorGrubu> AktifJeneratorGruplari(ElektrikProje proje)
    {
        if (proje.JeneratorGruplari.Count > 0)
            return proje.JeneratorGruplari;

        if (proje.Jenerator == null)
            return new List<JeneratorGrubu>();

        var jen = proje.Jenerator;
        return new List<JeneratorGrubu>
        {
            new()
            {
                AnaPanoAd = jen.AnaPanoAd,
                SenkronPanoAd = jen.TransferPanoAd,
                TransferSalterAd = jen.TransferSalterAd,
                Mod = jen.Mod,
                EmniyetFaktoru = jen.EmniyetFaktoru,
                SenkronHedefPanoKabloMesafeM = jen.SenkronHedefPanoKabloMesafeM,
                SenkronHedefKabloKesiti = jen.SenkronHedefKabloKesiti,
                SenkronHedefKabloParalelKolSayisi = jen.SenkronHedefKabloParalelKolSayisi,
                Jeneratorler = new List<Jenerator> { jen },
                HesaplananYukKW = jen.HesaplananYukKW,
                HesaplananYukKVA = jen.HesaplananYukKVA,
                GerekliKVAMin = jen.GerekliKVAMin,
                ToplamGucKW = jen.Mod == JeneratorMod.Prime ? 0 : 0,
                ToplamGucKVA = jen.HesaplananYukKVA,
                YukYeterliMi = jen.YukYeterliMi,
                YakitTuketimLh = jen.YakitTuketimLh
            }
        };
    }

    private static Pano? JeneratorHedefPanoBul(ElektrikProje proje, JeneratorGrubu grup,
        Dictionary<Pano, (double X, double Y)> pos)
    {
        var hedef = proje.TumPanolar.FirstOrDefault(p =>
            string.Equals(p.Ad, grup.AnaPanoAd, StringComparison.OrdinalIgnoreCase) &&
            pos.ContainsKey(p));
        if (hedef != null) return hedef;

        if (proje.KokPano != null && pos.ContainsKey(proje.KokPano))
            return proje.KokPano;

        hedef = proje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano && pos.ContainsKey(p));
        if (hedef != null) return hedef;

        return pos.Keys.FirstOrDefault();
    }
}
