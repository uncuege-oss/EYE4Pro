using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Materials;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Services;
using Kst.Acoustics.Core.Architecture.Templates;
using MimariIhtiyacProgrami = Kst.Acoustics.Core.Architecture.Models.IhtiyacProgrami;

namespace Kst.Acoustics.App.Architecture.Materials.ViewModels;

/// <summary>
/// M36.5 atama UI'si: 3D eleman hedeflerine yapi elemani tipi ve malzeme atar.
/// </summary>
public sealed class MalzemeAtamaViewModel : ObservableObject
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private MimariProje _proje = MimariProjeServisi.YeniOlustur("Yeni Mimari Proje");
    private MimariIhtiyacProgrami _program = new();
    private PlanTaslagi _plan = new();
    private Bim3DModel _model = new();

    private string _projeAdi = "Yeni Mimari Proje";
    private string _dosyaYoluText = "Kaydedilmedi";
    private string _durumMesaji = "Hazır.";
    private string _atamaOzetiText = "Atama yok";
    private AtamaHedefiSatiri? _seciliHedef;
    private YapiElemaniTipiSatiri? _seciliTip;
    private MalzemeSatiri? _seciliMalzeme;
    private MalzemeKategoriSecenegi _seciliKategori = MalzemeKategoriSecenegi.Tumu();
    private string _seciliHedefBilgisi = "Hedef seçilmedi.";

    public MalzemeAtamaViewModel()
    {
        OrnekProjeOlustur();
    }

    public ObservableCollection<AtamaHedefiSatiri> Hedefler { get; } = new();
    public ObservableCollection<YapiElemaniTipiSatiri> UygunTipler { get; } = new();
    public ObservableCollection<MalzemeSatiri> Malzemeler { get; } = new();
    public ObservableCollection<MalzemeKategoriSecenegi> KategoriSecenekleri { get; } = new();

    public string? DosyaYolu => _proje.DosyaYolu;

    public string ProjeAdi
    {
        get => _projeAdi;
        set => SetProperty(ref _projeAdi, value);
    }

    public string DosyaYoluText
    {
        get => _dosyaYoluText;
        private set => SetProperty(ref _dosyaYoluText, value);
    }

    public string DurumMesaji
    {
        get => _durumMesaji;
        set => SetProperty(ref _durumMesaji, value);
    }

    public string AtamaOzetiText
    {
        get => _atamaOzetiText;
        private set => SetProperty(ref _atamaOzetiText, value);
    }

    public string SeciliHedefBilgisi
    {
        get => _seciliHedefBilgisi;
        private set => SetProperty(ref _seciliHedefBilgisi, value);
    }

    public AtamaHedefiSatiri? SeciliHedef
    {
        get => _seciliHedef;
        set
        {
            if (SetProperty(ref _seciliHedef, value))
                HedefSecimiDegisti();
        }
    }

    public YapiElemaniTipiSatiri? SeciliTip
    {
        get => _seciliTip;
        set => SetProperty(ref _seciliTip, value);
    }

    public MalzemeSatiri? SeciliMalzeme
    {
        get => _seciliMalzeme;
        set => SetProperty(ref _seciliMalzeme, value);
    }

    public MalzemeKategoriSecenegi SeciliKategori
    {
        get => _seciliKategori;
        set
        {
            if (SetProperty(ref _seciliKategori, value))
                MalzemeleriYenile();
        }
    }

    public void OrnekProjeOlustur()
    {
        _program = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 60, katSayisi: 2, projeTipi: "Ofis");
        _proje = MimariProjeServisi.YeniOlustur("Yeni Mimari Proje");
        _proje.IhtiyacProgrami = _program;
        _proje.MahalSablonlari = VarsayilanMahalSablonlari.Tum.ToList();
        _proje.Malzemeler = VarsayilanMimariMalzemeler.Tum.ToList();
        _proje.YapiElemaniTipleri = VarsayilanYapiElemaniTipleri.Tum.ToList();
        _proje.FinisajProfilleri = VarsayilanMahalFinisajProfilleri.Tum.ToList();
        _plan = PlanTaslagiUretimServisi.Uret(_program, _proje.Id);
        _model = Model3DUretimServisi.Uret(_plan);
        _proje.PlanTaslagi = _plan;

        ProjeAdi = _proje.ProjeAdi;
        DosyaYoluText = "Kaydedilmedi";
        TumListeleriYenile("Örnek mimari proje yüklendi.");
        OnPropertyChanged(nameof(DosyaYolu));
    }

    public void ProjeYukle(MimariProje proje)
    {
        _proje = proje;
        _program = proje.IhtiyacProgrami ?? new MimariIhtiyacProgrami();
        ProjeAdi = string.IsNullOrWhiteSpace(proje.ProjeAdi) ? "Yeni Mimari Proje" : proje.ProjeAdi;

        _plan = proje.PlanTaslagi ?? PlanTaslagiUretimServisi.Uret(_program, proje.Id);
        if (string.IsNullOrWhiteSpace(_plan.ProjeId))
            _plan.ProjeId = proje.Id;

        _model = _plan.Model3D is { Katlar.Count: > 0 } mevcut ? mevcut : Model3DUretimServisi.Uret(_plan);
        _plan.Model3D = _model;
        _proje.PlanTaslagi = _plan;

        if (_proje.Malzemeler.Count == 0)
            _proje.Malzemeler = VarsayilanMimariMalzemeler.Tum.ToList();
        if (_proje.YapiElemaniTipleri.Count == 0)
            _proje.YapiElemaniTipleri = VarsayilanYapiElemaniTipleri.Tum.ToList();

        DosyaYoluText = string.IsNullOrWhiteSpace(proje.DosyaYolu)
            ? "Kaydedilmedi"
            : Path.GetFileName(proje.DosyaYolu);
        TumListeleriYenile($"Açıldı: {DosyaYoluText}");
        OnPropertyChanged(nameof(DosyaYolu));
    }

    public MimariProje KaydaHazirla()
    {
        _proje.ProjeAdi = string.IsNullOrWhiteSpace(ProjeAdi) ? "Yeni Mimari Proje" : ProjeAdi.Trim();
        if (string.IsNullOrWhiteSpace(_plan.ProjeId))
            _plan.ProjeId = _proje.Id;

        _plan.Model3D = _model;
        _proje.IhtiyacProgrami = _program;
        _proje.PlanTaslagi = _plan;
        return _proje;
    }

    public void KayitTamamlandi(string dosyaYolu)
    {
        _proje.DosyaYolu = dosyaYolu;
        DosyaYoluText = Path.GetFileName(dosyaYolu);
        DurumMesaji = $"Kaydedildi: {DosyaYoluText}";
        OnPropertyChanged(nameof(DosyaYolu));
    }

    public void Ata()
    {
        if (SeciliHedef is null)
        {
            DurumMesaji = "Önce bir hedef eleman seçin.";
            return;
        }

        if (SeciliTip is null)
        {
            DurumMesaji = "Önce bir yapı elemanı tipi seçin.";
            return;
        }

        var atama = _proje.MalzemeAtamalari.FirstOrDefault(a =>
            a.KaynakElemanTuru == SeciliHedef.Tur &&
            string.Equals(a.KaynakElemanId, SeciliHedef.ElemanId, StringComparison.OrdinalIgnoreCase));

        if (atama is null)
        {
            atama = new MalzemeAtamasi
            {
                KaynakElemanTuru = SeciliHedef.Tur,
                KaynakElemanId = SeciliHedef.ElemanId,
                KatNo = SeciliHedef.KatNo
            };
            _proje.MalzemeAtamalari.Add(atama);
        }

        atama.YapiElemaniTipiId = SeciliTip.Id;
        atama.KatNo = SeciliHedef.KatNo;
        atama.MalzemeIdleri = SeciliMalzeme is null ? new() : new() { SeciliMalzeme.Id };
        atama.AtamaTarihiUtc = DateTime.UtcNow;

        ElemanTipIdsiniYaz(SeciliHedef, SeciliTip.Id);
        HedefSatiriniGuncelle(SeciliHedef);
        AtamaOzetiniYenile();
        DurumMesaji = $"'{SeciliHedef.Etiket}' → {SeciliTip.Ad} atandı.";
    }

    public void AtamayiKaldir()
    {
        if (SeciliHedef is null)
        {
            DurumMesaji = "Önce bir hedef eleman seçin.";
            return;
        }

        var kaldirilan = _proje.MalzemeAtamalari.RemoveAll(a =>
            a.KaynakElemanTuru == SeciliHedef.Tur &&
            string.Equals(a.KaynakElemanId, SeciliHedef.ElemanId, StringComparison.OrdinalIgnoreCase));

        if (kaldirilan == 0)
        {
            DurumMesaji = "Seçili hedefte kaldırılacak atama yok.";
            return;
        }

        ElemanTipIdsiniYaz(SeciliHedef, "");
        HedefSatiriniGuncelle(SeciliHedef);
        AtamaOzetiniYenile();
        DurumMesaji = $"'{SeciliHedef.Etiket}' ataması kaldırıldı.";
    }

    private void TumListeleriYenile(string durumMesaji)
    {
        KategorileriYenile();
        MalzemeleriYenile();
        HedefleriYenile();
        AtamaOzetiniYenile();
        DurumMesaji = durumMesaji;
    }

    private void KategorileriYenile()
    {
        var oncekiKategori = _seciliKategori?.Kategori;
        KategoriSecenekleri.Clear();
        KategoriSecenekleri.Add(MalzemeKategoriSecenegi.Tumu());
        foreach (var kategori in _proje.Malzemeler
                     .Select(m => m.Kategori)
                     .Distinct()
                     .OrderBy(k => KategoriAdi(k), StringComparer.CurrentCulture))
        {
            KategoriSecenekleri.Add(new MalzemeKategoriSecenegi(kategori, KategoriAdi(kategori)));
        }

        _seciliKategori = KategoriSecenekleri.FirstOrDefault(k => k.Kategori == oncekiKategori)
            ?? KategoriSecenekleri[0];
        OnPropertyChanged(nameof(SeciliKategori));
    }

    private void MalzemeleriYenile()
    {
        Malzemeler.Clear();
        var kaynak = _proje.Malzemeler.AsEnumerable();
        if (_seciliKategori is { TumKategoriler: false } secili)
            kaynak = kaynak.Where(m => m.Kategori == secili.Kategori);

        foreach (var malzeme in kaynak.OrderBy(m => m.Ad, StringComparer.CurrentCulture))
        {
            Malzemeler.Add(new MalzemeSatiri(malzeme));
        }
    }

    private void HedefleriYenile()
    {
        Hedefler.Clear();
        foreach (var kat in _model.Katlar.OrderBy(k => k.KatNo))
        {
            foreach (var mahal in kat.Mahaller.OrderBy(m => m.Ad, StringComparer.CurrentCulture))
                Hedefler.Add(YeniHedef(MalzemeAtamaHedefTuru.Mahal, mahal.Id, kat.KatNo,
                    string.IsNullOrWhiteSpace(mahal.Ad) ? "Mahal" : mahal.Ad, "Mahal"));

            foreach (var doseme in kat.Dosemeler)
                Hedefler.Add(YeniHedef(MalzemeAtamaHedefTuru.Doseme, doseme.Id, kat.KatNo, "Döşeme", "Döşeme"));

            foreach (var tavan in kat.Tavanlar)
                Hedefler.Add(YeniHedef(MalzemeAtamaHedefTuru.Tavan, tavan.Id, kat.KatNo, "Tavan", "Tavan"));

            var duvarSayaci = 0;
            foreach (var duvar in kat.Duvarlar)
                Hedefler.Add(YeniHedef(MalzemeAtamaHedefTuru.Duvar, duvar.Id, kat.KatNo, $"Duvar {++duvarSayaci}", "Duvar"));

            foreach (var aciklik in kat.Acikliklar)
            {
                var tur = aciklik.Tip == Aciklik3DTipi.Pencere
                    ? MalzemeAtamaHedefTuru.Pencere
                    : MalzemeAtamaHedefTuru.Kapi;
                var etiket = aciklik.Tip == Aciklik3DTipi.Pencere ? "Pencere" : "Kapı";
                Hedefler.Add(YeniHedef(tur, aciklik.Id, kat.KatNo, etiket, etiket));
            }
        }
    }

    private AtamaHedefiSatiri YeniHedef(MalzemeAtamaHedefTuru tur, string elemanId, int katNo, string ad, string turAdi)
    {
        var satir = new AtamaHedefiSatiri(tur, elemanId, katNo, $"{ad} (Kat {katNo.ToString(Tr)})", turAdi);
        HedefSatiriniGuncelle(satir);
        return satir;
    }

    private void HedefSatiriniGuncelle(AtamaHedefiSatiri satir)
    {
        var atama = _proje.MalzemeAtamalari.FirstOrDefault(a =>
            a.KaynakElemanTuru == satir.Tur &&
            string.Equals(a.KaynakElemanId, satir.ElemanId, StringComparison.OrdinalIgnoreCase));

        if (atama is null || string.IsNullOrWhiteSpace(atama.YapiElemaniTipiId))
        {
            satir.AtananTipAdi = "—";
            return;
        }

        var tip = TipBul(atama.YapiElemaniTipiId);
        satir.AtananTipAdi = tip?.Ad ?? atama.YapiElemaniTipiId;
    }

    private void HedefSecimiDegisti()
    {
        UygunTipler.Clear();
        if (SeciliHedef is null)
        {
            SeciliHedefBilgisi = "Hedef seçilmedi.";
            SeciliTip = null;
            return;
        }

        foreach (var tip in TipKatalogu()
                     .Where(t => t.HedefTuruIleUyumluMu(SeciliHedef.Tur))
                     .OrderBy(t => t.Ad, StringComparer.CurrentCulture))
        {
            UygunTipler.Add(new YapiElemaniTipiSatiri(tip));
        }

        var atama = _proje.MalzemeAtamalari.FirstOrDefault(a =>
            a.KaynakElemanTuru == SeciliHedef.Tur &&
            string.Equals(a.KaynakElemanId, SeciliHedef.ElemanId, StringComparison.OrdinalIgnoreCase));

        SeciliTip = atama is null
            ? UygunTipler.FirstOrDefault()
            : UygunTipler.FirstOrDefault(t => string.Equals(t.Id, atama.YapiElemaniTipiId, StringComparison.OrdinalIgnoreCase))
              ?? UygunTipler.FirstOrDefault();

        SeciliHedefBilgisi = $"{SeciliHedef.TurAdi} • {SeciliHedef.Etiket} • {UygunTipler.Count.ToString(Tr)} uygun tip";
    }

    private void AtamaOzetiniYenile()
    {
        var toplam = Hedefler.Count;
        var atanan = _proje.MalzemeAtamalari.Count(a => a.GecerliMi);
        AtamaOzetiText = $"{atanan.ToString(Tr)} / {toplam.ToString(Tr)} hedef atandı";
    }

    private void ElemanTipIdsiniYaz(AtamaHedefiSatiri hedef, string tipId)
    {
        foreach (var kat in _model.Katlar)
        {
            switch (hedef.Tur)
            {
                case MalzemeAtamaHedefTuru.Duvar:
                    var duvar = kat.Duvarlar.FirstOrDefault(d => d.Id == hedef.ElemanId);
                    if (duvar is not null) duvar.DuvarTipiId = tipId;
                    break;
                case MalzemeAtamaHedefTuru.Doseme:
                    var doseme = kat.Dosemeler.FirstOrDefault(d => d.Id == hedef.ElemanId);
                    if (doseme is not null) doseme.DosemeTipiId = tipId;
                    break;
                case MalzemeAtamaHedefTuru.Tavan:
                    var tavan = kat.Tavanlar.FirstOrDefault(t => t.Id == hedef.ElemanId);
                    if (tavan is not null) tavan.TavanTipiId = tipId;
                    break;
            }
        }
    }

    private IEnumerable<YapiElemaniTipi> TipKatalogu() => _proje.YapiElemaniTipleri;

    private YapiElemaniTipi? TipBul(string id)
        => _proje.YapiElemaniTipleri.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    private static string KategoriAdi(MalzemeKategorisi kategori) => kategori switch
    {
        MalzemeKategorisi.ZeminKaplamasi => "Zemin Kaplaması",
        MalzemeKategorisi.DuvarKaplamasi => "Duvar Kaplaması",
        MalzemeKategorisi.TavanKaplamasi => "Tavan Kaplaması",
        MalzemeKategorisi.Kaplama => "Kaplama",
        MalzemeKategorisi.Yalitim => "Yalıtım",
        MalzemeKategorisi.Boya => "Boya",
        MalzemeKategorisi.Siva => "Sıva",
        MalzemeKategorisi.Seramik => "Seramik",
        MalzemeKategorisi.Cam => "Cam",
        MalzemeKategorisi.Metal => "Metal",
        MalzemeKategorisi.Ahsap => "Ahşap",
        MalzemeKategorisi.Beton => "Beton",
        MalzemeKategorisi.Alci => "Alçı",
        MalzemeKategorisi.Membran => "Membran",
        MalzemeKategorisi.Kapi => "Kapı",
        MalzemeKategorisi.Pencere => "Pencere",
        MalzemeKategorisi.Cekirdek => "Çekirdek",
        MalzemeKategorisi.Genel => "Genel",
        _ => "Tanımsız"
    };
}

public sealed class AtamaHedefiSatiri : ObservableObject
{
    private string _atananTipAdi = "—";

    public AtamaHedefiSatiri(MalzemeAtamaHedefTuru tur, string elemanId, int katNo, string etiket, string turAdi)
    {
        Tur = tur;
        ElemanId = elemanId;
        KatNo = katNo;
        Etiket = etiket;
        TurAdi = turAdi;
    }

    public MalzemeAtamaHedefTuru Tur { get; }
    public string ElemanId { get; }
    public int KatNo { get; }
    public string Etiket { get; }
    public string TurAdi { get; }

    public string AtananTipAdi
    {
        get => _atananTipAdi;
        set => SetProperty(ref _atananTipAdi, value);
    }
}

public sealed class YapiElemaniTipiSatiri
{
    public YapiElemaniTipiSatiri(YapiElemaniTipi tip)
    {
        Id = tip.Id;
        Ad = string.IsNullOrWhiteSpace(tip.Ad) ? tip.Kod : tip.Ad;
        Kod = tip.Kod;
    }

    public string Id { get; }
    public string Ad { get; }
    public string Kod { get; }
    public string Gosterim => string.IsNullOrWhiteSpace(Kod) ? Ad : $"{Ad} ({Kod})";
}

public sealed class MalzemeSatiri
{
    public MalzemeSatiri(MimariMalzeme malzeme)
    {
        Id = malzeme.Id;
        Ad = string.IsNullOrWhiteSpace(malzeme.Ad) ? malzeme.Kod : malzeme.Ad;
        Kod = malzeme.Kod;
    }

    public string Id { get; }
    public string Ad { get; }
    public string Kod { get; }
    public string Gosterim => string.IsNullOrWhiteSpace(Kod) ? Ad : $"{Ad} ({Kod})";
}

public sealed class MalzemeKategoriSecenegi
{
    public MalzemeKategoriSecenegi(MalzemeKategorisi? kategori, string ad)
    {
        Kategori = kategori;
        Ad = ad;
    }

    public MalzemeKategorisi? Kategori { get; }
    public string Ad { get; }
    public bool TumKategoriler => Kategori is null;

    public static MalzemeKategoriSecenegi Tumu() => new(null, "Tüm kategoriler");
}
