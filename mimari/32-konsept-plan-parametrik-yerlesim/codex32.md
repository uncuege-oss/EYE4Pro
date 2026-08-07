# CODEX 32 — Konsept Plan ve Parametrik Yerleşim

> **Durum:** MVP tamamlandı (2026-06-13)
> **Bağlı ana takip:** [../codex30.md](../codex30.md)
> **Önceki modüller:** M31 ihtiyaç programı ve M34 yönetmelik ön kontrol MVP tamamlandı.
> **Üst hedef:** M31'de üretilen mahal gereksinimlerini katlara, bloklara ve basit bağlantı
> ilişkilerine dönüştürerek mimari BIM akışında ilk 2D konsept plan taslağını üretmek.
> **M50 durumu:** M50 entegrasyonu bu modülde yapılmaz. M32 kendi `.e4mimari.json`
> plan taslağı verisi üzerinde çalışır; M50 adapteri Faz 9'a bırakılmıştır.

---

## 0. Kullanım Protokolü

M32 üzerinde çalışırken sıra:

1. `maincodex.md` oku.
2. `mimari/codex30.md` oku.
3. Bu dosyayı oku.
4. Aktif adımı uygula.
5. Her tamamlanan işlemden sonra hem bu dosyayı hem gerekiyorsa `mimari/codex30.md` dosyasını güncelle.
6. Kullanıcı onayı olmadan bir sonraki adıma geçme.

---

## 1. Amaç

M32, kesin mimari uygulama projesi üretmez. Bu modülün amacı, ihtiyaç programındaki mahalleri
erken tasarım için anlaşılır, düzenlenebilir ve daha sonra 3D/M50 adapterine taşınabilir bir
konsept plana dönüştürmektir.

İlk MVP hedefleri:

- M31 mahal gereksinimlerinden `MahalBlogu` üretmek.
- Mahalleri katlara dağıtmak ve her kat için basit bir `KatTaslagi` oluşturmak.
- Yakınlık, uzak durma, cephe alma ve aynı katta olma gibi ilişki kurallarını tanımlamak.
- İlk yerleşim algoritmasıyla mahalleri dikdörtgen bloklar halinde yerleştirmek.
- Ana koridor ve tali koridor bağlantılarını taslak seviyesinde göstermek.
- M34'ün kullanabileceği plan taslağı verisini üretmek; geometri ölçümü gerektiren kontroller
  burada ilk kez veri bulmaya başlayacak.

---

## 2. Kapsam ve Sınırlar

### 2.1 İlk kapsam

- Bloklar dikdörtgen veya sadeleştirilmiş çokgen olarak tutulur.
- Alan hedefi M31 `HedefAlanM2` değerinden gelir.
- İlk algoritma okunabilir ve deterministik olacak; kusursuz mimari kalite hedeflenmez.
- Koridor bağlantıları bloklar arası grafik olarak tutulur.
- Kullanıcı blokları seçip taşıyabilecek, ancak M50 düzeyinde detaylı bina geometrisi düzenlemeyecek.
- Plan taslağı `.e4mimari.json` içinde saklanacak.

### 2.2 Ertelenecek işler

- Gerçek duvar kalınlığı, kapı/pencere açıklıkları ve yapı elemanı üretimi.
- Merdiven/asansör/şaft sürekliliğinin tam çözümü; bunlar M33 ile derinleşecek.
- Kaçış mesafesi, yangın kompartımanı ve erişilebilir rota için kesin ölçüm.
- IFC, glTF veya `.e4bina.json` üretimi.
- M50 ortak bina geometrisine doğrudan yazma.

---

## 3. Veri Kararları

### 3.1 Ana modeller

```text
PlanTaslagi
├── Id
├── ProjeId
├── Katlar
├── Iliskiler
├── Parametreler
└── Notlar

KatTaslagi
├── Id
├── KatNo
├── Ad
├── HedefAlanM2
├── Bloklar
├── Koridorlar
└── Sinir

MahalBlogu
├── Id
├── MahalGereksinimiId
├── Ad
├── MahalTipi
├── KullanimSinifi
├── HedefAlanM2
├── HedefEnM
├── HedefBoyM
├── X
├── Y
├── GenislikM
├── DerinlikM
├── KatNo
└── Kilitli
```

### 3.2 İlişki matrisi

```text
MahalIliskisi
├── KaynakMahalId
├── HedefMahalId
├── IliskiTipi
├── Oncelik
└── Aciklama
```

İlk ilişki tipleri:

- `YakinOlmali`
- `UzakDurmali`
- `AyniKattaOlmali`
- `CepheAlmali`
- `KoridoraBaglanmali`
- `CekirdegeYakinOlmali`
- `ServisleBaglantiliOlmali`

### 3.3 Sonuç dili

M32 de M34 gibi kesin hüküm vermeyecek:

- Doğru: "Taslak yerleşim", "Yerleşim önerisi", "Ön kontrol uyarısı".
- Yanlış: "Plan uygundur", "Kesin proje", "Ruhsata hazır plan".

---

## 4. Önerilen Kod Konumu

```text
src/Kst.Acoustics.Core/Architecture/Layout/
├── PlanTaslagi.cs
├── KatTaslagi.cs
├── MahalBlogu.cs
├── KoridorTaslagi.cs
├── MahalIliskisi.cs
├── YerlesimParametreleri.cs
├── PlanTaslagiUretimServisi.cs
├── IliskiMatrisiServisi.cs
├── BasitYerlesimServisi.cs
└── KoridorYerlesimServisi.cs

src/Kst.Acoustics.App/Architecture/KonseptPlan/
├── ViewModels/
├── Views/
└── Controls/
```

Core tarafı UI'dan bağımsız kalacak. UI, Core modellerini düzenlenebilir görünüm modellerine
saracak.

---

## 5. M34 ile Bağlantı

M34 ilk sürümde geometri gerektiren kontrolleri `Kontrol edilemedi` döndürür. M32 sonrasında:

- `PlanTaslagi` var mı?
- Mahaller katlara dağıtılmış mı?
- Ana kullanım, sirkülasyon, teknik hacim ve ıslak hacimler plan üzerinde temsil edilmiş mi?
- Koridor bağlantısı olmayan mahal var mı?
- Cephe isteyen mahal cepheye atanmış mı?

gibi kontroller M34'ün veri bulabileceği ilk köprüyü oluşturur.

---

## 6. M32 Küçük Faz Listesi

### M32.1 — Codex ve klasör açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 32.1.1 | `mimari/32-konsept-plan-parametrik-yerlesim/` klasörü oluştur | Klasör var | ✅ Tamamlandı |
| 32.1.2 | `codex32.md` oluştur | Amaç, kapsam, veri kararları ve küçük fazlar var | ✅ Tamamlandı |
| 32.1.3 | `mimari/_index.md` bağlantısını güncelle | M32 codex linki çalışır | ✅ Tamamlandı |
| 32.1.4 | `codex30.md` 4.1 durumunu güncelle | 4.1 tamamlandı, 4.2 onay bekliyor | ✅ Tamamlandı |

### M32.2 — Plan blok modeli

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 32.2.1 | `Layout/` klasörünü aç | Core içinde layout klasörü var | ✅ Tamamlandı |
| 32.2.2 | `MahalBlogu` modeli | Mahal gereksinimi, alan, kat ve dikdörtgen geometri taşır | ✅ Tamamlandı |
| 32.2.3 | `KatTaslagi` modeli | Kat no, bloklar, koridorlar ve sınır verisi taşır | ✅ Tamamlandı |
| 32.2.4 | `PlanTaslagi` modeli | Katlar, ilişkiler ve parametreler tek modelde tutulur | ✅ Tamamlandı |
| 32.2.5 | Core test: model varsayılanları | Boş plan güvenli varsayılanlarla derlenir | ✅ Tamamlandı |

### M32.3 — İlişki matrisi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 32.3.1 | İlişki enumları | Yakın, uzak, cephe, koridor, çekirdek ilişkileri var | ✅ Tamamlandı |
| 32.3.2 | Varsayılan ilişki üretimi | Mahal tiplerinden temel ilişki listesi çıkar | ✅ Tamamlandı |
| 32.3.3 | Öncelik puanı | Kritik/yüksek/normal/düşük öncelikler var | ✅ Tamamlandı |
| 32.3.4 | Core test: ilişki üretimi | Ofis ve konut örneklerinde beklenen ilişkiler oluşur | ✅ Tamamlandı |

### M32.4 — İlk yerleşim algoritması

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 32.4.1 | M31 programından blok üret | Her mahal gereksinimi blok olur | ✅ Tamamlandı |
| 32.4.2 | Kat dağıtımı | Bloklar kat sayısına ve alanına göre dağıtılır | ✅ Tamamlandı |
| 32.4.3 | Dikdörtgen yerleşim | Bloklar çakışmadan kaba ızgaraya yerleşir | ✅ Tamamlandı |
| 32.4.4 | Alan toleransı | Blok alanı hedef alanla kabul edilebilir farkta kalır | ✅ Tamamlandı |
| 32.4.5 | Core test: ilk yerleşim | Üretilen bloklar çakışmaz ve katlara dağılır | ✅ Tamamlandı |

### M32.5 — Koridor modeli

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 32.5.1 | `KoridorTaslagi` modeli | Başlangıç, bitiş, genişlik ve bağlı bloklar var | ✅ Tamamlandı |
| 32.5.2 | Ana koridor üretimi | Kat içinde ana dolaşım aksı oluşur | ✅ Tamamlandı |
| 32.5.3 | Blok-koridor bağlantısı | Her ana mahal en az bir koridora bağlanır | ✅ Tamamlandı |
| 32.5.4 | Core test: bağlantısız mahal uyarısı | Koridorsuz blok tespit edilir | ✅ Tamamlandı |

### M32.6 — 2D editör iskeleti

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 32.6.1 | M32 pencere/viewmodel iskeleti | Plan taslağı görülebilir | ✅ Tamamlandı |
| 32.6.2 | Kat seçimi | Kullanıcı katlar arasında geçer | ✅ Tamamlandı |
| 32.6.3 | Blok seçme/sürükleme | Bloklar taşınabilir, kilitli bloklar korunur | ✅ Tamamlandı |
| 32.6.4 | Sağ panel özetleri | Seçili blok alan, tip, sınıf ve ilişki bilgisi gösterir | ✅ Tamamlandı |
| 32.6.5 | App build | UI derlenir | ✅ Tamamlandı |

### M32.7 — M34 geri bildirimi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 32.7.1 | Plan taslağını kontrol bağlamına ekle | M34 `PlanTaslagi` verisini görebilir | ✅ Tamamlandı |
| 32.7.2 | Yerleşim uyarıları | Bağlantısız, çakışan veya alanı sapmış bloklar uyarılır | ✅ Tamamlandı |
| 32.7.3 | UI geri bildirim görünümü | Uyarılar seçili blokla ilişkilendirilir | ✅ Tamamlandı |

### M32.8 — Taslak kaydı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 32.8.1 | `MimariProje` içine plan taslağı alanı ekle | `.e4mimari.json` plan taslağını taşır | ✅ Tamamlandı |
| 32.8.2 | Kaydet/aç round-trip | Plan taslağı dosyada korunur | ✅ Tamamlandı |
| 32.8.3 | Core test: plan round-trip | Blok ve koridor verisi korunur | ✅ Tamamlandı |
| 32.8.4 | Regresyon | Core test runner ve App build geçer | ✅ Tamamlandı |

---

## 7. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M32 MVP tamamlandı; Faz 5.1 / M33 için kullanıcı onayı bekleniyor |
| Sonraki Adım | M33 Düşey Çekirdek, Şaft ve Sirkülasyon codex açılışı |
| Kod Durumu | `MimariProje.PlanTaslagi` eklendi; `.e4mimari.json` plan taslağını kaydedip açıyor; M32 penceresine aç/kaydet/farklı kaydet akışı bağlandı |
| Test Durumu | Core test runner 27/27 geçti; Core test build 0 uyarı/0 hata; App build 0 uyarı/0 hata |

---

## 8. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-12 | M32 konsept plan ve parametrik yerleşim modülü açıldı; ilk hedef kesin proje değil, M31 ihtiyaç programından düzenlenebilir 2D taslak üretmek olacak |
| 2026-06-12 | M32, M50 entegrasyonu yapmayacak; plan taslağı `.e4mimari.json` içinde mimari domain verisi olarak tutulacak |
| 2026-06-12 | M32 sonuç dili "taslak yerleşim" ve "yerleşim önerisi" olacak; kesin ruhsat/uygunluk iddiası kullanılmayacak |
| 2026-06-12 | M32.1 tamamlandı: klasör, `codex32.md`, `mimari/_index.md`, `maincodex.md` ve `codex30.md` M32 açılışıyla güncellendi |
| 2026-06-12 | M32.2 tamamlandı: `MahalBlogu`, `KatTaslagi`, `PlanTaslagi`, `PlanSiniri`, `KoridorTaslagi`, `MahalIliskisi` ve `YerlesimParametreleri` eklendi; Core test runner 22/22 geçti |
| 2026-06-12 | M32.3 tamamlandı: `IliskiTipi`, `IliskiOnceligi` ve `IliskiMatrisiServisi` eklendi; ofis ve konut örnek ilişki üretimi test edildi; Core test runner 23/23 geçti |
| 2026-06-12 | M32.4 tamamlandı: M31 ihtiyaç programından plan taslağı üretimi, kat dağıtımı, kaba dikdörtgen yerleşim, çakışma ve alan toleransı kontrolleri eklendi; Core test runner 24/24 geçti |
| 2026-06-12 | M32.5 tamamlandı: `KoridorYerlesimServisi` ile kat başına ana koridor aksı, blok-koridor bağlantı listesi ve bağlantısız mahal tespiti eklendi; Core test runner 25/25 geçti |
| 2026-06-12 | M32.6 tamamlandı: `KonseptPlanWindow`, `KonseptPlanViewModel` ve özel `KonseptPlanCanvas` eklendi; M32 kartı modül merkezinde açılabilir oldu; App build 0 hata verdi |
| 2026-06-13 | M32.7 tamamlandı: M34 plan taslağı ön kontrolleri, Core testleri ve M32 UI yerleşim uyarıları paneli eklendi; Core test runner 26/26 geçti |
| 2026-06-13 | M32.8 tamamlandı: `MimariProje.PlanTaslagi`, M32 aç/kaydet/farklı kaydet akışı ve plan taslağı `.e4mimari.json` round-trip testi eklendi; Core test runner 27/27 geçti, Core/App build 0 uyarı/0 hata |
