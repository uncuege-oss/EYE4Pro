using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

/// <summary>
/// Mekan derin kopyalama (undo/snapshot ve düşey patlatma geri-alma için).
/// Kullanıcı girdileri, menfez/junction/açıklıklar, hesap sonuçları ve Bolme kimlikleri
/// eksiksiz korunur. Liste kopyasında AliciMekan ilişkileri klonlara yeniden bağlanır;
/// böylece geri-al sırasında topolojiyi yıkıcı biçimde yeniden üretmek gerekmez.
/// </summary>
public static class MekanKlonServisi
{
    public static Mekan Klonla(Mekan mekan) => KlonlaListe([mekan]).Single();

    public static ObservableCollection<Mekan> KlonlaListe(IEnumerable<Mekan> mekanlar)
    {
        var kaynaklar = mekanlar.ToList();
        var klonlar = kaynaklar.Select(KlonlaCekirdek).ToList();
        var mekanEsleme = kaynaklar
            .Select((kaynak, i) => (kaynak.Id, Klon: klonlar[i]))
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Klon);

        for (int mekanIndex = 0; mekanIndex < kaynaklar.Count; mekanIndex++)
        {
            var kaynak = kaynaklar[mekanIndex];
            var klon = klonlar[mekanIndex];
            for (int bolmeIndex = 0; bolmeIndex < kaynak.Bolmeler.Count && bolmeIndex < klon.Bolmeler.Count; bolmeIndex++)
            {
                var kaynakBolme = kaynak.Bolmeler[bolmeIndex];
                var klonBolme = klon.Bolmeler[bolmeIndex];
                klonBolme.KaynakMekan = klon;
                klonBolme.AliciMekan = kaynakBolme.AliciMekan is { } alici && mekanEsleme.TryGetValue(alici.Id, out var klonAlici)
                    ? klonAlici
                    : null;
            }
        }

        return new ObservableCollection<Mekan>(klonlar);
    }

    private static Mekan KlonlaCekirdek(Mekan m)
    {
        var clone = new Mekan
        {
            Id                = m.Id,
            Ad                = m.Ad,
            BagimsizBolumId   = m.BagimsizBolumId,
            KullanimTipi      = m.KullanimTipi,
            Sinif             = m.Sinif,
            Alan              = m.Alan,
            Yukseklik         = m.Yukseklik,
            Poligon           = m.Poligon.Select(p => new Point2D(p.X, p.Y)).ToList(),
            RT60              = m.RT60,
            KaynakDerecesi    = m.KaynakDerecesi,
            AliciSinifi       = m.AliciSinifi,
            EmilimAlani       = m.EmilimAlani,
            TesisatSurekliDb  = m.TesisatSurekliDb,
            TesisatKesikliDb  = m.TesisatKesikliDb,
            ZeminYapiElemani  = m.ZeminYapiElemani,
            TavanYapiElemani  = m.TavanYapiElemani,
            BirlesikHacimM3   = m.BirlesikHacimM3,
        };

        foreach (var b in m.Bolmeler)
        {
            clone.Bolmeler.Add(new Bolme
            {
                Id          = b.Id,
                Ad          = b.Ad,
                Alan        = b.Alan,
                YapiElemani = b.YapiElemani,
                KaynakMekan = clone,
                Baslangic   = new Point2D(b.Baslangic.X, b.Baslangic.Y),
                Bitis       = new Point2D(b.Bitis.X, b.Bitis.Y),
                HasGeometry = b.HasGeometry,
                ApparentRw  = b.ApparentRw,
                ApparentLnw = b.ApparentLnw,
                ApparentLnTw = b.ApparentLnTw,
                Dntw        = b.Dntw,
                OktavHavaSesiSonucu = Klonla(b.OktavHavaSesiSonucu),
                OktavDarbeSesiSonucu = Klonla(b.OktavDarbeSesiSonucu),
                UcuncuOktavHavaSesiSonucu = Klonla(b.UcuncuOktavHavaSesiSonucu),
                DisCepheSpektralSonuc = Klonla(b.DisCepheSpektralSonuc),
                Acikliklar = b.Acikliklar.Select(Klonla).ToList(),
                Menfezler = b.Menfezler.Select(Klonla).ToList(),
                Junctions = b.Junctions.Select(Klonla).ToList(),
            });
        }

        return clone;
    }

    private static BolmeAcikligi Klonla(BolmeAcikligi a) => new()
    {
        AciklikId = a.AciklikId,
        Tip = a.Tip,
        Baslangic = new Point2D(a.Baslangic.X, a.Baslangic.Y),
        Bitis = new Point2D(a.Bitis.X, a.Bitis.Y),
        Genislik = a.Genislik,
        Yukseklik = a.Yukseklik,
        EsikYuksekligiM = a.EsikYuksekligiM,
        YapiElemani = a.YapiElemani,
    };

    private static Menfez Klonla(Menfez m) => new()
    {
        Id = m.Id,
        Ad = m.Ad,
        Tip = m.Tip,
        DneDb = m.DneDb,
        DneThirdOctave = new List<double>(m.DneThirdOctave),
    };

    private static Junction Klonla(Junction j) => new()
    {
        Id = j.Id,
        Kenar = j.Kenar,
        GeometriTipi = j.GeometriTipi,
        BaglantiTipi = j.BaglantiTipi,
        StrukturelTipi = j.StrukturelTipi,
        Uzunluk = j.Uzunluk,
        KijOverrideDb = j.KijOverrideDb,
    };

    private static OktavBantSonucu? Klonla(OktavBantSonucu? s) => s == null ? null :
        new OktavBantSonucu((double[])s.R_f.Clone(), s.RwHesap, s.CHesap, s.CtrHesap, s.Kaynak);

    private static OktavDarbeSonucu? Klonla(OktavDarbeSonucu? s) => s == null ? null :
        new OktavDarbeSonucu((double[])s.Ln_f.Clone(), s.LnwHesap, s.Kaynak);

    private static UcuncuOktavHavaSonucu? Klonla(UcuncuOktavHavaSonucu? s) => s == null ? null :
        new UcuncuOktavHavaSonucu((double[])s.R_f.Clone(), s.RwHesap, s.CHesap, s.CtrHesap, s.Kaynak);

    private static DisCepheSpektralSonuc? Klonla(DisCepheSpektralSonuc? s) => s == null ? null :
        new DisCepheSpektralSonuc(
            (double[])s.Frekanslar.Clone(),
            (double[])s.DisSpektrum.Clone(),
            (double[])s.DntSpektrum.Clone(),
            (double[])s.IcSpektrum.Clone(),
            s.DnTATr,
            s.D2mNt50,
            s.IcToplam,
            s.Kaynak);
}
