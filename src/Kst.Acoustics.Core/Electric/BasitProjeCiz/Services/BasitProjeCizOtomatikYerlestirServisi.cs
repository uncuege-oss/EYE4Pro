using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

public class BasitProjeCizOtomatikYerlestirServisi
{
    public const string MahalListesiKaynak = "MahalListesi";

    private const double DuvarGiris          = 0.12;
    private const double AciklikKoruma       = 0.40;
    private const double ElemanMinAralik     = 0.25;
    private const double ArmatürDuvarMin     = 0.50;
    private const double DuvarTaramaAdimi   = 0.30;
    // Aydınlatma buatı, beslediği anahtarların üstüne binmemesi için duvardan
    // bu kadar oda içine çekilir (anahtar DuvarGiris=0.12 m'de durur).
    private const double BuatDuvarOfset      = 0.45;

    private readonly BasitProjeCizGeometriServisi _geometri;

    public BasitProjeCizOtomatikYerlestirServisi(BasitProjeCizGeometriServisi geometri)
        => _geometri = geometri;

    /// Güncel proje şema sürümü.
    /// 1: mahal-aktiflik semantiği · 2: M50 bina geometrisi bağlantı alanları.
    public const int GuncelSemaSurum = 2;

    /// Mahal-aktiflik migrasyonunun geldiği sürüm — kendi eşiğine karşı kapılanır,
    /// GuncelSemaSurum ilerlese de v1+ projelerde yeniden çalışmaz.
    private const int MahalAktiflikSemaSurum = 1;

    /// <summary>
    /// Sürüm 0 (eski) projeleri sürüm 1 semantiğine taşır. Eski davranışta genel
    /// ayarı Grid / "Duvar aralıklı" olan satırlar TÜM odalara otomatik yerleşirdi
    /// (EfektifAktif hücre Aktif'ine bakmadan true dönerdi). Yeni semantik yalnız
    /// Aktif=true hücreleri yerleştirir. Bu migrasyon, eski projeyi aynı görünüme
    /// taşımak için bu satırların genel-ayar kullanan hücrelerini Aktif yapar.
    /// </summary>
    /// <returns>Aktif edilen hücre sayısı.</returns>
    public static int MahalGridAktiflikMigrasyonu(BasitProjeCizProje proje)
    {
        if (proje.SemaSurum >= MahalAktiflikSemaSurum) return 0;

        int aktifEdilen = 0;
        foreach (var kat in proje.Katlar)
        {
            foreach (var satir in kat.MahalListesi)
            {
                if (satir.YerlesimTipi != "Grid" && satir.YerlesimTipi != "Duvar aralıklı")
                    continue;

                foreach (var oda in kat.Odalar)
                {
                    var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
                    if (ayar.GenelAyarKullan && !ayar.Aktif)
                    {
                        ayar.Aktif = true;
                        aktifEdilen++;
                    }
                }
            }
        }

        proje.SemaSurum = MahalAktiflikSemaSurum;
        return aktifEdilen;
    }

    // ── Sembol çözümleme ────────────────────────────────────────────────────

    public static BpcArmatürTanim? TanimBul(string tanimId, BasitProjeCizKutuphaneServisi kutuphane)
        => kutuphane.Armatürler .FirstOrDefault(t => t.Id == tanimId)
        ?? kutuphane.Anahtarlar .FirstOrDefault(t => t.Id == tanimId)
        ?? kutuphane.Prizler    .FirstOrDefault(t => t.Id == tanimId)
        ?? kutuphane.Klimalar   .FirstOrDefault(t => t.Id == tanimId)
        ?? kutuphane.Panolar    .FirstOrDefault(t => t.Id == tanimId);

    // ── Tek oda yerleştirme ──────────────────────────────────────────────────

    /// Bir odaya belirtilen sembolden <adet> kadar eleman yerleştirir.
    /// Dönen liste henüz kat.Elemanlar'a eklenmemiştir.
    public List<BasitProjeEleman> OdayaYerlestir(
        BasitProjeOda oda,
        BpcArmatürTanim tanim,
        string yerlesimTipi,
        string montajTipi,
        double gridAraligiM,
        double duvarAraligiM,
        int adetAdetYerlesimiIcin,
        List<BpcAciklik> acikliklar,
        IEnumerable<BasitProjeEleman> mevcutElemanlar,
        bool sadecaEksikleri,
        out string? uyari)
        => OdayaYerlestir(oda, tanim, yerlesimTipi, montajTipi, gridAraligiM, null,
            duvarAraligiM, adetAdetYerlesimiIcin, acikliklar, mevcutElemanlar, sadecaEksikleri, out uyari);

    /// Grid X ve Y aralıkları ayrı verilen sürüm. gridAraligiYM null ise X ile aynı (kare grid).
    public List<BasitProjeEleman> OdayaYerlestir(
        BasitProjeOda oda,
        BpcArmatürTanim tanim,
        string yerlesimTipi,
        string montajTipi,
        double gridAraligiM,
        double? gridAraligiYM,
        double duvarAraligiM,
        int adetAdetYerlesimiIcin,
        List<BpcAciklik> acikliklar,
        IEnumerable<BasitProjeEleman> mevcutElemanlar,
        bool sadecaEksikleri,
        out string? uyari)
    {
        double gridY = gridAraligiYM ?? gridAraligiM;
        uyari = null;
        var sonuc = new List<BasitProjeEleman>();
        if (oda.Poligon.Count < 3) return sonuc;

        var mevcutNoktalar = mevcutElemanlar.Select(e => new Point2D(e.X, e.Y)).ToList();
        var tip = BasitProjeCizKutuphaneServisi.TanimTipi(tanim);
        int MevcutAdet() => mevcutElemanlar.Count(e => e.OdaId == oda.Id && e.BlokAdi == tanim.BlokAdi
            && (tip != BpcElemanTipi.Sensör || !e.SensorKontrollu));

        List<Point2D> secilen = new();

        bool isPriz = BasitProjeCizKabloServisi.ElemanPrizMi(tip);
        bool isAdet = yerlesimTipi is "Adet";
        bool isAciklikYani = yerlesimTipi == "Açıklık Yanı";
        bool isHomojenDuvar = yerlesimTipi == "Homojen Duvar";
        // Priz için "Duvar aralıklı" de sayı zorunlu — konumlar aralığa göre, adet kullanıcıdan
        bool isDuvarAralikliPriz = isPriz && yerlesimTipi == "Duvar aralıklı";
        int eklenecek = (isAdet || isAciklikYani || isHomojenDuvar || isDuvarAralikliPriz)
            ? adetAdetYerlesimiIcin : 0;

        // Darbe kesici işaretlenmiş ise en az 2 adet oluşur.
        if (tip == BpcElemanTipi.DarbeAnahtari && isAdet)
        {
            eklenecek = Math.Max(2, eklenecek);
        }

        // Priz: Açıklık Yanı — Adet zorunlu; açıklıktan itibaren anahtar+buat sonrası duvar boyunca
        if (isAciklikYani)
        {
            if (eklenecek <= 0) return sonuc;
            if (sadecaEksikleri)
            {
                int mevcut = MevcutAdet();
                eklenecek = Math.Max(0, eklenecek - mevcut);
            }
            if (eklenecek <= 0) return sonuc;
            secilen = PrizAciklikYaniAdaylar(oda.Poligon, acikliklar, mevcutNoktalar, duvarAraligiM, eklenecek);
            if (secilen.Count < eklenecek)
            {
                int eksik = eklenecek - secilen.Count;
                secilen.AddRange(FallbackNoktalar(oda.Merkez, eksik, secilen, mevcutNoktalar));
                uyari = $"{oda.Ad}: {eksik} priz için uygun konum bulunamadı, yaklaşık yerleştirildi";
            }
        }
        // Priz: Homojen Duvar — tüm duvarlara eşit dağılım (kullanıcı adeti girer)
        else if (isHomojenDuvar || isDuvarAralikliPriz)
        {
            if (eklenecek <= 0) return sonuc;
            if (sadecaEksikleri)
            {
                int mevcut = MevcutAdet();
                eklenecek = Math.Max(0, eklenecek - mevcut);
            }
            if (eklenecek <= 0) return sonuc;
            var hamAdaylar = _geometri.DuvarAdaylariUret(oda.Poligon, Math.Max(0.15, duvarAraligiM), DuvarGiris);
            var filtreliAdaylar = hamAdaylar
                .Where(p => !_geometri.AciklikBolgesindeMi(p, acikliklar, AciklikKoruma))
                .ToList();
            secilen = AdaylardenSecDagit(filtreliAdaylar, mevcutNoktalar, eklenecek);
            if (secilen.Count < eklenecek)
            {
                int eksik = eklenecek - secilen.Count;
                secilen.AddRange(FallbackNoktalar(oda.Merkez, eksik, secilen, mevcutNoktalar));
                uyari = $"{oda.Ad}: {eksik} priz için uygun konum bulunamadı, yaklaşık yerleştirildi";
            }
        }
        else if (!isAdet)
        {
            if (yerlesimTipi == "Grid")
            {
                var adaylar = _geometri.IzgaraAdaylariUret(
                    oda.Poligon, Math.Max(0.5, gridAraligiM), Math.Max(0.5, gridY), ArmatürDuvarMin);
                eklenecek = adaylar.Count;
                secilen = adaylar;
            }
            else if (yerlesimTipi == "Duvar aralıklı")
            {
                var ham = _geometri.DuvarAdaylariUret(oda.Poligon, Math.Max(0.15, duvarAraligiM), DuvarGiris);
                var adaylar = ham.Where(p => !_geometri.AciklikBolgesindeMi(p, acikliklar, AciklikKoruma)).ToList();
                eklenecek = adaylar.Count;
                secilen = adaylar;
            }
            
            if (sadecaEksikleri)
            {
                int mevcut = MevcutAdet();
                if (mevcut >= eklenecek) eklenecek = 0;
                else eklenecek = eklenecek - mevcut;

                if (eklenecek > 0 && eklenecek < secilen.Count)
                    secilen = AdaylardenSecDagit(secilen, mevcutNoktalar, eklenecek);
            }

            // Küçük oda güvencesi: Grid/Duvar aralıklı yerleşimde hiç aday çıkmadıysa
            // (oda ızgara/duvar adımı + duvar payı için fazla küçük) ve bu blok odada
            // hiç yoksa en az 1 eleman oda içine (tercihen merkeze) yerleştirilir.
            if (secilen.Count == 0)
            {
                int mevcutBuBlok = MevcutAdet();
                if (mevcutBuBlok == 0)
                {
                    Point2D nokta;
                    if (PoligonYardimci.IsPointInside(oda.Merkez, oda.Poligon))
                        nokta = oda.Merkez;
                    else
                    {
                        var ic = IcMekanAdaylar(oda.Poligon, 1);
                        nokta = ic.Count > 0 ? ic[0] : oda.Merkez;
                    }
                    secilen.Add(nokta);
                    eklenecek = 1;
                    uyari = $"{oda.Ad}: oda küçük — 1 {tanim.Ad} odaya yerleştirildi";
                }
            }
        }

        if (!isAdet && !isAciklikYani && !isHomojenDuvar && !isDuvarAralikliPriz)
        {
            // Grid/Duvar aralıklı (armatür): eklenecek/secilen yukarıda set edildi
        }
        else if (isAdet)
        {
            if (eklenecek <= 0) return sonuc;
            if (sadecaEksikleri)
            {
                int mevcut = MevcutAdet();
                eklenecek = Math.Max(0, eklenecek - mevcut);
            }
            if (eklenecek <= 0) return sonuc;

            bool tavana = montajTipi == "Tavan" || tanim.MontajTipi == "Tavan";
            if (AnahtarMi(tip))
            {
                secilen = AnahtarYerlesim(oda.Poligon, acikliklar, eklenecek, mevcutNoktalar);
            }
            else if (tip == BpcElemanTipi.Armatür)
            {
                var adaylar = GridAdaylar(oda.Poligon, tanim, eklenecek);
                secilen = AdaylardenSecDagit(adaylar, mevcutNoktalar, eklenecek);
            }
            else
            {
                var adaylar = (tavana && !isPriz)
                    ? IcMekanAdaylar(oda.Poligon, eklenecek)
                    : DuvarAdaylar(oda.Poligon, acikliklar, eklenecek);
                secilen = AdaylardenSecDagit(adaylar, mevcutNoktalar, eklenecek);
            }

            if (secilen.Count < eklenecek)
            {
                int eksik = eklenecek - secilen.Count;
                secilen.AddRange(FallbackNoktalar(oda.Merkez, eksik, secilen, mevcutNoktalar));
                uyari = $"{oda.Ad}: {eksik} eleman için uygun konum bulunamadı, yaklaşık yerleştirildi";
            }
        }

        if (eklenecek <= 0) return sonuc;

        var pafta = isPriz ? BpcPaftaTipi.Priz : PaftaForBlok(tanim.BlokAdi);

        foreach (var p in secilen)
        {
            sonuc.Add(new BasitProjeEleman
            {
                Tip            = tip,
                Ad             = tanim.Ad,
                BlokAdi        = tanim.BlokAdi,
                ArmatürTanimId = tanim.Id,
                OdaId          = oda.Id,
                X              = p.X,
                Y              = p.Y,
                PaftaTipi      = pafta,
                MontajTipi     = montajTipi,
                YerlesimTipi   = yerlesimTipi,
                GridAraligiM   = gridAraligiM,
                GridAraligiYM  = gridAraligiYM,
                DuvarAraligiM  = duvarAraligiM,
                OtomatikYerlesimKaynak = MahalListesiKaynak
            });
        }

        return sonuc;
    }

    // ── Tüm mahal listesi orkestratörü ───────────────────────────────────────

    private static bool AsamaKalemiMi(BpcAydinlatmaAsamasi asama, BpcElemanTipi tip) => asama switch
    {
        BpcAydinlatmaAsamasi.Armatur => BasitProjeCizKabloServisi.ElemanArmaturMi(tip),
        BpcAydinlatmaAsamasi.Anahtar => BasitProjeCizKabloServisi.ElemanAnahtarMi(tip),
        BpcAydinlatmaAsamasi.Buat => tip == BpcElemanTipi.Buat,
        _ => false
    };

    public static bool MahalKalemiMi(BpcElemanTipi tip, BpcAydinlatmaAsamasi? asama, BpcPaftaTipi? pafta)
    {
        if (asama.HasValue) return AsamaKalemiMi(asama.Value, tip);
        return pafta switch
        {
            BpcPaftaTipi.Priz => BasitProjeCizKabloServisi.ElemanPrizMi(tip),
            BpcPaftaTipi.Mekanik => tip is BpcElemanTipi.KlimaIcUnite or BpcElemanTipi.KlimaDisUnite,
            null => true,
            _ => false
        };
    }

    /// <param name="tavaniAcikOdalar">5.2 kararı — M50 düşey birleşiminde TAVANI AÇIK
    /// olan oda Id'leri. Bu odalarda tavan montajlı otomatik yerleşim atlanır
    /// (fiziksel tavan yok); duvar montajlı yerleşim ve elle ekleme etkilenmez.</param>
    public (List<BasitProjeEleman> Yeni, List<string> Uyarilar) TumMahalListesindenYerlestir(
        BasitProjeKat kat,
        BasitProjeCizKutuphaneServisi kutuphane,
        bool sadecaEksikleri,
        IReadOnlySet<string>? tavaniAcikOdalar = null,
        BpcAydinlatmaAsamasi? asama = null, BpcPaftaTipi? pafta = null,
        BpcPrizAsamasi? prizAsamasi = null)
    {
        var yeni     = new List<BasitProjeEleman>();
        var uyarilar = new List<string>();

        if (prizAsamasi.HasValue)
        {
            if (prizAsamasi == BpcPrizAsamasi.Buat)
                OtomatikPrizBuatiTamamla(kat, yeni, uyarilar, mevcutUyelikleriGuncelle: false);
            if (prizAsamasi != BpcPrizAsamasi.Priz) return (yeni, uyarilar);
            pafta = BpcPaftaTipi.Priz;
        }

        if (asama == BpcAydinlatmaAsamasi.Buat)
        {
            OtomatikAydinlatmaBuatiTamamla(kat, yeni, mevcutUyelikleriGuncelle: false);
            return (yeni, uyarilar);
        }

        foreach (var satir in kat.MahalListesi)
        {
            if (!MahalKalemiMi(satir.Tip, asama, pafta)) continue;
            BpcArmatürTanim? tanim = null;

            if (satir.ArmatürTanimId is not null)
                tanim = TanimBul(satir.ArmatürTanimId, kutuphane);

            if (tanim is null)
            {
                // Zayıf akım tipleri için statik tanım dene
                tanim = BasitProjeCizKutuphaneServisi.ZayifAkimTanim(satir.Tip);
            }

            if (tanim is null)
            {
                // Kuvvetli akım / diğer tipler için kütüphaneden varsayılan el ile tanım dene
                tanim = kutuphane.BulManuelTanim(satir.Tip);
            }

            if (tanim is null)
            {
                uyarilar.Add($"Satır ({satir.Tip}): sembol tanımı yok, atlandı");
                continue;
            }

            foreach (var oda in kat.Odalar)
            {
                if (!oda.MahalListesineDahil) continue;
                
                if (!satir.HucreAyarlari.ContainsKey(oda.Id) && !satir.Sayilar.ContainsKey(oda.Id)) continue;
                var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
                if (!satir.EfektifAktif(oda.Id)) continue;

                var (yerlesim, montaj, gridM, duvarM) = satir.EfektifAyarlar(oda.Id);
                var gridYM = satir.EfektifGridY(oda.Id);
                // Duvar anahtarlarında eski projelerde farklı bir yerleşim kuralı
                // kayıtlı olsa da adet ve kapı yanı yerleşimi bu adımda geçerlidir.
                int adet = ayar.Adet;
                if (asama == BpcAydinlatmaAsamasi.Anahtar)
                {
                    if (yerlesim != "Adet" && adet == 0) adet = 1;
                    yerlesim = "Adet";
                    montaj = satir.Tip == BpcElemanTipi.Sensör ? "Tavan" : "Duvar";
                }

                // 5.2 — Patlatılmış (tavanı açık) odada tavan montajlı otomatik
                // yerleşim yapılmaz; elemanlar üst odanın tavanından servis eder.
                bool tavanMontaj = montaj == "Tavan" || tanim.MontajTipi == "Tavan";
                if (tavanMontaj && tavaniAcikOdalar is not null && tavaniAcikOdalar.Contains(oda.Id))
                {
                    uyarilar.Add($"{oda.Ad}: tavanı açık (düşey birleşim) — tavan montajlı '{tanim.Ad}' otomatik yerleştirilmedi");
                    continue;
                }

                var placed = OdayaYerlestir(
                    oda, tanim, yerlesim, montaj, gridM, gridYM, duvarM, adet, kat.Acikliklar,
                    kat.Elemanlar.Concat(yeni),
                    sadecaEksikleri || asama.HasValue || prizAsamasi.HasValue,
                    out string? uyari);

                foreach (var eleman in placed)
                    eleman.OtomatikYerlesimGrupIds = [satir.Id];

                OdaOzelOzellikleriUygula(placed, ayar, satir.EfektifGucW(oda.Id));

                // Sensör kontrollü hücre: yalnız armatür satırlarında ve oda-özel ayar
                // modunda. Her armatüre ayrı bir sensör eşlik eder; armatürler duvar
                // anahtarı almaz.
                if (satir.EfektifSensorlu(oda.Id) && BasitProjeCizKabloServisi.ElemanArmaturMi(satir.Tip))
                {
                    foreach (var armatur in placed) armatur.SensorKontrollu = true;
                    if (!asama.HasValue) yeni.AddRange(SensorleriEsleEkle(placed, oda));
                }
                yeni.AddRange(placed);

                if (uyari is not null) uyarilar.Add(uyari);
            }
        }

        if (asama == BpcAydinlatmaAsamasi.Anahtar)
            EksikArmaturSensorleriniEkle(kat, yeni, tavaniAcikOdalar);
        if (asama == BpcAydinlatmaAsamasi.Anahtar || (asama is null && pafta is null))
            OtomatikAnahtarTamamla(kat, yeni, uyarilar);
        if (asama is null && pafta is null)
            OtomatikAydinlatmaBuatiTamamla(kat, yeni);
        if (!prizAsamasi.HasValue && asama is null && pafta is null or BpcPaftaTipi.Priz)
            OtomatikPrizBuatiTamamla(kat, yeni, uyarilar);

        return (yeni, uyarilar);
    }

    private static void EksikArmaturSensorleriniEkle(
        BasitProjeKat kat, List<BasitProjeEleman> yeni, IReadOnlySet<string>? tavaniAcikOdalar)
    {
        foreach (var oda in kat.Odalar.Where(o => o.MahalListesineDahil))
        {
            if (tavaniAcikOdalar?.Contains(oda.Id) == true) continue;
            var armaturler = kat.Elemanlar.Where(e => e.OdaId == oda.Id && e.SensorKontrollu &&
                BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip)).ToList();
            foreach (var sensor in kat.Elemanlar.Where(e =>
                e.OdaId == oda.Id && e.Tip == BpcElemanTipi.Sensör && e.SensorKontrollu))
            {
                if (armaturler.Count == 0) break;
                var enYakin = armaturler.MinBy(a => Math.Pow(a.X - sensor.X, 2) + Math.Pow(a.Y - sensor.Y, 2));
                armaturler.Remove(enYakin!);
            }
            yeni.AddRange(SensorleriEsleEkle(armaturler, oda));
        }
    }

    /// Her armatürü SensorKontrollu işaretler ve hemen yanına bir sensör elemanı
    /// üretir (sensör de SensorKontrollu). Sensör, armatürün biraz yanına (oda
    /// merkezine doğru) ofsetlenir ki çizimde her ikisi de görünsün.
    private static List<BasitProjeEleman> SensorleriEsleEkle(
        List<BasitProjeEleman> armaturler, BasitProjeOda oda)
    {
        const double sensorOfset = 0.20;
        var merkez = PoligonMerkez(oda.Poligon);
        var sensorler = new List<BasitProjeEleman>();

        foreach (var arm in armaturler)
        {
            // Armatürden oda merkezine doğru küçük bir ofset
            double dx = merkez.X - arm.X, dy = merkez.Y - arm.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            double ox, oy;
            if (len > 1e-6) { ox = dx / len * sensorOfset; oy = dy / len * sensorOfset; }
            else { ox = sensorOfset; oy = 0; }

            sensorler.Add(new BasitProjeEleman
            {
                Id        = Guid.NewGuid().ToString(),
                Tip       = BpcElemanTipi.Sensör,
                Ad        = "Sensör",
                EkranAdi  = "S",
                BlokAdi   = "E4P_SENSOR",
                PaftaTipi = BpcPaftaTipi.Aydinlatma,
                MontajTipi = "Tavan",
                OdaId     = oda.Id,
                X         = arm.X + ox,
                Y         = arm.Y + oy,
                SensorKontrollu = true,
                OtomatikYerlesimKaynak = MahalListesiKaynak,
                OtomatikYerlesimGrupIds = (arm.OtomatikYerlesimGrupIds ?? new()).ToList()
            });
        }
        return sensorler;
    }

    /// Aydınlatma anahtarları oluştuktan sonra anahtar sayısına göre buat üretir:
    /// her 4 anahtar için 1 buat. Buatlar, ait oldukları anahtar grubunun yanına
    /// (grubun ağırlık merkezinden oda içine doğru ofsetli) yerleştirilir.
    private static void OtomatikAydinlatmaBuatiTamamla(BasitProjeKat kat, List<BasitProjeEleman> yeni,
        bool mevcutUyelikleriGuncelle = true)
    {
        const int AnahtarBasinaBuat = 4;

        var tumElemanlar = kat.Elemanlar.Concat(yeni).ToList();

        foreach (var oda in kat.Odalar)
        {
            if (!oda.MahalListesineDahil) continue;

            // Her duvar anahtarı ve tavan sensörü bir kumanda noktası olarak sayılır.
            var odadakiAnahtarlar = tumElemanlar
                .Where(e => e.OdaId == oda.Id &&
                            BasitProjeCizKabloServisi.ElemanAnahtarMi(e.Tip))
                .OrderBy(e => e.X).ThenBy(e => e.Y)
                .ToList();
            if (odadakiAnahtarlar.Count == 0) continue;

            int anahtarSayisi = odadakiAnahtarlar.Count;
            int gerekenBuat = (int)Math.Ceiling(anahtarSayisi / (double)AnahtarBasinaBuat);
            // Yalnız AYDINLATMA buatları sayılır — priz buatları (PaftaTipi=Priz)
            // sayıma girerse ikinci çalıştırmada aydınlatma buatı eksik üretilir.
            var odaGrupIds = tumElemanlar
                .Where(e => e.OdaId == oda.Id &&
                            (BasitProjeCizKabloServisi.ElemanAnahtarMi(e.Tip) ||
                             BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip)))
                .SelectMany(e => e.OtomatikYerlesimGrupIds ?? new())
                .Distinct()
                .ToList();

            var mevcutBuatlar = tumElemanlar.Where(e => e.OdaId == oda.Id &&
                e.Tip == BpcElemanTipi.Buat && e.PaftaTipi == BpcPaftaTipi.Aydinlatma).ToList();
            foreach (var buat in mevcutBuatlar.Where(b => mevcutUyelikleriGuncelle && b.OtomatikYerlesimKaynak == MahalListesiKaynak))
            {
                buat.OtomatikYerlesimGrupIds ??= new();
                foreach (var grupId in odaGrupIds)
                    if (!buat.OtomatikYerlesimGrupIds.Contains(grupId))
                        buat.OtomatikYerlesimGrupIds.Add(grupId);
            }
            int mevcutBuat = mevcutBuatlar.Count;
            if (gerekenBuat <= mevcutBuat) continue;

            var merkez = PoligonMerkez(oda.Poligon);

            // Anahtarları buatlara ORANTILI (dengeli) dağıt: 5 anahtar → 3-2 (4-1 değil).
            int baz = anahtarSayisi / gerekenBuat;
            int kalan = anahtarSayisi % gerekenBuat;

            for (int i = mevcutBuat; i < gerekenBuat; i++)
            {
                // i. grup: dengeli dilim (ilk 'kalan' grup birer fazla alır)
                int basla = i * baz + Math.Min(i, kalan);
                int adet = baz + (i < kalan ? 1 : 0);
                int bitir = basla + adet;
                double gx = 0, gy = 0; int n = 0;
                for (int k = basla; k < bitir && k < anahtarSayisi; k++) { gx += odadakiAnahtarlar[k].X; gy += odadakiAnahtarlar[k].Y; n++; }
                if (n == 0) continue;
                gx /= n; gy /= n;

                // Centroid'i en yakın duvar segmentine izdüş → duvar üzerindeki noktayı bul.
                // Oradan BuatDuvarOfset kadar oda içine çek (anahtarın üstüne binmesin).
                var (wallX, wallY, wux, wuy) = EnYakinDuvarNoktasi(oda.Poligon, gx, gy);
                double wnx = -wuy, wny = wux;
                double tox2 = merkez.X - wallX, toy2 = merkez.Y - wallY;
                if (wnx * tox2 + wny * toy2 < 0) { wnx = -wnx; wny = -wny; }
                double bx = wallX + wnx * BuatDuvarOfset;
                double by = wallY + wny * BuatDuvarOfset;

                // Çakışma önleme: mevcut eleman (anahtar/buat) üstüne denk gelirse
                // oda içine doğru adım adım kaydır, poligon dışına çıkma.
                (bx, by) = CakismayiGider(bx, by, wnx, wny, oda.Poligon, tumElemanlar);

                var buat = new BasitProjeEleman
                {
                    Id        = Guid.NewGuid().ToString(),
                    Tip       = BpcElemanTipi.Buat,
                    Ad        = "Buat",
                    EkranAdi  = "B",
                    BlokAdi   = "E4P_BUAT",
                    PaftaTipi = BpcPaftaTipi.Aydinlatma,
                    MontajTipi = "Tavan",
                    OdaId     = oda.Id,
                    X         = bx,
                    Y         = by,
                    OtomatikYerlesimKaynak = MahalListesiKaynak,
                    OtomatikYerlesimGrupIds = odaGrupIds.ToList()
                };
                yeni.Add(buat);
                tumElemanlar.Add(buat);
            }
        }
    }

    /// Mahal listesi hücresinde girilen oda-özel değerleri (ad, watt, tip-özel
    /// alanlar) yerleştirilen elemanlara uygular. Boş/null değerler sembol
    /// varsayılanını korur.
    private static void OdaOzelOzellikleriUygula(List<BasitProjeEleman> elemanlar, BpcMahalHucreAyar ayar, double? efektifGucW = null)
    {
        foreach (var el in elemanlar)
        {
            if (!string.IsNullOrWhiteSpace(ayar.OzelAd)) el.Ad = ayar.OzelAd;
            if (efektifGucW.HasValue)          el.CustomGucW    = efektifGucW;
            else if (ayar.OzelGucW.HasValue)   el.CustomGucW    = ayar.OzelGucW;
            if (ayar.OzelMonofazeAdet.HasValue) el.MonofazeAdet = ayar.OzelMonofazeAdet;
            if (ayar.OzelTrifazeAdet.HasValue)  el.TrifazeAdet  = ayar.OzelTrifazeAdet;
            if (ayar.OzelMegapiksel.HasValue)   el.Megapiksel   = ayar.OzelMegapiksel;
            if (ayar.OzelCihazAdet.HasValue)    el.CustomAdet   = ayar.OzelCihazAdet;
        }
    }

    /// Her 7 buat-gerektiren priz için 1 priz buatı (PaftaTipi=Priz) üretir.
    /// Müstakil/trifaze/kombine/UPS prizler buat almaz.
    private void OtomatikPrizBuatiTamamla(BasitProjeKat kat, List<BasitProjeEleman> yeni, List<string> uyarilar,
        bool mevcutUyelikleriGuncelle = true)
    {
        var tumElemanlar = kat.Elemanlar.Concat(yeni).ToList();

        foreach (var oda in kat.Odalar)
        {
            if (!oda.MahalListesineDahil) continue;

            var buatGerektirenPrizler = tumElemanlar
                .Where(e => e.OdaId == oda.Id &&
                            BasitProjeCizKabloServisi.ElemanPrizMi(e.Tip) &&
                            !BasitProjeCizKabloServisi.ElemanPrizDirektPanoMu(e))
                .OrderBy(e => e.X).ThenBy(e => e.Y)
                .ToList();

            if (buatGerektirenPrizler.Count == 0) continue;

            int gerekenBuat = (int)Math.Ceiling(buatGerektirenPrizler.Count / (double)BasitProjeCizKabloServisi.PrizBuatKapasitesi);
            var odaGrupIds = buatGerektirenPrizler
                .SelectMany(e => e.OtomatikYerlesimGrupIds ?? new())
                .Distinct()
                .ToList();

            var mevcutPrizBuatlar = tumElemanlar
                .Where(e => e.OdaId == oda.Id && e.Tip == BpcElemanTipi.Buat &&
                            e.PaftaTipi == BpcPaftaTipi.Priz)
                .ToList();
            foreach (var buat in mevcutPrizBuatlar.Where(b => mevcutUyelikleriGuncelle && b.OtomatikYerlesimKaynak == MahalListesiKaynak))
            {
                buat.OtomatikYerlesimGrupIds ??= new();
                foreach (var grupId in odaGrupIds)
                    if (!buat.OtomatikYerlesimGrupIds.Contains(grupId))
                        buat.OtomatikYerlesimGrupIds.Add(grupId);
            }
            int mevcutPrizBuat = mevcutPrizBuatlar.Count;
            if (gerekenBuat <= mevcutPrizBuat) continue;

            int baz = buatGerektirenPrizler.Count / gerekenBuat;
            int kalan = buatGerektirenPrizler.Count % gerekenBuat;
            // Duvar adayları poligonun sarım yönünden üretilir ve içeride oldukları doğrulanır.
            // İçbükey mahalde ağırlık merkezi duvarın yanlış tarafında kalabilir.
            var duvarAdaylari = _geometri.DuvarAdaylariUret(oda.Poligon, DuvarTaramaAdimi, BuatDuvarOfset);
            if (duvarAdaylari.Count == 0)
                duvarAdaylari = _geometri.DuvarAdaylariUret(oda.Poligon, DuvarTaramaAdimi, DuvarGiris);
            var doluNoktalar = tumElemanlar.Where(e => e.OdaId == oda.Id || e.OdaId == null)
                .Select(e => new Point2D(e.X, e.Y)).ToList();
            duvarAdaylari.RemoveAll(p => doluNoktalar.Any(dolu => Mesafe(dolu, p) < ElemanMinAralik));

            for (int i = mevcutPrizBuat; i < gerekenBuat; i++)
            {
                int basla = i * baz + Math.Min(i, kalan);
                int adet = baz + (i < kalan ? 1 : 0);
                int bitir = basla + adet;
                double gx = 0, gy = 0; int n = 0;
                for (int k = basla; k < bitir && k < buatGerektirenPrizler.Count; k++)
                    { gx += buatGerektirenPrizler[k].X; gy += buatGerektirenPrizler[k].Y; n++; }
                if (n == 0) continue;
                gx /= n; gy /= n;

                if (duvarAdaylari.Count == 0)
                {
                    uyarilar.Add($"{oda.Ad}: {gerekenBuat - i} priz buatı için mahal içinde boş yer bulunamadı. Konumu elle belirleyin.");
                    break;
                }
                var grupMerkezi = new Point2D(gx, gy);
                var konum = duvarAdaylari.MinBy(p => Mesafe(p, grupMerkezi));
                duvarAdaylari.RemoveAll(p => Mesafe(p, konum) < ElemanMinAralik);

                var buat = new BasitProjeEleman
                {
                    Id = Guid.NewGuid().ToString(),
                    Tip = BpcElemanTipi.Buat,
                    Ad = "Priz Buatı",
                    EkranAdi = "PB",
                    BlokAdi = "E4P_BUAT",
                    PaftaTipi = BpcPaftaTipi.Priz,  // Aydınlatma buatından bu şekilde ayrışır
                    MontajTipi = "Tavan",
                    OdaId = oda.Id,
                    X = konum.X,
                    Y = konum.Y,
                    OtomatikYerlesimKaynak = MahalListesiKaynak,
                    OtomatikYerlesimGrupIds = odaGrupIds.ToList()
                };
                yeni.Add(buat);
                tumElemanlar.Add(buat);
            }
        }
    }

    // Alan-ağırlıklı centroid (ortak yardımcı). Buatı oda içine çekerken kullanılır.
    private static Point2D PoligonMerkez(IReadOnlyList<Point2D> poligon)
        => poligon == null ? new Point2D(0, 0) : PoligonYardimci.Centroid(poligon);

    private static int KapasiteHesapla(BpcElemanTipi tip) => tip switch {
        BpcElemanTipi.Komütatör => 18,
        BpcElemanTipi.UcluKomutator => 27,
        BpcElemanTipi.Vavien => 9,
        BpcElemanTipi.DarbeAnahtari => int.MaxValue,
        BpcElemanTipi.KomütatörVavien => 18,
        _ => 9
    };

    /// Centroid (x,y)'yi en yakın duvar segmentine izdüş eder.
    /// Döner: (duvar üzerindeki nokta X, Y, segment tanjant Ux, Uy).
    private static (double WallX, double WallY, double Ux, double Uy) EnYakinDuvarNoktasi(
        List<Point2D> poligon, double x, double y)
    {
        if (poligon.Count < 2) return (x, y, 1.0, 0.0);
        double minDist = double.MaxValue;
        double bestPx = x, bestPy = y, bestUx = 1.0, bestUy = 0.0;

        for (int i = 0; i < poligon.Count; i++)
        {
            var a = poligon[i];
            var b = poligon[(i + 1) % poligon.Count];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double l2 = dx * dx + dy * dy;
            double t = l2 < 1e-12 ? 0.0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / l2, 0.0, 1.0);
            double px = a.X + t * dx, py = a.Y + t * dy;
            double d = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
            if (d < minDist)
            {
                minDist = d;
                bestPx = px; bestPy = py;
                double len = Math.Sqrt(l2);
                bestUx = len > 1e-6 ? dx / len : 1.0;
                bestUy = len > 1e-6 ? dy / len : 0.0;
            }
        }
        return (bestPx, bestPy, bestUx, bestUy);
    }

    private static (double Ux, double Uy) EnYakinDuvarVektoru(List<Point2D> poligon, double x, double y)
    {
        if (poligon.Count < 2) return (1.0, 0.0);
        double minDist = double.MaxValue;
        Point2D bestA = poligon[0], bestB = poligon[1];
        var p = new Point2D(x, y);

        for (int i = 0; i < poligon.Count; i++)
        {
            var a = poligon[i];
            var b = poligon[(i + 1) % poligon.Count];
            double l2 = (b.X - a.X)*(b.X - a.X) + (b.Y - a.Y)*(b.Y - a.Y);
            double d;
            if (l2 == 0) d = Math.Sqrt((p.X - a.X)*(p.X - a.X) + (p.Y - a.Y)*(p.Y - a.Y));
            else {
                double t = Math.Max(0, Math.Min(1, ((p.X - a.X)*(b.X - a.X) + (p.Y - a.Y)*(b.Y - a.Y)) / l2));
                double projX = a.X + t * (b.X - a.X);
                double projY = a.Y + t * (b.Y - a.Y);
                d = Math.Sqrt((p.X - projX)*(p.X - projX) + (p.Y - projY)*(p.Y - projY));
            }

            if (d < minDist)
            {
                minDist = d;
                bestA = a;
                bestB = b;
            }
        }

        double dx = bestB.X - bestA.X;
        double dy = bestB.Y - bestA.Y;
        double len = Math.Sqrt(dx*dx + dy*dy);
        if (len < 1e-6) return (1.0, 0.0);
        return (dx / len, dy / len);
    }

    private void OtomatikAnahtarTamamla(
        BasitProjeKat kat,
        List<BasitProjeEleman> yeni,
        List<string> uyarilar)
    {
        var tumElemanlar = kat.Elemanlar.Concat(yeni).ToList();

        foreach (var oda in kat.Odalar)
        {
            if (!oda.MahalListesineDahil) continue;

            // Sensör kontrollü armatürler duvar anahtarı almaz — gruplamadan çıkar.
            var odadakiArmaturler = tumElemanlar.Where(e => e.OdaId == oda.Id && BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip) && !e.SensorKontrollu).ToList();
            if (odadakiArmaturler.Count == 0) continue;

            var gruplar = odadakiArmaturler.GroupBy(a => a.BlokAdi).ToList();
            var odadakiAnahtarlar = tumElemanlar.Where(e => e.OdaId == oda.Id && !e.SensorKontrollu &&
                BasitProjeCizKabloServisi.ElemanAnahtarMi(e.Tip)).ToList();

            if (odadakiAnahtarlar.Count == 0)
            {
                uyarilar.Add($"{oda.Ad} odasında {odadakiArmaturler.Count} adet aydınlatma armatürü var ancak hiç anahtar atanmamış! Lütfen anahtar ekleyin.");
            }
            else
            {
                if (odadakiAnahtarlar.Count < gruplar.Count)
                {
                    uyarilar.Add($"{oda.Ad} odasında {gruplar.Count} farklı tipte armatür varken sadece {odadakiAnahtarlar.Count} anahtar var.");
                }

                int anahtarIdx = 0;
                foreach (var grup in gruplar)
                {
                    var anahtar = odadakiAnahtarlar[Math.Min(anahtarIdx, odadakiAnahtarlar.Count - 1)];
                    int armaturSayisi = grup.Count();
                    int kapasite = KapasiteHesapla(anahtar.Tip);
                    int gerekenAdet = (int)Math.Ceiling(armaturSayisi / (double)kapasite);
                    
                    if (anahtar.Tip == BpcElemanTipi.DarbeAnahtari && gerekenAdet < 2) 
                        gerekenAdet = 2;

                    var (ux, uy) = EnYakinDuvarVektoru(oda.Poligon, anahtar.X, anahtar.Y);
                    int mevcutAileAdedi = odadakiAnahtarlar.Count(a =>
                        a.OdaId == anahtar.OdaId &&
                        a.Tip == anahtar.Tip &&
                        string.Equals(a.BlokAdi, anahtar.BlokAdi, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(a.Ad, anahtar.Ad, StringComparison.OrdinalIgnoreCase));

                    for (int i = mevcutAileAdedi; i < gerekenAdet; i++)
                    {
                        var kopya = new BasitProjeEleman
                        {
                            Id = Guid.NewGuid().ToString(),
                            OdaId = anahtar.OdaId,
                            Ad = anahtar.Ad,
                            BlokAdi = anahtar.BlokAdi,
                            Tip = anahtar.Tip,
                            EkranAdi = anahtar.EkranAdi,
                            CustomAdet = anahtar.CustomAdet,
                            PaftaTipi = anahtar.PaftaTipi,
                            MontajTipi = anahtar.MontajTipi,
                            OtomatikYerlesimKaynak = MahalListesiKaynak,
                            OtomatikYerlesimGrupIds = (anahtar.OtomatikYerlesimGrupIds ?? new()).ToList(),
                            X = anahtar.X + (ux * i * 0.15),
                            Y = anahtar.Y + (uy * i * 0.15)
                        };
                        yeni.Add(kopya);
                        odadakiAnahtarlar.Add(kopya);
                    }
                    anahtarIdx++;
                }
            }
        }
    }

    // ── Priz: Açıklık Yanı yerleşimi ─────────────────────────────────────────

    /// Kapı açıklığından itibaren, mevcut duvar elemanlarından (anahtar+buat) sonra başlar
    /// ve kullanıcının duvar aralığıyla devam eder. Tüm duvarları dolaşır.
    private List<Point2D> PrizAciklikYaniAdaylar(
        List<Point2D> poligon,
        IEnumerable<BpcAciklik> acikliklar,
        IEnumerable<Point2D> mevcutlar,
        double aralikM,
        int istenen)
    {
        const double giris = 0.10;
        double adim = Math.Max(0.15, aralikM);

        // Tüm duvar noktaları (açıklık bölgesi hariç)
        var tumDuvar = _geometri.DuvarAdaylariUret(poligon, adim, giris)
            .Where(p => !_geometri.AciklikBolgesindeMi(p, acikliklar, AciklikKoruma))
            .ToList();

        // Kapı açıklığına en yakın noktadan başlayarak sırala (Açıklık Yanı semantiği).
        // TÜM kapılar dikkate alınır; aday, en yakın kapıya olan uzaklığıyla sıralanır.
        var kapilariYakin = acikliklar
            .Where(a => !a.IptalEdildi && a.Tip == BpcAciklikTipi.Kapı && AciklikOdayaAitMi(a, poligon))
            .Select(a => new Point2D((a.Bas.X + a.Bit.X) / 2, (a.Bas.Y + a.Bit.Y) / 2))
            .ToList();

        Point2D baslangic = kapilariYakin.Count > 0
            ? kapilariYakin[0]
            : (poligon.Count > 0 ? poligon[0] : new Point2D(0, 0));

        // Mevcut duvar elemanlarının konumlarını "dolu" say
        var doluMesafe = mevcutlar.ToHashSet(new Point2DProximityComparer(adim * 0.5));

        var sira = tumDuvar
            .Where(p => !doluMesafe.Contains(p))
            .OrderBy(p => kapilariYakin.Count > 0
                ? kapilariYakin.Min(k => Mesafe(p, k))
                : Mesafe(p, baslangic))
            .ToList();

        // "Açıklık Yanı" = kapıya EN YAKIN boş adaylardan başlayarak sırayla seç.
        // (Eski AdaylardenSecDagit eşit-aralık index dağıttığı için prizler kapı
        // çevresine değil tüm duvara yayılıyordu.) Ardışık adaylar zaten duvar
        // adımı (aralıkM) kadar ayrık; ElemanMinAralik koruması da uygulanır.
        var mevcutList = mevcutlar.ToList();
        var secilen = new List<Point2D>();
        foreach (var p in sira)
        {
            if (secilen.Count >= istenen) break;
            if (mevcutList.Concat(secilen).Any(m => Mesafe(m, p) < ElemanMinAralik)) continue;
            secilen.Add(p);
        }
        return secilen;
    }

    private sealed class Point2DProximityComparer : IEqualityComparer<Point2D>
    {
        private readonly double _tol;
        public Point2DProximityComparer(double tol) => _tol = tol;
        public bool Equals(Point2D a, Point2D b) =>
            Math.Abs(a.X - b.X) < _tol && Math.Abs(a.Y - b.Y) < _tol;
        // DİKKAT: Yakınlık-tabanlı eşitlik geçişli DEĞİLdir; grid'e kuantalayan bir hash
        // hücre sınırındaki iki yakın noktaya farklı hash verip HashSet değişmezini bozar
        // (yakın nokta "boş" sayılır → çakışan yerleşim). Sabit hash bilinçli tercihtir:
        // Contains her adayı tüm mevcutlarla karşılaştırır (oda başına küçük N; kabul edilir).
        // Büyük N gerekirse çözüm 3×3 komşu hücre tarayan ayrı bir uzamsal grid olmalı.
        public int GetHashCode(Point2D p) => 0;
    }

    // ── Strateji A: İç mekân ızgara (tavan montajlı) ────────────────────────

    private List<Point2D> IcMekanAdaylar(List<Point2D> poligon, int istenen)
    {
        foreach (double adim in new[] { 1.0, 0.7, 0.5, 0.4, 0.3 })
        {
            var adaylar = _geometri.IzgaraAdaylariUret(poligon, adim, ArmatürDuvarMin);
            if (adaylar.Count >= istenen) return adaylar;
        }
        return _geometri.IzgaraAdaylariUret(poligon, 0.3, 0.2);
    }

    private List<Point2D> GridAdaylar(List<Point2D> poligon, BpcArmatürTanim tanim, int istenen)
    {
        double stepX = Math.Max(1.0, tanim.GridAraligiM);
        double stepY = Math.Max(1.0, tanim.GridAraligiYM ?? tanim.GridAraligiM);
        // Aday yetmezse her iki ekseni aynı oranla sıklaştır (en-boy oranı korunur).
        foreach (double oran in new[] { 1.0, 0.75, 0.5 })
        {
            var adaylar = _geometri.IzgaraAdaylariUret(
                poligon, Math.Max(0.5, stepX * oran), Math.Max(0.5, stepY * oran), ArmatürDuvarMin);
            if (adaylar.Count >= istenen) return adaylar;
        }
        return _geometri.IzgaraAdaylariUret(
            poligon, Math.Max(0.5, stepX * 0.5), Math.Max(0.5, stepY * 0.5), 0.2);
    }

    // ── Strateji B: Duvar yakını (duvar montajlı) ────────────────────────────

    private List<Point2D> DuvarAdaylar(
        List<Point2D> poligon,
        List<BpcAciklik> acikliklar,
        int istenen)
    {
        foreach (double adim in new[] { DuvarTaramaAdimi, 0.20, 0.15 })
        {
            var ham = _geometri.DuvarAdaylariUret(poligon, adim, DuvarGiris);
            var filtreli = ham
                .Where(p => !_geometri.AciklikBolgesindeMi(p, acikliklar, AciklikKoruma))
                .ToList();
            if (filtreli.Count >= istenen) return filtreli;
        }
        var son = _geometri.DuvarAdaylariUret(poligon, 0.15, DuvarGiris);
        return son.Where(p => !_geometri.AciklikBolgesindeMi(p, acikliklar, AciklikKoruma)).ToList();
    }

    // ── Strateji C: Anahtar/komütatör → açıklık yanı ────────────────────────

    private static bool AnahtarMi(BpcElemanTipi tip) =>
        tip != BpcElemanTipi.Sensör && BasitProjeCizKabloServisi.ElemanAnahtarMi(tip);

    /// Her anahtarı bu odanın duvarındaki bir kapı açıklığının hemen yanına koyar.
    /// Açıklık yetmezse genel duvar adaylarından dağıtarak tamamlar.
    private List<Point2D> AnahtarYerlesim(
        List<Point2D> poligon,
        List<BpcAciklik> acikliklar,
        int istenen,
        List<Point2D> mevcutlar)
    {
        var sonuc = new List<Point2D>();

        var odaAciklik = acikliklar
            .Where(a => !a.IptalEdildi && a.Tip == BpcAciklikTipi.Kapı && AciklikOdayaAitMi(a, poligon))
            .ToList();

        // Parçalanmış tek kapıya (çok yakın açıklık segmentleri) iki anahtar konmasın
        var benzersizAciklik = new List<BpcAciklik>();
        foreach (var a in odaAciklik)
        {
            var orta = new Point2D((a.Bas.X + a.Bit.X) * 0.5, (a.Bas.Y + a.Bit.Y) * 0.5);
            bool yakinVar = benzersizAciklik.Any(b =>
            {
                var bo = new Point2D((b.Bas.X + b.Bit.X) * 0.5, (b.Bas.Y + b.Bit.Y) * 0.5);
                return Mesafe(orta, bo) < 0.5;
            });
            if (!yakinVar) benzersizAciklik.Add(a);
        }

        const double yanOffset = 0.25; // açıklık kenarından duvar boyunca kayma (m)

        foreach (var a in benzersizAciklik)
        {
            if (sonuc.Count >= istenen) break;
            double len = Mesafe(a.Bas, a.Bit);
            if (len < 0.01) continue;
            double ux = (a.Bit.X - a.Bas.X) / len, uy = (a.Bit.Y - a.Bas.Y) / len;

            // Açıklığın iki ucundan birinin yanı (poligon içinde olan)
            var aday = AnahtarNoktaDene(poligon, a.Bit, ux, uy, yanOffset)
                    ?? AnahtarNoktaDene(poligon, a.Bas, -ux, -uy, yanOffset);

            if (aday is { } p &&
                !mevcutlar.Concat(sonuc).Any(m => Mesafe(m, p) < ElemanMinAralik))
                sonuc.Add(p);
        }

        if (sonuc.Count < istenen)
        {
            int eksik = istenen - sonuc.Count;
            var duvar = DuvarAdaylar(poligon, acikliklar, eksik);
            var doluNoktalar = mevcutlar.Concat(sonuc).ToList();

            // Oda kapısı varsa: fallback'te de anahtar kapı yanında kalsın. Adayları
            // kapıya yakınlığa göre sırala ve en yakın boş olanları seç (ortadan
            // seçen AdaylardenSecDagit'i kullanma; o ters duvara düşürebiliyor).
            var kapiOrtalari = benzersizAciklik
                .Select(a => new Point2D((a.Bas.X + a.Bit.X) * 0.5, (a.Bas.Y + a.Bit.Y) * 0.5))
                .ToList();

            if (kapiOrtalari.Count > 0)
            {
                foreach (var p in duvar.OrderBy(p => kapiOrtalari.Min(k => Mesafe(p, k))))
                {
                    if (sonuc.Count >= istenen) break;
                    if (!doluNoktalar.Concat(sonuc).Any(m => Mesafe(m, p) < ElemanMinAralik))
                        sonuc.Add(p);
                }
            }
            else
            {
                sonuc.AddRange(AdaylardenSecDagit(duvar, doluNoktalar, eksik));
            }
        }

        return sonuc;
    }

    /// Açıklık ucundan <offset> kadar duvar boyunca kayıp içeri DuvarGiris kadar girer.
    /// İki perpendikül normalden poligon içinde olanı döner, yoksa null.
    private static Point2D? AnahtarNoktaDene(
        List<Point2D> poligon, Point2D uc, double ux, double uy, double offset)
    {
        double bx = uc.X + ux * offset, by = uc.Y + uy * offset;
        foreach (var (nx, ny) in new[] { (-uy, ux), (uy, -ux) })
        {
            var p = new Point2D(bx + nx * DuvarGiris, by + ny * DuvarGiris);
            if (PoligonYardimci.IsPointInside(p, poligon)) return p;
        }
        return null;
    }

    /// Açıklığın orta noktası poligonun bir kenarına <tol> metreden yakınsa bu odaya aittir.
    private static bool AciklikOdayaAitMi(BpcAciklik a, List<Point2D> poligon, double tol = 0.6)
    {
        var orta = new Point2D((a.Bas.X + a.Bit.X) * 0.5, (a.Bas.Y + a.Bit.Y) * 0.5);
        for (int i = 0; i < poligon.Count; i++)
            if (PoligonYardimci.SegmentMesafesi(orta, poligon[i], poligon[(i + 1) % poligon.Count]) < tol)
                return true;
        return false;
    }

    // ── Seçim + fallback ─────────────────────────────────────────────────────

    /// Adayları (çevre/ızgara sırasına göre) eşit aralıklı index'lerle seçer.
    /// Greedy baştan-al yöntemindeki "dip dibe kümelenme" sorununu önler.
    private static List<Point2D> AdaylardenSecDagit(
        List<Point2D> adaylar,
        List<Point2D> mevcutlar,
        int istenen)
    {
        if (istenen <= 0) return new();

        var uygun = adaylar
            .Where(a => !mevcutlar.Any(m => Mesafe(m, a) < ElemanMinAralik))
            .ToList();

        if (uygun.Count <= istenen) return uygun;

        var secilen = new List<Point2D>();
        var kullanilan = new HashSet<int>();
        double step = (double)uygun.Count / istenen;
        for (int i = 0; i < istenen; i++)
        {
            int idx = (int)Math.Floor(i * step + step * 0.5);
            if (idx >= uygun.Count) idx = uygun.Count - 1;
            while (kullanilan.Contains(idx) && idx < uygun.Count - 1) idx++;
            if (kullanilan.Add(idx)) secilen.Add(uygun[idx]);
        }
        return secilen;
    }

    /// Bir noktayı (bx,by) mevcut elemanlarla çakışıyorsa oda içine doğru (nx,ny
    /// normal yönünde) adım adım kaydırır; poligon dışına taşmadan ilk boş noktayı
    /// döner. Anahtar–buat üst üste binmesini engeller.
    private static (double X, double Y) CakismayiGider(
        double bx, double by, double nx, double ny,
        List<Point2D> poligon, List<BasitProjeEleman> mevcutlar)
    {
        const double adim = 0.20;
        const int maxAdim = 8;
        bool Cakisiyor(double x, double y) =>
            mevcutlar.Any(e => { double dx = e.X - x, dy = e.Y - y; return dx * dx + dy * dy < ElemanMinAralik * ElemanMinAralik; });

        if (!Cakisiyor(bx, by)) return (bx, by);
        for (int i = 1; i <= maxAdim; i++)
        {
            double nxp = bx + nx * adim * i, nyp = by + ny * adim * i;
            if (!PoligonYardimci.IsPointInside(new Point2D(nxp, nyp), poligon)) break;
            if (!Cakisiyor(nxp, nyp)) return (nxp, nyp);
        }
        // İçeri kaydırma çözmedi → duvar boyunca (teğet) dene
        double tx = -ny, ty = nx;
        for (int yon = -1; yon <= 1; yon += 2)
            for (int i = 1; i <= maxAdim; i++)
            {
                double nxp = bx + tx * adim * i * yon, nyp = by + ty * adim * i * yon;
                if (!PoligonYardimci.IsPointInside(new Point2D(nxp, nyp), poligon)) break;
                if (!Cakisiyor(nxp, nyp)) return (nxp, nyp);
            }
        return (bx, by);
    }

    private static List<Point2D> FallbackNoktalar(
        Point2D merkez, int adet,
        IEnumerable<Point2D> secilenler,
        IEnumerable<Point2D> mevcutlar)
    {
        var sonuc = new List<Point2D>();
        double r = 0.5;
        int deneme = 0;
        while (sonuc.Count < adet && deneme < adet * 16)
        {
            double aci = 2 * Math.PI * deneme / (adet * 2);
            var p = new Point2D(merkez.X + r * Math.Cos(aci), merkez.Y + r * Math.Sin(aci));
            if (!mevcutlar.Concat(secilenler).Concat(sonuc).Any(m => Mesafe(m, p) < ElemanMinAralik))
                sonuc.Add(p);
            deneme++;
            if (deneme % (adet * 2) == 0) r += 0.4;
        }
        return sonuc;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static double Mesafe(Point2D a, Point2D b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static BpcPaftaTipi PaftaForBlokPublic(string blokAdi) => PaftaForBlok(blokAdi);

    private static BpcPaftaTipi PaftaForBlok(string blokAdi)
    {
        var tip = BasitProjeCizKutuphaneServisi.TipBlokAdindan(blokAdi);
        if (tip is null) return BpcPaftaTipi.Aydinlatma;
        if (tip is BpcElemanTipi.KatPanosu) return BpcPaftaTipi.Aydinlatma;
        if (tip is BpcElemanTipi.KlimaIcUnite or BpcElemanTipi.KlimaDisUnite) return BpcPaftaTipi.Mekanik;
        foreach (var s in BpcSistemKatalogu.Tumu)
            if (s.CihazTipleri.Contains(tip.Value) || s.AraToplayiciTipleri.Contains(tip.Value) || s.MerkezMi(tip.Value))
                return s.Sistem;
        return BpcPaftaTipi.Aydinlatma;
    }
}
