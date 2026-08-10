using Kst.Acoustics.Core.BuildingAcoustics.Database;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

/// <summary>
/// Projedeki eksik ve uygunsuzluklardan kararlı anahtarlı iş kalemleri üretir.
/// Otomatik kalemler veri düzeldiğinde kendiliğinden kaybolur; yalnız kullanıcı durumu,
/// sorumlusu ve notu proje dosyasında saklanır.
/// </summary>
public sealed class AkustikIsListesiServisi
{
    public IReadOnlyList<AkustikIsKalemi> Uret(BinaProje proje, bool sonuclariDahilEt = true)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var liste = new List<AkustikIsKalemi>();
        var katMekanlar = proje.Katlar
            .SelectMany(k => k.Mekanlar.Select(m => (Kat: k, Mekan: m)))
            .ToList();

        if (katMekanlar.Count == 0)
            Ekle(liste, "PROJE:MEKAN-YOK", AkustikGorevKategorisi.Geometri,
                AkustikGorevOnceligi.Kritik, "Mekan bulunamadı",
                "Projede değerlendirilecek mekan yok.", "M50 geometrisini bağlayın veya kat planını içe aktarın.");

        bool cepheVar = false;
        foreach (var (kat, mekan) in katMekanlar)
        {
            string katId = KatKimligi(kat);
            if (mekan.Alan <= 0 || mekan.Yukseklik <= 0)
                Ekle(liste, $"MEKAN:{mekan.Id:N}:GEOMETRI", AkustikGorevKategorisi.Geometri,
                    AkustikGorevOnceligi.Kritik, "Mekan geometrisi eksik",
                    $"{mekan.Ad}: alan veya yükseklik sıfır/geçersiz.", "Alan ve kat yüksekliğini doğrulayın.",
                    katId, mekan.Id);
            if (string.IsNullOrWhiteSpace(mekan.KullanimTipi) || mekan.Sinif == MekanSinifi.Tanimsiz)
                Ekle(liste, $"MEKAN:{mekan.Id:N}:KULLANIM", AkustikGorevKategorisi.Siniflandirma,
                    AkustikGorevOnceligi.Yuksek, "Mekan kullanımını sınıflandırın",
                    $"{mekan.Ad}: yönetmelik kullanım tipi veya akustik sınıfı belirlenmedi.",
                    "Mekan kullanım tipini seçip kaynak/alıcı sınıfını doğrulayın.", katId, mekan.Id);
            if (string.IsNullOrWhiteSpace(mekan.KaynakDerecesi))
                Ekle(liste, $"MEKAN:{mekan.Id:N}:KAYNAK-SINIFI", AkustikGorevKategorisi.Siniflandirma,
                    AkustikGorevOnceligi.Normal, "Kaynak gürültü derecesi eksik",
                    $"{mekan.Ad}: OG/YG/DG derecesi seçilmedi.", "Kaynak derecesini doğrulayın.", katId, mekan.Id);
            if (string.IsNullOrWhiteSpace(mekan.AliciSinifi))
                Ekle(liste, $"MEKAN:{mekan.Id:N}:ALICI-SINIFI", AkustikGorevKategorisi.Siniflandirma,
                    AkustikGorevOnceligi.Normal, "Alıcı hassasiyet sınıfı eksik",
                    $"{mekan.Ad}: I/II/III sınıfı seçilmedi.", "Alıcı sınıfını doğrulayın.", katId, mekan.Id);
            if (mekan.ZeminYapiElemani is null)
                AtamaEkle(liste, mekan, katId, "ZEMIN", "Döşeme yapı elemanı atanmamış", YapiElemaniTipi.Doseme);
            else if (mekan.ZeminYapiElemani.Tip != YapiElemaniTipi.Doseme)
                YanlisTipEkle(liste, mekan, katId, null, mekan.ZeminYapiElemani, YapiElemaniTipi.Doseme, "döşeme");
            if (mekan.TavanYapiElemani is null)
                AtamaEkle(liste, mekan, katId, "TAVAN", "Tavan yapı elemanı atanmamış", YapiElemaniTipi.Tavan);
            else if (mekan.TavanYapiElemani.Tip != YapiElemaniTipi.Tavan)
                YanlisTipEkle(liste, mekan, katId, null, mekan.TavanYapiElemani, YapiElemaniTipi.Tavan, "tavan");

            if (mekan.EmilimAlani <= 0)
                Ekle(liste, $"MEKAN:{mekan.Id:N}:EMILIM", AkustikGorevKategorisi.Atama,
                    AkustikGorevOnceligi.Dusuk, "Emilim alanını doğrulayın",
                    $"{mekan.Ad}: reverberasyon hesabı varsayılan emilim kabulü kullanıyor.",
                    "Yüzeylerden toplam eşdeğer emilim alanını girin.", katId, mekan.Id);

            foreach (var bolme in mekan.Bolmeler)
            {
                cepheVar |= bolme.IsDisCephe;
                if (bolme.Alan <= 0)
                    Ekle(liste, $"BOLME:{bolme.Id:N}:ALAN", AkustikGorevKategorisi.Geometri,
                        AkustikGorevOnceligi.Kritik, "Bölme alanı geçersiz",
                        $"{mekan.Ad} / {bolme.Ad}: alan sıfır veya negatif.", "Geometri bağlantısını yenileyin.",
                        katId, mekan.Id, bolme.Id);
                if (bolme.YapiElemani is null)
                    Ekle(liste, $"BOLME:{bolme.Id:N}:ATAMA", AkustikGorevKategorisi.Atama,
                        AkustikGorevOnceligi.Kritik, "Bölme yapı elemanı atanmamış",
                        $"{mekan.Ad} / {bolme.Ad}: hesap yapılamaz.", "Uygun duvar/döşeme ürününü atayın.",
                        katId, mekan.Id, bolme.Id);
                else if (bolme.YapiElemani.Tip is not (YapiElemaniTipi.Duvar or YapiElemaniTipi.Doseme))
                    YanlisTipEkle(liste, mekan, katId, bolme, bolme.YapiElemani, YapiElemaniTipi.Duvar, "bölme");

                foreach (var aciklik in bolme.Acikliklar)
                {
                    var beklenen = aciklik.Tip == BolmeAcikligiTipi.Kapi
                        ? YapiElemaniTipi.Kapi : YapiElemaniTipi.Pencere;
                    if (aciklik.YapiElemani is null)
                        Ekle(liste, $"ACIKLIK:{aciklik.Id:N}:ATAMA", AkustikGorevKategorisi.Atama,
                            AkustikGorevOnceligi.Kritik, "Açıklık yapı elemanı atanmamış",
                            $"{mekan.Ad} / {bolme.Ad}: {aciklik.Tip} malzemesi yok.",
                            $"Bir {beklenen} ürünü atayın.", katId, mekan.Id, bolme.Id);
                    else if (aciklik.YapiElemani.Tip != beklenen)
                        Ekle(liste, $"ACIKLIK:{aciklik.Id:N}:TIP", AkustikGorevKategorisi.Atama,
                            AkustikGorevOnceligi.Yuksek, "Açıklık ürün tipi uyumsuz",
                            $"{aciklik.Tip} açıklığına {aciklik.YapiElemani.Tip} ürünü atanmış.",
                            $"{beklenen} tipinde ürün atayın.", katId, mekan.Id, bolme.Id,
                            aciklik.YapiElemani.Id);
                }
            }
        }

        if (cepheVar && !proje.DisGurultuKullaniciTarafindanDogrulandi)
            Ekle(liste, "PROJE:DIS-GURULTU-DOGRULAMA", AkustikGorevKategorisi.Olcum,
                AkustikGorevOnceligi.Kritik, "Dış gürültü girdisini doğrulayın",
                $"Cephe hesabı {proje.DisGurultuDb:F1} dBA varsayımını kullanıyor.",
                "Ölçüm/harita kaynağını belgeleyip dış gürültü girdisini onaylayın.");

        UrunBelgesiGorevleri(proje, liste);
        OlcumGorevleri(proje, liste);
        if (sonuclariDahilEt)
            SonucGorevleri(proje, liste);

        DurumlariUygula(proje, liste);
        ManuelGorevleriEkle(proje, liste);
        return liste
            .OrderByDescending(x => x.Oncelik)
            .ThenBy(x => x.Durum)
            .ThenBy(x => x.Baslik, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Anahtar, StringComparer.Ordinal)
            .ToList();
    }

    public void DurumKaydet(
        BinaProje proje, string anahtar, GorevDurumu durum,
        string sorumlu = "", DateTime? sonTarih = null, string not = "")
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentException.ThrowIfNullOrWhiteSpace(anahtar);
        proje.GorevDurumlari ??= new();
        var kayit = proje.GorevDurumlari.LastOrDefault(x =>
            !x.ManuelMi && string.Equals(x.Anahtar, anahtar, StringComparison.Ordinal));
        if (kayit is null)
        {
            kayit = new AkustikGorevKaydi { Anahtar = anahtar };
            proje.GorevDurumlari.Add(kayit);
        }
        kayit.Durum = durum;
        kayit.Sorumlu = sorumlu?.Trim() ?? "";
        kayit.SonTarih = sonTarih;
        kayit.Not = not?.Trim() ?? "";
        kayit.GuncellemeTarihi = DateTime.Now;
    }

    public AkustikGorevKaydi ManuelGorevEkle(
        BinaProje proje, string baslik, string aciklama = "",
        AkustikGorevKategorisi kategori = AkustikGorevKategorisi.Diger,
        AkustikGorevOnceligi oncelik = AkustikGorevOnceligi.Normal)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentException.ThrowIfNullOrWhiteSpace(baslik);
        var kayit = new AkustikGorevKaydi
        {
            Anahtar = $"MANUEL:{Guid.NewGuid():N}", ManuelMi = true,
            Baslik = baslik.Trim(), Aciklama = aciklama?.Trim() ?? "",
            Kategori = kategori, Oncelik = oncelik
        };
        proje.GorevDurumlari ??= new();
        proje.GorevDurumlari.Add(kayit);
        return kayit;
    }

    public int GecersizDurumlariTemizle(BinaProje proje)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var etkin = Uret(proje).Where(x => x.Otomatik).Select(x => x.Anahtar).ToHashSet(StringComparer.Ordinal);
        return proje.GorevDurumlari.RemoveAll(x => !x.ManuelMi && !etkin.Contains(x.Anahtar));
    }

    private static void SonucGorevleri(BinaProje proje, List<AkustikIsKalemi> liste)
    {
        var degerlendirme = new BinaAkustikDegerlendirmeServisi().Hesapla(proje);
        foreach (var satir in degerlendirme.Satirlar)
        {
            if (string.Equals(satir.Durum, nameof(UyumDurumu.VeriEksik), StringComparison.OrdinalIgnoreCase))
                Ekle(liste, $"SONUC:{satir.SonucId:N}:{satir.SonucTuru}:VERI", AkustikGorevKategorisi.Rapor,
                    AkustikGorevOnceligi.Kritik, "Hesap sonucu veri eksik",
                    $"{satir.Kat} / {satir.Mekan} / {satir.Bolme}: sonuç üretilemedi.",
                    "Eksik sınıflandırma, malzeme veya spektrum verisini tamamlayın.");
            else if (string.Equals(satir.Durum, nameof(UyumDurumu.Kaldi), StringComparison.OrdinalIgnoreCase))
                Ekle(liste, $"SONUC:{satir.SonucId:N}:{satir.SonucTuru}:KALDI", AkustikGorevKategorisi.Uygunsuzluk,
                    AkustikGorevOnceligi.Kritik, "Akustik limit sağlanmıyor",
                    $"{satir.Kat} / {satir.Mekan} / {satir.Bolme}: marj {satir.MarjDb:+0.0;-0.0;0.0}.",
                    "Yapı elemanını veya birleşim çözümünü iyileştirip yeniden hesaplayın.");
        }
    }

    private static void OlcumGorevleri(BinaProje proje, List<AkustikIsKalemi> liste)
    {
        var mekanIds = proje.Katlar.SelectMany(k => k.Mekanlar).Select(m => m.Id).ToHashSet();
        var bolmeIds = proje.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler).Select(b => b.Id).ToHashSet();
        foreach (var olcum in proje.SahaOlcumleri ?? [])
        {
            bool eslesmedi = olcum.BolmeId is { } b && !bolmeIds.Contains(b) ||
                             olcum.KaynakMekanId is { } k && !mekanIds.Contains(k) ||
                             olcum.AliciMekanId is { } a && !mekanIds.Contains(a) ||
                             olcum.BolmeId is null && olcum.KaynakMekanId is null &&
                             olcum.AliciMekanId is null && olcum.Tur != SahaOlcumTuru.DisGurultu;
            if (eslesmedi)
                Ekle(liste, $"OLCUM:{olcum.Id:N}:ESLEME", AkustikGorevKategorisi.Olcum,
                    AkustikGorevOnceligi.Yuksek, "Saha ölçümünü modele eşleyin",
                    $"{olcum.Ad}: geçerli bölme/mekan bağlantısı yok.", "Ölçümü ilgili mekan veya bölmeye bağlayın.");
            if (!olcum.RaporlamaIcinOnayli)
                Ekle(liste, $"OLCUM:{olcum.Id:N}:ONAY", AkustikGorevKategorisi.Olcum,
                    AkustikGorevOnceligi.Normal, "Saha ölçümünü gözden geçirin",
                    $"{olcum.Ad}: raporlama için doğrulanmadı.", "Belirsizlik ve cihaz bilgisini denetleyip onaylayın.");
        }
    }

    private static void UrunBelgesiGorevleri(BinaProje proje, List<AkustikIsKalemi> liste)
    {
        var elemanlar = proje.YapiElemaniKutuphanesi
            .Concat(proje.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m =>
                m.Bolmeler.Select(b => b.YapiElemani)
                    .Concat(m.Bolmeler.SelectMany(b => b.Acikliklar).Select(a => a.YapiElemani))
                    .Append(m.ZeminYapiElemani).Append(m.TavanYapiElemani)))
            .Where(e => e is not null).Cast<YapiElemani>()
            .GroupBy(e => e.Id == Guid.Empty ? M08KararliKimlik.YapiElemani(e) : e.Id)
            .Select(g => g.First());
        foreach (var eleman in elemanlar)
        {
            if (eleman.Belgeler is not { Count: > 0 })
            {
                Ekle(liste, $"URUN:{eleman.Id:N}:BELGE-YOK", AkustikGorevKategorisi.UrunBelgesi,
                    AkustikGorevOnceligi.Dusuk, "Ürün teknik belgesi eksik",
                    $"{eleman.Ad}: akustik test raporu veya teknik föy eklenmedi.",
                    "Üretici belgesini ürün kaydına ekleyin.", yapiElemaniId: eleman.Id);
                continue;
            }
            foreach (var belge in eleman.Belgeler)
            {
                if (belge.SuresiDolmus || (!string.IsNullOrWhiteSpace(belge.DosyaYolu) &&
                    !UrunBelgesiServisi.Dogrula(belge, out _)))
                    Ekle(liste, $"URUN:{eleman.Id:N}:BELGE:{belge.Id:N}", AkustikGorevKategorisi.UrunBelgesi,
                        AkustikGorevOnceligi.Yuksek, "Ürün belgesi geçersiz veya süresi dolmuş",
                        $"{eleman.Ad} / {belge.Ad}: belgeyi yenileyin.",
                        "Dosya bütünlüğünü ve geçerlilik tarihini doğrulayın.", yapiElemaniId: eleman.Id);
            }
        }
    }

    private static void DurumlariUygula(BinaProje proje, List<AkustikIsKalemi> liste)
    {
        var durumlar = (proje.GorevDurumlari ?? [])
            .Where(x => !x.ManuelMi && !string.IsNullOrWhiteSpace(x.Anahtar))
            .GroupBy(x => x.Anahtar, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.GuncellemeTarihi).First(), StringComparer.Ordinal);
        foreach (var kalem in liste)
        {
            if (!durumlar.TryGetValue(kalem.Anahtar, out var kayit)) continue;
            kalem.Durum = kayit.Durum;
            kalem.Sorumlu = kayit.Sorumlu;
            kalem.SonTarih = kayit.SonTarih;
            kalem.Not = kayit.Not;
        }
    }

    private static void ManuelGorevleriEkle(BinaProje proje, List<AkustikIsKalemi> liste)
    {
        foreach (var x in (proje.GorevDurumlari ?? []).Where(x => x.ManuelMi))
            liste.Add(new AkustikIsKalemi
            {
                Anahtar = x.Anahtar, Baslik = x.Baslik, Aciklama = x.Aciklama,
                Kategori = x.Kategori, Oncelik = x.Oncelik, Durum = x.Durum,
                KatKimligi = x.KatKimligi, MekanId = x.MekanId, BolmeId = x.BolmeId,
                YapiElemaniId = x.YapiElemaniId, Sorumlu = x.Sorumlu,
                SonTarih = x.SonTarih, Not = x.Not, Otomatik = false
            });
    }

    private static void AtamaEkle(List<AkustikIsKalemi> liste, Mekan mekan, string katId,
        string kod, string baslik, YapiElemaniTipi tip) =>
        Ekle(liste, $"MEKAN:{mekan.Id:N}:{kod}-ATAMA", AkustikGorevKategorisi.Atama,
            AkustikGorevOnceligi.Kritik, baslik, $"{mekan.Ad}: {tip} seçilmedi.",
            $"Kütüphaneden bir {tip} atayın.", katId, mekan.Id);

    private static void YanlisTipEkle(List<AkustikIsKalemi> liste, Mekan mekan, string katId,
        Bolme? bolme, YapiElemani eleman, YapiElemaniTipi beklenen, string yuzey) =>
        Ekle(liste, $"{yuzey.ToUpperInvariant()}:{bolme?.Id.ToString("N") ?? mekan.Id.ToString("N")}:TIP",
            AkustikGorevKategorisi.Atama, AkustikGorevOnceligi.Yuksek,
            "Yapı elemanı tipi uyumsuz", $"{mekan.Ad}: {yuzey} yüzeyine {eleman.Tip} atanmış.",
            $"{beklenen} tipinde ürün atayın.", katId, mekan.Id, bolme?.Id, eleman.Id);

    private static string KatKimligi(Kat kat) => string.IsNullOrWhiteSpace(kat.BinaGeometriKatId)
        ? kat.KatNo.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : kat.BinaGeometriKatId;

    private static void Ekle(List<AkustikIsKalemi> liste, string anahtar,
        AkustikGorevKategorisi kategori, AkustikGorevOnceligi oncelik,
        string baslik, string aciklama, string onerilenEylem,
        string katKimligi = "", Guid? mekanId = null, Guid? bolmeId = null, Guid? yapiElemaniId = null)
    {
        if (liste.Any(x => string.Equals(x.Anahtar, anahtar, StringComparison.Ordinal))) return;
        liste.Add(new AkustikIsKalemi
        {
            Anahtar = anahtar, Kategori = kategori, Oncelik = oncelik,
            Baslik = baslik, Aciklama = aciklama, OnerilenEylem = onerilenEylem,
            KatKimligi = katKimligi, MekanId = mekanId, BolmeId = bolmeId,
            YapiElemaniId = yapiElemaniId
        });
    }
}
