using System.Text;
using static Kst.Acoustics.Core.Architecture.Reports.MimariRaporMetin;

namespace Kst.Acoustics.Core.Architecture.Reports;

/// <summary>
/// M37 mimari rapor modelini tek bir HTML belgesine render eder (mahal listesi, alan tablosu,
/// kapi-pencere ve finisaj cizelgeleri, on kontrol raporu).
/// </summary>
public static class MimariRaporHtmlBuilder
{
    public static string Olustur(MimariRaporu rapor)
    {
        ArgumentNullException.ThrowIfNull(rapor);

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"tr\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <title>Mimari Rapor</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: Arial, sans-serif; color: #1f2933; margin: 32px; }");
        sb.AppendLine("    h1, h2 { color: #0f3f4a; }");
        sb.AppendLine("    table { border-collapse: collapse; width: 100%; margin: 12px 0 28px; }");
        sb.AppendLine("    th, td { border: 1px solid #d7dee5; padding: 7px 9px; font-size: 13px; text-align: left; }");
        sb.AppendLine("    th { background: #edf5f7; }");
        sb.AppendLine("    .bos { color: #5f7080; font-style: italic; margin: 8px 0 24px; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.Append("  <h1>Mimari Rapor — ").Append(Encode(string.IsNullOrWhiteSpace(rapor.ProjeAdi) ? "Mimari Proje" : rapor.ProjeAdi)).AppendLine("</h1>");

        MahalListesi(sb, rapor.MahalListesi);
        AlanTablosu(sb, rapor.AlanTablosu);
        KapiPencere(sb, rapor.KapiPencereCizelgesi);
        Finisaj(sb, rapor.FinisajCizelgesi);
        OnKontrol(sb, rapor.OnKontrol);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void MahalListesi(StringBuilder sb, MahalListesiTablosu tablo)
    {
        sb.AppendLine("  <h2>Mahal Listesi</h2>");
        if (!tablo.VeriVar)
        {
            Bos(sb, "Mahal verisi bulunmuyor.");
            return;
        }

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Kat</th><th>No</th><th>Ad</th><th>Tip</th><th>İşlev</th><th>Adet</th><th>Alan</th><th>Hacim</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var s in tablo.Satirlar)
        {
            sb.AppendLine("      <tr>");
            Hucre(sb, Tamsayi(s.KatNo));
            Hucre(sb, s.MahalNo);
            Hucre(sb, s.Ad);
            Hucre(sb, Ad(s.MahalTipi));
            Hucre(sb, Ad(s.KullanimSinifi));
            Hucre(sb, Tamsayi(s.Adet));
            Hucre(sb, M2(s.AlanM2));
            Hucre(sb, M3(s.HacimM3));
            sb.AppendLine("      </tr>");
        }
        sb.Append("      <tr><th colspan=\"5\">Toplam</th>");
        sb.Append("<th>").Append(Encode(Tamsayi(tablo.ToplamMahalAdedi))).Append("</th>");
        sb.Append("<th>").Append(Encode(M2(tablo.ToplamAlanM2))).Append("</th>");
        sb.Append("<th>").Append(Encode(M3(tablo.ToplamHacimM3))).AppendLine("</th></tr>");
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void AlanTablosu(StringBuilder sb, AlanTablosu tablo)
    {
        sb.AppendLine("  <h2>Alan Tablosu</h2>");
        if (!tablo.VeriVar)
        {
            Bos(sb, "Alan verisi bulunmuyor.");
            return;
        }

        sb.AppendLine("  <table>");
        Satir(sb, "Net Alan", M2(tablo.NetAlanM2));
        Satir(sb, "Sirkülasyon Alanı", M2(tablo.SirkulasyonAlanM2));
        Satir(sb, "Teknik Alan", M2(tablo.TeknikAlanM2));
        Satir(sb, "Brüt Alan", M2(tablo.BrutAlanM2));
        Satir(sb, "Net Hacim", M3(tablo.NetHacimM3));
        Satir(sb, "Hedef Toplam Alan", M2(tablo.HedefToplamAlanM2));
        Satir(sb, "Hedef Farkı", M2(tablo.HedefFarkM2));
        Satir(sb, "Mahal Satırı", Tamsayi(tablo.MahalSayisi));
        Satir(sb, "Toplam Mahal Adedi", Tamsayi(tablo.ToplamMahalAdedi));
        sb.AppendLine("  </table>");

        if (tablo.KullanimKirilimi.Count == 0)
            return;

        sb.AppendLine("  <h2>Kullanım Sınıfı Kırılımı</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Kullanım Sınıfı</th><th>Mahal</th><th>Adet</th><th>Alan</th><th>Hacim</th><th>Oran</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var k in tablo.KullanimKirilimi)
        {
            sb.AppendLine("      <tr>");
            Hucre(sb, Ad(k.KullanimSinifi));
            Hucre(sb, Tamsayi(k.MahalSayisi));
            Hucre(sb, Tamsayi(k.ToplamAdet));
            Hucre(sb, M2(k.AlanM2));
            Hucre(sb, M3(k.HacimM3));
            Hucre(sb, Yuzde(k.AlanOrani));
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void KapiPencere(StringBuilder sb, KapiPencereCizelgesi cizelge)
    {
        sb.AppendLine("  <h2>Kapı-Pencere Çizelgesi</h2>");
        if (!cizelge.VeriVar)
        {
            Bos(sb, "Taslak açıklık verisi bulunmuyor.");
            return;
        }

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Poz</th><th>Tip</th><th>Ölçü (cm)</th><th>Adet</th><th>Yapı Elemanı Tipi</th><th>Malzeme</th><th>Toplam Alan</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var s in cizelge.Satirlar)
        {
            sb.AppendLine("      <tr>");
            Hucre(sb, s.Poz);
            Hucre(sb, s.Tip);
            Hucre(sb, s.Olcu);
            Hucre(sb, Tamsayi(s.Adet));
            Hucre(sb, s.YapiElemaniTipiAdi);
            Hucre(sb, string.Join(", ", s.MalzemeAdlari));
            Hucre(sb, M2(s.ToplamAlanM2));
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void Finisaj(StringBuilder sb, FinisajCizelgesi cizelge)
    {
        sb.AppendLine("  <h2>Finisaj Çizelgesi</h2>");
        if (!cizelge.VeriVar)
        {
            Bos(sb, "Finisaj verisi bulunmuyor.");
            return;
        }

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Kat</th><th>No</th><th>Mahal</th><th>Zemin</th><th>Duvar</th><th>Tavan</th><th>Not</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var s in cizelge.Satirlar)
        {
            sb.AppendLine("      <tr>");
            Hucre(sb, Tamsayi(s.KatNo));
            Hucre(sb, s.MahalNo);
            Hucre(sb, s.MahalAdi);
            Hucre(sb, s.ZeminTipiAdi);
            Hucre(sb, s.DuvarTipiAdi);
            Hucre(sb, s.TavanTipiAdi);
            Hucre(sb, s.Not);
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void OnKontrol(StringBuilder sb, OnKontrolRaporu rapor)
    {
        sb.AppendLine("  <h2>Ön Kontrol Raporu</h2>");
        sb.Append("  <p class=\"bos\">Bu bölüm bir ön kontroldür; kesin uygunluk beyanı içermez. ")
          .Append("Kritik: ").Append(Encode(Tamsayi(rapor.KritikSayisi)))
          .Append(" • Uyarı: ").Append(Encode(Tamsayi(rapor.UyariSayisi)))
          .Append(" • Kontrol edilemedi: ").Append(Encode(Tamsayi(rapor.KontrolEdilemediSayisi)))
          .AppendLine("</p>");

        if (!rapor.VeriVar)
        {
            Bos(sb, "Ön kontrol uyarısı bulunmuyor.");
            return;
        }

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Seviye</th><th>Kural Kodu</th><th>Kaynak</th><th>Mesaj</th><th>Çözüm Önerisi</th><th>Etkilenen</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var s in rapor.Satirlar)
        {
            sb.AppendLine("      <tr>");
            Hucre(sb, Ad(s.Seviye));
            Hucre(sb, s.KuralKodu);
            Hucre(sb, s.KaynakKodu);
            Hucre(sb, s.Mesaj);
            Hucre(sb, s.CozumOnerisi);
            Hucre(sb, s.EtkilenenEleman);
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void Satir(StringBuilder sb, string etiket, string deger)
    {
        sb.Append("    <tr><th>").Append(Encode(etiket)).Append("</th><td>").Append(Encode(deger)).AppendLine("</td></tr>");
    }

    private static void Hucre(StringBuilder sb, string? deger)
    {
        sb.Append("        <td>").Append(Encode(deger)).AppendLine("</td>");
    }

    private static void Bos(StringBuilder sb, string mesaj)
    {
        sb.Append("  <p class=\"bos\">").Append(Encode(mesaj)).AppendLine("</p>");
    }
}
