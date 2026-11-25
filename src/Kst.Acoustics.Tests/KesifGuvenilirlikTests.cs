using ClosedXML.Excel;
using Kst.Acoustics.App.Electric.KesifHazirla;
using Kst.Acoustics.Core.Electric.KesifHazirla;

namespace Kst.Acoustics.Tests;

public static class KesifGuvenilirlikTests
{
    public static void KayitIzleyiciKaliciIcerigiIzlerGorunumDegisiminiAtlar()
    {
        using var test = new Ortam(); var vm = test.Vm; var poz = test.Poz();
        vm.SeciliPoz = poz; vm.PozuTabloyaEkleCommand.Execute(null);
        var tablo = vm.SeciliTablo!; var satir = tablo.Satirlar.Single();
        using var izleyici = new KesifProjeDegisiklikIzleyici(vm.Proje);
        var degisiklik = 0; izleyici.Degisti += (_, _) => degisiklik++;
        var temiz = KesifJsonServisi.ParmakIziOlustur(vm.Proje);
        vm.AramaMetni = "aranan"; vm.PozAtamaAramaMetni = "poz";
        vm.HesaplariYenile(); tablo.ToplamiYenile(); vm.Proje.ToplamlariYenile();
        satir.ReferansBilgisi = "Geçici balon";
        Dogrula(degisiklik == 0 && temiz == KesifJsonServisi.ParmakIziOlustur(vm.Proje), "Seçim/arama/toplam/balon değişimi kaydedilmedi göstermemeli.");
        void DegistiMi(Action islem, string alan)
        {
            var once = degisiklik; islem();
            Dogrula(degisiklik > once, $"Kalıcı {alan} değişimi izlenmeli.");
        }
        DegistiMi(() => satir.Miktar = 7, "miktar");
        DegistiMi(() => satir.BirimFiyat = 432, "fiyat");
        DegistiMi(() => tablo.TamamlandiMi = true, "tamamlandı tiki");
        DegistiMi(() => tablo.Ad = "Yeni tablo adı", "tablo adı");
        DegistiMi(() => vm.Proje.Bilgiler.Isveren = "İşveren", "proje künyesi");
        DegistiMi(() => vm.BirimFiyatListeleri.Single().AktifMi = false, "fiyat listesi aktifliği");
        DegistiMi(() => poz.BirimAdi = "Yeni imalat adı", "liste pozu");
        Dogrula(temiz != KesifJsonServisi.ParmakIziOlustur(vm.Proje), "Kalıcı değişiklik eski kayıt parmak izinden farklı olmalı.");
    }

    public static void KayitIzleyiciYeniOgeleriIzlerSilinenleriBirakir()
    {
        using var test = new Ortam(); var vm = test.Vm; var proje = vm.Proje;
        using var izleyici = new KesifProjeDegisiklikIzleyici(proje);
        var degisiklik = 0; izleyici.Degisti += (_, _) => degisiklik++;
        void DegistiMi(Action islem, string alan)
        {
            var once = degisiklik; islem(); Dogrula(degisiklik > once, $"Yeni {alan} izlenmeli.");
        }
        var alt = new KesifTablosu();
        DegistiMi(() => proje.Tablolar.Single().AltTablolar.Add(alt), "alt tablo");
        DegistiMi(() => alt.Ad = "Alt", "alt tablo adı");
        var satir = new KesifSatir(); DegistiMi(() => alt.Satirlar.Add(satir), "satır");
        DegistiMi(() => satir.Miktar = 8, "satır miktarı");
        DegistiMi(() => satir.OtomatikKatkilar.Add(new() { KaynakReferansId = Guid.NewGuid(), Miktar = 2 }), "referans katkısı");
        DegistiMi(() => vm.OzelPozEkleCommand.Execute(null), "özel poz");
        var ozel = vm.SeciliOzelPoz!;
        DegistiMi(() => ozel.BirimAdi = "Özel poz ayrıntı düzenlemesi", "özel poz adı");
        DegistiMi(() => vm.SeciliOzelPozSilCommand.Execute(null), "özel poz silme");
        var once = degisiklik; ozel.BirimAdi = "Silinmiş poz";
        Dogrula(degisiklik == once, "Silinmiş özel poz artık projeyi kirletmemeli.");
        var referans = Referans("A", 10);
        DegistiMi(() => proje.ReferansDosyalari.Add(referans), "metraj referansı");
        DegistiMi(() => referans.MetrajSatirlari.Single().Miktar = 12, "metraj miktarı");
        proje.Tablolar.Single().AltTablolar.Remove(alt); once = degisiklik; satir.Miktar = 99;
        Dogrula(degisiklik == once, "Silinmiş tablo satırları artık izlenmemeli.");
        izleyici.Dispose(); once = degisiklik; proje.Ad = "İzleme sonlandı";
        Dogrula(degisiklik == once, "Belge değişince/kapanınca eski izleyici bırakılmalı.");
    }

    public static void TamamlananTablodaManuelMudahaleEngellenir()
    {
        using var test = new Ortam(); var vm = test.Vm; var poz = test.Poz();
        var tablo = vm.SeciliTablo!;
        vm.SeciliPoz = poz; vm.PozuTabloyaEkleCommand.Execute(null);
        vm.TabloEkleCommand.Execute(null); vm.SeciliTablo = tablo;
        var bildirim = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.SeciliTabloSaltOkunur)) bildirim = true; };
        vm.TabloTamamlanmaDurumuAyarla(tablo, true);
        Dogrula(bildirim && vm.SeciliTabloSaltOkunur && vm.SeciliTabloKilitliMi, "Tik düzenlemeyi hemen salt okunur yapmalı.");
        var once = KesifJsonServisi.ParmakIziOlustur(vm.Proje);
        vm.PozuTabloyaEkleCommand.Execute(null);
        vm.SeciliSatir = tablo.Satirlar.Single(); vm.SatirSilCommand.Execute(null);
        vm.TabloAdiniDegistir(tablo, "Değişmemeli"); vm.TabloSilCommand.Execute(null);
        vm.ReferansEkle(Referans("Eklenmemeli", 20));
        Dogrula(once == KesifJsonServisi.ParmakIziOlustur(vm.Proje), "Kilitli tablonun içeriği/adı/referansları değişmemeli veya tablo silinmemeli.");
        vm.TabloTamamlanmaDurumuAyarla(tablo, false);
        Dogrula(!vm.SeciliTabloSaltOkunur, "Tik kaldırılınca hücre düzenleme açılmalı.");
        vm.PozuTabloyaEkleCommand.Execute(null); Dogrula(tablo.Satirlar.Single().Miktar == 2, "Kilit açılınca ekleme çalışmalı.");
        vm.TabloAdiniDegistir(tablo, "Düzenlendi"); Dogrula(tablo.Ad == "Düzenlendi", "Kilit açılınca ad değişebilmeli.");
        vm.SeciliSatir = tablo.Satirlar.Single(); vm.SatirSilCommand.Execute(null);
        Dogrula(tablo.Satirlar.Count == 0, "Kilit açılınca silme çalışmalı.");
    }

    public static void UstTabloKilidiAltTablolariKorur()
    {
        using var test = new Ortam(); var vm = test.Vm; var ust = vm.SeciliTablo!;
        vm.AltTabloEkleCommand.Execute(null); var alt = vm.SeciliTablo!;
        vm.AltTabloEkleCommand.Execute(null); var torun = vm.SeciliTablo!;
        vm.TabloTamamlanmaDurumuAyarla(torun, true);
        vm.TabloTamamlanmaDurumuAyarla(ust, true);
        Dogrula(vm.SeciliTabloSaltOkunur && KesifTabloAgaci.KilitliMi(vm.Proje, alt), "Üst tablo tamamlanınca tüm alt seviyeler kilitlenmeli.");
        Dogrula(!vm.TabloTamamlanmaDurumuAyarla(torun, false) && torun.TamamlandiMi, "Üst kilit kalkmadan alt tik değişmemeli.");
        vm.SeciliTablo = alt; var once = alt.AltTablolar.Count;
        vm.AltTabloEkleCommand.Execute(null); vm.TabloAdiniDegistir(alt, "Engelli");
        Dogrula(alt.AltTablolar.Count == once && alt.Ad != "Engelli", "Üst kilit alt tablo eklemeyi/ad değişimini de engellemeli.");
        vm.TabloTamamlanmaDurumuAyarla(ust, false); vm.SeciliTablo = torun;
        Dogrula(vm.SeciliTabloKilitliMi, "Üst kilit kalkınca alt tablonun kendi tiki korunmalı.");
        vm.TabloTamamlanmaDurumuAyarla(torun, false);
        Dogrula(!vm.SeciliTabloSaltOkunur, "İlgili tikler kaldırılınca düzenleme açılmalı.");
        vm.SeciliTablo = alt; vm.TabloTamamlanmaDurumuAyarla(ust, true);
        Dogrula(vm.SeciliTabloKilitliMi, "Üst tik değişimi seçili alt tablonun kilit görünümünü anında güncellemeli.");
    }

    public static void KilitliAltTabloUstTabloSilinerekAsilamaz()
    {
        using var test = new Ortam(); var vm = test.Vm; var ust = vm.SeciliTablo!;
        vm.AltTabloEkleCommand.Execute(null); var alt = vm.SeciliTablo!;
        vm.TabloTamamlanmaDurumuAyarla(alt, true);
        vm.TabloEkleCommand.Execute(null); vm.SeciliTablo = ust;
        vm.TabloSilCommand.Execute(null);
        Dogrula(vm.Tablolar.Contains(ust) && ust.AltTablolar.Contains(alt), "Kilitli alt tabloyu içeren üst tablo silinmemeli.");
        vm.TabloTamamlanmaDurumuAyarla(alt, false); vm.TabloSilCommand.Execute(null);
        Dogrula(!vm.Tablolar.Contains(ust), "Alt tik kaldırılınca üst tablo silinebilmeli.");
    }

    public static void KilitliReferansVePozEslemesiDegistirilemez()
    {
        using var test = new Ortam(); var vm = test.Vm; var p1 = test.Poz(); var p2 = test.Poz("P2", 200, false);
        p2.BirimAdi = "4x16 mm² NHXMH kablo";
        var r = Referans("A", 10); vm.ReferansEkle(r); vm.EslemeAtaVeUygula(r.MetrajSatirlari[0], p1);
        var esleme = vm.PozEslemeleri.Single();
        var bekleyen = Referans("NHXMH 4x16 mm²", 20); vm.ReferansEkle(bekleyen);
        var tablo = vm.SeciliTablo!; vm.TabloTamamlanmaDurumuAyarla(tablo, true);
        var once = KesifJsonServisi.ParmakIziOlustur(vm.Proje);
        vm.ReferansSil(r); vm.EslemeGuncelle(esleme, p2); vm.EslemeSil(esleme);
        vm.EslemeAtaVeUygula(r.MetrajSatirlari[0], p2); vm.TumEslenmemisPozlariOtomatikAta();
        Dogrula(once == KesifJsonServisi.ParmakIziOlustur(vm.Proje) && test.Db.Tum().Single().PozNo == p1.PozNo, "Kilitli referans silme/eşleme değiştirme/otomatik atama projeyi ve öğrenme verisini değiştirmemeli.");
        vm.TabloEkleCommand.Execute(null); var diger = vm.SeciliTablo!;
        var acikRef = Referans("B", 7); vm.ReferansEkle(acikRef); vm.EslemeAtaVeUygula(acikRef.MetrajSatirlari[0], p2);
        Dogrula(tablo.Satirlar.Single().Miktar == 10 && tablo.Satirlar.Single().PozNo == "P1" && diger.Satirlar.Single().Miktar == 7, "Açık tabloda çalışma kilitli tabloyu yeniden işlememeli.");
        vm.TabloTamamlanmaDurumuAyarla(tablo, false); vm.EslemeGuncelle(esleme, p2);
        Dogrula(tablo.Satirlar.Single().PozNo == "P2", "Tik kaldırılınca eşleme değiştirilebilmeli.");
        vm.ReferansSil(r); Dogrula(!vm.ReferansDosyalari.Contains(r), "Tik kaldırılınca referans silinebilmeli.");
    }

    public static void KilitliTabloKaydetAcVeHesapYenilemedeKorunur()
    {
        using var test = new Ortam(); var proje = test.Vm.Proje; var ust = proje.Tablolar.Single();
        var alt = new KesifTablosu { Ad = "Kilitli alt" }; ust.AltTablolar.Add(alt); ust.TamamlandiMi = true;
        var refId = Guid.NewGuid();
        alt.Satirlar.Add(new() { PozNo = "P1", Miktar = 10, BirimFiyat = 100, OrijinalBirimFiyat = 100, KaynakTipi = KesifKaynakTipi.Otomatik, KaynakReferansId = refId });
        alt.Satirlar.Add(new() { PozNo = "P1", Miktar = 20, BirimFiyat = 200, OrijinalBirimFiyat = 200 });
        var once = KesifJsonServisi.ParmakIziOlustur(proje); test.Vm.HesaplariYenile();
        Dogrula(once == KesifJsonServisi.ParmakIziOlustur(proje) && alt.Satirlar.Count == 2, "Yenileme kilitli eski satırları birleştirmemeli veya katkı listesi eklememeli.");
        var path = test.Yol("kilitli.e4p14"); KesifJsonServisi.Kaydet(proje, path);
        test.Vm.ProjeYukle(KesifJsonServisi.Ac(path));
        var geri = test.Vm.Tablolar.Single().AltTablolar.Single(); test.Vm.SeciliTablo = geri;
        Dogrula(test.Vm.SeciliTabloSaltOkunur && geri.Satirlar.Count == 2 && geri.Toplam == 5000, "Yükleme kilidi ve özgün satırları korumalı.");
        test.Vm.TabloTamamlanmaDurumuAyarla(test.Vm.Tablolar.Single(), false); test.Vm.HesaplariYenile();
        Dogrula(!test.Vm.SeciliTabloSaltOkunur && geri.Satirlar.Count == 1 && Math.Abs(geri.Toplam - 5000) < 0.0000001m,
            $"Kilit kaldırılınca olağan tekilleştirme tutarı koruyarak çalışmalı: salt okunur={test.Vm.SeciliTabloSaltOkunur}, satır={geri.Satirlar.Count}, toplam={geri.Toplam:G29}.");
    }

    public static void TabloTamamlanmaDurumuManuelVeKalicidir()
    {
        using var test = new Ortam(); var proje = test.Vm.Proje;
        var ust = proje.Tablolar.Single();
        var alt = new KesifTablosu { Ad = "Alt tablo" };
        var torun = new KesifTablosu { Ad = "Alt-alt tablo" };
        torun.Satirlar.Add(new() { PozNo = "P1", Miktar = 3, BirimFiyat = 100 });
        alt.AltTablolar.Add(torun); ust.AltTablolar.Add(alt);
        var temiz = KesifJsonServisi.ParmakIziOlustur(proje);
        var bildirildi = false;
        alt.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(KesifTablosu.TamamlandiMi)) bildirildi = true; };
        alt.TamamlandiMi = true;
        Dogrula(bildirildi && temiz != KesifJsonServisi.ParmakIziOlustur(proje), "Tik renk bağlamasını ve kaydedilmemiş değişiklik denetimini güncellemeli.");
        Dogrula(!ust.TamamlandiMi && !torun.TamamlandiMi && proje.GenelToplam == 300, "Her tablo elle ve bağımsız işaretlenmeli; hesap etkilenmemeli.");
        var path = test.Yol("tamamlandi.e4p14"); KesifJsonServisi.Kaydet(proje, path);
        var geri = KesifJsonServisi.Ac(path).Tablolar.Single();
        Dogrula(!geri.TamamlandiMi && geri.AltTablolar.Single().TamamlandiMi && !geri.AltTablolar.Single().AltTablolar.Single().TamamlandiMi, "Ağaçtaki tüm seviyelerin tik durumu kaydet/aç ile korunmalı.");
        alt.TamamlandiMi = false; KesifJsonServisi.Kaydet(proje, path);
        Dogrula(!KesifJsonServisi.Ac(path).Tablolar.Single().AltTablolar.Single().TamamlandiMi, "Tik kaldırma da kalıcı olmalı.");
        File.WriteAllText(path, "{\"Tablolar\":[{\"Ad\":\"Eski tablo\"}]}");
        Dogrula(!KesifJsonServisi.Ac(path).Tablolar.Single().TamamlandiMi, "Eski dosyalarda tamamlanma varsayılanı işaretsiz olmalı.");
    }

    public static void AsenkronKayitGercektenYazilanSurumuDondurur()
    {
        using var test = new Ortam();
        var proje = test.Vm.Proje;
        proje.Ad = "Kayıt öncesi";
        var path = test.Yol("kayit.e4p14");
        var kayit = KesifJsonServisi.KaydetAsync(proje, path);
        proje.Ad = "Kayıt sırasında değişti";
        var yazilan = kayit.GetAwaiter().GetResult();
        Dogrula(KesifJsonServisi.Ac(path).Ad == "Kayıt öncesi", "Disk kayıt başlangıcındaki içeriği tutmalı.");
        Dogrula(yazilan != KesifJsonServisi.ParmakIziOlustur(proje), "Kayıt sırasındaki düzenleme kirli kalmalı.");
        var sonKayit = KesifJsonServisi.KaydetAsync(proje, path).GetAwaiter().GetResult();
        Dogrula(sonKayit == KesifJsonServisi.ParmakIziOlustur(proje), "Düzenleme olmayan kayıt temiz olmalı.");
        Dogrula(KesifJsonServisi.Ac(path + ".bak").Ad == "Kayıt öncesi", "Atomik yedek önceki sürümü korumalı.");
    }

    public static void EslemeDegisikligiReferanslarinTablolariniKorur()
    {
        using var test = new Ortam();
        var vm = test.Vm; var p = test.Poz(); var a = vm.SeciliTablo!;
        var r1 = Referans("A", 10); vm.ReferansEkle(r1); vm.EslemeAtaVeUygula(r1.MetrajSatirlari[0], p);
        vm.TabloEkleCommand.Execute(null); var b = vm.SeciliTablo!;
        var r2 = Referans("B", 20); vm.ReferansEkle(r2); vm.EslemeAtaVeUygula(r2.MetrajSatirlari[0], p);
        Dogrula(a.Satirlar.Single().Miktar == 10 && b.Satirlar.Single().Miktar == 20, "Referanslar ilk hedeflerinde kalmalı.");
        vm.EslemeGuncelle(vm.PozEslemeleri.First(), p);
        Dogrula(a.Satirlar.Single().Miktar == 10 && b.Satirlar.Single().Miktar == 20, "Tekrar işleme miktarı artırmamalı/taşımamalı.");
    }

    public static void BaslikVeyaBosSecimdeEslemeSatirKaybettirmez()
    {
        using var test = new Ortam();
        var vm = test.Vm; var p = test.Poz(); var leaf = vm.SeciliTablo!;
        var r = Referans("A", 10); vm.ReferansEkle(r); vm.EslemeAtaVeUygula(r.MetrajSatirlari[0], p);
        vm.TabloEkleCommand.Execute(null); var parent = vm.SeciliTablo!;
        vm.AltTabloEkleCommand.Execute(null); vm.SeciliTablo = parent;
        vm.EslemeGuncelle(vm.PozEslemeleri.Single(), p);
        Dogrula(leaf.Satirlar.Single().Miktar == 10, "Başlık seçimi mevcut katkıyı silmemeli.");
        vm.SeciliTablo = null; vm.EslemeGuncelle(vm.PozEslemeleri.Single(), p);
        Dogrula(leaf.Satirlar.Single().Miktar == 10, "Boş seçim mevcut katkıyı silmemeli.");
        vm.SeciliTablo = parent; vm.ReferansEkle(Referans("Reddedilen", 20));
        Dogrula(vm.ReferansDosyalari.Count == 1, "Başlık tablosuna yeni referans alınmamalı.");
    }

    public static void YenidenIslemeElleDuzenlenmisSatiriKorur()
    {
        using var test = new Ortam();
        var vm = test.Vm; var p = test.Poz(); var r = Referans("A", 10);
        vm.ReferansEkle(r); vm.EslemeAtaVeUygula(r.MetrajSatirlari[0], p);
        var satir = vm.SeciliTablo!.Satirlar.Single();
        satir.BirimFiyat = 777; satir.Miktar = 15; satir.Aciklama = "Elle not";
        vm.EslemeAtaVeUygula(Kalem("İlgisiz", 1), p);
        Dogrula(ReferenceEquals(satir, vm.SeciliTablo.Satirlar.Single()), "Mevcut satır nesnesi korunmalı.");
        Dogrula(satir.BirimFiyat == 777 && satir.OrijinalBirimFiyat == 100 && satir.Miktar == 15 && satir.Aciklama == "Elle not", "Elle düzenlemeler korunmalı.");
        test.Poz("P1", 999);
        vm.EslemeGuncelle(vm.PozEslemeleri.First(e => e.TeknikOzellik == "A"), vm.PozKutuphane.Single());
        Dogrula(satir.BirimFiyat == 777, "Yeni liste fiyatı eski keşif fiyatını ezmemeli.");
    }

    public static void BulunamayanOgrenilmisPozSifirFiyatUretmez()
    {
        using var test = new Ortam();
        var vm = test.Vm; var p = test.Poz(); var r = Referans("A", 10);
        vm.ReferansEkle(r); vm.EslemeAtaVeUygula(r.MetrajSatirlari[0], p);
        var satir = vm.SeciliTablo!.Satirlar.Single();
        vm.BirimFiyatListesiniPasifeAlCommand.Execute(null);
        Dogrula(vm.EslenmemisKalemler.Count == 1, "Kütüphaneden çözülemeyen öğrenilmiş poz sorunlu gösterilmeli.");
        var p2 = test.Poz("P2"); vm.EslemeAtaVeUygula(Kalem("Başka", 1), p2);
        Dogrula(ReferenceEquals(satir, vm.SeciliTablo.Satirlar.Single()) && satir.BirimFiyat == 100, "Çözülemeyen pozun mevcut bedeli korunmalı.");
        Dogrula(vm.DurumMesaji.Contains("bulunamadı"), "Kullanılmayan öğrenilmiş poz açıkça bildirilmeli.");
        vm.ReferansEkle(Referans("A", 20));
        Dogrula(vm.SeciliTablo.Satirlar.Count == 1 && satir.Miktar == 10, "Yeni referans eksik pozdan sıfır fiyatlı satır üretmemeli.");
    }

    public static void BosBirimliReferanstaEksikPozEskiBedeliKorur()
    {
        using var test = new Ortam(); var vm = test.Vm; var p = test.Poz(); var r = Referans("A", 10);
        r.MetrajSatirlari[0].Birim = "";
        vm.ReferansEkle(r); vm.EslemeAtaVeUygula(r.MetrajSatirlari[0], p);
        var satir = vm.SeciliTablo!.Satirlar.Single();
        var p2 = test.Poz("P2"); vm.EslemeAtaVeUygula(Kalem("Başka", 1), p2);
        Dogrula(ReferenceEquals(satir, vm.SeciliTablo.Satirlar.Single()) && satir.BirimFiyat == 100 && satir.Birim == "m", "Kaynak birimi boşken kullanılan poz birimi ve mevcut bedel korunmalı.");
    }

    public static void EslemeDegisinceYalnizIlgiliKatkiTasınir()
    {
        using var test = new Ortam();
        var vm = test.Vm; var p1 = test.Poz(); var p2 = test.Poz("P2", 200, false);
        vm.SeciliPoz = p1; vm.PozuTabloyaEkleCommand.Execute(null);
        var r1 = Referans("A", 10); var r2 = Referans("B", 20);
        vm.ReferansEkle(r1); vm.EslemeAtaVeUygula(r1.MetrajSatirlari[0], p1);
        vm.ReferansEkle(r2); vm.EslemeAtaVeUygula(r2.MetrajSatirlari[0], p1);
        vm.EslemeGuncelle(vm.PozEslemeleri.Single(e => e.TeknikOzellik == "A"), p2);
        var satirlar = vm.SeciliTablo!.Satirlar;
        Dogrula(satirlar.Single(s => s.PozNo == "P1").Miktar == 21 && satirlar.Single(s => s.PozNo == "P2").Miktar == 10, "Manuel/diğer referans katkısı yerinde kalmalı.");
        vm.ReferansSil(r1);
        Dogrula(satirlar.Count == 1 && satirlar.Single().Miktar == 21, "Silme yalnız taşınmış referansı düşürmeli.");
        vm.EslemeSil(vm.PozEslemeleri.Single(e => e.TeknikOzellik == "B"));
        Dogrula(satirlar.Single().Miktar == 1, "Eşleşme silme manuel katkıyı tutmalı.");
    }

    public static void ReferansHedefiJsonVeEskiProjedeKorunur()
    {
        using var test = new Ortam();
        var vm = test.Vm; var p = test.Poz(); var a = vm.SeciliTablo!;
        var r = Referans("A", 10); vm.ReferansEkle(r); vm.EslemeAtaVeUygula(r.MetrajSatirlari[0], p);
        var path = test.Yol("hedef.e4p14"); KesifJsonServisi.Kaydet(vm.Proje, path);
        Dogrula(KesifJsonServisi.Ac(path).ReferansDosyalari.Single().HedefTabloId == a.Id, "Referans hedefi JSON'da tutulmalı.");
        r.HedefTabloId = null; vm.TabloEkleCommand.Execute(null); vm.ProjeYukle(vm.Proje);
        Dogrula(r.HedefTabloId == a.Id, "Eski proje hedefi mevcut katkıdan bulunmalı.");
        vm.SeciliTablo = vm.Tablolar.Last(); vm.EslemeGuncelle(vm.PozEslemeleri.Single(), p);
        Dogrula(a.Satirlar.Single().Miktar == 10 && vm.SeciliTablo.Satirlar.Count == 0, "Eski referans yeni seçime taşınmamalı.");
    }

    public static void SilinenReferansHedefiBaskaTablodaDirilmez()
    {
        using var test = new Ortam();
        var vm = test.Vm; var p = test.Poz(); var a = vm.SeciliTablo!;
        var r = Referans("A", 10); vm.ReferansEkle(r); vm.EslemeAtaVeUygula(r.MetrajSatirlari[0], p);
        vm.TabloEkleCommand.Execute(null); var b = vm.SeciliTablo!;
        vm.SeciliTablo = a; vm.TabloSilCommand.Execute(null);
        vm.EslemeGuncelle(vm.PozEslemeleri.Single(), p);
        Dogrula(b.Satirlar.Count == 0 && vm.DurumMesaji.Contains("hedef yaprak tablo bulunamadı"), "Silinen tablo başka tabloya metraj taşımamalı.");
    }

    public static void OzelPozPenceresiKendiSeciminiSiler()
    {
        using var test = new Ortam(); var vm = test.Vm;
        vm.OzelPozModuAktif = true;
        vm.OzelPozEkleCommand.Execute(null); var a = vm.SeciliOzelPoz!;
        vm.OzelPozEkleCommand.Execute(null); var b = vm.SeciliOzelPoz!;
        vm.SeciliPoz = a; vm.SeciliOzelPoz = b; vm.SeciliOzelPozSilCommand.Execute(null);
        Dogrula(vm.OzelPozlar.Contains(a) && !vm.OzelPozlar.Contains(b), "Özel poz penceresindeki seçim silinmeli.");
        vm.SeciliPoz = a; vm.OzelPozSilCommand.Execute(null);
        Dogrula(vm.OzelPozlar.Count == 0, "Ana ekran komutu ana ekran seçimini silmeli.");
    }

    public static void PozAtamaAdaylariKutuphaneDegisiklikleriniIzler()
    {
        using var test = new Ortam(); var vm = test.Vm; var p = test.Poz();
        Dogrula(vm.FiltrelenmisAtamaPozlari.Single() == p, "İçe aktarılan poz atamada anında görünmeli.");
        vm.OzelPozEkleCommand.Execute(null); var ozel = vm.SeciliOzelPoz!;
        Dogrula(vm.FiltrelenmisAtamaPozlari.Contains(ozel), "Eklenen özel poz atamada görünmeli.");
        vm.SeciliOzelPozSilCommand.Execute(null);
        Dogrula(!vm.FiltrelenmisAtamaPozlari.Contains(ozel) && vm.AtanacakPoz != ozel, "Silinen poz atama listesinden ve seçimden çıkmalı.");
        vm.BirimFiyatListesiniPasifeAlCommand.Execute(null);
        Dogrula(vm.FiltrelenmisAtamaPozlari.Count == 0, "Pasif pozlar atama listesinden çıkmalı.");
    }

    public static void AyniAdliTablolarIcmaldeAyriKalir()
    {
        var proje = new KesifProje();
        foreach (var q in new[] { 10m, 20m }) proje.Tablolar.Add(new()
        { Ad = "Kat", Satirlar = new() { new() { PozNo = "P1", Birim = "m", Miktar = q, BirimFiyat = 100 } } });
        var satir = KesifHesapServisi.IcmalOlustur(proje).Single();
        Dogrula(satir.Kirilimlar.Count == 2 && satir.Kirilimlar.Select(k => k.TabloId).Distinct().Count() == 2 && satir.ToplamTutar == 3000, "Kırılım tablo kimliğine göre ayrılmalı.");
    }

    public static void TurkceBuyukBasliklarPozVeMetrajdaOkunur()
    {
        using var test = new Ortam(); var path = test.Yol("buyuk.csv");
        File.WriteAllText(path, "POZ NO;İMALATIN CİNSİ;BİRİM;BİRİM FİYATI\nP1;Kablo;m;123\n");
        var p = KesifPozImportServisi.DosyadanOku(path).Single();
        Dogrula(p.PozNo == "P1" && p.BirimAdi == "Kablo" && p.Birim == "m" && p.BirimFiyat == 123, "Türkçe büyük harfli başlıklar okunmalı.");
        path = test.Yol("metraj.xlsx");
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Metraj");
            var headers = new[] { "ANA GRUP", "ALT GRUP", "TEKNİK ÖZELLİK", "BİRİM", "MİKTAR" };
            for (var i = 0; i < headers.Length; i++) ws.Cell(7, i + 1).Value = headers[i];
            ws.Cell(8, 1).Value = "Kablo"; ws.Cell(8, 3).Value = "4x16"; ws.Cell(8, 4).Value = "m"; ws.Cell(8, 5).Value = 10;
            wb.SaveAs(path);
        }
        Dogrula(KesifMetrajImportServisi.DosyadanOku(path).MetrajSatirlari.Single().Miktar == 10, "Metraj normalizasyonu da aynı başlıkları okumalı.");
    }

    public static void YaklasikMaliyetCetveliYedinciSatirdanOkunur()
    {
        using var test = new Ortam(); var path = test.Yol("cetvel.xlsx");
        using (var wb = new XLWorkbook())
        {
            wb.AddWorksheet("Kapak").Cell(1, 1).Value = "Açıklama";
            var ws = wb.AddWorksheet("BF"); ws.Range("C3:G3").Merge().Value = "YAKLAŞIK MALİYET CETVELİ";
            var basliklar = new[] { "Sıra No", "Poz No", "İmalatın Cinsi", "Birim", "Birim Fiyatı" };
            for (var i = 0; i < basliklar.Length; i++) ws.Cell(7, i + 3).Value = basliklar[i];
            ws.Range("C10:G10").Merge().Value = "İş Grubu: Elektrik>Asansör";
            ws.Cell(11, 3).Value = 1; ws.Cell(11, 4).Value = "35.710.1101";
            ws.Cell(11, 5).Value = "Asansör"; ws.Cell(11, 6).Value = "Adet"; ws.Cell(11, 7).Value = 1107437.49;
            ws.Cell(12, 4).Value = "35.710.1150"; ws.Cell(12, 5).Value = "Alt grup poz"; ws.Cell(12, 7).Value = 0;
            ws.Range("C14:G14").Merge().Value = "GENEL TOPLAM";
            wb.SaveAs(path);
        }
        var pozlar = KesifPozImportServisi.DosyadanOku(path);
        Dogrula(pozlar.Count == 2 && pozlar[0].PozNo == "35.710.1101" && pozlar[0].BirimFiyat == 1107437.49m && pozlar[0].Kategori == "Elektrik>Asansör", "Cetvelin ofsetli gerçek kolonları ve grup bilgisi korunmalı.");
        Dogrula(pozlar[1].BirimFiyat == 0, "Dosyadaki gerçek sıfır fiyat korunmalı.");
    }

    public static void CokSatirliCsvTekKayitVeFiyatKorur()
    {
        using var test = new Ortam(); var path = test.Yol("quoted.csv");
        File.WriteAllText(path, "Poz No;Tanım;Birim;Birim Fiyat;Not\r\nP1;\"İlk satır\r\nİkinci; satır \"\"alıntı\"\"\";m;123;\"Not\nDevam\"\r\nP2;Diğer;m;456;\r\n");
        var rows = KesifPozImportServisi.DosyadanOku(path);
        Dogrula(rows.Count == 2 && rows[0].BirimFiyat == 123 && rows[1].BirimFiyat == 456, "Fiziksel satır sonu yeni poz oluşturmamalı.");
        Dogrula(rows[0].BirimAdi.Contains("İlk satır\r\nİkinci; satır \"alıntı\"") && rows[0].Aciklama == "Not\nDevam", "Tırnak, ayraç ve satır sonları korunmalı.");
    }

    public static void EksikBaslikVeBozukCsvAcikHataVerir()
    {
        using var test = new Ortam(); var path = test.Yol("bozuk.csv");
        File.WriteAllText(path, "Poz No;İmalat Adı\nP1;Kablo");
        HataBekle(() => KesifPozImportServisi.DosyadanOku(path));
        File.WriteAllText(path, "Poz No;Tanım;Birim;Fiyat\nP1;\"Kapanmamış;m;123");
        HataBekle(() => KesifPozImportServisi.DosyadanOku(path));
    }

    public static void TeknikAnmaDegeriUyusmayanPozReddedilir()
    {
        foreach (var (birim, kaynak, yanlis) in new[] { ("kVAr", "50", "5"), ("kVA", "100", "10"), ("kW", "15", "5"), ("Hz", "50", "60"), ("kV", "36", "24"), ("P", "4", "2") })
        {
            var kalem = new KesifMetrajSatiri { AnaGrup = "Kompanzasyon", AltGrup = "Kondansatör", TeknikOzellik = $"{kaynak} {birim} kondansatör", Birim = "adet" };
            var poz = new KesifPoz { PozNo = "Yanlış", BirimAdi = $"{yanlis} {birim} kondansatör", Birim = "adet", BirimFiyat = 100 };
            Dogrula(KesifOtomatikPozEslemeServisi.Esle([kalem], [poz]).Count == 0, $"Uyumsuz {birim} reddedilmeli.");
            poz.BirimAdi = $"{kaynak}.0 {birim} kondansatör";
            Dogrula(KesifOtomatikPozEslemeServisi.Esle([kalem], [poz]).Count == 1, $"Eşit {birim} ondalık yazımdan etkilenmemeli.");
        }
        var busbar = new KesifMetrajSatiri { AnaGrup = "Busbar", TeknikOzellik = "160 A busbar", Birim = "m" };
        var kucuk = new KesifPoz { PozNo = "80A", BirimAdi = "80 A busbar", Birim = "m", BirimFiyat = 100 };
        Dogrula(KesifOtomatikPozEslemeServisi.Esle([busbar], [kucuk]).Count == 0, "Düşük akımlı busbar metin benzerliğiyle kabul edilmemeli.");
    }

    public static void PozDegisinceKontrolOnayiKalkar()
    {
        using var test = new Ortam(); var vm = test.Vm;
        var p1 = test.Poz(); var p2 = test.Poz("P2", 200, false); var k = Kalem("A", 10);
        vm.EslemeAtaVeUygula(k, p1); var e = vm.PozEslemeleri.Single(); e.KontrolEdildi = true;
        vm.EslemeGuncelle(e, p1); Dogrula(e.KontrolEdildi, "Aynı hedefin tekrar kaydı onayı kaldırmamalı.");
        vm.EslemeGuncelle(e, p2); Dogrula(!e.KontrolEdildi, "Yeni hedef yeniden kontrol edilmeli.");
        e.KontrolEdildi = true; test.Db.EkleVeyaGuncelle(k, p1.PozNo, p1.BirimAdi);
        Dogrula(!e.KontrolEdildi && !new KesifPozEslemeServisi(test.DbPath).Tum().Single().KontrolEdildi, "Servis güncellemesi ve disk de onayı kaldırmalı.");
    }

    private static KesifMetrajSatiri Kalem(string teknik, decimal miktar)
        => new() { AnaGrup = "Kablo", AltGrup = "Güç", TeknikOzellik = teknik, Birim = "m", Miktar = miktar };
    private static KesifReferansDosyasi Referans(string teknik, decimal miktar)
        => new() { DosyaAdi = teknik + ".xlsx", KaynakModul = "M13", MetrajSatirlari = new() { Kalem(teknik, miktar) } };
    private static void Dogrula(bool kosul, string mesaj) { if (!kosul) throw new InvalidOperationException(mesaj); }
    private static void HataBekle(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Geçersiz içe aktarma açık hata vermeliydi.");
    }

    private sealed class Ortam : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "m14-guvenilirlik-" + Guid.NewGuid().ToString("N"));
        public string DbPath => Yol("esleme.json");
        public KesifPozEslemeServisi Db { get; }
        public KesifHazirlaViewModel Vm { get; }
        public Ortam()
        {
            Directory.CreateDirectory(_dir); Db = new KesifPozEslemeServisi(DbPath); Vm = new KesifHazirlaViewModel(Db);
        }
        public string Yol(string ad) => Path.Combine(_dir, ad);
        public KesifPoz Poz(string no = "P1", decimal fiyat = 100, bool digerleriniPasifeAl = true)
        {
            var p = new KesifPoz { PozNo = no, BirimAdi = "Kablo", Birim = "m", BirimFiyat = fiyat };
            Vm.BirimFiyatListesiIceAktar("Test " + no, "test.xlsx", [p], digerleriniPasifeAl); return p;
        }
        public void Dispose() => Directory.Delete(_dir, true);
    }
}
