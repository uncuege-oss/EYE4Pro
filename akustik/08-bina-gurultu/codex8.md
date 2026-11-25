# M08 Codex — Bina Gürültü Hesabı

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları, kritik dikkat notları ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M08 — Bina Gürültü Hesabı |
| Amaç | Bina içi/dışı hava sesi, darbe sesi ve yönetmelik uyum hesabı |
| Standartlar | TS EN 12354-1 / -2 / -3, ISO 717-1 / -2 |
| Yerel mevzuat | Binaların Gürültüye Karşı Korunması Hakkında Yönetmelik |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Geliştiriliyor |

---

## 2. Modül Kapsamı

Aktif kapsam:
- ~~DXF kat planı üzerinden kat, mekan, duvar, döşeme, tavan ve açıklık üretimi~~
  → **M50'ye taşındı (2026-06-13):** geometri `.e4bina` dosyasından "Bina Geometrisi Yükle"
  ile gelir; kat/mekan/açıklık düzenleme ve patlatma M50 Bina Geometrisi modülünde yapılır
  (bkz. `ortak-moduller/50-bina-geometri/codex50.md`). M08 yalnız malzeme atar
  (açıklıklarda `AciklikId → YapiElemani`); boyutlar salt-okunur. Eski `.e4p08` projeleri
  açılışta otomatik migrate edilir.
- Yapı elemanı kütüphanesi ve toplu/filtreli malzeme atama
- Mekan kullanım sınıfı, kaynak gürültü derecesi ve alıcı hassasiyet sınıfı yönetimi
- Hava sesi hesabı: `R'w`, `DnT,w`, `DnT,A`, `DnT,A,tr`
- Darbe sesi hesabı: `L'n,w` ve yönetmelik karşılaştırması
- Dış cephe hesabı: `D2m,nT,w`, cephe form düzeltmeleri ve spektral yol
- 1/3 oktav spektral veri yolu ve 1/1 oktav görsel özet
- 12354-1 yaklaşımıyla junction / yanal iletim altyapısı
- Yönetmelik sonuçları, çözüm önerileri, rapor seçimi ve rapor dışa aktarımı
- Düşey mekan birleşimi, proje kilidi ve atama bekleyenler iş akışı

Geliştirilmeye açık alanlar:
- Tam EN 12354 junction modelinin standart tablolarla daha ayrıntılı hale getirilmesi
- Spektral dış gürültü girişlerinin kullanıcıya daha geniş açılması
- Cephe formu için tüm `DeltaLfsTam` parametrelerinin UI'da ayrı ayrı seçilebilmesi

### 2b. 2026-07-14 Kapsamlı UI/İş Akışı İncelemesi — Faz Takibi

15.000 satır kod incelemesi + canlı Avalonia arayüz testi sonucunda hesap motorundan
bağımsız, veri kaybı/yanlış raporlama riski taşıyan bulgular tespit edildi. Önerilen
geliştirme sırası (Faz 1-6 TÜM PLANLI FAZLAR TAMAMLANDI — 2026-07-14):

1. **Faz 1 — Veri güvenliği paketi: TAMAMLANDI** (bkz. §10, 2026-07-14 notu).
2. **Faz 2 — Rapor doğruluğu (hedefli düzeltmeler): TAMAMLANDI** — bilinçli olarak TAM
   mimari birleştirme (Core'da tek `SonucSatiriViewModel` motoru) YAPILMADI; bunun yerine
   4 ayrı hesap yolu (UI/metin/Excel/DXF) korunarak somut hatalar düzeltildi (bkz. §10).
   Kalan mimari borç: UI'nin worst-per-room gruplaması ile metin/Excel/DXF'in per-interface
   gruplaması hâlâ farklı granülaritede; ayrıca dış cephe `Deger`i iki farklı Core yolundan
   gelebilir (`HesaplaOlcumDegeri`'nin `bolme.Dntw+adapt`'ı vs `HesaplaSpektralDisCephe`'nin
   `DnTATr`'ı) — ölçüm/etkisi doğrulanmadı, gelecek bir oturumda araştırılmalı.
3. **Faz 3 — Darbe geometrisi: TAMAMLANDI** (bkz. §10, 2026-07-14 Faz 3 notu). Bounding-box
   yerine gerçek poligon kesişimi, çoklu alt-alıcı mekan desteği, `OneriUygula`'nın yalnız
   doğru yüzeye gitmesi ve daha önce fark edilmemiş bir ek bulgu (darbe özet satırlarında
   `IlgiliBolme` hiç set edilmiyordu — "Uygula" darbe sonuçlarında sessizce çalışmıyordu).
4. **Faz 4 — İş akışı sadeleştirmesi: KISMEN TAMAMLANDI** (bkz. §10, 2026-07-14 Faz 4 notu).
   Araştırma gösterdi ki 3 araçtan 2'si (kat yüksekliği, kat ofseti) zaten önceki bir "4.4"
   temizliğinde M50'ye yönlendirilmişti — UI zaten salt-okunuştu, yalnız erişilemez ölü
   mutasyon kodu (kullanılmayan setter'lar) kalmıştı; bu kod silindi. **`ProjeOlcek` (Proje
   Ölçeği) bilinçli olarak DOKUNULMADI** — hâlâ canlı ve gerçek geometriyi (Mekan.Poligon)
   yerinde değiştiriyor, ama M50'de karşılığı (manuel ölçek düzeltme) yok; kaldırmak
   kullanıcı için işlevsel gerileme olurdu. Bkz. §10 açık madde — M50'ye bu özellik
   eklenmeden `ProjeOlcek` M08'de kalmaya devam edecek.
5. **Faz 5 — Sonuç ekranı: TAMAMLANDI** (bkz. §10, 2026-07-14 Faz 5 notu). 18 sütunluk
   tablo varsayılan olarak dar/karar-odaklı sete (Kat/Mekan/Tür/Durum/Hesaplanan/Limit/
   Marj/Güven/Bölme, +Marj sütunu yeni eklendi) daraltıldı; 10 teknik/ayrıntı sütunu
   "Sade Görünüm ↔ Tüm Sütunlar" toggle'ı ile açılıp kapanıyor (kolon SİLİNMEDİ, yalnız
   varsayılan gizli — güncel-değil bandı zaten Faz 2'de vardı).
6. **Faz 6 — Test kapsamı: TAMAMLANDI.** Rapor/filtre/limit/VeriEksik testleri Faz 2'de,
   L/konkav oda + çoklu alt-oda darbe testleri Faz 3'te eklendi; kalan iki boşluk (VeriEksik'in
   metin raporunda `Kaldi` ile karışmadığını doğrulayan test, model değişikliği sonrası
   `SonuclarBayatMi`/`DisaAktarmaEtkin` davranışını doğrulayan ViewModel-seviyeli test) bu
   oturumda eklendi. Ayrıca inceleme sırasında not edilen `Mekan.AtamaTamamMi` bulgusu da
   (açıklık/kapı-pencere malzemesi kontrol edilmiyordu) düzeltildi.

Kalan bilinçli istisna (bulgu değil, karar):
- `ProjeOlcek` M08 içinde geometriyi (Mekan.Poligon) hâlâ doğrudan değiştiriyor — M50 salt-
  okunur kararını zedeliyor, ama M50'de eşdeğeri olmadan kaldırılamaz (bkz. Faz 4 notu).
  M50'ye manuel ölçek düzeltme eklenmeden bu istisna kalıcıdır.

Kalan mimari borç (bulgu değil, gelecekte araştırılabilir):
- UI'nin worst-per-room gruplaması ile metin/Excel/DXF'in per-interface gruplaması hâlâ farklı
  granülaritede; dış cephe `Deger`i iki farklı Core yolundan gelebilir (bkz. Faz 2 notu) —
  ölçüm/etkisi doğrulanmadı.

### 2c. Mimari Akustik Raporu (HTML) — 2026-07-17

Yönetmelik kaynak metinleri (`EYE4Pro/Yonetmelik/GeneratePdf.pdf` ana metin +
`7.5.23616-Ek.docx` EK-1…EK-10) incelenerek **Madde 5/5 uyumlu "Mimari Akustik Raporu"**
üretimi eklendi (581 test geçer):

- **Format kararı:** tek dosyalık, gömülü CSS'li, yazdırılabilir **HTML** (tarayıcıdan
  PDF'e). Kullanıcı DOCX yerine bunu seçti — yeni bağımlılık yok, mevcut TXT/Excel/DXF
  raporları korunur. Servis: `Core/BuildingAcoustics/Reports/MimariAkustikRaporServisi.cs`
  (`HtmlOlustur(BinaProje, ISet<Guid>? haric)`), UI: SonuclarPanel "🧾 Mimari Akustik
  Raporu" butonu (`DisaAktarmaEtkin`'e bağlı, "Rapora Dahil" filtresi geçer).
- **Bölüm yapısı** (Madde 5/5 + Madde 16/1 + EK-7 + EK-10 10.2'den türetildi):
  0 Kapak (proje/adres/müşteri/sorumlu + imza alanları + bağımsız birim sayısı + yönetmelik
  referansı — EK-10 10.2/1a) · 1 Giriş ("hesaplamaya dayalı ÖN DEĞERLENDİRME, resmi Akustik
  Performans Belgesi DEĞİL" uyarı kutusu — EK-10 10.1/4 ölçüm şartı) · 2 Mekan analizi
  (EK-2 Tablo 2.1 dereceleri) · 3 Dış gürültü verileri (EK-7 7.1.1) · 4 Sınır değerler
  (Madde 11/EK-3 + kapı toleransı) · 5 Yapı elemanları listesi + lab-ölçümü/model-tahmini
  veri kaynağı beyanı (Madde 16/1-a/b/ç) · 6 Hesap yöntemi beyanı (12354-1/2/3, Annex E,
  ISO 717, belirsizlik) · 7-9 Sonuç tabloları (DnT,A / DnT,A,tr+D2m,nT,50+iç gürültü /
  L'nT,w) · 10 Junction özeti ("detay çizimleri mimari projede" notu — EK-7 7.1.4/2) ·
  11 Sınıf ön değerlendirmesi · 12 Çözüm önerileri (yalnız limit sağlamayanlar) ·
  13 Standart listesi (EK-9 alt kümesi).
- **`SinifAtamaServisi`** (Core/Calculation): hedef sınıfa göre geçti/kaldı yerine hesap
  sonucundan **sağlanan sınıfı** türetir — A→F tarar, ilk geçen sınıf; F bile geçmezse
  "Sınıf dışı (F altı)"; hesap verisi yoksa **"performans belirlenemedi"** (EK-10 10.2/3
  ifadesi birebir). Bina düzeyi: özellik başına (dış cephe hava / bölme hava / darbe)
  en kötü eleman sınıfı; genel = değerlendirilebilen özelliklerin en kötüsü (EK-10/9
  en-alt-sınıf kuralı). Bu ÖN sınıftır; resmi sınıf ölçüm gerektirir.
- **Darbe boru hattı Core'a taşındı:** `KatlarArasiDarbeYoluServisi.DegerlendirilmisDarbeBolmeleri
  (proje)` — ViewModel'in Hesapla() akışındaki sentetik döşeme bölmesi üretimi + Lnw/LnTw
  hesabı artık Core'da paylaşılıyor (rapor servisleri ViewModel'siz aynı darbe satırlarını
  üretir). Sentetik bölme Id'si artık **deterministik** (`DarbeYoluId(kaynakMekanId,
  aliciMekanId)` XOR türetimi) — "Rapora Dahil" filtresi darbe satırları için de rapor
  yeniden üretimleri arasında çalışır (eski rastgele Guid.NewGuid ile eşleşmiyordu).
- `BinaProje`'ye kapak alanları eklendi: `MusteriAdi`, `SorumluKisiKurulus`
  (+ `BinaProjeMigrator` null-guard + Proje panelinde "RAPOR KİMLİK BİLGİLERİ" kutusu,
  ViewModel proxy'leri Kirlet() tetikler).
- HTML kaçışı: `WebUtility.HtmlEncode` KULLANILMAZ (Türkçe karakterleri sayısal entity'e
  çevirir, dosya zaten UTF-8) — yalnız 5 özel karakteri kaçışlayan yerel `H()` var.
- ~~Kapsam dışı bırakılan yönetmelik alanları: EK-4/EK-5/EK-6~~ → **§2d ile kapsama alındı
  (2026-07-17)**. Yalnız EK-8 (mekanik sistem nitel tasarım kuralları) kapsam dışı kaldı —
  hesaplanabilir içerik değil; raporun tesisat bölümünde bilgi notu olarak anılır.

### 2d. EK-4/EK-5/EK-6 Hesap Motorları — 2026-07-17

Rapor katmanının ardından üç yönetmelik alanı hesap kapsamına alındı (585 test geçer):

- **`EK4IcGurultuServisi`** — Tablo 4.1 mekan içi en yüksek gürültü düzeyleri (L_Aeq).
  Tablo aritmetik seridir (docx ile doğrulandı): her satırda A=C−8, B=C−4, D=C+4, E=C+8,
  F=C+12 → yalnız C değeri kodlanır, `SinifIcinDeger` türetir. Kendi anahtar-kelime
  eşleyicisi vardır (EK-2 kategorilerinden İNCE granülarite: konut mutfak/banyo, sağlık
  özel/çok-yataklı, kültürel konser-34/stüdyo-29 ayrımı); en uzun (en özgül) anahtar
  kazanır ("OZEL DERSLIK" > "DERSLIK"). Eşleşme yoksa null → `IcGurultuHedefi` 35 dBA
  varsayılanına düşer (eski davranış korunur).
  **Cephe entegrasyonu:** iç gürültü hedefi artık 3 yerde de (ViewModel.Hesapla,
  BinaAkustikRaporServisi, HTML rapor) sabit 35 değil alıcı mekanın Tablo 4.1 limiti.
  Yan bulgu: ViewModel'de Faz 2'de rapor servisinde düzeltilen "Lgag−35" ezmesinin bir
  benzeri daha vardı (`Math.Max(limit, Lgag−35)`) — Max olduğu için yanlış-limit riski
  yoktu ama artık `Lgag − Tablo4.1` konfor kriterine bağlandı.
- **`EK5TesisatServisi`** — Tablo 5.1 sürekli servis ekipmanı sınırı (L_Aeq,nT):
  kaynak metinde satır satır doğrulanan desen **Tablo 4.1 − 4 dB**; tablo yalnız 6 işlev
  için tanımlı (Konut/Eğitim/Sağlık/Büro/Konaklama/Yurt) — diğer işlevlerde null döner,
  UYDURMA DEĞER ÜRETİLMEZ (beyan girilmişse VeriEksik raporlanır). Tablo 5.2 kesikli
  ekipman (L_AF,max,nT): işlevden bağımsız tek satır C=34 serisi. Girdi: `Mekan.
  TesisatSurekliDb` / `Mekan.TesisatKesikliDb` (kullanıcı beyanı, 0=girilmedi →
  değerlendirilmez; MekanDuzenlemePanel'de NumericUpDown + Kirlet).
- **`EK6ReverberasyonServisi`** — Tablo 6.1 en yüksek reverberasyon süreleri (250–2000 Hz
  ort.). Dipnot 2: değerler yalnız C-D sınıfları için — diğer sınıflarda sonuç
  `UzmanKarariGerekir=true` bayrağıyla döner; dipnot 5 "-" satırları (tiyatro/konser/
  ibadet/spor/sanayi genel) `OzelTasarim=true` + limit null. RT ön tahmini Sabine
  `T = 0.161·V/A`, A = `Mekan.EtkiliEmilimAlani` (beyan yoksa 0.32·V → T≈0.5 s;
  emilim alanı girişi MekanDuzenlemePanel'e eklendi). Ayrıntılı iç hacim akustiği M01'in
  işidir — bu yalnız yönetmelik ön kontrolüdür.
- **Sonuç ekranı:** Hesapla() artık mekan bazlı "Tesisat" ve "Çınlama" HesapTuru satırları
  üretir (tesisat yalnız beyan varsa; çınlama Tablo 6.1 eşleşmesi olan her mekan için,
  özel-tasarım hariç). Top-3 en kötü bölme sıralaması Çınlama satırlarını DIŞLAR (marjı
  saniye cinsinden, dB marjlarıyla sıralamada karışır).
- **Sınıf motoru:** `SinifAtamaServisi`'ne `TesisatGurultusu` özelliği eklendi (EK-10
  10.1/5-ç) — beyan edilen düzey A→F taranır (sürekli Tablo 5.1, kesikli Tablo 5.2);
  beyansız projede özellik "belirlenemedi" olur ve genel sınıfı etkilemez.
- **HTML rapor:** yeni "10. Tesisat ve Servis Ekipmanı Gürültüsü (EK-5)" ve
  "11. Reverberasyon Süresi Ön Değerlendirmesi (EK-6)" bölümleri; sonraki bölümler
  12-15'e kaydı. Cephe bölümündeki iç hedef sütunu artık mekana göre değişir.

---

## 3. Veri ve Proje Kuralları

- M08 proje/autosave uzantısı `.e4p08` olarak kullanılır.
- Proje içeriği JSON tabanlıdır.
- Eski proje verileri eksik alanlar için güvenli varsayılanlarla açılmalıdır.
- Düşey patlatma/birleşim yıkıcı olmamalıdır:
  - Üst/alt mekanlar veri olarak korunur.
  - Görünümde tek isimli birleşim gibi davranır.
  - Partner mekan duvarları atama panelinde birlikte listelenir.
- Patlatılmış proje kalıcı kilitlenebilir; açılışta kullanıcı kilitli açma veya patlatmayı geri alma yolunu seçebilir.
- Mekan/bolme atama durumları merkezi bildirimlerle güncellenmelidir; elle mutasyon sonrası doğrulama ve çizim yenilenmelidir.

---

## 4. Hesap Kuralları

- Hava sesi hesabında otoriter yol 1/3 oktav spektral hesap olmalıdır.
- 1/3 oktav bant sırası:
  `100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150 Hz`
- Ölçülmüş `RThirdOctave` varsa doğrudan kullanılır.
- Ölçüm yoksa 1/1 oktav veriden log-frekans interpolasyonu veya fizik model tahmini kullanılır.
- Tek sayı `Rw`, `C`, `Ctr` ISO 717-1 16 bant sonucundan türetilir.
- Oktav panelindeki 7 bant görsel özet içindir; standart tek sayıların yerine geçmez.
- Açıklık + opak eleman bileşik yalıtımı enerji toplamıyla hesaplanır.
- Zayıf halka kuralı önemlidir: düşük R değerli pencere/kapı varsa opak duvar iyileştirmesi toplam cephe sonucunu sınırlayabilir.
- DnT,A hesabında lab `C/Ctr` yerine mümkünse apparent spektrumdan gelen `CHesap/CtrHesap` kullanılır.

---

## 5. Yanal İletim ve Junction Kuralları

- Hava sesi altyapısı `Dd + 4 x (Df + Fd + Ff)` 13-yol mantığına yakındır.
- Her ayırıcı bölme için alt, üst, sol, sağ junction varsayılanları üretilebilir.
- Junction alanları:
  - Kenar tipi: Alt, Üst, Sol, Sağ
  - Geometri tipi: T, L, X, Artı
  - Bağlantı tipi: Rijit, YarıElastik, Elastik, Kesintili
  - Uzunluk
  - Opsiyonel `KijOverrideDb`
- Kütle oranı (EN 12354-1 s.43): `M = lg(m'⊥,i / m'i)` — oran her yolda AYIRICI ↔ FLANK arasındadır; flank kütlesi iki yüzün ortalamasıdır. Ff: `lg(mD/mFlank)`, Df/Fd: `lg(mFlank/mD)`.
- Direkt elemana in-situ `T_s` düzeltmesi uygulanmaz; in-situ etki yanal iletimde Dv/absorpsiyon uzunluğu düzeltmeleri üzerinden ele alınır.
- Annex E formülleri (standart metniyle doğrulandı):
  - Rijit T in-line: `5.7 + 14.1*M + 5.7*M^2`, T branch: `5.7 + 5.7*M^2`
  - Rijit X cross: `8.7 + 17.1*M + 5.7*M^2`, X perp: `8.7 + 5.7*M^2`
  - L köşe: `15*|M| - 3`, minimum `-2 dB` (yalnız L'de negatif olabilir)
  - Hafif (E.7/E.8): `K13 = 10 + 20M − 3.3·lg(f/500)` min 10 dB; `K12 = 10 + 10|M| ± 3.3·lg(f/500)` (karışık E.7 → +, tüm-hafif E.8 → −)
  - Sandviç (E.5): rijit + `Δ1 = 10·lg(f/125)` (branch ×1, in-line/cross ×2; f ≤ 125 → 0)
- Sandviç sınıfında `BaglantiEklemesi` BASTIRILIR — esnek katman E.5 formülünde zaten içerilir (çift sayım yasağı).
- E.3 Limiting values: in-situ dönüşüm sonrası `D̄_v,ij ≥ 0 dB` kırpması zorunludur.
- Flanking yollarında ayırıcının R'si OPAK (açıklıksız) değerdir; kapı/pencere yapısal yola katılmaz.

---

## 6. Dış Cephe Kuralları

- Dış cephede `DeltaLfs` işareti pozitiftir:
  `D_2m,nT = R' + DeltaLfs + 10*log10(V/(6*T0*S))`
- ΔLfs kaynağı: EN 12354-3:2000 **Annex C Figure C.2** (Tablo 1 değil!) — standart metniyle birebir doğrulandı.
- Basit lookup değerleri (Figure C.2, 1,5–2,5 m görüş satırı):
  - Serbest alan: `0 dB` (form 1)
  - Trafik cephesi / yansıtıcı karşı cephe: `-3 dB` (C.2 dışı modelleme: Lgag yansıma-hariç tanımlıdır)
  - Balkon yansıtıcı (form 7, α≤0.3): `0 dB`
  - Balkon yutucu (form 7, α≥0.9): `+4 dB`
  - Loggia (form 8, α≥0.9): `+2 dB`
- `DeltaLfsTam(CepheFormu, CatiSogurmasi, GorusMesafesi)` Figure C.2 hücreleriyle birebir (Galeri=form 3, Balkon=form 7, Teras=form 9 açık çit).
- Dış cephe açıklıkları yanlış algılandıysa kullanıcı sol panelde ilgili duvarı seçip sağdaki Açıklıklar sekmesinden silebilmelidir.

---

## 6b. Yönetmelik Limit Kuralları

- Tüm sınır değerler EK-3 tablolarından okunur; eski R'w tabanlı sabit `GetLimit` tablosu kaldırılmıştır.
- Döşeme → Tablo 3.3 (KAYNAK odası derecesi × sınıf, `L'nT,w`); konutta aynı bağımsız birim içi döşeme → Tablo 3.5 (C: 62).
- Ortak duvar → Tablo 3.2 (kaynak × alıcı × sınıf, `DnT,A`); konutta aynı bağımsız birim içi bölme → Tablo 3.4 (C: 44).
- Dış cephe → Tablo 3.1: `DnT,A,tr ≥ Lgag − ofset(hassasiyet, sınıf)`; A–D sınıflarında 30 dB taban. Lgag cephe yansımaları HARİÇ ölçülür; `YonetmelikUyumServisi` metodlarına `disGurultuLgag` parametresiyle geçilir.
- Kapı içeren iç bölme elemanında sınır değer 14 dB düşürülür (Madde 11/3, `KapiToleransiDb`).
- Kaynak derecesi / alıcı sınıfı atanmamışsa limit 0 → "Veri Eksik" (sessiz varsayılan YOK).
- EK-2 Tablo 2.1 dereceleri `EK2SiniflandirmaServisi`'nde birebir kodludur; kategori ADLARI UI bağımlılığı nedeniyle değiştirilmez.

## 7. Darbe Sesi Kuralları

- Darbe sonuç satırlarında `IlgiliBolme` set edilmelidir; çözüm önerileri bu referansa ihtiyaç duyar.
- Darbe sesi için yüzer döşeme, kaplama/halı vb. öneriler `OneriUygula` akışından geçmelidir.
- Oktav darbe görünümünde tek sayı otoriter `ApparentLnw` değeridir; standart dışı 7-bant uyarlama tek sayı gibi kullanılmamalıdır.
- Yönetmelik karşılaştırması `L'nT,w` iledir: `L'nT,w = L'n,w − 10·lg(0.032·V)`; ViewModel her döşeme hesabından sonra `HesaplaApparentLnTw` çağırır.
- Darbe flanking (12354-2 Df): flanklar alıcı odanın DUVARLARIDIR; alıcı tavanı ayırıcı döşemenin kendisi olduğundan flank sayılmaz. `D̄_v,ij` flanking seviyesinden ÇIKARILIR (işaret negatif).

---

## 8. DXF ve Geometri Notları *(geometri üretimi M50'ye taşındı — tarihsel referans)*

- DXF çizimindeki kopukluklar veya çizgi boşlukları otomatik açıklık olarak algılanabilir.
- Isı haritası/heatmap üretimi raporlama motorundan bağımsız UI odaklı çalışmalıdır.
- Uzun duvar split/rebuild olduğunda eski malzeme atamaları parçalara aktarılmalıdır.
- Çok küçük duvarlarda atama korunması için sabit örtüşme eşiği kullanılmamalıdır; segment uzunluğuna orantılı eşik ve merkez-yakınlığı yedeği gerekir.
- `RefreshKomsuDuvarlar` veya 3D görünüm yenilemeleri kullanıcı atamalarını düşürmemelidir.

---

## 9. UI İş Akışı Notları

- Ana sekmeler: Yapı Elemanları, Filtre ile Duvar Ata, Atama Bekleyenler, Sınıflandırma, Sonuçlar.
- "Filtre ile Duvar Ata" sekmesi dış cephe, iç duvar, komşu bağımsız bölüm, kaynak derece, alıcı sınıfı, atama durumu, açıklık ve kat filtrelerini AND mantığıyla uygular.
- Sonuçlar tablosunda rapora dahil paneli başlangıçta kapalıdır; toggle ile açılır.
- Sonuç detayları, oktav bant, flanking yolları ve çözüm önerileri sağ açılır panelde gösterilir.
- Yapı elemanı panelinde duvar listesi, malzeme kütüphanesi ve seçili duvar düzenleme alanı birbirini kapatmamalıdır.
- Kat adı tablolarında kullanıcı tanımlı ad gösterilmelidir.
- Sınıflandırma veya atama bekleyenler satırına tıklamak ilgili kata/mekana/bolmeye navigasyon yapmalıdır.
- Görünür tüm metinlerde doğru Türkçe karakter kullanılmalıdır.

---

## 10. Kritik Dikkat Notları

- `OneriUygula` sonrası sonuçlar değişmiyorsa hesap cache/invalidation kontrol edilmelidir; her hesap öncesi eski akustik çıktılar temizlenmelidir.
- Spektral verili elemana ilave kaplama uygulanırken `DeltaRThirdOctave` kopyalanıp artış bant bazında eklenmelidir.
- Tek-sayı Rw'den sentetik `R(f)` üretiliyorsa ISO 717 sonucu nominal Rw'ye kalibre edilmelidir.
- Oktav gösteriminde 1/3 oktav sonucu varsa composite/flanking ikinci kez uygulanmamalıdır.
- DnT,A marjı lab adaptasyon terimiyle değil apparent spektrumla karşılaştırılmalıdır.
- Açıklıklar zayıf halka olabilir; pencere/kapı varken opak duvar iyileştirmesi sınırlı etki gösterebilir.
- 2026-06-22 hesap motoru kararı: `EnsureDefaultJunctions` mevcut junction kullanıcı ayarlarını
  hesap sırasında ezmez; `YapiElemani.FlankingDuzeltmesiDb` otomatik `KijOverrideDb` değildir.
  Açık Kij override yalnız `Junction.KijOverrideDb` alanındadır.
- Dış cephe spektral hesabı oda düzeyinde, aynı odadaki tüm dış cephe parçalarının enerji toplamıyla
  yapılmalıdır; tek bir cephe parçası oda toplamını temsil etmez.
- **2026-07-14 Faz 1 veri güvenliği paketi tamamlandı** (544 test geçer, Core.Tests 121):
  - P0 — `OdaTespitServisi.DurumuAktar` artık Menfez ve Junction'ı da (yalnız YapiElemani/
    Acikliklar değil) geometrik örtüşmeyle eski bölmeden yeni bölmeye aktarıyor. Kök neden:
    bu servis `Guncelle3DGorunum()` üzerinden (25 çağrı noktası — kat değişimi, ısı haritası
    toggle, her düzenleme sonrası) ve `KaydetYola` içinde HER ÇAĞRIDA çalışıyordu; kullanıcının
    junction/menfez ayarları neredeyse her etkileşimde sessizce siliniyordu — yalnız "Duvar
    Kontrolü" butonuna özgü değildi.
  - P0 — `DuvarKontrol()` artık kendi elle Bolmeler.Clear()+yeniden-inşa mantığını değil,
    `RefreshKomsuDuvarlar`'ın kullandığı aynı güvenli `OdaTespitServisi.KomsuluklariTespitEt`
    yolunu kullanıyor. Bilinçli karar: onay diyaloğu EKLENMEDİ — düzeltme sonrası işlem zaten
    kayıpsız ve `RefreshKomsuDuvarlar` da onaysız otomatik çalıştığından tutarlılık için.
  - P1 — `IsDirty` eklendi (`BinaProjesiViewModel.Kirlet()/TemizIsaretle()`, pencere başlığında
    `*`, kapanış/Yeni Proje/Proje Aç'ta `OnayKaydedilmemisDegisiklik` sorusu). Otomatik izleme
    Katlar/Mekanlar/Bolmeler ObservableCollection + nested PropertyChanged aboneliğiyle çalışır;
    Menfez/Junction/Acikliklar `List<T>` olduğundan (INotifyPropertyChanged yok)
    `BolmeMalzemePanel.OnAciklikDegisti`/`OnJunctionDegisti` kancalarıyla ayrıca işaretlenir.
    Kütüphane (`YapiElemaniKutuphanesi`/`AciklikOnayarKutuphanesi`) kasıtlı olarak IsDirty'e
    dahil değildir — kendi global dosyasına ayrı "Kaydet" ile yazılır, proje dosyasının parçası
    değildir. Hesapla() de kasıtlı dahil değildir (sonuç alanları serileşse de "hesapla" kullanıcı
    gözünde "değişiklik" sayılmaz).
  - P1 — `KaydetYola` artık atomik: `.tmp`'ye yazıp `File.Move(overwrite:true)` ile hedefe taşır.
    Otomatik yedek (`.autosave-*`) ayrı dosyaya yazdığından IsDirty'yi temizlemez — yalnız
    `_sonKaydedilenYol`'a yazım `TemizIsaretle()` tetikler.
  - P1 — `BinaProjeMigrator.Normalize` (Core/BuildingAcoustics/Parsers) eklendi; `ProjeAc` artık
    teker teker alan kopyalamak yerine `Proje = yuklenen` atıyor — eskiden atlanan `Id`, `Adres`,
    `OlusturmaTarihi`, `YapiElemaniKutuphanesi`, `AkustikNoktalar`, `DisGurultuKaynaklari` artık
    korunuyor; açık JSON null listeleri Normalize ile güvenli varsayılana çekiliyor.
- **2026-07-14 Faz 2 rapor doğruluğu (hedefli düzeltmeler) tamamlandı** (547 test geçer, Core.Tests 121):
  - P0 — `BinaAkustikRaporServisi.DisGurultuUygulanmisLimit` artık `proje.DisGurultuDb`'yi ilk
    `KontrolEtDetayli` çağrısına geçiyor. Eskiden Lgag=0 varsayımıyla (yalnız 30 dB taban) limit
    hesaplanıp ardından yanlış düz `Lgag−35` formülüyle eziliyordu — Tablo 3.1'in gerçek
    `Lgag − ofset(hassasiyet, sınıf)` formülünden farklı sonuç veriyordu (ör. C sınıfı/I
    hassasiyet/Lgag=65 → doğrusu 43 dB, eski kod 30 dB üretiyordu; UI "kaldı" derken metin/Excel
    raporu "geçti" diyebiliyordu). İç gürültü hedefi (35 dBA) ek konfor kontrolü korunuyor.
  - P1 — `DxfRenkliCiktiServisi` artık `proje.AkustikPerformansSinifi`/`proje.DisGurultuDb`'yi
    marj/limit hesabına geçiyor (eskiden hiç okunmuyordu, sınıf C + Lgag=0 varsayımına
    düşüyordu). Döşeme etiketi `ApparentLnTw`/"L'nT,w" kullanıyor (eski `ApparentLnw`/"L'n,w"
    değil). Dış cephe etiketi UI ile tutarlı `HesaplaSpektralDisCephe(...).DnTATr`/"DnT,A,tr"
    kullanıyor (eski ham `bolme.Dntw`/"DnT,w" değil) — bkz. §2b Faz 2 mimari borç notu (iki farklı
    dış cephe `Deger` yolu arasındaki tutarlılık doğrulanmadı).
  - **Yan bulgu (DXF dosya bozulması):** `DxfRenkliCiktiServisi`'nin mekan-adı ve lejant
    etiketlerinde gömülü `"\n"` karakteri vardı; netDxf'in `Text` entity'si tek satırlıktır ve
    gömülü satır sonu DXF'in satır-tabanlı ASCII yapısını bozup dosyayı netDxf'in kendi
    `DxfDocument.Load`'ında bile okunamaz hale getiriyordu (sessizce `null` dönüyordu, istisna
    fırlatmıyordu). İki ayrı tek-satır `Text` entity'sine bölündü. Round-trip testiyle yakalandı
    (`BuildingDxfHonorsPerformanceClassAndLgag`) — bu servisin çıktısı daha önce hiç geri
    okunarak doğrulanmamıştı.
  - P1 — Aynı dosyada, her çağrıda AYNI isimli ("UYGUN" vb.) YENİ `Layer` nesneleri üretiliyordu;
    artık üst metotta bir kez oluşturulan paylaşılan `Layer` nesneleri `SecLayerVeEtiket`'e
    parametre olarak geçiliyor (potansiyel yinelenen katman kaydı riski gideril­di).
  - P0 — "Rapora Dahil" seçimi artık gerçek bir filtre: `RaporSatiriViewModel.BolmeId`
    (kararlı `Guid`, eskiden yalnız Mekan+Bolme+HesapTuru ad üçlüsüyle eşleşiyordu — aynı adlı
    oda/bölmelerde yanlış satırı eşleştirebilirdi) → `BinaProjesiViewModel.RaporHaricTutulanBolmeIdleri`
    → `BinaAkustikRaporServisi.MetinRaporOlustur/TabloVerisiOlustur/ExcelRaporOlustur` ve
    `DxfRenkliCiktiServisi.DxfRenkliRaporOlustur`'a `ISet<Guid>? haricTutulanBolmeIdleri`
    parametresi olarak akıyor. DXF'te hariç tutulan bölme SİLİNMEZ (gerçek duvardır), nötr gri
    "Hariç Tutuldu" katmanına alınır.
  - P0 — Sonuç bayatlama: `Kirlet()` her çağrıda `_modelRevizyonu`'nu artırır; `Hesapla()`
    sonunda `_hesapRevizyonu = _modelRevizyonu` ile "güncel" damgalanır. `SonuclarGuncelMi`/
    `SonuclarBayatMi`/`DisaAktarmaEtkin` bu ikisinin eşitliğinden türer. `SonuclarPanel`'de
    kırmızı "Sonuçlar güncel değil" bandı + 5 dışa aktarım düğmesi `DisaAktarmaEtkin`'e bağlı
    (boş projede VE bayat sonuçta devre dışı — ikisi de eskiden aktif kalıyordu).
  - P1 — `VeriEksik` durumu artık metin raporunda "⚠️ VERİ EKSİK" (eskiden "❌ KALDI"'ya
    düşüyordu), Excel'de ayrı sarımsı renk (eskiden `Kaldi` ile aynı kırmızı).
  - Kullanılabilirlik — "Excel · Sadece Limit Aşan" tooltip'i ters okunuyordu ("aşmayan" diyordu,
    filtre aslında yalnız limiti AŞAN/kaldı satırları gösteriyor); düzeltildi.
- **2026-07-14 Faz 3 darbe geometrisi tamamlandı** (549 test geçer, Core.Tests 121):
  - P1 — `KatlarArasiDarbeYoluServisi` artık `ClipperAdaptor.Kesisim`/`NetAlan` ile GERÇEK
    poligon kesişimi kullanıyor (eskiden dikdörtgen bounding-box örtüşmesiydi; L/U/eğik/konkav
    odalarda gerçekte çakışmayan mekanlar arasında yanlışlıkla darbe yolu üretilebiliyordu).
    `BulAltMekanlar` eklendi (`IReadOnlyList<KatlarArasiDarbeYolu>`) — bir üst oda birden fazla
    alt odaya taşıyorsa artık HEPSİ işlenir (eskiden yalnız en fazla örtüşen tek alt mekan
    dönüyordu, ikinci/üçüncü alıcı sessizce atlanıyordu). `BulAltMekan` (tek, nullable) geriye
    dönük uyum için `BulAltMekanlar(...).FirstOrDefault()` olarak korundu.
  - **Yan bulgu (daha önce fark edilmemiş, P1 seviyesinde):** Darbe "en zayıf" özet satırları
    (`Hesapla()`'nın grup-başı seçim bloğu, Darbe dalı) `IlgiliBolme`'yi HİÇ set etmiyordu —
    yalnız Hava dalı set ediyordu. Bunun sonucu: `OneriUygula`'nın `satir.IlgiliBolme is not
    null` koruması darbe sonuçlarında HER ZAMAN başarısız oluyordu; "Uygula" butonu ve flanking-
    yolu gösterimi darbe sonuçlarında sessizce hiç çalışmıyordu (yalnız çok-yüzey-klonlama riski
    değil, tamamen işlevsizlik). Artık `enZayif.IlgiliBolme` kopyalanıyor.
  - P1 — `OneriUygula`'nın darbe dalı eskiden 4 rolü (KaynakMekan.Zemin/Tavan, AliciMekan.Zemin/
    Tavan) nesne-referans eşitliğiyle tarıyordu; aynı katalog döşeme elemanı birden fazla odanın
    zemin/tavanında kullanılıyorsa (kütüphaneden paylaşılan tip — yaygın), bir darbe yolunu
    iyileştirme önerisi KaynakMekan'ın kendi tavanını (bambaşka bir üst kat yolu) veya
    AliciMekan'ın kendi zeminini (bambaşka bir alt kat yolu) sessizce değiştirebiliyordu. Artık
    yalnız KaynakMekan.ZeminYapiElemani + AliciMekan.TavanYapiElemani (bu darbe yolunun fiziksel
    olarak AYNI döşeme plağı olan iki rolü) güncelleniyor; diğer ikisine dokunulmuyor. Ayrıca
    metotta kalmış `Console.WriteLine` debug çıktıları temizlendi.
- **2026-07-14 Faz 4 iş akışı sadeleştirmesi kısmen tamamlandı** (552 test geçer, Core.Tests 121):
  - Araştırma: kat yüksekliği (`SeciliKatYuksekligi`) ve kat ofseti (`KatOfsetX`/`KatOfsetY`)
    zaten önceki bir "4.4" temizliğinde M50'ye yönlendirilmişti (XAML'de salt-okunur
    `TextBlock`; hiçbir kontrol bu property'lere yazmıyordu) — ama ViewModel'de hâlâ erişilemez
    mutasyon kodu (setter + `ApplySelectedFloorHeight()` + `OnKatOfsetXChanged`/
    `OnKatOfsetYChanged` + `SyncKatOfset` + yetim `OfsetPaneliAcik` toggle) duruyordu. Bu ölü
    kod tamamen silindi; `SeciliKatYuksekligi` artık salt get-only (M50'den
    `BinaGeometriMekanAdapteri` ile gelen `Kat.KatYuksekligi`'ni salt-okunur yansıtır).
  - **Bilinçli olarak dokunulmayan: `ProjeOlcek`.** Bu araç hâlâ CANLI ve gerçekten
    `Mekan.Poligon`'u yerinde ölçeklendiriyor (`OnProjeOlcekChanged`, DXF arka planı + oda
    poligonları + `Bolme` uç noktaları). M50'de manuel ölçek düzeltme eşdeğeri YOK (yalnız
    otomatik birim tahmini var); kullanıcı muhtemelen bunu yanlış algılanan DXF ölçeğini
    düzeltmek için kullanıyor. Kaldırmak işlevsel gerileme olurdu — kullanıcı bunu onayladı,
    dokunulmadı. Not: zaten her M50 geometri yeniden yüklemesinde (`BinaGeometriMekanAdapteri.
    KatiGuncelle`) `Mekan.Poligon` M50 kaynağından ezildiğinden, M08'deki ölçek düzeltmesi
    kalıcı değildir — yalnız bir sonraki `.e4bina` yüklemesine kadar geçerlidir. M50'ye kalıcı
    bir "DXF birim/ölçek düzeltme" özelliği eklenmeden bu gerilim çözülmeyecek.
  - "Adım göstergesi" (Geometri → Sınıflandırma → Malzeme → Kontrol → Hesap → Rapor) eklenmedi
    — ayrı, katkısal bir UI özelliği; bu oturumda kapsam dışı bırakıldı.
- **2026-07-14 Faz 5 sonuç ekranı sadeleştirmesi tamamlandı** (570 test geçer, Core.Tests 121;
  bu Faz'da yeni test eklenmedi — saf XAML/UI değişikliği, mevcut test seti Avalonia görsel
  render'ını doğrulamıyor, yalnız `dotnet build` compiled-binding kontrolüyle ve kısa bir
  başlatma testiyle doğrulandı):
  - `SonuclarPanel` DataGrid'i artık varsayılan olarak dar/karar-odaklı sütun setiyle açılıyor:
    Kat, Mekan, Tür, Durum, Hesaplanan±σ, Limit, **Marj (yeni sütun, `MarjDb`)**, Güven, Bölme.
    10 teknik/ayrıntı sütunu (Komşuluk, Bağımsız Bölüm, Dış Lgag, İç/Hedef, D2m,nT,50,
    Baseline, Δ, Birim, Rezonans, Yönetmelik) **silinmedi** — `SonucTablosuGenisGorunum`
    (`BinaProjesiViewModel`, varsayılan `false`) bool'una bağlı `DataGridColumn.IsVisible`
    ile varsayılan gizli, başlık satırındaki "Sade Görünüm / Tüm Sütunlar" checkbox'ıyla açılır.
  - Seçili satırın TÜM ayrıntısı (oktav bant, flanking yolları, çözüm önerileri) zaten sağ
    panelde her zaman görünür durumda kalıyor — sadeleştirme yalnız ana tabloyu etkiler.
- **2026-07-14 Faz 6 test kapsamı + `Mekan.AtamaTamamMi` düzeltmesi TAMAMLANDI** (578 test geçer,
  Core.Tests 121 — Faz 1-6 kapsamı burada kapanıyor):
  - `MetinRaporOlustur`'un `VeriEksik` durumunu satır bazlı `Kaldi`'dan ayırt ettiğini doğrulayan
    test eklendi; `BinaProjesiViewModel`'in `Hesapla()` sonrası `SonuclarGuncelMi=true` /
    `SonuclarBayatMi=false` başladığını, sonra bir `Bolme.YapiElemani` değişikliğinin (Kirlet()
    tetikler) `SonuclarBayatMi=true`/`DisaAktarmaEtkin=false`'a çevirdiğini doğrulayan ilk
    ViewModel-seviyeli test eklendi (bu proje testlerinde `BinaProjesiViewModel`'in doğrudan
    örneklendiği ilk yer — Avalonia dispatcher'sız konsol test koşucusunda sorunsuz çalıştı).
  - P1 — `Mekan.AtamaTamamMi` artık her `Bolme.Acikliklar`'daki (kapı/pencere) `YapiElemani`'nin
    de atanmış olmasını şart koşuyor (eskiden yalnız duvar/döşeme/tavan kontrol ediyordu; eksik
    açıklık malzemesiyle oda yeşil "atama tamam" görünebiliyordu, hesap motoru sessizce 28/32 dB
    varsayılana düşüyordu). `BolmeAcikligi` `INotifyPropertyChanged` uygulamadığından,
    `BinaProjesiWindow.axaml.cs`'teki `OnAciklikDegisti` kancasına `SeciliMekan?.
    BildirAtamaDurumu()` eklendi — açıklık malzemesi atandığında yeşil işaretleme anında tazelenir.

---

## 11. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/BuildingAcoustics/Models/`
- `src/Kst.Acoustics.Core/BuildingAcoustics/Calculation/`
- `src/Kst.Acoustics.Core/BuildingAcoustics/Parsers/`
- `src/Kst.Acoustics.Core/BuildingAcoustics/Reports/`
- `src/Kst.Acoustics.Core/BuildingAcoustics/Database/`

App:
- `src/Kst.Acoustics.App/BuildingAcoustics/Views/BinaProjesiWindow.axaml`
- `src/Kst.Acoustics.App/BuildingAcoustics/Views/SonuclarPanel.axaml`
- `src/Kst.Acoustics.App/BuildingAcoustics/Views/BolmeMalzemePanel.axaml`
- `src/Kst.Acoustics.App/BuildingAcoustics/Views/MekanDuzenlemePanel.axaml`
- `src/Kst.Acoustics.App/BuildingAcoustics/ViewModels/BinaProjesiViewModel.cs`

Test:
- `src/Kst.Acoustics.Tests/`

---

## 12. Korunması Gereken Regresyon Alanları

- 1/3 oktav verinin apparent `Rw/C/Ctr` sonucunu sürmesi
- Junction 13-yol flanking davranışı
- Annex E T-junction formül yönü
- Annex E L-köşe formülü (`15|M|−3`, min −2) ve `D̄_v,ij ≥ 0` kırpması
- M oranının ayırıcı↔flank tanımı (flank↔flank değil)
- Darbe flanking'inin Dv ile AZALMASI ve tavan self-flank dışlaması
- Döşeme uyum/marj hesabının `ApparentLnTw` kullanması (ViewModel kablolaması)
- Direkt Rw'nin yapısal `T_s` değerinden bağımsız kalması
- Sentetik `R(f)` değerinin nominal Rw'ye kalibre edilmesi
- DnT,A'nın apparent `C/Ctr` kullanması
- Rapor ve UI sonuç değerlerinin `DnT,A` / `DnT,A,tr` / `L'nT,w` yönetmelik büyüklükleriyle aynı
  hesap yolunu kullanması
- Oktav gösterimin açıklık/flanking çift saymaması
- Cephe `DeltaLfs` işareti ve değerleri
- Çoklu dış cephede spektral `DnT,A,tr`, iç gürültü ve `D2m,nT,50` hesabının oda-toplam cephe
  alanı/spektrumuyla yapılması
- Darbe sonuç satırında çözüm önerilerinin çalışması
- Küçük duvar atamasının rebuild sonrası korunması
- Düşey birleşimde partner duvarların atama panelinde görünmesi
- **Menfez ve Junction atamalarının `KomsuluklariTespitEt` rebuild'inde (Duvar Kontrolü, her
  ekran yenilemesi, kaydetme) korunması** — yalnız YapiElemani/Acikliklar değil
- `KaydetYola`'nın atomik yazımı (`.tmp` + `Move`) ve otomatik yedeğin ana dosya IsDirty
  durumunu etkilememesi
- `ProjeAc`'ın `Id`/`Adres`/`OlusturmaTarihi`/`YapiElemaniKutuphanesi`/`AkustikNoktalar`/
  `DisGurultuKaynaklari` alanlarını kaybetmeden yüklemesi (`Proje = yuklenen` + `BinaProjeMigrator`)
- Dış cephe rapor limitinin (`BinaAkustikRaporServisi`) Tablo 3.1 `Lgag − ofset` formülünü
  kullanması — `disGurultuLgag` parametresi ilk `KontrolEtDetayli` çağrısına geçilmeli, düz
  sabit bir formülle ezilmemeli
- `DxfRenkliCiktiServisi`'nin `proje.AkustikPerformansSinifi`/`proje.DisGurultuDb`'yi kullanması,
  döşemede `ApparentLnTw`, dış cephede `HesaplaSpektralDisCephe(...).DnTATr` göstermesi
- netDxf `Text` entity değerlerine ASLA gömülü `"\n"` konmaması (dosyayı sessizce bozar,
  netDxf'in kendi `Load`'ı bile `null` döner) — çok satırlı etiket gerekiyorsa ayrı `Text`
  nesneleri kullanılmalı
- "Rapora Dahil" (`RaporHaricTutulanBolmeIdleri`) seçiminin metin/Excel/DXF dışa aktarımlarını
  gerçekten filtrelemesi (Bolme.Id tabanlı, ad tabanlı değil)
- Sonuç bayatlama: `Kirlet()` sonrası `SonuclarBayatMi=true` olması ve `Hesapla()` sonrası
  tekrar `false`'a dönmesi; `DisaAktarmaEtkin`'in boş/bayat durumda dışa aktarımı kapatması
- `VeriEksik` durumunun metin/Excel raporunda `Kaldi` ile aynı görünmemesi (ayrı etiket/renk)
- `KatlarArasiDarbeYoluServisi`'nin gerçek poligon kesişimi (Clipper2) kullanması — bbox'a
  geri dönülmemeli; L/konkav odalarda yanlış eşleşme üretmemeli
- Bir üst odanın birden fazla alt odaya taştığı durumda `BulAltMekanlar`'ın TÜM alt mekanları
  döndürmesi (yalnız en fazla örtüşeni değil)
- Darbe özet satırlarında `IlgiliBolme`'nin set edilmesi (Hava dalıyla aynı) — aksi halde
  "Uygula" ve flanking-yolu gösterimi darbe sonuçlarında sessizce çalışmaz
- `OneriUygula`'nın darbe dalının yalnız `KaynakMekan.ZeminYapiElemani` +
  `AliciMekan.TavanYapiElemani`'ni güncellemesi — paylaşılan katalog döşemesi üzerinden
  alakasız bir odanın zemin/tavanını klonlamaması
- `SeciliKatYuksekligi`'nin salt get-only kalması (kat yüksekliği/ofseti yalnız M50'de
  düzenlenir) — yeni bir "düzenle" UI'ı eklenirse önce M50'de eşdeğerinin olup olmadığı
  kontrol edilmeli
- `ProjeOlcek`'in kaldırılması/salt-okunur yapılması **M50'ye bir manuel ölçek düzeltme
  özelliği eklenmeden yapılmamalı** (bkz. §2b Faz 4 notu — bilinçli, belgelenmiş istisna;
  "M50 salt-okunur kuralını ihlal ediyor" diye tek başına kaldırmak kullanıcı için işlevsel
  gerileme olur)
- `SonuclarPanel` teknik/ayrıntı sütunları (`SonucTablosuGenisGorunum` ile gizlenenler)
  SİLİNMEMELİ, yalnız varsayılan gizli kalmalı — Excel/metin/DXF dışa aktarımları bu
  sütunlardaki veriyi (Baseline/Δ/Rezonans/Yönetmelik vb.) hâlâ kullanır, yalnız ana tablo
  görünümü etkilenir
- `Mekan.AtamaTamamMi`'nin her `Bolme.Acikliklar`'daki (kapı/pencere) malzemeyi de kontrol
  etmesi — yalnız duvar/döşeme/tavan yeterli değildir
- Açıklık malzemesi atandıktan sonra mekan listesindeki yeşil işaretlemenin anında tazelenmesi
  (`BinaProjesiWindow.axaml.cs`'teki `OnAciklikDegisti` → `SeciliMekan?.BildirAtamaDurumu()`)
- `SinifAtamaServisi`'nin sağlanan sınıfı A→F tarayarak türetmesi ve EK-10/9 en-alt-sınıf
  kuralı; hesap verisi olmayan eleman "performans belirlenemedi" (EK-10 10.2/3 ifadesi)
- Darbe sentetik bölme Id'sinin deterministik kalması (`KatlarArasiDarbeYoluServisi.
  DarbeYoluId`) — hem ViewModel Hesapla() hem Core rapor boru hattı aynı Id'yi üretmeli,
  aksi halde "Rapora Dahil" filtresi darbe satırlarında kopar
- HTML raporda Türkçe karakterlerin entity'e çevrilmemesi (yerel `H()` yalnız 5 özel
  karakteri kaçışlar; `WebUtility.HtmlEncode` kullanılmaz)
- HTML raporun Madde 5/5 bölüm iskeletini ve "hesaplamaya dayalı ön değerlendirme / resmi
  Akustik Performans Belgesi değildir" beyanını koruması
- EK-4 Tablo 4.1'in aritmetik serisi (A=C−8 … F=C+12) ve en-özgül-anahtar eşleşmesi
  ("OZEL DERSLIK" > "DERSLIK"); eşleşmeyen mekanda 35 dBA varsayılanına düşme
- Cephe iç gürültü hedefinin 3 yolda da (ViewModel/metin-Excel/HTML) EK-4'ten gelmesi —
  sabit 35'e geri dönülmemeli
- EK-5 Tablo 5.1 = Tablo 4.1 − 4 dB türetimi ve 6-işlev kapsam sınırı (kapsam dışı işlevde
  null — uydurma limit üretilmez, beyan varsa VeriEksik)
- EK-6 dipnot 2 (C-D dışı sınıflarda UzmanKarariGerekir) ve dipnot 5 (özel tasarım
  hacimlerinde limit yok) davranışları; Sabine ön tahmininin `EtkiliEmilimAlani` kullanması
- Tesisat/Çınlama sonuç satırlarının yalnız ilgili durumda üretilmesi (tesisat: beyan varsa;
  çınlama: Tablo 6.1 eşleşmesi olan, özel-tasarım olmayan mekanlar) ve Top-3 sıralamasının
  Çınlama satırlarını dışlaması (saniye/dB marj karışması)

---

## 13. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M08 regresyonları dahil mevcut test paketi geçmeli

---

## 14. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Hesap değişikliği için önce `Core/BuildingAcoustics/Calculation` dosyalarını incele.
4. Geometri/import değişikliği için parser ve model dosyalarını kontrol et.
5. UI değişikliği için ilgili `BuildingAcoustics/Views` ve `BinaProjesiViewModel` alanlarını incele.
6. Formülasyon veya regresyon riski varsa test ekle veya mevcut M08 testlerini çalıştır.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.

## 5 Kasım 2026 — sıralı modül denetimi

Kütüphane kayıt hatası testi Windows UnauthorizedAccessException davranışıyla uyumlu hale getirildi; üretim hataları yutulmuyor (T01).

Ayrıntılı hata listesi ve doğrulama: depo kökündeki `modul-denetimi-2026-11-05.md`.
