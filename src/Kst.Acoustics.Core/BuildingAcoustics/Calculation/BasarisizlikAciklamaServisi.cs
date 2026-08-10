using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public enum BasarisizlikNedeniKodu
{
    VeriEksik,
    LimitAcigi,
    BelirsizlikBandinda,
    BaskinOpakYol,
    BaskinAciklik,
    BaskinMenfez,
    BaskinYanalIletim,
    DarbeDosemePerformansi,
    DarbeYanalIletim,
    DisGurultuKriteri,
    IcGurultuKriteri,
    TesisatSeviyesi,
    Reverberasyon,
    DusukGuven,
}

public enum AciklamaOnceligi { Bilgi, Orta, Yuksek, Kritik }

public sealed record BasarisizlikNedeni(
    BasarisizlikNedeniKodu Kod,
    AciklamaOnceligi Oncelik,
    string Baslik,
    string Kanit,
    string OnerilenEylem,
    Guid? HedefId = null,
    double? EtkiYuzdesi = null);

public sealed record BasarisizlikAciklamasi(
    Guid SonucId,
    AkustikRaporSonucTuru SonucTuru,
    string Durum,
    string Ozet,
    RaporSatiri Sonuc,
    IReadOnlyList<BasarisizlikNedeni> Nedenler,
    string GirdiParmakIzi);

/// <summary>
/// Kanonik rapor satırını, enerji-yol analizi ve güvenilirlik kanıtlarıyla
/// açıklayan kural tabanlı servis. Çıktı yalnız "kaldı" için değil, veri eksik
/// ve belirsizlik bandındaki sonuçlar için de eyleme dönük neden verir.
/// </summary>
public sealed class BasarisizlikAciklamaServisi
{
    private readonly BinaAkustikDegerlendirmeServisi _degerlendirme = new();

    public BasarisizlikAciklamasi Acikla(
        BinaProje proje,
        Guid sonucId,
        AkustikRaporSonucTuru? sonucTuru = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var hesap = _degerlendirme.Hesapla(proje);
        var satir = hesap.SonucBul(sonucId, sonucTuru)
            ?? throw new KeyNotFoundException($"Açıklanacak M08 sonucu bulunamadı: {sonucId}");
        var nedenler = new List<BasarisizlikNedeni>();

        LimitVeBelirsizlik(satir, nedenler);
        switch (satir.SonucTuru)
        {
            case AkustikRaporSonucTuru.HavaSesi:
                HavaSesi(hesap.HesaplananProje, satir, nedenler);
                break;
            case AkustikRaporSonucTuru.DisCephe:
                DisCephe(hesap.HesaplananProje, satir, nedenler);
                break;
            case AkustikRaporSonucTuru.DarbeSesi:
                DarbeSesi(hesap.HesaplananProje, satir, nedenler);
                break;
            case AkustikRaporSonucTuru.TesisatSurekli:
            case AkustikRaporSonucTuru.TesisatKesikli:
                Tesisat(satir, nedenler);
                break;
            case AkustikRaporSonucTuru.Reverberasyon:
                Reverberasyon(satir, nedenler);
                break;
        }
        Guven(satir, nedenler);

        nedenler = nedenler
            .GroupBy(x => new { x.Kod, x.HedefId })
            .Select(x => x.First())
            .OrderByDescending(x => x.Oncelik)
            .ThenByDescending(x => x.EtkiYuzdesi ?? 0)
            .ToList();
        var ozet = satir.Durum switch
        {
            nameof(UyumDurumu.Gecti) => $"Sonuç uygun; marj {Marj(satir)}.",
            nameof(UyumDurumu.VeriEksik) => "Resmî uygunluk kararı üretilemedi; önce eksik girdiler tamamlanmalıdır.",
            _ => $"Sonuç limiti sağlamıyor; açık {Math.Abs(satir.MarjDb):F1} dB. En etkili nedenler aşağıda sıralandı.",
        };
        return new BasarisizlikAciklamasi(satir.SonucId, satir.SonucTuru,
            satir.Durum, ozet, satir, nedenler, hesap.GirdiParmakIzi);
    }

    private static void LimitVeBelirsizlik(RaporSatiri s, List<BasarisizlikNedeni> n)
    {
        if (s.Durum == nameof(UyumDurumu.VeriEksik) || double.IsNaN(s.MarjDb))
        {
            n.Add(new(BasarisizlikNedeniKodu.VeriEksik, AciklamaOnceligi.Kritik,
                "Uygunluk girdisi eksik", $"Hesap durumu VeriEksik. Kriter: {s.YonetmelikEk}",
                "Yapı elemanı, alan/hacim, mekan sınıfları ve gerekli spektrum verilerini tamamlayıp yeniden hesaplayın."));
            return;
        }
        if (s.MarjDb < 0)
            n.Add(new(BasarisizlikNedeniKodu.LimitAcigi, AciklamaOnceligi.Kritik,
                "Yönetmelik limiti sağlanmıyor",
                $"Hesaplanan {s.Deger:F1} {s.Birim}; limit {(s.DahaDusukDahaIyi ? "≤" : "≥")} {s.LimitDeger:F1}; marj {s.MarjDb:F1} dB.",
                "En baskın iletim yolunu iyileştirin ve çözümü kanonik hesapla yeniden doğrulayın."));
        if (Math.Abs(s.MarjDb) <= Math.Max(0.1, s.Belirsizlik))
            n.Add(new(BasarisizlikNedeniKodu.BelirsizlikBandinda, AciklamaOnceligi.Yuksek,
                "Sonuç hesap belirsizliği bandında",
                $"|Marj| {Math.Abs(s.MarjDb):F1} dB, beyan edilen hesap belirsizliği ±{s.Belirsizlik:F1} dB.",
                "En az belirsizlik kadar ek tasarım payı bırakın veya laboratuvar/saha verisiyle doğrulayın."));
    }

    private static void HavaSesi(BinaProje proje, RaporSatiri s, List<BasarisizlikNedeni> n)
    {
        var bolme = TumBolmeler(proje).FirstOrDefault(x => x.Id == s.SonucId);
        if (bolme is null) return;
        EksikBolmeVerileri([bolme], n);
        if (bolme.YapiElemani is null || bolme.Alan <= 0) return;
        var analiz = FrekansYolAnaliziServisi.AnalizEtHesaplanmisBolme(bolme);
        BaskinYoluEkle(analiz.BaskinYol, n);
    }

    private static void DisCephe(BinaProje proje, RaporSatiri s, List<BasarisizlikNedeni> n)
    {
        var mekan = proje.Katlar.SelectMany(k => k.Mekanlar)
            .FirstOrDefault(m => RaporSonucIdleri.DisCephe(m.Id) == s.SonucId);
        var cepheler = mekan?.Bolmeler.Where(b => b.IsDisCephe).ToList() ?? [];
        EksikBolmeVerileri(cepheler, n);
        if (proje.DisGurultuDb <= 0)
            n.Add(new(BasarisizlikNedeniKodu.VeriEksik, AciklamaOnceligi.Kritik,
                "Dış gürültü girdisi yok", "Lgag/Lden değeri pozitif değil.",
                "Ölçülmüş veya hesaplanmış dış gürültü seviyesini ve veri kaynağını girin."));
        else
        {
            var ek4Gerek = s.IcHedefDb is { } ic ? proje.DisGurultuDb - ic : double.NaN;
            n.Add(new(BasarisizlikNedeniKodu.DisGurultuKriteri,
                s.MarjDb < 0 ? AciklamaOnceligi.Yuksek : AciklamaOnceligi.Bilgi,
                "Cephe için iki kriter birlikte uygulanıyor",
                $"Dış seviye {proje.DisGurultuDb:F1} dBA; EK-3/EK-4 birleşik hedef {s.LimitDeger:F1} dB" +
                (double.IsNaN(ek4Gerek) ? "." : $" (EK-4 gereği {ek4Gerek:F1} dB)."),
                "Cephe çözümünü yalnız opak Rw ile değil pencere, kapı, menfez ve iç gürültü toplamıyla doğrulayın."));
        }
        if (s.IcGurultuDb is { } icToplam && s.IcHedefDb is { } icHedef && icToplam > icHedef)
            n.Add(new(BasarisizlikNedeniKodu.IcGurultuKriteri, AciklamaOnceligi.Kritik,
                "İç gürültü hedefi aşılıyor", $"Spektral iç toplam {icToplam:F1} dBA; hedef ≤ {icHedef:F1} dBA.",
                "Zayıf cephe bileşenini iyileştirin veya dış gürültü spektrumunu ölçümle doğrulayın."));

        var zayif = cepheler.Where(b => b.YapiElemani is not null && b.Alan > 0)
            .OrderBy(HavaSesiHesapServisi.CalculateCompositeRw).FirstOrDefault();
        if (zayif is not null)
            BaskinYoluEkle(FrekansYolAnaliziServisi.AnalizEtHesaplanmisBolme(zayif).BaskinYol, n);
    }

    private static void DarbeSesi(BinaProje proje, RaporSatiri s, List<BasarisizlikNedeni> n)
    {
        var doseme = new KatlarArasiDarbeYoluServisi().DegerlendirilmisDarbeBolmeleri(proje)
            .FirstOrDefault(x => x.Id == s.SonucId)
            ?? TumBolmeler(proje).FirstOrDefault(x => x.Id == s.SonucId);
        if (doseme?.YapiElemani is not { } e)
        {
            n.Add(new(BasarisizlikNedeniKodu.VeriEksik, AciklamaOnceligi.Kritik,
                "Döşeme verisi bulunamadı", "Darbe yoluna atanmış döşeme kataloğu yok.",
                "Üst mekanın zemin yapı elemanını ve kat örtüşmesini kontrol edin."));
            return;
        }
        var direkt = e.Lnw - e.DeltaLw;
        var yanalArtis = doseme.ApparentLnw - direkt;
        n.Add(new(BasarisizlikNedeniKodu.DarbeDosemePerformansi,
            s.MarjDb < 0 ? AciklamaOnceligi.Yuksek : AciklamaOnceligi.Bilgi,
            "Döşeme ve kaplama darbe performansı",
            $"Çıplak Ln,w {e.Lnw:F1} dB; ΔLw {e.DeltaLw:F1} dB; doğrudan seviye {direkt:F1} dB.",
            e.DeltaLw <= 0
                ? "Yüzer döşeme/kaplama için katalog ΔLw ve dinamik rijitlik verisi tanımlayın."
                : "Daha düşük Ln,w veya daha yüksek doğrulanmış ΔLw değerli döşeme sistemini değerlendirin.",
            doseme.KaynakMekan?.Id));
        if (yanalArtis > 0.5)
            n.Add(new(BasarisizlikNedeniKodu.DarbeYanalIletim, AciklamaOnceligi.Yuksek,
                "Yanal darbe iletimi belirgin",
                $"Yanal yollar L'n,w değerini doğrudan yola göre yaklaşık {yanalArtis:F1} dB yükseltiyor.",
                "Alıcı odanın düşük Rw'li çevre duvarlarını ve döşeme-duvar birleşimlerini iyileştirin.",
                doseme.AliciMekan?.Id));
    }

    private static void Tesisat(RaporSatiri s, List<BasarisizlikNedeni> n)
    {
        if (s.Durum != nameof(UyumDurumu.Gecti))
            n.Add(new(BasarisizlikNedeniKodu.TesisatSeviyesi,
                s.Durum == nameof(UyumDurumu.VeriEksik) ? AciklamaOnceligi.Kritik : AciklamaOnceligi.Yuksek,
                "Tesisat gürültü düzeyi uygun değil",
                $"Girilen {s.Deger:F1} {s.Birim}; sınır ≤ {s.LimitDeger:F1}.",
                "Kaynak ses gücünü düşürün; susturucu, titreşim yalıtımı ve kanal kaplamasını birlikte değerlendirin."));
    }

    private static void Reverberasyon(RaporSatiri s, List<BasarisizlikNedeni> n)
    {
        if (s.Durum != nameof(UyumDurumu.Gecti))
            n.Add(new(BasarisizlikNedeniKodu.Reverberasyon,
                s.Durum == nameof(UyumDurumu.VeriEksik) ? AciklamaOnceligi.Kritik : AciklamaOnceligi.Yuksek,
                "Reverberasyon hedefi sağlanmıyor",
                $"Tahmini T {s.Deger:F2} s; hedef ≤ {s.LimitDeger:F2} s; hacim {s.HacimM3:F1} m³.",
                "Emilim alanını malzeme bazında tanımlayın ve tavan/duvar yutucu yüzeylerini artırın."));
    }

    private static void EksikBolmeVerileri(IEnumerable<Bolme> bolmeler, List<BasarisizlikNedeni> n)
    {
        foreach (var b in bolmeler)
        {
            if (b.YapiElemani is null)
                n.Add(new(BasarisizlikNedeniKodu.VeriEksik, AciklamaOnceligi.Kritik,
                    "Ana yapı elemanı atanmamış", $"{b.Ad} için opak yapı elemanı yok.",
                    "Katalogdan uygun tipte yapı elemanı atayın.", b.Id));
            if (b.Alan <= 0)
                n.Add(new(BasarisizlikNedeniKodu.VeriEksik, AciklamaOnceligi.Kritik,
                    "Bölme alanı geçersiz", $"{b.Ad} alanı {b.Alan:F2} m².",
                    "M50 geometrisini ve kat yüksekliğini doğrulayın.", b.Id));
            foreach (var a in b.Acikliklar.Where(x => x.YapiElemani is null && x.Alan > 0))
                n.Add(new(BasarisizlikNedeniKodu.VeriEksik, AciklamaOnceligi.Kritik,
                    "Açıklık elemanı atanmamış", $"{a.Tip} ({a.Alan:F2} m²) katalog verisi taşımıyor.",
                    "Pencere/kapı için doğrulanmış spektrumlu katalog kaydı atayın.", a.Id));
        }
    }

    private static void BaskinYoluEkle(FrekansIletimYolu? yol, List<BasarisizlikNedeni> n)
    {
        if (yol is null) return;
        var (kod, baslik, eylem) = yol.Tur switch
        {
            FrekansYolTuru.Aciklik => (BasarisizlikNedeniKodu.BaskinAciklik, "Açıklık baskın iletim yolu",
                "Açıklığın Rw/R(f) değerini yükseltin; alan ve sızdırmazlık detayını kontrol edin."),
            FrekansYolTuru.Menfez => (BasarisizlikNedeniKodu.BaskinMenfez, "Menfez baskın iletim yolu",
                "Daha yüksek Dn,e(f) değerli akustik menfez veya susturucu kullanın."),
            FrekansYolTuru.Df or FrekansYolTuru.Fd or FrekansYolTuru.Ff =>
                (BasarisizlikNedeniKodu.BaskinYanalIletim, "Yanal iletim baskın",
                    "İlgili birleşimin Kij/uzunluk verisini doğrulayın; flank elemanı ve elastik bağlantı detayını iyileştirin."),
            _ => (BasarisizlikNedeniKodu.BaskinOpakYol, "Opak ayırıcı baskın iletim yolu",
                "Daha yüksek R(f) değerli opak sistem veya doğrulanmış kaplama ΔR(f) uygulayın."),
        };
        n.Add(new(kod, AciklamaOnceligi.Yuksek, baslik,
            $"{yol.Ad}, bantlar ortalamasında toplam iletimin %{yol.OrtalamaKatkiYuzde:F0}'ini oluşturuyor; tepe katkı %{yol.EnYuksekKatkiYuzde:F0}.",
            eylem, yol.HedefId, yol.EnYuksekKatkiYuzde));
    }

    private static void Guven(RaporSatiri s, List<BasarisizlikNedeni> n)
    {
        if (s.GuvenPuani >= 70) return;
        n.Add(new(BasarisizlikNedeniKodu.DusukGuven,
            s.GuvenPuani < 40 ? AciklamaOnceligi.Yuksek : AciklamaOnceligi.Orta,
            "Hesap güvenilirliği düşük", $"Güven puanı {s.GuvenPuani}/100 ({s.GuvenSeviyesi}): {s.GuvenOzeti}",
            "Model/varsayılan verileri ölçüm veya üretici laboratuvar spektrumuyla değiştirin."));
    }

    private static string Marj(RaporSatiri s) => double.IsNaN(s.MarjDb) ? "—" : $"{s.MarjDb:+0.0;-0.0;0.0} dB";
    private static IEnumerable<Bolme> TumBolmeler(BinaProje proje) =>
        proje.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler);
}
