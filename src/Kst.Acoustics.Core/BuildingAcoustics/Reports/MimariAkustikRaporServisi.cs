using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

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
        var sb = new StringBuilder(64 * 1024);
        var sinifBilgi = BinaSinifiHelper.BilgiAl(proje.BinaSinifi);
        var hedefSinif = AkustikPerformansSinifiHelper.BilgiAl(proje.AkustikPerformansSinifi);
        var islev = BinaIsleviHelper.BilgiAl(proje.BinaIslevi);

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

        sb.Append(HtmlBas(proje));

        // ── 0. KAPAK ─────────────────────────────────────────────────────────
        sb.Append($"""
            <section class="kapak">
              <div class="kapak-ust">
                <div class="logo">EYE4PRO Acoustics</div>
                <h1>MİMARİ AKUSTİK RAPORU</h1>
                <p class="alt-baslik">Binaların Gürültüye Karşı Korunması Hakkında Yönetmelik
                (RG 31.05.2017/30082) Madde 5/5 kapsamında hesaplamaya dayalı ön değerlendirme</p>
              </div>
              <table class="kapak-tablo">
                <tr><th>Proje adı</th><td>{H(Bos(proje.ProjeAdi))}</td></tr>
                <tr><th>Proje adresi</th><td>{H(Bos(proje.Adres))}</td></tr>
                <tr><th>Müşteri / yapı sahibi</th><td>{H(Bos(proje.MusteriAdi))}</td></tr>
                <tr><th>Değerlendirmeden sorumlu kişi ve kuruluş</th><td>{H(Bos(proje.SorumluKisiKurulus))}</td></tr>
                <tr><th>Bina sınıfı / işlevi</th><td>{H(sinifBilgi.KodGosterim)} — {H(islev.Ad)}</td></tr>
                <tr><th>Hedeflenen akustik performans sınıfı</th><td>{H(hedefSinif.Kod)} ({H(hedefSinif.Aciklama)})</td></tr>
                <tr><th>Değerlendirilen bağımsız birim sayısı</th><td>{proje.BagimsizBolumler.Count}</td></tr>
                <tr><th>Kat / mekan sayısı</th><td>{proje.Katlar.Count} kat · {proje.Katlar.Sum(k => k.Mekanlar.Count)} mekan</td></tr>
                <tr><th>Rapor tarihi</th><td>{DateTime.Now:dd.MM.yyyy}</td></tr>
                <tr><th>Uygulanan sınıflandırma referansı</th><td>Yönetmelik Madde 11, EK-2, EK-3; ön sınıf değerlendirmesi EK-10 ilkeleriyle</td></tr>
              </table>
              <div class="imza-alani">
                <div class="imza"><div class="cizgi"></div>Hazırlayan (ad, unvan, imza)</div>
                <div class="imza"><div class="cizgi"></div>Onaylayan (ad, unvan, imza)</div>
              </div>
            </section>
            <div class="sayfa-kes"></div>
            """);

        // ── 1. GİRİŞ VE KAPSAM ───────────────────────────────────────────────
        sb.Append($"""
            <section>
              <h2>1. Giriş ve Kapsam</h2>
              <p>Bu rapor, {H(Bos(proje.ProjeAdi))} projesi için Binaların Gürültüye Karşı Korunması
              Hakkında Yönetmelik (RG 31.05.2017/30082, değişiklik RG 31.05.2018/30437) Madde 5/5
              uyarınca hazırlanmıştır. Rapor; mekânların gürültüye hassasiyet ve gürültülülük yönünden
              ilişkilerinin analizini, ses iletim yollarını (hava doğuşlu ve darbe sesi), hedeflenen
              akustik performans sınıfına göre uygulanacak sınır değerleri ve bu değerlere göre
              belirlenen yapı elemanlarını içerir.</p>
              <div class="uyari-kutu">
                <strong>Önemli:</strong> Bu rapor <em>hesaplamaya dayalı bir ön değerlendirmedir</em>
                (TS EN 12354 serisi model hesabı). Yönetmelik EK-10 kapsamındaki resmi
                <em>Akustik Performans Belgesi</em>, tamamlanmış binada TS EN ISO 16283 serisine uygun
                ölçümler yapılmasını gerektirir; bu rapor o belgenin yerine geçmez.
              </div>
              <p>Yeni yapılacak binalarda Yönetmelik Madde 11/3 uyarınca en az C akustik performans
              sınıfı sağlanır. Bu projede hedeflenen sınıf: <strong>{H(hedefSinif.Kod)}</strong>.
              Tasarımcı tercihine bağlı olarak hesaplar hedef sınır değerlerin 2 dB üstüne göre
              yapılabilir (EK-7 7.1.4/1).</p>
            </section>
            """);

        // ── 2. MEKAN ANALİZİ (EK-2) ──────────────────────────────────────────
        sb.Append("""
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
              <table>
                <thead><tr><th>Eleman</th><th>Tip</th><th>R<sub>w</sub> (C; C<sub>tr</sub>) dB</th>
                <th>L<sub>n,w</sub> dB</th><th>ΔR kaplama dB</th><th>Veri kaynağı</th></tr></thead>
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
            var lnw = el.Tip == YapiElemaniTipi.Doseme ? $"{el.Lnw:F0}" : "—";
            var deltaR = el.DeltaRDb > 0 ? $"+{el.DeltaRDb:F0}" : "—";
            sb.Append($"<tr><td>{H(el.Ad)}</td><td>{H(el.Tip.ToString())}</td>" +
                      $"<td>{el.Rw:F0} ({el.C:F0}; {el.Ctr:F0})</td><td>{lnw}</td>" +
                      $"<td>{deltaR}</td><td>{H(kaynak)}</td></tr>");
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
        sb.Append(SinifBolumu(sinifSonucu, hedefSinif));

        // ── 14. ÇÖZÜM ÖNERİLERİ ─────────────────────────────────────────────
        sb.Append(OneriBolumu(proje, fizikselHaricIdleri, darbeBolmeleri));

        // ── 15. EK: STANDART LİSTESİ ────────────────────────────────────────
        sb.Append("""
            <section>
              <h2>15. Ek — Kullanılan Standartlar (EK-9'un ilgili alt kümesi)</h2>
              <ul class="standartlar">
                <li>TS EN 12354-1 — Binalarda ses iletimi: elemanlar arası hava doğuşlu ses</li>
                <li>TS EN 12354-2 — Binalarda ses iletimi: katlar arası darbe sesi</li>
                <li>TS EN 12354-3 — Binalarda ses iletimi: dış gürültünün içeri iletimi</li>
                <li>TS EN ISO 717-1 — Hava doğuşlu ses yalıtımının derecelendirilmesi</li>
                <li>TS EN ISO 717-2 — Darbe sesi yalıtımının derecelendirilmesi</li>
                <li>TS EN ISO 16283-1/-2/-3 — Sahada ses yalıtımı ölçümleri (doğrulama ölçümleri için)</li>
                <li>ISO 12999-1 — Ses yalıtımında ölçüm belirsizliği</li>
              </ul>
              <p class="not">Oktav bant spektrumları ve ayrıntılı hesap dökümü için Excel raporu
              ("Oktav Bantlar" sayfası) ile renkli DXF plan çıktısı bu raporun ekleri olarak üretilebilir.</p>
            </section>
            """);

        sb.Append(HtmlSon());
        return sb.ToString();
    }

    // ─── Bölüm üreticileri ────────────────────────────────────────────────────

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
                <th>Bağlantı tipi</th><th>K<sub>ij</sub> (dB)</th></tr></thead>
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
                                  $"<td>{H(j.GeometriTipi.ToString())}</td><td>{H(j.BaglantiTipi.ToString())}</td>" +
                                  $"<td>{j.KijDb:F0}{(j.KijOverrideDb > 0 ? " (kullanıcı)" : "")}</td></tr>");
                        say++;
                    }
                }
        if (say == 0)
            sb.Append("<tr><td colspan=\"6\" class=\"bos\">Kullanıcı tanımlı birleşim parametresi yok; hesapta Annex E varsayılan rijit birleşimler kullanılmıştır.</td></tr>");
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

    private static string HtmlBas(BinaProje proje) => $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
        <meta charset="utf-8">
        <title>Mimari Akustik Raporu — {{H(Bos(proje.ProjeAdi))}}</title>
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
          .alt-baslik { color: #4a5a68; max-width: 640px; margin: 6px auto 24px; }
          .kapak-tablo { max-width: 680px; margin: 0 auto; text-align: left; }
          .kapak-tablo th { width: 300px; background: #eaf1f6; color: #14415f; }
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
          .standartlar li { margin-bottom: 3px; }
          .sayfa-kes { page-break-after: always; }
          @media print {
            body { background: #fff; padding: 0; }
            section { border: none; border-radius: 0; margin-bottom: 10px; max-width: none; }
            thead { display: table-header-group; }
            tr { page-break-inside: avoid; }
          }
        </style>
        </head>
        <body>
        """;

    private static string HtmlSon() => $"""
        <section class="not" style="text-align:center">
          EYE4PRO Acoustics — M08 Bina Gürültü Hesabı · Rapor üretim zamanı: {DateTime.Now:dd.MM.yyyy HH:mm}
          · Bu belge hesaplamaya dayalı ön değerlendirmedir; resmi Akustik Performans Belgesi yerine geçmez.
        </section>
        </body>
        </html>
        """;
}
