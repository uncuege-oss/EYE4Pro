using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Tests;

public static class TekHatAktarimTests
{
    /// <summary>Bir kat + bir kat panosu + tek aydınlatma linyesi (armatür güçleri CustomGucW).</summary>
    private static (BasitProjeKat Kat, BasitProjeEleman Pano) KatYap(
        int katNo, string panoAd, int armaturSayisi, double armaturGucW)
    {
        var kat = new BasitProjeKat { KatNo = katNo, Ad = $"Kat {katNo}" };
        kat.Odalar.Add(new BasitProjeOda
        {
            Ad = "Oda",
            Poligon = [new(0, 0), new(10, 0), new(10, 8), new(0, 8)]
        });

        var anahtar = new BasitProjeEleman { Tip = BpcElemanTipi.Anahtar, Ad = "A", X = 0, Y = 1 };
        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B", X = 8, Y = 6 };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = panoAd, X = 1, Y = 2 };
        kat.Elemanlar.Add(anahtar);
        kat.Elemanlar.Add(buat);
        kat.Elemanlar.Add(pano);

        var armaturlar = new List<BasitProjeEleman>();
        for (int i = 0; i < armaturSayisi; i++)
        {
            var a = new BasitProjeEleman
            {
                Tip = BpcElemanTipi.Armatür,
                Ad = $"L{i}",
                X = 2 + i,
                Y = 4,
                CustomGucW = armaturGucW
            };
            kat.Elemanlar.Add(a);
            armaturlar.Add(a);
        }

        var devre = BasitProjeCizKabloServisi.EnsureDevre(kat, anahtar.Id);
        foreach (var a in armaturlar)
            BasitProjeCizKabloServisi.ArmaturAtama(kat, devre, a.Id);
        BasitProjeCizKabloServisi.ButaAta(kat, devre, buat.Id);
        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, []);
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        return (kat, pano);
    }

    public static void AnaVeTaliPanolariniOlusturur()
    {
        var (kat0, ana) = KatYap(0, "Ana KP", 2, 100);
        var (kat1, tali) = KatYap(1, "Kat 1 KP", 3, 100);

        // Ana → tali besleme; rota uzunluğu 3 + 4 = 7 m
        kat0.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = ana.Id,
            TaliPanoId = tali.Id,
            AnaKatNo = 0,
            TaliKatNo = 1,
            Rota = [new(0, 0), new(3, 0), new(3, 4)],
            Etiket = "BK1"
        });

        var proje = new BasitProjeCizProje { ProjeAdi = "Test" };
        proje.Katlar.Add(kat0);
        proje.Katlar.Add(kat1);

        var sonuc = TekHatAktarimServisi.Donustur(proje, []);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");

        var p = sonuc.Proje!;
        if (p.TumPanolar.Count != 2)
            throw new InvalidOperationException($"2 pano bekleniyordu. Bulunan: {p.TumPanolar.Count}.");
        if (p.KokPano is null || p.KokPano.Ad != "Ana Pano")
            throw new InvalidOperationException($"KokPano 'Ana Pano' olmalı. Bulunan: {p.KokPano?.Ad}.");
        if (p.KokPano.PanoTipi != PanoTipi.AnaPano)
            throw new InvalidOperationException("KokPano tipi AnaPano olmalı.");

        var taliPano = p.TumPanolar.Single(x => x.PanoTipi == PanoTipi.TaliPano);
        if (taliPano.UstPanoAd != "Ana Pano")
            throw new InvalidOperationException($"Tali pano UstPanoAd 'Ana Pano' olmalı. Bulunan: {taliPano.UstPanoAd}.");
        if (Math.Abs(taliPano.KabloMesafeM - 7.0) > 1e-6)
            throw new InvalidOperationException($"Tali kablo boyu 7 m olmalı (gerçek güzergah). Bulunan: {taliPano.KabloMesafeM}.");
    }

    public static void LinyeyiCikisSigortasiOlarakAktarir()
    {
        // Besleme yok + tek pano → o pano ana kabul edilir
        var (kat, _) = KatYap(0, "KP", 3, 100);
        var proje = new BasitProjeCizProje { ProjeAdi = "Test" };
        proje.Katlar.Add(kat);

        var sonuc = TekHatAktarimServisi.Donustur(proje, []);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");

        var pano = sonuc.Proje!.KokPano!;
        if (pano.Linyeler.Count != 1)
            throw new InvalidOperationException($"Tek linye bekleniyordu. Bulunan: {pano.Linyeler.Count}.");

        var linye = pano.Linyeler[0];
        if (linye.LinyeNo != "A1")
            throw new InvalidOperationException($"Linye etiketi A1 olmalı. Bulunan: {linye.LinyeNo}.");
        if (linye.YukTipiAd != "Aydınlatma")
            throw new InvalidOperationException($"Yük tipi Aydınlatma olmalı. Bulunan: {linye.YukTipiAd}.");
        if (linye.SortiSayisi != 3)
            throw new InvalidOperationException($"Sorti sayısı 3 olmalı. Bulunan: {linye.SortiSayisi}.");
        if (Math.Abs(linye.ToplamGucW - 300) > 1e-6)
            throw new InvalidOperationException($"Linye toplam gücü 300 W olmalı. Bulunan: {linye.ToplamGucW}.");
        if (linye.SigortaCinsi != SigortaCinsi.B)
            throw new InvalidOperationException($"Aydınlatma sigorta cinsi B olmalı. Bulunan: {linye.SigortaCinsi}.");
    }

    public static void TrifazePrizLinyesiniTrifazeAktarir()
    {
        var kat = new BasitProjeKat { KatNo = 0, Ad = "Zemin" };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", X = 0, Y = 0 };
        var priz = new BasitProjeEleman
        {
            Tip = BpcElemanTipi.TrifazePriz,
            Ad = "Trifaze Priz",
            X = 2,
            Y = 0,
            CustomGucW = 3000
        };
        kat.Elemanlar.Add(pano);
        kat.Elemanlar.Add(priz);
        kat.PrizDevreleri.Add(new BpcPrizDevre
        {
            BuatId = priz.Id,
            PrizElemanIds = [priz.Id]
        });
        BasitProjeCizKabloServisi.BuatPanoBagla(kat, priz.Id, pano.Id, [], "P");
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        var proje = new BasitProjeCizProje { ProjeAdi = "Trifaze" };
        proje.Katlar.Add(kat);

        var sonuc = TekHatAktarimServisi.Donustur(proje, []);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");

        var linye = sonuc.Proje!.KokPano!.Linyeler.Single();
        if (linye.YukTipiAd != "Priz")
            throw new InvalidOperationException($"Yük tipi Priz olmalı. Bulunan: {linye.YukTipiAd}.");
        if (linye.FazTipi != FazTipi.Trifaze)
            throw new InvalidOperationException($"Trifaze priz linyesi Trifaze aktarılmalı. Bulunan: {linye.FazTipi}.");
    }

    public static void BeslemeHiyerarsisiniKorur()
    {
        var (kat0, ana) = KatYap(0, "Ana KP", 1, 100);
        var (kat1, katPano) = KatYap(1, "Kat KP", 1, 100);
        var tali = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "Tali KP", X = 6, Y = 2 };
        kat1.Elemanlar.Add(tali);

        kat0.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = ana.Id,
            TaliPanoId = katPano.Id,
            AnaKatNo = 0,
            TaliKatNo = 1,
            Rota = [new(0, 0), new(4, 0)],
            Etiket = "BK1"
        });
        kat1.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = katPano.Id,
            TaliPanoId = tali.Id,
            AnaKatNo = 1,
            TaliKatNo = 1,
            Rota = [new(1, 1), new(4, 1), new(4, 3)],
            Etiket = "BK2"
        });

        var proje = new BasitProjeCizProje { ProjeAdi = "Hiyerarşi" };
        proje.Katlar.Add(kat0);
        proje.Katlar.Add(kat1);

        var sonuc = TekHatAktarimServisi.Donustur(proje, []);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");

        var aktarilanKatPano = sonuc.Proje!.TumPanolar.Single(p => p.Ad.StartsWith("Kat KP", StringComparison.OrdinalIgnoreCase));
        var aktarilanTali = sonuc.Proje.TumPanolar.Single(p => p.Ad.StartsWith("Tali KP", StringComparison.OrdinalIgnoreCase));

        if (aktarilanKatPano.UstPanoAd != "Ana Pano")
            throw new InvalidOperationException($"Kat panosu Ana Pano altında olmalı. Bulunan: {aktarilanKatPano.UstPanoAd}.");
        if (aktarilanTali.UstPanoAd != aktarilanKatPano.Ad)
            throw new InvalidOperationException($"Tali pano üstü '{aktarilanKatPano.Ad}' olmalı. Bulunan: {aktarilanTali.UstPanoAd}.");
        if (aktarilanTali.UstPano != aktarilanKatPano)
            throw new InvalidOperationException("Tali pano UstPano referansı kat panosuna bağlı olmalı.");
        if (!aktarilanKatPano.AltPanolar.Contains(aktarilanTali))
            throw new InvalidOperationException("Kat panosu AltPanolar listesinde tali panoyu içermeli.");
        if (Math.Abs(aktarilanTali.KabloMesafeM - 5.0) > 1e-6)
            throw new InvalidOperationException($"Tali pano kablo boyu 5 m olmalı. Bulunan: {aktarilanTali.KabloMesafeM}.");
    }

    /// A1 regresyonu: bir tali pano hem doğrudan ana panoya hem de kat panosuna
    /// besleme hattı taşırsa (iki üretici çakışması / eski proje), elektriksel ebeveyn
    /// ARA kat panosu olmalı (ana değil) ve kablo boyu yalnız o hattan gelmeli
    /// (doğrudan-ana hattı çift saymamalı).
    public static void CiftBeslemeHattindaKatPanosuEbeveynSecilir()
    {
        var (kat0, ana) = KatYap(0, "Ana KP", 1, 100);
        var (kat1, katPano) = KatYap(1, "Kat KP", 1, 100);
        var tali = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "Tali KP", X = 6, Y = 2 };
        kat1.Elemanlar.Add(tali);

        // Kat panosunu ana besler
        kat0.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = ana.Id, TaliPanoId = katPano.Id, AnaKatNo = 0, TaliKatNo = 1,
            Rota = [new(0, 0), new(4, 0)], Etiket = "BK1"
        });
        // ÇAKIŞMA: tali panoya ÖNCE doğrudan ana panodan uzun bir hat (eski TryAdd bunu seçerdi)
        kat1.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = ana.Id, TaliPanoId = tali.Id, AnaKatNo = 0, TaliKatNo = 1,
            Rota = [new(0, 0), new(20, 0)], Etiket = "BK2"
        });
        // DOĞRU: tali → kat panosu, kısa yerel hat (5 m)
        kat1.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = katPano.Id, TaliPanoId = tali.Id, AnaKatNo = 1, TaliKatNo = 1,
            Rota = [new(1, 1), new(4, 1), new(4, 3)], Etiket = "BK3"
        });

        var proje = new BasitProjeCizProje { ProjeAdi = "Çift besleme" };
        proje.Katlar.Add(kat0);
        proje.Katlar.Add(kat1);

        var sonuc = TekHatAktarimServisi.Donustur(proje, []);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");

        var aktarilanKatPano = sonuc.Proje!.TumPanolar.Single(p => p.Ad.StartsWith("Kat KP", StringComparison.OrdinalIgnoreCase));
        var aktarilanTali = sonuc.Proje.TumPanolar.Single(p => p.Ad.StartsWith("Tali KP", StringComparison.OrdinalIgnoreCase));

        if (aktarilanTali.UstPanoAd != aktarilanKatPano.Ad)
            throw new InvalidOperationException($"Tali ebeveyni kat panosu olmalı (ana değil). Bulunan: {aktarilanTali.UstPanoAd}.");
        // Yalnız tali→kat hattı (5 m); doğrudan-ana (20 m) çift sayılmamalı → 25 olmamalı.
        if (Math.Abs(aktarilanTali.KabloMesafeM - 5.0) > 1e-6)
            throw new InvalidOperationException($"Tali kablo boyu 5 m olmalı (yalnız seçilen ebeveyn hattı). Bulunan: {aktarilanTali.KabloMesafeM}.");
    }

    public static void KokPanoToplamGucuAltPanolariIcerir()
    {
        var (kat0, ana) = KatYap(0, "Ana KP", 2, 100);   // 200 W
        var (kat1, tali) = KatYap(1, "Tali KP", 3, 100);  // 300 W
        kat0.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = ana.Id, TaliPanoId = tali.Id, AnaKatNo = 0, TaliKatNo = 1,
            Rota = [new(0, 0), new(5, 0)], Etiket = "BK1"
        });

        var proje = new BasitProjeCizProje { ProjeAdi = "Test" };
        proje.Katlar.Add(kat0);
        proje.Katlar.Add(kat1);

        var sonuc = TekHatAktarimServisi.Donustur(proje, []);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");

        // KokPano toplam gücü = kendi 200 + tali 300 = 500 W
        if (Math.Abs(sonuc.Proje!.KokPano!.ToplamGucW - 500) > 1e-6)
            throw new InvalidOperationException($"KokPano toplam gücü 500 W olmalı. Bulunan: {sonuc.Proje.KokPano.ToplamGucW}.");
    }

    public static void PanoIsimleriBenzersizlestirilir()
    {
        var (kat0, ana) = KatYap(0, "Ana KP", 1, 100);
        var (kat1, tali1) = KatYap(1, "KP", 1, 100);
        var (kat2, tali2) = KatYap(2, "KP", 1, 100); // aynı ad, farklı kat

        kat0.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = ana.Id, TaliPanoId = tali1.Id, AnaKatNo = 0, TaliKatNo = 1,
            Rota = [new(0, 0), new(2, 0)], Etiket = "BK1"
        });
        kat0.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = ana.Id, TaliPanoId = tali2.Id, AnaKatNo = 0, TaliKatNo = 2,
            Rota = [new(0, 0), new(2, 0)], Etiket = "BK2"
        });

        var proje = new BasitProjeCizProje { ProjeAdi = "Test" };
        proje.Katlar.Add(kat0);
        proje.Katlar.Add(kat1);
        proje.Katlar.Add(kat2);

        var sonuc = TekHatAktarimServisi.Donustur(proje, []);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");

        var adlar = sonuc.Proje!.TumPanolar.Select(p => p.Ad).ToList();
        if (adlar.Distinct(StringComparer.OrdinalIgnoreCase).Count() != adlar.Count)
            throw new InvalidOperationException($"Pano adları benzersiz olmalı. Bulunan: {string.Join(", ", adlar)}.");
    }

    public static void E4p11RoundTripPanolariKorur()
    {
        var (kat0, ana) = KatYap(0, "Ana KP", 2, 100);
        var (kat1, tali) = KatYap(1, "Tali KP", 3, 100);
        kat0.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = ana.Id, TaliPanoId = tali.Id, AnaKatNo = 0, TaliKatNo = 1,
            Rota = [new(0, 0), new(4, 0)], Etiket = "BK1"
        });
        var proje = new BasitProjeCizProje { ProjeAdi = "RT" };
        proje.Katlar.Add(kat0);
        proje.Katlar.Add(kat1);

        var sonuc = TekHatAktarimServisi.Donustur(proje, []);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");

        // BasitProjeCiz UI'daki ve Tek Hat'taki ile birebir aynı JSON ayarları
        var opts = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        var json = System.Text.Json.JsonSerializer.Serialize(sonuc.Proje, opts);
        var geri = System.Text.Json.JsonSerializer.Deserialize<ElektrikProje>(json, opts);
        if (geri is null)
            throw new InvalidOperationException(".e4p11 deserialize null döndü.");
        if (geri.TumPanolar.Count != 2)
            throw new InvalidOperationException($"Round-trip sonrası 2 pano bekleniyordu. Bulunan: {geri.TumPanolar.Count}.");

        // Tek Hat yükleme mantığı: ana panoyu Ad ile yeniden bul (KokPano re-link)
        var anaGeri = geri.TumPanolar.FirstOrDefault(p => p.Ad == geri.KokPano?.Ad);
        if (anaGeri is null || anaGeri.PanoTipi != PanoTipi.AnaPano)
            throw new InvalidOperationException("Round-trip sonrası ana pano Ad ile bulunamadı.");

        var taliGeri = geri.TumPanolar.Single(p => p.PanoTipi == PanoTipi.TaliPano);
        if (taliGeri.UstPanoAd != "Ana Pano")
            throw new InvalidOperationException($"Round-trip sonrası tali UstPanoAd korunmalı. Bulunan: {taliGeri.UstPanoAd}.");
        if (taliGeri.Linyeler.Count != 1 || Math.Abs(taliGeri.Linyeler[0].ToplamGucW - 300) > 1e-6)
            throw new InvalidOperationException("Round-trip sonrası tali linye gücü korunmadı.");
        if (Math.Abs(taliGeri.KabloMesafeM - 4.0) > 1e-6)
            throw new InvalidOperationException($"Round-trip sonrası kablo boyu 4 m olmalı. Bulunan: {taliGeri.KabloMesafeM}.");
    }

    public static void BeslemeYokVeCokPanoVarsaHataVerir()
    {
        var (kat0, _) = KatYap(0, "KP-1", 1, 100);
        var (kat1, _) = KatYap(1, "KP-2", 1, 100);
        // besleme hattı çizilmedi → ana pano belirsiz

        var proje = new BasitProjeCizProje { ProjeAdi = "Test" };
        proje.Katlar.Add(kat0);
        proje.Katlar.Add(kat1);

        var sonuc = TekHatAktarimServisi.Donustur(proje, []);
        if (sonuc.Basarili)
            throw new InvalidOperationException("Besleme yok ve >1 pano varken aktarım başarısız olmalıydı.");
        if (string.IsNullOrEmpty(sonuc.Hata))
            throw new InvalidOperationException("Hata mesajı bekleniyordu.");
    }

    // ─── Faz 3: uçtan uca entegrasyon (round-trip + Tek Hat yükle/hesapla) ───

    /// <summary>Tek Hat ProjeyiYukle + TumunuHesapla eşdeğeri — Core servisleriyle.</summary>
    private static void TekHatYukleVeHesapla(ElektrikProje proje, ElektrikKutuphaneServisi kutuphane)
    {
        // UstPano referansları (UstPanoAd'tan); JSON'da [JsonIgnore] olduğu için round-trip sonrası boş
        foreach (var p in proje.TumPanolar)
        {
            p.UstPano = null;
            if (!string.IsNullOrEmpty(p.UstPanoAd))
                p.UstPano = proje.TumPanolar.FirstOrDefault(x => x.Ad == p.UstPanoAd && x != p);
        }
        foreach (var p in proje.TumPanolar) p.AltPanolar.Clear();
        foreach (var p in proje.TumPanolar)
            if (p.UstPano != null && !p.UstPano.AltPanolar.Contains(p))
                p.UstPano.AltPanolar.Add(p);

        // KokPano kimliğini Ad ile yeniden bağla (Faz 2 düzeltmesi)
        proje.KokPano = proje.TumPanolar.FirstOrDefault(p => p.Ad == proje.KokPano?.Ad)
            ?? proje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano)
            ?? proje.KokPano;

        // dip-first hesap
        foreach (var pano in proje.TumPanolar.OrderBy(Derinlik).Reverse())
        {
            PanoHesapServisi.PanoHesapla(pano, proje, kutuphane);
            KabloHesapServisi.PanoKabloHesapla(pano, proje);
        }

        if (proje.KokPano != null &&
            KompanzasyonHesapServisi.KompanzasyonGerekliMi(proje.KokPano, proje.KompanzasyonTetikKW))
            proje.Kompanzasyon = KompanzasyonHesapServisi.Hesapla(proje.KokPano, proje, kutuphane, proje.Kompanzasyon);
    }

    private static int Derinlik(Pano p)
    {
        int d = 0;
        var x = p;
        while (x.UstPano != null) { d++; x = x.UstPano; }
        return d;
    }

    private static ElektrikProje RoundTrip(ElektrikProje proje)
    {
        var opts = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
        var json = System.Text.Json.JsonSerializer.Serialize(proje, opts);
        return System.Text.Json.JsonSerializer.Deserialize<ElektrikProje>(json, opts)
            ?? throw new InvalidOperationException(".e4p11 deserialize null döndü.");
    }

    public static void EntegrasyonHesapZinciriVeKompanzasyonAutoTetik()
    {
        // Yüksek yük → kompanzasyon tetiği (>15 kW). Ana 10 kW + tali 10 kW = 20 kW.
        var (kat0, ana) = KatYap(0, "Ana KP", 10, 1000);
        var (kat1, tali) = KatYap(1, "Tali KP", 10, 1000);
        kat0.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            AnaPanoId = ana.Id, TaliPanoId = tali.Id, AnaKatNo = 0, TaliKatNo = 1,
            Rota = [new(0, 0), new(0, 30)], Etiket = "BK1" // 30 m gerçek kablo boyu
        });
        var kaynak = new BasitProjeCizProje { ProjeAdi = "Entegrasyon" };
        kaynak.Katlar.Add(kat0);
        kaynak.Katlar.Add(kat1);

        var sonuc = TekHatAktarimServisi.Donustur(kaynak, []);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");

        var yuklenen = RoundTrip(sonuc.Proje!);
        var kutuphane = new ElektrikKutuphaneServisi();
        TekHatYukleVeHesapla(yuklenen, kutuphane);

        var kok = yuklenen.KokPano ?? throw new InvalidOperationException("KokPano yok.");

        // 1) Faz dağılımı hesaplandı (R+S+T = ananın kendi linye gücü = 10 kW)
        var fd = kok.FazDagilimi;
        double fazToplam = fd.R_W + fd.S_W + fd.T_W;
        if (Math.Abs(fazToplam - 10000) > 1.0)
            throw new InvalidOperationException($"Faz dağılımı toplamı 10000 W olmalı. Bulunan: {fazToplam}.");

        // 2) Giriş sigortası hesaplandı
        if (kok.GirisSigortaA <= 0)
            throw new InvalidOperationException("KokPano giriş sigortası hesaplanmadı.");

        // 3) Linye sigortaları hesaplandı (Tek Hat tarafında)
        if (kok.Linyeler.Count == 0 || kok.Linyeler.Any(l => l.SigortaAmper <= 0))
            throw new InvalidOperationException("Linye sigortaları hesaplanmadı.");

        // 4) Tali pano kablo gerilim düşümü gerçek boydan (30 m) hesaplandı
        var taliP = yuklenen.TumPanolar.Single(p => p.PanoTipi == PanoTipi.TaliPano);
        if (taliP.KabloGerilimDususYuzde <= 0)
            throw new InvalidOperationException("Tali pano kablo gerilim düşümü hesaplanmadı (gerçek boy bekleniyordu).");

        // 5) Kompanzasyon otomatik tetiklendi ve kademe üretti
        if (!KompanzasyonHesapServisi.KompanzasyonGerekliMi(kok, yuklenen.KompanzasyonTetikKW))
            throw new InvalidOperationException("20 kW'da kompanzasyon tetiklenmeliydi.");
        if (yuklenen.Kompanzasyon is null || yuklenen.Kompanzasyon.KondansatorKademeleri.Count == 0)
            throw new InvalidOperationException("Kompanzasyon kademeleri üretilmeliydi.");
    }

    public static void EntegrasyonDusukYuktekompanzasyonTetiklenmez()
    {
        var (kat, _) = KatYap(0, "KP", 2, 100); // 200 W → tek pano = ana
        var kaynak = new BasitProjeCizProje { ProjeAdi = "Dusuk" };
        kaynak.Katlar.Add(kat);

        var sonuc = TekHatAktarimServisi.Donustur(kaynak, []);
        if (!sonuc.Basarili)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");

        var yuklenen = RoundTrip(sonuc.Proje!);
        var kutuphane = new ElektrikKutuphaneServisi();
        TekHatYukleVeHesapla(yuklenen, kutuphane);

        var kok = yuklenen.KokPano ?? throw new InvalidOperationException("KokPano yok.");
        if (KompanzasyonHesapServisi.KompanzasyonGerekliMi(kok, yuklenen.KompanzasyonTetikKW))
            throw new InvalidOperationException("200 W'da kompanzasyon tetiklenmemeliydi.");
        if (yuklenen.Kompanzasyon != null)
            throw new InvalidOperationException("Düşük yükte kompanzasyon sonucu üretilmemeliydi.");
    }
}
