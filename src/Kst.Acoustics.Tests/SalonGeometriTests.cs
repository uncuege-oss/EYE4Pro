using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Tests;

public static class SalonGeometriTests
{
    // SalonGeometri kaydet→aç round-trip: temel alanlar + katman + çizim öğesi korunmalı.
    public static void E4SalonRoundTrip()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4salon-test-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            var salon = SalonGeometriServisi.YeniOlustur("Test Salonu");
            salon.SalonYuksekligiM = 9.5;

            // YeniOlustur 11 varsayılan katman tohumlar (korniş/balkon/orkestra/kapı/pencere dahil).
            if (salon.Katmanlar.Count != 12)
                throw new InvalidOperationException($"12 varsayılan katman beklenir; {salon.Katmanlar.Count} bulundu.");

            var zeminKatman = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Zemin);

            // Dikdörtgen zemin konturu (10×8) çiz.
            salon.CizimOgeleri.Add(new SalonCizimOgesi
            {
                Id       = "oge-01",
                Tip      = SalonCizimTipi.Dikdortgen,
                Kapali   = true,
                KatmanId = zeminKatman.Id,
                Noktalar = new() { new(0, 0), new(10, 0), new(10, 8), new(0, 8) }
            });
            // Dairesel sahne konturu (merkez 5,2, R=2).
            salon.CizimOgeleri.Add(new SalonCizimOgesi
            {
                Id       = "oge-02",
                Tip      = SalonCizimTipi.Daire,
                Kapali   = true,
                YariCapM = 2.0,
                Noktalar = new() { new(5, 2) }
            });

            SalonGeometriServisi.Kaydet(salon, yol);

            if (!File.Exists(yol))
                throw new InvalidOperationException("Dosya oluşturulmadı.");
            if (salon.GeometriRevizyon != 1)
                throw new InvalidOperationException($"Kaydet sonrası GeometriRevizyon=1 beklenir; {salon.GeometriRevizyon} bulundu.");

            var salon2 = SalonGeometriServisi.Ac(yol);

            if (salon2.SalonAdi != "Test Salonu")
                throw new InvalidOperationException($"SalonAdi uyuşmuyor: '{salon2.SalonAdi}'.");
            if (Math.Abs(salon2.SalonYuksekligiM - 9.5) > 1e-9)
                throw new InvalidOperationException($"SalonYuksekligiM {salon2.SalonYuksekligiM} uyuşmuyor.");
            if (salon2.SemaSurum != SalonGeometriServisi.GuncelSemaSurum)
                throw new InvalidOperationException($"SemaSurum {SalonGeometriServisi.GuncelSemaSurum} beklenir; {salon2.SemaSurum} bulundu.");
            if (salon2.Katmanlar.Count != 12)
                throw new InvalidOperationException($"12 katman beklenir; {salon2.Katmanlar.Count} bulundu.");
            if (salon2.CizimOgeleri.Count != 2)
                throw new InvalidOperationException($"2 çizim öğesi beklenir; {salon2.CizimOgeleri.Count} bulundu.");

            var dikdortgen = salon2.CizimOgeleri.First(o => o.Id == "oge-01");
            if (dikdortgen.Noktalar.Count != 4)
                throw new InvalidOperationException($"Dikdörtgen 4 köşe beklenir; {dikdortgen.Noktalar.Count} bulundu.");
            if (dikdortgen.KatmanId != zeminKatman.Id)
                throw new InvalidOperationException("Çizim öğesi katman bağı uyuşmuyor.");

            var daire = salon2.CizimOgeleri.First(o => o.Id == "oge-02");
            if (Math.Abs(daire.YariCapM - 2.0) > 1e-9)
                throw new InvalidOperationException($"Daire yarıçapı {daire.YariCapM} uyuşmuyor.");

            if (salon2.DosyaYolu != yol)
                throw new InvalidOperationException($"DosyaYolu yükleme sonrası '{yol}' olmalı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // İkinci kayıt GeometriRevizyon'u artırmalı.
    public static void IkinciKayitRevizyonuArtirir()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4salon-rev-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            var salon = SalonGeometriServisi.YeniOlustur("Rev Test");
            SalonGeometriServisi.Kaydet(salon, yol);  // rev = 1
            SalonGeometriServisi.Kaydet(salon, yol);  // rev = 2
            if (salon.GeometriRevizyon != 2)
                throw new InvalidOperationException($"İki kayıt sonrası rev=2 beklenir; {salon.GeometriRevizyon} bulundu.");

            var salon2 = SalonGeometriServisi.Ac(yol);
            if (salon2.GeometriRevizyon != 2)
                throw new InvalidOperationException($"Yüklenen rev=2 beklenir; {salon2.GeometriRevizyon} bulundu.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // Eski SemaSurum yükleme sırasında güncel sürüme migrate edilmeli.
    public static void EskiSemaSurumMigrateEdilir()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4salon-mig-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            // SemaSurum=0 olan eski bir dosyayı elle yaz (servis PascalCase + case-sensitive).
            File.WriteAllText(yol, "{ \"SemaSurum\": 0, \"GeometriRevizyon\": 3, \"SalonAdi\": \"Eski\" }");
            var salon = SalonGeometriServisi.Ac(yol);
            if (salon.SemaSurum != SalonGeometriServisi.GuncelSemaSurum)
                throw new InvalidOperationException($"Migrate sonrası SemaSurum {SalonGeometriServisi.GuncelSemaSurum} beklenir; {salon.SemaSurum} bulundu.");
            if (salon.SalonAdi != "Eski")
                throw new InvalidOperationException("Migrate eski alanları korumalı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // Daire fasetlenince kapalı poligon + doğru yaklaşık alan üretmeli.
    public static void DaireFasetlemeKapaliPoligonUretir()
    {
        var oge = new SalonCizimOgesi
        {
            Tip      = SalonCizimTipi.Daire,
            YariCapM = 3.0,
            Noktalar = { new(2, 2) }
        };
        var poly = oge.KapaliPoligon(64);
        if (poly.Count != 64)
            throw new InvalidOperationException($"64 kenar beklenir; {poly.Count} bulundu.");

        // Shoelace alanı πr² = 28.27'ye yakın olmalı (yüksek faset → yaklaşık).
        double alan = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            alan += a.X * b.Y - b.X * a.Y;
        }
        alan = Math.Abs(alan) / 2.0;
        double beklenen = Math.PI * 9.0;
        if (Math.Abs(alan - beklenen) > 0.2)
            throw new InvalidOperationException($"Fasetli daire alanı ~{beklenen:F2} beklenir; {alan:F2} bulundu.");
    }

    // Kesişen iki segment beklenen noktada kesişmeli; paralel olanlar kesişmemeli.
    public static void SegmentKesisimDogruCalisir()
    {
        bool kesti = SalonTrimServisi.SegmentKesisim(
            new(0, 0), new(10, 0), new(5, -5), new(5, 5), out var nokta, out double t);
        if (!kesti) throw new InvalidOperationException("Kesişen segmentler kesişmedi.");
        if (Math.Abs(nokta.X - 5) > 1e-6 || Math.Abs(nokta.Y) > 1e-6)
            throw new InvalidOperationException($"Kesişim noktası (5,0) beklenir; ({nokta.X},{nokta.Y}) bulundu.");
        if (Math.Abs(t - 0.5) > 1e-6)
            throw new InvalidOperationException($"Parametre t=0.5 beklenir; {t} bulundu.");

        bool paralel = SalonTrimServisi.SegmentKesisim(
            new(0, 0), new(10, 0), new(0, 1), new(10, 1), out _, out _);
        if (paralel) throw new InvalidOperationException("Paralel segmentler kesişmiş sayıldı.");
    }

    // Ortadan trim: (0,0)-(10,0) çizgisi x=3 ve x=7 kesicileriyle, tıklama t=0.5 →
    // [3,7] çıkar, [0,3] ve [7,10] kalır (2 parça).
    public static void CizgiTrimOrtadanCikarir()
    {
        var kesiciler = new List<(Point2D, Point2D)>
        {
            (new(3, -1), new(3, 1)),
            (new(7, -1), new(7, 1))
        };
        bool ok = SalonTrimServisi.CizgiTrim(new(0, 0), new(10, 0), kesiciler, 0.5, out var parcalar);
        if (!ok) throw new InvalidOperationException("Kesim yapılmalıydı.");
        if (parcalar.Count != 2)
            throw new InvalidOperationException($"2 parça beklenir; {parcalar.Count} bulundu.");
        if (Math.Abs(parcalar[0].A.X - 0) > 1e-6 || Math.Abs(parcalar[0].B.X - 3) > 1e-6)
            throw new InvalidOperationException("İlk parça [0,3] beklenir.");
        if (Math.Abs(parcalar[1].A.X - 7) > 1e-6 || Math.Abs(parcalar[1].B.X - 10) > 1e-6)
            throw new InvalidOperationException("İkinci parça [7,10] beklenir.");
    }

    // Kapalı dikdörtgen → 1 zemin + 1 tavan + 4 duvar = 6 yüzey; doğru kotlar.
    public static void Uc3DDikdortgenZeminDuvarTavan()
    {
        var salon = SalonGeometriServisi.YeniOlustur("3D Test");
        salon.SalonYuksekligiM = 4.0;
        var zemin = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Zemin);
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Dikdortgen,
            Kapali = true,
            KatmanId = zemin.Id,
            Noktalar = new() { new(0, 0), new(10, 0), new(10, 8), new(0, 8) }
        });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        if (yuzeyler.Count != 6)
            throw new InvalidOperationException($"6 yüzey beklenir (zemin+tavan+4 duvar); {yuzeyler.Count} bulundu.");

        // Kabuk duvar kategorisi artık katmandan türetilir ("Duvar-Seyirci").
        var zeminY = yuzeyler.Where(s => s.Category == "Zemin").ToList();
        var tavanY = yuzeyler.Where(s => s.Category == "Tavan").ToList();
        var duvarY = yuzeyler.Where(s => s.Category == "Duvar-Seyirci").ToList();
        if (zeminY.Count != 1 || tavanY.Count != 1 || duvarY.Count != 4)
            throw new InvalidOperationException($"Kategoriler hatalı: zemin={zeminY.Count}, tavan={tavanY.Count}, duvar={duvarY.Count}.");

        if (zeminY[0].Vertices.Any(v => System.Math.Abs(v.Z) > 1e-9))
            throw new InvalidOperationException("Zemin z=0 olmalı.");
        if (tavanY[0].Vertices.Any(v => System.Math.Abs(v.Z - 4.0) > 1e-9))
            throw new InvalidOperationException("Tavan z=4 olmalı.");
        foreach (var d in duvarY)
        {
            if (d.Vertices.Count != 4)
                throw new InvalidOperationException("Duvar 4 köşeli olmalı.");
            if (d.Vertices.Count(v => System.Math.Abs(v.Z) < 1e-9) != 2 ||
                d.Vertices.Count(v => System.Math.Abs(v.Z - 4.0) < 1e-9) != 2)
                throw new InvalidOperationException("Duvar 2 taban + 2 üst köşe içermeli.");
        }
    }

    // Açık çizgi → tek duvar yüzeyi (segment × yükseklik).
    public static void Uc3DAcikCizgiDuvarUretir()
    {
        var salon = SalonGeometriServisi.YeniOlustur("Çizgi Duvar");
        salon.SalonYuksekligiM = 3.0;
        var duvarKat = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.DuvarSeyirci);
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Cizgi,
            KatmanId = duvarKat.Id,
            Noktalar = new() { new(0, 0), new(5, 0) }
        });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        if (yuzeyler.Count != 1)
            throw new InvalidOperationException($"1 duvar yüzeyi beklenir; {yuzeyler.Count} bulundu.");
        if (yuzeyler[0].Category != "Duvar-Seyirci")
            throw new InvalidOperationException($"Kategori 'Duvar-Seyirci' beklenir; '{yuzeyler[0].Category}' bulundu.");
    }

    // Parametrik dikdörtgen sahne → 1 üst yüzey ("Sahne") + 4 etek ("Sahne-Yan"); doğru kot.
    public static void Uc3DSahnePlatformuUretir()
    {
        var salon = SalonGeometriServisi.YeniOlustur("Sahne Test");
        salon.SalonYuksekligiM = 8.0;
        salon.Sahne = new SahneGeometri
        {
            Tip = SahneTipi.Dikdortgen,
            GenislikM = 12, DerinlikM = 8,
            YukseklikM = 1.2, TabanKotuM = 0,
            MerkezX = 0, MerkezY = 0
        };

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        var ust = yuzeyler.Where(s => s.Category == "Sahne").ToList();
        var etek = yuzeyler.Where(s => s.Category == "Sahne-Yan").ToList();

        if (ust.Count != 1)
            throw new InvalidOperationException($"1 sahne üst yüzeyi beklenir; {ust.Count} bulundu.");
        if (etek.Count != 4)
            throw new InvalidOperationException($"4 sahne eteği beklenir; {etek.Count} bulundu.");
        if (ust[0].Vertices.Any(v => System.Math.Abs(v.Z - 1.2) > 1e-9))
            throw new InvalidOperationException("Sahne üst yüzeyi z=1.2 olmalı.");

        // Footprint dikdörtgeni merkezde 12×8 olmalı.
        var poly = salon.Sahne.TabanPoligonu();
        if (poly.Count != 4)
            throw new InvalidOperationException($"Dikdörtgen sahne 4 köşe; {poly.Count} bulundu.");
        if (System.Math.Abs(poly[1].X - 6) > 1e-9 || System.Math.Abs(poly[2].Y - 4) > 1e-9)
            throw new InvalidOperationException("Sahne footprint 12×8 merkezde olmalı.");
    }

    // Kademeli seyirci: N=6 → 6 basamak ("Seyirci") + 5 korniş ("Korniş"); doğru kotlar.
    public static void Uc3DSeyirciKademeUretir()
    {
        var salon = SalonGeometriServisi.YeniOlustur("Seyirci Test");
        var sek = new SeyirciSekciyonu
        {
            GenislikM = 10, DerinlikM = 12,
            KademeSayisi = 6, KademeYuksekligiM = 0.5,
            TabanKotuM = 0, MerkezX = 0, MerkezY = 0
        };
        salon.SeyirciSekciyonlari.Add(sek);

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        var basamak = yuzeyler.Where(s => s.Category == "Seyirci").ToList();
        var kornis  = yuzeyler.Where(s => s.Category == "Korniş").ToList();

        if (basamak.Count != 6)
            throw new InvalidOperationException($"6 basamak beklenir; {basamak.Count} bulundu.");
        if (kornis.Count != 5)
            throw new InvalidOperationException($"5 korniş beklenir; {kornis.Count} bulundu.");

        // Basamak kotları 0, 0.5, ... 2.5 olmalı (her basamak düz/tek kotlu).
        var kotlar = basamak
            .Select(b => b.Vertices.Average(v => v.Z))
            .OrderBy(z => z)
            .ToList();
        for (int i = 0; i < 6; i++)
            if (System.Math.Abs(kotlar[i] - i * 0.5) > 1e-6)
                throw new InvalidOperationException($"Basamak {i} kotu {i * 0.5} beklenir; {kotlar[i]} bulundu.");

        // Footprint 10×12 ve 5 kademe sınırı.
        if (SalonSeyirciServisi.Footprint(sek).Count != 4)
            throw new InvalidOperationException("Footprint 4 köşeli olmalı.");
        if (SalonSeyirciServisi.KademeSinirlari(sek).Count != 5)
            throw new InvalidOperationException("5 kademe sınırı beklenir.");
    }

    // Balkon: dikdörtgen → 1 döşeme + 1 alt + 4 saçak + 4 parapet = 10 yüzey; döşeme kotu doğru.
    public static void Uc3DBalkonPlatformuUretir()
    {
        var salon = SalonGeometriServisi.YeniOlustur("Balkon Test");
        salon.Balkonlar.Add(new BalkonGeometri
        {
            Tip = BalkonTipi.Balkon,
            GenislikM = 16, DerinlikM = 5,
            TabanKotuM = 5.0, KalinlikM = 0.4, ParapetM = 1.0,
            MerkezX = 0, MerkezY = 0
        });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        var doseme  = yuzeyler.Where(s => s.Category == "Balkon").ToList();      // 1 döşeme + 4 saçak
        var alt     = yuzeyler.Where(s => s.Category == "Balkon-Alt").ToList();   // 1
        var parapet = yuzeyler.Where(s => s.Category == "Balkon-Parapet").ToList(); // 4

        if (alt.Count != 1)
            throw new InvalidOperationException($"1 alt yüzey beklenir; {alt.Count} bulundu.");
        if (parapet.Count != 4)
            throw new InvalidOperationException($"4 parapet beklenir; {parapet.Count} bulundu.");
        if (doseme.Count != 5)
            throw new InvalidOperationException($"1 döşeme + 4 saçak = 5 'Balkon' yüzeyi beklenir; {doseme.Count} bulundu.");

        // Döşeme üst yüzeyi (4 köşesi de z=5) tam olarak 1 tane.
        var ustDoseme = doseme.Where(s => s.Vertices.All(v => System.Math.Abs(v.Z - 5.0) < 1e-9)).ToList();
        if (ustDoseme.Count != 1)
            throw new InvalidOperationException($"z=5 düz döşeme 1 tane beklenir; {ustDoseme.Count} bulundu.");
    }

    // Şablon: dikdörtgen salon → kapalı dış hat (Zemin) + sahne + 1 seyirci; 3D yüzey üretir.
    public static void SablonDikdortgenTamSalonUretir()
    {
        var sablon = new SalonSablonu
        {
            SalonTip = SalonTipi.Dikdortgen,
            SalonGenislikM = 24, SalonDerinlikM = 30, SalonYuksekligiM = 10,
            SahneTip = SahneTipi.Dikdortgen, SahneKonum = SahneKonum.SalonIci,
            SahneGenislikM = 14, SahneDerinlikM = 8, SahneYuksekligiM = 1.0,
            SeyirciKademe = 8, SeyirciKademeYukM = 0.5
        };
        var salon = SalonParametrikServisi.Olustur(sablon);

        if (salon.CizimOgeleri.Count != 1)
            throw new InvalidOperationException($"1 dış hat öğesi beklenir; {salon.CizimOgeleri.Count} bulundu.");
        var hat = salon.CizimOgeleri[0];
        if (hat.Tip != SalonCizimTipi.Polyline || !hat.Kapali || hat.Noktalar.Count != 4)
            throw new InvalidOperationException("Dikdörtgen dış hat kapalı 4 köşeli polyline olmalı.");
        var zeminKat = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Zemin);
        if (hat.KatmanId != zeminKat.Id)
            throw new InvalidOperationException("Dış hat Zemin katmanına atanmalı.");
        if (salon.Sahne is null)
            throw new InvalidOperationException("Sahne üretilmeli.");
        if (salon.SeyirciSekciyonlari.Count != 1)
            throw new InvalidOperationException("1 seyirci alanı üretilmeli.");

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        if (yuzeyler.Count == 0)
            throw new InvalidOperationException("Şablon 3D yüzey üretmeli.");
        // Zemin + tavan + sahne üst + seyirci basamakları üretilmeli
        if (!yuzeyler.Any(s => s.Category == "Zemin") || !yuzeyler.Any(s => s.Category == "Tavan") ||
            !yuzeyler.Any(s => s.Category == "Sahne") || !yuzeyler.Any(s => s.Category == "Seyirci"))
            throw new InvalidOperationException("Zemin/Tavan/Sahne/Seyirci kategorileri üretilmeli.");
    }

    // At nalı planı: düz ön + yaylı arka → 3'ten çok köşeli kapalı poligon.
    public static void SablonAtNaliEgriUretir()
    {
        var sablon = new SalonSablonu { SalonTip = SalonTipi.AtNali, SalonGenislikM = 20, SalonDerinlikM = 26 };
        var hat = SalonParametrikServisi.PlanHatti(sablon);
        if (hat.Count < 8)
            throw new InvalidOperationException($"At nalı çok köşeli (yaylı) olmalı; {hat.Count} köşe bulundu.");
    }

    // Tam model round-trip: sahne + seyirci + balkon .e4salon.json'da korunmalı.
    public static void TamModelRoundTrip()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4salon-tam-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            var salon = SalonGeometriServisi.YeniOlustur("Tam Salon");
            salon.Sahne = new SahneGeometri { Tip = SahneTipi.Oval, GenislikM = 12, DerinlikM = 7, YukseklikM = 1.1, MerkezX = 1, MerkezY = 2 };
            salon.SeyirciSekciyonlari.Add(new SeyirciSekciyonu { Ad = "S1", GenislikM = 14, DerinlikM = 10, KademeSayisi = 5, KademeYuksekligiM = 0.4, TersYon = true, MerkezX = 3 });
            salon.Balkonlar.Add(new BalkonGeometri { Ad = "B1", Tip = BalkonTipi.Loca, GenislikM = 6, DerinlikM = 3, TabanKotuM = 4.5, ParapetM = 0.9, MerkezX = -2 });

            SalonGeometriServisi.Kaydet(salon, yol);
            var s2 = SalonGeometriServisi.Ac(yol);

            if (s2.Sahne is null || s2.Sahne.Tip != SahneTipi.Oval || Math.Abs(s2.Sahne.MerkezY - 2) > 1e-9)
                throw new InvalidOperationException("Sahne round-trip'te korunmadı.");
            if (s2.SeyirciSekciyonlari.Count != 1 || s2.SeyirciSekciyonlari[0].KademeSayisi != 5 || !s2.SeyirciSekciyonlari[0].TersYon)
                throw new InvalidOperationException("Seyirci round-trip'te korunmadı.");
            if (s2.Balkonlar.Count != 1 || s2.Balkonlar[0].Tip != BalkonTipi.Loca || Math.Abs(s2.Balkonlar[0].TabanKotuM - 4.5) > 1e-9)
                throw new InvalidOperationException("Balkon round-trip'te korunmadı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // DXF export Türkçe kategori adlarını (Korniş) bozmamalı — UTF-8 kodlama.
    public static void DxfExportTurkceKategoriBozulmaz()
    {
        var salon = SalonGeometriServisi.YeniOlustur("Türkçe DXF");
        salon.SeyirciSekciyonlari.Add(new SeyirciSekciyonu { GenislikM = 8, DerinlikM = 6, KademeSayisi = 3, KademeYuksekligiM = 0.5, MerkezX = 0, MerkezY = 0 });

        var yol = Path.Combine(Path.GetTempPath(), $"salon-tr-{Guid.NewGuid():N}.dxf");
        try
        {
            SalonDxfExportServisi.DisaAktar(salon, yol);
            var icerik = File.ReadAllText(yol);
            if (!icerik.Contains("Korniş"))
                throw new InvalidOperationException("DXF 'Korniş' kategorisini bozmadan içermeli (UTF-8).");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // M01 native köprü: şablon salon → AcousticsProject (yüzey + kategori + hacim + birim/format).
    public static void M01KopruNativeProjeUretir()
    {
        var salon = SalonParametrikServisi.Olustur(new SalonSablonu
        {
            SalonTip = SalonTipi.Dikdortgen, SalonGenislikM = 24, SalonDerinlikM = 30, SalonYuksekligiM = 10
        });

        var project = SalonImporter.Cevir(salon, "/tmp/test.e4salon.json");

        if (project.ImportFormat != SurfaceImportFormat.Salon)
            throw new InvalidOperationException("ImportFormat=Salon beklenir.");
        if (project.Unit != SurfaceImportUnit.Meters)
            throw new InvalidOperationException("Unit=Meters beklenir.");
        if (project.Surfaces.Count == 0)
            throw new InvalidOperationException("Yüzey üretilmeli.");
        if (project.RoomVolume <= 0)
            throw new InvalidOperationException($"RoomVolume>0 beklenir; {project.RoomVolume} bulundu.");
        if (project.VolumeOrigin != Kst.Acoustics.Core.Projects.RoomVolumeOrigin.SourceModel)
            throw new InvalidOperationException("M51 hacmi SourceModel kaynağıyla işaretlenmeli.");
        if (!project.Surfaces.Any(s => s.Category == "Zemin") || !project.Surfaces.Any(s => s.Category == "Tavan"))
            throw new InvalidOperationException("Zemin/Tavan kategorileri M01 projesine taşınmalı.");
        if (project.SemaSurum != AcousticsProjectSchema.CurrentVersion)
            throw new InvalidOperationException("M51 köprüsü güncel M01 proje şeması üretmeli.");
    }

    // M51 yeniden yükleme: yalnız geometri/kaynak damgası yenilenir; M01 akustik
    // çalışması fiziksel birimleri korunarak taşınır.
    public static void M01KopruGuncellemeAkustikCalismayiKorur()
    {
        var eskiSalon = KabukluSalon(10, 8, 4);
        eskiSalon.GeometriRevizyon = 3;
        var eskiKaynak = Path.Combine(Path.GetTempPath(), "onceki-salon.e4salon.json");
        var eskiImport = SalonImporter.Cevir(eskiSalon, eskiKaynak);
        var zemin = eskiImport.Surfaces.Single(surface => surface.Category == "Zemin");
        var settings = new SimulationParameters(
            RayCount: 12_345,
            ReceiverRadius: 500,
            ReceiverGrid: new ReceiverGrid(1_500, 0, 10_000, 0, 8_000, 500),
            UnitScale: 0.001,
            InputFingerprint: "onceki-iz");
        var eskiProje = eskiImport with
        {
            Name = "M01 Akustik Çalışması",
            Unit = SurfaceImportUnit.Millimeters,
            Surfaces = ProjectScalingService.ConvertUnits(
                eskiImport.Surfaces,
                SurfaceImportUnit.Meters,
                SurfaceImportUnit.Millimeters),
            Sources =
            [
                new AcousticPoint(
                    1, "S1", AcousticPointType.Source,
                    new Kst.Acoustics.Core.Geometry.Point3D(1_000, 2_000, 1_500))
            ],
            Receivers =
            [
                new AcousticPoint(
                    1, "R1", AcousticPointType.Receiver,
                    new Kst.Acoustics.Core.Geometry.Point3D(3_000, 4_000, 1_200))
            ],
            CategoryMaterials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Zemin"] = "mat-zemin",
                ["Tavan"] = "mat-tavan"
            },
            SurfaceMaterials = new Dictionary<string, string>
            {
                [zemin.Id.ToString()] = "mat-zemin-ozel",
                ["999"] = "mat-yetim"
            },
            SelectedRoomType = RoomType.MusicHall,
            SimulationSettings = settings,
            LastResult = new SimulationResult([], settings, TimeSpan.FromSeconds(1)),
            PanelKonfigurasyon = new AcousticPanelKonfigurasyon
            {
                PanelEniM = 0.6,
                PanelEniModu = PanelEniModu.TekTip,
                PanelEnleriM = [0.6],
                SarkmaCikmaM = 0.15,
                AktifTipler = [PanelTipi.Tavan],
                TavanYuzeyId = "2",
                Paneller = [new AcousticPanel { Tip = PanelTipi.Tavan, PanelEniM = 0.6 }]
            }
        };
        var scenario = AcousticScenarioManager.Create(
            "Alternatif",
            AcousticScenarioCloner.FromProject(eskiProje),
            DateTime.UnixEpoch);
        eskiProje = eskiProje with
        {
            Scenarios = [scenario],
            ActiveScenarioId = scenario.Id
        };

        var yeniSalon = KabukluSalon(12, 8, 4);
        yeniSalon.GeometriRevizyon = 9;
        var yeniKaynak = Path.Combine(Path.GetTempPath(), "guncel-salon.e4salon.json");
        var yeniGeometri = SalonImporter.Cevir(yeniSalon, yeniKaynak);
        var merged = SalonImporter.GeometriyiGuncelle(eskiProje, yeniGeometri);

        if (merged.Name != eskiProje.Name)
            throw new InvalidOperationException("M01 proje adı geometri güncellemesinde korunmalı.");
        if (merged.SourceFilePath != Path.GetFullPath(yeniKaynak) || merged.KaynakGeometriRevizyon != 9)
            throw new InvalidOperationException("Yeni M51 kaynak yolu ve revizyonu birlikte güncellenmeli.");
        if (merged.SemaSurum != AcousticsProjectSchema.CurrentVersion)
            throw new InvalidOperationException("Merge güncel M01 şema sürümü üretmeli.");
        if (merged.CategoryMaterials.Count != 2 || merged.CategoryMaterials["Zemin"] != "mat-zemin")
            throw new InvalidOperationException("Kategori malzemeleri korunmalı.");
        if (!merged.SurfaceMaterials.Values.Contains("mat-zemin-ozel"))
            throw new InvalidOperationException("Güvenle eşleşen yüzey malzemesi korunmalı.");
        if (merged.SurfaceMaterials.Values.Contains("mat-yetim") ||
            !merged.ImportWarnings.Any(warning => warning.Contains("yüzey malzemesi", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Eşleşmeyen yüzey override'ı yanlış yüzeye taşınmamalı ve kullanıcıya bildirilmelidir.");
        if (merged.SelectedRoomType != RoomType.MusicHall)
            throw new InvalidOperationException("Oda tipi korunmalı.");

        var source = merged.Sources.Single();
        var receiver = merged.Receivers.Single();
        if (Math.Abs(source.Position.X - 1) > 1e-9 ||
            Math.Abs(source.Position.Y - 2) > 1e-9 ||
            Math.Abs(receiver.Position.X - 3) > 1e-9)
            throw new InvalidOperationException("Kaynak/alıcılar eski proje biriminden metreye fiziksel konumları korunarak çevrilmeli.");

        var mergedSettings = merged.SimulationSettings
            ?? throw new InvalidOperationException("Simülasyon ayarları korunmalı.");
        if (mergedSettings.RayCount != settings.RayCount ||
            Math.Abs(mergedSettings.ReceiverRadius - 0.5) > 1e-9 ||
            Math.Abs(mergedSettings.ReceiverGrid!.XMax - 10) > 1e-9 ||
            mergedSettings.UnitScale != 1 ||
            mergedSettings.InputFingerprint is not null)
            throw new InvalidOperationException("Simülasyon ayarları hedef birime güvenle taşınmalı ve eski girdi izi temizlenmeli.");
        if (merged.LastResult is not { IsStale: true })
            throw new InvalidOperationException("Eski simülasyon sonucu yeni geometride stale işaretlenmeli.");
        if (merged.PanelKonfigurasyon is not { PanelEniM: 0.6, SarkmaCikmaM: 0.15 } panelSettings ||
            panelSettings.Paneller.Count != 0 || panelSettings.TavanYuzeyId is not null)
            throw new InvalidOperationException("Panel ölçüleri korunmalı; geometriye bağlı yerleşim ve hedefler temizlenmeli.");
        if (merged.Scenarios.Count != 1 || merged.Scenarios[0].Id != scenario.Id)
            throw new InvalidOperationException("Akustik senaryolar geometri güncellemesinde kaybolmamalı.");
        if (merged.ActiveScenarioId is not null)
            throw new InvalidOperationException("Yeni M51 ana geometrisinin görünmesi için eski etkin senaryo seçimi sıfırlanmalı.");
        if (!merged.ImportWarnings.Any(warning =>
                warning.Contains("senaryo", StringComparison.OrdinalIgnoreCase) &&
                warning.Contains("yeniden", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Etkin senaryonun yeniden uygulanması/hesaplanması için kalıcı uyarı üretilmeli.");
        if (Math.Abs(merged.RoomVolume - 384) > 1e-6)
            throw new InvalidOperationException("Oda hacmi yeni M51 geometrisinden alınmalı.");
    }

    // DXF export: dosya üretilir, 3DFACE + kategori katman adları içerir.
    public static void DxfExportSalonDosyaUretir()
    {
        var salon = SalonGeometriServisi.YeniOlustur("DXF Test");
        salon.SalonYuksekligiM = 4;
        var zemin = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Zemin);
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Dikdortgen, Kapali = true, KatmanId = zemin.Id,
            Noktalar = new() { new(0, 0), new(10, 0), new(10, 8), new(0, 8) }
        });

        var yol = Path.Combine(Path.GetTempPath(), $"salon-dxf-{Guid.NewGuid():N}.dxf");
        try
        {
            SalonDxfExportServisi.DisaAktar(salon, yol);
            if (!File.Exists(yol)) throw new InvalidOperationException("DXF dosyası oluşmadı.");
            var icerik = File.ReadAllText(yol);
            if (!icerik.Contains("3DFACE")) throw new InvalidOperationException("DXF 3DFACE içermeli.");
            if (!icerik.Contains("$INSUNITS")) throw new InvalidOperationException("DXF HEADER/$INSUNITS içermeli.");
            if (!icerik.Contains("Zemin") || !icerik.Contains("Tavan"))
                throw new InvalidOperationException("DXF kategori katman adlarını (Zemin/Tavan) içermeli.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // M51 DXF → M01 round-trip: metre damgası, boyutlar, kategoriler ve kapalı kabuk
    // hacmi doğrudan parser/factory hattında korunmalı.
    public static void DxfExportM01RoundTripBirimVeHacimKorur()
    {
        var salon = KabukluSalon(10, 8, 4);
        var native = SalonImporter.Cevir(salon, null);
        var yol = Path.Combine(Path.GetTempPath(), $"salon-dxf-roundtrip-{Guid.NewGuid():N}.dxf");
        try
        {
            SalonDxfExportServisi.DisaAktar(salon, yol);
            var parser = new DxfSurfaceParser();
            if (!parser.TryParse(File.ReadAllText(yol), out var document) || document is null)
                throw new InvalidOperationException("M51 DXF çıktısı M01 parser'ı tarafından okunmalı.");
            if (!document.UnitDetected || document.Unit != SurfaceImportUnit.Meters || document.InsUnitsCode != 6)
                throw new InvalidOperationException("M51 DXF çıktısı $INSUNITS=6 (metre) olarak algılanmalı; birim onayı gerektirmemeli.");

            var roundTrip = ProjectFactory.CreateFromImport(
                "DXF Round-trip",
                yol,
                new SurfaceImportResult(SurfaceImportFormat.Dxf, document));
            var nativeBounds = Bounds(native.Surfaces);
            var roundTripBounds = Bounds(roundTrip.Surfaces);
            if (!BoundsClose(nativeBounds, roundTripBounds, 1e-6))
                throw new InvalidOperationException("DXF round-trip salon boyutlarını değiştirmemeli.");
            if (Math.Abs(roundTrip.RoomVolume - native.RoomVolume) > 1e-6)
                throw new InvalidOperationException(
                    $"DXF round-trip hacmi native hacimle eşleşmeli ({native.RoomVolume} / {roundTrip.RoomVolume}).");
            if (roundTrip.VolumeOrigin != RoomVolumeOrigin.ClosedMesh)
                throw new InvalidOperationException("Dikdörtgen M51 DXF kabuğu watertight hacim üretmeli.");

            var nativeCategories = native.Surfaces.Select(surface => surface.Category).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var roundTripCategories = roundTrip.Surfaces.Select(surface => surface.Category).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!nativeCategories.SetEquals(roundTripCategories))
                throw new InvalidOperationException("DXF round-trip yüzey kategorilerini/katmanlarını korumalı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // Genel M01 DXF export yolu proje birimini HEADER'a yazmalı; koordinatlar proje
    // biriminde kalırken fiziksel hacim metre küp olarak aynı sonucu vermeli.
    public static void DxfExportMilimetreRoundTripBirimVeHacimKorur()
    {
        var salon = KabukluSalon(2, 3, 4);
        var native = SalonImporter.Cevir(salon, null);
        var millimeterSurfaces = ProjectScalingService.ConvertUnits(
            native.Surfaces,
            SurfaceImportUnit.Meters,
            SurfaceImportUnit.Millimeters);
        var yol = Path.Combine(Path.GetTempPath(), $"m01-dxf-mm-roundtrip-{Guid.NewGuid():N}.dxf");
        try
        {
            new Kst.Acoustics.Core.Exporting.DxfExportService().Export(
                yol,
                millimeterSurfaces,
                SurfaceImportUnit.Millimeters);
            var parser = new DxfSurfaceParser();
            if (!parser.TryParse(File.ReadAllText(yol), out var document) || document is null)
                throw new InvalidOperationException("Milimetre DXF çıktısı yeniden okunmalı.");
            if (!document.UnitDetected || document.Unit != SurfaceImportUnit.Millimeters || document.InsUnitsCode != 4)
                throw new InvalidOperationException("Milimetre proje DXF çıktısı $INSUNITS=4 taşımalı.");

            var roundTrip = ProjectFactory.CreateFromImport(
                "Milimetre DXF",
                yol,
                new SurfaceImportResult(SurfaceImportFormat.Dxf, document));
            if (roundTrip.Unit != SurfaceImportUnit.Millimeters)
                throw new InvalidOperationException("M01 round-trip proje birimini milimetre tutmalı.");
            if (Math.Abs(roundTrip.RoomVolume - native.RoomVolume) > 1e-6)
                throw new InvalidOperationException("Milimetre DXF round-trip fiziksel m³ hacmini değiştirmemeli.");
            var bounds = Bounds(roundTrip.Surfaces);
            if (Math.Abs((bounds.MaxX - bounds.MinX) - 2_000) > 1e-6 ||
                Math.Abs((bounds.MaxY - bounds.MinY) - 3_000) > 1e-6 ||
                Math.Abs((bounds.MaxZ - bounds.MinZ) - 4_000) > 1e-6)
                throw new InvalidOperationException("Milimetre DXF round-trip sayısal koordinat ölçeğini korumalı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    private static (double MinX, double MaxX, double MinY, double MaxY, double MinZ, double MaxZ) Bounds(
        IReadOnlyList<ProjectSurface> surfaces)
    {
        var points = surfaces.SelectMany(surface => surface.Vertices).ToArray();
        return (
            points.Min(point => point.X), points.Max(point => point.X),
            points.Min(point => point.Y), points.Max(point => point.Y),
            points.Min(point => point.Z), points.Max(point => point.Z));
    }

    private static bool BoundsClose(
        (double MinX, double MaxX, double MinY, double MaxY, double MinZ, double MaxZ) left,
        (double MinX, double MaxX, double MinY, double MaxY, double MinZ, double MaxZ) right,
        double tolerance) =>
        Math.Abs(left.MinX - right.MinX) <= tolerance &&
        Math.Abs(left.MaxX - right.MaxX) <= tolerance &&
        Math.Abs(left.MinY - right.MinY) <= tolerance &&
        Math.Abs(left.MaxY - right.MaxY) <= tolerance &&
        Math.Abs(left.MinZ - right.MinZ) <= tolerance &&
        Math.Abs(left.MaxZ - right.MaxZ) <= tolerance;

    // Uç-uca sınırlı çizgi: iki ucu da kesicilerle değen çizgiye tıklanınca tümü silinir.
    public static void CizgiTrimUcUcaTumunuSiler()
    {
        var kesiciler = new List<(Point2D, Point2D)>
        {
            (new(0, -1), new(0, 1)),    // sol uçta
            (new(10, -1), new(10, 1))   // sağ uçta
        };
        bool ok = SalonTrimServisi.CizgiTrim(new(0, 0), new(10, 0), kesiciler, 0.5, out var parcalar);
        if (!ok) throw new InvalidOperationException("Uç kesişimleri 'kesim var' sayılmalıydı.");
        if (parcalar.Count != 0)
            throw new InvalidOperationException($"Tüm segment silinmeli (0 parça); {parcalar.Count} bulundu.");

        // Hiç kesici değmiyorsa false dönmeli.
        bool ok2 = SalonTrimServisi.CizgiTrim(new(0, 0), new(10, 0),
            new List<(Point2D, Point2D)> { (new(20, -1), new(20, 1)) }, 0.5, out _);
        if (ok2) throw new InvalidOperationException("Kesici değmiyorken false dönmeliydi.");
    }

    // Taşma kırpma: (0,0)-(10,0) çizgisi x=8 kesicisiyle, tıklama t=0.9 →
    // taşan [8,10] çıkar, [0,8] kalır (1 parça).
    public static void CizgiTrimTasmaKirpar()
    {
        var kesiciler = new List<(Point2D, Point2D)> { (new(8, -1), new(8, 1)) };
        bool ok = SalonTrimServisi.CizgiTrim(new(0, 0), new(10, 0), kesiciler, 0.9, out var parcalar);
        if (!ok) throw new InvalidOperationException("Kesim yapılmalıydı.");
        if (parcalar.Count != 1)
            throw new InvalidOperationException($"1 parça beklenir; {parcalar.Count} bulundu.");
        if (Math.Abs(parcalar[0].A.X - 0) > 1e-6 || Math.Abs(parcalar[0].B.X - 8) > 1e-6)
            throw new InvalidOperationException("Kalan parça [0,8] beklenir.");
    }

    // ── P0: hacim, kırpma, bölge, şablon sığdırma, doğrulama ─────────────────

    /// Yüzeyin XY düzlemindeki (plan) shoelace alanı.
    private static double PlanAlani(Kst.Acoustics.Core.Projects.ProjectSurface s)
    {
        double a = 0;
        var v = s.Vertices;
        for (int i = 0; i < v.Count; i++)
        {
            var p = v[i];
            var q = v[(i + 1) % v.Count];
            a += p.X * q.Y - q.X * p.Y;
        }
        return Math.Abs(a) / 2.0;
    }

    private static SalonGeometri KabukluSalon(double genislik, double derinlik, double h)
    {
        var salon = SalonGeometriServisi.YeniOlustur("Hacim Test");
        salon.SalonYuksekligiM = h;
        var zemin = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Zemin);
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Dikdortgen,
            Kapali = true,
            KatmanId = zemin.Id,
            Noktalar = new() { new(0, 0), new(genislik, 0), new(genislik, derinlik), new(0, derinlik) }
        });
        return salon;
    }

    // Hacim: 10×8 kapalı zemin × h=4 → tam 320 m³ (bbox değil, prizma).
    public static void HacimPrizmaHesaplanir()
    {
        var salon = KabukluSalon(10, 8, 4);
        double v = SalonHacimServisi.Hesapla(salon);
        if (Math.Abs(v - 320.0) > 1e-6)
            throw new InvalidOperationException($"V=320 beklenir; {v:F3} bulundu.");
    }

    // Hacim düşümleri: sahne platformu ve seyirci terasları hacimden düşülür.
    public static void HacimPlatformDusumleriUygulanir()
    {
        var salon = KabukluSalon(20, 20, 10); // 4000 m³
        salon.Sahne = new SahneGeometri
        {
            Tip = SahneTipi.Dikdortgen, GenislikM = 10, DerinlikM = 4,
            YukseklikM = 1.0, TabanKotuM = 0, MerkezX = 10, MerkezY = 3
        };
        // 10×8, 4 kademe × 0.5 m: katı = (80/4)·0.5·(0+1+2+3) = 60 m³
        salon.SeyirciSekciyonlari.Add(new SeyirciSekciyonu
        {
            GenislikM = 10, DerinlikM = 8, KademeSayisi = 4, KademeYuksekligiM = 0.5,
            TabanKotuM = 0, MerkezX = 10, MerkezY = 14
        });

        double v = SalonHacimServisi.Hesapla(salon);
        double beklenen = 4000 - 40 - 60;
        if (Math.Abs(v - beklenen) > 0.5)
            throw new InvalidOperationException($"V≈{beklenen} beklenir; {v:F1} bulundu.");
    }

    // M01 köprüsü gerçek hacmi ve kaynak revizyonu taşır (bbox değil).
    public static void M01KopruGercekHacimVeRevizyonTasir()
    {
        var salon = SalonParametrikServisi.Olustur(new SalonSablonu
        {
            SalonTip = SalonTipi.Yelpaze, SalonGenislikM = 24, SalonDerinlikM = 30, SalonYuksekligiM = 10
        });
        var project = SalonImporter.Cevir(salon, null);

        double bbox = 24.0 * 30.0 * 10.0;
        // Yelpaze plan alanı = trapez (0.6W+W)/2 × D = 576 m² → V < 5760 + payı; bbox 7200.
        if (project.RoomVolume >= bbox * 0.85)
            throw new InvalidOperationException($"Yelpaze hacmi bbox'a yakın olmamalı; {project.RoomVolume:F0} bulundu.");
        if (project.RoomVolume <= 3000)
            throw new InvalidOperationException($"Hacim makul aralıkta olmalı; {project.RoomVolume:F0} bulundu.");
        if (project.KaynakGeometriRevizyon != salon.GeometriRevizyon)
            throw new InvalidOperationException("KaynakGeometriRevizyon salon revizyonuna eşit olmalı.");
    }

    // Zemin kırpma: seyirci zemine oturuyorsa altındaki zemin yüzeyi çıkarılır (delik keyhole açılır).
    public static void ZeminSeyirciAltindanKirpilir()
    {
        var salon = KabukluSalon(20, 20, 8);
        salon.SeyirciSekciyonlari.Add(new SeyirciSekciyonu
        {
            GenislikM = 8, DerinlikM = 8, KademeSayisi = 4, KademeYuksekligiM = 0.5,
            TabanKotuM = 0, MerkezX = 10, MerkezY = 10 // tam içeride → delik
        });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        var zeminler = yuzeyler.Where(s => s.Category == "Zemin").ToList();
        if (zeminler.Count < 1)
            throw new InvalidOperationException("Kırpılmış zemin yüzeyi üretilmeli.");

        // Delik keyhole kesiğiyle açılır → basit poligon(lar); alan = 400 − 64 (kesik ihmal).
        // Shoelace delikli (kendinden geçmeli) poligonda 336'dan sapardı; ≈336 = delik doğru açılmış.
        double alan = zeminler.Sum(PlanAlani);
        if (Math.Abs(alan - (400 - 64)) > 0.2)
            throw new InvalidOperationException($"Zemin alanı ≈336 beklenir; {alan:F2} bulundu.");

        // Tavan kırpılmaz: tek yüzey, 400 m².
        double tavanAlan = yuzeyler.Where(s => s.Category == "Tavan").Sum(PlanAlani);
        if (Math.Abs(tavanAlan - 400) > 0.2)
            throw new InvalidOperationException($"Tavan alanı 400 beklenir; {tavanAlan:F2} bulundu.");
    }

    // Kabuk dışı katmandaki kapalı öğe bölgedir: tek yüzey üretir, kabuk zemininden kırpılır,
    // tavan/duvar üretmez.
    public static void BolgeOgesiKabukUretmezZeminKirpar()
    {
        var salon = KabukluSalon(10, 10, 5);
        var sahneKat = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Sahne);
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Dikdortgen,
            Kapali = true,
            KatmanId = sahneKat.Id,
            Noktalar = new() { new(0, 0), new(4, 0), new(4, 4), new(0, 4) } // köşede → delik yok
        });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);

        var sahneY = yuzeyler.Where(s => s.Category == "Sahne").ToList();
        if (sahneY.Count != 1 || Math.Abs(PlanAlani(sahneY[0]) - 16) > 0.1)
            throw new InvalidOperationException("Sahne bölgesi tek yüzey (16 m²) olmalı.");
        if (sahneY[0].Vertices.Any(v => Math.Abs(v.Z) > 1e-9))
            throw new InvalidOperationException("Sahne bölge yüzeyi z=0'da olmalı.");

        // Bölge tam yükseklik duvar/ikinci tavan üretmemeli.
        if (yuzeyler.Count(s => s.Category == "Tavan") != 1)
            throw new InvalidOperationException("Tek tavan olmalı (bölge tavan üretmez).");
        if (yuzeyler.Count(s => s.Category == "Duvar-Seyirci") != 4)
            throw new InvalidOperationException("Yalnız kabuk çevresi duvar üretmeli.");

        double zeminAlan = yuzeyler.Where(s => s.Category == "Zemin").Sum(PlanAlani);
        if (Math.Abs(zeminAlan - (100 - 16)) > 0.1)
            throw new InvalidOperationException($"Zemin alanı 84 beklenir; {zeminAlan:F2} bulundu.");
    }

    // Şablon sığdırma: yelpaze ve at nalında seyirci alanı plan dış hattının içinde kalmalı.
    public static void SablonSeyirciDisHattaSigar()
    {
        foreach (var tip in new[] { SalonTipi.Yelpaze, SalonTipi.AtNali, SalonTipi.Dikdortgen })
        {
            var sablon = new SalonSablonu { SalonTip = tip, SalonGenislikM = 24, SalonDerinlikM = 30 };
            var salon = SalonParametrikServisi.Olustur(sablon);
            var hat = salon.CizimOgeleri[0].Noktalar;
            var sek = salon.SeyirciSekciyonlari[0];
            var fp = SalonSeyirciServisi.Footprint(sek);

            double fpAlan = Math.Abs(ClipperAdaptor.IsaretliAlan(fp));
            double icAlan = ClipperAdaptor.NetAlan(ClipperAdaptor.Kesisim(
                new[] { (IReadOnlyList<Point2D>)hat }, new[] { (IReadOnlyList<Point2D>)fp }));
            if (icAlan < fpAlan * 0.999)
                throw new InvalidOperationException(
                    $"{tip}: seyirci dış hatta sığmalı (içeride %{icAlan / fpAlan * 100:F1}).");
        }
    }

    // Şablon "salon önü" sahne: dış hatta sahne cebi açılır, sahne kapalı hacmin içinde kalır.
    public static void SablonSalonOnuSahneCebiAcilir()
    {
        var sablon = new SalonSablonu
        {
            SalonTip = SalonTipi.Dikdortgen, SalonGenislikM = 24, SalonDerinlikM = 30,
            SahneKonum = SahneKonum.SalonOnu, SahneGenislikM = 14, SahneDerinlikM = 8
        };
        var salon = SalonParametrikServisi.Olustur(sablon);

        var hat = salon.CizimOgeleri[0].Noktalar;
        if (hat.Count != 8)
            throw new InvalidOperationException($"Cepli dikdörtgen 8 köşe beklenir; {hat.Count} bulundu.");
        if (!hat.Any(p => p.Y < -1))
            throw new InvalidOperationException("Cep ön kenarın önüne (y<0) uzanmalı.");

        // Sahne footprint'i dış hattın içinde olmalı → doğrulama uyarı üretmemeli.
        var uyarilar = SalonDogrulamaServisi.Dogrula(salon);
        if (uyarilar.Any(u => u.Contains("Sahne")))
            throw new InvalidOperationException($"Sahne cebi içinde uyarı olmamalı: {string.Join(" | ", uyarilar)}");
    }

    // Undo altyapısı: KopyaJson → KopyadanYukle derin kopya round-trip (öğe + sahne + katman bağı).
    public static void KopyaJsonRoundTrip()
    {
        var salon = KabukluSalon(10, 8, 5);
        salon.Sahne = new SahneGeometri { Tip = SahneTipi.Oval, GenislikM = 6, DerinlikM = 4, MerkezX = 5, MerkezY = 2 };
        salon.SalonAdi = "Kopya Test";

        var kopya = SalonGeometriServisi.KopyadanYukle(SalonGeometriServisi.KopyaJson(salon));

        if (ReferenceEquals(kopya, salon))
            throw new InvalidOperationException("Kopya ayrı örnek olmalı.");
        if (kopya.SalonAdi != "Kopya Test" || kopya.CizimOgeleri.Count != 1
            || kopya.Sahne is not { Tip: SahneTipi.Oval })
            throw new InvalidOperationException("Kopya içeriği korunmadı.");
        if (kopya.CizimOgeleri[0].KatmanId != salon.CizimOgeleri[0].KatmanId)
            throw new InvalidOperationException("Katman bağı korunmadı.");

        // Kopyada değişiklik orijinali etkilememeli (derin kopya).
        kopya.CizimOgeleri[0].Noktalar[0] = new Point2D(-99, -99);
        if (salon.CizimOgeleri[0].Noktalar[0].X < -1)
            throw new InvalidOperationException("Kopya orijinalden bağımsız olmalı.");
    }

    // Atomik kaydet: yazım başarısız olursa GeometriRevizyon artışı geri alınır,
    // başarılı kayıtta mevcut dosya bozulmadan üzerine taşınır.
    public static void AtomikKaydetHataRevizyonuGeriAlir()
    {
        var salon = SalonGeometriServisi.YeniOlustur("Atomik");
        int rev = salon.GeometriRevizyon;

        var olmayanDizin = Path.Combine(Path.GetTempPath(),
            $"yok-{Guid.NewGuid():N}", "alt", "salon.e4salon.json");
        bool hata = false;
        try { SalonGeometriServisi.Kaydet(salon, olmayanDizin); }
        catch (IOException) { hata = true; } // DirectoryNotFoundException dahil
        if (!hata) throw new InvalidOperationException("Olmayan dizine kayıt hata fırlatmalı.");
        if (salon.GeometriRevizyon != rev)
            throw new InvalidOperationException("Başarısız kayıtta revizyon geri alınmalı.");

        // Başarılı kayıt + üzerine kayıt (Move overwrite yolu) çalışmalı.
        var yol = Path.Combine(Path.GetTempPath(), $"atomik-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            SalonGeometriServisi.Kaydet(salon, yol);
            SalonGeometriServisi.Kaydet(salon, yol);
            if (salon.GeometriRevizyon != rev + 2)
                throw new InvalidOperationException("İki başarılı kayıt revizyonu 2 artırmalı.");
            if (File.Exists(yol + ".tmp"))
                throw new InvalidOperationException("Geçici dosya taşıma sonrası kalmamalı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // Konkav poligon üçgenleme: L-şekli kulak-kesme ile poligon alanına eşit üçgenler üretir
    // (naif fan poligon dışına taşan üçgenler üretir; DXF ihracatı bu yolu kullanır).
    public static void KonkavPoligonUcgenlemeAlaniKorur()
    {
        var l = new List<Kst.Acoustics.Core.Geometry.Point3D>
        {
            new(0, 0, 0), new(4, 0, 0), new(4, 2, 0), new(2, 2, 0), new(2, 4, 0), new(0, 4, 0)
        };

        double toplam = 0;
        int adet = 0;
        foreach (var (a, b, c) in Kst.Acoustics.Core.Simulation.RayTracingService.TriangulatePolygon(l))
        {
            double ux = b.X - a.X, uy = b.Y - a.Y;
            double vx = c.X - a.X, vy = c.Y - a.Y;
            toplam += Math.Abs(ux * vy - uy * vx) / 2.0;
            adet++;
        }

        if (adet != 4)
            throw new InvalidOperationException($"6 köşeli poligon 4 üçgen üretmeli; {adet} bulundu.");
        if (Math.Abs(toplam - 12.0) > 1e-9)
            throw new InvalidOperationException($"Üçgen alan toplamı 12 olmalı (L-şekli); {toplam:F4} bulundu.");
    }

    // ── P2: tavan profili, kapasite, arayüz sözleşmesi ────────────────────────

    // Tavan profili enterpolasyonu: uçlarda clamp, arada doğrusal; <2 nokta = düz.
    public static void TavanProfiliEnterpolasyon()
    {
        var salon = SalonGeometriServisi.YeniOlustur("Tavan");
        salon.SalonYuksekligiM = 6;

        if (SalonTavanServisi.ProfilliMi(salon) || Math.Abs(SalonTavanServisi.Kot(salon, 5) - 6) > 1e-9)
            throw new InvalidOperationException("Profil yokken düz tavan yüksekliği dönmeli.");

        salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 0, KotM = 4 });
        if (SalonTavanServisi.ProfilliMi(salon))
            throw new InvalidOperationException("Tek nokta profil sayılmamalı.");

        salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 10, KotM = 8 });
        if (!SalonTavanServisi.ProfilliMi(salon))
            throw new InvalidOperationException("İki nokta profil sayılmalı.");
        if (Math.Abs(SalonTavanServisi.Kot(salon, -5) - 4) > 1e-9)
            throw new InvalidOperationException("Profil öncesi uç kotu (clamp) dönmeli.");
        if (Math.Abs(SalonTavanServisi.Kot(salon, 5) - 6) > 1e-9)
            throw new InvalidOperationException("Ara değer doğrusal enterpole edilmeli (6).");
        if (Math.Abs(SalonTavanServisi.Kot(salon, 15) - 8) > 1e-9)
            throw new InvalidOperationException("Profil sonrası uç kotu (clamp) dönmeli.");
        if (Math.Abs(SalonTavanServisi.MaksKot(salon) - 8) > 1e-9)
            throw new InvalidOperationException("MaksKot 8 olmalı.");
    }

    // Profilli tavan 3D: eğik tavan paneli (z = kot(y)) + duvar üstleri profili izler.
    public static void Uc3DProfilliTavanVeDuvarUretir()
    {
        var salon = KabukluSalon(10, 10, 6);
        salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 0, KotM = 4 });
        salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 10, KotM = 8 });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);

        var tavan = yuzeyler.Where(s => s.Category == "Tavan").ToList();
        if (tavan.Count != 1)
            throw new InvalidOperationException($"Tek eğik tavan paneli beklenir; {tavan.Count} bulundu.");
        foreach (var v in tavan[0].Vertices)
            if (Math.Abs(v.Z - (4 + 0.4 * v.Y)) > 1e-6)
                throw new InvalidOperationException($"Tavan köşesi z=4+0.4y izlemeli; y={v.Y:F2} z={v.Z:F2}.");

        var duvarlar = yuzeyler.Where(s => s.Category == "Duvar-Seyirci").ToList();
        if (duvarlar.Count != 4)
            throw new InvalidOperationException($"4 çevre duvarı beklenir; {duvarlar.Count} bulundu.");
        foreach (var d in duvarlar)
            foreach (var v in d.Vertices)
            {
                if (Math.Abs(v.Z) < 1e-9) continue; // taban köşesi
                if (Math.Abs(v.Z - (4 + 0.4 * v.Y)) > 1e-6)
                    throw new InvalidOperationException(
                        $"Duvar üst köşesi tavan profilini izlemeli; y={v.Y:F2} z={v.Z:F2}.");
            }

        // Ön duvar (y=0) üstü 4 m, arka duvar (y=10) üstü 8 m olmalı.
        if (!duvarlar.Any(d => d.Vertices.All(v => Math.Abs(v.Y) < 1e-9)
                               && d.Vertices.Any(v => Math.Abs(v.Z - 4) < 1e-6)))
            throw new InvalidOperationException("Ön duvar üst kotu 4 m olmalı.");
        if (!duvarlar.Any(d => d.Vertices.All(v => Math.Abs(v.Y - 10) < 1e-9)
                               && d.Vertices.Any(v => Math.Abs(v.Z - 8) < 1e-6)))
            throw new InvalidOperationException("Arka duvar üst kotu 8 m olmalı.");
    }

    // Profilli tavan hacmi: ∫(4+0.4y) dA (10×10) = 400 + 0.4·500 = 600 m³.
    public static void HacimProfilliTavanIntegrali()
    {
        var salon = KabukluSalon(10, 10, 6);
        salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 0, KotM = 4 });
        salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 10, KotM = 8 });

        double v = SalonHacimServisi.Hesapla(salon);
        if (Math.Abs(v - 600.0) > 0.5)
            throw new InvalidOperationException($"Profilli hacim ≈600 beklenir; {v:F1} bulundu.");
    }

    // Tavan profili .e4salon.json round-trip'te korunur.
    public static void TavanProfiliRoundTrip()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4salon-tavan-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            var salon = SalonGeometriServisi.YeniOlustur("Tavan RT");
            salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 2, KotM = 5.5 });
            salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 12, KotM = 9.25 });
            SalonGeometriServisi.Kaydet(salon, yol);

            var s2 = SalonGeometriServisi.Ac(yol);
            if (s2.TavanProfili.Count != 2
                || Math.Abs(s2.TavanProfili[1].KotM - 9.25) > 1e-9
                || Math.Abs(s2.TavanProfili[0].YM - 2) > 1e-9)
                throw new InvalidOperationException("Tavan profili round-trip'te korunmadı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // Koltuk kapasitesi: brüt alan / 0.5 m² (16×12 → 384).
    public static void KoltukKapasitesiHesaplanir()
    {
        var sek = new SeyirciSekciyonu { GenislikM = 16, DerinlikM = 12 };
        int k = SalonSeyirciServisi.KoltukKapasitesi(sek);
        if (k != 384)
            throw new InvalidOperationException($"16×12 alan 384 koltuk vermeli; {k} bulundu.");
        if (SalonSeyirciServisi.KoltukKapasitesi(sek, 0.6) != 320)
            throw new InvalidOperationException("0,6 m²/koltuk için 320 beklenir.");
    }

    // ISalonGeometri tüketici sözleşmesi: sahne/seyirci/balkon/tavan salt-okunur görünür.
    public static void ArayuzSozlesmesiTamKapsar()
    {
        var salon = SalonParametrikServisi.Olustur(new SalonSablonu());
        salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 0, KotM = 8 });
        salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 30, KotM = 11 });

        ISalonGeometri g = salon;
        if (g.Sahne is null || g.Sahne.TabanPoligonu().Count < 3)
            throw new InvalidOperationException("Arayüz sahneyi ve taban poligonunu sunmalı.");
        if (g.SeyirciSekciyonlari.Count != 1 || g.SeyirciSekciyonlari[0].KademeSayisi < 1)
            throw new InvalidOperationException("Arayüz seyirci bölümlerini sunmalı.");
        if (g.TavanProfili.Count != 2 || Math.Abs(g.TavanProfili[1].KotM - 11) > 1e-9)
            throw new InvalidOperationException("Arayüz tavan profilini sunmalı.");
        if (g.Balkonlar is null)
            throw new InvalidOperationException("Arayüz balkon listesini sunmalı.");
    }

    // Dışa taşan seyirci basamakları kabuğa kırpılır; korniş genişliği de kırpılır.
    public static void SeyirciKabugaKirpilir()
    {
        var salon = KabukluSalon(10, 10, 6);
        salon.SeyirciSekciyonlari.Add(new SeyirciSekciyonu
        {
            GenislikM = 20, DerinlikM = 4, KademeSayisi = 2, KademeYuksekligiM = 0.5,
            TabanKotuM = 0, MerkezX = 5, MerkezY = 5 // x: −5..15 → kabuk 0..10'a kırpılmalı
        });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);

        double seyirciAlan = yuzeyler.Where(s => s.Category == "Seyirci").Sum(PlanAlani);
        if (Math.Abs(seyirciAlan - 40) > 0.2) // 10 (kırpılmış genişlik) × 4
            throw new InvalidOperationException($"Kırpılmış seyirci alanı ≈40 beklenir; {seyirciAlan:F2} bulundu.");

        var kornisler = yuzeyler.Where(s => s.Category == "Korniş").ToList();
        if (kornisler.Count != 1)
            throw new InvalidOperationException($"1 korniş beklenir; {kornisler.Count} bulundu.");
        double kGen = kornisler[0].Vertices.Max(v => v.X) - kornisler[0].Vertices.Min(v => v.X);
        if (Math.Abs(kGen - 10) > 0.05)
            throw new InvalidOperationException($"Korniş genişliği kabuğa kırpılmalı (≈10); {kGen:F2} bulundu.");
    }

    // Orkestra çukuru: zemin açıklığı + taban (−d) + 4 çevre duvarı; hacim çukuru ekler.
    public static void OrkestraCukuru3DVeHacim()
    {
        var salon = KabukluSalon(20, 20, 5); // 2000 m³
        salon.OrkestraCukuru = new OrkestraCukuru
        {
            GenislikM = 10, DerinlikM = 3, CukurDerinligiM = 2, MerkezX = 10, MerkezY = 5
        };

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);

        double zeminAlan = yuzeyler.Where(s => s.Category == "Zemin").Sum(PlanAlani);
        if (Math.Abs(zeminAlan - (400 - 30)) > 0.2)
            throw new InvalidOperationException($"Zemin çukur açıklığıyla ≈370 olmalı; {zeminAlan:F2} bulundu.");

        var orkestra = yuzeyler.Where(s => s.Category == "Orkestra").ToList();
        var tabanlar = orkestra.Where(s => s.Vertices.All(v => Math.Abs(v.Z + 2) < 1e-9)).ToList();
        if (tabanlar.Count != 1 || Math.Abs(PlanAlani(tabanlar[0]) - 30) > 0.1)
            throw new InvalidOperationException("Çukur tabanı z=−2'de 30 m² olmalı.");
        int duvarSayisi = orkestra.Count(s => s.Vertices.Any(v => Math.Abs(v.Z) < 1e-9)
                                              && s.Vertices.Any(v => Math.Abs(v.Z + 2) < 1e-9));
        if (duvarSayisi != 4)
            throw new InvalidOperationException($"4 çukur duvarı beklenir; {duvarSayisi} bulundu.");

        double v = SalonHacimServisi.Hesapla(salon);
        if (Math.Abs(v - 2060) > 0.5) // 2000 + 30×2
            throw new InvalidOperationException($"Hacim çukuru eklemeli (≈2060); {v:F1} bulundu.");
    }

    // Orkestra çukuru round-trip'te korunur; doğrulama taşma/sahne çakışması uyarır.
    public static void OrkestraCukuruRoundTripVeDogrulama()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4salon-cukur-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            var salon = SalonGeometriServisi.YeniOlustur("Çukur RT");
            salon.OrkestraCukuru = new OrkestraCukuru { GenislikM = 9, DerinlikM = 2.5, CukurDerinligiM = 1.8, MerkezX = 1 };
            SalonGeometriServisi.Kaydet(salon, yol);
            var s2 = SalonGeometriServisi.Ac(yol);
            if (s2.OrkestraCukuru is null || Math.Abs(s2.OrkestraCukuru.CukurDerinligiM - 1.8) > 1e-9)
                throw new InvalidOperationException("Orkestra çukuru round-trip'te korunmadı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }

        // Sahneyle çakışan çukur uyarı üretmeli.
        var s = KabukluSalon(20, 20, 6);
        s.Sahne = new SahneGeometri { GenislikM = 10, DerinlikM = 6, YukseklikM = 1, MerkezX = 10, MerkezY = 4 };
        s.OrkestraCukuru = new OrkestraCukuru { GenislikM = 8, DerinlikM = 3, CukurDerinligiM = 2, MerkezX = 10, MerkezY = 6 };
        var uyarilar = SalonDogrulamaServisi.Dogrula(s);
        if (!uyarilar.Any(u => u.Contains("çakışıyor")))
            throw new InvalidOperationException("Çukur-sahne çakışma uyarısı beklenir.");
    }

    // Faset ayarı: daire kabuğun zemin köşe sayısını belirler; aralık dışı kelepçelenir.
    public static void FasetAyariEtkili()
    {
        var salon = SalonGeometriServisi.YeniOlustur("Faset");
        salon.SalonYuksekligiM = 4;
        salon.FasetKenarSayisi = 12;
        var zemin = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Zemin);
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Daire, Kapali = true, YariCapM = 5,
            KatmanId = zemin.Id, Noktalar = { new(0, 0) }
        });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        var zeminY = yuzeyler.First(s => s.Category == "Zemin");
        if (zeminY.Vertices.Count != 12)
            throw new InvalidOperationException($"Faset 12 → 12 köşeli zemin beklenir; {zeminY.Vertices.Count} bulundu.");
        if (yuzeyler.Count(s => s.Category == "Duvar-Seyirci") != 12)
            throw new InvalidOperationException("12 çevre duvarı beklenir.");

        if (Salon3DGorunumServisi.Faset(new SalonGeometri { FasetKenarSayisi = 500 }) != 128
            || Salon3DGorunumServisi.Faset(new SalonGeometri { FasetKenarSayisi = 4 }) != 12)
            throw new InvalidOperationException("Faset 12–128 aralığına kelepçelenmeli.");
    }

    /// 3D poligon alanı (Newell normali) — düşey duvar yüzeyleri için.
    private static double Alan3D(Kst.Acoustics.Core.Projects.ProjectSurface s)
    {
        double nx = 0, ny = 0, nz = 0;
        var v = s.Vertices;
        for (int i = 0; i < v.Count; i++)
        {
            var p = v[i];
            var q = v[(i + 1) % v.Count];
            nx += (p.Y - q.Y) * (p.Z + q.Z);
            ny += (p.Z - q.Z) * (p.X + q.X);
            nz += (p.X - q.X) * (p.Y + q.Y);
        }
        return Math.Sqrt(nx * nx + ny * ny + nz * nz) / 2.0;
    }

    // Kapı: duvardan kesilir, "Kapı" kategorisiyle doldurulur; toplam duvar alanı azalır.
    public static void KapiDuvariKeserVeDoldurur()
    {
        var salon = KabukluSalon(10, 8, 4);
        salon.Acikliklar.Add(new DuvarAcikligi
        {
            Tip = AciklikTipi.Kapi, GenislikM = 2.0, YukseklikM = 2.1, AltKotM = 0,
            MerkezX = 5, MerkezY = 0 // ön duvar (y=0) ortası
        });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);

        var kapilar = yuzeyler.Where(s => s.Category == "Kapı").ToList();
        double kapiAlan = kapilar.Sum(Alan3D);
        if (Math.Abs(kapiAlan - 4.2) > 0.05)
            throw new InvalidOperationException($"Kapı alanı 2×2,1=4,2 beklenir; {kapiAlan:F2} bulundu.");
        if (kapilar.Any(s => s.Vertices.Any(v => Math.Abs(v.Y) > 1e-6)))
            throw new InvalidOperationException("Kapı yüzeyi ön duvar düzleminde (y=0) olmalı.");

        double duvarAlan = yuzeyler.Where(s => s.Category == "Duvar-Seyirci").Sum(Alan3D);
        double beklenen = 2 * (10 + 8) * 4 - 4.2; // çevre − kapı
        if (Math.Abs(duvarAlan - beklenen) > 0.1)
            throw new InvalidOperationException($"Duvar alanı ≈{beklenen:F1} beklenir; {duvarAlan:F2} bulundu.");
    }

    // Pencere: alt kot uygulanır, delik duvar içinde yüzer (keyhole), alan korunur.
    public static void PencereAltKotUygulanir()
    {
        var salon = KabukluSalon(10, 8, 4);
        salon.Acikliklar.Add(new DuvarAcikligi
        {
            Tip = AciklikTipi.Pencere, GenislikM = 1.5, YukseklikM = 1.2, AltKotM = 1.0,
            MerkezX = 5, MerkezY = 0
        });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);

        var pencereler = yuzeyler.Where(s => s.Category == "Pencere").ToList();
        double alan = pencereler.Sum(Alan3D);
        if (Math.Abs(alan - 1.8) > 0.05)
            throw new InvalidOperationException($"Pencere alanı 1,5×1,2=1,8 beklenir; {alan:F2} bulundu.");

        double zMin = pencereler.SelectMany(s => s.Vertices).Min(v => v.Z);
        double zMax = pencereler.SelectMany(s => s.Vertices).Max(v => v.Z);
        if (Math.Abs(zMin - 1.0) > 1e-6 || Math.Abs(zMax - 2.2) > 1e-6)
            throw new InvalidOperationException($"Pencere z aralığı [1,0–2,2] beklenir; [{zMin:F2}–{zMax:F2}] bulundu.");

        double duvarAlan = yuzeyler.Where(s => s.Category == "Duvar-Seyirci").Sum(Alan3D);
        double beklenen = 2 * (10 + 8) * 4 - 1.8;
        if (Math.Abs(duvarAlan - beklenen) > 0.1)
            throw new InvalidOperationException($"Duvar alanı ≈{beklenen:F1} beklenir; {duvarAlan:F2} bulundu.");
    }

    // Açıklık round-trip'te korunur; duvara bağlanmayan açıklık uyarı üretir.
    public static void AciklikRoundTripVeDogrulama()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4salon-aciklik-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            var salon = SalonGeometriServisi.YeniOlustur("Açıklık RT");
            salon.Acikliklar.Add(new DuvarAcikligi
            {
                Ad = "Pencere 1", Tip = AciklikTipi.Pencere,
                GenislikM = 1.8, YukseklikM = 1.4, AltKotM = 0.9, MerkezX = 3, MerkezY = 0
            });
            SalonGeometriServisi.Kaydet(salon, yol);
            var s2 = SalonGeometriServisi.Ac(yol);
            if (s2.Acikliklar.Count != 1 || s2.Acikliklar[0].Tip != AciklikTipi.Pencere
                || Math.Abs(s2.Acikliklar[0].AltKotM - 0.9) > 1e-9)
                throw new InvalidOperationException("Açıklık round-trip'te korunmadı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }

        // Duvardan uzak açıklık → uyarı.
        var s = KabukluSalon(10, 8, 4);
        s.Acikliklar.Add(new DuvarAcikligi { Ad = "Kapı 1", MerkezX = 5, MerkezY = 4 }); // salon ortası
        var uyarilar = SalonDogrulamaServisi.Dogrula(s);
        if (!uyarilar.Any(u => u.Contains("hiçbir duvarın üzerinde değil")))
            throw new InvalidOperationException("Duvara bağlanmayan açıklık uyarısı beklenir.");

        // Duvar üzerindeki açıklık uyarı üretmemeli.
        var s2b = KabukluSalon(10, 8, 4);
        s2b.Acikliklar.Add(new DuvarAcikligi { Ad = "Kapı 1", MerkezX = 5, MerkezY = 0 });
        if (SalonDogrulamaServisi.Dogrula(s2b).Any(u => u.Contains("hiçbir duvarın")))
            throw new InvalidOperationException("Duvar üzerindeki açıklık uyarı üretmemeli.");
    }

    // ── Faz 14-17: yay, uzat, offset, dönüşümler ─────────────────────────────

    // Üç noktadan yay: merkez/yarıçap doğru, kavis noktası yay üzerinde, uçlar korunur.
    public static void UcNoktadanYayKurulur()
    {
        var yay = SalonCizimOgesi.UcNoktadanYay(new(0, 0), new(10, 0), new(5, 5));
        if (yay is null) throw new InvalidOperationException("Yay kurulmalıydı.");
        if (Math.Abs(yay.Noktalar[0].X - 5) > 1e-9 || Math.Abs(yay.Noktalar[0].Y) > 1e-9)
            throw new InvalidOperationException($"Merkez (5,0) beklenir; ({yay.Noktalar[0].X:F3},{yay.Noktalar[0].Y:F3}).");
        if (Math.Abs(yay.YariCapM - 5) > 1e-9)
            throw new InvalidOperationException($"Yarıçap 5 beklenir; {yay.YariCapM:F3}.");

        // Uçlar {S,E} kümesi olmalı (CCW normalizasyonu sırayı değiştirebilir).
        var uclar = yay.YayUclari() ?? throw new InvalidOperationException("Uçlar dönmeli.");
        bool sVar = Mesafe0(uclar.Bas, 0, 0) < 1e-6 || Mesafe0(uclar.Bitis, 0, 0) < 1e-6;
        bool eVar = Mesafe0(uclar.Bas, 10, 0) < 1e-6 || Mesafe0(uclar.Bitis, 10, 0) < 1e-6;
        if (!sVar || !eVar) throw new InvalidOperationException("Yay uçları (0,0) ve (10,0) olmalı.");

        // Kavis noktası yay üzerinde: fasetlerden birine yakın olmalı.
        var fp = yay.YayiFasetle(96);
        if (!fp.Any(p => Mesafe0(p, 5, 5) < 0.05))
            throw new InvalidOperationException("Kavis noktası (5,5) yay üzerinde olmalı.");

        // Yarım çember uzunluğu ≈ π·r.
        double boy = yay.Segmentler(96).Sum(s => Math.Sqrt(
            (s.B.X - s.A.X) * (s.B.X - s.A.X) + (s.B.Y - s.A.Y) * (s.B.Y - s.A.Y)));
        if (Math.Abs(boy - Math.PI * 5) > 0.05)
            throw new InvalidOperationException($"Yay uzunluğu ≈{Math.PI * 5:F2} beklenir; {boy:F2}.");

        // Doğrusal noktalar → null.
        if (SalonCizimOgesi.UcNoktadanYay(new(0, 0), new(10, 0), new(5, 0)) is not null)
            throw new InvalidOperationException("Doğrusal noktalar yay kurmamalı.");
    }

    private static double Mesafe0(Point2D p, double x, double y)
        => Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y));

    // Yay round-trip'te korunur ve 3D'de fasetli duvar üretir.
    public static void YayRoundTripVeDuvarUretir()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4salon-yay-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            var salon = SalonGeometriServisi.YeniOlustur("Yay RT");
            var duvarKat = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.DuvarSeyirci);
            salon.CizimOgeleri.Add(new SalonCizimOgesi
            {
                Tip = SalonCizimTipi.Yay,
                KatmanId = duvarKat.Id,
                YariCapM = 6,
                AciBasDeg = 0,
                AciBitisDeg = 180,
                Noktalar = { new(0, 0) }
            });
            SalonGeometriServisi.Kaydet(salon, yol);
            var s2 = SalonGeometriServisi.Ac(yol);
            var yay = s2.CizimOgeleri[0];
            if (yay.Tip != SalonCizimTipi.Yay || Math.Abs(yay.AciBitisDeg - 180) > 1e-9)
                throw new InvalidOperationException("Yay açıları round-trip'te korunmadı.");

            var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(s2);
            int duvar = yuzeyler.Count(sf => sf.Category == "Duvar-Seyirci");
            if (duvar != 24) // 48 faset / yarım çember
                throw new InvalidOperationException($"Yarım çember 24 duvar üretmeli; {duvar} bulundu.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // Uzat: uç ilk kesişime taşınır; gerideki/olmayan kesici hedef değildir.
    public static void UzatKesisimeUzar()
    {
        var kesiciler = new List<(Point2D, Point2D)>
        {
            (new(2, -5), new(2, 5)),   // ucun GERİSİNDE → yok sayılır
            (new(10, -5), new(10, 5)), // ilk ileri kesişim
            (new(14, -5), new(14, 5))  // daha uzak
        };
        var yeni = SalonTrimServisi.Uzat(new(0, 0), new(4, 0), kesiciler);
        if (yeni is null || Math.Abs(yeni.Value.X - 10) > 1e-9 || Math.Abs(yeni.Value.Y) > 1e-9)
            throw new InvalidOperationException($"Uç (10,0)'a uzamalı; {yeni?.X:F2},{yeni?.Y:F2}.");

        if (SalonTrimServisi.Uzat(new(0, 0), new(4, 0),
                new List<(Point2D, Point2D)> { (new(2, -5), new(2, 5)) }) is not null)
            throw new InvalidOperationException("İleri kesişim yokken null dönmeli.");
    }

    // Açık offset: L-şekli sola 1 m paralel — köşe miter kesişimi doğru.
    public static void AcikOffsetParalelUretir()
    {
        var pts = new List<Point2D> { new(0, 0), new(10, 0), new(10, 10) };
        var off = SalonPoligonServisi.AcikOffset(pts, 1.0);
        if (off.Count != 3)
            throw new InvalidOperationException($"3 nokta beklenir; {off.Count}.");
        if (Mesafe0(off[0], 0, 1) > 1e-9)
            throw new InvalidOperationException($"İlk nokta (0,1) beklenir; ({off[0].X:F2},{off[0].Y:F2}).");
        if (Mesafe0(off[1], 9, 1) > 1e-9)
            throw new InvalidOperationException($"Köşe miter (9,1) beklenir; ({off[1].X:F2},{off[1].Y:F2}).");
        if (Mesafe0(off[2], 9, 10) > 1e-9)
            throw new InvalidOperationException($"Son nokta (9,10) beklenir; ({off[2].X:F2},{off[2].Y:F2}).");
    }

    // Dönüşümler: taşı/döndür/aynala nokta matematiği + yay açı düzeltmeleri + kopya bağımsızlığı.
    public static void DonusumlerYayAcilariniKorur()
    {
        SalonCizimOgesi YeniYay() => new()
        {
            Tip = SalonCizimTipi.Yay, YariCapM = 5,
            AciBasDeg = 0, AciBitisDeg = 180, Noktalar = { new(5, 0) }
        };

        // Taşı
        var y1 = YeniYay();
        SalonDonusumServisi.Tasi(y1, 3, -2);
        if (Mesafe0(y1.Noktalar[0], 8, -2) > 1e-9)
            throw new InvalidOperationException("Taşı merkezi ötelemedi.");

        // Döndür 90° CCW merkez (5,0) etrafında: üst yarı → sol yarı (90..270).
        var y2 = YeniYay();
        SalonDonusumServisi.Dondur(y2, new Point2D(5, 0), 90);
        var u2 = y2.YayUclari()!.Value;
        if (Mesafe0(u2.Bas, 5, 5) > 1e-6 || Mesafe0(u2.Bitis, 5, -5) > 1e-6)
            throw new InvalidOperationException("Döndürülen yay uçları (5,5)→(5,-5) olmalı.");

        // Aynala X ekseni: üst yarı → alt yarı; CCW yönü korunur (uçlar takaslanır).
        var y3 = YeniYay();
        SalonDonusumServisi.Aynala(y3, new Point2D(0, 0), new Point2D(1, 0));
        var fp = y3.YayiFasetle(96);
        if (!fp.Any(p => Mesafe0(p, 5, -5) < 0.05))
            throw new InvalidOperationException("Aynalanan yay alt yarıdan geçmeli (5,-5).");
        if (fp.Any(p => p.Y > 1e-6))
            throw new InvalidOperationException("Aynalanan yay üst yarıda nokta içermemeli.");

        // Kopya: yeni Id + bağımsız nokta listesi.
        var kaynak = YeniYay();
        var kopya = SalonDonusumServisi.Kopyala(kaynak);
        if (kopya.Id == kaynak.Id) throw new InvalidOperationException("Kopya yeni Id almalı.");
        kopya.Noktalar[0] = new Point2D(-9, -9);
        if (kaynak.Noktalar[0].X < 0) throw new InvalidOperationException("Kopya bağımsız olmalı.");
    }

    // Doğrulama: tek noktalı tavan profili ve kot≤0 uyarı üretir.
    public static void DogrulamaTavanProfiliUyarilari()
    {
        var salon = KabukluSalon(10, 10, 6);
        salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 5, KotM = 7 });
        var u1 = SalonDogrulamaServisi.Dogrula(salon);
        if (!u1.Any(u => u.Contains("tek nokta")))
            throw new InvalidOperationException("Tek noktalı profil uyarısı beklenir.");

        salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = 8, KotM = -1 });
        var u2 = SalonDogrulamaServisi.Dogrula(salon);
        if (!u2.Any(u => u.Contains("kot ≤ 0")))
            throw new InvalidOperationException("Kot ≤ 0 uyarısı beklenir.");
    }

    // Doğrulama: dışarı taşan seyirci uyarı üretir; kapalı zemin yoksa kabuk uyarısı döner.
    public static void DogrulamaTasmaVeKabukUyarisi()
    {
        var salon = KabukluSalon(10, 10, 5);
        salon.SeyirciSekciyonlari.Add(new SeyirciSekciyonu
        {
            GenislikM = 30, DerinlikM = 30, KademeSayisi = 2, KademeYuksekligiM = 0.4,
            MerkezX = 5, MerkezY = 5
        });
        var uyarilar = SalonDogrulamaServisi.Dogrula(salon);
        if (!uyarilar.Any(u => u.Contains("seyirci") && u.Contains("taşıyor")))
            throw new InvalidOperationException("Taşan seyirci için uyarı beklenir.");

        var bos = SalonGeometriServisi.YeniOlustur("Boş");
        var uyari2 = SalonDogrulamaServisi.Dogrula(bos);
        if (!uyari2.Any(u => u.Contains("Kapalı zemin poligonu yok")))
            throw new InvalidOperationException("Kabuk yokken 'kapalı zemin yok' uyarısı beklenir.");
    }

    // Regresyon: Normalize, canlı UI bağlarının izlediği koleksiyon örneklerini korur;
    // v1 dosya yeni koleksiyonlar + eksik semantik katmanlarla v2'ye taşınır.
    public static void KaydetNormalizeKimlikVeSemaDogrular()
    {
        var salon = SalonGeometriServisi.YeniOlustur("Kimlik");
        var refs = new object[]
        {
            salon.Katmanlar, salon.CizimOgeleri, salon.SeyirciSekciyonlari,
            salon.Balkonlar, salon.Acikliklar, salon.TavanProfili,
            salon.Olculendirmeler, salon.GeometrikKisitlar,
            salon.ReferansAltliklar, salon.YansiticiPaneller
        };
        var yol = Path.Combine(Path.GetTempPath(), $"salon-kimlik-{Guid.NewGuid():N}.e4salon.json");
        var eskiYol = Path.Combine(Path.GetTempPath(), $"salon-v1-{Guid.NewGuid():N}.e4salon.json");
        var ileriYol = Path.Combine(Path.GetTempPath(), $"salon-v-ileri-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            SalonGeometriServisi.Kaydet(salon, yol);
            var sonRefs = new object[]
            {
                salon.Katmanlar, salon.CizimOgeleri, salon.SeyirciSekciyonlari,
                salon.Balkonlar, salon.Acikliklar, salon.TavanProfili,
                salon.Olculendirmeler, salon.GeometrikKisitlar,
                salon.ReferansAltliklar, salon.YansiticiPaneller
            };
            if (refs.Where((t, i) => !ReferenceEquals(t, sonRefs[i])).Any())
                throw new InvalidOperationException("Kaydet/Normalize koleksiyon kimliklerini korumalı.");

            File.WriteAllText(eskiYol,
                "{\"SemaSurum\":1,\"Katmanlar\":[{\"Id\":\"z\",\"Ad\":\"Zemin\",\"Tip\":\"Zemin\"}]," +
                "\"Olculendirmeler\":null,\"GeometrikKisitlar\":null," +
                "\"ReferansAltliklar\":null,\"YansiticiPaneller\":null}");
            var migrated = SalonGeometriServisi.Ac(eskiYol);
            if (migrated.SemaSurum != 2
                || migrated.Olculendirmeler is null || migrated.GeometrikKisitlar is null
                || migrated.ReferansAltliklar is null || migrated.YansiticiPaneller is null)
                throw new InvalidOperationException("v1 salon bütün v2 koleksiyonlarıyla migrate edilmeli.");
            foreach (SalonKatmanTipi tip in Enum.GetValues<SalonKatmanTipi>())
                if (tip != SalonKatmanTipi.Genel && !migrated.Katmanlar.Any(k => k.Tip == tip))
                    throw new InvalidOperationException($"Migrasyonda '{tip}' semantik katmanı eksik.");

            var ileri = SalonGeometriServisi.YeniOlustur("İleri");
            ileri.SemaSurum = SalonGeometriServisi.GuncelSemaSurum + 1;
            bool reddedildi = false;
            try { SalonGeometriServisi.Kaydet(ileri, ileriYol); }
            catch (InvalidDataException) { reddedildi = true; }
            if (!reddedildi || File.Exists(ileriYol))
                throw new InvalidOperationException("Kaydet gelecek şema sürümünü yazmadan reddetmeli.");
        }
        finally
        {
            foreach (var p in new[] { yol, eskiYol, ileriYol })
                if (File.Exists(p)) File.Delete(p);
        }
    }

    // Regresyon: dolu hacim sıfır kalır; kısmi seyirci gerçek kademe Y'siyle,
    // balkon ise tavanla düşey kesişimiyle düşülür.
    public static void HacimSinirDurumlariDogruKirpar()
    {
        var dolu = KabukluSalon(10, 10, 3);
        dolu.Sahne = new SahneGeometri
        {
            GenislikM = 10, DerinlikM = 10, YukseklikM = 3,
            TabanKotuM = 0, MerkezX = 5, MerkezY = 5
        };
        if (Math.Abs(SalonHacimServisi.Hesapla(dolu)) > 1e-6)
            throw new InvalidOperationException("Odayı bütünüyle dolduran sahnede hava hacmi 0 olmalı.");

        var kismi = KabukluSalon(10, 5, 10); // x=0..10, y=0..5
        var sek = new SeyirciSekciyonu
        {
            GenislikM = 10, DerinlikM = 10, KademeSayisi = 2,
            KademeYuksekligiM = 1, TabanKotuM = 0, MerkezX = 5, MerkezY = 0
        };
        kismi.SeyirciSekciyonlari.Add(sek);
        double yuksekYariIceride = SalonHacimServisi.Hesapla(kismi);
        if (Math.Abs(yuksekYariIceride - 450) > 1e-6)
            throw new InvalidOperationException($"İçerideki yüksek kademe 50 m³ düşmeli; {yuksekYariIceride:F3}.");
        sek.TersYon = true;
        double yuksekYariDisarida = SalonHacimServisi.Hesapla(kismi);
        if (Math.Abs(yuksekYariDisarida - 500) > 1e-6)
            throw new InvalidOperationException($"Dışarıdaki yüksek kademe hacim düşmemeli; {yuksekYariDisarida:F3}.");

        var balkon = KabukluSalon(10, 10, 3);
        balkon.Balkonlar.Add(new BalkonGeometri
        {
            GenislikM = 10, DerinlikM = 10, MerkezX = 5, MerkezY = 5,
            TabanKotuM = 10, KalinlikM = 0.4
        });
        if (Math.Abs(SalonHacimServisi.Hesapla(balkon) - 300) > 1e-6)
            throw new InvalidOperationException("Tavan üstündeki balkon oda havasından düşülmemeli.");
    }

    // Regresyon: örtüşen malzeme bölgelerinde son çizilen kazanır; toplam döşeme
    // alanı kabuk alanını aşmaz ve coplanar çift yüzey oluşmaz.
    public static void BolgeOrtusmeleriTekYuzeyKaplar()
    {
        var salon = KabukluSalon(10, 10, 4);
        var a = SalonKatman.Olustur(SalonKatmanTipi.Genel); a.Ad = "Bölge-A";
        var b = SalonKatman.Olustur(SalonKatmanTipi.Genel); b.Ad = "Bölge-B";
        salon.Katmanlar.Add(a); salon.Katmanlar.Add(b);
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Dikdortgen, Kapali = true, KatmanId = a.Id,
            Noktalar = [new(0,0), new(6,0), new(6,10), new(0,10)]
        });
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Dikdortgen, Kapali = true, KatmanId = b.Id,
            Noktalar = [new(4,0), new(10,0), new(10,10), new(4,10)]
        });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        double doseme = yuzeyler
            .Where(s => s.Vertices.All(v => Math.Abs(v.Z) < 1e-9))
            .Sum(PlanAlani);
        if (Math.Abs(doseme - 100) > 0.05)
            throw new InvalidOperationException($"Örtüşen bölgeler toplam 100 m² olmalı; {doseme:F3}.");
        double aAlan = yuzeyler.Where(s => s.Category == "Bölge-A").Sum(PlanAlani);
        double bAlan = yuzeyler.Where(s => s.Category == "Bölge-B").Sum(PlanAlani);
        if (Math.Abs(aAlan - 40) > 0.05 || Math.Abs(bAlan - 60) > 0.05)
            throw new InvalidOperationException("Son çizilen bölge 20 m² örtüşmede öncelikli olmalı.");
    }

    // Regresyon: zemin ve tavan zıt yönlenir; otomatik panel hedefi tavanı seçer.
    public static void ZeminTavanNormaliVeOtomatikHedefDogru()
    {
        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(KabukluSalon(10, 8, 4));
        var zemin = yuzeyler.Single(s => s.Category == "Zemin");
        var tavan = yuzeyler.Single(s => s.Category == "Tavan");
        if (IsaretliPlanAlani(zemin) >= 0 || IsaretliPlanAlani(tavan) <= 0)
            throw new InvalidOperationException("Zemin -Z, tavan +Z yönelimli olmalı.");

        var hedef = Kst.Acoustics.Core.Geometry.PanelHedefDetector.Bul(
            yuzeyler, new AcousticPanelKonfigurasyon()).Tavan;
        if (hedef?.Surface.Category != "Tavan")
            throw new InvalidOperationException("Otomatik tavan hedefi zemini değil tavanı seçmeli.");
    }

    private static double IsaretliPlanAlani(ProjectSurface s)
    {
        double alan = 0;
        for (int i = 0; i < s.Vertices.Count; i++)
        {
            var p = s.Vertices[i]; var q = s.Vertices[(i + 1) % s.Vertices.Count];
            alan += p.X * q.Y - q.X * p.Y;
        }
        return alan / 2;
    }

    // Regresyon: köşedeki açıklık tek duvara atanır; genişliği duvar ucundan
    // taşan açıklık sessizce küçülmek yerine doğrulama uyarısı verir.
    public static void AciklikTekDuvarVeTasmaUyarisi()
    {
        var kose = KabukluSalon(10, 8, 4);
        kose.Acikliklar.Add(new DuvarAcikligi
        {
            Ad = "Köşe Kapısı", GenislikM = 2, YukseklikM = 2,
            MerkezX = 0, MerkezY = 0
        });
        var kapilar = Salon3DGorunumServisi.YuzeyleriOlustur(kose)
            .Where(s => s.Category == "Kapı").ToList();
        if (kapilar.Count != 1)
            throw new InvalidOperationException($"Köşe açıklığı tek duvara bağlanmalı; {kapilar.Count} yüzey bulundu.");

        var tasan = KabukluSalon(10, 8, 4);
        tasan.Acikliklar.Add(new DuvarAcikligi
        {
            Ad = "Taşan Kapı", GenislikM = 2, YukseklikM = 2,
            MerkezX = 0.2, MerkezY = 0
        });
        if (!SalonDogrulamaServisi.Dogrula(tasan).Any(u => u.Contains("duvar ucundan taşıyor")))
            throw new InvalidOperationException("Duvar ucundan taşan açıklık için ölçü kırpma uyarısı beklenir.");
    }

    // Regresyon: M01 açık duvar bbox'ını oda kabul etmez; kısmi taşan sahne 3D'de
    // de hacim hesabıyla aynı kabuk kesişimine kırpılır.
    public static void M01AcikKabuguReddederVeSahneyiKirpar()
    {
        var acik = SalonGeometriServisi.YeniOlustur("Açık");
        acik.SalonYuksekligiM = 3;
        var duvar = acik.Katmanlar.First(k => k.Tip == SalonKatmanTipi.DuvarSeyirci);
        acik.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Polyline, Kapali = false, KatmanId = duvar.Id,
            Noktalar = [new(0,0), new(10,0), new(10,8)]
        });
        bool reddedildi = false;
        try { _ = SalonImporter.Cevir(acik, null); }
        catch (InvalidDataException) { reddedildi = true; }
        if (!reddedildi)
            throw new InvalidOperationException("Kapalı zemin kabuğu olmayan salon M01'e aktarılmamalı.");

        var sahneli = KabukluSalon(10, 10, 4);
        sahneli.Sahne = new SahneGeometri
        {
            GenislikM = 4, DerinlikM = 4, YukseklikM = 1,
            MerkezX = 9, MerkezY = 5
        };
        var sahneUstleri = Salon3DGorunumServisi.YuzeyleriOlustur(sahneli)
            .Where(s => s.Category == "Sahne").ToList();
        double alan = sahneUstleri.Sum(PlanAlani);
        if (Math.Abs(alan - 12) > 0.05
            || sahneUstleri.SelectMany(s => s.Vertices).Any(v => v.X > 10 + 1e-9))
            throw new InvalidOperationException($"Sahne kabuğa 12 m² olarak kırpılmalı; {alan:F3}.");
    }

    // Regresyon: keyhole ayrıştırma sabit 32 delikte durmamalı.
    public static void OtuzUcPencereDuvardanKesilir()
    {
        var salon = SalonGeometriServisi.YeniOlustur("33 açıklık");
        salon.SalonYuksekligiM = 4;
        var duvar = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.DuvarSeyirci);
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Cizgi, KatmanId = duvar.Id,
            Noktalar = [new(0,0), new(100,0)]
        });
        for (int i = 0; i < 33; i++)
            salon.Acikliklar.Add(new DuvarAcikligi
            {
                Ad = $"Pencere {i + 1}", Tip = AciklikTipi.Pencere,
                MerkezX = 2 + i * 3, MerkezY = 0,
                GenislikM = 1, YukseklikM = 1, AltKotM = 1
            });

        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        double pencereAlan = yuzeyler.Where(s => s.Category == "Pencere").Sum(Alan3D);
        double duvarAlan = yuzeyler.Where(s => s.Category == "Duvar-Seyirci").Sum(Alan3D);
        if (Math.Abs(pencereAlan - 33) > 0.05)
            throw new InvalidOperationException($"33 pencere dolgu alanı 33 m² olmalı; {pencereAlan:F3}.");
        if (duvarAlan < 366.2 || duvarAlan > 367.1)
            throw new InvalidOperationException(
                $"33 deliğin tümü duvardan çıkmalı (keyhole dahil ≈366,6 m²); {duvarAlan:F3}.");
    }
}
