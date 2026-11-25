using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;

public partial class BasitProjeCizViewModel
{
    private readonly BasitProjeCizOtomatikYerlestirServisi _yerlestir = new(new BasitProjeCizGeometriServisi());

    /// Ctrl/Cmd ile tıklanan eleman kümesi. Window code-behind tarafından yönetilir.
    public List<BasitProjeEleman> SeciliElemanlar { get; } = new();

    // ── Otomatik yerleştirme ─────────────────────────────────────────────────

    // Aydınlatma ve priz adımları varsayılan olarak eksikleri tamamlar. Kullanıcının
    // açık tercihini ana pencere ve mahal listesi ortak kullanır.
    private bool? _sadecaEksikleriEkle;
    public bool SadecaEksikleriEkle
    {
        get => _sadecaEksikleriEkle ?? AsamaliPaftaAktif;
        set
        {
            if (SetProperty(ref _sadecaEksikleriEkle, value))
                OnPropertyChanged(nameof(YerlestirModuAciklama));
        }
    }

    /// Ana penceredeki yerleştir butonunun tooltip'i — aktif mod gizli kalmasın.
    public string YerlestirModuAciklama => SadecaEksikleriEkle
        ? "Mod: yalnız eksikleri ekler — mevcut otomatik elemanlar korunur."
        : "Mod: TAM YENİLEME — eski otomatik elemanlar silinip mahal listesine göre yeniden yerleştirilir (bağlı kablolar da silinir).";

    private BpcAydinlatmaAsamasi? YerlesimAsamasi => AydinlatmaAkisiAktif ? AydinlatmaAsamasi : null;
    private BpcPaftaTipi? YerlesimPaftasi => ModulZayifMi || AydinlatmaAkisiAktif ? null : AktifPafta;

    public bool MahalKalemiGorunurMu(BpcElemanTipi tip) =>
        (!PrizAkisiAktif || PrizAsamasi == BpcPrizAsamasi.Priz) &&
        BasitProjeCizOtomatikYerlestirServisi.MahalKalemiMi(tip, YerlesimAsamasi, YerlesimPaftasi);

    private bool YerlesimElemaniMi(BasitProjeEleman eleman)
    {
        if (AsamaliPaftaAktif) return AsamadaDuzenlenebilir(eleman);
        return MahalKalemiGorunurMu(eleman.Tip);
    }

    [RelayCommand]
    private void MahalListesindenOtomatikYerlestir()
        => MahalYerlesiminiUygula(snapshotAl: true);

    private void MahalYerlesiminiUygula(bool snapshotAl)
    {
        var kat = SeciliKat;
        if (kat is null) return;
        if (ProjeKilitli)
        {
            DurumMesaji = "Proje kilitli — mahal listesinden yerleştirmek için önce kilidi açın.";
            return;
        }

        if (AydinlatmaAkisiAktif && AydinlatmaAsamasi is not
            (BpcAydinlatmaAsamasi.Armatur or BpcAydinlatmaAsamasi.Anahtar or BpcAydinlatmaAsamasi.Buat))
        {
            DurumMesaji = "Otomatik yerleşim için armatür, anahtar veya buat adımını seçin.";
            return;
        }
        if (PrizAkisiAktif && PrizAsamasi is not (BpcPrizAsamasi.Priz or BpcPrizAsamasi.Buat))
        {
            DurumMesaji = "Otomatik yerleşim için priz veya buat adımını seçin.";
            return;
        }
        if (snapshotAl && !AsamaliPaftaAktif)
        {
            ElemanSnapshotAl(kat);
            snapshotAl = false;
        }
        int silinenEleman = 0;
        int oncekiKablo = kat.Kablolar.Count;
        if (!SadecaEksikleriEkle)
        {
            // Yalnız mahal listesinden otomatik üretilen elemanları temizle; manuel
            // semboller ve elle taşınmış (kaynağı temizlenmiş) elemanlar korunur.
            var eskiler = kat.Elemanlar
                .Where(e => e.OtomatikYerlesimKaynak == BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak
                    && YerlesimElemaniMi(e))
                .ToList();
            if (snapshotAl && eskiler.Count > 0) ElemanSnapshotAl(kat);
            foreach (var e in eskiler) ElemanSil(e);
            silinenEleman = eskiler.Count;
        }

        // 5.2 kararı — bağlı M50 geometrisindeki düşey birleşimlerde tavanı açık
        // odalar: tavan montajlı otomatik yerleşim atlanır (fiziksel tavan yok).
        var tavaniAcik = TavaniAcikOdaIdleri(kat);

        var (yeni, uyarilar) = _yerlestir.TumMahalListesindenYerlestir(
            kat, _kutuphane, SadecaEksikleriEkle, tavaniAcik, YerlesimAsamasi, YerlesimPaftasi,
            PrizAkisiAktif ? PrizAsamasi : null);

        if (snapshotAl && silinenEleman == 0 && yeni.Count > 0) ElemanSnapshotAl(kat);
        foreach (var e in yeni) kat.Elemanlar.Add(e);

        string yenilemeNotu = silinenEleman > 0
            ? $" · Tam yenileme: {silinenEleman} otomatik eleman silinip yeniden üretildi."
            : "";
        DurumMesaji = uyarilar.Count > 0
            ? $"{yeni.Count} eleman yerleştirildi{yenilemeNotu} · {uyarilar.Count} uyarı (panelde)"
            : $"{yeni.Count} eleman yerleştirildi{yenilemeNotu}";

        // Uyarılar durum çubuğunda kaybolmasın: yerleştirme uyarıları + silinen
        // kablo bildirimi tek panelde listelenir. Tam yenileme, silinen elemanlara
        // bağlı devre/kabloları da götürür — kablosuz DXF sürprizi olmasın.
        int silinenKablo = oncekiKablo - kat.Kablolar.Count;
        var panelSatirlari = new List<string>();
        if (silinenKablo > 0)
        {
            RotalarBayat = true;
            panelSatirlari.Add($"Tam yenileme {silinenEleman} otomatik elemanla birlikte {silinenKablo} kablo/devreyi sildi. " +
                               "Kablolamayı yeniden çalıştırın (otomatik kablolama veya Kabloları Yenile).");
        }
        panelSatirlari.AddRange(uyarilar);
        if (panelSatirlari.Count > 0)
            UyariGoster("Yerleştirme uyarıları:\n• " + string.Join("\n• ", panelSatirlari));

        if (silinenEleman > 0 || yeni.Count > 0)
        {
            GuncelleFiltrelenmisElemanlar();
            GuncelleKabloGorunum();
            Guncelle3DGorunum();
        }
    }

    // ── Mahal listesi üretkenlik komutları (codex12 §20) ─────────────────────

    /// Başka bir katın mahal listesini seçili kata kopyalar (oda adı eşlemeli).
    /// Kullanıcının hedefte zaten aktif ettiği hücre ezilmez.
    public string MahalKattanKopyala(BasitProjeKat kaynak, BasitProjeKat hedef)
    {
        if (ProjeKilitli) return "Proje kilitli — kopyalamak için önce kilidi açın.";
        if (ReferenceEquals(kaynak, hedef)) return "Kaynak ve hedef kat aynı.";

        ElemanSnapshotAl(hedef);
        var sonuc = MahalListesiServisi.KattanKopyala(kaynak, hedef, satirFiltresi: s => MahalKalemiGorunurMu(s.Tip));
        DurumMesaji = $"{kaynak.Ad} → {hedef.Ad}: {sonuc.Ozet}";
        return sonuc.Ozet;
    }

    /// Oda adlarından tipik kalem setini kurar (mevcut aktif hücrelere dokunmaz).
    public string MahalAkilliDoldur(BasitProjeKat kat, bool zayifAkim)
    {
        if (ProjeKilitli) return "Proje kilitli — doldurmak için önce kilidi açın.";

        ElemanSnapshotAl(kat);
        var dosya = MahalListesiServisi.SablonlariYukle();
        var tablo = dosya.OdaTipiOnerileri.Count > 0
            ? dosya.OdaTipiOnerileri
            : MahalListesiServisi.VarsayilanOneriler;

        // Kabuk disiplinine uymayan kalemler önerilmez (M12 kuvvetli, M16 zayıf).
        var hedefDisiplin = zayifAkim ? BpcDisiplin.ZayifAkim : BpcDisiplin.KuvvetliAkim;
        var sonuc = MahalListesiServisi.AkilliDoldur(kat, _kutuphane, tablo,
            tip => BpcSistemKatalogu.Disiplini(tip) == hedefDisiplin && MahalKalemiGorunurMu(tip));

        DurumMesaji = $"Akıllı doldurma — {sonuc.Ozet}";
        return sonuc.Ozet;
    }

    /// Kattaki mahal listesini adlandırılmış şablon olarak diske kaydeder.
    public string MahalSablonKaydet(BasitProjeKat kat, string ad, string? aciklama = null)
    {
        if (string.IsNullOrWhiteSpace(ad)) return "Şablon adı boş olamaz.";
        var dosya = MahalListesiServisi.SablonlariYukle();
        var yeni = MahalListesiServisi.SablonaDonustur(kat, ad.Trim(), aciklama, MahalKalemiGorunurMu);

        // Aynı adlı şablon varsa üzerine yazılır (kullanıcı bilinçli olarak aynı adı verdi).
        dosya.Sablonlar.RemoveAll(s => string.Equals(s.Ad, yeni.Ad, StringComparison.OrdinalIgnoreCase));
        dosya.Sablonlar.Add(yeni);

        if (!MahalListesiServisi.SablonlariKaydet(dosya))
        {
            DurumMesaji = "Şablon kaydedilemedi (dosya yazılamadı).";
            return DurumMesaji;
        }
        DurumMesaji = $"Şablon kaydedildi: {yeni.Ad} ({yeni.Satirlar.Count} kalem)";
        return DurumMesaji;
    }

    /// Adlandırılmış şablonu seçili kata uygular.
    public string MahalSablonUygula(BasitProjeKat kat, MahalListesiServisi.MahalSablonu sablon)
    {
        if (ProjeKilitli) return "Proje kilitli — şablon uygulamak için önce kilidi açın.";
        ElemanSnapshotAl(kat);
        var sonuc = MahalListesiServisi.SablonUygula(sablon, kat, MahalKalemiGorunurMu);
        DurumMesaji = $"Şablon '{sablon.Ad}' uygulandı — {sonuc.Ozet}";
        return sonuc.Ozet;
    }

    /// Mahal listesi olan TÜM katlarda yerleştirmeyi çalıştırır; kat kat rapor döner.
    /// Aktif kat değişse de kullanıcıya döndürülür (bağlam kaybolmasın).
    public string TumKatlaraYerlestir()
    {
        if (ProjeKilitli) return "Proje kilitli — yerleştirmek için önce kilidi açın.";

        var hedefler = Katlar.Where(k => k.MahalListesi.Any(s => MahalKalemiGorunurMu(s.Tip))).ToList();
        if (hedefler.Count == 0) return "Mahal listesi olan kat yok.";

        var oncekiKat = SeciliKat;
        var satirlar = new List<string>();
        int toplamYeni = 0;

        ElemanSnapshotAl(hedefler);
        foreach (var kat in hedefler)
        {
            int once = kat.Elemanlar.Count;
            SeciliKat = kat;
            MahalYerlesiminiUygula(snapshotAl: false);
            int fark = kat.Elemanlar.Count - once;
            toplamYeni += Math.Max(0, fark);
            satirlar.Add($"{kat.Ad}: {(fark >= 0 ? "+" : "")}{fark} eleman (toplam {kat.Elemanlar.Count})");
        }

        SeciliKat = oncekiKat ?? hedefler[0];
        DurumMesaji = $"{hedefler.Count} katta yerleştirme tamamlandı · net {toplamYeni} yeni eleman";
        return string.Join("\n", satirlar);
    }

    // ── Mahal listesi satır silme ve yeniden yerleştirme ─────────────────────

    /// Mahal listesi satırını siler; o satırdan otomatik yerleştirilen tüm elemanları projeden kaldırır.
    public void MahalSatiriKaldir(BasitProjeKat kat, BpcMahalSatiri satir)
    {
        if (KilitliyseDur("mahal kalemini kaldırmak") || (PrizAkisiAktif && !MahalKalemiGorunurMu(satir.Tip))) return;
        SeciliKat = kat;
        ElemanSnapshotAl(kat);
        bool asamaListesi = YerlesimAsamasi is BpcAydinlatmaAsamasi.Armatur or BpcAydinlatmaAsamasi.Anahtar || PrizAkisiAktif;
        var eskiler = kat.Elemanlar.Where(e => MahalSatirElemaniMi(e, satir)
            && (!asamaListesi || YerlesimElemaniMi(e))).ToList();
        foreach (var e in eskiler)
        {
            e.OtomatikYerlesimGrupIds ??= new();
            if (e.OtomatikYerlesimGrupIds.Count > 1)
                e.OtomatikYerlesimGrupIds.Remove(satir.Id);
            else
                ElemanSil(e);
        }
        kat.MahalListesi.Remove(satir);
        Guncelle3DGorunum();
    }

    /// Satırın (ya da belirli bir odadaki) otomatik elemanlarını siler ve güncel ayarlarla yeniden yerleştirir.
    /// tekOdaId null ise satırdaki tüm aktif odalar yeniden işlenir.
    public void MahalSatiriYenidenYerlestir(BasitProjeKat kat, BpcMahalSatiri satir, string? tekOdaId = null)
    {
        if (KilitliyseDur("mahal kalemini yerleştirmek") || (PrizAkisiAktif && !MahalKalemiGorunurMu(satir.Tip))) return;
        SeciliKat = kat;
        var eskiler = kat.Elemanlar
            .Where(e => MahalSatirElemaniMi(e, satir)
                     && (!PrizAkisiAktif || YerlesimElemaniMi(e))
                     && (tekOdaId == null || e.OdaId == tekOdaId))
            .ToList();

        ElemanSnapshotAl(kat);
        // Eski yerleştirmede Aktif bayrağı taşımayan odaları otomatik aktifleştir
        foreach (var odaId in eskiler.Select(e => e.OdaId).Where(id => id != null).Distinct())
        {
            var ayar = satir.HucreGetirVeyaOlustur(odaId!);
            if (!ayar.Aktif) ayar.Aktif = true;
        }

        foreach (var e in eskiler) ElemanSil(e);

        var tanim = MahalSatirTanimiBul(satir);
        if (tanim is null) { Guncelle3DGorunum(); return; }

        var tavaniAcik = TavaniAcikOdaIdleri(kat);
        var odalar = tekOdaId != null
            ? kat.Odalar.Where(o => o.Id == tekOdaId).ToList()
            : kat.Odalar.ToList();

        foreach (var oda in odalar)
        {
            if (!oda.MahalListesineDahil) continue;
            if (!satir.EfektifAktif(oda.Id)) continue;
            if (tanim.MontajTipi == "Tavan" && tavaniAcik is not null && tavaniAcik.Contains(oda.Id)) continue;

            var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
            var (yerlesim, montaj, gridM, duvarM) = satir.EfektifAyarlar(oda.Id);
            var gridYM = satir.EfektifGridY(oda.Id);

            var placed = _yerlestir.OdayaYerlestir(
                oda, tanim, yerlesim, montaj, gridM, gridYM, duvarM, ayar.Adet,
                kat.Acikliklar, kat.Elemanlar, PrizAkisiAktif, out _);

            foreach (var e in placed)
            {
                e.OtomatikYerlesimGrupIds = [satir.Id];
                kat.Elemanlar.Add(e);
            }
        }

        Guncelle3DGorunum();
    }

    private static bool MahalSatirElemaniMi(BasitProjeEleman e, BpcMahalSatiri satir)
    {
        if (e.OtomatikYerlesimKaynak != BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak)
            return false;

        // Yeni projelerde kararlı grup kimliği birincil kaynak; ortak buat/sensörler
        // birden fazla satıra bağlıysa satır silinirken yalnız üyelikleri kaldırılır.
        if (e.OtomatikYerlesimGrupIds is { Count: > 0 })
            return e.OtomatikYerlesimGrupIds.Contains(satir.Id);

        // Eski projelerde grup kimliği yoktur; geriye dönük tip+tanım eşleşmesi.
        return e.Tip == satir.Tip
            && (string.IsNullOrEmpty(satir.ArmatürTanimId) || e.ArmatürTanimId == satir.ArmatürTanimId);
    }

    private BpcArmatürTanim? MahalSatirTanimiBul(BpcMahalSatiri satir)
    {
        BpcArmatürTanim? tanim = null;
        if (satir.ArmatürTanimId is not null)
            tanim = BasitProjeCizOtomatikYerlestirServisi.TanimBul(satir.ArmatürTanimId, _kutuphane);
        tanim ??= BasitProjeCizKutuphaneServisi.ZayifAkimTanim(satir.Tip);
        tanim ??= _kutuphane.BulManuelTanim(satir.Tip);
        return tanim;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// 5.2 — Bağlı M50 geometrisinden (gömülü kopya) bu kattaki tavanı açık
    /// (düşey birleşimli) oda Id'lerini döner; bağlantı yoksa null (filtre yok).
    private System.Collections.Generic.HashSet<string>? TavaniAcikOdaIdleri(BasitProjeKat kat)
    {
        var geo = Proje.BinaGeometriKopya;
        if (geo is null || geo.DuseyBirlesimleri.Count == 0) return null;
        var sonuc = new System.Collections.Generic.HashSet<string>(
            kat.Odalar
                .Where(o => Kst.Acoustics.Core.Bina.Services
                    .BinaDuseyBirlesimServisi.TavanAcikMi(geo, o.Id))
                .Select(o => o.Id));
        return sonuc.Count > 0 ? sonuc : null;
    }

    // ── Manuel eleman ekleme modu ────────────────────────────────────────────

    [ObservableProperty] private bool _elemanEkleModu;
    [ObservableProperty] private BpcArmatürTanim? _eklenecekSembol;

    [RelayCommand]
    private void ElemanEkleModeBaslat(BpcArmatürTanim tanim)
    {
        EklenecekSembol = tanim;
        ElemanEkleModu  = true;
        DurumMesaji = $"{tanim.Ad} yerleştirme modu — canvas'a tıkla, Esc ile çık";
    }

    [RelayCommand]
    private void ElemanEkleModIptal()
    {
        ElemanEkleModu  = false;
        EklenecekSembol = null;
        DurumMesaji = "";
    }

    /// ElemanEkleModu aktifken canvas tıklamasından gelir. Koordinatlar metre cinsindendir.
    public void EklenecekSembolKanvasaEkle(double x, double y)
    {
        var kat = SeciliKat;
        if (kat is null || EklenecekSembol is null) return;
        if (KilitliyseDur("eleman eklemek")) return;

        ElemanSnapshotAl(kat);

        // Hangi odanın içindeyiz?
        string? odaId = kat.Odalar
            .FirstOrDefault(o => o.Poligon.Count >= 3 &&
                PoligonYardimci.IsPointInside(new Point2D(x, y), o.Poligon))
            ?.Id;

        var tip = BasitProjeCizKutuphaneServisi.TanimTipi(EklenecekSembol);

        if (BasitProjeCizKabloServisi.ElemanDuvarSnapMi(tip) && kat.Odalar.Count > 0)
        {
            var snapped = BasitProjeCizDxfServisi.SnapToNearestWall(new Point2D(x, y), kat.Odalar);
            x = snapped.X;
            y = snapped.Y;
        }

        var eleman = new BasitProjeEleman
        {
            Tip            = tip,
            Ad             = EklenecekSembol.Ad,
            EkranAdi       = string.IsNullOrWhiteSpace(EklenecekSembol.EkranAdi)
                ? EklenecekSembol.SembolAdi
                : EklenecekSembol.EkranAdi,
            BlokAdi        = EklenecekSembol.BlokAdi,
            ArmatürTanimId = EklenecekSembol.Id,
            OdaId          = odaId,
            X              = x,
            Y              = y,
            PaftaTipi      = PrizAkisiAktif && (tip == BpcElemanTipi.Buat || BasitProjeCizKabloServisi.ElemanPrizMi(tip))
                ? BpcPaftaTipi.Priz : BasitProjeCizOtomatikYerlestirServisi.PaftaForBlokPublic(EklenecekSembol.BlokAdi),
            Ozellikler     = "",
            CustomGucW     = EklenecekSembol.GucW > 0 ? EklenecekSembol.GucW : null,
            MontajTipi     = EklenecekSembol.MontajTipi,
            YerlesimTipi   = EklenecekSembol.MontajTipi == "Duvar" ? "Duvar aralıklı" : "Grid",
            GridAraligiM   = EklenecekSembol.GridAraligiM,
            GridAraligiYM  = EklenecekSembol.GridAraligiYM,
            AlanBasinaAdetM2 = EklenecekSembol.AlanBasinaAdetM2,
            DuvarAraligiM  = 3.0,
            CustomAdet     = EklenecekSembol.VarsayilanAdet > 1 || EklenecekSembol.OzelAlanTuru == "Adet"
                ? Math.Max(1, EklenecekSembol.VarsayilanAdet)
                : null,
            MonofazeAdet   = EklenecekSembol.VarsayilanMonofazeAdet > 0 ? EklenecekSembol.VarsayilanMonofazeAdet : null,
            TrifazeAdet    = EklenecekSembol.VarsayilanTrifazeAdet > 0 ? EklenecekSembol.VarsayilanTrifazeAdet : null,
            Megapiksel     = EklenecekSembol.OzelAlanTuru == "Megapiksel" ? 2.0 : null,
            OzelAlanTuru   = EklenecekSembol.OzelAlanTuru
        };

        if (tip == BpcElemanTipi.KatPanosu && AydinlatmaAkisiAktif)
        {
            var kod = string.IsNullOrWhiteSpace(EklenecekSembol.SembolAdi) ? "P" : EklenecekSembol.SembolAdi;
            var panolar = Katlar.SelectMany(k => k.Elemanlar).Where(e => e.Tip == BpcElemanTipi.KatPanosu).ToList();
            int sira = 1;
            while (panolar.Any(p => p.Ad == $"{EklenecekSembol.Ad} {sira}" || p.EkranAdi == $"{kod}{sira}")) sira++;
            eleman.Ad = $"{EklenecekSembol.Ad} {sira}";
            eleman.EkranAdi = $"{kod}{sira}";
        }
        var eklenenAd = eleman.Ad;
        kat.Elemanlar.Add(eleman);
        if (tip == BpcElemanTipi.KatPanosu)
        {
            GuncelleTumKatPanolari();
            GuncellePanoIcerik();
        }
        SeciliElemanlar.Clear();
        SeciliEleman = eleman;
        SeciliElemanlar.Add(eleman);
        GuncelleElemanVurgu();
        ElemanEkleModu = false;
        EklenecekSembol = null;
        DurumMesaji = $"{eklenenAd} eklendi ({x:N2}, {y:N2})";
        Guncelle3DGorunum();
    }

    public bool SeciliElemandanOtomatikYerlesim()
    {
        var kat = SeciliKat;
        var referans = SeciliEleman;
        if (kat is null || referans is null || KilitliyseDur("otomatik yerleşim yapmak"))
            return false;

        if (referans.Tip == BpcElemanTipi.Buat)
        {
            DurumMesaji = "Buatlar için otomatik yerleştirme işlemi yoktur.";
            return false;
        }

        if (referans.Tip == BpcElemanTipi.KatPanosu)
        {
            DurumMesaji = "Kat panoları için otomatik yerleştirme işlemi yoktur.";
            return false;
        }

        var oda = !string.IsNullOrWhiteSpace(referans.OdaId)
            ? kat.Odalar.FirstOrDefault(o => o.Id == referans.OdaId)
            : kat.Odalar.FirstOrDefault(o => o.Poligon.Count >= 3 &&
                PoligonYardimci.IsPointInside(new Point2D(referans.X, referans.Y), o.Poligon));

        if (oda is null || oda.Poligon.Count < 3)
        {
            DurumMesaji = "Otomatik yerleşim için eleman oda içinde olmalı.";
            return false;
        }

        var yeni = string.Equals(referans.MontajTipi, "Duvar", StringComparison.OrdinalIgnoreCase)
            ? DuvarYerlesimiOlustur(kat, oda, referans)
            : TavanYerlesimiOlustur(oda, referans);

        if (yeni.Count == 0)
        {
            DurumMesaji = "Otomatik yerleşim için uygun nokta bulunamadı.";
            return false;
        }

        ElemanSnapshotAl(kat);
        // İlk priz mevcut linye ve pano kayıtlarında aynı kimlikle kullanılmaya devam eder.
        if (PrizAkisiAktif)
        {
            yeni[0].Id = referans.Id;
            if (kat.Kablolar.Count > 0) RotalarBayat = true;
        }
        kat.Elemanlar.Remove(referans);
        foreach (var eleman in yeni)
            kat.Elemanlar.Add(eleman);

        SeciliElemanlar.Clear();
        SeciliEleman = yeni.FirstOrDefault();
        if (SeciliEleman != null) SeciliElemanlar.Add(SeciliEleman);
        GuncelleElemanVurgu();
        GuncelleFiltrelenmisElemanlar();
        Guncelle3DGorunum();
        DurumMesaji = $"{referans.Ad}: {yeni.Count} adet otomatik yerleştirildi.";
        return true;
    }

    private static List<BasitProjeEleman> TavanYerlesimiOlustur(BasitProjeOda oda, BasitProjeEleman referans)
    {
        var (minX, minY, maxX, maxY) = Bounds(oda.Poligon);
        var alan = Math.Max(0.1, PolygonArea(oda.Poligon));
        var stepX = Math.Max(0.1, referans.GridAraligiM ?? 3.0);
        var stepY = Math.Max(0.1, referans.GridAraligiYM ?? referans.GridAraligiM ?? 3.0);
        if (string.Equals(referans.YerlesimTipi, "m²/adet", StringComparison.OrdinalIgnoreCase))
            stepX = stepY = Math.Sqrt(Math.Max(0.1, referans.AlanBasinaAdetM2 ?? 12.0));

        var points = new List<Point2D>();
        for (double x = minX + stepX / 2.0; x < maxX; x += stepX)
        for (double y = minY + stepY / 2.0; y < maxY; y += stepY)
        {
            var p = new Point2D(x, y);
            if (PoligonYardimci.IsPointInside(p, oda.Poligon))
                points.Add(p);
        }

        if (points.Count == 0)
            points.Add(CentroidOrFirst(oda.Poligon));

        return points.Select((p, i) => ElemanKopyasi(referans, oda.Id, p, i)).ToList();
    }

    private static List<BasitProjeEleman> DuvarYerlesimiOlustur(BasitProjeKat kat, BasitProjeOda oda, BasitProjeEleman referans)
    {
        var poly = oda.Poligon;
        var interval = Math.Max(0.1, referans.DuvarAraligiM ?? 3.0);
        var segments = new List<(Point2D A, Point2D B, double Len, double Start)>();
        double perimeter = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            var len = Distance(a, b);
            if (len < 1e-6) continue;
            segments.Add((a, b, len, perimeter));
            perimeter += len;
        }
        if (perimeter < 1e-6) return new();

        var refPoint = new Point2D(referans.X, referans.Y);
        var start = NearestPerimeterDistance(refPoint, segments);
        var count = Math.Max(1, (int)Math.Floor(perimeter / interval));
        var result = new List<Point2D>();

        for (int i = 0; i < count; i++)
        {
            var d = (start + i * interval) % perimeter;
            var p = PointAtPerimeter(d, segments);
            if (AcikliktanUzakMi(p, kat.Acikliklar, 0.50) &&
                PoligonYardimci.IsPointInside(NudgeInside(p, oda.Poligon), oda.Poligon))
                result.Add(p);
        }

        if (result.Count == 0 && AcikliktanUzakMi(refPoint, kat.Acikliklar, 0.50))
            result.Add(refPoint);

        return result.Select((p, i) => ElemanKopyasi(referans, oda.Id, p, i)).ToList();
    }

    private static BasitProjeEleman ElemanKopyasi(BasitProjeEleman kaynak, string odaId, Point2D p, int index) => new()
    {
        Tip = kaynak.Tip,
        Ad = kaynak.Ad,
        EkranAdi = kaynak.EkranAdi,
        OdaId = odaId,
        ArmatürTanimId = kaynak.ArmatürTanimId,
        BlokAdi = kaynak.BlokAdi,
        X = p.X,
        Y = p.Y,
        PaftaTipi = kaynak.PaftaTipi,
        Ozellikler = kaynak.Ozellikler,
        CustomGucW = kaynak.CustomGucW,
        MontajTipi = kaynak.MontajTipi,
        YerlesimTipi = kaynak.YerlesimTipi,
        GridAraligiM = kaynak.GridAraligiM,
        GridAraligiYM = kaynak.GridAraligiYM,
        AlanBasinaAdetM2 = kaynak.AlanBasinaAdetM2,
        DuvarAraligiM = kaynak.DuvarAraligiM,
        CustomAdet = kaynak.CustomAdet,
        MonofazeAdet = kaynak.MonofazeAdet,
        TrifazeAdet = kaynak.TrifazeAdet,
        Megapiksel = kaynak.Megapiksel,
        OzelAlanTuru = kaynak.OzelAlanTuru
    };

    private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(List<Point2D> poly)
        => (poly.Min(p => p.X), poly.Min(p => p.Y), poly.Max(p => p.X), poly.Max(p => p.Y));

    private static double PolygonArea(List<Point2D> poly)
    {
        double area = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(area) / 2.0;
    }

    private static Point2D CentroidOrFirst(List<Point2D> poly)
    {
        var p = new Point2D(poly.Average(x => x.X), poly.Average(x => x.Y));
        return PoligonYardimci.IsPointInside(p, poly) ? p : poly[0];
    }

    private static double Distance(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double NearestPerimeterDistance(Point2D p, List<(Point2D A, Point2D B, double Len, double Start)> segments)
    {
        double bestDist = double.MaxValue;
        double bestAlong = 0;
        foreach (var s in segments)
        {
            var t = ProjectionT(p, s.A, s.B);
            var q = new Point2D(s.A.X + (s.B.X - s.A.X) * t, s.A.Y + (s.B.Y - s.A.Y) * t);
            var dist = Distance(p, q);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestAlong = s.Start + s.Len * t;
            }
        }
        return bestAlong;
    }

    private static Point2D PointAtPerimeter(double distance, List<(Point2D A, Point2D B, double Len, double Start)> segments)
    {
        foreach (var s in segments)
        {
            if (distance <= s.Start + s.Len)
            {
                var t = Math.Clamp((distance - s.Start) / s.Len, 0, 1);
                return new Point2D(s.A.X + (s.B.X - s.A.X) * t, s.A.Y + (s.B.Y - s.A.Y) * t);
            }
        }
        return segments[^1].B;
    }

    private static bool AcikliktanUzakMi(Point2D p, IEnumerable<BpcAciklik> acikliklar, double koruma)
        => acikliklar.Where(a => !a.IptalEdildi).All(a => DistancePointToSegment(p, a.Bas, a.Bit) >= koruma);

    private static double DistancePointToSegment(Point2D p, Point2D a, Point2D b)
    {
        var t = ProjectionT(p, a, b);
        var q = new Point2D(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        return Distance(p, q);
    }

    private static double ProjectionT(Point2D p, Point2D a, Point2D b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        if (len2 < 1e-12) return 0;
        return Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
    }

    private static Point2D NudgeInside(Point2D p, List<Point2D> poly)
    {
        var c = CentroidOrFirst(poly);
        return new Point2D(p.X + (c.X - p.X) * 0.01, p.Y + (c.Y - p.Y) * 0.01);
    }

    // ── Manuel taşıma ────────────────────────────────────────────────────────

    [ObservableProperty] private bool _elemanTasimaModu;

    public void ManuelElemanKonumGuncelle(BasitProjeEleman eleman, double yeniX, double yeniY)
    {
        var kat = SeciliKat;
        if (kat is null) return;
        if (KilitliyseDur("eleman taşımak")) return;

        ElemanSnapshotAl(kat);

        var hedefOdaId = OdaIdHesapla(kat, yeniX, yeniY);
        if (BasitProjeCizKabloServisi.ElemanDuvarSnapMi(eleman.Tip) && kat.Odalar.Count > 0)
        {
            var snapped = BasitProjeCizDxfServisi.SnapToNearestWall(new Point2D(yeniX, yeniY), kat.Odalar);
            yeniX = snapped.X;
            yeniY = snapped.Y;
        }

        eleman.X = yeniX;
        eleman.Y = yeniY;

        // Elle taşınan otomatik eleman artık kullanıcının düzenidir — tam yenileme
        // (mahal listesinden yeniden yerleştirme) onu silip eski konuma döndürmesin.
        eleman.OtomatikYerlesimKaynak = "";
        (eleman.OtomatikYerlesimGrupIds ??= new()).Clear();

        // Ortak duvar iki mahalin sınırıdır; bırakılan tarafı duvara oturtmadan önce belirle.
        eleman.OdaId = hedefOdaId ?? OdaIdHesapla(kat, yeniX, yeniY);

        if (kat.Kablolar.Count > 0) RotalarBayat = true;
        DurumMesaji = $"{eleman.Ad} taşındı ({yeniX:N2}, {yeniY:N2})";
        Guncelle3DGorunum();
    }

    // ── Manuel silme ─────────────────────────────────────────────────────────

    private IEnumerable<BasitProjeKat> ElemanSilmeSnapshotKatlari(
        BasitProjeKat seciliKat, IEnumerable<BasitProjeEleman> elemanlar)
        => elemanlar.Any(e => e.Tip == BpcElemanTipi.KatPanosu)
            ? Proje.Katlar
            : [seciliKat];

    [RelayCommand]
    private void SeciliElemanSil()
    {
        var kat = SeciliKat;
        var el  = SeciliEleman;
        if (kat is null || el is null) return;
        if (KilitliyseDur("silmek")) return;

        ElemanSnapshotAl(ElemanSilmeSnapshotKatlari(kat, [el]));
        ElemanSil(el); // kapsamlı temizlik: devreler, kablolar, bağlantılar
        SeciliElemanlar.Remove(el);
        GuncelleElemanVurgu();
        DurumMesaji = "Eleman silindi · Ctrl+Z ile geri al";
    }

    /// Ctrl/Cmd ile seçilen tüm elemanları siler.
    public void SeciliElemanlariSil()
    {
        var kat = SeciliKat;
        if (kat is null || SeciliElemanlar.Count == 0) return;
        if (KilitliyseDur("silmek")) return;

        int n = SeciliElemanlar.Count;
        var liste = SeciliElemanlar.ToList();
        ElemanSnapshotAl(ElemanSilmeSnapshotKatlari(kat, liste));
        SeciliElemanlar.Clear();
        SeciliEleman = null;
        foreach (var el in liste) ElemanSil(el);
        GuncelleElemanVurgu();
        DurumMesaji = $"{n} eleman silindi · Ctrl+Z ile geri al";
    }

    // ── Çoklu taşıma commit (X/Y zaten drag sırasında güncellendi) ───────────

    /// Sürüklemenin başında (ilk gerçek harekette) bir kez çağrılır: taşıma öncesi
    /// durumu undo yığınına alır. Salt tıklamada çağrılmaz → undo kirlenmez.
    public void ElemanTasimaSnapshotAl()
    {
        var kat = SeciliKat;
        if (kat is not null) ElemanSnapshotAl(kat);
    }

    /// Drag sonunda çağrılır: tüm seçili elemanların OdaId'sini günceller.
    /// Snapshot artık sürükleme başında alınır (bkz. ElemanTasimaSnapshotAl).
    public void ManuelCokElemanKonumGuncelle(BasitProjeEleman primaryEl, double yeniX, double yeniY)
    {
        var kat = SeciliKat;
        if (kat is null) return;

        var hedefOdaId = OdaIdHesapla(kat, yeniX, yeniY);
        if (BasitProjeCizKabloServisi.ElemanDuvarSnapMi(primaryEl.Tip) && kat.Odalar.Count > 0)
        {
            var snapped = BasitProjeCizDxfServisi.SnapToNearestWall(new Point2D(yeniX, yeniY), kat.Odalar);
            yeniX = snapped.X;
            yeniY = snapped.Y;
        }

        double dx = yeniX - primaryEl.X;
        double dy = yeniY - primaryEl.Y;

        primaryEl.X = yeniX;
        primaryEl.Y = yeniY;
        primaryEl.OdaId = hedefOdaId ?? OdaIdHesapla(kat, yeniX, yeniY);
        primaryEl.OtomatikYerlesimKaynak = "";
        (primaryEl.OtomatikYerlesimGrupIds ??= new()).Clear();

        foreach (var el in SeciliElemanlar)
        {
            if (el == primaryEl) continue;
            el.X += dx;
            el.Y += dy;
            el.OdaId = OdaIdHesapla(kat, el.X, el.Y);
            // Elle taşınanlar "manuel" sayılır (tam yenileme korur).
            el.OtomatikYerlesimKaynak = "";
            (el.OtomatikYerlesimGrupIds ??= new()).Clear();
        }

        if (kat.Kablolar.Count > 0) RotalarBayat = true;
        DurumMesaji = SeciliElemanlar.Count > 1
            ? $"{SeciliElemanlar.Count} eleman taşındı"
            : $"{primaryEl.Ad} taşındı ({yeniX:N2}, {yeniY:N2})";
        Guncelle3DGorunum();
    }

    private static string? OdaIdHesapla(BasitProjeKat kat, double x, double y)
        => kat.Odalar
            .FirstOrDefault(o => o.Poligon.Count >= 3 &&
                PoligonYardimci.IsPointInside(new Point2D(x, y), o.Poligon))
            ?.Id;

    /// Seçim listesi değişince render'ı tetikler.
    public void GuncelleElemanVurgu() => OnPropertyChanged(nameof(VurgulananElemanPosList));

    // ── Eleman undo ──────────────────────────────────────────────────────────

    private readonly Stack<ProjeElemanUndoSnapshot> _elemanUndoStack = new();
    private readonly Stack<ProjeElemanUndoSnapshot> _elemanRedoStack = new();

    private void ElemanSnapshotAl(BasitProjeKat kat)
        => ElemanSnapshotAl([kat]);

    public void MahalDuzenlemesineBasla(BasitProjeKat kat)
    {
        if (!ProjeKilitli) ElemanSnapshotAl(kat);
    }

    /// Bir işlem birden fazla kattaki referansları değiştiriyorsa tüm katları tek
    /// undo adımında tutar (ör. başka kattaki ana panonun silinmesi).
    private void ElemanSnapshotAl(IEnumerable<BasitProjeKat> katlar)
    {
        // Snapshot yalnız mutasyon öncesi alınır → IsDirty için merkezî nokta.
        var kopyalar = katlar
            .GroupBy(k => k.Id)
            .Select(g => KatDurumKopyala(g.First()))
            .ToList();
        if (kopyalar.Count == 0) return;

        Kirlet();
        _elemanUndoStack.Push(new ProjeElemanUndoSnapshot(kopyalar));
        _elemanRedoStack.Clear();

        if (_elemanUndoStack.Count > 20)
        {
            var liste = _elemanUndoStack.Take(20).Reverse().ToList();
            _elemanUndoStack.Clear();
            foreach (var s in liste) _elemanUndoStack.Push(s);
        }
    }

    /// Kattaki tüm kablolama durumunu (eleman + tüm devre/kablo türleri) derin kopyalar.
    private static ElemanUndoSnapshot KatDurumKopyala(BasitProjeKat kat) => new(
        kat.Id,
        kat.KatNo,
        kat.Elemanlar.Select(CloneEleman).ToList(),
        kat.Devreler.Select(d => new BpcDevre {
            Id = d.Id, DevreNo = d.DevreNo, ManuelRotaTipi = d.ManuelRotaTipi,
            AnahtarElemanId = d.AnahtarElemanId, BuatId = d.BuatId,
            ArmaturElemanIds = d.ArmaturElemanIds.ToList()
        }).ToList(),
        kat.Kablolar.Select(k => new BpcKablo {
            Id = k.Id, DevreId = k.DevreId, Tip = k.Tip,
            PanoSira = k.PanoSira, SistemAnahtar = k.SistemAnahtar,
            Rota = k.Rota.Select(p => new Point2D(p.X, p.Y)).ToList()
        }).ToList(),
        kat.VavienGruplari.Select(v => new BpcVavienGrup {
            Id = v.Id, DevreId = v.DevreId,
            Anahtar1Id = v.Anahtar1Id, Anahtar2Id = v.Anahtar2Id
        }).ToList(),
        (kat.PrizDevreleri ?? new()).Select(p => new BpcPrizDevre {
            Id = p.Id, BuatId = p.BuatId,
            PrizElemanIds = p.PrizElemanIds.ToList()
        }).ToList(),
        (kat.MekanikDevreleri ?? new()).Select(m => new BpcMekanikDevre {
            Id = m.Id, KlimaElemanId = m.KlimaElemanId, PanoId = m.PanoId
        }).ToList(),
        (kat.SistemDevreleri ?? new()).Select(s => new BpcSistemDevre {
            Id = s.Id, Sistem = s.Sistem, MerkezId = s.MerkezId,
            AraToplayiciId = s.AraToplayiciId, CihazIds = s.CihazIds.ToList()
        }).ToList(),
        (kat.BuatPanoBaglantilari ?? new()).Select(b => new BpcBuatPanoBaglanti {
            Id = b.Id, BuatId = b.BuatId, PanoId = b.PanoId,
            GecisOdaIds = b.GecisOdaIds.ToList(), LinyePrefix = b.LinyePrefix
        }).ToList(),
        (kat.MahalListesi ?? new()).Select(CloneMahalSatiri).ToList(),
        kat.Odalar.ToDictionary(o => o.Id, o => (o.MahalListesineDahil, o.GecisSecili)),
        (kat.KabloTavalari ?? new()).Select(CloneKabloTavasi).ToList(),
        (kat.PanoBeslemeHatlari ?? new()).Select(p => new BpcPanoBeslemeHatti {
            Id = p.Id, AnaPanoId = p.AnaPanoId, TaliPanoId = p.TaliPanoId,
            AnaKatNo = p.AnaKatNo, TaliKatNo = p.TaliKatNo,
            UygulamaTipi = p.UygulamaTipi, Etiket = p.Etiket, OtomatikKatIciBesleme = p.OtomatikKatIciBesleme,
            Rota = p.Rota.Select(pt => new Point2D(pt.X, pt.Y)).ToList()
        }).ToList(),
        CloneTopraklama(kat.Topraklama));

    private static BpcMahalSatiri CloneMahalSatiri(BpcMahalSatiri s) => new()
    {
        Id = s.Id,
        Tip = s.Tip,
        Sayilar = new Dictionary<string, int>(s.Sayilar),
        HucreAyarlari = s.HucreAyarlari.ToDictionary(kv => kv.Key, kv => CloneHucreAyar(kv.Value)),
        ArmatürTanimId = s.ArmatürTanimId,
        ArmatürTanimAd = s.ArmatürTanimAd,
        YerlesimTipi = s.YerlesimTipi,
        MontajTipi = s.MontajTipi,
        GridAraligiM = s.GridAraligiM,
        GridAraligiYM = s.GridAraligiYM,
        DuvarAraligiM = s.DuvarAraligiM,
        GelenGucW = s.GelenGucW
    };

    private static BpcMahalHucreAyar CloneHucreAyar(BpcMahalHucreAyar a) => new()
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

    private static BpcKabloTavasi CloneKabloTavasi(BpcKabloTavasi t) => new()
    {
        Id = t.Id, Ad = t.Ad, Tip = t.Tip, Adet = t.Adet,
        GenislikMm = t.GenislikMm, YukseklikMm = t.YukseklikMm,
        EtKalinligiMm = t.EtKalinligiMm, AgirHizmet = t.AgirHizmet,
        KotM = t.KotM, EtiketGoster = t.EtiketGoster,
        Rota = t.Rota.Select(p => new Point2D(p.X, p.Y)).ToList(),
        TaliHatlar = t.TaliHatlar.Select(h => new BpcKabloTavasiTaliHat {
            Id = h.Id, GenislikMm = h.GenislikMm,
            Rota = h.Rota.Select(p => new Point2D(p.X, p.Y)).ToList()
        }).ToList()
    };

    private static BpcTopraklama CloneTopraklama(BpcTopraklama t) => new()
    {
        SecimNoktalari = (t?.SecimNoktalari ?? new()).Select(p => new Point2D(p.X, p.Y)).ToList(),
        Seritler = (t?.Seritler ?? new()).Select(s => new BpcTopraklamaSerit {
            A = new Point2D(s.A.X, s.A.Y), B = new Point2D(s.B.X, s.B.Y), Tip = s.Tip
        }).ToList(),
        Kaziklar = (t?.Kaziklar ?? new()).Select(p => new Point2D(p.X, p.Y)).ToList(),
        EkTopraklamaVar = t?.EkTopraklamaVar ?? false
    };

    /// Snapshot'taki durumu kata geri yazar (undo/redo ortak gövdesi).
    private static void KatDurumGeriYukle(BasitProjeKat kat, ElemanUndoSnapshot snap)
    {
        kat.PrizDevreleri    ??= new();
        kat.MekanikDevreleri ??= new();
        kat.SistemDevreleri  ??= new();
        kat.BuatPanoBaglantilari ??= new();

        kat.Elemanlar.Clear();
        foreach (var e in snap.Elemanlar) kat.Elemanlar.Add(e);

        kat.Devreler.Clear();
        foreach (var d in snap.Devreler) kat.Devreler.Add(d);

        kat.Kablolar.Clear();
        foreach (var k in snap.Kablolar) kat.Kablolar.Add(k);

        kat.VavienGruplari.Clear();
        foreach (var v in snap.VavienGruplari) kat.VavienGruplari.Add(v);

        kat.PrizDevreleri.Clear();
        foreach (var p in snap.PrizDevreleri) kat.PrizDevreleri.Add(p);

        kat.MekanikDevreleri.Clear();
        foreach (var m in snap.MekanikDevreleri) kat.MekanikDevreleri.Add(m);

        kat.SistemDevreleri.Clear();
        foreach (var s in snap.SistemDevreleri) kat.SistemDevreleri.Add(s);

        kat.BuatPanoBaglantilari.Clear();
        foreach (var b in snap.BuatPanoBaglantilari) kat.BuatPanoBaglantilari.Add(b);

        kat.MahalListesi ??= new();
        kat.MahalListesi.Clear();
        foreach (var m in snap.MahalListesi) kat.MahalListesi.Add(m);
        foreach (var oda in kat.Odalar)
            if (snap.MahalSecimleri.TryGetValue(oda.Id, out var secim))
            {
                oda.MahalListesineDahil = secim.Dahil;
                oda.GecisSecili = secim.Gecis;
            }

        kat.KabloTavalari ??= new();
        kat.KabloTavalari.Clear();
        foreach (var t in snap.KabloTavalari) kat.KabloTavalari.Add(t);

        kat.PanoBeslemeHatlari ??= new();
        kat.PanoBeslemeHatlari.Clear();
        foreach (var p in snap.PanoBeslemeHatlari) kat.PanoBeslemeHatlari.Add(p);

        kat.Topraklama = snap.Topraklama;
    }

    [RelayCommand]
    private void ElemanGeriAl()
    {
        if (_elemanUndoStack.Count == 0 || KilitliyseDur("değişikliği geri almak")) return;
        var snap = _elemanUndoStack.Pop();
        var eslesenler = snap.Katlar
            .Select(s => (Snap: s, Kat: SnapshotKatiniBul(s.KatId, s.KatNo)))
            .Where(x => x.Kat is not null)
            .ToList();
        if (eslesenler.Count == 0) return;

        _elemanRedoStack.Push(new ProjeElemanUndoSnapshot(
            eslesenler.Select(x => KatDurumKopyala(x.Kat!)).ToList()));
        if (KabloModuAktif) KabloModuKapat();
        foreach (var (katSnap, kat) in eslesenler)
            KatDurumGeriYukle(kat!, katSnap);
        Kirlet();

        SeciliElemanlar.Clear();
        SeciliEleman = null;
        GuncelleElemanVurgu();
        SeciliDevreId = null;
        DurumMesaji = "Geri alındı";
        OnPropertyChanged(nameof(Elemanlar)); // MahalListesiWindow için
        GuncelleFiltrelenmisElemanlar();
        GuncelleTumKatPanolari();
        GuncelleKabloGorunum();
        GuncellePanoIcerik();
        Guncelle3DGorunum();
    }

    [RelayCommand]
    private void ElemanIleriAl()
    {
        if (_elemanRedoStack.Count == 0 || KilitliyseDur("değişikliği ileri almak")) return;
        var snap = _elemanRedoStack.Pop();
        var eslesenler = snap.Katlar
            .Select(s => (Snap: s, Kat: SnapshotKatiniBul(s.KatId, s.KatNo)))
            .Where(x => x.Kat is not null)
            .ToList();
        if (eslesenler.Count == 0) return;

        _elemanUndoStack.Push(new ProjeElemanUndoSnapshot(
            eslesenler.Select(x => KatDurumKopyala(x.Kat!)).ToList()));
        if (KabloModuAktif) KabloModuKapat();
        foreach (var (katSnap, kat) in eslesenler)
            KatDurumGeriYukle(kat!, katSnap);
        Kirlet();

        SeciliElemanlar.Clear();
        SeciliEleman = null;
        GuncelleElemanVurgu();
        SeciliDevreId = null;
        DurumMesaji = "İleri alındı";
        OnPropertyChanged(nameof(Elemanlar)); // MahalListesiWindow için
        GuncelleFiltrelenmisElemanlar();
        GuncelleTumKatPanolari();
        GuncelleKabloGorunum();
        GuncellePanoIcerik();
        Guncelle3DGorunum();
    }

    private static BasitProjeEleman CloneEleman(BasitProjeEleman e) => new()
    {
        Id             = e.Id,
        Tip            = e.Tip,
        Ad             = e.Ad,
        EkranAdi       = e.EkranAdi,
        OdaId          = e.OdaId,
        ArmatürTanimId = e.ArmatürTanimId,
        BlokAdi        = e.BlokAdi,
        X              = e.X,
        Y              = e.Y,
        PaftaTipi      = e.PaftaTipi,
        Ozellikler     = e.Ozellikler,
        CustomGucW     = e.CustomGucW,
        MontajTipi     = e.MontajTipi,
        YerlesimTipi   = e.YerlesimTipi,
        GridAraligiM   = e.GridAraligiM,
        GridAraligiYM  = e.GridAraligiYM,
        AlanBasinaAdetM2 = e.AlanBasinaAdetM2,
        DuvarAraligiM  = e.DuvarAraligiM,
        CustomAdet     = e.CustomAdet,
        MonofazeAdet   = e.MonofazeAdet,
        TrifazeAdet    = e.TrifazeAdet,
        Megapiksel     = e.Megapiksel,
        OzelAlanTuru   = e.OzelAlanTuru,
        OtomatikYerlesimKaynak = e.OtomatikYerlesimKaynak,
        OtomatikYerlesimGrupIds = (e.OtomatikYerlesimGrupIds ?? new()).ToList(),
        SensorKontrollu = e.SensorKontrollu,
        BagliOdaIds    = e.BagliOdaIds.ToList(),
        KatArasiSecim  = e.KatArasiSecim,
        TaliPanoHedefPanoId = e.TaliPanoHedefPanoId,
        AydinlatmaSigortalari = e.AydinlatmaSigortalari?.ToDictionary(x => x.Key, x => x.Value)
    };

    private sealed record ElemanUndoSnapshot(
        string KatId,
        int KatNo,
        List<BasitProjeEleman> Elemanlar,
        List<BpcDevre> Devreler,
        List<BpcKablo> Kablolar,
        List<BpcVavienGrup> VavienGruplari,
        List<BpcPrizDevre> PrizDevreleri,
        List<BpcMekanikDevre> MekanikDevreleri,
        List<BpcSistemDevre> SistemDevreleri,
        List<BpcBuatPanoBaglanti> BuatPanoBaglantilari,
        List<BpcMahalSatiri> MahalListesi,
        Dictionary<string, (bool Dahil, bool Gecis)> MahalSecimleri,
        List<BpcKabloTavasi> KabloTavalari,
        List<BpcPanoBeslemeHatti> PanoBeslemeHatlari,
        BpcTopraklama Topraklama);

    private sealed record ProjeElemanUndoSnapshot(List<ElemanUndoSnapshot> Katlar);
}
