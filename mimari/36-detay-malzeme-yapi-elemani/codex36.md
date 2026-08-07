# CODEX 36 — Detay, Malzeme ve Yapı Elemanı

> **Durum:** MVP tamamlandı (2026-06-13)
> **Ana takip:** [../codex30.md](../codex30.md)
> **Önceki modüller:** M31 ihtiyaç programı, M32 konsept plan, M33 düşey çekirdek/şaft,
> M34 yönetmelik ön kontrol ve M35 3D BIM model MVP tamamlandı.
> **Amaç:** M35 ile oluşan 3D BIM elemanlarına teknik malzeme, yapı elemanı tipi ve mahal
> finisaj kararlarını bağlamak.
> **M50 kararı:** M50 entegrasyonu bu modülde de arkaya alınır. İlk MVP `.e4mimari.json`,
> `PlanTaslagi.Model3D` ve mimari domain modeli üzerinde ilerler; M50/IFC koordinasyonu M39/Faz 9-10
> tarafında ele alınır.

---

## 0. Kullanım Protokolü

1. Yeni oturumda önce `maincodex.md`, sonra `mimari/codex30.md`, sonra bu dosya okunur.
2. Aktif adım bu dosyadaki küçük faz tablosundan ve `codex30.md` Faz 7 tablosundan bulunur.
3. Kod yazmadan önce ilgili Core/App klasörleri incelenir.
4. Her işlemden sonra hem bu dosya hem `mimari/codex30.md` güncellenir.
5. Bir adım tamamlandığında durulur ve kullanıcıdan sonraki adıma geçiş onayı istenir.

---

## 1. Hedef

M36, mimari modelin yalnız hacim ve çizim olmaktan çıkıp teknik karar taşıyan bir BIM nesnesine
dönüşmesini sağlar. İlk hedef tam uygulama detayı veya marka/ürün kataloğu değildir; hedef,
mimarın duvar, döşeme, tavan, kapı, pencere ve mahal finisaj kararlarını hızlıca tanımlaması,
standartlara ve disiplin ihtiyaçlarına okunabilir teknik veri bırakmasıdır.

M36 tamamlandığında kullanıcı şunları yapabilmelidir:

- Duvar, döşeme, tavan, kapı ve pencere için yapı elemanı tipi tanımlayabilmek.
- Malzemeleri yangın, akustik, ısı, su, kaymazlık, dayanım ve metraj alanlarıyla saklayabilmek.
- Mahal tipine göre zemin/duvar/tavan varsayılan finisaj önerisi alabilmek.
- M35 3D eleman Id'lerine malzeme ve yapı elemanı tipi atayabilmek.
- Islak hacim, kaçış yolu, konferans salonu, ofis, konut gibi mahal ailelerinde uygunluk uyarıları
  üretebilmek.
- Akustik, elektrik, mekanik ve keşif/metraj modüllerinin okuyabileceği teknik özetler hazırlamak.

---

## 2. Kapsam

### MVP Kapsamında

- `Architecture/Materials` altında malzeme domain modeli.
- Malzeme kategorileri: zemin, duvar, tavan, kaplama, çekirdek, kapı, pencere, yalıtım, boya,
  sıva, seramik, cam, metal, ahşap ve genel.
- Malzeme performans alanları:
  - yangın tepki/dayanım sınıfı,
  - akustik yutuculuk veya ses azaltım notu,
  - ısı iletkenlik / U değeri notu,
  - suya dayanım ve ıslak hacim uygunluğu,
  - kaymazlık sınıfı,
  - yoğunluk, birim ağırlık ve metraj birimi.
- Katmanlı yapı elemanı tipleri: duvar, döşeme, tavan, kapı ve pencere.
- M35 elemanlarına kararlı Id üzerinden tip/malzeme ataması.
- Mahal tipine göre varsayılan finisaj profili.
- `.e4mimari.json` içinde malzeme, yapı elemanı tipi ve atama verilerinin korunması.
- İlk M34 entegrasyonu: eksik malzeme, ıslak hacimde uygunsuz zemin, kaçış yolunda eksik yangın
  bilgisi gibi ön kontrol uyarıları.
- UI iskeleti: malzeme listesi, tip listesi, mahal/eleman seçimi ve atama paneli.
- Disiplin veri özeti: akustik, elektrik, mekanik ve metrajın okuyacağı basit DTO/rapor alanları.

### MVP Dışında

- Marka/ürün bazlı ticari malzeme kataloğu.
- Tam uygulama detayı çizimi ve 2D kesit üretimi.
- EN/TS standartlarına göre otomatik sertifika doğrulama.
- IFC property set üretimi.
- M50 ortak modeline doğrudan yazma.
- Gerçek kapı/pencere doğrama parametrik geometri üretimi.
- LCA/karbon hesabı ve maliyet veritabanı.

---

## 3. Veri Modeli Taslağı

```text
MimariMalzeme
├── MalzemePerformansi
├── MetrajBilgisi
└── Kaynak/Notlar

YapiElemaniTipi
├── TipSinifi
├── Katmanlar
├── PerformansOzeti
└── MetrajKurali

MahalFinisajProfili
├── MahalTipi / KullanimSinifi
├── ZeminTipiId
├── DuvarTipiId
├── TavanTipiId
└── UyariNotlari

MalzemeAtamasi
├── KaynakElemanId
├── KaynakElemanTuru
├── YapiElemaniTipiId
└── MalzemeIdleri
```

### MimariMalzeme

Önerilen alanlar:

- `Id`, `Kod`, `Ad`, `Kategori`
- `UygunMahalTipleri`, `UygunKullanimSiniflari`
- `YanginTepkiSinifi`, `YanginDayanimDakika`
- `AkustikNot`, `SesAzaltimRw`, `YutuculukAw`
- `IsiIletkenlikWmK`, `UDegeriWm2K`
- `SuyaDayanikli`, `IslakHacimUygun`
- `KaymazlikSinifi`
- `Birim`, `BirimAgirlikKg`, `BirimMaliyetNotu`
- `KaynakKodu`, `Notlar`

### YapiElemaniTipi

Önerilen alanlar:

- `Id`, `Kod`, `Ad`, `Sinif`
- `Katmanlar`
- `ToplamKalinlikM`, `TahminiAgirlikKgM2`
- `YanginDayanimDakika`, `AkustikRw`, `UDegeriWm2K`
- `VarsayilanMetrajBirimi`
- `Notlar`

### YapiElemaniKatmani

Önerilen alanlar:

- `Id`, `SiraNo`, `Ad`
- `MalzemeId`
- `KalinlikM`
- `Fonksiyon`: taşıyıcı, kaplama, yalıtım, hava boşluğu, boya, sıva, bitiş.

### MalzemeAtamasi

Önerilen alanlar:

- `Id`
- `KaynakElemanTuru`: mahal, duvar, döşeme, tavan, kapı, pencere, çekirdek, şaft.
- `KaynakElemanId`
- `KatNo`
- `YapiElemaniTipiId`
- `MalzemeIdleri`
- `AtamaTarihiUtc`, `Notlar`

---

## 4. Kod Konumu

Planlanan Core yerleşimi:

```text
src/Kst.Acoustics.Core/Architecture/Materials/
├── MimariMalzeme.cs
├── MalzemePerformansi.cs
├── YapiElemaniTipi.cs
├── YapiElemaniKatmani.cs
├── MahalFinisajProfili.cs
├── MalzemeAtamasi.cs
├── VarsayilanMimariMalzemeler.cs
├── VarsayilanYapiElemaniTipleri.cs
├── MahalFinisajOneriServisi.cs
└── MalzemeAtamaServisi.cs
```

Planlanan UI yerleşimi:

```text
src/Kst.Acoustics.App/Architecture/Materials/
├── ViewModels/MalzemeAtamaViewModel.cs
├── Views/MalzemeAtamaWindow.axaml
└── Controls/
```

Testler ilk aşamada mevcut Core test runner içine eklenecek:

```text
src/Kst.Acoustics.Core.Tests/Program.cs
```

---

## 5. M31-M35 ve M37 Bağlantısı

M31 mahal tipini, kullanım sınıfını, alan ve hacim hedeflerini verir. M32/M33 bu kararları plan ve
çekirdek/şaft yerleşimine taşır. M35 bu veriyi 3D hacim ve temel yapı elemanlarına dönüştürür.
M36, bu 3D elemanlara teknik karar bağlar.

M37 rapor ve çizelge fazında M36 verileri şu çıktılara dönüşecektir:

- Mahal finisaj çizelgesi.
- Duvar/döşeme/tavan tip listesi.
- Kapı-pencere çizelgesi v1.
- Malzeme metraj özeti.
- Akustik/yangın/ıslak hacim ön kontrol raporu.

M36 boyunca M35 eleman Id'leri korunacak; yeni atama verisi elemanın geometrisini değiştirmeyecek,
yalnız teknik özellik ve raporlama bilgisini bağlayacaktır.

---

## 6. Yönetmelik ve Standart Yaklaşımı

M36 kesin uygunluk beyanı üretmez. Malzeme ve yapı elemanı kararları M34 yaklaşımıyla "ön kontrol"
olarak ele alınır. Kullanıcıya şu tür uyarılar verilir:

- Islak hacimde suya dayanım veya kaymazlık bilgisi olmayan zemin.
- Kaçış yolu/merdiven gibi kritik alanlarda yangın sınıfı eksik kaplama.
- Konferans, eğitim veya ofis gibi hacimlerde akustik performans verisi eksik tavan/duvar.
- Dış kabuk sayılabilecek elemanlarda ısı performans bilgisi eksik yapı elemanı.
- Metraj birimi veya kalınlık bilgisi eksik katmanlı tip.

Kaynak ve standart adları M34 kaynak profili sistemiyle bağlanacak; güncel mevzuat doğrulaması
gerekirse ilgili adımda ayrıca araştırma yapılacaktır.

---

## 7. Küçük Faz Listesi

### M36.1 — Codex Açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 36.1.1 | Klasör oluştur | `mimari/36-detay-malzeme-yapi-elemani/` var | ✅ Tamamlandı |
| 36.1.2 | `codex36.md` oluştur | Kapsam, model taslağı ve küçük faz listesi yazıldı | ✅ Tamamlandı |
| 36.1.3 | Ana takipleri güncelle | `maincodex.md`, `mimari/_index.md`, `codex30.md` M36 durumunu gösterir | ✅ Tamamlandı |

### M36.2 — Malzeme Modeli

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 36.2.1 | Core klasörü | `Architecture/Materials` klasörü açılır | ✅ Tamamlandı |
| 36.2.2 | Enumlar | Malzeme kategorisi, eleman sınıfı, atama hedefi ve teknik sınıf enumları eklenir | ✅ Tamamlandı |
| 36.2.3 | Malzeme modeli | `MimariMalzeme`, performans ve metraj alanları eklenir | ✅ Tamamlandı |
| 36.2.4 | Varsayılan katalog | İlk mimari malzeme kütüphanesi eklenir | ✅ Tamamlandı |
| 36.2.5 | Serileştirme testi | `.e4mimari.json` malzeme listesini korur | ✅ Tamamlandı |

### M36.3 — Yapı Elemanı Tipleri

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 36.3.1 | Tip modeli | Duvar, döşeme, tavan, kapı ve pencere tipleri temsil edilir | ✅ Tamamlandı |
| 36.3.2 | Katman modeli | Katman sırası, kalınlık ve malzeme bağlantısı hesaplanır | ✅ Tamamlandı |
| 36.3.3 | Performans özeti | Tip toplam kalınlık, ağırlık, yangın/akustik/ısı özetini üretir | ✅ Tamamlandı |
| 36.3.4 | M35 bağlantısı | `Duvar3D`, `Doseme3D`, `Tavan3D`, `Aciklik3D` Id'lerine tip atanabilir | ✅ Tamamlandı |
| 36.3.5 | Test | Tip hesapları ve M35 bağlantı sözleşmesi doğrulanır | ✅ Tamamlandı |

### M36.4 — Mahal Varsayılanları

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 36.4.1 | Finisaj profili | Mahal tipine göre zemin/duvar/tavan önerisi modeli eklenir | ✅ Tamamlandı |
| 36.4.2 | Varsayılanlar | Konut, ofis, eğitim, konferans, ıslak hacim, teknik hacim önerileri var | ✅ Tamamlandı |
| 36.4.3 | Öneri servisi | `MahalTipi` ve `KullanimSinifi` girdisinden öneri üretir | ✅ Tamamlandı |
| 36.4.4 | Ön kontrol | Eksik/uygunsuz finisaj kararları M34 uyarılarına taşınır | ✅ Tamamlandı |
| 36.4.5 | Test | Öneri ve uyarı üretimi Core test runner'da geçer | ✅ Tamamlandı |

### M36.5 — Atama UI

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 36.5.1 | Pencere iskeleti | Mimari merkezden malzeme/eleman atama ekranı açılır | ✅ Tamamlandı |
| 36.5.2 | Malzeme listesi | Kategori filtreli malzeme kataloğu görünür | ✅ Tamamlandı |
| 36.5.3 | Eleman seçimi | Mahal/duvar/döşeme/tavan hedefleri seçilebilir | ✅ Tamamlandı |
| 36.5.4 | Atama paneli | Seçili hedefe yapı elemanı tipi ve malzeme atanır | ✅ Tamamlandı |
| 36.5.5 | App build | UI derlemesi hatasız geçer | ✅ Tamamlandı |

### M36.6 — Disiplin Veri Özeti

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 36.6.1 | Akustik veri | Rw/yutuculuk/not alanları okunabilir özet üretir | ✅ Tamamlandı |
| 36.6.2 | Mekanik/enerji veri | Isı ve dış kabuk notları okunabilir özet üretir | ✅ Tamamlandı |
| 36.6.3 | Elektrik veri | Mahal yüzey/kaplama notları elektrik proje kullanımına hazırdır | ✅ Tamamlandı |
| 36.6.4 | Metraj veri | Birim, alan, hacim ve katman metrajı özetlenir | ✅ Tamamlandı |
| 36.6.5 | Test | Disiplin özetleri boş/eksik veride güvenli çalışır | ✅ Tamamlandı |

### M36.7 — Regresyon ve Kapanış

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 36.7.1 | Core testleri | Malzeme, tip, varsayılan ve atama testleri Core runner'da var | ✅ Tamamlandı |
| 36.7.2 | Kayıt testi | `.e4mimari.json` malzeme ve atama verisini kaybetmez | ✅ Tamamlandı |
| 36.7.3 | Build | Core test runner, Core build ve App build başarılıdır | ✅ Tamamlandı |

---

## 8. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M36 MVP tamamlandı — Faz 8 (M37 Rapor/Pafta/Çizelge) için kullanıcı onayı bekleniyor |
| Sonraki Adım | M37: mahal listesi, alan/finisaj çizelgeleri ve disiplin verisi raporları |
| Kod Durumu | M36 MVP kapandı: malzeme modeli, yapı elemanı tipi/atama, finisaj profili/öneri/ön kontrol, atama UI'si ve disiplin veri özeti (akustik/enerji/elektrik/metraj) hazır; kayıt-kurtarma tüm M36 verisini ve disiplin özetini koruyor |
| Test Durumu | Core test runner 51/51 geçti; Core test build 0 uyarı/0 hata; App build 0 hata (mevcut elektrik XAML uyarıları) |

---

## 9. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-13 | M36 başlatıldı; ilk MVP'nin M50'ye bağlanmadan `.e4mimari.json`, `PlanTaslagi.Model3D` ve mimari domain modeli üzerinde ilerlemesine karar verildi |
| 2026-06-13 | M36 kapsamı malzeme modeli, katmanlı yapı elemanı tipleri, mahal finisaj varsayılanları, atama UI ve disiplin veri özetleri olarak belirlendi |
| 2026-06-13 | Faz 7.1 tamamlandı: M36 klasörü ve ana takip bağlantıları açıldı; sıradaki adım 36.2 malzeme domain modeli |
| 2026-06-13 | Faz 7.2 tamamlandı: `Architecture/Materials` altında malzeme enumları, performans/metraj modelleri, `MimariMalzeme` ve `VarsayilanMimariMalzemeler` eklendi; malzeme listesi `.e4mimari.json` içinde korunuyor; Core test runner 43/43 geçti |
| 2026-06-13 | Faz 7.3 tamamlandı: `YapiElemaniTipi`, `YapiElemaniKatmani`, `YapiElemaniPerformansOzeti`, `MalzemeAtamasi` ve `VarsayilanYapiElemaniTipleri` eklendi; M35 3D eleman Id'lerine tip atama sözleşmesi `.e4mimari.json` içinde korunuyor; Core test runner 46/46 geçti |
| 2026-06-13 | Faz 7.4 tamamlandı: `MahalFinisajProfili`, `MahalFinisajUyarisi`, `VarsayilanMahalFinisajProfilleri` ve `MahalFinisajOneriServisi` eklendi; ıslak hacim döşeme ve boyalı tavan varsayılan tipleri kataloğa girdi; mahal tipi/kullanım sınıfı girdisinden zemin/duvar/tavan önerisi üretiliyor; ıslak zemin/akustik tavan/kaçış yangın ön kontrol uyarıları M34 `IlkVeriKontrolleriniCalistir` ve `FinisajKontrolleriniCalistir` üzerinden taşınıyor; `MimariProje.FinisajProfilleri` `.e4mimari.json` içinde korunuyor; Core test runner 49/49 geçti, Core build 0 uyarı/0 hata, App build 0 hata |
| 2026-06-13 | Faz 7.5 öncesi genel kontrol: 6 bulgu giderildi — (1) codex 7.4 durum/günlük güncellendi; (2) `MimariProjeServisi.Normalize` artık `FinisajProfilleri` ve profil `UyariNotlari` listelerini de güvenli hale getiriyor; (3) ıslak hacim tanımı `MahalTipiYardimcilari.IslakHacimMi` ortak yardımcısına çekildi (M33 + M36 aynı tanımı paylaşıyor, Restoran/Kafe/Laboratuvar dahil); (4) `MahalSablonu.GereksinimOlustur` kişi kapasitesini yalnız kapasiteye-bağlı mahallere yazıyor (WC/teknik oda 0); (5) hedef net yükseklik = minimum kararı kod içinde belgelendi; (6) `KacisGrafiUret` yan etkisi XML doc'a eklendi. Core test runner 49/49 geçti, Core/App build 0 hata |
| 2026-06-13 | Faz 7.5 tamamlandı: M36 atama UI'si eklendi — `Architecture/Materials/ViewModels/MalzemeAtamaViewModel` ve `Views/MalzemeAtamaWindow`; mimari modül merkezindeki 36 kartı aktifleştirildi. 3D model hedefleri (mahal/duvar/döşeme/tavan/kapı/pencere) kat bazında listeleniyor, hedefe uyumlu yapı elemanı tipi + kategori filtreli malzeme seçilip atanıyor, atama kaldırılabiliyor, duvar/döşeme/tavan tip Id'si 3D modele yazılıyor; örnek proje/aç/kaydet/farklı kaydet akışı çalışıyor. App build 0 hata, yeni koddan uyarı yok |
| 2026-06-13 | Faz 7.6 tamamlandı: `DisiplinVeriOzeti` DTO'ları ve `DisiplinVeriOzetiServisi` eklendi; atama + 3D varsayılan tip Id'lerinden eleman bazlı disiplin satırları (akustik Rw/Aw/not, mekanik-enerji U/dış kabuk, yangın, ıslak/kaymazlık, metraj) üretiliyor. Akustik/enerji/metraj alt özetleri ve mahal bazlı elektrik yüzey/kaplama özeti (finisaj önerisinden) hazırlanıyor; servis null/boş projede güvenli. Core test runner 50/50 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 7.7 (M36.7) tamamlandı — M36 MVP kapandı: uçtan-uca kapanış regresyon testi eklendi (tam M36 projesi → kaydet/aç → malzeme, tip, finisaj profili, atama sayıları ve disiplin özeti birebir korunuyor; kapı yangın, pencere dış kabuk bilgisi doğrulandı). Core test runner 51/51 geçti, Core build 0 uyarı/0 hata, tam çözüm build 0 hata |
