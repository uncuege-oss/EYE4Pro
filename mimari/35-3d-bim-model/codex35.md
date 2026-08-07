# CODEX 35 — 3D BIM Model ve Görselleştirme

> **Durum:** MVP tamamlandı (2026-06-13)
> **Ana takip:** [../codex30.md](../codex30.md)
> **Önceki modüller:** M31 ihtiyaç programı, M32 konsept plan, M33 düşey çekirdek/şaft,
> M34 yönetmelik ön kontrol MVP tamamlandı.
> **Amaç:** M32/M33 taslak plan verisini basit, tutarlı ve dışa aktarılabilir bir 3D BIM
> modeline dönüştürmek.
> **M50 kararı:** M50 entegrasyonu bu modülde de arkaya alınır. İlk MVP `.e4mimari.json`
> ve `PlanTaslagi` üzerinde çalışır; M50/IFC koordinasyonu M39/Faz 9-10 tarafında ele alınır.

---

## 0. Kullanım Protokolü

1. Yeni oturumda önce `maincodex.md`, sonra `mimari/codex30.md`, sonra bu dosya okunur.
2. Aktif adım bu dosyadaki küçük faz tablosundan ve `codex30.md` Faz 6 tablosundan bulunur.
3. Kod yazmadan önce ilgili Core/App klasörleri incelenir.
4. Her işlemden sonra hem bu dosya hem `mimari/codex30.md` güncellenir.
5. Bir adım tamamlandığında durulur ve kullanıcıdan sonraki adıma geçiş onayı istenir.

---

## 1. Hedef

Bu modül, mimari tasarımın yalnız 2D blok planda kalmamasını sağlar. İlk hedef gerçek bir IFC
motoru veya detaylı uygulama projesi üretmek değil; mimarın M31-M34 akışında oluşturduğu bina
kararlarını hızlıca 3D hacme çevirmek, kontrol etmek ve sonraki detay/malzeme fazlarına sağlam
bir geometri tabanı bırakmaktır.

M35 tamamlandığında kullanıcı şunları yapabilmelidir:

- Kat döşemelerini, mahal hacimlerini, duvarları ve temel açıklıkları 3D olarak görebilmek.
- Merdiven, asansör ve şaftları kütlesel 3D hacimler halinde takip edebilmek.
- 2D plandaki alan/yükseklik kararlarının 3D hacme doğru taşındığını kontrol edebilmek.
- 3D modeli boş olmayan, döndürülebilir ve yakınlaştırılabilir bir önizlemede inceleyebilmek.
- İlk paylaşım/koordinasyon için basit bir dışa aktarım hedefi seçebilmek.

---

## 2. Kapsam

### MVP Kapsamında

- `PlanTaslagi` ve `KatTaslagi` verisinden 3D kat modeli üretimi.
- Mahal bloklarından prizmatik mahal hacimleri.
- Kat döşemesi ve tavan temsilinin basit geometri olarak oluşturulması.
- Blok sınırlarından kaba duvar hacimleri veya duvar segmentleri.
- Kapı/pencere açıklıkları için v1 placeholder veri modeli.
- Merdiven, yangın merdiveni, asansör ve şaftların kütlesel 3D temsili.
- Kat yüksekliği, net mahal yüksekliği, taban kotu ve üst kot bilgisinin korunması.
- 3D önizleme için UI iskeleti.
- glTF/OBJ gibi ilk dışa aktarım yolunun araştırılması ve karar kaydı.
- Geometri testleri: alan/hacim ve kat yüksekliği tutarlılığı.

### MVP Dışında

- Tam parametrik kapı/pencere doğrama detayı.
- Gerçek merdiven basamak/rıht geometri üretimi.
- Çatı detay çözümü ve karmaşık kabuk geometrisi.
- IFC üretimi ve M50 ortak model entegrasyonu.
- Çakışma analizi, statik modelleme veya uygulama projesi seviyesinde yapı elemanı detayı.
- Malzeme katmanları; bu M36'ya bırakılır.

---

## 3. Veri Modeli Taslağı

```text
Bim3DModel
├── Kat3DModel
│   ├── Doseme3D
│   ├── Tavan3D
│   ├── MahalHacmi3D
│   ├── Duvar3D
│   ├── Aciklik3D
│   └── CekirdekHacmi3D / SaftHacmi3D
└── ModelOzet
```

### Bim3DModel

Önerilen alanlar:

- `Id`, `Ad`, `KaynakPlanId`
- `Katlar`
- `ToplamKatSayisi`, `ToplamMahalHacmiM3`, `ToplamDosemeAlaniM2`
- `UretimTarihiUtc`, `Notlar`

### Kat3DModel

Önerilen alanlar:

- `Id`, `KatNo`, `Ad`
- `TabanKotuM`, `KatYuksekligiM`, `NetYukseklikM`
- `Dosemeler`, `Tavanlar`, `Mahaller`, `Duvarlar`, `Acikliklar`
- `CekirdekHacimleri`, `SaftHacimleri`

### MahalHacmi3D

Önerilen alanlar:

- `Id`, `KaynakMahalBloguId`, `Ad`, `Tip`
- `X`, `Y`, `Z`, `GenislikM`, `DerinlikM`, `YukseklikM`
- `AlanM2`, `HacimM3`
- `KatNo`, `Notlar`

### Duvar3D

Önerilen alanlar:

- `Id`, `KaynakMahalBloguId`
- `BaslangicX`, `BaslangicY`, `BitisX`, `BitisY`
- `TabanKotuM`, `YukseklikM`, `KalinlikM`
- `DuvarTipiId`
- `AciklikIdleri`

### Aciklik3D

Önerilen alanlar:

- `Id`, `KaynakMahalBloguId`, `Tip`
- `DuvarId`, `MerkezX`, `MerkezY`, `AltKotuM`
- `GenislikM`, `YukseklikM`
- `Notlar`

---

## 4. Kod Konumu

Planlanan Core yerleşimi:

```text
src/Kst.Acoustics.Core/Architecture/Model3D/
├── Bim3DModel.cs
├── Kat3DModel.cs
├── MahalHacmi3D.cs
├── Duvar3D.cs
├── Doseme3D.cs
├── Aciklik3D.cs
├── Model3DUretimServisi.cs
├── Model3DGeometriServisi.cs
└── Model3DIhracatSecenekleri.cs
```

Planlanan UI yerleşimi:

```text
src/Kst.Acoustics.App/Architecture/Model3D/
├── ViewModels/Model3DViewModel.cs
├── Views/Model3DWindow.axaml
└── Rendering/
```

İlk UI hedefi, gerçek zamanlı profesyonel BIM viewer değil; modelin boş olmadığını, kat ve mahal
hacimlerinin doğru üretildiğini ve kullanıcı etkileşiminin çalıştığını gösteren hafif bir önizlemedir.

---

## 5. M32, M33, M34 ve M36 Bağlantısı

M32, mahal blokları ve koridorları üretir. M33 bu plana düşey çekirdek ve şaft bilgisini ekler.
M35, bu iki veri kümesini kat yüksekliğiyle 3D hacme taşır. M34 ön kontrol sonuçları 3D model
üretimini engellemez; ancak eksik veya riskli veriler 3D model özetinde uyarı olarak taşınabilir.

M36 başladığında duvar, döşeme, tavan, kapı ve pencere gibi 3D elemanlara malzeme ve teknik tip
atanacaktır. Bu yüzden M35 eleman Id'leri kararlı tutulmalı, sonraki fazlarda malzeme/metraj
bağlantısına izin vermelidir.

---

## 6. Dışa Aktarım Hedefleri

İlk sürümde öncelik görsel kontrol ve koordinasyondur.

| Format | Öncelik | Kullanım | Not |
|--------|---------|----------|-----|
| glTF/glb | Yüksek | 3D önizleme, paylaşım, hafif koordinasyon | İlk araştırılacak yol |
| OBJ | Orta | Basit geometri dışa aktarımı | Malzeme/metadata sınırlı |
| DXF 2D | Orta | Kat planı çıktısı | M37/M39 ile genişleyebilir |
| IFC | Uzun vade | BIM veri değişimi | M39'a bırakılır |

M35 içinde dışa aktarım hedefi yalnız araştırma ve v1 karar kaydıdır. Tam IFC ya da M50 adapteri bu
fazda yapılmayacaktır.

### 6.1 Araştırma Sonucu ve V1 Kararı

2026-06-13 kontrolünde M35 için ilk dışa aktarım hedefi **GLB** olarak seçildi.

Kaynak özeti:

| Seçenek | Kaynak | Değerlendirme | Karar |
|---------|--------|---------------|-------|
| glTF/glb | [Khronos glTF 2.0 Specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html) | glTF 2.0 resmi standart; GLB JSON ve binary veriyi tek dosyada taşıyabilir. Koordinat/birim kuralları net. | İlk hedef |
| SharpGLTF | [SharpGLTF GitHub](https://github.com/vpenades/SharpGLTF), [SharpGLTF.Core NuGet](https://www.nuget.org/packages/SharpGLTF.Core), [SharpGLTF.Toolkit NuGet](https://www.nuget.org/packages/SharpGLTF.Toolkit/) | .NET Standard / net10 uyumlu, glTF 2.0 okuma-yazma için odaklı C# kütüphanesi. | İlk export motoru adayı |
| AssimpNet | [AssimpNet NuGet](https://www.nuget.org/packages/AssimpNet/) | Birçok formatı içe/dışa aktarabilen Assimp wrapper'ı; native bağımlılık ve geniş kapsam nedeniyle M35 MVP için ağır. | Şimdilik ertelendi |
| OBJ | Bağımlılıksız basit writer | Mesh koordinatlarını yazmak kolay; metadata, malzeme ve hiyerarşi sınırlı. | Yedek format |

V1 sözleşme:

- Varsayılan format: `.glb`
- Yedek format: `.obj`
- Birim: metre (`m`)
- Koordinat ilkesi: glTF sağ el koordinat sistemi; modelde `+Y` yukarı kabul edilir.
- Mesh ayrımı: mahal, çekirdek ve şaft ayrı mesh/node olarak yazılır.
- Metadata: `extras` alanında kaynak `PlanTaslagi`, kat, mahal/çekirdek/şaft Id'leri ve adları taşınır.
- Dosya adı: proje adından temizlenmiş `*.glb`; varsayılan kök `eye4pro-mimari-3d.glb`.
- M35 MVP'de IFC yoktur; IFC/M50 koordinasyonu M39 tarafına bırakılır.

---

## 7. Küçük Faz Listesi

### M35.1 — Codex Açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 35.1.1 | Klasör oluştur | `mimari/35-3d-bim-model/` var | ✅ Tamamlandı |
| 35.1.2 | `codex35.md` oluştur | Kapsam, model taslağı ve dışa aktarım hedefleri yazıldı | ✅ Tamamlandı |
| 35.1.3 | Ana takipleri güncelle | `maincodex.md`, `mimari/_index.md`, `codex30.md` M35 durumunu gösterir | ✅ Tamamlandı |

### M35.2 — 3D Geometri Modelleri

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 35.2.1 | `Architecture/Model3D` klasörü | Core altında 3D model klasörü açılır | ✅ Tamamlandı |
| 35.2.2 | Ana model | `Bim3DModel`, `Kat3DModel`, `MahalHacmi3D` eklenir | ✅ Tamamlandı |
| 35.2.3 | Yapı elemanı v1 | `Duvar3D`, `Doseme3D`, `Tavan3D`, `Aciklik3D` eklenir | ✅ Tamamlandı |
| 35.2.4 | Çekirdek/şaft hacmi | M33 elemanları 3D kütle modeline bağlanır | ✅ Tamamlandı |
| 35.2.5 | Serileştirme testi | `.e4mimari.json` 3D model verisini kaybetmez | ✅ Tamamlandı |

### M35.3 — 2D Taslaktan 3D Üretim

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 35.3.1 | Üretim servisi | `PlanTaslagi` girdisinden `Bim3DModel` oluşur | ✅ Tamamlandı |
| 35.3.2 | Kat kotları | Kat no, taban kotu ve kat yüksekliği hesaplanır | ✅ Tamamlandı |
| 35.3.3 | Mahal hacimleri | Blok alanı ve yükseklikten hacim hesaplanır | ✅ Tamamlandı |
| 35.3.4 | Duvar/döşeme üretimi | Basit prizmatik duvar ve döşeme elemanları oluşur | ✅ Tamamlandı |
| 35.3.5 | Test | Alan/hacim değerleri 2D hedeflerle tutarlı çıkar | ✅ Tamamlandı |

### M35.4 — Basit 3D Önizleme

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 35.4.1 | Pencere iskeleti | Mimari merkezden 3D model ekranı açılır | ✅ Tamamlandı |
| 35.4.2 | Render katmanı | Model boş görünmez; kat/mahal hacimleri seçilebilir renkte görünür | ✅ Tamamlandı |
| 35.4.3 | Kamera kontrolü | Döndürme, yakınlaştırma ve kaydırma çalışır | ✅ Tamamlandı |
| 35.4.4 | Özet panel | Kat sayısı, toplam alan/hacim ve eleman sayıları görünür | ✅ Tamamlandı |
| 35.4.5 | App build | UI derlemesi hatasız geçer | ✅ Tamamlandı |

### M35.5 — glTF/OBJ Araştırma ve Karar

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 35.5.1 | Kütüphane araştırması | .NET/Avalonia uyumlu seçenekler listelenir | ✅ Tamamlandı |
| 35.5.2 | İlk format kararı | glTF/glb veya OBJ v1 yolu seçilir | ✅ Tamamlandı |
| 35.5.3 | Export sözleşmesi | Dosya adı, birim, koordinat ve metadata ilkeleri yazılır | ✅ Tamamlandı |

### M35.6 — Geometri Regresyonu

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 35.6.1 | Core testleri | 3D üretim testleri Core runner'a eklenir | ✅ Tamamlandı |
| 35.6.2 | Kayıt testi | 3D model `.e4mimari.json` ile aç/kaydet akışında korunur | ✅ Tamamlandı |
| 35.6.3 | Kapanış build | Core test runner, Core build ve App build başarılıdır | ✅ Tamamlandı |

---

## 8. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M35 MVP tamamlandı; Faz 7.1 / M36 için kullanıcı onayı bekleniyor |
| Sonraki Adım | M36 Detay, Malzeme ve Yapı Elemanı için `codex36.md` açılışı |
| Kod Durumu | M35 3D BIM model MVP'si tamamlandı; 2D plandan 3D hacim, basit önizleme ve GLB/OBJ export sözleşmesi hazır |
| Test Durumu | Core test runner 40/40 geçti; Core test build 0 uyarı/0 hata; App build 0 uyarı/0 hata |

---

## 9. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-13 | M35 başlatıldı; ilk MVP'nin M50'ye bağlanmadan `.e4mimari.json` ve `PlanTaslagi` üzerinde ilerlemesine karar verildi |
| 2026-06-13 | İlk 3D kapsam kat döşemeleri, mahal hacimleri, duvar prizması, açıklık placeholder'ları, çekirdek/şaft hacimleri ve basit 3D önizleme olarak belirlendi |
| 2026-06-13 | Dışa aktarımda ilk araştırma hedefi glTF/glb; OBJ basit geometri alternatifi, IFC ise M39 uzun vade hedefi olarak ayrıldı |
| 2026-06-13 | Faz 6.2 tamamlandı: `Architecture/Model3D` altında `Bim3DModel`, `Kat3DModel`, mahal hacmi, duvar, döşeme, tavan, açıklık, çekirdek ve şaft 3D hacim modelleri eklendi; `PlanTaslagi.Model3D` `.e4mimari.json` içinde korunuyor |
| 2026-06-13 | Faz 6.3 tamamlandı: `Model3DUretimServisi`, `Model3DGeometriServisi` ve üretim seçenekleri eklendi; 2D plan taslağından kat kotu, mahal hacmi, döşeme, tavan, duvar, çekirdek ve şaft 3D hacimleri üretiliyor |
| 2026-06-13 | Faz 6.4 tamamlandı: M35 `Model3DWindow`, `Model3DViewModel` ve `Model3DCanvas` eklendi; mimari modül merkezinden açılıyor, model hacimleri izometrik görünüyor, kamera döndürme/zoom ve özet panelleri çalışıyor |
| 2026-06-13 | Faz 6.5 tamamlandı: Khronos glTF 2.0, SharpGLTF ve AssimpNet kaynakları kontrol edildi; M35 v1 dışa aktarım hedefi GLB, yedek format OBJ olarak belirlendi; `Model3DIhracatSecenekleri` eklendi |
| 2026-06-13 | Faz 6.6 tamamlandı: M35 kapanış regresyonu eklendi; 3D alan/hacim toplamları ve `.e4mimari.json` kayıt-kurtarma akışı doğrulandı; Core test runner 40/40 geçti, Core/App build 0 uyarı/0 hata |
