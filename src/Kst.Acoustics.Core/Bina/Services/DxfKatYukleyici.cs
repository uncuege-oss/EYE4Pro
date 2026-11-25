using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Bina.Models;

namespace Kst.Acoustics.Core.Bina.Services;

/// DXF'ten tek BinaKat yükleme hattı: parse + flood-fill + açıklık tespiti + sadeleştirme.
/// M50 ve gelecek modüller için birincil giriş noktası.
public class DxfKatYukleyici
{
    public sealed class Ayarlar
    {
        /// Tüm koordinatları bounding-box min köşesine normalize eder. Çok katlı
        /// CAD dosyalarında ortak orijin korunacaksa false yapın.
        public bool NormalizeOrigin { get; set; } = true;

        /// Ham segmentleri 5 cm altı eleyerek 200k tavana sadeleştirir (görüntü/snap).
        public bool SegmentSadelestir { get; set; } = true;

        /// Açıklık (kapı/pencere) tespiti ve poligon-dışı temizleme çalıştırılır.
        public bool AciklikTespit { get; set; } = true;

        /// Açıklıkları komşu oda duvarına tamamla (kapı iki odaya hizmet eder).
        public bool KomsuDuvarlariTamamla { get; set; } = true;

        /// Unitless DXF'ler için başlangıç ölçek tahmini (m/birim).
        public double UnitlessScaleMeters { get; set; } = 0.001;
    }

    public string? UnitWarning { get; private set; }
    private BinaKat? _hamKat;

    public BinaKat Yukle(
        string filePath,
        int katNo,
        string katAdi,
        double katYuksekligiM,
        Ayarlar? ayar = null)
    {
        ayar ??= new Ayarlar();

        var parser = new DxfKatPlaniParser { UnitlessScaleMeters = ayar.UnitlessScaleMeters };
        var kat = parser.Parse(filePath, katNo, katYuksekligiM, ayar.NormalizeOrigin);
        kat.Ad = string.IsNullOrWhiteSpace(katAdi) ? $"Kat {katNo}" : katAdi.Trim();
        UnitWarning = parser.UnitWarning;

        if (!ayar.AciklikTespit)
        {
            kat.Acikliklar.Clear();
        }
        else
        {
            AciklikYardimci.DisindaKalanlariSil(kat);
            if (ayar.KomsuDuvarlariTamamla)
                AciklikYardimci.KomsuDuvarlariTamamla(kat);
        }

        if (!ayar.SegmentSadelestir)
        {
            kat.DxfSegmentler = kat.HamSegmentler
                .Select(s => new BinaDxfSegment { A = s.A, B = s.B })
                .ToList();
        }

        return kat;
    }

    // ── Çoklu kat tek DXF (yan yana planlar) — codex50 §9 ──────────────────────

    /// Bir dağıtım adayı: bir mekânsal küme + tahmini ad/sıra/dahil bilgisi.
    public sealed record CokluKatAdayi(
        KatKumelemeServisi.KatKumesi Kume,
        string OnerilenAd,
        double SiraDegeri,
        bool EtiketBulundu,
        bool OneriliDahil)
    {
        public MimariProjeMetinServisi.YukseklikSonucu? Olculer { get; init; }
    }

    public sealed record CokluKatTespitSonucu(
        IReadOnlyList<CokluKatAdayi> Adaylar,
        string? UnitWarning);

    /// Dosyada birden fazla yan yana kat planı olup olmadığını tespit eder. Tek plan
    /// varsa (veya kümeleme ayrışmazsa) null döner — çağıran normal tek-kat `Yukle`
    /// akışına düşmelidir. Ölçek/birim tespiti TÜM dosya için TEK seferde koşar
    /// (`DxfKatPlaniParser.Parse`'ın zaten kanıtlanmış alternatif-ölçek mantığı);
    /// kümeleme bu ortak ölçekte, dünya koordinatında (normalizeOrigin:false) yapılır.
    public CokluKatTespitSonucu? CokluKatTespitEt(
        string filePath,
        Ayarlar? ayar = null,
        KatKumelemeServisi.Ayarlar? kumeAyar = null)
    {
        ayar ??= new Ayarlar();
        var parser = new DxfKatPlaniParser { UnitlessScaleMeters = ayar.UnitlessScaleMeters };
        var birlesikKat = parser.HamVeriyiOku(filePath);
        return CokluKatTespitEt(birlesikKat, kumeAyar);
    }

    private CokluKatTespitSonucu? CokluKatTespitEt(BinaKat birlesikKat, KatKumelemeServisi.Ayarlar? kumeAyar = null)
    {
        _hamKat = birlesikKat;
        UnitWarning = birlesikKat.UnitWarning;

        var arcTuple = birlesikKat.DxfArclar
            .Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg))
            .ToList();
        var kumeler = KatKumelemeServisi.Kumele(
            birlesikKat.HamSegmentler, birlesikKat.HamMetinler, arcTuple, kumeAyar);
        if (kumeler.Count <= 1) return null;

        var (planlar, planDegil) = KatKumelemeServisi.PlanFiltrele(kumeler, kumeAyar);
        var planSet = new HashSet<KatKumelemeServisi.KatKumesi>(planlar);
        var hepsi = planlar.Concat(planDegil).OrderBy(k => k.MinY).ThenBy(k => k.MinX).ToList();

        var adaylar = hepsi.Select(k =>
        {
            var tahmin = KatEtiketServisi.TahminEt(k);
            return new CokluKatAdayi(k, tahmin.OnerilenAd, tahmin.SiraDegeri, tahmin.EtiketBulundu, planSet.Contains(k))
            { Olculer = MimariProjeMetinServisi.YukseklikOku(k.Metinler) };
        }).ToList();

        return new CokluKatTespitSonucu(MimariKatYukseklikServisi.Cozumle(adaylar), UnitWarning);
    }

    /// Onaylanan bir adaydan `BinaKat` üretir: kümenin kendi çerçevesine (bbox-min)
    /// normalize edilir, oda/açıklık tespiti + segment sadeleştirme tek-kat akışıyla
    /// AYNI servisler üzerinden çalışır — `CokluKatTespitEt`'ten SONRA, kullanıcı
    /// onayladığı her aday için ayrı ayrı çağrılır.
    public BinaKat CokluKatUret(
        CokluKatAdayi aday, int katNo, string katAdi, double katYuksekligiM, Ayarlar? ayar = null)
    {
        ayar ??= new Ayarlar();
        var kume = aday.Kume;

        var kat = new BinaKat
        {
            KatNo = katNo,
            Ad = string.IsNullOrWhiteSpace(katAdi) ? aday.OnerilenAd : katAdi.Trim(),
            KatYuksekligiM = katYuksekligiM,
            TabanKotuM = aday.Olculer?.TabanKotuM,
            DxfFilePath = _hamKat?.DxfFilePath ?? "",
            KaynakKatmanlari = _hamKat?.KaynakKatmanlari?.ToList(),
            HamSegmentler = kume.Segmentler.ToList(),
            HamMetinler = kume.Metinler.ToList(),
            DxfArclar = kume.Arclar
                .Select(a => new BinaDxfArc { Merkez = a.Merkez, R = a.R, BasAciDeg = a.BasDeg, BitAciDeg = a.BitDeg })
                .ToList()
        };

        if (_hamKat is not null)
        {
            // Yalnız kümeye gerçekten atanmış segmentler: pafta çerçevesi ve diğer
            // katların çizgileri sınır tespitine geri sokulmaz.
            var allowed = new HashSet<(Kst.Acoustics.Core.Common.Geometry.Point2D, Kst.Acoustics.Core.Common.Geometry.Point2D)>(kume.Segmentler);
            kat.HamTespitSegmentler = _hamKat.HamTespitSegmentler.Where(s => allowed.Contains(s)).ToList();
        }

        DxfKatPlaniParser.OdalariBosluklardanYenidenTara(kat);

        if (!ayar.AciklikTespit)
        {
            kat.Acikliklar.Clear();
        }
        else
        {
            var arcTuple = kat.DxfArclar.Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg)).ToList();
            kat.Acikliklar = AciklikTespitServisi.Bul(TespitSegmentleri(kat), arcTuple, Array.Empty<BinaAciklik>());
            AciklikYardimci.DisindaKalanlariSil(kat);
            if (ayar.KomsuDuvarlariTamamla)
                AciklikYardimci.KomsuDuvarlariTamamla(kat);
        }

        // Küme dünya koordinatındadır — her kat kendi yerel çerçevesine (bbox-min)
        // normalize edilir; kaba hizalama sonradan OriginOffset ile yapılır (§9.5).
        DxfKatPlaniParser.NormalizeOrigin(kat);
        if (kat.KaynakKatmanlari is not null)
            kat.KayitliTespitSegmentleri = kat.HamTespitSegmentler.Select(s => new BinaDxfSegment { A = s.A, B = s.B }).ToList();

        kat.DxfSegmentler = DxfSegmentYardimci.Sadelestir(kat.HamSegmentler)
            .Select(s => new BinaDxfSegment { A = s.A, B = s.B })
            .ToList();

        return kat;
    }

    public sealed record OtomatikSonuc(IReadOnlyList<BinaKat> Katlar, IReadOnlyList<string> Notlar);

    public OtomatikSonuc OtomatikYukle(string dxfPath, string? kaynakYolu = null)
    {
        var ham = new DxfKatPlaniParser().HamVeriyiOku(dxfPath);
        return OtomatikYukle(ham, kaynakYolu ?? dxfPath);
    }

    /// Önizlemede seçilen katmanların anlık kopyasından üretir; kaynağı tekrar açmaz.
    public OtomatikSonuc OtomatikYukle(BinaKat ham, string? kaynakYolu = null)
    {
        var tespit = CokluKatTespitEt(ham);
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(UnitWarning)) notes.Add(UnitWarning);
        var adaylar = tespit?.Adaylar.ToList();
        if (adaylar is null)
        {
            var kumeler = KatKumelemeServisi.Kumele(ham.HamSegmentler, ham.HamMetinler,
                ham.DxfArclar.Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg)).ToList());
            var (planlar, _) = KatKumelemeServisi.PlanFiltrele(kumeler);
            adaylar = planlar.Select(k => {
                var ad = KatEtiketServisi.TahminEt(k);
                return new CokluKatAdayi(k, ad.OnerilenAd, ad.SiraDegeri, ad.EtiketBulundu, true)
                { Olculer = MimariProjeMetinServisi.YukseklikOku(k.Metinler) };
            }).ToList();
        }
        var secilen = adaylar.Where(a => a.OneriliDahil).OrderBy(a => a.SiraDegeri).ToList();
        bool blokEslesmesiGerekli = MimariKatYukseklikServisi.BlokEslesmesiGerekli(adaylar);
        if (secilen.Count == 0) notes.Add("Güvenilir kat planı ayrılamadı. Çizim başlıklarını/katmanlarını kontrol edin veya tek kat DXF yükleyin.");
        var katlar = new List<BinaKat>();
        for (int i = 0; i < secilen.Count; i++)
        {
            var aday = secilen[i];
            var olcu = aday.Olculer!;
            double? height = olcu.KatYuksekligiM;
            string evidence = "çizim verileri; kat ana referansları arası";
            if (height is null && !blokEslesmesiGerekli && i + 1 < secilen.Count && aday.EtiketBulundu && secilen[i + 1].EtiketBulundu &&
                Math.Abs(secilen[i + 1].SiraDegeri - aday.SiraDegeri - 1) < .01 &&
                olcu.TabanKotuM is { } alt && secilen[i + 1].Olculer?.TabanKotuM is { } ust)
            { height = MimariProjeMetinServisi.KotFarkindanYukseklik(alt, ust); evidence = "ardışık kat kotları farkı"; }
            var kat = CokluKatUret(aday, i + 1, aday.OnerilenAd, height ?? 3.0);
            kat.DxfFilePath = kaynakYolu ?? ham.DxfFilePath;
            kat.UnitWarning = UnitWarning;
            var local = new List<string>(olcu.Uyarilar);
            local.AddRange(olcu.Kanitlar);
            if (!string.IsNullOrWhiteSpace(UnitWarning)) local.Insert(0, UnitWarning);
            if (height is null) local.Add("Kat yüksekliği okunamadı: 3,00 m geçici değer; kontrol gerekli.");
            else local.Add($"Kat yüksekliği {height:0.##} m ({evidence}).");
            if (!aday.EtiketBulundu) local.Add("Kat adı/sırası doğrulanamadı; kontrol gerekli.");
            var room = MimariProjeMetinServisi.OdaYukseklikleriniUygula(kat);
            var yerelKotlar = MimariKatYukseklikServisi.YerelKotlariOku(kat);
            local.AddRange(yerelKotlar.Uyarilar);
            foreach (var kot in yerelKotlar.Kotlar)
            {
                var oda = kat.Odalar.Single(o => o.Id == kot.OdaId);
                bool anaKotlaAyni = kat.TabanKotuM is { } ana && Math.Abs(ana - kot.TabanKotuM) < .02;
                if (anaKotlaAyni || (kot.DosemeKotuAcik && oda.YukseklikOverrideM is > 0))
                {
                    // Yerel taban değişince eski 4 m kat aralığını aynen taşımak
                    // üst katla çakışan hacim üretir. Farklı kot yalnız bağımsız
                    // net yükseklik kanıtıyla otomatik hacme uygulanır.
                    if (!anaKotlaAyni) oda.TabanKotuOverrideM = kot.TabanKotuM;
                    local.Add($"'{oda.Ad}': {kot.TabanKotuM:0.###} m taban kotu oda içindeki yazıyla doğrulandı.");
                }
                else
                    local.Add($"'{oda.Ad}': planda {kot.TabanKotuM:0.###} m yerel kot adayı var. Net yükseklik/üst döşeme ve merdiven ayrımı doğrulanmadığından oda hacmine uygulanmadı.");
                local.AddRange(kot.Kanitlar.Select(k => $"{oda.Ad}: {k}"));
            }
            var opening = MimariProjeMetinServisi.AciklikBoyutlariniUygula(kat);
            local.AddRange(room.Uyarilar); local.AddRange(opening.Uyarilar);
            local.AddRange(MimariGeometriKaliteServisi.Uygula(kat));
            local.Add($"{kat.Odalar.Count} oda, {kat.Acikliklar.Count} açıklık; {opening.Uygulanan} açıklıkta yazılı ölçü eşleştirildi.");
            int defaults = kat.Acikliklar.Count(a => !a.IptalEdildi) - opening.Uygulanan;
            if (defaults > 0) local.Add($"{defaults} açıklıkta yükseklik/denizlik tip varsayılanları kullanılıyor; kontrol gerekli.");
            if (kat.Odalar.Count == 0) local.Add("Kapalı oda sınırı bulunamadı; bu katın geometrisi eksik.");
            local.Add("Katların yatay hizası oda sınırına göre yapıldı; akslarla kontrol edin.");
            kat.IceriAktarimNotlari = local;
            notes.AddRange(local.Select(n => $"{kat.Ad}: {n}"));
            katlar.Add(kat);
        }
        katlar.Reverse();
        for (int i = 0; i < katlar.Count; i++) katlar[i].KatNo = i + 1;
        return new(katlar, notes);
    }

    /// Seçili katı güncellerken bütün açık çizimi tek kat olarak kullanır.
    public BinaKat Yukle(BinaKat hamKat, int katNo, string katAdi, double katYuksekligiM, Ayarlar? ayar = null)
    {
        ArgumentNullException.ThrowIfNull(hamKat);
        if (hamKat.HamSegmentler.Count == 0) throw new InvalidOperationException("Açık katmanlarda çizgi geometrisi yok.");
        _hamKat = hamKat;
        UnitWarning = hamKat.UnitWarning;
        var pts = hamKat.HamSegmentler.SelectMany(s => new[] { s.A, s.B }).ToList();
        var kume = new KatKumelemeServisi.KatKumesi(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.X), pts.Max(p => p.Y),
            hamKat.HamSegmentler.ToList(), hamKat.HamMetinler.ToList(), hamKat.DxfArclar.Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg)).ToList());
        var kat = CokluKatUret(new CokluKatAdayi(kume, katAdi, katNo, false, true), katNo, katAdi, katYuksekligiM, ayar);
        kat.UnitWarning = UnitWarning;
        return kat;
    }

    private static IReadOnlyList<(Kst.Acoustics.Core.Common.Geometry.Point2D A, Kst.Acoustics.Core.Common.Geometry.Point2D B)> TespitSegmentleri(BinaKat kat)
        => kat.KaynakKatmanlari is not null || kat.HamTespitSegmentler.Count > 0 ? kat.HamTespitSegmentler : kat.HamSegmentler;
}
