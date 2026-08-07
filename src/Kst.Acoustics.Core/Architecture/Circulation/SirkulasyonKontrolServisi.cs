using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Circulation;

public static class SirkulasyonKontrolServisi
{
    public const double VarsayilanAksToleransiM = 0.25;
    public const double VarsayilanIslakHacimSaftMaksUzaklikM = 4.0;

    public static IReadOnlyList<SirkulasyonKontrolUyarisi> KatlarArasiSureklilikUyarilari(
        PlanTaslagi? plan,
        double aksToleransiM = VarsayilanAksToleransiM)
    {
        if (plan is null || plan.Katlar.Count <= 1)
            return [];

        var katlar = plan.Katlar
            .Where(k => k is not null)
            .OrderBy(k => k.KatNo)
            .ToList();
        if (katlar.Count <= 1)
            return [];

        var katNolari = katlar
            .Select(k => k.KatNo)
            .Distinct()
            .OrderBy(k => k)
            .ToList();
        var minKatNo = katNolari.First();
        var maxKatNo = katNolari.Last();
        var tolerans = Math.Max(0, aksToleransiM);
        var uyarilar = new List<SirkulasyonKontrolUyarisi>();

        var cekirdekler = CekirdekKayitlari(katlar).ToList();
        var saftlar = SaftKayitlari(katlar).ToList();

        if (cekirdekler.Count == 0)
        {
            uyarilar.Add(new SirkulasyonKontrolUyarisi
            {
                KuralKodu = "M34-CEKIRDEK-YOK",
                Mesaj = "Çok katlı plan taslağında düşey çekirdek bulunmuyor.",
                CozumOnerisi = "Merdiven, yangın merdiveni veya asansör çekirdeğini plan taslağına ekleyin."
            });
        }

        TemelKayitUyarilariniEkle(cekirdekler, minKatNo, maxKatNo, "çekirdek", "M34-CEKIRDEK", uyarilar);
        TemelKayitUyarilariniEkle(saftlar, minKatNo, maxKatNo, "şaft", "M34-SAFT", uyarilar);

        AksGruplariniKontrolEt(cekirdekler, katNolari, tolerans, "M34-CEKIRDEK", uyarilar);
        AksGruplariniKontrolEt(saftlar, katNolari, tolerans, "M34-SAFT", uyarilar);

        return uyarilar;
    }

    public static IReadOnlyList<SirkulasyonKontrolUyarisi> IslakHacimSaftUyarilari(
        PlanTaslagi? plan,
        double maksSaftUzaklikM = VarsayilanIslakHacimSaftMaksUzaklikM)
    {
        if (plan is null || plan.Katlar.Count == 0)
            return [];

        var uyarilar = new List<SirkulasyonKontrolUyarisi>();
        var maksUzaklik = Math.Max(0, maksSaftUzaklikM);
        foreach (var kat in plan.Katlar.OrderBy(k => k.KatNo))
        {
            var islakHacimler = kat.Bloklar
                .Where(IslakHacimBloguMu)
                .Where(b => b.YerlesmisMi)
                .ToList();
            if (islakHacimler.Count == 0)
                continue;

            var saftlar = KatSaftKayitlari(kat).ToList();
            if (saftlar.Count == 0)
            {
                foreach (var blok in islakHacimler)
                {
                    uyarilar.Add(new SirkulasyonKontrolUyarisi
                    {
                        KuralKodu = "M34-ISLAK-HACIM-SAFT-YOK",
                        Mesaj = $"'{blok.Ad}' için aynı katta yerleşmiş şaft taslağı bulunmuyor.",
                        CozumOnerisi = "WC, banyo, mutfak ve benzeri ıslak hacimleri tesisat şaftına yakın konumlandırın veya kata şaft ekleyin.",
                        EtkilenenKatId = kat.Id,
                        EtkilenenMahalId = blok.Id
                    });
                }

                continue;
            }

            foreach (var blok in islakHacimler)
            {
                var enYakin = saftlar
                    .Select(s => (Saft: s, Mesafe: DikdortgenlerArasiMesafeM(blok.X, blok.Y, blok.GenislikM, blok.DerinlikM, s.X, s.Y, s.GenislikM, s.DerinlikM)))
                    .OrderBy(s => s.Mesafe)
                    .First();

                if (enYakin.Mesafe <= maksUzaklik)
                    continue;

                uyarilar.Add(new SirkulasyonKontrolUyarisi
                {
                    KuralKodu = "M34-ISLAK-HACIM-SAFT-UZAK",
                    Mesaj = $"'{blok.Ad}' en yakın şafttan yaklaşık {Metre(enYakin.Mesafe)} m uzak görünüyor.",
                    CozumOnerisi = "Islak hacmi şaft yakınına taşıyın, yakına yeni şaft ekleyin veya tesisat güzergahını ayrıca kontrol edin.",
                    EtkilenenKatId = kat.Id,
                    EtkilenenMahalId = blok.Id,
                    EtkilenenElemanId = enYakin.Saft.Id
                });
            }
        }

        return uyarilar;
    }

    /// <summary>
    /// Plan taslagindan kacis grafini yeniden uretir.
    /// YAN ETKI: grafi tazelemek icin <paramref name="plan"/>.KacisGrafi uzerine yazar; bu nedenle
    /// <see cref="KacisGrafiUyarilari"/> ve dolayli olarak M34 plan on kontrolu de plan grafini gunceller.
    /// </summary>
    public static KacisGrafi KacisGrafiUret(PlanTaslagi? plan)
    {
        var graf = new KacisGrafi
        {
            PlanTaslagiId = plan?.Id ?? "",
            Notlar = "M33 kaçış grafı v1: mahal-koridor-merdiven bağlantıları."
        };

        if (plan is null)
            return graf;

        foreach (var kat in plan.Katlar.OrderBy(k => k.KatNo))
        {
            var katDugumleri = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var blok in KoridorYerlesimServisi.BaglanmasiGerekenBloklariBul(kat).Where(b => b.YerlesmisMi))
            {
                DugumEkle(graf, katDugumleri, $"mahal:{blok.Id}", new KacisDugumu
                {
                    Id = $"mahal:{blok.Id}",
                    KaynakId = blok.Id,
                    KatId = kat.Id,
                    KatNo = kat.KatNo,
                    Ad = string.IsNullOrWhiteSpace(blok.Ad) ? "Mahal" : blok.Ad,
                    Tip = KacisDugumTipi.Mahal,
                    X = blok.MerkezX,
                    Y = blok.MerkezY
                });
            }

            foreach (var koridor in kat.Koridorlar.Where(k => k.UzunlukM > 0))
            {
                DugumEkle(graf, katDugumleri, $"koridor:{koridor.Id}", new KacisDugumu
                {
                    Id = $"koridor:{koridor.Id}",
                    KaynakId = koridor.Id,
                    KatId = kat.Id,
                    KatNo = kat.KatNo,
                    Ad = string.IsNullOrWhiteSpace(koridor.Ad) ? "Koridor" : koridor.Ad,
                    Tip = KacisDugumTipi.Koridor,
                    X = koridor.MerkezX,
                    Y = koridor.MerkezY
                });
            }

            foreach (var cekirdek in kat.Cekirdekler.Where(c => KacisMerdiveniMi(c) && c.YerlesmisMi && c.KatAraligindaMi(kat.KatNo)))
            {
                DugumEkle(graf, katDugumleri, $"merdiven:{cekirdek.Id}", new KacisDugumu
                {
                    Id = $"merdiven:{cekirdek.Id}",
                    KaynakId = cekirdek.Id,
                    KatId = kat.Id,
                    KatNo = kat.KatNo,
                    Ad = string.IsNullOrWhiteSpace(cekirdek.Ad) ? TipAdi(cekirdek.Tip) : cekirdek.Ad,
                    Tip = cekirdek.Tip == CekirdekTipi.YanginMerdiveni
                        ? KacisDugumTipi.YanginMerdiveni
                        : KacisDugumTipi.Merdiven,
                    X = cekirdek.MerkezX,
                    Y = cekirdek.MerkezY
                });
            }

            foreach (var blok in kat.Bloklar.Where(b => KacisMerdiveniBloguMu(b) && b.YerlesmisMi))
            {
                DugumEkle(graf, katDugumleri, $"merdiven:{blok.Id}", new KacisDugumu
                {
                    Id = $"merdiven:{blok.Id}",
                    KaynakId = blok.Id,
                    KatId = kat.Id,
                    KatNo = kat.KatNo,
                    Ad = string.IsNullOrWhiteSpace(blok.Ad) ? "Merdiven" : blok.Ad,
                    Tip = blok.MahalTipi == MahalTipi.YanginMerdiveni
                        ? KacisDugumTipi.YanginMerdiveni
                        : KacisDugumTipi.Merdiven,
                    X = blok.MerkezX,
                    Y = blok.MerkezY
                });
            }

            foreach (var koridor in kat.Koridorlar)
            {
                if (!katDugumleri.TryGetValue($"koridor:{koridor.Id}", out var koridorDugumId))
                    continue;

                foreach (var blokId in koridor.BagliBlokIdleri ?? [])
                {
                    if (!katDugumleri.TryGetValue($"mahal:{blokId}", out var mahalDugumId))
                        continue;

                    KenarEkle(graf, mahalDugumId, koridorDugumId, kat.KatNo, KacisKenariTipi.MahalKoridor);
                }
            }

            foreach (var cekirdek in kat.Cekirdekler.Where(c => KacisMerdiveniMi(c) && c.KatAraligindaMi(kat.KatNo)))
            {
                if (!katDugumleri.TryGetValue($"merdiven:{cekirdek.Id}", out var merdivenDugumId))
                    continue;

                foreach (var koridorId in cekirdek.BagliKoridorIdleri ?? [])
                {
                    if (!katDugumleri.TryGetValue($"koridor:{koridorId}", out var koridorDugumId))
                        continue;

                    KenarEkle(graf, koridorDugumId, merdivenDugumId, kat.KatNo, KacisKenariTipi.KoridorMerdiven);
                }
            }
        }

        plan.KacisGrafi = graf;
        return graf;
    }

    public static IReadOnlyList<SirkulasyonKontrolUyarisi> KacisGrafiUyarilari(PlanTaslagi? plan)
    {
        if (plan is null)
            return [];

        var graf = KacisGrafiUret(plan);
        if (graf.Dugumler.Count == 0)
            return [];

        var hedefDugumIdleri = graf.MerdivenDugumleri.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var komsular = KomsulukSozlugu(graf);
        var uyarilar = new List<SirkulasyonKontrolUyarisi>();
        foreach (var mahal in graf.MahalDugumleri)
        {
            if (HedefeUlasirMi(mahal.Id, hedefDugumIdleri, komsular))
                continue;

            uyarilar.Add(new SirkulasyonKontrolUyarisi
            {
                KuralKodu = "M34-KACIS-GRAFI-MERDIVENE-ULASAMAYAN-MAHAL",
                Mesaj = $"'{mahal.Ad}' kaçış grafında merdiven veya yangın merdivenine ulaşamıyor.",
                CozumOnerisi = "Mahali koridora, koridoru da uygun merdiven çekirdeğine bağlayın veya çekirdek bağlantısını tanımlayın.",
                EtkilenenKatId = mahal.KatId,
                EtkilenenMahalId = mahal.KaynakId
            });
        }

        return uyarilar;
    }

    private static IEnumerable<DuseyElemanKaydi> CekirdekKayitlari(IEnumerable<KatTaslagi> katlar)
    {
        foreach (var kat in katlar)
        {
            foreach (var cekirdek in kat.Cekirdekler ?? [])
            {
                if (cekirdek is null)
                    continue;

                yield return new DuseyElemanKaydi(
                    cekirdek.Id,
                    string.IsNullOrWhiteSpace(cekirdek.Ad) ? TipAdi(cekirdek.Tip) : cekirdek.Ad,
                    kat.Id,
                    cekirdek.KatNo,
                    cekirdek.AltKatNo,
                    cekirdek.UstKatNo,
                    cekirdek.MerkezX,
                    cekirdek.MerkezY,
                    NormalizeAks(cekirdek.AksKodu),
                    ((int)cekirdek.Tip).ToString(CultureInfo.InvariantCulture),
                    TipAdi(cekirdek.Tip));
            }
        }
    }

    private static IEnumerable<DuseyElemanKaydi> SaftKayitlari(IEnumerable<KatTaslagi> katlar)
    {
        foreach (var kat in katlar)
        {
            foreach (var saft in kat.Saftlar ?? [])
            {
                if (saft is null)
                    continue;

                yield return new DuseyElemanKaydi(
                    saft.Id,
                    string.IsNullOrWhiteSpace(saft.Ad) ? TipAdi(saft.TesisatTuru) : saft.Ad,
                    kat.Id,
                    saft.KatNo,
                    saft.AltKatNo,
                    saft.UstKatNo,
                    saft.MerkezX,
                    saft.MerkezY,
                    NormalizeAks(saft.AksKodu),
                    ((int)saft.TesisatTuru).ToString(CultureInfo.InvariantCulture),
                    TipAdi(saft.TesisatTuru));
            }
        }
    }

    private static void TemelKayitUyarilariniEkle(
        IReadOnlyList<DuseyElemanKaydi> kayitlar,
        int minKatNo,
        int maxKatNo,
        string elemanTuru,
        string kodKoku,
        List<SirkulasyonKontrolUyarisi> uyarilar)
    {
        foreach (var kayit in kayitlar)
        {
            if (string.IsNullOrWhiteSpace(kayit.AksKodu))
            {
                uyarilar.Add(new SirkulasyonKontrolUyarisi
                {
                    KuralKodu = $"{kodKoku}-AKS-KODU-EKSIK",
                    Mesaj = $"'{kayit.Ad}' için aks kodu tanımlanmamış.",
                    CozumOnerisi = $"Katlar arası süreklilik kontrolü için {elemanTuru} aks kodunu girin.",
                    EtkilenenKatId = kayit.KatId,
                    EtkilenenElemanId = kayit.Id
                });
            }

            if (!kayit.KatNoAraliktaMi)
            {
                uyarilar.Add(new SirkulasyonKontrolUyarisi
                {
                    KuralKodu = $"{kodKoku}-KAT-NO-UYUMSUZ",
                    Mesaj = $"'{kayit.Ad}' kat numarası, başlangıç-bitiş kat aralığının dışında kalıyor.",
                    CozumOnerisi = "Çekirdek/şaft kat numarası ile başlangıç-bitiş katlarını aynı aralıkta tutun.",
                    EtkilenenKatId = kayit.KatId,
                    EtkilenenElemanId = kayit.Id,
                    AksKodu = kayit.AksKodu
                });
            }

            if (kayit.AltKatNo < minKatNo || kayit.UstKatNo > maxKatNo)
            {
                uyarilar.Add(new SirkulasyonKontrolUyarisi
                {
                    KuralKodu = $"{kodKoku}-KAT-ARALIGI-PLAN-DISI",
                    Mesaj = $"'{kayit.Ad}' kat aralığı plan taslağındaki katların dışında kalıyor.",
                    CozumOnerisi = "Başlangıç ve bitiş katlarını plan taslağındaki kat numaralarıyla uyumlu yapın.",
                    EtkilenenKatId = kayit.KatId,
                    EtkilenenElemanId = kayit.Id,
                    AksKodu = kayit.AksKodu
                });
            }
        }
    }

    private static void AksGruplariniKontrolEt(
        IReadOnlyList<DuseyElemanKaydi> kayitlar,
        IReadOnlyList<int> katNolari,
        double aksToleransiM,
        string kodKoku,
        List<SirkulasyonKontrolUyarisi> uyarilar)
    {
        var gruplar = kayitlar
            .Where(k => !string.IsNullOrWhiteSpace(k.AksKodu))
            .GroupBy(k => $"{k.TipAnahtari}|{k.AksKodu}", StringComparer.OrdinalIgnoreCase);

        foreach (var grup in gruplar)
        {
            var liste = grup
                .OrderBy(k => k.AltKatNo)
                .ThenBy(k => k.UstKatNo)
                .ThenBy(k => k.Ad, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (liste.Count == 0)
                continue;

            var ilk = liste[0];
            var alt = Math.Max(katNolari.First(), liste.Min(k => k.AltKatNo));
            var ust = Math.Min(katNolari.Last(), liste.Max(k => k.UstKatNo));
            var eksikKatlar = katNolari
                .Where(katNo => katNo >= alt && katNo <= ust && !liste.Any(k => k.KatiKapsar(katNo)))
                .ToList();
            if (eksikKatlar.Count > 0)
            {
                uyarilar.Add(new SirkulasyonKontrolUyarisi
                {
                    KuralKodu = $"{kodKoku}-SUREKLILIK-KOPUK",
                    Mesaj = $"'{ilk.AksKodu}' aksındaki {ilk.TipAdi} katlar arasında kesintili görünüyor. Eksik katlar: {string.Join(", ", eksikKatlar)}.",
                    CozumOnerisi = "Aynı aks kodundaki düşey elemanı eksik katlara devam ettirin veya aks kodlarını bilinçli şekilde ayırın.",
                    EtkilenenKatId = ilk.KatId,
                    EtkilenenElemanId = ilk.Id,
                    AksKodu = ilk.AksKodu
                });
            }

            foreach (var kayit in liste.Skip(1))
            {
                var dx = Math.Abs(kayit.MerkezX - ilk.MerkezX);
                var dy = Math.Abs(kayit.MerkezY - ilk.MerkezY);
                if (dx <= aksToleransiM && dy <= aksToleransiM)
                    continue;

                uyarilar.Add(new SirkulasyonKontrolUyarisi
                {
                    KuralKodu = $"{kodKoku}-AKS-KAYMASI",
                    Mesaj = $"'{kayit.Ad}' aynı aks grubundaki referansa göre X:{Metre(dx)} m, Y:{Metre(dy)} m kaymış görünüyor.",
                    CozumOnerisi = "Aynı aks kodundaki çekirdek/şaft merkezlerini üst üste getirin veya farklı aks kodu kullanın.",
                    EtkilenenKatId = kayit.KatId,
                    EtkilenenElemanId = kayit.Id,
                    AksKodu = kayit.AksKodu
                });
            }
        }
    }

    private static string NormalizeAks(string? aksKodu)
        => string.IsNullOrWhiteSpace(aksKodu) ? "" : aksKodu.Trim().ToUpperInvariant();

    private static void DugumEkle(
        KacisGrafi graf,
        Dictionary<string, string> katDugumleri,
        string anahtar,
        KacisDugumu dugum)
    {
        if (katDugumleri.ContainsKey(anahtar))
            return;

        graf.Dugumler.Add(dugum);
        katDugumleri[anahtar] = dugum.Id;
    }

    private static void KenarEkle(
        KacisGrafi graf,
        string kaynakDugumId,
        string hedefDugumId,
        int katNo,
        KacisKenariTipi tip)
    {
        if (string.Equals(kaynakDugumId, hedefDugumId, StringComparison.OrdinalIgnoreCase))
            return;

        if (graf.Kenarlar.Any(k =>
                (string.Equals(k.KaynakDugumId, kaynakDugumId, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(k.HedefDugumId, hedefDugumId, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(k.KaynakDugumId, hedefDugumId, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(k.HedefDugumId, kaynakDugumId, StringComparison.OrdinalIgnoreCase))))
            return;

        var kaynak = graf.Dugumler.FirstOrDefault(d => string.Equals(d.Id, kaynakDugumId, StringComparison.OrdinalIgnoreCase));
        var hedef = graf.Dugumler.FirstOrDefault(d => string.Equals(d.Id, hedefDugumId, StringComparison.OrdinalIgnoreCase));
        graf.Kenarlar.Add(new KacisKenari
        {
            Id = $"{kaynakDugumId}>{hedefDugumId}",
            KaynakDugumId = kaynakDugumId,
            HedefDugumId = hedefDugumId,
            KatNo = katNo,
            Tip = tip,
            MesafeM = kaynak is null || hedef is null ? 0 : NoktaMesafesiM(kaynak.X, kaynak.Y, hedef.X, hedef.Y)
        });
    }

    private static Dictionary<string, List<string>> KomsulukSozlugu(KacisGrafi graf)
    {
        var komsular = graf.Dugumler.ToDictionary(d => d.Id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var kenar in graf.Kenarlar)
        {
            if (komsular.TryGetValue(kenar.KaynakDugumId, out var kaynakKomsular))
                kaynakKomsular.Add(kenar.HedefDugumId);

            if (komsular.TryGetValue(kenar.HedefDugumId, out var hedefKomsular))
                hedefKomsular.Add(kenar.KaynakDugumId);
        }

        return komsular;
    }

    private static bool HedefeUlasirMi(
        string baslangicDugumId,
        ISet<string> hedefDugumIdleri,
        IReadOnlyDictionary<string, List<string>> komsular)
    {
        if (hedefDugumIdleri.Count == 0)
            return false;

        var gorulenler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kuyruk = new Queue<string>();
        kuyruk.Enqueue(baslangicDugumId);
        gorulenler.Add(baslangicDugumId);

        while (kuyruk.Count > 0)
        {
            var dugumId = kuyruk.Dequeue();
            if (hedefDugumIdleri.Contains(dugumId))
                return true;

            if (!komsular.TryGetValue(dugumId, out var dugumKomsulari))
                continue;

            foreach (var komsu in dugumKomsulari)
            {
                if (gorulenler.Add(komsu))
                    kuyruk.Enqueue(komsu);
            }
        }

        return false;
    }

    private static bool KacisMerdiveniMi(CekirdekElemani cekirdek)
        => cekirdek.Tip is CekirdekTipi.Merdiven or CekirdekTipi.YanginMerdiveni;

    private static bool KacisMerdiveniBloguMu(MahalBlogu blok)
        => blok.MahalTipi is MahalTipi.Merdiven or MahalTipi.YanginMerdiveni;

    private static bool IslakHacimBloguMu(MahalBlogu blok)
        => MahalTipiYardimcilari.IslakHacimMi(blok.MahalTipi, blok.KullanimSinifi);

    private static IEnumerable<SaftGeometriKaydi> KatSaftKayitlari(KatTaslagi kat)
    {
        foreach (var saft in kat.Saftlar ?? [])
        {
            if (saft is null || !saft.YerlesmisMi || !saft.KatAraligindaMi(kat.KatNo))
                continue;

            yield return new SaftGeometriKaydi(saft.Id, saft.X, saft.Y, saft.GenislikM, saft.DerinlikM);
        }

        foreach (var blok in kat.Bloklar ?? [])
        {
            if (blok is null || !blok.YerlesmisMi || blok.MahalTipi != MahalTipi.Saft)
                continue;

            yield return new SaftGeometriKaydi(blok.Id, blok.X, blok.Y, blok.GenislikM, blok.DerinlikM);
        }
    }

    private static double DikdortgenlerArasiMesafeM(
        double ax,
        double ay,
        double aGenislik,
        double aDerinlik,
        double bx,
        double by,
        double bGenislik,
        double bDerinlik)
    {
        var aMaxX = ax + Math.Max(0, aGenislik);
        var aMaxY = ay + Math.Max(0, aDerinlik);
        var bMaxX = bx + Math.Max(0, bGenislik);
        var bMaxY = by + Math.Max(0, bDerinlik);
        var dx = Math.Max(0, Math.Max(bx - aMaxX, ax - bMaxX));
        var dy = Math.Max(0, Math.Max(by - aMaxY, ay - bMaxY));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double NoktaMesafesiM(double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static string TipAdi(CekirdekTipi tip)
        => tip switch
        {
            CekirdekTipi.Merdiven => "merdiven",
            CekirdekTipi.YanginMerdiveni => "yangın merdiveni",
            CekirdekTipi.Asansor => "asansör",
            CekirdekTipi.Rampa => "rampa",
            CekirdekTipi.Saft => "şaft",
            CekirdekTipi.CekirdekGrubu => "çekirdek grubu",
            _ => "çekirdek elemanı"
        };

    private static string TipAdi(SaftTesisatTuru tur)
        => tur switch
        {
            SaftTesisatTuru.Mekanik => "mekanik şaftı",
            SaftTesisatTuru.Elektrik => "elektrik şaftı",
            SaftTesisatTuru.SihhiTesisat => "sıhhi tesisat şaftı",
            SaftTesisatTuru.Yangin => "yangın tesisatı şaftı",
            SaftTesisatTuru.Havalandirma => "havalandırma şaftı",
            SaftTesisatTuru.DumanTahliye => "duman tahliye şaftı",
            SaftTesisatTuru.Ortak => "ortak şaft",
            _ => "şaft"
        };

    private static string Metre(double deger)
        => deger.ToString("0.##", CultureInfo.InvariantCulture);

    private sealed record DuseyElemanKaydi(
        string Id,
        string Ad,
        string KatId,
        int KatNo,
        int AltKatNo,
        int UstKatNo,
        double MerkezX,
        double MerkezY,
        string AksKodu,
        string TipAnahtari,
        string TipAdi)
    {
        public bool KatNoAraliktaMi => KatNo >= AltKatNo && KatNo <= UstKatNo;

        public bool KatiKapsar(int katNo)
            => katNo >= AltKatNo && katNo <= UstKatNo;
    }

    private sealed record SaftGeometriKaydi(
        string Id,
        double X,
        double Y,
        double GenislikM,
        double DerinlikM);
}
