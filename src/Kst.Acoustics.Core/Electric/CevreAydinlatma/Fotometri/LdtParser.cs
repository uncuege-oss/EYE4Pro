using System;
using System.Globalization;
using System.IO;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma.Fotometri;

/// <summary>
/// EULUMDAT (.ldt) fotometrik dosya ayrıştırıcı. Projenin ihtiyacı olan alanları okur: armatür
/// adı, sistem gücü (balast dahil), toplam ışık akısı, lamba sayısı ve C-γ ışık şiddeti tablosu
/// (cd/1000 lm). EULUMDAT satır-bazlı, her değer kendi satırında. Tam simetri açılımı bu fazda
/// yapılmaz; dosyada saklanan düzlemler olduğu gibi korunur (Isym ile birlikte).
/// </summary>
public static class LdtParser
{
    public static FotometrikVeri DosyadanOku(string yol) => Ayristir(File.ReadAllText(yol));

    public static FotometrikVeri Ayristir(string icerik)
    {
        if (string.IsNullOrWhiteSpace(icerik))
            throw new InvalidOperationException("LDT içeriği boş.");

        var satirlar = icerik.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int i = 0;

        string Next()
        {
            if (i >= satirlar.Length)
                throw new InvalidOperationException("LDT beklenenden kısa: satır tükendi.");
            return satirlar[i++].Trim();
        }
        double NextD() => double.Parse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture);
        int NextInt() => (int)Math.Round(NextD());

        Next();                  // 1  şirket / format
        NextInt();               // 2  Ityp
        int isym = NextInt();    // 3  Isym (simetri)
        int mc = NextInt();      // 4  Mc (C düzlem sayısı)
        NextD();                 // 5  Dc
        int ng = NextInt();      // 6  Ng (gama sayısı)
        NextD();                 // 7  Dg
        Next();                  // 8  ölçüm rapor no
        string ad = Next();      // 9  armatür adı
        Next();                  // 10 armatür no
        Next();                  // 11 dosya adı
        Next();                  // 12 tarih / kullanıcı
        for (int k = 0; k < 9; k++)
            Next();              // 13..21 geometri (boy/en/yükseklik + ışıklı alan ölçüleri)
        NextD();                 // 22 DFF
        NextD();                 // 23 LORL
        NextD();                 // 24 dönüşüm faktörü
        NextD();                 // 25 tilt

        int setSayisi = NextInt(); // 26 lamba set sayısı
        int lambaSayisi = 0;
        double toplamAki = 0;
        double toplamGuc = 0;
        for (int s = 0; s < setSayisi; s++)
        {
            int adet = NextInt();  // set: lamba sayısı
            Next();                // tip
            double aki = NextD();  // toplam ışık akısı (lm)
            Next();                // renk sıcaklığı
            Next();                // renksel geri verim grubu
            double guc = NextD();  // balast dahil güç (W)
            lambaSayisi += adet;
            toplamAki += aki;
            toplamGuc += guc;
        }

        for (int k = 0; k < 10; k++)
            Next();              // 10 DR (oda indeksleri için doğrudan oranlar)

        var cAcilari = new double[mc];
        for (int k = 0; k < mc; k++)
            cAcilari[k] = NextD();
        var gAcilari = new double[ng];
        for (int k = 0; k < ng; k++)
            gAcilari[k] = NextD();

        int saklananDuzlem = isym switch
        {
            0 => mc,
            1 => 1,
            2 => mc / 2 + 1,
            3 => mc / 2 + 1,
            4 => mc / 4 + 1,
            _ => mc
        };

        var tablo = new double[saklananDuzlem][];
        for (int c = 0; c < saklananDuzlem; c++)
        {
            tablo[c] = new double[ng];
            for (int g = 0; g < ng; g++)
                tablo[c][g] = NextD();
        }

        return new FotometrikVeri
        {
            Kaynak = FotometriKaynak.Ldt,
            ArmaturAdi = ad,
            ToplamGucW = toplamGuc,
            ToplamAkiLm = toplamAki,
            LambaSayisi = lambaSayisi,
            Simetri = isym,
            CDuzlemAcilari = cAcilari,
            GamaAcilari = gAcilari,
            IsikSiddeti_cd_klm = tablo
        };
    }
}
