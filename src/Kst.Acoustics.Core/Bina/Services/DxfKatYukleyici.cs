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
        bool OneriliDahil);

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
        var birlesikKat = parser.Parse(filePath, 0, 3.0, normalizeOrigin: false);
        UnitWarning = parser.UnitWarning;

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
            return new CokluKatAdayi(k, tahmin.OnerilenAd, tahmin.SiraDegeri, tahmin.EtiketBulundu, planSet.Contains(k));
        }).ToList();

        return new CokluKatTespitSonucu(adaylar, UnitWarning);
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
            HamSegmentler = kume.Segmentler.ToList(),
            HamMetinler = kume.Metinler.ToList(),
            DxfArclar = kume.Arclar
                .Select(a => new BinaDxfArc { Merkez = a.Merkez, R = a.R, BasAciDeg = a.BasDeg, BitAciDeg = a.BitDeg })
                .ToList()
        };

        DxfKatPlaniParser.OdalariBosluklardanYenidenTara(kat);

        if (!ayar.AciklikTespit)
        {
            kat.Acikliklar.Clear();
        }
        else
        {
            var arcTuple = kat.DxfArclar.Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg)).ToList();
            kat.Acikliklar = AciklikTespitServisi.Bul(kat.HamSegmentler, arcTuple, Array.Empty<BinaAciklik>());
            AciklikYardimci.DisindaKalanlariSil(kat);
            if (ayar.KomsuDuvarlariTamamla)
                AciklikYardimci.KomsuDuvarlariTamamla(kat);
        }

        // Küme dünya koordinatındadır — her kat kendi yerel çerçevesine (bbox-min)
        // normalize edilir; kaba hizalama sonradan OriginOffset ile yapılır (§9.5).
        DxfKatPlaniParser.NormalizeOrigin(kat);

        kat.DxfSegmentler = DxfSegmentYardimci.Sadelestir(kat.HamSegmentler)
            .Select(s => new BinaDxfSegment { A = s.A, B = s.B })
            .ToList();

        return kat;
    }
}
