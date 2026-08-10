using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Workflow;

namespace Kst.Acoustics.Core.BuildingAcoustics.Reports;

/// <summary>
/// "Mimari Akustik Raporu" — Binaların Gürültüye Karşı Korunması Hakkında Yönetmelik
/// (RG 31.05.2017/30082) Madde 5/5 içeriğine göre yapılandırılmış, yazdırılabilir
/// (tarayıcıdan PDF'e) tek dosyalık HTML raporu üretir.
///
/// Bölüm yapısı Madde 5/5 + Madde 16/1 + EK-7 + EK-10 10.2'den türetilmiştir:
/// kapak, giriş/kapsam, mekan analizi (EK-2), dış gürültü verileri (EK-7 7.1.1),
/// yapı elemanları listesi (Madde 16/1-ç, lab/model beyanı 16/1-a/b), hesap yöntemi
/// beyanı (16/1-c), sonuç tabloları (bölme DnT,A / cephe DnT,A,tr / darbe L'nT,w),
/// birleşim özeti, sınıf ön değerlendirmesi (EK-10 esinli) ve çözüm önerileri.
///
/// DİKKAT: Bu rapor HESAPLAMAYA dayalı bir ön değerlendirmedir; EK-10'daki resmi
/// Akustik Performans Belgesi tamamlanmış binada ölçüm gerektirir — rapor bunu
/// açıkça beyan eder.
/// </summary>
public class MimariAkustikRaporServisi
{
    private readonly BinaAkustikRaporServisi _tabloServisi = new();
    private readonly YonetmelikUyumServisi _uyum = new();
    private readonly SinifAtamaServisi _sinifServisi = new();
    private readonly CozumOnerisiServisi _oneriServisi = new();

    public string HtmlOlustur(BinaProje proje, ISet<Guid>? haricTutulanBolmeIdleri = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        proje = new BinaAkustikDegerlendirmeServisi()
            .Hesapla(proje, haricTutulanBolmeIdleri).HesaplananProje;
        var sb = new StringBuilder(64 * 1024);
        var sinifBilgi = BinaSinifiHelper.BilgiAl(proje.BinaSinifi);
        var hedefSinif = AkustikPerformansSinifiHelper.BilgiAl(proje.AkustikPerformansSinifi);
        var islev = BinaIsleviHelper.BilgiAl(proje.BinaIslevi);
        var raporAyari = proje.RaporAyarlari ?? new M08RaporAyarlari();
        var uretimZamani = raporAyari.YayinTarihi ?? DateTime.Now;
        var performansGecerlilikTarihi = raporAyari.BelgeGecerlilikTarihi ?? uretimZamani.Date.AddYears(10);
        var uygulanabilirlik = M08RaporUygulanabilirlikServisi.Degerlendir(proje, haricTutulanBolmeIdleri);
        if (raporAyari.YayinDurumu is M08RaporYayinDurumu.OnayaHazir or M08RaporYayinDurumu.Imzali &&
            uygulanabilirlik.YayinaEngelVar)
            throw new InvalidOperationException(
                $"{M08RaporUygulanabilirlikServisi.YayinDurumuAdi(raporAyari.YayinDurumu)} belge üretilemez: " +
                string.Join(" | ", uygulanabilirlik.Bulgular
                    .Where(x => x.Seviye == RaporOnKontrolSeviyesi.Engel)
                    .Select(x => x.Mesaj)));

        // HTML, metin ve Excel aynı kanonik sonuç koleksiyonunu kullanır. Böylece tesisat,
        // reverberasyon ve sentetik darbe seçimleri de bütün formatlarda aynı davranır.
        var tumSatirlar = _tabloServisi.TabloVerisiOlustur(proje, haricTutulanBolmeIdleri);
        var fizikselHaricIdleri = FizikselBolmeIdleriniGenislet(proje, haricTutulanBolmeIdleri);
        var icBolme = tumSatirlar.Where(s => s.SonucTuru == AkustikRaporSonucTuru.HavaSesi).ToList();
        var disCephe = tumSatirlar.Where(s => s.SonucTuru == AkustikRaporSonucTuru.DisCephe).ToList();
        var darbeSatirlari = tumSatirlar.Where(s => s.SonucTuru == AkustikRaporSonucTuru.DarbeSesi).ToList();
        var tesisatSatirlari = tumSatirlar.Where(s => s.SonucTuru is
            AkustikRaporSonucTuru.TesisatSurekli or AkustikRaporSonucTuru.TesisatKesikli).ToList();
        var reverberasyonSatirlari = tumSatirlar.Where(s =>
            s.SonucTuru == AkustikRaporSonucTuru.Reverberasyon).ToList();

        var darbeBolmeleri = new KatlarArasiDarbeYoluServisi()
            .DegerlendirilmisDarbeBolmeleri(proje)
            .Where(b => fizikselHaricIdleri.Contains(b.Id) != true)
            .ToList();

        var sinifSonucu = _sinifServisi.BinaSinifiDegerlendir(proje, fizikselHaricIdleri);

        sb.Append(HtmlBas(proje, uretimZamani));

        // ── 0. KAPAK ─────────────────────────────────────────────────────────
        sb.Append($"""
            <section class="kapak">
              <div class="kapak-ust">
                <div class="logo">EYE4PRO Acoustics</div>
                <h1>{H(BelgeBasligi(raporAyari.BelgeTuru).ToUpper(new CultureInfo("tr-TR")))}</h1>
                <div class="belge-durumu">{H(M08RaporUygulanabilirlikServisi.YayinDurumuAdi(raporAyari.YayinDurumu))}</div>
                <p class="alt-baslik">{H(BelgeAltBasligi(raporAyari.BelgeTuru))}</p>
              </div>
              {(raporAyari.BelgeTuru == M08RaporBelgeTuru.AkustikPerformansBelgesi ? BinaFotografiHtml(raporAyari.BinaFotografiYolu) : "")}
              <table class="kapak-tablo">
                <tr><th>Rapor numarası / revizyon</th><td>{H(Bos(raporAyari.RaporNumarasi))} / {H(Bos(raporAyari.RevizyonKodu))}</td></tr>
                <tr><th>Proje adı</th><td>{H(Bos(proje.ProjeAdi))}</td></tr>
                <tr><th>Proje adresi</th><td>{H(Bos(proje.Adres))}</td></tr>
                <tr><th>Ada / parsel / ruhsat</th><td>{H(Bos(raporAyari.Ada))} / {H(Bos(raporAyari.Parsel))} / {H(Bos(raporAyari.RuhsatNumarasi))}</td></tr>
                <tr><th>Müşteri / yapı sahibi</th><td>{H(Bos(proje.MusteriAdi))}</td></tr>
                <tr><th>Değerlendirmeden sorumlu kişi ve kuruluş</th><td>{H(Bos(proje.SorumluKisiKurulus))}</td></tr>
                <tr><th>Hazırlayan / unvan / yetki</th><td>{H(Bos(raporAyari.HazirlayanAdi))} / {H(Bos(raporAyari.HazirlayanUnvani))} / {H(Bos(raporAyari.HazirlayanYetkiNo))}</td></tr>
                <tr><th>Bina sınıfı / işlevi</th><td>{H(sinifBilgi.KodGosterim)} — {H(islev.Ad)}</td></tr>
                <tr><th>Hedeflenen akustik performans sınıfı</th><td>{H(hedefSinif.Kod)} ({H(hedefSinif.Aciklama)})</td></tr>
                <tr><th>Değerlendirilen bağımsız birim sayısı</th><td>{proje.BagimsizBolumler.Count}</td></tr>
                <tr><th>Kat / mekan sayısı</th><td>{proje.Katlar.Count} kat · {proje.Katlar.Sum(k => k.Mekanlar.Count)} mekan</td></tr>
                <tr><th>Rapor tarihi</th><td>{uretimZamani:dd.MM.yyyy}</td></tr>
                {(raporAyari.BelgeTuru == M08RaporBelgeTuru.AkustikPerformansBelgesi ? $"<tr><th>Belge geçerlilik tarihi</th><td>{performansGecerlilikTarihi:dd.MM.yyyy}</td></tr>" : "")}
                <tr><th>Mevzuat sürümü</th><td>{H(Bos(raporAyari.MevzuatSurumu))}</td></tr>
              </table>
              <div class="imza-alani">
                <div class="imza"><div class="cizgi"></div>Hazırlayan: {H(Bos(raporAyari.HazirlayanAdi))}</div>
                <div class="imza"><div class="cizgi"></div>Kontrol eden: {H(Bos(raporAyari.KontrolEdenAdi))}</div>
                <div class="imza"><div class="cizgi"></div>Onaylayan: {H(Bos(raporAyari.OnaylayanAdi))}</div>
              </div>
            </section>
            <div class="sayfa-kes"></div>
            """);

        sb.Append(BelgeKontrolBolumu(proje, uygulanabilirlik, haricTutulanBolmeIdleri, uretimZamani));

        // ── 1. GİRİŞ VE KAPSAM ───────────────────────────────────────────────
        sb.Append($"""
            <section>
              <h2>1. Giriş ve Kapsam</h2>
              <p>Bu {H(BelgeBasligi(raporAyari.BelgeTuru))}, {H(Bos(proje.ProjeAdi))} projesi için
              Binaların Gürültüye Karşı Korunması Hakkında Yönetmelik kapsamında hazırlanmıştır.
              Rapor; mekânların gürültüye hassasiyet ve gürültülülük yönünden
              ilişkilerinin analizini, ses iletim yollarını (hava doğuşlu ve darbe sesi), hedeflenen
              akustik performans sınıfına göre uygulanacak sınır değerleri ve bu değerlere göre
              belirlenen yapı elemanlarını içerir.</p>
              {BelgeBeyani(raporAyari.BelgeTuru)}
              <p>Yeni yapılacak binalarda Yönetmelik Madde 11/3 uyarınca en az C akustik performans
              sınıfı sağlanır. Bu projede hedeflenen sınıf: <strong>{H(hedefSinif.Kod)}</strong>.
              Tasarımcı tercihine bağlı olarak hesaplar hedef sınır değerlerin 2 dB üstüne göre
              yapılabilir (EK-7 7.1.4/1).</p>
            </section>
            """);

        // ── 2. MEKAN ANALİZİ (EK-2) ──────────────────────────────────────────
        sb.Append($"""
            <section>
              <h2>2. Mekan Analizi — Gürültülülük ve Hassasiyet Dereceleri (EK-2 Tablo 2.1)</h2>
              <p>Mekanların gürültü kaynağı olmaları durumunda gürültülülük dereceleri (YG: yüksek,
              OG: orta, DG: düşük) ve alıcı olmaları durumunda hassasiyet dereceleri (I: çok hassas,
              II: hassas, III: az hassas) EK-2 Tablo 2.1'e göre belirlenmiştir.</p>
              <table>
                <thead><tr><th>Kat</th><th>Mekan</th><th>Kullanım</th><th>Kaynak derecesi</th>
                <th>Alıcı sınıfı</th><th>Bağımsız bölüm</th><th>Alan (m²)</th><th>Hacim (m³)</th></tr></thead>
                <tbody>
            """);
        foreach (var kat in proje.Katlar)
        {
            foreach (var m in kat.Mekanlar)
            {
                var bolumAd = m.BagimsizBolumId is { } bid
                    ? proje.BagimsizBolumler.FirstOrDefault(b => b.Id == bid)?.Ad ?? "—"
                    : "—";
                sb.Append($"<tr><td>{H(KatAdi(kat))}</td><td>{H(m.Ad)}</td><td>{H(Bos(m.KullanimTipi))}</td>" +
                          $"<td>{H(Bos(m.KaynakDerecesi))}</td><td>{H(Bos(m.AliciSinifi))}</td>" +
                          $"<td>{H(bolumAd)}</td><td>{m.Alan:F1}</td><td>{m.Hacim:F0}</td></tr>");
            }
        }
        sb.Append("</tbody></table></section>");

        // ── 3. DIŞ GÜRÜLTÜ VERİLERİ (EK-7 7.1.1) ─────────────────────────────
        var spektrumKaynagi = proje.DisGurultuThirdOctave.Count >= 19
            ? $"{BinaAkustikRaporServisi.KaynakEtiketi(proje.DisGurultuSpektrumKaynagi.ToString())} 1/3 oktav spektrum (50–3150 Hz)"
            : "Varsayılan normalize trafik spektrumu (Lgag değerine kalibre edilmiş)";
        sb.Append($"""
            <section>
              <h2>3. Dış Gürültü Verileri (EK-7 7.1.1)</h2>
              <table class="ozet-tablo">
                <tr><th>Dış gürültü düzeyi (Lgag/Lden)</th><td>{proje.DisGurultuDb:F0} dBA</td></tr>
                <tr><th>Spektrum kaynağı</th><td>{H(spektrumKaynagi)}</td></tr>
                <tr><th>Belirleme yöntemi / veri tarihi</th><td>{H(Bos(raporAyari.DisGurultuYontemi))} / {(raporAyari.DisGurultuVeriTarihi is { } d ? d.ToString("dd.MM.yyyy") : "—")}</td></tr>
                <tr><th>Konum / kaynak belge</th><td>{H(Bos(raporAyari.DisGurultuKonumu))} / {H(Bos(raporAyari.DisGurultuKaynakBelgesi))}</td></tr>
                <tr><th>Cephe formu (EN 12354-3 Annex C)</th><td>{H(proje.DisGurultuCepheMaruziyeti.ToString())} (ΔL<sub>fs</sub> = {DisGurultuServisi.DeltaLfs(proje.DisGurultuCepheMaruziyeti):F0} dB)</td></tr>
                <tr><th>Alıcı mekan iç gürültü hedefi</th><td>Mekan işlevine göre EK-4 Tablo 4.1 (L<sub>Aeq</sub>; eşleşme yoksa sonuç “Veri Eksik”, sabit 35 dBA varsayımı uygulanmaz)</td></tr>
              </table>
              <p class="not">Dış gürültü düzeyinin kaynağı (ölçüm, gürültü haritası veya hesap) proje
              müellifince beyan edilmelidir; Lgag cephe yansımaları hariç, cephenin 2 m önünde tanımlıdır.</p>
            </section>
            """);

        // ── 4. SINIR DEĞERLERİN BELİRLENMESİ ────────────────────────────────
        sb.Append($"""
            <section>
              <h2>4. Uygulanan Sınır Değerler (Madde 11, EK-3)</h2>
              <p>Sınır değerler hedeflenen {H(hedefSinif.Kod)} sınıfı için EK-3 tablolarından alınmıştır:
              dış cepheler Tablo 3.1 (L<sub>gag</sub> − ofset, A–D sınıflarında 30 dB taban), bölme
              elemanları Tablo 3.2 (kaynak derecesi × alıcı sınıfı){(proje.BinaSinifi == BinaSinifi.S1 ? " — konutta aynı bağımsız birim içi bölmeler Tablo 3.4" : "")},
              döşemeler Tablo 3.3{(proje.BinaSinifi == BinaSinifi.S1 ? " / aynı birim içi Tablo 3.5" : "")}.
              Kapı içeren bölme elemanlarında Madde 11/3 uyarınca sınır değerden en fazla 14 dB düşük
              yalıtıma izin verilir; uygulanan satırlar sonuç tablolarındaki limit açıklamalarında belirtilmiştir.
              Her sonuç satırındaki limit, o satırda kullanılan tablo referansıyla birlikte verilmiştir.</p>
            </section>
            """);

        // ── 5. YAPI ELEMANLARI LİSTESİ (Madde 16/1-ç) ───────────────────────
        sb.Append("""
            <section>
              <h2>5. Yapı Elemanları Listesi (Madde 16/1-ç)</h2>
              <p>Projede kullanılan yapı elemanları, akustik performans değerleri ve veri kaynağı
              beyanı (Madde 16/1-a/b: laboratuvar ölçümü veya bilimsel yöntemle model tahmini) aşağıdadır.</p>
              <table class="genis">
                <thead><tr><th>Eleman / ürün</th><th>Katman dizilimi</th><th>Kalınlık / yüzey kütlesi</th>
                <th>R<sub>w</sub> (C; C<sub>tr</sub>) dB</th><th>L<sub>n,w</sub> / ΔL<sub>w</sub></th>
                <th>Veri kaynağı</th><th>Belge</th></tr></thead>
                <tbody>
            """);
        foreach (var el in KullanilanElemanlar(proje))
        {
            var seciliKoken = el.Tip == YapiElemaniTipi.Doseme
                ? el.DarbeSpektrumKaynagi
                : el.SpektrumKaynagi;
            var hamKaynak = seciliKoken == SpektrumVeriKaynagi.Belirsiz && el.HasRealOktavData
                ? "Olcum"
                : seciliKoken.ToString();
            var kaynak = BinaAkustikRaporServisi.KaynakEtiketi(hamKaynak);
            var lnw = el.Tip == YapiElemaniTipi.Doseme ? $"{el.Lnw:F0} / Δ{el.DeltaLw:F0}" : "—";
            var katmanlar = el.Katmanlar.Count == 0
                ? "—"
                : string.Join(" + ", el.Katmanlar.Select(k => $"{k.MalzemeAdi} {k.Kalinlik:F0} mm"));
            var belge = el.Belgeler.OrderByDescending(b => b.YayinTarihi).FirstOrDefault();
            var belgeMetni = belge == null ? "—" :
                $"{Bos(belge.BelgeNo)} / {Bos(belge.VerenKurulus)} / {(belge.YayinTarihi is { } bt ? bt.ToString("dd.MM.yyyy") : "—")}";
            sb.Append($"<tr><td>{H(el.Ad)}<br><span class=\"kucuk\">{H(Bos(el.Uretici))} / {H(Bos(el.UrunKodu))} · {H(el.Tip.ToString())}</span></td>" +
                      $"<td>{H(katmanlar)}</td><td>{el.ToplamKalinlikMm:F0} mm / {el.ToplamYuzeyKutlesi:F1} kg/m²</td>" +
                      $"<td>{el.Rw:F0} ({el.C:F0}; {el.Ctr:F0}){(el.DeltaRDb > 0 ? $" · ΔR +{el.DeltaRDb:F0}" : "")}</td>" +
                      $"<td>{lnw}</td><td>{H(kaynak)}</td><td class=\"kucuk\">{H(belgeMetni)}</td></tr>");
        }
        sb.Append("""
                </tbody></table>
              <p class="not">Ses yalıtım detayları ve noktasal birleşim detay çizimleri, EK-7 7.1.4/2 ve
              7.3 uyarınca mimari/akustik proje paftalarında verilir; bu rapor hesaba esas birleşim
              parametrelerini Bölüm 12'de özetler.</p>
            </section>
            """);

        // ── 6. HESAP YÖNTEMİ BEYANI (Madde 16/1-c, EK-9) ────────────────────
        sb.Append($"""
            <section>
              <h2>6. Hesap Yöntemi Beyanı (Madde 16, EK-9)</h2>
              <ul>
                <li>Bina içi ses yayılımı ve yanal (flanking) iletim: <strong>TS EN 12354-1</strong>
                (hava doğuşlu, Dd + Df/Fd/Ff 13-yol modeli), <strong>TS EN 12354-2</strong> (darbe sesi),
                <strong>TS EN 12354-3</strong> (dış gürültünün içeri iletimi, cephe formu ΔL<sub>fs</sub>
                Annex C).</li>
                <li>Birleşim titreşim azalım indeksleri K<sub>ij</sub>: TS EN 12354-1 Annex E
                (rijit T/X/L formülleri, hafif ve sandviç eleman düzeltmeleri); in-situ eşdeğer
                soğurma uzunluğu dönüşümü ve D̄<sub>v,ij</sub> ≥ 0 sınırlaması uygulanmıştır.</li>
                <li>Tek sayılı büyüklükler: <strong>TS EN ISO 717-1</strong> (R<sub>w</sub>, C, C<sub>tr</sub>)
                ve <strong>TS EN ISO 717-2</strong> (L<sub>n,w</sub>) referans eğri yöntemleri;
                değerlendirme büyüklükleri D<sub>nT,A</sub>, D<sub>nT,A,tr</sub>, L'<sub>nT,w</sub>
                (Madde 10).</li>
                <li>Hesap belirsizliği: ±{HavaSesiHesapServisi.HesapBelirsizligi:F0} dB
                (TS EN 12354-1 Tablo B.1 mertebesi); darbe için ±{DarbeSesiHesapServisi.HesapBelirsizligi:F0} dB.</li>
                <li>Yazılım: EYE4PRO Acoustics — M08 Bina Gürültü Hesabı modülü.</li>
              </ul>
            </section>
            <div class="sayfa-kes"></div>
            """);

        // ── 7. İÇ BÖLME SONUÇLARI ────────────────────────────────────────────
        sb.Append(TopluSonucMatrisi(tumSatirlar));
        sb.Append(SonucTablosu("7. Hava Doğuşlu Ses Yalıtımı — İç Bölme Elemanları (DnT,A)", icBolme));

        // ── 8. DIŞ CEPHE SONUÇLARI ──────────────────────────────────────────
        sb.Append("""
            <section>
              <h2>8. Hava Doğuşlu Ses Yalıtımı — Dış Yapı Elemanları (DnT,A,tr)</h2>
              <table>
                <thead><tr><th>Kat</th><th>Mekan</th><th>Eleman</th><th>Hesaplanan (dB)</th>
                <th>Limit (dB)</th><th>İç gürültü (dBA)</th><th>D<sub>2m,nT,50</sub></th>
                <th>Durum</th><th>Dayanak</th></tr></thead>
                <tbody>
            """);
        foreach (var s in disCephe)
        {
            var limitMetni = s.LimitDeger > 0 ? $"≥ {s.LimitDeger:F0}" : "—";
            sb.Append($"<tr><td>{H(s.Kat)}</td><td>{H(s.Mekan)}</td><td>{H(s.Bolme)}</td>" +
                      $"<td>{s.Deger:F1} ±{s.Belirsizlik:F0}</td><td>{limitMetni}</td>" +
                      $"<td>{(s.IcGurultuDb is { } ic ? $"{ic:F1} / ≤{s.IcHedefDb:F0}" : "—")}</td>" +
                      $"<td>{(s.D2mNt50Db is { } d2 ? $"{d2:F1}" : "—")}</td>" +
                      $"<td class=\"{DurumSinif(s.Durum)}\">{H(DurumMetni(s.Durum))}</td><td class=\"kucuk\">{H(s.YonetmelikEk)}</td></tr>");
        }
        if (disCephe.Count == 0)
            sb.Append("<tr><td colspan=\"9\" class=\"bos\">Değerlendirilecek dış cephe elemanı bulunmuyor.</td></tr>");
        sb.Append("</tbody></table></section>");

        // ── 9. DARBE SESİ SONUÇLARI ─────────────────────────────────────────
        sb.Append("""
            <section>
              <h2>9. Darbe Sesi Yalıtımı — Döşemeler (L'nT,w)</h2>
              <table>
                <thead><tr><th>Kaynak mekan</th><th>Alıcı mekan</th><th>Döşeme</th>
                <th>L'<sub>nT,w</sub> (dB)</th><th>Limit (dB)</th><th>Marj</th><th>Durum</th><th>Dayanak</th></tr></thead>
                <tbody>
            """);
        foreach (var s in darbeSatirlari)
        {
            sb.Append($"<tr><td>{H(Bos(s.KaynakMekan))}</td><td>{H(Bos(s.AliciMekan))}</td>" +
                      $"<td>{H(s.Bolme)}</td><td>{s.Deger:F1} ±{s.Belirsizlik:F0}</td>" +
                      $"<td>≤ {s.LimitDeger:F0}</td><td>{(double.IsNaN(s.MarjDb) ? "—" : $"{s.MarjDb:+0.0;-0.0}")}</td>" +
                      $"<td class=\"{DurumSinif(s.Durum)}\">{H(DurumMetni(s.Durum))}</td><td class=\"kucuk\">{H(s.YonetmelikEk)}</td></tr>");
        }
        if (darbeSatirlari.Count == 0)
            sb.Append("<tr><td colspan=\"8\" class=\"bos\">Katlar arası değerlendirilebilir darbe yolu bulunmuyor (tek katlı proje veya döşeme ataması eksik).</td></tr>");
        sb.Append("</tbody></table></section>");

        // ── 10. TESİSAT GÜRÜLTÜSÜ (EK-5) ────────────────────────────────────
        sb.Append(TesisatBolumu(tesisatSatirlari));

        // ── 11. REVERBERASYON (EK-6) ────────────────────────────────────────
        sb.Append(ReverberasyonBolumu(reverberasyonSatirlari));

        // ── 12. BİRLEŞİM ÖZETİ ──────────────────────────────────────────────
        sb.Append(JunctionOzeti(proje, fizikselHaricIdleri));

        // ── 13. SINIF ÖN DEĞERLENDİRMESİ (EK-10 esinli) ─────────────────────
        sb.Append(raporAyari.BelgeTuru == M08RaporBelgeTuru.AkustikPerformansBelgesi
            ? OlcumPerformansSinifiBolumu(proje, fizikselHaricIdleri, uygulanabilirlik)
            : SinifBolumu(sinifSonucu, hedefSinif));

        // ── 14. ÇÖZÜM ÖNERİLERİ ─────────────────────────────────────────────
        sb.Append(OneriBolumu(proje, fizikselHaricIdleri, darbeBolmeleri));

        // ── 15–17. DOĞRULAMA, METRAJ VE İZLENEBİLİRLİK ─────────────────────
        sb.Append(OlcumDogrulamaBolumu(proje));
        sb.Append(MetrajBolumu(proje));
        sb.Append(OnayVeIzlenebilirlikBolumu(proje));

        // ── 18. EK: STANDART LİSTESİ ────────────────────────────────────────
        sb.Append($"""
            <section>
              <h2>18. Ek — Kullanılan Standartlar (EK-9'un ilgili alt kümesi)</h2>
              <table><thead><tr><th>Standart</th><th>Rapordaki rolü</th><th>Sürüm/tarih durumu</th></tr></thead><tbody>
                <tr><td>TS EN 12354-1/-2/-3</td><td>Hava sesi, darbe sesi, dış gürültü ve yanal iletim hesabı</td><td>{H(Bos(raporAyari.HesapStandartlariSurumu))}</td></tr>
                <tr><td>TS EN ISO 717-1/-2</td><td>Tek sayılı hava ve darbe sesi derecelendirmesi</td><td>{H(Bos(raporAyari.DerecelendirmeStandartlariSurumu))}</td></tr>
                <tr><td>TS EN ISO 16283-1/-2/-3</td><td>Saha ses yalıtımı ölçümleri</td><td>{H(Bos(raporAyari.OlcumStandartlariSurumu))}</td></tr>
                <tr><td>TS EN ISO 10052 / 16032</td><td>Servis ekipmanı ölçümleri</td><td>{H(Bos(raporAyari.ServisOlcumStandartlariSurumu))}</td></tr>
                <tr><td>ISO 12999-1</td><td>Ölçüm belirsizliği</td><td>{H(Bos(raporAyari.BelirsizlikStandardiSurumu))}</td></tr>
              </tbody></table>
              <p class="uyari-kutu">{H(Bos(raporAyari.StandartSurumNotu))}</p>
              <p class="not">Oktav bant spektrumları ve ayrıntılı hesap dökümü için Excel raporu
              ("Oktav Bantlar" sayfası) ile renkli DXF plan çıktısı bu raporun ekleri olarak üretilebilir.</p>
            </section>
            """);

        sb.Append(HtmlSon(proje, uretimZamani));
        return sb.ToString();
    }

    // ─── Bölüm üreticileri ────────────────────────────────────────────────────

    private static string BelgeKontrolBolumu(
        BinaProje proje,
        M08RaporUygulanabilirlikSonucu kontrol,
        ISet<Guid>? haric,
        DateTime uretimZamani)
    {
        var a = proje.RaporAyarlari;
        var tum = new BinaAkustikRaporServisi().TabloVerisiOlustur(proje);
        var dahil = new BinaAkustikRaporServisi().TabloVerisiOlustur(proje, haric);
        var snapshot = AkustikRaporSnapshotServisi.Olustur(
            proje, haric, new DateTimeOffset(uretimZamani).ToUniversalTime());
        var sb = new StringBuilder($"""
            <section>
              <h2>A. Belge Kontrolü ve Uygulanabilirlik Kararı</h2>
              <table class="ozet-tablo">
                <tr><th>Belge türü / yayın durumu</th><td>{H(BelgeBasligi(a.BelgeTuru))} / {H(M08RaporUygulanabilirlikServisi.YayinDurumuAdi(a.YayinDurumu))}</td></tr>
                <tr><th>Rapor no / revizyon / tarih</th><td>{H(Bos(a.RaporNumarasi))} / {H(Bos(a.RevizyonKodu))} / {uretimZamani:dd.MM.yyyy}</td></tr>
                <tr><th>Yapı durumu</th><td>{H(M08RaporUygulanabilirlikServisi.YapiDurumuAdi(a.YapiDurumu))}</td></tr>
                <tr><th>Madde 5/6 kapsama esas verileri</th><td>{(a.KapsamaEsasKatSayisi > 0 ? a.KapsamaEsasKatSayisi : "—")} kat / {(a.ToplamInsaatAlaniM2 > 0 ? $"{a.ToplamInsaatAlaniM2:N0} m²" : "—")}</td></tr>
                <tr><th>Akustik proje kararı</th><td>{(kontrol.AkustikProjeGerekli ? "GEREKLİ" : kontrol.AkustikProjeKarariKesin ? "GEREKLİ DEĞİL" : "VERİ EKSİK — KESİNLEŞMEDİ")}</td></tr>
                <tr><th>Mimari / mekanik proje revizyonu</th><td>{H(Bos(a.MimariProjeRevizyonu))} / {H(Bos(a.MekanikProjeRevizyonu))}</td></tr>
                <tr><th>Sonuç kapsamı</th><td>{dahil.Count} dahil / {Math.Max(0, tum.Count - dahil.Count)} kapsam dışı / {dahil.Count(x => x.Durum == nameof(UyumDurumu.VeriEksik))} veri eksik</td></tr>
                <tr><th>Rapor içerik parmak izi</th><td class="kucuk">{H(snapshot.RaporParmakIziSha256)}</td></tr>
              </table>
            """);
        if (kontrol.AkustikProjeGerekceleri.Count > 0)
        {
            sb.Append("<h3>Akustik proje gereklilik gerekçeleri</h3><ul>");
            foreach (var gerekce in kontrol.AkustikProjeGerekceleri)
                sb.Append($"<li>{H(gerekce)}</li>");
            sb.Append("</ul>");
        }
        sb.Append("<h3>Yayın ön kontrolü</h3>");
        if (kontrol.Bulgular.Count == 0)
            sb.Append("<p class=\"gecti kutu\">Yayın ön kontrolünde bulgu yok.</p>");
        else
        {
            sb.Append("<table><thead><tr><th>Seviye</th><th>Kod</th><th>Bulgu / gerekli işlem</th></tr></thead><tbody>");
            foreach (var b in kontrol.Bulgular)
            {
                var css = b.Seviye == RaporOnKontrolSeviyesi.Engel ? "kaldi" :
                    b.Seviye == RaporOnKontrolSeviyesi.Uyari ? "eksik" : "";
                sb.Append($"<tr><td class=\"{css}\">{H(b.Seviye.ToString().ToUpperInvariant())}</td>" +
                          $"<td class=\"kucuk\">{H(b.Kod)}</td><td>{H(b.Mesaj)}</td></tr>");
            }
            sb.Append("</tbody></table>");
        }

        sb.Append("<h3>Kapsam dışı bırakılan değerlendirmeler</h3><table><thead><tr><th>Sonuç ID</th><th>Sonuç</th><th>Gerekçe</th><th>Kullanıcı / tarih</th></tr></thead><tbody>");
        var haricler = haric ?? new HashSet<Guid>();
        var kayitlar = proje.RaporKapsamDisiKayitlari.ToDictionary(x => x.SonucId);
        foreach (var satir in tum.Where(x => haricler.Contains(x.SonucId)))
        {
            kayitlar.TryGetValue(satir.SonucId, out var kayit);
            sb.Append($"<tr><td class=\"kucuk\">{H(satir.SonucId.ToString("D"))}</td>" +
                      $"<td>{H(satir.Kat)} / {H(satir.Mekan)} / {H(satir.Bolme)}</td>" +
                      $"<td class=\"{(string.IsNullOrWhiteSpace(kayit?.Gerekce) ? "eksik" : "")}\">{H(Bos(kayit?.Gerekce))}</td>" +
                      $"<td>{H(Bos(kayit?.Kullanici))} / {(kayit is null ? "—" : kayit.Tarih.ToString("dd.MM.yyyy"))}</td></tr>");
        }
        if (haricler.Count == 0)
            sb.Append("<tr><td colspan=\"4\" class=\"bos\">Kapsam dışı bırakılan değerlendirme yok.</td></tr>");
        return sb.Append("</tbody></table></section>").ToString();
    }

    private static string BelgeBasligi(M08RaporBelgeTuru tur) =>
        M08RaporUygulanabilirlikServisi.BelgeTuruAdi(tur);

    private static string BelgeAltBasligi(M08RaporBelgeTuru tur) => tur switch
    {
        M08RaporBelgeTuru.MimariAkustikRaporu => "Yönetmelik Madde 5/5 kapsamında hesaplamaya dayalı tasarım raporu",
        M08RaporBelgeTuru.AkustikProjeRaporPaketi => "Yönetmelik Madde 5/6 kapsamında pafta, detay ve hesaplarla birlikte akustik proje rapor paketi",
        M08RaporBelgeTuru.SahaOlcumRaporu => "Yapım/uygulama sonrası doğrulanmış saha ölçümleri raporu",
        _ => "Yönetmelik EK-10 kapsamında ölçüme dayalı akustik performans değerlendirmesi",
    };

    private static string BelgeBeyani(M08RaporBelgeTuru tur) => tur switch
    {
        M08RaporBelgeTuru.MimariAkustikRaporu or M08RaporBelgeTuru.AkustikProjeRaporPaketi =>
            "<div class=\"uyari-kutu\"><strong>Belge niteliği:</strong> Bu çıktı hesaplamaya dayalı tasarım değerlendirmesidir. Tamamlanmış binada ölçümle doğrulanmış Akustik Performans Belgesi değildir.</div>",
        M08RaporBelgeTuru.SahaOlcumRaporu =>
            "<div class=\"uyari-kutu\"><strong>Belge niteliği:</strong> Bu çıktı doğrulanmış saha ölçümlerini içerir; EK-10 kapsamı ve örneklemesi tamamlanmadıkça Akustik Performans Belgesi değildir.</div>",
        _ =>
            "<div class=\"uyari-kutu\"><strong>Ölçüm tabanlı belge:</strong> Performans sınıfı yalnız doğrulanmış, raporlamaya onaylı ve EK-10 örneklemesini sağlayan ölçümlerden türetilebilir. Eksik özelliklerde “performans belirlenemedi” yazılır.</div>",
    };

    private static string TopluSonucMatrisi(IReadOnlyList<RaporSatiri> satirlar)
    {
        var sb = new StringBuilder("""
            <section class="yatay-sayfa">
              <h2>6A. Toplu Uygunluk ve İzlenebilirlik Matrisi</h2>
              <p class="not">Bütün sonuç türleri aynı karar şemasıyla gösterilir. “Tasarım kriterini
              sağlıyor” ifadesi hesap sonucudur; ölçümle doğrulanmış bina sınıfı anlamına gelmez.</p>
              <table class="genis"><thead><tr><th>Sonuç ID</th><th>Kat</th><th>Kaynak → alıcı</th>
              <th>Eleman / kriter</th><th>Gösterge / aralık</th><th>Sonuç ±σ</th><th>Limit / marj</th>
              <th>Durum</th><th>Veri kaynağı</th><th>Yönetmelik</th></tr></thead><tbody>
            """);
        foreach (var s in satirlar)
        {
            var yon = s.DahaDusukDahaIyi ? "≤" : "≥";
            var limit = s.LimitDeger > 0 ? $"{yon} {s.LimitDeger:F1}" : "—";
            var marj = double.IsFinite(s.MarjDb) ? $"{s.MarjDb:+0.0;-0.0;0.0}" : "—";
            var iliski = !string.IsNullOrWhiteSpace(s.AliciMekan)
                ? $"{Bos(s.KaynakMekan)} → {Bos(s.AliciMekan)}"
                : Bos(s.Mekan);
            sb.Append($"<tr><td class=\"kucuk\">{H(KisaIz(s.SonucId.ToString("D")))}</td><td>{H(s.Kat)}</td>" +
                      $"<td>{H(iliski)}</td><td>{H(Bos(s.Bolme))}</td><td>{H(GostergeVeAralik(s.SonucTuru))}</td>" +
                      $"<td>{s.Deger:F1} ±{s.Belirsizlik:F1} {H(s.Birim)}</td><td>{limit} / {marj}</td>" +
                      $"<td class=\"{DurumSinif(s.Durum)}\">{H(DurumMetni(s.Durum))}</td>" +
                      $"<td class=\"kucuk\">{H(Bos(s.VeriKaynagi))} · güven {s.GuvenPuani}/100</td>" +
                      $"<td class=\"kucuk\">{H(Bos(s.YonetmelikEk))}</td></tr>");
        }
        if (satirlar.Count == 0)
            sb.Append("<tr><td colspan=\"10\" class=\"bos\">Rapora dahil edilmiş sonuç bulunmuyor.</td></tr>");
        return sb.Append("</tbody></table></section>").ToString();
    }

    private static string GostergeVeAralik(AkustikRaporSonucTuru tur) => tur switch
    {
        AkustikRaporSonucTuru.HavaSesi => "DnT,A · 100–3150 Hz",
        AkustikRaporSonucTuru.DisCephe => "DnT,A,tr / D2m,nT,50 · 50–3150 Hz",
        AkustikRaporSonucTuru.DarbeSesi => "L'nT,w · 100–3150 Hz",
        AkustikRaporSonucTuru.TesisatSurekli => "LAeq,nT · 63–8000 Hz",
        AkustikRaporSonucTuru.TesisatKesikli => "LAF,max,nT · 63–8000 Hz",
        AkustikRaporSonucTuru.Reverberasyon => "T · 250–2000 Hz ort.",
        _ => tur.ToString(),
    };

    private string SonucTablosu(string baslik, List<RaporSatiri> satirlar)
    {
        var sb = new StringBuilder();
        sb.Append($"""
            <section>
              <h2>{H(baslik)}</h2>
              <table>
                <thead><tr><th>Kat</th><th>Mekan</th><th>Eleman</th><th>Hesaplanan (dB)</th>
                <th>Limit (dB)</th><th>Marj</th><th>Güven</th><th>Durum</th><th>Dayanak</th></tr></thead>
                <tbody>
            """);
        foreach (var s in satirlar)
        {
            sb.Append($"<tr><td>{H(s.Kat)}</td><td>{H(s.Mekan)}</td><td>{H(s.Bolme)}</td>" +
                      $"<td>{s.Deger:F1} ±{s.Belirsizlik:F0}</td><td>≥ {s.LimitDeger:F0}</td>" +
                      $"<td>{(double.IsNaN(s.MarjDb) ? "—" : $"{s.MarjDb:+0.0;-0.0}")}</td>" +
                      $"<td>{s.GuvenPuani}/100</td>" +
                      $"<td class=\"{DurumSinif(s.Durum)}\">{H(DurumMetni(s.Durum))}</td><td class=\"kucuk\">{H(s.YonetmelikEk)}</td></tr>");
        }
        if (satirlar.Count == 0)
            sb.Append("<tr><td colspan=\"9\" class=\"bos\">Değerlendirilecek eleman bulunmuyor.</td></tr>");
        sb.Append("</tbody></table></section>");
        return sb.ToString();
    }

    private static string TesisatBolumu(IReadOnlyList<RaporSatiri> satirlar)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <section>
              <h2>10. Tesisat ve Servis Ekipmanı Gürültüsü (EK-5)</h2>
              <p>Beyan edilen tesisat gürültü düzeyleri EK-5 sınırlarıyla karşılaştırılmıştır:
              sürekli ekipman Tablo 5.1 (L<sub>Aeq,nT</sub>), kesikli ekipman Tablo 5.2
              (L<sub>AF,max,nT</sub>). Düzey beyanı olmayan mekanlar değerlendirilmemiştir.</p>
              <table>
                <thead><tr><th>Kat</th><th>Mekan</th><th>Tür</th><th>Beyan (dBA)</th>
                <th>Limit (dBA)</th><th>Marj</th><th>Durum</th><th>Dayanak</th></tr></thead>
                <tbody>
            """);
        foreach (var s in satirlar)
        {
            var limitStr = s.Durum == nameof(UyumDurumu.VeriEksik) ? "—" : $"≤ {s.LimitDeger:F0}";
            var marjStr = double.IsNaN(s.MarjDb) ? "—" : $"{s.MarjDb:+0.0;-0.0}";
            sb.Append($"<tr><td>{H(s.Kat)}</td><td>{H(s.Mekan)}</td><td>{H(s.SonucTuruEtiketi)}</td>" +
                      $"<td>{s.Deger:F0}</td><td>{limitStr}</td><td>{marjStr}</td>" +
                      $"<td class=\"{DurumSinif(s.Durum)}\">{H(DurumMetni(s.Durum))}</td>" +
                      $"<td class=\"kucuk\">{H(s.YonetmelikEk)}</td></tr>");
        }
        if (satirlar.Count == 0)
            sb.Append("<tr><td colspan=\"8\" class=\"bos\">Tesisat gürültü düzeyi beyan edilen mekan bulunmuyor.</td></tr>");
        sb.Append("""
                </tbody></table>
              <p class="not">EK-8'deki mekanik sistem gürültü kontrol kuralları (titreşim izolasyonu,
              esnek bağlantı, susturucu vb.) nitel tasarım kurallarıdır; uygulama mekanik proje
              kapsamında belgelenir.</p>
            </section>
            """);
        return sb.ToString();
    }

    private static string ReverberasyonBolumu(IReadOnlyList<RaporSatiri> satirlar)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <section>
              <h2>11. Reverberasyon Süresi Ön Değerlendirmesi (EK-6)</h2>
              <p>Tablo 6.1 sınır değerleri (250–2000 Hz aritmetik ortalaması) ile Sabine ön tahmini
              (T = 0,161·V/A; A = beyan edilen emilim alanı) karşılaştırılmıştır. Emilim alanı
              beyan edilmemiş tahminler veri kökeni ve düşük güven notuyla işaretlenir. Tablo değerleri C-D sınıfları için geçerlidir; diğer sınıflarda
              akustik uzman kararı gerekir (dipnot 2). "Özel tasarım" hacimlerinde sayısal sınır
              tanımlı değildir (dipnot 5). Ayrıntılı iç hacim akustiği için M01 modülü kullanılmalıdır.</p>
              <table>
                <thead><tr><th>Kat</th><th>Mekan</th><th>Hacim (m³)</th><th>Tahmini T (s)</th>
                <th>T<sub>max</sub> (s)</th><th>Durum</th><th>Dayanak</th></tr></thead>
                <tbody>
            """);
        foreach (var s in satirlar)
        {
            var tmaxStr = s.LimitDeger > 0 ? $"≤ {s.LimitDeger:F1}" : "özel tasarım";
            sb.Append($"<tr><td>{H(s.Kat)}</td><td>{H(s.Mekan)}</td><td>{s.HacimM3:F0}</td>" +
                      $"<td>{(s.Deger > 0 ? $"{s.Deger:F2}" : "—")}</td><td>{tmaxStr}</td>" +
                      $"<td class=\"{DurumSinif(s.Durum)}\">{H(DurumMetni(s.Durum))}</td>" +
                      $"<td class=\"kucuk\">{H($"{s.YonetmelikEk} — {s.VeriKaynagi}; {s.GuvenOzeti}")}</td></tr>");
        }
        if (satirlar.Count == 0)
            sb.Append("<tr><td colspan=\"7\" class=\"bos\">Tablo 6.1'de karşılığı olan mekan bulunmuyor.</td></tr>");
        sb.Append("</tbody></table></section>");
        return sb.ToString();
    }

    private static string JunctionOzeti(BinaProje proje, ISet<Guid>? haric)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <section>
              <h2>12. Birleşim (Junction) Parametreleri Özeti (Madde 5/5)</h2>
              <p>Yanal iletim hesabına esas birleşim parametreleri aşağıdadır; noktasal birleşim
              detay çizimleri mimari/akustik proje paftalarında verilir (EK-7 7.1.4/2).</p>
              <table>
                <thead><tr><th>Mekan</th><th>Eleman</th><th>Kenar</th><th>Geometri</th>
                <th>Bağlantı / strüktür</th><th>Uzunluk</th><th>K<sub>ij</sub> / kaynak</th><th>Detay</th></tr></thead>
                <tbody>
            """);
        int say = 0;
        foreach (var kat in proje.Katlar)
            foreach (var m in kat.Mekanlar)
                foreach (var b in m.Bolmeler)
                {
                    if (haric?.Contains(b.Id) == true) continue;
                    foreach (var j in b.Junctions)
                    {
                        sb.Append($"<tr><td>{H(m.Ad)}</td><td>{H(b.Ad)}</td><td>{H(j.Kenar.ToString())}</td>" +
                                  $"<td>{H(j.GeometriTipi.ToString())}</td><td>{H(j.BaglantiTipi.ToString())} / {H(j.StrukturelTipi.ToString())}</td>" +
                                  $"<td>{j.Uzunluk:F2} m</td><td>{j.KijDb:F1} dB / {(j.KijOverrideDb > 0 ? "kullanıcı değeri" : "model/varsayılan")}</td>" +
                                  $"<td class=\"{(string.IsNullOrWhiteSpace(j.DetayReferansi) ? "eksik" : "")}\">{H(Bos(j.DetayReferansi))}" +
                                  $"{(string.IsNullOrWhiteSpace(j.UygulamaNotu) ? "" : $"<br><span class=\"kucuk\">{H(j.UygulamaNotu)}</span>")}</td></tr>");
                        say++;
                    }
                }
        if (say == 0)
            sb.Append("<tr><td colspan=\"8\" class=\"bos\">Kullanıcı tanımlı birleşim parametresi yok; hesapta Annex E varsayılan rijit birleşimler kullanılmıştır.</td></tr>");
        sb.Append("</tbody></table></section>");
        return sb.ToString();
    }

    private static string SinifBolumu(SinifAtamaServisi.BinaSinifSonucu sonuc, AkustikPerformansSinifiInfo hedef)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <section>
              <h2>13. Akustik Performans Sınıfı Ön Değerlendirmesi (EK-10 ilkeleriyle)</h2>
              <p>Aşağıdaki sınıflar hesap sonuçlarından türetilmiş <em>ön değerlendirmedir</em>;
              resmi sınıf ataması tamamlanmış binada ölçüm gerektirir (EK-10 10.1/4). Farklı
              özellikler farklı sınıflar gösterdiğinde en alt performans sınıfı atanır (EK-10/9).
              Değerlendirilemeyen özellik için "performans belirlenemedi" ifadesi kullanılır (EK-10 10.2/3).</p>
              <table class="ozet-tablo">
                <thead><tr><th>Akustik özellik</th><th>Değerlendirilen eleman</th><th>Sağlanan sınıf (ön)</th></tr></thead>
                <tbody>
            """);
        foreach (var o in sonuc.Ozellikler)
        {
            var ad = o.Ozellik switch
            {
                SinifAtamaServisi.AkustikOzellik.DisCepheHavaSesi => "Dış yapı elemanları — hava doğuşlu (DnT,A,tr)",
                SinifAtamaServisi.AkustikOzellik.BolmeHavaSesi => "Bölme elemanları — hava doğuşlu (DnT,A)",
                SinifAtamaServisi.AkustikOzellik.TesisatGurultusu => "Servis ekipmanı iç gürültüsü (EK-5)",
                _ => "Döşemeler — darbe sesi (L'nT,w)",
            };
            var eleman = o.DegerlendirilenEleman > 0
                ? $"{o.DegerlendirilenEleman}{(o.BelirlenemeyenEleman > 0 ? $" (+{o.BelirlenemeyenEleman} belirlenemedi)" : "")}"
                : "—";
            sb.Append($"<tr><td>{H(ad)}</td><td>{H(eleman)}</td><td class=\"{SinifRenk(o.Sonuc)}\">{H(o.Sonuc.Etiket)}</td></tr>");
        }
        sb.Append($"""
                </tbody>
              </table>
              <p class="genel-sinif">Bina geneli ön değerlendirme sınıfı (EK-10/9 en alt sınıf kuralı):
              <span class="{SinifRenk(sonuc.Genel)} buyuk">{H(sonuc.Genel.Etiket)}</span>
              &nbsp;·&nbsp; Hedeflenen sınıf: <strong>{H(hedef.Kod)}</strong></p>
            </section>
            """);
        return sb.ToString();
    }

    private static string OlcumPerformansSinifiBolumu(
        BinaProje proje,
        ISet<Guid>? haric,
        M08RaporUygulanabilirlikSonucu onKontrol)
    {
        var sonuc = new OlcumPerformansSiniflandirmaServisi().Degerlendir(proje, haric);
        var genelEtiket = onKontrol.YayinaEngelVar ? "performans belirlenemedi" : sonuc.Genel.Etiket;
        var genelCss = onKontrol.YayinaEngelVar || sonuc.Genel.Belirlenemedi ? "eksik" :
            sonuc.Genel.SinifDisi ? "kaldi" : "gecti";
        var sb = new StringBuilder("""
            <section>
              <h2>13. Ölçüme Dayalı Akustik Performans Sınıflandırması (EK-10)</h2>
              <div class="sinif-olcegi"><span>A</span><span>B</span><span>C</span><span>D</span><span>E</span><span>F</span></div>
              <p>Yalnız doğrulanmış ve raporlamaya onaylı ölçümler sınıflandırılmıştır.
              Her özellikte en düşük performans, bina genelinde de özelliklerin en düşüğü esas alınır.
              Örnekleme veya zorunlu özellik eksiği varsa bina sınıfı atanmaz.</p>
              <table class="ozet-tablo"><thead><tr><th>Akustik özellik</th><th>Onaylı ölçüm sayısı</th><th>Ölçülen sınıf</th></tr></thead><tbody>
            """);
        foreach (var o in sonuc.Ozellikler)
            sb.Append($"<tr><td>{H(OlcumOzellikAdi(o.Ozellik))}</td><td>{o.OlcumSayisi}</td>" +
                      $"<td class=\"{OlcumSinifCss(o.Sonuc)}\">{H(o.Sonuc.Etiket)}</td></tr>");
        sb.Append($"""
              </tbody></table>
              <p class="genel-sinif">Bina geneli ölçüme dayalı sınıf:
              <span class="{genelCss} buyuk">{H(genelEtiket)}</span></p>
              <p class="not">Belge geçerlilik süresi yayın tarihinden itibaren on yıldır. Yapının akustik performansını
              etkileyen kullanım, tesisat, cephe, bölme veya döşeme değişikliklerinde belge yeniden değerlendirilmelidir.</p>
            """);
        if (onKontrol.YayinaEngelVar)
            sb.Append("<p class=\"eksik kutu\">EK-10 kapsamı veya örnekleme koşulları tamamlanmadığından resmî bina sınıfı atanmadı. Ayrıntılar Belge Kontrolü bölümündedir.</p>");
        sb.Append("<h3>Ölçüm bazında sınıflandırma</h3><table><thead><tr><th>Ölçüm</th><th>Özellik</th><th>Değer</th><th>Sınıf</th><th>Açıklama</th></tr></thead><tbody>");
        foreach (var o in sonuc.Olcumler)
            sb.Append($"<tr><td>{H(o.OlcumAdi)}</td><td>{H(OlcumOzellikAdi(o.Ozellik))}</td>" +
                      $"<td>{o.Deger:F1} {H(o.Birim)}</td><td class=\"{OlcumSinifCss(o.Sonuc)}\">{H(o.Sonuc.Etiket)}</td>" +
                      $"<td class=\"kucuk\">{H(o.Aciklama)}</td></tr>");
        if (sonuc.Olcumler.Count == 0)
            sb.Append("<tr><td colspan=\"5\" class=\"bos\">Sınıflandırılabilir onaylı ölçüm yok.</td></tr>");
        return sb.Append("</tbody></table></section>").ToString();
    }

    private static string OlcumOzellikAdi(OlcumPerformansOzelligi ozellik) => ozellik switch
    {
        OlcumPerformansOzelligi.DisCepheHavaSesi => "Dış cephe hava sesi",
        OlcumPerformansOzelligi.BolmeHavaSesi => "İç bölme hava sesi",
        OlcumPerformansOzelligi.DarbeSesi => "Darbe sesi",
        OlcumPerformansOzelligi.IcOrtamArkaPlanGurultusu => "İç ortam arka plan gürültüsü",
        OlcumPerformansOzelligi.ServisEkipmaniGurultusu => "Servis ekipmanı gürültüsü",
        _ => "Reverberasyon süresi",
    };

    private static string OlcumSinifCss(OlcumPerformansSinifi sonuc) =>
        sonuc.Belirlenemedi ? "eksik" : sonuc.SinifDisi ? "kaldi" :
        (int)sonuc.Sinif!.Value <= (int)AkustikPerformansSinifi.C ? "gecti" : "kaldi";

    private string OneriBolumu(BinaProje proje, ISet<Guid>? haric, IReadOnlyList<Bolme> darbeBolmeleri)
    {
        var sb = new StringBuilder();
        var oneriler = new List<(string Yer, CozumOnerisi Oneri)>();

        void Topla(Bolme b, string yer)
        {
            var marj = _uyum.HesaplaMarjDb(proje.BinaSinifi, proje.AkustikPerformansSinifi, b, proje.DisGurultuDb);
            if (double.IsNaN(marj) || marj >= 0) return;
            foreach (var o in _oneriServisi.OnerileriGetir(b, -marj))
                oneriler.Add((yer, o));
        }

        foreach (var kat in proje.Katlar)
            foreach (var m in kat.Mekanlar)
                foreach (var b in m.Bolmeler)
                {
                    if (haric?.Contains(b.Id) == true || b.YapiElemani == null) continue;
                    Topla(b, $"{m.Ad} / {b.Ad}");
                }
        foreach (var b in darbeBolmeleri)
            Topla(b, b.Ad);

        sb.Append("""
            <section>
              <h2>14. Çözüm Önerileri (limit sağlanamayan elemanlar için)</h2>
            """);
        if (oneriler.Count == 0)
        {
            sb.Append("<p class=\"bos\">Hedef sınıf sınır değerini sağlamayan eleman bulunmuyor; ek önlem önerisi gerekmemiştir.</p>");
        }
        else
        {
            sb.Append("""
                <table>
                  <thead><tr><th>Eleman</th><th>Öneri</th><th>Beklenen katkı</th><th>Açıklama</th></tr></thead>
                  <tbody>
                """);
            foreach (var (yer, o) in oneriler)
                sb.Append($"<tr><td>{H(yer)}</td><td>{H(o.OneriAdi)}</td>" +
                          $"<td>{(o.Uyari ? "—" : $"+{o.ArtisDb:F0} dB")}</td><td class=\"kucuk\">{H(o.Aciklama)}</td></tr>");
            sb.Append("</tbody></table>");
        }
        sb.Append("</section>");
        return sb.ToString();
    }

    private static string OlcumDogrulamaBolumu(BinaProje proje)
    {
        var rows = new OlcumKarsilastirmaServisi().Karsilastir(proje, sadeceRaporlamaIcinOnayli: true);
        var kayitlar = proje.SahaOlcumleri.ToDictionary(x => x.Id);
        var taslakSayisi = proje.SahaOlcumleri.Count(x =>
            !x.RaporlamaIcinOnayli || x.Durum != SahaOlcumDurumu.Dogrulandi);
        var sb = new StringBuilder("""
            <section>
              <h2>15. Saha Ölçümü ile Model Doğrulaması</h2>
              <p>Yalnız <strong>doğrulanmış ve raporlama için onaylı</strong> ölçümler ana rapora
              alınır. Taslak veya reddedilmiş kayıtlar hesabı ve uygunluk kararını etkilemez.</p>
              <table class="genis"><thead><tr><th>Ölçüm / tarih</th><th>Tür</th><th>Model</th><th>Ölçüm ±u</th>
              <th>Model Δ</th><th>Ölçüm marjı</th><th>Durum</th><th>Standart / yöntem</th>
              <th>Cihaz / operatör / kaynak</th></tr></thead><tbody>
            """);
        foreach (var x in rows)
        {
            kayitlar.TryGetValue(x.OlcumId, out var kayit);
            sb.Append($"<tr><td>{H(x.OlcumAdi)}<br><span class=\"kucuk\">{(kayit is null ? "—" : kayit.OlcumTarihi.ToString("dd.MM.yyyy"))}</span></td><td>{H(x.OlcumTuru.ToString())}</td>" +
                      $"<td>{Sayi(x.ModelDegeri)} {H(x.Birim)}</td><td>{x.OlcumDegeri:F1}" +
                      $"{(kayit?.Belirsizlik is { } u ? $" ±{u:F1}" : "")} {H(x.Birim)}</td>" +
                      $"<td>{Sayi(x.ModeldenSapma)}</td><td>{Sayi(x.OlcumMarji)}</td>" +
                      $"<td class=\"{(x.OlcumUygunMu is null ? "eksik" : x.OlcumUygunMu.Value ? "gecti" : "kaldi")}\">" +
                      $"{H(x.OlcumUygunMu is null ? x.EslesmeDurumu : x.OlcumUygunMu.Value ? "GEÇTİ" : "KALDI")}</td>" +
                      $"<td class=\"kucuk\">{H(Bos(kayit?.Standart))} / {H(Bos(kayit?.Yontem))}</td>" +
                      $"<td class=\"kucuk\">{H(Bos(kayit?.Cihaz))} / {H(Bos(kayit?.Operator))}<br>{H(Bos(kayit?.KaynakDosya))}</td></tr>");
        }
        if (rows.Count == 0)
            sb.Append("<tr><td colspan=\"9\" class=\"bos\">Doğrulanmış ve raporlamaya onaylı saha ölçümü bulunmuyor.</td></tr>");
        sb.Append($"</tbody></table><p class=\"not\">Ana rapora alınmayan taslak/reddedilmiş ölçüm sayısı: {taslakSayisi}.</p></section>");
        return sb.ToString();
    }

    private static string MetrajBolumu(BinaProje proje)
    {
        var rows = new AkustikMetrajServisi().Hesapla(proje);
        var urunler = proje.YapiElemaniKutuphanesi.ToDictionary(x => x.Id);
        var sb = new StringBuilder("""
            <div class="sayfa-kes"></div>
            <section class="karar-destek">
              <h2>16. Karar Destek Eki — Akustik Metraj ve Yaklaşık Maliyet Özeti</h2>
              <div class="uyari-kutu"><strong>Mevzuat dışı ek:</strong> Bu bölüm yönetmelik uygunluk
              kararının ve resmî akustik belgenin parçası değildir. Fiyatlar yalnız karar desteğidir.</div>
              <p>İç duvarlar fiziksel yüz kimliğiyle tek sayılır; açıklıklar, döşeme ve tavan
              atamaları ayrı metraj kaynaklarıdır. Fiyatlar katalogdaki beyan tarihine bağlı yaklaşık değerlerdir.</p>
              <table><thead><tr><th>Ürün / Poz</th><th>Tür</th><th>Birim</th><th>Net</th>
              <th>Fire</th><th>Miktar</th><th>Birim fiyat / tarih</th><th>Tutar</th></tr></thead><tbody>
            """);
        foreach (var x in rows)
        {
            urunler.TryGetValue(x.YapiElemaniId, out var urun);
            sb.Append($"<tr><td>{H(x.YapiElemaniAdi)}<br><span class=\"kucuk\">{H(Bos(x.PozNo))}</span></td>" +
                      $"<td>{H(x.Tip.ToString())}</td><td>{H(x.Birim.ToString())}</td><td>{x.NetMiktar:N3}</td>" +
                      $"<td>%{x.FireYuzdesi:N1}</td><td>{x.Miktar:N3}</td><td>{x.BirimFiyat:N2} {H(x.ParaBirimi)}<br>" +
                      $"<span class=\"kucuk\">{(urun?.BirimFiyatTarihi is { } tarih ? tarih.ToString("dd.MM.yyyy") : "tarih girilmedi")}</span></td>" +
                      $"<td>{x.ToplamTutar:N2} {H(x.ParaBirimi)}</td></tr>");
        }
        if (rows.Count == 0)
            sb.Append("<tr><td colspan=\"8\" class=\"bos\">Metraj oluşturacak atanmış yapı elemanı bulunmuyor.</td></tr>");
        foreach (var g in rows.GroupBy(x => x.ParaBirimi))
            sb.Append($"<tr><th colspan=\"7\">Toplam ({H(g.Key)})</th><th>{g.Sum(x => x.ToplamTutar):N2} {H(g.Key)}</th></tr>");
        return sb.Append("</tbody></table></section>").ToString();
    }

    private static string OnayVeIzlenebilirlikBolumu(BinaProje proje)
    {
        var sb = new StringBuilder("""
            <section>
              <h2>17. Onay, Denetim İzi ve Ürün Belgeleri</h2>
              <h3>Onay kayıtları</h3><table><thead><tr><th>Tarih</th><th>Tür</th><th>Onaylayan</th>
              <th>Kuruluş</th><th>Durum</th><th>Rapor parmak izi</th><th>Açıklama</th></tr></thead><tbody>
            """);
        foreach (var x in proje.OnayKayitlari.OrderByDescending(x => x.Tarih))
            sb.Append($"<tr><td>{x.Tarih:g}</td><td>{H(x.Tur.ToString())}</td><td>{H(Bos(x.Onaylayan))}</td>" +
                      $"<td>{H(Bos(x.Kurulus))}</td><td>{H(x.Durum.ToString())}</td><td class=\"kucuk\">{H(KisaIz(x.GirdiParmakIzi))}</td>" +
                      $"<td>{H(Bos(x.Aciklama))}</td></tr>");
        if (proje.OnayKayitlari.Count == 0) sb.Append("<tr><td colspan=\"7\" class=\"bos\">Onay kaydı yok.</td></tr>");
        sb.Append("</tbody></table><h3>Son denetim olayları</h3><table><thead><tr><th>Tarih</th><th>Eylem</th><th>Kullanıcı</th><th>Hedef</th><th>Açıklama</th></tr></thead><tbody>");
        foreach (var x in proje.DenetimKayitlari.OrderByDescending(x => x.Tarih).Take(50))
            sb.Append($"<tr><td>{x.Tarih:g}</td><td>{H(x.Eylem.ToString())}</td><td>{H(Bos(x.Kullanici))}</td>" +
                      $"<td>{H(x.HedefTuru)} / {H(KisaIz(x.HedefId))}</td><td>{H(x.Aciklama)}</td></tr>");
        if (proje.DenetimKayitlari.Count == 0) sb.Append("<tr><td colspan=\"5\" class=\"bos\">Denetim kaydı yok.</td></tr>");
        sb.Append("</tbody></table><h3>Kullanılan ürün belgeleri</h3><table><thead><tr><th>Ürün</th><th>Üretici / Kod</th><th>Belge / no</th><th>Tür</th><th>Kuruluş</th><th>Yayın / geçerlilik</th><th>SHA-256</th></tr></thead><tbody>");
        var products = KullanilanElemanlar(proje).ToList();
        foreach (var e in products)
        foreach (var b in e.Belgeler)
            sb.Append($"<tr><td>{H(e.Ad)}</td><td>{H(Bos(e.Uretici))} / {H(Bos(e.UrunKodu))}</td><td>{H(Bos(b.Ad))}<br><span class=\"kucuk\">{H(Bos(b.BelgeNo))}</span></td>" +
                      $"<td>{H(b.Tur.ToString())}</td><td>{H(Bos(b.VerenKurulus))}</td><td>{(b.YayinTarihi is { } y ? y.ToString("d") : "—")} / {(b.GecerlilikTarihi is { } d ? d.ToString("d") : "—")}" +
                      $"{(b.SuresiDolmus ? " (SÜRESİ DOLMUŞ)" : "")}</td><td class=\"kucuk\">{H(KisaIz(b.Sha256))}</td></tr>");
        if (!products.Any(e => e.Belgeler.Count > 0)) sb.Append("<tr><td colspan=\"7\" class=\"bos\">Kullanılan ürünlere bağlı belge yok.</td></tr>");
        return sb.Append("</tbody></table><p class=\"not\">SHA-256 parmak izi içerik bütünlüğü içindir; nitelikli elektronik imza değildir.</p></section>").ToString();
    }

    // ─── Yardımcılar ─────────────────────────────────────────────────────────

    private static IEnumerable<YapiElemani> KullanilanElemanlar(BinaProje proje)
    {
        var set = new List<YapiElemani>();
        void Ekle(YapiElemani? el)
        {
            if (el != null && !set.Contains(el)) set.Add(el);
        }

        foreach (var kat in proje.Katlar)
            foreach (var m in kat.Mekanlar)
            {
                Ekle(m.ZeminYapiElemani);
                Ekle(m.TavanYapiElemani);
                foreach (var b in m.Bolmeler)
                {
                    Ekle(b.YapiElemani);
                    foreach (var a in b.Acikliklar) Ekle(a.YapiElemani);
                }
            }
        return set.OrderBy(e => e.Tip).ThenBy(e => e.Ad, StringComparer.OrdinalIgnoreCase);
    }

    private static string KatAdi(Kat kat) =>
        string.IsNullOrWhiteSpace(kat.Ad) ? $"Kat {kat.KatNo}" : kat.Ad;

    private static HashSet<Guid> FizikselBolmeIdleriniGenislet(
        BinaProje proje, ISet<Guid>? kanonikHaricIdleri)
    {
        var sonuc = kanonikHaricIdleri == null
            ? []
            : new HashSet<Guid>(kanonikHaricIdleri);
        if (kanonikHaricIdleri == null)
            return sonuc;

        foreach (var mekan in proje.Katlar.SelectMany(k => k.Mekanlar))
        {
            var cepheler = mekan.Bolmeler.Where(b => b.IsDisCephe).ToList();
            if (!kanonikHaricIdleri.Contains(RaporSonucIdleri.DisCephe(mekan.Id)) &&
                !cepheler.Any(b => kanonikHaricIdleri.Contains(b.Id)))
                continue;
            foreach (var cephe in cepheler)
                sonuc.Add(cephe.Id);
        }
        return sonuc;
    }

    private static string Bos(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s;

    private static string Sayi(double? value) => value is { } x && double.IsFinite(x)
        ? x.ToString("+0.0;-0.0;0.0") : "—";

    private static string KisaIz(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "—";
        var temiz = value.Trim();
        return temiz.Length <= 18 ? temiz : temiz[..18] + "…";
    }

    private static string BinaFotografiHtml(string? yol)
    {
        if (string.IsNullOrWhiteSpace(yol) || !File.Exists(yol))
            return "<div class=\"bina-fotografi foto-eksik\">Bina fotoğrafı eklenmedi</div>";
        try
        {
            var bilgi = new FileInfo(yol);
            if (bilgi.Length > 10L * 1024 * 1024)
                return "<div class=\"bina-fotografi foto-eksik\">Bina fotoğrafı 10 MB sınırını aşıyor</div>";
            var mime = Path.GetExtension(yol).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => ""
            };
            if (mime.Length == 0)
                return "<div class=\"bina-fotografi foto-eksik\">Bina fotoğrafı biçimi desteklenmiyor</div>";
            var veri = Convert.ToBase64String(File.ReadAllBytes(yol));
            return $"<div class=\"bina-fotografi\"><img src=\"data:{mime};base64,{veri}\" alt=\"Bina fotoğrafı\"></div>";
        }
        catch (IOException)
        {
            return "<div class=\"bina-fotografi foto-eksik\">Bina fotoğrafı okunamadı</div>";
        }
        catch (UnauthorizedAccessException)
        {
            return "<div class=\"bina-fotografi foto-eksik\">Bina fotoğrafına erişilemedi</div>";
        }
    }

    /// Minimal HTML kaçışı — WebUtility.HtmlEncode Türkçe karakterleri de sayısal
    /// entity'e çevirdiğinden (dosya zaten UTF-8) yalnız 5 özel karakter kaçışlanır.
    private static string H(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");

    private static string DurumMetni(string durum) => durum switch
    {
        "Gecti" => "GEÇTİ",
        "Kaldi" => "KALDI",
        "VeriEksik" => "performans belirlenemedi",
        _ => durum,
    };

    private static string DurumSinif(string durum) => durum switch
    {
        "Gecti" => "gecti",
        "Kaldi" => "kaldi",
        "VeriEksik" => "eksik",
        _ => "",
    };

    private static string SinifRenk(SinifAtamaServisi.ElemanSinifi s) =>
        s.Belirlenemedi ? "eksik" : s.SinifDisi ? "kaldi" :
        (int)s.Sinif!.Value <= (int)AkustikPerformansSinifi.C ? "gecti" : "kaldi";

    private static string HtmlBas(BinaProje proje, DateTime uretimZamani) => $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
        <meta charset="utf-8">
        <title>{{H(BelgeBasligi(proje.RaporAyarlari.BelgeTuru))}} — {{H(Bos(proje.ProjeAdi))}}</title>
        <style>
          :root { color-scheme: light; }
          * { box-sizing: border-box; }
          body { font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif; color: #1a2733;
                 margin: 0; padding: 24px; background: #f4f6f8; font-size: 13px; line-height: 1.5; }
          section { background: #fff; border: 1px solid #d7dee5; border-radius: 6px;
                    padding: 18px 22px; margin: 0 auto 14px; max-width: 960px; }
          h1 { font-size: 26px; margin: 8px 0 4px; letter-spacing: 1px; }
          h2 { font-size: 16px; border-bottom: 2px solid #1d6fa5; padding-bottom: 4px;
               margin: 0 0 10px; color: #14415f; }
          table { border-collapse: collapse; width: 100%; font-size: 12px; }
          th, td { border: 1px solid #c9d4dd; padding: 4px 8px; text-align: left; vertical-align: top; }
          thead th { background: #14415f; color: #fff; font-weight: 600; }
          tbody tr:nth-child(even) { background: #f2f6f9; }
          .kapak { text-align: center; padding-top: 40px; }
          .kapak-ust .logo { color: #1d6fa5; font-weight: 700; letter-spacing: 2px; }
          .belge-durumu { display: inline-block; border: 1px solid #14415f; padding: 3px 12px;
                          margin: 3px 0 8px; font-size: 11px; font-weight: 700; letter-spacing: .7px; }
          .alt-baslik { color: #4a5a68; max-width: 640px; margin: 6px auto 24px; }
          .kapak-tablo { max-width: 680px; margin: 0 auto; text-align: left; }
          .kapak-tablo th { width: 300px; background: #eaf1f6; color: #14415f; }
          .bina-fotografi { width: 680px; height: 220px; margin: 0 auto 18px; border: 1px solid #c9d4dd;
                            display: flex; align-items: center; justify-content: center; overflow: hidden; background: #eef3f6; }
          .bina-fotografi img { width: 100%; height: 100%; object-fit: cover; }
          .foto-eksik { color: #7a5c07; font-style: italic; }
          .imza-alani { display: flex; justify-content: space-around; margin-top: 60px; }
          .imza { width: 260px; color: #4a5a68; font-size: 12px; }
          .imza .cizgi { border-bottom: 1px solid #4a5a68; height: 48px; margin-bottom: 6px; }
          .uyari-kutu { background: #fff6e5; border: 1px solid #e5b96a; border-radius: 4px;
                        padding: 10px 14px; margin: 10px 0; }
          .ozet-tablo th { width: 320px; background: #eaf1f6; color: #14415f; }
          .not { color: #5a6a78; font-size: 11px; font-style: italic; }
          .kucuk { font-size: 10.5px; color: #4a5a68; }
          .bos { text-align: center; color: #7a8a98; font-style: italic; }
          .gecti { background: #d9f2e2 !important; color: #135c31; font-weight: 600; }
          .kaldi { background: #fbdcdc !important; color: #8a1d1d; font-weight: 600; }
          .eksik { background: #fdf3cf !important; color: #7a5c07; font-weight: 600; }
          .genel-sinif { font-size: 14px; margin-top: 12px; }
          .genel-sinif .buyuk { font-size: 20px; padding: 2px 14px; border-radius: 4px; }
          .kutu { padding: 8px 12px; border: 1px solid currentColor; }
          .karar-destek { border-top: 5px solid #6b5b2a; }
          .sinif-olcegi { display: grid; grid-template-columns: repeat(6, 1fr); max-width: 520px;
                          margin: 8px 0 14px; color: #fff; font-weight: 700; text-align: center; }
          .sinif-olcegi span { padding: 5px; background: #286b45; }
          .sinif-olcegi span:nth-child(2) { background:#3f7d52; }
          .sinif-olcegi span:nth-child(3) { background:#6e8a45; }
          .sinif-olcegi span:nth-child(4) { background:#9a8637; }
          .sinif-olcegi span:nth-child(5) { background:#a96532; }
          .sinif-olcegi span:nth-child(6) { background:#963b36; }
          .standartlar li { margin-bottom: 3px; }
          .sayfa-kes { page-break-after: always; }
          .rapor-ustbilgi, .rapor-altbilgi { display: none; }
          .genis { font-size: 10px; }
          @media print {
            @page { size: A4 portrait; margin: 17mm 12mm 16mm; }
            @page yatay { size: A4 landscape; margin: 14mm 10mm 14mm; }
            body { background: #fff; padding: 0; }
            section { border: none; border-radius: 0; margin-bottom: 10px; max-width: none; padding: 4px 0; }
            .yatay-sayfa { page: yatay; page-break-before: always; page-break-after: always; }
            thead { display: table-header-group; }
            tr { page-break-inside: avoid; }
            h2, h3 { page-break-after: avoid; }
            .rapor-ustbilgi, .rapor-altbilgi { display: block; position: fixed; left: 0; right: 0;
                                              color: #526574; font-size: 8.5px; }
            .rapor-ustbilgi { top: -12mm; border-bottom: 1px solid #b8c4cd; padding-bottom: 2mm; }
            .rapor-altbilgi { bottom: -11mm; border-top: 1px solid #b8c4cd; padding-top: 2mm; }
            .rapor-altbilgi .sayfa::after { content: "Sayfa " counter(page); float: right; }
          }
        </style>
        </head>
        <body>
        <div class="rapor-ustbilgi">{{H(Bos(proje.ProjeAdi))}} · {{H(Bos(proje.RaporAyarlari.RaporNumarasi))}} · Rev. {{H(Bos(proje.RaporAyarlari.RevizyonKodu))}}</div>
        <div class="rapor-altbilgi">{{H(M08RaporUygulanabilirlikServisi.YayinDurumuAdi(proje.RaporAyarlari.YayinDurumu))}} · {{uretimZamani:dd.MM.yyyy}} <span class="sayfa"></span></div>
        """;

    private static string HtmlSon(BinaProje proje, DateTime uretimZamani) => $"""
        <section class="not" style="text-align:center">
          EYE4PRO Acoustics — M08 Bina Gürültü Hesabı · Rapor üretim zamanı: {uretimZamani:dd.MM.yyyy HH:mm}
          · Belge türü: {H(BelgeBasligi(proje.RaporAyarlari.BelgeTuru))}
          · Yayın durumu: {H(M08RaporUygulanabilirlikServisi.YayinDurumuAdi(proje.RaporAyarlari.YayinDurumu))}.
        </section>
        </body>
        </html>
        """;
}
