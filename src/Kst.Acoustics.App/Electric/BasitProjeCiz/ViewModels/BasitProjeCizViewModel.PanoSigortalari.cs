using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;

public partial class BasitProjeCizViewModel
{
    public List<PanoOzet> PrizSigortaHesaplari() =>
        BasitProjeCizPanoServisi.TasarimPanoOzetleri(Proje, Armatürler.Concat(Prizler));

    public List<PanoOzet> AydinlatmaSigortaHesaplari() => PrizSigortaHesaplari();

    public bool PanoAydinlatmaSigortalariniOlustur() => TasarimSigortalariniOlustur(prizDahil: false);

    public bool PrizProjesiniTamamla() => TasarimSigortalariniOlustur(prizDahil: true);

    private bool TasarimSigortalariniOlustur(bool prizDahil)
    {
        string tasarim = prizDahil ? "Priz projesi" : "Aydınlatma";
        if (!ModulKuvvetliMi || KilitliyseDur("sigortaları hesaplamak")) return false;
        var elemanlar = Katlar.SelectMany(k => k.Elemanlar);
        bool yukVar = prizDahil
            ? elemanlar.Any(e => BasitProjeCizKabloServisi.ElemanPrizMi(e.Tip))
            : elemanlar.Any(e => BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip));
        // Son priz kaldırılıp hatlar temizlendiğinde eski çıkışları da kaldırıp girişi düşürebilmeli.
        bool oncekiCetveliGuncelle = prizDahil && PrizAsamasi == BpcPrizAsamasi.Sigortalar &&
            elemanlar.Any(e => e.AydinlatmaSigortalari is { Count: > 0 });
        if (!yukVar && !oncekiCetveliGuncelle)
        {
            DurumMesaji = prizDahil ? "Tamamlamak için önce prizleri yerleştirin." : "Tamamlamak için önce aydınlatma armatürlerini yerleştirin.";
            return false;
        }

        var eksikler = Katlar.SelectMany(k => AydinlatmaTasarimServisi.EksikBaglantilariBul(k, true)).ToList();
        if (prizDahil) eksikler.AddRange(Katlar.SelectMany(PrizTasarimServisi.EksikBaglantilariBul));
        eksikler.AddRange(Katlar.SelectMany(k => EksikPanoBeslemeleri(k, prizDahil)));
        var hesaplar = PrizSigortaHesaplari();
        foreach (var ozet in hesaplar)
        {
            var katalogHatalari = prizDahil ? BasitProjeCizPanoServisi.SigortaKatalogHatalari(ozet) :
                AydinlatmaTasarimServisi.SigortaYerleri(ozet).SelectMany(s => BasitProjeCizPanoServisi.SigortaKatalogHatalari(ozet, s.Id));
            eksikler.AddRange(katalogHatalari);
            // Her iki aşamada da giriş hesabı, bağlı tüm A ve P linyelerinin gücüne dayanır.
            foreach (var linye in ozet.Linyeler.Where(l => l.LinyePrefix is "A" or "P"))
            {
                if (linye.ElemanSayisi > 0 && (linye.EksikGucTanimSayisi > 0 || !double.IsFinite(linye.GucW) || linye.GucW <= 0))
                    eksikler.Add($"{ozet.Ad} / {linye.Etiket} — {(linye.LinyePrefix == "P" ? "priz" : "armatür")} gücünü girin.");
            }
        }
        if (eksikler.Count > 0)
        {
            DurumMesaji = eksikler[0];
            UyariGoster($"{tasarim} için tamamlanacaklar\n" + string.Join("\n", eksikler.Distinct()));
            return false;
        }

        var degisenler = hesaplar.Select(o => (o.Pano, Kayit: AydinlatmaTasarimServisi.SigortalariHazirla(o, prizDahil)))
            .Where(p => p.Pano.AydinlatmaSigortalari is { } eski
                ? eski.Count != p.Kayit.Count || p.Kayit.Any(k => eski.GetValueOrDefault(k.Key) != k.Value)
                : p.Kayit.Count > 0).ToList();
        if (degisenler.Count > 0)
        {
            var panoIds = degisenler.Select(p => p.Pano.Id).ToHashSet();
            ElemanSnapshotAl(Katlar.Where(k => k.Elemanlar.Any(e => panoIds.Contains(e.Id))));
            foreach (var (pano, kayit) in degisenler) pano.AydinlatmaSigortalari = kayit;
            GuncellePanoIcerik();
        }
        DurumMesaji = $"{tasarim} tamamlandı. Pano giriş ve çıkış sigortaları hazır.";
        return true;
    }

    public void PanoSigortasiniDuzenle(BasitProjeEleman pano, string kaynakId, int amper)
    {
        if (SeciliKat is not { } kat || !kat.Elemanlar.Contains(pano) || pano.Tip != BpcElemanTipi.KatPanosu ||
            amper < 0 || string.IsNullOrEmpty(kaynakId) || KilitliyseDur("sigorta düzenlemek")) return;
        var ayar = new BpcPanoSigortasi(amper, true);
        if (pano.AydinlatmaSigortalari?.GetValueOrDefault(kaynakId) == ayar) return;
        bool yedekSil = amper == 0 && kaynakId.StartsWith(AydinlatmaTasarimServisi.YedekOnEk, StringComparison.Ordinal);
        if (yedekSil && pano.AydinlatmaSigortalari?.ContainsKey(kaynakId) != true) return;
        ElemanSnapshotAl(kat);
        var kayit = pano.AydinlatmaSigortalari ??= new();
        if (yedekSil) kayit.Remove(kaynakId);
        else kayit[kaynakId] = ayar;
        GuncellePanoIcerik();
        DurumMesaji = amper > 0 ? $"Pano sigortası {amper} A olarak kaydedildi." : "Pano sigortası kaldırıldı.";
    }

    public void PanoSigortasiniOtomatigeAl(BasitProjeEleman pano, string kaynakId)
    {
        if (SeciliKat is not { } kat || !kat.Elemanlar.Contains(pano) || KilitliyseDur("sigortayı otomatiğe almak") ||
            pano.AydinlatmaSigortalari is not { } kayit || kayit.GetValueOrDefault(kaynakId) is not { Manuel: true }) return;
        var ozet = PrizSigortaHesaplari().FirstOrDefault(o => o.Pano == pano);
        if (ozet == null) return;
        if (BasitProjeCizPanoServisi.SigortaKatalogHatalari(ozet, kaynakId).FirstOrDefault() is { } hata)
        {
            DurumMesaji = hata;
            UyariGoster(hata);
            return;
        }
        var yer = AydinlatmaTasarimServisi.SigortaYerleri(ozet, prizDahil: true).FirstOrDefault(s => s.Id == kaynakId);
        if (yer.Id == null) return;
        bool gucEksik = kaynakId == AydinlatmaTasarimServisi.Giris ? ozet.EksikGucVarMi :
            ozet.Linyeler.Any(l => l.BaglantiId == kaynakId && l.EksikGucTanimSayisi > 0);
        if (gucEksik)
        {
            DurumMesaji = $"{ozet.Ad} / {yer.Ad} — otomatik hesap için eksik yük güçlerini girin.";
            UyariGoster(DurumMesaji);
            return;
        }
        ElemanSnapshotAl(kat);
        if (yer.Amper > 0) kayit[kaynakId] = new BpcPanoSigortasi(yer.Amper, false);
        else kayit.Remove(kaynakId);
        GuncellePanoIcerik();
        DurumMesaji = "Sigorta otomatik hesaba alındı.";
    }
}
