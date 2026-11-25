using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// <summary>
/// Mahal listesi üretkenlik servisi (codex12 §20): kattan kopyalama, oda adına
/// göre akıllı doldurma ve adlandırılmış mahal şablonları.
///
/// Tasarım kuralı: bu servisin hiçbir işlemi kullanıcının ELLE aktif ettiği
/// hücreyi ezmez — yalnız boş/pasif hücreleri doldurur veya yeni satır ekler.
/// Kullanıcı emeğinin sessizce kaybolması kabul edilmez.
/// </summary>
public static class MahalListesiServisi
{
    // ─── Ortak yardımcılar ────────────────────────────────────────────────

    /// Oda adlarını karşılaştırma için normalize eder: küçük harf, Türkçe
    /// karakter sadeleştirme, sayı/boşluk/noktalama atma.
    /// "Yatak Odası 2" ve "YATAK ODASI-2" aynı anahtara düşer.
    public static string OdaAdiNormalize(string? ad)
    {
        if (string.IsNullOrWhiteSpace(ad)) return "";
        var s = ad.Trim().ToLowerInvariant()
            .Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g')
            .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c')
            .Replace('â', 'a').Replace('î', 'i').Replace('û', 'u');
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
            if (char.IsLetter(ch)) sb.Append(ch);
        return sb.ToString();
    }

    /// Hücre ayarının derin kopyası (kopyalama/şablon için).
    public static BpcMahalHucreAyar HucreKopyala(BpcMahalHucreAyar a) => new()
    {
        Aktif = a.Aktif,
        GenelAyarKullan = a.GenelAyarKullan,
        Sensorlu = a.Sensorlu,
        YerlesimTipi = a.YerlesimTipi,
        MontajTipi = a.MontajTipi,
        GridAraligiM = a.GridAraligiM,
        GridAraligiYM = a.GridAraligiYM,
        DuvarAraligiM = a.DuvarAraligiM,
        Adet = a.Adet,
        OzelAd = a.OzelAd,
        OzelGucW = a.OzelGucW,
        OzelMonofazeAdet = a.OzelMonofazeAdet,
        OzelTrifazeAdet = a.OzelTrifazeAdet,
        OzelMegapiksel = a.OzelMegapiksel,
        OzelCihazAdet = a.OzelCihazAdet
    };

    /// Satırın genel ayarlarının kopyası (hücreler hariç).
    private static BpcMahalSatiri SatirIskeletKopyala(BpcMahalSatiri s) => new()
    {
        Tip = s.Tip,
        ArmatürTanimId = s.ArmatürTanimId,
        ArmatürTanimAd = s.ArmatürTanimAd,
        YerlesimTipi = s.YerlesimTipi,
        MontajTipi = s.MontajTipi,
        GridAraligiM = s.GridAraligiM,
        GridAraligiYM = s.GridAraligiYM,
        DuvarAraligiM = s.DuvarAraligiM,
        GelenGucW = s.GelenGucW
    };

    /// İki satırın "aynı kalem" olup olmadığı. Aynı sembolden ikinci satır
    /// desteklenmediği için (codex12 §20 kararı) kimlik = tanım Id veya tip.
    private static bool AyniKalem(BpcMahalSatiri a, BpcMahalSatiri b)
        => !string.IsNullOrEmpty(a.ArmatürTanimId) || !string.IsNullOrEmpty(b.ArmatürTanimId)
            ? a.ArmatürTanimId == b.ArmatürTanimId
            : a.Tip == b.Tip;

    // ─── 1) Kattan kopyalama ──────────────────────────────────────────────

    public readonly record struct KopyalamaSonucu(
        int EklenenSatir, int GuncellenenSatir, int EslesenOda, int EslesmeyenOda)
    {
        public string Ozet =>
            $"{EklenenSatir} kalem eklendi, {GuncellenenSatir} kalem güncellendi · " +
            $"{EslesenOda} oda adı eşleşti" + (EslesmeyenOda > 0 ? $", {EslesmeyenOda} oda eşleşmedi (pasif geldi)" : "");
    }

    /// <summary>
    /// Kaynak katın mahal listesini hedef kata kopyalar. Hücre ayarları oda
    /// ADINA göre eşleştirilir (Id'ler katlar arasında farklıdır); adı eşleşmeyen
    /// hedef odada hücre PASİF kalır — yanlış odaya eleman yerleşmesin.
    /// Hedefte zaten bulunan kalem silinmez; yalnız o kalemde HENÜZ AKTİF OLMAYAN
    /// hücreler kaynaktan doldurulur (kullanıcı emeği korunur).
    /// </summary>
    public static KopyalamaSonucu KattanKopyala(
        BasitProjeKat kaynak, BasitProjeKat hedef, bool yalnizSecilenTipler = false,
        Func<BpcMahalSatiri, bool>? satirFiltresi = null)
    {
        if (ReferenceEquals(kaynak, hedef)) return default;

        // Hedef oda adı → oda (aynı ada sahip birden çok oda varsa ilki)
        var hedefOdaByAd = new Dictionary<string, BasitProjeOda>();
        foreach (var o in hedef.Odalar)
        {
            var k = OdaAdiNormalize(o.Ad);
            if (k.Length > 0 && !hedefOdaByAd.ContainsKey(k)) hedefOdaByAd[k] = o;
        }

        int eklenen = 0, guncellenen = 0;
        var eslesenOdalar = new HashSet<string>();
        var eslesmeyenOdalar = new HashSet<string>();

        foreach (var kaynakSatir in kaynak.MahalListesi)
        {
            if (satirFiltresi is not null && !satirFiltresi(kaynakSatir)) continue;

            var hedefSatir = hedef.MahalListesi.FirstOrDefault(s => AyniKalem(s, kaynakSatir));
            bool yeni = hedefSatir is null;
            if (hedefSatir is null)
            {
                hedefSatir = SatirIskeletKopyala(kaynakSatir);
                hedef.MahalListesi.Add(hedefSatir);
                eklenen++;
            }
            else
            {
                guncellenen++;
            }

            // Hücreleri oda adına göre taşı
            foreach (var kaynakOda in kaynak.Odalar)
            {
                if (!kaynakSatir.HucreAyarlari.TryGetValue(kaynakOda.Id, out var kaynakAyar)) continue;
                if (!kaynakAyar.Aktif) continue; // yalnız gerçekten kullanılan hücreler taşınır

                var anahtar = OdaAdiNormalize(kaynakOda.Ad);
                if (anahtar.Length == 0 || !hedefOdaByAd.TryGetValue(anahtar, out var hedefOda))
                {
                    eslesmeyenOdalar.Add(kaynakOda.Ad);
                    continue;
                }
                eslesenOdalar.Add(kaynakOda.Ad);

                var mevcut = hedefSatir.HucreGetirVeyaOlustur(hedefOda.Id);
                // Kullanıcının hedefte zaten aktif ettiği hücre EZİLMEZ.
                if (mevcut.Aktif && !yeni) continue;

                var kopya = HucreKopyala(kaynakAyar);
                hedefSatir.HucreAyarlari[hedefOda.Id] = kopya;
            }
        }

        return new KopyalamaSonucu(eklenen, guncellenen, eslesenOdalar.Count, eslesmeyenOdalar.Count);
    }

    // ─── 2) Oda adına göre akıllı doldurma ────────────────────────────────

    /// Bir oda tipi için önerilen tek kalem.
    public sealed class OneriKalem
    {
        /// Eşleşecek eleman tipi (sembol seçimi kütüphaneden yapılır).
        public BpcElemanTipi Tip { get; set; }
        /// "Adet" | "Grid" | "Duvar aralıklı" | "Açıklık Yanı" | "Homojen Duvar"
        public string YerlesimTipi { get; set; } = "Adet";
        public string MontajTipi { get; set; } = "Tavan";
        /// Adet modunda kullanılacak sayı (0 = yerleşim tipi belirlesin).
        public int Adet { get; set; }
        public double? GridAraligiM { get; set; }
        public double? DuvarAraligiM { get; set; }
    }

    /// Oda adı kalıbı → önerilen kalem seti.
    public sealed class OdaTipiOnerisi
    {
        public string Ad { get; set; } = "";
        /// Normalize edilmiş oda adında aranacak parçalar (herhangi biri eşleşirse).
        public List<string> Anahtarlar { get; set; } = new();
        public List<OneriKalem> Kalemler { get; set; } = new();
    }

    /// Yerleşik (gömülü) öneri tablosu. Kullanıcı şablon dosyasıyla genişletebilir.
    /// Anahtarlar OdaAdiNormalize'dan geçmiş biçimde yazılır (ı→i, ş→s …).
    public static IReadOnlyList<OdaTipiOnerisi> VarsayilanOneriler { get; } = new List<OdaTipiOnerisi>
    {
        new()
        {
            Ad = "Yatak Odası",
            Anahtarlar = { "yatak", "ebeveyn", "cocukodasi", "cocuk" },
            Kalemler =
            {
                new() { Tip = BpcElemanTipi.Armatür,  YerlesimTipi = "Adet", MontajTipi = "Tavan", Adet = 1 },
                new() { Tip = BpcElemanTipi.Anahtar,  YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
                new() { Tip = BpcElemanTipi.Priz,     YerlesimTipi = "Açıklık Yanı", MontajTipi = "Duvar", Adet = 4 },
            }
        },
        new()
        {
            Ad = "Salon / Oturma",
            Anahtarlar = { "salon", "oturma", "yasama", "misafir" },
            Kalemler =
            {
                new() { Tip = BpcElemanTipi.Armatür,   YerlesimTipi = "Grid", MontajTipi = "Tavan", GridAraligiM = 3.0 },
                new() { Tip = BpcElemanTipi.Komütatör, YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
                new() { Tip = BpcElemanTipi.Priz,      YerlesimTipi = "Açıklık Yanı", MontajTipi = "Duvar", Adet = 6 },
                new() { Tip = BpcElemanTipi.TvPriz,    YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
            }
        },
        new()
        {
            Ad = "Mutfak",
            Anahtarlar = { "mutfak", "ankastre" },
            Kalemler =
            {
                new() { Tip = BpcElemanTipi.Armatür,     YerlesimTipi = "Grid", MontajTipi = "Tavan", GridAraligiM = 2.5 },
                new() { Tip = BpcElemanTipi.Anahtar,     YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
                new() { Tip = BpcElemanTipi.Priz,        YerlesimTipi = "Homojen Duvar", MontajTipi = "Duvar", Adet = 6 },
                new() { Tip = BpcElemanTipi.TrifazePriz, YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
            }
        },
        new()
        {
            Ad = "Banyo / WC",
            Anahtarlar = { "banyo", "wc", "tuvalet", "lavabo", "dus" },
            Kalemler =
            {
                new() { Tip = BpcElemanTipi.Armatür,          YerlesimTipi = "Adet", MontajTipi = "Tavan", Adet = 1 },
                new() { Tip = BpcElemanTipi.IkiKutupluAnahtar, YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
                new() { Tip = BpcElemanTipi.KapakliPriz,      YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
            }
        },
        new()
        {
            Ad = "Hol / Koridor",
            Anahtarlar = { "hol", "koridor", "antre", "giris", "sofa" },
            Kalemler =
            {
                new() { Tip = BpcElemanTipi.Armatür, YerlesimTipi = "Duvar aralıklı", MontajTipi = "Tavan", DuvarAraligiM = 3.0 },
                new() { Tip = BpcElemanTipi.Vavien,  YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 2 },
                new() { Tip = BpcElemanTipi.Priz,    YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
            }
        },
        new()
        {
            Ad = "Ofis / Çalışma",
            Anahtarlar = { "ofis", "calisma", "buro", "toplanti", "sekreter", "mudur" },
            Kalemler =
            {
                new() { Tip = BpcElemanTipi.Armatür,   YerlesimTipi = "Grid", MontajTipi = "Tavan", GridAraligiM = 2.5 },
                new() { Tip = BpcElemanTipi.Komütatör, YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
                new() { Tip = BpcElemanTipi.Priz,      YerlesimTipi = "Homojen Duvar", MontajTipi = "Duvar", Adet = 6 },
                new() { Tip = BpcElemanTipi.DataPriz,  YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 2 },
            }
        },
        new()
        {
            Ad = "Depo / Teknik",
            Anahtarlar = { "depo", "teknik", "kazan", "sigin", "ardiye", "arsiv" },
            Kalemler =
            {
                new() { Tip = BpcElemanTipi.Armatür, YerlesimTipi = "Duvar aralıklı", MontajTipi = "Tavan", DuvarAraligiM = 4.0 },
                new() { Tip = BpcElemanTipi.Anahtar, YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
                new() { Tip = BpcElemanTipi.SivaUstuPriz, YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
            }
        },
        new()
        {
            Ad = "Balkon / Teras",
            Anahtarlar = { "balkon", "teras", "veranda" },
            Kalemler =
            {
                new() { Tip = BpcElemanTipi.Armatür,      YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
                new() { Tip = BpcElemanTipi.KapakliPriz,  YerlesimTipi = "Adet", MontajTipi = "Duvar", Adet = 1 },
            }
        },
    };

    /// Oda adına uyan öneriyi bulur (en uzun anahtar eşleşmesi kazanır —
    /// "cocukodasi" varken "oda" gibi kısa anahtarın yanlış yakalaması engellenir).
    public static OdaTipiOnerisi? OneriBul(string? odaAd, IReadOnlyList<OdaTipiOnerisi>? tablo = null)
    {
        var norm = OdaAdiNormalize(odaAd);
        if (norm.Length == 0) return null;
        OdaTipiOnerisi? enIyi = null;
        int enUzun = 0;
        foreach (var o in tablo ?? VarsayilanOneriler)
        {
            foreach (var a in o.Anahtarlar)
            {
                if (a.Length == 0 || !norm.Contains(a, StringComparison.Ordinal)) continue;
                if (a.Length > enUzun) { enUzun = a.Length; enIyi = o; }
            }
        }
        return enIyi;
    }

    public readonly record struct AkilliDoldurmaSonucu(
        int EklenenSatir, int AktifEdilenHucre, IReadOnlyList<string> TaninmayanOdalar)
    {
        public string Ozet
        {
            get
            {
                var s = $"{AktifEdilenHucre} hücre dolduruldu ({EklenenSatir} yeni kalem)";
                if (TaninmayanOdalar.Count > 0)
                    s += $" · tanınmayan {TaninmayanOdalar.Count} oda: {string.Join(", ", TaninmayanOdalar.Take(6))}" +
                         (TaninmayanOdalar.Count > 6 ? "…" : "");
                return s;
            }
        }
    }

    /// <summary>
    /// Oda adlarından tipik kalem setini kurar. Kullanıcının ELLE aktif ettiği
    /// hücrelere dokunmaz; yalnız pasif hücreleri doldurur ve eksik kalemleri ekler.
    /// Sembol çözümü kütüphaneden yapılır; sembolü bulunamayan kalem atlanır.
    /// </summary>
    public static AkilliDoldurmaSonucu AkilliDoldur(
        BasitProjeKat kat,
        BasitProjeCizKutuphaneServisi kutuphane,
        IReadOnlyList<OdaTipiOnerisi>? tablo = null,
        Func<BpcElemanTipi, bool>? tipFiltresi = null)
    {
        int eklenenSatir = 0, aktifEdilen = 0;
        var taninmayan = new List<string>();

        foreach (var oda in kat.Odalar)
        {
            if (!oda.MahalListesineDahil) continue;

            var oneri = OneriBul(oda.Ad, tablo);
            if (oneri is null)
            {
                if (!string.IsNullOrWhiteSpace(oda.Ad)) taninmayan.Add(oda.Ad);
                continue;
            }

            foreach (var kalem in oneri.Kalemler)
            {
                if (tipFiltresi is not null && !tipFiltresi(kalem.Tip)) continue;

                var tanim = SembolBul(kalem.Tip, kutuphane);
                if (tanim is null) continue; // sembolü olmayan kalem sessizce atlanır

                var satir = kat.MahalListesi.FirstOrDefault(s =>
                    (!string.IsNullOrEmpty(s.ArmatürTanimId) && s.ArmatürTanimId == tanim.Id) ||
                    (string.IsNullOrEmpty(s.ArmatürTanimId) && s.Tip == kalem.Tip));

                if (satir is null)
                {
                    satir = new BpcMahalSatiri
                    {
                        Tip = kalem.Tip,
                        ArmatürTanimId = tanim.Id,
                        ArmatürTanimAd = $"{tanim.BlokAdi} — {tanim.Ad}",
                        YerlesimTipi = kalem.YerlesimTipi,
                        MontajTipi = kalem.MontajTipi,
                        GridAraligiM = kalem.GridAraligiM ?? 3.0,
                        DuvarAraligiM = kalem.DuvarAraligiM ?? 3.0
                    };
                    kat.MahalListesi.Add(satir);
                    eklenenSatir++;
                }

                var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
                if (ayar.Aktif) continue; // kullanıcı zaten açmış — dokunma

                ayar.Aktif = true;
                // Oda-özel ayar: satırın genel ayarı başka odalar için farklı olabilir.
                ayar.GenelAyarKullan = false;
                ayar.YerlesimTipi = kalem.YerlesimTipi;
                ayar.MontajTipi = kalem.MontajTipi;
                if (kalem.GridAraligiM.HasValue) ayar.GridAraligiM = kalem.GridAraligiM.Value;
                if (kalem.DuvarAraligiM.HasValue) ayar.DuvarAraligiM = kalem.DuvarAraligiM.Value;
                if (kalem.Adet > 0) ayar.Adet = kalem.Adet;
                aktifEdilen++;
            }
        }

        return new AkilliDoldurmaSonucu(eklenenSatir, aktifEdilen, taninmayan);
    }

    /// Bir eleman tipi için kütüphaneden uygun sembolü seçer.
    private static BpcArmatürTanim? SembolBul(BpcElemanTipi tip, BasitProjeCizKutuphaneServisi kutuphane)
    {
        var tumu = kutuphane.Armatürler
            .Concat(kutuphane.Anahtarlar)
            .Concat(kutuphane.Prizler)
            .Concat(kutuphane.Klimalar);

        var eslesen = tumu.FirstOrDefault(t => BasitProjeCizKutuphaneServisi.TanimTipi(t) == tip);
        if (eslesen is not null) return eslesen;

        return BasitProjeCizKutuphaneServisi.ZayifAkimTanim(tip) ?? kutuphane.BulManuelTanim(tip);
    }

    // ─── 4) Yerleşim ön-tahmini (M5) ──────────────────────────────────────

    public static bool KullaniciAdediGerekir(BpcElemanTipi tip, string yerlesim) =>
        yerlesim is "Adet" or "Açıklık Yanı" or "Homojen Duvar" ||
        (yerlesim == "Duvar aralıklı" && BasitProjeCizKabloServisi.ElemanPrizMi(tip));

    /// <summary>
    /// Bir hücrenin YERLEŞTİR'de kaç eleman üreteceğini tahmin eder. Grid ve
    /// "Duvar aralıklı" modlarda adet kullanıcı girmediği için aday üretici
    /// çalıştırılır; Adet tabanlı modlarda kullanıcının girdiği sayı döner.
    /// Yerleştirmenin kendisi bu değeri kullanmaz (kaynak: OdayaYerlestir) —
    /// tahmin yalnız kullanıcıya öngörü göstermek içindir.
    /// </summary>
    public static int YerlesimTahmini(
        BpcMahalSatiri satir, BasitProjeOda oda, BasitProjeCizGeometriServisi geometri)
    {
        if (!satir.EfektifAktif(oda.Id) || !oda.MahalListesineDahil) return 0;
        if (oda.Poligon.Count < 3) return 0;

        var (yerlesim, _, gridM, duvarM) = satir.EfektifAyarlar(oda.Id);
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        if (KullaniciAdediGerekir(satir.Tip, yerlesim))
            return Math.Max(0, ayar.Adet);

        const double armaturDuvarMin = 0.50;
        const double duvarGiris = 0.12;

        if (yerlesim == "Grid")
        {
            double gy = satir.EfektifGridY(oda.Id) ?? gridM;
            int n = geometri.IzgaraAdaylariUret(
                oda.Poligon, Math.Max(0.5, gridM), Math.Max(0.5, gy), armaturDuvarMin).Count;
            return Math.Max(1, n); // küçük oda güvencesi: en az 1 (OdayaYerlestir ile aynı)
        }

        if (yerlesim == "Duvar aralıklı")
        {
            int n = geometri.DuvarAdaylariUret(
                oda.Poligon, Math.Max(0.15, duvarM), duvarGiris).Count;
            return Math.Max(1, n);
        }

        return Math.Max(0, ayar.Adet);
    }

    /// Satırın kat genelindeki tahmini toplamı (aktif + dahil odalar).
    public static int SatirTahminToplami(
        BpcMahalSatiri satir, IEnumerable<BasitProjeOda> odalar, BasitProjeCizGeometriServisi geometri)
        => odalar.Sum(o => YerlesimTahmini(satir, o, geometri));

    /// Katın tahmini toplam gücü (W) — pano yükü öngörüsü. Sembol gücü
    /// kütüphaneden, oda-özel güç varsa hücreden okunur.
    public static double KatTahminiGucW(
        BasitProjeKat kat, BasitProjeCizKutuphaneServisi kutuphane, BasitProjeCizGeometriServisi geometri,
        Func<BpcElemanTipi, bool>? tipFiltresi = null)
    {
        double toplam = 0;
        foreach (var satir in kat.MahalListesi)
        {
            if (tipFiltresi != null && !tipFiltresi(satir.Tip)) continue;
            var tanim = satir.ArmatürTanimId is null
                ? null
                : BasitProjeCizOtomatikYerlestirServisi.TanimBul(satir.ArmatürTanimId, kutuphane);
            double sembolGuc = tanim?.GucW ?? 0;

            foreach (var oda in kat.Odalar)
            {
                int adet = YerlesimTahmini(satir, oda, geometri);
                if (adet == 0) continue;
                double guc = satir.EfektifGucW(oda.Id) ?? sembolGuc;
                toplam += guc * adet;
            }
        }
        return toplam;
    }

    // ─── 3) Mahal şablonları ──────────────────────────────────────────────

    /// Diske yazılan şablon: satırların genel ayarları + oda ADI bazlı hücreler.
    /// Oda Id'leri projeye özgü olduğu için şablonda AD saklanır.
    public sealed class MahalSablonu
    {
        public string Ad { get; set; } = "";
        public string? Aciklama { get; set; }
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public List<SablonSatir> Satirlar { get; set; } = new();
    }

    public sealed class SablonSatir
    {
        public BpcElemanTipi Tip { get; set; }
        public string? ArmatürTanimId { get; set; }
        public string? ArmatürTanimAd { get; set; }
        public string YerlesimTipi { get; set; } = "Adet";
        public string MontajTipi { get; set; } = "Tavan";
        public double GridAraligiM { get; set; } = 3.0;
        public double? GridAraligiYM { get; set; }
        public double DuvarAraligiM { get; set; } = 3.0;
        public double? GelenGucW { get; set; }
        /// Oda ADI (normalize edilmemiş, görüntü için) → hücre ayarı.
        public Dictionary<string, BpcMahalHucreAyar> OdaAdiHucreleri { get; set; } = new();
    }

    public sealed class SablonDosyasi
    {
        public int Surum { get; set; } = 1;
        public List<MahalSablonu> Sablonlar { get; set; } = new();
        /// Kullanıcının genişlettiği oda-tipi öneri tablosu (boşsa gömülü tablo kullanılır).
        public List<OdaTipiOnerisi> OdaTipiOnerileri { get; set; } = new();
    }

    private static readonly JsonSerializerOptions SablonJson = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static string SablonDosyaYolu()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Eye4Pro", "Electric", "BasitProjeCiz");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "mahal-sablonlari.json");
    }

    public static SablonDosyasi SablonlariYukle(string? yol = null)
    {
        yol ??= SablonDosyaYolu();
        if (!File.Exists(yol)) return new SablonDosyasi();
        try
        {
            return JsonSerializer.Deserialize<SablonDosyasi>(File.ReadAllText(yol), SablonJson)
                   ?? new SablonDosyasi();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[M12] Mahal şablonu okunamadı: {ex.Message}");
            return new SablonDosyasi();
        }
    }

    /// Atomik yazım (.tmp + Replace/Move) — yarım yazımda mevcut dosya bozulmaz.
    public static bool SablonlariKaydet(SablonDosyasi dosya, string? yol = null)
    {
        yol ??= SablonDosyaYolu();
        var gecici = yol + ".tmp";
        try
        {
            File.WriteAllText(gecici, JsonSerializer.Serialize(dosya, SablonJson));
            if (File.Exists(yol)) File.Replace(gecici, yol, null);
            else File.Move(gecici, yol);
            return true;
        }
        catch (Exception ex)
        {
            try { if (File.Exists(gecici)) File.Delete(gecici); } catch { /* yut */ }
            System.Diagnostics.Debug.WriteLine($"[M12] Mahal şablonu yazılamadı: {ex.Message}");
            return false;
        }
    }

    /// Kattaki mahal listesini şablona dönüştürür (yalnız aktif hücreler saklanır).
    public static MahalSablonu SablonaDonustur(BasitProjeKat kat, string ad, string? aciklama = null,
        Func<BpcElemanTipi, bool>? tipFiltresi = null)
    {
        var sablon = new MahalSablonu { Ad = ad, Aciklama = aciklama };
        var odaAdById = kat.Odalar.ToDictionary(o => o.Id, o => o.Ad);

        foreach (var satir in kat.MahalListesi)
        {
            if (tipFiltresi != null && !tipFiltresi(satir.Tip)) continue;
            var ss = new SablonSatir
            {
                Tip = satir.Tip,
                ArmatürTanimId = satir.ArmatürTanimId,
                ArmatürTanimAd = satir.ArmatürTanimAd,
                YerlesimTipi = satir.YerlesimTipi,
                MontajTipi = satir.MontajTipi,
                GridAraligiM = satir.GridAraligiM,
                GridAraligiYM = satir.GridAraligiYM,
                DuvarAraligiM = satir.DuvarAraligiM,
                GelenGucW = satir.GelenGucW
            };
            foreach (var (odaId, ayar) in satir.HucreAyarlari)
            {
                if (!ayar.Aktif) continue;
                if (!odaAdById.TryGetValue(odaId, out var odaAd) || string.IsNullOrWhiteSpace(odaAd)) continue;
                ss.OdaAdiHucreleri[odaAd] = HucreKopyala(ayar);
            }
            sablon.Satirlar.Add(ss);
        }
        return sablon;
    }

    /// <summary>
    /// Şablonu kata uygular. Oda eşleme ADA göredir; eşleşmeyen oda atlanır.
    /// Kullanıcının aktif ettiği hücre ezilmez (KattanKopyala ile aynı sözleşme).
    /// </summary>
    public static KopyalamaSonucu SablonUygula(MahalSablonu sablon, BasitProjeKat kat,
        Func<BpcElemanTipi, bool>? tipFiltresi = null)
    {
        var odaByAd = new Dictionary<string, BasitProjeOda>();
        foreach (var o in kat.Odalar)
        {
            var k = OdaAdiNormalize(o.Ad);
            if (k.Length > 0 && !odaByAd.ContainsKey(k)) odaByAd[k] = o;
        }

        int eklenen = 0, guncellenen = 0;
        var eslesen = new HashSet<string>();
        var eslesmeyen = new HashSet<string>();

        foreach (var ss in sablon.Satirlar)
        {
            if (tipFiltresi != null && !tipFiltresi(ss.Tip)) continue;
            var satir = kat.MahalListesi.FirstOrDefault(s =>
                (!string.IsNullOrEmpty(ss.ArmatürTanimId) && s.ArmatürTanimId == ss.ArmatürTanimId) ||
                (string.IsNullOrEmpty(ss.ArmatürTanimId) && string.IsNullOrEmpty(s.ArmatürTanimId) && s.Tip == ss.Tip));

            bool yeni = satir is null;
            if (satir is null)
            {
                satir = new BpcMahalSatiri
                {
                    Tip = ss.Tip,
                    ArmatürTanimId = ss.ArmatürTanimId,
                    ArmatürTanimAd = ss.ArmatürTanimAd,
                    YerlesimTipi = ss.YerlesimTipi,
                    MontajTipi = ss.MontajTipi,
                    GridAraligiM = ss.GridAraligiM,
                    GridAraligiYM = ss.GridAraligiYM,
                    DuvarAraligiM = ss.DuvarAraligiM,
                    GelenGucW = ss.GelenGucW
                };
                kat.MahalListesi.Add(satir);
                eklenen++;
            }
            else guncellenen++;

            foreach (var (odaAd, ayar) in ss.OdaAdiHucreleri)
            {
                var anahtar = OdaAdiNormalize(odaAd);
                if (anahtar.Length == 0 || !odaByAd.TryGetValue(anahtar, out var oda))
                {
                    eslesmeyen.Add(odaAd);
                    continue;
                }
                eslesen.Add(odaAd);

                var mevcut = satir.HucreGetirVeyaOlustur(oda.Id);
                if (mevcut.Aktif && !yeni) continue;
                satir.HucreAyarlari[oda.Id] = HucreKopyala(ayar);
            }
        }

        return new KopyalamaSonucu(eklenen, guncellenen, eslesen.Count, eslesmeyen.Count);
    }
}
