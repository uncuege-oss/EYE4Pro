using Kst.Acoustics.App.Electric.KesifHazirla;
using Kst.Acoustics.Core.Electric.KesifHazirla;

namespace Kst.Acoustics.Tests;

public static class KesifTopluPozTests
{
    public static void ExcelSatirlariVeAyriParaSimgesiOkunur()
    {
        var rows = KesifTopluPozServisi.PanoCoz("EÖZ-IS-001\tLIGHTING CONTROLLER\tAdet\t₺\t126.801,50\r\nEÖZ-IS-002\tVOLTAGE:0-240V/50-60HZ LAMP\tAdet\t73.719,50\tTL\r\n");
        var sonuc = KesifTopluPozServisi.Hazirla(SatirlaraCevir(rows), []);
        Dogrula(sonuc.HataliSatirSayisi == 0 && sonuc.Pozlar.Count == 2, "Excel satırları ve ayrı ₺/TL sütunu okunmalı.");
        Dogrula(sonuc.Pozlar[0].PozNo == "EÖZ-IS-001" && sonuc.Pozlar[0].BirimFiyat == 126801.50m && sonuc.Pozlar[1].BirimFiyat == 73719.50m, "Poz kodu ve kuruşlar korunmalı.");
    }

    public static void TirnakliCokSatirliHucreVeBaslikKorunur()
    {
        var rows = KesifTopluPozServisi.PanoCoz("POZ ADI\tPOZ TANIMI\tPOZ BİRİMİ\tPOZ FİYATI\r\nP1\t\"İlk satır\r\nİkinci\tsekme ve \"\"alıntı\"\"\"\tadet\t100\r\n\t\t\t\r\nP2\tLamba 4\" model\tadet\t0\r\n");
        Dogrula(rows.Count == 2 && rows[0][1] == "İlk satır\r\nİkinci\tsekme ve \"alıntı\"" && rows[1][1] == "Lamba 4\" model", "Başlık/boş satır atlanmalı; hücre içindeki satır sonu, sekme ve tırnak korunmalı.");
        var sonuc = KesifTopluPozServisi.Hazirla(SatirlaraCevir(rows), []);
        Dogrula(sonuc.Pozlar.Count == 2 && sonuc.Pozlar[1].BirimFiyat == 0, "Sıfır fiyat geçerli olmalı.");
    }

    public static void FiyatlarKulturdenBagimsizVeHataliMetinReddedilir()
    {
        foreach (var (text, expected) in new[] { ("₺ 126.801,50", 126801.50m), ("126,801.50 TL", 126801.50m), ("1 234,56", 1234.56m), ("1\u00A0234,56", 1234.56m), ("1234.56", 1234.56m), ("1,234", 1.234m), ("1.234", 1234m), ("0", 0m), ("2000000", 2000000m) })
            Dogrula(KesifTopluPozServisi.FiyatCoz(text, out var fiyat) && fiyat == expected, $"Fiyat doğru okunmalı: {text}");
        foreach (var text in new[] { "", "TL", "belirsiz", "12.34,56", "1,2,3", "999999999999999999999999999999999" })
            Dogrula(!KesifTopluPozServisi.FiyatCoz(text, out _), $"Hatalı fiyat sıfıra dönüşmemeli: {text}");
    }

    public static void TekrarEksikAlanVeNegatifFiyatAktarimiEngeller()
    {
        var rows = new[] { Satir("P1"), Satir(" p1 "), Satir("MEVCUT"), Satir("P3"), new KesifTopluPozSatiri(), Satir("P4"), Satir("P5") };
        rows[3].Fiyat = "-1"; rows[5].BirimAdi = ""; rows[6].Birim = "";
        var sonuc = KesifTopluPozServisi.Hazirla(rows, ["mevcut"]);
        Dogrula(sonuc.HataliSatirSayisi == 6 && sonuc.Pozlar.Count == 0, "Tek hata bile kısmi aktarımı engellemeli; boş satır hata sayılmamalı.");
        Dogrula(rows[0].Hata.Contains("tekrarlanıyor") && rows[2].Hata.Contains("zaten var") && rows[3].Hata.Contains("Fiyat"), "Hatalar ilgili satırda gösterilmeli.");
        rows[1].PozNo = "P2"; rows[2].PozNo = "YENİ"; rows[3].Fiyat = "0"; rows[5].BirimAdi = "Tanım"; rows[6].Birim = "adet";
        sonuc = KesifTopluPozServisi.Hazirla(rows, ["mevcut"]);
        Dogrula(sonuc.HataliSatirSayisi == 0 && sonuc.Pozlar.Count == 6 && rows.All(r => r.Hata.Length == 0), "Düzeltilen giriş yeniden doğrulanabilmeli.");
    }

    public static void PanoFazlaSutunVeBozukTirnagiReddeder()
    {
        HataBekle(() => KesifTopluPozServisi.PanoCoz("P1\tTanım\tadet\t100\tFazladan"));
        HataBekle(() => KesifTopluPozServisi.PanoCoz("P1\t\"Eksik tırnak\tadet\t100"));
        HataBekle(() => KesifTopluPozServisi.PanoCoz("P1\tTanım\tadet\t100", 1));
        var rows = KesifTopluPozServisi.PanoCoz("Tanım\tadet\t100\r\nİkinci\tm\t200", 1);
        Dogrula(rows.Count == 2 && rows[1].Length == 3, "Seçili hücreden kısmi sütun yapıştırma desteklenmeli.");
        var fiyat = KesifTopluPozServisi.PanoCoz("₺\t123,45", 3);
        Dogrula(fiyat.Single().Length == 1 && KesifTopluPozServisi.FiyatCoz(fiyat[0][0], out var tutar) && tutar == 123.45m, "Fiyat sütununa ayrı para simgesi yapıştırılabilmeli.");
    }

    public static void TopluEkleKutuphaneKayitVeDegisiklikIzlemeyeYansir()
    {
        using var ortam = new TestOrtami(); var vm = ortam.Vm;
        var mevcut = new KesifPoz { PozNo = "ESKİ", BirimAdi = "Eski poz", BirimFiyat = 42 };
        vm.Proje.PozKutuphane.Add(mevcut); // Eski proje uyumu: ManuelPozlar henüz boş.
        using var izleyici = new KesifProjeDegisiklikIzleyici(vm.Proje);
        var degisti = false; izleyici.Degisti += (_, _) => degisti = true;
        Dogrula(vm.TopluOzelPozEkle([Satir("YENİ-1"), Satir("YENİ-2")], out _), "Geçerli pozlar eklenmeli.");
        Dogrula(vm.OzelPozlar.Count == 3 && vm.Proje.ManuelPozlar.Count == 3 && degisti, "Kalıcı kaynak, ekran ve kaydedilmedi takibi güncellenmeli.");
        Dogrula(vm.SeciliOzelPoz?.PozNo == "YENİ-1" && vm.Proje.ManuelPozlar.Contains(mevcut), "İlk eklenen seçilmeli, eski özel poz korunmalı.");
        vm.OzelPozModuAktif = true;
        Dogrula(vm.FiltrelenmisPozKutuphane.Count == 3 && vm.FiltrelenmisAtamaPozlari.Any(p => p.PozNo == "YENİ-2"), "Ana liste ve atama adayları yeni pozları göstermeli.");
        var dosya = Path.Combine(ortam.Dizin, "test.e4p14");
        KesifJsonServisi.Kaydet(vm.Proje, dosya);
        var acilan = KesifJsonServisi.Ac(dosya);
        vm.ProjeYukle(acilan);
        Dogrula(acilan.ManuelPozlar.Count == 3 && acilan.PozKutuphane.Any(p => p.PozNo == "YENİ-2" && p.BirimFiyat == 100), "Kaydet/aç tüm özel pozları korumalı.");
    }

    public static void TopluEkleHatalardaProjeyiDegistirmez()
    {
        using var ortam = new TestOrtami(); var vm = ortam.Vm;
        var listePozu = new KesifPoz { PozNo = "ÇAKIŞAN", BirimAdi = "Liste pozu", BirimFiyat = 42 };
        vm.BirimFiyatListesiIceAktar("Liste", "test.xlsx", [listePozu], false);
        vm.Proje.BirimFiyatListeleri.Single().AktifMi = false;
        vm.PozKutuphaneYenile();
        var once = KesifJsonServisi.ParmakIziOlustur(vm.Proje);
        var rows = new[] { Satir("YENİ"), Satir(" ÇAKIŞAN ") };
        Dogrula(!vm.TopluOzelPozEkle(rows, out _) && rows[1].Hata.Contains("zaten var"), "Pasif listedeki kod da çakışma sayılmalı.");
        Dogrula(once == KesifJsonServisi.ParmakIziOlustur(vm.Proje), "Hatalı aktarım hiçbir kalıcı alanı değiştirmemeli.");
        Dogrula(!vm.TopluOzelPozEkle([new()], out _) && once == KesifJsonServisi.ParmakIziOlustur(vm.Proje), "Tamamen boş giriş projeyi değiştirmemeli.");
    }

    private static KesifTopluPozSatiri Satir(string kod) => new() { PozNo = kod, BirimAdi = "İmalat tanımı", Birim = "adet", Fiyat = "100" };
    private static IEnumerable<KesifTopluPozSatiri> SatirlaraCevir(IReadOnlyList<string[]> rows)
        => rows.Select(c => { var r = new KesifTopluPozSatiri(); for (var i = 0; i < c.Length; i++) r.HucreAyarla(i, c[i]); return r; });
    private static void Dogrula(bool kosul, string mesaj) { if (!kosul) throw new InvalidOperationException(mesaj); }
    private static void HataBekle(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Hatalı pano açık hata vermeli.");
    }
    private sealed class TestOrtami : IDisposable
    {
        public string Dizin { get; } = Path.Combine(Path.GetTempPath(), "m14-toplu-test-" + Guid.NewGuid().ToString("N"));
        public KesifHazirlaViewModel Vm { get; }
        public TestOrtami()
        {
            Directory.CreateDirectory(Dizin);
            Vm = new KesifHazirlaViewModel(new KesifPozEslemeServisi(Path.Combine(Dizin, "esleme.json")));
        }
        public void Dispose() => Directory.Delete(Dizin, true);
    }
}
