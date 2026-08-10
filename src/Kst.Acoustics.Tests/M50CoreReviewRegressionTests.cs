using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;
using Xunit;

namespace Kst.Acoustics.Tests;

/// M50 Core/Bina güvenilirlik incelemesi regresyonları. Program.cs kaydı kök
/// ajan tarafından yapılır; Fact sarmalayıcıları dosyanın tek başına da koşmasını sağlar.
public static class M50CoreReviewRegressionTests
{
    public static void OtomatikAciklikVeYansimaKimligiKararliKalir()
    {
        var segmentler = new List<(Point2D A, Point2D B)>
        {
            (new(0, 0), new(4, 0))
        };
        var arclar = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)>
        {
            (new(1, 0), 0.9, 0, 90)
        };

        var ilk = AciklikTespitServisi.Bul(segmentler, arclar, Array.Empty<BinaAciklik>());
        var ana = ilk.Single(a => a.Kaynak == "arc");
        ana.Id = "kararli-aciklik";
        ana.Tip = BinaAciklikTipi.Pencere;
        ana.YukseklikM = 1.35;
        ana.EsikYuksekligiM = 0.82;
        ana.EkAlanlar = new Dictionary<string, JsonElement>
        {
            ["uretici"] = JsonDocument.Parse("{\"koru\":true}").RootElement.Clone()
        };

        var kat = new BinaKat
        {
            Id = "kat-1",
            KatNo = 1,
            KatYuksekligiM = 3,
            Odalar = new ObservableCollection<BinaOda>
            {
                Oda("oda-alt", 0, -3, 4, 0),
                Oda("oda-ust", 0, 0.2, 4, 3)
            },
            Acikliklar = ilk
        };
        AciklikYardimci.KomsuDuvarlariTamamla(kat);
        var ilkYansima = kat.Acikliklar.Single(a => a.Kaynak?.EndsWith("-yansima") == true);
        Esit("kararli-aciklik-yansima", ilkYansima.Id, "İlk yansıma kimliği");

        var ikinci = AciklikTespitServisi.Bul(segmentler, arclar, kat.Acikliklar);
        kat.Acikliklar = ikinci;
        AciklikYardimci.KomsuDuvarlariTamamla(kat);

        var ikinciAna = kat.Acikliklar.Single(a => a.Kaynak == "arc");
        var ikinciYansima = kat.Acikliklar.Single(a => a.Kaynak?.EndsWith("-yansima") == true);
        Esit("kararli-aciklik", ikinciAna.Id, "Ana açıklık kimliği");
        Esit(BinaAciklikTipi.Pencere, ikinciAna.Tip, "Ana açıklık tipi");
        Yakin(1.35, ikinciAna.YukseklikM, "Ana açıklık yüksekliği");
        Yakin(0.82, ikinciAna.EsikYuksekligiM, "Ana açıklık eşiği");
        if (ikinciAna.EkAlanlar?.ContainsKey("uretici") != true)
            throw new InvalidOperationException("Yeniden tarama otomatik açıklığın bilinmeyen JSON alanını kaybetti.");
        Esit("kararli-aciklik-yansima", ikinciYansima.Id, "Yansıma kimliği");
        Esit(ikinciAna.Tip, ikinciYansima.Tip, "Yansıma tipi");
        Yakin(ikinciAna.YukseklikM, ikinciYansima.YukseklikM, "Yansıma yüksekliği");
        Yakin(ikinciAna.EsikYuksekligiM, ikinciYansima.EsikYuksekligiM, "Yansıma eşiği");
    }

    public static void IcBukeyOdaMerkeziEslestirmeVeDuseyBirlesimdeCalisir()
    {
        var u = new List<Point2D>
        {
            new(0, 0), new(6, 0), new(6, 6), new(4, 6),
            new(4, 2), new(2, 2), new(2, 6), new(0, 6)
        };
        var eski = new BinaOda
        {
            Id = "u-oda", Ad = "U", AlanM2 = 20, Poligon = u,
            EkAlanlar = new Dictionary<string, JsonElement>
            {
                ["oda-uretici"] = JsonDocument.Parse("42").RootElement.Clone()
            }
        };
        if (!PoligonYardimci.IsPointInside(eski.Merkez, eski.Poligon))
            throw new InvalidOperationException($"İçbükey oda temsil noktası içeride olmalı: {eski.Merkez}.");

        var yeni = new BinaOda
        {
            Id = "gecici",
            Ad = "U yeni",
            AlanM2 = 20,
            Poligon = u.Select(p => new Point2D(p.X, p.Y)).ToList()
        };
        var silinenler = OdaEslestirmeServisi.Eslestir([yeni], [eski]);
        Esit("u-oda", yeni.Id, "İçbükey oda yeniden tarama kimliği");
        Esit(0, silinenler.Count, "Silinmiş sayılan oda sayısı");
        if (yeni.EkAlanlar?["oda-uretici"].GetInt32() != 42)
            throw new InvalidOperationException("Oda yeniden eşleştirmesi bilinmeyen JSON alanını kaybetti.");

        var bolunenEski = Oda("bolunen", 0, 0, 4, 4);
        var solYari = Oda("yeni-sol", 0, 0, 2, 4);
        var sagYari = Oda("yeni-sag", 2, 0, 4, 4);
        var bolunmedeSilinen = OdaEslestirmeServisi.Eslestir([solYari, sagYari], [bolunenEski]);
        if (solYari.Id == "bolunen" || sagYari.Id == "bolunen" ||
            !bolunmedeSilinen.Contains("bolunen"))
            throw new InvalidOperationException("Bölünen odada eski Id parser sırasına göre keyfi devredilmemeli.");

        var ustOda = new BinaOda
        {
            Id = "ust-u", Ad = "Üst U", AlanM2 = 20,
            Poligon = u.Select(p => new Point2D(p.X, p.Y)).ToList()
        };
        var altOda = new BinaOda
        {
            Id = "alt-u", Ad = "Alt U", AlanM2 = 20,
            Poligon = u.Select(p => new Point2D(p.X, p.Y)).ToList()
        };
        var ust = new BinaKat { Id = "ust", KatNo = 1, Ad = "Üst", Odalar = new([ustOda]) };
        var alt = new BinaKat { Id = "alt", KatNo = 2, Ad = "Alt", Odalar = new([altOda]) };
        var geo = BinaGeometriServisi.YeniOlustur("İçbükey");
        geo.Katlar.Add(ust);
        geo.Katlar.Add(alt);
        var sonuc = BinaDuseyBirlesimServisi.TavanPatlat(geo, alt, altOda);
        if (!sonuc.Basarili || sonuc.Birlesim is null)
            throw new InvalidOperationException("İçbükey odalar düşey olarak eşleşmeliydi: " + sonuc.Mesaj);
    }

    public static void BinaOdaAlanYukseklikVePoligonDegisiklikleriniBildirir()
    {
        var oda = Oda("o", 0, 0, 4, 3);
        var bildirilen = new List<string?>();
        oda.PropertyChanged += (_, e) => bildirilen.Add(e.PropertyName);
        oda.AlanM2 = 13;
        oda.YukseklikOverrideM = 3.2;
        oda.Poligon = Dikdortgen(0, 0, 5, 3);

        foreach (var ad in new[]
                 {
                     nameof(BinaOda.AlanM2), nameof(BinaOda.YukseklikOverrideM),
                     nameof(BinaOda.Poligon), nameof(BinaOda.Merkez)
                 })
            if (!bildirilen.Contains(ad))
                throw new InvalidOperationException($"PropertyChanged '{ad}' bildirilmedi.");
    }

    public static void Sema3MetadataGoreliYolVeV2MigrasyonRoundTrip()
    {
        string dizin = GeciciDizin();
        try
        {
            string dxfDizin = Path.Combine(dizin, "dxf");
            Directory.CreateDirectory(dxfDizin);
            string dxfYolu = Path.Combine(dxfDizin, "kat.dxf");
            File.WriteAllText(dxfYolu, "geçici");
            string geoYolu = Path.Combine(dizin, "bina.e4bina.json");

            var geo = BinaGeometriServisi.YeniOlustur("Metadata");
            var kat = new BinaKat
            {
                Id = "kat-meta", Ad = "Kat", KatNo = 1, KatYuksekligiM = 3,
                DxfFilePath = Path.GetFullPath(dxfYolu),
                DxfBirimOlcegiM = 0.001,
                DxfNormalizeOfsetiM = new(123.4, 567.8),
                DxfKaynakBolgesiMinM = new(123.4, 567.8),
                DxfKaynakBolgesiMaxM = new(133.4, 575.8),
                Odalar = new([Oda("oda-meta", 0, 0, 10, 8)])
            };
            geo.Katlar.Add(kat);
            var katlarReferansi = geo.Katlar;
            var odalarReferansi = kat.Odalar;
            var acikliklarReferansi = kat.Acikliklar;
            var birlesimlerReferansi = geo.DuseyBirlesimleri;
            BinaGeometriServisi.Kaydet(geo, geoYolu);

            if (!ReferenceEquals(katlarReferansi, geo.Katlar) ||
                !ReferenceEquals(odalarReferansi, kat.Odalar) ||
                !ReferenceEquals(acikliklarReferansi, kat.Acikliklar) ||
                !ReferenceEquals(birlesimlerReferansi, geo.DuseyBirlesimleri))
                throw new InvalidOperationException("Kaydet canlı model koleksiyon referanslarını değiştirmemeli.");
            Esit(Path.GetFullPath(dxfYolu), kat.DxfFilePath, "Canlı model DXF yolu");
            string json = File.ReadAllText(geoYolu);
            if (!json.Contains($"dxf{Path.DirectorySeparatorChar}kat.dxf") &&
                !json.Contains("dxf/kat.dxf") && !json.Contains("dxf\\\\kat.dxf"))
                throw new InvalidOperationException("DXF yolu JSON içinde proje-klasörüne göre göreli yazılmadı.");

            var acilan = BinaGeometriServisi.Ac(geoYolu);
            Esit(BinaGeometriServisi.GuncelSemaSurum, acilan.SemaSurum, "Şema sürümü");
            var acilanKat = acilan.Katlar.Single();
            Esit(Path.GetFullPath(dxfYolu), acilanKat.DxfFilePath, "Çözümlenmiş DXF yolu");
            Yakin(0.001, acilanKat.DxfBirimOlcegiM, "DXF ölçeği");
            Yakin(123.4, acilanKat.DxfNormalizeOfsetiM.X, "DXF normalize X");
            Yakin(133.4, acilanKat.DxfKaynakBolgesiMaxM!.Value.X, "DXF bölge max X");

            string v2Yolu = Path.Combine(dizin, "v2.e4bina.json");
            File.WriteAllText(v2Yolu,
                "{\"SemaSurum\":2,\"GeometriRevizyon\":0,\"BinaAdi\":\"v2\"," +
                "\"Katlar\":[{\"Id\":\"v2-kat\",\"Ad\":\"Kat\",\"KatNo\":1," +
                "\"KatYuksekligiM\":3,\"Odalar\":[],\"Acikliklar\":[]," +
                "\"DxfSegmentler\":[],\"DxfArclar\":[]}],\"DuseyBirlesimleri\":[]}");
            var v2 = BinaGeometriServisi.Ac(v2Yolu);
            Esit(3, v2.SemaSurum, "v2→v3 migrasyon sürümü");
            Yakin(0, v2.Katlar[0].DxfBirimOlcegiM, "Legacy ölçek fallback'i");
            if (v2.Katlar[0].DxfKaynakBolgesiMinM is not null ||
                v2.Katlar[0].DxfKaynakBolgesiMaxM is not null)
                throw new InvalidOperationException("v2 kaynak bölgesi null fallback olmalı.");

            var kurtarmaKopyasi = BinaGeometriServisi.DerinKopyala(geo);
            kurtarmaKopyasi.BinaAdi = "Kopya";
            kurtarmaKopyasi.Katlar.Clear();
            Esit("Metadata", geo.BinaAdi, "Derin kopya kaynak bina adı");
            Esit(1, geo.Katlar.Count, "Derin kopya kaynak kat koleksiyonu");
        }
        finally
        {
            Directory.Delete(dizin, recursive: true);
        }
    }

    public static void GecersizGelecekVeBilinmeyenJsonAlanlariGuvenleEleAlinir()
    {
        string dizin = GeciciDizin();
        try
        {
            string gelecek = Path.Combine(dizin, "gelecek.json");
            File.WriteAllText(gelecek, "{\"SemaSurum\":999,\"Katlar\":[],\"DuseyBirlesimleri\":[]}");
            HataBekle<InvalidDataException>(() => BinaGeometriServisi.Ac(gelecek), "Gelecek şema");

            string nullYolu = Path.Combine(dizin, "null.json");
            File.WriteAllText(nullYolu,
                "{\"SemaSurum\":2,\"GeometriRevizyon\":0,\"BinaAdi\":null," +
                "\"Katlar\":null,\"DuseyBirlesimleri\":null}");
            var normalize = BinaGeometriServisi.Ac(nullYolu);
            if (normalize.Katlar is null || normalize.DuseyBirlesimleri is null || normalize.BinaAdi is null)
                throw new InvalidOperationException("Null kök koleksiyonlar normalize edilmedi.");

            string ekYolu = Path.Combine(dizin, "ek.json");
            File.WriteAllText(ekYolu,
                "{\"SemaSurum\":3,\"GeometriRevizyon\":0,\"BinaAdi\":\"Ek\"," +
                "\"UreticiAlani\":{\"deger\":42},\"Katlar\":[{" +
                "\"Id\":\"k\",\"Ad\":\"K\",\"KatNo\":1,\"KatYuksekligiM\":3," +
                "\"KatUreticiAlani\":\"koru\",\"Odalar\":[],\"Acikliklar\":[]," +
                "\"DxfSegmentler\":[],\"DxfArclar\":[]}],\"DuseyBirlesimleri\":[]}");
            var ek = BinaGeometriServisi.Ac(ekYolu);
            if (ek.EkAlanlar?.ContainsKey("UreticiAlani") != true ||
                ek.Katlar[0].EkAlanlar?.ContainsKey("KatUreticiAlani") != true)
                throw new InvalidOperationException("Bilinmeyen güncel-şema alanları tutulmadı.");
            string tekrar = Path.Combine(dizin, "ek-tekrar.json");
            BinaGeometriServisi.Kaydet(ek, tekrar);
            string tekrarJson = File.ReadAllText(tekrar);
            if (!tekrarJson.Contains("UreticiAlani") || !tekrarJson.Contains("KatUreticiAlani"))
                throw new InvalidOperationException("Bilinmeyen alanlar round-trip sırasında kayboldu.");
        }
        finally
        {
            Directory.Delete(dizin, recursive: true);
        }
    }

    public static void FizikselSinirlarVeAtomikKayitBozukVeriyiReddeder()
    {
        string dizin = GeciciDizin();
        try
        {
            string hedef = Path.Combine(dizin, "korunacak.json");
            File.WriteAllText(hedef, "korunacak-içerik");
            var geo = BinaGeometriServisi.YeniOlustur("Bozuk");
            var kat = new BinaKat { Id = "k", KatNo = 1, Ad = "K", KatYuksekligiM = double.NaN };
            geo.Katlar.Add(kat);
            int revizyon = geo.GeometriRevizyon;

            HataBekle<InvalidDataException>(() => BinaGeometriServisi.Kaydet(geo, hedef), "NaN yükseklik");
            Esit("korunacak-içerik", File.ReadAllText(hedef), "NaN sonrası hedef dosya");
            Esit(revizyon, geo.GeometriRevizyon, "NaN sonrası revizyon");
            if (File.Exists(hedef + ".tmp"))
                throw new InvalidOperationException("Başarısız kayıttan sonra tmp dosyası kaldı.");

            kat.KatYuksekligiM = 3;
            kat.Odalar.Add(new BinaOda
            {
                Id = "o", Ad = "Aşırı", AlanM2 = 10,
                Poligon = [new(0, 0), new(20_000_000, 0), new(20_000_000, 1), new(0, 1)]
            });
            HataBekle<InvalidDataException>(() => BinaGeometriServisi.Kaydet(geo, hedef), "Aşırı koordinat");
            Esit("korunacak-içerik", File.ReadAllText(hedef), "Aşırı değer sonrası hedef dosya");
            Esit(revizyon, geo.GeometriRevizyon, "Aşırı değer sonrası revizyon");

            kat.Odalar.Clear();
            geo.GeometriRevizyon = int.MaxValue;
            HataBekle<InvalidDataException>(() => BinaGeometriServisi.Kaydet(geo, hedef), "Revizyon taşması");
            Esit("korunacak-içerik", File.ReadAllText(hedef), "Revizyon taşması sonrası hedef dosya");
            Esit(int.MaxValue, geo.GeometriRevizyon, "Taşma sonrası revizyon");

            geo.GeometriRevizyon = 0;
            var aciklik = new BinaAciklik
            {
                Id = "a", Bas = new(0, 0), Bit = new(1, 0), GenislikM = 0.5,
                YukseklikM = 2, EsikYuksekligiM = 0
            };
            kat.Acikliklar.Add(aciklik);
            HataBekle<InvalidDataException>(() => BinaGeometriServisi.Kaydet(geo, hedef), "Açıklık genişlik tutarsızlığı");
            Esit("korunacak-içerik", File.ReadAllText(hedef), "Açıklık genişliği sonrası hedef dosya");

            aciklik.GenislikM = 1;
            aciklik.Tip = BinaAciklikTipi.Pencere;
            aciklik.YukseklikM = 2.2; // etkin pencere eşiği 0.9 m → 3.1 m
            HataBekle<InvalidDataException>(() => BinaGeometriServisi.Kaydet(geo, hedef), "Açıklık kat yüksekliği");
            Esit("korunacak-içerik", File.ReadAllText(hedef), "Açıklık yüksekliği sonrası hedef dosya");

            // Aynı sidecar'ı iki M50 oturumu açtığında ilk kaydeden kazanır; bayat
            // oturum yeni revizyonu veya sonradan bozulan dosyayı sessizce ezemez.
            string cakismaYolu = Path.Combine(dizin, "eszamanli.e4bina.json");
            var ilkKayit = BinaGeometriServisi.YeniOlustur("Eşzamanlı");
            BinaGeometriServisi.Kaydet(ilkKayit, cakismaYolu);
            var bayatOturum = BinaGeometriServisi.Ac(cakismaYolu);
            var guncelOturum = BinaGeometriServisi.Ac(cakismaYolu);
            guncelOturum.BinaAdi = "Yeni oturum";
            BinaGeometriServisi.Kaydet(guncelOturum, cakismaYolu);

            string guncelDiskIcerigi = File.ReadAllText(cakismaYolu);
            int bayatRevizyon = bayatOturum.GeometriRevizyon;
            bayatOturum.BinaAdi = "Bayat oturum";
            InvalidOperationException? cakismaHatasi = null;
            try
            {
                BinaGeometriServisi.Kaydet(bayatOturum, cakismaYolu);
            }
            catch (InvalidOperationException ex)
            {
                cakismaHatasi = ex;
            }
            if (cakismaHatasi is null ||
                !cakismaHatasi.Message.Contains("beklenen Rev.", StringComparison.Ordinal) ||
                !cakismaHatasi.Message.Contains("disk Rev.", StringComparison.Ordinal))
                throw new InvalidOperationException("Bayat M50 kaydı açık revizyon çakışması üretmeliydi.");
            Esit(guncelDiskIcerigi, File.ReadAllText(cakismaYolu), "Çakışma sonrası disk içeriği");
            Esit(bayatRevizyon, bayatOturum.GeometriRevizyon, "Çakışma sonrası canlı revizyon");

            // Süreç-içi lock'tan bağımsız hedef-bitişik OS kilidi: başka bir
            // süreç FileShare.None ile kilidi tutuyormuş gibi simüle et.
            string osKilitYolu = Path.Combine(dizin, "os-kilit.e4bina.json");
            var osKilitGeo = BinaGeometriServisi.YeniOlustur("OS kilidi");
            BinaGeometriServisi.Kaydet(osKilitGeo, osKilitYolu);
            var osKilitOturumu = BinaGeometriServisi.Ac(osKilitYolu);
            string osKilitDiskIcerigi = File.ReadAllText(osKilitYolu);
            int osKilitRevizyon = osKilitOturumu.GeometriRevizyon;
            InvalidOperationException? osKilitHatasi = null;
            using (new FileStream(osKilitYolu + ".lock", FileMode.OpenOrCreate,
                       FileAccess.ReadWrite, FileShare.None))
            {
                try
                {
                    BinaGeometriServisi.Kaydet(osKilitOturumu, osKilitYolu);
                }
                catch (InvalidOperationException ex)
                {
                    osKilitHatasi = ex;
                }
            }
            if (osKilitHatasi is null ||
                !osKilitHatasi.Message.Contains("başka bir M50 oturumunda", StringComparison.Ordinal))
                throw new InvalidOperationException("Tutulan OS kayıt kilidi açık ve kısa hata üretmeliydi.");
            Esit(osKilitDiskIcerigi, File.ReadAllText(osKilitYolu), "OS kilidi sonrası disk içeriği");
            Esit(osKilitRevizyon, osKilitOturumu.GeometriRevizyon, "OS kilidi sonrası canlı revizyon");
            if (Directory.EnumerateFiles(dizin, "os-kilit.e4bina.json.*.tmp").Any())
                throw new InvalidOperationException("Kilit başarısızlığından sonra işleme özel tmp dosyası kaldı.");

            // Kilit bırakılınca aynı oturum normal kaydedilebilmeli.
            BinaGeometriServisi.Kaydet(osKilitOturumu, osKilitYolu);
            Esit(osKilitRevizyon + 1, osKilitOturumu.GeometriRevizyon,
                "OS kilidi bırakıldıktan sonraki revizyon");
            if (Directory.EnumerateFiles(dizin, "os-kilit.e4bina.json.*.tmp").Any())
                throw new InvalidOperationException("Başarılı kayıttan sonra işleme özel tmp dosyası kaldı.");

            var bozukDiskiBekleyen = BinaGeometriServisi.Ac(cakismaYolu);
            int bozukDiskOncesiRevizyon = bozukDiskiBekleyen.GeometriRevizyon;
            const string bozukDiskIcerigi = "{ bozuk-json";
            File.WriteAllText(cakismaYolu, bozukDiskIcerigi);
            InvalidOperationException? bozukDiskHatasi = null;
            try
            {
                BinaGeometriServisi.Kaydet(bozukDiskiBekleyen, cakismaYolu);
            }
            catch (InvalidOperationException ex)
            {
                bozukDiskHatasi = ex;
            }
            if (bozukDiskHatasi is null ||
                !bozukDiskHatasi.Message.Contains("okunamadığı veya doğrulanamadığı", StringComparison.Ordinal))
                throw new InvalidOperationException("Bozuk eş-hedef sidecar güvenli kayıt hatası üretmeliydi.");
            Esit(bozukDiskIcerigi, File.ReadAllText(cakismaYolu), "Bozuk sidecar üzerine yazmama");
            Esit(bozukDiskOncesiRevizyon, bozukDiskiBekleyen.GeometriRevizyon,
                "Bozuk sidecar sonrası canlı revizyon");

            string farkliKaydetYolu = Path.Combine(dizin, "eszamanli-farkli.e4bina.json");
            BinaGeometriServisi.Kaydet(bozukDiskiBekleyen, farkliKaydetYolu);
            Esit(bozukDiskOncesiRevizyon + 1,
                BinaGeometriServisi.Ac(farkliKaydetYolu).GeometriRevizyon,
                "Çakışma sonrası Farklı Kaydet revizyonu");
        }
        finally
        {
            Directory.Delete(dizin, recursive: true);
        }
    }

    public static void UzamsalIndeksAsiriSonluKoordinatVeGenisSorgudaTasmaz()
    {
        var segmentler = new List<(Point2D A, Point2D B)>
        {
            (new(-1e300, -1e300), new(1e300, 1e300)),
            (new(0, 0), new(4, 0))
        };
        var arclar = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)>
        {
            (new(1, 0), 0.9, 0, 90)
        };
        var ayar = new AciklikTespitServisi.Ayarlar { MaxMenteseWallDistM = 1e300 };
        var sonuc = AciklikTespitServisi.Bul(segmentler, arclar, Array.Empty<BinaAciklik>(), ayar);
        if (!sonuc.Any(a => a.Kaynak == "arc"))
            throw new InvalidOperationException("Aşırı koordinatlı segment yanında yerel arc kapısı bulunmalıydı.");
    }

    public static void PoligonInvariantlariDogrulanirVeAlanNormalizeEdilir()
    {
        string dizin = GeciciDizin();
        try
        {
            string yol = Path.Combine(dizin, "poligon.json");
            var duzeltilen = TekOdaliGeometri(
                [new(0, 0), new(4, 0), new(4, 3), new(0, 3)], 999);
            BinaGeometriServisi.Kaydet(duzeltilen, yol);
            Yakin(12, duzeltilen.Katlar[0].Odalar[0].AlanM2, "Poligondan normalize oda alanı");

            var sifirAlan = TekOdaliGeometri([new(0, 0), new(1, 0), new(2, 0)], 1);
            HataBekle<InvalidDataException>(
                () => BinaGeometriServisi.Kaydet(sifirAlan, yol), "Sıfır alanlı poligon");

            var yinelenen = TekOdaliGeometri(
                [new(0, 0), new(4, 0), new(4, 0), new(4, 3), new(0, 3)], 12);
            HataBekle<InvalidDataException>(
                () => BinaGeometriServisi.Kaydet(yinelenen, yol), "Ardışık yinelenen köşe");

            var kesisen = TekOdaliGeometri(
                [new(0, 0), new(4, 4), new(0, 4), new(4, 0)], 8);
            HataBekle<InvalidDataException>(
                () => BinaGeometriServisi.Kaydet(kesisen, yol), "Kendi kendini kesen poligon");

            // Aynı oda iki aktif birleşimde bulunursa BirlesimBul/listesel seçim
            // belirsizleşir; pasif geçmiş kayıtları ise bu tekilliği engellememeli.
            var birlesimGeo = BinaGeometriServisi.YeniOlustur("Birleşim tekilliği");
            for (int i = 0; i < 3; i++)
            {
                var kat = new BinaKat
                {
                    Id = $"kat-{i}", Ad = $"Kat {i}", KatNo = i + 1, KatYuksekligiM = 3
                };
                kat.Odalar.Add(new BinaOda
                {
                    Id = $"oda-{i}", Ad = $"Oda {i}", AlanM2 = 12,
                    Poligon = [new(0, 0), new(4, 0), new(4, 3), new(0, 3)]
                });
                birlesimGeo.Katlar.Add(kat);
            }
            birlesimGeo.DuseyBirlesimleri.Add(new BinaDuseyBirlesim
            {
                Id = "aktif-1",
                Uyeler =
                [
                    new() { KatId = "kat-0", KatNo = 1, OdaId = "oda-0" },
                    new() { KatId = "kat-1", KatNo = 2, OdaId = "oda-1" }
                ]
            });
            var ikinciBirlesim = new BinaDuseyBirlesim
            {
                Id = "aktif-2",
                Uyeler =
                [
                    new() { KatId = "kat-0", KatNo = 1, OdaId = "oda-0" },
                    new() { KatId = "kat-2", KatNo = 3, OdaId = "oda-2" }
                ]
            };
            birlesimGeo.DuseyBirlesimleri.Add(ikinciBirlesim);
            string birlesimYolu = Path.Combine(dizin, "birlesim.json");
            HataBekle<InvalidDataException>(
                () => BinaGeometriServisi.Kaydet(birlesimGeo, birlesimYolu),
                "Odanın iki aktif birleşime üyeliği");

            ikinciBirlesim.AktifMi = false;
            BinaGeometriServisi.Kaydet(birlesimGeo, birlesimYolu);
            Esit(1, birlesimGeo.GeometriRevizyon, "Pasif birleşimin tekilliği engellememesi");
        }
        finally
        {
            Directory.Delete(dizin, recursive: true);
        }
    }

    public static void KumeEsigiAnnotationVeYakinEtiketiDogruAyirir()
    {
        var sekizMetreAralik = DikdortgenSegmentleri(0, 0, 10, 8)
            .Concat(DikdortgenSegmentleri(18, 0, 28, 8)).ToList();
        var kumeler = KatKumelemeServisi.Kumele(sekizMetreAralik, [], []);
        Esit(2, kumeler.Count, "8 m aralıklı plan kümesi");

        var metin = (Pos: new Point2D(5, -1), Value: "ZEMİN KAT");
        kumeler = KatKumelemeServisi.Kumele(sekizMetreAralik, [metin], []);
        var ilk = kumeler.MinBy(k => k.MinX)!;
        var ikinci = kumeler.MaxBy(k => k.MinX)!;
        Esit(1, ilk.Metinler.Count, "BBox dışındaki yakın etiket ataması");
        Esit(0, ikinci.Metinler.Count, "Etiketin ikinci kümeye çoğaltılmaması");
        Esit("Zemin Kat", KatEtiketServisi.TahminEt(ilk).OnerilenAd, "Yakın kat etiketi tahmini");
    }

    public static void TekVeCokluDxfYenidenOkumaOlcekOfsetBolgeyiKorur()
    {
        string dizin = GeciciDizin();
        try
        {
            TekDxfYenidenOkumaDogrula(dizin);
            CokluDxfYenidenOkumaDogrula(dizin);
        }
        finally
        {
            Directory.Delete(dizin, recursive: true);
        }
    }

    private static void TekDxfYenidenOkumaDogrula(string dizin)
    {
        string dxfYolu = Path.Combine(dizin, "tek-mm.dxf");
        var dxf = new DxfDocument();
        dxf.DrawingVariables.InsUnits = DrawingUnits.Millimeters;
        DikdortgenEkle(dxf, 100_000, 200_000, 110_000, 208_000);
        dxf.Entities.Add(new Text("SALON", new Vector2(105_000, 204_000), 300));
        dxf.Save(dxfYolu);

        var parser = new DxfKatPlaniParser();
        var kat = parser.Parse(dxfYolu, 1, 3, normalizeOrigin: true);
        Yakin(0.001, kat.DxfBirimOlcegiM, "Tek DXF mm ölçeği");
        Yakin(100, kat.DxfNormalizeOfsetiM.X, "Tek DXF normalize X");
        Yakin(200, kat.DxfNormalizeOfsetiM.Y, "Tek DXF normalize Y");

        var geo = BinaGeometriServisi.YeniOlustur("Tek");
        geo.Katlar.Add(kat);
        string geoYolu = Path.Combine(dizin, "tek.e4bina.json");
        BinaGeometriServisi.Kaydet(geo, geoYolu);
        var acilanKat = BinaGeometriServisi.Ac(geoYolu).Katlar.Single();
        var yeniden = new DxfKatPlaniParser().ExtractRawSegmentsFromFile(acilanKat.DxfFilePath, acilanKat);
        SegmentSetleriEsit(kat.TespitSegmentler, yeniden, "Tek DXF yeniden okuma");
    }

    private static void CokluDxfYenidenOkumaDogrula(string dizin)
    {
        string dxfYolu = Path.Combine(dizin, "coklu-unitless.dxf");
        var dxf = new DxfDocument();
        dxf.DrawingVariables.InsUnits = DrawingUnits.Unitless;
        DikdortgenEkle(dxf, 0, 0, 10, 8);
        DikdortgenEkle(dxf, 30, 0, 40, 8);
        dxf.Entities.Add(new Text("ZEMİN KAT", new Vector2(5, -1), 0.5));
        dxf.Entities.Add(new Text("1. KAT", new Vector2(35, -1), 0.5));
        dxf.Entities.Add(new Line(new Vector2(10, 4), new Vector2(30, 4))
        {
            Layer = new Layer("YAZI")
        });
        dxf.Save(dxfYolu);

        var ayar = new DxfKatYukleyici.Ayarlar { UnitlessScaleMeters = 1 };
        var yukleyici = new DxfKatYukleyici();
        var tespit = yukleyici.CokluKatTespitEt(dxfYolu, ayar)
            ?? throw new InvalidOperationException("Annotation çizgisi ayrı planları birleştirmemeli.");
        Esit(2, tespit.Adaylar.Count, "Annotation-hariç çoklu kat aday sayısı");
        var aday = tespit.Adaylar.Single(a => a.OnerilenAd == "1. Kat");
        Esit(Path.GetFullPath(dxfYolu), Path.GetFullPath(aday.KaynakDosyaYolu), "Çoklu aday kaynak yolu");
        if (string.IsNullOrWhiteSpace(aday.BirimUyarisi))
            throw new InvalidOperationException("Unitless çoklu aday birim uyarısını taşımalı.");

        var kat = yukleyici.CokluKatUret(aday, 1, "1. Kat", 3, ayar);
        Esit(Path.GetFullPath(dxfYolu), Path.GetFullPath(kat.DxfFilePath), "Üretilen kat kaynak yolu");
        if (kat.DxfKaynakBolgesiMinM is null || kat.DxfKaynakBolgesiMaxM is null)
            throw new InvalidOperationException("Çoklu kat seçili kaynak bölgesini saklamalı.");
        if (kat.TespitSegmentler.Any(s => Math.Max(s.A.X, s.B.X) > 10.001))
            throw new InvalidOperationException("İkinci kata birinci plan/dünya X koordinatı sızdı.");

        var geo = BinaGeometriServisi.YeniOlustur("Çoklu");
        geo.Katlar.Add(kat);
        string geoYolu = Path.Combine(dizin, "coklu.e4bina.json");
        BinaGeometriServisi.Kaydet(geo, geoYolu);
        var acilanKat = BinaGeometriServisi.Ac(geoYolu).Katlar.Single();
        var yeniden = new DxfKatPlaniParser().ExtractRawSegmentsFromFile(acilanKat.DxfFilePath, acilanKat);
        SegmentSetleriEsit(kat.TespitSegmentler, yeniden, "Çoklu DXF seçili bölge yeniden okuma");
    }

    private static BinaOda Oda(string id, double x0, double y0, double x1, double y1)
        => new()
        {
            Id = id,
            Ad = id,
            AlanM2 = Math.Abs((x1 - x0) * (y1 - y0)),
            Poligon = Dikdortgen(x0, y0, x1, y1)
        };

    private static BinaGeometri TekOdaliGeometri(List<Point2D> poligon, double alan)
    {
        var geo = BinaGeometriServisi.YeniOlustur("Poligon");
        geo.Katlar.Add(new BinaKat
        {
            Id = Guid.NewGuid().ToString(), Ad = "Kat", KatNo = 1, KatYuksekligiM = 3,
            Odalar = new([new BinaOda
            {
                Id = Guid.NewGuid().ToString(), Ad = "Oda", AlanM2 = alan, Poligon = poligon
            }])
        });
        return geo;
    }

    private static List<Point2D> Dikdortgen(double x0, double y0, double x1, double y1)
        => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];

    private static List<(Point2D A, Point2D B)> DikdortgenSegmentleri(
        double x0, double y0, double x1, double y1)
        =>
        [
            (new(x0, y0), new(x1, y0)), (new(x1, y0), new(x1, y1)),
            (new(x1, y1), new(x0, y1)), (new(x0, y1), new(x0, y0))
        ];

    private static void DikdortgenEkle(DxfDocument dxf, double x0, double y0, double x1, double y1)
    {
        dxf.Entities.Add(new Line(new Vector2(x0, y0), new Vector2(x1, y0)));
        dxf.Entities.Add(new Line(new Vector2(x1, y0), new Vector2(x1, y1)));
        dxf.Entities.Add(new Line(new Vector2(x1, y1), new Vector2(x0, y1)));
        dxf.Entities.Add(new Line(new Vector2(x0, y1), new Vector2(x0, y0)));
    }

    private static void SegmentSetleriEsit(
        IReadOnlyList<(Point2D A, Point2D B)> beklenen,
        IReadOnlyList<(Point2D A, Point2D B)> bulunan,
        string ad)
    {
        if (beklenen.Count != bulunan.Count)
            throw new InvalidOperationException($"{ad}: segment sayısı {beklenen.Count}, bulunan {bulunan.Count}.");
        var kalan = bulunan.ToList();
        foreach (var e in beklenen)
        {
            int i = kalan.FindIndex(a =>
                (NoktaYakin(e.A, a.A) && NoktaYakin(e.B, a.B)) ||
                (NoktaYakin(e.A, a.B) && NoktaYakin(e.B, a.A)));
            if (i < 0)
                throw new InvalidOperationException($"{ad}: ({e.A})–({e.B}) segmenti bulunamadı.");
            kalan.RemoveAt(i);
        }
    }

    private static bool NoktaYakin(Point2D a, Point2D b)
        => Math.Abs(a.X - b.X) <= 1e-7 && Math.Abs(a.Y - b.Y) <= 1e-7;

    private static string GeciciDizin()
    {
        string yol = Path.Combine(Path.GetTempPath(), $"m50-core-review-{Guid.NewGuid():N}");
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static void HataBekle<T>(Action action, string ad) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"{ad}: {typeof(T).Name} bekleniyordu.");
    }

    private static void Yakin(double beklenen, double bulunan, string ad, double tol = 1e-8)
    {
        if (Math.Abs(beklenen - bulunan) > tol)
            throw new InvalidOperationException($"{ad}: {beklenen} bekleniyordu, {bulunan} bulundu.");
    }

    private static void Esit<T>(T beklenen, T bulunan, string ad)
    {
        if (!EqualityComparer<T>.Default.Equals(beklenen, bulunan))
            throw new InvalidOperationException($"{ad}: '{beklenen}' bekleniyordu, '{bulunan}' bulundu.");
    }
}

public sealed class M50CoreReviewRegressionXunitTests
{
    [Fact] public void AciklikKimligi() => M50CoreReviewRegressionTests.OtomatikAciklikVeYansimaKimligiKararliKalir();
    [Fact] public void IcBukeyOda() => M50CoreReviewRegressionTests.IcBukeyOdaMerkeziEslestirmeVeDuseyBirlesimdeCalisir();
    [Fact] public void BinaOdaInpc() => M50CoreReviewRegressionTests.BinaOdaAlanYukseklikVePoligonDegisiklikleriniBildirir();
    [Fact] public void SemaVeYol() => M50CoreReviewRegressionTests.Sema3MetadataGoreliYolVeV2MigrasyonRoundTrip();
    [Fact] public void JsonGuvenligi() => M50CoreReviewRegressionTests.GecersizGelecekVeBilinmeyenJsonAlanlariGuvenleEleAlinir();
    [Fact] public void FizikselSinirlar() => M50CoreReviewRegressionTests.FizikselSinirlarVeAtomikKayitBozukVeriyiReddeder();
    [Fact] public void UzamsalIndeks() => M50CoreReviewRegressionTests.UzamsalIndeksAsiriSonluKoordinatVeGenisSorgudaTasmaz();
    [Fact] public void PoligonInvariantlari() => M50CoreReviewRegressionTests.PoligonInvariantlariDogrulanirVeAlanNormalizeEdilir();
    [Fact] public void Kumeleme() => M50CoreReviewRegressionTests.KumeEsigiAnnotationVeYakinEtiketiDogruAyirir();
    [Fact] public void DxfYenidenOkuma() => M50CoreReviewRegressionTests.TekVeCokluDxfYenidenOkumaOlcekOfsetBolgeyiKorur();
}
