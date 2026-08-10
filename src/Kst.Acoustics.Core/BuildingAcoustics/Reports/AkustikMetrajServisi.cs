using System.Globalization;
using ClosedXML.Excel;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Parsers;

namespace Kst.Acoustics.Core.BuildingAcoustics.Reports;

/// <summary>Akustik yapı elemanlarını fiziksel yüzeyleri çift saymadan metraj ve maliyete dönüştürür.</summary>
public sealed class AkustikMetrajServisi
{
    public IReadOnlyList<AkustikMetrajSatiri> Hesapla(BinaProje proje)
    {
        ArgumentNullException.ThrowIfNull(proje);
        proje.MetrajAyarlari ??= new();
        var girdiler = new List<MetrajGirdisi>();
        var acikDosemeler = proje.DuseyMekanBirlesimleri.Where(x => x.AktifMi)
            .SelectMany(x => x.Uyeler).Where(x => x.DosemeAcik).Select(x => x.MekanId).ToHashSet();
        var acikTavanlar = proje.DuseyMekanBirlesimleri.Where(x => x.AktifMi)
            .SelectMany(x => x.Uyeler).Where(x => x.TavanAcik).Select(x => x.MekanId).ToHashSet();

        foreach (var kat in proje.Katlar)
        {
            string katId = string.IsNullOrWhiteSpace(kat.BinaGeometriKatId)
                ? kat.KatNo.ToString(CultureInfo.InvariantCulture) : kat.BinaGeometriKatId;
            var mekanlar = kat.Mekanlar
                .Where(m => proje.MetrajAyarlari.HesapDisiMekanlariDahilEt || !HesapDisi(m))
                .ToList();

            var bolmeler = mekanlar.SelectMany(m => m.Bolmeler.Select(b => (Mekan: m, Bolme: b)))
                .Where(x => x.Bolme.YapiElemani is not null);
            if (proje.MetrajAyarlari.IcDuvarlariTekYuzSay)
                bolmeler = bolmeler.GroupBy(x => FizikselBolmeAnahtari(katId, x.Mekan, x.Bolme), StringComparer.Ordinal)
                    .Select(g => g.OrderBy(x => x.Bolme.Id).First());

            foreach (var (mekan, bolme) in bolmeler)
            {
                var eleman = bolme.YapiElemani!;
                GirdiEkle(girdiler, eleman, katId, mekan.Id, bolme.Id,
                    $"{mekan.Ad} / {bolme.Ad} opak yüzey",
                    Miktar(eleman, bolme.NetOpakAlan, BolmeUzunlugu(bolme), 1));

                // Seçilen fiziksel yüzün açıklıkları sayılır. M50 AciklikId aynı olsa bile
                // hatalı çoğaltılmış kayıtlar tek açıklık kabul edilir.
                foreach (var aciklik in bolme.Acikliklar
                    .Where(a => a.YapiElemani is not null)
                    .GroupBy(a => string.IsNullOrWhiteSpace(a.AciklikId)
                        ? a.Id.ToString("N") : a.AciklikId!, StringComparer.Ordinal)
                    .Select(g => g.First()))
                {
                    var aciklikElemani = aciklik.YapiElemani!;
                    GirdiEkle(girdiler, aciklikElemani, katId, mekan.Id, bolme.Id,
                        $"{mekan.Ad} / {bolme.Ad} / {aciklik.Tip}",
                        Miktar(aciklikElemani, Math.Max(0, aciklik.Alan),
                            Math.Max(0, aciklik.Genislik), 1));
                }
            }

            foreach (var mekan in mekanlar)
            {
                double cevre = PoligonCevresi(mekan);
                if (mekan.ZeminYapiElemani is { } zemin && !acikDosemeler.Contains(mekan.Id))
                    GirdiEkle(girdiler, zemin, katId, mekan.Id, null,
                        $"{mekan.Ad} / döşeme", Miktar(zemin, Math.Max(0, mekan.Alan), cevre, 1));
                if (mekan.TavanYapiElemani is { } tavan && !acikTavanlar.Contains(mekan.Id))
                    GirdiEkle(girdiler, tavan, katId, mekan.Id, null,
                        $"{mekan.Ad} / tavan", Miktar(tavan, Math.Max(0, mekan.Alan), cevre, 1));
            }
        }

        return girdiler
            .Where(x => x.Miktar > 0)
            .GroupBy(x => (x.Eleman.Id, x.Eleman.Tip, x.Eleman.MetrajBirimi))
            .Select(g => SatirOlustur(proje.MetrajAyarlari, g.First().Eleman, g.ToList()))
            .OrderBy(x => x.Tip).ThenBy(x => x.YapiElemaniAdi, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void ExcelOlustur(BinaProje proje, string dosyaYolu)
    {
        var satirlar = Hesapla(proje);
        string tamYol = Path.GetFullPath(dosyaYolu);
        string klasor = Path.GetDirectoryName(tamYol)
                         ?? throw new IOException($"Metraj hedef klasörü belirlenemedi: {dosyaYolu}");
        Directory.CreateDirectory(klasor);
        string gecici = Path.Combine(klasor,
            $".{Path.GetFileNameWithoutExtension(tamYol)}.{Guid.NewGuid():N}.tmp.xlsx");
        try
        {
            using var wb = new XLWorkbook();
            var ozet = wb.Worksheets.Add("M08 Metraj Özeti");
            string[] basliklar =
            [
                "Ana Grup", "Alt Grup", "Teknik Özellik", "Birim", "Miktar", "Açıklama",
                "Poz No", "Net Miktar", "Fire %", "Birim Fiyat", "Tutar", "Para Birimi", "Ürün Id"
            ];
            for (int i = 0; i < basliklar.Length; i++) ozet.Cell(1, i + 1).Value = basliklar[i];
            BaslikBicimlendir(ozet, basliklar.Length);
            int r = 2;
            foreach (var x in satirlar)
            {
                ozet.Cell(r, 1).Value = "Akustik Yapı Elemanları";
                ozet.Cell(r, 2).Value = x.Tip.ToString();
                ozet.Cell(r, 3).Value = x.YapiElemaniAdi;
                ozet.Cell(r, 4).Value = BirimEtiketi(x.Birim);
                ozet.Cell(r, 5).Value = x.Miktar;
                ozet.Cell(r, 6).Value = $"{x.Kaynaklar.Count} fiziksel yüzey/açıklık";
                ozet.Cell(r, 7).Value = x.PozNo;
                ozet.Cell(r, 8).Value = x.NetMiktar;
                ozet.Cell(r, 9).Value = x.FireYuzdesi;
                ozet.Cell(r, 10).Value = x.BirimFiyat;
                ozet.Cell(r, 11).Value = x.ToplamTutar;
                ozet.Cell(r, 12).Value = x.ParaBirimi;
                ozet.Cell(r, 13).Value = x.YapiElemaniId.ToString("D");
                r++;
            }
            ozet.Columns().AdjustToContents();

            var detay = wb.Worksheets.Add("M08 Metraj Kaynakları");
            string[] detayBaslik = ["Ürün", "Kat", "Mekan Id", "Bölme Id", "Açıklama", "Birim", "Miktar"];
            for (int i = 0; i < detayBaslik.Length; i++) detay.Cell(1, i + 1).Value = detayBaslik[i];
            BaslikBicimlendir(detay, detayBaslik.Length);
            r = 2;
            foreach (var satir in satirlar)
            foreach (var kaynak in satir.Kaynaklar)
            {
                detay.Cell(r, 1).Value = satir.YapiElemaniAdi;
                detay.Cell(r, 2).Value = kaynak.KatKimligi;
                detay.Cell(r, 3).Value = kaynak.MekanId?.ToString("D") ?? "";
                detay.Cell(r, 4).Value = kaynak.BolmeId?.ToString("D") ?? "";
                detay.Cell(r, 5).Value = kaynak.Aciklama;
                detay.Cell(r, 6).Value = BirimEtiketi(satir.Birim);
                detay.Cell(r, 7).Value = kaynak.Miktar;
                r++;
            }
            detay.Columns().AdjustToContents();
            wb.SaveAs(gecici);
            File.Move(gecici, tamYol, overwrite: true);
        }
        finally
        {
            if (File.Exists(gecici)) File.Delete(gecici);
        }
    }

    public string CsvOlustur(BinaProje proje)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("YapiElemaniId;Ad;Tip;PozNo;Birim;NetMiktar;FireYuzdesi;Miktar;BirimFiyat;ParaBirimi;ToplamTutar");
        foreach (var x in Hesapla(proje))
            sb.AppendLine(string.Join(';',
                x.YapiElemaniId.ToString("D"), M08Csv.Kacisla(x.YapiElemaniAdi), x.Tip,
                M08Csv.Kacisla(x.PozNo), x.Birim,
                x.NetMiktar.ToString(CultureInfo.InvariantCulture), x.FireYuzdesi.ToString(CultureInfo.InvariantCulture),
                x.Miktar.ToString(CultureInfo.InvariantCulture), x.BirimFiyat.ToString(CultureInfo.InvariantCulture),
                M08Csv.Kacisla(x.ParaBirimi), x.ToplamTutar.ToString(CultureInfo.InvariantCulture)));
        return sb.ToString();
    }

    private static AkustikMetrajSatiri SatirOlustur(
        AkustikMetrajAyar ayar, YapiElemani eleman, List<MetrajGirdisi> girdiler)
    {
        if (eleman.Id == Guid.Empty) eleman.Id = M08KararliKimlik.YapiElemani(eleman);
        decimal net = Math.Round(girdiler.Sum(x => x.Miktar), 3, MidpointRounding.AwayFromZero);
        decimal fire = eleman.FireYuzdesi > 0 ? eleman.FireYuzdesi : ayar.VarsayilanFireYuzdesi;
        fire = Math.Clamp(fire, 0m, 100m);
        decimal miktar = Math.Round(net * (1m + fire / 100m), 3, MidpointRounding.AwayFromZero);
        decimal fiyat = Math.Max(0m, eleman.BirimFiyat);
        string para = string.IsNullOrWhiteSpace(eleman.ParaBirimi) ? ayar.ParaBirimi : eleman.ParaBirimi;
        return new AkustikMetrajSatiri
        {
            YapiElemaniId = eleman.Id, YapiElemaniAdi = eleman.Ad, Tip = eleman.Tip,
            PozNo = eleman.PozNo, Birim = eleman.MetrajBirimi,
            NetMiktar = net, FireYuzdesi = fire, Miktar = miktar,
            BirimFiyat = fiyat, ParaBirimi = string.IsNullOrWhiteSpace(para) ? "TRY" : para,
            ToplamTutar = Math.Round(miktar * fiyat, 2, MidpointRounding.AwayFromZero),
            Kaynaklar = girdiler.Select(x => new AkustikMetrajKaynagi
            {
                KatKimligi = x.KatKimligi, MekanId = x.MekanId, BolmeId = x.BolmeId,
                Aciklama = x.Aciklama, Miktar = Math.Round(x.Miktar, 3, MidpointRounding.AwayFromZero)
            }).ToList()
        };
    }

    private static decimal Miktar(YapiElemani eleman, double alanM2, double uzunlukM, int adet) =>
        eleman.MetrajBirimi switch
        {
            AkustikMetrajBirimi.Metre => Donustur(Math.Max(0, uzunlukM)),
            AkustikMetrajBirimi.Adet => adet,
            AkustikMetrajBirimi.Metrekup => Donustur(Math.Max(0, alanM2) * Math.Max(0, eleman.ToplamKalinlikMm) / 1000d),
            _ => Donustur(Math.Max(0, alanM2))
        };

    private static decimal Donustur(double deger) => double.IsFinite(deger) && deger > 0
        ? Convert.ToDecimal(deger, CultureInfo.InvariantCulture) : 0m;

    private static void GirdiEkle(List<MetrajGirdisi> liste, YapiElemani eleman,
        string katKimligi, Guid mekanId, Guid? bolmeId, string aciklama, decimal miktar)
    {
        if (eleman.Id == Guid.Empty) eleman.Id = M08KararliKimlik.YapiElemani(eleman);
        liste.Add(new(eleman, katKimligi, mekanId, bolmeId, aciklama, miktar));
    }

    private static string FizikselBolmeAnahtari(string katId, Mekan mekan, Bolme bolme)
    {
        if (bolme.IsDisCephe)
            return $"D|{katId}|{mekan.Id:N}|{bolme.Id:N}";
        if (bolme.HasGeometry)
        {
            static string P(double x, double y) => $"{Math.Round(x, 3):F3},{Math.Round(y, 3):F3}";
            string a = P(bolme.Baslangic.X, bolme.Baslangic.Y);
            string b = P(bolme.Bitis.X, bolme.Bitis.Y);
            if (string.CompareOrdinal(a, b) > 0) (a, b) = (b, a);
            return $"I|{katId}|{a}|{b}";
        }
        var ids = new[] { bolme.KaynakMekan?.Id ?? mekan.Id, bolme.AliciMekan?.Id ?? Guid.Empty }
            .OrderBy(x => x).Select(x => x.ToString("N"));
        return $"I|{katId}|{string.Join('|', ids)}|{Math.Round(bolme.Alan, 3):F3}|{bolme.Ad.Trim().ToUpperInvariant()}";
    }

    private static double BolmeUzunlugu(Bolme bolme)
    {
        double dx = bolme.Bitis.X - bolme.Baslangic.X;
        double dy = bolme.Bitis.Y - bolme.Baslangic.Y;
        double uzunluk = Math.Sqrt(dx * dx + dy * dy);
        if (uzunluk > 0 && double.IsFinite(uzunluk)) return uzunluk;
        double yukseklik = bolme.KaynakMekan?.Yukseklik ?? 0;
        return yukseklik > 0 ? bolme.Alan / yukseklik : 0;
    }

    private static double PoligonCevresi(Mekan mekan)
    {
        if (mekan.Poligon is not { Count: >= 3 }) return 0;
        double toplam = 0;
        for (int i = 0; i < mekan.Poligon.Count; i++)
        {
            var a = mekan.Poligon[i];
            var b = mekan.Poligon[(i + 1) % mekan.Poligon.Count];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            toplam += Math.Sqrt(dx * dx + dy * dy);
        }
        return toplam;
    }

    private static bool HesapDisi(Mekan mekan) => mekan.Sinif == MekanSinifi.HesapDisi ||
        EK2SiniflandirmaServisi.HesapDisiMi(mekan.KullanimTipi) ||
        MekanKullanimiHelper.Parse(mekan.KullanimTipi) == MekanKullanimiTipi.HesapDisi;

    private static string BirimEtiketi(AkustikMetrajBirimi birim) => birim switch
    {
        AkustikMetrajBirimi.Metrekare => "m²", AkustikMetrajBirimi.Metre => "m",
        AkustikMetrajBirimi.Metrekup => "m³", _ => "adet"
    };

    private static void BaslikBicimlendir(IXLWorksheet sayfa, int sutunSayisi)
    {
        var aralik = sayfa.Range(1, 1, 1, sutunSayisi);
        aralik.Style.Font.Bold = true;
        aralik.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        aralik.Style.Font.FontColor = XLColor.White;
        sayfa.SheetView.FreezeRows(1);
    }

    private sealed record MetrajGirdisi(
        YapiElemani Eleman, string KatKimligi, Guid MekanId, Guid? BolmeId,
        string Aciklama, decimal Miktar);
}
