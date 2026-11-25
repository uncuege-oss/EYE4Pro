# MAINCODEX — EYE4PRO Ana Referans Dosyası

> **Kullanım:** Yeni bir Claude oturumu açılmadan önce bu dosya okunur.
> Her modülün kendi codex dosyası bu belgeye bağlıdır.
> Ortak kararlar buraya yazılır; modüle özgü kararlar ilgili codex'e yazılır.

---

## 1. Program Kimliği

| Alan | Değer |
|------|-------|
| Program Adı | **EYE4PRO** |
| Akustik Ürünü | EYE4PRO Acoustics |
| Elektrik Ürünü | EYE4PRO Electric |
| Stack | C# + Avalonia (net10.0) |
| UI Dili | Türkçe (tüm görünür etiket ve mesajlar) |
| Proje Dosyası | `.e4p01` (JSON tabanlı; eski `.eye4proj.json` yalnız geriye uyumluluk) |
| Solution | `src/Kst.Acoustics.slnx` |

---

## 2. Dizin Yapısı

```
<depo-koku>/                            ← Proje kökü (macOS/Windows bağımsız)
│
├── maincodex.md                        ← Bu dosya
├── _ortak/
│   ├── belgeler/                       ← Kullanım kılavuzu vb.
│   ├── sablonlar/                      ← Panel şablonları, DXF örnekleri
│   └── scratch/                        ← Deneme alanı
│
├── akustik/                            ← Ana Başlık 1 (01–09)
│   ├── _index.md
│   ├── 01-proje-geometri/codex.md
│   ├── 02-delikli-panel/codex2.md
│   ├── 03-ses-yalitimi/codex3.md
│   ├── 04-ses-bariyeri/codex4.md
│   ├── 05-darbe-yuzey/codex5.md
│   ├── 06-oda-modlari/codex6.md
│   ├── 07-yerinde-olcum/codex7.md
│   └── 08-bina-gurultu/codex8.md
│
├── elektrik/                           ← Ana Başlık 2 (11–19)
│   ├── _index.md
│   ├── 11-tek-hat-semasi/codex11.md
│   └── 12-basit-proje-ciz/codex12.md
│
├── mekanik/                            ← Ana Başlık 3 (21–29)
├── mimari/                             ← Ana Başlık 4 (30–39) — BIM planlama başladı
│   ├── _index.md
│   ├── codex30.md                      ← Mimari BIM ana faz takip dosyası
│   ├── 31-ihtiyac-programi-mahal-sablonlari/codex31.md
│   ├── 32-konsept-plan-parametrik-yerlesim/codex32.md
│   ├── 33-dusey-cekirdek-saft-sirkulasyon/codex33.md
│   ├── 34-yonetmelik-on-kontrol/codex34.md
│   ├── 35-3d-bim-model/codex35.md
│   ├── 36-detay-malzeme-yapi-elemani/codex36.md
│   └── mimari-modul-planlama-raporu.md
├── insaat/                             ← Ana Başlık 5 (41–49) — boş
│
├── terrain/                            ← Ana Başlık 6 (60–69) — EYE4PRO Terrain
│   ├── _index.md
│   ├── 60-hafriyat-hesapla/codex60.md
│   └── 61-bina-temeli-kota-oturtma/codex61.md
│
├── landscape/                          ← Ana Başlık 7 (70–79) — EYE4PRO Landscape
│   ├── _index.md
│   ├── 70-park-tasarimi/codex70.md
│   └── 71-bitkilendirme-peyzaj/codex71.md
│
├── ortak-moduller/                     ← Ortak Modüller (50–59)
│   ├── 50-bina-geometri/codex50.md
│   └── 51-salon-tasarla/codex51.md
│
└── src/                                ← Kaynak kod
    ├── Kst.Acoustics.App/              ← UI (Avalonia)
    ├── Kst.Acoustics.Core/             ← İş mantığı (ortak çekirdek)
    ├── Kst.Acoustics.Core.Tests/       ← UI'siz Core test runner
    ├── Kst.Acoustics.Metraj/           ← Metraj motoru
    ├── Kst.Acoustics.Metraj.App/
    ├── Kst.Acoustics.Metraj.Tests/
    ├── Kst.Acoustics.ImportPreview/
    ├── Kst.Acoustics.Tests/
    └── Kst.Acoustics.slnx
```

---

## 3. Modül Dizini

### Akustik

| # | Modül Adı | Durum | Codex |
|---|-----------|-------|-------|
| 01 | Proje Yönetimi & Geometri Import | Tamamlandı | [codex.md](akustik/01-proje-geometri/codex.md) |
| 02 | Delikli Panel Tasarımı (TMM) | Tamamlandı | [codex2.md](akustik/02-delikli-panel/codex2.md) |
| 03 | Ses Yalıtımı & Duvar Paneli (Rw) | Tamamlandı | [codex3.md](akustik/03-ses-yalitimi/codex3.md) |
| 04 | Ses Bariyeri & Çevresel Gürültü | Tamamlandı | [codex4.md](akustik/04-ses-bariyeri/codex4.md) |
| 05 | Darbe Emici Yüzey (Impact Surface) | Tamamlandı | [codex5.md](akustik/05-darbe-yuzey/codex5.md) |
| 06 | Oda Modları Hesabı | Tamamlandı | [codex6.md](akustik/06-oda-modlari/codex6.md) |
| 07 | Yerinde Ölçüm (REW düzeyi) | Geliştiriliyor | [codex7.md](akustik/07-yerinde-olcum/codex7.md) |
| 08 | Bina Gürültü Hesabı (TS EN 12354) | Geliştiriliyor | [codex8.md](akustik/08-bina-gurultu/codex8.md) |

### Elektrik

| # | Modül Adı | Durum | Codex |
|---|-----------|-------|-------|
| 11 | Tek Hat Şeması | Geliştiriliyor | [codex11.md](elektrik/11-tek-hat-semasi/codex11.md) |
| 12 | Basit Proje Çiz (Kuvvetli Akım) | Geliştiriliyor | [codex12.md](elektrik/12-basit-proje-ciz/codex12.md) |
| 13 | Zayıf Akım Tek Hat Şeması | Geliştiriliyor (7 sistem: Data/Kamera/Anons/TV/Yangın/Telefon/Diafon; DXF+SVG+PNG, şema sürümü, kapasite denetimi) | [codex13.md](elektrik/13-zayif-akim-tek-hat/codex13.md) |
| 14 | Keşif Hazırla | Faz 2 tamamlandı (otomatik metraj + öğrenen/otomatik Poz Atama + Referanslar) | [codex14.md](elektrik/14-kesif-hazirla/codex14.md) |
| 15 | Çevre Aydınlatma Projesi Hazırla | Core + ilk UI akışı mevcut | [codex15.md](elektrik/15-cevre-aydinlatma/codex15.md) |
| 16 | Zayıf Akım Proje Çiz | Geliştiriliyor | [codex16.md](elektrik/16-zayif-akim-proje-ciz/codex16.md) |

### Mekanik

| # | Modül Adı | Durum | Codex |
|---|-----------|-------|-------|
| 21 | Kanal Hesabı | Planlı; başlatıcı mevcut, hesap motoru henüz yok | [codex21.md](mekanik/21-kanal-hesabi/codex21.md) |

### Mimari

| # | Modül Adı | Durum | Codex |
|---|-----------|-------|-------|
| 30 | Mimari BIM Ana Plan ve Faz Takibi | Başlatıldı | [codex30.md](mimari/codex30.md) |
| 31 | İhtiyaç Programı ve Mahal Şablonları | MVP tamamlandı | [codex31.md](mimari/31-ihtiyac-programi-mahal-sablonlari/codex31.md) |
| 32 | Konsept Plan ve Parametrik Yerleşim | MVP tamamlandı | [codex32.md](mimari/32-konsept-plan-parametrik-yerlesim/codex32.md) |
| 33 | Düşey Çekirdek, Şaft ve Sirkülasyon | MVP tamamlandı | [codex33.md](mimari/33-dusey-cekirdek-saft-sirkulasyon/codex33.md) |
| 34 | Yönetmelik Ön Kontrol Motoru | MVP tamamlandı | [codex34.md](mimari/34-yonetmelik-on-kontrol/codex34.md) |
| 35 | 3D BIM Model ve Görselleştirme | MVP tamamlandı | [codex35.md](mimari/35-3d-bim-model/codex35.md) |
| 36 | Detay, Malzeme ve Yapı Elemanı | MVP tamamlandı | [codex36.md](mimari/36-detay-malzeme-yapi-elemani/codex36.md) |
| 37 | Mimari Pafta, Rapor ve Çizelgeler | MVP tamamlandı | [codex37.md](mimari/37-pafta-rapor-cizelgeler/codex37.md) |
| 38 | Cephe, Günışığı ve Kabuk Analizi | Planlandı | [codex38.md](mimari/38-cephe-gunisigi-kabuk/codex38.md) |
| 39 | BIM Koordinasyon ve İhracat | MVP tamamlandı | [codex39.md](mimari/39-bim-koordinasyon-ihracat/codex39.md) |

### Terrain (EYE4PRO Terrain)

| # | Modül Adı | Durum | Codex |
|---|-----------|-------|-------|
| 60 | Hafriyat Hesapla (Yarma/Dolgu) | MVP tamam (F0-F8 + App + M15 köprü) | [codex60.md](terrain/60-hafriyat-hesapla/codex60.md) |
| 61 | Bina Temeli Kota Oturtma | Tamam (F0-F5 + M50/M35 bina kaynakları) | [codex61.md](terrain/61-bina-temeli-kota-oturtma/codex61.md) |

### Landscape (EYE4PRO Landscape)

| # | Modül Adı | Durum | Codex |
|---|-----------|-------|-------|
| 70 | Park Tasarımı (Sert Zemin) | F0-F6 tamam (MVP: Core + App park penceresi) | [codex70.md](landscape/70-park-tasarimi/codex70.md) |
| 71 | Bitkilendirme & Peyzaj (Yumuşak Zemin) | Planlandı | [codex71.md](landscape/71-bitkilendirme-peyzaj/codex71.md) |

### Ortak Modüller

| # | Modül Adı | Durum | Codex |
|---|-----------|-------|-------|
| 50 | Bina Geometrisi | Aktif | [codex50.md](ortak-moduller/50-bina-geometri/codex50.md) |
| 51 | Salon Tasarla (Oditoryum) | MVP tamam (Faz 0-8) | [codex51.md](ortak-moduller/51-salon-tasarla/codex51.md) |

### İnşaat

Modüller henüz tanımlanmadı. Yeni modül açıldığında:
1. `eye4pro/<baslik>/<NN-modul-adi>/` klasörü oluştur
2. `codex<NN>.md` dosyasını bu klasöre yaz
3. İlgili `_index.md`'yi güncelle
4. Bu tabloya satır ekle

---

## 4. Ortak Teknik Kararlar

### 4.1 Geometri ve İçe Aktarma
- Dış geometri içe aktarma **yalnızca DXF** formatında yapılır.
- Desteklenen DXF varlıkları: `3DFACE`, kapalı `POLYLINE`, kapalı `LWPOLYLINE`.
- `$INSUNITS` bloğu okunur; bulunamazsa kullanıcıdan birim istenir.
- DXF katman adları yüzeylerin kategori/grup kaynağıdır.
- 4'ten fazla köşeli yüzeyler geçerlidir (kavisli CAD öğelerinin çokgen yaklaşımı).
- Malzeme isimleri içe aktarma sırasında zorunlu değildir; sonradan atanır.

### 4.2 Proje Dosyası
- M01 aktif formatı: JSON, uzantı `.e4p01`; eski `.eye4proj.json` yalnız geriye uyumluluk içindir.
- Geriye dönük uyumluluk: eksik JSON alanları boş liste/null olarak deserialize edilir.
- Kayıt durumu `IsDirty` bayrağıyla izlenir; başlık çubuğunda `*` gösterilir.

### 4.3 UI Kuralları
- Tüm görünür etiket, mesaj ve hata metni **Türkçe** yazılır.
- ASCII-Türkçe karakter hataları kabul edilmez (`ö`, `ü`, `ç`, `ş`, `ı`, `İ`, `ğ` doğru kullanılmalı).
- Pencere boyutu büyüdükçe içerik alanı büyümelidir — sabit `Height` kullanılmaz, `MinHeight + *` tercih edilir.
- Modül karmaşıklığı arttıkça `ViewModel` partial class'lara bölünür (`FileOps.cs`, `Categories.cs`, vb.).

### 4.4 Mimari
- Core kütüphanesi (`Kst.Acoustics.Core`) tüm iş mantığını barındırır; UI katmanı ona bağımlıdır, tersi yasaktır.
- Büyük N işlemlerde O(N²) döngü yerine `Dictionary` tabanlı O(1) arama tercih edilir.
- `async/await` + `Task.Run` ile büyük dosya/hesaplama işlemleri UI thread'ini bloke etmemeli.

### 4.5 Bina Geometrisi (M50)
- Bina geometrisi düzenleme (DXF import, oda blokları, açıklıklar, kat yönetimi, oda adları,
  döşeme/tavan patlatma) **yalnız M50 modülünde** yapılır.
- Her ana başlığın modül girişinde en üstte "Bina Geometrisi Oluştur" girişi bulunur → M50 açılır.
- M50 çıktısı `.e4bina.json` dosyasıdır; modüller bu dosyayı **salt-okunur** yükler
  ("Bina Geometrisi Yükle"). Modül içinden geometri değişikliğine izin verilmez.
- Açıklık (kapı/pencere) tanımı ve boyutları M50'de girilir; modüller yalnız malzeme/atama yapar.
- Yeni modül bina geometrisi gerektiriyorsa kendi editörünü yazmaz, M50 dosyası yükler.
- Ayrıntılar: [codex50.md](ortak-moduller/50-bina-geometri/codex50.md)

### 4.6 Mimari BIM Planı (M30-M39)
- Mimari başlığın ana takip dosyası [codex30.md](mimari/codex30.md)'dir.
- M50 tasarım aşamasında olduğu için mimari BIM geliştirmesinde M50 entegrasyonu ilk fazlarda yapılmaz;
  entegrasyon [codex30.md](mimari/codex30.md) Faz 9'a ertelenmiştir.
- İlk programlama `.e4mimari.json` ve `src/Kst.Acoustics.Core/Architecture/` domain modeli üzerinde ilerler.
- Mimari tasarım akışı: bina işlevi → ihtiyaç programı → mahal şablonları → konsept yerleşim
  → yönetmelik ön kontrolü → 3D BIM → detay/malzeme → rapor/çıktı → M50/diğer disiplin entegrasyonu.
- Mimari başlıkta her işlemden sonra `mimari/codex30.md` durum tablosu güncellenir.

### 4.7 Arazi Geometrisi (M60 / Terrain)
- Arazi yüzeyi (mevcut + tasarım/hafriyat sonrası kotlar) M50 deseniyle **paylaşılan salt-okunur
  artifact**'tır: **`.e4arazi.json`**. Üretici M60'tır.
- `.e4arazi.json` **disiplinler arası ortak altlık**tır. Tüketiciler salt-okunur yükler, geometriyi
  değiştirmez: M61 (temel kota oturtma), mimari M30-M39 (tabii zemin/tesviye kotu), **elektrik M15
  (çevre/park aydınlatma)**, ileride mekanik (dış tesisat güzergâh kotu) ve akustik (M04 arazi
  difraksiyon altlığı).
- Arazi gerektiren modül kendi DTM editörünü yazmaz; M60 dosyası yükler (M50 kuralının arazi karşılığı).
- Ayrıntılar: [terrain/_index.md](terrain/_index.md), [codex60.md](terrain/60-hafriyat-hesapla/codex60.md)

### 4.8 Park ve Peyzaj Geometrisi (M70 / Landscape)
- Landscape başlığı (70–79) M50/M60 **paylaşılan salt-okunur artifact** desenini sürdürür.
- **M70 Park Tasarımı** sert zemini (yollar, döşeme, oturma alanı, bank, süs havuzu) üretir; çıktısı
  **`.e4park.json`**'dır. Park, araziyi M60 `.e4arazi.json`'dan, varsa binayı M50 `.e4bina.json`'dan
  **salt-okunur** alır; bu geometrileri değiştirmez (arazi §4.7, bina §4.5).
- Arazi içinde bina varsa park yerleşimi bina ayak izi + yapı yaklaşma (setback) alanını **dışlar**.
- **M71 Bitkilendirme** ağaç/çalı/bitki yerleşimini (yumuşak zemin) yapar; M70 `.e4park.json` + arazi
  `.e4arazi.json`'ı salt-okunur tüketir, çıktısı `.e4peyzaj.json`'dır. Bitki M70'te değil M71'dedir.
- `.e4park.json` ileride disiplinler arası altlık olabilir: M15 (park aydınlatma armatür yerleşimi),
  M05 (park oyun alanı EN 1177 düşme yüzeyi). Tüketici geometriyi değiştirmez.
- Ayrıntılar: [landscape/_index.md](landscape/_index.md), [codex70](landscape/70-park-tasarimi/codex70.md)

### 4.10 Salon Geometrisi (M51 / Ortak Modül)
- **M51 Salon Tasarla** salon/oditoryum geometrisini (zemin, sahne, duvar, tavan, kademeli
  seyirci, opsiyonel balkon/loca) SketchUp benzeri basit 2D editörle üretir; çıktısı M50/M60/M70
  deseniyle **paylaşılan salt-okunur artifact**: **`.e4salon.json`**. Üretici M51'dir.
- **Geometri düzenleme yalnız M51'de** yapılır. Tüketiciler dosyayı salt-okunur yükler ve
  geometriyi değiştirmez (`ISalonGeometri` arayüzü). İlk tüketici **akustik M01**'dir (Salon Yükle);
  ileride elektrik (sahne aydınlatma) ve mekanik (salon HVAC) altlık olarak tüketebilir.
- M01 çıktısı **iki birinci-sınıf yol**: (a) native `.e4salon.json` → M01 "Salon Yükle" köprüsü
  (kategori/hacim/metadata korunur); (b) **DXF** → M01 mevcut DXF import yolundan okunur. Eğriler
  (daire/oval sahne) 3D üretim/DXF anında çokgene fasetlenir.
- Salon katmanının adı (`SalonKatman.Ad`) M01'de `ProjectSurface.Category` olur; malzeme bu
  kategori üzerinden atanır. Hazır şablonlar katmanları otomatik atar.
- Ayrıntılar: [codex51.md](ortak-moduller/51-salon-tasarla/codex51.md)

### 4.9 Standartlar (Başlığa Göre)
| Başlık | Geçerli Standartlar |
|--------|---------------------|
| Akustik M03 | ISO 717-1, Rw, C, Ctr |
| Akustik M04 | ISO 9613-2, Maekawa difraksiyon |
| Akustik M05 | EN 1177, ASTM F1292 |
| Akustik M08 | TS EN 12354-1/-2/-3, Binaların Gürültüye Karşı Korunması Yönetmeliği |
| Elektrik M11 | IEC 60617, IEC 60364, TS HD 60364 |
| Mimari M30-M39 | Planlı Alanlar İmar Yönetmeliği, Binaların Yangından Korunması Hakkında Yönetmelik, Otopark Yönetmeliği, TBDY, erişilebilirlik ve enerji performansı ön kontrolleri |
| Terrain M60-M69 | Average-end-area / prizmatik hacim yöntemleri, Karayolları Teknik Şartnamesi (kazı/dolgu, kabarma katsayıları), İSG kazı/iksa, TBDY geoteknik ön kontrol, Planlı Alanlar İmar Yönetmeliği (tabii zemin/tesviye kotu) |
| Landscape M70-M79 | Planlı Alanlar İmar Yönetmeliği (yeşil/aktif yeşil alan oranı, yapı yaklaşma), Mekânsal Planlar Yapım Yönetmeliği, erişilebilirlik (TS 9111 / TS 12576), çocuk oyun alanı güvenliği (EN 1176/EN 1177), Otopark Yönetmeliği |

---

## 5. Kaynak Kod Haritası (Core)

```
Kst.Acoustics.Core/
├── AbsorptiveSurface/    → M01 yüzey malzeme ataması
├── PanelDesign/          → M02 delikli panel / TMM motoru
├── SoundInsulation/      → M03 ses yalıtımı / Rw hesabı
├── NoiseBarrier/         → M04 ses bariyeri / difraksiyon motoru
├── ImpactSurface/        → M05 darbe emici yüzey / HIC motoru
├── RoomModes/            → M06 oda modları hesabı
├── Measurement/          → M07 yerinde ölçüm / sinyal işleme
├── BuildingAcoustics/    → M08 bina gürültü hesabı / EN 12354
├── Electric/             → M11, M12 elektrik modülleri
├── Architecture/         → M30-M39 mimari BIM domain modeli, ihtiyaç programı, kural motoru
├── Terrain/              → M60-M69 arazi DTM/TIN, hafriyat (yarma/dolgu), temel kota oturtma
├── Landscape/            → M70-M79 park tasarımı (sert zemin), bitkilendirme/peyzaj (yumuşak zemin)
├── Salon/                → M51 salon/oditoryum geometrisi (2D eskiz, .e4salon.json, 2D→3D, şablonlar)
├── Core.Tests/           → UI'siz Core test runner; mimari Core testleri burada da çalışır
├── Geometry/             → Ortak: Point3D, Vector3D, GeometryMath
├── Importing/            → Ortak: DXF parser, ISurfaceImporter
├── Exporting/            → Ortak: DXF export
├── Projects/             → Ortak: proje modeli, kaydet/aç, malzeme kütüphanesi
├── Simulation/           → Ortak: ray tracing, ışın izleme, akustik parametreler
└── Validation/           → Ortak: sızıntı testi (LeakTestService)
```

---

## 6. Yeni Oturum Başlatma Protokolü

1. Bu dosyayı oku (`maincodex.md`)
2. Çalışılacak modülün codex dosyasını oku (bağlantılar §3'te). Mimari başlıkta önce
   `mimari/codex30.md` okunur.
3. İlgili kaynak kod klasörünü incele (`src/Kst.Acoustics.Core/<Modul>/`)
4. Yeni modül açılıyorsa: codex dosyasını ilgili klasöre yaz, §3 tablosunu güncelle
5. Çalışma dizini: `maincodex.md` dosyasını içeren depo kökü
6. Kod yalnızca `EYE4Pro/src/` içine yazılır

---

## 7. Versiyon Notu

| Tarih | Not |
|-------|-----|
| 2026-08-07 | **M01 bütüncül güvenilirlik ve kullanılabilirlik paketi** — belge yaşam döngüsü/revizyonlu kayıt, atomik doğrulamalı `.e4p01` + recovery-aware `.bak`, şema/sonuç doğrulama, DXF/birim/hacim/snap/winding düzeltmeleri, atomik taşma-korumalı ölçek, güncel host tabanlı paneller, simülasyon girdi parmak izi ve yeniden açılabilir sonuç özeti, responsive Türkçe UI. Release build 0 hata/0 uyarı; M01 testleri 17/17, genel özel koşucu 699/699. Ayrıntı: `akustik/01-proje-geometri/codex.md` §8.3. |
| 2026-04-23 | EYE4PRO Acoustics ilk modül (M01) — proje kökü o zaman `KST01/CDX` |
| 2026-05-15 | EYE4PRO Electric ilk modül (M11) başladı |
| 2026-05-19 | M12 Basit Proje Çiz başladı |
| 2026-05-19 | **Tüm dosyalar `eye4pro/` yapısına taşındı — bu tarihten itibaren kök bu dizindir** |
| 2026-06-12 | M50 Bina Geometrisi ortak modül kararı — geometri düzenleme tek merkez, modüller `.e4bina` yükler |
| 2026-06-12 | M30 Mimari BIM ana plan açıldı — M50 entegrasyonu ileri faza ertelendi, ilk geliştirme `.e4mimari.json` üzerinde başlayacak |
| 2026-06-13 | M12 ikiye ayrıldı — M12 kuvvetli akım, M16 zayıf akım proje çiz. Ortak motor iki kabuk; M16 `.e4p16`, fork yok, çapraz görünürlük yok |
| 2026-06-13 | **EYE4PRO Terrain ana başlığı (60–69) açıldı** — M60 Hafriyat Hesapla (yarma/dolgu) + M61 Bina Temeli Kota Oturtma planlandı. Arazi yüzeyi M50 deseniyle paylaşılan salt-okunur `.e4arazi.json` artifact'ı; kod yazılmadı |
| 2026-06-13 | `.e4arazi.json` disiplinler arası ortak altlık kararı — tüketiciler: M61, mimari M30-39, elektrik M15 (park aydınlatma); ileride mekanik + akustik. Tüketici geometriyi değiştirmez (M50 salt-okunurluk deseni) |
| 2026-06-14 | **EYE4PRO Landscape ana başlığı (70–79) açıldı** — M70 Park Tasarımı (sert zemin: yol/döşeme/oturma/bank/süs havuzu) + M71 Bitkilendirme (ağaç/bitki). M70 araziyi `.e4arazi` ve binayı `.e4bina`'dan salt-okunur alır, bina+setback alanını dışlar; çıktısı paylaşılan `.e4park.json`. Bitki M70'te değil M71'de. Yalnız planlama yapıldı, kod yazılmadı |
| 2026-06-14 | M70 poligon boolean kararı — bina+setback dışlama için **Clipper2** (difference/union/offset); `ClipperAdaptor` metre↔integer ölçeği tek noktada izole eder. Setback offset'i elle yazılmaz |
| 2026-06-23 | **M51 Salon Tasarla ortak modülü açıldı** (50-59 bandı) — salon/oditoryum geometrisi (zemin/sahne/duvar/tavan, kademeli seyirci, opsiyonel balkon/loca, hazır şablonlar) SketchUp benzeri 2D editörle üretilir; çıktı paylaşılan salt-okunur `.e4salon.json`. M01'e iki birinci-sınıf yol: native "Salon Yükle" köprüsü + DXF. İlk teslim serbest çizim motoru. Faz 0 tamam (domain + artifact + pencere iskeleti + launcher + 4 test) |
| 2026-07-03 | **M51 profesyonel çizim paketi (Faz 14-17)** — editör CAD çekirdek setine çıkarıldı: çoklu seçim (Shift + window/crossing bandı), Taşı/Kopyala/Aynala/Döndür (`SalonDonusumServisi`), yay çizimi (`SalonCizimTipi.Yay`, 3-nokta), offset (Clipper Tampon + `AcikOffset`), uzat (`SalonTrimServisi.Uzat`), `uzunluk<açı` girişi, polar F10 + dik/merkez snap + F3/F9 grid, ölçüm aracı, tek harf kısayolları. Bilinçli kapsam dışı: hatch/metin/blok/spline/fillet/array. 533 test geçer — codex51 §3.10 |
| 2026-07-02 | **M51 duvar açıklıkları (Faz 13)** — kapı/pencere tanımı: konum-tabanlı duvar bağı (trim'e dayanıklı), duvardan Clipper ile delik + açıklık kategorisiyle dolgu (kabuk sızdırmaz; M01'de "Kapı"/"Pencere" ayrı malzeme kategorisi); 🚪 canvas aracı; varsayılan katman 11. M51 açıklıkları kendi içinde tanımlar (maincodex §4.5 M50 kuralı bina geometrisi içindir; salon M51'in kendi artifact'ıdır). 528 test geçer — codex51 §3.9 |
| 2026-07-02 | **M51 P2 devam (Faz 12)** — seyirci/balkon 3D yüzeyleri kabuğa kırpılır (taşan yerleşim hayalet yüzey üretmez); **orkestra çukuru** (zemin açıklığı + taban + duvarlar, "Orkestra" kategorisi, hacim düzeltmesi); faset kenar sayısı kullanıcı ayarı (12–128); sahne kulesi = sahne cebi + tavan profili reçetesi. 525 test geçer — codex51 §3.8 |
| 2026-07-02 | **M51 P2 akustik özellik paketi (Faz 11)** — eğik/parçalı **tavan kesit profili** (Y→kot kırılımları; bantlı düzlemsel paneller, duvar üstleri profili izler, hacim ∫z(y)dA); koltuk kapasitesi (0,5 m²/koltuk + m³/koltuk); `ISalonGeometri` tam tüketici sözleşmesi (ISahne/ISeyirci/IBalkon/ITavanKirilim); seyirci/balkon "Seçiliyi Güncelle" + sahne/seyirci taban kotu (balkon üstü seyirci). 521 test geçer — codex51 §3.7 |
| 2026-07-02 | **M51 P1 editör güvenilirliği (Faz 10)** — undo/redo (JSON snapshot, Ctrl+Z/Y/S + toolbar), IsDirty + başlıkta `*` + temizken sormadan kapanma (§4.2 uyumu), atomik kaydet (tmp+move), vertex self-snap düzeltmesi, trim kapalılık uyarısı, `DxfExportService` fan→kulak-kesme üçgenleme (konkav zeminler; `RayTracingService.TriangulatePolygon` public), Sığdır platform footprint'lerini kapsar. 514 test geçer — codex51 Faz 10 |
| 2026-07-02 | **M51 P0 güvenilirlik paketi (Faz 9)** — RoomVolume bbox→gerçek plan hacmi; kabuk üretimi katman tipine bağlandı (çift tavan/hayalet duvar kapandı); zemine oturan platform footprint'leri Clipper2 ile zeminden kırpılır (keyhole); kategoriler katmandan türetilir; şablon seyircisi plan hattına sığdırıldı + "salon önü" sahneye proscenium cebi; `SalonDogrulamaServisi` + "✔ Kontrol" (LeakTest dahil); `AcousticsProject.KaynakGeometriRevizyon` ile M01 salon revizyon uyarısı. `ClipperAdaptor`'a `Kesisim` + çoklu-özne `Fark` eklendi. 511 test geçer — codex51 §3.6 |
| 2026-07-14 | **M08 Faz 1 veri güvenliği paketi tamamlandı** — kapsamlı UI/iş akışı incelemesi sonrası: `OdaTespitServisi` artık Menfez/Junction'ı da (yalnız YapiElemani/Acikliklar değil) rebuild'de koruyor (kök neden — her ekran yenilemesi + kaydetmede sessizce siliniyordu); "Duvar Kontrolü" butonu aynı güvenli yolu kullanacak şekilde düzeltildi; `IsDirty` + kapanış/Yeni Proje/Proje Aç onayı eklendi; `KaydetYola` atomik (.tmp+Move); `BinaProjeMigrator` + `ProjeAc` artık `Proje = yuklenen` (Id/Adres/OlusturmaTarihi/YapiElemaniKutuphanesi/AkustikNoktalar/DisGurultuKaynaklari kaybı giderildi). 544 test geçer — codex8.md §2b, §10 |
| 2026-07-17 | **M50 güvenilirlik paketi 2 (13 bulgu düzeltildi)** — DXF yeniden yükleme Id kararlılığı (`OdaEslestirmeServisi`, merkez-örtüşme) + açıklık koruma; Yeni/Aç IsDirty onayı; kat listesi üst/alt konvansiyonu görsel etiketi; açıklık yükseklik varsayılanları M12/M08 köprülerinde hizalandı; `AciklikTara` tam segment setini dosyadan yeniden okur; `Kaydet` revizyonu yalnız başarılı yazımda kalıcılaşır; başarısız patlatma/geri-al sahte undo/IsDirty üretmez; undo/redo silinmiş kata ait adımı atlar; iki ayrı düşey birleşim patlatmayla buluşunca birleştirilir; `AyniBirlesimdeAcik` bayrak denetler; mimari adapter güncel SemaSurum'la üretir; MTEXT ölçek tahmininde okunur — codex50 §8 |
| 2026-07-19 | **M12 mahal listesi üretkenlik paketi (M1-M7)** — "mahal listesi kullanışsız" denetimi sonrası 4 fazlı plan: yeni `MahalListesiServisi` (Core) — kattan kopyalama (oda adı eşlemeli, elle açılan hücre ezilmez), oda adından akıllı doldurma (8 oda-tipi öneri seti: yatak/salon/mutfak/banyo/hol/ofis/depo/balkon), adlandırılmış mahal şablonu kaydet/yükle (`mahal-sablonlari.json`, atomik yazım). Matris ergonomisi: satır/sütun çift-tık toplu aç-kapat, sürükle-boya, klavye gezinme (ok/Space/rakam/Delete), oda arama+sıralama. Öngörü: Grid/Duvar hücrelerinde ✓ yerine gerçek aday üreticiden `~N` tahmini, kat toplam güç bandı, canvas↔mahal listesi çift yönlü oda vurgusu. "🏢 Tüm Katlar" tek komutla tüm katları yerleştirir. Kapsam notu: plandaki "ana pencereye gömülü dock panel" yüksek regresyon riski nedeniyle YAPILMADI — yerine düşük riskli eşdeğer çözüm (pencere tek-örnek + `Activate()`, zaten non-modal + çift yönlü vurgu). 625 test geçer (14 yeni regresyon) — codex12 §20 |
| 2026-08-07 | **M12 güvenilirlik + hesap doğruluğu paketi** — özellik paneli kilit/undo/IsDirty hattına alındı; çok katlı pano silme referans temizliği + bileşik undo; M12→M50 kaynak kimlikli güvenli migrasyon; elektrik verili eski kat koruma + kat-seviyesi mutabakat; doğrudan priz pano yükü; graf-kök tabanlı Tek Hat + BK çift sayım engeli; mahal satırı/grup kimliği; pafta-kapsamlı Tamamını Sil; yetim buat-pano temizliği ve doğru rota sonucu mesajı. Tam koşucu 709/709, Core 121/121, çözüm 0 hata/uyarı — codex12 §21 |
| 2026-07-19 | **M12 kablo modu UX paketi (K1-K12)** — kablolama akışı yeniden tasarlandı: canlı rota çizimi (atama tıklamasında kablo anında görünür; bağlanmamış "Hesapla" butonu sorunu kökten kalktı), Esc iptal/Enter+sağ tık bitir, aktif mod vurgusu + adım göstergesi (①→②→③), ikinci tık devreden çıkarır, hover ön-vurgusu (turkuaz halka), Shift+bant toplu atama, aday armatür vurgusu + "Odadakileri Ata", kablo yakalama toleransı 0.8 m, akıllı tıklama (buat→pano akışı buata tıklayınca başlar), devre listesi paneli (vurgula/sil), duvar graf önbelleği (`DuvarAgindaRota` taban grafı oda-kümesi anahtarlı; O(E²) köprü kurulumu sorgu başına tekrarlanmaz). 611 test geçer — codex12 §19 |
| 2026-08-07 | **M11/M14 güvenilirlik ve UX paketi** — M11 ile M14'e gerçek değişiklik parmak izi, Yeni/Aç/Geri-Modüller/X için koşullu Kaydet/Kaydetmeden Devam/İptal akışı, atomik proje kaydı + `.bak` yedeği ve Ctrl/Cmd kaydetme kısayolları eklendi. M14 çift uzantı ve iki taraflı Poz Atama araması; M11/M14 dar ekran araç çubukları; launcher durum sayıları ve eski Avalonia `Watermark` kullanımları düzeltildi. Çözüm 0 hata/0 uyarı, özel koşucu 668/668 — codex11 §11/§15, codex14 §7/§12 |
| 2026-07-18 | **M12 güvenilirlik + UX paketi (17 düzeltme)** — kapsamlı mahal listesi + otomatik kablolama denetimi: IsDirty/kapanış onayı (§4.2 borcu), KuvvetliTipler eksik tipleri, hücre tıklamada adet koruması, undo kapsamı (mahal listesi/kablo tavası/pano besleme/topraklama + `TaliPanoHedefPanoId`), aydınlatma buat sayımı pafta filtresi, priz çift devre koruması, "Açıklık Yanı" kapıya-yakın seçim, SadecaEksikleri VM↔pencere senkronu, boş rotada kablo üretmeme + uyarı paneli, kapasite muhasebesi (`AnahtarMevcutYuk`), bayat rota bandı, taşınan otomatik elemanın korunması, priz zincir `GeometrikSirala`, satır toplam sütunu. 609 test geçer (8 yeni regresyon) — codex12 §18 |
| 2026-07-17 | **M50 §9 Çoklu Kat Tek DXF tamamlandı** — yan yana çizilmiş kat planları (yaygın CAD pratiği) tek DXF'ten otomatik tespit edilip katlara dağıtılır: `KatKumelemeServisi` (grid+BFS mekânsal kümeleme) + `KatEtiketServisi` (ad/sıra tahmini — bodrum/zemin/asma/normal/çatı/tip-kat regex'leri) + `DxfKatYukleyici.CokluKatTespitEt/CokluKatUret` + `CokluKatDagitimWindow` onay diyaloğu. Tek plan içeren DXF'lerde davranış değişmez. **Test koşucusu düzeltmesi:** proje testleri `dotnet test` değil `dotnet run --project Kst.Acoustics.Tests` ile çalışıyor (Program.cs dizi tabanlı özel koşucu, xUnit yalnız 50 ayrı Fact içeriyor) — 597 test geçer; Core.Tests (`dotnet run --project Kst.Acoustics.Core.Tests`): 121 test geçer — codex50.md §9 |
