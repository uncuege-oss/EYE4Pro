using ClosedXML.Excel;
using Kst.Acoustics.Core.Electric.Models;
using System.Globalization;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// M11 projesini pano başına ayrı çalışma sayfası içeren bir Excel raporuna aktarır.
/// </summary>
public static class TekHatSemasiExcelServisi
{
    private const int SutunSayisi = 14;

    private static readonly string[] Basliklar =
    [
        "Pano Adı",
        "Beslendiği Üst Pano",
        "Besleme Kablosu / Kesiti",
        "Besleme Mesafesi (m)",
        "Giriş Sigortası (A)",
        "Giriş Kaçak Akım Rölesi",
        "Linye No",
        "Alt Pano / Yük Tipi",
        "Çıkış Sigortası (A)",
        "Sigorta Tipi",
        "Kesme Kapasitesi",
        "Beslediği Kablo Kesiti",
        "Mesafe (m)",
        "Açıklama"
    ];

    public static void RaporOlustur(ElektrikProje proje, string filePath)
    {
        ArgumentNullException.ThrowIfNull(proje);
        if (proje.TumPanolar.Count == 0)
            throw new InvalidOperationException("Excel raporu için projede en az bir pano bulunmalıdır.");

        using var workbook = new XLWorkbook();
        var kullanilanSayfaAdlari = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ozetSayfaAdi = BenzersizSayfaAdi("M13 Metraj Özeti", 0, kullanilanSayfaAdlari);
        var ozetSayfasi = workbook.Worksheets.Add(ozetSayfaAdi);
        ToplamMetrajSayfasiYaz(ozetSayfasi, proje);

        for (var panoIndex = 0; panoIndex < proje.TumPanolar.Count; panoIndex++)
        {
            var pano = proje.TumPanolar[panoIndex];
            var sayfaAdi = BenzersizSayfaAdi(pano.Ad, panoIndex + 1, kullanilanSayfaAdlari);
            var worksheet = workbook.Worksheets.Add(sayfaAdi);
            PanoSayfasiYaz(worksheet, proje, pano, panoIndex + 1);
        }

        workbook.Properties.Title = $"{proje.ProjeAdi} - Pano Çıkış Listeleri";
        workbook.Properties.Subject = "M11 Tek Hat Şeması pano ve linye raporu";
        workbook.Properties.Author = "EYE4PRO Electric";
        workbook.SaveAs(filePath);
    }

    private static void ToplamMetrajSayfasiYaz(IXLWorksheet ws, ElektrikProje proje)
    {
        const int sutunSayisi = 6;
        var satirlar = M13MetrajSatirlari(proje);

        ws.ShowGridLines = false;
        var baslik = ws.Range(1, 1, 1, sutunSayisi);
        baslik.Merge();
        ws.Cell(1, 1).Value = $"{proje.ProjeAdi} - M13 Toplam Metraj Özeti";
        baslik.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE9DF");
        baslik.Style.Font.FontColor = XLColor.FromHtml("#173F2A");
        baslik.Style.Font.Bold = true;
        baslik.Style.Font.FontSize = 16;
        baslik.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        baslik.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 30;

        var aciklama = ws.Range(2, 1, 2, sutunSayisi);
        aciklama.Merge();
        ws.Cell(2, 1).Value = "M13 keşif/metraj modülü için referans: kablolar gerçek satın alma metresiyle " +
                              "hesaplanır; paralel kollarda güzergâh uzunluğu kol sayısıyla çarpılır.";
        aciklama.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0EA");
        aciklama.Style.Font.FontColor = XLColor.FromHtml("#254B34");
        aciklama.Style.Font.Italic = true;
        aciklama.Style.Alignment.WrapText = true;
        ws.Row(2).Height = 30;

        // Kablolar tek "Kablo" bölümünde tip başına toplandı; kartlar kategori bazında özet verir.
        double gucKabloToplam = satirlar.Where(s => s.AnaGrup == "Kablo" && s.AltGrup == "Güç kablosu").Sum(s => s.Miktar);
        double digerKabloToplam = satirlar.Where(s => s.AnaGrup == "Kablo" && s.AltGrup != "Güç kablosu").Sum(s => s.Miktar);
        double tumKabloToplam = satirlar.Where(s => s.AnaGrup == "Kablo").Sum(s => s.Miktar);
        OzetKartiYaz(ws.Range(4, 1, 4, 2), ws.Range(5, 1, 5, 2), "Güç Kabloları", gucKabloToplam);
        OzetKartiYaz(ws.Range(4, 3, 4, 4), ws.Range(5, 3, 5, 4), "Sinyal / OG Kabloları", digerKabloToplam);
        OzetKartiYaz(ws.Range(4, 5, 4, 6), ws.Range(5, 5, 5, 6), "Tüm Kablolar", tumKabloToplam, vurgu: true);

        var basliklar = new[] { "Ana Grup", "Alt Grup", "Teknik Özellik", "Birim", "Miktar", "Açıklama" };
        for (int col = 0; col < basliklar.Length; col++)
            ws.Cell(7, col + 1).Value = basliklar[col];

        int row = 8;
        foreach (var satir in satirlar)
        {
            ws.Cell(row, 1).Value = satir.AnaGrup;
            ws.Cell(row, 2).Value = satir.AltGrup;
            ws.Cell(row, 3).Value = satir.TeknikOzellik;
            ws.Cell(row, 4).Value = satir.Birim;
            ws.Cell(row, 5).Value = satir.Miktar;
            string notMetni = string.Join("; ", satir.Notlar.Distinct(StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(notMetni))
                ws.Cell(row, 6).Value = notMetni;
            row++;
        }

        int sonSatir = Math.Max(8, row - 1);
        var tablo = ws.Range(7, 1, sonSatir, sutunSayisi).CreateTable("M13MetrajOzeti");
        tablo.Theme = XLTableTheme.None;
        tablo.ShowAutoFilter = true;
        tablo.ShowRowStripes = false;

        var tabloBasligi = ws.Range(7, 1, 7, sutunSayisi);
        tabloBasligi.Style.Fill.BackgroundColor = XLColor.FromHtml("#6F9E42");
        tabloBasligi.Style.Font.FontColor = XLColor.White;
        tabloBasligi.Style.Font.Bold = true;
        tabloBasligi.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        tabloBasligi.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(7).Height = 28;

        ws.Range(8, 1, sonSatir, sutunSayisi).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Range(8, 5, sonSatir, 5).Style.NumberFormat.Format = "#,##0.###";
        ws.Range(8, 6, sonSatir, 6).Style.Alignment.WrapText = true;
        for (int dataRow = 8; dataRow <= sonSatir; dataRow++)
        {
            ws.Row(dataRow).Height = 30;
            ws.Range(dataRow, 1, dataRow, sutunSayisi).Style.Fill.BackgroundColor =
                dataRow % 2 == 0 ? XLColor.FromHtml("#EEF5E8") : XLColor.White;
        }

        var genislikler = new[] { 25d, 30d, 40d, 12d, 15d, 58d };
        for (int col = 1; col <= sutunSayisi; col++)
            ws.Column(col).Width = genislikler[col - 1];

        ws.SheetView.FreezeRows(7);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.SetRowsToRepeatAtTop(1, 7);
        ws.PageSetup.Margins.SetLeft(0.25).SetRight(0.25).SetTop(0.5).SetBottom(0.5);
    }

    private static void OzetKartiYaz(IXLRange etiket, IXLRange deger, string baslik, double miktar, bool vurgu = false)
    {
        etiket.Merge();
        deger.Merge();
        etiket.FirstCell().Value = baslik;
        deger.FirstCell().Value = miktar;
        etiket.Style.Fill.BackgroundColor = XLColor.FromHtml(vurgu ? "#BFD8A5" : "#DCE9DF");
        etiket.Style.Font.FontColor = XLColor.FromHtml("#173F2A");
        etiket.Style.Font.Bold = true;
        etiket.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        deger.Style.Fill.BackgroundColor = XLColor.FromHtml(vurgu ? "#D5E8C5" : "#EEF5E8");
        deger.Style.Font.FontColor = XLColor.FromHtml("#173F2A");
        deger.Style.Font.Bold = true;
        deger.Style.Font.FontSize = 14;
        deger.Style.NumberFormat.Format = "#,##0.0 \"m\"";
        deger.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static List<M13MetrajSatiri> M13MetrajSatirlari(ElektrikProje proje)
    {
        var satirlar = new List<M13MetrajSatiri>();
        KabloMetrajlariniEkle(satirlar, proje);
        KabloSatirlariniBirlestir(satirlar);
        KorumaCihazlariniEkle(satirlar, proje);
        KorumaCihaziSatirlariniBirlestir(satirlar);
        BusbarMetrajlariniEkle(satirlar, proje);
        PanoMetrajlariniEkle(satirlar, proje);
        KompanzasyonMetrajlariniEkle(satirlar, proje);
        JeneratorMetrajlariniEkle(satirlar, proje);
        KumandaVeOlcumMetrajlariniEkle(satirlar, proje);
        UpsMetrajlariniEkle(satirlar, proje);
        OrtaGerilimEkipmanlariniEkle(satirlar, proje);
        AksesuarMetrajlariniEkle(satirlar, proje);
        return satirlar;
    }

    private static void KabloMetrajlariniEkle(List<M13MetrajSatiri> satirlar, ElektrikProje proje)
    {
        foreach (var pano in proje.TumPanolar.Where(p => p.PanoTipi != PanoTipi.KontrolPanosu))
        {
            bool ustPanoVar = pano.UstPano != null || !string.IsNullOrWhiteSpace(pano.UstPanoAd);
            if (!ustPanoVar || pano.KabloMesafeM <= 0 || string.IsNullOrWhiteSpace(pano.KabloKesiti))
                continue;

            var busbar = BagliBusbar(proje, pano);
            var kutu = busbar?.CikisKutulari.FirstOrDefault(k =>
                string.Equals(k.BagliPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase));
            double mesafe = kutu?.KabloMesafeM > 0 ? kutu.KabloMesafeM : pano.KabloMesafeM;
            int kol = Math.Max(1, pano.KabloParalelKolSayisi);
            string altGrup = busbar == null ? "Pano besleme" : "Busbar kutusu → Pano";
            EkleMetraj(satirlar, "Kablo - Pano/Tesis", altGrup,
                KabloTekKolEtiketi(pano.KabloTipi, pano.KabloKesiti, kol), "m", mesafe * kol,
                $"{pano.Ad}: {mesafe:0.##} m x {kol} kol");
        }

        foreach (var kontrol in proje.TumPanolar.Where(p => p.PanoTipi == PanoTipi.KontrolPanosu))
        foreach (var kablo in kontrol.KontrolSinyalKablolari.Where(k => k.MetrajM > 0))
            EkleMetraj(satirlar, "Kablo - Pano/Tesis", "Kontrol sinyal kablosu",
                $"{kablo.KabloTipi} {kablo.DamarSayisi}x{kablo.KesitMm2:0.##} mm²", "m", kablo.MetrajM,
                $"{kontrol.Ad} → {kablo.KaynakPanoAd}");

        var og = proje.OrtaGerilim;
        if (og != null)
        {
            OgXlpeKablosuEkle(satirlar, "Şebeke → OG Hücre", og.SebekeHucreXlpeKablosu);
            OgXlpeKablosuEkle(satirlar, "OG Hücre → Trafo", og.HucreTrafoXlpeKablosu);

            var cikis = og.CikisBaglanti;
            if (cikis.Tip == TrafoCikisBaglantiTipi.Kablo && cikis.UzunlukM > 0)
            {
                int kol = Math.Max(1, cikis.HesaplananParalelKolSayisi > 0
                    ? cikis.HesaplananParalelKolSayisi
                    : cikis.ParalelKolSayisi);
                EkleMetraj(satirlar, "Kablo - Pano/Tesis", "Trafo → Ana Pano",
                    KabloTekKolEtiketi(cikis.ReferansAd, cikis.KesitMm2, kol), "m", cikis.UzunlukM * kol,
                    $"{cikis.UzunlukM:0.##} m x {kol} kol");
            }
        }

        var gruplar = proje.JeneratorGruplari;
        if (gruplar.Count > 0)
        {
            foreach (var grup in gruplar)
            {
                foreach (var jen in grup.Jeneratorler.Where(j => j.SenkronPanoKabloMesafeM > 0))
                {
                    int kol = Math.Max(1, jen.SenkronKabloParalelKolSayisi);
                    EkleMetraj(satirlar, "Kablo - Pano/Tesis", "Jeneratör → Senkron Pano",
                        KabloTekKolEtiketi(proje.VarsayilanKabloTipi, jen.SenkronKabloKesiti, kol), "m",
                        jen.SenkronPanoKabloMesafeM * kol,
                        $"{jen.JeneratorTanimAd}: {jen.SenkronPanoKabloMesafeM:0.##} m x {kol} kol");
                }

                if (grup.SenkronHedefPanoKabloMesafeM > 0)
                {
                    int kol = Math.Max(1, grup.SenkronHedefKabloParalelKolSayisi);
                    EkleMetraj(satirlar, "Kablo - Pano/Tesis", "Senkron Pano → Hedef Pano",
                        KabloTekKolEtiketi(proje.VarsayilanKabloTipi, grup.SenkronHedefKabloKesiti, kol), "m",
                        grup.SenkronHedefPanoKabloMesafeM * kol,
                        $"{grup.SenkronPanoAd} → {grup.AnaPanoAd}: {grup.SenkronHedefPanoKabloMesafeM:0.##} m x {kol} kol");
                }
            }
        }
        else if (proje.Jenerator is { } jen)
        {
            int unitKol = Math.Max(1, jen.SenkronKabloParalelKolSayisi);
            EkleMetraj(satirlar, "Kablo - Pano/Tesis", "Jeneratör → Transfer Pano",
                KabloTekKolEtiketi(proje.VarsayilanKabloTipi, jen.SenkronKabloKesiti, unitKol), "m",
                jen.SenkronPanoKabloMesafeM * unitKol,
                $"{jen.JeneratorTanimAd}: {jen.SenkronPanoKabloMesafeM:0.##} m x {unitKol} kol");
            int hedefKol = Math.Max(1, jen.SenkronHedefKabloParalelKolSayisi);
            EkleMetraj(satirlar, "Kablo - Pano/Tesis", "Transfer Pano → Hedef Pano",
                KabloTekKolEtiketi(proje.VarsayilanKabloTipi, jen.SenkronHedefKabloKesiti, hedefKol), "m",
                jen.SenkronHedefPanoKabloMesafeM * hedefKol,
                $"{jen.TransferPanoAd} → {jen.AnaPanoAd}: {jen.SenkronHedefPanoKabloMesafeM:0.##} m x {hedefKol} kol");
        }

        foreach (var pano in proje.TumPanolar)
        foreach (var linye in pano.Linyeler.Where(l => l.KabloMesafeM > 0 && !string.IsNullOrWhiteSpace(l.KabloKesiti)))
        {
            int kol = Math.Max(1, linye.KabloParalelKolSayisi);
            EkleMetraj(satirlar, "Kablo - Linye", "Linye kablosu",
                KabloTekKolEtiketi(proje.VarsayilanKabloTipi, linye.KabloKesiti, kol), "m",
                linye.KabloMesafeM * kol,
                $"{pano.Ad}/{linye.LinyeNo}: {linye.KabloMesafeM:0.##} m x {kol} kol");
        }
    }

    /// <summary>
    /// Linye + pano + tüm kabloları tek "Kablo" bölümünde, <b>kablo tipi başına tek satırda</b>
    /// birleştirir: aynı tip/kesit (örn. "NHXMH 4x240 mm²") farklı güzergâhlardan (trafo, jeneratör,
    /// pano besleme, linye) gelse de toplam satın alma metresi tek satırda toplanır. Kategori:
    /// güç / sinyal / OG XLPE. Bu adım <see cref="M13MetrajSatirlari"/>'nde kablo satırları eklendikten
    /// hemen sonra (diğer bölümlerden önce) çağrılır; böylece kablolar listede başta kalır.
    /// </summary>
    private static void KabloSatirlariniBirlestir(List<M13MetrajSatiri> satirlar)
    {
        var kablolar = satirlar
            .Where(s => s.AnaGrup.StartsWith("Kablo", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (kablolar.Count == 0) return;
        satirlar.RemoveAll(s => s.AnaGrup.StartsWith("Kablo", StringComparison.OrdinalIgnoreCase));

        foreach (var grup in kablolar
                     .GroupBy(s => (Kategori: KabloKategorisi(s.AltGrup), s.TeknikOzellik, s.Birim))
                     .OrderBy(g => KategoriSira(g.Key.Kategori))
                     .ThenBy(g => g.Key.TeknikOzellik, StringComparer.OrdinalIgnoreCase))
        {
            var birlesik = new M13MetrajSatiri
            {
                AnaGrup = "Kablo",
                AltGrup = grup.Key.Kategori,
                TeknikOzellik = grup.Key.TeknikOzellik,
                Birim = grup.Key.Birim,
                Miktar = grup.Sum(g => g.Miktar)
            };
            foreach (var not in grup.SelectMany(g => g.Notlar))
                birlesik.Notlar.Add(not);
            satirlar.Add(birlesik);
        }
    }

    /// <summary>Eski kablo güzergâh etiketinden BOQ kategorisi türetir.</summary>
    private static string KabloKategorisi(string altGrup) =>
        altGrup.Contains("Kontrol", StringComparison.OrdinalIgnoreCase) ||
        altGrup.Contains("sinyal", StringComparison.OrdinalIgnoreCase) ? "Sinyal kablosu"
        : altGrup.Contains("Hücre", StringComparison.OrdinalIgnoreCase) ? "OG XLPE kablosu"
        : "Güç kablosu";

    private static int KategoriSira(string kategori) => kategori switch
    {
        "Güç kablosu" => 0,
        "OG XLPE kablosu" => 1,
        _ => 2
    };

    private static void OgXlpeKablosuEkle(List<M13MetrajSatiri> satirlar, string altGrup, OgXlpeKablo kablo)
    {
        if (kablo.UzunlukM <= 0) return;
        string tip = kablo.IletkenMalzemesi == OgKabloIletkenMalzemesi.Bakir ? "N2XSY Cu" : "NA2XSY Al";
        EkleMetraj(satirlar, "Kablo - Pano/Tesis", altGrup,
            $"{tip} 1x{kablo.KesitMm2:0.##} mm² XLPE", "m", kablo.UzunlukM * 3,
            $"{kablo.UzunlukM:0.##} m güzergâh x 3 faz");
    }

    private static void KorumaCihazlariniEkle(List<M13MetrajSatiri> satirlar, ElektrikProje proje)
    {
        foreach (var pano in proje.TumPanolar.Where(p => p.PanoTipi != PanoTipi.KontrolPanosu))
        {
            if (pano.GirisSigortaA > 0)
                KorumaCihaziEkle(satirlar, "Pano giriş koruması", pano.GirisSigortaA,
                    KorumaCihaziServisi.TmsGerekli(pano.GirisSigortaA),
                    KorumaCihaziServisi.KesmeKapasitesiSec(pano.GirisSigortaA, pano.KisaDevreAkimiKA));
            if (pano.PanoTipi == PanoTipi.AnaPano && pano.GirisSalteriA > 0)
                KorumaCihaziEkle(satirlar, "Ana pano giriş şalteri", pano.GirisSalteriA, true,
                    KorumaCihaziServisi.KesmeKapasitesiSec(pano.GirisSalteriA, pano.KisaDevreAkimiKA, true, null));

            foreach (var linye in pano.Linyeler.Where(l => l.SigortaAmper > 0))
                KorumaCihaziEkle(satirlar, "Linye çıkışı", linye.SigortaAmper, linye.KorumaCihaziTipi == "TMŞ",
                    linye.KesmeKapasitesi, linye.KorumaCihaziTipi == "Sigorta" ? linye.SigortaCinsi.ToString() : "");

            foreach (var alt in AltPanolar(proje, pano).Where(p => p.BaglantiTipi != BaglantiTipi.Busbar && p.CikisKorumaAkimA > 0))
                KorumaCihaziEkle(satirlar, "Pano çıkışı", alt.CikisKorumaAkimA,
                    alt.CikisKorumaCihaziTipi == "TMŞ", alt.CikisKesmeKapasitesi);

            if (pano.GirisBicakliSigorta is { } nh)
                EkleMetraj(satirlar, "Koruma Cihazı", "Kompanzasyon NH giriş",
                    $"{nh.Boy} {nh.AnmaAkimA}A {nh.Karakteristik} {nh.KesmeKapasitesiKA}kA", "adet", 1,
                    $"Kompanzasyon NH giriş ({pano.Ad})");
        }

        foreach (var busbar in proje.TumBusbarlar)
        foreach (var kutu in busbar.CikisKutulari.Where(k => k.SigortaA > 0))
        {
            var pano = proje.TumPanolar.FirstOrDefault(p =>
                string.Equals(p.Ad, kutu.BagliPanoAd, StringComparison.OrdinalIgnoreCase));
            bool tms = pano?.CikisKorumaCihaziTipi == "TMŞ" || KorumaCihaziServisi.TmsGerekli(kutu.SigortaA);
            string kesme = pano?.CikisKesmeKapasitesi ?? "";
            if (string.IsNullOrWhiteSpace(kesme))
                kesme = KorumaCihaziServisi.KesmeKapasitesiSec(kutu.SigortaA, pano?.UstPano?.KisaDevreAkimiKA ?? 0, tms, null);
            KorumaCihaziEkle(satirlar, "Busbar çıkış kutusu", kutu.SigortaA, tms, kesme);
        }

        var komp = proje.Kompanzasyon;
        if (komp == null) return;
        var kompPano = proje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.Kompanzasyon);
        double ik = kompPano?.KisaDevreAkimiKA ?? 0;
        foreach (var kademe in komp.KondansatorKademeleri)
            KompanzasyonKademeSigortasiEkle(satirlar, "Kondansatör kademe sigortası", kademe.KVar, proje.SistemGerilimV, ik);
        foreach (var kademe in komp.ReaktorKademeleri)
            KompanzasyonKademeSigortasiEkle(satirlar, "Reaktör kademe sigortası", kademe.KVar, proje.SistemGerilimV, ik);
    }

    private static void KompanzasyonKademeSigortasiEkle(List<M13MetrajSatiri> satirlar, string altGrup,
        double kvar, double hatV, double ik)
    {
        int amper = PanoHesapServisi.KondansatorKademeSigortaA(kvar, hatV);
        if (amper <= 0) return;
        bool tms = KorumaCihaziServisi.TmsGerekli(amper);
        KorumaCihaziEkle(satirlar, altGrup, amper, tms,
            KorumaCihaziServisi.KesmeKapasitesiSec(amper, ik, tms, null));
    }

    private static void KorumaCihaziEkle(List<M13MetrajSatiri> satirlar, string altGrup, int amper,
        bool tms, string? kesmeKapasitesi, string karakteristik = "")
    {
        string tip = tms ? "TMŞ" : "Sigorta";
        string teknik = string.Join(" ", new[] { tip, karakteristik, $"{amper}A", BosIseTire(kesmeKapasitesi) }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        // Güzergâh (giriş/çıkış/linye/busbar/kademe) nota düşer; satır cihaz tipine göre birleşir.
        EkleMetraj(satirlar, "Koruma Cihazı", altGrup, teknik, "adet", 1, altGrup);
    }

    /// <summary>
    /// Koruma cihazlarını (sigorta/TMŞ/NH) <b>cihaz tipi başına tek satırda</b> birleştirir: aynı cihaz
    /// (örn. "TMŞ 100A 36kA") giriş/çıkış/linye/busbar/kademe gibi farklı güzergâhlarda geçse de adetleri
    /// tek satırda toplanır. Kategori: TMŞ / otomatik sigorta / NH; güzergâhlar satır notuna düşer.
    /// <see cref="M13MetrajSatirlari"/>'nde koruma satırları eklendikten hemen sonra çağrılır.
    /// </summary>
    private static void KorumaCihaziSatirlariniBirlestir(List<M13MetrajSatiri> satirlar)
    {
        var koruma = satirlar.Where(s => s.AnaGrup == "Koruma Cihazı").ToList();
        if (koruma.Count == 0) return;
        satirlar.RemoveAll(s => s.AnaGrup == "Koruma Cihazı");

        foreach (var grup in koruma
                     .GroupBy(s => (s.TeknikOzellik, s.Birim))
                     .OrderBy(g => KorumaKategoriSira(g.Key.TeknikOzellik))
                     .ThenBy(g => g.Key.TeknikOzellik, StringComparer.OrdinalIgnoreCase))
        {
            var birlesik = new M13MetrajSatiri
            {
                AnaGrup = "Koruma Cihazı",
                AltGrup = KorumaKategorisi(grup.Key.TeknikOzellik),
                TeknikOzellik = grup.Key.TeknikOzellik,
                Birim = grup.Key.Birim,
                Miktar = grup.Sum(g => g.Miktar)
            };
            foreach (var not in grup.SelectMany(g => g.Notlar))
                birlesik.Notlar.Add(not);
            satirlar.Add(birlesik);
        }
    }

    private static string KorumaKategorisi(string teknik) =>
        teknik.StartsWith("TMŞ", StringComparison.OrdinalIgnoreCase) ? "TMŞ / kompakt şalter"
        : teknik.StartsWith("Sigorta", StringComparison.OrdinalIgnoreCase) ? "Otomatik sigorta (MCB)"
        : "NH bıçaklı sigorta";

    private static int KorumaKategoriSira(string teknik) =>
        teknik.StartsWith("TMŞ", StringComparison.OrdinalIgnoreCase) ? 0
        : teknik.StartsWith("Sigorta", StringComparison.OrdinalIgnoreCase) ? 1
        : 2;

    private static void BusbarMetrajlariniEkle(List<M13MetrajSatiri> satirlar, ElektrikProje proje)
    {
        foreach (var busbar in proje.TumBusbarlar.Where(b => b.ToplamUzunlukM > 0))
            EkleMetraj(satirlar, "Busbar", "Busbar hattı",
                BosIseTire(busbar.BusbarTipiAd), "m", busbar.ToplamUzunlukM, busbar.Ad);

        if (proje.OrtaGerilim?.CikisBaglanti is { Tip: TrafoCikisBaglantiTipi.Bar24 } bar24 && bar24.UzunlukM > 0)
            EkleMetraj(satirlar, "Busbar", "Trafo → Ana Pano Bar-24",
                BosIseTire(bar24.ReferansAd), "m", bar24.UzunlukM);

        foreach (var grup in proje.TumBusbarlar.SelectMany(b => b.CikisKutulari).GroupBy(k => k.SigortaA).OrderBy(g => g.Key))
            EkleMetraj(satirlar, "Busbar", "Çıkış kutusu", $"{grup.Key}A", "adet", grup.Count());
    }

    private static void PanoMetrajlariniEkle(List<M13MetrajSatiri> satirlar, ElektrikProje proje)
    {
        // Jeneratör sisteminin senkron/transfer panosu aşağıda kendi ekipman satırı olarak
        // yazılır. Aynı fiziksel panoyu burada ikinci kez genel pano metrajına katma.
        var jeneratorPanoAdlari = JeneratorSenkronPanoAdlari(proje);

        // Metraj kimliğine göre tek satırda topla: duvar tipi standart ön-yüz alanına (0,30 m²),
        // dikili tip derinliğe (90 cm) göre. Farklı ham gerekli alanlar aynı standart/derinliğe
        // oturuyorsa tek satırda birleşir (ör. 0,21 / 0,26 / 0,29 m² → "Duvar Tipi 0,30 m²").
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        foreach (var grup in proje.TumPanolar
                     .Where(p => !jeneratorPanoAdlari.Contains(p.Ad))
                     .GroupBy(PanoBoyutlandirmaServisi.MetrajAnahtari)
                     .OrderBy(g => g.Key.Govde).ThenBy(g => g.Key.StandartAlanM2).ThenBy(g => g.Key.DerinlikCm))
        {
            var anahtar = grup.Key;
            if (anahtar.Govde == PanoGovdeTipi.DikiliTip)
                EkleMetraj(satirlar, "Pano", "Dikili Tip", $"{anahtar.DerinlikCm} cm derinlik",
                    "adet", grup.Count(), "Dikili tip pano (derinlik bazında)");
            else
                EkleMetraj(satirlar, "Pano", "Duvar Tipi",
                    $"{anahtar.StandartAlanM2.ToString("0.00", tr)} m² standart ölçü",
                    "adet", grup.Count(), "Duvar tipi pano (standart ön-yüz alanı bazında)");
        }
    }

    private static void KompanzasyonMetrajlariniEkle(List<M13MetrajSatiri> satirlar, ElektrikProje proje)
    {
        var komp = proje.Kompanzasyon;
        var kompPanolari = proje.TumPanolar.Where(p => p.PanoTipi == PanoTipi.Kompanzasyon).ToList();
        if (komp != null)
        {
            foreach (var grup in komp.KondansatorKademeleri.GroupBy(k => new { k.KVar, k.FazTipi }))
                EkleMetraj(satirlar, "Kompanzasyon", "Kondansatör",
                    $"{grup.Key.KVar:0.##} kVAr {grup.Key.FazTipi}", "adet", grup.Count());
            foreach (var grup in komp.ReaktorKademeleri.GroupBy(r => new { r.KVar, r.AkortFrekansHz, r.ReaktorAd }))
                EkleMetraj(satirlar, "Kompanzasyon", "Reaktör",
                    $"{grup.Key.KVar:0.##} kVAr / {grup.Key.AkortFrekansHz:0.#} Hz", "adet", grup.Count(),
                    grup.Key.ReaktorAd);

            var surucu = komp.SecilenYukSurucu ?? kompPanolari.Select(p => p.SeciliSvcSurucu).FirstOrDefault(s => s != null);
            if (surucu != null)
                EkleMetraj(satirlar, "Kompanzasyon", "Yük sürücü",
                    $"{surucu.Ad} / {surucu.KademeGucKVar:0.##} kVAr / {surucu.AnmaAkimA}A",
                    "adet", 1, surucu.Aciklama);
        }

        // Eski projelerde kademe listesi bulunmayıp seçilen reaktör yalnız pano üzerinde kalabilir.
        if (komp?.ReaktorKademeleri.Count is null or 0)
        {
            foreach (var grup in kompPanolari.Where(p => p.SeciliReaktor != null)
                         .GroupBy(p => p.SeciliReaktor!.Ad, StringComparer.OrdinalIgnoreCase))
            {
                var reaktor = grup.First().SeciliReaktor!;
                EkleMetraj(satirlar, "Kompanzasyon", "Reaktör",
                    $"{reaktor.Ad} / {reaktor.GucKVar:0.##} kVAr / {reaktor.AkortFrekansHz:0.#} Hz",
                    "adet", grup.Count());
            }
        }

        foreach (var grup in kompPanolari.Where(p => p.SeciliKompanzasyonRolesi != null)
                     .GroupBy(p => new { p.SeciliKompanzasyonRolesi!.Ad, p.SeciliKompanzasyonRolesi.KademeSayisi }))
            EkleMetraj(satirlar, "Kompanzasyon", "Kompanzasyon rölesi",
                $"{grup.Key.Ad} / {grup.Key.KademeSayisi} kademe", "adet", grup.Count());
    }

    private static void JeneratorMetrajlariniEkle(List<M13MetrajSatiri> satirlar, ElektrikProje proje)
    {
        var gruplar = proje.JeneratorGruplari;
        var jeneretorler = gruplar.Count > 0
            ? gruplar.SelectMany(g => g.Jeneratorler).ToList()
            : proje.Jenerator == null ? new List<Jenerator>() : [proje.Jenerator];

        foreach (var grup in jeneretorler.GroupBy(j => new { j.JeneratorTanimAd, j.Mod }))
            EkleMetraj(satirlar, "Jeneratör Sistemi", "Jeneratör",
                $"{BosIseTire(grup.Key.JeneratorTanimAd)} / {grup.Key.Mod}", "adet", grup.Count());

        var senkronPanoAdlari = JeneratorSenkronPanoAdlari(proje).ToList();
        if (senkronPanoAdlari.Count > 0)
            EkleMetraj(satirlar, "Jeneratör Sistemi", "Senkron / Transfer panosu",
                "Senkron pano", "adet", senkronPanoAdlari.Count, string.Join(", ", senkronPanoAdlari));

        // Yeni grup alanı boş olan eski/ara sürüm projelerde şalter adı ünite üzerinde bulunabilir.
        // Bir grupta kaç jeneratör olursa olsun fiziksel transfer şalteri bir adettir.
        var salterler = gruplar.Count > 0
            ? gruplar.Select(g => !string.IsNullOrWhiteSpace(g.TransferSalterAd)
                ? g.TransferSalterAd
                : g.Jeneratorler.Select(j => j.TransferSalterAd).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? "")
            : proje.Jenerator == null ? [] : new[] { proje.Jenerator.TransferSalterAd };
        foreach (var grup in salterler.Where(s => !string.IsNullOrWhiteSpace(s)).GroupBy(s => s, StringComparer.OrdinalIgnoreCase))
            EkleMetraj(satirlar, "Jeneratör Sistemi", "Transfer şalteri", grup.Key, "adet", grup.Count());

        foreach (var grup in jeneretorler.GroupBy(j => BosIseTire(j.JeneratorTanimAd), StringComparer.OrdinalIgnoreCase))
        {
            EkleMetraj(satirlar, "Jeneratör Aksesuarı", "Egzoz sistemi", $"{grup.Key} egzoz seti", "set", grup.Count());
            EkleMetraj(satirlar, "Jeneratör Aksesuarı", "Yakıt tankı", $"{grup.Key} yakıt tankı", "adet", grup.Count());
            EkleMetraj(satirlar, "Jeneratör Aksesuarı", "Marş aküsü", $"{grup.Key} marş aküsü seti", "set", grup.Count());
        }
        foreach (var grup in jeneretorler.Where(j => j.DisMekandaMi)
                     .GroupBy(j => BosIseTire(j.JeneratorTanimAd), StringComparer.OrdinalIgnoreCase))
            EkleMetraj(satirlar, "Jeneratör Aksesuarı", "Jeneratör kabini",
                $"{grup.Key} dış mekân koruma kabini", "adet", grup.Count());
    }

    private static void KumandaVeOlcumMetrajlariniEkle(List<M13MetrajSatiri> satirlar, ElektrikProje proje)
    {
        foreach (var grup in proje.TumPanolar.Where(p => p.KacakAkimRolesiAktif && p.KacakAkimRolesi != null)
                     .GroupBy(p => p.KacakAkimRolesi!.Ad, StringComparer.OrdinalIgnoreCase))
        {
            var kar = grup.First().KacakAkimRolesi!;
            EkleMetraj(satirlar, "Koruma ve Kumanda", kar.TroidTipiMi ? "Troid kaçak akım rölesi" : "Kaçak akım rölesi",
                $"{kar.Ad} / {kar.AnmaAkimA}A / {kar.SensitiviteMa}mA / {kar.KutupSayisi}P", "adet", grup.Count());
        }
        foreach (var grup in proje.TumPanolar.Where(p => p.KacakAkimAkimTrafosu != null)
                     .GroupBy(p => p.KacakAkimAkimTrafosu!.Ad, StringComparer.OrdinalIgnoreCase))
        {
            var at = grup.First().KacakAkimAkimTrafosu!;
            EkleMetraj(satirlar, "Koruma ve Kumanda", "Troid akım trafosu",
                $"{at.Ad} / {at.PrimerA}/{at.SekonderA}A / Ø{at.IcCapMm} mm", "adet", grup.Count());
        }

        var linyeler = proje.TumPanolar.SelectMany(p => p.Linyeler).ToList();
        foreach (var grup in linyeler.Where(l => l.BireyselKar != null).GroupBy(l => l.BireyselKar!.Ad, StringComparer.OrdinalIgnoreCase))
            EkleMetraj(satirlar, "Koruma ve Kumanda", "Linye kaçak akım rölesi", grup.Key, "adet", grup.Count());
        foreach (var grup in linyeler.Where(l => l.Kontaktor != null).GroupBy(l => l.Kontaktor!.Ad, StringComparer.OrdinalIgnoreCase))
        {
            var k = grup.First().Kontaktor!;
            EkleMetraj(satirlar, "Koruma ve Kumanda", "Kontaktör",
                $"{k.Ad} / {k.AnmaAkimA}A / {k.KutupSayisi}P / {k.BobinGerilimV}V", "adet", grup.Count());
        }
        foreach (var grup in linyeler.Where(l => l.DarbeKesici != null).GroupBy(l => l.DarbeKesici!.Ad, StringComparer.OrdinalIgnoreCase))
        {
            var d = grup.First().DarbeKesici!;
            EkleMetraj(satirlar, "Koruma ve Kumanda", "Darbe akım anahtarı",
                $"{d.Ad} / {d.AnmaAkimA}A / {d.KutupSayisi}P", "adet", grup.Count());
        }
        foreach (var grup in proje.TumPanolar.Where(p => p.AstronomikZamanRolesi != null)
                     .GroupBy(p => p.AstronomikZamanRolesi!.Ad, StringComparer.OrdinalIgnoreCase))
            EkleMetraj(satirlar, "Koruma ve Kumanda", "Astronomik zaman rölesi", grup.Key, "adet", grup.Count());

        foreach (var grup in proje.TumPanolar.Where(p => p.SayacEkle || p.AydinlatmaSayacli)
                     .GroupBy(p => string.IsNullOrWhiteSpace(p.SayacTipi) ? "Trifaze sayaç" : p.SayacTipi, StringComparer.OrdinalIgnoreCase))
            EkleMetraj(satirlar, "Ölçüm", "Elektrik sayacı", grup.Key, "adet", grup.Count());

        foreach (var grup in proje.TumPanolar.Where(p => p.HarmonikFiltreliMi && p.SeciliHarmonikFiltre != null)
                     .GroupBy(p => p.SeciliHarmonikFiltre!.Ad, StringComparer.OrdinalIgnoreCase))
        {
            var f = grup.First().SeciliHarmonikFiltre!;
            EkleMetraj(satirlar, "Kompanzasyon", "Harmonik filtre",
                $"{f.Ad} / {f.Tipi} / {f.GucKVar:0.##} kVAr / {f.AnmaAkimA}A", "adet", grup.Count());
        }
    }

    private static void UpsMetrajlariniEkle(List<M13MetrajSatiri> satirlar, ElektrikProje proje)
    {
        foreach (var grup in proje.Upsler.GroupBy(u => new { u.UpsTanimAd, u.SeciliUpsGucKVA }))
        {
            EkleMetraj(satirlar, "UPS Sistemi", "UPS",
                $"{BosIseTire(grup.Key.UpsTanimAd)} / {grup.Key.SeciliUpsGucKVA:0.##} kVA", "adet", grup.Count());
            EkleMetraj(satirlar, "UPS Sistemi", "Bakım bypass şalteri",
                $"{BosIseTire(grup.Key.UpsTanimAd)} bakım bypass seti", "set", grup.Count());
        }
        foreach (var grup in proje.Upsler.Where(u => u.ToplamBlokSayisi > 0)
                     .GroupBy(u => u.AkuTanimAd, StringComparer.OrdinalIgnoreCase))
            EkleMetraj(satirlar, "UPS Sistemi", "Akü bloğu", BosIseTire(grup.Key), "adet",
                grup.Sum(u => u.ToplamBlokSayisi), $"UPS akü grubu; seri/paralel hesap sonucudur");
    }

    private static void OrtaGerilimEkipmanlariniEkle(List<M13MetrajSatiri> satirlar, ElektrikProje proje)
    {
        var og = proje.OrtaGerilim;
        if (og == null) return;
        if (!string.IsNullOrWhiteSpace(og.TrafoTanimAd))
            EkleMetraj(satirlar, "Orta Gerilim", "Trafo", og.TrafoTanimAd, "adet", 1);
        foreach (var grup in og.Hucreler.GroupBy(h => new { h.HucreTanimAd, h.HucreTipi }))
            EkleMetraj(satirlar, "Orta Gerilim", "OG hücresi",
                $"{BosIseTire(grup.Key.HucreTanimAd)} / {grup.Key.HucreTipi}", "adet", grup.Count());
        EkleMetraj(satirlar, "Orta Gerilim", "Köşk",
            $"{og.KoskTipi} / {og.KoskGenislikMm:0}x{og.KoskDerinlikMm:0}x{og.KoskYukseklikMm:0} mm", "adet", 1);
        if (og.CikisBaglanti.Tip != TrafoCikisBaglantiTipi.Kablo)
            EkleMetraj(satirlar, "Orta Gerilim", "Trafo AG bağlantı ekipmanı",
                $"{og.CikisBaglanti.Tip} / {BosIseTire(og.CikisBaglanti.ReferansAd)}", "set", 1);
    }

    private static void AksesuarMetrajlariniEkle(List<M13MetrajSatiri> satirlar, ElektrikProje proje)
    {
        int agBaglantiSeti = 0;
        int agMufSeti = 0;
        foreach (var p in proje.TumPanolar.Where(p => p.UstPano != null || !string.IsNullOrWhiteSpace(p.UstPanoAd)))
            if (p.KabloMesafeM > 0)
            {
                int kol = Math.Max(1, p.KabloParalelKolSayisi);
                agBaglantiSeti += 2 * kol;
                agMufSeti += AraEkSayisi(p.KabloMesafeM, 100) * kol;
            }
        foreach (var l in proje.TumPanolar.SelectMany(p => p.Linyeler).Where(l => l.KabloMesafeM > 0))
        {
            int kol = Math.Max(1, l.KabloParalelKolSayisi);
            agBaglantiSeti += 2 * kol;
            agMufSeti += AraEkSayisi(l.KabloMesafeM, 100) * kol;
        }
        if (proje.OrtaGerilim?.CikisBaglanti is { Tip: TrafoCikisBaglantiTipi.Kablo } ag && ag.UzunlukM > 0)
        {
            int kol = Math.Max(1, ag.HesaplananParalelKolSayisi);
            agBaglantiSeti += 2 * kol;
            agMufSeti += AraEkSayisi(ag.UzunlukM, 100) * kol;
        }
        var jenGruplari = proje.JeneratorGruplari;
        if (jenGruplari.Count > 0)
        {
            foreach (var grup in jenGruplari)
            {
                foreach (var jen in grup.Jeneratorler.Where(j => j.SenkronPanoKabloMesafeM > 0))
                {
                    int kol = Math.Max(1, jen.SenkronKabloParalelKolSayisi);
                    agBaglantiSeti += 2 * kol;
                    agMufSeti += AraEkSayisi(jen.SenkronPanoKabloMesafeM, 100) * kol;
                }
                if (grup.SenkronHedefPanoKabloMesafeM > 0)
                {
                    int kol = Math.Max(1, grup.SenkronHedefKabloParalelKolSayisi);
                    agBaglantiSeti += 2 * kol;
                    agMufSeti += AraEkSayisi(grup.SenkronHedefPanoKabloMesafeM, 100) * kol;
                }
            }
        }
        else if (proje.Jenerator is { } jen)
        {
            int unitKol = Math.Max(1, jen.SenkronKabloParalelKolSayisi);
            int hedefKol = Math.Max(1, jen.SenkronHedefKabloParalelKolSayisi);
            agBaglantiSeti += 2 * unitKol + 2 * hedefKol;
            agMufSeti += AraEkSayisi(jen.SenkronPanoKabloMesafeM, 100) * unitKol;
            agMufSeti += AraEkSayisi(jen.SenkronHedefPanoKabloMesafeM, 100) * hedefKol;
        }
        EkleMetraj(satirlar, "Kablo Aksesuarı", "AG kablo bağlantı seti", "Kablo pabucu + rakor + ısı büzüşmeli başlık", "set", agBaglantiSeti);
        EkleMetraj(satirlar, "Kablo Aksesuarı", "AG kablo mufu", "Her 100 m tambur boyu sonrası ara ek seti", "set", agMufSeti);

        int ogSegment = 0;
        if (proje.OrtaGerilim?.SebekeHucreXlpeKablosu.UzunlukM > 0) ogSegment++;
        if (proje.OrtaGerilim?.HucreTrafoXlpeKablosu.UzunlukM > 0) ogSegment++;
        EkleMetraj(satirlar, "Kablo Aksesuarı", "OG kablo başlığı", "3 faz XLPE kablo başlık takımı", "takım", ogSegment * 2);
        int ogMuf = proje.OrtaGerilim == null ? 0
            : AraEkSayisi(proje.OrtaGerilim.SebekeHucreXlpeKablosu.UzunlukM, 500) +
              AraEkSayisi(proje.OrtaGerilim.HucreTrafoXlpeKablosu.UzunlukM, 500);
        EkleMetraj(satirlar, "Kablo Aksesuarı", "OG kablo mufu", "3 faz XLPE ara ek takımı; 500 m tambur boyu", "takım", ogMuf);

        foreach (var busbar in proje.TumBusbarlar.Where(b => b.ToplamUzunlukM > 0))
        {
            EkleMetraj(satirlar, "Busbar Aksesuarı", "Askı/destek", busbar.BusbarTipiAd, "adet", Math.Ceiling(busbar.ToplamUzunlukM / 1.5), busbar.Ad);
            EkleMetraj(satirlar, "Busbar Aksesuarı", "Sonlandırma", busbar.BusbarTipiAd, "set", 2, busbar.Ad);
            EkleMetraj(satirlar, "Busbar Aksesuarı", "Ek modülü", busbar.BusbarTipiAd, "adet", Math.Max(0, Math.Ceiling(busbar.ToplamUzunlukM / 3.0) - 1), busbar.Ad);
        }

        EkleMetraj(satirlar, "Pano Aksesuarı", "Klemens", "Pano klemensi", "adet", proje.TumPanolar.Sum(p => p.KlemensSayisi));
        EkleMetraj(satirlar, "Pano Aksesuarı", "Topraklama barası", "Bakır pano topraklama barası", "adet", proje.TumPanolar.Count);
        EkleMetraj(satirlar, "Pano Aksesuarı", "Topraklama bağlantı seti", "Pano PE bağlantı iletkeni ve pabuç seti", "set", proje.TumPanolar.Count);
        EkleMetraj(satirlar, "Pano Aksesuarı", "Sinyal lambası", "R-S-T faz gösterge lambası seti", "set", proje.TumPanolar.Count(p => p.PanoTipi != PanoTipi.KontrolPanosu));
        EkleMetraj(satirlar, "Pano Aksesuarı", "Ölçü cihazı", "Dijital pano multimetresi", "adet", proje.TumPanolar.Count(p => p.PanoTipi is PanoTipi.AnaPano or PanoTipi.TaliPano or PanoTipi.TransferPanosu));
        int kumandaliPano = proje.TumPanolar.Count(p => p.AstronomikZamanRolesi != null || p.Linyeler.Any(l => l.Kontaktor != null || l.DarbeKesici != null));
        EkleMetraj(satirlar, "Pano Aksesuarı", "Yardımcı röle", "Pano yardımcı kumanda rölesi", "adet", kumandaliPano);
    }

    private static int AraEkSayisi(double uzunlukM, double tamburBoyuM) =>
        uzunlukM <= 0 || tamburBoyuM <= 0 ? 0 : Math.Max(0, (int)Math.Ceiling(uzunlukM / tamburBoyuM) - 1);

    private static HashSet<string> JeneratorSenkronPanoAdlari(ElektrikProje proje)
    {
        var adlar = proje.JeneratorGruplari.Count > 0
            ? proje.JeneratorGruplari.Select(g => g.SenkronPanoAd)
            : proje.Jenerator == null ? [] : new[] { proje.Jenerator.TransferPanoAd };
        return adlar.Where(a => !string.IsNullOrWhiteSpace(a))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string KabloTekKolEtiketi(string? kabloTipi, string? kesit, int paralelKol)
    {
        string temizKesit = (kesit ?? "").Trim();
        int acParantez = temizKesit.IndexOf("x(", StringComparison.OrdinalIgnoreCase);
        int kapaParantez = temizKesit.LastIndexOf(')');
        if (paralelKol > 1 && acParantez > 0 && kapaParantez > acParantez + 2)
        {
            string ic = temizKesit[(acParantez + 2)..kapaParantez];
            string sonEk = temizKesit[(kapaParantez + 1)..].Trim();
            temizKesit = string.IsNullOrWhiteSpace(sonEk) ? ic : $"{ic} {sonEk}";
        }
        return string.Join(" ", new[] { kabloTipi, temizKesit }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
    }

    private static void EkleMetraj(List<M13MetrajSatiri> satirlar, string anaGrup, string altGrup,
        string teknikOzellik, string birim, double miktar, string? aciklamaNotu = null)
    {
        if (miktar <= 0) return;
        anaGrup = MetrajMetniniNormalizeEt(anaGrup);
        altGrup = MetrajMetniniNormalizeEt(altGrup);
        teknikOzellik = MetrajMetniniNormalizeEt(teknikOzellik);
        birim = MetrajMetniniNormalizeEt(birim);
        var mevcut = satirlar.FirstOrDefault(s =>
            string.Equals(s.AnaGrup, anaGrup, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.AltGrup, altGrup, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.TeknikOzellik, teknikOzellik, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.Birim, birim, StringComparison.OrdinalIgnoreCase));
        if (mevcut == null)
        {
            mevcut = new M13MetrajSatiri
            {
                AnaGrup = anaGrup,
                AltGrup = altGrup,
                TeknikOzellik = teknikOzellik,
                Birim = birim
            };
            satirlar.Add(mevcut);
        }
        mevcut.Miktar += miktar;
        if (!string.IsNullOrWhiteSpace(aciklamaNotu)) mevcut.Notlar.Add(aciklamaNotu);
    }

    private static string MetrajMetniniNormalizeEt(string? metin) =>
        string.Join(' ', (metin ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void PanoSayfasiYaz(IXLWorksheet ws, ElektrikProje proje, Pano pano, int panoIndex)
    {
        ws.ShowGridLines = false;

        var baslik = ws.Range(1, 1, 1, SutunSayisi);
        baslik.Merge();
        ws.Cell(1, 1).Value = $"{pano.Ad} - Pano Çıkış Listesi";
        baslik.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE9DF");
        baslik.Style.Font.FontColor = XLColor.FromHtml("#173F2A");
        baslik.Style.Font.Bold = true;
        baslik.Style.Font.FontSize = 15;
        baslik.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        baslik.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 28;

        var ozet = ws.Range(2, 1, 2, SutunSayisi);
        ozet.Merge();
        ws.Cell(2, 1).Value = $"Kat: {BosIseTire(pano.Kat)}   |   Tip: {PanoTipiEtiketi(pano.PanoTipi)}   |   " +
                              $"Toplam Güç: {pano.ToplamGucW / 1000.0:0.##} kW   |   Giriş Akımı: {pano.GirisAkimA:0.##} A";
        ozet.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0EA");
        ozet.Style.Font.FontColor = XLColor.FromHtml("#254B34");
        ozet.Style.Font.Italic = true;
        ozet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(2).Height = 22;

        for (var col = 0; col < Basliklar.Length; col++)
            ws.Cell(3, col + 1).Value = Basliklar[col];

        var satirlar = CikisSatirlari(proje, pano).ToList();
        if (satirlar.Count == 0)
            satirlar.Add(new CikisSatiri { Aciklama = "Bu panoda raporlanacak çıkış bulunmuyor." });

        var row = 4;
        foreach (var satir in satirlar)
            SatirYaz(ws, row++, proje, pano, satir);

        var sonSatir = row - 1;
        ws.Range(1, 1, sonSatir, SutunSayisi).Style.Fill.BackgroundColor = XLColor.White;
        baslik.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE9DF");
        ozet.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0EA");

        var tablo = ws.Range(3, 1, sonSatir, SutunSayisi).CreateTable($"PanoRaporu{panoIndex}");
        tablo.Theme = XLTableTheme.None;
        tablo.ShowAutoFilter = true;
        tablo.ShowRowStripes = false;

        ws.Row(3).Height = 36;
        var tabloBasligi = ws.Range(3, 1, 3, SutunSayisi);
        tabloBasligi.Style.Fill.BackgroundColor = XLColor.FromHtml("#6F9E42");
        tabloBasligi.Style.Font.FontColor = XLColor.White;
        tabloBasligi.Style.Font.Bold = true;
        tabloBasligi.Style.Alignment.WrapText = true;
        tabloBasligi.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(3, 1, sonSatir, SutunSayisi).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Range(4, 4, sonSatir, 5).Style.NumberFormat.Format = "0.0";
        ws.Range(4, 9, sonSatir, 9).Style.NumberFormat.Format = "0";
        ws.Range(4, 13, sonSatir, 13).Style.NumberFormat.Format = "0.0";
        ws.Range(4, 6, sonSatir, 6).Style.Alignment.WrapText = true;
        ws.Range(4, 14, sonSatir, 14).Style.Alignment.WrapText = true;
        for (var dataRow = 4; dataRow <= sonSatir; dataRow++)
        {
            ws.Row(dataRow).Height = 32;
            ws.Range(dataRow, 1, dataRow, SutunSayisi).Style.Fill.BackgroundColor =
                dataRow % 2 == 0 ? XLColor.FromHtml("#EEF5E8") : XLColor.White;
        }

        var genislikler = new[] { 20d, 22d, 26d, 15d, 16d, 34d, 12d, 27d, 18d, 17d, 18d, 24d, 13d, 36d };
        for (var col = 1; col <= SutunSayisi; col++)
            ws.Column(col).Width = genislikler[col - 1];

        ws.SheetView.FreezeRows(3);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.SetRowsToRepeatAtTop(1, 3);
        ws.PageSetup.Margins.SetLeft(0.25).SetRight(0.25).SetTop(0.5).SetBottom(0.5);
    }

    private static IEnumerable<CikisSatiri> CikisSatirlari(ElektrikProje proje, Pano pano)
    {
        foreach (var linye in pano.Linyeler)
        {
            yield return new CikisSatiri
            {
                LinyeNo = linye.LinyeNo,
                Hedef = linye.YukTipiAd,
                SigortaA = linye.SigortaAmper > 0 ? linye.SigortaAmper : null,
                SigortaTipi = linye.KorumaCihaziTipi == "Sigorta"
                    ? $"Sigorta {linye.SigortaCinsi}"
                    : "TMŞ",
                KesmeKapasitesi = linye.KesmeKapasitesi,
                KabloKesiti = linye.KabloKesiti,
                MesafeM = linye.KabloMesafeM > 0 ? linye.KabloMesafeM : null,
                Aciklama = linye.Aciklama
            };
        }

        foreach (var altPano in AltPanolar(proje, pano))
        {
            var busbar = BagliBusbar(proje, altPano);
            yield return new CikisSatiri
            {
                Hedef = altPano.Ad,
                SigortaA = altPano.CikisKorumaAkimA > 0 ? altPano.CikisKorumaAkimA : null,
                SigortaTipi = altPano.CikisKorumaCihaziTipi,
                KesmeKapasitesi = altPano.CikisKesmeKapasitesi,
                KabloKesiti = altPano.KabloKesiti,
                MesafeM = altPano.KabloMesafeM > 0 ? altPano.KabloMesafeM : null,
                Aciklama = busbar == null
                    ? $"Alt pano beslemesi; {PanoTipiEtiketi(altPano.PanoTipi)}, kat {BosIseTire(altPano.Kat)}"
                    : $"Alt pano beslemesi; {busbar.Ad} çıkış kutusu, kat {BosIseTire(altPano.Kat)}"
            };
        }

        foreach (var grup in proje.JeneratorGruplari.Where(g =>
                     string.Equals(g.SenkronPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase)))
        {
            for (var i = 0; i < grup.Jeneratorler.Count; i++)
            {
                var jenerator = grup.Jeneratorler[i];
                yield return new CikisSatiri
                {
                    LinyeNo = $"JEN-{i + 1}",
                    Hedef = string.IsNullOrWhiteSpace(jenerator.JeneratorTanimAd)
                        ? $"Jeneratör {i + 1}"
                        : jenerator.JeneratorTanimAd,
                    SigortaTipi = "Jeneratör → Senkron",
                    KabloKesiti = jenerator.SenkronKabloKesiti,
                    MesafeM = jenerator.SenkronPanoKabloMesafeM,
                    Aciklama = $"Jeneratör {i + 1} güç kablosu"
                };
            }

            yield return new CikisSatiri
            {
                LinyeNo = "SENKRON",
                Hedef = grup.AnaPanoAd,
                SigortaTipi = "Senkron → Hedef Pano",
                KabloKesiti = grup.SenkronHedefKabloKesiti,
                MesafeM = grup.SenkronHedefPanoKabloMesafeM,
                Aciklama = "Senkron panodan transfer / ara / hedef panoya güç kablosu"
            };
        }

        if (pano.PanoTipi != PanoTipi.KontrolPanosu)
            yield break;

        foreach (var buton in pano.KontrolButonlari)
        {
            var sinyalKablosu = pano.KontrolSinyalKablolari.FirstOrDefault(k =>
                string.Equals(k.KaynakPanoAd, buton.KaynakPanoAd, StringComparison.OrdinalIgnoreCase));
            yield return new CikisSatiri
            {
                LinyeNo = buton.LinyeNo,
                Hedef = $"{buton.KaynakPanoAd} / Kontrol",
                SigortaA = buton.SigortaAmper > 0 ? buton.SigortaAmper : null,
                SigortaTipi = "Kontrol sinyali",
                KabloKesiti = sinyalKablosu == null
                    ? "Sinyal"
                    : $"{sinyalKablosu.DamarSayisi}x{sinyalKablosu.KesitMm2:0.##} mm² {sinyalKablosu.KabloTipi}",
                MesafeM = sinyalKablosu?.MetrajM > 0 ? sinyalKablosu.MetrajM : null,
                Aciklama = buton.LinyeAciklama
            };
        }
    }

    private static void SatirYaz(IXLWorksheet ws, int row, ElektrikProje proje, Pano pano, CikisSatiri satir)
    {
        ws.Cell(row, 1).Value = pano.Ad;
        ws.Cell(row, 2).Value = UstPanoEtiketi(proje, pano);
        MetinYaz(ws.Cell(row, 3), BeslemeEtiketi(proje, pano));
        if (BeslemeMesafesi(proje, pano) is { } beslemeMesafesi)
            ws.Cell(row, 4).Value = beslemeMesafesi;
        if (pano.PanoTipi != PanoTipi.KontrolPanosu && pano.GirisSigortaA > 0)
            ws.Cell(row, 5).Value = pano.GirisSigortaA;
        MetinYaz(ws.Cell(row, 6), KacakAkimEtiketi(pano));
        MetinYaz(ws.Cell(row, 7), satir.LinyeNo);
        MetinYaz(ws.Cell(row, 8), satir.Hedef);
        if (satir.SigortaA is { } sigortaA)
            ws.Cell(row, 9).Value = sigortaA;
        MetinYaz(ws.Cell(row, 10), satir.SigortaTipi);
        MetinYaz(ws.Cell(row, 11), satir.KesmeKapasitesi);
        MetinYaz(ws.Cell(row, 12), satir.KabloKesiti);
        if (satir.MesafeM is { } mesafeM)
            ws.Cell(row, 13).Value = mesafeM;
        MetinYaz(ws.Cell(row, 14), satir.Aciklama);
    }

    private static void MetinYaz(IXLCell cell, string? deger)
    {
        if (!string.IsNullOrWhiteSpace(deger))
            cell.Value = deger;
    }

    private static IEnumerable<Pano> AltPanolar(ElektrikProje proje, Pano pano)
    {
        var gorulen = new HashSet<Pano>();
        foreach (var alt in pano.AltPanolar.Concat(proje.TumPanolar.Where(p =>
                     p.UstPano == pano || string.Equals(p.UstPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase))))
        {
            if (gorulen.Add(alt))
                yield return alt;
        }
    }

    private static BusbarHatti? BagliBusbar(ElektrikProje proje, Pano pano)
    {
        if (pano.BaglantiTipi != BaglantiTipi.Busbar)
            return null;
        return proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId)
               ?? proje.TumBusbarlar.FirstOrDefault(b => b.CikisKutulari.Any(k =>
                   string.Equals(k.BagliPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase)));
    }

    private static string UstPanoEtiketi(ElektrikProje proje, Pano pano)
    {
        if (proje.JeneratorGruplari.Any(g =>
                string.Equals(g.SenkronPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase)))
            return "Jeneratör Grubu";
        var ustPanoAdi = pano.UstPano?.Ad ?? pano.UstPanoAd;
        if (!string.IsNullOrWhiteSpace(ustPanoAdi))
            return ustPanoAdi;
        if (pano.PanoTipi == PanoTipi.KontrolPanosu)
            return "Bağımsız kontrol panosu";
        return proje.OrtaGerilim != null && pano.PanoTipi == PanoTipi.AnaPano
            ? "OG / Trafo"
            : "Şebeke";
    }

    private static string BeslemeEtiketi(ElektrikProje proje, Pano pano)
    {
        if (proje.JeneratorGruplari.Any(g =>
                string.Equals(g.SenkronPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase)))
            return "Jeneratör güç kabloları";
        if (pano.PanoTipi == PanoTipi.KontrolPanosu)
            return "Sinyal kabloları";

        var kablo = string.Join(" ", new[] { pano.KabloTipi, pano.KabloKesiti }
            .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        var busbar = BagliBusbar(proje, pano);
        if (busbar != null)
            return string.IsNullOrWhiteSpace(kablo) ? busbar.Ad : $"{busbar.Ad} + {kablo}";

        if (pano.UstPano == null && string.IsNullOrWhiteSpace(pano.UstPanoAd) &&
            proje.OrtaGerilim?.CikisBaglanti is { } ogBaglanti && pano.PanoTipi == PanoTipi.AnaPano)
        {
            var referans = string.IsNullOrWhiteSpace(ogBaglanti.ReferansAd) ? kablo : ogBaglanti.ReferansAd;
            var kesit = string.IsNullOrWhiteSpace(ogBaglanti.KesitMm2) ? pano.KabloKesiti : ogBaglanti.KesitMm2;
            return string.Join(" ", new[] { referans, kesit }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        }

        return kablo;
    }

    private static double? BeslemeMesafesi(ElektrikProje proje, Pano pano)
    {
        if (pano.PanoTipi == PanoTipi.KontrolPanosu)
            return null;
        if (pano.UstPano != null || !string.IsNullOrWhiteSpace(pano.UstPanoAd))
            return pano.KabloMesafeM > 0 ? pano.KabloMesafeM : null;
        if (proje.OrtaGerilim?.CikisBaglanti is { } ogBaglanti && pano.PanoTipi == PanoTipi.AnaPano)
            return ogBaglanti.UzunlukM > 0 ? ogBaglanti.UzunlukM : null;
        return null;
    }

    private static string KacakAkimEtiketi(Pano pano)
    {
        if (pano.PanoTipi == PanoTipi.KontrolPanosu)
            return "";
        if (!pano.KacakAkimRolesiAktif)
            return "Kapalı";
        var kar = pano.KacakAkimRolesi;
        if (kar == null)
            return "Yok";
        if (!kar.TroidTipiMi)
            return $"{kar.Ad} | {kar.SensitiviteMa}mA | {kar.AnmaAkimA}A | {kar.KutupSayisi}P {kar.Tip}";
        var at = pano.KacakAkimAkimTrafosu;
        return at == null
            ? $"{kar.Ad} | {kar.SensitiviteMa}mA | Troid | Akım trafosu bulunamadı"
            : $"{kar.Ad} | {kar.SensitiviteMa}mA | Troid | AT {at.PrimerA}/{at.SekonderA}A Ø{at.IcCapMm}mm";
    }

    private static string BenzersizSayfaAdi(string panoAdi, int panoIndex, ISet<string> kullanilan)
    {
        var temiz = string.IsNullOrWhiteSpace(panoAdi) ? $"Pano {panoIndex}" : panoAdi.Trim();
        foreach (var karakter in new[] { '[', ']', ':', '*', '?', '/', '\\' })
            temiz = temiz.Replace(karakter, '-');
        temiz = temiz.Trim('\'', ' ');
        if (string.IsNullOrWhiteSpace(temiz))
            temiz = $"Pano {panoIndex}";
        if (temiz.Length > 31)
            temiz = temiz[..31];

        var aday = temiz;
        var sira = 2;
        while (!kullanilan.Add(aday))
        {
            var ek = $" ({sira++})";
            aday = temiz[..Math.Min(temiz.Length, 31 - ek.Length)] + ek;
        }
        return aday;
    }

    private static string PanoTipiEtiketi(PanoTipi tip) => tip switch
    {
        PanoTipi.AnaPano => "Ana Pano",
        PanoTipi.TaliPano => "Tali Pano",
        PanoTipi.Kompanzasyon => "Kompanzasyon Panosu",
        PanoTipi.AydinlatmaPanosu => "Aydınlatma Panosu",
        PanoTipi.TransferPanosu => "Transfer Panosu",
        PanoTipi.UpsPanosu => "UPS Panosu",
        PanoTipi.KontrolPanosu => "Kontrol Panosu",
        PanoTipi.YanginPanosu => "Yangın Panosu (ana şalter öncesi)",
        _ => tip.ToString()
    };

    private static string BosIseTire(string? deger) => string.IsNullOrWhiteSpace(deger) ? "-" : deger;

    private sealed class CikisSatiri
    {
        public string LinyeNo { get; init; } = "";
        public string Hedef { get; init; } = "";
        public int? SigortaA { get; init; }
        public string SigortaTipi { get; init; } = "";
        public string KesmeKapasitesi { get; init; } = "";
        public string KabloKesiti { get; init; } = "";
        public double? MesafeM { get; init; }
        public string Aciklama { get; init; } = "";
    }

    private sealed class M13MetrajSatiri
    {
        public string AnaGrup { get; init; } = "";
        public string AltGrup { get; init; } = "";
        public string TeknikOzellik { get; init; } = "";
        public string Birim { get; init; } = "";
        public double Miktar { get; set; }
        public List<string> Notlar { get; } = new();
    }
}
