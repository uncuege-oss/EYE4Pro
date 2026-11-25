# M03 Codex — Ses Yalıtımı ve Duvar Paneli

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları, kritik sınırlamalar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M03 — Ses Yalıtımı ve Duvar Paneli |
| Amaç | Katmanlı duvar/panel sistemlerinde ses geçiş kaybı ve tek sayı performans hesabı |
| Ana standart | ISO 717-1 |
| Referans standartlar | ISO 10140, ISO 12354, ASTM E90, ASTM E413, ASTM E1332 |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Tamamlandı; gelişmiş modelleme ve bakım sürüyor |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Katmanlı duvar/panel sistemi tanımı
- Tek katmanlı masif levha hesabı
- Çift cidarlı, hava boşluklu ve dolgulu sistem hesabı
- Çok yapraklı sistem uyarıları ve ampirik penaltı mantığı
- Mekanik bağlantı/köprüleme etkileri
- Resilient channel, klips ve karkas etkileri
- Gözenekli dolgu etkileri
- Kompozit alan hesabı: kapı, pencere veya zayıf elemanlı duvarlar
- `R/TL/STL` frekans eğrisi
- `Rw`, `C`, `Ctr`, tahmini `STC`, tahmini `OITC`
- Malzeme kütüphanesi, kullanıcı malzemeleri, CSV içe/dışa aktarma
- Metin raporu, güvenilirlik puanı, uyarılar ve sınırlayıcı frekanslar
- Empirical, TMM ve Hybrid hesap yöntemi seçimi

Kapsam dışı / model sınırı:
- Bina ölçeğinde flanking transmission M03 kapsamı değildir; M08 tarafına aittir.
- Resmi laboratuvar sertifikası yerine geçmez; mühendislik tahminidir.
- Katı-katı bağlı arayüzler ve PVB gibi viskoelastik laminasyonlar tam global-matris yöntemiyle çözülmez.
- Mekanik köprüleme TMM içinde doğrudan modellenmez; empirical/hybrid yaklaşımda dikkate alınır.

---

## 3. Hesap Yöntemleri

Hesap yöntemi `CalculationMethod` ile seçilir:
- `Empirical` — varsayılan pratik mühendislik modeli
- `TMM` — Transfer Matrix Method
- `Hybrid` — düşük frekansta TMM, yüksek frekansta empirical, ara bölgede smooth geçiş

Empirical model:
- Kütle kanunu
- Kritik frekans / coincidence düzeltmesi
- Çift cidar kütle-hava-kütle rezonansı
- Gözenekli dolgu katkısı
- Mekanik köprüleme ve bağlantı tipi etkisi
- Triple-leaf uyarısı/penaltısı

TMM model:
- Kompleks 2x2 zincir matris çarpımı
- Plate, air, porous ve solid layer dalları
- Poröz katmanlar için Johnson-Champoux-Allard (JCA) etkin akışkan yaklaşımı
- Elastik veri bulunan masif katmanlarda Allard-Atalla katı tabaka matrisi
- Alan/field incidence integrali 0-78 derece aralığında örneklenir

Hybrid model:
- 300 Hz altı TMM davranışı baskın
- 700 Hz üstü empirical davranışı baskın
- Ara bant smoothstep ağırlığıyla harmanlanır

---

## 4. Frekans ve Tek Sayı Kuralları

- Ana hesap 1/3 oktav bantlarında yürür.
- ISO 717-1 için gereken bantlar eksikse `Rw` sahte varsayımla hesaplanmamalıdır.
- `Rw` ISO 717-1 referans eğrisi kaydırma yöntemiyle hesaplanır.
- Olumsuz sapmalar ve sınırlayıcı frekanslar sonuç modelinde tutulur.
- `C` ve `Ctr` spektrum adaptasyon terimleri `Rw`'den ayrı hesaplanır.
- `STC` ve `OITC` tahmini değerlerdir; kesin ASTM sertifikası gibi sunulmamalıdır.
- OITC için mevcut 1/3 oktav bant verileri kullanılır; lisanslı kesin ASTM tablo doğrulaması ileride ayrıca yapılmalıdır.

---

## 5. Katman ve Malzeme Kuralları

Desteklenen katman/malzeme türleri:
- Beton, tuğla, gazbeton, alçı levha, lifli alçı
- OSB, MDF, kontrplak ve ahşap türevleri
- Cam, lamine cam, metal levha
- Vinil bariyer, kauçuk membran, bitümlü levha
- Mineral yün, cam yünü, polyester elyaf ve akustik keçe
- Hava boşluğu
- Karkas, resilient channel ve klips bağlantı şablonları

Malzeme alanları:
- Yoğunluk, kalınlık, yüzey kütlesi
- Young modülü, Poisson oranı, kayıp faktörü
- Kritik frekans
- Akış direnci, porozite, tortuozite
- Viscous length ve thermal length; eksikse tahmini türetilebilir
- Üretici, kaynak, güvenilirlik ve referans

Kütüphane kuralları:
- Yerleşik ve kullanıcı tanımlı malzemeler ayrı tutulur.
- `SaveLibrary` yalnız kullanıcı malzemelerini diske yazar.
- Kaynak ve güvenilirlik bilgisi rapora taşınmalıdır.
- CSV içe/dışa aktarma invariant culture ve `;` ayracıyla çalışır; tırnak ve satır sonu kaçırılmalıdır.

---

## 6. UI İş Akışı

- Ses yalıtımı ekranı katman tabanlı tasarım arayüzüdür.
- Katman ekleme, silme, yukarı/aşağı taşıma desteklenir.
- Malzeme seçimi ve kalınlık/parametre düzenleme canlı hesap tetikler.
- UI hesapları 150 ms debounce ile çalıştırır; sürükleme/yazma sırasında donmamalıdır.
- Bağlantı tipi ve dikme aralığı UI'dan seçilebilir.
- Grafik TL eğrisi, Rw referans eğrisi, kritik frekans ve MAM rezonans işaretlerini gösterebilir.
- TMM overlay opsiyonel olarak empirical/hybrid eğrinin üstüne çizilebilir.
- Sonuç panelinde `Rw (C; Ctr)`, `STC`, `OITC`, uyarılar, eksik bantlar, kritik frekanslar ve güvenilirlik gösterilir.
- "PDF" gibi yanıltıcı etiket kullanılmaz; mevcut çıktı metin raporudur.

---

## 7. Kompozit Hesap

- Birleşik sistemler alan ağırlıklı iletim katsayısı üzerinden hesaplanır.
- Zayıf elemanlar toplam yalıtımı ciddi düşürebilir; kullanıcıya açıkça gösterilmelidir.
- Kompozit hesapta mevcut sistem alan değeriyle listeye eklenebilir.
- Kompozit `Rw` ve bileşenlerin etkisi UI ve raporda görünür olmalıdır.

---

## 8. Raporlama ve Güvenilirlik

Metin raporunda bulunması gerekenler:
- Sistem adı ve katman listesi
- Katman fiziksel özellikleri
- Malzeme kaynakları ve referanslar
- Frekans bazlı `R/TL` tablosu
- `Rw`, `C`, `Ctr`, varsa `STC`, `OITC`
- Kritik frekanslar ve MAM rezonansı
- Sınırlayıcı bantlar
- Hesap uyarıları
- Güvenilirlik puanı
- Model sınırlamaları

Güvenilirlik puanı:
- Laboratuvar/kaynaklı veri puanı yükseltir.
- Tahmini veri, çok yapraklı sistemler, eksik fizik parametreleri ve model sınırı olan durumlar puanı düşürür.

---

## 9. Kritik Dikkat Notları

- Çift cidarlı Sharp sabiti SI birim sistemi için `-19` olmalıdır; eski `-29` yaklaşık 10 dB hata üretir.
- Çift cidar hesabı her yaprak grubunun kendi TL davranışıyla beslenmelidir; yalnız toplam yüzey kütlesi yeterli değildir.
- Dolgu etkisi sabit `+6 dB` olarak uygulanmamalı; flow resistivity, porosity ve tortuosity verileri dikkate alınmalıdır.
- Varsayılan sistem doğrudan köprülenmiş kabul edilmemelidir; `ConnectionType=None`, `StudSpacing=0` güvenli başlangıçtır.
- Eksik ISO bantlarında `0 dB` varsayımıyla Rw üretmek yasaktır.
- Çok yapraklı sistemlerde kullanıcıya triple-leaf uyarısı gösterilmelidir.
- TMM katı tabaka modeli elastik verisi olan masif katmanlarda kullanılır; veri yoksa plate fallback çalışır.
- TMM'de JCA parametreleri kullanıcı tarafından verilmezse türetilir; sıra dışı poröz malzemelerde override önerilir.
- **Empirical tek levha coincidence üstü** kütle-kanunu(f)'ye çapalıdır (`massLaw(f) + sınırlı plateau`); eski `plateau(fc) + 10·oktav` formülü kütle kanununu sınırsız aşıyordu (200mm beton 5000 Hz'de 100 dB). Toparlanma `CoincidenceRecoveryOctaves=0.75` oktav içinde smoothstep ile yapılır; süreksizlik bırakmaz.
- **Coincidence çukur derinliği** `MaxCoincidenceDipDb=-15 dB` ile sınırlıdır; çok düşük sönümlü malzemelerde (cam, çelik) sonsuz-panel formülünün -20..-35 dB değeri gerçekçi 1/3 oktav ölçümüne (10-15 dB) çekilir.
- **Empirical çift cidar** sınırlayıcı frekans `f_l = c/(2π·d)` üstünde kavite terimini dondurur; `20·log(f·d)` terimi `f_l`'de doygunlaşır. Eski model `f_l`'siz olduğundan TL sınırsız büyüyordu (100/50/100 beton 3150 Hz'de 186 dB, Rw=95). Sharp sabiti **-19** korunur (bkz. çift cidar maddesi).
- **Global fiziksel tavan** `MaxRealisticTl=85 dB`: empirical (tek/çift/triple) tüm bantlar bu değerle sınırlanır — sonlu eleman boyutu, sızıntı yolları ve laboratuvar ölçüm tabanı gerçeğini yansıtan güvenlik ağı.
- Bu sınırlandırmalar yalnız Empirical/Hybrid'i etkiler; TMM fiziksel olarak zaten sınırlıdır (dokunulmadı).
- **Köprü (stud) platosu** `CalculateBridgeTL` bağlantı tipiyle ofsetlenir: `Direct=+6`, `ResilientChannel=+16`, `Clips=+24` (baseBridge = `20·log(f·m_tot)−50` üstüne). Eski `Direct=−5` platoyu çok düşük tutup MAM-sınırlı çelik-stud duvarları Rw 28-35'e çökertiyordu; çelik stud gerçekte yalnız yüksek-frekans platosunu sınırlar. Sıralama her zaman `None(ideal) ≥ Clips ≥ RC ≥ Direct`.
- **MAM rezonans çukuru dar Gauss çentiği**: çift cidar TL'de rezonans f₀'da merkezlenmiş, log-frekansta Gauss (σ≈0.30 oktav) bir çentiktir; tepe derinliği dolgulu boşlukta 7 dB, boş hava boşluğunda 12 dB. Poröz dolgu çentiği söndürür. ESKİ HATA: `f0·0.7..f0·1.4` arası DÜZ ceza, rezonans bandının kenarlarını da (f0=120 iken 100/160 Hz) cezalandırıp ayrıştırılmış ideali gerçek-dışı düşük tutuyordu.
- **Bağlantı tipi ayrışması**: Dar çentik sayesinde ayrıştırılmış ideal (None) artık gerçekçi (ince yünlü duvar ~49, ağır ~68); `None ≥ Clips ≥ RC > Direct` net ayrışır (örn. ince yünlü: 49/48/46/39, ağır: 68/62/56/46). **f₀ bağlantı tipiyle kaydırılmaz** — hava yayı + mekanik yay paraleldir, resilient bağlantı hava-yayı f₀'ını düşüremez; ayrışma çentik + köprü platosundan gelir, `MassAirMassResonance` raporu değişmez. (Önceki "resilient f₀'ı düşürür" notu fizik-yanlıştı, düzeltildi.)
- **Triple-leaf (3+ yaprak) tutarlılığı**: Yaklaşımda f0 hem de kavite `d`/f_l artık **toplam boşluğu** kullanır (sentetik temsilci boşluk, dolgulu boşluk öncelikli). ESKİ TUTARSIZLIK: f0 totalGap ile, `CalculateDoubleLayerTL` çağrısı gaps[0] ile hesaplanıyordu. Sonuç global tavanla sınırlı, uyarı verilir.
- **TMM JCA poröz Zc**: Karakteristik empedans `Z_c = √(ρ_eff·K_eff)` makroskopik formdadır (ρ_eff, K_eff zaten /φ içerir). ESKİ HATA: ayrıca `/φ` → 1/φ² (Allard konvansiyonuna göre fazladan bir 1/φ). φ≈1'de ihmal (~0.1 dB), düşük porozitede önemli; eski kod φ düştükçe TL'yi yanlış yönde **artırıyordu**.
- **OITC formülü doğru** (kod hatası değil): `10·log10(incident/transmitted)` formu, incident terimi mutlak ofseti normalize ettiğinden ASTM E1332'nin `100.13 − 10log(Σ…)` formuna **matematiksel eşdeğerdir**. Yalnız göreli referans spektrumu yaklaşıktır; lisanslı ASTM tablosuyla doğrulanmalı (kalan açık).

---

## 10. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/SoundInsulation/`
- `src/Kst.Acoustics.Core/SoundInsulation/SoundInsulationCalculator.cs`
- `src/Kst.Acoustics.Core/SoundInsulation/TransferMatrixCalculator.cs`
- `src/Kst.Acoustics.Core/SoundInsulation/RwRatingCalculator.cs`
- `src/Kst.Acoustics.Core/SoundInsulation/SoundInsulationLibrary.cs`
- `src/Kst.Acoustics.Core/SoundInsulation/SoundInsulationReportService.cs`

App:
- `src/Kst.Acoustics.App/Views/SoundInsulationView.axaml`
- `src/Kst.Acoustics.App/ViewModels/SoundInsulationViewModel.cs`
- `src/Kst.Acoustics.App/Controls/SoundInsulationPlotControl.cs`
- `src/Kst.Acoustics.App/Controls/SoundInsulationCrossSectionControl.cs`
- `src/Kst.Acoustics.App/Converters/EnumEqualsConverter.cs`

Test:
- `src/Kst.Acoustics.Tests/`

---

## 11. Korunması Gereken Regresyon Alanları

- Tek katman mass law sanity check
- Kritik frekans / coincidence dip davranışı
- Çift cidar MAM rezonansı ve üst frekans performansı
- Gözenekli dolgunun sınırlı ve veriye bağlı katkısı
- Bağlantı/köprüleme ayarlarının çift duvar sonucunu etkilemesi
- Eksik ISO bantlarında Rw hesaplanmaması
- Zayıf kompozit elemanın toplam yalıtımı düşürmesi
- TMM tek camın mass law aralığında kalması
- TMM çift duvarın tek eşdeğer kütleden iyi performans vermesi
- Hybrid yöntemin düşük/yüksek frekansta doğru yönteme yaklaşması
- Solid-layer TMM'nin düşük frekansta mass law'a indirgenmesi
- Camda coincidence dip görülmesi
- Kalın beton TMM sonucunun empirical aşırı iyimserliğinden düşük ve makul kalması
- **Empirical tek levha bant TL'si kütle kanununu +8 dB'den fazla aşmamalı** (coincidence üstü kaçağı yok)
- **Empirical çift cidar hiçbir bandı 85 dB fiziksel tavanı aşmamalı** (eski 186 dB regresyonu)
- **Empirical TL eğrisinde komşu 1/3 oktav bantlar arası sıçrama ≤ 15 dB** (eski 26 dB sahte sıçrama)
- **Kalın beton (200mm) Empirical Rw [52,64] ve STC [52,66] sınırında** (eski Rw=65/STC=68 kaçağı)
- **Köprü bağlantı sıralaması** `None ≥ Clips ≥ RC > Direct` ve Direct (2x12.5/70mm) Rw ∈ [42,54] (eski Direct çöküşü)
- **Poröz dolgu MAM çukurunu söndürür**: yünlü çift duvar boş hava boşluklu eşdeğerinden yüksek Rw ve yüksek 125 Hz TL vermeli
- **Triple-leaf (3+ yaprak) sınırlı ve uyarılı**: tüm bantlar ≤ 85 dB ve sonlu, uyarı verilir, Rw makul sınırda; f0/kavite toplam boşluğa göre tutarlı
- **TMM JCA poröz tüm porozite aralığında sonlu/sınırlı**: φ∈[0.3, 0.99] için TL ∈ [0, 60] (NaN/patlama yok)

---

## 12. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M03 regresyonları dahil mevcut test paketi geçmeli

---

## 13. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Hesap değişikliği için önce `Core/SoundInsulation` dosyalarını incele.
4. UI değişikliği için `SoundInsulationView` ve `SoundInsulationViewModel` dosyalarını kontrol et.
5. TMM değişikliği yapılıyorsa `TransferMatrixCalculator` ve ilgili TMM testlerini özellikle incele.
6. Formülasyon riski varsa regresyon testi ekle veya mevcut M03 testlerini çalıştır.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.

## 5 Kasım 2026 — sıralı modül denetimi

Çok satırlı CSV malzeme adları korunuyor; duvar dosyaları atomik ve yedekli yazılıyor (A03–A04).

Ayrıntılı hata listesi ve doğrulama: depo kökündeki `modul-denetimi-2026-11-05.md`.
