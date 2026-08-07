using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Kst.Acoustics.App.Bina.ViewModels;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Tests;

public static class BinaGeometriTests
{
    // BinaGeometri kaydet→aç round-trip: tüm temel alanlar korunmalı.
    public static void E4BinaRoundTrip()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4bina-test-{Guid.NewGuid():N}.e4bina.json");
        try
        {
            // ── Oluştur ──────────────────────────────────────────────────────
            var geo = BinaGeometriServisi.YeniOlustur("Test Binası");
            var kat = new BinaKat
            {
                Id    = "kat-01",
                Ad    = "Zemin Kat",
                KatNo = 1,
                KatYuksekligiM = 3.0,
                DxfFilePath = ""
            };
            var oda = new BinaOda
            {
                Id     = "oda-01",
                Ad     = "Salon",
                AlanM2 = 20.0,
                Poligon = new() { new(0, 0), new(5, 0), new(5, 4), new(0, 4) }
            };
            kat.Odalar.Add(oda);
            kat.Acikliklar.Add(new BinaAciklik
            {
                Id        = "ac-01",
                Bas       = new(1.5, 0),
                Bit       = new(2.5, 0),
                Tip       = BinaAciklikTipi.Kapi,
                GenislikM = 1.0,
                YukseklikM = 2.1
            });
            kat.DxfSegmentler.Add(new BinaDxfSegment { A = new(0, 0), B = new(5, 0) });
            geo.Katlar.Add(kat);

            // ── Kaydet ───────────────────────────────────────────────────────
            BinaGeometriServisi.Kaydet(geo, yol);

            if (!File.Exists(yol))
                throw new InvalidOperationException("Dosya oluşturulmadı.");
            if (geo.GeometriRevizyon != 1)
                throw new InvalidOperationException($"Kaydet sonrası GeometriRevizyon=1 beklenir; {geo.GeometriRevizyon} bulundu.");

            // ── Aç ───────────────────────────────────────────────────────────
            var geo2 = BinaGeometriServisi.Ac(yol);

            if (geo2.BinaAdi != "Test Binası")
                throw new InvalidOperationException($"BinaAdi uyuşmuyor: '{geo2.BinaAdi}'.");
            if (geo2.GeometriRevizyon != 1)
                throw new InvalidOperationException($"GeometriRevizyon 1 beklenir; {geo2.GeometriRevizyon} bulundu.");
            if (geo2.SemaSurum != BinaGeometriServisi.GuncelSemaSurum)
                throw new InvalidOperationException($"SemaSurum {BinaGeometriServisi.GuncelSemaSurum} beklenir; {geo2.SemaSurum} bulundu.");
            if (geo2.Katlar.Count != 1)
                throw new InvalidOperationException($"1 kat beklenir; {geo2.Katlar.Count} bulundu.");

            var kat2 = geo2.Katlar[0];
            if (kat2.Id != "kat-01") throw new InvalidOperationException($"Kat Id '{kat2.Id}' uyuşmuyor.");
            if (kat2.Ad != "Zemin Kat") throw new InvalidOperationException($"Kat Ad '{kat2.Ad}' uyuşmuyor.");
            if (Math.Abs(kat2.KatYuksekligiM - 3.0) > 1e-9)
                throw new InvalidOperationException($"KatYuksekligiM {kat2.KatYuksekligiM} uyuşmuyor.");
            if (kat2.Odalar.Count != 1)
                throw new InvalidOperationException($"1 oda beklenir; {kat2.Odalar.Count} bulundu.");

            var oda2 = kat2.Odalar[0];
            if (oda2.Id != "oda-01") throw new InvalidOperationException($"Oda Id '{oda2.Id}' uyuşmuyor.");
            if (oda2.Ad != "Salon") throw new InvalidOperationException($"Oda Ad '{oda2.Ad}' uyuşmuyor.");
            if (Math.Abs(oda2.AlanM2 - 20.0) > 1e-9)
                throw new InvalidOperationException($"AlanM2 {oda2.AlanM2} uyuşmuyor.");
            if (oda2.Poligon.Count != 4)
                throw new InvalidOperationException($"Poligon köşe sayısı 4 beklenir; {oda2.Poligon.Count} bulundu.");

            if (kat2.Acikliklar.Count != 1)
                throw new InvalidOperationException($"1 açıklık beklenir; {kat2.Acikliklar.Count} bulundu.");
            var ac2 = kat2.Acikliklar[0];
            if (ac2.Id != "ac-01") throw new InvalidOperationException($"Açıklık Id '{ac2.Id}' uyuşmuyor.");
            if (ac2.Tip != BinaAciklikTipi.Kapi) throw new InvalidOperationException("Açıklık Tip uyuşmuyor.");
            if (Math.Abs(ac2.GenislikM - 1.0) > 1e-9)
                throw new InvalidOperationException($"GenislikM {ac2.GenislikM} uyuşmuyor.");

            if (kat2.DxfSegmentler.Count != 1)
                throw new InvalidOperationException($"1 DxfSegment beklenir; {kat2.DxfSegmentler.Count} bulundu.");

            // DosyaYolu yükleme sonrası set edilmeli
            if (geo2.DosyaYolu != yol)
                throw new InvalidOperationException($"DosyaYolu yükleme sonrası '{yol}' olmalı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // İkinci kayıt GeometriRevizyon'u artırmalı.
    public static void IkincKayitRevizYonuArtirir()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4bina-rev-{Guid.NewGuid():N}.e4bina.json");
        try
        {
            var geo = BinaGeometriServisi.YeniOlustur("Rev Test");
            BinaGeometriServisi.Kaydet(geo, yol);  // rev = 1
            BinaGeometriServisi.Kaydet(geo, yol);  // rev = 2
            var geo2 = BinaGeometriServisi.Ac(yol);
            if (geo2.GeometriRevizyon != 2)
                throw new InvalidOperationException($"İkinci kayıt sonrası GeometriRevizyon=2 beklenir; {geo2.GeometriRevizyon} bulundu.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // Tavan patlat: üst kattaki kapsayan oda ile birleşim oluşmalı; geri al kaldırmalı.
    public static void TavanPatlatVeGeriAl()
    {
        var (geo, altKat, altOda, ustKat, ustOda) = IkiKatliOrnek();

        var sonuc = BinaDuseyBirlesimServisi.TavanPatlat(geo, altKat, altOda);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Tavan patlat başarısız: {sonuc.Mesaj}");
        if (geo.DuseyBirlesimleri.Count != 1)
            throw new InvalidOperationException($"1 birleşim beklenir; {geo.DuseyBirlesimleri.Count} bulundu.");

        var b = geo.DuseyBirlesimleri[0];
        if (b.AltKatNo != 2 || b.UstKatNo != 1)
            throw new InvalidOperationException($"Alt/ÜstKatNo (2,1) beklenir; ({b.AltKatNo},{b.UstKatNo}) bulundu.");
        if (!BinaDuseyBirlesimServisi.TavanAcikMi(geo, altOda.Id))
            throw new InvalidOperationException("Alt odanın tavanı açık olmalı.");
        if (!BinaDuseyBirlesimServisi.DosemeAcikMi(geo, ustOda.Id))
            throw new InvalidOperationException("Üst odanın döşemesi açık olmalı.");
        if (!BinaDuseyBirlesimServisi.AyniBirlesimdeAcik(geo, 2, altOda.Id, 1, ustOda.Id))
            throw new InvalidOperationException("İki oda aynı birleşimde görünmeli.");

        // Birleşik hacim: 20 m² × 3 m × 2 kat = 120 m³
        double hacim = BinaDuseyBirlesimServisi.BirlesikHacimM3(geo, b);
        if (Math.Abs(hacim - 120.0) > 1e-6)
            throw new InvalidOperationException($"Birleşik hacim 120 m³ beklenir; {hacim} bulundu.");

        // Geri al
        if (!BinaDuseyBirlesimServisi.GeriAl(geo, altOda.Id, out _))
            throw new InvalidOperationException("Geri al başarısız.");
        if (geo.DuseyBirlesimleri.Count != 0)
            throw new InvalidOperationException("Geri al sonrası birleşim kalmamalı.");
    }

    // Oda silinince ilişkili birleşimler otomatik düşmeli (askıda kayıt yok).
    public static void OdaSilinceBirlesimDusmeli()
    {
        var (geo, altKat, altOda, _, _) = IkiKatliOrnek();
        BinaDuseyBirlesimServisi.TavanPatlat(geo, altKat, altOda);
        if (geo.DuseyBirlesimleri.Count != 1)
            throw new InvalidOperationException("Patlatma sonrası 1 birleşim beklenir.");

        altKat.Odalar.Remove(altOda);
        int n = BinaDuseyBirlesimServisi.OdaSilindi(geo, altOda.Id);
        if (n != 1)
            throw new InvalidOperationException($"1 birleşim kaldırılmalıydı; {n} kaldırıldı.");
        if (geo.DuseyBirlesimleri.Count != 0)
            throw new InvalidOperationException("Oda silindikten sonra birleşim kalmamalı.");
    }

    // Komşu kat yoksa patlatma reddedilmeli.
    public static void KomsuKatYoksaPatlatmaReddedilir()
    {
        var (geo, altKat, altOda, ustKat, ustOda) = IkiKatliOrnek();
        var sonuc = BinaDuseyBirlesimServisi.DosemePatlat(geo, altKat, altOda); // listede alt komşu yok
        if (sonuc.Basarili)
            throw new InvalidOperationException("Alt kat yokken döşeme patlatma reddedilmeliydi.");
        var sonuc2 = BinaDuseyBirlesimServisi.TavanPatlat(geo, ustKat, ustOda); // listede üst komşu yok
        if (sonuc2.Basarili)
            throw new InvalidOperationException("Üst kat yokken tavan patlatma reddedilmeliydi.");
        if (geo.DuseyBirlesimleri.Count != 0)
            throw new InvalidOperationException("Reddedilen patlatma kayıt bırakmamalı.");
    }

    public static void PatlatmaSolPanelSirasinaGoreKomsuKatSecer()
    {
        var geo = BinaGeometriServisi.YeniOlustur("Kat Sırası Testi");
        var poligon = new System.Collections.Generic.List<Point2D>
            { new(0, 0), new(5, 0), new(5, 4), new(0, 4) };

        var kat1Oda = new BinaOda { Id = "oda-kat1", Ad = "Kat 1 Oda", AlanM2 = 20, Poligon = new(poligon) };
        var kat1 = new BinaKat { Id = "kat-1", Ad = "Kat 1", KatNo = 3, KatYuksekligiM = 3 };
        kat1.Odalar.Add(kat1Oda);

        var zeminOda = new BinaOda { Id = "oda-zemin", Ad = "Zemin Oda", AlanM2 = 20, Poligon = new(poligon) };
        var zemin = new BinaKat { Id = "kat-zemin", Ad = "Zemin", KatNo = 4, KatYuksekligiM = 3 };
        zemin.Odalar.Add(zeminOda);

        var bodrumOda = new BinaOda { Id = "oda-bodrum", Ad = "Bodrum Oda", AlanM2 = 20, Poligon = new(poligon) };
        var bodrum = new BinaKat { Id = "kat-bodrum", Ad = "Bodrum", KatNo = 5, KatYuksekligiM = 3 };
        bodrum.Odalar.Add(bodrumOda);

        geo.Katlar.Add(kat1);
        geo.Katlar.Add(zemin);
        geo.Katlar.Add(bodrum);

        var sonuc = BinaDuseyBirlesimServisi.TavanPatlat(geo, zemin, zeminOda);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Zemin tavanı listede üstteki kata patlatılmalıydı: {sonuc.Mesaj}");

        var b = sonuc.Birlesim ?? throw new InvalidOperationException("Birleşim oluşmalı.");
        if (!b.Uyeler.Any(u => u.OdaId == kat1Oda.Id))
            throw new InvalidOperationException("Zemin tavan patlatma Bodrum'a değil, listede üstteki Kat 1'e bağlanmalı.");
        if (b.Uyeler.Any(u => u.OdaId == bodrumOda.Id))
            throw new InvalidOperationException("Zemin tavan patlatma listede alttaki Bodrum'a bağlanmamalı.");
        if (b.AltKatNo != 4 || b.UstKatNo != 3)
            throw new InvalidOperationException($"Alt/ÜstKatNo liste sırasına göre (4,3) olmalı; ({b.AltKatNo},{b.UstKatNo}) bulundu.");
    }

    // Uçtan uca: düzenle → patlat → kaydet → aç. Birleşim + açıklık alanları korunmalı.
    public static void UctanUcaPatlatKaydetAc()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4bina-e2e-{Guid.NewGuid():N}.e4bina.json");
        try
        {
            var (geo, altKat, altOda, _, ustOda) = IkiKatliOrnek();

            // Düzenleme: açıklık ekle (tüm alanlar dolu)
            altKat.Acikliklar.Add(new BinaAciklik
            {
                Id = "ac-e2e",
                Bas = new(2, 0), Bit = new(3, 0),
                Tip = BinaAciklikTipi.Pencere,
                GenislikM = 1.0,
                YukseklikM = 1.5,
                EsikYuksekligiM = 0.85,
                ManuelEklendi = true,
                Kaynak = "manuel"
            });

            // Patlat
            var sonuc = BinaDuseyBirlesimServisi.TavanPatlat(geo, altKat, altOda);
            if (!sonuc.Basarili) throw new InvalidOperationException(sonuc.Mesaj);

            // Kaydet → aç
            BinaGeometriServisi.Kaydet(geo, yol);
            var geo2 = BinaGeometriServisi.Ac(yol);

            // Birleşim korunmalı
            if (geo2.DuseyBirlesimleri.Count != 1)
                throw new InvalidOperationException($"1 birleşim beklenir; {geo2.DuseyBirlesimleri.Count} bulundu.");
            var b2 = geo2.DuseyBirlesimleri[0];
            if (b2.Uyeler.Count != 2)
                throw new InvalidOperationException($"2 üye beklenir; {b2.Uyeler.Count} bulundu.");
            if (!BinaDuseyBirlesimServisi.TavanAcikMi(geo2, altOda.Id))
                throw new InvalidOperationException("Yükleme sonrası alt odanın tavanı açık kalmalı.");
            if (!BinaDuseyBirlesimServisi.DosemeAcikMi(geo2, ustOda.Id))
                throw new InvalidOperationException("Yükleme sonrası üst odanın döşemesi açık kalmalı.");

            // Açıklık alanları korunmalı
            var ac2 = geo2.Katlar.First(k => k.Id == altKat.Id).Acikliklar.FirstOrDefault(a => a.Id == "ac-e2e")
                ?? throw new InvalidOperationException("Açıklık yükleme sonrası bulunamadı.");
            if (ac2.Tip != BinaAciklikTipi.Pencere) throw new InvalidOperationException("Açıklık tipi korunmadı.");
            if (Math.Abs(ac2.YukseklikM - 1.5) > 1e-9) throw new InvalidOperationException("YukseklikM korunmadı.");
            if (Math.Abs(ac2.EsikYuksekligiM - 0.85) > 1e-9) throw new InvalidOperationException("EsikYuksekligiM korunmadı.");
            if (!ac2.ManuelEklendi) throw new InvalidOperationException("ManuelEklendi korunmadı.");
            if (ac2.Kaynak != "manuel") throw new InvalidOperationException("Kaynak korunmadı.");

            // 3D yüzey üretimi: patlatılan tavan/döşeme atlanmalı
            // 2 oda × (2 yatay + 4 duvar) = 12 yüzey; patlatma 2 yatay yüzeyi düşürür → 10
            var yuzeyler = Bina3DGorunumServisi.YuzeyleriOlustur(geo2);
            if (yuzeyler.Count != 10)
                throw new InvalidOperationException($"10 yüzey beklenir (patlatma 2 yüzey düşürür); {yuzeyler.Count} bulundu.");
            if (yuzeyler.Any(s => s.Category == "Tavan" && s.OwnerId == altOda.Id))
                throw new InvalidOperationException("Alt odanın tavanı 3D'de üretilmemeli (patlatıldı).");
            if (yuzeyler.Any(s => s.Category == "Doseme" && s.OwnerId == ustOda.Id))
                throw new InvalidOperationException("Üst odanın döşemesi 3D'de üretilmemeli (patlatıldı).");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    public static void UcBoyutOnizlemeGorselKatSirasiniKullanir()
    {
        var geo = BinaGeometriServisi.YeniOlustur("3D Sıra Testi");

        var ustOda = new BinaOda
        {
            Id = "oda-gorsel-ust",
            Ad = "Görsel Üst",
            AlanM2 = 20,
            Poligon = new() { new(0, 0), new(5, 0), new(5, 4), new(0, 4) }
        };
        var ustKat = new BinaKat
        {
            Id = "kat-gorsel-ust",
            Ad = "Listede Üst",
            KatNo = 1,
            KatYuksekligiM = 3.0
        };
        ustKat.Odalar.Add(ustOda);
        ustKat.DxfSegmentler.Add(new BinaDxfSegment { A = new(0, 0), B = new(5, 0) });
        ustKat.Acikliklar.Add(new BinaAciklik { Bas = new(1, 0), Bit = new(2, 0), Tip = BinaAciklikTipi.Kapi });

        var altOda = new BinaOda
        {
            Id = "oda-gorsel-alt",
            Ad = "Görsel Alt",
            AlanM2 = 20,
            Poligon = new() { new(10, 0), new(15, 0), new(15, 4), new(10, 4) }
        };
        var altKat = new BinaKat
        {
            Id = "kat-gorsel-alt",
            Ad = "Listede Alt",
            KatNo = 2,
            KatYuksekligiM = 3.0
        };
        altKat.Odalar.Add(altOda);
        altKat.DxfSegmentler.Add(new BinaDxfSegment { A = new(10, 0), B = new(15, 0) });
        altKat.Acikliklar.Add(new BinaAciklik { Bas = new(11, 0), Bit = new(12, 0), Tip = BinaAciklikTipi.Kapi });

        geo.Katlar.Add(ustKat);
        geo.Katlar.Add(altKat);

        var yuzeyler = Bina3DGorunumServisi.YuzeyleriOlustur(geo);
        double ustDosemeZ = yuzeyler.Single(s => s.OwnerId == ustOda.Id && s.Category == "Doseme").Vertices[0].Z;
        double altDosemeZ = yuzeyler.Single(s => s.OwnerId == altOda.Id && s.Category == "Doseme").Vertices[0].Z;
        if (Math.Abs(altDosemeZ - 0.0) > 1e-9)
            throw new InvalidOperationException($"Listede alttaki kat 3D tabanında olmalı; Z={altDosemeZ:F2}.");
        if (Math.Abs(ustDosemeZ - 3.0) > 1e-9)
            throw new InvalidOperationException($"Listede üstteki kat 3D'de üst kotta olmalı; Z={ustDosemeZ:F2}.");

        var bg = Bina3DGorunumServisi.ArkaPlanSegmentleri(geo);
        if (Math.Abs(bg[0].A.Z - 0.0) > 1e-9 || Math.Abs(bg[1].A.Z - 3.0) > 1e-9)
            throw new InvalidOperationException("3D arka plan segmentleri görsel kat sırasını izlemeli.");

        var acikliklar = Bina3DGorunumServisi.Acikliklar3D(geo);
        if (Math.Abs(acikliklar[0].Bas.Z - 0.0) > 1e-9 || Math.Abs(acikliklar[1].Bas.Z - 3.0) > 1e-9)
            throw new InvalidOperationException("3D açıklık çizgileri görsel kat sırasını izlemeli.");
    }

    // 3.1 — M12 import: Id kararlılığı + eleman/bayrak koruması + revizyon damgası.
    public static void M12ImportIdKararliligi()
    {
        var (geo, altKat, altOda, _, _) = IkiKatliOrnek();
        geo.GeometriRevizyon = 7;

        var proje = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje();
        var sonuc = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BinaGeometriImportServisi.Uygula(proje, geo, "/tmp/test.e4bina.json");

        if (sonuc.YeniKat != 2 || sonuc.GuncellenenKat != 0)
            throw new InvalidOperationException($"(2 yeni, 0 güncellenen) beklenir; ({sonuc.YeniKat},{sonuc.GuncellenenKat}) bulundu.");
        if (proje.BinaGeometriYolu != "/tmp/test.e4bina.json")
            throw new InvalidOperationException("BinaGeometriYolu yazılmadı.");
        if (proje.BinaGeometriRevizyon != 7)
            throw new InvalidOperationException($"Revizyon damgası 7 beklenir; {proje.BinaGeometriRevizyon} bulundu.");
        if (proje.BinaGeometriKopya is null)
            throw new InvalidOperationException("Gömülü geometri kopyası yazılmadı.");

        // Id kararlılığı: kat ve oda Id'leri geometriden aynen alınmalı
        var katAlt = proje.Katlar.FirstOrDefault(k => k.Id == altKat.Id)
            ?? throw new InvalidOperationException($"{altKat.Id} Id'si korunmadı.");
        if (katAlt.Odalar.FirstOrDefault(o => o.Id == "oda-alt") is null)
            throw new InvalidOperationException("oda-alt Id'si korunmadı.");

        // M12 verisi ekle: eleman + oda bayrağı
        katAlt.Elemanlar.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeEleman
            { OdaId = "oda-alt", X = 1, Y = 1 });
        katAlt.Odalar[0].AsmaTavan = true;
        katAlt.Odalar[0].MahalListesineDahil = false;

        // Geometri değişir (oda büyür + yeni oda) → yeniden import
        altOda.Poligon[2] = new Point2D(6, 5);
        altOda.AlanM2 = 25.0;
        altKat.Odalar.Add(new BinaOda
        {
            Id = "oda-yeni", Ad = "Yeni Oda", AlanM2 = 9.0,
            Poligon = new() { new(6, 0), new(9, 0), new(9, 3), new(6, 3) }
        });
        geo.GeometriRevizyon = 8;

        var sonuc2 = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BinaGeometriImportServisi.Uygula(proje, geo, "/tmp/test.e4bina.json");

        if (sonuc2.YeniKat != 0 || sonuc2.GuncellenenKat != 2)
            throw new InvalidOperationException($"(0 yeni, 2 güncellenen) beklenir; ({sonuc2.YeniKat},{sonuc2.GuncellenenKat}) bulundu.");
        if (proje.BinaGeometriRevizyon != 8)
            throw new InvalidOperationException("Revizyon damgası güncellenmedi.");

        var kat1b = proje.Katlar.First(k => k.Id == altKat.Id);
        // Eleman korunmalı ve OdaId bağı geçerli kalmalı
        if (kat1b.Elemanlar.Count != 1 || kat1b.Elemanlar[0].OdaId != "oda-alt")
            throw new InvalidOperationException("Eleman/OdaId bağı yeniden import sonrası korunmadı.");
        var odaAlt = kat1b.Odalar.First(o => o.Id == "oda-alt");
        // M12 bayrakları korunmalı, geometri alanları güncellenmiş olmalı
        if (!odaAlt.AsmaTavan || odaAlt.MahalListesineDahil)
            throw new InvalidOperationException("Oda M12 bayrakları (AsmaTavan/MahalListesineDahil) korunmadı.");
        if (Math.Abs(odaAlt.AlanM2 - 25.0) > 1e-9)
            throw new InvalidOperationException("Oda alanı geometriden güncellenmedi.");
        if (kat1b.Odalar.All(o => o.Id != "oda-yeni"))
            throw new InvalidOperationException("Yeni oda import edilmedi.");
    }

    public static void M12ImportM50KatSirasiniKorur()
    {
        var geo = BinaGeometriServisi.YeniOlustur("Kat Sırası");
        foreach (var (id, ad, katNo) in new[]
        {
            ("kat-3", "Kat 3", 1),
            ("kat-2", "Kat 2", 2),
            ("kat-1", "Kat 1", 3),
            ("zemin", "Zemin", 4),
            ("bodrum", "Bodrum", 5)
        })
        {
            geo.Katlar.Add(new BinaKat
            {
                Id = id,
                Ad = ad,
                KatNo = katNo,
                KatYuksekligiM = 5.5
            });
        }

        var proje = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje();
        Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BinaGeometriImportServisi.Uygula(proje, geo, "/tmp/sira.e4bina.json");

        var sira = string.Join(",", proje.Katlar.Select(k => k.Id));
        if (sira != "kat-3,kat-2,kat-1,zemin,bodrum")
            throw new InvalidOperationException($"M12 kat sırası M50 görsel sırasını korumalı; bulunan: {sira}");
    }

    // 3.1 — Geometride olmayan eski kat: elemanlıysa korunur, boşsa silinir.
    public static void M12ImportEskiKatKurali()
    {
        var (geo, _, _, _, _) = IkiKatliOrnek();
        var proje = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje();

        // Geometride olmayan iki eski kat: biri elemanlı, biri boş
        var elemanli = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeKat
            { Id = "eski-elemanli", Ad = "Eski Bodrum", KatNo = 0 };
        elemanli.Elemanlar.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeEleman());
        var bos = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeKat
            { Id = "eski-bos", Ad = "Eski Çatı", KatNo = 9 };
        proje.Katlar.Add(elemanli);
        proje.Katlar.Add(bos);

        var sonuc = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BinaGeometriImportServisi.Uygula(proje, geo, null);

        if (proje.Katlar.Any(k => k.Id == "eski-bos"))
            throw new InvalidOperationException("Boş eski kat silinmeliydi.");
        if (proje.Katlar.All(k => k.Id != "eski-elemanli"))
            throw new InvalidOperationException("Elemanlı eski kat korunmalıydı.");
        if (sonuc.KorunanEskiKat != 1 || sonuc.Uyarilar.Count != 1)
            throw new InvalidOperationException("Korunan kat sayısı/uyarı beklenenden farklı.");
    }

    // 3.2 — Eski .e4p12 migrasyonu: gömülü geometri .e4bina'ya dışa aktarılır,
    // Id'ler korunur, M12 verisi (elemanlar) dokunulmadan kalır.
    public static void M12EskiProjeMigrasyonu()
    {
        var dizin = Path.Combine(Path.GetTempPath(), $"e4p12-mig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dizin);
        var e4p12Yolu = Path.Combine(dizin, "EskiProje.e4p12");
        try
        {
            var proje = EskiM12Projesi();
            var eleman = proje.Katlar[0].Elemanlar[0];

            // ── İlk açılış: dışa aktarım ─────────────────────────────────────
            var not = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
                .BinaGeometriExportServisi.EskiProjeyiMigrateEt(proje, e4p12Yolu);
            if (not is null)
                throw new InvalidOperationException("Eski projede migrasyon çalışmalıydı.");

            var beklenenYol = Path.Combine(dizin, "EskiProje.e4bina.json");
            if (!File.Exists(beklenenYol))
                throw new InvalidOperationException(".e4bina dosyası oluşturulmadı.");
            if (proje.BinaGeometriYolu != beklenenYol)
                throw new InvalidOperationException("BinaGeometriYolu yazılmadı.");
            if (proje.BinaGeometriRevizyon is null || proje.BinaGeometriKopya is null)
                throw new InvalidOperationException("Revizyon damgası / gömülü kopya eksik.");

            // Veri kaybı yok: M12 elemanı ve OdaId bağı aynen durmalı
            if (proje.Katlar[0].Elemanlar.Count != 1 || proje.Katlar[0].Elemanlar[0] != eleman)
                throw new InvalidOperationException("M12 elemanları migrasyon sırasında değişti.");

            // Id kararlılığı: dışa aktarılan dosyada kat/oda/açıklık Id'leri korunmalı
            var geo = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi.Ac(beklenenYol);
            var kat = geo.Katlar.FirstOrDefault(k => k.Id == "m12-kat-1")
                ?? throw new InvalidOperationException("Kat Id'si dışa aktarımda korunmadı.");
            if (kat.Odalar.All(o => o.Id != "m12-oda-1"))
                throw new InvalidOperationException("Oda Id'si dışa aktarımda korunmadı.");
            var ac = kat.Acikliklar.FirstOrDefault(a => a.Id == "m12-ac-1")
                ?? throw new InvalidOperationException("Açıklık Id'si dışa aktarımda korunmadı.");
            if (Math.Abs(ac.GenislikM - 1.0) > 1e-9)
                throw new InvalidOperationException($"Açıklık GenislikM 1.0 beklenir; {ac.GenislikM} bulundu.");

            // ── İkinci açılış: dosya var → üzerine yazılmaz, bağlanır ────────
            var oncekiYazim = File.GetLastWriteTimeUtc(beklenenYol);
            var proje2 = EskiM12Projesi();
            var not2 = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
                .BinaGeometriExportServisi.EskiProjeyiMigrateEt(proje2, e4p12Yolu);
            if (not2 is null || !not2.Contains("bağlandı"))
                throw new InvalidOperationException("İkinci açılışta mevcut dosya bağlanmalıydı.");
            if (File.GetLastWriteTimeUtc(beklenenYol) != oncekiYazim)
                throw new InvalidOperationException("Mevcut .e4bina üzerine yazılmamalıydı.");

            // ── Zaten bağlı proje: migrasyon çalışmaz ────────────────────────
            if (Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
                    .BinaGeometriExportServisi.EskiProjeyiMigrateEt(proje, e4p12Yolu) is not null)
                throw new InvalidOperationException("Bağlı projede migrasyon tekrar çalışmamalıydı.");
        }
        finally
        {
            if (Directory.Exists(dizin)) Directory.Delete(dizin, recursive: true);
        }
    }

    // 3.2 — Boş (geometrisiz) proje migrasyon tetiklemez.
    public static void M12BosProjeMigrasyonTetiklemez()
    {
        var proje = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje();
        proje.Katlar.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeKat());
        var not = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BinaGeometriExportServisi.EskiProjeyiMigrateEt(proje, "/tmp/yok.e4p12");
        if (not is not null)
            throw new InvalidOperationException("Boş projede migrasyon tetiklenmemeliydi.");
        if (proje.BinaGeometriKopya is not null)
            throw new InvalidOperationException("Boş projede kopya yazılmamalıydı.");
    }

    // 3.4 — Mutabakat raporu: oda silinmiş senaryoda eleman/devre/mahal askıda çıkar.
    public static void MutabakatOdaSilinmisSenaryo()
    {
        var proje = EskiM12Projesi(); // m12-kat-1 / m12-oda-1: eleman bağlı
        var kat = proje.Katlar[0];

        // İkinci oda + ona bağlı eleman, devre ve mahal hücreleri
        kat.Odalar.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeOda
        {
            Id = "m12-oda-2", Ad = "Mutfak", AlanM2 = 9.0,
            Poligon = new() { new(6, 0), new(9, 0), new(9, 3), new(6, 3) }
        });
        var anahtar = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeEleman
            { Id = "el-anahtar", OdaId = "m12-oda-2", EkranAdi = "Anahtar A1", X = 7, Y = 1 };
        kat.Elemanlar.Add(anahtar);
        kat.Devreler.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcDevre
            { AnahtarElemanId = "el-anahtar" });
        var satir = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcMahalSatiri
            { Tip = Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcElemanTipi.Priz };
        satir.Sayilar["m12-oda-2"] = 3;
        satir.Sayilar["m12-oda-1"] = 2;
        kat.MahalListesi.Add(satir);

        var kayitlar = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BasitProjeGeometriBagimliVeri(proje).BagimliKayitlar().ToList();

        // ── Senaryo A: geometri her iki odayı da içeriyor → askıda yok ───────
        var geoTam = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BinaGeometriExportServisi.GeometriyeCevir(proje);
        var raporTam = Kst.Acoustics.Core.Bina.Services
            .GeometriMutabakatServisi.Karsilastir(geoTam, kayitlar);
        if (raporTam.AskidaVeriVar)
            throw new InvalidOperationException($"Tam geometride askıda veri olmamalı; {raporTam.AskidaKayitlar.Count} bulundu.");

        // ── Senaryo B: m12-oda-2 geometriden silinmiş → eleman+devre+mahal askıda ─
        var geoEksik = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BinaGeometriExportServisi.GeometriyeCevir(proje);
        var silinecek = geoEksik.Katlar[0].Odalar.First(o => o.Id == "m12-oda-2");
        geoEksik.Katlar[0].Odalar.Remove(silinecek);

        var rapor = Kst.Acoustics.Core.Bina.Services
            .GeometriMutabakatServisi.Karsilastir(geoEksik, kayitlar);
        if (!rapor.AskidaVeriVar)
            throw new InvalidOperationException("Oda silinmiş senaryoda askıda veri raporlanmalıydı.");

        int eleman = rapor.AskidaKayitlar.Count(k => k.Tur == "Eleman");
        int devre = rapor.AskidaKayitlar.Count(k => k.Tur == "Devre");
        int mahal = rapor.AskidaKayitlar.Count(k => k.Tur == "Mahal satırı");
        if (eleman != 1)
            throw new InvalidOperationException($"1 askıda eleman beklenir; {eleman} bulundu.");
        if (devre != 1)
            throw new InvalidOperationException($"1 askıda devre beklenir; {devre} bulundu.");
        if (mahal != 1)
            throw new InvalidOperationException($"1 askıda mahal satırı beklenir; {mahal} bulundu.");

        // m12-oda-1'e bağlı kayıtlar (eleman + mahal hücresi) askıda OLMAMALI
        if (rapor.AskidaKayitlar.Any(k => k.OdaId == "m12-oda-1"))
            throw new InvalidOperationException("Mevcut odanın kayıtları askıda görünmemeli.");

        // Özet metni tür sayılarını içermeli
        var ozet = rapor.Ozet();
        if (!ozet.Contains("Eleman") || !ozet.Contains("Devre") || !ozet.Contains("Mahal satırı"))
            throw new InvalidOperationException($"Özet eksik: '{ozet}'");
    }

    // 3.5 — Taşınabilir kanonik proje ve isteğe bağlı gerçek örneklerle M12 regresyonu.
    // Dış örnekler EYE4PRO_M12_REGRESSION_FILES ile verilir ve test öncesi geçici
    // dizine kopyalanır; kaynak dosyalara hiçbir yan etki yapılmaz.
    public static void M12GercekProjeRegresyonu()
    {
        var dizin = Path.Combine(Path.GetTempPath(), $"e4p12-regresyon-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dizin);
        try
        {
            var adaylar = new List<string>();
            var kanonikYol = Path.Combine(dizin, "kanonik.e4p12");
            var kanonikJson = System.Text.Json.JsonSerializer.Serialize(
                EskiM12Projesi(),
                Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels.BasitProjeCizViewModel.ProjeJsonAyarlari);
            File.WriteAllText(kanonikYol, kanonikJson);
            adaylar.Add(kanonikYol);

            var ortamDegeri = Environment.GetEnvironmentVariable("EYE4PRO_M12_REGRESSION_FILES");
            if (!string.IsNullOrWhiteSpace(ortamDegeri))
            {
                var disYollar = ortamDegeri.Split(
                    Path.PathSeparator,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (var i = 0; i < disYollar.Length; i++)
                {
                    var kaynak = Path.GetFullPath(disYollar[i]);
                    if (!File.Exists(kaynak))
                        throw new FileNotFoundException("M12 regresyon örneği bulunamadı.", kaynak);

                    var hedef = Path.Combine(dizin, $"dis-{i + 1}-{Path.GetFileName(kaynak)}");
                    File.Copy(kaynak, hedef);
                    adaylar.Add(hedef);
                }
            }

            foreach (var yol in adaylar)
                GercekProjeRegresyonKontrol(yol);
        }
        finally
        {
            if (Directory.Exists(dizin)) Directory.Delete(dizin, recursive: true);
        }
    }

    private static void GercekProjeRegresyonKontrol(string yol)
    {
        var ad = Path.GetFileName(yol);
        var json = File.ReadAllText(yol);
        var proje = System.Text.Json.JsonSerializer.Deserialize<
                Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje>(
                json, Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels.BasitProjeCizViewModel.ProjeJsonAyarlari)
            ?? throw new InvalidOperationException($"{ad}: deserialize null döndü.");

        // ── Geçiş öncesi anlık görüntü ───────────────────────────────────────
        var once = ProjeOzeti(proje);

        // ── ProjeyiYukle akışındaki migrasyonlar (dosya YAZMADAN, in-memory) ─
        Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BasitProjeCizOtomatikYerlestirServisi.MahalGridAktiflikMigrasyonu(proje);
        Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BinaGeometriExportServisi.EskiProjeyiMigrateEt(proje, null);

        if (once.OdaSayisi > 0 && proje.BinaGeometriKopya is null)
            throw new InvalidOperationException($"{ad}: migrasyon gömülü kopya üretmedi.");

        var sonraMigrasyon = ProjeOzeti(proje);
        OzetKarsilastir(ad + " (migrasyon)", once, sonraMigrasyon);

        // Mutabakat: kendi geometrisine karşı askıda kayıt sayısı (round-trip öncesi)
        var kayitlarOnce = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BasitProjeGeometriBagimliVeri(proje).BagimliKayitlar().ToList();
        int askidaOnce = proje.BinaGeometriKopya is { } g0
            ? Kst.Acoustics.Core.Bina.Services.GeometriMutabakatServisi
                .Karsilastir(g0, kayitlarOnce).AskidaKayitlar.Count
            : 0;

        // ── Export→Import round-trip (M50'den geri yükleme simülasyonu) ─────
        if (proje.BinaGeometriKopya is { } geo)
        {
            Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
                .BinaGeometriImportServisi.Uygula(proje, geo, null);

            var sonra = ProjeOzeti(proje);
            OzetKarsilastir(ad + " (round-trip)", once, sonra);

            // Askıda bağ sayısı artmamalı (geçiş yeni kopukluk yaratmaz)
            var kayitlarSonra = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
                .BasitProjeGeometriBagimliVeri(proje).BagimliKayitlar().ToList();
            int askidaSonra = Kst.Acoustics.Core.Bina.Services.GeometriMutabakatServisi
                .Karsilastir(geo, kayitlarSonra).AskidaKayitlar.Count;
            if (askidaSonra != askidaOnce)
                throw new InvalidOperationException(
                    $"{ad}: askıda kayıt sayısı değişti ({askidaOnce} → {askidaSonra}).");
        }
    }

    private sealed record M12Ozet(
        int KatSayisi, int OdaSayisi, int ElemanSayisi, int DevreSayisi,
        int MahalSatiri, int AciklikSayisi, int PoligonKoseToplami,
        double AlanToplami, int CozulenOdaBagi, int GecisSayisi, int AsmaTavanSayisi,
        string OdaIdImzasi);

    private static M12Ozet ProjeOzeti(Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje proje)
    {
        var katlar = proje.Katlar;
        var odaIdleri = new HashSet<string>(katlar.SelectMany(k => k.Odalar).Select(o => o.Id));
        return new M12Ozet(
            katlar.Count,
            katlar.Sum(k => k.Odalar.Count),
            katlar.Sum(k => k.Elemanlar.Count),
            katlar.Sum(k => k.Devreler.Count),
            katlar.Sum(k => k.MahalListesi.Count),
            katlar.Sum(k => k.Acikliklar.Count),
            katlar.Sum(k => k.Odalar.Sum(o => o.Poligon.Count)),
            katlar.Sum(k => k.Odalar.Sum(o => o.AlanM2)),
            katlar.Sum(k => k.Elemanlar.Count(e => e.OdaId is not null && odaIdleri.Contains(e.OdaId))),
            katlar.Sum(k => k.Odalar.Count(o => o.GecisSecili)),
            katlar.Sum(k => k.Odalar.Count(o => o.AsmaTavan)),
            string.Join("|", katlar.SelectMany(k => k.Odalar).Select(o => o.Id).OrderBy(x => x)));
    }

    private static void OzetKarsilastir(string baglam, M12Ozet once, M12Ozet sonra)
    {
        if (once.KatSayisi != sonra.KatSayisi)
            throw new InvalidOperationException($"{baglam}: kat sayısı {once.KatSayisi} → {sonra.KatSayisi}.");
        if (once.OdaSayisi != sonra.OdaSayisi)
            throw new InvalidOperationException($"{baglam}: oda sayısı {once.OdaSayisi} → {sonra.OdaSayisi}.");
        if (once.ElemanSayisi != sonra.ElemanSayisi)
            throw new InvalidOperationException($"{baglam}: eleman sayısı {once.ElemanSayisi} → {sonra.ElemanSayisi}.");
        if (once.DevreSayisi != sonra.DevreSayisi)
            throw new InvalidOperationException($"{baglam}: devre sayısı {once.DevreSayisi} → {sonra.DevreSayisi}.");
        if (once.MahalSatiri != sonra.MahalSatiri)
            throw new InvalidOperationException($"{baglam}: mahal satırı {once.MahalSatiri} → {sonra.MahalSatiri}.");
        if (once.AciklikSayisi != sonra.AciklikSayisi)
            throw new InvalidOperationException($"{baglam}: açıklık sayısı {once.AciklikSayisi} → {sonra.AciklikSayisi}.");
        if (once.PoligonKoseToplami != sonra.PoligonKoseToplami)
            throw new InvalidOperationException($"{baglam}: poligon köşe toplamı {once.PoligonKoseToplami} → {sonra.PoligonKoseToplami}.");
        if (Math.Abs(once.AlanToplami - sonra.AlanToplami) > 1e-6)
            throw new InvalidOperationException($"{baglam}: alan toplamı {once.AlanToplami:F3} → {sonra.AlanToplami:F3}.");
        if (once.CozulenOdaBagi != sonra.CozulenOdaBagi)
            throw new InvalidOperationException($"{baglam}: çözülen eleman-oda bağı {once.CozulenOdaBagi} → {sonra.CozulenOdaBagi}.");
        if (once.GecisSayisi != sonra.GecisSayisi)
            throw new InvalidOperationException($"{baglam}: Geçiş bayrağı sayısı {once.GecisSayisi} → {sonra.GecisSayisi}.");
        if (once.AsmaTavanSayisi != sonra.AsmaTavanSayisi)
            throw new InvalidOperationException($"{baglam}: AsmaTavan bayrağı sayısı {once.AsmaTavanSayisi} → {sonra.AsmaTavanSayisi}.");
        if (once.OdaIdImzasi != sonra.OdaIdImzasi)
            throw new InvalidOperationException($"{baglam}: oda Id kümesi değişti (Id kararlılığı bozuldu).");
    }

    // 4.1 — M08 adapteri: bölme üretimi eski (doğrudan Mekan + KomsuluklariTespitEt)
    // yol ile birebir aynı olmalı.
    public static void M08AdapterBolmeUretimiBirebir()
    {
        // Ortak duvar paylaşan iki dikdörtgen oda
        var poligonA = new System.Collections.Generic.List<Point2D>
            { new(0, 0), new(5, 0), new(5, 4), new(0, 4) };
        var poligonB = new System.Collections.Generic.List<Point2D>
            { new(5, 0), new(9, 0), new(9, 4), new(5, 4) };

        // ── Eski yol: Mekanlar doğrudan kurulur + KomsuluklariTespitEt ──────
        var mekanA = new Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan
            { Ad = "Salon", Poligon = new(poligonA), Alan = 20, Yukseklik = 3.0 };
        var mekanB = new Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan
            { Ad = "Mutfak", Poligon = new(poligonB), Alan = 16, Yukseklik = 3.0 };
        var eskiMekanlar = new System.Collections.Generic.List<
            Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan> { mekanA, mekanB };
        new Kst.Acoustics.Core.BuildingAcoustics.Parsers.OdaTespitServisi()
            .KomsuluklariTespitEt(eskiMekanlar);

        // ── Yeni yol: BinaGeometri → adapter ────────────────────────────────
        var geo = BinaGeometriServisi.YeniOlustur("Adapter Testi");
        var kat = new BinaKat { Id = Guid.NewGuid().ToString(), Ad = "Zemin", KatNo = 1, KatYuksekligiM = 3.0 };
        kat.Odalar.Add(new BinaOda { Id = Guid.NewGuid().ToString(), Ad = "Salon", AlanM2 = 20, Poligon = new(poligonA) });
        kat.Odalar.Add(new BinaOda { Id = Guid.NewGuid().ToString(), Ad = "Mutfak", AlanM2 = 16, Poligon = new(poligonB) });
        geo.Katlar.Add(kat);

        var proje = new Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje();
        var sonuc = Kst.Acoustics.Core.BuildingAcoustics.Geometry
            .BinaGeometriMekanAdapteri.Uygula(proje, geo);

        if (sonuc.YeniKat != 1 || proje.Katlar.Count != 1)
            throw new InvalidOperationException("Adapter 1 yeni kat üretmeliydi.");
        var yeniMekanlar = proje.Katlar[0].Mekanlar;
        if (yeniMekanlar.Count != 2)
            throw new InvalidOperationException($"2 mekan beklenir; {yeniMekanlar.Count} bulundu.");

        // ── Bölme karşılaştırması: sayı + uç noktalar + komşuluk + dış cephe ─
        foreach (var (eski, ad) in new[] { (mekanA, "Salon"), (mekanB, "Mutfak") })
        {
            var yeniM = yeniMekanlar.First(m => m.Ad == ad);
            if (eski.Bolmeler.Count != yeniM.Bolmeler.Count)
                throw new InvalidOperationException(
                    $"{ad}: bölme sayısı eski={eski.Bolmeler.Count}, yeni={yeniM.Bolmeler.Count}.");

            var eskiImza = BolmeImzalari(eski);
            var yeniImza = BolmeImzalari(yeniM);
            for (int i = 0; i < eskiImza.Count; i++)
            {
                if (eskiImza[i] != yeniImza[i])
                    throw new InvalidOperationException(
                        $"{ad}: bölme {i} farklı — eski '{eskiImza[i]}', yeni '{yeniImza[i]}'.");
            }
        }

        // Yükseklik kat yüksekliğinden gelmeli
        if (System.Math.Abs(yeniMekanlar[0].Yukseklik - 3.0) > 1e-9)
            throw new InvalidOperationException("Mekan yüksekliği kat yüksekliğinden alınmalı.");
    }

    public static void M08AdapterM50KatSirasiniKorur()
    {
        var geo = BinaGeometriServisi.YeniOlustur("M08 Sıra Testi");
        foreach (var (ad, katNo) in new[] { ("Kat 3", 1), ("Kat 2", 2), ("Kat 1", 3), ("Zemin", 4), ("Bodrum", 5) })
        {
            geo.Katlar.Add(new BinaKat
            {
                Id = Guid.NewGuid().ToString(),
                Ad = ad,
                KatNo = katNo,
                KatYuksekligiM = 5.5
            });
        }

        var proje = new Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje();
        Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriMekanAdapteri.Uygula(proje, geo);

        var sira = string.Join(",", proje.Katlar.Select(k => k.Ad));
        if (sira != "Kat 3,Kat 2,Kat 1,Zemin,Bodrum")
            throw new InvalidOperationException($"M08 import M50 kat sırasını korumalıydı; alınan sıra: {sira}");

        var tekrarGeo = Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriM08ExportServisi.GeometriyeCevir(proje);
        var tekrarSira = string.Join(",", tekrarGeo.Katlar.Select(k => k.Ad));
        if (tekrarSira != sira)
            throw new InvalidOperationException($"M08 export mevcut kat sırasını korumalıydı; alınan sıra: {tekrarSira}");
    }

    private static System.Collections.Generic.List<string> BolmeImzalari(
        Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan m)
    {
        return m.Bolmeler
            .Select(b => $"{b.Baslangic.X:F3},{b.Baslangic.Y:F3}→{b.Bitis.X:F3},{b.Bitis.Y:F3}" +
                         $"|dis={b.IsDisCephe}|komsu={b.AliciMekan?.Ad ?? "-"}|alan={b.Alan:F3}")
            .OrderBy(s => s)
            .ToList();
    }

    // 4.1 — Yeniden uygulama: akustik alanlar ve atamalar korunur (Id kararlılığı).
    public static void M08AdapterAkustikAlanKorur()
    {
        var geo = BinaGeometriServisi.YeniOlustur("Koruma Testi");
        var kat = new BinaKat { Id = Guid.NewGuid().ToString(), Ad = "Zemin", KatNo = 1, KatYuksekligiM = 3.0 };
        var odaId = Guid.NewGuid().ToString();
        kat.Odalar.Add(new BinaOda
        {
            Id = odaId, Ad = "Yatak Odası", AlanM2 = 20,
            Poligon = new() { new(0, 0), new(5, 0), new(5, 4), new(0, 4) }
        });
        kat.Odalar.Add(new BinaOda
        {
            Id = Guid.NewGuid().ToString(), Ad = "Salon", AlanM2 = 16,
            Poligon = new() { new(5, 0), new(9, 0), new(9, 4), new(5, 4) }
        });
        geo.Katlar.Add(kat);

        var proje = new Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje();
        Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriMekanAdapteri.Uygula(proje, geo);

        var mekan = proje.Katlar[0].Mekanlar.First(m => m.Ad == "Yatak Odası");
        if (mekan.Sinif != Kst.Acoustics.Core.BuildingAcoustics.Models.MekanSinifi.Alici)
            throw new InvalidOperationException("'Yatak Odası' kullanım tipinden Alıcı sınıfı türemeliydi.");

        // Akustik atamalar yap
        var zemin = new Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemani { Ad = "Döşeme-1" };
        var duvarEl = new Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemani { Ad = "Duvar-1" };
        mekan.ZeminYapiElemani = zemin;
        mekan.KaynakDerecesi = "YG";
        mekan.AliciSinifi = "I";
        mekan.RT60 = 0.8;
        var ortakBolme = mekan.Bolmeler.First(b => !b.IsDisCephe);
        ortakBolme.YapiElemani = duvarEl;

        // Geometri değişir: oda büyür + ad korunur → yeniden uygula
        kat.Odalar[0].Poligon[2] = new Point2D(5, 5);
        kat.Odalar[0].Poligon[3] = new Point2D(0, 5);
        kat.Odalar[0].AlanM2 = 25;

        Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriMekanAdapteri.Uygula(proje, geo);

        var mekan2 = proje.Katlar[0].Mekanlar.First(m => m.Ad == "Yatak Odası");
        if (!ReferenceEquals(mekan, mekan2))
            throw new InvalidOperationException("Mekan nesnesi Id eşleşmesiyle korunmalıydı.");
        if (mekan2.Id != Kst.Acoustics.Core.BuildingAcoustics.Geometry
                .BinaGeometriMekanAdapteri.OdaIdToGuid(odaId))
            throw new InvalidOperationException("Mekan Id'si kararlı değil.");
        if (System.Math.Abs(mekan2.Alan - 25) > 1e-9)
            throw new InvalidOperationException("Alan geometriden güncellenmedi.");
        if (mekan2.ZeminYapiElemani?.Ad != "Döşeme-1")
            throw new InvalidOperationException("Zemin ataması korunmadı.");
        if (mekan2.KaynakDerecesi != "YG" || mekan2.AliciSinifi != "I" || System.Math.Abs(mekan2.RT60 - 0.8) > 1e-9)
            throw new InvalidOperationException("Akustik alanlar (derece/sınıf/RT60) korunmadı.");
        // Ortak duvar bölmesi yeniden üretildi; YapiElemani geometrik aktarımla korunmalı
        var ortak2 = mekan2.Bolmeler.FirstOrDefault(b => !b.IsDisCephe)
            ?? throw new InvalidOperationException("Yeniden üretimde ortak bölme kayboldu.");
        if (ortak2.YapiElemani?.Ad != "Duvar-1")
            throw new InvalidOperationException("Bölme malzeme ataması KomsuluklariTespitEt aktarımıyla korunmalıydı.");
    }

    // 4.2 — Açıklık malzeme ataması: boyutlar M50'den okunur; AciklikId → YapiElemani
    // eşlemesi geometri yeniden uygulandığında korunur; elle eklenen açıklığa dokunulmaz.
    public static void M08AciklikMalzemeEslemesi()
    {
        var geo = BinaGeometriServisi.YeniOlustur("Açıklık Testi");
        var kat = new BinaKat { Id = Guid.NewGuid().ToString(), Ad = "Zemin", KatNo = 1, KatYuksekligiM = 3.0 };
        kat.Odalar.Add(new BinaOda
        {
            Id = Guid.NewGuid().ToString(), Ad = "Salon", AlanM2 = 20,
            Poligon = new() { new(0, 0), new(5, 0), new(5, 4), new(0, 4) }
        });
        kat.Acikliklar.Add(new BinaAciklik
        {
            Id = "ac-kapi",
            Bas = new(1.5, 0), Bit = new(2.5, 0),
            Tip = BinaAciklikTipi.Kapi,
            GenislikM = 1.0, YukseklikM = 2.1, EsikYuksekligiM = 0.0
        });
        geo.Katlar.Add(kat);

        var proje = new Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje();
        Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriMekanAdapteri.Uygula(proje, geo);

        var mekan = proje.Katlar[0].Mekanlar[0];
        var bolmeAcikligi = mekan.Bolmeler.SelectMany(b => b.Acikliklar).FirstOrDefault()
            ?? throw new InvalidOperationException("Açıklık bölmeye yerleştirilmedi.");

        // Boyutlar M50'den gelmeli + geometriden-gelir işareti
        if (bolmeAcikligi.AciklikId != "ac-kapi")
            throw new InvalidOperationException("AciklikId M50 kimliğini taşımalı.");
        if (!bolmeAcikligi.GeometridenGelir)
            throw new InvalidOperationException("GeometridenGelir true olmalı.");
        if (Math.Abs(bolmeAcikligi.Genislik - 1.0) > 1e-9 || Math.Abs(bolmeAcikligi.Yukseklik - 2.1) > 1e-9)
            throw new InvalidOperationException("Açıklık boyutları M50'den okunmadı.");
        if (bolmeAcikligi.Tip != Kst.Acoustics.Core.BuildingAcoustics.Models.BolmeAcikligiTipi.Kapi)
            throw new InvalidOperationException("Açıklık tipi M50'den okunmadı.");

        // M08 malzeme atar + elle açıklık ekler
        var kapiMalzeme = new Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemani { Ad = "Ahşap Kapı 40dB" };
        bolmeAcikligi.YapiElemani = kapiMalzeme;
        var elleBolme = mekan.Bolmeler.First(b => b.HasGeometry);
        elleBolme.Acikliklar.Add(new Kst.Acoustics.Core.BuildingAcoustics.Models.BolmeAcikligi
        {
            Tip = Kst.Acoustics.Core.BuildingAcoustics.Models.BolmeAcikligiTipi.Pencere,
            Genislik = 0.8, Yukseklik = 1.0,
            // Gerçek akışta HesaplaAciklikGeometry duvar üzerine konumlar —
            // DurumuAktar merkez-yakınlık aktarımı için konum şart.
            Baslangic = elleBolme.Baslangic,
            Bitis = elleBolme.Bitis
        });

        // Geometri değişir (açıklık genişler) → yeniden uygula
        kat.Acikliklar[0].Bit = new Point2D(2.7, 0);
        kat.Acikliklar[0].GenislikM = 1.2;
        Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriMekanAdapteri.Uygula(proje, geo);

        var mekan2 = proje.Katlar[0].Mekanlar[0];
        var tumAciklik = mekan2.Bolmeler.SelectMany(b => b.Acikliklar).ToList();

        var geoAciklik = tumAciklik.FirstOrDefault(a => a.AciklikId == "ac-kapi")
            ?? throw new InvalidOperationException("Yeniden uygulamada M50 açıklığı kayboldu.");
        if (Math.Abs(geoAciklik.Genislik - 1.2) > 1e-9)
            throw new InvalidOperationException("Açıklık genişliği M50'den güncellenmedi.");
        if (geoAciklik.YapiElemani?.Ad != "Ahşap Kapı 40dB")
            throw new InvalidOperationException("AciklikId → YapiElemani eşlemesi korunmadı.");

        var elleAciklik = tumAciklik.FirstOrDefault(a => !a.GeometridenGelir)
            ?? throw new InvalidOperationException("Elle eklenen açıklık korunmalıydı.");
        if (Math.Abs(elleAciklik.Genislik - 0.8) > 1e-9)
            throw new InvalidOperationException("Elle eklenen açıklığın boyutu değişmemeliydi.");

        // Çift kayıt olmamalı: 1 geometriden + 1 elle = 2
        if (tumAciklik.Count != 2)
            throw new InvalidOperationException($"2 açıklık beklenir (1 M50 + 1 elle); {tumAciklik.Count} bulundu.");

        // İptal edilen açıklık M08'e taşınmamalı
        kat.Acikliklar[0].IptalEdildi = true;
        Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriMekanAdapteri.Uygula(proje, geo);
        var kalan = proje.Katlar[0].Mekanlar[0].Bolmeler.SelectMany(b => b.Acikliklar).ToList();
        if (kalan.Any(a => a.AciklikId == "ac-kapi"))
            throw new InvalidOperationException("İptal edilen açıklık bölmelerden düşmeliydi.");
        if (kalan.Count != 1 || kalan[0].GeometridenGelir)
            throw new InvalidOperationException("Yalnız elle eklenen açıklık kalmalıydı.");
    }

    // 4.3 — Eski M08 projesi migrasyonu: gömülü geometri .e4bina'ya aktarılır,
    // Id'ler kararlı, mekan atamaları kayıpsız; round-trip sonrası veriler aynı.
    public static void M08EskiProjeMigrasyonu()
    {
        var dizin = Path.Combine(Path.GetTempPath(), $"e4p08-mig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dizin);
        var e4p08Yolu = Path.Combine(dizin, "AkustikProje.e4p08");
        try
        {
            // ── Eski M08 projesi: 2 mekan + atamalar + bölme açıklığı ────────
            var proje = new Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje { ProjeAdi = "Eski M08" };
            var kat = new Kst.Acoustics.Core.BuildingAcoustics.Models.Kat
                { KatNo = 1, Ad = "Zemin", KatYuksekligi = 2.9 };
            var mekanA = new Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan
            {
                Ad = "Yatak Odası", KullanimTipi = "Yatak Odası", Alan = 20, Yukseklik = 2.9,
                Poligon = new() { new(0, 0), new(5, 0), new(5, 4), new(0, 4) },
                KaynakDerecesi = "DG", AliciSinifi = "I", RT60 = 0.6
            };
            var mekanB = new Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan
            {
                Ad = "Salon", KullanimTipi = "Salon", Alan = 16, Yukseklik = 2.9,
                Poligon = new() { new(5, 0), new(9, 0), new(9, 4), new(5, 4) }
            };
            kat.Mekanlar.Add(mekanA);
            kat.Mekanlar.Add(mekanB);
            proje.Katlar.Add(kat);
            new Kst.Acoustics.Core.BuildingAcoustics.Parsers.OdaTespitServisi()
                .KomsuluklariTespitEt(kat.Mekanlar);

            // Atamalar + elle açıklık (eski projede AciklikId yok)
            var zemin = new Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemani { Ad = "Döşeme-X" };
            mekanA.ZeminYapiElemani = zemin;
            var ortakBolme = mekanA.Bolmeler.First(b => !b.IsDisCephe);
            var kapiMalzeme = new Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemani { Ad = "Kapı-X" };
            ortakBolme.Acikliklar.Add(new Kst.Acoustics.Core.BuildingAcoustics.Models.BolmeAcikligi
            {
                Tip = Kst.Acoustics.Core.BuildingAcoustics.Models.BolmeAcikligiTipi.Kapi,
                Genislik = 0.9, Yukseklik = 2.1,
                Baslangic = new(5, 1.5), Bitis = new(5, 2.4),
                YapiElemani = kapiMalzeme
            });

            // ── Migrasyon ────────────────────────────────────────────────────
            var not = Kst.Acoustics.Core.BuildingAcoustics.Geometry
                .BinaGeometriM08ExportServisi.EskiProjeyiMigrateEt(proje, e4p08Yolu);
            if (not is null)
                throw new InvalidOperationException("Eski M08 projesinde migrasyon çalışmalıydı.");

            var beklenenYol = Path.Combine(dizin, "AkustikProje.e4bina.json");
            if (!File.Exists(beklenenYol))
                throw new InvalidOperationException(".e4bina dosyası oluşturulmadı.");
            if (proje.BinaGeometriYolu != beklenenYol || proje.BinaGeometriRevizyon is null || proje.BinaGeometriKopya is null)
                throw new InvalidOperationException("Bağlantı alanları (yol/revizyon/kopya) yazılmadı.");

            // Dışa aktarılan geometri: Id kararlılığı + içerik
            var geo = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi.Ac(beklenenYol);
            var geoKat = geo.Katlar.Single();
            if (geoKat.Id != "m08-kat-1" || geoKat.KatNo != 1)
                throw new InvalidOperationException("Kat Id/No dışa aktarımda beklendiği gibi değil.");
            if (geoKat.Odalar.All(o => o.Id != mekanA.Id.ToString()))
                throw new InvalidOperationException("Mekan Guid'i BinaOda.Id olarak korunmadı.");
            // Ortak kapı tek kayıt olmalı (dedupe) ve elle eklendiği için ManuelEklendi
            if (geoKat.Acikliklar.Count != 1 || !geoKat.Acikliklar[0].ManuelEklendi)
                throw new InvalidOperationException($"1 manuel açıklık beklenir; {geoKat.Acikliklar.Count} bulundu.");

            // ── Round-trip: geometriyi geri uygula → veri kaybı yok ──────────
            Kst.Acoustics.Core.BuildingAcoustics.Geometry
                .BinaGeometriMekanAdapteri.Uygula(proje, geo, beklenenYol);

            var mekanA2 = proje.Katlar[0].Mekanlar.First(m => m.Ad == "Yatak Odası");
            if (!ReferenceEquals(mekanA, mekanA2))
                throw new InvalidOperationException("Round-trip mekan nesnesini korumalıydı (Id kararlılığı).");
            if (mekanA2.ZeminYapiElemani?.Ad != "Döşeme-X")
                throw new InvalidOperationException("Zemin ataması round-trip'te kayboldu.");
            if (mekanA2.KaynakDerecesi != "DG" || mekanA2.AliciSinifi != "I")
                throw new InvalidOperationException("Akustik sınıflar round-trip'te kayboldu.");
            // Elle açıklık geometriye terfi etti: artık AciklikId'li ve malzemesi korunmuş
            var aciklik2 = proje.Katlar[0].Mekanlar
                .SelectMany(m => m.Bolmeler).SelectMany(b => b.Acikliklar)
                .FirstOrDefault(a => a.GeometridenGelir)
                ?? throw new InvalidOperationException("Açıklık round-trip'te geometriden gelmeli.");
            if (Math.Abs(aciklik2.Genislik - 0.9) > 1e-9 || Math.Abs(aciklik2.Yukseklik - 2.1) > 1e-9)
                throw new InvalidOperationException("Açıklık boyutları round-trip'te değişti.");

            // ── Aynı adlı fakat başka projeye ait yan dosya: sessizce bağlanmaz ──
            var proje2 = new Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje { ProjeAdi = "Eski M08" };
            proje2.Katlar.Add(new Kst.Acoustics.Core.BuildingAcoustics.Models.Kat { KatNo = 1, KatYuksekligi = 2.9 });
            proje2.Katlar[0].Mekanlar.Add(new Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan
                { Ad = "X", Poligon = new() { new(0, 0), new(1, 0), new(1, 1) } });
            var oncekiYazim = File.GetLastWriteTimeUtc(beklenenYol);
            var not2 = Kst.Acoustics.Core.BuildingAcoustics.Geometry
                .BinaGeometriM08ExportServisi.EskiProjeyiMigrateEt(proje2, e4p08Yolu);
            if (not2 is null || !not2.Contains("güvenli yeni dosya"))
                throw new InvalidOperationException("Kimliği uyuşmayan yan dosya yerine güvenli yeni dosya oluşturulmalıydı.");
            if (File.GetLastWriteTimeUtc(beklenenYol) != oncekiYazim)
                throw new InvalidOperationException("Mevcut .e4bina üzerine yazılmamalıydı.");
            if (proje2.BinaGeometriYolu == beklenenYol ||
                string.IsNullOrWhiteSpace(proje2.BinaGeometriYolu) ||
                !File.Exists(proje2.BinaGeometriYolu))
                throw new InvalidOperationException("Uyuşmayan proje benzersiz bir .e4bina dosyasına bağlanmalıydı.");
            var proje2Geo = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi.Ac(proje2.BinaGeometriYolu);
            if (proje2.BinaGeometriKopya?.Katlar.SelectMany(k => k.Odalar).Select(o => o.Id)
                    .SequenceEqual(proje2Geo.Katlar.SelectMany(k => k.Odalar).Select(o => o.Id)) != true)
                throw new InvalidOperationException("Bağlı yan dosya ile gömülü geometri aynı içeriği taşımalıydı.");

            // Bağlı projede migrasyon tekrar çalışmaz
            if (Kst.Acoustics.Core.BuildingAcoustics.Geometry
                    .BinaGeometriM08ExportServisi.EskiProjeyiMigrateEt(proje, e4p08Yolu) is not null)
                throw new InvalidOperationException("Bağlı projede migrasyon tekrar çalışmamalıydı.");
        }
        finally
        {
            if (Directory.Exists(dizin)) Directory.Delete(dizin, recursive: true);
        }
    }

    // 4.4 — Patlatma M50 modeline bağlanır: M08 birleşimi export'ta geometriye
    // taşınır, import'ta M08 modeline geri indirgenir (Id ve yüzey bayrakları korunur).
    public static void M08PatlatmaM50ModelineBaglanir()
    {
        var proje = new Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje { ProjeAdi = "Patlatma Senkron" };
        var alt = new Kst.Acoustics.Core.BuildingAcoustics.Models.Kat { KatNo = 1, Ad = "Zemin", KatYuksekligi = 3.0 };
        var ust = new Kst.Acoustics.Core.BuildingAcoustics.Models.Kat { KatNo = 2, Ad = "Kat 1", KatYuksekligi = 3.0 };
        var poligon = new System.Collections.Generic.List<Point2D>
            { new(0, 0), new(5, 0), new(5, 4), new(0, 4) };
        var altMekan = new Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan
            { Ad = "Alt Salon", Alan = 20, Yukseklik = 3.0, Poligon = new(poligon) };
        var ustMekan = new Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan
            { Ad = "Üst Salon", Alan = 20, Yukseklik = 3.0, Poligon = new(poligon) };
        alt.Mekanlar.Add(altMekan);
        ust.Mekanlar.Add(ustMekan);
        proje.Katlar.Add(alt);
        proje.Katlar.Add(ust);

        // M08 patlatma kaydı (yıkıcı olmayan model)
        proje.DuseyMekanBirlesimleri.Add(new Kst.Acoustics.Core.BuildingAcoustics.Models.DuseyMekanBirlesimi
        {
            AktifMi = true, AltKatNo = 1, UstKatNo = 2,
            Uyeler =
            {
                new() { KatNo = 1, MekanId = altMekan.Id, TavanAcik = true,  DosemeAcik = false },
                new() { KatNo = 2, MekanId = ustMekan.Id, TavanAcik = false, DosemeAcik = true  }
            }
        });

        // Export: M08 → M50
        var geo = Kst.Acoustics.Core.BuildingAcoustics.Geometry
            .BinaGeometriM08ExportServisi.GeometriyeCevir(proje);
        if (geo.DuseyBirlesimleri.Count != 1)
            throw new InvalidOperationException("Patlatma export'ta M50 modeline taşınmadı.");
        var b50 = geo.DuseyBirlesimleri[0];
        if (!BinaDuseyBirlesimServisi.TavanAcikMi(geo, altMekan.Id.ToString()))
            throw new InvalidOperationException("Alt mekanın tavan-açık bayrağı M50'ye taşınmadı.");

        // Import: M50 → M08 (boş projeye round-trip)
        var proje2 = new Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje();
        Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriMekanAdapteri.Uygula(proje2, geo);
        if (proje2.DuseyMekanBirlesimleri.Count != 1)
            throw new InvalidOperationException("Patlatma import'ta M08 modeline indirgenmedi.");
        var b08 = proje2.DuseyMekanBirlesimleri[0];
        if (b08.AltKatNo != 1 || b08.UstKatNo != 2 || b08.Uyeler.Count != 2)
            throw new InvalidOperationException("Birleşim alanları round-trip'te bozuldu.");
        var altUye = b08.Uyeler.First(u => u.KatNo == 1);
        if (!altUye.TavanAcik || altUye.DosemeAcik)
            throw new InvalidOperationException("Üye yüzey bayrakları round-trip'te bozuldu.");
        if (altUye.MekanId != altMekan.Id)
            throw new InvalidOperationException("Üye MekanId bağı round-trip'te korunmadı.");

        // Yeniden uygulama çift kayıt yaratmamalı (Id merge)
        Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriMekanAdapteri.Uygula(proje2, geo);
        if (proje2.DuseyMekanBirlesimleri.Count != 1)
            throw new InvalidOperationException("Yeniden uygulama birleşimi çoğaltmamalı.");
    }

    // 4.5 — Golden regresyon: DnT,w ve spektral dış cephe hesapları M50
    // round-trip'inden (export → import) birebir etkilenmemeli.
    public static void M08GoldenHesapRoundTripDegismez()
    {
        var M = Kst.Acoustics.Core.BuildingAcoustics.Models.MekanSinifi.Tanimsiz; _ = M;

        // ── Proje: iki komşu mekan, ortak duvar + dış cephe ──────────────────
        var proje = new Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje
        {
            ProjeAdi = "Golden RT",
            DisGurultuDb = 75,
            DisGurultuCepheMaruziyeti = Kst.Acoustics.Core.BuildingAcoustics.Models.CepheMaruziyet.TrafikCephesi
        };
        var kat = new Kst.Acoustics.Core.BuildingAcoustics.Models.Kat { KatNo = 1, Ad = "Zemin", KatYuksekligi = 2.5 };
        var kaynak = new Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan
        {
            Ad = "Salon", KullanimTipi = "Salon", Alan = 20, Yukseklik = 2.5,
            Poligon = new() { new(0, 0), new(5, 0), new(5, 4), new(0, 4) }
        };
        var alici = new Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan
        {
            Ad = "Yatak Odası", KullanimTipi = "Yatak Odası", Alan = 20, Yukseklik = 2.5,
            Poligon = new() { new(5, 0), new(10, 0), new(10, 4), new(5, 4) }
        };
        kat.Mekanlar.Add(kaynak);
        kat.Mekanlar.Add(alici);
        proje.Katlar.Add(kat);
        new Kst.Acoustics.Core.BuildingAcoustics.Parsers.OdaTespitServisi()
            .KomsuluklariTespitEt(kat.Mekanlar);

        // Atamalar
        var ayiriciDuvar = new Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemani
        {
            Ad = "Ayırıcı Duvar", Tip = Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemaniTipi.Duvar,
            Rw = 52, RThirdOctave = Enumerable.Repeat(52.0, 16).ToList()
        };
        var cepheDuvari = new Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemani
        {
            Ad = "Cephe Duvarı", Tip = Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemaniTipi.Duvar,
            Rw = 48, Ctr = -3, RThirdOctave = Enumerable.Repeat(48.0, 16).ToList()
        };
        var pencere = new Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemani
        {
            Ad = "Pencere", Tip = Kst.Acoustics.Core.BuildingAcoustics.Models.YapiElemaniTipi.Pencere,
            Rw = 34, Ctr = -4, RThirdOctave = Enumerable.Repeat(34.0, 16).ToList()
        };

        var ortak = alici.Bolmeler.First(b => !b.IsDisCephe);
        ortak.YapiElemani = ayiriciDuvar;

        // Resmî cephe hesabı oda kabuğunun tamamını değerlendirir. Tek bir atanmamış
        // cephe parçası sonsuz yalıtımlı varsayılamayacağı için tüm dış yüzleri ata.
        foreach (var disCephe in alici.Bolmeler.Where(b => b.IsDisCephe))
            disCephe.YapiElemani = cepheDuvari;

        // Alıcının dış (y=0) kenarı: cephe duvarı + elle pencere açıklığı
        var cephe = alici.Bolmeler.First(b =>
            b.IsDisCephe && Math.Abs(b.Baslangic.Y) < 1e-6 && Math.Abs(b.Bitis.Y) < 1e-6);
        cephe.Acikliklar.Add(new Kst.Acoustics.Core.BuildingAcoustics.Models.BolmeAcikligi
        {
            Tip = Kst.Acoustics.Core.BuildingAcoustics.Models.BolmeAcikligiTipi.Pencere,
            YapiElemani = pencere,
            Genislik = 1.5, Yukseklik = 1.4,
            Baslangic = new(6.5, 0), Bitis = new(8.0, 0)
        });

        // ── Hesap (ÖNCE) ─────────────────────────────────────────────────────
        var (dntwOnce, icToplamOnce, d2mOnce) = GoldenHesapla(proje, alici);

        // ── M50 round-trip ───────────────────────────────────────────────────
        var geo = Kst.Acoustics.Core.BuildingAcoustics.Geometry
            .BinaGeometriM08ExportServisi.GeometriyeCevir(proje);
        Kst.Acoustics.Core.BuildingAcoustics.Geometry
            .BinaGeometriMekanAdapteri.Uygula(proje, geo);

        // ── Hesap (SONRA) ────────────────────────────────────────────────────
        var alici2 = proje.Katlar[0].Mekanlar.First(m => m.Ad == "Yatak Odası");
        var (dntwSonra, icToplamSonra, d2mSonra) = GoldenHesapla(proje, alici2);

        if (Math.Abs(dntwOnce - dntwSonra) > 1e-6)
            throw new InvalidOperationException($"DnT,w round-trip'te değişti: {dntwOnce:F4} → {dntwSonra:F4}.");
        if (Math.Abs(icToplamOnce - icToplamSonra) > 1e-6)
            throw new InvalidOperationException($"Cephe iç gürültü round-trip'te değişti: {icToplamOnce:F4} → {icToplamSonra:F4}.");
        if (Math.Abs(d2mOnce - d2mSonra) > 1e-6)
            throw new InvalidOperationException($"D2m,nT,50 round-trip'te değişti: {d2mOnce:F4} → {d2mSonra:F4}.");

        // Çift açıklık olmamalı (elle pencere geometriye terfi etti, kopya yok)
        var cephe2 = alici2.Bolmeler.First(b =>
            b.IsDisCephe && Math.Abs(b.Baslangic.Y) < 1e-6 && Math.Abs(b.Bitis.Y) < 1e-6);
        if (cephe2.Acikliklar.Count != 1)
            throw new InvalidOperationException($"1 cephe açıklığı beklenir; {cephe2.Acikliklar.Count} bulundu (çift kayıt?).");
        if (!cephe2.Acikliklar[0].GeometridenGelir)
            throw new InvalidOperationException("Elle pencere round-trip'te geometriye terfi etmeliydi.");
        if (cephe2.Acikliklar[0].YapiElemani?.Ad != "Pencere")
            throw new InvalidOperationException("Pencere malzemesi terfi sırasında devralınmalıydı.");
    }

    private static (double Dntw, double IcToplam, double D2mNt50) GoldenHesapla(
        Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje proje,
        Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan alici)
    {
        var ortak = alici.Bolmeler.First(b => !b.IsDisCephe);
        ortak.ApparentRw = 0; // her çağrıda YapiElemani'den yeniden hesaplansın
        double dntw = new Kst.Acoustics.Core.BuildingAcoustics.Calculation.HavaSesiHesapServisi()
            .HesaplaDntw(ortak);
        if (dntw <= 0) throw new InvalidOperationException("DnT,w hesaplanamadı.");

        var cephe = alici.Bolmeler.First(b =>
            b.IsDisCephe && Math.Abs(b.Baslangic.Y) < 1e-6 && Math.Abs(b.Bitis.Y) < 1e-6);
        var disServis = new Kst.Acoustics.Core.BuildingAcoustics.Calculation.DisGurultuServisi();
        disServis.UygulaDisCepheYalitimi(alici,
            new System.Collections.Generic.List<Kst.Acoustics.Core.BuildingAcoustics.Models.Bolme> { cephe },
            proje.DisGurultuCepheMaruziyeti);
        var sonuc = disServis.HesaplaSpektralDisCephe(proje, cephe)
            ?? throw new InvalidOperationException("Cephe spektral sonucu üretilemedi.");
        return (dntw, sonuc.IcToplam, sonuc.D2mNt50);
    }

    // 5.2 — M12 patlatma davranışı: tavanı açık odada tavan montajlı otomatik
    // yerleşim atlanır; duvar montajlı yerleşim etkilenmez.
    public static void M12PatlatmaTavanYerlesimiAtlanir()
    {
        var kat = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeKat
            { Id = "kat-1", Ad = "Zemin", KatNo = 1, KatYuksekligiM = 3.0 };
        kat.Odalar.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeOda
        {
            Id = "oda-acik", Ad = "Galerili Salon", AlanM2 = 20,
            Poligon = new() { new(0, 0), new(5, 0), new(5, 4), new(0, 4) }
        });
        kat.Odalar.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeOda
        {
            Id = "oda-normal", Ad = "Mutfak", AlanM2 = 16,
            Poligon = new() { new(5, 0), new(9, 0), new(9, 4), new(5, 4) }
        });

        // Tavan montajlı armatür satırı — her iki odada aktif
        var satir = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcMahalSatiri
        {
            Tip = Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcElemanTipi.Armatür,
            YerlesimTipi = "Adet", MontajTipi = "Tavan"
        };
        foreach (var odaId in new[] { "oda-acik", "oda-normal" })
        {
            var hucre = satir.HucreGetirVeyaOlustur(odaId);
            hucre.Aktif = true;
            hucre.GenelAyarKullan = false;
            hucre.Adet = 2;
            hucre.YerlesimTipi = "Adet";
            hucre.MontajTipi = "Tavan";
        }
        kat.MahalListesi.Add(satir);

        var kutuphane = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Services.BasitProjeCizKutuphaneServisi();
        var servis = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BasitProjeCizOtomatikYerlestirServisi(
                new Kst.Acoustics.Core.Electric.BasitProjeCiz.Services.BasitProjeCizGeometriServisi());

        // Filtresiz: iki odaya da yerleşir (temel doğrulama)
        var (hepsi, _) = servis.TumMahalListesindenYerlestir(kat, kutuphane, sadecaEksikleri: false);
        if (!hepsi.Any(e => e.OdaId == "oda-acik") || !hepsi.Any(e => e.OdaId == "oda-normal"))
            throw new InvalidOperationException("Filtresiz yerleşim iki odaya da eleman koymalıydı.");

        // Tavanı açık filtresiyle: oda-acik atlanır + uyarı; oda-normal yerleşir
        var tavaniAcik = new HashSet<string> { "oda-acik" };
        var (yeni, uyarilar) = servis.TumMahalListesindenYerlestir(
            kat, kutuphane, sadecaEksikleri: false, tavaniAcik);

        if (yeni.Any(e => e.OdaId == "oda-acik"))
            throw new InvalidOperationException("Tavanı açık odaya tavan montajlı eleman yerleştirilmemeliydi.");
        if (!yeni.Any(e => e.OdaId == "oda-normal"))
            throw new InvalidOperationException("Normal odaya yerleşim devam etmeliydi.");
        if (!uyarilar.Any(u => u.Contains("tavanı açık")))
            throw new InvalidOperationException("Atlama uyarısı üretilmeliydi.");
    }

    // Geometri taşıyan, M50 bağlantısı olmayan eski M12 projesi.
    private static Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje EskiM12Projesi()
    {
        var proje = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje
            { ProjeAdi = "Eski M12" };
        var kat = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeKat
            { Id = "m12-kat-1", Ad = "Zemin", KatNo = 1, KatYuksekligiM = 2.8 };
        kat.Odalar.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeOda
        {
            Id = "m12-oda-1", Ad = "Salon", AlanM2 = 20.0,
            Poligon = new() { new(0, 0), new(5, 0), new(5, 4), new(0, 4) }
        });
        kat.Acikliklar.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcAciklik
        {
            Id = "m12-ac-1", Bas = new(1, 0), Bit = new(2, 0),
            Tip = Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcAciklikTipi.Kapı,
            ManuelEklendi = true, Kaynak = "manuel"
        });
        kat.Elemanlar.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeEleman
            { OdaId = "m12-oda-1", X = 2, Y = 2 });
        proje.Katlar.Add(kat);
        return proje;
    }

    // İki katlı, üst üste binen 20 m² odalı örnek geometri.
    private static (BinaGeometri geo, BinaKat altKat, BinaOda altOda, BinaKat ustKat, BinaOda ustOda) IkiKatliOrnek()
    {
        var geo = BinaGeometriServisi.YeniOlustur("Patlatma Testi");
        var poligon = new System.Collections.Generic.List<Point2D>
            { new(0, 0), new(5, 0), new(5, 4), new(0, 4) };

        var ustOda = new BinaOda { Id = "oda-ust", Ad = "Üst Salon", AlanM2 = 20.0, Poligon = new(poligon) };
        var ustKat = new BinaKat { Id = "kat-ust", Ad = "Üst Kat", KatNo = 1, KatYuksekligiM = 3.0 };
        ustKat.Odalar.Add(ustOda);

        var altOda = new BinaOda { Id = "oda-alt", Ad = "Alt Salon", AlanM2 = 20.0, Poligon = new(poligon) };
        var altKat = new BinaKat { Id = "kat-alt", Ad = "Alt Kat", KatNo = 2, KatYuksekligiM = 3.0 };
        altKat.Odalar.Add(altOda);

        geo.Katlar.Add(ustKat);
        geo.Katlar.Add(altKat);
        return (geo, altKat, altOda, ustKat, ustOda);
    }

    // Hata 1: Kat taşınınca (KatNo yeniden atanınca) düşey birleşim kararlı KatId
    // sayesinde doğru kata bağlı kalmalı; üyeler yeni KatNo'ları yansıtmalı.
    public static void KatTasinincaBirlesimKatIdKorunur()
    {
        var (geo, altKat, altOda, ustKat, ustOda) = IkiKatliOrnek(); // liste: [ust(1), alt(2)]
        var b = BinaDuseyBirlesimServisi.TavanPatlat(geo, altKat, altOda).Birlesim
            ?? throw new InvalidOperationException("Birleşim oluşmalı.");

        // Alt katı listenin en üstüne taşı ve katları yeniden numaralandır → [alt(1), ust(2)]
        geo.Katlar.Move(geo.Katlar.IndexOf(altKat), 0);
        for (int i = 0; i < geo.Katlar.Count; i++) geo.Katlar[i].KatNo = i + 1;
        BinaDuseyBirlesimServisi.KatNolariYenile(geo);

        // Hacim KatId çözümü sayesinde hâlâ doğru: 20×3 + 20×3 = 120 m³
        double hacim = BinaDuseyBirlesimServisi.BirlesikHacimM3(geo, b);
        if (Math.Abs(hacim - 120.0) > 1e-6)
            throw new InvalidOperationException($"Taşıma sonrası hacim 120 m³ beklenir; {hacim} bulundu.");

        var altUye = b.Uyeler.First(u => u.OdaId == altOda.Id);
        var ustUye = b.Uyeler.First(u => u.OdaId == ustOda.Id);

        // KatId kararlı → doğru katları işaret etmeli
        if (altUye.KatId != altKat.Id || ustUye.KatId != ustKat.Id)
            throw new InvalidOperationException("Üye KatId'leri taşımadan sonra bozuldu.");
        // KatNo tazelendi: alt artık 1, ust artık 2
        if (altUye.KatNo != 1 || ustUye.KatNo != 2)
            throw new InvalidOperationException($"Üye KatNo tazelenmeliydi; alt={altUye.KatNo}, ust={ustUye.KatNo}.");
        // Üst/Alt KatNo liste sırasına göre: alt üstte(1), ust altta(2)
        if (b.UstKatNo != 1 || b.AltKatNo != 2)
            throw new InvalidOperationException($"Üst/AltKatNo (1,2) beklenir; ({b.UstKatNo},{b.AltKatNo}) bulundu.");
        // OdaId bazlı sorgular değişmeden doğru kalmalı
        if (!BinaDuseyBirlesimServisi.AyniBirlesimdeAcik(geo, 1, altOda.Id, 2, ustOda.Id))
            throw new InvalidOperationException("İki oda taşımadan sonra da aynı birleşimde görünmeli.");
    }

    // Hata 1 (migrasyon): eski dosyada (KatId'siz üyeler) yükleme sırasında KatId
    // KatNo eşlemesinden doldurulmalı.
    public static void EskiBirlesimKatIdMigrasyonu()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4bina-katid-{Guid.NewGuid():N}.e4bina.json");
        try
        {
            var (geo, altKat, altOda, ustKat, ustOda) = IkiKatliOrnek();
            BinaDuseyBirlesimServisi.TavanPatlat(geo, altKat, altOda);
            // Eski dosya simülasyonu: KatId'leri temizle + sürümü düşür
            foreach (var b in geo.DuseyBirlesimleri)
                foreach (var u in b.Uyeler) u.KatId = "";
            geo.SemaSurum = 1;
            BinaGeometriServisi.Kaydet(geo, yol);

            var geo2 = BinaGeometriServisi.Ac(yol);
            if (geo2.SemaSurum != BinaGeometriServisi.GuncelSemaSurum)
                throw new InvalidOperationException("Migrasyon SemaSurum'u güncellemeliydi.");
            var uye = geo2.DuseyBirlesimleri[0].Uyeler.First(u => u.OdaId == altOda.Id);
            var beklenenKatId = geo2.Katlar.First(k => k.KatNo == uye.KatNo).Id;
            if (string.IsNullOrEmpty(uye.KatId) || uye.KatId != beklenenKatId)
                throw new InvalidOperationException($"KatId backfill başarısız: '{uye.KatId}'.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    // Hata 2: Bir kat silinince o kattaki odalara bağlı düşey birleşimler otomatik
    // düşmeli (askıda birleşim / hayalet açık tavan kalmamalı).
    public static void KatSilincaOdaBirlesimiTemizlenir()
    {
        var (geo, altKat, altOda, _, ustOda) = IkiKatliOrnek();
        BinaDuseyBirlesimServisi.TavanPatlat(geo, altKat, altOda);

        var vm = new BinaGeometriViewModel { Geo = geo };
        vm.SeciliKat = altKat;
        vm.KatSil();

        if (geo.DuseyBirlesimleri.Count != 0)
            throw new InvalidOperationException(
                $"Kat silinince birleşim temizlenmeliydi; {geo.DuseyBirlesimleri.Count} kaldı.");
        // Kalan üst odanın döşemesi artık açık raporlanmamalı (hayalet birleşim yok)
        if (BinaDuseyBirlesimServisi.DosemeAcikMi(geo, ustOda.Id))
            throw new InvalidOperationException("Kat silindikten sonra üst oda döşemesi açık kalmamalı.");
    }

    // Hata 3: Otomatik (arc) tespit edilen kapı, tip varsayılan yüksekliğini almalı.
    public static void OtomatikAciklikVarsayilanYukseklik()
    {
        var segs = new List<(Point2D A, Point2D B)> { (new(0, 0), new(3, 0)) };
        var arcs = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)>
            { (new(1, 0), 0.9, 0, 90) };

        var sonuc = AciklikTespitServisi.Bul(segs, arcs, Array.Empty<BinaAciklik>());
        var kapi = sonuc.FirstOrDefault(a => a.Kaynak == "arc")
            ?? throw new InvalidOperationException("Arc tabanlı kapı tespit edilmeliydi.");
        if (Math.Abs(kapi.YukseklikM - BinaAciklik.VarsayilanYukseklik(BinaAciklikTipi.Kapi)) > 1e-9)
            throw new InvalidOperationException($"Kapı varsayılan yüksekliği 2.1 m beklenir; {kapi.YukseklikM} bulundu.");
        if (BinaAciklik.VarsayilanYukseklik(BinaAciklikTipi.Pencere) is var pw && Math.Abs(pw - 1.5) > 1e-9)
            throw new InvalidOperationException($"Pencere varsayılanı 1.5 m beklenir; {pw} bulundu.");
    }

    // Hata 3 (koruma): yeniden tarama, kullanıcının girdiği yükseklik/eşiği sıfırlamamalı.
    public static void AciklikTaramaManuelYuksekligiKorur()
    {
        var manuel = new BinaAciklik
        {
            Id = "ac-manuel", Bas = new(1, 0), Bit = new(2, 0),
            Tip = BinaAciklikTipi.Pencere, GenislikM = 1.0,
            YukseklikM = 2.4, EsikYuksekligiM = 0.7,
            ManuelEklendi = true, Kaynak = "manuel"
        };
        var segs = new List<(Point2D A, Point2D B)> { (new(0, 0), new(3, 0)) };
        var arcs = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)>();

        var sonuc = AciklikTespitServisi.Bul(segs, arcs,
            new List<BinaAciklik> { manuel });
        var korunan = sonuc.First(a => a.Id == "ac-manuel");
        if (Math.Abs(korunan.YukseklikM - 2.4) > 1e-9)
            throw new InvalidOperationException($"Tarama manuel yüksekliği korumadı; {korunan.YukseklikM} bulundu.");
        if (Math.Abs(korunan.EsikYuksekligiM - 0.7) > 1e-9)
            throw new InvalidOperationException($"Tarama manuel eşiği korumadı; {korunan.EsikYuksekligiM} bulundu.");
    }

    // Orta 4: düzenleme IsDirty'yi işaretler (başlıkta *), TemizIsaretle sıfırlar.
    public static void IsDirtyDuzenlemeVeTemizleme()
    {
        var geo = BinaGeometriServisi.YeniOlustur("Dirty Test");
        var kat = new BinaKat { Id = "k1", Ad = "Kat 1", KatNo = 1, KatYuksekligiM = 3 };
        geo.Katlar.Add(kat);

        var vm = new BinaGeometriViewModel { Geo = geo };
        vm.SeciliKat = kat;

        if (vm.IsDirty)
            throw new InvalidOperationException("Yeni geometri temiz başlamalı.");
        if (vm.PencereBasligi.Contains('*'))
            throw new InvalidOperationException("Temiz başlıkta * olmamalı.");

        vm.OdaEkle(new List<Point2D> { new(0, 0), new(4, 0), new(4, 3), new(0, 3) });
        if (!vm.IsDirty)
            throw new InvalidOperationException("Oda ekleme IsDirty'yi işaretlemeliydi.");
        if (!vm.PencereBasligi.StartsWith("*"))
            throw new InvalidOperationException("Kirli başlık * ile başlamalı.");

        vm.TemizIsaretle();
        if (vm.IsDirty)
            throw new InvalidOperationException("TemizIsaretle IsDirty'yi sıfırlamalıydı.");

        // Aç/yeni de temiz başlatmalı
        vm.Kirlet();
        vm.Geo = BinaGeometriServisi.YeniOlustur("Yeni");
        if (vm.IsDirty)
            throw new InvalidOperationException("Yeni geometri ataması IsDirty'yi sıfırlamalıydı.");
    }

    // SemaSurum < güncel olan dosyalar yükleme sırasında migrate edilmeli.
    public static void EskiSemaSurumMigrateEdilir()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4bina-mig-{Guid.NewGuid():N}.e4bina.json");
        try
        {
            var geo = BinaGeometriServisi.YeniOlustur("Eski");
            geo.SemaSurum = 0;  // eski sürüm simüle et
            BinaGeometriServisi.Kaydet(geo, yol);

            // Kaydet SemaSurum'u üzerine yazmaz (serileştirir olduğu haliyle);
            // Ac sonrası migrate edilmiş olmalı.
            var geo2 = BinaGeometriServisi.Ac(yol);
            if (geo2.SemaSurum != BinaGeometriServisi.GuncelSemaSurum)
                throw new InvalidOperationException(
                    $"Migrasyon sonrası SemaSurum={BinaGeometriServisi.GuncelSemaSurum} beklenir; {geo2.SemaSurum} bulundu.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }
}
