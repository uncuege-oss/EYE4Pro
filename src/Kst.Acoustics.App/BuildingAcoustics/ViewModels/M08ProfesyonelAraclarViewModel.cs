using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Field;
using Kst.Acoustics.Core.BuildingAcoustics.Geometry;
using Kst.Acoustics.Core.BuildingAcoustics.Interop;
using Kst.Acoustics.Core.BuildingAcoustics.Mapping;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Parsers;
using Kst.Acoustics.Core.BuildingAcoustics.Portfolio;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.BuildingAcoustics.Workflow;

namespace Kst.Acoustics.App.BuildingAcoustics.ViewModels;

/// <summary>M08'in ileri analiz ve teslim iş akışlarını tek pencerede birleştiren UI modeli.</summary>
public sealed class M08ProfesyonelAraclarViewModel : ObservableObject
{
    private readonly BinaProjesiViewModel _ana;
    private readonly BinaAkustikDegerlendirmeServisi _degerlendirme = new();
    private readonly AkustikIsListesiServisi _isListesi = new();
    private readonly M08SenaryoServisi _senaryoServisi = new();
    private readonly AkustikMetrajServisi _metrajServisi = new();
    private string? _revizyonDosyasi;

    public M08ProfesyonelAraclarViewModel(BinaProjesiViewModel ana)
    {
        _ana = ana ?? throw new ArgumentNullException(nameof(ana));
        SenaryoAdi = $"Senaryo {ana.Proje.Senaryolar.Count + 1}";
        KullanimTipiSecenekleri = EK2SiniflandirmaServisi.GetKategoriler(ana.Proje.BinaIslevi).Select(x => x.Ad).ToList();
        YenileMalzemeSecenekleri();
        TumunuYenile();
    }

    public BinaProjesiViewModel AnaViewModel => _ana;
    public BinaProje Proje => _ana.Proje;

    public ObservableCollection<M08AracSatiri> GorevSatirlari { get; } = new();
    public ObservableCollection<M08AracSatiri> RevizyonSatirlari { get; } = new();
    public ObservableCollection<M08AracSatiri> SenaryoSatirlari { get; } = new();
    public ObservableCollection<M08AracSatiri> SonucSecenekleri { get; } = new();
    public ObservableCollection<M08AracSatiri> AnalizSatirlari { get; } = new();
    public ObservableCollection<M08AracSatiri> OptimizasyonSatirlari { get; } = new();
    public ObservableCollection<M08AracSatiri> OlcumSatirlari { get; } = new();
    public ObservableCollection<M08AracSatiri> MetrajSatirlari { get; } = new();
    public ObservableCollection<M08TopluMekanSatiri> TopluMekanSatirlari { get; } = new();
    public ObservableCollection<M08TopluBolmeSatiri> TopluBolmeSatirlari { get; } = new();
    public ObservableCollection<M08AracSatiri> CepheHaritaSatirlari { get; } = new();
    public ObservableCollection<M08AracSatiri> PortfoySatirlari { get; } = new();
    public ObservableCollection<M08AracSatiri> OnayVeDenetimSatirlari { get; } = new();
    public ObservableCollection<M08AracSatiri> SahaSatirlari { get; } = new();

    public IReadOnlyList<string> KullanimTipiSecenekleri { get; }
    public IReadOnlyList<string> KaynakDerecesiSecenekleri { get; } = ["DG", "OG", "YG"];
    public IReadOnlyList<string> AliciSinifiSecenekleri { get; } = ["I", "II", "III"];
    public IReadOnlyList<YapiElemani> DosemeMalzemeSecenekleri { get; private set; } = [];
    public IReadOnlyList<YapiElemani> TavanMalzemeSecenekleri { get; private set; } = [];
    public IReadOnlyList<YapiElemani> DuvarMalzemeSecenekleri { get; private set; } = [];

    private string _ozetMetni = "";
    public string OzetMetni { get => _ozetMetni; private set => SetProperty(ref _ozetMetni, value); }
    private string _durumMesaji = "Hazır.";
    public string DurumMesaji { get => _durumMesaji; set => SetProperty(ref _durumMesaji, value); }
    private string _senaryoAdi = "";
    public string SenaryoAdi { get => _senaryoAdi; set => SetProperty(ref _senaryoAdi, value); }
    private decimal _monteCarloTekrar = 500;
    public decimal MonteCarloTekrar { get => _monteCarloTekrar; set => SetProperty(ref _monteCarloTekrar, value); }
    private string _metrajOzetMetni = "";
    public string MetrajOzetMetni { get => _metrajOzetMetni; private set => SetProperty(ref _metrajOzetMetni, value); }
    private string _topluAramaMetni = "";
    public string TopluAramaMetni { get => _topluAramaMetni; set => SetProperty(ref _topluAramaMetni, value); }
    private string? _topluKullanimTipi;
    public string? TopluKullanimTipi { get => _topluKullanimTipi; set => SetProperty(ref _topluKullanimTipi, value); }
    private string? _topluKaynakDerecesi;
    public string? TopluKaynakDerecesi { get => _topluKaynakDerecesi; set => SetProperty(ref _topluKaynakDerecesi, value); }
    private string? _topluAliciSinifi;
    public string? TopluAliciSinifi { get => _topluAliciSinifi; set => SetProperty(ref _topluAliciSinifi, value); }
    private YapiElemani? _topluZeminMalzemesi;
    public YapiElemani? TopluZeminMalzemesi { get => _topluZeminMalzemesi; set => SetProperty(ref _topluZeminMalzemesi, value); }
    private YapiElemani? _topluTavanMalzemesi;
    public YapiElemani? TopluTavanMalzemesi { get => _topluTavanMalzemesi; set => SetProperty(ref _topluTavanMalzemesi, value); }
    private YapiElemani? _topluDuvarMalzemesi;
    public YapiElemani? TopluDuvarMalzemesi { get => _topluDuvarMalzemesi; set => SetProperty(ref _topluDuvarMalzemesi, value); }
    private string _onaylayan = Environment.UserName;
    public string Onaylayan { get => _onaylayan; set => SetProperty(ref _onaylayan, value); }
    private string _onayNotu = "";
    public string OnayNotu { get => _onayNotu; set => SetProperty(ref _onayNotu, value); }
    private string _sahaKullanicisi = Environment.UserName;
    public string SahaKullanicisi { get => _sahaKullanicisi; set => SetProperty(ref _sahaKullanicisi, value); }
    private string _portfoyOzetMetni = "Henüz klasör taranmadı.";
    public string PortfoyOzetMetni { get => _portfoyOzetMetni; private set => SetProperty(ref _portfoyOzetMetni, value); }

    private M08AracSatiri? _seciliGorev;
    public M08AracSatiri? SeciliGorev { get => _seciliGorev; set => SetProperty(ref _seciliGorev, value); }
    private M08AracSatiri? _seciliSenaryo;
    public M08AracSatiri? SeciliSenaryo { get => _seciliSenaryo; set => SetProperty(ref _seciliSenaryo, value); }
    private M08AracSatiri? _seciliSonuc;
    public M08AracSatiri? SeciliSonuc { get => _seciliSonuc; set => SetProperty(ref _seciliSonuc, value); }
    private M08AracSatiri? _seciliAnalizSatiri;
    public M08AracSatiri? SeciliAnalizSatiri { get => _seciliAnalizSatiri; set => SetProperty(ref _seciliAnalizSatiri, value); }
    private M08AracSatiri? _seciliOptimizasyon;
    public M08AracSatiri? SeciliOptimizasyon { get => _seciliOptimizasyon; set => SetProperty(ref _seciliOptimizasyon, value); }

    public void TumunuYenile()
    {
        YenileMalzemeSecenekleri();
        GorevleriYenile(); SenaryolariYenile(); SonuclariYenile(); OlcumleriYenile();
        MetrajiYenile(); TopluTabloyuYenile(); CepheHaritasiniYenile(); DenetimiYenile();
        var mekan = Proje.Katlar.Sum(x => x.Mekanlar.Count);
        OzetMetni = $"{Proje.ProjeAdi} · {Proje.Katlar.Count} kat · {mekan} mekân · {SonucSecenekleri.Count} sonuç · {GorevSatirlari.Count} iş";
        DurumMesaji = "Profesyonel araç verileri güncellendi.";
    }

    public void GorevleriYenile()
    {
        GorevSatirlari.Clear();
        foreach (var x in _isListesi.Uret(Proje))
            GorevSatirlari.Add(new() { Baslik = x.Baslik, AltBaslik = Yer(x),
                Aciklama = $"{x.Aciklama}  → {x.OnerilenEylem}", Deger = x.Durum.ToString(),
                Durum = x.Oncelik.ToString(), HedefId = x.MekanId ?? x.BolmeId ?? x.YapiElemaniId, Tag = x });
    }

    public bool GoreviTamamla()
    {
        if (SeciliGorev?.Tag is not AkustikIsKalemi g) return Hata("Önce bir iş seçin.");
        if (!MutasyonBaslat()) return false;
        _isListesi.DurumKaydet(Proje, g.Anahtar, GorevDurumu.Tamamlandi, Onaylayan, not: "Profesyonel araçlardan tamamlandı.");
        MutasyonTamamla(DenetimEylemi.Degistirme, "Akustik görev", g.Anahtar, $"'{g.Baslik}' tamamlandı.");
        GorevleriYenile(); return true;
    }

    public bool GoreveGit()
    {
        if (SeciliGorev?.Tag is not AkustikIsKalemi g) return Hata("Önce bir iş seçin.");
        foreach (var kat in Proje.Katlar)
        {
            var mekan = kat.Mekanlar.FirstOrDefault(x => x.Id == g.MekanId || x.Bolmeler.Any(b => b.Id == g.BolmeId));
            if (mekan is null) continue;
            _ana.SeciliKat = kat; _ana.SeciliMekan = mekan;
            _ana.SeciliBolmePaneli = g.BolmeId is { } bid ? mekan.Bolmeler.FirstOrDefault(x => x.Id == bid) : null;
            DurumMesaji = $"Ana pencerede {kat.Ad} / {mekan.Ad} seçildi."; return true;
        }
        return Hata("İşin bağlı olduğu mekân bulunamadı.");
    }

    public void RevizyonKarsilastir(string yol)
    {
        var yeni = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi.Ac(yol);
        var rapor = new M08RevizyonEtkiServisi().Degerlendir(Proje, yeni);
        _revizyonDosyasi = yol; RevizyonSatirlari.Clear();
        foreach (var f in rapor.GeometriFarki.Farklar)
            RevizyonSatirlari.Add(new() { Durum = f.Tur.ToString(), Baslik = $"{f.Nesne}: {f.Baslik}",
                AltBaslik = f.YeniKatId ?? f.EskiKatId ?? "—", Aciklama = f.Aciklama, Tag = f });
        foreach (var e in rapor.Etkiler)
            RevizyonSatirlari.Add(new() { Durum = e.Duzey.ToString(), Baslik = e.Baslik,
                AltBaslik = e.MekanId?.ToString("D") ?? "M08 etkisi", Aciklama = e.Aciklama, HedefId = e.MekanId, Tag = e });
        DurumMesaji = $"Rev. {rapor.GeometriFarki.EskiRevizyon} → {rapor.GeometriFarki.YeniRevizyon}: " +
                       $"{rapor.GeometriFarki.Eklenen} eklenen, {rapor.GeometriFarki.Silinen} silinen, {rapor.KritikSayi} kritik etki.";
    }

    public string? RevizyonDosyasi => _revizyonDosyasi;

    public void SenaryoKaydet()
    {
        if (!MutasyonBaslat()) return;
        var s = _senaryoServisi.Kaydet(Proje, string.IsNullOrWhiteSpace(SenaryoAdi) ? $"Senaryo {Proje.Senaryolar.Count + 1}" : SenaryoAdi);
        MutasyonTamamla(DenetimEylemi.Olusturma, nameof(AkustikSenaryo), s.Id.ToString("D"), $"'{s.Ad}' senaryosu kaydedildi.");
        SenaryoAdi = $"Senaryo {Proje.Senaryolar.Count + 1}"; SenaryolariYenile();
    }

    public bool SenaryoUygula()
    {
        if (SeciliSenaryo?.Tag is not AkustikSenaryo s) return Hata("Önce bir senaryo seçin.");
        if (!MutasyonBaslat()) return false;
        var n = _senaryoServisi.Uygula(Proje, s);
        MutasyonTamamla(DenetimEylemi.Degistirme, nameof(AkustikSenaryo), s.Id.ToString("D"), $"'{s.Ad}' senaryosundan {n} alan uygulandı.");
        TumunuYenile(); return true;
    }

    public bool SenaryoSil()
    {
        if (SeciliSenaryo?.Tag is not AkustikSenaryo s) return Hata("Önce bir senaryo seçin.");
        if (!MutasyonBaslat()) return false;
        Proje.Senaryolar.Remove(s); if (Proje.AktifSenaryoId == s.Id) Proje.AktifSenaryoId = null;
        MutasyonTamamla(DenetimEylemi.Silme, nameof(AkustikSenaryo), s.Id.ToString("D"), $"'{s.Ad}' senaryosu silindi.");
        SenaryolariYenile(); return true;
    }

    private void SenaryolariYenile()
    {
        SenaryoSatirlari.Clear();
        foreach (var s in Proje.Senaryolar.OrderByDescending(x => x.GuncellemeTarihi))
        {
            var g = s.Sonuclar.Count(x => x.Durum == nameof(UyumDurumu.Gecti));
            var k = s.Sonuclar.Count(x => x.Durum == nameof(UyumDurumu.Kaldi));
            SenaryoSatirlari.Add(new() { Baslik = s.Ad, AltBaslik = s.OlusturmaTarihi.ToString("g"),
                Deger = $"{g} geçti / {k} kaldı", Aciklama = $"{s.Degisiklikler.Count} kayıt · {s.Aciklama}",
                Durum = Proje.AktifSenaryoId == s.Id ? "Aktif" : s.GuncelDegil ? "Bayat" : "Kayıtlı", HedefId = s.Id, Tag = s });
        }
    }

    private void SonuclariYenile()
    {
        var onceki = (SeciliSonuc?.Tag as RaporSatiri)?.SonucId;
        SonucSecenekleri.Clear();
        foreach (var x in _degerlendirme.Hesapla(Proje, Haricler()).Satirlar.OrderBy(x => x.MarjDb))
            SonucSecenekleri.Add(new() { Baslik = $"{x.SonucTuru} · {x.Kat} / {x.Mekan} / {x.Bolme}",
                AltBaslik = x.YonetmelikEk, Deger = $"{x.Deger:F1} {x.Birim} · marj {Marj(x.MarjDb)}",
                Durum = x.Durum, HedefId = x.SonucId, Tag = x });
        SeciliSonuc = SonucSecenekleri.FirstOrDefault(x => x.HedefId == onceki) ??
                      SonucSecenekleri.FirstOrDefault(x => (x.Tag as RaporSatiri)?.Durum != nameof(UyumDurumu.Gecti)) ?? SonucSecenekleri.FirstOrDefault();
    }

    public void BasarisizlikAcikla()
    {
        if (!Sonuc(out var x)) return;
        var a = new BasarisizlikAciklamaServisi().Acikla(Proje, x.SonucId, x.SonucTuru);
        AnalizSatirlari.Clear();
        AnalizSatirlari.Add(new() { Durum = "Özet", Baslik = a.Durum, Deger = Marj(a.Sonuc.MarjDb), Aciklama = a.Ozet, Tag = a });
        foreach (var n in a.Nedenler)
            AnalizSatirlari.Add(new() { Durum = n.Oncelik.ToString(), Baslik = n.Baslik,
                Deger = n.EtkiYuzdesi is { } p ? $"%{p:F0} etki" : n.Kod.ToString(),
                Aciklama = $"{n.Kanit}  → {n.OnerilenEylem}", HedefId = n.HedefId, Tag = n });
        DurumMesaji = "Başarısızlık açıklaması kanonik hesap ve iletim yolu kanıtlarıyla üretildi.";
    }

    public void FrekansAnalizi()
    {
        if (!Sonuc(out var x)) return;
        var bolmeId = FrekansBolmesi(x);
        if (bolmeId is null) { Hata("Bu sonuç türü için fiziksel bölme tabanlı frekans/yol analizi yok."); return; }
        var a = new FrekansYolAnaliziServisi().AnalizEt(Proje, bolmeId.Value);
        AnalizSatirlari.Clear();
        foreach (var y in a.Yollar.OrderByDescending(y => y.OrtalamaKatkiYuzde))
            AnalizSatirlari.Add(new() { Durum = y.Tur.ToString(), Baslik = y.Ad,
                Deger = $"ort. %{y.OrtalamaKatkiYuzde:F1} · tepe %{y.EnYuksekKatkiYuzde:F1}",
                Aciklama = string.Join(" · ", y.Bantlar.OrderByDescending(b => b.KatkiYuzde).Take(3).Select(b => $"{b.FrekansHz} Hz %{b.KatkiYuzde:F0}")),
                HedefId = y.HedefId, Tag = y });
        DurumMesaji = $"{a.BolmeAdi}: {a.Yollar.Count} iletim yolu, {a.Bantlar.Count} frekans bandı çözümlendi.";
    }

    public async Task OptimizasyonYapAsync()
    {
        if (!Sonuc(out var x)) return;
        DurumMesaji = "Katalog çözümleri kanonik hesapla değerlendiriliyor…";
        var katalog = TumElemanlar();
        var r = await Task.Run(() => new CozumOptimizasyonServisi().OptimizeEt(Proje, katalog,
            new CozumOptimizasyonIstegi(x.SonucId, x.SonucTuru, AzamiCozumSayisi: 12, AzamiDegisiklikSayisi: 2)));
        OptimizasyonSatirlari.Clear();
        foreach (var c in r.Cozumler)
            OptimizasyonSatirlari.Add(new() { Baslik = $"#{c.Sira} " + string.Join(" + ", c.Degisiklikler.Select(d => d.YeniYapiElemani)),
                AltBaslik = string.Join("; ", c.Degisiklikler.Select(d => $"{d.OncekiYapiElemani} → {d.YeniYapiElemani}")),
                Deger = $"{c.SonucDegeri:F1} · marj {Marj(c.Marj)} · {c.ToplamMaliyet:N2} {c.ParaBirimi}",
                Aciklama = $"Δ kalınlık {c.ToplamKalinlikArtisiMm:F0} mm · Δ kütle {c.ToplamKutleArtisiKgM2:F1} kg/m² · {c.Ozet}",
                Durum = c.Uygun ? "Uygun" : "Yetersiz", Tag = c });
        DurumMesaji = $"{r.DegerlendirilenKombinasyonSayisi} gerçek katalog kombinasyonu değerlendirildi. " + string.Join(" ", r.Uyarilar);
    }

    public async Task BelirsizlikAnaliziAsync()
    {
        if (!Sonuc(out var x)) return;
        var ps = BelirsizlikParametreleri(x);
        if (ps.Count == 0) { Hata("Bu sonuç için otomatik belirsizlik hedefi oluşturulamadı."); return; }
        DurumMesaji = "Duyarlılık ve Monte Carlo analizi çalışıyor…";
        var service = new BelirsizlikAnalizServisi();
        var tekrar = Math.Clamp((int)MonteCarloTekrar, 50, 5000);
        var tuple = await Task.Run(() => (service.DuyarlilikAnalizi(Proje, x.SonucId, ps, x.SonucTuru),
            service.MonteCarlo(Proje, x.SonucId, ps, tekrar, 2026, x.SonucTuru)));
        AnalizSatirlari.Clear();
        foreach (var d in tuple.Item1.Parametreler)
            AnalizSatirlari.Add(new() { Durum = "Duyarlılık", Baslik = d.Parametre,
                Deger = $"1σ etki {d.BirStandartSapmaEtkisi:+0.00;-0.00;0.00}", Aciklama = d.Yorum, Tag = d });
        var mc = tuple.Item2;
        AnalizSatirlari.Add(new() { Durum = "Monte Carlo", Baslik = $"{mc.GecerliTekrar}/{mc.IstenenTekrar} geçerli örnek",
            Deger = $"Uygunluk %{mc.UygunlukOlasiligi * 100:F1}",
            Aciklama = $"Ort. {mc.Ortalama:F2}; σ {mc.StandartSapma:F2}; P05/P50/P95 {mc.P05:F2}/{mc.P50:F2}/{mc.P95:F2}", Tag = mc });
        DurumMesaji = "Belirsizlik analizi tamamlandı; sonuçlar tekrarlanabilir seed=2026 ile üretildi.";
    }

    public int OlcumIceriAktar(string yol)
    {
        if (!MutasyonBaslat()) return 0;
        var import = SahaOlcumImportServisi.DosyadanOku(yol);
        if (!import.Basarili) throw new InvalidDataException(string.Join("; ", import.Hatalar));
        var n = SahaOlcumImportServisi.ProjeyeEkle(Proje, import);
        MutasyonTamamla(DenetimEylemi.IceriAktarma, nameof(SahaOlcumKaydi), Proje.Id.ToString("D"),
            $"{Path.GetFileName(yol)} dosyasından {n} yeni saha ölçümü içe aktarıldı.");
        OlcumleriYenile(); return n;
    }

    private void OlcumleriYenile()
    {
        OlcumSatirlari.Clear();
        foreach (var x in new OlcumKarsilastirmaServisi().Karsilastir(Proje))
            OlcumSatirlari.Add(new() { Baslik = $"{x.OlcumAdi} · {x.OlcumTuru}", AltBaslik = x.EslesmeDurumu,
                Deger = x.ModelDegeri is { } m ? $"{x.OlcumDegeri:F1} {x.Birim} · model Δ {x.ModeldenSapma:+0.0;-0.0;0.0}" : $"{x.OlcumDegeri:F1} {x.Birim}",
                Aciklama = x.Aciklama, Durum = x.OlcumUygunMu is null ? (x.RaporlamaIcinOnayli ? "Onaylı" : "Taslak") : x.OlcumUygunMu.Value ? "Geçti" : "Kaldı",
                HedefId = x.OlcumId, Tag = x });
    }

    public void TopluCsvDisaAktar(string yol) { M08TopluCsvServisi.DosyayaYaz(Proje, yol); DenetleDisaAktar("Toplu CSV", yol); }
    public M08TopluCsvSonucu TopluCsvIceriAktar(string yol)
    {
        if (!MutasyonBaslat()) return new();
        var r = M08TopluCsvServisi.DosyadanUygula(Proje, yol);
        if (!r.Basarili) throw new InvalidDataException(string.Join("; ", r.Hatalar));
        MutasyonTamamla(DenetimEylemi.IceriAktarma, "M08TopluCsv", Proje.Id.ToString("D"),
            $"CSV'den {r.MekanGuncellemeSayisi} mekân, {r.BolmeGuncellemeSayisi} bölme güncellendi.");
        TumunuYenile(); return r;
    }

    public void SablonKaydet(string yol)
    {
        var s = M08SablonServisi.ProjedenOlustur(Proje, Path.GetFileNameWithoutExtension(yol));
        M08SablonServisi.Kaydet(s, yol); DenetleDisaAktar("M08 proje şablonu", yol);
    }
    public SablonUygulamaSonucu SablonUygula(string yol)
    {
        if (!MutasyonBaslat()) return new();
        var r = M08SablonServisi.Uygula(Proje, M08SablonServisi.Oku(yol));
        MutasyonTamamla(DenetimEylemi.IceriAktarma, nameof(BinaAkustikSablon), Proje.Id.ToString("D"),
            $"Şablon {r.MekanGuncellemeSayisi} mekân ve {r.BolmeAtamaSayisi} bölmeye uygulandı.");
        TumunuYenile(); return r;
    }

    public void MetrajiYenile()
    {
        MetrajSatirlari.Clear(); var rows = _metrajServisi.Hesapla(Proje);
        foreach (var x in rows) MetrajSatirlari.Add(new() { Baslik = $"{x.YapiElemaniAdi} · {x.PozNo}", AltBaslik = $"{x.Tip} / {x.Birim}",
            Deger = $"{x.Miktar:N3} · {x.ToplamTutar:N2} {x.ParaBirimi}",
            Aciklama = $"Net {x.NetMiktar:N3}; fire %{x.FireYuzdesi:N1}; {x.Kaynaklar.Count} kaynak", HedefId = x.YapiElemaniId, Tag = x });
        var totals = rows.GroupBy(x => x.ParaBirimi).Select(g => $"{g.Sum(x => x.ToplamTutar):N2} {g.Key}");
        MetrajOzetMetni = $"{rows.Count} kalem · " + string.Join(" + ", totals);
    }
    public void MetrajExcelOlustur(string yol) { _metrajServisi.ExcelOlustur(Proje, yol); DenetleDisaAktar("Metraj Excel", yol); }

    public void TopluTabloyuYenile()
    {
        TopluMekanSatirlari.Clear(); TopluBolmeSatirlari.Clear(); var es = TumElemanlar();
        foreach (var kat in Proje.Katlar.OrderBy(x => x.KatNo))
        foreach (var m in kat.Mekanlar.Where(m => Arama(m.Ad, kat.Ad)))
        {
            TopluMekanSatirlari.Add(new(Proje, kat, m, es));
            foreach (var b in m.Bolmeler) TopluBolmeSatirlari.Add(new(kat, b, es));
        }
    }

    public int TopluMekanaUygula(IEnumerable<M08TopluMekanSatiri> rows)
    {
        var list = rows.Distinct().ToList(); if (list.Count == 0) { Hata("En az bir mekân satırı seçin."); return 0; }
        if (!MutasyonBaslat()) return 0;
        foreach (var row in list)
        {
            var m = row.MekanModel;
            if (!string.IsNullOrWhiteSpace(TopluKullanimTipi)) { m.KullanimTipi = TopluKullanimTipi; var k = EK2SiniflandirmaServisi.KategoridekiDegerleri(Proje.BinaIslevi, TopluKullanimTipi); m.KaynakDerecesi = k.Kaynak; m.AliciSinifi = k.Alici; }
            if (!string.IsNullOrWhiteSpace(TopluKaynakDerecesi)) m.KaynakDerecesi = TopluKaynakDerecesi;
            if (!string.IsNullOrWhiteSpace(TopluAliciSinifi)) m.AliciSinifi = TopluAliciSinifi;
            if (TopluZeminMalzemesi is not null) m.ZeminYapiElemani = TopluZeminMalzemesi;
            if (TopluTavanMalzemesi is not null) m.TavanYapiElemani = TopluTavanMalzemesi;
            row.Yenile();
        }
        MutasyonTamamla(DenetimEylemi.Degistirme, "MekanTopluAtama", Proje.Id.ToString("D"), $"{list.Count} mekâna toplu akustik atama yapıldı."); return list.Count;
    }

    public int TopluBolmeyeUygula(IEnumerable<M08TopluBolmeSatiri> rows)
    {
        var list = rows.Distinct().ToList(); if (list.Count == 0 || TopluDuvarMalzemesi is null) { Hata("Bölme satırlarını ve duvar malzemesini seçin."); return 0; }
        if (!MutasyonBaslat()) return 0;
        foreach (var row in list) { row.BolmeModel.YapiElemani = TopluDuvarMalzemesi; row.Yenile(); }
        MutasyonTamamla(DenetimEylemi.Degistirme, "BolmeTopluAtama", Proje.Id.ToString("D"), $"{list.Count} bölmeye {TopluDuvarMalzemesi.Ad} atandı."); return list.Count;
    }

    public void GridDuzenlemesiBaslat() { if (!_ana.ProjeKilitli) _ana.BeginUndoSnapshot(); }
    public void GridDuzenlemesiBitti()
    {
        if (_ana.ProjeKilitli) return; _ana.Kirlet(); _ana.GuncelleDogrulamaUyarisi(); _ana.ZorlaCizimYenile();
        DurumMesaji = "Tablo düzenlemesi uygulandı; sonuçlar yeniden hesaplanmalıdır.";
    }

    public string IfcOlustur() { var p = HesaplanmisProje(); return Ifc4AkustikPropertyExportServisi.Olustur(p, Haricler()); }
    public string CepheSvgOlustur() => CepheGurultuHaritasiServisi.SvgOlustur(CepheGurultuHaritasiServisi.Olustur(HesaplanmisProje(), Haricler()));
    public string CepheCsvOlustur() => CepheGurultuHaritasiServisi.CsvOlustur(CepheGurultuHaritasiServisi.Olustur(HesaplanmisProje(), Haricler()));

    private void CepheHaritasiniYenile()
    {
        CepheHaritaSatirlari.Clear();
        foreach (var x in CepheGurultuHaritasiServisi.Olustur(HesaplanmisProje(), Haricler()).Hucreler)
            CepheHaritaSatirlari.Add(new() { Baslik = $"{x.MekanAdi} / {x.BolmeAdi}", AltBaslik = x.KatAdi,
                Deger = $"Lgag {x.LgagDb:F1} · iç {Fmt(x.IcGurultuDb)} · marj {Fmt(x.MarjDb)}",
                Aciklama = $"DnT,A,tr {Fmt(x.DnTATrDb)} dB · {x.AlanM2:F1} m²", Durum = x.Durum, HedefId = x.BolmeId, Tag = x });
    }

    public async Task PortfoyTaraAsync(string klasor)
    {
        DurumMesaji = "Portföy klasörü taranıyor…";
        var r = await Task.Run(() => M08PortfoyServisi.Tara(klasor)); PortfoySatirlari.Clear();
        foreach (var x in r.Projeler)
            PortfoySatirlari.Add(new() { Baslik = string.IsNullOrWhiteSpace(x.ProjeAdi) ? Path.GetFileName(x.DosyaYolu) : x.ProjeAdi,
                AltBaslik = $"{Path.GetFileName(x.DosyaYolu)} · {x.SonYazmaUtc.LocalDateTime:g}",
                Deger = $"{x.MekanSayisi} mekân · {x.GectiSayisi} geçti · {x.KaldiSayisi + x.VeriEksikSayisi} açık",
                Aciklama = x.DosyaDurumu == M08PortfoyDosyaDurumu.Gecerli ? $"{x.Adres} · {x.MusteriAdi}" : x.Hata,
                Durum = x.DosyaDurumu.ToString(), Tag = x });
        PortfoyOzetMetni = $"{r.GecerliDosyaSayisi} geçerli / {r.SorunluDosyaSayisi} sorunlu · {r.ToplamMekanSayisi} mekân · {r.ToplamUygunsuzlukSayisi} uygunsuzluk";
        DurumMesaji = "Portföy taraması tamamlandı.";
    }

    public RaporOnKontrolSonucu RaporOnKontrol()
    {
        var s = AkustikRaporSnapshotServisi.Olustur(HesaplanmisProje(), Haricler());
        var r = RaporOnKontrolServisi.Denetle(s); AnalizSatirlari.Clear();
        foreach (var x in r.Bulgular) AnalizSatirlari.Add(new() { Durum = x.Seviye.ToString(), Baslik = x.Kod,
            Aciklama = x.Mesaj, HedefId = x.SonucId, Tag = x });
        DurumMesaji = r.OnaylanabilirMi ? $"Rapor ön kontrolü geçti; {r.UyariSayisi} uyarı var." : $"Rapor onaya hazır değil: {r.EngelSayisi} engel, {r.UyariSayisi} uyarı.";
        return r;
    }
    public string RaporHtmlOlustur() => new MimariAkustikRaporServisi().HtmlOlustur(HesaplanmisProje(), Haricler());
    public E4R08PaketOlusturmaSonucu RaporDondur(string yol)
    {
        var p = HesaplanmisProje(); var r = E4R08RaporPaketServisi.DosyayaOlustur(p, yol, Haricler());
        DenetleDisaAktar("Dondurulmuş M08 raporu", yol, r.Snapshot.RaporParmakIziSha256); return r;
    }
    public E4R08PaketDogrulamaSonucu RaporDogrula(string yol)
    {
        using var s = File.OpenRead(yol); var r = E4R08RaporPaketServisi.Dogrula(s);
        DurumMesaji = r.Gecerli ? "Dondurulmuş rapor paketi ve SHA-256 manifesti geçerli." : "Paket doğrulanamadı: " + string.Join("; ", r.Hatalar); return r;
    }
    public bool RaporOnayla()
    {
        if (_ana.ProjeKilitli) return Hata("Proje kilitli; rapor onayı için önce ana pencerede kilidi açın.");
        var calculated = HesaplanmisProje(); var snapshot = AkustikRaporSnapshotServisi.Olustur(calculated, Haricler());
        var result = RaporOnayServisi.Onayla(calculated, snapshot, Onaylayan, aciklama: OnayNotu);
        if (result.OnayKaydi is null) { DurumMesaji = "Onay eklenmedi: " + string.Join("; ", result.OnKontrol.Bulgular.Where(x => x.Seviye == RaporOnKontrolSeviyesi.Engel).Select(x => x.Mesaj)); return false; }
        _ana.BeginUndoSnapshot();
        Proje.OnayKayitlari.Add(result.OnayKaydi); if (result.DenetimKaydi is not null) Proje.DenetimKayitlari.Add(result.DenetimKaydi);
        _ana.Kirlet(); DenetimiYenile(); DurumMesaji = $"Rapor {Onaylayan} adına onaylandı; parmak izi {snapshot.RaporParmakIziSha256[..12]}…"; return true;
    }

    public void DisaAktarimKaydiEkle(string tur, string yol) =>
        DenetleDisaAktar(tur, yol, BinaAkustikDegerlendirmeServisi.GirdiParmakIzi(Proje));

    public string SahaPaketiHtmlOlustur() => M08SahaPaketServisi.HtmlOlustur(HesaplanmisProje(), Haricler());
    public M08SahaBirlesimSonucu SahaPaketiIceriAktar(string yol)
    {
        if (!MutasyonBaslat()) return new();
        // Birleştirme güncellik kontrolünü hesaplanmış kopyada yapar; kabul edilen kayıtlar canlı projeye kimlikle taşınır.
        var p = HesaplanmisProje(); var r = M08SahaPaketServisi.GeriDonusuBirlesitir(p, File.ReadAllText(yol), true, false, SahaKullanicisi);
        if (!r.Basarili) throw new InvalidDataException(string.Join("; ", r.Hatalar));
        foreach (var rec in r.KabulEdilenKayitlar)
        {
            var old = Proje.SahaOlcumleri.FirstOrDefault(x => x.Id == rec.Id);
            if (old is not null) Proje.SahaOlcumleri.Remove(old); Proje.SahaOlcumleri.Add(rec);
        }
        MutasyonTamamla(DenetimEylemi.IceriAktarma, "M08SahaPaketi", Proje.Id.ToString("D"), $"{r.Eklenen} ölçüm eklendi, {r.Guncellenen} güncellendi.");
        OlcumleriYenile(); DenetimiYenile(); return r;
    }

    private void DenetimiYenile()
    {
        OnayVeDenetimSatirlari.Clear();
        foreach (var x in Proje.OnayKayitlari.OrderByDescending(x => x.Tarih))
            OnayVeDenetimSatirlari.Add(new() { Baslik = $"Onay · {x.Tur}", AltBaslik = $"{x.Onaylayan} · {x.Tarih:g}",
                Deger = Kisa(x.GirdiParmakIzi), Aciklama = x.Aciklama, Durum = x.Durum.ToString(), HedefId = x.Id, Tag = x });
        foreach (var x in Proje.DenetimKayitlari.OrderByDescending(x => x.Tarih).Take(100))
            OnayVeDenetimSatirlari.Add(new() { Baslik = $"Denetim · {x.Eylem}", AltBaslik = $"{x.Kullanici} · {x.Tarih:g}",
                Deger = Kisa(x.GirdiParmakIzi), Aciklama = x.Aciklama, Durum = x.HedefTuru, HedefId = x.Id, Tag = x });
        SahaSatirlari.Clear();
        foreach (var x in Proje.SahaOlcumleri.OrderByDescending(x => x.OlcumTarihi))
            SahaSatirlari.Add(new() { Baslik = $"{x.Ad} · {x.Tur}", AltBaslik = $"{x.KatKimligi} · {x.Operator}",
                Deger = $"{x.Deger:F1} {x.Birim}" + (x.Belirsizlik is { } u ? $" ± {u:F1}" : ""),
                Aciklama = x.Notlar, Durum = x.Durum.ToString(), HedefId = x.Id, Tag = x });
    }

    private List<BelirsizlikParametresi> BelirsizlikParametreleri(RaporSatiri x)
    {
        var r = new List<BelirsizlikParametresi>();
        if (x.SonucTuru == AkustikRaporSonucTuru.DisCephe)
        {
            r.Add(new("Dış gürültü", BelirsizlikParametreTuru.DisGurultuDb, 2));
            var mid = Proje.Katlar.SelectMany(k => k.Mekanlar).FirstOrDefault(m => RaporSonucIdleri.DisCephe(m.Id) == x.SonucId);
            var b = mid?.Bolmeler.FirstOrDefault(y => y.IsDisCephe && y.YapiElemani is not null);
            if (b is not null) r.Add(new("Cephe Rw", BelirsizlikParametreTuru.BolmeRwOfsetiDb, 1.5, b.Id));
        }
        else if (x.SonucTuru == AkustikRaporSonucTuru.HavaSesi)
            r.Add(new("Ayırıcı Rw", BelirsizlikParametreTuru.BolmeRwOfsetiDb, 1.5, x.SonucId));
        else
        {
            var m = Proje.Katlar.SelectMany(k => k.Mekanlar).FirstOrDefault(m =>
                RaporSonucIdleri.Reverberasyon(m.Id) == x.SonucId || RaporSonucIdleri.Tesisat(m.Id, false) == x.SonucId || RaporSonucIdleri.Tesisat(m.Id, true) == x.SonucId);
            if (m is not null) r.Add(new("Mekân yüksekliği", BelirsizlikParametreTuru.MekanYukseklikOrani, .03, m.Id, -.10, .10));
        }
        return r;
    }

    private Guid? FrekansBolmesi(RaporSatiri x)
    {
        if (x.SonucTuru == AkustikRaporSonucTuru.HavaSesi) return x.SonucId;
        if (x.SonucTuru != AkustikRaporSonucTuru.DisCephe) return null;
        return Proje.Katlar.SelectMany(k => k.Mekanlar).FirstOrDefault(m => RaporSonucIdleri.DisCephe(m.Id) == x.SonucId)
            ?.Bolmeler.FirstOrDefault(b => b.IsDisCephe && b.YapiElemani is not null)?.Id;
    }

    private void YenileMalzemeSecenekleri()
    {
        var e = TumElemanlar(); DosemeMalzemeSecenekleri = e.Where(x => x.Tip == YapiElemaniTipi.Doseme).ToList();
        TavanMalzemeSecenekleri = e.Where(x => x.Tip == YapiElemaniTipi.Tavan).ToList();
        DuvarMalzemeSecenekleri = e.Where(x => x.Tip == YapiElemaniTipi.Duvar).ToList();
        OnPropertyChanged(nameof(DosemeMalzemeSecenekleri)); OnPropertyChanged(nameof(TavanMalzemeSecenekleri)); OnPropertyChanged(nameof(DuvarMalzemeSecenekleri));
    }
    private List<YapiElemani> TumElemanlar() => Proje.YapiElemaniKutuphanesi.Concat(_ana.KutuphaneElemanlari)
        .Where(x => x is not null).GroupBy(x => x.Id == Guid.Empty ? M08KararliKimlik.YapiElemani(x) : x.Id).Select(x => x.First()).ToList();
    private BinaProje HesaplanmisProje() => _degerlendirme.Hesapla(Proje, Haricler()).HesaplananProje;
    private HashSet<Guid> Haricler() => _ana.RaporHaricTutulanBolmeIdleri;
    private bool Sonuc(out RaporSatiri x) { x = SeciliSonuc?.Tag as RaporSatiri ?? null!; if (x is not null) return true; Hata("Önce analiz edilecek bir sonuç seçin."); return false; }
    private bool MutasyonBaslat() { if (_ana.ProjeKilitli) return Hata("Proje kilitli; düzenleme için önce ana pencerede kilidi açın."); _ana.BeginUndoSnapshot(); return true; }
    private void MutasyonTamamla(DenetimEylemi eylem, string tur, string id, string aciklama)
    {
        Proje.DenetimKayitlari.Add(new() { Tarih = DateTime.Now, Eylem = eylem, Kullanici = string.IsNullOrWhiteSpace(Onaylayan) ? Environment.UserName : Onaylayan,
            HedefTuru = tur, HedefId = id, Aciklama = aciklama, GirdiParmakIzi = BinaAkustikDegerlendirmeServisi.GirdiParmakIzi(Proje) });
        _ana.Kirlet(); _ana.GuncelleDogrulamaUyarisi(); _ana.ZorlaCizimYenile(); DurumMesaji = aciklama + " Sonuçlar yeniden hesaplanmalıdır.";
    }
    private void DenetleDisaAktar(string tur, string yol, string parmakIzi = "")
    {
        Proje.DenetimKayitlari.Add(new() { Tarih = DateTime.Now, Eylem = DenetimEylemi.DisaAktarma, Kullanici = Environment.UserName,
            HedefTuru = tur, HedefId = Path.GetFileName(yol), Aciklama = $"{tur} dışa aktarıldı: {Path.GetFileName(yol)}", GirdiParmakIzi = parmakIzi });
        _ana.Kirlet(); DenetimiYenile(); DurumMesaji = $"{tur} oluşturuldu: {yol}";
    }
    private bool Hata(string mesaj) { DurumMesaji = mesaj; return false; }
    private bool Arama(string mekan, string kat) => string.IsNullOrWhiteSpace(TopluAramaMetni) || (mekan + " " + kat).Contains(TopluAramaMetni, StringComparison.CurrentCultureIgnoreCase);
    private static string Yer(AkustikIsKalemi x) => string.Join(" / ", new[] { x.KatKimligi,
        x.MekanId?.ToString("D"), x.BolmeId?.ToString("D") }.Where(s => !string.IsNullOrWhiteSpace(s)));
    private static string Marj(double d) => double.IsFinite(d) ? d.ToString("+0.0;-0.0;0.0") + " dB" : "—";
    private static string Fmt(double? d) => d is { } v && double.IsFinite(v) ? v.ToString("0.0") : "—";
    private static string Kisa(string s) => string.IsNullOrWhiteSpace(s) ? "—" : s[..Math.Min(16, s.Length)] + (s.Length > 16 ? "…" : "");
}
