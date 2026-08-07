using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;

namespace Kst.Acoustics.Core.BuildingAcoustics.Reports;

public class BinaAkustikRaporServisi
{
    private readonly YonetmelikUyumServisi _uyumServisi = new();
    private readonly DisGurultuServisi _disGurultuServisi = new();
    private readonly HesapGuvenilirlikServisi _guvenServisi = new();

    /// <param name="haricTutulanBolmeIdleri">Kullanıcının "Rapora Dahil" işaretini kaldırdığı
    /// kanonik sonuç kimlikleri. İç bölmeler kendi Id'sini; oda-birleşik dış cephe, sentetik
    /// darbe, tesisat ve reverberasyon satırları kararlı sonuç Id'lerini kullanır.</param>
    public string MetinRaporOlustur(BinaProje proje, ISet<Guid>? haricTutulanBolmeIdleri = null)
    {
        var sb = new StringBuilder();
        var sinifBilgi = BinaSinifiHelper.BilgiAl(proje.BinaSinifi);

        sb.AppendLine("AKUSTİK PERFORMANS RAPORU");
        sb.AppendLine($"Proje    : {proje.ProjeAdi}");
        sb.AppendLine($"Sınıf    : {sinifBilgi.KodGosterim} — {sinifBilgi.Aciklama}");
        sb.AppendLine($"Referans : {sinifBilgi.YonetmelikEk}");
        sb.AppendLine($"Dış Gür. : Lgag/Lden {proje.DisGurultuDb:F0} dBA, iç hedef mekan işlevine göre EK-4 Tablo 4.1, cephe formu {proje.DisGurultuCepheMaruziyeti} (ΔLfs={DisGurultuServisi.DeltaLfs(proje.DisGurultuCepheMaruziyeti):F0} dB)");
        sb.AppendLine($"Belirsiz.: ±{HavaSesiHesapServisi.HesapBelirsizligi} dB (12354-1 Tab.B.1)");
        sb.AppendLine("Dış yapı elemanları: DnT,A,tr, D2m,nT,50 ve spektral iç gürültü değerlendirmesi.");
        sb.AppendLine(new string('-', 55));
        sb.AppendLine();

        foreach (var grup in TabloVerisiOlustur(proje, haricTutulanBolmeIdleri)
                     .GroupBy(s => new { s.Kat, s.Mekan }))
        {
            sb.AppendLine($"KAT {grup.Key.Kat} — {grup.Key.Mekan}");
            foreach (var satir in grup)
            {
                var durum = satir.Durum switch
                {
                    nameof(UyumDurumu.Gecti) => "✅ GEÇTİ",
                    nameof(UyumDurumu.VeriEksik) => "⚠️ VERİ EKSİK",
                    _ => "❌ KALDI",
                };
                var yon = satir.DahaDusukDahaIyi ? "≤" : "≥";
                var belirsizlik = satir.Belirsizlik > 0 ? $" ±{satir.Belirsizlik:F0}" : "";
                var kaynak = string.IsNullOrWhiteSpace(satir.VeriKaynagi) ? "" : $" | Kaynak: {satir.VeriKaynagi}";
                var limitMetni = satir.LimitDeger > 0 ? $"{yon} {satir.LimitDeger:F1}" : "belirlenemedi";
                var marjMetni = double.IsNaN(satir.MarjDb) ? "—" : $"{satir.MarjDb:+0.0;-0.0;0.0}";
                sb.AppendLine(
                    $"  - [{satir.SonucTuruEtiketi}] {satir.Bolme}: {satir.Deger:F1}{belirsizlik} {satir.Birim} " +
                    $"(limit {limitMetni}, marj {marjMetni}) | {durum} " +
                    $"| Güven: {satir.GuvenOzeti}{kaynak}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// Tüm çıktı biçimlerinin kullandığı kanonik kapsam. Fiziksel hava/cephe bölmelerine ek
    /// olarak katlar arası sentetik darbe yolları, EK-5 tesisat ve EK-6 RT satırlarını da içerir.
    public List<RaporSatiri> TabloVerisiOlustur(BinaProje proje, ISet<Guid>? haricTutulanBolmeIdleri = null)
    {
        var satirlar = new List<RaporSatiri>();

        foreach (var kat in proje.Katlar)
        {
            foreach (var mekan in kat.Mekanlar)
            {
                if (HesapDisiMekan(mekan))
                    continue;

                // Dış cephe, tek tek duvar parçalarının değil odadaki bütün cephelerin
                // enerjetik birleşik sonucudur. Fiziksel parçaları ayrı rapor satırlarına
                // yazmak aynı DnT,A,tr değerini çoğaltıyor ve seçim filtresini belirsiz
                // bırakıyordu. İç bölmeler fiziksel kimlikleriyle, cephe ise oda-türü
                // temelli kararlı bir sonuç kimliğiyle tam bir kez raporlanır.
                var cepheler = mekan.Bolmeler
                    .Where(b => b.IsDisCephe && !HesapDisiBolme(b))
                    .OrderBy(b => b.Id)
                    .ToList();

                foreach (var bolme in mekan.Bolmeler.Where(b => !b.IsDisCephe))
                {
                    if (HesapDisiBolme(bolme))
                        continue;
                    if (haricTutulanBolmeIdleri?.Contains(bolme.Id) == true)
                        continue;

                    satirlar.Add(BolmeSatiriOlustur(proje, kat, mekan, bolme));
                }

                if (cepheler.Count > 0)
                {
                    var sonucId = RaporSonucIdleri.DisCephe(mekan.Id);
                    // Eski oturumlarda fiziksel cephe Id'siyle yapılmış bir seçim varsa
                    // güvenli geriye uyum: o seçimi birleşik oda sonucuna uygula.
                    var haric = haricTutulanBolmeIdleri?.Contains(sonucId) == true ||
                                cepheler.Any(b => haricTutulanBolmeIdleri?.Contains(b.Id) == true);
                    if (!haric)
                    {
                        var temsilci = cepheler.FirstOrDefault(b => b.YapiElemani?.EffectiveRw > 0)
                                        ?? cepheler[0];
                        var satir = BolmeSatiriOlustur(proje, kat, mekan, temsilci);
                        satir.SonucId = sonucId;
                        if (cepheler.Count > 1)
                            satir.Bolme = $"Birleşik dış cephe ({cepheler.Count} parça)";
                        satirlar.Add(satir);
                    }
                }
            }
        }

        // Katlar arası darbe yolları mekan.Bolmeler koleksiyonunda yaşamaz. Aynı servis ve
        // deterministik Id hem UI hem bütün rapor formatlarının aynı yolu seçmesini sağlar.
        foreach (var bolme in new KatlarArasiDarbeYoluServisi().DegerlendirilmisDarbeBolmeleri(proje))
        {
            if (haricTutulanBolmeIdleri?.Contains(bolme.Id) == true || HesapDisiBolme(bolme))
                continue;
            var kat = proje.Katlar.FirstOrDefault(k =>
                k.Mekanlar.Any(m => m.Id == bolme.KaynakMekan?.Id));
            satirlar.Add(BolmeSatiriOlustur(proje, kat, bolme.KaynakMekan, bolme));
        }

        foreach (var kat in proje.Katlar)
        foreach (var mekan in kat.Mekanlar)
        {
            if (HesapDisiMekan(mekan)) continue;

            var (surekli, kesikli) = EK5TesisatServisi.Degerlendir(proje, mekan);
            if (surekli != null)
            {
                var id = RaporSonucIdleri.Tesisat(mekan.Id, kesikli: false);
                if (haricTutulanBolmeIdleri?.Contains(id) != true)
                    satirlar.Add(TesisatSatiri(kat, mekan, surekli, id, "Sürekli tesisat"));
            }
            if (kesikli != null)
            {
                var id = RaporSonucIdleri.Tesisat(mekan.Id, kesikli: true);
                if (haricTutulanBolmeIdleri?.Contains(id) != true)
                    satirlar.Add(TesisatSatiri(kat, mekan, kesikli, id, "Kesikli tesisat"));
            }

            var rt = EK6ReverberasyonServisi.Degerlendir(proje, mekan);
            if (rt != null)
            {
                var id = RaporSonucIdleri.Reverberasyon(mekan.Id);
                if (haricTutulanBolmeIdleri?.Contains(id) != true)
                    satirlar.Add(ReverberasyonSatiri(kat, mekan, rt, id));
            }
        }

        return satirlar;
    }

    /// Tek bir geometrik elemanı, DXF ve diğer görselleştirmelerin de kullanabileceği aynı
    /// değerlendirme satırına dönüştürür.
    public RaporSatiri BolmeSatiriOlustur(BinaProje proje, Kat? kat, Mekan? mekan, Bolme bolme)
    {
        var isDoseme = bolme.YapiElemani?.Tip == YapiElemaniTipi.Doseme;
        var isDisCephe = bolme.IsDisCephe;
        var spektral = isDisCephe ? _disGurultuServisi.HesaplaSpektralDisCephe(proje, bolme) : null;
        var (uyum, limit) = DisGurultuUygulanmisLimit(proje, bolme, spektral);
        var icHedef = isDisCephe ? EK4IcGurultuServisi.IcGurultuHedefiBul(proje, bolme.KaynakMekan) : null;
        var deger = isDoseme
            ? bolme.ApparentLnTw
            : isDisCephe
                // Spektral bileşik hesap üretilemediyse eski tek-sayı sonucuna düşmek,
                // eksik bir pencere/menfezi görünmez yapıp sahte uygunluk üretebilir.
                ? spektral?.DnTATr ?? 0
                : _uyumServisi.HesaplaOlcumDegeri(bolme, limit);
        var guven = _guvenServisi.Degerlendir(proje, bolme);
        var kaynak = isDoseme
            ? bolme.OktavDarbeSesiSonucu?.Kaynak
              ?? bolme.YapiElemani?.DarbeSpektrumKaynagi.ToString()
              ?? "Belirsiz"
            : bolme.UcuncuOktavHavaSesiSonucu?.Kaynak
              ?? bolme.OktavHavaSesiSonucu?.Kaynak
              ?? bolme.YapiElemani?.SpektrumKaynagi.ToString()
              ?? "Belirsiz";
        var kaynakEtiketi = KaynakEtiketi(kaynak);

        return new RaporSatiri
        {
            SonucId = bolme.Id,
            SonucTuru = isDoseme ? AkustikRaporSonucTuru.DarbeSesi
                : isDisCephe ? AkustikRaporSonucTuru.DisCephe
                : AkustikRaporSonucTuru.HavaSesi,
            Kat = kat?.KatNo.ToString() ?? "—",
            Mekan = mekan?.Ad ?? bolme.KaynakMekan?.Ad ?? "—",
            KaynakMekan = bolme.KaynakMekan?.Ad ?? mekan?.Ad ?? "",
            AliciMekan = bolme.AliciMekan?.Ad ?? "",
            Bolme = bolme.Ad,
            Rw = bolme.ApparentRw,
            Dntw = bolme.Dntw,
            Lnw = bolme.ApparentLnw,
            Deger = deger,
            Belirsizlik = isDoseme
                ? DarbeSesiHesapServisi.HesapBelirsizligi
                : HavaSesiHesapServisi.HesapBelirsizligi,
            Birim = isDoseme ? "dB L'nT,w" : limit.Birim,
            LimitDeger = limit.HedefDeger,
            MarjDb = UygulanmisMarj(uyum, limit, deger, isDoseme, isDisCephe, spektral, icHedef),
            DahaDusukDahaIyi = isDoseme,
            YonetmelikEk = limit.Aciklama,
            Durum = uyum.ToString(),
            DisGurultuDb = isDisCephe ? proje.DisGurultuDb : null,
            IcGurultuDb = isDisCephe ? spektral?.IcToplam : null,
            IcHedefDb = icHedef,
            D2mNt50Db = isDisCephe ? spektral?.D2mNt50 : null,
            SpektrumKaynak = isDisCephe ? spektral?.Kaynak ?? kaynak : kaynak,
            VeriKaynagi = isDisCephe
                ? $"Yapı: {kaynakEtiketi}; dış gürültü: {KaynakEtiketi(proje.DisGurultuSpektrumKaynagi.ToString())}"
                : kaynakEtiketi,
            GuvenPuani = guven.Puan,
            GuvenSeviyesi = guven.Seviye,
            GuvenOzeti = guven.Ozet,
        };
    }

    private static RaporSatiri TesisatSatiri(Kat kat, Mekan mekan,
        EK5TesisatServisi.TesisatSonucu sonuc, Guid id, string ad) => new()
    {
        SonucId = id,
        SonucTuru = ad.StartsWith("Sürekli", StringComparison.Ordinal)
            ? AkustikRaporSonucTuru.TesisatSurekli
            : AkustikRaporSonucTuru.TesisatKesikli,
        Kat = kat.KatNo.ToString(), Mekan = mekan.Ad, Bolme = ad,
        KaynakMekan = mekan.Ad,
        Deger = sonuc.GirilenDb, LimitDeger = sonuc.LimitDb,
        MarjDb = sonuc.Durum == UyumDurumu.VeriEksik ? double.NaN : sonuc.MarjDb,
        DahaDusukDahaIyi = true, Birim = sonuc.Birim,
        YonetmelikEk = sonuc.SatirAdi, Durum = sonuc.Durum.ToString(),
        VeriKaynagi = "Kullanıcı beyanı", GuvenPuani = 85, GuvenSeviyesi = "Beyan",
        GuvenOzeti = "Tesisat düzeyi kullanıcı tarafından beyan edilmiştir.",
    };

    private static RaporSatiri ReverberasyonSatiri(Kat kat, Mekan mekan,
        EK6ReverberasyonServisi.RtSonucu sonuc, Guid id)
    {
        var varsayilan = mekan.EmilimAlani <= 0;
        return new RaporSatiri
        {
            SonucId = id, SonucTuru = AkustikRaporSonucTuru.Reverberasyon,
            Kat = kat.KatNo.ToString(), Mekan = mekan.Ad, Bolme = "Reverberasyon süresi",
            KaynakMekan = mekan.Ad,
            HacimM3 = mekan.Hacim,
            Deger = sonuc.TahminiTSn, LimitDeger = sonuc.TMaxSn ?? 0,
            MarjDb = sonuc.Durum == UyumDurumu.VeriEksik ? double.NaN : sonuc.MarjSn ?? double.NaN,
            DahaDusukDahaIyi = true,
            Birim = "s T", YonetmelikEk = $"EK-6 Tablo 6.1: {sonuc.SatirAdi}",
            Durum = sonuc.Durum.ToString(), VeriKaynagi = varsayilan ? "Varsayılan/tahmin" : "Kullanıcı emilim beyanı",
            GuvenPuani = varsayilan ? 30 : 75, GuvenSeviyesi = varsayilan ? "Düşük" : "Orta",
            GuvenOzeti = varsayilan
                ? "Emilim alanı beyan edilmedi; bu satır yalnız ön tahmindir."
                : "Sabine ön tahmini, kullanıcı emilim alanına dayanır.",
        };
    }

    public static string KaynakEtiketi(string? kaynak)
    {
        var k = kaynak?.Trim() ?? "";
        return k.ToUpperInvariant() switch
        {
            "OLCUM" or "ÖLÇÜM" or "OKTAVVERI" or "UCUNCUOKTAVVERI" => "Ölçüm",
            "URETICI" or "ÜRETİCİ" => "Üretici",
            "MODEL" or "MODELTAHMIN" or "MODELTAHMİN" or "TEKSAYI" => "Model",
            "VARSAYILAN" => "Varsayılan",
            "BELIRSIZ" or "BELİRSİZ" or "" => "Belirsiz",
            _ => k,
        };
    }

    // ─── Excel Raporu ─────────────────────────────────────────────────────────

    /// UX — Excel rapor filtreleri.
    public enum RaporFiltresi
    {
        /// Tüm bölmeler.
        Tumu,
        /// Sadece yönetmelik limitini aşmayan (kalan) bölmeler.
        SadeceLimitAsan,
        /// Sadece güven puanı 70'in altında olan bölmeler.
        SadeceDusukGuven,
    }

    public void ExcelRaporOlustur(BinaProje proje, string filePath, ISet<System.Guid>? haricTutulanBolmeIdleri = null) =>
        ExcelRaporOlustur(proje, filePath, RaporFiltresi.Tumu, haricTutulanBolmeIdleri);

    public void ExcelRaporOlustur(BinaProje proje, string filePath, RaporFiltresi filtre,
        ISet<System.Guid>? haricTutulanBolmeIdleri = null)
    {
        using var wb = new XLWorkbook();
        YazOzetSayfasi(wb, proje, filtre, haricTutulanBolmeIdleri);
        YazOktavSayfasi(wb, proje, filtre, haricTutulanBolmeIdleri);
        wb.SaveAs(filePath);
    }

    private static bool SatirGecer(RaporSatiri s, RaporFiltresi filtre) => filtre switch
    {
        RaporFiltresi.SadeceLimitAsan   => s.Durum == UyumDurumu.Kaldi.ToString(),
        RaporFiltresi.SadeceDusukGuven  => s.GuvenPuani < 70,
        _                                => true,
    };

    private void YazOzetSayfasi(XLWorkbook wb, BinaProje proje, RaporFiltresi filtre = RaporFiltresi.Tumu,
        ISet<System.Guid>? haricTutulanBolmeIdleri = null)
    {
        var ws = wb.Worksheets.Add("Özet");
        var sinifBilgi = BinaSinifiHelper.BilgiAl(proje.BinaSinifi);

        // Başlık bilgisi
        ws.Cell(1, 1).Value = "AKUSTİK PERFORMANS RAPORU";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Proje: {proje.ProjeAdi}";
        ws.Cell(3, 1).Value = $"Sınıf: {sinifBilgi.KodGosterim} — {sinifBilgi.Aciklama}";
        ws.Cell(4, 1).Value = $"Referans: {sinifBilgi.YonetmelikEk}";
        ws.Cell(5, 1).Value = $"Belirsizlik: ±{HavaSesiHesapServisi.HesapBelirsizligi} dB";

        // Sütun başlıkları
        string[] headers = ["Kat", "Mekan", "Sonuç Türü", "Eleman / Kriter", "Hesaplanan", "±σ", "Limit", "Marj", "Birim", "Dış Lgag", "İç Gürültü", "İç Hedef", "D2m,nT,50", "Veri Kaynağı", "Güven", "Durum", "Yönetmelik/Not"];
        int hRow = 7;
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(hRow, i + 1).Value = headers[i];
            ws.Cell(hRow, i + 1).Style.Font.Bold = true;
            ws.Cell(hRow, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            ws.Cell(hRow, i + 1).Style.Font.FontColor = XLColor.White;
        }

        // UX — Filtre etiketi
        if (filtre != RaporFiltresi.Tumu)
        {
            ws.Cell(6, 1).Value = $"Filtre: {(filtre == RaporFiltresi.SadeceLimitAsan ? "Sadece limit aşan" : "Sadece güven puanı < 70")}";
            ws.Cell(6, 1).Style.Font.Italic = true;
            ws.Cell(6, 1).Style.Font.FontColor = XLColor.FromHtml("#A04000");
        }

        // Veri satırları
        int row = hRow + 1;
        foreach (var satir in TabloVerisiOlustur(proje, haricTutulanBolmeIdleri).Where(s => SatirGecer(s, filtre)))
        {
            ws.Cell(row, 1).Value = satir.Kat;
            ws.Cell(row, 2).Value = satir.Mekan;
            ws.Cell(row, 3).Value = satir.SonucTuruEtiketi;
            ws.Cell(row, 4).Value = satir.Bolme;
            ws.Cell(row, 5).Value = satir.Deger;
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, 6).Value = satir.Belirsizlik;
            ws.Cell(row, 6).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, 7).Value = satir.LimitDeger;
            ws.Cell(row, 7).Style.NumberFormat.Format = "0.0";
            if (!double.IsNaN(satir.MarjDb)) ws.Cell(row, 8).Value = satir.MarjDb;
            ws.Cell(row, 8).Style.NumberFormat.Format = "+0.0;-0.0;0.0";
            ws.Cell(row, 9).Value = satir.Birim;
            ws.Cell(row, 10).Value = satir.DisGurultuDb;
            ws.Cell(row, 11).Value = satir.IcGurultuDb;
            ws.Cell(row, 12).Value = satir.IcHedefDb;
            ws.Cell(row, 13).Value = satir.D2mNt50Db;
            ws.Cell(row, 14).Value = satir.VeriKaynagi;
            ws.Cell(row, 15).Value = $"{satir.GuvenSeviyesi} {satir.GuvenPuani}/100";
            ws.Cell(row, 16).Value = satir.Durum;
            ws.Cell(row, 17).Value = $"{satir.YonetmelikEk} | Güven notu: {satir.GuvenOzeti}";

            // Durum rengi — VeriEksik, başarısızlıkla (Kaldı) aynı kırmızıya boyanmamalı;
            // ne "geçti" ne "kaldı" olduğundan ayrı (sarı) bir uyarı rengi kullanılır.
            var durumRengi = satir.Durum switch
            {
                var d when d == UyumDurumu.Gecti.ToString()     => XLColor.FromHtml("#1E5C3A"),
                var d when d == UyumDurumu.VeriEksik.ToString() => XLColor.FromHtml("#7A6A1E"),
                _                                                => XLColor.FromHtml("#5C1E1E"),
            };
            ws.Cell(row, 16).Style.Fill.BackgroundColor = durumRengi;
            ws.Cell(row, 16).Style.Font.FontColor = XLColor.White;

            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private void YazOktavSayfasi(XLWorkbook wb, BinaProje proje, RaporFiltresi filtre,
        ISet<System.Guid>? haricTutulanBolmeIdleri = null)
    {
        var ws = wb.Worksheets.Add("Oktav Bantlar");

        // Özet ve oktav sayfaları aynı kanonik sonuç kümesini kullanır. Cephe ayrıntı
        // satırları fiziksel elemanları gösterebilir; dahil olup olmadıkları oda-birleşik
        // cephe SonucId'sinden belirlenir.
        var dahilSonucIdleri = TabloVerisiOlustur(proje, haricTutulanBolmeIdleri)
            .Where(s => SatirGecer(s, filtre))
            .Select(s => s.SonucId)
            .ToHashSet();

        string[] freqBasliklari = ["63 Hz", "125 Hz", "250 Hz", "500 Hz", "1000 Hz", "2000 Hz", "4000 Hz"];
        string[] headers =
        [
            "Kat", "Mekan", "Bölme", "Tip", "Kaynak",
            .. freqBasliklari,
            "Rw/Lnw (tek sayı)", "C", "Ctr"
        ];

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var kat in proje.Katlar)
        {
            foreach (var mekan in kat.Mekanlar)
            {
                if (HesapDisiMekan(mekan))
                    continue;

                foreach (var bolme in mekan.Bolmeler)
                {
                    if (HesapDisiBolme(bolme))
                        continue;

                    var sonucId = bolme.IsDisCephe
                        ? RaporSonucIdleri.DisCephe(mekan.Id)
                        : bolme.Id;
                    if (!dahilSonucIdleri.Contains(sonucId))
                        continue;

                    bool isDoseme = bolme.YapiElemani?.Tip == YapiElemaniTipi.Doseme;

                    if (!isDoseme && bolme.OktavHavaSesiSonucu is { } hava)
                    {
                        YazOktavSatiri(ws, row++, kat.KatNo, mekan.Ad, bolme.Ad,
                            "R'(f)", hava.Kaynak, hava.R_f, hava.RwHesap, hava.CHesap, hava.CtrHesap);
                    }
                    else if (isDoseme && bolme.OktavDarbeSesiSonucu is { } darbe)
                    {
                        YazOktavSatiri(ws, row++, kat.KatNo, mekan.Ad, bolme.Ad,
                            "L'n(f)", darbe.Kaynak, darbe.Ln_f, darbe.LnwHesap, 0, 0);
                    }
                }
            }
        }

        ws.Columns().AdjustToContents();

        // Referans eğrisi bilgi notu
        int noteRow = row + 2;
        ws.Cell(noteRow, 1).Value = "Not: Veri kökeni ölçüm, üretici, model, varsayılan veya belirsiz olarak açıkça belirtilir; model/varsayılan spektrumlar laboratuvar ölçümü değildir.";
        ws.Cell(noteRow, 1).Style.Font.Italic = true;
        ws.Cell(noteRow, 1).Style.Font.FontColor = XLColor.Gray;
        ws.Range(noteRow, 1, noteRow, headers.Length).Merge();
    }

    private static void YazOktavSatiri(IXLWorksheet ws, int row, int katNo, string mekan,
        string bolme, string tip, string kaynak, double[] bantlar, double tekSayi, double c, double ctr)
    {
        ws.Cell(row, 1).Value = katNo.ToString();
        ws.Cell(row, 2).Value = mekan;
        ws.Cell(row, 3).Value = bolme;
        ws.Cell(row, 4).Value = tip;
        var kaynakEtiketi = KaynakEtiketi(kaynak);
        ws.Cell(row, 5).Value = kaynakEtiketi;

        for (int i = 0; i < bantlar.Length && i < 7; i++)
        {
            ws.Cell(row, 6 + i).Value = bantlar[i];
            ws.Cell(row, 6 + i).Style.NumberFormat.Format = "0.0";
        }

        ws.Cell(row, 13).Value = tekSayi;
        ws.Cell(row, 13).Style.NumberFormat.Format = "0.0";
        ws.Cell(row, 13).Style.Font.Bold = true;

        if (c != 0 || ctr != 0)
        {
            ws.Cell(row, 14).Value = c;
            ws.Cell(row, 15).Value = ctr;
        }

        // Model tahmini satırlarını soluk renklendirme
        if (kaynakEtiketi != "Ölçüm" && kaynakEtiketi != "Üretici")
            ws.Range(row, 1, row, 15).Style.Font.FontColor = XLColor.FromHtml("#7894A8");
    }

    private static bool HesapDisiMekan(Mekan? mekan) =>
        mekan != null &&
        (
            mekan.Sinif == MekanSinifi.HesapDisi ||
            EK2SiniflandirmaServisi.HesapDisiMi(mekan.KullanimTipi) ||
            MekanKullanimiHelper.Parse(mekan.KullanimTipi) == MekanKullanimiTipi.HesapDisi
        );

    private static bool HesapDisiBolme(Bolme bolme) =>
        HesapDisiMekan(bolme.KaynakMekan) || HesapDisiMekan(bolme.AliciMekan);

    private (UyumDurumu Durum, AkustikLimit Limit) DisGurultuUygulanmisLimit(
        BinaProje proje, Bolme bolme, DisCepheSpektralSonuc? spektral)
    {
        // P0 düzeltmesi (2026-07-14) — disGurultuLgag artık ilk çağrıya geçiliyor; limit
        // Tablo 3.1'in gerçek Lgag − ofset(hassasiyet, sınıf) formülüyle geliyor
        // (YonetmelikLimitleri.GetTablo31Limit). Eskiden bu parametre geçilmiyordu (Lgag=0
        // varsayımıyla yalnız 30 dB taban hesaplanıyordu), ardından yanlış düz "Lgag−35"
        // formülüyle elle eziliyordu — örn. C sınıfı/I hassasiyette gerçek limit 43 dB iken
        // rapor 30 dB'ye düşürüyordu (UI'da "kaldı" iken raporda "geçti" görünebiliyordu).
        var (uyum, limit) = _uyumServisi.KontrolEtDetayli(
            proje.BinaSinifi,
            proje.AkustikPerformansSinifi,
            bolme,
            proje.DisGurultuDb);

        if (!bolme.IsDisCephe)
            return (uyum, limit);

        if (proje.DisGurultuDb <= 0)
        {
            limit.Aciklama = "Dış cephe değerlendirmesi için Lgag/Lden girilmedi.";
            return (UyumDurumu.VeriEksik, limit);
        }

        if (limit.HedefDeger <= 0)
            return (UyumDurumu.VeriEksik, limit);

        // Ek konfor kriteri (ViewModel.Hesapla() ile tutarlı): DnT,A,tr yönetmelik limitini
        // geçse de ortaya çıkan iç gürültü hedefin üstündeyse "Kaldı" sayılır.
        // EK-4 entegrasyonu — hedef alıcı mekan işlevine göre Tablo 4.1'den gelir;
        // eşleşmeyen kullanım tipi resmî değerlendirmede VeriEksik kalır.
        var icHedef = EK4IcGurultuServisi.IcGurultuHedefiBul(proje, bolme.KaynakMekan);
        if (icHedef == null)
        {
            limit.Aciklama = "EK-4 Tablo 4.1 için mekan kullanım tipi eşleşmedi; iç gürültü hedefi belirlenemedi.";
            return (UyumDurumu.VeriEksik, limit);
        }

        // Tek ortak hedef: EK-3 Tablo 3.1 gereği ile EK-4 iç düzeyini sağlayacak
        // Lgag−L_iç gereğinin daha sıkısı. UI, metin, HTML, Excel, DXF ve 3D bu
        // rapor satırını kullandığında aynı limite bakar.
        var ek3Hedef = limit.HedefDeger;
        var ek4Hedef = proje.DisGurultuDb - icHedef.Value;
        limit.HedefDeger = Math.Max(ek3Hedef, ek4Hedef);
        limit.Aciklama =
            $"EK-3 Tablo 3.1: {ek3Hedef:F0} dB; EK-4 Tablo 4.1 iç hedef {icHedef:F0} dBA " +
            $"(Lgag {proje.DisGurultuDb:F0} − {icHedef:F0} = {ek4Hedef:F0} dB); uygulanan DnT,A,tr ≥ {limit.HedefDeger:F0} dB";

        if (spektral == null || !double.IsFinite(spektral.DnTATr) || spektral.DnTATr <= 0 ||
            !double.IsFinite(spektral.IcToplam))
            return (UyumDurumu.VeriEksik, limit);

        var deger = spektral.DnTATr;
        var icToplam = spektral.IcToplam;
        uyum = deger > 0 && deger >= limit.HedefDeger &&
               !double.IsNaN(icToplam) && icToplam <= icHedef.Value
            ? UyumDurumu.Gecti
            : UyumDurumu.Kaldi;
        return (uyum, limit);
    }

    private static double UygulanmisMarj(
        UyumDurumu uyum,
        AkustikLimit limit,
        double deger,
        bool isDoseme,
        bool isDisCephe,
        DisCepheSpektralSonuc? spektral,
        double? icHedef)
    {
        if (uyum == UyumDurumu.VeriEksik || limit.HedefDeger <= 0 || deger <= 0)
            return double.NaN;

        var marj = isDoseme ? limit.HedefDeger - deger : deger - limit.HedefDeger;
        if (isDisCephe && spektral != null && icHedef is { } hedef)
            marj = Math.Min(marj, hedef - spektral.IcToplam);
        return marj;
    }

}

public enum AkustikRaporSonucTuru
{
    HavaSesi,
    DisCephe,
    DarbeSesi,
    TesisatSurekli,
    TesisatKesikli,
    Reverberasyon,
}

/// Fiziksel tek bir bölmeye bağlı olmayan kanonik sonuçların proje aç/kaydet ve tekrar rapor
/// üretimleri arasında değişmeyen kimlikleri. ViewModel rapor seçim satırları oda-birleşik
/// cephe, tesisat ve reverberasyon için bu yardımcıları kullanmalıdır.
public static class RaporSonucIdleri
{
    private static readonly Guid DisCepheNamespace = new("db65d6d2-3bbf-4125-a629-cc48f9c0c593");
    private static readonly Guid TesisatSurekliNamespace = new("f64e676f-44f1-4c8b-9b6a-9b754265f4a1");
    private static readonly Guid TesisatKesikliNamespace = new("3a45ef60-6b9e-47f0-9bd1-baa290572d7c");
    private static readonly Guid ReverberasyonNamespace = new("d04f32c4-fcdd-4f7a-a437-7e9c4b087ab8");

    public static Guid DisCephe(Guid mekanId) =>
        KatlarArasiDarbeYoluServisi.DarbeYoluId(mekanId, DisCepheNamespace);

    public static Guid Tesisat(Guid mekanId, bool kesikli) =>
        KatlarArasiDarbeYoluServisi.DarbeYoluId(
            mekanId, kesikli ? TesisatKesikliNamespace : TesisatSurekliNamespace);

    public static Guid Reverberasyon(Guid mekanId) =>
        KatlarArasiDarbeYoluServisi.DarbeYoluId(mekanId, ReverberasyonNamespace);
}

public class RaporSatiri
{
    public Guid SonucId { get; set; }
    public AkustikRaporSonucTuru SonucTuru { get; set; }
    public string SonucTuruEtiketi => SonucTuru switch
    {
        AkustikRaporSonucTuru.HavaSesi => "Hava sesi",
        AkustikRaporSonucTuru.DisCephe => "Dış cephe",
        AkustikRaporSonucTuru.DarbeSesi => "Darbe sesi",
        AkustikRaporSonucTuru.TesisatSurekli => "Tesisat / sürekli",
        AkustikRaporSonucTuru.TesisatKesikli => "Tesisat / kesikli",
        AkustikRaporSonucTuru.Reverberasyon => "Reverberasyon",
        _ => SonucTuru.ToString(),
    };
    public string Kat          { get; set; } = "";
    public string Mekan        { get; set; } = "";
    public string KaynakMekan  { get; set; } = "";
    public string AliciMekan   { get; set; } = "";
    public double? HacimM3     { get; set; }
    public string Bolme        { get; set; } = "";
    public double Rw           { get; set; }
    public double Dntw         { get; set; }
    public double Lnw          { get; set; }
    public double Deger        { get; set; }
    public double Belirsizlik  { get; set; }
    public string Birim        { get; set; } = "";
    public double LimitDeger   { get; set; }
    public double MarjDb       { get; set; } = double.NaN;
    public bool DahaDusukDahaIyi { get; set; }
    public string YonetmelikEk { get; set; } = "";
    public string Durum        { get; set; } = "";
    public double? DisGurultuDb { get; set; }
    public double? IcGurultuDb  { get; set; }
    public double? IcHedefDb    { get; set; }
    public double? D2mNt50Db    { get; set; }
    public string SpektrumKaynak { get; set; } = "";
    public string VeriKaynagi { get; set; } = "";
    public int GuvenPuani { get; set; }
    public string GuvenSeviyesi { get; set; } = "";
    public string GuvenOzeti { get; set; } = "";
}
