using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.App.Bina.ViewModels;

public class BinaGeometriViewModel : INotifyPropertyChanged
{
    // ── Model ────────────────────────────────────────────────────────────────

    private BinaGeometri _geo = BinaGeometriServisi.YeniOlustur();
    public BinaGeometri Geo
    {
        get => _geo;
        set
        {
            if (_geo == value) return;
            _geo = value;
            IsDirty = false; // yeni/açılan geometri temiz başlar
            OnPropertyChanged();
            OnPropertyChanged(nameof(PencereBasligi));
            SeciliKat = value.Katlar.Count > 0 ? value.Katlar[0] : null;
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

    /// Geometriyi değiştiren her yolun çağırdığı işaret.
    public void Kirlet() => IsDirty = true;

    /// Kaydet / yeni / aç sonrası temiz işaretle.
    public void TemizIsaretle() => IsDirty = false;

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
            SeciliOdaId = null;
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

    // ── Seçili kat proxy properties (OriginOffset struct olduğundan proxy şart) ─

    private bool _katOfsetSyncing;

    private double _katOfsetX;
    public double KatOfsetX
    {
        get => _katOfsetX;
        set
        {
            if (System.Math.Abs(_katOfsetX - value) < 1e-9) return;
            _katOfsetX = value;
            OnPropertyChanged();
            if (!_katOfsetSyncing && _seciliKat is { } kat)
            {
                kat.OriginOffset = new Point2D(value, kat.OriginOffset.Y);
                IsDirty = true;
            }
        }
    }

    private double _katOfsetY;
    public double KatOfsetY
    {
        get => _katOfsetY;
        set
        {
            if (System.Math.Abs(_katOfsetY - value) < 1e-9) return;
            _katOfsetY = value;
            OnPropertyChanged();
            if (!_katOfsetSyncing && _seciliKat is { } kat)
            {
                kat.OriginOffset = new Point2D(kat.OriginOffset.X, value);
                IsDirty = true;
            }
        }
    }

    // ── Kat yönetimi komutları ────────────────────────────────────────────────

    public void KatSil()
    {
        var kat = _seciliKat;
        if (kat is null) return;
        int idx = Geo.Katlar.IndexOf(kat);

        // Silinen katın odalarına bağlı düşey birleşimleri kaldır — aksi halde
        // partner oda hayalet açık tavan/döşeme raporlar (askıda birleşim).
        int kaldirilanBirlesim = 0;
        foreach (var oda in kat.Odalar)
            kaldirilanBirlesim += BinaDuseyBirlesimServisi.OdaSilindi(Geo, oda.Id);

        Geo.Katlar.Remove(kat);
        KatlariYenidenNumaralandir();
        IsDirty = true;
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
        Geo.Katlar.Move(idx, idx - 1);
        KatlariYenidenNumaralandir();
        IsDirty = true;
    }

    public void KatAsagi()
    {
        var kat = _seciliKat;
        if (kat is null) return;
        int idx = Geo.Katlar.IndexOf(kat);
        if (idx >= Geo.Katlar.Count - 1) return;
        Geo.Katlar.Move(idx, idx + 1);
        KatlariYenidenNumaralandir();
        IsDirty = true;
    }

    public void KatlariHizala()
    {
        foreach (var k in Geo.Katlar)
            k.OriginOffset = new Point2D(0, 0);
        SyncKatProperties();
        IsDirty = true;
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
        kat.SayilariYenile();
        // Askıda birleşim bırakma (Id kararlılığı — silinen odanın birleşimleri düşer)
        int kaldirilanBirlesim = BinaDuseyBirlesimServisi.OdaSilindi(Geo, oda.Id);
        SeciliOdaId = null;
        DurumMesaji = kaldirilanBirlesim > 0
            ? $"'{oda.Ad}' silindi ({kaldirilanBirlesim} düşey birleşim kaldırıldı)."
            : $"'{oda.Ad}' silindi.";
    }

    public void VertexGuncelle(int index, double x, double y)
    {
        var oda = SeciliOda;
        if (oda is null || index < 0 || index >= oda.Poligon.Count) return;
        oda.Poligon[index] = new Point2D(x, y);
        oda.AlanM2 = AlanHesapla(oda.Poligon);
        OnPropertyChanged(nameof(SeciliOdaBilgi));
    }

    public void VertexSil(int index)
    {
        var oda = SeciliOda;
        if (oda is null || oda.Poligon.Count <= 3) return; // üçgenden küçülemez
        if (index < 0 || index >= oda.Poligon.Count) return;
        UndoSnapshotAl();
        oda.Poligon.RemoveAt(index);
        oda.AlanM2 = AlanHesapla(oda.Poligon);
        OnPropertyChanged(nameof(SeciliOdaBilgi));
        DurumMesaji = $"'{oda.Ad}' köşesi silindi ({oda.Poligon.Count} köşe kaldı).";
    }

    public void VertexEkleSonra(int index, double x, double y)
    {
        var oda = SeciliOda;
        if (oda is null || index < 0 || index >= oda.Poligon.Count) return;
        UndoSnapshotAl();
        oda.Poligon.Insert(index + 1, new Point2D(x, y));
        oda.AlanM2 = AlanHesapla(oda.Poligon);
        OnPropertyChanged(nameof(SeciliOdaBilgi));
        DurumMesaji = $"'{oda.Ad}' kenarına köşe eklendi ({oda.Poligon.Count} köşe).";
    }

    // ── Undo / Redo (M12 snapshot deseni — oda + açıklık + birleşim kapsanır) ──

    private sealed record GeoSnapshot(
        string KatId,
        string? SeciliOdaId,
        System.Collections.Generic.List<BinaOda> Odalar,
        System.Collections.Generic.List<BinaAciklik> Acikliklar,
        System.Collections.Generic.List<BinaDuseyBirlesim> Birlesimler);

    private readonly System.Collections.Generic.Stack<GeoSnapshot> _undoStack = new();
    private readonly System.Collections.Generic.Stack<GeoSnapshot> _redoStack = new();
    private const int MaxUndo = 20;

    public bool GeriAlinabilir  => _undoStack.Count > 0;
    public bool IleriAlinabilir => _redoStack.Count > 0;

    /// Geometriyi değiştiren her komutun başında çağrılır.
    public void UndoSnapshotAl()
    {
        var kat = _seciliKat;
        if (kat is null) return;
        SnapshotKaydet(SnapshotOlustur(kat));
    }

    /// Önceden alınmış bir snapshot'ı yığına işler. Yalnız İŞLEM BAŞARILI olduğunda
    /// çağrılmalı (aksi halde başarısız komutlar boş undo adımı + sahte IsDirty üretir).
    private void SnapshotKaydet(GeoSnapshot snapshot)
    {
        IsDirty = true; // her geri-alınabilir düzenleme geometriyi kirletir
        _undoStack.Push(snapshot);
        _redoStack.Clear();
        if (_undoStack.Count > MaxUndo)
        {
            var liste = _undoStack.ToList();
            _undoStack.Clear();
            foreach (var s in System.Linq.Enumerable.Reverse(liste.Take(MaxUndo)))
                _undoStack.Push(s);
        }
        UndoDurumBildir();
    }

    public void GeriAl()
    {
        // Aradaki adımlardan biri silinmiş bir kata aitse (ör. UndoSnapshotAl sonrası
        // kat silindi) o adımı sessizce yutmak yerine atlayıp bir öncekini dener.
        while (_undoStack.Count > 0)
        {
            var snapshot = _undoStack.Pop();
            var kat = Geo.Katlar.FirstOrDefault(k => k.Id == snapshot.KatId);
            if (kat is null) continue;
            _redoStack.Push(SnapshotOlustur(kat));
            SnapshotGeriYukle(kat, snapshot);
            DurumMesaji = "Geri alındı.";
            return;
        }
        UndoDurumBildir();
        DurumMesaji = "Geri alınacak adım yok (ilgili kat silinmiş olabilir).";
    }

    public void IleriAl()
    {
        while (_redoStack.Count > 0)
        {
            var snapshot = _redoStack.Pop();
            var kat = Geo.Katlar.FirstOrDefault(k => k.Id == snapshot.KatId);
            if (kat is null) continue;
            _undoStack.Push(SnapshotOlustur(kat));
            SnapshotGeriYukle(kat, snapshot);
            DurumMesaji = "Yinelendi.";
            return;
        }
        UndoDurumBildir();
        DurumMesaji = "Yinelenecek adım yok (ilgili kat silinmiş olabilir).";
    }

    /// Yeni dosya/aç sonrası yığınlar temizlenir (eski kat Id'leri geçersiz).
    public void UndoYiginlariniTemizle()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UndoDurumBildir();
    }

    private GeoSnapshot SnapshotOlustur(BinaKat kat) => new(
        kat.Id,
        _seciliOdaId,
        kat.Odalar.Select(CloneOda).ToList(),
        kat.Acikliklar.Select(CloneAciklik).ToList(),
        Geo.DuseyBirlesimleri.Select(CloneBirlesim).ToList());

    private void SnapshotGeriYukle(BinaKat kat, GeoSnapshot snapshot)
    {
        kat.Odalar.Clear();
        foreach (var o in snapshot.Odalar) kat.Odalar.Add(o);
        kat.Acikliklar.Clear();
        kat.Acikliklar.AddRange(snapshot.Acikliklar);
        kat.SayilariYenile();
        Geo.DuseyBirlesimleri.Clear();
        Geo.DuseyBirlesimleri.AddRange(snapshot.Birlesimler);

        if (_seciliKat != kat) SeciliKat = kat;
        SeciliOdaId = snapshot.SeciliOdaId is { } id && kat.Odalar.Any(o => o.Id == id) ? id : null;
        SeciliAciklikId = null;
        OnPropertyChanged(nameof(SeciliOdaBirlesik));
        OnPropertyChanged(nameof(SeciliOdaBilgi));
        UndoDurumBildir();
    }

    private void UndoDurumBildir()
    {
        OnPropertyChanged(nameof(GeriAlinabilir));
        OnPropertyChanged(nameof(IleriAlinabilir));
    }

    private static BinaOda CloneOda(BinaOda o) => new()
    {
        Id = o.Id,
        Ad = o.Ad,
        AlanM2 = o.AlanM2,
        YukseklikOverrideM = o.YukseklikOverrideM,
        TabanKotuOverrideM = o.TabanKotuOverrideM,
        KaynakMahalAdlari = o.KaynakMahalAdlari.ToList(),
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
        EsikYuksekligiM = a.EsikYuksekligiM, EsikYuksekligiBelirtildi = a.EsikYuksekligiBelirtildi,
        IptalEdildi = a.IptalEdildi,
        ManuelEklendi = a.ManuelEklendi,
        Kaynak = a.Kaynak
    };

    private static BinaDuseyBirlesim CloneBirlesim(BinaDuseyBirlesim b) => new()
    {
        Id = b.Id,
        AktifMi = b.AktifMi,
        UstKatNo = b.UstKatNo,
        AltKatNo = b.AltKatNo,
        Uyeler = b.Uyeler.Select(u => new BinaBirlesimUyesi
        {
            KatId = u.KatId,
            KatNo = u.KatNo,
            OdaId = u.OdaId,
            TavanAcik = u.TavanAcik,
            DosemeAcik = u.DosemeAcik
        }).ToList()
    };

    // ── Döşeme/tavan patlatma ─────────────────────────────────────────────────

    /// Seçili oda bir aktif düşey birleşime bağlı mı?
    public bool SeciliOdaBirlesik =>
        _seciliOdaId is { } id && BinaDuseyBirlesimServisi.BirlesimBul(Geo, id) is not null;

    public void TavanPatlat()
    {
        var kat = _seciliKat;
        var oda = SeciliOda;
        if (kat is null || oda is null) return;
        var snapshot = SnapshotOlustur(kat);
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
        var snapshot = SnapshotOlustur(kat);
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
        var snapshot = SnapshotOlustur(kat);
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

        // Ham segmentler yalnız DXF yüklemesinden hemen sonra bellekte olur ([JsonIgnore]);
        // kayıtlı `.e4bina` açıldıktan sonra boştur. DXF dosyası hâlâ erişilebilirse tam
        // ham segment setini oradan yeniden okuruz (codex50 §5: tespit TAM segment setinde
        // koşmalı); yoksa son çare olarak sadeleştirilmiş segmentlere düşülür.
        System.Collections.Generic.IReadOnlyList<(Point2D, Point2D)> duvarlar;
        if (kat.KaynakKatmanlari is not null)
        {
            // Katman seçimi üretim sınırıdır; kaynak dosyayı filtresiz yeniden
            // okumak hem kapalı katmanları hem diğer katların çizgilerini getirir.
            duvarlar = kat.KayitliTespitSegmentleri is { } kayitli
                ? kayitli.Select(s => (s.A, s.B)).ToList()
                : kat.HamTespitSegmentler;
        }
        else if (kat.HamSegmentler.Count > 0)
        {
            duvarlar = kat.HamSegmentler;
        }
        else if (!string.IsNullOrWhiteSpace(kat.DxfFilePath) && System.IO.File.Exists(kat.DxfFilePath))
        {
            try
            {
                duvarlar = new DxfKatPlaniParser().ExtractRawSegmentsFromFile(kat.DxfFilePath);
                DurumMesaji = "Ham DXF segmentleri dosyadan yeniden okundu.";
            }
            catch
            {
                duvarlar = kat.DxfSegmentler.Select(s => (s.A, s.B)).ToList();
            }
        }
        else
        {
            duvarlar = kat.DxfSegmentler.Select(s => (s.A, s.B)).ToList();
        }

        if (duvarlar.Count == 0)
        {
            DurumMesaji = "Açıklık taraması için DXF duvar segmenti yok.";
            return;
        }

        var arclar = kat.DxfArclar
            .Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg))
            .ToList();

        UndoSnapshotAl();
        var oncekiAcikliklar = kat.Acikliklar.ToList();
        var bulunan = AciklikTespitServisi.Bul(duvarlar, arclar, oncekiAcikliklar);
        kat.Acikliklar.Clear();
        kat.Acikliklar.AddRange(bulunan);
        AciklikYardimci.DisindaKalanlariSil(kat);
        int yansima = AciklikYardimci.KomsuDuvarlariTamamla(kat);
        AciklikTespitServisi.DegismeyenAciklikBilgileriniKoru(kat.Acikliklar, oncekiAcikliklar);
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
        double genislik = System.Math.Sqrt(
            (bit.X - bas.X) * (bit.X - bas.X) + (bit.Y - bas.Y) * (bit.Y - bas.Y));
        if (genislik < 0.1)
        {
            DurumMesaji = "Açıklık çok kısa — en az 10 cm olmalı.";
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
        UndoSnapshotAl();
        int komsuSilinen = AciklikYardimci.KomsuAcikliklariSil(kat, ac);
        kat.Acikliklar.Remove(ac);
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
        if (ac is null) return;
        UndoSnapshotAl();
        ac.IptalEdildi = !ac.IptalEdildi;
        OnPropertyChanged(nameof(SeciliAciklik));
        DurumMesaji = ac.IptalEdildi ? "Açıklık iptal edildi (açıklık değil)." : "Açıklık yeniden etkin.";
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
