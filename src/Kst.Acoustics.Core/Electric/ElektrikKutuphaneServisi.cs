using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// JSON tabanlı kütüphane servisi — yük tipleri, KAR'lar ve kondansatörler.
/// </summary>
public class ElektrikKutuphaneServisi
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _dataDir;

    public List<YukTipiTanim> YukTipleri { get; private set; } = new();
    public List<KacakAkimRolesiTanim> KacakAkimRoleleri { get; private set; } = new();
    public List<AkimTrafosuTanim> AkimTrafolari { get; private set; } = new();
    public List<KondansatorTanim> Kondansatorler { get; private set; } = new();
    public List<BusbarTanim> BusbarTipleri { get; private set; } = new();
    public List<AstronomikZamanRolesiTanim> AstronomikZamanRoleleri { get; private set; } = new();
    public List<KontaktorTanim> Kontaktorler { get; private set; } = new();
    public List<DarbeKesiciTanim> DarbeKesiciler { get; private set; } = new();
    public List<SigortaTanim> Sigortalar { get; private set; } = new();
    public List<TmsTanim> Tmsler { get; private set; } = new();
    public List<BicakliSigortaTanim> BicakliSigortalar { get; private set; } = new();
    public List<KompanzasyonRolesiTanim> KompanzasyonRoleleri { get; private set; } = new();
    public List<ReaktorTanim> Reaktorler { get; private set; } = new();
    public List<SvcSurucuTanim> SvcSuruculer { get; private set; } = new();
    public List<HarmonikFiltreTanim> HarmonikFiltreler { get; private set; } = new();
    public List<MotorTanim> Motorlar { get; private set; } = new();
    public List<AlternatorTanim> Alternatorler { get; private set; } = new();
    public List<JeneratorTanim> Jeneratorler { get; private set; } = new();
    public List<TransferSalterTanim> TransferSalterleri { get; private set; } = new();
    public List<UpsTanim> Upsler { get; private set; } = new();
    public List<AkuTanim> Akuler { get; private set; } = new();
    public List<TrafoTanim> Trafolar { get; private set; } = new();
    public List<OgHucreTanim> OgHucreleri { get; private set; } = new();

    public ElektrikKutuphaneServisi(string? dataDir = null)
    {
        _dataDir = dataDir ?? GetDefaultDataDir();
        YukleriBelirt();
    }

    private static string GetDefaultDataDir()
    {
        // Embedded resource yerine dosya sistemi
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataPath = Path.Combine(baseDir, "Electric", "Data");
        if (!Directory.Exists(dataPath))
        {
            // Geliştirme: proje kökünden bul
            var devPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Electric", "Data");
            if (Directory.Exists(devPath))
                return devPath;
        }
        return dataPath;
    }

    private void YukleriBelirt()
    {
        YukTipleri = YukleDosya<YukTipiTanim>("YukTipiKutuphanesi.json") ?? VarsayilanYukTipleri();
        YukTipiVarsayilanlariBirlestir();
        KacakAkimRoleleri = YukleDosya<KacakAkimRolesiTanim>("KacakAkimRolesiKutuphanesi.json") ?? VarsayilanKarlar();
        KacakAkimRolesiVarsayilanlariBirlestir();
        AkimTrafolari = YukleDosya<AkimTrafosuTanim>("AkimTrafosuKutuphanesi.json") ?? VarsayilanAkimTrafolari();
        AkimTrafosuVarsayilanlariBirlestir();
        Kondansatorler = YukleDosya<KondansatorTanim>("KondansatorKutuphanesi.json") ?? VarsayilanKondansatorler();
        KondansatorVarsayilanlariBirlestir();
        BusbarTipleri = YukleDosya<BusbarTanim>("BusbarKutuphanesi.json") ?? VarsayilanBusbarlar();
        BusbarVarsayilanlariBirlestir();
        AstronomikZamanRoleleri = YukleDosya<AstronomikZamanRolesiTanim>("AstronomikZamanRolesiKutuphanesi.json") ?? VarsayilanAzrler();
        Kontaktorler = YukleDosya<KontaktorTanim>("KontaktorKutuphanesi.json") ?? VarsayilanKontaktorler();
        DarbeKesiciler = YukleDosya<DarbeKesiciTanim>("DarbeKesiciKutuphanesi.json") ?? VarsayilanDarbeKesiciler();
        Sigortalar = YukleDosya<SigortaTanim>("SigortaKutuphanesi.json") ?? VarsayilanSigortalar();
        Tmsler = YukleDosya<TmsTanim>("TmsKutuphanesi.json") ?? VarsayilanTmsler();
        BicakliSigortalar = YukleDosya<BicakliSigortaTanim>("BicakliSigortaKutuphanesi.json") ?? VarsayilanBicakliSigortalar();
        KompanzasyonRoleleri = YukleDosya<KompanzasyonRolesiTanim>("KompanzasyonRolesiKutuphanesi.json") ?? VarsayilanKompRoleleri();
        Reaktorler = YukleDosya<ReaktorTanim>("ReaktorKutuphanesi.json") ?? VarsayilanReaktorler();
        SvcSuruculer = YukleDosya<SvcSurucuTanim>("SvcSurucuKutuphanesi.json") ?? VarsayilanSvcSuruculer();
        HarmonikFiltreler = YukleDosya<HarmonikFiltreTanim>("HarmonikFiltreKutuphanesi.json") ?? VarsayilanHarmonikFiltreler();
        Motorlar = YukleDosya<MotorTanim>("MotorKutuphanesi.json") ?? VarsayilanMotorlar();
        Alternatorler = YukleDosya<AlternatorTanim>("AlternatorKutuphanesi.json") ?? VarsayilanAlternatorler();
        Jeneratorler = YukleDosya<JeneratorTanim>("JeneratorKutuphanesi.json") ?? VarsayilanJeneratorler();
        JeneratorVarsayilanlariBirlestir();
        TransferSalterleri = YukleDosya<TransferSalterTanim>("TransferSalterKutuphanesi.json") ?? VarsayilanTransferSalterleri();
        TransferSalterVarsayilanlariBirlestir();
        Upsler = YukleDosya<UpsTanim>("UpsKutuphanesi.json") ?? VarsayilanUpsler();
        Akuler = YukleDosya<AkuTanim>("AkuKutuphanesi.json") ?? VarsayilanAkuler();
        Trafolar = YukleDosya<TrafoTanim>("TrafoKutuphanesi.json") ?? VarsayilanTrafolar();
        TrafoVarsayilanlariBirlestir();
        OgHucreleri = YukleDosya<OgHucreTanim>("OgHucreKutuphanesi.json") ?? VarsayilanOgHucreleri();
        OgHucreVarsayilanlariBirlestir();
    }

    /// <summary>
    /// Eski/eksik yük tipi kütüphanelerine, henüz bulunmayan varsayılan tipleri ekler.
    /// Kullanıcının eklediği/düzenlediği tipler korunur.
    /// </summary>
    private void YukTipiVarsayilanlariBirlestir()
    {
        var mevcut = new HashSet<string>(
            YukTipleri.Select(y => y.Ad), StringComparer.OrdinalIgnoreCase);
        foreach (var v in VarsayilanYukTipleri())
            if (!mevcut.Contains(v.Ad))
                YukTipleri.Add(v);
    }

    /// <summary>
    /// Eski/eksik kaçak akım rölesi kütüphanelerine troid röleleri ekler.
    /// 100A üstünde klasik 4P KAR yerine troid röle + akım trafosu seçilir.
    /// </summary>
    private void KacakAkimRolesiVarsayilanlariBirlestir()
    {
        var mevcut = new HashSet<string>(
            KacakAkimRoleleri.Select(k => k.Ad), StringComparer.OrdinalIgnoreCase);
        foreach (var v in VarsayilanKarlar())
            if (!mevcut.Contains(v.Ad))
                KacakAkimRoleleri.Add(v);

        KacakAkimRoleleri = KacakAkimRoleleri
            .OrderBy(k => k.SensitiviteMa)
            .ThenBy(k => k.TroidTipiMi)
            .ThenBy(k => k.AnmaAkimA)
            .ThenBy(k => k.Tip, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Eski/eksik akım trafosu kütüphanelerine troid AT serisini ekler.
    /// Kullanıcı kayıtları korunur.
    /// </summary>
    private void AkimTrafosuVarsayilanlariBirlestir()
    {
        var mevcut = new HashSet<string>(
            AkimTrafolari.Select(a => a.Ad), StringComparer.OrdinalIgnoreCase);
        foreach (var v in VarsayilanAkimTrafolari())
            if (!mevcut.Contains(v.Ad))
                AkimTrafolari.Add(v);

        AkimTrafolari = AkimTrafolari
            .OrderBy(a => a.PrimerA)
            .ThenBy(a => a.IcCapMm)
            .ThenBy(a => a.Tip, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Eski/eksik trafo kütüphanelerine, henüz bulunmayan varsayılan trafoları (standart kVA × tip)
    /// ekler. Kullanıcının eklediği/düzenlediği tipler korunur.
    /// </summary>
    private void TrafoVarsayilanlariBirlestir()
    {
        var mevcut = new HashSet<string>(Trafolar.Select(t => t.Ad), StringComparer.OrdinalIgnoreCase);
        foreach (var v in VarsayilanTrafolar())
            if (!mevcut.Contains(v.Ad))
                Trafolar.Add(v);
        Trafolar = Trafolar.OrderBy(t => t.GucKVA).ThenBy(t => t.Tip).ToList();
    }

    /// <summary>
    /// Eski/eksik OG hücre kütüphanelerine, henüz bulunmayan varsayılan hücreleri ekler.
    /// Kullanıcının eklediği/düzenlediği tipler korunur.
    /// </summary>
    private void OgHucreVarsayilanlariBirlestir()
    {
        var mevcut = new HashSet<string>(OgHucreleri.Select(h => h.Ad), StringComparer.OrdinalIgnoreCase);
        foreach (var v in VarsayilanOgHucreleri())
            if (!mevcut.Contains(v.Ad))
                OgHucreleri.Add(v);
        OgHucreleri = OgHucreleri.OrderBy(h => h.HucreTipi).ThenBy(h => h.AnmaAkimA).ToList();
    }

    /// <summary>
    /// Eski/eksik busbar kütüphanelerine, henüz bulunmayan varsayılan busbar tiplerini ekler
    /// (yüksek amperli ve alüminyum tipler dahil). Kullanıcının eklediği/düzenlediği tipler korunur.
    /// </summary>
    private void BusbarVarsayilanlariBirlestir()
    {
        var mevcut = new HashSet<string>(
            BusbarTipleri.Select(b => b.Ad), StringComparer.OrdinalIgnoreCase);
        foreach (var v in VarsayilanBusbarlar())
            if (!mevcut.Contains(v.Ad))
                BusbarTipleri.Add(v);
        // Anma akımına, sonra malzemeye göre sırala (Bakır önce)
        BusbarTipleri = BusbarTipleri
            .OrderBy(b => b.NominalAkimA)
            .ThenBy(b => b.Malzeme, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Eski/eksik kondansatör kütüphanelerine, kademeli kompanzasyon için gereken ince standart
    /// boyutları (2,5–50 kVAr) ekler. Kullanıcının eklediği/düzenlediği tipler korunur.
    /// </summary>
    private void KondansatorVarsayilanlariBirlestir()
    {
        var mevcut = new HashSet<string>(
            Kondansatorler.Select(k => k.Ad), StringComparer.OrdinalIgnoreCase);
        foreach (var v in VarsayilanKondansatorler())
            if (!mevcut.Contains(v.Ad))
                Kondansatorler.Add(v);
        Kondansatorler = Kondansatorler.OrderBy(k => k.GucKVar).ThenBy(k => k.FazTipi).ToList();
    }

    /// <summary>
    /// Eski/eksik jeneratör kütüphanelerine güncel varsayılan marka serilerini ekler.
    /// Kullanıcının eklediği/düzenlediği jeneratörler korunur.
    /// </summary>
    private void JeneratorVarsayilanlariBirlestir()
    {
        var mevcut = new HashSet<string>(
            Jeneratorler.Select(j => j.Ad), StringComparer.OrdinalIgnoreCase);
        foreach (var v in VarsayilanJeneratorler())
            if (!mevcut.Contains(v.Ad))
                Jeneratorler.Add(v);

        Jeneratorler = Jeneratorler
            .OrderBy(j => j.StandbyGucKVA)
            .ThenBy(j => j.Marka, StringComparer.OrdinalIgnoreCase)
            .ThenBy(j => j.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Eski/eksik transfer şalteri kütüphanelerine yüksek akım ATS tiplerini ekler.
    /// Kullanıcının eklediği/düzenlediği şalterler korunur.
    /// </summary>
    private void TransferSalterVarsayilanlariBirlestir()
    {
        var mevcut = new HashSet<string>(
            TransferSalterleri.Select(t => t.Ad), StringComparer.OrdinalIgnoreCase);
        foreach (var v in VarsayilanTransferSalterleri())
            if (!mevcut.Contains(v.Ad))
                TransferSalterleri.Add(v);

        TransferSalterleri = TransferSalterleri
            .OrderBy(t => t.AnmaAkimA)
            .ThenBy(t => t.Tipi, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Marka, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<T>? YukleDosya<T>(string dosyaAdi)
    {
        var yol = Path.Combine(_dataDir, dosyaAdi);
        if (!File.Exists(yol)) return null;
        try
        {
            var json = File.ReadAllText(yol);
            return JsonSerializer.Deserialize<List<T>>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public void Kaydet()
    {
        Directory.CreateDirectory(_dataDir);
        KaydetDosya("YukTipiKutuphanesi.json", YukTipleri);
        KaydetDosya("KacakAkimRolesiKutuphanesi.json", KacakAkimRoleleri);
        KaydetDosya("AkimTrafosuKutuphanesi.json", AkimTrafolari);
        KaydetDosya("KondansatorKutuphanesi.json", Kondansatorler);
        KaydetDosya("BusbarKutuphanesi.json", BusbarTipleri);
        KaydetDosya("AstronomikZamanRolesiKutuphanesi.json", AstronomikZamanRoleleri);
        KaydetDosya("KontaktorKutuphanesi.json", Kontaktorler);
        KaydetDosya("DarbeKesiciKutuphanesi.json", DarbeKesiciler);
        KaydetDosya("SigortaKutuphanesi.json", Sigortalar);
        KaydetDosya("TmsKutuphanesi.json", Tmsler);
        KaydetDosya("BicakliSigortaKutuphanesi.json", BicakliSigortalar);
        KaydetDosya("KompanzasyonRolesiKutuphanesi.json", KompanzasyonRoleleri);
        KaydetDosya("ReaktorKutuphanesi.json", Reaktorler);
        KaydetDosya("SvcSurucuKutuphanesi.json", SvcSuruculer);
        KaydetDosya("HarmonikFiltreKutuphanesi.json", HarmonikFiltreler);
        KaydetDosya("MotorKutuphanesi.json", Motorlar);
        KaydetDosya("AlternatorKutuphanesi.json", Alternatorler);
        KaydetDosya("JeneratorKutuphanesi.json", Jeneratorler);
        KaydetDosya("TransferSalterKutuphanesi.json", TransferSalterleri);
        KaydetDosya("UpsKutuphanesi.json", Upsler);
        KaydetDosya("AkuKutuphanesi.json", Akuler);
        KaydetDosya("TrafoKutuphanesi.json", Trafolar);
        KaydetDosya("OgHucreKutuphanesi.json", OgHucreleri);
    }

    private void KaydetDosya<T>(string dosyaAdi, List<T> veri)
    {
        var yol = Path.Combine(_dataDir, dosyaAdi);
        var json = JsonSerializer.Serialize(veri, JsonOpts);
        File.WriteAllText(yol, json);
    }

    // === Varsayılan Veriler (JSON bulunamazsa) ===

    private static List<YukTipiTanim> VarsayilanYukTipleri() => new()
    {
        new() { Ad = "Aydınlatma",       BirimGucW = 40,   SigortaCinsi = "B", TalepFaktoru = 1.0 },
        new() { Ad = "Priz",              BirimGucW = 300,  SigortaCinsi = "B", TalepFaktoru = 1.0 },
        new() { Ad = "Islak Hacim Priz",  BirimGucW = 300,  SigortaCinsi = "B", TalepFaktoru = 1.0 },
        new() { Ad = "Mekanik",           BirimGucW = 200,  SigortaCinsi = "B", TalepFaktoru = 1.0 },
        new() { Ad = "Motor (3 Faz)",     BirimGucW = 0,    SigortaCinsi = "C", TalepFaktoru = 1.0 },
        new() { Ad = "Özel Yük",          BirimGucW = 0,    SigortaCinsi = "B", TalepFaktoru = 1.0 },
    };

    private static List<KacakAkimRolesiTanim> VarsayilanKarlar() => new()
    {
        new() { Ad = "30mA 2P 25A AC",   SensitiviteMa = 30,   AnmaAkimA = 25,  KutupSayisi = 2, Tip = "AC" },
        new() { Ad = "30mA 2P 40A AC",   SensitiviteMa = 30,   AnmaAkimA = 40,  KutupSayisi = 2, Tip = "AC" },
        new() { Ad = "30mA 4P 25A AC",   SensitiviteMa = 30,   AnmaAkimA = 25,  KutupSayisi = 4, Tip = "AC" },
        new() { Ad = "30mA 4P 40A AC",   SensitiviteMa = 30,   AnmaAkimA = 40,  KutupSayisi = 4, Tip = "AC" },
        new() { Ad = "30mA 4P 63A AC",   SensitiviteMa = 30,   AnmaAkimA = 63,  KutupSayisi = 4, Tip = "AC" },
        new() { Ad = "30mA Troid Röle 630A A",   SensitiviteMa = 30,  AnmaAkimA = 630,  KutupSayisi = 4, Tip = "A", TroidTipiMi = true, AkimTrafosuSekonderA = 5, MinAkimTrafosuIcCapMm = 70 },
        new() { Ad = "30mA Troid Röle 1250A A",  SensitiviteMa = 30,  AnmaAkimA = 1250, KutupSayisi = 4, Tip = "A", TroidTipiMi = true, AkimTrafosuSekonderA = 5, MinAkimTrafosuIcCapMm = 105 },
        new() { Ad = "100mA Troid Röle 630A A",  SensitiviteMa = 100, AnmaAkimA = 630,  KutupSayisi = 4, Tip = "A", TroidTipiMi = true, AkimTrafosuSekonderA = 5, MinAkimTrafosuIcCapMm = 70 },
        new() { Ad = "300mA 4P 63A AC",  SensitiviteMa = 300,  AnmaAkimA = 63,  KutupSayisi = 4, Tip = "AC" },
        new() { Ad = "300mA 4P 100A AC", SensitiviteMa = 300,  AnmaAkimA = 100, KutupSayisi = 4, Tip = "AC" },
        new() { Ad = "300mA Troid Röle 630A A",  SensitiviteMa = 300, AnmaAkimA = 630,  KutupSayisi = 4, Tip = "A", TroidTipiMi = true, AkimTrafosuSekonderA = 5, MinAkimTrafosuIcCapMm = 70 },
        new() { Ad = "300mA Troid Röle 1250A A", SensitiviteMa = 300, AnmaAkimA = 1250, KutupSayisi = 4, Tip = "A", TroidTipiMi = true, AkimTrafosuSekonderA = 5, MinAkimTrafosuIcCapMm = 105 },
        new() { Ad = "300mA Troid Röle 2500A A", SensitiviteMa = 300, AnmaAkimA = 2500, KutupSayisi = 4, Tip = "A", TroidTipiMi = true, AkimTrafosuSekonderA = 5, MinAkimTrafosuIcCapMm = 140 },
    };

    private static List<AkimTrafosuTanim> VarsayilanAkimTrafolari() => new()
    {
        new() { Ad = "Toroid AT 160/5 Ø70",  Tip = "Toroid", PrimerA = 160,  SekonderA = 5, IcCapMm = 70,  Sinif = "RCD", GerilimV = 400, Aciklama = "Troid kaçak akım rölesi için" },
        new() { Ad = "Toroid AT 250/5 Ø70",  Tip = "Toroid", PrimerA = 250,  SekonderA = 5, IcCapMm = 70,  Sinif = "RCD", GerilimV = 400, Aciklama = "Troid kaçak akım rölesi için" },
        new() { Ad = "Toroid AT 400/5 Ø105", Tip = "Toroid", PrimerA = 400,  SekonderA = 5, IcCapMm = 105, Sinif = "RCD", GerilimV = 400, Aciklama = "Troid kaçak akım rölesi için" },
        new() { Ad = "Toroid AT 630/5 Ø105", Tip = "Toroid", PrimerA = 630,  SekonderA = 5, IcCapMm = 105, Sinif = "RCD", GerilimV = 400, Aciklama = "Troid kaçak akım rölesi için" },
        new() { Ad = "Toroid AT 1000/5 Ø140", Tip = "Toroid", PrimerA = 1000, SekonderA = 5, IcCapMm = 140, Sinif = "RCD", GerilimV = 400, Aciklama = "Troid kaçak akım rölesi için" },
        new() { Ad = "Toroid AT 1250/5 Ø140", Tip = "Toroid", PrimerA = 1250, SekonderA = 5, IcCapMm = 140, Sinif = "RCD", GerilimV = 400, Aciklama = "Troid kaçak akım rölesi için" },
        new() { Ad = "Toroid AT 1600/5 Ø210", Tip = "Toroid", PrimerA = 1600, SekonderA = 5, IcCapMm = 210, Sinif = "RCD", GerilimV = 400, Aciklama = "Troid kaçak akım rölesi için" },
        new() { Ad = "Toroid AT 2000/5 Ø210", Tip = "Toroid", PrimerA = 2000, SekonderA = 5, IcCapMm = 210, Sinif = "RCD", GerilimV = 400, Aciklama = "Troid kaçak akım rölesi için" },
        new() { Ad = "Toroid AT 2500/5 Ø300", Tip = "Toroid", PrimerA = 2500, SekonderA = 5, IcCapMm = 300, Sinif = "RCD", GerilimV = 400, Aciklama = "Troid kaçak akım rölesi için" },
    };

    private static List<KondansatorTanim> VarsayilanKondansatorler() => new()
    {
        // Standart trifaze kondansatör kademeleri — kademeli (graded) kompanzasyon için ince adımlar
        new() { Ad = "2,5 kVAr Trifaze",  GucKVar = 2.5,   FazTipi = "Trifaze",  GerilimV = 400 },
        new() { Ad = "5 kVAr Trifaze",    GucKVar = 5.0,   FazTipi = "Trifaze",  GerilimV = 400 },
        new() { Ad = "7,5 kVAr Trifaze",  GucKVar = 7.5,   FazTipi = "Trifaze",  GerilimV = 400 },
        new() { Ad = "10 kVAr Trifaze",   GucKVar = 10.0,  FazTipi = "Trifaze",  GerilimV = 400 },
        new() { Ad = "12,5 kVAr Trifaze", GucKVar = 12.5,  FazTipi = "Trifaze",  GerilimV = 400 },
        new() { Ad = "15 kVAr Trifaze",   GucKVar = 15.0,  FazTipi = "Trifaze",  GerilimV = 400 },
        new() { Ad = "20 kVAr Trifaze",   GucKVar = 20.0,  FazTipi = "Trifaze",  GerilimV = 400 },
        new() { Ad = "25 kVAr Trifaze",   GucKVar = 25.0,  FazTipi = "Trifaze",  GerilimV = 400 },
        new() { Ad = "30 kVAr Trifaze",   GucKVar = 30.0,  FazTipi = "Trifaze",  GerilimV = 400 },
        new() { Ad = "40 kVAr Trifaze",   GucKVar = 40.0,  FazTipi = "Trifaze",  GerilimV = 400 },
        new() { Ad = "50 kVAr Trifaze",   GucKVar = 50.0,  FazTipi = "Trifaze",  GerilimV = 400 },
    };

    // EAE E-Line busbar kataloğu (KO-II + KX). Direnç değeri EAE'nin 35 °C işletme
    // sıcaklığındaki direncidir (gerilim düşümü bu değerle hesaplanır); reaktans sıcaklıktan bağımsızdır.
    // Kaynak: EAE E-Line KO-II ve E-Line KX katalogları (IEC/TS EN 61439-6).
    private static List<BusbarTanim> VarsayilanBusbarlar() => new()
    {
        // ── E-Line KO-II — Alüminyum (KOA), hava izoleli dağıtım 160–800A ──
        new() { Ad = "E-Line KOA-160A", NominalAkimA = 160, Malzeme = "Alüminyum", KesitMm2 = "",      DirencMOhmPerM = 0.379, ReaktansMOhmPerM = 0.169, Aciklama = "EAE E-Line KO-II (Al)" },
        new() { Ad = "E-Line KOA-250A", NominalAkimA = 250, Malzeme = "Alüminyum", KesitMm2 = "",      DirencMOhmPerM = 0.309, ReaktansMOhmPerM = 0.158, Aciklama = "EAE E-Line KO-II (Al)" },
        new() { Ad = "E-Line KOA-315A", NominalAkimA = 315, Malzeme = "Alüminyum", KesitMm2 = "",      DirencMOhmPerM = 0.271, ReaktansMOhmPerM = 0.147, Aciklama = "EAE E-Line KO-II (Al)" },
        new() { Ad = "E-Line KOA-400A", NominalAkimA = 400, Malzeme = "Alüminyum", KesitMm2 = "",      DirencMOhmPerM = 0.151, ReaktansMOhmPerM = 0.111, Aciklama = "EAE E-Line KO-II (Al)" },
        new() { Ad = "E-Line KOA-500A", NominalAkimA = 500, Malzeme = "Alüminyum", KesitMm2 = "",      DirencMOhmPerM = 0.127, ReaktansMOhmPerM = 0.094, Aciklama = "EAE E-Line KO-II (Al)" },
        new() { Ad = "E-Line KOA-600A", NominalAkimA = 600, Malzeme = "Alüminyum", KesitMm2 = "",      DirencMOhmPerM = 0.104, ReaktansMOhmPerM = 0.083, Aciklama = "EAE E-Line KO-II (Al)" },
        new() { Ad = "E-Line KOA-800A", NominalAkimA = 800, Malzeme = "Alüminyum", KesitMm2 = "",      DirencMOhmPerM = 0.066, ReaktansMOhmPerM = 0.064, Aciklama = "EAE E-Line KO-II (Al)" },

        // ── E-Line KO-II — Bakır (KOC) 250–800A ──
        new() { Ad = "E-Line KOC-250A", NominalAkimA = 250, Malzeme = "Bakır", KesitMm2 = "",          DirencMOhmPerM = 0.213, ReaktansMOhmPerM = 0.171, Aciklama = "EAE E-Line KO-II (Cu)" },
        new() { Ad = "E-Line KOC-315A", NominalAkimA = 315, Malzeme = "Bakır", KesitMm2 = "",          DirencMOhmPerM = 0.172, ReaktansMOhmPerM = 0.159, Aciklama = "EAE E-Line KO-II (Cu)" },
        new() { Ad = "E-Line KOC-400A", NominalAkimA = 400, Malzeme = "Bakır", KesitMm2 = "",          DirencMOhmPerM = 0.145, ReaktansMOhmPerM = 0.147, Aciklama = "EAE E-Line KO-II (Cu)" },
        new() { Ad = "E-Line KOC-600A", NominalAkimA = 600, Malzeme = "Bakır", KesitMm2 = "",          DirencMOhmPerM = 0.090, ReaktansMOhmPerM = 0.112, Aciklama = "EAE E-Line KO-II (Cu)" },
        new() { Ad = "E-Line KOC-800A", NominalAkimA = 800, Malzeme = "Bakır", KesitMm2 = "",          DirencMOhmPerM = 0.061, ReaktansMOhmPerM = 0.085, Aciklama = "EAE E-Line KO-II (Cu)" },

        // ── E-Line KX — Alüminyum (KXA), güç dağıtımı 1000–6000A ──
        new() { Ad = "E-Line KXA-1000A", NominalAkimA = 1000, Malzeme = "Alüminyum", KesitMm2 = "6x60",       DirencMOhmPerM = 0.101, ReaktansMOhmPerM = 0.020, Aciklama = "EAE E-Line KX (Al)" },
        new() { Ad = "E-Line KXA-1250A", NominalAkimA = 1250, Malzeme = "Alüminyum", KesitMm2 = "6x110",      DirencMOhmPerM = 0.058, ReaktansMOhmPerM = 0.013, Aciklama = "EAE E-Line KX (Al)" },
        new() { Ad = "E-Line KXA-1600A", NominalAkimA = 1600, Malzeme = "Alüminyum", KesitMm2 = "6x140",      DirencMOhmPerM = 0.043, ReaktansMOhmPerM = 0.010, Aciklama = "EAE E-Line KX (Al)" },
        new() { Ad = "E-Line KXA-2000A", NominalAkimA = 2000, Malzeme = "Alüminyum", KesitMm2 = "6x180",      DirencMOhmPerM = 0.035, ReaktansMOhmPerM = 0.008, Aciklama = "EAE E-Line KX (Al)" },
        new() { Ad = "E-Line KXA-2500A", NominalAkimA = 2500, Malzeme = "Alüminyum", KesitMm2 = "6x230",      DirencMOhmPerM = 0.026, ReaktansMOhmPerM = 0.007, Aciklama = "EAE E-Line KX (Al)" },
        new() { Ad = "E-Line KXA-3200A", NominalAkimA = 3200, Malzeme = "Alüminyum", KesitMm2 = "2(6x140)",   DirencMOhmPerM = 0.022, ReaktansMOhmPerM = 0.005, Aciklama = "EAE E-Line KX (Al)" },
        new() { Ad = "E-Line KXA-4000A", NominalAkimA = 4000, Malzeme = "Alüminyum", KesitMm2 = "2(6x200)",   DirencMOhmPerM = 0.016, ReaktansMOhmPerM = 0.004, Aciklama = "EAE E-Line KX (Al)" },
        new() { Ad = "E-Line KXA-5000A", NominalAkimA = 5000, Malzeme = "Alüminyum", KesitMm2 = "2(6x250)",   DirencMOhmPerM = 0.014, ReaktansMOhmPerM = 0.003, Aciklama = "EAE E-Line KX (Al)" },
        new() { Ad = "E-Line KXA-6000A", NominalAkimA = 6000, Malzeme = "Alüminyum", KesitMm2 = "",           DirencMOhmPerM = 0.010, ReaktansMOhmPerM = 0.002, Aciklama = "EAE E-Line KX (Al)" },

        // ── E-Line KX — Bakır (KXC), güç dağıtımı 1000–6300A ──
        new() { Ad = "E-Line KXC-1000A", NominalAkimA = 1000, Malzeme = "Bakır", KesitMm2 = "6x55",       DirencMOhmPerM = 0.071, ReaktansMOhmPerM = 0.023, Aciklama = "EAE E-Line KX (Cu)" },
        new() { Ad = "E-Line KXC-1250A", NominalAkimA = 1250, Malzeme = "Bakır", KesitMm2 = "6x70",       DirencMOhmPerM = 0.056, ReaktansMOhmPerM = 0.019, Aciklama = "EAE E-Line KX (Cu)" },
        new() { Ad = "E-Line KXC-1600A", NominalAkimA = 1600, Malzeme = "Bakır", KesitMm2 = "6x95",       DirencMOhmPerM = 0.044, ReaktansMOhmPerM = 0.015, Aciklama = "EAE E-Line KX (Cu)" },
        new() { Ad = "E-Line KXC-2000A", NominalAkimA = 2000, Malzeme = "Bakır", KesitMm2 = "6x125",      DirencMOhmPerM = 0.033, ReaktansMOhmPerM = 0.010, Aciklama = "EAE E-Line KX (Cu)" },
        new() { Ad = "E-Line KXC-2500A", NominalAkimA = 2500, Malzeme = "Bakır", KesitMm2 = "6x200",      DirencMOhmPerM = 0.021, ReaktansMOhmPerM = 0.008, Aciklama = "EAE E-Line KX (Cu)" },
        new() { Ad = "E-Line KXC-3300A", NominalAkimA = 3300, Malzeme = "Bakır", KesitMm2 = "2(6x110)",   DirencMOhmPerM = 0.019, ReaktansMOhmPerM = 0.007, Aciklama = "EAE E-Line KX (Cu)" },
        new() { Ad = "E-Line KXC-4000A", NominalAkimA = 4000, Malzeme = "Bakır", KesitMm2 = "2(6x140)",   DirencMOhmPerM = 0.015, ReaktansMOhmPerM = 0.005, Aciklama = "EAE E-Line KX (Cu)" },
        new() { Ad = "E-Line KXC-5000A", NominalAkimA = 5000, Malzeme = "Bakır", KesitMm2 = "2(6x200)",   DirencMOhmPerM = 0.010, ReaktansMOhmPerM = 0.004, Aciklama = "EAE E-Line KX (Cu)" },
        new() { Ad = "E-Line KXC-6300A", NominalAkimA = 6300, Malzeme = "Bakır", KesitMm2 = "",           DirencMOhmPerM = 0.006, ReaktansMOhmPerM = 0.003, Aciklama = "EAE E-Line KX (Cu)" },
    };

    private static List<AstronomikZamanRolesiTanim> VarsayilanAzrler() => new()
    {
        new() { Ad = "AZR 16A 1Ç",  AnmaAkimA = 16, CikisSayisi = 1, KutupSayisi = 1, BeslemeGerilimV = "230" },
        new() { Ad = "AZR 16A 2Ç",  AnmaAkimA = 16, CikisSayisi = 2, KutupSayisi = 1, BeslemeGerilimV = "230" },
        new() { Ad = "AZR 25A 1Ç",  AnmaAkimA = 25, CikisSayisi = 1, KutupSayisi = 1, BeslemeGerilimV = "230" },
        new() { Ad = "AZR 25A 2Ç",  AnmaAkimA = 25, CikisSayisi = 2, KutupSayisi = 1, BeslemeGerilimV = "230" },
        new() { Ad = "AZR 40A 4Ç",  AnmaAkimA = 40, CikisSayisi = 4, KutupSayisi = 1, BeslemeGerilimV = "230" },
    };

    private static List<KontaktorTanim> VarsayilanKontaktorler() => new()
    {
        // AC-3 (motor) anma akımına göre tipik kontaktör serisi; aydınlatma (AC-1) için de kullanılır.
        new() { Ad = "Kontaktör 9A 3P",   AnmaAkimA = 9,   KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~4 kW" },
        new() { Ad = "Kontaktör 12A 3P",  AnmaAkimA = 12,  KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~5,5 kW" },
        new() { Ad = "Kontaktör 18A 3P",  AnmaAkimA = 18,  KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~7,5 kW" },
        new() { Ad = "Kontaktör 25A 3P",  AnmaAkimA = 25,  KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~11 kW" },
        new() { Ad = "Kontaktör 32A 3P",  AnmaAkimA = 32,  KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~15 kW" },
        new() { Ad = "Kontaktör 40A 3P",  AnmaAkimA = 40,  KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~18,5 kW" },
        new() { Ad = "Kontaktör 50A 3P",  AnmaAkimA = 50,  KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~22 kW" },
        new() { Ad = "Kontaktör 65A 3P",  AnmaAkimA = 65,  KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~30 kW" },
        new() { Ad = "Kontaktör 80A 3P",  AnmaAkimA = 80,  KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~37 kW" },
        new() { Ad = "Kontaktör 95A 3P",  AnmaAkimA = 95,  KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~45 kW" },
        new() { Ad = "Kontaktör 115A 3P", AnmaAkimA = 115, KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~55 kW" },
        new() { Ad = "Kontaktör 150A 3P", AnmaAkimA = 150, KutupSayisi = 3, BobinGerilimV = 230, Aciklama = "AC-3 ~75 kW" },
        // 4P (aydınlatma/genel amaçlı, nötr dahil) seçenekleri
        new() { Ad = "Kontaktör 25A 4P",  AnmaAkimA = 25,  KutupSayisi = 4, BobinGerilimV = 230, Aciklama = "AC-1 aydınlatma" },
        new() { Ad = "Kontaktör 40A 4P",  AnmaAkimA = 40,  KutupSayisi = 4, BobinGerilimV = 230, Aciklama = "AC-1 aydınlatma" },
        new() { Ad = "Kontaktör 63A 4P",  AnmaAkimA = 63,  KutupSayisi = 4, BobinGerilimV = 230, Aciklama = "AC-1 aydınlatma" },
        // 400V bobinli ağır seri
        new() { Ad = "Kontaktör 185A 3P (400V)", AnmaAkimA = 185, KutupSayisi = 3, BobinGerilimV = 400, Aciklama = "AC-3 ~90 kW" },
        new() { Ad = "Kontaktör 225A 3P (400V)", AnmaAkimA = 225, KutupSayisi = 3, BobinGerilimV = 400, Aciklama = "AC-3 ~110 kW" },
    };

    private static List<SigortaTanim> VarsayilanSigortalar() => new()
    {
        // MCB (otomatik sigorta) ≤ 63A; her anma akımı 6 ve 10 kA kesme kapasitesiyle.
        new() { Ad = "MCB 6A C 6kA",   AnmaAkimA = 6,  Karakteristik = "C", KesmeKapasitesiKA = 6,  KutupSayisi = 1 },
        new() { Ad = "MCB 10A C 6kA",  AnmaAkimA = 10, Karakteristik = "C", KesmeKapasitesiKA = 6,  KutupSayisi = 1 },
        new() { Ad = "MCB 16A C 6kA",  AnmaAkimA = 16, Karakteristik = "C", KesmeKapasitesiKA = 6,  KutupSayisi = 1 },
        new() { Ad = "MCB 20A C 6kA",  AnmaAkimA = 20, Karakteristik = "C", KesmeKapasitesiKA = 6,  KutupSayisi = 1 },
        new() { Ad = "MCB 25A C 6kA",  AnmaAkimA = 25, Karakteristik = "C", KesmeKapasitesiKA = 6,  KutupSayisi = 1 },
        new() { Ad = "MCB 32A C 6kA",  AnmaAkimA = 32, Karakteristik = "C", KesmeKapasitesiKA = 6,  KutupSayisi = 1 },
        new() { Ad = "MCB 40A C 6kA",  AnmaAkimA = 40, Karakteristik = "C", KesmeKapasitesiKA = 6,  KutupSayisi = 1 },
        new() { Ad = "MCB 50A C 6kA",  AnmaAkimA = 50, Karakteristik = "C", KesmeKapasitesiKA = 6,  KutupSayisi = 1 },
        new() { Ad = "MCB 63A C 6kA",  AnmaAkimA = 63, Karakteristik = "C", KesmeKapasitesiKA = 6,  KutupSayisi = 1 },
        new() { Ad = "MCB 16A C 10kA", AnmaAkimA = 16, Karakteristik = "C", KesmeKapasitesiKA = 10, KutupSayisi = 1 },
        new() { Ad = "MCB 20A C 10kA", AnmaAkimA = 20, Karakteristik = "C", KesmeKapasitesiKA = 10, KutupSayisi = 1 },
        new() { Ad = "MCB 25A C 10kA", AnmaAkimA = 25, Karakteristik = "C", KesmeKapasitesiKA = 10, KutupSayisi = 1 },
        new() { Ad = "MCB 32A C 10kA", AnmaAkimA = 32, Karakteristik = "C", KesmeKapasitesiKA = 10, KutupSayisi = 1 },
        new() { Ad = "MCB 40A C 10kA", AnmaAkimA = 40, Karakteristik = "C", KesmeKapasitesiKA = 10, KutupSayisi = 1 },
        new() { Ad = "MCB 50A C 10kA", AnmaAkimA = 50, Karakteristik = "C", KesmeKapasitesiKA = 10, KutupSayisi = 1 },
        new() { Ad = "MCB 63A C 10kA", AnmaAkimA = 63, Karakteristik = "C", KesmeKapasitesiKA = 10, KutupSayisi = 1 },
    };

    private static List<TmsTanim> VarsayilanTmsler() => new()
    {
        // TMŞ / kompakt şalter (MCCB) > 63A; kesme kapasitesi 36 / 50 / 100 kA.
        new() { Ad = "TMŞ 80A 36kA 3P",   AnmaAkimA = 80,   KesmeKapasitesiKA = 36,  KutupSayisi = 3 },
        new() { Ad = "TMŞ 100A 36kA 3P",  AnmaAkimA = 100,  KesmeKapasitesiKA = 36,  KutupSayisi = 3 },
        new() { Ad = "TMŞ 125A 36kA 3P",  AnmaAkimA = 125,  KesmeKapasitesiKA = 36,  KutupSayisi = 3 },
        new() { Ad = "TMŞ 160A 36kA 3P",  AnmaAkimA = 160,  KesmeKapasitesiKA = 36,  KutupSayisi = 3 },
        new() { Ad = "TMŞ 250A 50kA 3P",  AnmaAkimA = 250,  KesmeKapasitesiKA = 50,  KutupSayisi = 3 },
        new() { Ad = "TMŞ 400A 50kA 3P",  AnmaAkimA = 400,  KesmeKapasitesiKA = 50,  KutupSayisi = 3 },
        new() { Ad = "TMŞ 630A 50kA 3P",  AnmaAkimA = 630,  KesmeKapasitesiKA = 50,  KutupSayisi = 3 },
        new() { Ad = "TMŞ 250A 100kA 3P", AnmaAkimA = 250,  KesmeKapasitesiKA = 100, KutupSayisi = 3 },
        new() { Ad = "TMŞ 400A 100kA 3P", AnmaAkimA = 400,  KesmeKapasitesiKA = 100, KutupSayisi = 3 },
        new() { Ad = "TMŞ 630A 100kA 3P", AnmaAkimA = 630,  KesmeKapasitesiKA = 100, KutupSayisi = 3 },
        new() { Ad = "TMŞ 800A 100kA 4P", AnmaAkimA = 800,  KesmeKapasitesiKA = 100, KutupSayisi = 4 },
        new() { Ad = "TMŞ 1000A 100kA 4P",AnmaAkimA = 1000, KesmeKapasitesiKA = 100, KutupSayisi = 4 },
    };

    private static List<DarbeKesiciTanim> VarsayilanDarbeKesiciler() => new()
    {
        new() { Ad = "Darbe Kesici 16A 1P", AnmaAkimA = 16, KutupSayisi = 1, BobinGerilimV = 230 },
        new() { Ad = "Darbe Kesici 25A 1P", AnmaAkimA = 25, KutupSayisi = 1, BobinGerilimV = 230 },
        new() { Ad = "Darbe Kesici 32A 2P", AnmaAkimA = 32, KutupSayisi = 2, BobinGerilimV = 230 },
        new() { Ad = "Darbe Kesici 40A 2P", AnmaAkimA = 40, KutupSayisi = 2, BobinGerilimV = 230 },
        new() { Ad = "Darbe Kesici 63A 4P", AnmaAkimA = 63, KutupSayisi = 4, BobinGerilimV = 230 },
    };

    private static List<BicakliSigortaTanim> VarsayilanBicakliSigortalar() => new()
    {
        new() { Ad = "NH00 63A gG",   Boy = "NH00", AnmaAkimA = 63,   Karakteristik = "gG", KesmeKapasitesiKA = 120, GerilimV = 500 },
        new() { Ad = "NH00 100A gG",  Boy = "NH00", AnmaAkimA = 100,  Karakteristik = "gG", KesmeKapasitesiKA = 120, GerilimV = 500 },
        new() { Ad = "NH00 160A gG",  Boy = "NH00", AnmaAkimA = 160,  Karakteristik = "gG", KesmeKapasitesiKA = 120, GerilimV = 500 },
        new() { Ad = "NH1 200A gG",   Boy = "NH1",  AnmaAkimA = 200,  Karakteristik = "gG", KesmeKapasitesiKA = 120, GerilimV = 500 },
        new() { Ad = "NH1 250A gG",   Boy = "NH1",  AnmaAkimA = 250,  Karakteristik = "gG", KesmeKapasitesiKA = 120, GerilimV = 500 },
        new() { Ad = "NH2 315A gG",   Boy = "NH2",  AnmaAkimA = 315,  Karakteristik = "gG", KesmeKapasitesiKA = 120, GerilimV = 500 },
        new() { Ad = "NH2 400A gG",   Boy = "NH2",  AnmaAkimA = 400,  Karakteristik = "gG", KesmeKapasitesiKA = 120, GerilimV = 500 },
        new() { Ad = "NH3 500A gG",   Boy = "NH3",  AnmaAkimA = 500,  Karakteristik = "gG", KesmeKapasitesiKA = 120, GerilimV = 500 },
        new() { Ad = "NH3 630A gG",   Boy = "NH3",  AnmaAkimA = 630,  Karakteristik = "gG", KesmeKapasitesiKA = 120, GerilimV = 500 },
        new() { Ad = "NH00 50A aM",   Boy = "NH00", AnmaAkimA = 50,   Karakteristik = "aM", KesmeKapasitesiKA = 120, GerilimV = 500, Aciklama = "Motor koruma" },
        new() { Ad = "NH00 100A aM",  Boy = "NH00", AnmaAkimA = 100,  Karakteristik = "aM", KesmeKapasitesiKA = 120, GerilimV = 500, Aciklama = "Motor koruma" },
    };

    private static List<KompanzasyonRolesiTanim> VarsayilanKompRoleleri() => new()
    {
        new() { Ad = "Komp Rölesi 6 Kademe",       KademeSayisi = 6,  SvcDestekli = false, CtOrani = "100/5",  IletisimArayuzu = "RS-485" },
        new() { Ad = "Komp Rölesi 12 Kademe",      KademeSayisi = 12, SvcDestekli = false, CtOrani = "200/5",  IletisimArayuzu = "RS-485" },
        new() { Ad = "Komp Rölesi 18 Kademe",      KademeSayisi = 18, SvcDestekli = false, CtOrani = "400/5",  IletisimArayuzu = "Modbus" },
        new() { Ad = "Komp Rölesi 6 Kademe SVC",   KademeSayisi = 6,  SvcDestekli = true,  CtOrani = "100/5",  IletisimArayuzu = "RS-485" },
        new() { Ad = "Komp Rölesi 12 Kademe SVC",  KademeSayisi = 12, SvcDestekli = true,  CtOrani = "200/5",  IletisimArayuzu = "Modbus" },
        new() { Ad = "Komp Rölesi 18 Kademe SVC",  KademeSayisi = 18, SvcDestekli = true,  CtOrani = "400/5",  IletisimArayuzu = "Modbus" },
    };

    private static List<ReaktorTanim> VarsayilanReaktorler() => new()
    {
        new() { Ad = "Reaktör 5 kVAr 7%",    GucKVar = 5,   AkortFrekansHz = 189, AkortYuzde = 5.67, GerilimV = 400, FazTipi = "Trifaze" },
        new() { Ad = "Reaktör 10 kVAr 7%",   GucKVar = 10,  AkortFrekansHz = 189, AkortYuzde = 5.67, GerilimV = 400, FazTipi = "Trifaze" },
        new() { Ad = "Reaktör 12.5 kVAr 7%", GucKVar = 12.5,AkortFrekansHz = 189, AkortYuzde = 5.67, GerilimV = 400, FazTipi = "Trifaze" },
        new() { Ad = "Reaktör 25 kVAr 7%",   GucKVar = 25,  AkortFrekansHz = 189, AkortYuzde = 5.67, GerilimV = 400, FazTipi = "Trifaze" },
        new() { Ad = "Reaktör 50 kVAr 7%",   GucKVar = 50,  AkortFrekansHz = 189, AkortYuzde = 5.67, GerilimV = 400, FazTipi = "Trifaze" },
        new() { Ad = "Reaktör 25 kVAr 14%",  GucKVar = 25,  AkortFrekansHz = 134, AkortYuzde = 14.0, GerilimV = 400, FazTipi = "Trifaze", Aciklama = "Audio-frekans dalgalanma riski olan tesisler" },
        new() { Ad = "Reaktör 50 kVAr 14%",  GucKVar = 50,  AkortFrekansHz = 134, AkortYuzde = 14.0, GerilimV = 400, FazTipi = "Trifaze", Aciklama = "Audio-frekans dalgalanma riski olan tesisler" },
    };

    private static List<SvcSurucuTanim> VarsayilanSvcSuruculer() => new()
    {
        new() { Ad = "SVC Sürücü 12.5 kVAr",  KademeGucKVar = 12.5, AnmaAkimA = 30,  GerilimV = 400, SogutmaTipi = "Doğal" },
        new() { Ad = "SVC Sürücü 25 kVAr",    KademeGucKVar = 25,   AnmaAkimA = 60,  GerilimV = 400, SogutmaTipi = "Doğal" },
        new() { Ad = "SVC Sürücü 50 kVAr",    KademeGucKVar = 50,   AnmaAkimA = 120, GerilimV = 400, SogutmaTipi = "Fanlı" },
        new() { Ad = "SVC Sürücü 75 kVAr",    KademeGucKVar = 75,   AnmaAkimA = 180, GerilimV = 400, SogutmaTipi = "Fanlı" },
        new() { Ad = "SVC Sürücü 100 kVAr",   KademeGucKVar = 100,  AnmaAkimA = 240, GerilimV = 400, SogutmaTipi = "Fanlı" },
    };

    private static List<HarmonikFiltreTanim> VarsayilanHarmonikFiltreler() => new()
    {
        new() { Ad = "Pasif Filtre 25 kVAr (5. harm.)",  Tipi = "Pasif", FiltreFrekansHz = 250,  GucKVar = 25,  AnmaAkimA = 40,  ThdYuzde = 5.0, GerilimV = 400 },
        new() { Ad = "Pasif Filtre 50 kVAr (5. harm.)",  Tipi = "Pasif", FiltreFrekansHz = 250,  GucKVar = 50,  AnmaAkimA = 80,  ThdYuzde = 5.0, GerilimV = 400 },
        new() { Ad = "Pasif Filtre 100 kVAr (5. harm.)", Tipi = "Pasif", FiltreFrekansHz = 250,  GucKVar = 100, AnmaAkimA = 160, ThdYuzde = 5.0, GerilimV = 400 },
        new() { Ad = "Pasif Filtre 50 kVAr (7. harm.)",  Tipi = "Pasif", FiltreFrekansHz = 350,  GucKVar = 50,  AnmaAkimA = 80,  ThdYuzde = 5.0, GerilimV = 400 },
        new() { Ad = "Aktif Filtre 60 A",                 Tipi = "Aktif", FiltreFrekansHz = 0,    GucKVar = 0,   AnmaAkimA = 60,  ThdYuzde = 3.0, GerilimV = 400, Aciklama = "Tüm spektrumu kompanse eder" },
        new() { Ad = "Aktif Filtre 100 A",                Tipi = "Aktif", FiltreFrekansHz = 0,    GucKVar = 0,   AnmaAkimA = 100, ThdYuzde = 3.0, GerilimV = 400, Aciklama = "Tüm spektrumu kompanse eder" },
        new() { Ad = "Aktif Filtre 200 A",                Tipi = "Aktif", FiltreFrekansHz = 0,    GucKVar = 0,   AnmaAkimA = 200, ThdYuzde = 3.0, GerilimV = 400, Aciklama = "Tüm spektrumu kompanse eder" },
        new() { Ad = "Hibrit Filtre 50 kVAr + 60A",       Tipi = "Hibrit",FiltreFrekansHz = 250,  GucKVar = 50,  AnmaAkimA = 60,  ThdYuzde = 4.0, GerilimV = 400, Aciklama = "Pasif + Aktif kombine" },
    };

    private static List<MotorTanim> VarsayilanMotorlar() => new()
    {
        new() { Ad = "Perkins 1103A-33TG1",   Marka = "Perkins", Model = "1103A-33TG1", YakitTipi = "Dizel", SilindirSayisi = 3, HacimL = 3.3, DevirRpm = 1500, GucKW = 36,  Sogutma = "Su" },
        new() { Ad = "Perkins 1104A-44TAG2",  Marka = "Perkins", Model = "1104A-44TAG2", YakitTipi = "Dizel", SilindirSayisi = 4, HacimL = 4.4, DevirRpm = 1500, GucKW = 90,  Sogutma = "Su" },
        new() { Ad = "Perkins 1106A-70TAG4",  Marka = "Perkins", Model = "1106A-70TAG4", YakitTipi = "Dizel", SilindirSayisi = 6, HacimL = 7.0, DevirRpm = 1500, GucKW = 175, Sogutma = "Su" },
        new() { Ad = "Volvo TAD734GE",         Marka = "Volvo",   Model = "TAD734GE",     YakitTipi = "Dizel", SilindirSayisi = 6, HacimL = 7.1, DevirRpm = 1500, GucKW = 185, Sogutma = "Su" },
        new() { Ad = "Cummins QSL9-G7",        Marka = "Cummins", Model = "QSL9-G7",      YakitTipi = "Dizel", SilindirSayisi = 6, HacimL = 8.9, DevirRpm = 1500, GucKW = 250, Sogutma = "Su" },
        new() { Ad = "MAN E2876 LE302",        Marka = "MAN",     Model = "E2876 LE302",  YakitTipi = "Doğalgaz", SilindirSayisi = 6, HacimL = 12.8, DevirRpm = 1500, GucKW = 220, Sogutma = "Su" },
    };

    private static List<AlternatorTanim> VarsayilanAlternatorler() => new()
    {
        new() { Ad = "Stamford UCI224F",   Marka = "Stamford", Model = "UCI224F",  AnmaGucKVA = 45,  GerilimV = 400, FazTipi = "Trifaze", CosF = 0.8, FrekansHz = 50, VerimYuzde = 90 },
        new() { Ad = "Stamford UCI274D",   Marka = "Stamford", Model = "UCI274D",  AnmaGucKVA = 100, GerilimV = 400, FazTipi = "Trifaze", CosF = 0.8, FrekansHz = 50, VerimYuzde = 92 },
        new() { Ad = "Stamford UCI274F",   Marka = "Stamford", Model = "UCI274F",  AnmaGucKVA = 200, GerilimV = 400, FazTipi = "Trifaze", CosF = 0.8, FrekansHz = 50, VerimYuzde = 93 },
        new() { Ad = "Leroy-Somer LSA47.2",Marka = "Leroy-Somer", Model = "LSA47.2", AnmaGucKVA = 250, GerilimV = 400, FazTipi = "Trifaze", CosF = 0.8, FrekansHz = 50, VerimYuzde = 93 },
        new() { Ad = "Leroy-Somer LSA50.2",Marka = "Leroy-Somer", Model = "LSA50.2", AnmaGucKVA = 400, GerilimV = 400, FazTipi = "Trifaze", CosF = 0.8, FrekansHz = 50, VerimYuzde = 94 },
        new() { Ad = "Mecc Alte ECP34",    Marka = "Mecc Alte",   Model = "ECP34",   AnmaGucKVA = 600, GerilimV = 400, FazTipi = "Trifaze", CosF = 0.8, FrekansHz = 50, VerimYuzde = 94 },
    };

    private static List<JeneratorTanim> VarsayilanJeneratorler()
    {
        var liste = new List<JeneratorTanim>
        {
            // Küçük (konut / küçük işletme) - eski kayıt adları geriye dönük uyumluluk için korunur.
            new() { Ad = "JEN-40",   Marka = "Aksa",       Model = "APD 50A",   PrimeGucKVA = 40,  PrimeGucKW = 32,  StandbyGucKVA = 45,  StandbyGucKW = 36,  MotorAd = "Perkins 1103A-33TG1",  AlternatorAd = "Stamford UCI224F",  GerilimV = 400, FrekansHz = 50, Kabin = "Sessiz", SesSeviyeDb = 72, YakitTuketimLh100 = 9,  YakitDeposuL = 100, AgirlikKg = 950 },
            new() { Ad = "JEN-80",   Marka = "Aksa",       Model = "APD 88A",   PrimeGucKVA = 80,  PrimeGucKW = 64,  StandbyGucKVA = 88,  StandbyGucKW = 70,  MotorAd = "Perkins 1104A-44TAG2", AlternatorAd = "Stamford UCI274D",  GerilimV = 400, FrekansHz = 50, Kabin = "Sessiz", SesSeviyeDb = 74, YakitTuketimLh100 = 18, YakitDeposuL = 180, AgirlikKg = 1450 },
            new() { Ad = "JEN-150",  Marka = "Teksan",     Model = "TJ165",     PrimeGucKVA = 150, PrimeGucKW = 120, StandbyGucKVA = 165, StandbyGucKW = 132, MotorAd = "Perkins 1106A-70TAG4", AlternatorAd = "Stamford UCI274F",  GerilimV = 400, FrekansHz = 50, Kabin = "Sessiz", SesSeviyeDb = 75, YakitTuketimLh100 = 32, YakitDeposuL = 300, AgirlikKg = 2100 },
            new() { Ad = "JEN-200",  Marka = "Teksan",     Model = "TJ220",     PrimeGucKVA = 200, PrimeGucKW = 160, StandbyGucKVA = 220, StandbyGucKW = 176, MotorAd = "Volvo TAD734GE",       AlternatorAd = "Stamford UCI274F",  GerilimV = 400, FrekansHz = 50, Kabin = "Sessiz", SesSeviyeDb = 75, YakitTuketimLh100 = 42, YakitDeposuL = 400, AgirlikKg = 2500 },
            new() { Ad = "JEN-275",  Marka = "FG Wilson",  Model = "P275-3",    PrimeGucKVA = 275, PrimeGucKW = 220, StandbyGucKVA = 300, StandbyGucKW = 240, MotorAd = "Cummins QSL9-G7",      AlternatorAd = "Leroy-Somer LSA47.2", GerilimV = 400, FrekansHz = 50, Kabin = "Sessiz", SesSeviyeDb = 76, YakitTuketimLh100 = 58, YakitDeposuL = 500, AgirlikKg = 3200 },
            new() { Ad = "JEN-400",  Marka = "FG Wilson",  Model = "P400-3",    PrimeGucKVA = 400, PrimeGucKW = 320, StandbyGucKVA = 440, StandbyGucKW = 352, MotorAd = "Volvo TAD734GE",       AlternatorAd = "Leroy-Somer LSA50.2", GerilimV = 400, FrekansHz = 50, Kabin = "Sessiz", SesSeviyeDb = 78, YakitTuketimLh100 = 80, YakitDeposuL = 600, AgirlikKg = 4500 },
            new() { Ad = "JEN-600",  Marka = "Caterpillar", Model = "C18 PGAI", PrimeGucKVA = 600, PrimeGucKW = 480, StandbyGucKVA = 660, StandbyGucKW = 528, MotorAd = "Cummins QSL9-G7",      AlternatorAd = "Mecc Alte ECP34",   GerilimV = 400, FrekansHz = 50, Kabin = "Sessiz", SesSeviyeDb = 80, YakitTuketimLh100 = 120, YakitDeposuL = 800, AgirlikKg = 6500 },
            // Doğalgaz alternatifi
            new() { Ad = "JEN-220G", Marka = "MWM",        Model = "TCG2016-V12", PrimeGucKVA = 220, PrimeGucKW = 176, StandbyGucKVA = 240, StandbyGucKW = 192, MotorAd = "MAN E2876 LE302", AlternatorAd = "Stamford UCI274F", GerilimV = 400, FrekansHz = 50, Kabin = "Sessiz", SesSeviyeDb = 78, YakitTuketimLh100 = 0, YakitDeposuL = 0, AgirlikKg = 2800, Aciklama = "Doğalgaz kojenerasyon" },
        };

        liste.AddRange(MarkaJeneratorSerisi("Aksa", "APD"));
        liste.AddRange(MarkaJeneratorSerisi("Emsa", "EM"));
        liste.AddRange(MarkaJeneratorSerisi("Arken", "ARK"));
        liste.AddRange(MarkaJeneratorSerisi("Alimar", "ALM"));
        return liste;
    }

    private static IEnumerable<JeneratorTanim> MarkaJeneratorSerisi(string marka, string modelOnEk)
    {
        var gucler = new (double PrimeKVA, double StandbyKVA)[]
        {
            (50, 55), (100, 110), (150, 165), (200, 220), (275, 300), (400, 440),
            (500, 550), (630, 700), (800, 880), (1000, 1100), (1250, 1375),
            (1600, 1760), (2000, 2200)
        };

        foreach (var (primeKVA, standbyKVA) in gucler)
        {
            double primeKW = primeKVA * 0.8;
            double standbyKW = standbyKVA * 0.8;
            string motor = primeKVA switch
            {
                <= 50 => "Perkins 1103A-33TG1",
                <= 150 => "Perkins 1104A-44TAG2",
                <= 275 => "Perkins 1106A-70TAG4",
                <= 500 => "Volvo TAD734GE",
                _ => "Cummins QSL9-G7"
            };
            string alternator = primeKVA switch
            {
                <= 50 => "Stamford UCI224F",
                <= 150 => "Stamford UCI274D",
                <= 275 => "Stamford UCI274F",
                <= 500 => "Leroy-Somer LSA50.2",
                _ => "Mecc Alte ECP34"
            };

            yield return new JeneratorTanim
            {
                Ad = $"{marka} {modelOnEk}-{standbyKVA:0}",
                Marka = marka,
                Model = $"{modelOnEk} {standbyKVA:0} kVA",
                PrimeGucKVA = primeKVA,
                PrimeGucKW = primeKW,
                StandbyGucKVA = standbyKVA,
                StandbyGucKW = standbyKW,
                MotorAd = motor,
                AlternatorAd = alternator,
                GerilimV = 400,
                FrekansHz = 50,
                Kabin = "Sessiz",
                SesSeviyeDb = TahminiSesSeviyesiDb(primeKVA),
                YakitTuketimLh100 = TahminiYakitTuketimiLh(primeKW),
                YakitDeposuL = TahminiYakitDeposuL(primeKVA),
                AgirlikKg = TahminiJeneratorAgirligiKg(primeKVA),
                Aciklama = "Standart 50Hz dizel jeneratör serisi; kesin değerler proje/katalog seçimiyle güncellenebilir."
            };
        }
    }

    private static double TahminiYakitTuketimiLh(double primeKW)
        => Math.Round(primeKW * 0.25, 1);

    private static double TahminiYakitDeposuL(double primeKVA)
        => Math.Round(Math.Max(100, primeKVA * 1.4) / 10.0) * 10.0;

    private static double TahminiJeneratorAgirligiKg(double primeKVA)
        => Math.Round((700 + primeKVA * 8.5) / 10.0) * 10.0;

    private static double TahminiSesSeviyesiDb(double primeKVA)
        => primeKVA switch
        {
            <= 100 => 72,
            <= 275 => 75,
            <= 630 => 78,
            <= 1250 => 80,
            _ => 82
        };

    private static List<TransferSalterTanim> VarsayilanTransferSalterleri() => new()
    {
        new() { Ad = "ATS 100A 4P",  Marka = "Socomec", Model = "ATyS d M",   AnmaAkimA = 100,  KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 100, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 160A 4P",  Marka = "Socomec", Model = "ATyS d M",   AnmaAkimA = 160,  KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 100, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 250A 4P",  Marka = "Socomec", Model = "ATyS d M",   AnmaAkimA = 250,  KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 120, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 400A 4P",  Marka = "ABB",     Model = "OTM400E4_3D", AnmaAkimA = 400,  KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 150, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 630A 4P",  Marka = "ABB",     Model = "OTM630E4_3D", AnmaAkimA = 630,  KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 180, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 800A 4P",  Marka = "Schneider", Model = "TransferPacT", AnmaAkimA = 800, KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 200, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 1250A 4P", Marka = "Schneider", Model = "TransferPacT", AnmaAkimA = 1250, KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 250, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 1600A 4P", Marka = "Schneider", Model = "TransferPacT", AnmaAkimA = 1600, KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 300, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 2000A 4P", Marka = "ABB",     Model = "TruONE",      AnmaAkimA = 2000, KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 300, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 2500A 4P", Marka = "ABB",     Model = "TruONE",      AnmaAkimA = 2500, KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 320, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 3200A 4P", Marka = "Socomec", Model = "ATyS p",      AnmaAkimA = 3200, KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 350, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 4000A 4P", Marka = "Socomec", Model = "ATyS p",      AnmaAkimA = 4000, KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 400, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 5000A 4P", Marka = "Socomec", Model = "ATyS p",      AnmaAkimA = 5000, KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 450, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "ATS 6300A 4P", Marka = "Socomec", Model = "ATyS p",      AnmaAkimA = 6300, KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 500, KontrolTipi = "Mikroişlemci" },
        new() { Ad = "MTS 250A 4P",  Marka = "Socomec", Model = "Atys",        AnmaAkimA = 250, KutupSayisi = 4, GerilimV = 400, Tipi = "MTS", TransferSuresiMs = 0, KontrolTipi = "Manuel" },
    };

    private static List<UpsTanim> VarsayilanUpsler() => new()
    {
        // Monofaze çıkışlı küçük online UPS'ler (DC bara ~48–96V)
        new() { Ad = "UPS-6",   Marka = "Socomec", Model = "Masterys GP 6",   GucKVA = 6,   GucKW = 6,   VerimYuzde = 95, GirisCosF = 0.99, DcBaraGerilimV = 192, MinDcBaraGerilimV = 160, GirisSalterA = 16,  BypassSalterA = 16,  Topoloji = "Online (VFI)" },
        new() { Ad = "UPS-10",  Marka = "Socomec", Model = "Masterys GP 10",  GucKVA = 10,  GucKW = 10,  VerimYuzde = 95, GirisCosF = 0.99, DcBaraGerilimV = 240, MinDcBaraGerilimV = 200, GirisSalterA = 25,  BypassSalterA = 25,  Topoloji = "Online (VFI)" },
        new() { Ad = "UPS-20",  Marka = "Socomec", Model = "Masterys GP 20",  GucKVA = 20,  GucKW = 20,  VerimYuzde = 96, GirisCosF = 0.99, DcBaraGerilimV = 384, MinDcBaraGerilimV = 320, GirisSalterA = 40,  BypassSalterA = 40,  Topoloji = "Online (VFI)" },
        new() { Ad = "UPS-30",  Marka = "Eaton",   Model = "93PS 30",         GucKVA = 30,  GucKW = 30,  VerimYuzde = 96, GirisCosF = 0.99, DcBaraGerilimV = 384, MinDcBaraGerilimV = 320, GirisSalterA = 63,  BypassSalterA = 63,  Topoloji = "Online (VFI)" },
        new() { Ad = "UPS-60",  Marka = "Eaton",   Model = "93PS 60",         GucKVA = 60,  GucKW = 60,  VerimYuzde = 96, GirisCosF = 0.99, DcBaraGerilimV = 480, MinDcBaraGerilimV = 400, GirisSalterA = 125, BypassSalterA = 125, Topoloji = "Online (VFI)" },
        new() { Ad = "UPS-100", Marka = "ABB",     Model = "PowerWave 33 100", GucKVA = 100, GucKW = 100, VerimYuzde = 96, GirisCosF = 0.99, DcBaraGerilimV = 480, MinDcBaraGerilimV = 400, GirisSalterA = 200, BypassSalterA = 200, Topoloji = "Online (VFI)" },
        new() { Ad = "UPS-160", Marka = "ABB",     Model = "PowerWave 33 160", GucKVA = 160, GucKW = 160, VerimYuzde = 97, GirisCosF = 0.99, DcBaraGerilimV = 480, MinDcBaraGerilimV = 400, GirisSalterA = 315, BypassSalterA = 315, Topoloji = "Online (VFI)" },
    };

    private static List<AkuTanim> VarsayilanAkuler() => new()
    {
        // VRLA 12V monobloklar — sabit-güç deşarj tablosu (W/blok @ dakika, ~1.75V/hücre Uf)
        new()
        {
            Ad = "VRLA 12V 7Ah", Marka = "CSB", Model = "GP1272", Tip = "VRLA",
            BlokGerilimV = 12, KapasiteAh = 7, DesarjSonuGerilimV = 10.5, AgirlikKg = 2.4,
            DesarjTablosu = new() {
                new() { Dakika = 5,  BlokGucW = 130 }, new() { Dakika = 10, BlokGucW = 90 },
                new() { Dakika = 15, BlokGucW = 68 },  new() { Dakika = 30, BlokGucW = 40 },
            }
        },
        new()
        {
            Ad = "VRLA 12V 9Ah", Marka = "CSB", Model = "HR1234W", Tip = "VRLA",
            BlokGerilimV = 12, KapasiteAh = 9, DesarjSonuGerilimV = 10.5, AgirlikKg = 2.6,
            DesarjTablosu = new() {
                new() { Dakika = 5,  BlokGucW = 165 }, new() { Dakika = 10, BlokGucW = 115 },
                new() { Dakika = 15, BlokGucW = 87 },  new() { Dakika = 30, BlokGucW = 52 },
            }
        },
        new()
        {
            Ad = "VRLA 12V 26Ah", Marka = "Yuasa", Model = "NP26-12", Tip = "VRLA",
            BlokGerilimV = 12, KapasiteAh = 26, DesarjSonuGerilimV = 10.5, AgirlikKg = 9.5,
            DesarjTablosu = new() {
                new() { Dakika = 5,  BlokGucW = 430 }, new() { Dakika = 10, BlokGucW = 300 },
                new() { Dakika = 15, BlokGucW = 230 }, new() { Dakika = 30, BlokGucW = 140 },
            }
        },
        new()
        {
            Ad = "VRLA 12V 65Ah", Marka = "Yuasa", Model = "NP65-12", Tip = "VRLA",
            BlokGerilimV = 12, KapasiteAh = 65, DesarjSonuGerilimV = 10.5, AgirlikKg = 22,
            DesarjTablosu = new() {
                new() { Dakika = 5,  BlokGucW = 980 }, new() { Dakika = 10, BlokGucW = 700 },
                new() { Dakika = 15, BlokGucW = 540 }, new() { Dakika = 30, BlokGucW = 330 },
            }
        },
        new()
        {
            Ad = "VRLA 12V 100Ah", Marka = "Yuasa", Model = "SWL2500", Tip = "VRLA",
            BlokGerilimV = 12, KapasiteAh = 100, DesarjSonuGerilimV = 10.5, AgirlikKg = 35,
            DesarjTablosu = new() {
                new() { Dakika = 5,  BlokGucW = 1500 }, new() { Dakika = 10, BlokGucW = 1080 },
                new() { Dakika = 15, BlokGucW = 850 },  new() { Dakika = 30, BlokGucW = 520 },
            }
        },
    };

    // === Trafo (güç transformatörü) varsayılanları ===
    // Standart dağıtım trafosu serisi (CENELEC/EN 50588 mertebesinde tipik P0/Pk ve uk değerleri).
    // uk: ≤630 kVA → %4, ≥800 kVA → %6. Ölçüler ve ağırlıklar yaklaşıktır (köşk yerleşimi için).
    private static List<TrafoTanim> VarsayilanTrafolar()
    {
        var liste = new List<TrafoTanim>();

        // (kVA, uk%, P0 W, Pk W, GxDxY mm, kg)
        (double kva, double uk, double p0, double pk, double g, double d, double y, double agir)[] seri =
        {
            (100,  4.0, 210,  1750,  1200, 900,  1400, 600),
            (160,  4.0, 300,  2350,  1250, 950,  1450, 750),
            (250,  4.0, 425,  3250,  1350, 1000, 1500, 1050),
            (400,  4.0, 610,  4600,  1500, 1100, 1600, 1450),
            (630,  4.0, 860,  6500,  1600, 1150, 1650, 1950),
            (800,  6.0, 1000, 8400,  1700, 1300, 1800, 2400),
            (1000, 6.0, 1100, 10500, 1800, 1350, 1850, 2900),
            (1250, 6.0, 1350, 13500, 2000, 1500, 2000, 3500),
            (1600, 6.0, 1700, 17000, 2100, 1550, 2050, 4300),
            (2000, 6.0, 2100, 21000, 2300, 1650, 2150, 5200),
            (2500, 6.0, 2500, 24000, 2400, 1700, 2200, 6100),
        };

        foreach (var s in seri)
        {
            // Yağlı (ONAN) — serbest soluyan, genleşme tanklı
            liste.Add(new TrafoTanim
            {
                Ad = $"Yağlı {s.kva:N0} kVA", Marka = "—", Tip = TrafoTipi.Yagli,
                GucKVA = s.kva, PrimerGerilimKV = 34.5, SekonderGerilimV = 400, UkYuzde = s.uk,
                VektorGrubu = "Dyn11", BostaKayipW = s.p0, YukKaybiW = s.pk, SogutmaTipi = "ONAN",
                GenislikMm = s.g, DerinlikMm = s.d, YukseklikMm = s.y, AgirlikKg = s.agir
            });
            // Hermetik (tam dolu, sızdırmaz) — ≤2000 kVA tipik
            if (s.kva <= 2000)
                liste.Add(new TrafoTanim
                {
                    Ad = $"Hermetik {s.kva:N0} kVA", Marka = "—", Tip = TrafoTipi.Hermetik,
                    GucKVA = s.kva, PrimerGerilimKV = 34.5, SekonderGerilimV = 400, UkYuzde = s.uk,
                    VektorGrubu = "Dyn11", BostaKayipW = s.p0, YukKaybiW = s.pk, SogutmaTipi = "ONAN",
                    GenislikMm = s.g, DerinlikMm = s.d * 0.9, YukseklikMm = s.y, AgirlikKg = s.agir * 0.95
                });
            // Kuru tip (dökme reçine) — ≥250 kVA tipik; yük kaybı ve ölçüler biraz büyük, hava soğutmalı
            if (s.kva >= 250)
                liste.Add(new TrafoTanim
                {
                    Ad = $"Kuru Tip {s.kva:N0} kVA", Marka = "—", Tip = TrafoTipi.KuruTip,
                    GucKVA = s.kva, PrimerGerilimKV = 34.5, SekonderGerilimV = 400,
                    UkYuzde = Math.Max(6.0, s.uk),
                    VektorGrubu = "Dyn11", BostaKayipW = s.p0 * 1.4, YukKaybiW = s.pk * 1.05, SogutmaTipi = "AN",
                    GenislikMm = s.g * 1.05, DerinlikMm = s.d, YukseklikMm = s.y * 1.05, AgirlikKg = s.agir * 0.8
                });
        }

        return liste;
    }

    // === OG hücre varsayılanları ===
    // Tipik metal mahfazalı modüler hücreler (SF6 / hava izoleli). Genişlikler köşk yerleşimi içindir.
    private static List<OgHucreTanim> VarsayilanOgHucreleri() => new()
    {
        // ── 36 kV ──
        new() { Ad = "Giriş YA 36kV 630A",        HucreTipi = OgHucreTipi.Giris,       KorumaTipi = OgHucreKorumaTipi.YukAyirici,        AnmaGerilimKV = 36, AnmaAkimA = 630,  KisaSureDayanimKA = 16, TepeDayanimKA = 40, YalitimTipi = "SF6",  GenislikMm = 500, Aciklama = "Giriş — yük ayırıcılı" },
        new() { Ad = "Ölçü 36kV",                 HucreTipi = OgHucreTipi.Olcu,        KorumaTipi = OgHucreKorumaTipi.YukAyirici,        AnmaGerilimKV = 36, AnmaAkimA = 630,  KisaSureDayanimKA = 16, TepeDayanimKA = 40, YalitimTipi = "Hava", GenislikMm = 840, Aciklama = "Ölçü — gerilim/akım trafoları + sayaç" },
        new() { Ad = "Bara Bağlama 36kV 630A",    HucreTipi = OgHucreTipi.BaraBaglama, KorumaTipi = OgHucreKorumaTipi.Kesicili,          AnmaGerilimKV = 36, AnmaAkimA = 630,  KisaSureDayanimKA = 16, TepeDayanimKA = 40, YalitimTipi = "SF6",  GenislikMm = 750, Aciklama = "Bara bağlama (bus coupler)" },
        new() { Ad = "Çıkış YA 36kV 630A",        HucreTipi = OgHucreTipi.Cikis,       KorumaTipi = OgHucreKorumaTipi.YukAyirici,        AnmaGerilimKV = 36, AnmaAkimA = 630,  KisaSureDayanimKA = 16, TepeDayanimKA = 40, YalitimTipi = "SF6",  GenislikMm = 500, Aciklama = "Çıkış (fider) — yük ayırıcılı" },
        new() { Ad = "Trafo Koruma YAS 36kV",     HucreTipi = OgHucreTipi.TrafoKoruma, KorumaTipi = OgHucreKorumaTipi.YukAyiriciSigortali, AnmaGerilimKV = 36, AnmaAkimA = 200,  KisaSureDayanimKA = 16, TepeDayanimKA = 40, YalitimTipi = "SF6",  GenislikMm = 500, Aciklama = "Trafo koruma — yük ayırıcı + HRC sigorta" },
        new() { Ad = "Trafo Koruma Kesicili 36kV",HucreTipi = OgHucreTipi.TrafoKoruma, KorumaTipi = OgHucreKorumaTipi.Kesicili,          AnmaGerilimKV = 36, AnmaAkimA = 630,  KisaSureDayanimKA = 20, TepeDayanimKA = 50, YalitimTipi = "SF6",  GenislikMm = 750, Aciklama = "Trafo koruma — vakum/SF6 kesici + röle" },

        // ── 24 kV ──
        new() { Ad = "Giriş YA 24kV 630A",        HucreTipi = OgHucreTipi.Giris,       KorumaTipi = OgHucreKorumaTipi.YukAyirici,        AnmaGerilimKV = 24, AnmaAkimA = 630,  KisaSureDayanimKA = 20, TepeDayanimKA = 50, YalitimTipi = "SF6",  GenislikMm = 375, Aciklama = "Giriş — yük ayırıcılı" },
        new() { Ad = "Çıkış YA 24kV 630A",        HucreTipi = OgHucreTipi.Cikis,       KorumaTipi = OgHucreKorumaTipi.YukAyirici,        AnmaGerilimKV = 24, AnmaAkimA = 630,  KisaSureDayanimKA = 20, TepeDayanimKA = 50, YalitimTipi = "SF6",  GenislikMm = 375, Aciklama = "Çıkış (fider) — yük ayırıcılı" },
        new() { Ad = "Trafo Koruma YAS 24kV",     HucreTipi = OgHucreTipi.TrafoKoruma, KorumaTipi = OgHucreKorumaTipi.YukAyiriciSigortali, AnmaGerilimKV = 24, AnmaAkimA = 200,  KisaSureDayanimKA = 20, TepeDayanimKA = 50, YalitimTipi = "SF6",  GenislikMm = 375, Aciklama = "Trafo koruma — yük ayırıcı + HRC sigorta" },
        new() { Ad = "Trafo Koruma Kesicili 24kV",HucreTipi = OgHucreTipi.TrafoKoruma, KorumaTipi = OgHucreKorumaTipi.Kesicili,          AnmaGerilimKV = 24, AnmaAkimA = 630,  KisaSureDayanimKA = 20, TepeDayanimKA = 50, YalitimTipi = "SF6",  GenislikMm = 600, Aciklama = "Trafo koruma — vakum kesici + röle" },

        // ── 12 kV ──
        new() { Ad = "Giriş YA 12kV 630A",        HucreTipi = OgHucreTipi.Giris,       KorumaTipi = OgHucreKorumaTipi.YukAyirici,        AnmaGerilimKV = 12, AnmaAkimA = 630,  KisaSureDayanimKA = 20, TepeDayanimKA = 50, YalitimTipi = "SF6",  GenislikMm = 375, Aciklama = "Giriş — yük ayırıcılı" },
        new() { Ad = "Çıkış Kesicili 12kV 1250A", HucreTipi = OgHucreTipi.Cikis,       KorumaTipi = OgHucreKorumaTipi.Kesicili,          AnmaGerilimKV = 12, AnmaAkimA = 1250, KisaSureDayanimKA = 25, TepeDayanimKA = 63, YalitimTipi = "SF6",  GenislikMm = 600, Aciklama = "Çıkış — vakum kesici + röle" },
        new() { Ad = "Trafo Koruma Kesicili 12kV",HucreTipi = OgHucreTipi.TrafoKoruma, KorumaTipi = OgHucreKorumaTipi.Kesicili,          AnmaGerilimKV = 12, AnmaAkimA = 630,  KisaSureDayanimKA = 25, TepeDayanimKA = 63, YalitimTipi = "SF6",  GenislikMm = 600, Aciklama = "Trafo koruma — vakum kesici + röle" },
    };
}
