using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma.Fotometri;

/// <summary>
/// IESNA LM-63 (.ies) fotometrik dosya ayrıştırıcı. Anahtar-kelime başlığı + TILT satırı + fotometrik
/// sayısal blok okunur. Işık şiddeti gerçek kandeladan cd/1000 lm'ye çevrilerek [C düzlemi][gama]
/// sırasıyla saklanır (LDT ile aynı model). Bu fazda TILT=NONE / TILT=&lt;dosya&gt; desteklenir.
/// </summary>
public static class IesParser
{
    public static FotometrikVeri DosyadanOku(string yol) => Ayristir(File.ReadAllText(yol));

    public static FotometrikVeri Ayristir(string icerik)
    {
        if (string.IsNullOrWhiteSpace(icerik))
            throw new InvalidOperationException("IES içeriği boş.");

        var satirlar = icerik.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string ad = "";
        int li = 0;
        bool tiltGorundu = false;

        for (; li < satirlar.Length; li++)
        {
            var satir = satirlar[li].Trim();
            if (satir.StartsWith("[LUMINAIRE]", StringComparison.OrdinalIgnoreCase))
                ad = satir.Substring("[LUMINAIRE]".Length).Trim();
            else if (ad.Length == 0 && satir.StartsWith("[LUMCAT]", StringComparison.OrdinalIgnoreCase))
                ad = satir.Substring("[LUMCAT]".Length).Trim();

            if (satir.StartsWith("TILT=", StringComparison.OrdinalIgnoreCase))
            {
                tiltGorundu = true;
                li++; // TILT satırını atla; INCLUDE bloğu bu fazda desteklenmiyor.
                break;
            }
        }
        if (!tiltGorundu)
            throw new InvalidOperationException("IES içinde TILT satırı bulunamadı.");

        var jetonlar = new Queue<string>();
        for (; li < satirlar.Length; li++)
            foreach (var p in satirlar[li].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                jetonlar.Enqueue(p);

        double NextD()
        {
            if (jetonlar.Count == 0)
                throw new InvalidOperationException("IES beklenenden kısa: jeton tükendi.");
            return double.Parse(jetonlar.Dequeue(), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        int NextInt() => (int)Math.Round(NextD());

        int lambaSayisi = NextInt();
        double lumenLamba = NextD();
        double carpan = NextD();
        int dikeySayi = NextInt(); // dikey açılar = gama
        int yatkSayi = NextInt();  // yatay açılar = C düzlemleri
        NextD();                   // fotometrik tip
        NextD();                   // birim tipi
        NextD();                   // genişlik
        NextD();                   // uzunluk
        NextD();                   // yükseklik

        NextD();                   // balast faktörü
        NextD();                   // gelecek / balast-lamba faktörü
        double guc = NextD();      // giriş gücü (W)

        var gAcilari = new double[dikeySayi];
        for (int k = 0; k < dikeySayi; k++)
            gAcilari[k] = NextD();
        var cAcilari = new double[yatkSayi];
        for (int k = 0; k < yatkSayi; k++)
            cAcilari[k] = NextD();

        double toplamAki = lambaSayisi * lumenLamba;
        var tablo = new double[yatkSayi][];
        for (int c = 0; c < yatkSayi; c++)
        {
            tablo[c] = new double[dikeySayi];
            for (int g = 0; g < dikeySayi; g++)
            {
                double gercekKandela = NextD() * carpan;
                tablo[c][g] = toplamAki > 0 ? gercekKandela * 1000.0 / toplamAki : 0;
            }
        }

        return new FotometrikVeri
        {
            Kaynak = FotometriKaynak.Ies,
            ArmaturAdi = ad,
            ToplamGucW = guc,
            ToplamAkiLm = toplamAki,
            LambaSayisi = lambaSayisi,
            Simetri = 0,
            CDuzlemAcilari = cAcilari,
            GamaAcilari = gAcilari,
            IsikSiddeti_cd_klm = tablo
        };
    }
}
