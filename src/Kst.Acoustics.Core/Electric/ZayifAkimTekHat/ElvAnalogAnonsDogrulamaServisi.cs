namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public enum ElvAnalogDogrulamaSeviyesi { Bilgi, Uyari, Hata }
public sealed record ElvAnalogDogrulamaMaddesi(ElvAnalogDogrulamaSeviyesi Seviye, string Kaynak, string Mesaj);
public sealed record ElvAnalogAnonsDogrulamaRaporu(IReadOnlyList<ElvAnalogDogrulamaMaddesi> Maddeler)
{
    public bool Gecerli => Maddeler.All(m => m.Seviye != ElvAnalogDogrulamaSeviyesi.Hata);
    public int HataSayisi => Maddeler.Count(m => m.Seviye == ElvAnalogDogrulamaSeviyesi.Hata);
    public int UyariSayisi => Maddeler.Count(m => m.Seviye == ElvAnalogDogrulamaSeviyesi.Uyari);
}

/// <summary>Analog acil anons topoloji, güç, ürün uygunluğu ve hat denetimini tek raporda birleştirir.</summary>
public static class ElvAnalogAnonsDogrulamaServisi
{
    public static ElvAnalogAnonsDogrulamaRaporu Dogrula(ElvAnonsSistemi sistem, double rezervYuzdesi = 20)
    {
        var maddeler = new List<ElvAnalogDogrulamaMaddesi>();
        if (sistem.AnalogZonlar.Count == 0)
            Hata("Sistem", "En az bir analog tahliye zonu tanımlanmalıdır.");

        foreach (var zon in sistem.AnalogZonlar)
        {
            var topoloji = ElvAnalogAnonsTopolojiServisi.Dogrula(zon);
            foreach (var sorun in topoloji.Sorunlar) Hata(zon.Ad, sorun);
            foreach (var hat in new[] { zon.HatA, zon.HatB }.Where(h => h is not null).Cast<ElvAnalogHoparlorHatti>())
            {
                foreach (var grup in hat.HoparlorGruplari)
                {
                    var tap = ElvAnalogHoparlorServisi.TapGucunuDogrula(grup);
                    if (!tap.Gecerli) Hata($"{zon.Ad} / Hat {hat.Kol}", tap.Mesaj);
                    if (!grup.En54_24Uygun)
                        Hata($"{zon.Ad} / Hat {hat.Kol}", $"{grup.Ad}: EN 54-24 uygunluğu onaylanmamış.");
                }
                if (hat.KabloSegmentleri.Count != hat.ToplamHoparlor)
                    Hata($"{zon.Ad} / Hat {hat.Kol}", "Her hoparlör için bir kablo segmenti bulunmalıdır.");
            }
        }

        var guc = ElvAnalogAnonsGucServisi.Hesapla(sistem, rezervYuzdesi);
        foreach (var sorun in guc.Sorunlar) Hata("Güç", sorun);
        // EN 54-4 akü ön boyutlandırması: kesin hesap üretici verisiyle yapılacağından uyarı seviyesindedir.
        var aku = ElvAnalogAnonsAkuServisi.Hesapla(sistem, rezervYuzdesi);
        foreach (var sorun in aku.Sorunlar) Uyari("Güç Kaynağı (EN 54-4)", sorun);
        var hatRapor = ElvAnalogAnonsHatDenetimServisi.Denetle(sistem);
        foreach (var sorun in hatRapor.Sorunlar)
            maddeler.Add(new(sorun.Seviye == ElvDenetimSorunSeviyesi.Hata
                ? ElvAnalogDogrulamaSeviyesi.Hata : ElvAnalogDogrulamaSeviyesi.Uyari, "Hat Denetimi", sorun.Mesaj));

        var temelKodlar = ElvAnalogAnonsAnaMerkezKatalogu.Kalemler.Where(k => k.TemelSistemKalemi).Select(k => k.Kod).ToList();
        var mevcutKodlar = sistem.AnaMerkez.Cihazlar.Select(c => c.KatalogKodu).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var kod in temelKodlar.Where(k => !mevcutKodlar.Contains(k)))
        {
            var kalem = ElvAnalogAnonsAnaMerkezKatalogu.Bul(kod)!;
            Uyari("Ana Merkez", $"Temel sistem kalemi eksik: {kalem.Ad}.");
        }

        if (maddeler.Count == 0)
            maddeler.Add(new(ElvAnalogDogrulamaSeviyesi.Bilgi, "Sistem", "Analog acil anons doğrulaması başarılı."));
        return new(maddeler);

        void Hata(string kaynak, string mesaj) => maddeler.Add(new(ElvAnalogDogrulamaSeviyesi.Hata, kaynak, mesaj));
        void Uyari(string kaynak, string mesaj) => maddeler.Add(new(ElvAnalogDogrulamaSeviyesi.Uyari, kaynak, mesaj));
    }
}
