using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.App.Bina.ViewModels;

public class BinaGeometriViewModel : INotifyPropertyChanged
{
    private const double MaxYerelKoordinatM = 10_000_000;
    // ── Model ────────────────────────────────────────────────────────────────

    private BinaGeometri _geo = BinaGeometriServisi.YeniOlustur();
    public BinaGeometri Geo
    {
        get => _geo;
        set
        {
            if (_geo == value) return;
            _geo = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PencereBasligi));
            ReferansKat = null;
            SeciliKat = value.Katlar.Count > 0 ? value.Katlar[0] : null;
            SeciliAciklikId = null;
            UndoYiginlariniTemizle();
            OnPropertyChanged(nameof(KatYokMu));
        }
    }

    // ── Değişiklik durumu (IsDirty) — codex §4.2 ──────────────────────────────

    private bool _isDirty;
    /// Kaydedilmemiş değişiklik var mı? Başlıkta `*` gösterilir; temizken modül
    /// kapanışta soru sormadan çıkar.
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PencereBasligi));
        }
    }

    /// History dışından gelen son-çare işaret. Normal UI mutasyonları
    /// UndoSnapshotAl/SnapshotKaydet hattından geçmelidir. Kayıt checkpoint'indeyken
    /// ayrı durum kimliği üretir; böylece sonraki edit→undo dış mutasyonu yanlışlıkla
    /// temiz saymaz (ör. otomatik kurtarma açılışı).
    public void Kirlet()
    {
        if (_mevcutDurumId == _kayitliDurumId)
            _mevcutDurumId = ++_sonrakiDurumId;
        _redoStack.Clear();
        IsDirty = true;
        UndoDurumBildir();
    }

    /// Kaydet / yeni / aç sonrası temiz işaretle.
    public void TemizIsaretle()
    {
        _kayitliDurumId = _mevcutDurumId;
        IsDirty = false;
    }

    private BinaKat? _seciliKat;
    public BinaKat? SeciliKat
    {
        get => _seciliKat;
        set
        {
            if (_seciliKat == value) return;
            _seciliKat = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EtkinReferansKat));
            OnPropertyChanged(nameof(KatYukariTasinabilir));
            OnPropertyChanged(nameof(KatAsagiTasinabilir));
            OnPropertyChanged(nameof(AciklikTaranabilir));
            SeciliOdaId = null;
            SeciliAciklikId = null;
            SyncKatProperties();
        }
    }

    // ── Referans (hayalet) kat — 2D'de iki katı üst üste gösterip hizalama ──────

    private BinaKat? _referansKat;
    /// Combo'dan seçilen referans kat. Aktif katla aynıysa hayalet gösterilmez.
    public BinaKat? ReferansKat
    {
        get => _referansKat;
        set
        {
            if (_referansKat == value) return;
            _referansKat = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EtkinReferansKat));
        }
    }

    /// Canvas'a verilen gerçek referans: aktif katla aynıysa null (kendini hayalet çizmez).
    public BinaKat? EtkinReferansKat =>
        _referansKat is not null && !ReferenceEquals(_referansKat, _seciliKat)
            ? _referansKat : null;

    private string? _seciliOdaId;
    public string? SeciliOdaId
    {
        get => _seciliOdaId;
        set
        {
            if (_seciliOdaId == value) return;
            _seciliOdaId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SeciliOda));
            OnPropertyChanged(nameof(OdaSecili));
            OnPropertyChanged(nameof(SeciliOdaBilgi));
            OnPropertyChanged(nameof(SeciliOdaBirlesik));
        }
    }

    public BinaOda? SeciliOda =>
        _seciliKat?.Odalar.FirstOrDefault(o => o.Id == _seciliOdaId);

    public bool OdaSecili => SeciliOda is not null;

    public string SeciliOdaBilgi =>
        SeciliOda is { } o ? OdaBilgiMetni(o) : "";

    private static string OdaBilgiMetni(BinaOda oda)
    {
        if (oda.Poligon.Count < 3)
            return $"{oda.AlanM2:F1} m²";

        double minX = oda.Poligon.Min(p => p.X);
        double maxX = oda.Poligon.Max(p => p.X);
        double minY = oda.Poligon.Min(p => p.Y);
        double maxY = oda.Poligon.Max(p => p.Y);
        return $"{oda.AlanM2:F1} m²  ·  ~{maxX - minX:0.##} × {maxY - minY:0.##} m";
    }

    // ── UI durumu ─────────────────────────────────────────────────────────────

    private string _durumMesaji = "Yeni bina geometrisi ─ DXF yükleyerek veya kat ekleyerek başlayın.";
    public string DurumMesaji
    {
        get => _durumMesaji;
        set { if (_durumMesaji != value) { _durumMesaji = value; OnPropertyChanged(); } }
    }

    public string PencereBasligi =>
        (IsDirty ? "* " : "") +
        (string.IsNullOrWhiteSpace(Geo.BinaAdi)
            ? "Bina Geometrisi"
            : $"Bina Geometrisi — {Geo.BinaAdi}");

    private bool _cetvelGoster = true;
    public bool CetvelGoster
    {
        get => _cetvelGoster;
        set
        {
            if (_cetvelGoster == value) return;
            _cetvelGoster = value;
            OnPropertyChanged();
        }
    }

    public bool KatVarMi => _seciliKat is not null;
    public bool KatYokMu => _seciliKat is null;
    public bool KatYukariTasinabilir => _seciliKat is { } k && Geo.Katlar.IndexOf(k) > 0;
    public bool KatAsagiTasinabilir => _seciliKat is { } k && Geo.Katlar.IndexOf(k) is var i && i >= 0 && i < Geo.Katlar.Count - 1;
    public bool AciklikTaranabilir => _seciliKat is { } k &&
        (k.TespitSegmentler.Count > 0 || k.HamSegmentler.Count > 0 ||
         (!string.IsNullOrWhiteSpace(k.DxfFilePath) && System.IO.File.Exists(k.DxfFilePath)));

    public bool BinaAdiGuncelle(string? ad)
    {
        string yeni = string.IsNullOrWhiteSpace(ad) ? "Bina" : ad.Trim();
        if (Geo.BinaAdi == yeni) return false;
        UndoSnapshotAl();
        Geo.BinaAdi = yeni;
        OnPropertyChanged(nameof(PencereBasligi));
        OnPropertyChanged(nameof(Geo));
        return true;
    }

    public bool KatAdiGuncelle(string? ad)
    {
        if (_seciliKat is not { } kat) return false;
        string yeni = string.IsNullOrWhiteSpace(ad) ? $"Kat {kat.KatNo}" : ad.Trim();
        if (kat.Ad == yeni) return false;
        UndoSnapshotAl();
        kat.Ad = yeni;
        DurumMesaji = $"Kat adı '{yeni}' olarak güncellendi.";
        return true;
    }

    public bool OdaAdiGuncelle(string? ad)
    {
        if (SeciliOda is not { } oda) return false;
        string yeni = string.IsNullOrWhiteSpace(ad) ? "Oda" : ad.Trim();
        if (oda.Ad == yeni) return false;
        UndoSnapshotAl();
        oda.Ad = yeni;
        DurumMesaji = $"Oda adı '{yeni}' olarak güncellendi.";
        return true;
    }

    public bool KatYuksekligiGuncelle(double yukseklikM)
    {
        if (_seciliKat is not { } kat) return false;
        if (!double.IsFinite(yukseklikM) || yukseklikM < 0.2 || yukseklikM > 100)
        {
            DurumMesaji = "Kat yüksekliği 0,20–100 m arasında sonlu bir sayı olmalı.";
            return false;
        }
        double gereken = kat.Acikliklar.Where(a => !a.IptalEdildi)
            .Select(a => a.EtkinEsikYuksekligiM + a.YukseklikM)
            .DefaultIfEmpty(0).Max();
        if (gereken > yukseklikM + 1e-9)
        {
            DurumMesaji = $"Kat yüksekliği, açıklık üst kotundan ({gereken:F2} m) küçük olamaz.";
            return false;
        }
        if (System.Math.Abs(kat.KatYuksekligiM - yukseklikM) < 1e-9) return false;
        UndoSnapshotAl();
        kat.KatYuksekligiM = yukseklikM;
        return true;
    }

    public bool KatGorunumuGuncelle(bool? dxfGoster = null, bool? bloklariGoster = null)
    {
        if (_seciliKat is not { } kat) return false;
        bool degisti = (dxfGoster.HasValue && kat.DxfGoster != dxfGoster.Value) ||
                       (bloklariGoster.HasValue && kat.BloklariGoster != bloklariGoster.Value);
        if (!degisti) return false;
        UndoSnapshotAl();
        if (dxfGoster.HasValue) kat.DxfGoster = dxfGoster.Value;
        if (bloklariGoster.HasValue) kat.BloklariGoster = bloklariGoster.Value;
        return true;
    }

    // ── Seçili kat proxy properties (OriginOffset struct olduğundan proxy şart) ─

    private bool _katOfsetSyncing;

    private double _katOfsetX;
    public double KatOfsetX
    {
        get => _katOfsetX;
        set
        {
            if (!double.IsFinite(value) || System.Math.Abs(value) > MaxYerelKoordinatM)
            {
                DurumMesaji = $"Ofset sonlu ve ±{MaxYerelKoordinatM:N0} m aralığında olmalı.";
                return;
            }
            if (System.Math.Abs(_katOfsetX - value) < 1e-9) return;
            if (!_katOfsetSyncing && _seciliKat is not null) UndoSnapshotAl();
            _katOfsetX = value;
            OnPropertyChanged();
            if (!_katOfsetSyncing && _seciliKat is { } kat)
                kat.OriginOffset = new Point2D(value, kat.OriginOffset.Y);
        }
    }

    private double _katOfsetY;
    public double KatOfsetY
    {
        get => _katOfsetY;
        set
        {
            if (!double.IsFinite(value) || System.Math.Abs(value) > MaxYerelKoordinatM)
            {
                DurumMesaji = $"Ofset sonlu ve ±{MaxYerelKoordinatM:N0} m aralığında olmalı.";
                return;
            }
            if (System.Math.Abs(_katOfsetY - value) < 1e-9) return;
            if (!_katOfsetSyncing && _seciliKat is not null) UndoSnapshotAl();
            _katOfsetY = value;
            OnPropertyChanged();
            if (!_katOfsetSyncing && _seciliKat is { } kat)
                kat.OriginOffset = new Point2D(kat.OriginOffset.X, value);
        }
    }

    // ── Kat yönetimi komutları ────────────────────────────────────────────────

    public BinaKat KatEkle(string? ad = null, double yukseklikM = 3.0)
    {
        int no = Geo.Katlar.Count + 1;
        UndoSnapshotAl();
        var kat = new BinaKat
        {
            Id = Guid.NewGuid().ToString(),
            Ad = string.IsNullOrWhiteSpace(ad) ? $"Kat {no}" : ad.Trim(),
            KatNo = no,
            KatYuksekligiM = double.IsFinite(yukseklikM) && yukseklikM is >= 0.2 and <= 100
                ? yukseklikM : 3.0
        };
        Geo.Katlar.Add(kat);
        SeciliKat = kat;
        DurumMesaji = $"'{kat.Ad}' eklendi.";
        return kat;
    }

    public void KatlarEkle(System.Collections.Generic.IEnumerable<BinaKat> katlar)
    {
        var eklenecekler = katlar.ToList();
        if (eklenecekler.Count == 0) return;
        UndoSnapshotAl();
        foreach (var kat in eklenecekler) Geo.Katlar.Add(kat);
        KatlariYenidenNumaralandir();
        SeciliKat = eklenecekler[0];
        DurumMesaji = $"{eklenecekler.Count} kat eklendi.";
    }

    public (bool Basarili, string Mesaj) SeciliKataDxfUygula(
        BinaGeometri beklenenGeo, BinaKat hedefKat, BinaKat yeniKat, string path)
    {
        if (!ReferenceEquals(_geo, beklenenGeo) || !Geo.Katlar.Contains(hedefKat))
            return (false, "Belge veya hedef kat yükleme sırasında değişti; DXF sonucu uygulanmadı.");

        // Pahalı/eşleştirme işlemlerini mevcut modele dokunmadan hazırla.
        var eskiOdalar = hedefKat.Odalar.ToList();
        var eskiAcikliklar = hedefKat.Acikliklar.ToList();
        int eskiSegmentSayisi = hedefKat.DxfSegmentler.Count;
        var silinenOdaIdler = OdaEslestirmeServisi.Eslestir(yeniKat.Odalar, eskiOdalar);
        var arclarTuple = yeniKat.DxfArclar
            .Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg))
            .ToList();
        var yeniAcikliklar = AciklikTespitServisi.Bul(
            yeniKat.TespitSegmentler.Count > 0 ? yeniKat.TespitSegmentler : yeniKat.HamSegmentler,
            arclarTuple, eskiAcikliklar);
        yeniKat.Acikliklar = yeniAcikliklar;
        AciklikYardimci.DisindaKalanlariSil(yeniKat);
        AciklikYardimci.KomsuDuvarlariTamamla(yeniKat);

        UndoSnapshotAl();
        hedefKat.DxfFilePath = path;
        hedefKat.DxfSegmentler = yeniKat.DxfSegmentler;
        hedefKat.DxfArclar = yeniKat.DxfArclar;
        hedefKat.UnitWarning = yeniKat.UnitWarning;
        hedefKat.DxfBirimOlcegiM = yeniKat.DxfBirimOlcegiM;
        hedefKat.DxfNormalizeOfsetiM = yeniKat.DxfNormalizeOfsetiM;
        hedefKat.DxfKaynakBolgesiMinM = yeniKat.DxfKaynakBolgesiMinM;
        hedefKat.DxfKaynakBolgesiMaxM = yeniKat.DxfKaynakBolgesiMaxM;
        hedefKat.HamSegmentler = yeniKat.HamSegmentler;
        hedefKat.TespitSegmentler = yeniKat.TespitSegmentler;
        hedefKat.HamMetinler = yeniKat.HamMetinler;
        hedefKat.Odalar.Clear();
        foreach (var oda in yeniKat.Odalar) hedefKat.Odalar.Add(oda);
        hedefKat.Acikliklar = yeniKat.Acikliklar;
        hedefKat.SayilariYenile();

        int kaldirilanBirlesim = 0;
        foreach (var id in silinenOdaIdler)
            kaldirilanBirlesim += BinaDuseyBirlesimServisi.OdaSilindi(Geo, id);

        SeciliOdaId = null;
        SeciliAciklikId = null;
        OnPropertyChanged(nameof(AciklikTaranabilir));
        string uyari = yeniKat.UnitWarning is { } w ? $"  ⚠ {w}" : "";
        return (true,
            $"Kat DXF'i değiştirildi: '{hedefKat.Ad}' · " +
            $"oda {eskiOdalar.Count}→{hedefKat.Odalar.Count}, " +
            $"açıklık {eskiAcikliklar.Count}→{hedefKat.Acikliklar.Count}, " +
            $"segment {eskiSegmentSayisi}→{hedefKat.DxfSegmentler.Count}, " +
            $"kaldırılan düşey birleşim {kaldirilanBirlesim}{uyari}");
    }

    public void KatSil()
    {
        var kat = _seciliKat;
        if (kat is null) return;
        int idx = Geo.Katlar.IndexOf(kat);
        if (idx < 0) return;
        UndoSnapshotAl();

        // Silinen katın odalarına bağlı düşey birleşimleri kaldır — aksi halde
        // partner oda hayalet açık tavan/döşeme raporlar (askıda birleşim).
        int kaldirilanBirlesim = 0;
        foreach (var oda in kat.Odalar)
            kaldirilanBirlesim += BinaDuseyBirlesimServisi.OdaSilindi(Geo, oda.Id);

        Geo.Katlar.Remove(kat);
        KatlariYenidenNumaralandir();
        SeciliKat = Geo.Katlar.Count > 0
            ? Geo.Katlar[System.Math.Max(0, idx - 1)]
            : null;
        DurumMesaji = kaldirilanBirlesim > 0
            ? $"'{kat.Ad}' silindi ({kaldirilanBirlesim} düşey birleşim kaldırıldı)."
            : $"'{kat.Ad}' silindi.";
    }

    public void KatYukari()
    {
        var kat = _seciliKat;
        if (kat is null) return;
        int idx = Geo.Katlar.IndexOf(kat);
        if (idx <= 0) return;
        UndoSnapshotAl();
        Geo.Katlar.Move(idx, idx - 1);
        KatlariYenidenNumaralandir();
        OnPropertyChanged(nameof(KatYukariTasinabilir));
        OnPropertyChanged(nameof(KatAsagiTasinabilir));
    }

    public void KatAsagi()
    {
        var kat = _seciliKat;
        if (kat is null) return;
        int idx = Geo.Katlar.IndexOf(kat);
        if (idx < 0 || idx >= Geo.Katlar.Count - 1) return;
        UndoSnapshotAl();
        Geo.Katlar.Move(idx, idx + 1);
        KatlariYenidenNumaralandir();
        OnPropertyChanged(nameof(KatYukariTasinabilir));
        OnPropertyChanged(nameof(KatAsagiTasinabilir));
    }

    public void KatlariHizala()
    {
        if (!Geo.Katlar.Any(k => System.Math.Abs(k.OriginOffset.X) > 1e-9 || System.Math.Abs(k.OriginOffset.Y) > 1e-9))
        {
            DurumMesaji = "Katlar zaten sıfır ofsetinde hizalı.";
            return;
        }
        UndoSnapshotAl();
        foreach (var k in Geo.Katlar)
            k.OriginOffset = new Point2D(0, 0);
        SyncKatProperties();
        DurumMesaji = "Tüm katlar sıfır noktasına hizalandı.";
    }

    private void KatlariYenidenNumaralandir()
    {
        for (int i = 0; i < Geo.Katlar.Count; i++)
            Geo.Katlar[i].KatNo = i + 1;
        // KatNo değişti → düşey birleşim üyelerinin KatNo/Üst-Alt bilgisini kararlı
        // KatId üzerinden tazele (aksi halde reorder birleşimleri yanlış kata bağlar).
        BinaDuseyBirlesimServisi.KatNolariYenile(Geo);
    }

    private void SyncKatProperties()
    {
        OnPropertyChanged(nameof(KatVarMi));
        OnPropertyChanged(nameof(KatYokMu));
        _katOfsetSyncing = true;
        _katOfsetX = _seciliKat?.OriginOffset.X ?? 0;
        _katOfsetY = _seciliKat?.OriginOffset.Y ?? 0;
        _katOfsetSyncing = false;
        OnPropertyChanged(nameof(KatOfsetX));
        OnPropertyChanged(nameof(KatOfsetY));
    }

    // ── Oda düzenleme komutları ───────────────────────────────────────────────

    /// Çizilen poligondan yeni oda oluşturur (dünya koordinatı = kat-yerel koordinat).
    public void OdaEkle(System.Collections.Generic.List<Point2D> poligon)
    {
        var kat = _seciliKat;
        if (kat is null || poligon.Count < 3) return;
        UndoSnapshotAl();
        var oda = new BinaOda
        {
            Ad      = $"Oda {kat.Odalar.Count + 1}",
            AlanM2  = AlanHesapla(poligon),
            Poligon = poligon
        };
        kat.Odalar.Add(oda);
        kat.SayilariYenile();
        SeciliOdaId = oda.Id;
        DurumMesaji = $"'{oda.Ad}' çizildi ({oda.AlanM2:N1} m²).";
    }

    public void SeciliOdaSil()
    {
        var kat = _seciliKat;
        var oda = SeciliOda;
        if (kat is null || oda is null) return;
        UndoSnapshotAl();
        kat.Odalar.Remove(oda);
        int yetimAciklik = YetimAcikliklariSil(kat);
        kat.SayilariYenile();
        // Askıda birleşim bırakma (Id kararlılığı — silinen odanın birleşimleri düşer)
        int kaldirilanBirlesim = BinaDuseyBirlesimServisi.OdaSilindi(Geo, oda.Id);
        SeciliOdaId = null;
        var ek = yetimAciklik > 0 ? $", {yetimAciklik} yetim açıklık kaldırıldı" : "";
        DurumMesaji = kaldirilanBirlesim > 0
            ? $"'{oda.Ad}' silindi ({kaldirilanBirlesim} düşey birleşim kaldırıldı{ek})."
            : $"'{oda.Ad}' silindi{(yetimAciklik > 0 ? $" ({yetimAciklik} yetim açıklık kaldırıldı)" : "")}.";
    }

    public void VertexGuncelle(int index, double x, double y)
    {
        var oda = SeciliOda;
        if (oda is null || index < 0 || index >= oda.Poligon.Count) return;
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        var yeni = new Point2D(x, y);
        var eski = oda.Poligon[index];
        if (Distance(eski, yeni) < 1e-9) return;
        int onceki = (index - 1 + oda.Poligon.Count) % oda.Poligon.Count;
        int sonraki = (index + 1) % oda.Poligon.Count;
        if (_seciliKat is { } kat)
        {
            BagliAcikliklariDonustur(kat, oda.Poligon[onceki], eski, oda.Poligon[onceki], yeni);
            BagliAcikliklariDonustur(kat, eski, oda.Poligon[sonraki], yeni, oda.Poligon[sonraki]);
        }
        oda.Poligon[index] = yeni;
        oda.AlanM2 = AlanHesapla(oda.Poligon);
        OdaAlanDegistiBildir(oda);
    }

    public void KenarGuncelle(int index, double x0, double y0, double x1, double y1)
    {
        var oda = SeciliOda;
        var kat = _seciliKat;
        if (oda is null || kat is null || index < 0 || index >= oda.Poligon.Count) return;
        int i1 = (index + 1) % oda.Poligon.Count;
        var yeni0 = new Point2D(x0, y0);
        var yeni1 = new Point2D(x1, y1);
        if (!double.IsFinite(x0) || !double.IsFinite(y0) || !double.IsFinite(x1) || !double.IsFinite(y1)) return;
        var eski0 = oda.Poligon[index];
        var eski1 = oda.Poligon[i1];
        BagliAcikliklariDonustur(kat, eski0, eski1, yeni0, yeni1);
        oda.Poligon[index] = yeni0;
        oda.Poligon[i1] = yeni1;
        oda.AlanM2 = AlanHesapla(oda.Poligon);
        OdaAlanDegistiBildir(oda);
    }

    public void OdaGeometriDuzenlemeBitti()
    {
        if (_seciliKat is not { } kat) return;
        int silinen = YetimAcikliklariSil(kat);
        if (silinen > 0)
        {
            kat.SayilariYenile();
            DurumMesaji = $"Geometri güncellendi; {silinen} duvardan kopan açıklık kaldırıldı.";
        }
    }

    public void VertexSil(int index)
    {
        var oda = SeciliOda;
        if (oda is null || oda.Poligon.Count <= 3) return; // üçgenden küçülemez
        if (index < 0 || index >= oda.Poligon.Count) return;
        UndoSnapshotAl();
        oda.Poligon.RemoveAt(index);
        oda.AlanM2 = AlanHesapla(oda.Poligon);
        if (_seciliKat is { } kat) YetimAcikliklariSil(kat);
        OdaAlanDegistiBildir(oda);
        DurumMesaji = $"'{oda.Ad}' köşesi silindi ({oda.Poligon.Count} köşe kaldı).";
    }

    public void VertexEkleSonra(int index, double x, double y)
    {
        var oda = SeciliOda;
        if (oda is null || index < 0 || index >= oda.Poligon.Count) return;
        UndoSnapshotAl();
        oda.Poligon.Insert(index + 1, new Point2D(x, y));
        oda.AlanM2 = AlanHesapla(oda.Poligon);
        OdaAlanDegistiBildir(oda);
        DurumMesaji = $"'{oda.Ad}' kenarına köşe eklendi ({oda.Poligon.Count} köşe).";
    }

    private void OdaAlanDegistiBildir(BinaOda oda)
    {
        OnPropertyChanged(nameof(SeciliOdaBilgi));
        OnPropertyChanged(nameof(SeciliOda));
    }

    // ── Undo / Redo — tüm belgeyi kronolojik olarak kapsar ──────────────────

    private sealed record GeoSnapshot(
        BinaGeometri Geo,
        string? SeciliKatId,
        string? SeciliOdaId,
        string? SeciliAciklikId,
        string? ReferansKatId,
        long DurumId);

    private readonly System.Collections.Generic.Stack<GeoSnapshot> _undoStack = new();
    private readonly System.Collections.Generic.Stack<GeoSnapshot> _redoStack = new();
    private const int MaxUndo = 20;
    private long _sonrakiDurumId = 1;
    private long _mevcutDurumId = 1;
    private long _kayitliDurumId = 1;

    public bool GeriAlinabilir  => _undoStack.Count > 0;
    public bool IleriAlinabilir => _redoStack.Count > 0;

    /// Geometriyi değiştiren her komutun başında çağrılır.
    public void UndoSnapshotAl()
        => SnapshotKaydet(SnapshotOlustur());

    /// Önceden alınmış bir snapshot'ı yığına işler. Yalnız İŞLEM BAŞARILI olduğunda
    /// çağrılmalı (aksi halde başarısız komutlar boş undo adımı + sahte IsDirty üretir).
    private void SnapshotKaydet(GeoSnapshot snapshot)
    {
        _undoStack.Push(snapshot);
        _redoStack.Clear();
        if (_undoStack.Count > MaxUndo)
        {
            var liste = _undoStack.ToList();
            _undoStack.Clear();
            foreach (var s in System.Linq.Enumerable.Reverse(liste.Take(MaxUndo)))
                _undoStack.Push(s);
        }
        _mevcutDurumId = ++_sonrakiDurumId;
        IsDirty = _mevcutDurumId != _kayitliDurumId;
        UndoDurumBildir();
    }

    public void GeriAl()
    {
        if (_undoStack.Count > 0)
        {
            var snapshot = _undoStack.Pop();
            _redoStack.Push(SnapshotOlustur());
            _mevcutDurumId = snapshot.DurumId;
            SnapshotGeriYukle(snapshot);
            DurumMesaji = "Geri alındı.";
            return;
        }
        UndoDurumBildir();
        DurumMesaji = "Geri alınacak adım yok.";
    }

    public void IleriAl()
    {
        if (_redoStack.Count > 0)
        {
            var snapshot = _redoStack.Pop();
            _undoStack.Push(SnapshotOlustur());
            _mevcutDurumId = snapshot.DurumId;
            SnapshotGeriYukle(snapshot);
            DurumMesaji = "Yinelendi.";
            return;
        }
        UndoDurumBildir();
        DurumMesaji = "Yinelenecek adım yok.";
    }

    /// Yeni dosya/aç sonrası yığınlar temizlenir (eski kat Id'leri geçersiz).
    public void UndoYiginlariniTemizle()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _mevcutDurumId = ++_sonrakiDurumId;
        _kayitliDurumId = _mevcutDurumId;
        IsDirty = false;
        UndoDurumBildir();
    }

    private GeoSnapshot SnapshotOlustur() => new(
        CloneGeo(_geo),
        _seciliKat?.Id,
        _seciliOdaId,
        _seciliAciklikId,
        _referansKat?.Id,
        _mevcutDurumId);

    /// Otomatik kurtarma için canlı modeli değiştirmeyen bağımsız bir kopya üretir.
    public BinaGeometri KurtarmaKopyasiOlustur() => CloneGeo(_geo);

    private void SnapshotGeriYukle(GeoSnapshot snapshot)
    {
        int revizyon = _geo.GeometriRevizyon;
        int semaSurum = _geo.SemaSurum;
        string? dosyaYolu = _geo.DosyaYolu;
        string? kaynakModul = _geo.KaynakModul;
        string? kaynakKimlik = _geo.KaynakArtifactKimligi;
        _geo = CloneGeo(snapshot.Geo);
        // Undo içeriği geri alır; persistence kimliği/revizyonu geri sarmaz.
        // Aksi halde save→undo→save aynı revizyonu yeniden üretip bağlı modüllerin
        // güncelleme algılamasını kaçırabilir.
        _geo.GeometriRevizyon = revizyon;
        _geo.SemaSurum = semaSurum;
        _geo.DosyaYolu = dosyaYolu;
        _geo.KaynakModul = kaynakModul;
        _geo.KaynakArtifactKimligi = kaynakKimlik;
        _seciliKat = null;
        _seciliOdaId = null;
        _seciliAciklikId = null;
        _referansKat = null;

        OnPropertyChanged(nameof(Geo));
        OnPropertyChanged(nameof(PencereBasligi));

        var kat = snapshot.SeciliKatId is { } katId
            ? _geo.Katlar.FirstOrDefault(k => k.Id == katId)
            : _geo.Katlar.FirstOrDefault();
        SeciliKat = kat;
        ReferansKat = snapshot.ReferansKatId is { } refId
            ? _geo.Katlar.FirstOrDefault(k => k.Id == refId)
            : null;
        SeciliOdaId = kat is not null && snapshot.SeciliOdaId is { } odaId && kat.Odalar.Any(o => o.Id == odaId)
            ? odaId : null;
        SeciliAciklikId = kat is not null && snapshot.SeciliAciklikId is { } acId && kat.Acikliklar.Any(a => a.Id == acId)
            ? acId : null;

        IsDirty = _mevcutDurumId != _kayitliDurumId;
        OnPropertyChanged(nameof(SeciliOdaBirlesik));
        OnPropertyChanged(nameof(SeciliOdaBilgi));
        UndoDurumBildir();
    }

    private void UndoDurumBildir()
    {
        OnPropertyChanged(nameof(GeriAlinabilir));
        OnPropertyChanged(nameof(IleriAlinabilir));
    }

    private static BinaGeometri CloneGeo(BinaGeometri g)
    {
        var clone = new BinaGeometri
        {
            SemaSurum = g.SemaSurum,
            GeometriRevizyon = g.GeometriRevizyon,
            BinaAdi = g.BinaAdi,
            DosyaYolu = g.DosyaYolu,
            KaynakModul = g.KaynakModul,
            KaynakArtifactKimligi = g.KaynakArtifactKimligi,
            EkAlanlar = CloneEkAlanlar(g.EkAlanlar),
            Katlar = new System.Collections.ObjectModel.ObservableCollection<BinaKat>(g.Katlar.Select(CloneKat)),
            DuseyBirlesimleri = g.DuseyBirlesimleri.Select(CloneBirlesim).ToList()
        };
        return clone;
    }

    private static BinaKat CloneKat(BinaKat k) => new()
    {
        Id = k.Id,
        Ad = k.Ad,
        KatNo = k.KatNo,
        KatYuksekligiM = k.KatYuksekligiM,
        DxfFilePath = k.DxfFilePath,
        UnitWarning = k.UnitWarning,
        DxfBirimOlcegiM = k.DxfBirimOlcegiM,
        DxfNormalizeOfsetiM = k.DxfNormalizeOfsetiM,
        DxfKaynakBolgesiMinM = k.DxfKaynakBolgesiMinM,
        DxfKaynakBolgesiMaxM = k.DxfKaynakBolgesiMaxM,
        OriginOffset = k.OriginOffset,
        DxfGoster = k.DxfGoster,
        BloklariGoster = k.BloklariGoster,
        EkAlanlar = CloneEkAlanlar(k.EkAlanlar),
        Odalar = new System.Collections.ObjectModel.ObservableCollection<BinaOda>(k.Odalar.Select(CloneOda)),
        Acikliklar = k.Acikliklar.Select(CloneAciklik).ToList(),
        // DXF varlıkları UI'da yerinde değiştirilmez; listeler snapshot'lar arasında
        // güvenle paylaşılarak 200k segmentli planlarda history belleği sınırlı tutulur.
        DxfSegmentler = k.DxfSegmentler,
        DxfArclar = k.DxfArclar,
        HamSegmentler = k.HamSegmentler,
        TespitSegmentler = k.TespitSegmentler,
        HamMetinler = k.HamMetinler
    };

    private static BinaOda CloneOda(BinaOda o) => new()
    {
        Id = o.Id,
        Ad = o.Ad,
        AlanM2 = o.AlanM2,
        YukseklikOverrideM = o.YukseklikOverrideM,
        EkAlanlar = CloneEkAlanlar(o.EkAlanlar),
        Poligon = o.Poligon.Select(p => new Point2D(p.X, p.Y)).ToList()
    };

    private static BinaAciklik CloneAciklik(BinaAciklik a) => new()
    {
        Id = a.Id,
        Bas = a.Bas,
        Bit = a.Bit,
        Tip = a.Tip,
        GenislikM = a.GenislikM,
        YukseklikM = a.YukseklikM,
        EsikYuksekligiM = a.EsikYuksekligiM,
        IptalEdildi = a.IptalEdildi,
        ManuelEklendi = a.ManuelEklendi,
        Kaynak = a.Kaynak,
        EkAlanlar = CloneEkAlanlar(a.EkAlanlar)
    };

    private static BinaDuseyBirlesim CloneBirlesim(BinaDuseyBirlesim b) => new()
    {
        Id = b.Id,
        AktifMi = b.AktifMi,
        UstKatNo = b.UstKatNo,
        AltKatNo = b.AltKatNo,
        EkAlanlar = CloneEkAlanlar(b.EkAlanlar),
        Uyeler = b.Uyeler.Select(u => new BinaBirlesimUyesi
        {
            KatId = u.KatId,
            KatNo = u.KatNo,
            OdaId = u.OdaId,
            TavanAcik = u.TavanAcik,
            DosemeAcik = u.DosemeAcik,
            EkAlanlar = CloneEkAlanlar(u.EkAlanlar)
        }).ToList(),
        GeriAlmaUstKatOdalari = b.GeriAlmaUstKatOdalari?.Select(CloneOda).ToList(),
        GeriAlmaAltKatOdalari = b.GeriAlmaAltKatOdalari?.Select(CloneOda).ToList()
    };

    private static System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>? CloneEkAlanlar(
        System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>? kaynak)
        => kaynak?.ToDictionary(kv => kv.Key, kv => kv.Value.Clone(), StringComparer.Ordinal);

    // ── Döşeme/tavan patlatma ─────────────────────────────────────────────────

    /// Seçili oda bir aktif düşey birleşime bağlı mı?
    public bool SeciliOdaBirlesik =>
        _seciliOdaId is { } id && BinaDuseyBirlesimServisi.BirlesimBul(Geo, id) is not null;

    public void TavanPatlat()
    {
        var kat = _seciliKat;
        var oda = SeciliOda;
        if (kat is null || oda is null) return;
        var snapshot = SnapshotOlustur();
        var sonuc = BinaDuseyBirlesimServisi.TavanPatlat(Geo, kat, oda);
        DurumMesaji = sonuc.Mesaj;
        if (!sonuc.Basarili) return;
        SnapshotKaydet(snapshot);
        if (sonuc.Birlesim is { } b)
            DurumMesaji += $"  (Birleşik hacim: {BinaDuseyBirlesimServisi.BirlesikHacimM3(Geo, b):N1} m³)";
        OnPropertyChanged(nameof(SeciliOdaBirlesik));
    }

    public void DosemePatlat()
    {
        var kat = _seciliKat;
        var oda = SeciliOda;
        if (kat is null || oda is null) return;
        var snapshot = SnapshotOlustur();
        var sonuc = BinaDuseyBirlesimServisi.DosemePatlat(Geo, kat, oda);
        DurumMesaji = sonuc.Mesaj;
        if (!sonuc.Basarili) return;
        SnapshotKaydet(snapshot);
        if (sonuc.Birlesim is { } b)
            DurumMesaji += $"  (Birleşik hacim: {BinaDuseyBirlesimServisi.BirlesikHacimM3(Geo, b):N1} m³)";
        OnPropertyChanged(nameof(SeciliOdaBirlesik));
    }

    public void PatlatmaGeriAl()
    {
        var kat = _seciliKat;
        if (kat is null || _seciliOdaId is not { } id) return;
        var snapshot = SnapshotOlustur();
        bool basarili = BinaDuseyBirlesimServisi.GeriAl(Geo, id, out string mesaj);
        DurumMesaji = mesaj;
        if (basarili) SnapshotKaydet(snapshot);
        OnPropertyChanged(nameof(SeciliOdaBirlesik));
    }

    // ── Açıklık yönetimi ──────────────────────────────────────────────────────

    private string? _seciliAciklikId;
    public string? SeciliAciklikId
    {
        get => _seciliAciklikId;
        set
        {
            if (_seciliAciklikId == value) return;
            _seciliAciklikId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SeciliAciklik));
            OnPropertyChanged(nameof(AciklikSecili));
        }
    }

    public BinaAciklik? SeciliAciklik =>
        _seciliKat?.Acikliklar.FirstOrDefault(a => a.Id == _seciliAciklikId);

    public bool AciklikSecili => SeciliAciklik is not null;

    /// Açıklıkları yeniden tarar. Manuel eklenen ve iptal edilenler korunur.
    public void AciklikTara()
    {
        var kat = _seciliKat;
        if (kat is null) return;

        // TespitSegmentler annotation katmanları ayıklanmış ve içe aktarımla aynı
        // ölçek/orijin dönüşümünden geçmiş TAM kümedir. JSON'a yazılmadığı için kayıt
        // yeniden açıldığında Core'daki metadata-aware overload aynı ölçek, normalize
        // ofseti ve (çoklu DXF'te) kaynak bölgesiyle kümeyi birebir yeniden üretir.
        System.Collections.Generic.IReadOnlyList<(Point2D, Point2D)> duvarlar;
        if (kat.TespitSegmentler.Count > 0)
        {
            duvarlar = kat.TespitSegmentler;
        }
        else if (!string.IsNullOrWhiteSpace(kat.DxfFilePath) && System.IO.File.Exists(kat.DxfFilePath))
        {
            try
            {
                duvarlar = new DxfKatPlaniParser().ExtractRawSegmentsFromFile(kat.DxfFilePath, kat);
                DurumMesaji = "DXF tespit segmentleri aynı ölçek ve yerel koordinatlarla yeniden okundu.";
            }
            catch (Exception ex)
            {
                if (kat.HamSegmentler.Count > 0)
                {
                    duvarlar = kat.HamSegmentler;
                    DurumMesaji = $"DXF yeniden okunamadı; bu oturumdaki tam segment kümesi kullanılıyor: {ex.Message}";
                }
                else
                {
                    DurumMesaji = $"Açıklık taranamadı: kaynak DXF okunamıyor ve tam tespit kümesi yok. DXF'i yeniden seçin. ({ex.Message})";
                    return;
                }
            }
        }
        else if (kat.HamSegmentler.Count > 0)
        {
            duvarlar = kat.HamSegmentler;
        }
        else
        {
            DurumMesaji = "Açıklık taranamadı: tam duvar kümesi yok. Kaynak DXF'i yeniden seçin.";
            return;
        }

        if (duvarlar.Count == 0)
        {
            DurumMesaji = "Açıklık taraması için DXF duvar segmenti yok.";
            return;
        }

        var arclar = kat.DxfArclar
            .Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg))
            .ToList();

        var bulunan = AciklikTespitServisi.Bul(duvarlar, arclar, kat.Acikliklar);
        UndoSnapshotAl();
        kat.Acikliklar.Clear();
        kat.Acikliklar.AddRange(bulunan);
        AciklikYardimci.DisindaKalanlariSil(kat);
        int yansima = AciklikYardimci.KomsuDuvarlariTamamla(kat);
        kat.SayilariYenile();
        SeciliAciklikId = null;

        int kapi = kat.Acikliklar.Count(a => !a.IptalEdildi && a.Tip == BinaAciklikTipi.Kapi);
        int pencere = kat.Acikliklar.Count(a => !a.IptalEdildi && a.Tip == BinaAciklikTipi.Pencere);
        DurumMesaji = yansima > 0
            ? $"Açıklık taraması: {kapi} kapı, {pencere} pencere bulundu; {yansima} komşu yüz eklendi."
            : $"Açıklık taraması: {kapi} kapı, {pencere} pencere bulundu.";
    }

    public void AciklikEkleManuel(Point2D bas, Point2D bit, BinaAciklikTipi tip)
    {
        var kat = _seciliKat;
        if (kat is null) return;
        if (!double.IsFinite(bas.X) || !double.IsFinite(bas.Y) || !double.IsFinite(bit.X) || !double.IsFinite(bit.Y))
        {
            DurumMesaji = "Açıklık koordinatları geçersiz.";
            return;
        }
        double genislik = System.Math.Sqrt(
            (bit.X - bas.X) * (bit.X - bas.X) + (bit.Y - bas.Y) * (bit.Y - bas.Y));
        if (genislik < 0.1)
        {
            DurumMesaji = "Açıklık çok kısa — en az 10 cm olmalı.";
            return;
        }
        var aday = new BinaAciklik { Bas = bas, Bit = bit, GenislikM = genislik, Tip = tip };
        if (!AciklikBirOdaDuvarindaMi(kat, aday))
        {
            DurumMesaji = "Açıklık bir oda duvarı üzerinde olmalı.";
            return;
        }
        UndoSnapshotAl();
        var aciklik = new BinaAciklik
        {
            Bas = bas, Bit = bit, Tip = tip,
            GenislikM = genislik,
            YukseklikM = BinaAciklik.VarsayilanYukseklik(tip),
            ManuelEklendi = true,
            Kaynak = "manuel"
        };
        kat.Acikliklar.Add(aciklik);
        var yansima = AciklikYardimci.KomsuDuvaraYansit(kat, aciklik);
        kat.SayilariYenile();
        SeciliAciklikId = aciklik.Id;
        string tipAd = tip == BinaAciklikTipi.Kapi ? "Kapı" : "Pencere";
        DurumMesaji = yansima is not null
            ? $"{tipAd} eklendi ({genislik:F2} m); komşu odada da oluşturuldu."
            : $"{tipAd} eklendi ({genislik:F2} m).";
    }

    public void SeciliAciklikSil()
    {
        var kat = _seciliKat;
        var ac = SeciliAciklik;
        if (kat is null || ac is null) return;
        var silinecekler = AyniFizikselAcikliklariBul(kat, ac);
        UndoSnapshotAl();
        foreach (var silinecek in silinecekler) kat.Acikliklar.Remove(silinecek);
        int komsuSilinen = System.Math.Max(0, silinecekler.Count - 1);
        kat.SayilariYenile();
        SeciliAciklikId = null;
        DurumMesaji = komsuSilinen > 0
            ? "Açıklık ve komşu odadaki karşılığı silindi."
            : "Açıklık silindi.";
    }

    /// "Açıklık değil" işareti — yeniden taramada korunur, çizimde gri görünür.
    public void SeciliAciklikIptalToggle()
    {
        var ac = SeciliAciklik;
        var kat = _seciliKat;
        if (ac is null || kat is null) return;
        bool yeni = !ac.IptalEdildi;
        UndoSnapshotAl();
        foreach (var es in AyniFizikselAcikliklariBul(kat, ac)) es.IptalEdildi = yeni;
        OnPropertyChanged(nameof(SeciliAciklik));
        DurumMesaji = yeni ? "Açıklık iptal edildi (açıklık değil)." : "Açıklık yeniden etkin.";
    }

    public bool SeciliAciklikTipGuncelle(BinaAciklikTipi tip)
    {
        var ac = SeciliAciklik;
        var kat = _seciliKat;
        if (ac is null || kat is null || ac.Tip == tip) return false;
        var esler = AyniFizikselAcikliklariBul(kat, ac);
        UndoSnapshotAl();
        foreach (var es in esler) es.Tip = tip;
        OnPropertyChanged(nameof(SeciliAciklik));
        DurumMesaji = tip == BinaAciklikTipi.Kapi ? "Açıklık kapı olarak güncellendi." : "Açıklık pencere olarak güncellendi.";
        return true;
    }

    public bool SeciliAciklikYukseklikGuncelle(double yukseklikM)
    {
        var ac = SeciliAciklik;
        var kat = _seciliKat;
        if (ac is null || kat is null) return false;
        if (!double.IsFinite(yukseklikM) || yukseklikM <= 0 ||
            ac.EtkinEsikYuksekligiM + yukseklikM > kat.KatYuksekligiM + 1e-9)
        {
            DurumMesaji = $"Açıklık yüksekliği sonlu, pozitif ve kat yüksekliği ({kat.KatYuksekligiM:F2} m) içinde olmalı.";
            return false;
        }
        if (System.Math.Abs(ac.YukseklikM - yukseklikM) < 1e-9) return false;
        var esler = AyniFizikselAcikliklariBul(kat, ac);
        UndoSnapshotAl();
        foreach (var es in esler) es.YukseklikM = yukseklikM;
        OnPropertyChanged(nameof(SeciliAciklik));
        return true;
    }

    public bool SeciliAciklikEsikGuncelle(double esikM)
    {
        var ac = SeciliAciklik;
        var kat = _seciliKat;
        if (ac is null || kat is null) return false;
        if (!double.IsFinite(esikM) || esikM < 0 || esikM + ac.YukseklikM > kat.KatYuksekligiM + 1e-9)
        {
            DurumMesaji = $"Eşik + açıklık yüksekliği kat yüksekliğini ({kat.KatYuksekligiM:F2} m) aşamaz.";
            return false;
        }
        if (System.Math.Abs(ac.EsikYuksekligiM - esikM) < 1e-9) return false;
        var esler = AyniFizikselAcikliklariBul(kat, ac);
        UndoSnapshotAl();
        foreach (var es in esler) es.EsikYuksekligiM = esikM;
        OnPropertyChanged(nameof(SeciliAciklik));
        return true;
    }

    private static System.Collections.Generic.List<BinaAciklik> AyniFizikselAcikliklariBul(BinaKat kat, BinaAciklik kaynak)
    {
        var sonuc = new System.Collections.Generic.List<BinaAciklik> { kaynak };
        string esId = kaynak.Id.EndsWith("-yansima", StringComparison.Ordinal)
            ? kaynak.Id[..^"-yansima".Length]
            : kaynak.Id + "-yansima";
        var kimlikEsi = kat.Acikliklar.FirstOrDefault(a =>
            !ReferenceEquals(a, kaynak) && string.Equals(a.Id, esId, StringComparison.Ordinal));
        if (kimlikEsi is not null)
        {
            sonuc.Add(kimlikEsi);
            return sonuc;
        }

        // Sema 3 öncesinde yansıma Id'si kararlı değildi. Yalnız Kaynak işaretinin
        // bir tarafı yansıma dediği legacy çiftlerde sıkı geometri fallback'i uygula;
        // yakındaki bağımsız açıklıkları birlikte düzenlememek için normal açıklıklar
        // birbirleriyle salt yakınlığa göre asla eşleştirilmez.
        bool kaynakYansima = YansimaKaynagiMi(kaynak);
        double kdx = kaynak.Bit.X - kaynak.Bas.X, kdy = kaynak.Bit.Y - kaynak.Bas.Y;
        double kuz = System.Math.Sqrt(kdx * kdx + kdy * kdy);
        if (kuz < 1e-9) return sonuc;
        var km = new Point2D((kaynak.Bas.X + kaynak.Bit.X) * 0.5, (kaynak.Bas.Y + kaynak.Bit.Y) * 0.5);

        foreach (var aday in kat.Acikliklar)
        {
            if (ReferenceEquals(aday, kaynak)) continue;
            if (kaynakYansima == YansimaKaynagiMi(aday)) continue;
            double adx = aday.Bit.X - aday.Bas.X, ady = aday.Bit.Y - aday.Bas.Y;
            double auz = System.Math.Sqrt(adx * adx + ady * ady);
            if (auz < 1e-9 || System.Math.Abs(kuz - auz) > System.Math.Max(0.03, kuz * 0.05)) continue;
            if (System.Math.Abs((kdx * ady - kdy * adx) / (kuz * auz)) > 0.08) continue;
            var am = new Point2D((aday.Bas.X + aday.Bit.X) * 0.5, (aday.Bas.Y + aday.Bit.Y) * 0.5);
            double mesafe = Distance(km, am);
            if (mesafe < 0.03 || mesafe > 0.80) continue;
            double eksenSapma = System.Math.Abs((am.X - km.X) * kdx / kuz + (am.Y - km.Y) * kdy / kuz);
            if (eksenSapma <= System.Math.Max(0.05, kuz * 0.05))
            {
                sonuc.Add(aday);
                break;
            }
        }
        return sonuc;
    }

    private static bool YansimaKaynagiMi(BinaAciklik aciklik)
        => aciklik.Kaynak?.Contains("-yansima", StringComparison.OrdinalIgnoreCase) == true;

    private static void BagliAcikliklariDonustur(
        BinaKat kat, Point2D eskiA, Point2D eskiB, Point2D yeniA, Point2D yeniB)
    {
        double ex = eskiB.X - eskiA.X, ey = eskiB.Y - eskiA.Y;
        double nx = yeniB.X - yeniA.X, ny = yeniB.Y - yeniA.Y;
        double eskiLen2 = ex * ex + ey * ey;
        double yeniLen = System.Math.Sqrt(nx * nx + ny * ny);
        if (eskiLen2 < 1e-12 || yeniLen < 1e-9) return;
        double eskiLen = System.Math.Sqrt(eskiLen2);

        // Önce eski kenarın doğrudan taşıdığı açıklıkları bul; sonra bunların karşı
        // duvardaki fiziksel eşlerini de kümeye kat. Eşler 12 cm hit toleransının
        // dışında olabilir (tipik duvar kalınlığı 20 cm).
        var dogrudan = kat.Acikliklar.Where(ac =>
        {
            var orta = new Point2D((ac.Bas.X + ac.Bit.X) * 0.5, (ac.Bas.Y + ac.Bit.Y) * 0.5);
            if (NoktaSegmentMesafesi(orta, eskiA, eskiB) > 0.12) return false;
            double ax = ac.Bit.X - ac.Bas.X, ay = ac.Bit.Y - ac.Bas.Y;
            double alen = System.Math.Sqrt(ax * ax + ay * ay);
            return alen >= 1e-9 &&
                   System.Math.Abs((ax * ey - ay * ex) / (alen * eskiLen)) <= 0.20;
        }).ToList();
        if (dogrudan.Count == 0) return;

        var tasinacak = new System.Collections.Generic.HashSet<BinaAciklik>();
        foreach (var ac in dogrudan)
            foreach (var es in AyniFizikselAcikliklariBul(kat, ac))
                tasinacak.Add(es);

        double eskiTx = ex / eskiLen, eskiTy = ey / eskiLen;
        double eskiNx = -eskiTy, eskiNy = eskiTx;
        double yeniNx = -ny / yeniLen, yeniNy = nx / yeniLen;

        foreach (var ac in tasinacak)
        {
            Point2D Donustur(Point2D p)
            {
                double rx = p.X - eskiA.X, ry = p.Y - eskiA.Y;
                double t = (rx * ex + ry * ey) / eskiLen2;
                double normalOfset = rx * eskiNx + ry * eskiNy;
                return new Point2D(
                    yeniA.X + t * nx + normalOfset * yeniNx,
                    yeniA.Y + t * ny + normalOfset * yeniNy);
            }

            ac.Bas = Donustur(ac.Bas);
            ac.Bit = Donustur(ac.Bit);
            ac.GenislikM = Distance(ac.Bas, ac.Bit);
        }
    }

    private static int YetimAcikliklariSil(BinaKat kat)
    {
        var silinecek = kat.Acikliklar.Where(a => !AciklikBirOdaDuvarindaMi(kat, a)).ToList();
        foreach (var ac in silinecek) kat.Acikliklar.Remove(ac);
        return silinecek.Count;
    }

    private static bool AciklikBirOdaDuvarindaMi(BinaKat kat, BinaAciklik ac)
    {
        var orta = new Point2D((ac.Bas.X + ac.Bit.X) * 0.5, (ac.Bas.Y + ac.Bit.Y) * 0.5);
        double ax = ac.Bit.X - ac.Bas.X, ay = ac.Bit.Y - ac.Bas.Y;
        double alen = System.Math.Sqrt(ax * ax + ay * ay);
        if (alen < 1e-9) return false;
        foreach (var oda in kat.Odalar)
        {
            for (int i = 0; i < oda.Poligon.Count; i++)
            {
                var a = oda.Poligon[i];
                var b = oda.Poligon[(i + 1) % oda.Poligon.Count];
                double ex = b.X - a.X, ey = b.Y - a.Y;
                double elen = System.Math.Sqrt(ex * ex + ey * ey);
                if (elen < 1e-9) continue;
                if (System.Math.Abs((ax * ey - ay * ex) / (alen * elen)) > 0.20) continue;
                if (NoktaSegmentMesafesi(orta, a, b) <= 0.18) return true;
            }
        }
        return false;
    }

    private static double NoktaSegmentMesafesi(Point2D p, Point2D a, Point2D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-12) return Distance(p, a);
        double t = System.Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
        return Distance(p, new Point2D(a.X + t * dx, a.Y + t * dy));
    }

    private static double Distance(Point2D a, Point2D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return System.Math.Sqrt(dx * dx + dy * dy);
    }

    private static double AlanHesapla(System.Collections.Generic.List<Point2D> poly)
    {
        if (poly.Count < 3) return 0;
        double s = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            s += a.X * b.Y - b.X * a.Y;
        }
        return System.Math.Abs(s) * 0.5;
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
