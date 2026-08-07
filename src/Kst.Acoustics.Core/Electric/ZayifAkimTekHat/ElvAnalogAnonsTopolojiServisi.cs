namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public sealed record ElvTopolojiIslemSonucu(bool Basarili, string Mesaj);
public sealed record ElvTopolojiDogrulamaSonucu(bool Gecerli, IReadOnlyList<string> Sorunlar);

/// <summary>Analog 100 V tahliye zonlarının tek hat ve dönüşümlü A/B hat davranışları.</summary>
public static class ElvAnalogAnonsTopolojiServisi
{
    public static ElvAnonsZonu ZonOlustur(string ad, int katNo, string katAdi, bool ciftHatli)
    {
        var zon = new ElvAnonsZonu
        {
            Ad = ad,
            KatNo = katNo,
            KatAdi = katAdi,
            Topoloji = ciftHatli ? ElvAnonsHatTopolojisi.CiftHatAB : ElvAnonsHatTopolojisi.TekHat,
            HatA = YeniHat(ElvAnonsHatKolu.A)
        };
        if (ciftHatli)
            zon.HatB = YeniHat(ElvAnonsHatKolu.B);
        return zon;
    }

    public static ElvTopolojiIslemSonucu CiftHatEtkinlestir(ElvAnonsZonu zon)
    {
        zon.Topoloji = ElvAnonsHatTopolojisi.CiftHatAB;
        zon.HatA.Kol = ElvAnonsHatKolu.A;
        zon.HatB ??= YeniHat(ElvAnonsHatKolu.B);
        zon.HatB.Kol = ElvAnonsHatKolu.B;
        return new(true, "A/B çift hat topolojisi etkinleştirildi.");
    }

    public static ElvTopolojiIslemSonucu TekHataDonustur(ElvAnonsZonu zon, bool hatBVerisiniHatAyaTasi)
    {
        double eskiToplamMetraj = Math.Max(0, zon.HatA.Metraj) + Math.Max(0, zon.HatB?.Metraj ?? 0);
        if (zon.HatB is { } hatB && (hatB.HoparlorGruplari.Count > 0 || hatB.Metraj > 0))
        {
            if (!hatBVerisiniHatAyaTasi)
                return new(false, "Hat B üzerinde hoparlör veya metraj bulunduğu için veri aktarılmadan kapatılamaz.");

            foreach (var grup in hatB.HoparlorGruplari)
                GrubuEkleVeyaBirlestir(zon.HatA, grup);
        }

        zon.HatB = null;
        zon.Topoloji = ElvAnonsHatTopolojisi.TekHat;
        zon.HatA.Kol = ElvAnonsHatKolu.A;
        KabloSegmentleriniEsitle(zon.HatA);
        if (zon.HatA.SegmentToplamMetraj <= 0 && eskiToplamMetraj > 0 && zon.HatA.KabloSegmentleri.Count > 0)
        {
            zon.HatA.KabloSegmentleri[0].Metraj = eskiToplamMetraj;
            zon.HatA.Metraj = eskiToplamMetraj;
        }
        return new(true, "Zon tek hat topolojisine dönüştürüldü.");
    }

    /// <summary>
    /// Aynı hoparlör tipini A ve B hatlarına adet farkı en fazla bir olacak biçimde dağıtır.
    /// Tek hat topolojisinde bütün hoparlörler Hat A'ya eklenir.
    /// </summary>
    public static ElvTopolojiIslemSonucu HoparlorleriDengeliDagit(
        ElvAnonsZonu zon, string katalogKodu, int toplamAdet, double tapGucuW, string mahalNotu = "")
    {
        if (toplamAdet <= 0)
            return new(false, "Hoparlör adedi sıfırdan büyük olmalıdır.");
        if (ElvAnalogHoparlorKatalogu.Bul(katalogKodu) is null)
            return new(false, "Hoparlör katalog kalemi bulunamadı.");

        int adetA = zon.CiftHatli ? (toplamAdet + 1) / 2 : toplamAdet;
        int adetB = zon.CiftHatli ? toplamAdet / 2 : 0;
        var grupA = GrupOlustur(katalogKodu, adetA, tapGucuW, mahalNotu);
        if (grupA is null) return new(false, "Seçilen güç tap değeri hoparlör kataloğuyla uyumlu değil.");
        GrubuEkleVeyaBirlestir(zon.HatA, grupA);

        if (adetB > 0 && zon.HatB is not null)
        {
            var grupB = GrupOlustur(katalogKodu, adetB, tapGucuW, mahalNotu);
            if (grupB is null)
            {
                zon.HatA.HoparlorGruplari.Remove(grupA);
                return new(false, "Seçilen güç tap değeri hoparlör kataloğuyla uyumlu değil.");
            }
            GrubuEkleVeyaBirlestir(zon.HatB, grupB);
        }

        KabloSegmentleriniEsitle(zon.HatA);
        if (zon.HatB is not null) KabloSegmentleriniEsitle(zon.HatB);

        return new(true, zon.CiftHatli
            ? $"{toplamAdet} hoparlör Hat A/B üzerine {adetA}/{adetB} dağıtıldı."
            : $"{toplamAdet} hoparlör Hat A'ya eklendi.");
    }

    public static bool HoparlorGrubunuSil(ElvAnalogHoparlorHatti hat, string? grupId)
    {
        var grup = hat.HoparlorGruplari.FirstOrDefault(g => g.Id == grupId);
        if (grup is null) return false;
        hat.HoparlorGruplari.Remove(grup);
        KabloSegmentleriniEsitle(hat);
        return true;
    }

    public static void KabloSegmentleriniEsitle(ElvAnalogHoparlorHatti hat)
    {
        // Bozuk/elle düzenlenmiş dosyada yinelenen (grup, no) anahtarı çökme yaratmasın; ilki alınır.
        var eski = new Dictionary<(string, int), ElvAnalogKabloSegmenti>();
        foreach (var s in hat.KabloSegmentleri)
            eski.TryAdd((s.HoparlorGrupId, s.GrupIciHoparlorNo), s);
        var yeni = new List<ElvAnalogKabloSegmenti>();
        string onceki = $"Hat {hat.Kol} Çıkışı";
        int sira = 1;
        foreach (var grup in hat.HoparlorGruplari)
        {
            for (int no = 1; no <= Math.Max(0, grup.Adet); no++)
            {
                string bitis = $"{grup.Ad} {no}";
                if (!eski.TryGetValue((grup.Id, no), out var segment))
                    segment = new ElvAnalogKabloSegmenti { HoparlorGrupId = grup.Id, GrupIciHoparlorNo = no };
                segment.SiraNo = sira++;
                segment.Baslangic = onceki;
                segment.Bitis = bitis;
                yeni.Add(segment);
                onceki = bitis;
            }
        }
        hat.KabloSegmentleri = yeni;
        hat.Metraj = hat.SegmentToplamMetraj;
    }

    public static ElvTopolojiDogrulamaSonucu Dogrula(ElvAnonsZonu zon)
    {
        var sorunlar = new List<string>();
        var hatA = zon.HatA;
        if (hatA is null)
            sorunlar.Add("Hat A tanımlı değil.");
        else if (hatA.Kol != ElvAnonsHatKolu.A)
            sorunlar.Add("Hat A kol bilgisi A olmalıdır.");

        if (zon.Topoloji == ElvAnonsHatTopolojisi.CiftHatAB)
        {
            if (zon.HatB is null)
                sorunlar.Add("Çift hat topolojisinde Hat B zorunludur.");
            else if (zon.HatB.Kol != ElvAnonsHatKolu.B)
                sorunlar.Add("Hat B kol bilgisi B olmalıdır.");
        }
        else if (zon.HatB is not null)
            sorunlar.Add("Tek hat topolojisinde Hat B bulunmamalıdır.");

        if (hatA is not null && zon.HatB is not null)
        {
            var ortakId = hatA.HoparlorGruplari.Select(g => g.Id)
                .Intersect(zon.HatB.HoparlorGruplari.Select(g => g.Id)).Any();
            if (ortakId) sorunlar.Add("Aynı hoparlör grubu Hat A ve Hat B üzerinde birlikte kullanılamaz.");
        }
        return new(sorunlar.Count == 0, sorunlar);
    }

    private static ElvAnalogHoparlorHatti YeniHat(ElvAnonsHatKolu kol) => new()
    {
        Ad = $"Hat {kol}",
        Kol = kol,
        KabloTipi = "2x1.5 mm² FE180"
    };

    private static ElvAnalogHoparlorGrubu? GrupOlustur(
        string katalogKodu, int adet, double tapGucuW, string mahalNotu)
    {
        var grup = ElvAnalogHoparlorServisi.KatalogdanGrupOlustur(katalogKodu, adet);
        if (!ElvAnalogHoparlorServisi.TapGucunuAyarla(grup, tapGucuW)) return null;
        grup.MahalNotu = mahalNotu;
        return grup;
    }

    private static void GrubuEkleVeyaBirlestir(ElvAnalogHoparlorHatti hat, ElvAnalogHoparlorGrubu yeni)
    {
        var mevcut = hat.HoparlorGruplari.FirstOrDefault(g =>
            string.Equals(g.KatalogKodu, yeni.KatalogKodu, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(g.TapGucuW - yeni.TapGucuW) < 0.001
            && string.Equals(g.MahalNotu, yeni.MahalNotu, StringComparison.CurrentCultureIgnoreCase));
        if (mevcut is null) hat.HoparlorGruplari.Add(yeni);
        else mevcut.Adet += yeni.Adet;
    }
}
