using Kst.Acoustics.App.Electric.ZayifAkimTekHat;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Kst.Acoustics.Tests;

public class ZayifAkimTekHatTests
{
    [Fact]
    public void ImportServisi_M12DenYukle_BasitProjeTest()
    {
        // Arrange: Minimal M12 proje oluştur
        var m12Proje = new BasitProjeCizProje { ProjeAdi = "Test Projesi" };
        var kat = new BasitProjeKat { KatNo = 1, Ad = "1. Kat" };

        // Data sistemi oluştur
        var dataRack = new BasitProjeEleman
        {
            Id = "merkez-1",
            Tip = BpcElemanTipi.DataRack,
            Ad = "Data Rack",
            OdaId = null
        };

        var dataPriz1 = new BasitProjeEleman
        {
            Id = "cihaz-1",
            Tip = BpcElemanTipi.DataPriz,
            Ad = "Data Prizi 1",
            OdaId = null
        };

        var dataPriz2 = new BasitProjeEleman
        {
            Id = "cihaz-2",
            Tip = BpcElemanTipi.DataPriz,
            Ad = "Data Prizi 2",
            OdaId = null
        };

        kat.Elemanlar.Add(dataRack);
        kat.Elemanlar.Add(dataPriz1);
        kat.Elemanlar.Add(dataPriz2);

        var devre = new BpcSistemDevre
        {
            Sistem = BpcPaftaTipi.Data,
            MerkezId = "merkez-1",
            CihazIds = new() { "cihaz-1", "cihaz-2" }
        };

        kat.SistemDevreleri.Add(devre);
        m12Proje.Katlar.Add(kat);

        // JSON serialize et temp dosyaya
        var tempPath = Path.Combine(Path.GetTempPath(), "test_m12.e4p12");
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        var json = JsonSerializer.Serialize(m12Proje, opts);
        File.WriteAllText(tempPath, json);

        try
        {
            // Act
            var m13Proje = ZayifAkimTekHatImportServisi.M12DenYukle(tempPath);

            // Assert
            Assert.NotNull(m13Proje);
            Assert.Single(m13Proje.Sayfalar);
            Assert.Equal("Test Projesi", m13Proje.ProjeAdi);
            Assert.Single(m13Proje.DataSistemi.RackKabinler);
            Assert.Equal(2, m13Proje.DataSistemi.RackKabinler[0].DataHatlari.Count);

            var sayfa = m13Proje.Sayfalar[0];
            Assert.Equal(BpcPaftaTipi.Data, sayfa.Sistem);
            Assert.Equal("Data", sayfa.SistemAdi);

            // 1 merkez + 1 consolidated cihaz = 2 blocks
            Assert.Equal(2, sayfa.Bloklar.Count);

            var merkez = sayfa.Bloklar.First(b => b.Rol == ELVBlokRol.Merkez);
            Assert.Equal(BpcElemanTipi.DataRack, merkez.ElemanTipi);
            Assert.Single(merkez.BagliCocukBlokIds);

            var cihaz = sayfa.Bloklar.First(b => b.Rol == ELVBlokRol.Cihaz);
            Assert.Equal(2, cihaz.Miktar);
            Assert.Contains(cihaz.Id, merkez.BagliCocukBlokIds);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void LayoutServisi_OtomatikYerlestir_BlokKonumlandirmasıTest()
    {
        // Arrange
        var sayfa = new ELVSayfa
        {
            Sistem = BpcPaftaTipi.Data,
            SistemAdi = "Data",
            KabloRengi = "#4ED0E0",
            Bloklar = new()
        };

        var merkez = new ELVBlok
        {
            ElemanTipi = BpcElemanTipi.DataRack,
            ElemanAdi = "Merkez",
            Rol = ELVBlokRol.Merkez,
            Kat = 1
        };

        var ara = new ELVBlok
        {
            ElemanTipi = BpcElemanTipi.KatDagitimKutusu,
            ElemanAdi = "Aratoplayıcı",
            Rol = ELVBlokRol.AraToplayici,
            Kat = 2
        };

        var cihaz1 = new ELVBlok
        {
            ElemanTipi = BpcElemanTipi.DataPriz,
            ElemanAdi = "Cihaz 1",
            Rol = ELVBlokRol.Cihaz,
            Kat = 2
        };

        var cihaz2 = new ELVBlok
        {
            ElemanTipi = BpcElemanTipi.DataPriz,
            ElemanAdi = "Cihaz 2",
            Rol = ELVBlokRol.Cihaz,
            Kat = 2
        };

        merkez.BagliCocukBlokIds.Add(ara.Id);
        ara.BagliCocukBlokIds.Add(cihaz1.Id);
        ara.BagliCocukBlokIds.Add(cihaz2.Id);

        sayfa.Bloklar.AddRange(new[] { merkez, ara, cihaz1, cihaz2 });

        var katlar = new List<ELVKatInfo>
        {
            new ELVKatInfo { KatNo = 2, Ad = "2. Kat" },
            new ELVKatInfo { KatNo = 1, Ad = "1. Kat" }
        };

        // Act
        ZayifAkimTekHatLayoutServisi.OtomatikYerlestir(sayfa, katlar);

        // Assert
        // Column X coordinates
        Assert.Equal(80, merkez.LayoutX);
        Assert.Equal(260, ara.LayoutX);
        Assert.Equal(440, cihaz1.LayoutX);
        Assert.Equal(600, cihaz2.LayoutX);

        // Floor Y coordinates: Floor 2 (index 0) => Y = 50 + 35 = 85. Floor 1 (index 1) => Y = 50 + 120 + 35 = 205.
        Assert.Equal(205, merkez.LayoutY);
        Assert.Equal(85, ara.LayoutY);
        Assert.Equal(85, cihaz1.LayoutY);
        Assert.Equal(85, cihaz2.LayoutY);
    }

    [Fact]
    public void LayoutServisi_BaglantiBicimi_LShape()
    {
        // Arrange
        double x1 = 100, y1 = 100;
        double x2 = 300, y2 = 300;

        // Act
        var noktalar = ZayifAkimTekHatLayoutServisi.BaglantiBicimi(x1, y1, x2, y2);

        // Assert
        Assert.Equal(4, noktalar.Count);

        // Başlangıç
        Assert.Equal((x1, y1), noktalar[0]);

        // Dikey orta
        var ortaY = (y1 + y2) / 2;
        Assert.Equal((x1, ortaY), noktalar[1]);

        // Yatay orta
        Assert.Equal((x2, ortaY), noktalar[2]);

        // Bitiş
        Assert.Equal((x2, y2), noktalar[3]);
    }

    [Fact]
    public void DxfServisi_DxfCiktiAl_BasitProjeTest()
    {
        // Arrange
        var proje = new ZayifAkimTekHatProje { ProjeAdi = "Test DXF" };
        var sayfa = new ELVSayfa
        {
            Sistem = BpcPaftaTipi.Data,
            SistemAdi = "Data",
            KabloRengi = "#4ED0E0",
            Bloklar = new()
        };

        var blok = new ELVBlok
        {
            ElemanTipi = BpcElemanTipi.DataRack,
            ElemanAdi = "Rack",
            Rol = ELVBlokRol.Merkez,
            LayoutX = 100,
            LayoutY = 100
        };

        sayfa.Bloklar.Add(blok);
        proje.Sayfalar.Add(sayfa);

        var hedefDosya = Path.Combine(Path.GetTempPath(), "test_output.dxf");

        try
        {
            // Act
            ZayifAkimTekHatDxfServisi.DxfCiktiAl(proje, hedefDosya);

            // Assert
            Assert.True(File.Exists(hedefDosya));
            Assert.True(new FileInfo(hedefDosya).Length > 0);
        }
        finally
        {
            if (File.Exists(hedefDosya))
                File.Delete(hedefDosya);
        }
    }

    [Fact]
    public void ImportServisi_M12DenYukle_CctvKameraSistemiTest()
    {
        var m12Proje = new BasitProjeCizProje { ProjeAdi = "CCTV Test Projesi" };
        var kat = new BasitProjeKat { KatNo = 2, Ad = "2. Kat" };

        kat.Elemanlar.Add(new BasitProjeEleman
        {
            Id = "nvr-1",
            Tip = BpcElemanTipi.CctvKayit,
            Ad = "NVR"
        });
        kat.Elemanlar.Add(new BasitProjeEleman
        {
            Id = "kamera-1",
            Tip = BpcElemanTipi.Kamera,
            Ad = "Kamera 1"
        });
        kat.Elemanlar.Add(new BasitProjeEleman
        {
            Id = "kamera-2",
            Tip = BpcElemanTipi.Kamera,
            Ad = "Kamera 2"
        });

        kat.SistemDevreleri.Add(new BpcSistemDevre
        {
            Sistem = BpcPaftaTipi.Cctv,
            MerkezId = "nvr-1",
            CihazIds = new() { "kamera-1", "kamera-2" }
        });
        m12Proje.Katlar.Add(kat);

        var tempPath = Path.Combine(Path.GetTempPath(), "test_m12_cctv.e4p12");
        var dxfPath = Path.Combine(Path.GetTempPath(), "test_cctv_output.dxf");
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        File.WriteAllText(tempPath, JsonSerializer.Serialize(m12Proje, opts));

        try
        {
            var m13Proje = ZayifAkimTekHatImportServisi.M12DenYukle(tempPath);

            Assert.Single(m13Proje.KameraSistemi.RackKabinler);
            var rack = m13Proje.KameraSistemi.RackKabinler[0];
            Assert.Equal("2. Kat", rack.KatAdi);
            Assert.Equal(2, rack.KameraSayisi);
            Assert.Equal(2, rack.KameraHatlari.Count);

            ZayifAkimTekHatDxfServisi.KameraDxfCiktiAl(m13Proje.KameraSistemi, dxfPath);
            Assert.True(File.Exists(dxfPath));
            Assert.True(new FileInfo(dxfPath).Length > 0);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (File.Exists(dxfPath)) File.Delete(dxfPath);
        }
    }

    [Fact]
    public void ImportServisi_M12DenYukle_AnonsHatlariTest()
    {
        var m12Proje = new BasitProjeCizProje { ProjeAdi = "Anons Test Projesi" };
        var kat = new BasitProjeKat { KatNo = 1, Ad = "1. Kat" };

        kat.Elemanlar.Add(new BasitProjeEleman { Id = "amfi-1", Tip = BpcElemanTipi.AnonsAmfi, Ad = "Anons Amfi" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "hop-1", Tip = BpcElemanTipi.Hoparlor, Ad = "Hoparlör 1" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "hop-2", Tip = BpcElemanTipi.Hoparlor, Ad = "Hoparlör 2" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "hop-3", Tip = BpcElemanTipi.Hoparlor, Ad = "Hoparlör 3" });

        kat.SistemDevreleri.Add(new BpcSistemDevre
        {
            Sistem = BpcPaftaTipi.Anons,
            MerkezId = "amfi-1",
            CihazIds = new() { "hop-1", "hop-2" }
        });
        kat.SistemDevreleri.Add(new BpcSistemDevre
        {
            Sistem = BpcPaftaTipi.Anons,
            MerkezId = "amfi-1",
            CihazIds = new() { "hop-3" }
        });
        m12Proje.Katlar.Add(kat);

        var tempPath = Path.Combine(Path.GetTempPath(), "test_m12_anons.e4p12");
        var dxfPath = Path.Combine(Path.GetTempPath(), "test_anons_output.dxf");
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        File.WriteAllText(tempPath, JsonSerializer.Serialize(m12Proje, opts));

        try
        {
            var m13Proje = ZayifAkimTekHatImportServisi.M12DenYukle(tempPath);

            Assert.Single(m13Proje.AnonsSistemi.RackKabinler);
            var rack = m13Proje.AnonsSistemi.RackKabinler[0];
            Assert.Equal(2, rack.AnonsHatSayisi);
            Assert.Equal(2, rack.AnonsHatlari.Count);
            Assert.Equal(2, rack.AnonsHatlari[0].HoparlorSayisi);
            Assert.Equal(1, rack.AnonsHatlari[1].HoparlorSayisi);
            Assert.Equal("2x1.5 mm² FE180", rack.MerkezeKabloTipi);
            Assert.All(rack.AnonsHatlari, h => Assert.Equal("2x1.5 mm² FE180", h.KabloTipi));

            ZayifAkimTekHatDxfServisi.AnonsDxfCiktiAl(m13Proje.AnonsSistemi, dxfPath);
            Assert.True(File.Exists(dxfPath));
            Assert.True(new FileInfo(dxfPath).Length > 0);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (File.Exists(dxfPath)) File.Delete(dxfPath);
        }
    }

    [Fact]
    public void ImportServisi_M12DenYukle_TvSistemiKoaksiyelHatlarTest()
    {
        var m12Proje = new BasitProjeCizProje { ProjeAdi = "TV Test Projesi" };
        var kat = new BasitProjeKat { KatNo = 0, Ad = "Zemin Kat" };

        kat.Elemanlar.Add(new BasitProjeEleman { Id = "tvm-1", Tip = BpcElemanTipi.TvMerkezi, Ad = "TV Merkezi" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "tv-1", Tip = BpcElemanTipi.TvPriz, Ad = "TV Prizi 1" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "tv-2", Tip = BpcElemanTipi.TvPriz, Ad = "TV Prizi 2" });

        kat.SistemDevreleri.Add(new BpcSistemDevre
        {
            Sistem = BpcPaftaTipi.Tv,
            MerkezId = "tvm-1",
            CihazIds = new() { "tv-1", "tv-2" }
        });
        m12Proje.Katlar.Add(kat);

        var tempPath = Path.Combine(Path.GetTempPath(), "test_m12_tv.e4p12");
        var dxfPath = Path.Combine(Path.GetTempPath(), "test_tv_output.dxf");
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        File.WriteAllText(tempPath, JsonSerializer.Serialize(m12Proje, opts));

        try
        {
            var m13Proje = ZayifAkimTekHatImportServisi.M12DenYukle(tempPath);

            Assert.Single(m13Proje.TvSistemi.RackKabinler);
            Assert.True(m13Proje.TvSistemi.CatiUyduSayisi >= 1);
            Assert.NotEmpty(m13Proje.TvSistemi.CatiHatlari);

            var rack = m13Proje.TvSistemi.RackKabinler[0];
            Assert.Equal(2, rack.TvPrizSayisi);
            Assert.Equal(2, rack.TvHatlari.Count);
            Assert.Equal("RG-6/U Koaksiyel", rack.MerkezeKabloTipi);
            Assert.All(rack.TvHatlari, h => Assert.Equal("RG-6/U Koaksiyel", h.KabloTipi));
            Assert.All(m13Proje.TvSistemi.CatiHatlari, h => Assert.Equal("RG-11/U Koaksiyel", h.KabloTipi));

            ZayifAkimTekHatDxfServisi.TvDxfCiktiAl(m13Proje.TvSistemi, dxfPath);
            Assert.True(File.Exists(dxfPath));
            Assert.True(new FileInfo(dxfPath).Length > 0);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (File.Exists(dxfPath)) File.Delete(dxfPath);
        }
    }

    [Fact]
    public void TvDirektLnb_TunerHatlariniUretirVeKapasiteyiDogrular()
    {
        var tv = new ElvTvSistemi();
        tv.OrtakUclar.Add(new ElvTvUcu { Ad = "Salon TV", KatNo = 0, KatAdi = "Zemin", TunerSayisi = 2 });
        tv.OrtakUclar.Add(new ElvTvUcu { Ad = "Yatak TV", KatNo = 1, KatAdi = "1. Kat", TunerSayisi = 1 });
        ElvTvTopolojiServisi.TeknolojiyiHazirla(tv, TvDagitimTeknolojisi.DirektLnb);

        Assert.Equal(3, tv.DirektLnb.Hatlar.Count);
        Assert.Equal(new[] { 1, 2, 3 }, tv.DirektLnb.Hatlar.Select(h => h.LnbCikisNo).ToArray());
        Assert.Empty(ElvTvTopolojiServisi.Dogrula(tv));

        tv.DirektLnb.LnbCikisSayisi = 2;
        Assert.Contains(ElvTvTopolojiServisi.Dogrula(tv), s => s.Mesaj.Contains("kapasitesi"));
        tv.DirektLnb.LnbTipi = "Quattro LNB";
        Assert.Contains(ElvTvTopolojiServisi.Dogrula(tv), s => s.Mesaj.Contains("Quattro"));
    }

    [Fact]
    public void TvIpTv_DataBenzeriRackleriVeCatHatlariniUretir_BantGenisliginiDogrular()
    {
        var tv = new ElvTvSistemi();
        tv.OrtakUclar.AddRange(
        [
            new ElvTvUcu { Ad = "TV 1", KatNo = 0, KatAdi = "Zemin Kat" },
            new ElvTvUcu { Ad = "TV 2", KatNo = 0, KatAdi = "Zemin Kat" },
            new ElvTvUcu { Ad = "TV 3", KatNo = 1, KatAdi = "1. Kat" }
        ]);
        ElvTvTopolojiServisi.TeknolojiyiHazirla(tv, TvDagitimTeknolojisi.IpTv);

        Assert.Equal(2, tv.IpTv.RackKabinler.Count);
        Assert.Equal(3, tv.IpTv.ToplamUc);
        Assert.All(tv.IpTv.RackKabinler.SelectMany(r => r.TvHatlari), h => Assert.Contains("Cat", h.KabloTipi));
        Assert.Equal(172.8, tv.IpTv.GerekliMerkezBantGenisligiMbps, 1);

        tv.IpTv.AgAyarlari.IgmpSnooping = false;
        Assert.Contains(ElvTvTopolojiServisi.Dogrula(tv), s => s.Kaynak == "IGMP" && s.Hata);
        tv.IpTv.RackKabinler.OrderByDescending(r => r.TvPrizSayisi).First()
            .Cihazlar.Add(new ElvCihaz { Ad = "Managed Switch 1 Port" });
        Assert.Contains(ElvTvTopolojiServisi.Dogrula(tv), s => s.Mesaj.Contains("Switch portu"));
    }

    [Fact]
    public void TvUcTeknolojideKorunur_DirektVeIpCiktilariUretilir()
    {
        var tv = new ElvTvSistemi();
        tv.OrtakUclar.Add(new ElvTvUcu { Ad = "Test TV", KatNo = 0, KatAdi = "Zemin Kat" });
        string taban = Path.Combine(Path.GetTempPath(), $"tv_uc_teknoloji_{Guid.NewGuid():N}");
        try
        {
            ElvTvTopolojiServisi.TeknolojiyiHazirla(tv, TvDagitimTeknolojisi.DirektLnb);
            ZayifAkimTekHatSvgServisi.TvSvgCiktiAl(tv, taban + "_direkt.svg");
            ZayifAkimTekHatDxfServisi.TvDxfCiktiAl(tv, taban + "_direkt.dxf");
            Assert.Contains("Test TV", File.ReadAllText(taban + "_direkt.svg"));

            ElvTvTopolojiServisi.TeknolojiyiHazirla(tv, TvDagitimTeknolojisi.IpTv);
            ZayifAkimTekHatSvgServisi.TvSvgCiktiAl(tv, taban + "_ip.svg");
            Assert.Contains("IP-TV", File.ReadAllText(taban + "_ip.svg"));
            Assert.Single(tv.OrtakUclar);
        }
        finally
        {
            foreach (var uz in new[] { "_direkt.svg", "_direkt.dxf", "_ip.svg" })
                if (File.Exists(taban + uz)) File.Delete(taban + uz);
        }
    }

    [Fact]
    public void ImportServisi_M12DenYukle_YanginLooplariKatPanosuzTest()
    {
        var m12Proje = new BasitProjeCizProje { ProjeAdi = "Yangın Test Projesi" };
        var kat = new BasitProjeKat { KatNo = 1, Ad = "1. Kat" };

        kat.Elemanlar.Add(new BasitProjeEleman { Id = "ys-1", Tip = BpcElemanTipi.YanginSantrali, Ad = "Yangın Santrali" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "d-1", Tip = BpcElemanTipi.DumanDedektor, Ad = "Duman Dedektörü 1" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "d-2", Tip = BpcElemanTipi.IsiDedektor, Ad = "Isı Dedektörü 1" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "b-1", Tip = BpcElemanTipi.YanginButonu, Ad = "Yangın Butonu" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "s-1", Tip = BpcElemanTipi.Siren, Ad = "Siren" });

        kat.SistemDevreleri.Add(new BpcSistemDevre
        {
            Sistem = BpcPaftaTipi.Yangin,
            MerkezId = "ys-1",
            CihazIds = new() { "d-1", "d-2" }
        });
        kat.SistemDevreleri.Add(new BpcSistemDevre
        {
            Sistem = BpcPaftaTipi.Yangin,
            MerkezId = "ys-1",
            CihazIds = new() { "b-1", "s-1" }
        });
        m12Proje.Katlar.Add(kat);

        var tempPath = Path.Combine(Path.GetTempPath(), "test_m12_yangin.e4p12");
        var dxfPath = Path.Combine(Path.GetTempPath(), "test_yangin_output.dxf");
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        File.WriteAllText(tempPath, JsonSerializer.Serialize(m12Proje, opts));

        try
        {
            var m13Proje = ZayifAkimTekHatImportServisi.M12DenYukle(tempPath);

            Assert.Empty(m13Proje.YanginSistemi.KatLoopGruplari.SelectMany(k => k.Looplar).Where(l => l.KabloTipi.Contains("Cat", StringComparison.OrdinalIgnoreCase)));
            Assert.Single(m13Proje.YanginSistemi.KatLoopGruplari);
            Assert.Equal(0, m13Proje.YanginSistemi.AnaMerkez.KatNo);

            var grup = m13Proje.YanginSistemi.KatLoopGruplari[0];
            Assert.Equal(2, grup.LoopSayisi);
            Assert.Equal(2, grup.Looplar.Count);
            Assert.Equal(4, grup.ToplamCihaz);
            Assert.Equal(
                [ElvYanginCihazTipi.DumanDedektoru, ElvYanginCihazTipi.IsiDedektoru],
                grup.Looplar[0].Cihazlar.Select(c => c.Tip).ToArray());
            Assert.Equal(
                [ElvYanginCihazTipi.YanginButonu, ElvYanginCihazTipi.Siren],
                grup.Looplar[1].Cihazlar.Select(c => c.Tip).ToArray());
            Assert.All(grup.Looplar, l => Assert.Equal("J-Y(St)Y 1x2x0.8 Yangın Kablosu", l.KabloTipi));

            ZayifAkimTekHatDxfServisi.YanginDxfCiktiAl(m13Proje.YanginSistemi, dxfPath);
            Assert.True(File.Exists(dxfPath));
            Assert.True(new FileInfo(dxfPath).Length > 0);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (File.Exists(dxfPath)) File.Delete(dxfPath);
        }
    }

    [Fact]
    public void YanginLoopCihazZinciri_ToplamMetrajiSegmentlerdenHesaplarVeJsonKorur()
    {
        var loop = new ElvYanginLoop
        {
            LoopNo = 1,
            Ad = "Loop 1",
            Cihazlar =
            [
                new ElvYanginLoopCihazi { Tip = ElvYanginCihazTipi.DumanDedektoru, Ad = "Duman Dedektörü", OncekiCihazaMesafeMetre = 12.5 },
                new ElvYanginLoopCihazi { Tip = ElvYanginCihazTipi.YanginButonu, Ad = "Yangın İhbar Butonu", OncekiCihazaMesafeMetre = 7.25 },
                new ElvYanginLoopCihazi { Tip = ElvYanginCihazTipi.Flasor, Ad = "Flaşör", OncekiCihazaMesafeMetre = 3 }
            ]
        };

        Assert.Equal(3, loop.CihazSayisi);
        Assert.Equal(22.75, loop.Metraj, 2);

        var json = JsonSerializer.Serialize(loop);
        var tekrar = JsonSerializer.Deserialize<ElvYanginLoop>(json)!;
        Assert.Equal(3, tekrar.CihazSayisi);
        Assert.Equal(22.75, tekrar.Metraj, 2);
        Assert.Equal(ElvYanginCihazTipi.YanginButonu, tekrar.Cihazlar[1].Tip);
    }

    [Fact]
    public void YanginLoopEditoru_CihazEklerkenSecimiKorur()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.SistemSec("Yangin");
        var loop = new ElvYanginLoop { LoopNo = 1, Ad = "Loop 1" };
        var grup = new ElvYanginKatLoopGrubu
        {
            Ad = "Zemin Yangın Loopları",
            KatNo = 0,
            KatAdi = "Zemin Kat",
            LoopSayisi = 1,
            Looplar = [loop]
        };
        vm.Proje.YanginSistemi.KatLoopGruplari.Add(grup);
        vm.YanginLoopGrubuSec(grup);
        vm.SeciliYanginLoop = loop;

        vm.YanginLoopCihaziEkle(nameof(ElvYanginCihazTipi.DumanDedektoru));

        Assert.Same(loop, vm.SeciliYanginLoop);
        Assert.Same(grup, vm.SeciliYanginKatLoopGrubu);
        Assert.Single(loop.Cihazlar);
        Assert.Contains("Duman Dedektörü", vm.DurumMesaji);

        vm.YanginLoopKabloMetrajiDegisti(loop.Cihazlar[0], 14.75);
        Assert.Same(loop, vm.SeciliYanginLoop);
        Assert.True(vm.YanginLoopEditorGorunur);
        Assert.Equal(14.75, loop.Metraj, 2);
    }

    [Fact]
    public void YanginLoopEditoru_HerCihazTipineAyriSimgeVerir()
    {
        var converter = new YanginCihazSimgeConverter();
        var simgeler = Enum.GetValues<ElvYanginCihazTipi>()
            .Where(t => t != ElvYanginCihazTipi.Bilinmeyen)
            .Select(t => converter.Convert(t, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)?.ToString())
            .ToList();

        Assert.Equal(7, simgeler.Count);
        Assert.Equal(7, simgeler.Distinct().Count());
    }

    [Fact]
    public void YanginSvg_HerLoopIcinAyriCikisVeKoridorKullanir()
    {
        var sistem = new ElvYanginSistemi();
        sistem.KatLoopGruplari.Add(new ElvYanginKatLoopGrubu
        {
            KatNo = 0,
            KatAdi = "Zemin Kat",
            LoopSayisi = 3,
            Looplar =
            [
                new ElvYanginLoop { LoopNo = 1, Ad = "Loop 1" },
                new ElvYanginLoop { LoopNo = 2, Ad = "Loop 2" },
                new ElvYanginLoop { LoopNo = 3, Ad = "Loop 3" }
            ]
        });
        string yol = Path.Combine(Path.GetTempPath(), $"yangin_offset_{Guid.NewGuid():N}.svg");
        try
        {
            ZayifAkimTekHatSvgServisi.YanginSvgCiktiAl(sistem, yol);
            var svg = File.ReadAllText(yol);
            var hatlar = System.Text.RegularExpressions.Regex.Matches(
                    svg, "<polyline points=\\\"([^\\\"]+)\\\"[^>]+stroke=\\\"#E05050\\\"")
                .Select(m => m.Groups[1].Value.Split(' '))
                .ToList();

            Assert.Equal(3, hatlar.Count);
            Assert.Equal(3, hatlar.Select(h => h[0].Split(',')[1]).Distinct().Count());
            Assert.Equal(3, hatlar.Select(h => h[1].Split(',')[0]).Distinct().Count());
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    [Fact]
    public void DataRack_EkUcBirimleri_ToplamVeDxf()
    {
        var data = new ElvDataSistemi();
        var rack = new ElvRackKabin
        {
            Ad = "1. Kat Rack",
            KatNo = 1,
            KatAdi = "1. Kat",
            DataHatSayisi = 10,
            WirelessSayisi = 2,
            IpTelefonSayisi = 5,
            YaziciSayisi = 1,
            ErisimKontrolSayisi = 3,
            DiafonSayisi = 2
        };
        rack.IpTelefonHatlari.Add(new ElvHatMetraj { HatNo = 1, Ad = "IP Telefon 1", Metraj = 12 });
        rack.DiafonHatlari.Add(new ElvHatMetraj { HatNo = 1, Ad = "Diafon/IP Ses 1", Metraj = 8 });
        data.RackKabinler.Add(rack);

        // Rack ve sistem toplamları
        Assert.Equal(11, rack.DataEkUcSayisi); // 5 + 1 + 3 + 2
        Assert.Equal(5, data.ToplamIpTelefon);
        Assert.Equal(1, data.ToplamYazici);
        Assert.Equal(3, data.ToplamErisimKontrol);
        Assert.Equal(2, data.ToplamDiafon);
        Assert.Equal(12, data.ToplamIpTelefonHatMetraj);
        Assert.Equal(8, data.ToplamDiafonHatMetraj);

        // DXF çıktısı ek uç birimlerle üretilebilmeli
        var dxfPath = Path.Combine(Path.GetTempPath(), $"m13_ekuc_{Guid.NewGuid():N}.dxf");
        try
        {
            ZayifAkimTekHatDxfServisi.DataDxfCiktiAl(data, dxfPath);
            Assert.True(File.Exists(dxfPath));
            Assert.True(new FileInfo(dxfPath).Length > 0);
        }
        finally
        {
            if (File.Exists(dxfPath)) File.Delete(dxfPath);
        }
    }

    [Fact]
    public void ViewModel_SeciliUstRackId_ModeliVeRevizyonuGunceller()
    {
        var vm = new ZayifAkimTekHatViewModel();   // YeniProje → Data sistemi
        vm.RackEkleKata(0);                         // Zemin rack
        vm.RackEkleKata(2);                         // 2. kat rack
        var racks = vm.DataSistemi.RackKabinler;
        var zemin = racks.First(r => r.KatNo == 0);
        var ikinci = racks.First(r => r.KatNo == 2);

        vm.SeciliRack = ikinci;                     // 2. kat rack'i seç
        int revOnce = vm.SemaRevizyon;

        vm.SeciliUstRackId = zemin.Id;              // üstünü zemin rack yap

        Assert.Equal(zemin.Id, ikinci.UstRackId);   // model güncellendi
        Assert.True(vm.SemaRevizyon > revOnce);     // şema yenileme tetiklendi
        Assert.Equal(zemin.Id, vm.SeciliUstRackId); // getter yeni değeri döndürür

        // Ana Merkez'e geri al ("" sentinel → null)
        vm.SeciliUstRackId = "";
        Assert.Null(ikinci.UstRackId);
    }

    [Fact]
    public void ViewModel_UstBaglantiDegisince_ComboSecenekListesiYenidenKurulmaz()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.RackEkleKata(0);
        vm.RackEkleKata(2);
        var zemin = vm.DataSistemi.RackKabinler.First(r => r.KatNo == 0);
        var ikinci = vm.DataSistemi.RackKabinler.First(r => r.KatNo == 2);
        vm.SeciliRack = ikinci;

        var secenekler = vm.UstBaglantiSecenekleri;
        bool itemsSourceYenilendi = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.UstBaglantiSecenekleri))
                itemsSourceYenilendi = true;
        };

        vm.UstBaglantiUygula(zemin.Id);

        Assert.Equal(zemin.Id, ikinci.UstRackId);
        Assert.False(itemsSourceYenilendi);
        Assert.Equal(secenekler.Select(x => x.Id), vm.UstBaglantiSecenekleri.Select(x => x.Id));
    }

    [Fact]
    public void Rack_KatIciKabloMetraji_TumTekilHatlarinOtomatikToplamidir()
    {
        var rack = new ElvRackKabin { KatIciKabloMetraj = 999 };
        rack.DataHatlari.Add(new ElvHatMetraj { Metraj = 10 });
        rack.WirelessHatlari.Add(new ElvHatMetraj { Metraj = 6 });
        rack.IpTelefonHatlari.Add(new ElvHatMetraj { Metraj = 4 });
        rack.KameraHatlari.Add(new ElvHatMetraj { Metraj = 12 });
        rack.AnonsHatlari.Add(new ElvAnonsHatti { Metraj = 8 });
        rack.TvHatlari.Add(new ElvHatMetraj { Metraj = 5 });

        Assert.Equal(45, rack.KatIciKabloMetraj);
    }

    [Fact]
    public void ViewModel_MerkezKabloMetrajiDegisince_SemaAnindaYenilenir()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.RackEkleKata(0);
        int oncekiRevizyon = vm.SemaRevizyon;

        vm.MerkezeKabloMetrajiDegistir(37.5);

        Assert.Equal(37.5, vm.SeciliRack!.MerkezeKabloMetraj);
        Assert.True(vm.SemaRevizyon > oncekiRevizyon);
        Assert.Contains("37", vm.ToplamMerkezKabloText);
    }

    [Fact]
    public void ViewModel_GenelBilgiMetrajlari_KabloTipineGoreAyriToplanir()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.RackEkleKata(0);
        var rack = vm.SeciliRack!;
        rack.DataHatlari.Add(new ElvHatMetraj { KabloTipi = "Cat6 UTP", Metraj = 10 });
        rack.DataHatlari.Add(new ElvHatMetraj { KabloTipi = "Cat6 UTP", Metraj = 5 });
        rack.IpTelefonHatlari.Add(new ElvHatMetraj { KabloTipi = "Cat6 U/UTP", Metraj = 7 });

        Assert.Contains("Cat6 UTP: 15", vm.ToplamHatMetrajText);
        Assert.Contains("Cat6 U/UTP: 7", vm.ToplamHatMetrajText);
    }

    [Fact]
    public void KameraRack_UcKameraTipiniAyriTutarVeToplar()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.SistemSec("Kamera");
        vm.RackEkleKata(0);

        vm.SeciliRackDahiliKameraSayisi = 2;
        vm.SeciliRackHariciKameraSayisi = 3;
        vm.SeciliRackPtzKameraSayisi = 1;

        var rack = vm.SeciliRack!;
        Assert.Equal(6, rack.KameraSayisi);
        Assert.Equal(6, rack.KameraHatlari.Count);
        Assert.Equal(2, rack.KameraHatlari.Count(h => h.Ad.StartsWith("Dahili Kamera")));
        Assert.Equal(3, rack.KameraHatlari.Count(h => h.Ad.StartsWith("Harici Kamera")));
        Assert.Single(rack.KameraHatlari.Where(h => h.Ad.StartsWith("PTZ Kamera")));

        var eskiRack = new ElvRackKabin { KameraSayisi = 4 };
        Assert.Equal(4, eskiRack.DahiliKameraSayisi);
        Assert.Equal(4, eskiRack.KameraSayisi);
    }

    [Fact]
    public void KameraCihazKatalogu_YuksekKapasiteliKayitVeDepolamaKalemleriniIcerir()
    {
        Assert.Contains("NVR 128 Kanal", ElvCihazKatalogu.KameraCihazlari);
        Assert.Contains("NVR 256 Kanal", ElvCihazKatalogu.KameraCihazlari);
        Assert.Contains("Gözetim HDD 8 TB", ElvCihazKatalogu.KameraCihazlari);
        Assert.Contains("Gözetim HDD 24 TB", ElvCihazKatalogu.KameraCihazlari);
        Assert.Contains("IP SAN / NAS Depolama", ElvCihazKatalogu.KameraCihazlari);
        Assert.Contains("Video Analiz Sunucusu", ElvCihazKatalogu.KameraCihazlari);
    }

    [Fact]
    public void DataCihazKatalogu_AnaMerkezVeKatRackEkipmanlariniKapsar()
    {
        Assert.Contains("Omurga Switch L3 48 Port 10G", ElvCihazKatalogu.DataAnaMerkezCihazlari);
        Assert.Contains("Sanallaştırma Sunucusu", ElvCihazKatalogu.DataAnaMerkezCihazlari);
        Assert.Contains("Firewall / UTM Güvenlik Duvarı", ElvCihazKatalogu.DataAnaMerkezCihazlari);
        Assert.Contains("PoE++ Switch 48 Port", ElvCihazKatalogu.DataKatRackCihazlari);
        Assert.Contains("Patch Panel 24 Port Cat6A", ElvCihazKatalogu.DataKatRackCihazlari);
        Assert.Contains("SFP+ Modül 10G Singlemode", ElvCihazKatalogu.DataKatRackCihazlari);
    }

    [Fact]
    public void AnonsTeknolojisi_DijitalPoeSecilirVeYeniHatKabloVarsayilaniDegisir()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.SistemSec("Anons");
        vm.SeciliAnonsTeknolojisi = "Dijital (PoE)";
        vm.RackEkleKata(0);
        vm.SeciliRackIpTavanHoparlorSayisi = 2;
        vm.SeciliRackIpHornHoparlorSayisi = 1;
        vm.SeciliRackIpAnonsTerminaliSayisi = 1;

        Assert.Equal("Dijital (PoE)", vm.AnonsSistemi.Teknoloji);
        Assert.Equal("Fiber 12 Core Singlemode", vm.SeciliRack!.MerkezeKabloTipi);
        Assert.Equal(4, vm.SeciliRack.DijitalAnonsUclari.Count);
        Assert.All(vm.SeciliRack.DijitalAnonsUclari, h => Assert.Equal("Cat6A F/UTP LSZH", h.KabloTipi));
        Assert.Equal(3, vm.AnonsSistemi.ToplamHoparlor);
        Assert.Contains(vm.SeciliRack.DijitalAnonsUclari, h => h.Ad.StartsWith("IP Horn Hoparlör"));
        Assert.Contains("Yönetilebilir PoE Switch 24 Port", vm.CihazKatalogu);

        vm.MerkeziSec();
        Assert.Contains("Dijital Anons Kontrol Sunucusu", vm.CihazKatalogu);

        var json = JsonSerializer.Serialize(vm.Proje);
        var geri = JsonSerializer.Deserialize<ZayifAkimTekHatProje>(json);
        Assert.Equal("Dijital (PoE)", geri!.AnonsSistemi.Teknoloji);
    }

    [Fact]
    public void AnonsTeknolojisiDegisince_MevcutCihazVeKablolarTemizlenir()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.SistemSec("Anons");
        var eskiRack = new ElvRackKabin { Ad = "Eski Anons Rack", KatNo = 0, KatAdi = "Zemin" };
        eskiRack.Cihazlar.Add(new ElvCihaz { Ad = "Switch" });
        eskiRack.AnonsHatlari.Add(new ElvAnonsHatti { Ad = "Eski Hat", Metraj = 20 });
        eskiRack.AnonsHatSayisi = 1;
        eskiRack.MerkezeKabloMetraj = 25;
        vm.AnonsSistemi.RackKabinler.Add(eskiRack);
        vm.SeciliRack = eskiRack;
        vm.AnonsSistemi.AnaMerkez.Cihazlar.Add(new ElvCihaz { Ad = "Amfi" });
        Assert.True(vm.AnonsTeknolojiDegisikligindeSilinecekVeriVar);

        vm.AnonsTeknolojisiniDegistirVeTemizle("Dijital (PoE)");

        Assert.Empty(vm.AnonsSistemi.AnaMerkez.Cihazlar);
        Assert.Empty(eskiRack.Cihazlar);
        Assert.Empty(eskiRack.AnonsHatlari);
        Assert.Empty(eskiRack.DijitalAnonsUclari);
        Assert.Equal(0, eskiRack.MerkezeKabloMetraj);
        Assert.Equal("Fiber 12 Core Singlemode", eskiRack.MerkezeKabloTipi);
    }

    [Fact]
    public void AnalogAnonsDomaini_ZonABHatVeGucToplamlariniKorur()
    {
        var zon = new ElvAnonsZonu
        {
            Ad = "Zemin Tahliye Zonu",
            KatNo = 0,
            HatA = new ElvAnalogHoparlorHatti
            {
                Ad = "Zemin Hat A",
                Kol = ElvAnonsHatKolu.A,
                EolDenetimiVar = true,
                HoparlorGruplari =
                [
                    new ElvAnalogHoparlorGrubu { HoparlorTipi = "Tavan Tipi 100 V", Adet = 10, TapGucuW = 6 }
                ]
            },
            HatB = new ElvAnalogHoparlorHatti
            {
                Ad = "Zemin Hat B",
                Kol = ElvAnonsHatKolu.B,
                HoparlorGruplari =
                [
                    new ElvAnalogHoparlorGrubu { HoparlorTipi = "Duvar Tipi 100 V", Adet = 4, TapGucuW = 10 }
                ]
            }
        };
        var proje = new ZayifAkimTekHatProje();
        proje.AnonsSistemi.AnalogZonlar.Add(zon);
        proje.AnonsSistemi.AnalogAmplifikatorler.Add(new ElvAnonsAmplifikatoru { NominalGucW = 240 });
        proje.AnonsSistemi.AnalogMesajSenaryolari.Add(new ElvAnonsMesajSenaryosu { ZonIds = [zon.Id] });

        Assert.True(zon.CiftHatli);
        Assert.Equal(14, zon.ToplamHoparlor);
        Assert.Equal(100, zon.ToplamGucW);

        var json = JsonSerializer.Serialize(proje);
        var geri = JsonSerializer.Deserialize<ZayifAkimTekHatProje>(json)!;
        Assert.Single(geri.AnonsSistemi.AnalogZonlar);
        Assert.Equal(100, geri.AnonsSistemi.AnalogZonlar[0].ToplamGucW);
        Assert.Single(geri.AnonsSistemi.AnalogAmplifikatorler);
        Assert.Single(geri.AnonsSistemi.AnalogMesajSenaryolari);
    }

    [Fact]
    public void AnalogAnonsDomaini_EskiProjeAlanlariOlmadanVarsayilanOlusur()
    {
        const string eskiJson = "{\"projeAdi\":\"Eski\",\"anonsSistemi\":{\"teknoloji\":\"Analog\",\"rackKabinler\":[]}}";
        var proje = JsonSerializer.Deserialize<ZayifAkimTekHatProje>(eskiJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.NotNull(proje.AnonsSistemi.AnalogZonlar);
        Assert.Empty(proje.AnonsSistemi.AnalogZonlar);
        Assert.NotNull(proje.AnonsSistemi.AnalogGucKaynagi);
    }

    [Fact]
    public void AnalogHoparlorKatalogu_TipleriVeTapDegerleriniDogrular()
    {
        Assert.Contains(ElvAnalogHoparlorKatalogu.Kalemler, k => k.Kategori == "Tavan");
        Assert.Contains(ElvAnalogHoparlorKatalogu.Kalemler, k => k.Kategori == "Horn");
        Assert.Contains(ElvAnalogHoparlorKatalogu.Kalemler, k => k.Kategori == "Line-Array");
        Assert.Contains(ElvAnalogHoparlorKatalogu.Kalemler,
            k => k.Ortam == ElvHoparlorOrtamTipi.DisMekan && k.En54_24UrunSecilmeli);

        var grup = ElvAnalogHoparlorServisi.KatalogdanGrupOlustur("HORN-30W", 4);
        Assert.Equal(30, grup.ToplamGucW); // varsayılan 7.5 W × 4
        Assert.True(ElvAnalogHoparlorServisi.TapGucunuAyarla(grup, 15));
        Assert.Equal(60, grup.ToplamGucW);
        Assert.False(ElvAnalogHoparlorServisi.TapGucunuAyarla(grup, 12));
        Assert.Equal(15, grup.TapGucuW); // geçersiz seçim önceki değeri korur
        Assert.True(ElvAnalogHoparlorServisi.TapGucunuDogrula(grup).Gecerli);
    }

    [Fact]
    public void AnalogAnonsTopolojisi_HoparlorleriABHatlarinaDengeliDagitir()
    {
        var zon = ElvAnalogAnonsTopolojiServisi.ZonOlustur("Zemin", 0, "Zemin Kat", ciftHatli: true);
        var sonuc = ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(zon, "TAVAN-6W", 11, 6, "Koridor");

        Assert.True(sonuc.Basarili);
        Assert.Equal(6, zon.HatA.ToplamHoparlor);
        Assert.Equal(5, zon.HatB!.ToplamHoparlor);
        Assert.Equal(66, zon.ToplamGucW);
        Assert.NotEqual(zon.HatA.HoparlorGruplari[0].Id, zon.HatB.HoparlorGruplari[0].Id);
        Assert.True(ElvAnalogAnonsTopolojiServisi.Dogrula(zon).Gecerli);
    }

    [Fact]
    public void AnalogAnonsTopolojisi_HatBVerisiniKaybetmedenTekHataDonusturur()
    {
        var zon = ElvAnalogAnonsTopolojiServisi.ZonOlustur("1. Kat", 1, "1. Kat", ciftHatli: true);
        ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(zon, "DUVAR-10W", 4, 5);
        zon.HatB!.Metraj = 30;

        var reddedilen = ElvAnalogAnonsTopolojiServisi.TekHataDonustur(zon, hatBVerisiniHatAyaTasi: false);
        Assert.False(reddedilen.Basarili);
        Assert.NotNull(zon.HatB);

        var aktarilan = ElvAnalogAnonsTopolojiServisi.TekHataDonustur(zon, hatBVerisiniHatAyaTasi: true);
        Assert.True(aktarilan.Basarili);
        Assert.Null(zon.HatB);
        Assert.Equal(ElvAnonsHatTopolojisi.TekHat, zon.Topoloji);
        Assert.Equal(4, zon.HatA.ToplamHoparlor);
        Assert.Equal(30, zon.HatA.Metraj);
        Assert.True(ElvAnalogAnonsTopolojiServisi.Dogrula(zon).Gecerli);
    }

    [Fact]
    public void AnalogAnonsGucHesabi_RezervKanalVeYedekKapasitesiniDogrular()
    {
        var sistem = new ElvAnonsSistemi { Teknoloji = "Analog" };
        var anaAmp = new ElvAnonsAmplifikatoru { Ad = "Ana 240 W", NominalGucW = 240, KanalSayisi = 2 };
        var yedekAmp = new ElvAnonsAmplifikatoru { Ad = "Yedek 240 W", NominalGucW = 240, KanalSayisi = 2, Yedek = true };
        sistem.AnalogAmplifikatorler.AddRange([anaAmp, yedekAmp]);
        var zon = ElvAnalogAnonsTopolojiServisi.ZonOlustur("Zemin", 0, "Zemin", ciftHatli: true);
        zon.HatA.AmplifikatorId = anaAmp.Id;
        zon.HatB!.AmplifikatorId = anaAmp.Id;
        ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(zon, "TAVAN-10W", 20, 10);
        sistem.AnalogZonlar.Add(zon);

        var rapor = ElvAnalogAnonsGucServisi.Hesapla(sistem, 20);

        Assert.Equal(200, rapor.ToplamHoparlorYukuW);
        Assert.Equal(240, rapor.ToplamRezervliGucW);
        Assert.True(rapor.YedekKapasitesiYeterli);
        Assert.True(rapor.Gecerli);
        Assert.Equal(240, ElvAnalogAnonsGucServisi.OnerilenStandartAmplifikatorGucu(200, 20));
    }

    [Fact]
    public void AnalogAnonsGucHesabi_AsiriYukVeEksikYedegiRaporlar()
    {
        var sistem = new ElvAnonsSistemi { Teknoloji = "Analog" };
        var amp = new ElvAnonsAmplifikatoru { Ad = "Küçük Amfi", NominalGucW = 120, KanalSayisi = 1 };
        sistem.AnalogAmplifikatorler.Add(amp);
        var zon = ElvAnalogAnonsTopolojiServisi.ZonOlustur("Salon", 0, "Zemin", ciftHatli: true);
        zon.AmplifikatorId = amp.Id;
        ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(zon, "HORN-30W", 8, 30);
        sistem.AnalogZonlar.Add(zon);

        var rapor = ElvAnalogAnonsGucServisi.Hesapla(sistem);

        Assert.False(rapor.Gecerli);
        Assert.Contains(rapor.Sorunlar, s => s.Contains("kapasiteyi aşıyor"));
        Assert.Contains(rapor.Sorunlar, s => s.Contains("kanal kapasitesi"));
        Assert.Contains(rapor.Sorunlar, s => s.Contains("yedek kapasitesi yetersiz"));
    }

    [Fact]
    public void AnalogHatDenetimi_EolDalVeBagimsizABGuzergahiniDogrular()
    {
        var sistem = new ElvAnonsSistemi { Teknoloji = "Analog" };
        var zon = ElvAnalogAnonsTopolojiServisi.ZonOlustur("Zemin", 0, "Zemin", ciftHatli: true);
        ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(zon, "TAVAN-6W", 8, 6);
        foreach (var hat in new[] { zon.HatA, zon.HatB! })
        {
            hat.DenetimYontemi = ElvHatDenetimYontemi.EolVeEmpedans;
            hat.DalSayisi = 1;
            hat.EolModulAdedi = 1;
            hat.HatDurumu = ElvHoparlorHattiDurumu.Normal;
        }
        zon.HatA.GuzergahKodu = "Şaft-A";
        zon.HatB!.GuzergahKodu = "Şaft-B";
        sistem.AnalogZonlar.Add(zon);

        var rapor = ElvAnalogAnonsHatDenetimServisi.Denetle(sistem);
        Assert.True(rapor.Gecerli);
        Assert.Equal(0, rapor.HataSayisi);
    }

    [Fact]
    public void AnalogHatDenetimi_DenetimsizArizaliVeOrtakGuzergahiRaporlar()
    {
        var sistem = new ElvAnonsSistemi { Teknoloji = "Analog" };
        var zon = ElvAnalogAnonsTopolojiServisi.ZonOlustur("Salon", 0, "Zemin", ciftHatli: true);
        ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(zon, "HORN-30W", 6, 15);
        zon.HatA.KabloTipi = "Cat6 UTP";
        zon.HatA.GuzergahKodu = "Ortak Şaft";
        zon.HatB!.GuzergahKodu = "Ortak Şaft";
        zon.HatB.DenetimYontemi = ElvHatDenetimYontemi.Eol;
        zon.HatB.DalSayisi = 2;
        zon.HatB.EolModulAdedi = 1;
        zon.HatB.HatDurumu = ElvHoparlorHattiDurumu.KisaDevre;
        sistem.AnalogZonlar.Add(zon);

        var rapor = ElvAnalogAnonsHatDenetimServisi.Denetle(sistem);
        Assert.False(rapor.Gecerli);
        Assert.Contains(rapor.Sorunlar, s => s.Mesaj.Contains("hat denetimi tanımlı değil"));
        Assert.Contains(rapor.Sorunlar, s => s.Mesaj.Contains("EOL modülü yetersiz"));
        Assert.Contains(rapor.Sorunlar, s => s.Mesaj.Contains("kısa devre"));
        Assert.Contains(rapor.Sorunlar, s => s.Mesaj.Contains("bağımsız güzergâhlardan"));
        Assert.Contains(rapor.Sorunlar, s => s.Mesaj.Contains("yangına dayanımlı"));
    }

    [Fact]
    public void AnalogAnaMerkezKatalogu_TemelEn54CihazlariniVeAmplifikatorleriIcerir()
    {
        var katalog = ElvAnalogAnonsAnaMerkezKatalogu.Kalemler;
        Assert.Contains(katalog, k => k.Kod == "CTRL-6Z" && k.Standard == "EN 54-16");
        Assert.Contains(katalog, k => k.Kod == "MIC-EMG" && k.TemelSistemKalemi);
        Assert.Contains(katalog, k => k.Kod == "LINE-SUP" && k.TemelSistemKalemi);
        Assert.Contains(katalog, k => k.Kod == "FACP-IO" && k.TemelSistemKalemi);
        Assert.Contains(katalog, k => k.Kod == "PSU-24" && k.Standard == "EN 54-4");

        var cihaz = ElvAnalogAnonsAnaMerkezKatalogu.CihazOlustur("CTRL-6Z");
        Assert.Equal("CTRL-6Z", cihaz.KatalogKodu);
        Assert.Contains("6 Zon", cihaz.Ad);

        var amp = ElvAnalogAnonsAnaMerkezKatalogu.AmplifikatorOlustur("AMP-4X120", yedek: true);
        Assert.True(amp.Yedek);
        Assert.Equal(480, amp.NominalGucW);
        Assert.Equal(4, amp.KanalSayisi);
    }

    [Fact]
    public void AnalogAnonsUiAkisi_ZonHoparlorVeAmplifikatorEkler()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.SistemSec("Anons");
        vm.RackEkleKata(0);

        Assert.True(vm.AnalogAnonsAktif);
        Assert.Single(vm.AnonsSistemi.AnalogZonlar);
        Assert.NotNull(vm.SeciliAnalogAnonsZonu);
        Assert.True(vm.SeciliAnalogCiftHat);

        vm.YeniAnalogHoparlorKodu = "TAVAN-6W";
        vm.YeniAnalogHoparlorAdedi = 5;
        vm.YeniAnalogHoparlorTapGucu = 6;
        vm.AnalogHoparlorEkle();
        Assert.Equal(3, vm.SeciliAnalogAnonsZonu!.HatA.ToplamHoparlor);
        Assert.Equal(2, vm.SeciliAnalogAnonsZonu.HatB!.ToplamHoparlor);

        vm.YeniAnalogAmplifikatorKodu = "AMP-240";
        vm.AnalogAmplifikatorEkle();
        Assert.Single(vm.AnonsSistemi.AnalogAmplifikatorler);
        Assert.Equal(240, vm.AnonsSistemi.AnalogAmplifikatorler[0].NominalGucW);
    }

    [Fact]
    public void AnalogHoparlorEkleme_AyniGrubuBirlestirirSegmentMetrajlariniVeSilmeyiKorur()
    {
        var zon = ElvAnalogAnonsTopolojiServisi.ZonOlustur("Zemin", 0, "Zemin", ciftHatli: true);
        ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(zon, "TAVAN-6W", 4, 6);
        ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(zon, "TAVAN-6W", 2, 6);

        Assert.Single(zon.HatA.HoparlorGruplari);
        Assert.Single(zon.HatB!.HoparlorGruplari);
        Assert.Equal(3, zon.HatA.HoparlorGruplari[0].Adet);
        Assert.Equal(3, zon.HatB.HoparlorGruplari[0].Adet);
        Assert.Equal(3, zon.HatA.KabloSegmentleri.Count);
        Assert.Equal("Hat A Çıkışı", zon.HatA.KabloSegmentleri[0].Baslangic);
        Assert.Equal(zon.HatA.KabloSegmentleri[0].Bitis, zon.HatA.KabloSegmentleri[1].Baslangic);

        zon.HatA.KabloSegmentleri[0].Metraj = 12;
        zon.HatA.KabloSegmentleri[1].Metraj = 8;
        zon.HatA.KabloSegmentleri[2].Metraj = 5;
        ElvAnalogAnonsTopolojiServisi.KabloSegmentleriniEsitle(zon.HatA);
        Assert.Equal(25, zon.HatA.Metraj);

        Assert.True(ElvAnalogAnonsTopolojiServisi.HoparlorGrubunuSil(
            zon.HatA, zon.HatA.HoparlorGruplari[0].Id));
        Assert.Empty(zon.HatA.HoparlorGruplari);
        Assert.Empty(zon.HatA.KabloSegmentleri);
    }

    [Fact]
    public void AnalogAnonsBirlesikDogrulamaVeDxfCiktisi_Calisir()
    {
        var sistem = new ElvAnonsSistemi { Teknoloji = "Analog" };
        var amp = ElvAnalogAnonsAnaMerkezKatalogu.AmplifikatorOlustur("AMP-4X120");
        var yedek = ElvAnalogAnonsAnaMerkezKatalogu.AmplifikatorOlustur("AMP-4X120", true);
        sistem.AnalogAmplifikatorler.AddRange([amp, yedek]);
        var zon = ElvAnalogAnonsTopolojiServisi.ZonOlustur("Zemin", 0, "Zemin", true);
        zon.HatA.AmplifikatorId = amp.Id;
        zon.HatB!.AmplifikatorId = amp.Id;
        ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(zon, "TAVAN-6W", 6, 6);
        foreach (var hat in new[] { zon.HatA, zon.HatB })
        {
            foreach (var grup in hat.HoparlorGruplari) grup.En54_24Uygun = true;
            hat.DenetimYontemi = ElvHatDenetimYontemi.EolVeEmpedans;
            hat.EolModulAdedi = 1;
            hat.HatDurumu = ElvHoparlorHattiDurumu.Normal;
            hat.GuzergahKodu = hat.Kol == ElvAnonsHatKolu.A ? "Şaft-A" : "Şaft-B";
            foreach (var segment in hat.KabloSegmentleri) segment.Metraj = 5;
            ElvAnalogAnonsTopolojiServisi.KabloSegmentleriniEsitle(hat);
        }
        sistem.AnalogZonlar.Add(zon);

        var rapor = ElvAnalogAnonsDogrulamaServisi.Dogrula(sistem);
        Assert.True(rapor.Gecerli);
        Assert.Equal(0, rapor.HataSayisi);

        string yol = Path.Combine(Path.GetTempPath(), $"analog_anons_{Guid.NewGuid():N}.dxf");
        try
        {
            ZayifAkimTekHatDxfServisi.AnonsDxfCiktiAl(sistem, yol);
            Assert.True(File.Exists(yol));
            Assert.True(new FileInfo(yol).Length > 0);
            var doc = netDxf.DxfDocument.Load(yol);
            Assert.NotNull(doc.Layers["ZA-ANALOG-HAT-A"]);
            Assert.NotNull(doc.Layers["ZA-ANALOG-HAT-B"]);
        }
        finally { if (File.Exists(yol)) File.Delete(yol); }
    }

    [Fact]
    public void ViewModel_UstBaglanti_DonguVeKendineBaglamaEngellenir()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.RackEkleKata(0);
        vm.RackEkleKata(2);
        var a = vm.DataSistemi.RackKabinler.First(r => r.KatNo == 0);
        var b = vm.DataSistemi.RackKabinler.First(r => r.KatNo == 2);

        // b → a (geçerli)
        vm.SeciliRack = b;
        vm.SeciliUstRackId = a.Id;
        Assert.Equal(a.Id, b.UstRackId);

        // a → b denemesi: b zaten a'nın altı → döngü, reddedilmeli
        vm.SeciliRack = a;
        vm.SeciliUstRackId = b.Id;
        Assert.Null(a.UstRackId);

        // a → a (kendine) reddedilmeli
        vm.SeciliUstRackId = a.Id;
        Assert.Null(a.UstRackId);

        // Rack değiştirince seçili üst bağlantı doğru gelir (boş değil)
        vm.SeciliRack = b;
        Assert.Equal(a.Id, vm.SeciliUstRackId);
        vm.SeciliRack = a;
        Assert.Equal(ZayifAkimTekHatViewModel.AnaMerkezSecenekId, vm.SeciliUstRackId);
    }

    [Fact]
    public void Rack_UstBaglanti_ZincirlemeVeDxf()
    {
        var data = new ElvDataSistemi();
        var anaRack = new ElvRackKabin { Ad = "Zemin Ana Rack", KatNo = 0, KatAdi = "Zemin Kat", DataHatSayisi = 8 };
        var katRack = new ElvRackKabin
        {
            Ad = "3. Kat Rack", KatNo = 3, KatAdi = "3. Kat", DataHatSayisi = 6,
            UstRackId = anaRack.Id // merkez yerine ana rack'e bağlı (daisy-chain)
        };
        data.RackKabinler.Add(anaRack);
        data.RackKabinler.Add(katRack);

        Assert.Null(anaRack.UstRackId);                 // ana rack merkeze bağlı
        Assert.Equal(anaRack.Id, katRack.UstRackId);    // kat rack, ana rack'e bağlı

        // JSON round-trip: UstRackId korunmalı
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var proje = new ZayifAkimTekHatProje { DataSistemi = data };
        var geri = JsonSerializer.Deserialize<ZayifAkimTekHatProje>(JsonSerializer.Serialize(proje, opts), opts);
        Assert.NotNull(geri);
        var geriKat = geri!.DataSistemi.RackKabinler.First(r => r.KatNo == 3);
        var geriAna = geri.DataSistemi.RackKabinler.First(r => r.KatNo == 0);
        Assert.Equal(geriAna.Id, geriKat.UstRackId);

        // Zincirli topoloji DXF üretebilmeli
        var dxfPath = Path.Combine(Path.GetTempPath(), $"m13_zincir_{Guid.NewGuid():N}.dxf");
        try
        {
            ZayifAkimTekHatDxfServisi.DataDxfCiktiAl(data, dxfPath);
            Assert.True(File.Exists(dxfPath));
            Assert.True(new FileInfo(dxfPath).Length > 0);
        }
        finally
        {
            if (File.Exists(dxfPath)) File.Delete(dxfPath);
        }
    }

    [Fact]
    public void ImportServisi_DevreVarken_DevresizCihazlarKaybolmaz()
    {
        // Kat üzerinde hem devreye atanmış hem devresiz zayıf akım cihazları var;
        // devresizler ayrı rack/hat/loop olarak aktarılmalı (veri kaybı regresyonu).
        var m12 = new BasitProjeCizProje { ProjeAdi = "Devresiz Test" };
        var kat = new BasitProjeKat { KatNo = 1, Ad = "1. Kat" };

        void Ekle(string id, BpcElemanTipi tip) =>
            kat.Elemanlar.Add(new BasitProjeEleman { Id = id, Tip = tip, Ad = id });

        // Data: 2 devrede + 3 devresiz priz
        Ekle("d1", BpcElemanTipi.DataPriz); Ekle("d2", BpcElemanTipi.DataPriz);
        Ekle("d3", BpcElemanTipi.DataPriz); Ekle("d4", BpcElemanTipi.DataPriz); Ekle("d5", BpcElemanTipi.DataPriz);
        kat.SistemDevreleri.Add(new BpcSistemDevre { Sistem = BpcPaftaTipi.Data, CihazIds = new() { "d1", "d2" } });

        // Kamera: 1 devrede + 2 devresiz
        Ekle("k1", BpcElemanTipi.Kamera); Ekle("k2", BpcElemanTipi.Kamera); Ekle("k3", BpcElemanTipi.Kamera);
        kat.SistemDevreleri.Add(new BpcSistemDevre { Sistem = BpcPaftaTipi.Cctv, CihazIds = new() { "k1" } });

        // Anons: 2 devrede + 1 devresiz hoparlör
        Ekle("h1", BpcElemanTipi.Hoparlor); Ekle("h2", BpcElemanTipi.Hoparlor); Ekle("h3", BpcElemanTipi.Hoparlor);
        kat.SistemDevreleri.Add(new BpcSistemDevre { Sistem = BpcPaftaTipi.Anons, CihazIds = new() { "h1", "h2" } });

        // TV: 1 devrede + 1 devresiz priz
        Ekle("t1", BpcElemanTipi.TvPriz); Ekle("t2", BpcElemanTipi.TvPriz);
        kat.SistemDevreleri.Add(new BpcSistemDevre { Sistem = BpcPaftaTipi.Tv, CihazIds = new() { "t1" } });

        // Yangın: 1 devrede + 2 devresiz dedektör
        Ekle("y1", BpcElemanTipi.DumanDedektor); Ekle("y2", BpcElemanTipi.DumanDedektor); Ekle("y3", BpcElemanTipi.IsiDedektor);
        kat.SistemDevreleri.Add(new BpcSistemDevre { Sistem = BpcPaftaTipi.Yangin, CihazIds = new() { "y1" } });

        m12.Katlar.Add(kat);
        var m13 = ZayifAkimTekHatImportServisi.Donustur(m12);

        // Data: devre rack'i (2 hat) + devresiz rack (3 hat) = toplam 5 hat
        Assert.Equal(2, m13.DataSistemi.RackKabinler.Count);
        Assert.Equal(5, m13.DataSistemi.ToplamDataHat);
        Assert.Contains(m13.DataSistemi.RackKabinler, r => r.Ad.Contains("Devresiz") && r.DataHatSayisi == 3);

        // Kamera: 1 + 2 = 3 kamera
        Assert.Equal(2, m13.KameraSistemi.RackKabinler.Count);
        Assert.Equal(3, m13.KameraSistemi.ToplamKamera);

        // Anons: 1 devre hattı (2 hoparlör) + 1 devresiz hat (1 hoparlör) = 2 hat, 3 hoparlör
        var anonsRack = Assert.Single(m13.AnonsSistemi.RackKabinler);
        Assert.Equal(2, anonsRack.AnonsHatSayisi);
        Assert.Equal(3, anonsRack.ToplamAnonsHoparlor);

        // TV: kattaki tüm prizler sayılır
        Assert.Equal(2, m13.TvSistemi.ToplamTvPriz);

        // Yangın: devre loop'u (1 cihaz) + devresiz loop (2 cihaz)
        var loopGrubu = Assert.Single(m13.YanginSistemi.KatLoopGruplari);
        Assert.Equal(2, loopGrubu.Looplar.Count);
        Assert.Equal(3, loopGrubu.ToplamCihaz);
    }

    [Fact]
    public void AnalogAmplifikatorSil_AtamalariTemizler()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.SistemSec("Anons");
        Assert.Equal("Analog", vm.AnonsSistemi.Teknoloji);

        // Zon + amplifikatör kur, Hat A/B ve zona ata.
        vm.RackEkleKata(0);
        var zon = Assert.Single(vm.AnonsSistemi.AnalogZonlar);
        vm.YeniAnalogAmplifikatorKodu = "AMP-240";
        vm.AnalogAmplifikatorEkle();
        var amp = Assert.Single(vm.AnonsSistemi.AnalogAmplifikatorler);
        zon.AmplifikatorId = amp.Id;
        zon.HatA.AmplifikatorId = amp.Id;
        zon.HatB!.AmplifikatorId = amp.Id;

        // Hat atama seçenekleri yedek olmayan amplifikatörü içermeli (+ Atanmamış).
        Assert.Contains(vm.AnalogAmplifikatorSecenekleri, s => s.Id == amp.Id);

        vm.SeciliAnalogAmplifikator = amp;
        vm.AnalogAmplifikatorSil();

        Assert.Empty(vm.AnonsSistemi.AnalogAmplifikatorler);
        Assert.Null(zon.AmplifikatorId);
        Assert.Null(zon.HatA.AmplifikatorId);
        Assert.Null(zon.HatB!.AmplifikatorId);
    }

    [Fact]
    public void AnalogHatAmplifikatorAtamasi_ViewModelUzerindenYapilir()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.SistemSec("Anons");
        vm.RackEkleKata(0);
        vm.YeniAnalogAmplifikatorKodu = "AMP-480";
        vm.AnalogAmplifikatorEkle();
        var amp = Assert.Single(vm.AnonsSistemi.AnalogAmplifikatorler);
        var zon = Assert.Single(vm.AnonsSistemi.AnalogZonlar);
        vm.AnalogAnonsZonuSec(zon);

        vm.SeciliHatAAmplifikatorId = amp.Id;
        vm.SeciliHatBAmplifikatorId = amp.Id;
        Assert.Equal(amp.Id, zon.HatA.AmplifikatorId);
        Assert.Equal(amp.Id, zon.HatB!.AmplifikatorId);

        // (Atanmamış) seçilince modelde null olmalı.
        vm.SeciliHatAAmplifikatorId = ZayifAkimTekHatViewModel.AmplifikatorYokSecenekId;
        Assert.Null(zon.HatA.AmplifikatorId);
    }

    [Fact]
    public void Migrasyon_EskiSurumuGuncellerYeniSurumuReddeder()
    {
        // Sürüm 0 (eski dosya): "Cat6 UTP" kablo adı katalog adına taşınır.
        var eski = new ZayifAkimTekHatProje();
        var rack = new ElvRackKabin { KatIciKabloTipi = "Cat6 UTP", MerkezeKabloTipi = "Cat6 UTP", DataHatSayisi = 1 };
        rack.DataHatlari.Add(new ElvHatMetraj { HatNo = 1, Ad = "Data 1", KabloTipi = "Cat6 UTP" });
        eski.DataSistemi.RackKabinler.Add(rack);

        var sonuc = ZayifAkimTekHatMigrasyonServisi.Uygula(eski);
        Assert.True(sonuc.Basarili);
        Assert.Equal(ZayifAkimTekHatProje.GuncelSemaSurum, eski.SemaSurum);
        Assert.Equal("Cat6 U/UTP", rack.KatIciKabloTipi);
        Assert.Equal("Cat6 U/UTP", rack.MerkezeKabloTipi);
        Assert.Equal("Cat6 U/UTP", rack.DataHatlari[0].KabloTipi);

        // Gelecekten gelen dosya reddedilir.
        var yeni = new ZayifAkimTekHatProje { SemaSurum = ZayifAkimTekHatProje.GuncelSemaSurum + 1 };
        Assert.False(ZayifAkimTekHatMigrasyonServisi.Uygula(yeni).Basarili);
    }

    [Fact]
    public void ImportServisi_TelefonVeDiafonSistemleriniKurar()
    {
        var m12 = new BasitProjeCizProje { ProjeAdi = "Telefon Diafon Test" };
        var kat = new BasitProjeKat { KatNo = 2, Ad = "2. Kat" };
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "tp1", Tip = BpcElemanTipi.TelefonPriz, Ad = "Telefon Prizi" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "tp2", Tip = BpcElemanTipi.TelefonPriz, Ad = "Telefon Prizi" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "df1", Tip = BpcElemanTipi.Diafon, Ad = "Diafon" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "kz1", Tip = BpcElemanTipi.KapiZili, Ad = "Kapı Zili" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "ds1", Tip = BpcElemanTipi.DiafonSantrali, Ad = "Diafon Santrali" });
        m12.Katlar.Add(kat);

        var m13 = ZayifAkimTekHatImportServisi.Donustur(m12);

        var telRack = Assert.Single(m13.TelefonSistemi.RackKabinler);
        Assert.Equal(2, telRack.TelefonPrizSayisi);
        Assert.Equal(2, telRack.TelefonHatlari.Count);
        Assert.Equal("Telefon Santrali", m13.TelefonSistemi.AnaMerkez.Ad);

        // DiafonSantrali merkez cihazıdır, üniteye sayılmaz: Diafon + KapiZili = 2.
        var diafonRack = Assert.Single(m13.DiafonSistemi.RackKabinler);
        Assert.Equal(2, diafonRack.DiafonUniteSayisi);
        Assert.Equal(2, diafonRack.DiafonUniteHatlari.Count);

        // DXF ve SVG çıktıları üretilebilmeli.
        var tmp = Path.Combine(Path.GetTempPath(), $"m13_telefon_{Guid.NewGuid():N}");
        try
        {
            ZayifAkimTekHatDxfServisi.TelefonDxfCiktiAl(m13.TelefonSistemi, tmp + ".dxf");
            ZayifAkimTekHatDxfServisi.DiafonDxfCiktiAl(m13.DiafonSistemi, tmp + "_d.dxf");
            ZayifAkimTekHatSvgServisi.TelefonSvgCiktiAl(m13.TelefonSistemi, tmp + ".svg");
            ZayifAkimTekHatSvgServisi.DiafonSvgCiktiAl(m13.DiafonSistemi, tmp + "_d.svg");
            Assert.True(new FileInfo(tmp + ".dxf").Length > 0);
            Assert.Contains("Telefon priz", File.ReadAllText(tmp + ".svg"));
        }
        finally
        {
            foreach (var uz in new[] { ".dxf", "_d.dxf", ".svg", "_d.svg" })
                if (File.Exists(tmp + uz)) File.Delete(tmp + uz);
        }
    }

    [Fact]
    public void KapasiteDenetimi_PortVeKanalYetersizligindeUyarir()
    {
        var data = new ElvDataSistemi();
        var rack = new ElvRackKabin { Ad = "Zemin Rack", DataHatSayisi = 30 };
        rack.Cihazlar.Add(new ElvCihaz { Ad = "PoE Switch 24 Port", Adet = 1 });
        rack.Cihazlar.Add(new ElvCihaz { Ad = "Patch Panel 24 Port Cat6", Adet = 1 });
        data.RackKabinler.Add(rack);

        var rapor = ElvKapasiteDenetimServisi.DataDenetle(data);
        Assert.False(rapor.Temiz);
        Assert.Contains(rapor.Sorunlar, s => s.Mesaj.Contains("Switch port"));
        Assert.Contains(rapor.Sorunlar, s => s.Mesaj.Contains("Patch panel"));

        // Yeterli kapasite: 48 port switch + 2x24 panel → temiz.
        rack.Cihazlar.Clear();
        rack.Cihazlar.Add(new ElvCihaz { Ad = "PoE Switch 48 Port", Adet = 1 });
        rack.Cihazlar.Add(new ElvCihaz { Ad = "Patch Panel 24 Port Cat6", Adet = 2 });
        Assert.True(ElvKapasiteDenetimServisi.DataDenetle(data).Temiz);

        // Kamera: NVR kanal yetersizliği.
        var kamera = new ElvKameraSistemi();
        var cctvRack = new ElvRackKabin { Ad = "Zemin CCTV", DahiliKameraSayisi = 20 };
        cctvRack.Cihazlar.Add(new ElvCihaz { Ad = "PoE Switch 24 Port", Adet = 1 });
        kamera.RackKabinler.Add(cctvRack);
        kamera.AnaMerkez.Cihazlar.Add(new ElvCihaz { Ad = "NVR 16 Kanal", Adet = 1 });
        var kameraRapor = ElvKapasiteDenetimServisi.KameraDenetle(kamera);
        Assert.Contains(kameraRapor.Sorunlar, s => s.Mesaj.Contains("NVR kanal"));

        kamera.AnaMerkez.Cihazlar.Add(new ElvCihaz { Ad = "NVR 16 Kanal", Adet = 1 });
        Assert.True(ElvKapasiteDenetimServisi.KameraDenetle(kamera).Temiz);
    }

    [Fact]
    public void CctvIzlemeNoktasi_BirdenFazlaOlur_RackeBaglanirVeOzelKatalogKullanir()
    {
        var vm = new ZayifAkimTekHatViewModel();
        vm.SistemSec("Kamera");
        var rack1 = new ElvRackKabin { Ad = "Zemin CCTV Rack", KatNo = 0, KatAdi = "Zemin Kat" };
        var rack2 = new ElvRackKabin { Ad = "1. Kat CCTV Rack", KatNo = 1, KatAdi = "1. Kat" };
        vm.KameraSistemi.RackKabinler.AddRange([rack1, rack2]);
        vm.RackSec(rack1);

        vm.CctvIzlemeNoktasiEkle();
        var nokta1 = Assert.Single(vm.KameraSistemi.IzlemeNoktalari);
        Assert.Equal(rack1.Id, nokta1.BagliRackId);
        Assert.Contains(vm.CctvIzlemeBaglantiSecenekleri, x => x.Id == "");
        Assert.Contains(vm.CctvIzlemeBaglantiSecenekleri, x => x.Id == rack2.Id);
        Assert.Contains("CCTV Monitör 27 inç", vm.CihazKatalogu);

        int oncekiRevizyon = vm.SemaRevizyon;
        vm.SeciliCctvIzlemeKatNo = 3;
        Assert.Equal(3, nokta1.KatNo);
        Assert.Equal("3. Kat", nokta1.KatAdi);
        Assert.True(vm.SemaRevizyon > oncekiRevizyon);

        vm.CctvIzlemeBaglantisiUygula(rack2.Id);
        vm.YeniCihazAdi = "CCTV Monitör 27 inç";
        vm.CihazEkle();
        Assert.Equal(rack2.Id, nokta1.BagliRackId);
        Assert.Contains(nokta1.Cihazlar, c => c.Ad == "CCTV Monitör 27 inç");

        vm.CctvIzlemeNoktasiEkle();
        Assert.Equal(2, vm.KameraSistemi.IzlemeNoktalari.Count);

        string taban = Path.Combine(Path.GetTempPath(), $"cctv_izleme_{Guid.NewGuid():N}");
        try
        {
            ZayifAkimTekHatSvgServisi.KameraSvgCiktiAl(vm.KameraSistemi, taban + ".svg");
            ZayifAkimTekHatDxfServisi.KameraDxfCiktiAl(vm.KameraSistemi, taban + ".dxf");
            Assert.Contains(nokta1.Ad, File.ReadAllText(taban + ".svg"));
            Assert.True(new FileInfo(taban + ".dxf").Length > 0);
        }
        finally
        {
            if (File.Exists(taban + ".svg")) File.Delete(taban + ".svg");
            if (File.Exists(taban + ".dxf")) File.Delete(taban + ".dxf");
        }
    }

    [Fact]
    public void M13Kutuphane_YerlesiklerSaltOkunur_OzelKalemlerDuzenlenipProjeyeKaydolur()
    {
        Assert.Equal(11, ElvKutuphaneServisi.AltBasliklar.Count);
        Assert.All(ElvKutuphaneServisi.AltBasliklar, alt =>
        {
            Assert.NotEmpty(ElvKutuphaneServisi.YerlesikCihazlar(alt.Kod));
            Assert.NotEmpty(ElvKutuphaneServisi.YerlesikKablolar(alt.Kod));
        });

        var proje = new ZayifAkimTekHatProje();
        var kutuphane = new ZayifAkimKutuphaneViewModel(proje, "KameraIzleme");
        Assert.All(kutuphane.Cihazlar.Take(ElvKutuphaneServisi.YerlesikCihazlar("KameraIzleme").Count), x => Assert.True(x.Yerlesik));

        kutuphane.CihazEkleCommand.Execute(null);
        var kullaniciCihazi = Assert.Single(kutuphane.Cihazlar.Where(x => !x.Yerlesik));
        kullaniciCihazi.Ad = "Özel Operatör Konsolu";
        Assert.Contains(proje.OzelKutuphaneKalemleri, x => x.Ad == "Özel Operatör Konsolu" && x.AltBaslikKodu == "KameraIzleme");

        kutuphane.KabloEkleCommand.Execute(null);
        var kullaniciKablosu = Assert.Single(kutuphane.Kablolar.Where(x => !x.Yerlesik));
        kullaniciKablosu.Ad = "Özel İzleme Fiberi";

        var anaVm = new ZayifAkimTekHatViewModel { Proje = proje };
        anaVm.SistemSec("Kamera");
        var nokta = new ElvCctvIzlemeNoktasi();
        proje.KameraSistemi.IzlemeNoktalari.Add(nokta);
        anaVm.CctvIzlemeNoktasiSec(nokta);
        Assert.Contains("Özel Operatör Konsolu", anaVm.CihazKatalogu);
        Assert.Contains("Özel İzleme Fiberi", anaVm.KabloKatalogu);

        string json = JsonSerializer.Serialize(proje);
        var tekrar = JsonSerializer.Deserialize<ZayifAkimTekHatProje>(json)!;
        Assert.Equal(2, tekrar.OzelKutuphaneKalemleri.Count);
    }

    [Fact]
    public void AkuHesabi_En54_4KapasiteyiDogrular()
    {
        var sistem = new ElvAnonsSistemi { Teknoloji = "Analog" };
        var zon = ElvAnalogAnonsTopolojiServisi.ZonOlustur("Zemin", 0, "Zemin Kat", ciftHatli: false);
        ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(zon, "TAVAN-6W", 20, 6); // 120 W
        sistem.AnalogZonlar.Add(zon);

        // Yetersiz akü: kapasite tanımsız.
        var sonuc = ElvAnalogAnonsAkuServisi.Hesapla(sistem);
        Assert.False(sonuc.Yeterli);
        Assert.Contains(sonuc.Sorunlar, s => s.Contains("tanımlanmamış"));

        // 2x 12V 40Ah + EN 54-4 onayı → yeterli olmalı (gerekli ≈ 24h×0.5A + 0.5h×9.2A ×1.25 ≈ 20.8 Ah).
        sistem.AnalogGucKaynagi.AkuKapasitesiAh = 40;
        sistem.AnalogGucKaynagi.AkuAdedi = 2;
        sistem.AnalogGucKaynagi.En54_4Uygun = true;
        sonuc = ElvAnalogAnonsAkuServisi.Hesapla(sistem);
        Assert.True(sonuc.Yeterli);
        Assert.Empty(sonuc.Sorunlar);
        Assert.True(sonuc.GerekliAh > 15 && sonuc.GerekliAh < 30);
    }
}
