# CODEX 30 — Mimari BIM Ana Plan ve Faz Takibi

> **Durum:** Başlatıldı (2026-06-12)
> **Bağlı rapor:** [mimari-modul-planlama-raporu.md](mimari-modul-planlama-raporu.md)
> **Amaç:** EYE4PRO içinde mimari BIM akışını sıfırdan tasarım, ihtiyaç programı,
> kurallı plan, 3D model, detay/malzeme ve çıktı üretimi olarak programlamak.
> **Önemli karar:** M50 entegrasyonu şu an arkaya alınmıştır. M50 tasarım aşamasında olduğu için
> ilk fazlar mimari modülün kendi domain modeli ve `.e4mimari.json` proje dosyası üzerinde ilerler.

---

## 0. Kullanım Protokolü

Yeni mimari geliştirme oturumunda sıra:

1. `maincodex.md` oku.
2. Bu dosyayı oku: `mimari/codex30.md`.
3. Aktif faz ve ilk `Bekliyor` adımı bul.
4. İlgili modül codex'i varsa onu oku (`codex31.md`, `codex32.md`, vb.).
5. Kod yazmadan önce ilgili kaynak klasörlerini incele.
6. Her tamamlanan işlemden sonra bu dosyada:
   - ilgili adımın `Durum` alanını güncelle,
   - gerekiyorsa `Sonraki Adım` alanını güncelle,
   - yeni karar varsa `Karar Günlüğü`ne satır ekle.
7. Kullanıcı onayı olmadan bir sonraki adıma veya faza geçme. Her adım tamamlandığında dur,
   sonucu bildir ve geçiş için kullanıcıdan onay bekle.

Bu dosya mimari başlığın ana takip dosyasıdır. Modül codex'leri açıldığında ayrıntılar ilgili
dosyaya taşınır; bu dosyada yalnız ana faz durumu, bağımlılıklar ve yön kararları tutulur.

---

## 1. Genel Hedef

EYE4PRO mimari başlığı, yalnız dışarıdan DXF alan bir yardımcı modül olmayacak. Kullanıcı program
içinde binayı şu sırayla kurabilecek:

```text
Bina işlevi
-> ihtiyaç programı
-> mahal şablonları
-> alan/hacim hedefleri
-> konsept blok plan
-> koridor/çekirdek/şaft yerleşimi
-> yönetmelik ön kontrolü
-> basit 3D BIM model
-> detay/malzeme
-> rapor/pafta/çizelge
-> M50/diğer disiplin entegrasyonu
```

İlk geliştirme hedefi tam BIM programı olmak değil, küçük ve güvenilir adımlarla mimari BIM
çekirdeğini kurmaktır.

---

## 2. Modül Haritası

| # | Modül | Codex | Durum | Not |
|---|-------|-------|-------|-----|
| 30 | Mimari BIM Ana Plan ve Faz Takibi | `mimari/codex30.md` | Başlatıldı | Bu dosya |
| 31 | İhtiyaç Programı ve Mahal Şablonları | `mimari/31-ihtiyac-programi-mahal-sablonlari/codex31.md` | MVP tamamlandı | İlk programlanacak modül |
| 32 | Konsept Plan ve Parametrik Yerleşim | `mimari/32-konsept-plan-parametrik-yerlesim/codex32.md` | MVP tamamlandı | Blok plan + koridor |
| 33 | Düşey Çekirdek, Şaft ve Sirkülasyon | `mimari/33-dusey-cekirdek-saft-sirkulasyon/codex33.md` | MVP tamamlandı | Merdiven/asansör/şaft |
| 34 | Yönetmelik Ön Kontrol Motoru | `mimari/34-yonetmelik-on-kontrol/codex34.md` | MVP tamamlandı | Parametreli uyarı motoru |
| 35 | 3D BIM Model ve Görselleştirme | `mimari/35-3d-bim-model/codex35.md` | MVP tamamlandı | Basit 3D hacimler |
| 36 | Detay, Malzeme ve Yapı Elemanı | `mimari/36-detay-malzeme-yapi-elemani/codex36.md` | MVP tamamlandı | Teknik malzeme kütüphanesi |
| 37 | Mimari Pafta, Rapor ve Çizelgeler | `mimari/37-pafta-rapor-cizelgeler/codex37.md` | MVP tamamlandı | Çıktı merkezi |
| 38 | Cephe, Günışığı ve Kabuk Analizi | `mimari/38-cephe-gunisigi-kabuk/codex38.md` | Planlandı | Orta/uzun vade |
| 39 | BIM Koordinasyon ve İhracat | `mimari/39-bim-koordinasyon-ihracat/codex39.md` | MVP tamamlandı | M50/IFC/glTF/DXF entegrasyonu |

Not: 31-39 modül klasörleri ve kendi codex dosyaları ilgili modüle başlanırken açılır.
Şimdilik ana takip `codex30.md` içindedir.

---

## 3. Mimari Kod Konumu

Önerilen kaynak kod yerleşimi:

```text
src/Kst.Acoustics.Core/Architecture/
├── Models/
├── Services/
├── Rules/
├── Templates/
├── Layout/
├── Materials/
├── Reports/
└── Exporting/

src/Kst.Acoustics.App/Architecture/
├── ViewModels/
├── Views/
├── Controls/
└── Rendering/

src/Kst.Acoustics.Tests/Architecture/
```

UI metinleri Türkçe olacaktır. Core tarafında iş mantığı UI'dan bağımsız kalacaktır.

---

## 4. Dosya Stratejisi

| Dosya | Zaman | Amaç |
|---|---|---|
| `.e4mimari.json` | İlk fazlardan itibaren | Mimari tasarım kararları, ihtiyaç programı, mahal şablonları, kurallar |
| `.e4bina.json` | Sonraki faz | M50 ortak bina modeli; diğer disiplinlere dağıtılacak geometri |

M50 entegrasyonu ileri faza bırakıldığı için ilk programlama `.e4mimari.json` üzerinden yürür.
M50 hazır olduğunda `Architecture -> Bina` adapteri yazılır.

---

## 5. Faz Listesi

### Faz 0 — Dokümantasyon ve Başlangıç Takibi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 0.1 | `mimari/codex30.md` oluştur | Bu dosyada fazlar, modül haritası ve takip protokolü var | ✅ Tamamlandı |
| 0.2 | `mimari/_index.md` güncelle | `codex30.md` ve planlanan modüller index'te görünür | ✅ Tamamlandı |
| 0.3 | `maincodex.md` güncelle | Mimari bölümünde `codex30.md` ve 31-39 modül atıfları var | ✅ Tamamlandı |
| 0.4 | Son kontrol | Dosyalar okunur, bağlantılar ve durumlar doğrulanır | ✅ Tamamlandı |

### Faz 1 — Mimari Domain Çekirdeği

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 1.1 | `Core/Architecture` klasör iskeleti | Models/Services/Rules/Templates klasörleri eklenir | ✅ Tamamlandı |
| 1.2 | Temel enumlar | `BinaIslevi`, `MahalTipi`, `KullanimSinifi`, `KontrolSeviyesi` derlenir | ✅ Tamamlandı |
| 1.3 | Temel modeller | `MimariProje`, `IhtiyacProgrami`, `MahalGereksinimi`, `MahalSablonu` eklenir | ✅ Tamamlandı |
| 1.4 | JSON serileştirme | `.e4mimari.json` round-trip testi geçer | ✅ Tamamlandı |
| 1.5 | Geçerlilik kontrolleri | Boş proje, eksik bina işlevi, negatif alan gibi hatalar yakalanır | ✅ Tamamlandı |
| 1.6 | Birim testleri | Core testleri yeşil; UI yok | ✅ Tamamlandı |

### Faz 2 — M31 İhtiyaç Programı ve Mahal Şablonları

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 2.1 | `codex31.md` aç | M31 kapsamı, veri modeli ve adım listesi yazılır | ✅ Tamamlandı |
| 2.2 | Varsayılan bina işlevleri | Konut, ofis, eğitim, sağlık, otel, kültür, ticari, depo şablonları var | ✅ Tamamlandı |
| 2.3 | Mahal şablon kütüphanesi | Her işlev için önerilen mahal tipleri, min/ideal alan, yükseklik, kapasite var | ✅ Tamamlandı |
| 2.4 | İhtiyaç programı üretici | Kullanıcı kapasitesinden mahal gereksinimleri üretilir | ✅ Tamamlandı |
| 2.5 | Net/brüt alan hesabı | Net alan, sirkülasyon, teknik alan, brüt hedef hesaplanır | ✅ Tamamlandı |
| 2.6 | M31 pencere iskeleti | Bina işlevi, kapasite, mahal listesi, özet paneli görünür | ✅ Tamamlandı |
| 2.7 | Düzenleme akışı | Kullanıcı mahal adet/alan/hacim değerlerini düzenleyebilir | ✅ Tamamlandı |
| 2.8 | Kaydet/aç | `.e4mimari.json` ile M31 verisi korunur | ✅ Tamamlandı |
| 2.9 | İhtiyaç programı raporu | HTML/CSV basit çıktı alınır | ✅ Tamamlandı |
| 2.10 | Regresyon | Build/test yeşil; Türkçe UI metinleri kontrol edilir | ✅ Tamamlandı |

### Faz 3 — Hafif Yönetmelik/Kural Altyapısı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 3.1 | `codex34.md` aç | Yönetmelik ön kontrol hedefi ve uyarı dili yazılır | ✅ Tamamlandı |
| 3.2 | Kural modeli | `KuralSeti`, `Kural`, `KuralSonucu`, `KontrolBaglami` eklenir | ✅ Tamamlandı |
| 3.3 | Kural çalıştırıcı | Basit koşul servisleriyle sonuç listesi üretir | ✅ Tamamlandı |
| 3.4 | İlk kontroller | Eksik mahal sınıfı, min alan, min yükseklik, eksik kapasite uyarıları var | ✅ Tamamlandı |
| 3.5 | Kaynak profili | Yönetmelik kaynakları sürüm/tarih bilgisiyle saklanır | ✅ Tamamlandı |
| 3.6 | UI sonuç paneli | Bilgi/Uyarı/Kritik/Kontrol edilemedi ayrımı görünür | ✅ Tamamlandı |
| 3.7 | Dil kontrolü | "Kesin uygundur" değil, "ön kontrol" dili kullanılır | ✅ Tamamlandı |

### Faz 4 — M32 Konsept Plan ve Parametrik Yerleşim

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 4.1 | `codex32.md` aç | Blok plan yaklaşımı ve sınırlar yazılır | ✅ Tamamlandı |
| 4.2 | Plan blok modeli | `MahalBlogu`, `KatTaslagi`, `PlanTaslagi` modelleri eklenir | ✅ Tamamlandı |
| 4.3 | İlişki matrisi | Yakın olmalı, uzak durmalı, cephe almalı, aynı aksta olmalı kuralları var | ✅ Tamamlandı |
| 4.4 | İlk yerleşim algoritması | Mahalleri alanlarına göre dikdörtgen bloklar halinde dizer | ✅ Tamamlandı |
| 4.5 | Koridor modeli | Ana koridor aksı ve blok bağlantı grafı oluşur | ✅ Tamamlandı |
| 4.6 | 2D editör iskeleti | Kullanıcı blokları görebilir, seçebilir, sürükleyebilir | ✅ Tamamlandı |
| 4.7 | Kural geri bildirimi | Yerleşim sonrası ilişki/alan/koridor uyarıları çalışır | ✅ Tamamlandı |
| 4.8 | Taslak kaydı | Plan taslağı `.e4mimari.json` içinde saklanır | ✅ Tamamlandı |

### Faz 5 — M33 Düşey Çekirdek, Şaft ve Sirkülasyon

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 5.1 | `codex33.md` aç | Çekirdek/şaft/sirkülasyon kapsamı yazılır | ✅ Tamamlandı |
| 5.2 | Çekirdek modelleri | Merdiven, asansör, rampa, şaft, çekirdek grubu modelleri eklenir | ✅ Tamamlandı |
| 5.3 | Katlar arası süreklilik | Aynı aksta devam etmeyen elemanlar uyarılır | ✅ Tamamlandı |
| 5.4 | Islak hacim hizası | WC/banyo/şaft ilişkisi kontrol edilir | ✅ Tamamlandı |
| 5.5 | Kaçış grafı v1 | Mahaller, koridor, merdiven bağlantıları graf olarak tutulur | ✅ Tamamlandı |
| 5.6 | UI gösterimi | Çekirdekler plan üzerinde ayrı renkte/katmanda görünür | ✅ Tamamlandı |

### Faz 6 — M35 Basit 3D BIM Model

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 6.1 | `codex35.md` aç | 3D model kapsamı ve dışa aktarım hedefleri yazılır | ✅ Tamamlandı |
| 6.2 | 3D geometri modelleri | Kat, duvar, döşeme, tavan, açıklık, mahal hacmi temsil edilir | ✅ Tamamlandı |
| 6.3 | 2D taslaktan 3D üretim | Kat yüksekliğiyle prizmatik hacim oluşur | ✅ Tamamlandı |
| 6.4 | Basit 3D önizleme | Model boş görünmez, döndür/zoom yapılabilir | ✅ Tamamlandı |
| 6.5 | glTF/OBJ araştırma | İlk dışa aktarım yolu seçilir | ✅ Tamamlandı |
| 6.6 | Geometri testleri | Alan/hacim değerleri 2D hedeflerle tutarlı | ✅ Tamamlandı |

### Faz 7 — M36 Detay, Malzeme ve Yapı Elemanı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 7.1 | `codex36.md` aç | Malzeme/yapı elemanı kapsamı yazılır | ✅ Tamamlandı |
| 7.2 | Malzeme modeli | Yangın, akustik, ısı, su, kaymazlık ve metraj alanları var | ✅ Tamamlandı |
| 7.3 | Yapı elemanı tipleri | Duvar, döşeme, tavan, kapı, pencere tipleri var | ✅ Tamamlandı |
| 7.4 | Mahal varsayılanları | Mahal tipine göre finisaj ve yapı elemanı önerilir | ✅ Tamamlandı |
| 7.5 | Atama UI | Seçili mahal/eleman için malzeme atanır | ✅ Tamamlandı |
| 7.6 | Diğer disiplin verisi | Akustik/elektrik/mekanik/keşif için okunabilir teknik alanlar hazırlanır | ✅ Tamamlandı |

### Faz 8 — M37 Rapor, Pafta ve Çizelgeler

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 8.1 | `codex37.md` aç | Çıktı hedefleri ve formatlar yazılır | ✅ Tamamlandı |
| 8.2 | Mahal listesi | Kat, mahal no, ad, alan, hacim, işlev tablosu çıkar | ✅ Tamamlandı |
| 8.3 | Alan tablosu | Net/brüt, sirkülasyon, teknik alan özetleri çıkar | ✅ Tamamlandı |
| 8.4 | Kapı-pencere çizelgesi v1 | M50 öncesi taslak açıklık verisinden tablo çıkar | ✅ Tamamlandı |
| 8.5 | Finisaj çizelgesi | Zemin/duvar/tavan tablosu çıkar | ✅ Tamamlandı |
| 8.6 | Ön kontrol raporu | Uyarılar kaynak/kural koduyla raporlanır | ✅ Tamamlandı |
| 8.7 | HTML/CSV/XLSX | En az iki çıktı formatı çalışır | ✅ Tamamlandı (HTML + CSV + XLSX) |

### Faz 9 — M50 ve Diğer Disiplin Entegrasyonu (✅ Tamamlandı — M50 köprüsü kuruldu)

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 9.1 | M50 durum kontrolü | `codex50.md` güncel fazı okunur, entegrasyon noktaları belirlenir | ✅ Tamamlandı |
| 9.2 | Adapter tasarımı | `Architecture -> BinaGeometri/BinaModeli` dönüştürücü tasarlanır | ✅ Tamamlandı |
| 9.3 | `.e4bina.json` üretimi | Mimari taslaktan M50 uyumlu dosya çıkar | ✅ Tamamlandı |
| 9.4 | Id kararlılığı | Mahal/oda/çekirdek/şaft Id'leri M50 revizyon sistemiyle uyumlu | ✅ Tamamlandı |
| 9.5 | M12/M08/M21 okuma testi | Elektrik, akustik, mekanik mimari üretimli geometriyi okuyabilir | ✅ Tamamlandı (M12+M08; M21 henüz köprü yok) |

**9.1 bulguları (2026-06-13):**
- **M50 tamamlandı** (Faz 1-5, M12/M08 köprüleri + 302 test). Erteleme gerekçesi (M50 tasarımda) kalktı; entegrasyon uygulanabilir.
- **M50 veri modeli** (`Core/Bina/Models`): `BinaGeometri`(SemaSurum, GeometriRevizyon, BinaAdi, Katlar[], DuseyBirlesimleri[]) → `BinaKat`(Id, Ad, KatNo, KatYuksekligiM, OriginOffset, Odalar[], Acikliklar[]) → `BinaOda`(Id, Ad, AlanM2, Poligon: `List<Point2D>`, YukseklikOverrideM?) ; `BinaAciklik`(Id, Bas/Bit `Point2D`, Tip Kapı/Pencere, GenislikM, YukseklikM, EsikYuksekligiM). `.e4bina.json` (`BinaGeometriServisi`).
- **Yön:** Mimari **üretici/tohum** (M12/M08'in tersi — onlar tüketici). Kavramsal dikdörtgen blok → M50 poligon geometrisi; kullanıcı M50'de inceltir.
- **Eşleme:** `MimariProje`→`BinaGeometri` (BinaAdi=ProjeAdi); `Kat3DModel`→`BinaKat` (KatNo/Ad/KatYuksekligiM); `MahalHacmi3D`(X/Y/Gen/Der)→`BinaOda` (4-köşe dikdörtgen poligon, AlanM2, Id mimari mahal Id'sinden); `Aciklik3D`→`BinaAciklik` (Tip/Gen/Yük; Bas/Bit duvar doğrusu + MerkezX/Y'den türetilir — en hassas eşleme); patlatma yok→`DuseyBirlesimleri` boş.
- **Kaynak:** `PlanTaslagi.Model3D` (en zengin: mahal+açıklık+kat yüksekliği). Hedef yol `MimariProje.BinaGeometriYolu` (alan zaten var).
- **Riskler:** (a) açıklık Bas/Bit segment türetimi; (b) Id kararlılığı 9.4; (c) M50'de elle düzenlenmiş `.e4bina` üzerine yazma koruması (codex50 M12 deseni: varsa bağla, üzerine yazma); (d) `Point2D` = `Core/Common/Geometry`.
- **Önerilen kod konumu:** `Architecture/Exporting/BinaGeometriAdapteri` (+ üretim servisi). Tek yön (export) yeterli.

### Faz 10 — Uzun Vade BIM İhracat ve Koordinasyon

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 10.1 | `codex39.md` aç | BIM koordinasyon ve ihracat kapsamı yazılır | ✅ Tamamlandı |
| 10.2 | Revizyon farkı | Eski/yeni mimari tasarım fark raporu çıkar | ✅ Tamamlandı |
| 10.3 | IFC araştırması | IFC üretimi için kütüphane/format kararı verilir | ✅ Tamamlandı (OBJ uygulandı; GLB/DXF/IFC ertelendi) |
| 10.4 | Disiplin mutabakatı | Askıda kalan oda/eleman/veri raporu hazırlanır | ✅ Tamamlandı |

---

## 6. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Faz | Faz 10 tamamlandı (M39 MVP) — **mimari başlıktaki tüm planlı fazlar (0-10) bitti** |
| Aktif Adım | UI bağlama sürüyor: "Mimari Rapor ve İhracat" penceresi eklendi (HTML/XLSX/OBJ/.e4bina). Sıradaki UI/yön onayı bekleniyor |
| Sonraki Adım | Kullanıcı kararı: revizyon farkı/mutabakat UI · 3D önizlemeden OBJ · GLB/DXF/IFC · M38 · M21 köprüsü |
| M50 Entegrasyonu | ✅ Tamamlandı: mimari→`.e4bina` üretim+bağlama+Id kararlılığı; M12/M08 köprüleri uçtan uca okuyor |
| İlk Kod Fazı | ✅ Faz 1 tamamlandı |

---

## 7. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-12 | Mimari başlık BIM hedefiyle ele alınacak; DXF tüketimi korunacak ama sıfırdan tasarım akışı eklenecek |
| 2026-06-12 | M50 entegrasyonu M50 tasarım aşamasında olduğu için Faz 9'a ertelendi |
| 2026-06-12 | İlk programlama `.e4mimari.json` ve `Core/Architecture` domain modeli üzerinde başlayacak |
| 2026-06-12 | Her işlemden sonra `mimari/codex30.md` durum tablosu güncellenecek |
| 2026-06-12 | Faz 0 dokümantasyon bağlantıları tamamlandı; sıradaki iş Faz 1.1 `Core/Architecture` iskeleti |
| 2026-06-12 | Kullanıcı isteğiyle her adım/faz geçişinde durup onay alma kuralı eklendi |
| 2026-06-12 | Faz 1.1 tamamlandı: `src/Kst.Acoustics.Core/Architecture/Models`, `Services`, `Rules`, `Templates` klasörleri oluşturuldu |
| 2026-06-12 | Faz 1.2 tamamlandı: `BinaIslevi`, `MahalTipi`, `KullanimSinifi`, `KontrolSeviyesi` enumları eklendi; Core derlemesi başarılı |
| 2026-06-12 | Faz 1.3 tamamlandı: `MimariProje`, `IhtiyacProgrami`, `MahalGereksinimi`, `MahalSablonu` modelleri eklendi; Core derlemesi başarılı |
| 2026-06-12 | Faz 1.4 tamamlandı: `MimariProjeServisi` ve `.e4mimari.json` round-trip testi eklendi; Core derlemesi ve izole round-trip doğrulaması başarılı |
| 2026-06-12 | Faz 1.5 tamamlandı: `MimariProjeDogrulamaServisi` boş proje, eksik bina işlevi, boş mahal listesi, negatif alan/adet/yükseklik kontrolleriyle eklendi; izole doğrulama başarılı |
| 2026-06-12 | Faz 1.6 tamamlandı: `Kst.Acoustics.Core.Tests` App referansı olmadan eklendi; mimari Core test runner 5/5 geçti; Faz 1 kapandı |
| 2026-06-12 | Faz 2.1 tamamlandı: M31 klasörü ve `codex31.md` açıldı; M31 küçük faz listesi yazıldı |
| 2026-06-12 | Faz 2.2 tamamlandı: `BinaIsleviTanim` ve `BinaIsleviTanimlari` eklendi; Core test runner 8/8 geçti |
| 2026-06-12 | Faz 2.3 tamamlandı: `VarsayilanMahalSablonlari` eklendi; Core test runner 11/11 geçti |
| 2026-06-12 | Faz 2.4 tamamlandı: `IhtiyacProgramiUretimServisi` eklendi; ofis ve konferans üretim testleriyle Core test runner 13/13 geçti; Core test build 0 uyarı, 0 hata |
| 2026-06-12 | Faz 2.5 tamamlandı: `AlanOzeti` ve `AlanOzetiServisi` eklendi; net/brüt alan, hedef farkı ve kullanım sınıfı kırılımı testleriyle Core test runner 15/15 geçti; Core test build 0 uyarı, 0 hata |
| 2026-06-12 | Faz 2.6 tamamlandı: M31 ViewModel/pencere iskeleti ve mimari modül merkezi eklendi; ana ekrandaki mimari kartı aktif edildi; App build 0 hata, mevcut elektrik XAML dosyalarından 4 uyarı verdi |
| 2026-06-12 | Faz 2.7 tamamlandı: M31 mahal satırlarında ad/adet/alan/yükseklik düzenleme, yeni/kopyala/sil mahal ve canlı alan özeti yenileme eklendi; App build 0 hata, mevcut elektrik XAML dosyalarından 4 uyarı verdi |
| 2026-06-12 | Faz 2.8 tamamlandı: `.e4mimari.json` aç/kaydet/farklı kaydet UI akışı ve ön kontrol paneli eklendi; App build 0 hata, mevcut elektrik XAML dosyalarından 4 uyarı verdi |
| 2026-06-12 | Faz 2.9 tamamlandı: HTML ihtiyaç programı raporu ve CSV mahal listesi dışa aktarımı eklendi; Core test runner 16/16 geçti; Core test build ve App normal build 0 hata verdi |
| 2026-06-12 | Faz 2.10 tamamlandı: M31 regresyon kontrolü yapıldı; Core test runner 16/16 geçti, Core test build 0 uyarı/0 hata, App build 0 hata verdi; M31 MVP kapandı |
| 2026-06-12 | Faz 3.1 tamamlandı: M34 yönetmelik ön kontrol klasörü ve `codex34.md` açıldı; kaynak profilli, kesin uygunluk iddiası taşımayan ön kontrol dili belirlendi |
| 2026-06-12 | Faz 3.2 tamamlandı: `KuralSeti`, `Kural`, `KuralSonucu`, `KontrolBaglami` modelleri eklendi; Core test runner 17/17 geçti; App build 0 hata verdi |
| 2026-06-12 | Faz 3.3 tamamlandı: `YonetmelikOnKontrolServisi` eklendi; aktif kural setleri çalışıyor, eksik veri `Kontrol edilemedi` sonucu üretiyor, Core test runner 18/18 geçti |
| 2026-06-12 | Faz 3.4 tamamlandı: ilk veri tabanlı kontroller `YonetmelikOnKontrolServisi` içine eklendi; Core test runner 19/19 geçti; Core test build 0 uyarı/0 hata, App build 0 hata verdi |
| 2026-06-12 | Faz 3.5 tamamlandı: `YonetmelikKaynakProfili` ve varsayılan kaynak profilleri eklendi; ilk veri kontrol sonuçları kaynak profiline bağlandı; Core test runner 21/21 geçti, Core/App build 0 uyarı/0 hata |
| 2026-06-12 | Faz 3.6 tamamlandı: M31 ön kontrol paneli M34 kural sonuçlarına bağlandı; UI kartlarında seviye, kural kodu, kaynak profili ve eksik veri gösteriliyor; App build 0 hata verdi |
| 2026-06-12 | Faz 3.7 tamamlandı: M34 kapanış regresyonu geçti; kesin uygunluk dili görünür UI/Core mesajlarında kullanılmıyor; Faz 3 kapandı, sıradaki adım Faz 4.1 |
| 2026-06-12 | Faz 4.1 tamamlandı: M32 klasörü ve `codex32.md` açıldı; konsept plan, blok yerleşim, ilişki matrisi, koridor modeli ve M34 geri bildirim kapsamı yazıldı |
| 2026-06-12 | Faz 4.2 tamamlandı: M32 plan blok modelleri Core `Architecture/Layout` altında eklendi; Core test runner 22/22 geçti, Core test build 0 uyarı/0 hata, App build 0 hata verdi |
| 2026-06-12 | Faz 4.3 tamamlandı: M32 ilişki enumları ve varsayılan ilişki matrisi servisi eklendi; ofis/konut örnekleriyle Core test runner 23/23 geçti |
| 2026-06-12 | Faz 4.4 tamamlandı: `PlanTaslagiUretimServisi` ve `BasitYerlesimServisi` eklendi; M31 programından çakışmasız kaba yerleşim üretiliyor; Core test runner 24/24 geçti |
| 2026-06-12 | Faz 4.5 tamamlandı: `KoridorYerlesimServisi` eklendi; kat başına ana koridor aksı, blok-koridor bağlantısı ve bağlantısız mahal tespiti üretildi; Core test runner 25/25 geçti |
| 2026-06-12 | Faz 4.6 tamamlandı: M32 konsept plan penceresi, viewmodel'i ve özel 2D canvas'ı eklendi; kat seçimi, blok seçimi/sürükleme, kilitleme ve sağ panel özetleri çalışır hale geldi; App build 0 hata verdi |
| 2026-06-13 | Faz 4.7 tamamlandı: M34 `PlanTaslagi` bağlamı, yerleşim uyarıları ve M32 UI geri bildirim paneli eklendi; Core test runner 26/26 geçti, App build 0 hata verdi |
| 2026-06-13 | Faz 4.8 tamamlandı: Plan taslağı `.e4mimari.json` içine kaydedilip açılıyor; M32 aç/kaydet/farklı kaydet UI akışı eklendi; Core test runner 27/27 geçti, Core/App build 0 uyarı/0 hata |
| 2026-06-13 | Faz 5.1 tamamlandı: M33 klasörü ve `codex33.md` açıldı; çekirdek/şaft/sirkülasyon kapsamı, veri modeli taslağı ve küçük faz listesi yazıldı |
| 2026-06-13 | Faz 5.2 tamamlandı: M33 çekirdek/şaft modelleri ve `KatTaslagi` bağlantısı eklendi; `.e4mimari.json` serileştirme testiyle Core test runner 29/29 geçti; Core/App build 0 uyarı/0 hata |
| 2026-06-13 | Faz 5.3 tamamlandı: `SirkulasyonKontrolServisi` katlar arası aks sürekliliği, kopuk çekirdek/şaft ve aks kayması uyarılarını üretiyor; M34 plan kontrolüne bağlandı; Core test runner 31/31 geçti, Core build 0 uyarı/0 hata, App build 0 hata verdi |
| 2026-06-13 | Faz 5.4 tamamlandı: Islak hacim/şaft yakınlık kontrolü eklendi; şaft yokluğu ve uzak şaft uyarıları M34 plan kontrolüne bağlandı; Core test runner 33/33 geçti, Core build 0 uyarı/0 hata, App build 0 hata verdi |
| 2026-06-13 | Faz 5.5 tamamlandı: Kaçış grafı v1 modelleri ve mahal-koridor-merdiven bağlantı üretimi eklendi; merdivene ulaşamayan mahal uyarısı M34 plan kontrolüne bağlandı; Core test runner 35/35 geçti, Core build 0 uyarı/0 hata, App build 0 hata verdi |
| 2026-06-13 | Faz 5.6 tamamlandı: M32 plan canvas'ında çekirdek/şaft katmanı, sağ panel çekirdek/şaft özeti ve M34 uyarısından çekirdek/şaft seçimi eklendi; Core test runner 35/35 geçti, Core build 0 uyarı/0 hata, App build 0 hata verdi; M33 MVP kapandı |
| 2026-06-13 | Faz 6.1 tamamlandı: M35 klasörü ve `codex35.md` açıldı; 3D BIM model kapsamı, veri modeli taslağı, dışa aktarım hedefleri ve küçük faz listesi yazıldı |
| 2026-06-13 | Faz 6.2 tamamlandı: M35 3D geometri modelleri eklendi; `PlanTaslagi.Model3D` `.e4mimari.json` içinde korunuyor; Core test runner 37/37 geçti, Core test build 0 uyarı/0 hata, App build 0 uyarı/0 hata |
| 2026-06-13 | Faz 6.3 tamamlandı: 2D `PlanTaslagi` verisinden `Bim3DModel` üreten servis eklendi; kat kotları, mahal hacimleri, döşeme/tavan, duvar, çekirdek ve şaft hacimleri üretiliyor; Core test runner 38/38 geçti, Core test build 0 uyarı/0 hata, App build 0 hata verdi |
| 2026-06-13 | Faz 6.4 tamamlandı: M35 basit 3D önizleme penceresi, izometrik render canvas'ı, döndürme/zoom kamera kontrolü, özet panelleri ve mimari modül merkezi bağlantısı eklendi; Core test runner 38/38 geçti, App build 0 hata verdi |
| 2026-06-13 | Faz 6.5 tamamlandı: glTF/OBJ araştırması yapıldı; M35 v1 için GLB birincil, OBJ yedek format seçildi; `Model3DIhracatSecenekleri` export sözleşmesi eklendi; Core test runner 39/39 geçti, Core test build 0 uyarı/0 hata, App build 0 hata verdi |
| 2026-06-13 | Faz 6.6 tamamlandı: M35 kapanış regresyonu eklendi; 3D alan/hacim toplamları ve `.e4mimari.json` kayıt-kurtarma akışı doğrulandı; Core test runner 40/40 geçti, Core/App build 0 uyarı/0 hata; M35 MVP kapandı |
| 2026-06-13 | Faz 7.1 tamamlandı: M36 klasörü ve `codex36.md` açıldı; malzeme modeli, katmanlı yapı elemanı tipleri, mahal finisaj varsayılanları, atama UI ve disiplin veri özetleri kapsamı yazıldı |
| 2026-06-13 | Faz 7.2 tamamlandı: M36 malzeme enumları, `MimariMalzeme`, performans/metraj modelleri ve varsayılan mimari malzeme kataloğu eklendi; `MimariProje.Malzemeler` `.e4mimari.json` içinde korunuyor; Core test runner 43/43 geçti, Core/App build 0 uyarı/0 hata |
| 2026-06-13 | Faz 7.3 tamamlandı: M36 katmanlı yapı elemanı tipleri, performans özeti, varsayılan duvar/döşeme/tavan/kapı/pencere tipi katalogu ve M35 eleman Id atama sözleşmesi eklendi; `YapiElemaniTipleri` ve `MalzemeAtamalari` `.e4mimari.json` içinde korunuyor; Core test runner 46/46 geçti, Core/App build 0 uyarı/0 hata |
| 2026-06-13 | Faz 7.4 tamamlandı: M36 mahal finisaj profili modeli, varsayılan profil kataloğu (konut/ofis/eğitim/konferans/ıslak hacim/teknik hacim) ve `MahalFinisajOneriServisi` eklendi; ıslak hacim döşeme + boyalı tavan varsayılan tipleri kataloğa girdi; ıslak zemin/akustik tavan/kaçış yangın ön kontrol uyarıları M34'e taşındı; `MimariProje.FinisajProfilleri` `.e4mimari.json` içinde korunuyor; Core test runner 49/49 geçti, Core build 0 uyarı/0 hata, App build 0 hata |
| 2026-06-13 | Faz 7.5 öncesi genel kontrol (Faz 0-7.4): kritik hata bulunmadı, 6 tutarsızlık/iyileştirme giderildi — codex 7.4 günlüğü, `Normalize` `FinisajProfilleri` kapsaması, ortak `MahalTipiYardimcilari.IslakHacimMi` (M33+M36 tek tanım), `GereksinimOlustur` kişi kapasitesi düzeltmesi, hedef yükseklik kararının belgelenmesi, `KacisGrafiUret` yan etki dokümantasyonu. Core test runner 49/49 geçti, tam çözüm 0 hata |
| 2026-06-13 | Faz 7.5 tamamlandı: M36 atama UI'si (`MalzemeAtamaWindow`/`MalzemeAtamaViewModel`) eklendi ve mimari modül merkezindeki 36 kartı aktifleştirildi; 3D eleman hedefleri kat bazında listelenip uyumlu yapı elemanı tipi + kategori filtreli malzeme atanıyor, atama kaldırılabiliyor, tip Id'si 3D modele yazılıyor, aç/kaydet akışı çalışıyor; App build 0 hata |
| 2026-06-13 | Faz 7.6 tamamlandı: `DisiplinVeriOzetiServisi` ve `DisiplinVeriOzeti` DTO'ları eklendi; M36 atama + 3D verisinden akustik (Rw/Aw/not), mekanik-enerji (U/dış kabuk), elektrik (mahal yüzey/kaplama) ve metraj (birim/alan/hacim/ağırlık) için okunabilir disiplin özetleri üretiliyor; servis null/boş projede güvenli. Core test runner 50/50 geçti, Core build 0 uyarı/0 hata. Sırada M36.7 regresyon/kapanış |
| 2026-06-13 | Faz 7.7 tamamlandı, M36 MVP kapandı: uçtan-uca kapanış regresyon testi eklendi; tam M36 projesi kaydet/aç sonrası malzeme/tip/finisaj/atama sayıları ve disiplin özeti birebir korunuyor. Core test runner 51/51 geçti, tam çözüm build 0 hata. Mimari başlıkta Faz 7 (M36) tamamlandı; sıradaki faz Faz 8 — M37 Rapor, Pafta ve Çizelgeler |
| 2026-06-13 | Faz 8.1 tamamlandı: M37 klasörü ve `codex37.md` açıldı; çıktı kapsamı (mahal listesi, alan tablosu, kapı-pencere çizelgesi v1, finisaj çizelgesi, ön kontrol raporu, disiplin özeti tablosu) ve iki katmanlı rapor modeli → HTML/CSV builder yaklaşımı belirlendi; `_index.md` ve `maincodex.md` M36 MVP/M37 başlatıldı olarak güncellendi |
| 2026-06-13 | Faz 8.2 tamamlandı: `MimariRaporModeli` ve `MimariRaporUretimServisi.MahalListesiOlustur` eklendi; mahal listesi 3D modelden (gerçek alan/hacim + kat + plan bloğu kullanım sınıfı) veya model yoksa ihtiyaç programından (hedef alan/hacim) üretiliyor, null/boş güvenli. Core test runner 52/52 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.3 tamamlandı: `AlanTablosu`/`AlanKirilimSatiri` ve `AlanTablosuOlustur` eklendi; `AlanOzetiServisi` çıktısı (net/brüt/sirkülasyon/teknik + hedef farkı + kullanım sınıfı kırılımı) raporlanabilir tabloya çevriliyor, null/boş güvenli. Core test runner 53/53 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.4 tamamlandı: `KapiPencereCizelgesi`/`KapiPencereSatiri` ve `KapiPencereCizelgesiOlustur` eklendi; 3D açıklıklar tip+ölçü+yapı elemanı tipine göre gruplanıp pozisyon/adet/ölçü/malzeme ile çizelgeleniyor, atanmamış açıklık ayrı görünüyor. Core test runner 54/54 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.5 tamamlandı: `FinisajCizelgesi`/`FinisajCizelgesiSatiri` ve `FinisajCizelgesiOlustur` eklendi; mahal listesi satırları M36 finisaj önerisiyle eşleştirilip mahal bazında zemin/duvar/tavan tip adı, ıslak hacim bayrağı ve uyarı notu üretiliyor. Core test runner 55/55 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.6 tamamlandı: `OnKontrolRaporu`/`OnKontrolSatiri` ve `OnKontrolRaporuOlustur` eklendi; M34 ilk veri + plan + M36 finisaj uyarıları seviye/kaynak/kural kodlu satırlara çevriliyor, seviye sayıları ve kritik bayrağı üretiliyor. Core test runner 56/56 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.7 tamamlandı, M37 MVP kapandı: `MimariRaporu`+`TumRaporuOlustur`, paylaşılan `MimariRaporMetin`, `MimariRaporHtmlBuilder` (tek belgede tüm tablolar) ve `MimariRaporCsvBuilder` (tablo başına CSV) eklendi; HTML+CSV çalışıyor. Core test runner 57/57 geçti, tam çözüm build 0 hata. Mimari başlıkta açık planlı MVP modülleri (M31-M37) tamamlandı; Faz 9 (M50) ertelendi, Faz 10 (M39) açık |
| 2026-06-13 | M37 XLSX tamamlandı (stretch kapandı): bağımlılık olmadan `XlsxYazici` (System.IO.Compression ile minimal OOXML) ve `MimariRaporXlsxBuilder` (her tablo bir sayfa, ölçümler sayısal hücre) eklendi; ZipArchive ile yapısal doğrulama testi geçti. Artık üç format (HTML+CSV+XLSX) çalışıyor. Core test runner 58/58 geçti, Core/tam çözüm build 0 hata |
| 2026-06-13 | Faz 9 açıldı (erteleme kalktı): M50'nin tamamlandığı (Faz 1-5, M12/M08 köprüleri, 302 test) tespit edildi; codex50 başlığındaki eskimiş "Planlama" notu düzeltildi. Faz 9.1 tamamlandı: M50 veri modeli ve `.e4bina` formatı incelendi, mimari→M50 üretici adaptör eşlemesi ve riskleri belirlendi (bkz. Faz 9 tablosu altı). Kod yazılmadı; 9.2 adaptör tasarımı onayı bekleniyor |
| 2026-06-13 | Faz 9.2 tamamlandı: `Architecture/Exporting/BinaGeometriAdapteri` eklendi — `MimariProje.PlanTaslagi.Model3D` → `BinaGeometri`: kat→BinaKat (KatNo/Ad/KatYüksekliği), mahal→BinaOda (4-köşe dikdörtgen poligon + alan, Id=KaynakMahalBloguId kararlı), açıklık→BinaAciklik (yalnız Kapı/Pencere; Bas/Bit duvar doğrusu+merkezden, eşik=AltKotu). Boş/null güvenli. Core test runner 59/59 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 9.3 tamamlandı: `BinaGeometriDisaAktarimServisi` eklendi — adaptör çıktısını `BinaGeometriServisi.Kaydet` ile `.e4bina`'ya yazar, `MimariProje.BinaGeometriYolu`'na bağlar. Üzerine-yazma koruması (dosya varsa onaysız bağla, yazma) + üzerine yazınca mevcut `GeometriRevizyon` devralınıp artırılır (artan damga korunur). Durum: Uretildi/BaglandiUzerineYazilmadi/UzerineYazildi. Core test runner 60/60 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 9.4 tamamlandı (doğrulama): oda Id = kaynak mahal bloğu Id (kararlılık çapası), benzersiz; 3D model yeniden üretiminde ve `.e4mimari` kaydet/aç round-trip'inde oda+kat Id imzası birebir korunuyor. Yeni production kodu yok; kararlılık adaptöre baştan gömülüydü. Core test runner 61/61 geçti |
| 2026-06-13 | Faz 9.5 tamamlandı, **Faz 9 (M50 entegrasyonu) kapandı**: mimari üretilen `.e4bina` diskten yüklenip M12 (`BinaGeometriImportServisi`) ve M08 (`BinaGeometriMekanAdapteri`) köprülerine verildi — M12 kat/oda sayıları + oda Id'lerini AYNEN koruyor, uyarısız; M08 mekan sayısı eşleşiyor ve Mekan.Id `OdaIdToGuid` ile deterministik. Mimari→M50→disiplin zinciri uçtan uca kanıtlandı. Core test runner 62/62 geçti, tam çözüm build 0 hata. (M21 mekanik henüz köprüsüz — gelecekte aynı `.e4bina`'yı okuyacak) |
| 2026-06-13 | Faz 10.1 tamamlandı: M39 klasörü ve `codex39.md` açıldı; kapsam (revizyon farkı, disiplin mutabakatı, IFC/glTF/DXF format kararı) ve `Architecture/Coordination` kod konumu belirlendi; IFC tam üretiminin uzun vadeye bırakılması öngörüldü. `_index.md` ve `maincodex.md` M39 başlatıldı olarak güncellendi |
| 2026-06-13 | Faz 10.2 tamamlandı: `Architecture/Coordination/RevizyonFarki` modelleri + `RevizyonFarkiServisi` eklendi; iki `MimariProje` (adaptörle geometriye çevrilir) veya iki `BinaGeometri` kararlı oda Id'siyle karşılaştırılıp eklenen/silinen/değişen mahal, eklenen/silinen kat ve toplam alan deltası raporlanıyor. Core test runner 63/63 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 10.3 tamamlandı: format kararı belgelendi (OBJ şimdi uygulandı; GLB/DXF ertelendi; IFC uzun vade — ağır kütüphane). `Architecture/Exporting/MimariObjIhracatServisi` eklendi: bağımlılıksız Wavefront OBJ, mahal+duvar kutu mesh. Core test runner 64/64 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 10.4 tamamlandı + M39.5 regresyon, **M39 MVP ve Faz 10 kapandı**: `Architecture/Coordination/MutabakatServisi` + `MutabakatRaporu` eklendi — askıda atama (var olmayan 3D eleman) ve kırık tip referansı yakalanıyor. Core test runner 65/65 geçti, tam çözüm build 0 hata. **Mimari başlıkta planlı tüm fazlar (Faz 0-10) tamamlandı**; kalan işler isteğe bağlı (UI bağlama, GLB/DXF/IFC, M38 cephe, M21 köprüsü) |
| 2026-06-13 | UI bağlama (1): `Architecture/Reports/Views/MimariCiktiWindow` + `MimariCiktiViewModel` eklendi — proje aç/örnek, özet + ön kontrol gösterimi, HTML/XLSX rapor kaydet, OBJ ve `.e4bina` dışa aktarım (kayıt diyaloglarıyla). Mimari modül merkezine M37 "Rapor ve İhracat" kartı eklendi (grid 4 satır). App build 0 hata, yeni koddan uyarı yok. (GUI render bu ortamda elle doğrulanmadı; compiled binding derlemede geçti) |
