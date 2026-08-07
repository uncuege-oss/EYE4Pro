# CODEX 34 — Yönetmelik Ön Kontrol Motoru

> **Durum:** M34 MVP tamamlandı (2026-06-12)
> **Bağlı ana takip:** [../codex30.md](../codex30.md)
> **Üst hedef:** EYE4PRO mimari BIM akışında tasarım kararlarını, ihtiyaç programını
> ve ileride üretilecek plan/geometri verisini yönetmelik kaynaklı ön kontrollerden geçirmek.
> **M50 durumu:** M50 entegrasyonu bu modülde yapılmaz. M34 ilk etapta `.e4mimari.json`
> ve `Core/Architecture` verisi üzerinde çalışır; geometri gereken kurallar `Kontrol edilemedi`
> sonucuyla döner.

---

## 0. Kullanım Protokolü

M34 üzerinde çalışırken sıra:

1. `maincodex.md` oku.
2. `mimari/codex30.md` oku.
3. Bu dosyayı oku.
4. Aktif adımı uygula.
5. Her tamamlanan işlemden sonra hem bu dosyayı hem gerekiyorsa `mimari/codex30.md` dosyasını güncelle.
6. Kullanıcı onayı olmadan bir sonraki adıma geçme.

---

## 1. Amaç

M34, mimari tasarımın "kesin uygunluk" onayı değildir. Kullanıcıya erken tasarım aşamasında
uyarı veren, eksik veri varsa bunu açıkça söyleyen ve her sonucu kaynak profiline bağlayan
bir ön kontrol motorudur.

İlk MVP hedefleri:

- Bina işlevi, kapasite, kat sayısı, mahal listesi, alan ve yükseklik verilerini kontrol etmek.
- Ön kontrol sonucunu `Bilgi`, `Uyarı`, `Kritik`, `Kontrol edilemedi` olarak sınıflandırmak.
- Her sonuçta kural kodu, kaynak profili, gerekçe ve çözüm önerisi göstermek.
- M31 ihtiyaç programı panelindeki mevcut doğrulama dilini M34 kural motoruna taşımaya hazır hale getirmek.
- Geometri gerektiren kurallarda yanlış güven vermemek; örneğin kaçış mesafesi veya merdiven konumu
  M32/M33 verisi yoksa ölçülmüş gibi davranmamak.

---

## 2. Kapsam ve Sınırlar

### 2.1 İlk kapsam

İlk sürüm veri tabanlı ön kontrollerle sınırlıdır:

- Eksik bina işlevi, kapasite, kat sayısı ve proje tipi.
- Mahal adı, mahal tipi, kullanım sınıfı, adet, alan ve net yükseklik geçerliliği.
- Hedef alanın minimum/ideal alanla ilişkisi.
- Kullanım sınıfı kırılımı ve net/brüt alan oranlarının uyarı üretmesi.
- Zorunlu mahal ailelerinin bina işlevine göre bulunup bulunmaması.
- Islak hacim, teknik hacim, sirkülasyon ve ana kullanım alanı gibi kritik sınıfların varlığı.

### 2.2 Geometri gelmeden ertelenecek kontroller

Aşağıdaki kontroller M32/M33 veya daha sonra M50 entegrasyonu olmadan kesin hesaplanmayacak:

- Kaçış mesafesi, kaçış yönü ve merdivene erişim.
- Merdiven, asansör, şaft ve ıslak hacimlerin aynı aksta sürekliliği.
- Koridor genişliği ve kapı geçiş netliği.
- Yangın kompartımanı, duman tahliye, yangın merdiveni sayısı/konumu.
- Otopark yerleşimi ve rampa geometrisi.
- Erişilebilir rota sürekliliği.

Bu durumlarda sonuç `Kontrol edilemedi` olacak ve hangi verinin eksik olduğu yazılacak.

---

## 3. Kaynak Profili Yaklaşımı

Kural motoru doğrudan sabit metinlere gömülü "tek doğru" değerlerle ilerlemeyecek. Kurallar,
kaynak profiline bağlanacak:

```text
YonetmelikKaynakProfili
├── Kod
├── Ad
├── YayinlayanKurum
├── SurumTarihi
├── ResmiKaynakUrl
├── Aciklama
└── Kurallar
```

Başlangıç kaynak profili adayları:

- Planlı Alanlar İmar Yönetmeliği.
- Binaların Yangından Korunması Hakkında Yönetmelik.
- Otopark Yönetmeliği.
- Türkiye Bina Deprem Yönetmeliği.
- Erişilebilirlik mevzuatı ve ilgili standart/proje kontrol ilkeleri.
- Binalarda Enerji Performansı Yönetmeliği.
- Yerel belediye imar notları ve plan hükümleri.

Faz 3.5'te resmi kaynak bağlantıları, sürüm/tarih bilgisi ve hangi kuralın hangi kaynağa dayandığı
ayrı ayrı işlenecek.

---

## 4. Önerilen Kod Konumu

```text
src/Kst.Acoustics.Core/Architecture/Rules/
├── KontrolSeviyesi.cs
├── MimariKontrolSonucu.cs
├── MimariProjeDogrulamaServisi.cs
├── KuralSeti.cs
├── Kural.cs
├── KuralSonucu.cs
├── KontrolBaglami.cs
├── YonetmelikKaynakProfili.cs
└── YonetmelikOnKontrolServisi.cs

src/Kst.Acoustics.Core/Architecture/Templates/
└── VarsayilanYonetmelikKaynakProfilleri.cs
```

Mevcut `MimariProjeDogrulamaServisi` ilk veri geçerliliği katmanıdır. M34 ile birlikte bu katman
kural motoruna taşınabilir veya uyumluluk için ince bir facade olarak korunabilir.

---

## 5. Veri Kararları

### 5.1 Sonuç dili

Kullanıcıya gösterilecek dil kesin hüküm vermeyecek:

- Doğru: "Ön kontrol uyarısı", "Kontrol edilemedi", "Eksik veri".
- Yanlış: "Yönetmeliğe uygundur", "Kesin onaylandı", "Ruhsata hazırdır".

### 5.2 Kural sonucu

```text
KuralSonucu
├── Seviye
├── KuralKodu
├── KaynakKodu
├── Baslik
├── Mesaj
├── CozumOnerisi
├── EtkilenenMahalId
├── EtkilenenKatId
├── VeriEksigi
└── KontrolZamani
```

### 5.3 Kontrol bağlamı

```text
KontrolBaglami
├── MimariProje
├── IhtiyacProgrami
├── PlanTaslagi
├── CekirdekTaslagi
├── AktifKaynakProfilleri
└── Parametreler
```

`PlanTaslagi` ve `CekirdekTaslagi` M32/M33 gelene kadar boş kalabilir.

---

## 6. M34 Küçük Faz Listesi

### M34.1 — Codex ve klasör açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 34.1.1 | `mimari/34-yonetmelik-on-kontrol/` klasörü oluştur | Klasör var | ✅ Tamamlandı |
| 34.1.2 | `codex34.md` oluştur | Amaç, kapsam, kaynak profili ve küçük fazlar var | ✅ Tamamlandı |
| 34.1.3 | `mimari/_index.md` bağlantısını güncelle | M34 codex linki çalışır | ✅ Tamamlandı |
| 34.1.4 | `codex30.md` 3.1 durumunu güncelle | 3.1 tamamlandı, 3.2 onay bekliyor | ✅ Tamamlandı |

### M34.2 — Kural modeli

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 34.2.1 | `KuralSeti` modelini ekle | Kural seti kod/ad/açıklama/kural listesi taşır | ✅ Tamamlandı |
| 34.2.2 | `Kural` modelini ekle | Kod, başlık, kapsam, kaynak ve çalıştırma tipi var | ✅ Tamamlandı |
| 34.2.3 | `KuralSonucu` modelini ekle | Seviye, mesaj, çözüm, kaynak ve veri eksiği alanları var | ✅ Tamamlandı |
| 34.2.4 | `KontrolBaglami` modelini ekle | Mimari proje ve opsiyonel plan/çekirdek verisini taşır | ✅ Tamamlandı |
| 34.2.5 | Core test: model varsayılanları | Boş model güvenli varsayılanlarla derlenir | ✅ Tamamlandı |

### M34.3 — Kural çalıştırıcı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 34.3.1 | `YonetmelikOnKontrolServisi` ekle | Kural setlerini bağlam üzerinde çalıştırır | ✅ Tamamlandı |
| 34.3.2 | Sonuç sıralama | Kritik, uyarı, kontrol edilemedi, bilgi sırası korunur | ✅ Tamamlandı |
| 34.3.3 | Veri eksikliği yardımcıları | Geometri yoksa kontrol edilemedi sonucu üretir | ✅ Tamamlandı |
| 34.3.4 | Core test: çalıştırıcı boş bağlamı yakalar | Null/boş proje güvenli sonuç döner | ✅ Tamamlandı |

### M34.4 — İlk veri tabanlı kontroller

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 34.4.1 | Bina işlevi/kapsam kontrolleri | İşlev, proje tipi, kapasite ve kat sayısı uyarıları var | ✅ Tamamlandı |
| 34.4.2 | Mahal veri kontrolleri | Ad/tip/sınıf/adet/alan/yükseklik kontrolleri var | ✅ Tamamlandı |
| 34.4.3 | Zorunlu mahal aileleri | Bina işlevine göre temel mahal eksikleri uyarılır | ✅ Tamamlandı |
| 34.4.4 | Alan oranı kontrolleri | Net/brüt/sirkülasyon/teknik alan oranları kontrol edilir | ✅ Tamamlandı |
| 34.4.5 | Core test: ilk kural seti | Beklenen uyarı/kritik sonuçlar üretilir | ✅ Tamamlandı |

### M34.5 — Kaynak profili

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 34.5.1 | `YonetmelikKaynakProfili` modeli | Kaynak adı, kurum, tarih, URL ve açıklama alanları var | ✅ Tamamlandı |
| 34.5.2 | Varsayılan kaynak profilleri | Başlangıç kaynakları listelenir | ✅ Tamamlandı |
| 34.5.3 | Kural-kaynak bağı | Her kural kaynak koduyla raporlanır | ✅ Tamamlandı |
| 34.5.4 | Resmi kaynak doğrulaması | URL/tarih bilgileri resmi kaynaklara göre güncellenir | ✅ Tamamlandı |

### M34.6 — UI ve M31 entegrasyonu

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 34.6.1 | M31 ön kontrol panelini kural motoruna bağla | Mevcut panel M34 sonuçlarını gösterir | ✅ Tamamlandı |
| 34.6.2 | Kaynak/kural kodu görünümü | Sonuç kartlarında kural ve kaynak kodu okunur | ✅ Tamamlandı |
| 34.6.3 | Kontrol edilemedi dili | Eksik veri kullanıcıya açık gösterilir | ✅ Tamamlandı |
| 34.6.4 | App build | UI değişiklikleri derlenir | ✅ Tamamlandı |

### M34.7 — Regresyon ve kapanış

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 34.7.1 | Core test runner | `Kst.Acoustics.Core.Tests` geçer | ✅ Tamamlandı |
| 34.7.2 | App build | `Kst.Acoustics.App` 0 hata verir | ✅ Tamamlandı |
| 34.7.3 | Dil kontrolü | "Kesin uygunluk" dili kullanılmaz | ✅ Tamamlandı |
| 34.7.4 | Ana codex güncellemesi | `codex30.md`, `codex34.md`, `_index.md` tutarlı | ✅ Tamamlandı |

---

## 7. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M34 MVP tamamlandı; Faz 4.1 için kullanıcı onayı bekleniyor |
| Sonraki Adım | M32 konsept plan ve parametrik yerleşim için `codex32.md` açılışı |
| Kod Durumu | M34 veri tabanlı ön kontrol, kaynak profili ve M31 UI sonuç paneli MVP kapsamıyla tamamlandı |
| Test Durumu | Core test runner 21/21 geçti; Core test build 0 uyarı/0 hata; App build 0 uyarı/0 hata |

---

## 8. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-12 | M34 yönetmelik ön kontrol motoru olarak açıldı; ilk hedef kesin uygunluk değil, kaynak profilli ve açıklanabilir ön kontrol dili olacak |
| 2026-06-12 | Geometri gerektiren kontroller M32/M33 verisi gelene kadar `Kontrol edilemedi` sonucu üretecek |
| 2026-06-12 | M34.1 tamamlandı: klasör ve `codex34.md` açıldı; `maincodex.md`, `mimari/_index.md` ve `codex30.md` bağlantıları güncellendi |
| 2026-06-12 | M34.2 tamamlandı: kural seti, kural, kural sonucu ve kontrol bağlamı modelleri eklendi; Core test runner 17/17 geçti |
| 2026-06-12 | M34.3 tamamlandı: `YonetmelikOnKontrolServisi` eklendi; eksik veri sonuçları `Kontrol edilemedi` olarak üretiliyor ve sonuçlar önem sırasına göre dönüyor |
| 2026-06-12 | M34.4 tamamlandı: ilk veri tabanlı ön kontroller eklendi; bina kapsamı, mahal verileri, zorunlu mahal aileleri ve alan oranları kritik/uyarı sonuçları üretiyor; Core test runner 19/19 geçti |
| 2026-06-12 | M34.5 tamamlandı: kaynak profili modeli, varsayılan resmi yönetmelik profilleri ve ilk veri kontrolleri için kural-kaynak bağı eklendi; Core test runner 21/21 geçti |
| 2026-06-12 | M34.6 tamamlandı: M31 ön kontrol paneli M34 sonuçlarını gösterecek şekilde bağlandı; kural kodu, kaynak profili ve eksik veri dili UI kartlarına eklendi |
| 2026-06-12 | M34.7 tamamlandı: dil taraması kesin uygunluk iddiası bulunmadığını doğruladı; Core test runner 21/21, Core test build ve App build 0 uyarı/0 hata geçti; M34 MVP kapandı |
