# EYE4PRO — BİRLEŞİK CODEX (Telefon / Sohbet Sürümü)

> Bu dosya tüm codex ve index dosyalarının tek belgede birleştirilmiş halidir.
> Üretim tarihi: 2026-06-14
> Amaç: Claude mobil uygulamasına yükleyip masaüstü dışında planlama yapmak.
> Telefonda alınan kararlar bu dosyaya YAZILMAZ; masaya dönünce ilgili codex'e işlenir.

---

## İçindekiler

1. `maincodex.md`
2. `akustik/_index.md`
3. `akustik/01-proje-geometri/codex.md`
4. `akustik/02-delikli-panel/codex2.md`
5. `akustik/03-ses-yalitimi/codex3.md`
6. `akustik/04-ses-bariyeri/codex4.md`
7. `akustik/05-darbe-yuzey/codex5.md`
8. `akustik/06-oda-modlari/codex6.md`
9. `akustik/07-yerinde-olcum/codex7.md`
10. `akustik/08-bina-gurultu/codex8.md`
11. `elektrik/_index.md`
12. `elektrik/11-tek-hat-semasi/codex11.md`
13. `elektrik/12-basit-proje-ciz/codex12.md`
14. `elektrik/13-zayif-akim-tek-hat/codex13.md`
15. `elektrik/14-kesif-hazirla/codex14.md`
16. `elektrik/15-cevre-aydinlatma/codex15.md`
17. `elektrik/16-zayif-akim-proje-ciz/codex16.md`
18. `mekanik/_index.md`
19. `mekanik/21-kanal-hesabi/codex21.md`
20. `mimari/_index.md`
21. `mimari/codex30.md`
22. `mimari/mimari-modul-planlama-raporu.md`
23. `mimari/31-ihtiyac-programi-mahal-sablonlari/codex31.md`
24. `mimari/32-konsept-plan-parametrik-yerlesim/codex32.md`
25. `mimari/33-dusey-cekirdek-saft-sirkulasyon/codex33.md`
26. `mimari/34-yonetmelik-on-kontrol/codex34.md`
27. `mimari/35-3d-bim-model/codex35.md`
28. `mimari/36-detay-malzeme-yapi-elemani/codex36.md`
29. `mimari/37-pafta-rapor-cizelgeler/codex37.md`
30. `mimari/39-bim-koordinasyon-ihracat/codex39.md`
31. `terrain/_index.md`
32. `terrain/60-hafriyat-hesapla/codex60.md`
33. `terrain/61-bina-temeli-kota-oturtma/codex61.md`
34. `ortak-moduller/50-bina-geometri/codex50.md`
35. `insaat/_index.md`

---


<!-- ============================================================ -->
# 📄 DOSYA: `maincodex.md`
<!-- ============================================================ -->

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
| Proje Dosyası | `.eye4proj.json` (JSON tabanlı) |
| Solution | `eye4pro/src/Kst.Acoustics.slnx` |

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
├── ortak-moduller/                     ← Ortak Modüller (50–59)
│   └── 50-bina-geometri/codex50.md
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
| 13 | Zayıf Akım Tek Hat Şeması | Başlatıldı | [codex13.md](elektrik/13-zayif-akim-tek-hat/codex13.md) |
| 14 | Keşif Hazırla | Planlama bekliyor | [codex14.md](elektrik/14-kesif-hazirla/codex14.md) |
| 15 | Çevre Aydınlatma Projesi Hazırla | Planlama | [codex15.md](elektrik/15-cevre-aydinlatma/codex15.md) |
| 16 | Zayıf Akım Proje Çiz | Geliştiriliyor | [codex16.md](elektrik/16-zayif-akim-proje-ciz/codex16.md) |

### Mekanik

| # | Modül Adı | Durum | Codex |
|---|-----------|-------|-------|
| 21 | Kanal Hesabı | Başlatıldı | [codex21.md](mekanik/21-kanal-hesabi/codex21.md) |

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
| 61 | Bina Temeli Kota Oturtma | Geliştiriliyor (F0-F2 Core; .e4bina köprülü) | [codex61.md](terrain/61-bina-temeli-kota-oturtma/codex61.md) |

### Ortak Modüller

| # | Modül Adı | Durum | Codex |
|---|-----------|-------|-------|
| 50 | Bina Geometrisi | Aktif | [codex50.md](ortak-moduller/50-bina-geometri/codex50.md) |

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
- Format: JSON, uzantı `.eye4proj.json`.
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

### 4.8 Standartlar (Başlığa Göre)
| Başlık | Geçerli Standartlar |
|--------|---------------------|
| Akustik M03 | ISO 717-1, Rw, C, Ctr |
| Akustik M04 | ISO 9613-2, Maekawa difraksiyon |
| Akustik M05 | EN 1177, ASTM F1292 |
| Akustik M08 | TS EN 12354-1/-2/-3, Binaların Gürültüye Karşı Korunması Yönetmeliği |
| Elektrik M11 | IEC 60617, IEC 60364, TS HD 60364 |
| Mimari M30-M39 | Planlı Alanlar İmar Yönetmeliği, Binaların Yangından Korunması Hakkında Yönetmelik, Otopark Yönetmeliği, TBDY, erişilebilirlik ve enerji performansı ön kontrolleri |
| Terrain M60-M69 | Average-end-area / prizmatik hacim yöntemleri, Karayolları Teknik Şartnamesi (kazı/dolgu, kabarma katsayıları), İSG kazı/iksa, TBDY geoteknik ön kontrol, Planlı Alanlar İmar Yönetmeliği (tabii zemin/tesviye kotu) |

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
6. Kod yalnızca `eye4pro/src/` içine yazılır

---

## 7. Versiyon Notu

| Tarih | Not |
|-------|-----|
| 2026-04-23 | EYE4PRO Acoustics ilk modül (M01) — proje kökü o zaman `KST01/CDX` |
| 2026-05-15 | EYE4PRO Electric ilk modül (M11) başladı |
| 2026-05-19 | M12 Basit Proje Çiz başladı |
| 2026-05-19 | **Tüm dosyalar `eye4pro/` yapısına taşındı — bu tarihten itibaren kök bu dizindir** |
| 2026-06-12 | M50 Bina Geometrisi ortak modül kararı — geometri düzenleme tek merkez, modüller `.e4bina` yükler |
| 2026-06-12 | M30 Mimari BIM ana plan açıldı — M50 entegrasyonu ileri faza ertelendi, ilk geliştirme `.e4mimari.json` üzerinde başlayacak |
| 2026-06-13 | M12 ikiye ayrıldı — M12 kuvvetli akım, M16 zayıf akım proje çiz. Ortak motor iki kabuk; M16 `.e4p16`, fork yok, çapraz görünürlük yok |
| 2026-06-13 | **EYE4PRO Terrain ana başlığı (60–69) açıldı** — M60 Hafriyat Hesapla (yarma/dolgu) + M61 Bina Temeli Kota Oturtma planlandı. Arazi yüzeyi M50 deseniyle paylaşılan salt-okunur `.e4arazi.json` artifact'ı; kod yazılmadı |
| 2026-06-13 | `.e4arazi.json` disiplinler arası ortak altlık kararı — tüketiciler: M61, mimari M30-39, elektrik M15 (park aydınlatma); ileride mekanik + akustik. Tüketici geometriyi değiştirmez (M50 salt-okunurluk deseni) |


<!-- ============================================================ -->
# 📄 DOSYA: `akustik/_index.md`
<!-- ============================================================ -->

# Akustik — Ana Başlık 1

**Modül aralığı:** 01–09  
**Durum:** 8 modül aktif

## Modüller

| # | Klasör | Modül Adı | Durum |
|---|--------|-----------|-------|
| 01 | [01-proje-geometri](01-proje-geometri/codex.md) | Proje Yönetimi & Geometri Import | Aktif |
| 02 | [02-delikli-panel](02-delikli-panel/codex2.md) | Delikli Panel Tasarımı | Aktif |
| 03 | [03-ses-yalitimi](03-ses-yalitimi/codex3.md) | Ses Yalıtımı & Duvar Paneli Tasarımı | Aktif |
| 04 | [04-ses-bariyeri](04-ses-bariyeri/codex4.md) | Ses Bariyeri & Çevresel Gürültü Simülasyonu | Aktif |
| 05 | [05-darbe-yuzey](05-darbe-yuzey/codex5.md) | Darbe Emici Yüzey (Impact Surface) | Aktif |
| 06 | [06-oda-modlari](06-oda-modlari/codex6.md) | Oda Modları Hesabı | Aktif |
| 07 | [07-yerinde-olcum](07-yerinde-olcum/codex7.md) | Yerinde Ölçüm | Aktif |
| 08 | [08-bina-gurultu](08-bina-gurultu/codex8.md) | Bina Gürültü Hesabı (TS EN 12354) | Aktif |
| 09 | — | _rezerve_ | — |

## Kaynak Kod

`../src/Kst.Acoustics.Core/` — tüm akustik modüllerin iş mantığı  
`../src/Kst.Acoustics.App/BuildingAcoustics/` — UI katmanı


<!-- ============================================================ -->
# 📄 DOSYA: `akustik/01-proje-geometri/codex.md`
<!-- ============================================================ -->

# M01 Codex — Proje Yönetimi ve Geometri Import

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam ve dikkat edilmesi gereken teknik notlar yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M01 — Proje Yönetimi ve Geometri Import |
| Stack | C# + Avalonia (`net10.0`) |
| Solution | `src/Kst.Acoustics.slnx` |
| UI dili | Türkçe |

---

## 2. Modül Kapsamı

M01, akustik proje geometrisinin programa alınması ve proje dosyası olarak yönetilmesini sağlar.

Aktif kapsam:
- DXF geometri içe aktarma
- Yüzey/layer/kategori yönetimi
- 2B/3B geometri önizleme
- Kaynak ve alıcı noktalarının yönetimi
- Proje kaydetme/açma
- Malzeme atama ve proje penceresine geçiş
- Sızdırmazlık/leak test
- Akustik panel yerleşim eklentisi

Kapsam dışı / eski karar:
- DXF dışı geometri formatları aktif değildir.
- Eski nokta tablosu, özel metin veya deneysel importer yolları ürün kapsamından çıkarılmıştır.

---

## 3. Geometri Import Kuralları

- Dış geometri içe aktarma yalnızca DXF üzerinden yapılır.
- Desteklenen DXF varlıkları:
  - `3DFACE`
  - kapalı `POLYLINE`
  - kapalı `LWPOLYLINE`
- `$INSUNITS` okunur; yoksa kullanıcıdan birim seçimi istenir.
- DXF layer adları yüzey kategori/grup kaynağıdır.
- 4'ten fazla köşeli yüzeyler geçerlidir.
- Malzeme isimleri import sırasında zorunlu değildir; uygulama içinde atanır.
- Yüzeyler import sonrası kategori değişimi, görünürlük, filtreleme ve seçim işlemlerini destekler.

---

## 4. Proje Dosyası

- Aktif uzantı: `.e4p01`
- İçerik formatı: JSON
- Eski `.eye4proj.json` bilgisi tarihsel kabul edilir; yeni kayıt/açma akışı `.e4p01` kullanır.
- Eksik JSON alanları geriye dönük uyumluluk için boş liste/null olarak karşılanmalıdır.
- Kayıt durumu `IsDirty` ile izlenir; kaydedilmemiş değişiklikler başlıkta `*` ile gösterilir.

---

## 5. UI Kuralları

- Tüm görünür etiket, mesaj ve hata metinleri Türkçe yazılır.
- Türkçe karakterler doğru kullanılmalıdır: `ö`, `ü`, `ç`, `ş`, `ı`, `İ`, `ğ`.
- Büyük dosya importu veya ağır hesaplama UI thread'ini kilitlememelidir.
- Sabit yükseklikten kaçınılır; pencere büyüdükçe içerik alanı da büyümelidir.
- Karmaşık ViewModel dosyaları partial class'lara bölünür.

---

## 6. Mimari Notlar

- İş mantığı `Kst.Acoustics.Core` içinde kalır.
- UI katmanı `Kst.Acoustics.App` içindedir ve Core'a bağımlıdır.
- Core, UI projesine bağımlı olmamalıdır.
- Büyük N işlemlerde O(N²) arama yerine `Dictionary`/indeks yapıları tercih edilir.
- Simülasyon ve import gibi pahalı işler `async/await` ve gerektiğinde `Task.Run` ile çalıştırılır.

---

## 7. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Importing/` — DXF parser ve import servisleri
- `src/Kst.Acoustics.Core/Projects/` — proje modeli, dosya servisi, malzeme bilgileri
- `src/Kst.Acoustics.Core/Geometry/` — ortak geometri tipleri ve hesapları
- `src/Kst.Acoustics.Core/Validation/` — sızdırmazlık testi
- `src/Kst.Acoustics.Core/Simulation/` — ışın izleme ve akustik simülasyon

App:
- `src/Kst.Acoustics.App/MainWindow*` — ana import/proje başlangıç ekranı
- `src/Kst.Acoustics.App/ProjectWindow*` — proje çalışma ekranı
- `src/Kst.Acoustics.App/Controls/GeometryPreviewControl.cs` — geometri görünümü
- `src/Kst.Acoustics.App/ViewModels/` — ana ViewModel ve partial dosyalar

Test:
- `src/Kst.Acoustics.Tests/`

---

## 8. Güncel Özellik Notları

- DXF import doğrudan masaüstü uygulaması içinden yapılır.
- Geometri önizleme zoom, pan, seçim, kategori filtreleme ve görünürlük kontrolü destekler.
- Kaynaklar ve alıcılar tablo üzerinden düzenlenebilir; viewport üzerinden yerleştirme ve yüzeye snap vardır.
- ProjectWindow içinde kategori ağacı, malzeme atama, simülasyon, sonuçlar, ışık izleme ve panel yerleşimi bulunur.
- Panel yerleşiminde panel konfigürasyonu kaynak veri kabul edilir; `ProjectSurface` çizim/hesap yüzeyidir.
- Simülasyon `_workingSurfaces` üzerinden çalışır; döndürülmüş/silinmiş/üretilmiş yüzeyler hesaba katılır.
- Sızdırmazlık testi Fibonacci küresiyle 12 yön ve yüksek yansıma sayısı kullanır.

---

## 9. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel durum:
- Build hedefi: 0 hata
- Testler: mevcut test paketi geçmeli

---

## 10. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. İş yapılacak alana göre ilgili Core/App dosyalarını incele.
4. Değişiklikleri mümkün olduğunca dar kapsamlı tut.
5. Görünür UI metinlerinde Türkçe karakter kontrolü yap.
6. Uygunsa build ve ilgili testleri çalıştır.


<!-- ============================================================ -->
# 📄 DOSYA: `akustik/02-delikli-panel/codex2.md`
<!-- ============================================================ -->

# M02 Codex — Delikli Panel Tasarımı

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M02 — Delikli Panel Tasarımı |
| Amaç | Çok katmanlı panel emilim hesabı ve materyal kütüphanesine aktarım |
| Yöntem | Transfer Matrix Method (TMM) |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Sınırsız/çok katmanlı akustik panel hesabı
- Delikli, mikro-delikli, yivli, hava boşluğu, gözenekli malzeme ve kumaş katmanları
- Normal geliş ve rastgele geliş emilim hesapları
- NRC, `αw`, soğurma sınıfı ve Helmholtz rezonans özeti
- Parametrik delik düzeni ve canlı grafik
- Özel DXF panel deseni içe aktarma
- Panel kesiti ve performans raporu dışa aktarımı
- Tasarlanan panelin `MaterialDatabase` sistemine kaydedilmesi

Kapsam dışı / model sınırı:
- Özel DXF desenleri tam dalga/FEA olarak çözülmez.
- Özel açıklıklar porozite, hidrolik çap dağılımı ve paralel akustik admitans yaklaşımına indirgenir.
- Çok dar boğazlı slot, voronoi veya aşırı heterojen açıklık dağılımlarında sonuç mühendislik yaklaşımıdır; kalite uyarıları dikkate alınmalıdır.

---

## 3. Akustik Hesap Kuralları

- Ana hesap motoru `System.Numerics.Complex` tabanlı TMM'dir.
- Gözenekli katman modelleri:
  - Delany-Bazley-Macdonald
  - Miki
- Delikli panel empedansı Maa tipi viskoz tüp direnci, kenar direnci ve Fok açık alan düzeltmesiyle hesaplanır.
- Delik düzenleri:
  - Düz
  - Şaşırtmalı
- Delik düzenine göre Fok düzeltmesi ayrı katsayılarla uygulanır.
- NRC yalnızca 250, 500, 1000 ve 2000 Hz değerlerinin ortalamasından hesaplanır ve 0.05'e yuvarlanır.
- `αw` ISO 11654 referans eğrisiyle hesaplanır; NRC yerine geçmez.
- Oda akustiği materyal kaydı için oktav bant değerleri rastgele geliş tahmininden türetilir.
- Grafik normal/rastgele geliş seçimine göre gösterilir.

---

## 4. Katman ve Parametre Kuralları

- Katmanlar sırayla panel yüzünden arka sınıra doğru değerlendirilir.
- Her katmanda `Hesaba kat` anahtarı vardır; kapalı katman hesap, grafik, rapor ve materyal kaydından çıkarılır.
- SoundTex tipi kumaş ince dirençli levha olarak modellenir.
- Parametre sınırları güvenli tutulmalıdır:
  - Boşluk oranı `0 < p <= 0.99`
  - Hava boşluğu kalınlığı en az 1 mm
  - Negatif kalınlık, çap veya akış direnci kabul edilmez
- Kullanıcı manuel delik çapı/boşluk/aks aralığı değiştirirse özel DXF açıklık dağılımı temizlenir.
- UI parametre değişiklikleri debounce ile hesaplanmalıdır; sürükleme sırasında UI donmamalıdır.

---

## 5. DXF Panel Import Kuralları

- Özel desen importu DXF üzerinden yapılır.
- Parser kapalı geometriyi analiz eder; açık rehber çizgiler alan hesabına dahil edilmez.
- Desteklenen/çizilebilen varlıklar:
  - `CIRCLE`
  - `LWPOLYLINE`
  - `POLYLINE`
  - `3DFACE`
  - `ARC`
  - `ELLIPSE`
  - `SPLINE`
- En büyük kapalı şekil panel sınırı kabul edilir.
- Daha küçük kapalı şekiller açıklık/delik olarak değerlendirilir.
- İç içe poligonlar panel/delik/ada hiyerarşisiyle hesaplanır.
- LWPOLYLINE bulge/yay bilgisi tessellation ile alan hesabına katılır.
- `$INSUNITS` algılanamazsa kullanıcıdan birim onayı alınır.
- Özel açıklıklar için her açıklığın alanı, çevresi, hidrolik çapı, daireselliği ve aspect ratio değeri üretilir.
- DXF kalite analizi uyarıları rapora ve UI'a yansıtılır.

---

## 6. Rapor ve Entegrasyon

- Panel sonuçları `MaterialDatabase` sistemine kaydedilebilir.
- Materyal kaydında oktav bant soğurma değerleri ve tahmini saçılma katsayısı üretilir.
- Rapor formatları:
  - `.txt`
  - `.md`
  - `.html`
- Raporlarda hesap güveni, varsayımlar, katman listesi, geometri özeti ve emilim tablosu bulunmalıdır.
- DXF şablonu AutoCAD R12 ASCII DXF formatında üretilir; kullanıcı `.dwg` adı verirse çıktı `.dxf` uzantısına çevrilir.

---

## 7. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/PanelDesign/` — TMM, katman modelleri, panel DXF analizi
- `src/Kst.Acoustics.Core/Projects/MaterialDatabase.cs` — materyal kütüphanesi entegrasyonu

App:
- `src/Kst.Acoustics.App/PanelDesignWindow*` — panel tasarım penceresi
- `src/Kst.Acoustics.App/ViewModels/PanelDesignViewModel*` — panel tasarım ViewModel dosyaları
- `src/Kst.Acoustics.App/Controls/AbsorptionPlotControl.cs` — emilim grafiği
- `src/Kst.Acoustics.App/Controls/PanelPreviewControl.cs` — panel/desen önizleme

Test:
- `src/Kst.Acoustics.Tests/` — panel hesap, DXF parser ve regresyon testleri

---

## 8. Güncel Özellik Notları

- Parametrik panel tasarımında delik çapı, aks aralığı, boşluk oranı ve delik düzeni karşılıklı güncellenir.
- Panel önizlemesi düz ve şaşırtmalı delik dizilimini gösterebilir.
- Özel DXF importu hidrolik çap dağılımıyla hesaplanır; tek eşdeğer çap yaklaşımı yalnızca uygun durumlarda referans davranış olarak kalır.
- Kalite paketi yüksek/düşük porozite, küçük hidrolik çap, geniş açıklık dağılımı, yüksek slot aspect ratio, düşük dairesellik, belirsiz birim ve açık rehber entity durumlarını işaretler.
- Helmholtz benchmark ve DXF parser davranışları regresyon testleriyle korunmalıdır.

---

## 9. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- Panel regresyon testleri geçmeli

---

## 10. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Panel hesabı için önce `Core/PanelDesign` dosyalarını incele.
4. UI değişikliği gerekiyorsa `PanelDesignWindow` ve ilgili ViewModel dosyalarını kontrol et.
5. Formülasyon değişikliği yapıldıysa regresyon testi ekle veya mevcut panel testlerini çalıştır.
6. Görünür UI metinlerinde Türkçe karakter kontrolü yap.


<!-- ============================================================ -->
# 📄 DOSYA: `akustik/03-ses-yalitimi/codex3.md`
<!-- ============================================================ -->

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


<!-- ============================================================ -->
# 📄 DOSYA: `akustik/04-ses-bariyeri/codex4.md`
<!-- ============================================================ -->

# M04 Codex — Ses Bariyeri ve Çevresel Gürültü

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları, kritik sınırlamalar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M04 — Ses Bariyeri ve Çevresel Gürültü |
| Amaç | Bina cephesinde bariyer öncesi/sonrası dış gürültü seviyelerini hesaplamak ve görselleştirmek |
| Referans yaklaşım | ISO 9613-2 eğilimleri, Maekawa, Kurze-Anderson |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Tamamlandı; gelişmiş modelleme ve bakım sürüyor |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Dikdörtgen prizma bina modeli
- Bina ön cephesinde otomatik alıcı grid
- Noktasal, çok noktalı, çizgisel ve trafik kaynakları
- Tek veya çoklu düz bariyer senaryosu
- Bariyer yüksekliği, uzunluğu, kalınlığı, mesafesi, X kaydırması ve etkinlik durumu
- Bariyer malzeme kütüphanesi ve manuel/Excel malzeme girişi
- 8 oktav bant hesabı: `63, 125, 250, 500, 1000, 2000, 4000, 8000 Hz`
- Bariyersiz, bariyerli ve insertion loss sonuçları
- `dB(A)` geniş bant sonuç
- Cephe renk haritası, alıcı sonuç tablosu ve frekans tablosu
- Orbit kamera, pan ve zoom destekli yazılım tabanlı 3B/izometrik önizleme
- Basit PDF raporu ve Excel/CSV benzeri malzeme veri akışları

Kapsam dışı / ileri faz:
- Resmi sertifikasyon veya ÇED hesabı
- Tam CNOSSOS/FHWA/NMPB resmi trafik modeli
- BEM/FEM veya tam dalga çözücü
- Topografya, meteoroloji sınıfları, rüzgar ve sıcaklık gradyanı
- Çok cepheli kentsel kanyon için tam ray tracing
- Eski tek-bariyerli `.kstnb` dosyaları için otomatik migration

---

## 3. Koordinat ve Geometri Kuralları

- `X`: bina cephesi boyunca yatay eksen
- `Y`: bina önünden kaynak yönüne mesafe ekseni
- `Z`: yükseklik ekseni
- Bina ön cephesi `Y = 0` düzlemindedir.
- Bariyer bina önünde negatif `Y` tarafındadır.
- Gürültü kaynakları bariyerin daha önünde, negatif `Y` tarafında yer alır.
- Alıcılar bina ön cephesinde küçük cephe ofsetiyle konumlanır.
- Alıcı grid tek satır/sütun durumunda merkezlenmelidir.
- Kaynak, bariyer ve bina sıralaması doğrulanmalı; hatalı geometri uyarı üretmelidir.

---

## 4. Hesap Kuralları

Temel seviye yaklaşımı:
- `Lp(f) = Lw(f) + Dc(f) - A(f)`
- `A(f) = Adiv + Aatm + Agr + Abar + Amisc`

Aktif bileşenler:
- Geometrik yayılım
- Atmosferik absorpsiyon
- Basitleştirilmiş zemin etkisi / ayar bağlı düzeltme
- Bariyer kırınımı
- Bariyer malzeme iletim yolu
- Sınırlı absorptif yüz etkisi
- Sınırlı cephe-bariyer yansıma cezası

Genel kurallar:
- Spektrum tüm oktav bantlarda hesapta kullanılmalıdır.
- `OverallLw`, kaynak spektrumunu enerji bazlı hedef toplam seviyeye normalize eder.
- Çoklu kaynaklar aritmetik değil enerji toplamıyla birleşir.
- A-ağırlıklama bant sonuçlarından enerji toplamıyla üretilir.
- Bariyer zayıflatması negatif değer üretmemelidir.
- Sayısal sonuçlarda `NaN`/`Infinity` güvenli şekilde engellenmelidir.

---

## 5. Bariyer Kırınım ve İletim Kuralları

- Kırınım hesabı üst, sol ve sağ kenar yollarını değerlendirir.
- Yol farkı:
  `delta = d1 + d2 - d0`
- Fresnel sayısı:
  `N = 2 * delta / lambda`
- Maekawa ve Kurze-Anderson yardımcı formülleri ortak `NoiseBarrierDiffraction` alanında tutulur.
- Sonlu uzunluk etkisi yan kenar kaçaklarıyla hesaba katılır.
- Bariyer malzemesinin `TL/R(f)` değeri düşükse toplam performans malzeme iletim yolu tarafından sınırlanır.
- Diffracted ve transmitted enerji yolları enerji bazlı birleştirilir.
- Absorptif yüzler tam yansıma modeli değildir; kırınım/kaçak enerjisi ve yansıma cezası üzerinde sınırlı katkı olarak modellenir.

---

## 6. Çoklu Bariyer Kuralları

- Projede `List<BarrierModel> Barriers` kullanılır; tekil `Barrier` artık aktif model değildir.
- Her bariyerin adı, etkinlik durumu ve malzemesi vardır.
- Son bariyer silinemez.
- Her aktif bariyer bağımsız hesaplanır.
- Alıcı/frekans bazında birincil bariyer, en yüksek `Abar` veren bariyerdir.
- Çoklu bariyer kombinasyonu güncel olarak `Math.Max` ile yapılır.
- Bu yaklaşım paralel/seri bariyerlerde tutucu mühendislik önizlemesidir; ISO 9613-2 toplam-delta seri kombinasyonu ayrı geliştirme konusudur.
- Önizleme şimdilik örnek kırınım yolunu kaynağa en yakın bariyer üzerinden çizer.

---

## 7. Trafik ve Çizgisel Kaynak Kuralları

- Çizgisel kaynaklar segmentlere bölünür; her segment katkısı enerji toplamıyla birleştirilir.
- Trafik kaynakları debi, ortalama hız ve ağır taşıt oranından oktav spektrumu üretir.
- Trafik için seçilebilir önizleme profilleri:
  - CNOSSOS-EU benzeri
  - FHWA TNM benzeri
  - NMPB Routes benzeri
- Bu profiller resmi standart tabloları değildir; davranış trendi veren mühendislik önizleme modelleridir.
- Trafik kaynağı seçiliyken manuel `OverallLw` alanı kullanıcı beklentisini bozmasın diye gizlenir; motor trafik parametrelerini esas alır.
- Varsayılan trafik seviye ölçeği line-source motoruyla uyumlu olacak şekilde kalibre edilmiştir.

---

## 8. Malzeme Kütüphanesi

Bariyer malzemesi alanları:
- Kimlik, ad, kategori
- Kalınlık
- Yüzey kütlesi
- `Rw` / genel `TL`
- 8 oktav `TL` bantları
- 8 oktav absorpsiyon bantları
- `NRC`
- Kaynak
- Güven / confidence
- Notlar

Kurallar:
- Yerleşik malzemelerde kaynak ve güven alanları dolu olmalıdır.
- Kütüphane okuma/yazma hataları sessiz yutulmamalı; `LastError` veya durum mesajıyla görünür olmalıdır.
- Excel şablonu 8 oktav `TL` ve `Alpha` değerleriyle round-trip yapabilmelidir.
- Manuel malzeme eklemede tek `TL/Rw` değeri tüm bantlara uygulanabilir; ayrıntılı spektrum Excel ile girilebilir.
- Kullanıcı `.dwg` gibi yanlış çıktı uzantısı verirse sahte format üretilmemelidir.

---

## 9. UI İş Akışı

- Ana ekran sol/orta/sağ düzenindedir:
  - Sol/sağ paneller: senaryo, kaynak, bariyer ve malzeme girişleri
  - Orta: geniş önizleme/3B sahne
  - Alt/yan: sonuç tabloları, seçili alıcı ve frekans detayları
- Kaynak tipine göre ilgisiz UI alanları gizlenmelidir.
- `NoiseSource.Type` değişimi `IsLineLike`, `IsTraffic`, `ShowManualLw` görünürlüklerini tetiklemelidir.
- Numeric ve seçim değişiklikleri binding modele yazıldıktan sonra hesaplanmalıdır.
- Aynı `Project` nesnesinin iç değeri değiştiğinde önizleme `SimulationRevision` ile yeniden çizilmelidir.
- Geri düğmesi uygulamayı kapatmamalı; güvenli şekilde `LauncherWindow`'a dönmelidir.
- Görünür tüm metinler Türkçe olmalıdır.

---

## 10. Görselleştirme Kuralları

- Önizleme harici 3B paket gerektirmez; Avalonia çizim primitifleri ve yazılım projeksiyonu kullanır.
- Orbit, pan ve zoom desteklenir.
- Trafik kaynağı nokta gibi değil, kalın yol bandı ve merkez/kenar çizgileriyle çizilir.
- Çoklu bariyerler farklı renk paletiyle çizilir; devre dışı bariyerler gri tonlamada gösterilir.
- Cephe alıcı hücreleri sonuç seviyesine göre renklendirilir.
- Kaynak-alıcı görüş çizgisi ve örnek üst kenar kırınım yolu çizilebilir.
- Önizleme tam fiziksel ray tracing değildir; hesap geometrisini okunur kılan mühendislik görselidir.

---

## 11. Raporlama

Rapor içeriği:
- Proje adı
- Bina ölçüleri
- Bariyer listesi, boyutlar ve malzemeler
- Kaynak tipi ve kaynak parametreleri
- Alıcı grid bilgisi
- Hesap ayarları
- Frekans bandı sonuçları
- Bariyersiz/bariyerli karşılaştırma
- Ortalama, minimum, maksimum ve kritik alıcı sonuçları
- Model sınırlamaları ve güvenilirlik uyarıları

Mevcut PDF:
- Ek bağımlılık gerektirmeyen minimal PDF üreticisidir.
- Grafik ve cephe haritası içeren gelişmiş rapor şablonu ayrı geliştirme konusudur.

---

## 12. Kritik Dikkat Notları

- Kısa bariyerde yan kaçaklar insertion loss'u düşürmelidir.
- Bariyer yükseldikçe genel zayıflama artma eğilimi göstermelidir.
- Görüş çizgisi kapanmıyorsa insertion loss sıfıra yakın kalmalıdır.
- Düşük `TL` malzeme, kırınım hesabı iyi görünse bile toplam performansı sınırlamalıdır.
- Trafik modeli resmi CNOSSOS/FHWA/NMPB değildir; raporda önizleme modeli olduğu belirtilmelidir.
- Absorpsiyon katsayıları tam cephe yansıma/akustik ışın modeli yerine sınırlı ceza/bonus olarak kullanılır.
- Eski tek-bariyerli proje dosyaları kırıcı model değişikliği nedeniyle otomatik açılmayabilir.

---

## 13. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/NoiseBarrier/`
- `src/Kst.Acoustics.Core/NoiseBarrier/NoiseBarrierEngine.cs`
- `src/Kst.Acoustics.Core/NoiseBarrier/NoiseBarrierDiffraction.cs`
- `src/Kst.Acoustics.Core/NoiseBarrier/NoiseBarrierProject.cs`
- `src/Kst.Acoustics.Core/NoiseBarrier/BarrierModel.cs`
- `src/Kst.Acoustics.Core/NoiseBarrier/NoiseBarrierLibraryService.cs`

App:
- `src/Kst.Acoustics.App/Views/NoiseBarrierView.axaml`
- `src/Kst.Acoustics.App/ViewModels/NoiseBarrierViewModel.cs`
- `src/Kst.Acoustics.App/Controls/NoiseBarrierPreviewControl.cs`
- `src/Kst.Acoustics.App/Views/NoiseBarrierLibraryWindow.axaml`

Test:
- `src/Kst.Acoustics.Tests/`

---

## 14. Korunması Gereken Regresyon Alanları

- Maekawa referans noktaları
- Kurze-Anderson referans noktaları
- Bariyer yükseldikçe insertion loss artışı
- Kısa bariyerde yan kaçak performans düşüşü
- Düşük `TL` malzemenin performansı sınırlaması
- Görüş çizgisi kapanmıyorsa insertion loss'un sıfıra yakın kalması
- Çizgisel kaynak hesabının sonlu ve kararlı kalması
- Trafik parametrelerinden gerçekçi kaynak seviyesi üretimi
- Trafik model ailelerinin farklı sonuç üretmesi
- Absorptif bariyerin yansıma cezasını düşürmesi
- Excel malzeme şablonu round-trip
- Çoklu bariyerde ikinci yüksek bariyerin sonucu artırması
- Devre dışı bariyerin hesaba katılmaması

---

## 15. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M04 regresyonları dahil mevcut test paketi geçmeli

---

## 16. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Hesap değişikliği için önce `Core/NoiseBarrier` dosyalarını incele.
4. UI değişikliği için `NoiseBarrierView`, `NoiseBarrierViewModel` ve `NoiseBarrierPreviewControl` dosyalarını kontrol et.
5. Bariyer formülü veya trafik modeli değişirse regresyon testi ekle.
6. Malzeme kütüphanesi değişirse Excel round-trip davranışını kontrol et.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.


<!-- ============================================================ -->
# 📄 DOSYA: `akustik/05-darbe-yuzey/codex5.md`
<!-- ============================================================ -->

# M05 Codex — Darbe Emici ve Ses Emici Yüzey

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları, kritik sınırlamalar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M05 — Darbe Emici Yüzey |
| Ek mod | Ses Emici İnşaat Yüzeyi |
| Amaç | Katmanlı yüzeylerde düşme darbesi güvenliği ve ses emicilik ön tasarımı |
| Standart referansları | EN 1177, ASTM F1292 |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Tamamlandı; güvenilirlik ve raporlama bakımı sürüyor |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Katmanlı darbe emici yüzey tasarımı
- Half-sine darbe ivme-zaman sinyali üretimi
- HIC hesabı için sliding-window integral
- `Gmax`, `HIC`, pulse duration ve CFH hesabı
- Kafa kütlesi ve temas alanı parametreleri
- Katman kalınlığına göre seri-yay eşdeğer sertlik ve sönümleme
- EN 1177 / ASTM F1292 tarzı compliance taraması
- Sertifika tipi Markdown raporu
- JSON proje kaydet/aç
- Özel darbe malzemeleri
- Ayrı sekmede ses emici inşaat yüzeyi tasarımı
- Ses emici yüzey için oktav bant `α`, `αw`, `NRC`, `SAA`, kullanım önerisi ve rapor

Model sınırları:
- Sonuçlar simülasyon/ön hesap niteliğindedir; saha veya laboratuvar testinin yerine geçmez.
- Sıcaklık etkili EN 1177 prosedürü henüz yoktur; malzeme veritabanında sıcaklık bağımlı veri bulunmaz.
- Ses emici yüzey hesabı resmi laboratuvar ölçümü değildir.
- Ses emici tarafta tam empedans transfer matrisi henüz yoktur; Maa + poröz absorber mühendislik modeli kullanılır.

---

## 3. Darbe Hesap Kuralları

- HIC hesabı `g` birimindeki ivme-zaman serisi üzerinden yapılır.
- Darbe sinyali half-sine dalga formu ve damping parametreleriyle üretilir.
- HIC, standart prensibe uygun sliding-window integral ile hesaplanır.
- CFH hesabı hem `HIC <= 1000` hem `Gmax <= 200g` limitlerini birlikte kontrol eder.
- Sertifika tipi CFH bin arama ile yaklaşık 5 mm hassasiyetle bulunur.
- Compliance taraması varsayılan olarak `0.5, 1.0, 1.5, 2.0, 2.5, 3.0 m` yüksekliklerini değerlendirir.
- Geçersiz girişler (`NaN`, `Infinity`, negatif/sıfır kalınlık, geçersiz kafa kütlesi veya temas alanı) sessiz yutulmamalı; kullanıcıya `StatusMessage` ile bildirilmelidir.

---

## 4. Darbe Malzeme ve Katman Kuralları

- Katman hesabında kalınlık oranlarına göre ağırlıklı seri-yay yaklaşımı kullanılır.
- Eşdeğer E-modülü ve sönümleme kalınlık ağırlığıyla türetilir.
- Özel malzemeler proje dosyasına gömülür ve açılışta geri yüklenir.
- Proje dosyasında kafa kütlesi ve temas alanı da saklanmalıdır.
- Katman listesi ve 3D viewport seçimleri senkron kalmalıdır.
- Katman satırlarında malzeme/fonksiyon etiketleri doğru güncellenmelidir.

---

## 5. Ses Emici Yüzey Kuralları

Aktif hesap çıktıları:
- Oktav bant emilim katsayıları
- `αw`
- `NRC`
- `SAA`
- Kullanım önerisi
- Uyarılar

Model notları:
- Perfore/rezonatör davranışı Maa formülasyonuyla yaklaşık hesaplanır.
- Rezonans:
  `f0 = (c / 2π) * sqrt(sigma / (t_eff * D))`
- Uç düzeltme:
  `t_eff = t + 0.85 * d`
- Poröz arka katman varsa tepe daha geniş ve yüksek; yoksa daha dar ve düşük kabul edilir.
- Poröz absorber hesabı kalınlık asimptotu ve `λ/4 = t_porous + t_air` düşük frekans etkisini içerir.
- Eski empirik `airGapFactor` ve `LowMidWeight` yaklaşımı aktif karar değildir.

Başlangıç malzemeleri:
- Taş yünü
- Cam yünü
- PET
- Melamin köpük
- Akustik kumaş
- Perfore alçıpan
- Slotlu ahşap
- Hava boşluğu
- Rijit arka yüzey

---

## 6. UI İş Akışı

- Pencere iki ana sekmeye ayrılır:
  - Darbe Emici Yüzey
  - Ses Emici İnşaat Yüzeyi
- Darbe sekmesinde kafa kütlesi, temas alanı, katman listesi, sonuçlar, kaydet/aç ve rapor komutları bulunur.
- Ses emici sekmede katman ekle/sil/sırala, hava boşluğu ekle, özel malzeme ekle ve rapor alma komutları bulunur.
- Darbe sonuçları kullanıcıya tahmini olarak etiketlenmelidir.
- Hatalar ve geçersiz hesaplar kullanıcıya görünür durum mesajı üretmelidir.
- Eski `Watermark` yerine Avalonia uyumlu `PlaceholderText` kullanılmalıdır.

---

## 7. Raporlama

Darbe raporu:
- Parametreler
- Katman tablosu
- `Gmax`, `HIC`, CFH ve pulse duration
- ASCII ivme-zaman grafiği
- EN 1177 / ASTM F1292 tarama tablosu
- Her deneme için karar: `GEÇTİ`, `HIC aştı`, `Gmax aştı` vb.
- Sertifika tipi CFH
- Model sınırlamaları

Ses emici raporu:
- Katman özeti
- Oktav bant `α` tablosu
- `αw`, `NRC`, `SAA`
- 40 sütunluk ASCII spektrum çubuk grafiği
- Kullanım önerileri ve uyarılar

---

## 8. Kritik Dikkat Notları

- HIC ölçeği `g` biriminde kalmalıdır; SI ivme ile karışmamalıdır.
- Drop yüksekliği etkisi yaklaşık `sqrt(h)` ölçeğiyle doğrulanmalıdır.
- Kafa kütlesi etkisi yaklaşık `1/sqrt(m)` eğilimi göstermelidir.
- Temas alanı etkisi yaklaşık `sqrt(A)` eğilimiyle kontrol edilmelidir.
- Daha sert katman daha yüksek darbe ivmesi üretmelidir.
- Half-sine profil non-negatif ve deterministik olmalıdır.
- CFH her zaman HIC ve Gmax limitlerinden daha kısıtlayıcı olanı esas almalıdır.
- Ses emici yüzeyde hava boşluğu düşük frekans emilimini artırabilmelidir.
- Perfore panel rezonansının beklenen bantta tepe üretmesi korunmalıdır.

---

## 9. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/ImpactSurface/`
- `src/Kst.Acoustics.Core/ImpactSurface/ImpactCalculationService.cs`
- `src/Kst.Acoustics.Core/ImpactSurface/ImpactCalculationResult.cs`
- `src/Kst.Acoustics.Core/ImpactSurface/StandardComplianceReport.cs`
- `src/Kst.Acoustics.Core/ImpactSurface/ImpactReportBuilder.cs`
- `src/Kst.Acoustics.Core/AbsorptiveSurface/`
- `src/Kst.Acoustics.Core/AbsorptiveSurface/AbsorptiveSurfaceCalculationService.cs`
- `src/Kst.Acoustics.Core/AbsorptiveSurface/AbsorptiveReportBuilder.cs`

App:
- `src/Kst.Acoustics.App/ViewModels/ImpactSurfaceViewModel.cs`
- `src/Kst.Acoustics.App/ViewModels/ImpactLayerRowViewModel.cs`
- `src/Kst.Acoustics.App/ViewModels/AbsorptiveLayerRowViewModel.cs`
- `src/Kst.Acoustics.App/Views/ImpactSurfaceWindow.axaml`

Test:
- `src/Kst.Acoustics.Tests/ImpactValidationTests.cs`
- `src/Kst.Acoustics.Tests/`

---

## 10. Korunması Gereken Regresyon Alanları

- HIC hesap ölçeği
- Drop yüksekliği, kafa kütlesi, temas alanı ve sertlik monotonluğu
- Tek katman analitik half-sine sonucu
- Determinizm
- Golden EVA fixture sonucu
- Non-negatif half-sine profili
- Seri-yay tutarlılığı
- Geçersiz giriş doğrulaması
- Proje JSON round-trip ve özel malzeme geri yükleme
- Ses emici yüzey kalınlık/NRC davranışı
- Perfore panel Maa rezonans tepesi
- Hava boşluğunun 125 Hz civarı emilimi artırması

---

## 11. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M05 regresyonları dahil mevcut test paketi geçmeli

---

## 12. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Darbe hesabı için önce `Core/ImpactSurface` dosyalarını incele.
4. Ses emici yüzey için önce `Core/AbsorptiveSurface` dosyalarını incele.
5. UI değişikliği için `ImpactSurfaceViewModel` ve `ImpactSurfaceWindow` dosyalarını kontrol et.
6. Fizik/formülasyon değişirse regresyon testi ekle veya mevcut M05 testlerini çalıştır.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.


<!-- ============================================================ -->
# 📄 DOSYA: `akustik/06-oda-modlari/codex6.md`
<!-- ============================================================ -->

# M06 Codex — Oda Modları Hesabı

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları, kritik sınırlamalar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M06 — Oda Modları Hesabı |
| Amaç | Dikdörtgen odalarda düşük frekans modlarını, basınç dağılımını ve difüzör ön tasarımını hesaplamak |
| Modül kodu | OM |
| UI rengi | `#C49BE4` |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Tamamlandı; DXF/CSV çıktıları ileri faz |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Dikdörtgen oda için axial, tangential ve oblique mod frekansları
- 0-500 Hz arası düşük frekans oda modu analizi
- Sıcaklığa bağlı ses hızı
- Schroeder geçiş frekansı
- Oda oran analizi ve Bolt bölgesi yaklaşımı
- Seçili mod için 3D basınç haritası
- Orbit kamera, wireframe oda ve yüzey basınç örnekleri
- QRD 1D difüzör ön tasarımı
- Skyline 2D difüzör ön tasarımı
- Düşük frekansta difüzör uygunluk uyarıları

İleri faz:
- DXF çıktı
- CSV çıktı
- Daha kapsamlı modal yoğunluk grafiği

---

## 3. Fizik Kuralları

Mod frekansı:

```text
f(nx, ny, nz) = (c / 2) * sqrt((nx/Lx)^2 + (ny/Ly)^2 + (nz/Lz)^2)
```

Ses hızı:

```text
c = 331.3 * sqrt(1 + T / 273.15)
```

Mod tipleri:
- Axial: yalnız 1 indeks > 0
- Tangential: tam 2 indeks > 0
- Oblique: 3 indeks > 0

Schroeder geçiş frekansı:

```text
fs = 2000 * sqrt(RT60 / V)
```

Kurallar:
- RT60 girilmezse hacme bağlı mühendislik varsayımıyla etkin RT60 ve Schroeder frekansı tahmin edilir.
- Hatalı oda boyutu, sıcaklık, maksimum frekans veya RT60 çökme yerine uyarılı boş sonuç üretmelidir.
- Modlar özellikle Schroeder frekansı altında anlamlıdır; kullanıcıya bu sınır görünür olmalıdır.

---

## 4. Basınç Haritası Kuralları

Seçili mod için basınç alanı:

```text
p(x, y, z) = cos(nx*pi*x/Lx) * cos(ny*pi*y/Ly) * cos(nz*pi*z/Lz)
```

Kurallar:
- Değer aralığı normalize olarak `-1 .. +1` kalmalıdır.
- `+1` sıkışma, `-1` seyrelme, `0` nötr bölge olarak gösterilir.
- Oda wireframe kutu olarak çizilir.
- Basınç örnekleri oda yüzeylerinde gösterilir.
- Tabloda mod seçimi değişince basınç haritası aynı modla güncellenir.
- Kamera orbit, pan/zoom ve sağ tık döndürme mantığıyla çalışır.

---

## 5. Difüzör Tasarımı Kuralları

QRD 1D derinlik dizisi:

```text
s(n) = n^2 mod N
d(n) = s(n) * lambda / (2N)
```

Skyline 2D hücre dizisi:

```text
s(x,y) = (x^2 + y^2) mod N
d(x,y) = s(x,y) * lambda / (2N)
```

Kurallar:
- `N` asal sıra değeridir; desteklenen tipik değerler `7, 11, 13, 17`.
- Hedef frekans seçili moda veya kullanıcı girdisine bağlı olabilir.
- Maksimum derinlik sınırı uygulanmalı; aşan kuyular kırpılmalı ve uyarı üretilmelidir.
- Schroeder frekansının belirgin altında kalan hedeflerde difüzör yerine bas tuzağı veya ayarlı yutucu önerisi verilmelidir.
- Skyline görünümü `N x N` kare hücreden oluşur ve izometrik 3B önizleme ile gösterilir.

---

## 6. UI İş Akışı

- Ana pencere üç ana bölgeden oluşur:
  - Giriş paneli
  - Mod sonuç tablosu
  - 3D basınç/difüzör görsel alanı
- Giriş değişiklikleri hesaplamayı otomatik yeniler.
- Uyarı ve durum mesajları kullanıcıya görünür olmalıdır.
- Difüzör tasarımı ayrı sekmede veya aynı pencere içinde net ayrılmış bir bölümde gösterilir.
- Görünür tüm metinler Türkçe olmalıdır.

---

## 7. Kritik Dikkat Notları

- İlk axial mod frekansı oda boyutuyla tutarlı olmalıdır.
- Basınç örnekleri normalize sınır dışına taşmamalıdır.
- Geçersiz girişler hesap motorunda güvenli ele alınmalıdır.
- RT60 tahmini kullanıcı girdisinin yerine sessizce geçmemeli; tahmin olduğu belirtilmelidir.
- Düşük modal frekanslarda QRD difüzör önerisi yanıltıcı olabilir; uygunluk uyarısı korunmalıdır.
- QRD/Skyline derinlik dizileri asal sıra ve maksimum derinlik sınırına göre regresyonla korunmalıdır.

---

## 8. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/RoomModes/RoomMode.cs`
- `src/Kst.Acoustics.Core/RoomModes/RoomModeType.cs`
- `src/Kst.Acoustics.Core/RoomModes/RoomModeInput.cs`
- `src/Kst.Acoustics.Core/RoomModes/RoomModeResult.cs`
- `src/Kst.Acoustics.Core/RoomModes/RoomModeCalculator.cs`
- `src/Kst.Acoustics.Core/RoomModes/RoomRatioAnalyzer.cs`
- `src/Kst.Acoustics.Core/RoomModes/PressureFieldSampler.cs`
- `src/Kst.Acoustics.Core/RoomModes/Diffusers/`

App:
- `src/Kst.Acoustics.App/ViewModels/RoomModesViewModel.cs`
- `src/Kst.Acoustics.App/Views/RoomModesWindow.axaml`
- `src/Kst.Acoustics.App/Views/RoomModesWindow.axaml.cs`
- `src/Kst.Acoustics.App/Controls/RoomModePressureControl.cs`
- `src/Kst.Acoustics.App/Controls/QrdDiffuserPreviewControl.cs`

Test:
- `src/Kst.Acoustics.Tests/`

---

## 9. Korunması Gereken Regresyon Alanları

- İlk axial mod frekansı
- Geçersiz giriş uyarıları
- RT60 tahmini
- Basınç örneklerinin normalize kalması
- QRD kalıntı dizisi
- Skyline hücre dizisi
- Düşük frekans difüzör uygunluk uyarısı
- Maksimum derinlik kırpması

---

## 10. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M06 regresyonları dahil mevcut test paketi geçmeli

---

## 11. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Mod hesabı için önce `Core/RoomModes` dosyalarını incele.
4. Difüzör değişikliği için `Core/RoomModes/Diffusers` dosyalarını incele.
5. UI değişikliği için `RoomModesViewModel`, `RoomModesWindow` ve ilgili kontrol dosyalarını kontrol et.
6. Formülasyon/görsel örnekleme değişirse regresyon testi ekle veya mevcut M06 testlerini çalıştır.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.


<!-- ============================================================ -->
# 📄 DOSYA: `akustik/07-yerinde-olcum/codex7.md`
<!-- ============================================================ -->

# M07 Codex — Yerinde Ölçüm

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, platform sınırlamaları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M07 — Yerinde Ölçüm |
| Amaç | Log sweep tabanlı oda akustiği ölçümü, analiz, raporlama ve simülasyon karşılaştırması |
| Hedef seviye | Room EQ Wizard benzeri ölçüm platformu |
| Modül kodu | YO |
| UI rengi | `#4EA8DE` |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Oturum uzantısı | `.eye4meas.json` |
| Durum | Geliştiriliyor; ana DSP/UI çekirdeği mevcut |

---

## 2. Mevcut Güvenli Kapsam

Aktif kapsam:
- Log sweep üretimi
- Sweep kaydı ve IR çıkarımı
- Frekans yanıtı, faz ve grup gecikmesi
- IR, step response, FR, waterfall/CSD, spektrogram
- RTA ve SPL metre
- ISO 3382 tabanlı akustik parametreler
- STI / MTI hesap altyapısı
- THD ve IMD analizleri
- Mikrofon `.cal` düzeltmesi
- SPL referans kalibrasyonu
- Oturum kaydet/yükle
- CSV ve minimal PDF raporu
- Otomatik EQ önerisi ve hedef eğri karşılaştırması
- 3B oda/kurulum noktası görünümü
- DXF oda geometrisi yükleme
- Hacim akustiği simülasyonu ile ölçüm karşılaştırması

Önemli sınırlamalar:
- Windows dışı platformlarda NAudio WASAPI gerçek kayıt desteklenmez; macOS'ta simülasyon modu korunur.
- macOS'ta oynatma için `afplay` fallback kullanılır.
- THD/IMD, log sweep kaydından otomatik ve tam güvenilir şekilde çıkarılmaz; ayrı sabit ton veya multitone ölçüm akışı gerekir.
- REW tam kapsamı hedeflenir, fakat resmi REW eşdeğerliği iddia edilmemelidir.

---

## 3. Ölçüm ve Analiz Kuralları

Sinyaller:
- Log sweep
- MLS
- Pembe gürültü
- Beyaz gürültü
- Multitone
- Sabit sine

Ana analizler:
- Impulse Response
- Frequency Response
- Phase
- Group Delay
- Step Response
- RTA
- SPL / Leq / Lmax / Lmin / Lpeak
- Waterfall / CSD
- Spectrogram
- THD / HD2-HD5
- IMD
- EQ önerileri

Akustik parametreler:
- RT20
- RT30
- T60
- EDT
- Ts
- C50
- C80
- D50
- G
- STI
- MTI

Kurallar:
- Parametreler 8 oktav bantta gösterilir: `63, 125, 250, 500, 1000, 2000, 4000, 8000 Hz`.
- IR işlenmeden önce peak alignment yapılmalıdır.
- Schroeder EDC hesaplarında gürültü tabanı ve kuyruk kırpma dikkate alınmalıdır.
- FFT dekonvolüsyonda circular aliasing oluşmaması için FFT boyutu `N + M - 1` tabanlı seçilmelidir.
- Biquad/IIR hesaplarında `NaN` üretimi engellenmelidir.

---

## 4. Kalibrasyon Kuralları

- Loopback latency ölçümü çapraz korelasyonla yapılır.
- Mikrofon kalibrasyon dosyası `.cal` formatında frekans/dB sapma tablosu olarak yüklenir.
- FR ölçümü mikrofon kalibrasyon eğrisiyle düzeltilir.
- SPL kalibrasyonu referans değerle yapılır; tipik referanslar `94 dB` veya `114 dB`.
- Arka plan gürültüsü ve düşük S/N bantları kullanıcıya uyarı olarak gösterilmelidir.
- macOS mikrofon izni için `Info.plist` içinde `NSMicrophoneUsageDescription` bulunur; gerçek izin akışı bundle içinde çalışır.

---

## 5. 3B Oda ve Kurulum Haritası

Aktif yapı:
- Manuel dikdörtgen oda: `L x W x H`
- DXF oda geometrisi: mevcut `DxfSurfaceParser` sarmalayıcısı
- `RoomGeometry.FromBox`
- `RoomGeometry.FromSurfaces`
- Hoparlör ve mikrofon kurulum noktaları
- Zemin pick ve XYZ girişi
- Kaynak-mikrofon mesafe çizgisi
- Oturum dosyasına oda ve nokta bilgisi kaydı

Görsel kurallar:
- Hoparlör: turuncu üçgen
- Mikrofon: mavi daire
- Tamamlanan ölçüm: yeşil dolu işaret
- Aktif ölçüm: sarı vurgulu işaret
- Orbit kamera, pan ve zoom desteklenir.
- 3B oda görünümü ölçüm verisinin yerleşimini anlatmak içindir; hacim akustiği simülasyon motoru değildir.

---

## 6. Simülasyon Karşılaştırması

Aktif amaç:
- M01/M08 hacim akustiği sonuçlarıyla gerçek ölçümü yan yana göstermek.
- Ölçüm ve simülasyon farkını oktav bant ve parametre bazında hesaplamak.

Desteklenen karşılaştırmalar:
- RT30
- EDT
- C50
- C80
- D50
- Ts
- FR overlay

Kurallar:
- Simülasyon `.e4p01` / ilgili proje dosyasından yüklenir.
- Karşılaştırma satırlarında ölçüm, simülasyon, delta ve durum bulunur.
- Toleranslar kullanıcı tarafından ayarlanabilir.
- Durumlar `Ok`, `Warn`, `Critical`, `NoMeasurement`, `NoSimulation` gibi ayrılmalıdır.
- PDF raporu varsa karşılaştırma özeti rapora dahil edilir.

---

## 7. EQ ve Raporlama

EQ:
- Hedef eğri olarak Harman benzeri veya özel hedef kullanılabilir.
- PEQ filtreleri frekans, Q ve gain olarak üretilir.
- Orijinal FR, filtre yanıtı ve tahmini düzeltilmiş FR overlay çizilir.
- REW uyumlu filtre dışa aktarımı ileri/opsiyonel akış olarak korunur.

Rapor:
- Minimal PDF üretici harici kütüphane kullanmaz.
- Rapor içeriği:
  - Oturum metadata
  - Donanım / cihaz bilgileri
  - Oda bilgisi
  - İlk kurulum noktaları
  - Ölçüm parametre tabloları
  - THD/IMD özetleri
  - Varsa simülasyon karşılaştırması
- PDF metni ASCII güvenli sanitize edilir.
- CSV dışa aktarımı seçili ölçüm veya parametre verisi için kullanılabilir.

---

## 8. UI İş Akışı

Ana pencere:
- Araç çubuğu: cihaz seçimi, gecikme ölçümü, SPL kalibrasyonu, mikrofon kalibrasyonu, RTA, kayıt/aç, CSV/PDF çıktı
- Sol liste: ölçümler ve görünürlük/renk yönetimi
- Ana sekmeler:
  - FR
  - IR
  - Parametreler
  - Waterfall
  - Spektrogram
  - Step
  - RTA
  - SPL
  - THD/IMD
  - EQ
  - 3B Oda
  - Karşılaştırma

UI kuralları:
- Uzun süren yakalama/analiz UI thread'ini kilitlememelidir.
- UI property güncellemeleri Dispatcher güvenliğiyle yapılmalıdır.
- Koyu temada TabControl, DataGrid ve giriş kontrolleri okunabilir kalmalıdır.
- Grafiklerde çok sayıda çizgi için `StreamGeometry`, cache'li Pen/Brush ve benzeri performans önlemleri korunmalıdır.
- Geri butonu ana ekrana güvenli dönüş yapmalıdır.
- Görünür tüm metinler Türkçe olmalıdır.

---

## 9. Platform ve Bağımlılık Notları

Bağımlılıklar:
- NAudio: Windows WASAPI ses I/O
- FftSharp / MathNet benzeri FFT altyapısı
- Mevcut DXF parser
- ClosedXML varsa Excel/rapor tarafında kullanılabilir

Platform notları:
- Windows: gerçek capture/playback yolu
- macOS: oynatma fallback ve simülasyon modu; gerçek capture desteği ayrıca ele alınmalı
- macOS bundle için `Info.plist` korunmalıdır
- Audio capture örnek listesi thread-safe olmalıdır

---

## 10. Kritik Dikkat Notları

- Log sweep dekonvolüsyonda circular aliasing tekrar oluşmamalıdır.
- Octave band filtre katsayıları Audio EQ Cookbook düzeltmeleriyle uyumlu kalmalıdır.
- Parametre hesabı peak-aligned IR üzerinden yapılmalıdır.
- Schroeder kuyruğu gürültü tabanı nedeniyle yapay uzamamalıdır.
- THD/IMD sonuçları uygun test sinyali olmadan kesin gibi gösterilmemelidir.
- RTA canlı akışında cross-thread UI update hataları engellenmelidir.
- macOS'ta gerçek kayıt yoksa kullanıcıya simülasyon modu açıkça belirtilmelidir.
- Ölçüm/simülasyon karşılaştırmasında toleransların birimi ve anlamı görünür olmalıdır.

---

## 11. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Measurement/Signals/`
- `src/Kst.Acoustics.Core/Measurement/Capture/`
- `src/Kst.Acoustics.Core/Measurement/Processing/`
- `src/Kst.Acoustics.Core/Measurement/Analysis/`
- `src/Kst.Acoustics.Core/Measurement/Spatial/`
- `src/Kst.Acoustics.Core/Measurement/Comparison/`
- `src/Kst.Acoustics.Core/Measurement/Data/`

App:
- `src/Kst.Acoustics.App/Views/MeasurementWindow.axaml`
- `src/Kst.Acoustics.App/ViewModels/MeasurementViewModel.cs`
- `src/Kst.Acoustics.App/ViewModels/MeasurementViewModel.Capture.cs`
- `src/Kst.Acoustics.App/ViewModels/MeasurementViewModel.Analysis.cs`
- `src/Kst.Acoustics.App/ViewModels/MeasurementViewModel.Spatial.cs`
- `src/Kst.Acoustics.App/ViewModels/MeasurementViewModel.Comparison.cs`
- `src/Kst.Acoustics.App/ViewModels/MeasurementViewModel.Distortion.cs`
- `src/Kst.Acoustics.App/Controls/FrequencyResponsePlot.cs`
- `src/Kst.Acoustics.App/Controls/ImpulseResponsePlot.cs`
- `src/Kst.Acoustics.App/Controls/WaterfallPlot.cs`
- `src/Kst.Acoustics.App/Controls/SpectrogramControl.cs`
- `src/Kst.Acoustics.App/Controls/RtaBarControl.cs`
- `src/Kst.Acoustics.App/Controls/DistortionPlot.cs`
- `src/Kst.Acoustics.App/Controls/StepResponsePlot.cs`
- `src/Kst.Acoustics.App/Controls/MeasurementRoomViewport.cs`

Test:
- `src/Kst.Acoustics.Tests/`

---

## 12. Korunması Gereken Regresyon Alanları

- Log sweep üretimi
- IR dekonvolüsyon FFT boyutu
- FR genlik/faz üretimi
- Octave band filtre kararlılığı
- Schroeder EDC ve RT parametreleri
- STI/MTI hesap aralığı
- RTA ring buffer ve bant seviyeleri
- SPL kalibrasyon offseti
- THD/HD2-HD5 harmonik tespiti
- IMD ürün tespiti
- Spectrogram veri boyutları ve render güvenliği
- Session JSON round-trip
- Mikrofon `.cal` interpolasyonu
- 3B oda kutusu/DXF yükleme ve setup point kaydı
- Ölçüm-simülasyon karşılaştırma toleransları
- PDF rapor üretimi

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
- M07 regresyonları dahil mevcut test paketi geçmeli

---

## 14. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. DSP/formülasyon için önce `Core/Measurement/Processing` ve `Core/Measurement/Analysis` dosyalarını incele.
4. Ses I/O için `Core/Measurement/Capture` dosyalarını ve platform şartlarını kontrol et.
5. 3B oda için `Core/Measurement/Spatial` ve `MeasurementRoomViewport` dosyalarını incele.
6. Simülasyon karşılaştırması için `Core/Measurement/Comparison` ve ilgili VM partial dosyasını incele.
7. UI değişikliği için `MeasurementWindow` ve `MeasurementViewModel` partial dosyalarını kontrol et.
8. Risk varsa regresyon testi ekle veya mevcut M07 testlerini çalıştır.
9. Görünür UI metinlerinde Türkçe karakter kontrolü yap.


<!-- ============================================================ -->
# 📄 DOSYA: `akustik/08-bina-gurultu/codex8.md`
<!-- ============================================================ -->

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
- Oktav gösterimin açıklık/flanking çift saymaması
- Cephe `DeltaLfs` işareti ve değerleri
- Darbe sonuç satırında çözüm önerilerinin çalışması
- Küçük duvar atamasının rebuild sonrası korunması
- Düşey birleşimde partner duvarların atama panelinde görünmesi

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


<!-- ============================================================ -->
# 📄 DOSYA: `elektrik/_index.md`
<!-- ============================================================ -->

# Elektrik — Ana Başlık 2

**Modül aralığı:** 11–19  
**Durum:** 6 modül aktif / başlatıldı

## Modüller

| # | Klasör | Modül Adı | Durum |
|---|--------|-----------|-------|
| 11 | [11-tek-hat-semasi](11-tek-hat-semasi/codex11.md) | Tek Hat Şeması | Geliştiriliyor |
| 12 | [12-basit-proje-ciz](12-basit-proje-ciz/codex12.md) | Basit Proje Çiz (Kuvvetli Akım) | Geliştiriliyor |
| 13 | [13-zayif-akim-tek-hat](13-zayif-akim-tek-hat/codex13.md) | Zayıf Akım Tek Hat Şeması | Başlatıldı |
| 14 | [14-kesif-hazirla](14-kesif-hazirla/codex14.md) | Keşif Hazırla | Planlama bekliyor |
| 15 | [15-cevre-aydinlatma](15-cevre-aydinlatma/codex15.md) | Çevre Aydınlatma Projesi Hazırla | Planlama |
| 16 | [16-zayif-akim-proje-ciz](16-zayif-akim-proje-ciz/codex16.md) | Zayıf Akım Proje Çiz | Geliştiriliyor |
| 17–19 | — | _rezerve_ | — |

## Standartlar

IEC 60617, IEC 60364, TS HD 60364

## Kaynak Kod

`../src/Kst.Acoustics.Core/Electric/` — elektrik iş mantığı  
`../src/Kst.Acoustics.App/Electric/` — UI katmanı


<!-- ============================================================ -->
# 📄 DOSYA: `elektrik/11-tek-hat-semasi/codex11.md`
<!-- ============================================================ -->

# M11 Codex — Tek Hat Şeması

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M11 — Tek Hat Şeması |
| Amaç | Pano, linye, kablo, busbar, kompanzasyon ve jeneratör içeren tek hat şeması üretmek |
| Standart referansları | IEC 60617, IEC 60364, TS HD 60364 |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Proje uzantısı | `.e4p11` |
| Durum | Geliştiriliyor |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Ana pano, tali pano, aydınlatma panosu, kompanzasyon panosu ve transfer panosu
- Linye girişi ve düzenleme
- Monofaze/trifaze yük ayrımı
- Faz dağılımı
- Giriş/çıkış sigortası seçimi
- Kaçak akım rölesi seçimi
- Kablo kesiti, gerilim düşümü ve ısı kaybı hesabı
- Dikey busbar sistemi ve çıkış kutuları
- Aydınlatma panosu için astronomik zaman rölesi ve kontaktör seçimi
- Kompanzasyon hesabı, kondansatör kademeleri, SVC reaktör kademeleri ve harmonik filtre
- Jeneratör, transfer panosu ve ATS/MTS şalteri
- Bina kat konfigürasyonu ve kat bantlı şema çizimi
- Kütüphane düzenleme penceresi
- M12 Basit Proje Çiz'den `.e4p11` aktarım uyumu

---

## 3. Temel Tasarım Kararları

- Aydınlatma varsayılan birim güç: `40 W`
- Priz varsayılan birim güç: `300 W`
- Islak hacim priz varsayılan güç: `300 W`
- Tali pano KAR: `30 mA`
- Ana pano KAR: `300 mA`
- Kompanzasyon tetik: toplam güç `> 15 kW`
- Kompanzasyon yalnız ana pano düzeyinde değerlendirilir.
- Hedef cosφ: `0.99`
- Varsayılan kablo tipi: `NHXMH`
- Selektivite proje ayarıdır; aktifse üst pano çıkış sigortası bir üst standart değere çıkar.
- İzin verilen gerilim düşümü proje ayarıdır; varsayılan `3.0%`.
- Ana pano adı her zaman `Ana Pano` olmalıdır.
- Projede yalnız bir ana pano olabilir; ikinci ana pano UI ve ViewModel seviyesinde engellenmelidir.
- Tüm görünür metinler Türkçe olmalıdır.

---

## 4. Pano ve Linye Kuralları

Pano tipleri:
- Ana pano
- Tali pano
- Aydınlatma panosu
- Kompanzasyon panosu
- Transfer panosu

Linye kuralları:
- Yük tipi kütüphaneden gelir.
- Linye no, yük tipi, faz tipi, sorti sayısı, birim güç, talep faktörü, kablo mesafesi, sigorta cinsi ve açıklama düzenlenebilir.
- Sigorta amperi ve akım hesap servisleriyle yeniden hesaplanır.
- Monofaze/trifaze ayrımı korunur.
- Faz dağılımı greedy dengeli dağıtım mantığıyla yapılır.
- `NumberHelper` hem nokta hem virgül ondalık ayracını kabul etmelidir.

Aydınlatma panosu:
- Girişinde KAR + sigorta + astronomik zaman rölesi bulunur.
- Sayaçlı/sayaçsız olabilir.
- Aydınlatma linyelerinde kontaktör otomatik seçilir.

---

## 5. Busbar ve Kablo Kuralları

Busbar:
- Busbar dikey çizilir.
- Busbar uzunluğu kullanıcıdan sabit alınmaz; bağlı panoların pozisyonlarına göre otomatik hesaplanır.
- Çıkış kutuları pozisyona göre sıralanır.
- Her çıkış kutusunda sigorta bulunur.
- Çıkış kutusundan panoya kısa kablo mesafesi girilebilir.
- Busbar segmentlerinde akım, gerilim düşümü ve ısı kaybı hesaplanır.

Kablo:
- Kablo hesabı üst pano çıkış sigortasına göre yapılır.
- Busbar bağlı panoda çıkış kutusu sigortası esas alınır.
- Gerilim düşümü gerçek akım ve mesafeye göre hesaplanır.
- Isı kaybı `P = 3 * I^2 * R` yaklaşımıyla hesaplanır.
- Direkt kablolu alt pano bağlantıları pano merkezinden geçmemeli; ayrı kablo kanallarıyla çizilmelidir.

---

## 6. Kompanzasyon Kuralları

Kompanzasyon:
- Toplam güç tetik sınırını aşarsa otomatik hesaplanır.
- Kullanıcı kompanzasyon panosu eklediyse tetik gücü altında bile `Proje.Kompanzasyon` sonucu silinmemelidir.
- Elle ayarlanan kademeler hesap sırasında korunmalıdır.

Kondansatör kademeleri:
- Kondansatör kademe sayısı `6`, `12` veya `18` olabilir.
- Kondansatör ve reaktör aynı kademe üzerinde olamaz.
- Kondansatör kademeleri ayrı listede tutulur.

Reaktör/SVC:
- Reaktör ya kapalıdır ya da sabit `3` reaktör kademesi vardır.
- SVC yük sürücü, 3 reaktörün toplam kVAr değerinden büyük/uygun tek sürücü olarak seçilir.
- SVC sürücü manuel seçimi yeterliyse otomatik hesap tarafından ezilmemelidir.

Kompanzasyon panosu:
- Linye girilmez.
- Röle, NH bıçaklı sigorta, kondansatör, reaktör, SVC sürücü ve harmonik filtre otomatik seçilebilir.
- Kullanıcı override açarsa seçilen bileşenler korunur.
- Harmonik filtreli kompanzasyon panosu desteklenir.

---

## 7. Jeneratör Kuralları

- Jeneratör yalnız ana panoya eklenebilir.
- Proje düzeyinde tek jeneratör modeli bulunur.
- Jeneratör modları:
  - Prime / PRP
  - Standby / ESP
- Emniyet faktörü varsayılan `1.25`.
- Jeneratör gücü ana pano yüküne göre otomatik seçilebilir.
- Transfer panosu otomatik oluşturulur.
- Transfer şalteri ATS/MTS olarak kütüphaneden seçilir veya otomatik atanır.
- Jeneratör anma akımı:
  `kVA * 1000 / (sqrt(3) * V)`
- Transfer panosu normal kök pano olarak ayrıca çizilmemeli; jeneratör görseli içinde tek kaynak olarak temsil edilmelidir.
- Jeneratör bloğu ana pano alt ağacının sağ sınırından sonraya yerleştirilmeli; kompanzasyon/alt panolarla çakışmamalıdır.

---

## 8. Bina ve Şema Çizimi

- Bina konfigürasyonu bodrum, zemin, normal kat ve çatı bilgisinden oluşur.
- Kat etiketleri üstten alta sıralanır.
- Pano katı kullanıcı tarafından seçilir.
- İlk ana pano eklerken bina konfigürasyon diyaloğu otomatik açılmamalı; üst bardaki mevcut kat seçimi korunmalıdır.
- Şemada kat bantları yatay çizgilerle ayrılır.
- Aynı kattaki direkt alt panolar parent üzerine çizilmemeli; ayrı yatay sütunlara veya uygun istifleme düzenine yerleşmelidir.
- Busbar bağlantıları dikey busbar hattı + çıkış kutusu + yatay dal olarak gösterilir.
- Kablo etiketleri üst üste binmeyecek şekilde kanal/offset mantığıyla çizilmelidir.

---

## 9. Kütüphane Kuralları

Kaynak ağacında temel JSON dosyaları:
- `YukTipiKutuphanesi.json`
- `KacakAkimRolesiKutuphanesi.json`
- `KondansatorKutuphanesi.json`

Runtime'da kullanıcı veri konumunda üretilebilen kütüphaneler:
- Busbar
- Astronomik zaman rölesi
- Kontaktör
- NH bıçaklı sigorta
- Kompanzasyon rölesi
- Reaktör
- SVC sürücü
- Harmonik filtre
- Motor
- Alternatör
- Jeneratör
- Transfer şalteri

Kütüphane penceresi:
- Yük, KAR, kondansatör, busbar, AZR, kontaktör, kompanzasyon bileşenleri, jeneratör bileşenleri ve harmonik filtreleri düzenleyebilmelidir.
- Varsayılan fallback verileri `ElektrikKutuphaneServisi` içinde korunmalıdır.

---

## 10. Serileştirme ve Uyumluluk

- Proje dosyası `.e4p11` JSON formatındadır.
- JSON ayarları M12 aktarımıyla uyumlu olmalıdır.
- `PropertyNameCaseInsensitive = true` kullanılırken alias property çakışmalarına dikkat edilmelidir.
- `Jenerator.GereklikVAMin` eski yazım alias'ı kaldırılmıştır; case-insensitive ayar eski alanı `GerekliKVAMin` ile eşleştirebilir.
- `AstronomikZamanRolesiTanim.BesgelimGerilimV` alias'ı çakışmadığı için geriye dönük uyumluluk amacıyla korunabilir.
- M12'den gelen `.e4p11` yüklenince `KokPano`, `TumPanolar` listesine ad ile yeniden bağlanmalıdır.

---

## 11. UI İş Akışı

- Electric modül merkezi üzerinden Tek Hat Şeması açılır.
- Tek Hat penceresi üç ana panelden oluşur:
  - Sol: pano ağacı, kat/bina/busbar/pano komutları
  - Orta: linye formu, tablo, faz dağılımı, şema
  - Sağ: hesap özeti, kompanzasyon, jeneratör, çıkış sigortaları ve uyarılar
- Kompanzasyon panosu seçiliyken linye formu gizlenir, kompanzasyon paneli görünür.
- Alt pano çıkış sigortaları ana/üst pano ekranında görünür olmalıdır.
- Sağ tık pano düzenleme ile ad, kat, tip ve kompanzasyon override ayarları değiştirilebilir.
- Kaydet/Aç `StorageProvider` ile yapılır.
- İlk kaydetmede Farklı Kaydet diyaloğu açılır.
- Görünür tüm metinlerde Türkçe karakterler doğru kullanılmalıdır.

---

## 12. Kritik Dikkat Notları

- Ana pano tekil kuralı bozulmamalıdır.
- Kompanzasyon kademe sayısı kullanıcı seçimi hesap sırasında tekrar 6'ya düşmemelidir.
- Kondansatör ve reaktör aynı liste/kademe gibi modellenmemelidir.
- Jeneratör ve kompanzasyon aynı anda varken şema çakışmamalıdır.
- Transfer panosu çift çizilmemelidir.
- Busbar uzunluğu pozisyonlardan yeniden düzenlenmelidir.
- Runtime üretilen kütüphane JSON dosyaları repo dosyası gibi varsayılmamalıdır.
- JSON alias eklerken case-insensitive çakışma riski test edilmelidir.

---

## 13. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Electric/Models/`
- `src/Kst.Acoustics.Core/Electric/Calculation/`
- `src/Kst.Acoustics.Core/Electric/Data/`
- `src/Kst.Acoustics.Core/Electric/ElektrikKutuphaneServisi.cs`

App:
- `src/Kst.Acoustics.App/Electric/Views/TekHatSemasiWindow.axaml`
- `src/Kst.Acoustics.App/Electric/Views/TekHatSemasiWindow.axaml.cs`
- `src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.cs`
- `src/Kst.Acoustics.App/Electric/Views/KutuphaneWindow.axaml`
- `src/Kst.Acoustics.App/Electric/Views/PanoDuzenleDialog.axaml`
- `src/Kst.Acoustics.App/Electric/Views/JeneratorEkleDialog.axaml`
- `src/Kst.Acoustics.App/Electric/Views/BusbarEkleDialog.axaml`
- `src/Kst.Acoustics.App/Electric/Views/PanoBusbarBaglaDialog.axaml`
- `src/Kst.Acoustics.App/Electric/Views/LinyeDuzenleDialog.axaml`

Test:
- `src/Kst.Acoustics.Tests/`

---

## 14. Korunması Gereken Regresyon Alanları

- Linye akım ve sigorta hesabı
- Faz dağılımı ve dengesizlik oranı
- Kablo kesiti, gerilim düşümü ve ısı kaybı
- Pano toplam güç, KAR ve sayaç tayini
- Busbar segment hesabı
- Aydınlatma panosu AZR/kontaktör seçimi
- Kompanzasyon tetik ve cosφ hedefi
- Kompanzasyon 6/12/18 kademe seçimi
- Reaktör 3 kademe ve SVC sürücü kapasitesi
- Harmonik filtre seçimi
- Jeneratör PRP/ESP ve transfer şalteri seçimi
- Ana pano tekil kuralı
- `.e4p11` JSON round-trip
- M12 Tek Hat aktarımı sonrası KokPano re-link
- Jeneratör serileştirme alias çakışmasının tekrar oluşmaması

---

## 15. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M11/M12 aktarım regresyonları dahil mevcut test paketi geçmeli

---

## 16. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Hesap değişikliği için önce `Core/Electric/Calculation` dosyalarını incele.
4. Model/serileştirme değişikliği için `Core/Electric/Models` dosyalarını ve JSON uyumluluğunu kontrol et.
5. UI değişikliği için `TekHatSemasiWindow` ve `TekHatSemasiViewModel` dosyalarını incele.
6. Kütüphane değişikliği için `ElektrikKutuphaneServisi` ve `KutuphaneWindow` alanlarını kontrol et.
7. M12 aktarımını etkileyen değişikliklerde `.e4p11` round-trip testini koru.
8. Görünür UI metinlerinde Türkçe karakter kontrolü yap.


<!-- ============================================================ -->
# 📄 DOSYA: `elektrik/12-basit-proje-ciz/codex12.md`
<!-- ============================================================ -->

# M12 Codex — Basit Proje Çiz

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, iş akışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M12 — Basit Proje Çiz (Kuvvetli Akım) |
| Amaç | DXF mimari plan üzerinden oda tespiti, **kuvvetli akım** elemanı yerleşimi, kablolama, pano/linye özeti ve DXF çıktı |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Proje uzantısı | `.e4p12` |
| Durum | Geliştiriliyor |

> **M16 ayrışması (2026-06-13):** Zayıf akım proje çizimi **M16 Zayıf Akım Proje Çiz**
> modülüne taşındı — bkz. `elektrik/16-zayif-akim-proje-ciz/codex16.md`. M12 ve M16
> **ortak motoru** paylaşır (`BasitProjeCizWindow`/`BasitProjeCizViewModel`, kurucuya
> `BpcDisiplin modulModu`); kod fork edilmedi. M12 yalnız kuvvetli akımda çalışır,
> M16 yalnız zayıf akımda (`.e4p16`). Disiplin sınıflaması `BpcSistemKatalogu.Disiplini*`;
> ayıklama `DisiplinAyiklamaServisi`. M12 eski karma `.e4p12` açarsa zayıf akımı
> ayıklayıp uyarır (geometri korunur). Aşağıdaki zayıf akım bölümleri **ortak motor**
> referansıdır; M12 UI'ında zayıf akım gizlidir.

---

## 2. Modül Kapsamı

> **M50 yönlendirmesi (2026-06-13):** Bina geometrisi (DXF kat yükleme, kat/oda/açıklık
> düzenleme, kat hizalama, döşeme/tavan patlatma) **M50 Bina Geometrisi** modülüne taşındı —
> bkz. `ortak-moduller/50-bina-geometri/codex50.md`. M12 geometriyi `.e4bina` dosyasından
> salt-okunur tüketir ("Bina Geometrisi Yükle"); eski `.e4p12` projeleri açılışta otomatik
> migrate edilir. Aşağıdaki geometri bölümleri tarihsel referanstır.

Aktif kapsam:
- Bina geometrisi tüketimi: `.e4bina` yükleme + mutabakat raporu (düzenleme M50'de)
- Mahal listesi ve otomatik eleman yerleştirme (patlatılmış/tavanı açık odada tavan
  montajlı otomatik yerleşim atlanır — codex50 §3.7)
- Manuel eleman ekleme, seçme, çoklu seçme, taşıma, silme
- Aydınlatma, priz, pano, buat, klima ve zayıf akım elemanları
- Aydınlatma ve pano/priz kablolama akışları
- Kablo tavası, besleme hattı ve topraklama çizimleri
- DXF proje çıktısı ve DXF şablon üretimi
- M11 Tek Hat Şeması'na `.e4p11` aktarım
- Undo/redo ve pafta/sistem görünüm yönetimi (elemanlar + devreler + kablolar + vavien grupları)

Model sınırları:
- Armatür otomatik yerleşiminde güncel karar yalnız iki yoldur: grid dizilim ve manuel ekleme.
- Eski koridor ekseni, oda merkezi, alan bazlı adet ve özel lineer koridor algoritmaları aktif tasarım kararı değildir.
- Zayıf akım elemanları DXF çıktısında anlık blok/katman olarak üretilir; Tek Hat aktarımına dahil edilmez.
- Omurga kablolama yalnız daisy chain'dir; paralel omurga kablolama iptal edilmiştir.

---

## 3. Veri ve Koordinat Kuralları

- Eleman ve oda poligonları lokal koordinatta saklanır: `el.X`, `el.Y`, `oda.Poligon`.
- Render sırasında `OriginOffset` eklenerek dünya koordinatına geçilir.
- Viewport tıklaması dünya koordinatı döndürür; işlem yaparken lokal koordinata çevrilmelidir:
  `localX = worldX - originX`, `localY = worldY - originY`.
- Eski `.e4p12` projeleri eksik alanlar için güvenli varsayılanlarla açılmalıdır.
- Kat planı içe aktarma, oda silme, eleman taşıma/silme gibi işlemler undo snapshot almalıdır.
- Kablo çekme (mahal ve kat otomatik) işlemleri de undo snapshot almalıdır; snapshot elemanlar, devreler, kablolar ve vavien gruplarını içerir.
- Kilitli projede silme/düzenleme sessizce yutulmamalı; kullanıcıya durum mesajı verilmelidir.

---

## 4. DXF Import ve Oda Tespiti *(M50'ye taşındı — tarihsel referans)*

- DXF mimari plan kat bazlı yüklenir.
- ARC entity'leri kapı tespitinde kullanılabilir; INSERT içi ARC/segment verisi aynı koordinat sistemine normalize edilir.
- Açıklık tespiti iki kaynaktan gelir:
  - Kapı yayı / arc tabanlı tespit
  - Duvar boşluğu / gap analizi
- Manuel eklenen veya iptal edilen açıklıklar yeniden taramada korunur.
- Otomatik açıklıklar oda yüzeyi dışında kalıyorsa temizlenir; manuel ve iptal kayıtları etkilenmez.
- Açıklık tespit parametreleri kullanıcı tarafından ayarlanabilir:
  - Minimum açıklık genişliği
  - Söğe arama ucu
  - Minimum söğe boyu
  - Maksimum karşı duvar mesafesi
- Presetler: Sıkı, Dengeli, Gevşek.
- Sağ tık context menüsü açıklık üzerinde iptal, sil ve tüm gap açıklıklarını temizle işlemlerini destekler.

---

## 5. Yerleşim Kuralları

- Armatürler için güncel otomatik yerleşim grid tabanlıdır.
- Armatür kütüphanesi yüklenirken/kaydedilirken armatür tanımları `GridDizilim` değerine normalize edilir.
- Manuel ekleme her zaman desteklenir.
- Priz, anahtar, pano, buat ve klima gibi duvar snap'i gereken elemanlar duvar yakınına yerleşir.
- Anahtar/komütatör yerleşiminde kapı açıklığına yakın adaylar tercih edilir.
- Anahtar çoğaltma: Bir odada armatür sayısı anahtarın kapasitesini aştığında sistem otomatik olarak fiziksel kopya anahtar üretir.
  - Kapasite limitleri: Normal anahtar/Vavien = 9, Komütatör/KomütatörVavien = 18, Üçlü Komütatör = 27, Darbe Anahtarı = sınırsız.
  - Darbe anahtarında minimum 2 adet üretilir.
  - Kopyalar **en yakın duvara paralel** olarak 0.15 m aralıklarla dizilir (`EnYakinDuvarVektoru` algoritması).
  - Her kopya bağımsız bir `BasitProjeEleman` nesnesidir ve kendi devrelerine sahiptir.
- Mahal listesinden otomatik yerleştirmede sembol tanımı eksikse uygun varsayılan tanım fallback olarak kullanılmalıdır.
- Genel mahal yerleşim modu grid veya duvar aralıklı ise tek tek hücre aktif etme gerekmeden satırlar efektif aktif sayılabilir.
- `Sadece eksikleri ekle` açıkken mevcut otomatik elemanlar korunur; kapalıysa (tam yenilemede) eski otomatik elemanlar silinir ancak manuel yerleştirilmiş elemanlar korunur.
- Kilitli projede mahal listesinden otomatik yerleştirme yapılmaz.
- Duvar aralığı küçüldükçe yerleşen eleman sayısı artar; duvar aralığı ve otomatik kaynak işareti yeni elemanlara yazılır.

---

## 6. Seçim ve Düzenleme

- Elemanlar ve odalar tekli veya Cmd/Ctrl ile çoklu seçilebilir.
- Çoklu eleman sürükleme grup halinde taşır.
- Salt tıklama elemanı kaydırmamalı ve undo stack'i kirletmemelidir.
- Delete seçili elemanları, seçili odaları veya seçili açıklığı uygun öncelikle silmelidir.
- Eleman tıklanınca oda seçimi temizlenir; oda tıklanınca eleman seçimi temizlenir.
- `Esc` aktif modları iptal eder.
- Üst toolbar'da dosya aç/kaydet/farklı kaydet ikonları, geri al/ileri al ve panel açma düğmeleri bulunur.
- Klavye kısayolları:
  - `Cmd/Ctrl+O`
  - `Cmd/Ctrl+S`
  - `Cmd/Ctrl+Shift+S`
  - `Cmd/Ctrl+Z`
  - `Cmd/Ctrl+Shift+Z`
  - `Cmd/Ctrl+Y`

---

## 7. Kablolama Kuralları

- Kablo tipleri:
  - Güç kablosu
  - Güç kablosu 2. devre
  - Vavien seyahat kablosu
  - Darbe/sensör kontrol kablosu
  - Pano, mekanik, zayıf akım, yangın ve topraklama hatları
- Aydınlatma kablolamada üç ana akış vardır:
  - Manuel mod
  - Mahal modu
  - Tüm kat modu
- Mahal bazlı otomatik kablolama, odadaki boş armatürleri ilgili anahtara daisy-chain yöntemiyle bağlar.
- Kat bazlı otomatik kablolama:
  - Tek anahtarlı odada tüm boş armatürleri o anahtara bağlar.
  - Çok anahtarlı odada armatürleri en yakın anahtarlara dağıtır.
- Anahtar kapasite dağılımı:
  - Armatürler geometrik sıralanır, ardından odadaki anahtarlar sırayla doldurulur.
  - Her anahtar kapasitesi kadar armatür alır (ör. komütatör → 18), kapasitesi dolunca sıradaki (kopyalanmış) anahtara geçilir.
  - Her anahtarın kendi `MaxDevreNo` değerine göre devreler oluşturulur ve armatürler devrelere eşit bölünür.
- Kablo rota algoritması (DaisyChain):
  - Omurga yalnız daisy chain'dir; paralel omurga iptal edilmiştir.
  - `GeometrikSirala`: Orthogonal (grid) hizalama ödüllüdür — X veya Y ekseninde hizalı armatürler 20× daha çekici sayılır.
  - Anahtardan ilk armatüre doğrudan çapraz çizgi yerine L (dirsek) şeklinde ortogonal rota çizilir.
  - Armatürler zincir halinde sırayla bağlanır.
- "Boştakileri Göster" açıkken bağlı olmayan armatür, buat ve anahtarlar 2D görünümde vurgulanır.
- Kablo silme işlemleri seçili hat veya tüm hatlar seviyesinde desteklenir.

---

## 8. Pano, Linye ve Tek Hat Aktarımı

- M12 pano/linye özetleri M11 Tek Hat Şeması'na `.e4p11` olarak aktarılabilir.
- Aktarıma yalnız kuvvetli akım A/P/M linyeleri girer:
  - Aydınlatma
  - Priz
  - Klima / mekanik
- Zayıf akım ve yangın sistemleri Tek Hat aktarımına dahil edilmez.
- Ana pano belirleme:
  - Besleme hatlarındaki ortak `AnaPanoId`
  - Yoksa tek pano varsa o pano
  - Çok pano + besleme yoksa hata
- Tali panolarda `UstPanoAd` korunmalıdır.
- Pano kablo mesafesi besleme rotasının gerçek segment uzunluğundan hesaplanır; besleme yoksa uyarılı varsayılan mesafe kullanılabilir.
- `.e4p11` JSON ayarları M11 ile uyumlu olmalıdır.
- M11 yükleme sonrası `KokPano` ad ile `TumPanolar` listesine yeniden bağlanmalıdır.
- Jeneratör serileştirme alias çakışmaları tekrar oluşturulmamalıdır.

---

## 9. DXF Çıktı Kuralları

- DXF çıktı eleman ve kabloları E4P katmanlarına ayırır.
- Aynı blok adı/tip ve çok yakın koordinattaki üst üste elemanlar 5 cm toleransla dedup edilir.
- Kullanıcı şablon dışa aktarımında `.dwg` adı verirse sahte DWG yazılmaz; çıktı `.dxf` uzantısına çevrilir ve kullanıcı bilgilendirilir.
- Dosya adları geçersiz karakterlerden temizlenmelidir.
- Çıktı başarısı veya hatası `DurumMesaji` ile görünür olmalıdır.

---

## 10. DXF Katman Haritası

Temel eleman katmanları:
- `E4P-REFERENCE` — DXF arka plan çizgileri
- `E4P-LIGHTING` — armatürler
- `E4P-SWITCH` — anahtar vb.
- `E4P-PANEL` — kat panosu
- `E4P-SENSOR` — sensör
- `E4P-BUAT` — buat
- `E4P-MECHANICAL` — klima iç/dış ünite
- `E4P-YANGIN` — yangın elemanları
- `E4P-DATA`, `E4P-TV`, `E4P-TELEFON`, `E4P-CCTV`, `E4P-ANONS`, `E4P-DIAFON`
- `E4P-ANNOTATION` — oda poligonu ve genel yazılar

Temel kablo katmanları:
- `E4P-KABLO-GUC`
- `E4P-KABLO-GUC2`
- `E4P-KABLO-SEYAHAT`
- `E4P-KABLO-KONTROL`
- `E4P-KABLO-PANO`
- `E4P-KABLO-PANO-PRIZ`
- `E4P-KABLO-MEKANIK`
- `E4P-KABLO-TAVASI`
- `E4P-KABLO-BESLEME`
- `E4P-KABLO-DATA`, `E4P-KABLO-TV`, `E4P-KABLO-TELEFON`
- `E4P-KABLO-CCTV`, `E4P-KABLO-ANONS`, `E4P-KABLO-DIAFON`
- `E4P-KABLO-YANGIN`
- `E4P-TOPRAKLAMA`, `E4P-TOPRAKLAMA-BAKIR`

---

## 11. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Electric/BasitProjeCiz/`
- `src/Kst.Acoustics.Core/Electric/BasitProjeCiz/Services/`
- `src/Kst.Acoustics.Core/Electric/BasitProjeCiz/Models/`
- `src/Kst.Acoustics.Core/Electric/Models/`

App:
- `src/Kst.Acoustics.App/Electric/BasitProjeCiz/Views/BasitProjeCizWindow.axaml`
- `src/Kst.Acoustics.App/Electric/BasitProjeCiz/Views/BasitProjeCizWindow.axaml.cs`
- `src/Kst.Acoustics.App/Electric/BasitProjeCiz/ViewModels/BasitProjeCizViewModel*.cs`
- `src/Kst.Acoustics.App/Controls/GeometryPreviewControl.cs`

Test:
- `src/Kst.Acoustics.Tests/`

---

## 12. Korunması Gereken Regresyon Alanları

- Açıklık tespiti manuel/iptal kayıtlarını korur.
- Açıklık yüzey dışı otomatik sonuçları temizler.
- Sağ tık açıklık menüsü doğru açıklığı seçer.
- Mahal listesi tanımsız armatürü varsayılan sembolle yerleştirir.
- Armatür yerleşimi eski enum değerlerinde bile grid kullanır.
- Eleman salt tıklama konumu değiştirmez ve undo snapshot oluşturmaz.
- Çoklu eleman/oda seçimi ve silme çalışır.
- Otomatik aydınlatma kablolama boş armatürleri bağlar.
- Tek Hat aktarımı `.e4p11` round-trip sonrası panoları, linyeleri, üst pano bağını ve kablo boyunu korur.
- DXF çıktı üst üste blokları dedup eder.
- `.dwg` adı verilen şablon çıktısı `.dxf` olarak güvenli yazılır.
- Mahal listesi tam yenilemesi (sadece eksikler kapalıyken) manuel elemanları korur, eski otomatik elemanları siler.
- Kilitli projede mahal listesinden otomatik yerleştirme yapılmaz.
- Duvar aralığı değişimi yerleşen eleman sayısını etkiler, duvar aralığı ve otomatik kaynak işareti yeni elemana yazılır.
- Geri Al (Undo), kablo çekme işleminde yalnız kabloları/devreleri geri alır; armatür pozisyonları korunur.
- Anahtar çoğaltma kopyaları duvara paralel dizilir ve armatürler kapasite bazlı eşit dağıtılır.
- Kablo rotası anahtardan ilk armatüre L (dirsek) çizer; çapraz (spagetti) hat çizmez.

---

## 13. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Windows tek dosya yayınlama komutu:

```bash
dotnet publish src/Kst.Acoustics.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o publish/win-x64
```

Son bilinen genel hedef:
- Build: 0 hata
- M12 regresyonları dahil mevcut test paketi geçmeli

---

## 14. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. İş Core davranışıysa önce `Core/Electric/BasitProjeCiz` servis/model dosyalarını incele.
4. UI davranışıysa `BasitProjeCizWindow` ve `BasitProjeCizViewModel` partial dosyalarını kontrol et.
5. Viewport etkileşimi gerekiyorsa `GeometryPreviewControl.cs` içindeki callback/hit-test akışını incele.
6. Tek Hat aktarımı değişiyorsa M11 yükleme/serileştirme uyumunu kontrol et.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.
8. Risk varsa ilgili regresyon testini ekle veya mevcut testleri çalıştır.


<!-- ============================================================ -->
# 📄 DOSYA: `elektrik/13-zayif-akim-tek-hat/codex13.md`
<!-- ============================================================ -->

# M13 Codex — Zayıf Akım Tek Hat Şeması

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, sistem davranışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M13 — Zayıf Akım Tek Hat Şeması |
| Amaç | ELV sistem merkezleri, rack/hat topolojileri, cihaz özetleri ve tek hat çıktıları |
| Proje uzantısı | `.e4p13` |
| Ana veri kaynağı | M12 `.e4p12` |
| Standart referansları | IEC 60364-8, TS HD 60364-8, IEC 61076 |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Başlatıldı; Data/Kamera/Anons/TV/Yangın akışları kullanılabilir |

---

## 2. Modül Kapsamı

Aktif kapsam:
- M12 Basit Proje Çiz dosyasından zayıf akım sistemlerini içe aktarma
- Data sistemi rack merkezli tek hat
- Kamera / CCTV sistemi rack merkezli tek hat
- Acil anons sistemi hat bazlı tek hat
- TV sistemi ana santral + çatı uydu/anten hatları
- Yangın algılama sistemi loop bazlı tek hat
- Sistem bazlı şematik canvas
- Kat bazlı düzen ve kat şeritleri
- Cihaz konsolidasyonu ve adet gösterimi
- Tekil hat metrajları
- `.e4p13` proje kaydet/aç
- Aktif sisteme göre DXF çıktı

İleri/eksik alanlar:
- Telefon, diafon ve diğer ELV sistemlerinin aynı olgunlukta eklenmesi
- Data özel import/DXF testlerinin daha ayrıntılı genişletilmesi
- SVG/PNG çıktı
- Daha kapsamlı cihaz/port kapasite denetimleri

---

## 3. Genel Model Kuralları

Temel modeller:
- `ELVBlokRol`: Merkez, AraToplayici, Cihaz
- `ELVBlok`
- `ELVSayfa`
- `ELVKatInfo`
- `ZayifAkimTekHatProje`

Kurallar:
- M12 `BpcSistemKatalogu` ve `BpcSistemDevre` yapıları yeniden kullanılır.
- Katlar M12'den alınır ve büyükten küçüğe sıralanır.
- Cihazlar kat ve tip bazında konsolide edilir; tekil blok yerine `Miktar` alanı kullanılır.
- Ana merkeze bağlı olmayan üst kat toplayıcı/cihazları otomatik olarak merkez bloğuna bağlanmalıdır.
- Layout kat bantları üzerinden yapılır.
- DXF çıktısında Y ekseni CAD görünümü için ters çevrilir.

---

## 4. Data Sistemi

Model:
- `ElvDataSistemi`
- `ElvMerkez`
- `ElvRackKabin`
- `ElvCihaz`
- `ElvHatMetraj`

Kurallar:
- Her kat için rack kabin oluşturulabilir.
- M12 Data devreleri rack kabine dönüştürülür.
- `DataPriz` elemanları `DataHatSayisi` olarak sayılır.
- Devresiz Data prizleri kat bazında tek rack altında toplanır.
- Wireless sayısı M12'den otomatik türetilmez; kullanıcı manuel girer.
- Data ve Wireless hatları tekil metraj satırları olarak tutulur.
- Varsayılan hat metraj listesi hat sayısıyla senkron kalmalıdır.
- Data DXF çıktısı ana merkez solda, kat rack kabinleri sağda olacak şekilde üretilir.

---

## 5. Kamera / CCTV Sistemi

Model:
- `ElvKameraSistemi`
- Rack kabinlerde `KameraSayisi`
- `KameraHatlari`

Kurallar:
- M12 `Cctv` devrelerindeki `Kamera` elemanları kat rack'lerine aktarılır.
- Devresiz kameralar kat bazında tek CCTV rack altında toplanır.
- Kamera hatları tekil metraj satırları olarak tutulur.
- Data ile aynı rack merkezli şema mantığını kullanır.
- CCTV cihaz kataloğu NVR, PoE switch, patch panel, medya dönüştürücü, kayıt sunucusu, UPS vb. içerir.
- Aktif sistem Kamera ise DXF çıktı `KameraDxfCiktiAl` yoluna gider.

---

## 6. Acil Anons Sistemi

Model:
- `ElvAnonsSistemi`
- `ElvAnonsHatti`

Kurallar:
- Anons hoparlörleri geçişli hat mantığıyla modellenir.
- M12 `Anons` devreleri ayrı anons hattı kabul edilir.
- Her hattın `HoparlorSayisi`, kablo tipi ve metrajı ayrı düzenlenir.
- Devresiz hoparlörler kat bazında tek anons hattı altında toplanır.
- Ana merkez `Acil Anons Merkezi` olarak gösterilir.
- Cihaz kataloğu amfi, yedek amfi, mikser, acil mikrofon, bölge seçici, hat denetim modülü gibi öğeler içerir.
- Aktif sistem Acil Anons ise DXF çıktı `AnonsDxfCiktiAl` yoluna gider.

---

## 7. TV Sistemi

Model:
- `ElvTvSistemi`
- `TvHatlari`
- `ElvTvCatiHatti`

Kurallar:
- Her TV prizi ayrı koaksiyel hat satırıdır.
- TV hatlarında Cat kablo kullanılmamalıdır.
- Varsayılan TV priz hattı kablosu: `RG-6/U Koaksiyel`.
- Ana merkez `TV Ana Santral` olarak gösterilir.
- Ana santralden çatıdaki uydu/antenlere çıkan hatlar ayrıca tutulur.
- Çatıda her zaman en az 1 uydu/anten hattı bulunmalıdır.
- Varsayılan çatı hattı kablosu: `RG-11/U Koaksiyel`.
- M12 `Tv` devrelerindeki `TvPriz` elemanları aktarılır; devresiz TV prizleri de kat bazında eklenir.
- Aktif sistem TV ise DXF çıktı `TvDxfCiktiAl` yoluna gider.

---

## 8. Yangın Algılama Sistemi

Model:
- `ElvYanginSistemi`
- `ElvYanginKatLoopGrubu`
- `ElvYanginLoop`

Kurallar:
- Yangın sistemi rack/pano mantığı kullanmaz.
- Her kat/hata bir veya daha fazla loop olabilir.
- Her loop doğrudan ana yangın algılama santraline gider.
- Varsayılan kablo: `J-Y(St)Y 1x2x0.8 Yangın Kablosu`.
- Cat kablo kullanılmamalıdır.
- M12 `Yangin` devreleri ayrı loop kabul edilir.
- Devre yoksa kattaki yangın cihazları tek loop altında toplanır.
- Duman, ısı, kombine, asma tavan dedektörü, buton, siren ve flaşör cihaz sayısına dahil edilir.
- DXF çıktısında yangın santrali solda, looplar sağda çizilir; rack çizilmez.

---

## 9. Layout ve Görselleştirme Kuralları

- Kat bazlı layout kullanılır.
- Blok X konumları role göre hizalanır:
  - Merkez: sol kolon
  - Ara toplayıcı/rack: orta kolon
  - Cihaz/hat grupları: sağ kolonlar
- Kat şeritleri alternatif arka planla gösterilir.
- Cihaz bloklarında miktar öneki gösterilir: örn. `5x Data Prizi`.
- Şematik alan `ScrollViewer` içinde olmalı; çok kat/çok cihaz taşmasında kaydırma çıkmalıdır.
- Data/Kamera/Anons/TV/Yangın aktif sisteme göre aynı ekran dinamik çalışmalıdır.
- `SemaRevizyon` benzeri sinyal ile düzenleme sonrası çizim yenilenmelidir.

---

## 10. DXF Çıktı Kuralları

- DXF üretiminde `netDxf` kullanılır.
- `Polyline2D` ve `Polyline2DVertex` sınıfları kullanılmalıdır; `LwPolyline` kullanılmamalıdır.
- `AciColor.White` yoktur; uygun durumda `AciColor.Default` veya açık renk eşlemesi kullanılmalıdır.
- Her sayfa/sistem için bloklar, bağlantılar, kat çizgileri ve etiketler çizilir.
- Kat çizgileri `ZA-KAT-CIZGI` gibi ayrı katmanda tutulur.
- Cihaz adetleri DXF etiketlerine yansıtılır.
- Aktif sistem neyse DXF çıktısı ilgili özel metoda yönlenmelidir:
  - Data
  - Kamera
  - Anons
  - TV
  - Yangın

---

## 11. UI İş Akışı

- Electric modül merkezinden M13 açılır.
- Üst barda sistem seçimi bulunur: Data, Kamera, Acil Anons, TV, Yangın.
- `M12'den İçe Aktar` ile `.e4p12` seçilir.
- `Kaydet` ile `.e4p13` kaydedilir.
- `DXF Çıktı` aktif sisteme göre çıktı üretir.
- Sol panel aktif sisteme göre merkez/rack/loop grup listesini gösterir.
- Orta panel aktif sistem şemasını çizer.
- Sağ panel seçili rack/merkez/loop için cihaz ve hat düzenleme alanıdır.
- Görünür tüm metinlerde Türkçe karakterler doğru kullanılmalıdır.

---

## 12. Kritik Dikkat Notları

- Data `WirelessSayisi` M12'den otomatik alınmıyor; manuel alan olduğu unutulmamalıdır.
- Yangın sistemi rack mantığına sokulmamalıdır.
- TV sistemi Cat kabloya düşmemelidir; koaksiyel kablo mantığı korunmalıdır.
- Çatı uydu hattı sayısı minimum 1 olmalıdır.
- Hat sayısı değişince tekil hat metraj listeleri senkronlanmalıdır.
- Konsolidasyon sonrası testlerde blok sayısı yerine `Miktar` kontrol edilmelidir.
- DXF Y ekseni tersleme korunmalıdır; CAD'de şema yukarıdan aşağı doğru görünmelidir.
- AXAML'de `&` karakteri `&amp;` olarak kaçırılmalıdır.
- `StackPanel` doğrudan `Padding` almaz; gerektiğinde `Border` wrapper kullanılmalıdır.

---

## 13. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ZayifAkimTekHatModels.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ZayifAkimTekHatImportServisi.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ZayifAkimTekHatLayoutServisi.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ZayifAkimTekHatDxfServisi.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ElvDataModels.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ElvCihazKatalogu.cs`

App:
- `src/Kst.Acoustics.App/Electric/ZayifAkimTekHat/ZayifAkimTekHatWindow.axaml`
- `src/Kst.Acoustics.App/Electric/ZayifAkimTekHat/ZayifAkimTekHatWindow.axaml.cs`
- `src/Kst.Acoustics.App/Electric/ZayifAkimTekHat/ZayifAkimTekHatViewModel.cs`
- `src/Kst.Acoustics.App/Electric/ZayifAkimTekHat/ELVSematikControl.cs`
- `src/Kst.Acoustics.App/Electric/ZayifAkimTekHat/ElvDataSemaControl.cs`

Test:
- `src/Kst.Acoustics.Tests/ZayifAkimTekHatTests.cs`

---

## 14. Korunması Gereken Regresyon Alanları

- M12 import temel blok yapısı
- Kat bilgisinin aktarılması ve sıralanması
- Cihaz konsolidasyonu ve `Miktar`
- Layout role göre X ve kat bandına göre Y yerleşimi
- L-şekilli bağlantı hesabı
- DXF dosya üretimi
- DXF Y ekseni çevrimi
- Data rack import ve metraj listesi
- Kamera rack import ve kamera hatları
- Anons hat grubu ve hoparlör sayıları
- TV koaksiyel hatları ve çatı uydu hattı minimumu
- Yangın loop grupları ve doğrudan santral bağlantısı

---

## 15. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet test src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj --filter "FullyQualifiedName~ZayifAkimTekHat"
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M13 regresyonları geçmeli

---

## 16. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. M12 import değişikliği için `ZayifAkimTekHatImportServisi` dosyasını incele.
4. Sistem modeli değişikliği için `ZayifAkimTekHatModels` ve `ElvDataModels` dosyalarını kontrol et.
5. Layout/çizim değişikliği için layout servisi, `ELVSematikControl` ve `ElvDataSemaControl` dosyalarını incele.
6. DXF değişikliği için `ZayifAkimTekHatDxfServisi` ve ilgili testleri kontrol et.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.


<!-- ============================================================ -->
# 📄 DOSYA: `elektrik/14-kesif-hazirla/codex14.md`
<!-- ============================================================ -->

# M14 Codex — Keşif Hazırla

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap kuralları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M14 — Keşif Hazırla |
| Amaç | Poz kütüphanesi ve çoklu keşif tabloları ile elektrik keşfi hazırlamak |
| Proje uzantısı | `.e4p14` |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Faz 1 manuel keşif uygulaması tamamlandı |

---

## 2. Modül Kapsamı

Faz 1 aktif kapsam:
- Poz kütüphanesi yönetimi
- Birim fiyat listeleri yönetimi
- Excel/CSV/TSV poz içe aktarma
- Özel poz yönetimi
- Birden fazla keşif tablosu oluşturma
- Kütüphaneden poz seçip tabloya satır ekleme
- Miktar, birim fiyat, satır toplamı ve tablo toplamı
- Ana icmal ve tablo kırılımı
- Aynı poz/birim için tablo bazlı kırılım
- Farklı birim fiyat uyarısı
- JSON proje kaydet/aç

Faz 2 plan:
- M11, M12, M13 ve diğer elektrik modüllerinden otomatik keşif satırı üretmek
- Otomatik ve manuel satırları aynı tabloda kontrollü birleştirmek
- Otomatik satırların kaynak modülünü ve hesap dayanağını göstermek

Kapsam dışı:
- Kamu poz veritabanı entegrasyonu
- Online fiyat güncelleme
- Hakediş
- Yaklaşık maliyet kapakları
- Pursantaj
- İhale dosyası üretimi

---

## 3. Veri Modeli Kuralları

Poz:
- `PozNo`
- `BirimAdi`
- `Birim`
- `BirimFiyat`
- `Kategori`
- `Not`
- `AktifMi`
- Özel pozlarda `SonGuncellemeTarihi`

Keşif projesi:
- Proje adı
- Birim fiyat listeleri
- Manuel/özel pozlar
- Keşif tabloları
- Oluşturma/güncelleme tarihleri

Keşif tablosu:
- Ad
- Satırlar
- Not
- Toplam computed değerdir

Keşif satırı:
- Poz değerlerinin kopyasını saklar.
- Miktar ve birim fiyat düzenlenebilir.
- Satır toplamı computed değerdir.
- `KaynakTipi` ileride manuel/otomatik ayrımı için korunur.

Kalıcılık:
- `PozKutuphane` doğrudan JSON'a yazılmamalıdır; aktif listeler + manuel pozlardan yeniden oluşturulur.
- Liste pozları ve manuel pozlar kaydet/aç round-trip sonrası tekilleşmiş biçimde korunmalıdır.
- Computed toplam alanları JSON'a yazılmamalıdır.

---

## 4. Hesap Kuralları

Satır toplamı:

```text
Satır Toplamı = Miktar * Birim Fiyat
```

Tablo toplamı:

```text
Tablo Toplamı = tablodaki satır toplamları
```

Genel toplam:

```text
Genel Toplam = tüm keşif tablolarının toplamı
```

Ana icmal:
- Keşifte kullanılan bütün poz/birimler gösterilir; yalnız tekrar edenler değil.
- Tek tabloda kullanılan pozlar da kaynak tablo ve miktar bilgisiyle görünür.
- Birden çok tabloda geçen pozlar tek konsolide satırda toplanır.
- Kırılımda her tablo ayrı miktar ile gösterilir.
- Aynı `PozNo` farklı birim fiyatla kullanılmışsa `BirimFiyatFarkiVar` uyarısı verilmelidir.
- Toplam tutar, satır bazlı gerçek tutarların toplamından hesaplanır.

---

## 5. Birim Fiyat Listesi Kuralları

- Birim fiyat listeleri ayrı liste olarak saklanır.
- Yeni liste yüklendiğinde önceki listeler otomatik pasife alınır.
- Pasif liste silinmez; kullanıcı tekrar aktif yapabilir.
- Birden fazla liste aynı anda aktif olabilir.
- Poz kütüphanesi aktif listelerin pozları + manuel pozlardan oluşur.
- Manuel pozlar liste aktif/pasif değişimlerinden etkilenmez.
- Liste içinde aynı `PozNo` varsa tekilleştirme yapılır; son değer kazanır.
- Varsayılan `birim_fiyat_042026.xlsx` projeden kaldırılmıştır; kullanıcı kendi listesini ekler.

---

## 6. İçe Aktarma Kuralları

Desteklenen formatlar:
- Excel
- CSV
- TSV

Başlık eşleştirme:
- Türkçe karakterlerden bağımsız normalize edilir.
- Desteklenen temel başlıklar:
  - Poz No: `Poz No`, `PozNo`
  - Birim adı: `Birim Adı`, `İmalat Adı`, `Tanım`, `Tanımı`, `Poz Adı`, `Açıklama`
  - Birim: `Birim`
  - Birim fiyat: `Birim Fiyat`, `Fiyat`
  - Ek: `Kategori`, `Not`

Excel okuma:
- Başlık hücrelerinin gerçek kolon numarası kullanılmalıdır; boş başlık hücreleri kolon kaydırmamalıdır.
- Sayısal hücreler kültürden bağımsız doğrudan okunmalıdır.
- Para/sayı parse işlemi hem `1.107.437,49` hem `1234.56` gibi formatları doğru okumalıdır.
- Aynı `PozNo` tekrar içe aktarılırsa mevcut poz güncellenir.

---

## 7. UI İş Akışı

Ana pencere:
- Üst: dosya/komut çubuğu ve `Ana İcmal` butonu
- Sol/orta bölünmüş alan:
  - Üst: keşif tabloları ve seçili tablo satırları
  - Alt: birim fiyat pozları veya özel pozlar
- GridSplitter ile üst/alt alan yeniden boyutlandırılabilir.
- Ana icmal ayrı `KesifIcmalWindow` penceresinde açılır.

Poz alanı:
- Birim fiyat listeleri sürekli tablo olarak görünmez; flyout ile yönetilir.
- Arama alanı poz no ve birim adı üzerinde canlı filtreleme yapar.
- Birim fiyat modu ve özel poz modu ayrı tutulur.
- Kütüphane grid'i salt okunurdur.
- Pozu tabloya ekleme çift tık ile yapılabilir.
- Keşif satırı çift tık ile silinebilir; miktar kolonu bu davranıştan hariçtir.

Özel pozlar:
- `OzelPozlarWindow` ayrı penceredir.
- Özel poz ekleme/silme/düzenleme burada yapılır.
- Özel poz güncelleme tarihi değişiklikte otomatik güncellenir.

Tablo adı:
- Ana ekrandaki büyük isim TextBox kaldırılmıştır.
- Tablo adı çift tıkla açılan `RenameTableWindow` üzerinden değiştirilir.
- İsim değişince icmal kırılımları güncellenmelidir.

---

## 8. Görünüm ve Format Kuralları

- Para alanları uygun yerlerde `N2 ₺` formatıyla gösterilir.
- Düzenlenebilir `BirimFiyat` kolonunda para simgesi/string format kullanılmamalıdır; parse sorunları doğurur.
- DataGrid yazı boyutları kompakt olmalıdır.
- Seçili keşif tablosu ListBox satırında okunabilir kontrast kullanılmalıdır.
- Seçili tablo arka planı lacivert, yazı beyaz olmalıdır.
- `DataGridColumn` binding'leri görsel ağaç dışında olduğundan görünürlük kod-arkasında yönetilebilir.
- Görünür tüm metinler Türkçe olmalıdır.

---

## 9. Kritik Dikkat Notları

- Satırlar poz kütüphanesinden bağımsız kopya saklar; kütüphane fiyatı değişince eski satırlar otomatik değişmemelidir.
- `PozKutuphane` JSON'da iki kez yazılmamalıdır.
- Manuel pozlar `ManuelPozlar` üzerinden kalıcı tutulmalıdır.
- Özel poz eklenirken benzersiz `PozNo` üretilmelidir; aynı varsayılan poz no icmalde yanlış birleşmeye yol açar.
- Çift tık satır silme, kolon başlık metnine bağlı kalmamalıdır; kolon referansı kullanılmalıdır.
- Pencere ctor sırası ve kolon referansları dikkatli kurulmalıdır.
- Aktif/pasif liste değişince filtrelenmiş poz kütüphanesi yenilenmelidir.

---

## 10. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifModels.cs`
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifBirimFiyatListesiServisi.cs`
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifHesapServisi.cs`
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifPozImportServisi.cs`
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifJsonServisi.cs`

App:
- `src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs`
- `src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaWindow.axaml`
- `src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaWindow.axaml.cs`
- `src/Kst.Acoustics.App/Electric/KesifHazirla/KesifIcmalWindow.axaml`
- `src/Kst.Acoustics.App/Electric/KesifHazirla/KesifIcmalWindow.axaml.cs`
- `src/Kst.Acoustics.App/Electric/KesifHazirla/OzelPozlarWindow.axaml`
- `src/Kst.Acoustics.App/Electric/KesifHazirla/OzelPozlarWindow.axaml.cs`
- `src/Kst.Acoustics.App/Electric/KesifHazirla/RenameTableWindow.axaml`

Test:
- `src/Kst.Acoustics.Tests/KesifHazirlaTests.cs`

---

## 11. Korunması Gereken Regresyon Alanları

- Ana icmal bütün pozları gösterir.
- Tek tabloda kullanılan pozların kırılımı gösterilir.
- Aynı poz farklı birim fiyat uyarısı verir.
- Türkçe başlıklar import edilir.
- Birim fiyat listeleri aktif/pasif yönetilir.
- Özel poz tarih güncellemesi çalışır.
- Poz kütüphanesi arama/filtreleme çalışır.
- Tablo isim değişikliği icmale yansır.
- JSON round-trip manuel ve liste pozlarını tekil korur.
- Excel başlık-kolon kayması olmaz.
- Para/sayı parse kültürden bağımsız çalışır.

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
- M14 regresyonları dahil mevcut test paketi geçmeli

---

## 13. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Veri veya hesap değişikliği için `Core/Electric/KesifHazirla` dosyalarını incele.
4. UI değişikliği için `KesifHazirlaWindow`, `KesifHazirlaViewModel`, `KesifIcmalWindow` ve `OzelPozlarWindow` alanlarını kontrol et.
5. Import değişikliği yapılıyorsa Excel/CSV parse regresyonlarını koru.
6. JSON model değişikliği yapılıyorsa round-trip testini çalıştır.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.


<!-- ============================================================ -->
# 📄 DOSYA: `elektrik/15-cevre-aydinlatma/codex15.md`
<!-- ============================================================ -->

# M15 Codex — Çevre Aydınlatma Projesi Hazırla

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kapsam, kararlar, iş akışı, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M15 — Çevre Aydınlatma Projesi Hazırla |
| Amaç | Park/çevre düzenleme DXF altlığı üzerinden dış aydınlatma projesi hazırlamak |
| Proje uzantısı | `.e4p15` |
| Tek hat çıktısı | `.e4p11` |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Core boru hattı tamamlandı; ilk App/UI dilimleri var; çizim/zoom/pan ve detay panelleri eksik |

---

## 2. Modül Amacı ve Akış

Temel kullanıcı akışı:

```text
Park DXF yükle
  -> yüzeyleri belirle: yeşil alan / yol / spor alanı / bina
  -> armatür + direk bilgilerini gir veya LDT/IES fotometri dosyasından oku
  -> yüzeyden/güzergâhtan direk yerleşimi üret
  -> aydınlatma hesabı yap
  -> panoya kablo çek ve gerilim düşümüne göre kesit kontrol et
  -> TT topraklama şeridi + kazık hesabı yap
  -> Tek Hat'a Aktar (.e4p11)
  -> proje kaydet/aç (.e4p15)
```

Mevcut çizimsiz UI akışı:

```text
DXF Yükle
  -> yüzey kategorisini elle düzelt
  -> Seçili Yüzeyden Güzergâh
  -> Otomatik Direk Yerleştir
  -> harita önizleme
  -> Tek Hat'a Aktar (.e4p11) veya Kaydet (.e4p15)
```

---

## 3. Kapsam

Aktif kapsam:
- DXF kapalı poligonlarından yüzey üretimi
- DXF arka plan çizgilerinin çıkarılması ve sadeleştirilmesi
- Katman adından yüzey kategorisi tahmini
- Yüzey kategorisinin UI'da elle düzeltilmesi
- LDT ve IES fotometri okuma
- Fotometriden armatür tanımı üretme
- Manuel armatür/direk tanımı
- Yüzey çevresinden veya açık güzergâhtan direk yerleşimi
- Lümen metodu ile ortalama aydınlık ve armatür/adım önerisi
- Devre kablo hesabı ve gerilim düşümü için M11 `KabloHesapServisi` kullanımı
- TT topraklama şerit/kazık direnç hesabı
- M11 tek hat aktarımı (`.e4p11`)
- M15 proje JSON kaydet/aç (`.e4p15`)
- Harita önizlemede arka plan, yüzey ve direk render

Terrain entegrasyonu (köprü ✅ — 2026-06-14):
- "Arazi Yükle (.e4arazi)" butonu → `CevreAydinlatmaViewModel.AraziYukleVeKotAta`: M60 `.e4arazi.json`
  `AraziTuketimServisi` ile salt-okunur yüklenir, TIN kurulur, mevcut direklere `ZeminKotuM` (dip kotu) atanır.
  Durum çubuğunda `AraziBilgisi` (rev, kot aralığı, üçgen). Arazi geometrisi değiştirilmez.
- `AydinlatmaDiregi.ZeminKotuM` (nullable) eklendi. Bkz. [codex60](../../terrain/60-hafriyat-hesapla/codex60.md).

Eksik veya sonraki iş:
- Terrain: yerleştirme sonrası otomatik kot atama, 3D yerleşim, gerilim düşümünde gerçek (eğimli) hat uzunluğu.
- Serbest güzergâh çizimi
- Manuel poligon çizimi
- Zoom/pan
- LDT/IES dosya seçimi için tam kütüphane UI'ı
- Aydınlatma, kablo ve topraklama detay panelleri
- DXF çıktı ve rapor
- Nokta-nokta izolüks hesabı
- Gerçek üretici LDT/IES dosyalarıyla saha doğrulaması

Kapsam dışı:
- Hakediş
- Pursantaj
- Online fiyat/veri güncellemesi
- M11 içinde topraklama çizimini tamamen çözmek

---

## 4. Ana Kararlar

- Yüzey işaretleme iki kademelidir: katmandan otomatik tahmin + kullanıcı düzeltmesi.
- Park DXF birimi belirsizse `1 birim = 1 m` kabul edilir.
- Büyük DXF için segment sadeleştirme zorunludur; çok kısa parçalar elenir.
- Aydınlatma hesabı önce Kademe A lümen metodu ile yapılır.
- Kademe B nokta-nokta hesap ve izolüks sonraya bırakılmıştır.
- LDT ve IES düz ASCII olarak proje içinde, dış bağımlılık eklemeden okunur.
- Fotometrik dosya yoksa manuel armatür girişi fallback kalır.
- Kablo hesabı M11 `KabloHesapServisi` ile yapılır.
- Dış aydınlatmada varsayılan gerilim düşümü hedefi `%5` kabul edilmiştir.
- Topraklama sistemi TT'dir.
- TT uygunluk kontrolü `R_A * I_Delta_n <= 50 V` kuralıyla yapılır.
- Tek hat şeması M15 içinde çizilmez; M11'e `.e4p11` aktarımı yapılır.
- M11 aktarımı her seferinde yeni proje üretir; M11 tarafındaki elle yapılan ekler yeniden aktarımda korunmaz.
- M15 için M12'nin karmaşık 3D preview'i kopyalanmadı; M15'e özel basit 2D preview kullanılır.

---

## 5. Veri Modeli Özeti

Proje kökü:
- `CevreAydinlatmaProje`
- Yüzeyler
- Armatür kütüphanesi
- Direk kütüphanesi
- Güzergâhlar
- Yerleşmiş direkler
- Arka plan çizgileri
- Topraklama

Yüzey:
- `CevreYuzey`
- `YuzeyKategori`: `Diger`, `YesilAlan`, `Yol`, `SporAlani`, `Bina`
- Poligon, kaynak katman, alan

Armatür:
- `CevreArmaturTanim`
- Güç W, akı lm, marka/model, IP, optik, renk sıcaklığı
- `FotometriKaynak`: `Ldt`, `Ies`, `Manuel`
- Fotometri dosya yolu opsiyoneldir

Direk:
- `CevreDirekTanim`
- Boy, malzeme, konsol bilgileri, varsayılan armatür

Güzergâh:
- `AydinlatmaGuzergahi`
- Açık rota veya kapalı poligon çevresi
- Direk aralığı, kenar ofseti, varsayılan direk/armatür

Yerleşmiş direk:
- `AydinlatmaDiregi`
- Konum, güzergâh, direk tanımı, armatür tanımı

Topraklama:
- `TopraklamaTasarimi`
- TT/TN sistem tipi, şerit güzergâhı, kazıklar, direnç bilgileri

Kalıcılık:
- `.e4p15` JSON olarak kaydedilir.
- `Point2D` round-trip testle doğrulanmıştır.
- `.e4p11` aktarımı M11 JSON ayarlarıyla yapılır.

---

## 6. Hesap Kuralları

Lümen metodu:

```text
E_ort = (N * Phi * MF * UF) / A
```

Gerekli armatür sayısı:

```text
N = ceil(HedefLux * A / (Phi * MF * UF))
```

Önerilen direk aralığı:

```text
Aralik = Phi * MF * UF / (HedefLux * SeritGenisligi)
```

Fotometri:
- LDT/EULUMDAT ve IES/LM-63 desteklenir.
- Güç W elektrik yükü için kullanılır.
- Akı lm lümen hesabı için kullanılır.
- Işık şiddeti dağılımı nokta-nokta hesap için saklanabilir.
- `cd/1000 lm` değerleri gerçek kandela hesabında akı ile ölçeklenir.

Kablo:
- Her güzergâh/devre panodan çıkan radyal linye gibi ele alınır.
- Direk yükleri downstream kümülatif akımla hesaplanır.
- Eşdeğer uzunluk:

```text
L_eq = Sum(I_segment * L_segment) / I_toplam
```

- Kesit ve gerilim düşümü M11 `KabloHesapServisi` ile hesaplanır.
- Monofaze 230 V, trifaze 400 V kabul edilir.

Topraklama:
- Kazık, şerit ve paralel toplam direnç tahmini yapılır.
- Paralel kazık hesabı iyimserdir; etkileşim/kullanım faktörü ihmal edilmiştir.
- TT uygunluk:

```text
R_A * I_Delta_n <= 50 V
```

---

## 7. M11 ve M12 ile İlişki

M12'den gelen ortaklar:
- `Electric/Ortak/YerlesimGeometri.cs`
- `Electric/Ortak/DxfSegmentYardimci.cs`
- `Electric/Ortak/BosluktanOdaTespitServisi.cs`

M11'den kullanılanlar:
- `KabloHesapServisi`
- `ElektrikProje`
- `PanoTipi.AydinlatmaPanosu`
- `.e4p11` JSON aktarım deseni

M15 -> M11 aktarım kuralları:
- Tek kök pano: `AydinlatmaPanosu`
- Her güzergâh bir `Linye`
- `SortiSayisi` direk sayısıdır
- Yük armatür güçlerinden hesaplanır
- Kablo boyu gerçek güzergâh uzunluğudur
- Sigorta, kesit, faz dağılımı ve kompanzasyon M11 tarafında yeniden hesaplanır
- Direksiz güzergâh atlanır ve uyarı üretir
- Armatür bulunamazsa yük 0 W olur ve uyarı üretir

---

## 8. UI Durumu

Var:
- Electric launcher içinde Çevre Aydınlatma kartı
- `CevreAydinlatmaWindow`
- `CevreAydinlatmaViewModel`
- DXF açma
- `.e4p15` aç/kaydet
- `.e4p11` tek hat aktarımı kaydetme
- Yüzey listesi
- Kategori ComboBox ile elle düzeltme
- Seçili yüzeyden güzergâh üretme
- Otomatik direk yerleştirme
- 2D önizleme: arka plan çizgileri, yüzeyler, direkler

Eksik:
- Pointer ile serbest güzergâh çizimi
- Pointer ile manuel poligon çizimi
- Zoom/pan
- Armatür/direk kütüphane ekranı
- LDT/IES dosya seçme UI'ı
- Hesap/kablo/topraklama detay panelleri

Görsel doğrulama notu:
- Headless ortamda buton, diyalog ve render görsel olarak tam denenmemiştir.
- VM/Core akışları testlerle doğrulanmıştır.

---

## 9. Kritik Dikkat Notları

- Yüzey kategorisi otomatik tahmindir; kullanıcı düzeltmesi her zaman mümkün kalmalıdır.
- Türkçe katman adları aksan ve büyük/küçük harften bağımsız normalize edilmelidir.
- Açık polyline yüzey değildir; kapalı poligon yüzeye çevrilir.
- DXF arka plan çizgileri yüzeylerin altında soluk çizilmelidir.
- Unitless DXF için metre varsayımı korunmalıdır.
- M15 preview basit 2D kontroldür; M12 3D preview'e bağlı değildir.
- LDT/IES gerçek üretici dosyalarıyla farklı `Isym`, `TILT` ve açı tabloları kontrol edilmelidir.
- Direk yerleşiminde köşe kümelenmesi olmamalıdır; arc-length tabanlı yürüyüş korunmalıdır.
- Kapalı poligon çevresinde dikiş noktası tekrarı ayıklanmalıdır.
- M11 aktarımında enum/JSON ayarları M11 ile uyumlu kalmalıdır.
- Topraklama sonucu mühendislik tahminidir; zemin özgül direnci ve uygulama şartları kullanıcı tarafından doğrulanmalıdır.

---

## 10. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/CevreAydinlatmaModels.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/CevreYuzeyImportServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/YuzeyKategoriEsleyici.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/ArmaturTanimFabrikasi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/AydinlatmaHesapServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/DirekYerlesimServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/KabloGuzergahServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/TopraklamaServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/CevreAydinlatmaTekHatAktarimServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/CevreAydinlatmaJsonServisi.cs`

Fotometri:
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/Fotometri/FotometrikVeri.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/Fotometri/LdtParser.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/Fotometri/IesParser.cs`

Ortak:
- `src/Kst.Acoustics.Core/Electric/Ortak/YerlesimGeometri.cs`
- `src/Kst.Acoustics.Core/Electric/Ortak/DxfSegmentYardimci.cs`
- `src/Kst.Acoustics.Core/Electric/Ortak/BosluktanOdaTespitServisi.cs`

App:
- `src/Kst.Acoustics.App/Electric/CevreAydinlatma/CevreAydinlatmaWindow.axaml`
- `src/Kst.Acoustics.App/Electric/CevreAydinlatma/CevreAydinlatmaWindow.axaml.cs`
- `src/Kst.Acoustics.App/Electric/CevreAydinlatma/CevreAydinlatmaViewModel.cs`
- `src/Kst.Acoustics.App/Electric/CevreAydinlatma/CevreAydinlatmaPreview.cs`
- `src/Kst.Acoustics.App/Views/ElectricLauncherWindow.axaml`
- `src/Kst.Acoustics.App/Views/ElectricLauncherWindow.axaml.cs`

Test:
- `src/Kst.Acoustics.Tests/CevreAydinlatmaTests.cs`
- `src/Kst.Acoustics.Tests/Program.cs`

---

## 11. Korunması Gereken Regresyon Alanları

- LDT parser armatür adı, güç, akı ve tablo değerlerini okur.
- IES parser güç, akı ve kandela normalizasyonunu doğru yapar.
- Boş fotometri hata verir.
- Ortak yerleşim geometrisi kenar ve ızgara adaylarını üretir.
- DXF segment sadeleştirme kısa parçaları eler.
- Katman adı yüzey kategorisine doğru eşleşir.
- Kapalı poligonlar yüzeye çevrilir; açık polyline atlanır.
- Armatür fotometriden üretilir.
- Açık güzergâha eşit aralıkla direk yerleşir.
- Kapalı poligon çevresine direk yerleşir.
- Lümen metodu sınır değerlerde hata verir ve normal değerleri doğru hesaplar.
- Devre kablo hesabı M11 ile tutarlı kalır.
- TT topraklama hesabı RCD eşiğini uygular.
- Tek hat aktarımı pano ve linyeleri üretir.
- `.e4p11` round-trip yük ve linye bilgilerini korur.
- ViewModel çizimsiz akışı çalışır.
- `.e4p15` round-trip yüzey, armatür, güzergâh, direk ve topraklamayı korur.
- Arka plan çizgileri çıkarılır ve sadeleştirilir.
- Seçili yüzeyden güzergâh ve direk üretimi çalışır.

---

## 12. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.Core/Kst.Acoustics.Core.csproj
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj --no-incremental
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen hedef:
- Core build: 0 hata
- App build: 0 hata
- Test paketi: M15 regresyonları dahil geçmeli

---

## 13. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Core değişikliği için `Electric/CevreAydinlatma` ve gerekirse `Electric/Ortak` alanlarını incele.
4. UI değişikliği için `CevreAydinlatmaWindow`, `CevreAydinlatmaViewModel` ve `CevreAydinlatmaPreview` dosyalarını incele.
5. M11 aktarımı değişiyorsa M11 model/JSON beklentilerini kontrol et.
6. DXF veya yüzey değişikliği yapılıyorsa unitless metre varsayımını ve kapalı poligon davranışını koru.
7. Fotometri değişikliği yapılıyorsa LDT/IES testlerini genişlet.
8. Son olarak M15 testlerini ve ilgili App build'i çalıştır.


<!-- ============================================================ -->
# 📄 DOSYA: `elektrik/16-zayif-akim-proje-ciz/codex16.md`
<!-- ============================================================ -->

# M16 Codex — Zayıf Akım Proje Çiz

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. M16, M12 ile **ortak motoru** paylaşan ikinci kabuktur; yalnız zayıf akım disiplininde çalışır. Ayrıntılı kronolojik günlük değildir.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M16 — Zayıf Akım Proje Çiz |
| Amaç | DXF/M50 mimari plan üzerinden zayıf akım eleman yerleşimi, kablolama, merkez özeti ve DXF çıktı |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Proje uzantısı | `.e4p16` |
| Durum | Geliştiriliyor |

---

## 2. Mimari Karar — Ortak Motor, İki Kabuk

> **Ayrışma kararı (2026-06-13):** M12'deki kuvvetli + zayıf akım tek pencerede
> büyüyünce ikiye ayrıldı. M12 = yalnız kuvvetli akım, M16 = yalnız zayıf akım.
> **Kod fork edilmedi.** Core (`BasitProjeCiz`) modelleri/servisleri tektir;
> aynı `BasitProjeCizWindow` + `BasitProjeCizViewModel`, kurucuya verilen
> `BpcDisiplin modulModu` ile modüle sabitlenir.

- M12 girişi: `new BasitProjeCizWindow()` → `BpcDisiplin.KuvvetliAkim`.
- M16 girişi: `new BasitProjeCizWindow(BpcDisiplin.ZayifAkim)` (Electric launcher "Zayıf Akım Proje Tasarımı" kartı).
- `BasitProjeCizViewModel.ModulModu` açılışta bir kez `ModulModuAyarla` ile sabitlenir;
  **çalışma zamanında disiplin değiştirilemez** (`OnAktifDisiplinChanged` sapmayı geri çevirir).
- `ModulKuvvetliMi` / `ModulZayifMi` özellikleri UI görünürlüğünü sürer (sistem seçici,
  Tek Hat/M13 aktarım butonları, mahal listesi disiplin sekmeleri).
- Çapraz görünürlük **yok**: M16 kuvvetli akım elemanlarını göstermez/çizmez.

---

## 3. Disiplin Ayrımının Tek Kaynağı

- `BpcSistemKatalogu` registry'si her sistemin `Disiplin` alanını taşır.
- Eleman/kablo → disiplin sınıflaması: `BpcSistemKatalogu.Disiplini(BpcElemanTipi)`,
  `Disiplini(BpcKablo)`, `Disiplini(BpcPaftaTipi)`. Zayıf akım eleman tipleri katalogdan türetilir;
  listede olmayan her tip kuvvetli akıma düşer (Buat, kablo tavası, topraklama ortak → M12).
- `DisiplinAyiklamaServisi` (Core):
  - `Say(proje)` → kuvvetli/zayıf eleman, kablo, devre sayıları.
  - `Ayikla(proje, hedef)` → diğer disiplinin eleman/kablo/devre/mahal satırlarını siler,
    **geometriyi (kat/oda/açıklık/DXF/M50 bağı) korur**, `ModulDisiplini` damgalar,
    kalan devrelerdeki silinmiş eleman referanslarını arındırır.

---

## 4. Dosya Formatı ve Geçiş

- M16 projeleri `.e4p16` uzantısıyla kaydedilir; `BasitProjeCizProje.ModulDisiplini = ZayifAkim`.
- M16 açılış diyaloğu hem `.e4p16` hem eski `.e4p12` dosyalarını listeler.
- **İçe aktarma:** `ProjeyiYukle` içinde `DisiplinMutabakati` çalışır — yüklenen projede
  modüle ait olmayan içerik varsa `DisiplinAyiklamaServisi.Ayikla` ile ayıklanır ve
  durum çubuğunda uyarı + "'.e4p16' olarak kaydedin" önerisi gösterilir.
- M12 eski karma `.e4p12` açarsa zayıf akımı ayıklayıp uyarır (veri proje seviyesinde temiz kalır).
- DXF çıktısı kat içeriğini gezdiği için ayrım otomatiktir: M16 projesi yalnız zayıf akım
  elemanı taşıdığından çıktıda yalnız zayıf akım katmanları (E4P-DATA/TV/CCTV/...) üretilir.

---

## 5. Kapsam (M12'den miras, zayıf akımla sınırlı)

- Bina geometrisi tüketimi: `.e4bina` salt-okunur yükleme + mutabakat (düzenleme M50'de).
- Mahal listesi + otomatik/manuel eleman yerleştirme (zayıf akım sembolleri).
- Zayıf akım sistemleri: Data, TV, Telefon, CCTV, Acil Anons, Diafon, Yangın Algılama.
- Kablolama topolojileri: BuatDaisy (Data/TV/Telefon), YildizMerkez (CCTV/Anons/Diafon), Loop (Yangın).
- DXF proje çıktısı.
- **M16 → M13 Zayıf Akım Tek Hat aktarımı (tamamlandı):** "📡 M13'e Aktar" butonu
  `ZayifAkimTekHatImportServisi.Donustur(proje)` ile **bellek içi** dönüşüm yapar
  (dosya round-trip yok → serileştirme uyumsuzluğu yok), sonucu M13'ün native
  `.e4p13` formatına (CamelCase + IgnoreCycles) yazar. Kullanıcı M13'te "Proje Aç"
  ile açar. Zayıf akım sistemi yoksa "aktarılacak sistem yok" uyarısı verilir.

---

## 6. Önemli Kaynak Alanları

- Core ortak: `src/Kst.Acoustics.Core/Electric/BasitProjeCiz/` (Models + Services)
- Disiplin motoru: `Services/DisiplinAyiklamaServisi.cs`, `Models/BasitProjeCizModels.cs` (`BpcSistemKatalogu.Disiplini*`)
- UI ortak: `src/Kst.Acoustics.App/Electric/BasitProjeCiz/Views/BasitProjeCizWindow.axaml(.cs)`,
  `ViewModels/BasitProjeCizViewModel*.cs` (`ModulModu`, `ModulModuAyarla`, `DisiplinMutabakati`)
- Giriş: `src/Kst.Acoustics.App/Views/ElectricLauncherWindow.axaml(.cs)` (`OpenZayifAkimProjeCiz_OnClick`)
- Test: `src/Kst.Acoustics.Tests/DisiplinAyiklamaTests.cs`

---

## 7. Korunması Gereken Regresyon Alanları

- M12 penceresi yalnız kuvvetli, M16 yalnız zayıf akım sistemlerini listeler (kabuk sabit).
- Çalışma zamanında disiplin değiştirilemez (sapma `ModulModu`'na geri çevrilir).
- `DisiplinAyiklamaServisi.Ayikla` geometriyi korur, yalnız elektrik içeriğini ayıklar.
- M16 eski `.e4p12` açınca kuvvetli akımı ayıklar, zayıf akımı + mahal satırlarını korur.
- Kaydetme `ModulDisiplini`'ni damgalar; M16 → `.e4p16`, M12 → `.e4p12`.
- Ayıklama, kalan devrelerdeki silinmiş eleman referanslarını temizler.

---

## 8. Doğrulama Komutları

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen durum: build 0 hata; custom runner 309 test geçer (7 disiplin/aktarım testi dahil),
xUnit M13 import testleri 8/8 geçer.


<!-- ============================================================ -->
# 📄 DOSYA: `mekanik/_index.md`
<!-- ============================================================ -->

# Mekanik — Ana Başlık 3

**Modül aralığı:** 21–29  
**Durum:** 1 modül başlatıldı

## Modüller

| # | Klasör | Modül Adı | Durum |
|---|--------|-----------|-------|
| 21 | [21-kanal-hesabi](21-kanal-hesabi/codex21.md) | Kanal Hesabı | Başlatıldı |
| 22–29 | — | _rezerve_ | — |

## Standartlar

ASHRAE Fundamentals, SMACNA HVAC Systems Duct Design, TS EN 16798, TS EN 1505, TS EN 1506

## Kaynak Kod

`../src/Kst.Acoustics.Core/Mechanical/` — mekanik iş mantığı  
`../src/Kst.Acoustics.App/Mechanical/` — UI katmanı


<!-- ============================================================ -->
# 📄 DOSYA: `mekanik/21-kanal-hesabi/codex21.md`
<!-- ============================================================ -->

# M21 Codex — Kanal Hesabı

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, sistem davranışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Mechanics |
| Modül | M21 — Kanal Hesabı |
| Amaç | HVAC hava kanalı boyutlandırma, basınç kaybı ve hat özeti |
| Proje uzantısı | `.e4p21` |
| Standart referansları | ASHRAE Fundamentals, SMACNA HVAC Systems Duct Design, TS EN 16798, TS EN 1505, TS EN 1506 |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Başlatıldı; ürün ailesi ve modül merkezi aktif |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Dikdörtgen ve dairesel kanal kesiti hesabı
- Debi, hız, alan ve eşdeğer çap hesapları
- Hat bazlı kanal segment listesi
- Sürtünme basınç kaybı ve yerel kayıp katsayısı girişi
- Toplam basınç kaybı özeti
- `.e4p21` proje kaydet/aç akışı
- DXF veya tablo çıktısı için veri modeli hazırlığı

İleri/eksik alanlar:
- Fan çalışma noktası ve cihaz seçimi
- Kanal fitting kataloğu
- Kat planı üstünden kanal rotası
- Otomatik metraj ve keşif bağlantısı
- Rapor/PDF çıktı

---

## 3. Genel Model Kuralları

Temel modeller:
- `MekanikKanalProje`
- `KanalHatti`
- `KanalSegmenti`
- `KanalKesiti`
- `KanalBasincKaybiSonucu`

Kurallar:
- İş mantığı `Kst.Acoustics.Core/Mechanical/` altında tutulur.
- UI katmanı yalnızca ViewModel ve görünüm davranışını içerir; hesap motoru Core içinde kalır.
- Tüm görünür metinler Türkçe olmalıdır.
- Debi birimi varsayılan olarak `m3/h`, hesap içi SI karşılığı `m3/s` olmalıdır.
- Uzunluk, genişlik, yükseklik ve çap metrik birimlerde tutulmalıdır.
- Negatif veya sıfır fiziksel değerler validasyon hatası üretmelidir.

---

## 4. Hesap Kuralları

- Kesit alanı dikdörtgen kanalda `genişlik x yükseklik`, dairesel kanalda `pi x çap^2 / 4` ile hesaplanır.
- Hız `debi / alan` üzerinden hesaplanır.
- Eşdeğer çap hesap yöntemi açıkça isimlendirilmelidir.
- Basınç kaybı segment bazında hesaplanmalı, hat toplamı segment toplamlarından türetilmelidir.
- Yerel kayıplar ayrı `K` katsayısı veya katalog öğesi olarak izlenmelidir.
- Büyük hat listelerinde segment aramaları `Dictionary` tabanlı yapılmalıdır.

---

## 5. UI İş Akışı

- Complete Engineering ana ekranından EYE4PRO Mechanics açılır.
- M21 Kanal Hesabı kartı ilk aktif mekanik modül olarak gösterilir.
- İlk uygulama ekranında proje bilgisi, hat listesi, segment tablosu ve sonuç özeti bulunmalıdır.
- Kullanıcı debi ve kesit bilgilerini düzenlediğinde sonuçlar yenilenmelidir.
- Hata ve uyarılar Türkçe, ölçü birimleri açık ve tutarlı gösterilmelidir.

---

## 6. Kritik Dikkat Notları

- Mekanik modüller elektrik M12 içindeki “Mekanik Paftası” ile karıştırılmamalıdır; M12 yalnız elektrik besleme/linye tarafıdır.
- Kanal hesabı HVAC hava tarafı modülüdür.
- İlk sürümde sade ve doğrulanabilir hesap motoru önceliklidir; çizim ve otomatik rota sonraki fazdır.
- AXAML içinde `&` karakteri `&amp;` olarak kaçırılmalıdır.
- Pencere büyüdükçe içerik alanı büyümeli; sabit yükseklikler kullanılmamalıdır.


<!-- ============================================================ -->
# 📄 DOSYA: `mimari/_index.md`
<!-- ============================================================ -->

# Mimari — Ana Başlık 4

**Modül aralığı:** 30–39  
**Durum:** Planlı tüm fazlar (Faz 0-10) tamamlandı — M31-M37 MVP + M50 entegrasyonu (Faz 9) + M39 koordinasyon/ihracat (Faz 10) bitti. Kalan isteğe bağlı: UI bağlama, GLB/DXF/IFC, M38 cephe, M21 köprüsü  
**Ana takip dosyası:** [codex30.md](codex30.md)

## Modüller

| # | Klasör | Modül Adı | Durum | Codex |
|---|--------|-----------|-------|-------|
| 30 | — | Mimari BIM Ana Plan ve Faz Takibi | Başlatıldı | [codex30.md](codex30.md) |
| 31 | 31-ihtiyac-programi-mahal-sablonlari | İhtiyaç Programı ve Mahal Şablonları | MVP tamamlandı | [codex31.md](31-ihtiyac-programi-mahal-sablonlari/codex31.md) |
| 32 | 32-konsept-plan-parametrik-yerlesim | Konsept Plan ve Parametrik Yerleşim | MVP tamamlandı | [codex32.md](32-konsept-plan-parametrik-yerlesim/codex32.md) |
| 33 | 33-dusey-cekirdek-saft-sirkulasyon | Düşey Çekirdek, Şaft ve Sirkülasyon | MVP tamamlandı | [codex33.md](33-dusey-cekirdek-saft-sirkulasyon/codex33.md) |
| 34 | 34-yonetmelik-on-kontrol | Yönetmelik Ön Kontrol Motoru | MVP tamamlandı | [codex34.md](34-yonetmelik-on-kontrol/codex34.md) |
| 35 | 35-3d-bim-model | 3D BIM Model ve Görselleştirme | MVP tamamlandı | [codex35.md](35-3d-bim-model/codex35.md) |
| 36 | 36-detay-malzeme-yapi-elemani | Detay, Malzeme ve Yapı Elemanı | MVP tamamlandı | [codex36.md](36-detay-malzeme-yapi-elemani/codex36.md) |
| 37 | 37-pafta-rapor-cizelgeler | Mimari Pafta, Rapor ve Çizelgeler | MVP tamamlandı | [codex37.md](37-pafta-rapor-cizelgeler/codex37.md) |
| 38 | 38-cephe-gunisigi-kabuk | Cephe, Günışığı ve Kabuk Analizi | Planlandı | codex38.md |
| 39 | 39-bim-koordinasyon-ihracat | BIM Koordinasyon ve İhracat | MVP tamamlandı | [codex39.md](39-bim-koordinasyon-ihracat/codex39.md) |

## Planlama Belgeleri

| Belge | Açıklama |
|-------|----------|
| [codex30.md](codex30.md) | Mimari BIM ana faz takip dosyası; her işlemden sonra güncellenecek |
| [mimari-modul-planlama-raporu.md](mimari-modul-planlama-raporu.md) | Mimari başlık için BIM hedefi, sıfırdan tasarım akışı, yönetmelik ön kontrolü ve önerilen 31–39 modül haritası |


<!-- ============================================================ -->
# 📄 DOSYA: `mimari/codex30.md`
<!-- ============================================================ -->

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


<!-- ============================================================ -->
# 📄 DOSYA: `mimari/mimari-modul-planlama-raporu.md`
<!-- ============================================================ -->

# Mimari BIM Modül Planlama Raporu

> Tarih: 2026-06-12
> Bağlam: EYE4PRO ana modül yapısı, M50 Bina Geometrisi ortak modülü ve mimari ana başlığı 31-39 aralığı.
> Hedef: EYE4PRO içinde DXF içe aktarmaya ek olarak, binayı sıfırdan tasarlayabilen BIM yönlü mimari üretim hattı kurmak.

---

## 1. Yeni Hedef Tanımı

EYE4PRO mimari başlığı yalnız mevcut DXF dosyasını okuyup mahal/alan raporu çıkaran bir yardımcı
olmamalıdır. Hedef, kullanıcının binayı tasarım kararından başlayarak program içinde kurabilmesidir:

1. Bina işlevi seçilir.
2. İhtiyaç programı otomatik önerilir.
3. Mahaller, alanlar, hacimler, kapasite ve ilişki kuralları tanımlanır.
4. Koridor, merdiven, asansör, wc, şaft ve teknik hacimler kurallara göre yerleştirilir.
5. Basit 2D plan ve 3D kütle/BIM model oluşturulur.
6. Yönetmelik ön kontrolleri yapılır.
7. Detay, malzeme, kapı-pencere, cephe ve pafta çalışmaları ayrı modüllerde geliştirilir.
8. Sonunda mimarın ihtiyacı olan çizelge, rapor, metraj ve dışa aktarımlar hazırlanır.

Bu hedefte M50 sadece "DXF yükleme aracı" olarak kalmamalı; EYE4PRO'nun ortak bina modeli
çekirdeği haline gelmelidir. Mimari modüller ise bu çekirdeği akıllı tasarım, kural kontrolü,
detaylandırma ve çıktı üretimi için kullanmalıdır.

---

## 2. Stratejik Mimari Karar

### 2.1 DXF akışı korunmalı

Mevcut akış değerli ve korunmalıdır:

```text
DXF -> M50 Bina Geometrisi -> Akustik / Elektrik / Mekanik / Mimari / Keşif
```

Bu akış dışarıdan gelen projelerde hızlı başlangıç sağlar.

### 2.2 Sıfırdan BIM akışı eklenmeli

Yeni hedef için ikinci akış eklenmelidir:

```text
Mimari İhtiyaç Programı
-> Parametrik Mahal Üretimi
-> Kurallı Plan Yerleşimi
-> M50 Ortak Bina Modeli
-> 3D BIM Model
-> Akustik / Elektrik / Mekanik / Keşif / Rapor
```

Bu ikinci akışta kullanıcı binayı EYE4PRO içinde kurar. DXF artık zorunlu başlangıç değil,
yalnız alternatif veri girişidir.

### 2.3 Dosya stratejisi

Mevcut `.e4bina.json` yapısı genişletilmelidir; fakat diğer modülleri kırmamak için iki katmanlı
yaklaşım önerilir:

| Dosya | Amaç |
|---|---|
| `.e4bina.json` | Ortak bina modeli: katlar, mahaller, açıklıklar, çekirdekler, şaftlar, merdivenler, temel 3D hacimler |
| `.e4mimari.json` | Mimari tasarım kararları: ihtiyaç programı, mahal şablonları, kurallar, malzemeler, detay kararları, rapor ayarları |

Akustik, elektrik ve mekanik modüller öncelikle `.e4bina.json` okur. Mimari modül daha zengin
tasarım bilgisini `.e4mimari.json` içinde tutar.

---

## 3. Bir BIM Mimari Modülünün Çözmesi Gereken İşler

### 3.1 Bina işlevi ve proje tipi kararı

Kullanıcı işe "hangi bina tasarlanıyor?" sorusuyla başlamalıdır. Örnek bina işlevleri:

- Konut / apartman
- Villa / müstakil yapı
- Ofis
- Eğitim yapısı
- Sağlık yapısı
- Otel / konaklama
- Kültür merkezi / konferans salonu
- Ticari yapı
- Endüstri / depo
- Karma kullanım

Bu seçim daha sonra mahal listesi, koridor genişliği, wc sayısı, merdiven ihtiyacı, yangın kaçış
yaklaşımı, otopark, erişilebilirlik ve mekanik/elektrik ön kabullerini etkiler.

### 3.2 İhtiyaç programı üretimi

Program kullanıcıdan kapasite ve kullanım bilgilerini almalıdır:

- Kullanıcı/kişi sayısı
- Daire/oda/ofis/derslik/yatak sayısı
- Kat adedi ve yaklaşık kat yüksekliği
- Hedef toplam alan
- Servis alanı oranı
- Sirkülasyon oranı
- Teknik hacim ihtiyacı
- Islak hacim ihtiyacı
- Otopark/servis/yangın/erişilebilirlik özel koşulları

Sistem bu bilgilerle önerilen mahal programı üretir.

### 3.3 Mahal şablon kütüphanesi

Her mahal tipi için bir şablon olmalıdır. Şablon yalnız isim değil, tasarım bilgisi taşımalıdır:

```text
MahalSablonu
├── MahalTipi
├── VarsayilanAd
├── MinimumAlanM2
├── IdealAlanM2
├── MinimumNetYukseklikM
├── KisiKapasitesi
├── EnBoyOraniAraligi
├── GerekliKomşuluklar
├── UzakDurulacakMahaller
├── GerekliAciklikTipleri
├── HavalandirmaAydinlatmaIhtiyaci
├── AkustikOncelik
├── ElektrikOncelik
├── MekanikOncelik
└── VarsayilanMalzemeSeti
```

Örnek mahal tipleri:

- Salon, yatak odası, çocuk odası, mutfak, banyo, wc, antre, hol, depo.
- Açık ofis, yönetici odası, toplantı odası, resepsiyon, arşiv, server odası.
- Derslik, laboratuvar, atölye, öğretmen odası, kütüphane, konferans salonu.
- Muayene, bekleme, danışma, steril alan, personel alanı.
- Restoran, mutfak, çamaşırhane, teknik oda, yükleme alanı.
- Merdiven, asansör, yangın merdiveni, rampa, şaft, elektrik odası, mekanik oda.

### 3.4 Mahal ilişki matrisi

BIM tasarımın akıllı olabilmesi için mahaller arası ilişkiler tanımlanmalıdır:

| İlişki | Örnek |
|---|---|
| Yakın olmalı | mutfak-servis, konferans salonu-foye, yatak odası-banyo |
| Aynı aksta/üst üste olmalı | wc, ıslak hacim, tesisat şaftı, merdiven, asansör |
| Ayrı tutulmalı | gürültülü teknik hacim-yatak odası, servis alanı-temsil alanı |
| Dış cephe almalı | yatak odası, ofis, derslik |
| İç bölgede kalabilir | depo, şaft, teknik hacim |
| Ana dolaşıma bağlı olmalı | merdiven, asansör, yangın çıkışı, wc |

Bu ilişki matrisi ileride otomatik plan yerleşiminin ana kural seti olur.

### 3.5 Koridor ve sirkülasyon üretimi

Kullanıcı "koridor genişliği" gibi değerleri elle girebilmelidir; ama sistem bina işlevine ve
kapasiteye göre öneri de üretmelidir.

Gerekli kabiliyetler:

- Kullanım tipine göre koridor genişliği önerisi.
- Kat bazlı ana koridor / tali koridor ayrımı.
- Engelli erişimi için minimum geçiş uyarıları.
- Kaçış yolu olarak kullanılan koridorların yangın ön kontrolüne bağlanması.
- Koridorun mahal alanı mı sirkülasyon alanı mı sayılacağının belirlenmesi.

Koridorlar yalnız çizgi değil, mahal ve dolaşım grafı olarak tutulmalıdır. Böylece yangın kaçış,
erişilebilirlik ve elektrik/mekanik yerleşim analizleri aynı veriden beslenir.

### 3.6 Düşey çekirdek ve aks sürekliliği

Merdiven, asansör, wc ve şaftlar BIM modelinde özel eleman olmalıdır. Bunlar sıradan oda gibi
tutulursa yönetmelik ve koordinasyon kontrolleri zayıf kalır.

Gerekli kurallar:

- Merdivenler katlar arasında aynı aksta devam etmeli.
- Asansör kuyuları aynı aksta devam etmeli.
- Tesisat şaftları üst üste gelmeli.
- Wc/ıslak hacimlerin mümkün olduğunca aynı düşey hatta toplanması önerilmeli.
- Katlar arasında aks kaçıklığı varsa kullanıcıya uyarı verilmeli.
- Şaftın geçtiği katlar ve hizmet ettiği mahaller listelenmeli.

Bu yapı M12 elektrik, M21 mekanik ve M14 keşif için büyük değer üretir.

### 3.7 Basit yapı ve 3D aktarım

İlk BIM hedefi karmaşık serbest modelleme değil, güvenilir bir parametrik yapı üretimi olmalıdır:

- Katlar
- Döşemeler
- Duvarlar
- Tavanlar
- Açıklıklar
- Merdiven/asansör/şaft çekirdekleri
- Mahallerin 3D hacimleri
- Basit çatı/kütle
- Cephe açıklıkları

İlk 3D model "görsel ve koordinasyon amaçlı" olmalıdır. Sonraki fazlarda IFC/glTF/OBJ dışa
aktarımı düşünülebilir.

### 3.8 Detay ve malzeme çalışması

Detay çalışması ayrı bir mimari modül olmalıdır. Bu modül şu işleri yapar:

- Duvar tipi seçimi.
- Döşeme/tavan tipi seçimi.
- Zemin, duvar, tavan finisajı.
- Kapı/pencere/doğrama tipleri.
- Islak hacim malzeme setleri.
- Yangın dayanımı, akustik performans, ısı geçirgenliği gibi teknik özellikler.
- Mahal tipine göre varsayılan malzeme önerisi.

Malzeme kütüphanesi yalnız mimari görünüş değil, diğer disiplinlerin kullanacağı teknik veri de
taşımalıdır:

```text
MalzemeYapiElemani
├── Kod
├── Ad
├── Kategori
├── KullanimYeri
├── YanginSinifi
├── YanginDayanimDakika
├── AkustikRw
├── SesYutmaNrc
├── IsilIletkenlik
├── UDegeri
├── KaymazlikSinifi
├── IslakHacimUygun
├── DisMekanUygun
├── BakimNotu
└── BirimMetrajTipi
```

---

## 4. Yönetmelik ve Standart Ön Kontrol Kapsamı

Bu bölümde amaç, programın resmi ruhsat onayı verdiğini iddia etmek değildir. Amaç, mimara
tasarım sırasında erken uyarı veren parametrik bir ön kontrol sistemi kurmaktır.

### 4.1 Öncelikli yönetmelik/kural başlıkları

Programın mimari BIM tarafında takip etmesi gereken ana başlıklar:

| Başlık | Programda kullanılacağı yer |
|---|---|
| Planlı Alanlar İmar Yönetmeliği | bina yüksekliği, kat, çekme mesafesi, kullanım, emsal/alan, yapı yaklaşımı |
| Binaların Yangından Korunması Hakkında Yönetmelik | kaçış yolları, merdiven, yangın kompartımanı, kapı, koridor, duman kontrolü ön uyarıları |
| Otopark Yönetmeliği | kullanım tipine göre otopark ihtiyacı ön hesabı |
| Türkiye Bina Deprem Yönetmeliği | taşıyıcı sistemle koordinasyon, düzensizlik/kat bilgisi, mimari eleman uyarıları |
| Erişilebilirlik mevzuatı ve TS 9111 | engelli erişimi, kapı/koridor/rampa/wc ön kontrolleri |
| Binalarda Enerji Performansı Yönetmeliği | kabuk, cephe, enerji kimlik belgesi için ön veri |
| Binalarda Su Yalıtımı Yönetmeliği | ıslak hacim, bodrum, çatı, teras detay uyarıları |
| Sığınak Yönetmeliği | yapı tipine göre sığınak ihtiyacı kontrolü |
| Asansör Yönetmeliği ve ilgili TS EN 81 standartları | asansör alanı, erişilebilirlik ve düşey çekirdek kararları |
| Yapı Malzemeleri Yönetmeliği | malzeme performans beyanı ve teknik sınıflar |
| Gürültüye karşı koruma mevzuatı | akustik modül ile mahal tipi ve yapı elemanı koordinasyonu |

### 4.2 Resmi kaynak takip listesi

Uygulamada değerler hard-code edilmemeli; kural setleri güncellenebilir olmalıdır. İlk kaynak
takip listesi:

- Planlı Alanlar İmar Yönetmeliği, Resmî Gazete 03.07.2017 / 30113:
  https://www.resmigazete.gov.tr/eskiler/2017/07/20170703-8.htm
- Binaların Yangından Korunması Hakkında Yönetmelik, Resmî Gazete 19.12.2007 / 26735:
  https://www.resmigazete.gov.tr/eskiler/2007/12/20071219-2.htm
- Otopark Yönetmeliği, Resmî Gazete 22.02.2018 / 30340:
  https://www.resmigazete.gov.tr/eskiler/2018/02/20180222-7.htm
- Türkiye Bina Deprem Yönetmeliği, Resmî Gazete 18.03.2018 / 30364 mükerrer:
  https://www.resmigazete.gov.tr/eskiler/2018/03/20180318M1-2.htm

Not: Resmî Gazete içerikleri program geliştirme aşamasında madde madde doğrulanmalı ve uygulama
içindeki kural değerleri sürüm bilgisiyle saklanmalıdır. Yerel belediye koşulları ve güncel mevzuat
değişiklikleri kullanıcı tarafından seçilebilir profil olarak yönetilmelidir.

### 4.3 Kural motoru yaklaşımı

Kural motoru şu yapıda olmalıdır:

```text
KuralSeti
├── Kod
├── Ad
├── Kaynak
├── SurumTarihi
├── ProjeTipleri
├── Kurallar[]
└── KullaniciParametreleri

Kural
├── Kod
├── Aciklama
├── Seviye: Bilgi/Uyari/Kritik
├── GirdiTipleri
├── Kosul
├── SonucMesaji
└── CozumOnerisi
```

Kural sonuçları "uygun / uyarı / kritik / kontrol edilemedi" olarak ayrılmalıdır. "Kontrol edilemedi"
sonucu özellikle önemlidir; çünkü bazı mevzuat maddeleri yalnız geometriden değil, proje notu,
yerel imar durumu veya statik/mekanik veriyle değerlendirilebilir.

---

## 5. Önerilen Mimari Modül Haritası

Mimari ana başlık 31-39 aralığı BIM hedefiyle şöyle kurgulanmalıdır:

| # | Modül | Amaç | Öncelik |
|---|---|---|---|
| 31 | İhtiyaç Programı ve Mahal Şablonları | bina işlevi, kapasite, mahal listesi, alan/hacim hedefleri | 1 |
| 32 | Konsept Plan ve Parametrik Yerleşim | mahalleri plana yerleştirme, koridor/çekirdek üretimi, ilişki matrisi | 1 |
| 33 | Düşey Çekirdek, Şaft ve Sirkülasyon | merdiven, asansör, wc, şaft aks sürekliliği ve kaçış grafı | 1 |
| 34 | Yönetmelik Ön Kontrol Motoru | imar, yangın, erişilebilirlik, otopark, enerji, sığınak ön kontrolleri | 1 |
| 35 | 3D BIM Model ve Görselleştirme | kat/duvar/döşeme/açıklık hacimleri, 3D önizleme, dışa aktarım | 2 |
| 36 | Detay, Malzeme ve Yapı Elemanı | duvar/döşeme/tavan/kapı/pencere tipleri, standart uyumlu malzeme kütüphanesi | 2 |
| 37 | Mimari Pafta, Rapor ve Çizelgeler | mahal listesi, alan tablosu, kapı-pencere, finisaj, yönetmelik raporu | 2 |
| 38 | Cephe, Günışığı ve Kabuk Analizi | cephe açıklıkları, yönlenme, kabuk kararları, enerji/akustik ön veri | 3 |
| 39 | BIM Koordinasyon ve İhracat | elektrik/akustik/mekanik/keşif veri mutabakatı, IFC/glTF/DXF çıktı hedefleri | 3 |

Bu yapı, mimariyi EYE4PRO'nun diğer disiplinlerine veri veren ana tasarım katmanı yapar.

---

## 6. M31 - İhtiyaç Programı ve Mahal Şablonları

### 6.1 Amaç

Kullanıcının binanın işlevini seçip ihtiyaç listesini hızlıca oluşturmasını sağlamak.

### 6.2 Kullanıcı akışı

1. Yeni mimari proje açılır.
2. Bina işlevi seçilir.
3. Kapasite bilgileri girilir.
4. Sistem mahal ihtiyaç listesini önerir.
5. Kullanıcı metrekare, hacim, adet ve öncelikleri düzenler.
6. Sistem net/brüt alan, sirkülasyon ve teknik hacim hedeflerini hesaplar.
7. Program M32'ye aktarılır.

### 6.3 Üretilecek veri

```text
IhtiyacProgrami
├── BinaIslevi
├── ProjeTipi
├── Kapasite
├── HedefToplamAlan
├── MahalGereksinimleri[]
├── SirkulasyonOrani
├── TeknikAlanOrani
└── Notlar
```

---

## 7. M32 - Konsept Plan ve Parametrik Yerleşim

### 7.1 Amaç

M31'den gelen mahal listesini plan şemasına dönüştürmek.

### 7.2 Temel kabiliyetler

- Mahal bloklarını otomatik veya yarı otomatik yerleştirme.
- Koridorları dolaşım grafı olarak üretme.
- Dış cephe isteyen mahalleri cepheye yerleştirme.
- Islak hacimleri ve şaftları çekirdeğe bağlama.
- Mahaller arası yakınlık/uzaklık kurallarını kontrol etme.
- Kullanıcı sürükle-bırak düzenleme yaptığında kuralları tekrar çalıştırma.
- M50'ye `BinaOda`, `BinaKat`, `BinaAciklik`, `BinaCekirdek` olarak aktarma.

### 7.3 Önemli karar

M32 serbest çizim programı gibi başlamamalıdır. İlk sürümde blok tabanlı çalışmalı:

```text
Mahal gereksinimi -> dikdörtgen/çokgen blok -> koridor bağlantısı -> kat planı -> M50 geometri
```

Bu yaklaşım hızlı ürün verir ve kurallı tasarımı mümkün kılar.

---

## 8. M33 - Düşey Çekirdek, Şaft ve Sirkülasyon

### 8.1 Amaç

Binanın katlar arası çalışan çekirdek sistemini yönetmek.

### 8.2 Kapsam

- Ana merdiven.
- Yangın merdiveni.
- Asansör.
- Rampa.
- Tesisat şaftı.
- Elektrik şaftı.
- Mekanik şaft.
- Wc/ıslak hacim düşey hizası.
- Kaçış güzergahı grafı.

### 8.3 Kritik kontroller

- Merdiven ve asansör tüm gerekli katlarda devam ediyor mu?
- Şaftlar aynı aks üzerinde mi?
- Wc/ıslak hacimler şaftla ilişkili mi?
- Kaçış yolu kesintiye uğruyor mu?
- Kapı/koridor/merdiven ilişkisi yangın ön kontrolünde uyarı veriyor mu?

---

## 9. M34 - Yönetmelik Ön Kontrol Motoru

### 9.1 Amaç

Tasarım sürecinde mimara erken uyarı vermek.

### 9.2 İlk sürüm kontrolleri

- Adsız veya sınıflandırılmamış mahal.
- Minimum alan/ölçü hedefinden sapan mahal.
- Kapısız mahal.
- Dış cephe gerektirip cephe almayan mahal.
- Aynı aksta devam etmeyen merdiven/asansör/şaft.
- Kapasiteye göre eksik wc veya servis mahali.
- Koridor genişliği kullanıcı kural setine göre yetersiz.
- Erişilebilir wc/rampa/asansör gerekliliği için ön uyarı.
- Yangın kaçış güzergahı oluşturulamıyor uyarısı.
- Otopark/sığınak/teknik alan için "kontrol gerekli" uyarısı.

### 9.3 Dil

Uygulama "kesin uygundur" dememelidir. Doğru dil:

- "Ön kontrol uygun görünüyor."
- "Kullanıcı kural setine göre uyarı var."
- "Bu kontrol için ek veri gerekiyor."
- "Güncel yerel imar koşullarıyla doğrulayın."

---

## 10. M35 - 3D BIM Model ve Görselleştirme

### 10.1 Amaç

2D planı basit ama tutarlı 3D modele dönüştürmek.

### 10.2 İlk 3D kapsam

- Kat döşemeleri.
- Duvar prizması.
- Mahal hacimleri.
- Kapı/pencere boşlukları.
- Merdiven/asansör/şaft hacimleri.
- Çatı/kütle basit gösterimi.
- Kat kesiti ve mahal yüksekliği.

### 10.3 Dışa aktarım hedefleri

İlk sürümde görsel ve koordinasyon yeterlidir. Sonra:

- glTF: hızlı 3D önizleme ve paylaşım.
- OBJ: basit geometri dışa aktarımı.
- DXF: 2D plan çıktısı.
- IFC: uzun vadeli BIM veri değişimi hedefi.

---

## 11. M36 - Detay, Malzeme ve Yapı Elemanı

### 11.1 Amaç

Mimari elemanları standartlara ve disiplinler arası kullanıma uygun teknik nesnelere dönüştürmek.

### 11.2 Kapsam

- Duvar tipleri.
- Döşeme tipleri.
- Tavan tipleri.
- Kapı/pencere/doğrama tipleri.
- Islak hacim detay setleri.
- Cephe kaplama ve yalıtım setleri.
- Yangın, akustik, ısı ve su yalıtımı parametreleri.

### 11.3 Diğer modüllere katkı

- Akustik: duvar/kapı/pencere Rw ve ses yutma bilgisi.
- Elektrik: mahal tipi ve tavan/duvar malzemesine göre yerleşim varsayımları.
- Mekanik: hacim, kullanım tipi, ısı kaybı/kazancı için ön veri.
- Keşif: malzeme metrajları ve poz başlangıçları.

---

## 12. M37 - Pafta, Rapor ve Çizelgeler

Mimarın ihtiyaç duyacağı çıktı ailesi:

- İhtiyaç programı raporu.
- Mahal listesi.
- Net/brüt alan tablosu.
- Kat bazlı alan özeti.
- Kapı-pencere çizelgesi.
- Mahal finisaj çizelgesi.
- Malzeme/yapı elemanı listesi.
- Yönetmelik ön kontrol raporu.
- Revizyon fark raporu.
- 2D plan çıktısı.
- 3D görsel/koordinasyon çıktısı.
- CSV/XLSX/HTML/PDF dışa aktarım.

---

## 13. M50 ile Yeni İlişki

M50'nin rolü genişletilmelidir.

### 13.1 M50'de kalacak/geçecek temel işler

- DXF içe aktarma.
- Sıfırdan oluşturulan mimari planı bina modeline alma.
- Kat, oda, açıklık, duvar, döşeme, tavan, merdiven, asansör, şaft temel geometrisi.
- Id kararlılığı.
- Revizyon numarası.
- `.e4bina.json` kaydet/aç.
- Modüllere salt-okunur bina modeli sağlama.

### 13.2 Mimari modüllerde kalacak işler

- İhtiyaç programı.
- Mahal şablonları.
- Tasarım kural setleri.
- Parametrik yerleşim.
- Yönetmelik ön kontrolü.
- Detay/malzeme.
- 3D görselleştirme ve rapor.

Bu ayrım sayesinde M50 ortak çekirdek olur; mimari ise bu çekirdeğin akıllı tasarım katmanı olur.

---

## 14. Önerilen Core Veri Modeli Genişletmeleri

Mevcut `Core/Bina` modeli BIM hedefi için genişletilmelidir.

```text
BinaModeli
├── SemaSurum
├── GeometriRevizyon
├── ProjeBilgisi
├── ParselBilgisi
├── BinaIslevi
├── Katlar[]
├── Mahaller[]
├── Duvarlar[]
├── Dosemeler[]
├── Tavanlar[]
├── Acikliklar[]
├── Merdivenler[]
├── Asansorler[]
├── Saftlar[]
├── Cekirdekler[]
├── MalzemeAtamalari[]
└── KuralKontrolSonuclari[]
```

Yeni mimari domain için:

```text
MimariTasarim
├── IhtiyacProgrami
├── MahalSablonlari[]
├── IliskiMatrisi
├── YerlesimKurallari
├── YonetmelikProfili
├── MalzemeKutuphaneBaglantilari
├── PaftaAyarları
└── RaporAyarları
```

---

## 15. Uygulama Fazları

### Faz 1 - BIM programlama temeli

- `mimari/31-ihtiyac-programi-mahal-sablonlari/codex31.md` açılır.
- Bina işlevi ve kapasite ekranı yapılır.
- Mahal şablon kütüphanesi oluşturulur.
- İhtiyaç programı raporu üretilir.

### Faz 2 - Parametrik plan üretimi

- M32 konsept plan editörü başlar.
- Mahal blokları, koridorlar ve çekirdekler 2D olarak yerleştirilir.
- M50'ye aktarılabilen `BinaModeli` üretilir.

### Faz 3 - Çekirdek ve yönetmelik ön kontrolü

- M33 düşey çekirdek/şaft modeli eklenir.
- M34 kural motoru başlar.
- Merdiven, koridor, wc, şaft, erişilebilirlik ve yangın ön uyarıları eklenir.

### Faz 4 - 3D BIM model

- M35 ile duvar/döşeme/açıklık hacimleri üretilir.
- 3D önizleme ve basit dışa aktarım yapılır.

### Faz 5 - Detay ve malzeme

- M36 malzeme ve yapı elemanı kütüphanesi kurulur.
- Yangın, akustik, ısı, su yalıtımı ve metraj parametreleri bağlanır.

### Faz 6 - Rapor ve disiplin koordinasyonu

- M37 rapor/pafta çıktıları tamamlanır.
- M39 ile akustik, elektrik, mekanik ve keşif modüllerine veri aktarımı standartlaştırılır.

---

## 16. Net Öneri

Önceki rapordaki "M31 - Mahal Programı ve Alan Kontrolü" önerisi genişletilmelidir.
Yeni ilk modül şu olmalıdır:

```text
mimari/31-ihtiyac-programi-mahal-sablonlari/codex31.md
```

İlk MVP kapsamı:

- Bina işlevi seçimi.
- Kapasite ve hedef alan girişi.
- Mahal ihtiyaç listesi üretimi.
- Mahal şablon kütüphanesi.
- Alan/hacim hedefleri.
- Sirkülasyon/teknik alan oranı.
- Yönetmelik profili seçimi.
- İhtiyaç programı raporu.

İkinci adım hemen M32 olmalıdır:

```text
mimari/32-konsept-plan-parametrik-yerlesim/codex32.md
```

Bu modül mahalleri blok plan haline getirir, koridorları ve çekirdeği üretir, sonra M50 ortak
bina modeline aktarır.

Bu yol izlenirse EYE4PRO, yalnız DXF tüketen bir disiplin koordinasyon aracı olmaktan çıkar;
binayı programdan tasarlayan, yönetmelik ön kontrolü yapan, 3D BIM modele dönüştüren ve diğer
disiplinlere aynı veri omurgasını dağıtan bir mimari BIM platformuna evrilir.



<!-- ============================================================ -->
# 📄 DOSYA: `mimari/31-ihtiyac-programi-mahal-sablonlari/codex31.md`
<!-- ============================================================ -->

# CODEX 31 — İhtiyaç Programı ve Mahal Şablonları

> **Durum:** Başlatıldı (2026-06-12)
> **Bağlı ana takip:** [../codex30.md](../codex30.md)
> **Üst hedef:** EYE4PRO mimari BIM akışında bina işlevi, kapasite, mahal şablonları ve
> ihtiyaç programını üretmek.
> **M50 durumu:** M50 entegrasyonu bu modülde yapılmaz. M31 yalnız `.e4mimari.json` ve
> `Core/Architecture` domain modeli üzerinde çalışır.

---

## 0. Kullanım Protokolü

M31 üzerinde çalışırken sıra:

1. `maincodex.md` oku.
2. `mimari/codex30.md` oku.
3. Bu dosyayı oku.
4. Aktif adımı uygula.
5. Her tamamlanan işlemden sonra hem bu dosyayı hem gerekiyorsa `mimari/codex30.md` dosyasını güncelle.
6. Kullanıcı onayı olmadan bir sonraki adıma geçme.

---

## 1. Amaç

Kullanıcı yeni bir mimari BIM projesine başladığında önce "ne tasarlanıyor?" sorusuna cevap verir.
M31 bu cevaptan hareketle ihtiyaç programını üretir:

- Bina işlevi seçimi.
- Proje tipi ve kapasite bilgileri.
- Mahal şablonlarının bina işlevine göre filtrelenmesi.
- Mahal gereksinimlerinin otomatik üretilmesi.
- Net alan, sirkülasyon alanı, teknik alan ve brüt alan hedeflerinin hesaplanması.
- Kullanıcının mahal adet/alan/hacim değerlerini düzenleyebilmesi.
- `.e4mimari.json` içinde kalıcı saklama.
- İlk rapor çıktısı.

M31 çizim yapmaz. Konsept plan üretimi M32'ye bırakılır.

---

## 2. Mevcut Altyapı

Faz 1'de hazır olan sınıflar:

```text
src/Kst.Acoustics.Core/Architecture/
├── Models/
│   ├── BinaIslevi.cs
│   ├── MahalTipi.cs
│   ├── KullanimSinifi.cs
│   ├── MimariProje.cs
│   ├── IhtiyacProgrami.cs
│   ├── MahalGereksinimi.cs
│   └── MahalSablonu.cs
├── Services/
│   └── MimariProjeServisi.cs
└── Rules/
    ├── KontrolSeviyesi.cs
    ├── MimariKontrolSonucu.cs
    └── MimariProjeDogrulamaServisi.cs
```

Core-only test runner:

```text
src/Kst.Acoustics.Core.Tests/
```

---

## 3. Önerilen Kod Konumu

```text
src/Kst.Acoustics.Core/Architecture/Templates/
├── BinaIsleviTanimlari.cs
├── VarsayilanMahalSablonlari.cs
└── MahalSablonuFiltreServisi.cs

src/Kst.Acoustics.Core/Architecture/Services/
├── IhtiyacProgramiUretimServisi.cs
└── AlanOzetiServisi.cs

src/Kst.Acoustics.App/Architecture/IhtiyacProgrami/
├── ViewModels/
│   └── IhtiyacProgramiViewModel.cs
└── Views/
    └── IhtiyacProgramiWindow.axaml
```

UI fazına gelene kadar Core tarafı bağımsız ilerler.

---

## 4. Veri Kararları

### 4.1 Bina işlevi tanımı

`BinaIslevi` enum tek başına yeterli değildir; UI ve şablon üretimi için insan okunur tanım gerekir:

```text
BinaIsleviTanim
├── Islev
├── Ad
├── Aciklama
├── VarsayilanSirkulasyonOrani
├── VarsayilanTeknikAlanOrani
├── VarsayilanKatYuksekligiM
└── KapasiteEtiketi
```

Örnek kapasite etiketleri: kişi sayısı, daire sayısı, oda sayısı, yatak sayısı, öğrenci sayısı.

### 4.2 Mahal şablonu

Faz 1'deki `MahalSablonu` kullanılacak. M31, varsayılan şablon kütüphanesini doldurur.

İlk sürümde zorunlu minimum şablon aileleri:

- Konut / villa
- Ofis
- Eğitim
- Sağlık
- Konaklama
- Kültür / konferans
- Ticari / restoran
- Endüstri / depo

### 4.3 İhtiyaç programı üretimi

Üretim algoritması ilk sürümde deterministik ve açıklanabilir olacak:

1. Bina işlevine uygun şablonları seç.
2. Kullanıcı kapasitesi varsa kişi başına alanı uygula.
3. Minimum/ideal/hedef alanları hesapla.
4. Zorunlu mahalleri otomatik ekle.
5. Opsiyonel mahalleri işaretli ama pasif olarak sunmaya hazırla.
6. Net alan toplamını hesapla.
7. Sirkülasyon ve teknik alan oranlarını uygula.

---

## 5. M31 Küçük Faz Listesi

### M31.1 — Codex ve klasör açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.1.1 | `mimari/31-ihtiyac-programi-mahal-sablonlari/` klasörü oluştur | Klasör var | ✅ Tamamlandı |
| 31.1.2 | `codex31.md` oluştur | Bu dosyada amaç, kapsam ve küçük fazlar var | ✅ Tamamlandı |
| 31.1.3 | `mimari/_index.md` bağlantısını güncelle | M31 codex linki çalışır | ✅ Tamamlandı |
| 31.1.4 | `codex30.md` 2.1 durumunu güncelle | 2.1 tamamlandı, 2.2 onay bekliyor | ✅ Tamamlandı |

### M31.2 — Varsayılan bina işlevleri

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.2.1 | `BinaIsleviTanim` modelini ekle | Ad/açıklama/oran/yükseklik/kapasite etiketi alanları var | ✅ Tamamlandı |
| 31.2.2 | `BinaIsleviTanimlari` katalog servisini ekle | Enum değerleri için Türkçe tanımlar döner | ✅ Tamamlandı |
| 31.2.3 | Core test: tüm tanımlar benzersiz | Bina işlevi kodları tekrarsız | ✅ Tamamlandı |
| 31.2.4 | Core test runner'a bağla | `Kst.Acoustics.Core.Tests` geçer | ✅ Tamamlandı |

### M31.3 — Mahal şablon kütüphanesi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.3.1 | `VarsayilanMahalSablonlari` servisini ekle | İlk sekiz bina işlevi için şablonlar var | ✅ Tamamlandı |
| 31.3.2 | Konut/villa şablonları | Salon, oda, mutfak, banyo, wc, antre, koridor, depo vb. var | ✅ Tamamlandı |
| 31.3.3 | Ofis şablonları | Açık ofis, toplantı, yönetici, resepsiyon, arşiv, server, wc vb. var | ✅ Tamamlandı |
| 31.3.4 | Eğitim/sağlık/kültür/ticari/depo şablonları | Her işlev için temel mahal seti var | ✅ Tamamlandı |
| 31.3.5 | Core test: şablonlar doğrulanır | Negatif alan/yükseklik yok, tipler tanımlı | ✅ Tamamlandı |

### M31.4 — İhtiyaç programı üretici

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.4.1 | `IhtiyacProgramiUretimServisi` ekle | Bina işlevi + kapasiteden program üretir | ✅ Tamamlandı |
| 31.4.2 | Kapasiteye bağlı alan hesabı | `KisiBasinaAlanM2` olan şablonlar kapasiteyle ölçeklenir | ✅ Tamamlandı |
| 31.4.3 | Varsayılan oran aktarımı | Sirkülasyon/teknik alan/kat yüksekliği bina işlevinden gelir | ✅ Tamamlandı |
| 31.4.4 | Core test: ofis programı | Ofis işlevi açık ofis/toplantı/wc/teknik gibi mahaller üretir | ✅ Tamamlandı |
| 31.4.5 | Core test: konferans programı | Konferans salonu/foye/wc/teknik mahaller üretir | ✅ Tamamlandı |

### M31.5 — Alan özeti

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.5.1 | `AlanOzeti` modeli ekle | Net, sirkülasyon, teknik, brüt, hedef fark alanları var | ✅ Tamamlandı |
| 31.5.2 | `AlanOzetiServisi` ekle | `IhtiyacProgrami` üstünden özet çıkarır | ✅ Tamamlandı |
| 31.5.3 | Kullanım sınıfı kırılımı | Ana kullanım, sirkülasyon, teknik vb. gruplanır | ✅ Tamamlandı |
| 31.5.4 | Core test: alan toplamları | Net/brüt hesabı beklenen değerleri verir | ✅ Tamamlandı |

### M31.6 — UI iskeleti

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.6.1 | App klasör iskeleti | `App/Architecture/IhtiyacProgrami` oluşur | ✅ Tamamlandı |
| 31.6.2 | ViewModel iskeleti | Bina işlevi, kapasite, mahal listesi ve alan özeti property'leri var | ✅ Tamamlandı |
| 31.6.3 | Pencere iskeleti | Türkçe etiketli basit Avalonia pencere açılır | ✅ Tamamlandı |
| 31.6.4 | Ana menü/giriş bağlantısı | Mimari başlık altında M31 açılır | ✅ Tamamlandı |
| 31.6.5 | UI build | `dotnet build` App tarafındaki mevcut hatalar giderildiyse geçer; değilse durum not edilir | ✅ Tamamlandı |

### M31.7 — Düzenleme ve kaydet/aç

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.7.1 | Mahal satırı düzenleme | Adet/alan/yükseklik/kapasite değişince özet güncellenir | ✅ Tamamlandı |
| 31.7.2 | Yeni/kopyala/sil mahal | Kullanıcı programı elle revize edebilir | ✅ Tamamlandı |
| 31.7.3 | `.e4mimari.json` kaydet/aç UI | `MimariProjeServisi` UI'dan kullanılır | ✅ Tamamlandı |
| 31.7.4 | Doğrulama paneli | `MimariProjeDogrulamaServisi` sonuçları gösterilir | ✅ Tamamlandı |

### M31.8 — İlk rapor

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.8.1 | HTML rapor servisi | Proje bilgisi, mahal listesi ve alan özeti HTML çıkar | ✅ Tamamlandı |
| 31.8.2 | CSV dışa aktarım | Mahal listesi CSV olarak kaydedilir | ✅ Tamamlandı |
| 31.8.3 | Core test: rapor boş değil | HTML/CSV temel içerikleri üretir | ✅ Tamamlandı |

### M31.9 — Regresyon ve kapanış

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.9.1 | Core test runner | `Kst.Acoustics.Core.Tests` geçer | ✅ Tamamlandı |
| 31.9.2 | Ana codex güncellemesi | `codex30.md`, `codex31.md`, `_index.md` tutarlı | ✅ Tamamlandı |
| 31.9.3 | M31 MVP kapanış | Sonraki faza geçiş için kullanıcı onayı beklenir | ✅ Tamamlandı |

---

## 6. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M31 MVP tamamlandı; sonraki faz için kullanıcı onayı bekleniyor |
| Sonraki Adım | Ana plan sırasına göre Faz 3.1 yönetmelik/kural altyapısı açılışı veya kullanıcı tercihiyle M32 konsept plan geçişi |
| Kod Durumu | M31 Core servisleri, UI iskeleti, canlı mahal düzenleme, `.e4mimari.json` kaydet/aç, doğrulama paneli, HTML rapor ve CSV dışa aktarım eklendi |
| Test Durumu | `Kst.Acoustics.Core.Tests` 16/16 geçti; Core test build 0 uyarı, 0 hata; App build 0 hata ve mevcut Electric XAML dosyalarından 4 uyarı verdi; App `--no-incremental` build 0 hata ve mevcut 5 uyarı verdi |

---

## 7. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-12 | M31 ilk programlanacak mimari BIM modülü olarak açıldı |
| 2026-06-12 | M31 çizim/yerleşim yapmayacak; konsept plan M32'ye bırakıldı |
| 2026-06-12 | M31 ilk etapta Core ağırlıklı ilerleyecek; UI iskeleti şablon ve üretim servisleri hazırlandıktan sonra yapılacak |
| 2026-06-12 | M31.1 açılış adımları tamamlandı; sıradaki iş M31.2 varsayılan bina işlevleri |
| 2026-06-12 | M31.2 tamamlandı: varsayılan bina işlevi tanım kataloğu eklendi; enum kapsama/benzersizlik/geçerlilik testleri geçti |
| 2026-06-12 | M31.3 tamamlandı: varsayılan mahal şablon kataloğu eklendi; kod benzersizliği, zorunlu işlev aileleri ve değer geçerliliği testleri geçti |
| 2026-06-12 | M31.4 tamamlandı: `IhtiyacProgramiUretimServisi` eklendi; ofis ve konferans ihtiyaç programı testleriyle Core test runner 13/13 geçti |
| 2026-06-12 | M31.5 tamamlandı: `AlanOzeti` ve `AlanOzetiServisi` eklendi; net/brüt alan, hedef farkı ve kullanım sınıfı kırılımı testleriyle Core test runner 15/15 geçti |
| 2026-06-12 | M31.6 tamamlandı: M31 ViewModel/pencere iskeleti ve `ArchitectureLauncherWindow` eklendi; ana ekrandaki mimari kartı M31'e bağlandı; App build 0 hata verdi |
| 2026-06-12 | M31.7.1-31.7.2 tamamlandı: mahal satırında ad/adet/alan/yükseklik düzenleme, yeni/kopyala/sil ve canlı alan özeti yenileme eklendi; kaydet/aç 2.8'e bırakıldı |
| 2026-06-12 | M31.7.3-31.7.4 tamamlandı: `.e4mimari.json` aç/kaydet/farklı kaydet UI akışı ve `MimariProjeDogrulamaServisi` tabanlı ön kontrol paneli eklendi |
| 2026-06-12 | M31.8 tamamlandı: `IhtiyacProgramiRaporServisi` ile HTML ihtiyaç programı raporu ve CSV mahal listesi üretildi; M31 penceresine rapor/dışa aktarım düğmeleri eklendi |
| 2026-06-12 | M31.9 tamamlandı: Core test/build, App build, Türkçe UI komut metni ve takip dosyası tutarlılığı kontrol edildi; M31 MVP kapandı |


<!-- ============================================================ -->
# 📄 DOSYA: `mimari/32-konsept-plan-parametrik-yerlesim/codex32.md`
<!-- ============================================================ -->

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


<!-- ============================================================ -->
# 📄 DOSYA: `mimari/33-dusey-cekirdek-saft-sirkulasyon/codex33.md`
<!-- ============================================================ -->

# CODEX 33 — Düşey Çekirdek, Şaft ve Sirkülasyon

> **Durum:** MVP tamamlandı (2026-06-13)
> **Ana takip:** [../codex30.md](../codex30.md)
> **Önceki modüller:** M31 ihtiyaç programı, M32 konsept plan, M34 yönetmelik ön kontrol MVP tamamlandı.
> **Amaç:** M32 plan taslağına merdiven, yangın merdiveni, asansör, rampa, şaft,
> çekirdek grubu ve kaçış/sirkülasyon ön altyapısını eklemek.
> **M50 kararı:** M50 entegrasyonu bu modülde arkaya alınır. İlk MVP `.e4mimari.json`
> ve `PlanTaslagi` üzerinde çalışır; M50 adapteri Faz 9/M39 tarafında ele alınır.

---

## 0. Kullanım Protokolü

1. Yeni oturumda önce `maincodex.md`, sonra `mimari/codex30.md`, sonra bu dosya okunur.
2. Aktif adım bu dosyadaki küçük faz tablosundan ve `codex30.md` Faz 5 tablosundan bulunur.
3. Kod yazmadan önce ilgili Core/App klasörleri incelenir.
4. Her işlemden sonra hem bu dosya hem `mimari/codex30.md` güncellenir.
5. Bir adım tamamlandığında durulur ve kullanıcıdan sonraki adıma geçiş onayı istenir.

---

## 1. Hedef

Bu modül, mimari taslak planın sadece mahal bloklarından oluşmasını aşarak binanın düşey
organizasyonunu temsil eder. İlk hedef, ruhsat veya kesin uygunluk iddiası taşıyan tam bir
yangın/kaçış hesabı değil; tasarım sırasında mimarı erkenden uyaran, katlar arası sürekliliği
koruyan ve ileride 3D BIM modele taşınabilecek kararlı bir çekirdek verisi üretmektir.

M33 tamamlandığında kullanıcı şunları yapabilmelidir:

- Plana merdiven, yangın merdiveni, asansör, rampa ve şaft taslakları ekleyebilmek.
- Bu elemanları çekirdek grubu olarak aynı aks/zone altında toplayabilmek.
- Katlar arasında aynı aksta devam etmeyen çekirdek ve şaftları görebilmek.
- WC/banyo/ıslak hacimlerin şaft ilişkisini ön kontrol olarak değerlendirebilmek.
- Mahal, koridor ve merdiven bağlantılarından ilk kaçış grafını oluşturabilmek.
- M32 plan ekranında çekirdekleri ayrı katman/renk ile görebilmek.

---

## 2. Kapsam

### MVP Kapsamında

- `CekirdekTipi` enum'u: Merdiven, Yangın merdiveni, asansör, rampa, şaft, çekirdek grubu.
- `CekirdekElemani`, `CekirdekGrubu`, `SaftTaslagi` veri modelleri.
- Kat aralığı, aks kodu, konum, boyut, net genişlik ve kapasite alanları.
- M32 `PlanTaslagi` ile ilişki: kat, mahal bloğu ve koridor bağlantıları.
- M34 ön kontrol entegrasyonu: süreklilik, bağlantısız çekirdek, ıslak hacim/şaft yakınlığı.
- Kaçış grafı v1: mahal, koridor ve merdiven düğümleri ile basit kenarlar.
- `.e4mimari.json` içinde çekirdek/şaft bilgisinin korunması.
- M32 konsept plan canvas'ında çekirdeklerin görünmesi.

### MVP Dışında

- Kesin yangın kaçış mesafesi ve ruhsat uygunluğu hesabı.
- Tam IFC/M50 nesne eşlemesi.
- Detaylı merdiven basamak/rıht/geometri üretimi.
- Asansör makine dairesi, kuyu detayı ve marka/katalog hesabı.
- Tam 3D yapı elemanı üretimi; bu M35'e bırakılır.

---

## 3. Veri Modeli Taslağı

```text
PlanTaslagi
├── KatTaslagi
│   ├── MahalBlogu
│   ├── KoridorTaslagi
│   ├── CekirdekElemani
│   ├── CekirdekGrubu
│   └── SaftTaslagi
└── KacisGrafi
```

### CekirdekElemani

Önerilen alanlar:

- `Id`, `Ad`, `Tip`
- `KatNo`, `BaslangicKatNo`, `BitisKatNo`
- `X`, `Y`, `GenislikM`, `DerinlikM`
- `NetGenislikM`, `KapasiteKisi`
- `AksKodu`, `CekirdekGrubuId`
- `BagliKoridorIdleri`, `BagliMahalIdleri`
- `Kilitli`, `Notlar`

### CekirdekGrubu

Önerilen alanlar:

- `Id`, `Ad`, `AksKodu`
- `BaslangicKatNo`, `BitisKatNo`
- `MerkezX`, `MerkezY`
- `ElemanIdleri`
- `Notlar`

### SaftTaslagi

Önerilen alanlar:

- `Id`, `Ad`, `TesisatTuru`
- `BaslangicKatNo`, `BitisKatNo`
- `X`, `Y`, `GenislikM`, `DerinlikM`
- `AksKodu`
- `BagliMahalIdleri`
- `Notlar`

### KacisGrafi

Önerilen alanlar:

- `Dugumler`: Mahal, koridor, merdiven, kapı/bağlantı placeholder.
- `Kenarlar`: İki düğüm arası bağlantı, mesafe ve kat bilgisi.
- `UyariKodlari`: Bağlantısız düğüm, merdivene ulaşamayan mahal, koridorsuz kaçış aksı.

---

## 4. Kod Konumu

Planlanan Core yerleşimi:

```text
src/Kst.Acoustics.Core/Architecture/Circulation/
├── CekirdekTipi.cs
├── CekirdekElemani.cs
├── CekirdekGrubu.cs
├── SaftTaslagi.cs
├── KacisGrafi.cs
├── CekirdekYerlesimServisi.cs
└── SirkulasyonKontrolServisi.cs
```

Planlanan UI bağlantısı:

```text
src/Kst.Acoustics.App/Architecture/KonseptPlan/
├── ViewModels/KonseptPlanViewModel.cs
└── Views/KonseptPlanCanvas.cs
```

---

## 5. M32 ve M34 Bağlantısı

M32, `PlanTaslagi`, `KatTaslagi`, `MahalBlogu` ve `KoridorTaslagi` üretir. M33 bu taslağın üstüne
düşey çekirdek ve şaft verisini ekler. M34 ise bu veriyi ön kontrol olarak değerlendirir.

İlk M34 uyarıları:

- Çok katlı planda düşey çekirdek yok.
- Merdiven veya şaft katlar arasında aynı aks koduyla devam etmiyor.
- Koridor hiçbir merdivene bağlanmıyor.
- Islak hacimlerin yakınında şaft yok.
- Kaçış grafında merdivene ulaşamayan mahal var.

Uyarı dili daima ön kontrol niteliğinde kalır; "kesin uygundur" veya ruhsat garantisi verilmez.

---

## 6. Küçük Faz Listesi

### M33.1 — Codex Açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.1.1 | Klasör oluştur | `mimari/33-dusey-cekirdek-saft-sirkulasyon/` var | ✅ Tamamlandı |
| 33.1.2 | `codex33.md` oluştur | Kapsam, model taslağı ve küçük faz listesi yazıldı | ✅ Tamamlandı |
| 33.1.3 | Ana takipleri güncelle | `maincodex.md`, `mimari/_index.md`, `codex30.md` M33 durumunu gösterir | ✅ Tamamlandı |

### M33.2 — Çekirdek Modelleri

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.2.1 | `Architecture/Circulation` klasörü | Core altında sirkülasyon klasörü açılır | ✅ Tamamlandı |
| 33.2.2 | Çekirdek enum/model | `CekirdekTipi`, `CekirdekElemani` eklenir | ✅ Tamamlandı |
| 33.2.3 | Grup ve şaft modelleri | `CekirdekGrubu`, `SaftTaslagi` eklenir | ✅ Tamamlandı |
| 33.2.4 | Plan/proje bağlantısı | Modeller `.e4mimari.json` içinde korunur | ✅ Tamamlandı |
| 33.2.5 | Model testleri | Varsayılan değerler ve serileştirme test edilir | ✅ Tamamlandı |

### M33.3 — Katlar Arası Süreklilik

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.3.1 | Aks eşleştirme | Aynı aks kodundaki elemanlar kat aralığına göre gruplanır | ✅ Tamamlandı |
| 33.3.2 | Süreklilik servisi | Eksik kat, aks kayması ve kopuk çekirdek uyarısı üretir | ✅ Tamamlandı |
| 33.3.3 | Test | Aynı aksta devam etmeyen eleman için uyarı testi geçer | ✅ Tamamlandı |

### M33.4 — Islak Hacim ve Şaft İlişkisi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.4.1 | Islak mahal tespiti | WC, banyo, mutfak vb. mahal tipleri sınıflanır | ✅ Tamamlandı |
| 33.4.2 | Yakınlık kontrolü | Şaft mesafe/tolerans parametreleriyle ön kontrol yapılır | ✅ Tamamlandı |
| 33.4.3 | Test | Şaftsız ıslak hacim uyarısı testi geçer | ✅ Tamamlandı |

### M33.5 — Kaçış Grafı v1

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.5.1 | Graf modelleri | Düğüm ve kenar modelleri eklenir | ✅ Tamamlandı |
| 33.5.2 | Plan graf üretimi | Mahal-koridor-merdiven bağlantıları üretilir | ✅ Tamamlandı |
| 33.5.3 | Bağlantı uyarısı | Merdivene ulaşamayan mahal uyarısı üretilir | ✅ Tamamlandı |

### M33.6 — UI Gösterimi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.6.1 | Canvas katmanı | Çekirdekler M32 plan canvas'ında ayrı renkte görünür | ✅ Tamamlandı |
| 33.6.2 | Sağ panel özeti | Seçili çekirdek/şaft bilgileri listelenir | ✅ Tamamlandı |
| 33.6.3 | Uyarı seçimi | M34 uyarısı tıklanınca ilgili çekirdek/şaft seçilir | ✅ Tamamlandı |
| 33.6.4 | App build | UI derlemesi hatasız geçer | ✅ Tamamlandı |

---

## 7. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M33 MVP tamamlandı; M35 başlatıldı |
| Sonraki Adım | M35 takibi için `mimari/35-3d-bim-model/codex35.md` kullanılacak |
| Kod Durumu | M33 çekirdek/şaft/sirkülasyon MVP'si tamamlandı; M50 entegrasyonu arkaya alınmış durumda |
| Test Durumu | Core test runner 35/35 geçti; Core test build 0 uyarı/0 hata; App build 0 hata, mevcut farklı modüllerden 6 uyarı |

---

## 8. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-13 | M33 başlatıldı; ilk MVP'nin M50'ye bağlanmadan `.e4mimari.json` ve `PlanTaslagi` üzerinde ilerlemesine karar verildi |
| 2026-06-13 | Modül kapsamı merdiven, yangın merdiveni, asansör, rampa, şaft, çekirdek grubu ve kaçış grafı v1 olarak belirlendi |
| 2026-06-13 | Uyarı dili M34 çizgisine uygun şekilde ön kontrol olarak kalacak; kesin uygunluk veya ruhsat garantisi verilmeyecek |
| 2026-06-13 | Faz 5.2 tamamlandı: `CekirdekTipi`, `SaftTesisatTuru`, `CekirdekElemani`, `CekirdekGrubu`, `SaftTaslagi` eklendi; plan/kat taslağı ve `.e4mimari.json` kayıt akışı çekirdek/şaft verisini koruyor |
| 2026-06-13 | Faz 5.3 tamamlandı: `SirkulasyonKontrolServisi` aks kodu, kat aralığı, kopuk süreklilik ve aks kayması uyarıları üretiyor; M34 plan kontrolü bu uyarıları kaynak profilli sonuçlara taşıyor |
| 2026-06-13 | Faz 5.4 tamamlandı: WC, banyo, mutfak ve benzeri ıslak hacim blokları için aynı kattaki şaft yokluğu ve şaft uzaklığı ön kontrol uyarıları eklendi; M34 plan kontrolüne bağlandı |
| 2026-06-13 | Faz 5.5 tamamlandı: `KacisGrafi`, `KacisDugumu`, `KacisKenari` modelleri ve mahal-koridor-merdiven graf üretimi eklendi; merdivene ulaşamayan mahal M34 uyarısı olarak raporlanıyor |
| 2026-06-13 | Faz 5.6 tamamlandı: M32 konsept plan canvas'ında çekirdek/şaft katmanı, sağ panel özeti ve M34 uyarısından çekirdek/şaft seçimi eklendi; M33 MVP kapandı |
| 2026-06-13 | M35 açıldı; sonraki 3D BIM model geliştirme takibi `codex35.md` üzerinden yürütülecek |


<!-- ============================================================ -->
# 📄 DOSYA: `mimari/34-yonetmelik-on-kontrol/codex34.md`
<!-- ============================================================ -->

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


<!-- ============================================================ -->
# 📄 DOSYA: `mimari/35-3d-bim-model/codex35.md`
<!-- ============================================================ -->

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


<!-- ============================================================ -->
# 📄 DOSYA: `mimari/36-detay-malzeme-yapi-elemani/codex36.md`
<!-- ============================================================ -->

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


<!-- ============================================================ -->
# 📄 DOSYA: `mimari/37-pafta-rapor-cizelgeler/codex37.md`
<!-- ============================================================ -->

# CODEX 37 — Mimari Pafta, Rapor ve Çizelgeler

> **Durum:** MVP tamamlandı (2026-06-13)
> **Ana takip:** [../codex30.md](../codex30.md)
> **Önceki modüller:** M31 ihtiyaç programı, M32 konsept plan, M33 düşey çekirdek/şaft,
> M34 yönetmelik ön kontrol, M35 3D BIM model ve M36 detay/malzeme MVP tamamlandı.
> **Amaç:** M31–M36 boyunca üretilen mimari kararları (mahal programı, plan, 3D model, malzeme/atama,
> disiplin verisi, ön kontrol uyarıları) okunabilir tablo, çizelge ve raporlara dökmek.
> **M50 kararı:** M37 de M50'ye bağlanmadan ilerler; çıktılar `.e4mimari.json` ve mevcut domain
> modeli üzerinden üretilir. M50/IFC koordinasyonu M39/Faz 9-10 tarafındadır.

---

## 0. Kullanım Protokolü

1. Yeni oturumda önce `maincodex.md`, sonra `mimari/codex30.md`, sonra bu dosya okunur.
2. Aktif adım bu dosyadaki küçük faz tablosundan ve `codex30.md` Faz 8 tablosundan bulunur.
3. Kod yazmadan önce ilgili Core/App klasörleri incelenir.
4. Her işlemden sonra hem bu dosya hem `mimari/codex30.md` güncellenir.
5. Bir adım tamamlandığında durulur ve kullanıcıdan sonraki adıma geçiş onayı istenir.

---

## 1. Hedef

M37, mimari modeli "çıktı alınabilir" hale getirir. Hedef tam pafta/antet düzeni veya CAD ölçekli
çizim üretimi değildir; hedef, mimarın ve diğer disiplinlerin kullanabileceği güvenilir **tablo ve
çizelgeleri** tek merkezde üretmektir.

M37 tamamlandığında kullanıcı şunları alabilmelidir:

- Kat / mahal no / ad / alan / hacim / işlev içeren **mahal listesi**.
- Net/brüt, sirkülasyon ve teknik alan kırılımını içeren **alan tablosu**.
- Taslak açıklık verisinden **kapı-pencere çizelgesi v1**.
- Mahal bazında zemin/duvar/tavan **finisaj çizelgesi**.
- Kaynak ve kural koduyla etiketli **ön kontrol raporu**.
- En az iki dosya formatında (HTML + CSV; XLSX stretch) dışa aktarım.

---

## 2. Kapsam

### MVP Kapsamında

- `Architecture/Reports` altında rapor/çizelge domain modelleri ve üretici servisler.
- Mahal listesi tablosu (3D mahaller + ihtiyaç programı işlev/kullanım sınıfı).
- Alan tablosu (`AlanOzetiServisi` çıktısının raporlanabilir hali).
- Kapı-pencere çizelgesi v1 (`Model3D` açıklıkları + atanan tip/malzeme).
- Finisaj çizelgesi (mahal finisaj önerisi / atama → zemin/duvar/tavan).
- Ön kontrol raporu (`YonetmelikOnKontrolServisi` + M36 finisaj uyarıları, kaynak/kural koduyla).
- Disiplin özeti raporu (`DisiplinVeriOzetiServisi` çıktısının tablolaştırılması).
- HTML ve CSV üreticiler; ortak bir rapor model → format katmanı.
- Boş/eksik veride güvenli çalışan üreticiler.

### MVP Dışında

- Ölçekli mimari pafta ve antet/çizim düzeni.
- 2D plan/kesit/görünüş çizimi.
- PDF üretimi (ileride değerlendirilir).
- Tam XLSX biçim/şablon (varsa basit XLSX stretch hedefidir).
- IFC/BIM ihracat (M39).
- M50 ortak modeline yazma.

---

## 3. Veri Modeli / Çıktı Taslağı

```text
RaporModeli (tek üretim için)
├── MahalListesiTablosu  -> satır: Kat, No, Ad, MahalTipi, KullanimSinifi, AlanM2, HacimM3
├── AlanTablosu          -> net/brüt/sirkülasyon/teknik + kullanım sınıfı kırılımı
├── KapiPencereCizelgesi -> satır: Kat, Tip(Kapı/Pencere), Ölçü, Adet, YapiElemaniTipi, Malzeme
├── FinisajCizelgesi     -> satır: Kat, Mahal, Zemin, Duvar, Tavan, Not
├── OnKontrolRaporu      -> satır: Seviye, KuralKodu, Kaynak, Mesaj, EtkilenenEleman
└── DisiplinOzetiTablosu -> akustik/enerji/elektrik/metraj okunabilir kırılımları
```

Üretim girdisi `MimariProje`'dir. Servisler önce yapısal **rapor modeli** üretir, sonra biçim
katmanı (HTML/CSV) bu modeli render eder. Böylece yeni format eklemek model üretimini bozmaz.

---

## 4. Kod Konumu

Planlanan Core yerleşimi:

```text
src/Kst.Acoustics.Core/Architecture/Reports/
├── IhtiyacProgramiRaporServisi.cs   (mevcut — M31)
├── MimariRaporModeli.cs             (tablo/satır modelleri)
├── MimariRaporUretimServisi.cs      (proje -> rapor modeli)
├── MimariRaporHtmlBuilder.cs        (model -> HTML)
└── MimariRaporCsvBuilder.cs         (model -> CSV)
```

Testler mevcut Core test runner içine eklenir: `src/Kst.Acoustics.Core.Tests/Program.cs`.
UI tarafı (rapor önizleme/dışa aktarım butonları) gerekiyorsa M37 sonunda değerlendirilir; çekirdek
hedef Core üreticilerdir.

---

## 5. M31–M36 Bağlantısı

- M31 → ihtiyaç programı, mahal tipi/kullanım sınıfı, alan/hacim hedefleri ve `AlanOzeti`.
- M32/M33 → plan taslağı, kat/blok, çekirdek/şaft (mahal listesi kat bilgisi).
- M35 → 3D mahaller, açıklıklar, döşeme/tavan/duvar (alan/hacim ve kapı-pencere ölçüleri).
- M36 → malzeme, yapı elemanı tipi, atama, finisaj profili ve `DisiplinVeriOzeti`.
- M34 → ön kontrol uyarıları (kaynak/kural kodlu rapor satırları).

M37 yalnız okur ve raporlar; mevcut Id'leri ve geometriyi değiştirmez.

---

## 6. Dil ve Standart Yaklaşımı

M37, M34 "ön kontrol" dilini korur: raporlarda kesin uygunluk beyanı yer almaz. Ön kontrol raporu
uyarıları kaynak profili (PAIY, BYKHY, OTOPARK, TBDY, BEPY, erişilebilirlik, M34-VERI) ve kural
koduyla etiketler. Tüm görünür metin Türkçe ve doğru Türkçe karakterlerle yazılır.

---

## 7. Küçük Faz Listesi

### M37.1 — Codex Açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.1.1 | Klasör oluştur | `mimari/37-pafta-rapor-cizelgeler/` var | ✅ Tamamlandı |
| 37.1.2 | `codex37.md` oluştur | Kapsam, çıktı taslağı ve küçük faz listesi yazıldı | ✅ Tamamlandı |
| 37.1.3 | Ana takipleri güncelle | `maincodex.md`, `mimari/_index.md`, `codex30.md` M37 durumunu gösterir | ✅ Tamamlandı |

### M37.2 — Mahal Listesi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.2.1 | Rapor modeli | Mahal listesi tablo/satır modeli eklenir | ✅ Tamamlandı |
| 37.2.2 | Üretim | Kat, no, ad, tip, kullanım sınıfı, alan, hacim satırları üretilir | ✅ Tamamlandı |
| 37.2.3 | Test | Boş/dolu projede mahal listesi tutarlı üretilir | ✅ Tamamlandı |

### M37.3 — Alan Tablosu

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.3.1 | Alan tablosu | Net/brüt/sirkülasyon/teknik ve kullanım sınıfı kırılımı tablolaşır | ✅ Tamamlandı |
| 37.3.2 | Test | `AlanOzeti` ile tutarlı tablo üretilir | ✅ Tamamlandı |

### M37.4 — Kapı-Pencere Çizelgesi v1

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.4.1 | Çizelge | Açıklık tipi, ölçü, adet, yapı elemanı tipi ve malzeme satırları çıkar | ✅ Tamamlandı |
| 37.4.2 | Test | Açıklık + atama verisinden çizelge tutarlı üretilir | ✅ Tamamlandı |

### M37.5 — Finisaj Çizelgesi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.5.1 | Çizelge | Mahal bazında zemin/duvar/tavan finisajı tablolaşır | ✅ Tamamlandı |
| 37.5.2 | Test | Finisaj öneri/atama verisinden çizelge üretilir | ✅ Tamamlandı |

### M37.6 — Ön Kontrol Raporu

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.6.1 | Rapor | M34 + M36 uyarıları seviye/kaynak/kural koduyla raporlanır | ✅ Tamamlandı |
| 37.6.2 | Test | Sorunlu projede uyarı satırları kaynak koduyla çıkar | ✅ Tamamlandı |

### M37.7 — Çıktı Formatları

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.7.1 | HTML builder | Tüm tablolar tek HTML raporda render edilir | ✅ Tamamlandı |
| 37.7.2 | CSV builder | Her tablo CSV olarak dışa aktarılır | ✅ Tamamlandı |
| 37.7.3 | Regresyon | En az iki format çalışır; Core/test build temiz | ✅ Tamamlandı |
| 37.7.4 | XLSX (stretch) | Bağımlılıksız OOXML; her tablo bir sayfa | ✅ Tamamlandı |

---

## 8. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M37 MVP tamamlandı — Faz 9/10 (M50 entegrasyonu / BIM ihracat) için onay bekleniyor |
| Sonraki Adım | Mimari başlıkta açık MVP modülleri bitti; sıradaki yön kullanıcı kararına bağlı (M50 ertelendi) |
| Kod Durumu | M37 MVP kapandı: mahal listesi, alan tablosu, kapı-pencere ve finisaj çizelgeleri, ön kontrol raporu + tümünü toplayan `TumRaporuOlustur` ve `MimariRaporHtmlBuilder`/`MimariRaporCsvBuilder` hazır |
| Test Durumu | Core test runner 58/58 geçti; Core build 0 uyarı/0 hata; tam çözüm build 0 hata |
| Çıktı Formatları | HTML + CSV + XLSX (üçü de çalışır; XLSX bağımlılıksız OOXML) |

---

## 9. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-13 | M37 başlatıldı; M50'ye bağlanmadan `.e4mimari.json` ve mevcut domain modeli üzerinden çıktı üretimine karar verildi |
| 2026-06-13 | M37 kapsamı mahal listesi, alan tablosu, kapı-pencere çizelgesi v1, finisaj çizelgesi, ön kontrol raporu ve disiplin özeti tablosu olarak belirlendi |
| 2026-06-13 | Rapor üretimi iki katmanlı tasarlanacak: yapısal rapor modeli → biçim (HTML/CSV) builder'ları; XLSX stretch hedefi |
| 2026-06-13 | Faz 8.1 tamamlandı: M37 klasörü ve `codex37.md` açıldı; ana takip bağlantıları güncellendi; sıradaki adım 37.2 mahal listesi |
| 2026-06-13 | Faz 8.2 tamamlandı: `MimariRaporModeli` (`MahalListesiSatiri`/`MahalListesiTablosu`) ve `MimariRaporUretimServisi.MahalListesiOlustur` eklendi; 3D model mahallerinden gerçek alan/hacim + kat ve plan bloğundan kullanım sınıfı, model yoksa ihtiyaç programından hedef alan/hacim üretiliyor; null/boş güvenli. Core test runner 52/52 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.3 tamamlandı: `AlanTablosu` + `AlanKirilimSatiri` modelleri ve `AlanTablosuOlustur` eklendi; `AlanOzetiServisi` çıktısı (net/brüt/sirkülasyon/teknik, hedef farkı, kullanım sınıfı kırılımı) raporlanabilir tabloya çevriliyor, null/boş güvenli. Core test runner 53/53 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.4 tamamlandı: `KapiPencereSatiri`/`KapiPencereCizelgesi` ve `KapiPencereCizelgesiOlustur` eklendi; 3D açıklıklar tip+ölçü+yapı elemanı tipine göre gruplanıp pozisyon (K1/P1...), adet, ölçü (cm "G/Y"), tip ve malzeme adlarıyla üretiliyor; atanmamış açıklık "Atanmadı" görünüyor. Ortak `TipSozlugu`/`MalzemeSozlugu` yardımcıları eklendi. Core test runner 54/54 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.5 tamamlandı: `FinisajCizelgesiSatiri`/`FinisajCizelgesi` ve `FinisajCizelgesiOlustur` eklendi; mahal listesi satırları M36 finisaj önerisiyle (`MahalFinisajOneriServisi.Oner`) eşleştirilip her mahal için zemin/duvar/tavan tip adı, ıslak hacim bayrağı ve uyarı notu üretiliyor. Core test runner 55/55 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.6 tamamlandı: `OnKontrolSatiri`/`OnKontrolRaporu` ve `OnKontrolRaporuOlustur` eklendi; `IlkVeriKontrolleriniCalistir` (M34 veri + plan + M36 finisaj) çıktısı seviye/kaynak/kural kodu, mesaj, çözüm önerisi ve etkilenen eleman satırlarına çevriliyor; seviye sayıları ve kritik bayrağı üretiliyor. Core test runner 56/56 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.7 tamamlandı, M37 MVP kapandı: `MimariRaporu` + `TumRaporuOlustur`, paylaşılan `MimariRaporMetin` (görünüm/format/CSV yardımcıları), `MimariRaporHtmlBuilder` (tek belge, tüm bölümler, ön kontrol dili) ve `MimariRaporCsvBuilder` (tablo başına CSV) eklendi. Boş projede de güvenli. Core test runner 57/57 geçti, Core build 0 uyarı/0 hata, tam çözüm build 0 hata |
| 2026-06-13 | XLSX stretch (37.7.4) tamamlandı: `XlsxYazici` (bağımlılıksız, System.IO.Compression ile minimal OOXML — Content_Types/rels/workbook/worksheet, sayısal+inlineStr hücreler) ve `MimariRaporXlsxBuilder` (her tablo bir sayfa) eklendi; ZipArchive ile yapısal doğrulama testi geçti. Üç format (HTML+CSV+XLSX) çalışıyor. Core test runner 58/58 geçti |


<!-- ============================================================ -->
# 📄 DOSYA: `mimari/39-bim-koordinasyon-ihracat/codex39.md`
<!-- ============================================================ -->

# CODEX 39 — BIM Koordinasyon ve İhracat

> **Durum:** MVP tamamlandı (2026-06-13)
> **Ana takip:** [../codex30.md](../codex30.md)
> **Önceki:** M31-M37 MVP tamamlandı; Faz 9 M50 entegrasyonu (mimari→`.e4bina`, M12/M08 köprüleri) bitti.
> **Amaç:** Mimari tasarımın disiplinler arası koordinasyonunu ve dış formatlara ihracatını
> ele almak: revizyon farkı, askıda veri (mutabakat) raporu ve IFC/glTF/DXF ihracat yönü.

---

## 0. Kullanım Protokolü

1. Yeni oturumda önce `maincodex.md`, sonra `mimari/codex30.md`, sonra bu dosya okunur.
2. Aktif adım bu dosyadaki küçük faz tablosundan ve `codex30.md` Faz 10 tablosundan bulunur.
3. Kod yazmadan önce ilgili Core klasörleri (`Architecture/Exporting`, `Reports`, `Materials`) incelenir.
4. Her işlemden sonra hem bu dosya hem `mimari/codex30.md` güncellenir.
5. Bir adım tamamlandığında durulur ve kullanıcıdan sonraki adıma geçiş onayı istenir.

---

## 1. Hedef

M39, mimari modeli "tek başına" olmaktan çıkarıp koordinasyon ve dış dünya katmanını ekler:

- **Revizyon farkı:** iki mimari tasarım (veya iki `.e4bina`) arasında eklenen/silinen/değişen
  mahal, kat ve alan farkını okunabilir raporlamak.
- **Disiplin mutabakatı:** geometri/atama değiştiğinde askıda kalan veriyi (artık var olmayan
  elemana bağlı malzeme ataması, hedefsiz finisaj vb.) raporlamak.
- **İhracat yönü:** glTF/OBJ (M35 sözleşmesi) ve DXF gibi pratik değişim formatları;
  IFC için kütüphane/format kararı (uzun vade).

İlk hedef tam IFC üretimi değildir; küçük, test edilebilir Core katmanları + net format kararıdır.

---

## 2. Kapsam

### MVP Kapsamında

- `Architecture/Coordination` (veya `Exporting`) altında revizyon farkı ve mutabakat modelleri.
- **Revizyon farkı servisi:** iki `MimariProje`/`BinaGeometri` → fark raporu
  (eklenen/silinen/değişen mahal, kat; alan/hacim deltaları).
- **Mutabakat raporu:** askıda `MalzemeAtamalari` (var olmayan 3D eleman Id'sine bağlı),
  hedefsiz finisaj, geometri-veri tutarsızlıkları.
- IFC/glTF/DXF için **format kararı** ve (varsa) hafif DXF/OBJ ihracat sözleşmesinin netleştirilmesi.
- Boş/eksik veride güvenli; Core test runner kapsamı.

### MVP Dışında

- Tam IFC property set üretimi (kütüphane bağımlılığı; uzun vade).
- Gerçek 3D mesh tessellation/optimizasyon.
- Bulut/sunucu tabanlı model koordinasyonu (BCF, clash detection).
- M50 ortak modeline yazma (mimari yalnız üretir — Faz 9 kararı).

---

## 3. Veri Modeli Taslağı

```text
RevizyonFarki
├── EklenenMahaller[] / SilinenMahaller[] / DegisenMahaller[]   (Id, ad, alan eski→yeni)
├── KatFarklari[]     (eklenen/silinen kat, kat alan deltası)
└── ToplamAlanDeltaM2, ozet sayilari

MutabakatRaporu
├── AskidaAtamalar[]  (MalzemeAtamasi → var olmayan eleman Id)
├── HedefsizFinisaj[] (profil → karşılığı olmayan mahal tipi)
└── ToplamAskidaSayisi
```

Revizyon farkı girdisi iki `MimariProje` (ör. kayıtlı eski vs. güncel) veya iki `BinaGeometri`.
Mutabakat girdisi tek `MimariProje` (kendi içsel tutarlılığı) — M50'nin `IGeometriBagimliVeri`
deseniyle uyumlu kalır.

---

## 4. Kod Konumu

```text
src/Kst.Acoustics.Core/Architecture/Coordination/
├── RevizyonFarki.cs            (fark modelleri)
├── RevizyonFarkiServisi.cs     (iki proje/geometri → fark)
├── MutabakatRaporu.cs          (askıda veri modelleri)
└── MutabakatServisi.cs         (proje → askıda veri raporu)
```

Testler mevcut Core test runner içine eklenir: `src/Kst.Acoustics.Core.Tests/Program.cs`.

---

## 5. M31-M37 / M50 Bağlantısı

- Revizyon farkı, M37 `MahalListesiOlustur` / `MimariRaporUretimServisi` çıktısını veya doğrudan
  3D modeli kaynak alır; Id eşleşmesi M35 `KaynakMahalBloguId` üzerinden yürür.
- Mutabakat, M36 `MalzemeAtamalari` ve `FinisajProfilleri`'ni 3D model elemanlarıyla karşılaştırır.
- M50 tarafındaki `GeometriMutabakat`/`IGeometriBagimliVeri` deseni referans alınır; mimari
  kendi içsel mutabakatını üretir (geometri üreticisi olduğu için).

---

## 6. IFC / Format Yaklaşımı (KARAR — 10.3, 2026-06-13)

Format öncelik kararı:
- **OBJ → ŞİMDİ uygulandı.** Düz metin, bağımlılıksız, evrensel içe aktarım. `MimariObjIhracatServisi`
  mahal hacimleri + duvarları kutu mesh olarak yazar (M35 GLB/OBJ sözleşmesindeki "OBJ yedek" somutlaştı).
- **GLB/glTF → ertelendi.** İkili tampon + JSON; OBJ ihtiyacı karşıladığı için MVP dışı. M35
  `Model3DIhracatSecenekleri` sözleşmesi (GLB birincil) korunur; serileştirici sonra yazılır.
- **DXF → ertelendi.** Kod tabanında ortak `Exporting/DXF` var; mimari 2D plan DXF'i ileride bununla
  hizalanır (ayrı iş).
- **IFC → uzun vade.** Tam IFC üretimi ağır kütüphane (xBIM / IfcOpenShell) gerektirir; bağımlılık ve
  bakım yükü nedeniyle MVP'ye alınmadı. İhtiyaç netleşince kütüphane değerlendirmesi yeniden açılır.

Gerekçe: bağımlılıksızlık ilkesi (Core saf mantık) + "en az iki çalışan format" hedefi OBJ ile
karşılanıyor; ağır formatlar gerçek talep oluşunca ele alınacak.

---

## 7. Küçük Faz Listesi

### M39.1 — Codex Açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 39.1.1 | Klasör + `codex39.md` | Kapsam, model taslağı ve faz listesi yazıldı | ✅ Tamamlandı |
| 39.1.2 | Ana takipleri güncelle | `maincodex.md`, `_index.md`, `codex30.md` M39 durumunu gösterir | ✅ Tamamlandı |

### M39.2 — Revizyon Farkı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 39.2.1 | Fark modeli | Eklenen/silinen/değişen mahal + kat + alan deltası modeli | ✅ Tamamlandı |
| 39.2.2 | Fark servisi | İki proje/geometriden fark üretir (Id eşleşmeli) | ✅ Tamamlandı |
| 39.2.3 | Test | Mahal ekle/sil/alan değiştir senaryoları doğru rapor çıkar | ✅ Tamamlandı |

### M39.3 — IFC / Format Kararı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 39.3.1 | Araştırma + karar | IFC/glTF/OBJ/DXF yönü ve kütüphane kararı belgelenir | ✅ Tamamlandı |
| 39.3.2 | (Opsiyonel) OBJ/DXF | Bağımlılıksız hafif bir ihracat sözleşmesi/iskeleti | ✅ Tamamlandı (OBJ) |

### M39.4 — Disiplin Mutabakatı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 39.4.1 | Mutabakat modeli | Askıda atama / kırık tip referansı modeli | ✅ Tamamlandı |
| 39.4.2 | Mutabakat servisi | Proje içsel tutarlılık raporu üretir | ✅ Tamamlandı |
| 39.4.3 | Test | Askıda atama / kırık tip referansı senaryoları yakalanır | ✅ Tamamlandı |

### M39.5 — Regresyon ve Kapanış

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 39.5.1 | Regresyon | Tüm M39 testleri + Core/test build temiz | ✅ Tamamlandı |

---

## 8. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M39 MVP tamamlandı — Faz 10 (M39) kapandı |
| Sonraki Adım | Mimari başlıkta açık tüm faz/modül MVP'leri bitti; yön kullanıcı kararına bağlı (UI bağlama, GLB/DXF/IFC, M38, M21 köprüsü) |
| Kod Durumu | Revizyon farkı + format kararı/OBJ + disiplin mutabakatı (`MutabakatServisi`) hazır |
| Test Durumu | Core test runner 65/65 geçti; Core/tam çözüm build 0 hata |

---

## 9. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-13 | M39 başlatıldı; kapsam revizyon farkı + disiplin mutabakatı + IFC/format kararı olarak belirlendi |
| 2026-06-13 | IFC tam üretimi uzun vadeye bırakılması öngörülüyor (ağır kütüphane); MVP'de araştırma+karar (10.3) |
| 2026-06-13 | Faz 10.1 tamamlandı: M39 klasörü ve `codex39.md` açıldı; ana takip bağlantıları güncellendi; sıradaki adım 39.2 revizyon farkı |
| 2026-06-13 | Faz 10.2 tamamlandı: `RevizyonFarki`/`RevizyonMahalGirisi`/`RevizyonMahalDegisikligi` modelleri + `RevizyonFarkiServisi` (iki `MimariProje` veya iki `BinaGeometri` → kararlı oda Id eşleşmeli fark) eklendi; eklenen/silinen/değişen mahal, eklenen/silinen kat ve toplam alan deltası üretiliyor. Core test runner 63/63 geçti |
| 2026-06-13 | Faz 10.3 tamamlandı: Format kararı belgelendi (OBJ şimdi, GLB/DXF ertelendi, IFC uzun vade — §6). `MimariObjIhracatServisi` eklendi: bağımlılıksız Wavefront OBJ; mahal hacimleri + duvarlar kutu mesh (8 vertex/6 yüz), grup adlı, geçerli indeksli. Core test runner 64/64 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 10.4 tamamlandı + M39.5 regresyon, **M39 MVP kapandı**: `MutabakatRaporu`/`MutabakatBulgusu` + `MutabakatServisi.Denetle` eklendi — askıda atama (var olmayan 3D eleman) ve kırık tip referansı (atama/finisaj → katalogda olmayan yapı elemanı tipi) yakalanıyor. §3'teki "hedefsiz finisaj" kırık tip referansına çevrildi (varsayılan katalog yanlış-pozitifini önlemek için). Core test runner 65/65 geçti, tam çözüm build 0 hata |


<!-- ============================================================ -->
# 📄 DOSYA: `terrain/_index.md`
<!-- ============================================================ -->

# Terrain — Ana Başlık 6

**Ürün:** EYE4PRO Terrain (arazi, topografya, hafriyat)
**Modül aralığı:** 60–69
**Durum:** Planlandı — M60 ilk modül

## Modüller

| # | Klasör | Modül Adı | Durum |
|---|--------|-----------|-------|
| 60 | [60-hafriyat-hesapla](60-hafriyat-hesapla/codex60.md) | Hafriyat Hesapla (Yarma/Dolgu) | MVP tamam (F0-F8 + App + renk haritası + M15 köprü) |
| 61 | [61-bina-temeli-kota-oturtma](61-bina-temeli-kota-oturtma/codex61.md) | Bina Temeli Kota Oturtma | Geliştiriliyor (F0-F2 Core; .e4bina köprülü) |
| 62 | — | Şev & İstinat Duvarı | _öneri_ |
| 63 | — | Yol & Saha Profili | _öneri_ |
| 64 | — | Saha Drenajı & Yağmursuyu | _öneri_ |
| 65 | — | Kazı Güvenliği & Şantiye (İksa) | _öneri_ |
| 66–69 | — | _rezerve_ | — |

## Paylaşılan Arazi Geometrisi

- Arazi yüzeyi M50 bina geometrisi deseniyle **paylaşılan salt-okunur artifact**'tır: **`.e4arazi.json`**.
- Üretici M60'tır; tüketiciler bu dosyayı salt-okunur yükler ("Arazi Yükle").
- `SemaSurum` migrasyonu + `GeometriRevizyon` damgası + mutabakat raporu baştan dahildir.

## Tüketici Modüller (cross-başlık entegrasyon)

`.e4arazi.json` disiplinler arası ortak altlıktır. Bilinen tüketiciler:

| Tüketici | Başlık | Kullanım |
|---|---|---|
| M61 Bina Temeli Kota Oturtma | Terrain | Hafriyat sonrası kotlara temel oturtma |
| Mimari M30-M39 | Mimari | Tabii zemin/tesviye kotu, kademeli bina kesiti |
| **M15 Çevre/Park Aydınlatma** ✅ | Elektrik | "Arazi Yükle" köprüsü kuruldu — direklere `ZeminKotuM` atanır; sonraki: 3D yerleşim, gerçek hat uzunluğu |
| Mekanik (M21+) | Mekanik | İleride: dış tesisat güzergâh kotu, eğim |
| Akustik (M04 ses bariyeri vb.) | Akustik | İleride: arazi topografyası difraksiyon/yansıma altlığı |

- Tüketici, arazi **geometrisini değiştirmez**; yalnız kot/yüzey verisini okur (M50 salt-okunurluk deseni).
- Tüketici hem `.e4arazi.json` yolunu hem gömülü kopyasını saklar; revizyon farkında "araziden güncelle?" sorulur.

## Standartlar

Average-end-area / prizmatik hacim yöntemleri, Karayolları Teknik Şartnamesi (kazı/dolgu, kabarma katsayıları),
İSG kazı/iksa, TBDY (geoteknik ön kontrol), Planlı Alanlar İmar Yönetmeliği (tabii zemin/tesviye kotu).

## Kaynak Kod

`../src/Kst.Acoustics.Core/Terrain/` — arazi iş mantığı (DTM/TIN, hafriyat motoru)
`../src/Kst.Acoustics.App/Terrain/` — UI katmanı


<!-- ============================================================ -->
# 📄 DOSYA: `terrain/60-hafriyat-hesapla/codex60.md`
<!-- ============================================================ -->

# M60 Codex — Hafriyat Hesapla (Yarma/Dolgu)

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir;
yalnızca güncel kararlar, kapsam, sistem davranışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

> **Durum:** F0-F8 TAMAM (planlı MVP fazlarının tümü). Terrain başlatıcı + M60 Hafriyat penceresi (DXF→TIN→hedef kot→hesap→**yarma/dolgu renk haritası**→rapor→.e4arazi) + M15 "Arazi Yükle" köprüsü çalışıyor. 32 Core testi geçer; App 0 hata derlenir; gerçek DXF ile uçtan uca doğrulandı. Kalan (ileri/opsiyonel): interaktif platform/poligon/şev çizimi, F9, M61.
> **Bağlı codex'ler:** [codex50](../../ortak-moduller/50-bina-geometri/codex50.md) (desen referansı),
> [codex61](../61-bina-temeli-kota-oturtma/codex61.md) (tüketici)

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Terrain |
| Modül | M60 — Hafriyat Hesapla |
| Amaç | DXF arazi kotlarından 3D arazi yüzeyi, tasarım (hedef) kotları ve yarma/dolgu hacim hesabı |
| Proje uzantısı | `.e4p60` |
| Paylaşılan çıktı | `.e4arazi.json` (salt-okunur artifact — M61 ve mimari tüketir) |
| Standart ref. | Average-end-area / prizmatik hacim, Karayolları Teknik Şartnamesi, İSG kazı |
| Stack / UI dili | C# + Avalonia (net10.0) / Türkçe |
| Çekirdek | `src/Kst.Acoustics.Core/Terrain/` · UI: `src/Kst.Acoustics.App/Terrain/` |

---

## 2. Modül Kapsamı

Aktif kapsam (MVP hedefi):
- DXF'ten arazi verisi içe aktarma: kontur eğrileri + spot kotlar (mevcut kapalı-yüzey parser'dan AYRI)
- Arazi sayısal yüzeyi (DTM): TIN — Delaunay üçgenlemesi
- 3D görselleştirme (M35/M50 render altyapısı yeniden kullanılır)
- Tasarım yüzeyi: nokta kotu, bölge platform kotu, şev/eğim, kademe (teras)
- Yarma (cut) / Dolgu (fill) hacim hesabı — grid yöntemi
- Yarma/dolgu renk haritası ve bölge bazlı döküm
- `.e4p60` kaydet/aç + `.e4arazi.json` üretimi

İleri/eksik alanlar:
- TIN-TIN kompozit hacim (hassas yöntem)
- Kabarma/sıkışma (şişme-büzülme) katsayıları, kütle dengesi
- Kütle-haul (Bruckner) diyagramı, kazı nakil optimizasyonu
- Metraj/keşif köprüsü (`Kst.Acoustics.Metraj`)
- Spot kotlardan otomatik kontur üretimi
- Rapor/PDF çıktı, kotlu kesit çizimi

---

## 3. DXF Arazi Girişi (kritik karar)

- maincodex §4.1'deki DXF kuralı kapalı yüzeyler içindir (3DFACE, kapalı POLYLINE) — bina geometrisi.
- Arazi **AYRI bir importer** ister; açık geometriyi okur:
  - **Kontur eğrileri:** açık `LWPOLYLINE`/`POLYLINE`. Kot kaynağı öncelik sırası:
    vertex Z → entity elevation → katman adı (örn. `KONTUR_540`) → yakın TEXT etiketi.
  - **Spot kotlar:** `POINT` (Z'li) veya nokta + TEXT eşlemesi.
  - **3DFACE / nokta bulutu** opsiyonel girdi.
- `$INSUNITS` okunur; yoksa kullanıcıdan birim istenir (mevcut kural korunur).
- Konturlar TIN öncesi noktalara yoğunlaştırılır (densify).

---

## 4. Yüzey ve Hesap Modeli

### 4.1 DTM
- **TIN (Delaunay)** birincil. Grid/DEM ikincil.
- `Core/Geometry` (Point3D, Vector3D, GeometryMath) yeniden kullanılır.

### 4.2 Tasarım yüzeyi (grading)
- **Nokta**: tekil hedef kot.
- **Bölge (poligon)**: sabit platform kotu (pad).
- **Şev**: platform kenarından eğim oranı (örn. 2/3, 1/1) ile arazi kesişimine kadar.
- **Kademe (teras)**: eğimli arazide birden çok platform (kademeli yerleşim).

### 4.3 Yarma/Dolgu motoru
- **Grid (ortalama-uç-alan / prizmatik)** ilk sürüm: alan ızgaraya bölünür, hücrede
  mevcut−tasarım kot farkı → hacim. Sağlam ve basit.
- **TIN-TIN kompozit hacim** ileri sürüm (iki yüzey arası prizma) — hassas.
- Çıktı: Toplam Yarma (m³), Toplam Dolgu (m³), Net, hücre/bölge döküm, renk haritası.
- İş mantığı Core'da; UI yalnız ViewModel/görünüm (maincodex §4.4).

---

## 5. Veri Modeli (`Core/Terrain/Models`)

```
AraziModeli            ← kök: SemaSurum, GeometriRevizyon, Birim,
│                         Noktalar[], Konturlar[], MevcutYuzey(TIN), TasarimYuzeyi, HafriyatSonucu
├── AraziNoktasi       ← X, Y, Z, Kaynak
├── AraziKonturu       ← Poligon(2D), KotM
├── AraziYuzeyi (TIN)  ← Dugumler[], Ucgenler[] (Delaunay)
├── TasarimBolgesi     ← Poligon, HedefKotM, SevEgimi?, Tip(Platform/Sev/Kademe)
└── HafriyatSonucu     ← YarmaHacmiM3, DolguHacmiM3, NetM3, hücre/üçgen detay, KabarmaKatsayisi
```

---

## 6. `.e4arazi.json` Paylaşılan Artifact

- M50 deseni izlenir: JSON, `SemaSurum` migrasyonu + `GeometriRevizyon` artan damga.
- M60 üreticidir; **M61 ve mimari salt-okunur yükler** ("Arazi Yükle").
- Geometri değişikliği yalnız M60'ta yapılır.
- Tüketici hem yolu hem gömülü kopyayı tutar; revizyon farkında "araziden güncelle?" sorulur.
- Mutabakat: tasarım kotu/yüzey değişince bağlı verilere (M61 temel oturtma) uyarı.

### Tüketiciler (cross-başlık)
- `.e4arazi.json` disiplinler arası ortak altlıktır. Tüketiciler: M61 (temel oturtma),
  mimari M30-M39 (tabii zemin/tesviye kotu), **M15 Çevre/Park Aydınlatma** (park DXF
  altlığına arazi kotu → direk dibi/şev kotu, gerçek 3D yerleşim), ileride mekanik (dış
  tesisat güzergâh kotu) ve akustik (M04 arazi topografyası difraksiyon altlığı).
- M15 mevcut "Park DXF yükle" akışına ek olarak **"Arazi Yükle" (.e4arazi.json)** girişi alır;
  arazi varsa direk/armatür yerleşimi gerçek kotlara oturur.
- Tüketici geometriyi değiştirmez; salt-okunur okur. Detay: [_index.md](../_index.md).

---

## 7. Açık Riskler / Kararlar

1. Yeni DXF arazi importer'ı (açık kontur) — mevcut kapalı-yüzey parser'dan ayrı (§3).
2. 3D render altyapısı M35'ten soyutlanıp paylaşılmalı; kod kopyası riski.
3. `.e4arazi.json` paylaşılan salt-okunur artifact kararı **onaylandı** (2026-06-13).
4. İlk sürümde grid hacim yöntemi; TIN-TIN hassas yöntem ileri faz.

---

## 8. Faz Planı (programlama)

Desen: Core-first, UI'siz analitik test (M01/M05/M08 gibi). Her faz `Core.Tests` ile kapanır.

| Faz | Başlık | Çıktı / Tanım | Test (UI'siz) |
|-----|--------|---------------|---------------|
| **F0** ✅ | İskelet & ürün ailesi | `Core/Terrain/Models` + `Services` oluşturuldu; modeller derlenir | derlenir |
| **F1** ✅ | Veri modeli + DXF arazi importer | `AraziModeli/AraziNoktasi/AraziKonturu` + `IAraziGeometri` salt-okunur arayüz; `DxfAraziParser` (kapalı-yüzey parser'dan ayrı): `POINT` spot, `Polyline2D`/`Polyline3D` kontur, kot öncelik zinciri (VertexZ→DuzlemKotu→KatmanAdi→MetinEtiketi→Bilinmiyor), `$INSUNITS` ölçek | 6 Core testi geçer ✅ |
| **F2** ✅ | DTM / TIN üretimi | Kontur densify + **Delaunay (Triangle.NET / `Triangle` 0.0.6-Beta3)** + `(x,y)→z` baricentric interpolasyon. `AraziYuzeyi`/`AraziDugum`/`AraziUcgen`, `AraziYuzeyServisi` | 5 Core testi geçer ✅ (sabit/eğik düzlem tam interpolasyon, hull-dışı null, kontur arası, yetersiz nokta) |
| **F3** ✅ | 3D görselleştirme | `Arazi3DGorunumServisi` (TIN→`ProjectSurface`, Core) + App: `HafriyatWindow` mevcut `GeometryPreviewControl` (Isometric3D) ile TIN render. zoom/pan/orbit kontrolden gelir | 1 Core testi (üçgen→yüzey) + App derlenir ✅ |
| **F4** ✅ | Tasarım yüzeyi (grading) | `TasarimBolgesi` (Platform/Şev/Kademe) + `TasarimYuzeyi.HedefKot(x,y)` (poligon-içi platform kotu). Şev eğimi modeli var, kademe=platform gibi; şev araziye inişi F9 | F5 testleriyle dolaylı doğrulandı ✅ |
| **F5** ✅ | Hafriyat motoru (grid) | `HafriyatSonucu` + `HafriyatHesabiServisi` (ızgara hücre merkezi mevcut−tasarım farkı → yarma/dolgu/net, kapsam-dışı sayımı) | 5 Core testi: saf yarma 1000 m³, saf dolgu, dengeli eğik zemin (net 0), kapsam-dışı, tasarımsız ✅ |
| **F6** ✅ | Sonuç görseli + rapor | Bölge dökümü (`HafriyatBolgeSonucu`), kabarma/sıkışma (`HafriyatKatsayilari`), bank denge + Türkçe özet (`HafriyatRaporServisi`); **yarma/dolgu renk haritası** (`HafriyatRenkHaritasi` → `HitIntensityMap` heatmap, yarma=kırmızı/dolgu=mavi) | 5 Core testi (4 rapor + 1 renk haritası) ✅ |
| **F7** ✅ | Kaydet/aç + `.e4arazi.json` | `AraziGeometriServisi` (Kaydet/Ac/YeniOlustur/Migrate, `GeometriRevizyon` her kayıtta artar); `AraziModeli.Tasarim` artifact'a gömüldü; türetilen TIN/hafriyat serileştirilmez, yüklemede yeniden hesaplanır | 4 Core testi: round-trip (Point2D dahil), revizyon artışı, dosya-yok, yükleme sonrası yeniden hesap ✅ |
| **F8** ✅ | Tüketici köprüsü | `AraziTuketim`/`AraziTuketimServisi` (salt-okunur yükle + KotSorgula + kot aralığı); **M15 "Arazi Yükle"** butonu → direklere `ZeminKotuM` atanır (geriye-uyumlu nullable alan) | 1 Core testi (köprü) + App derlenir ✅ |
| **F9** | İleri | TIN-TIN kompozit hassas hacim; kütle-haul (Bruckner); Metraj/keşif köprüsü; kontur üretme; PDF/kotlu kesit | — |

**Önerilen MVP kesimi:** F0–F7 (içe aktar → yüzey → tasarım → hafriyat → kaydet/paylaş).
F8 M15 entegrasyonunu açar; F9 ileri faz.

**M61 (Bina Temeli Kota Oturtma)** M60 F7 (yani `.e4arazi.json` üretimi) tamamlandıktan sonra
ayrı faz dizisiyle başlar — bkz. [codex61](../61-bina-temeli-kota-oturtma/codex61.md).

### Gerçekleşen kaynak (F0-F1)
- `src/Kst.Acoustics.Core/Terrain/Models/`: `AraziEnums.cs` (AraziVeriKaynagi, AraziKotKaynagi),
  `IAraziGeometri.cs` (salt-okunur arayüzler), `AraziNoktasi.cs`, `AraziKonturu.cs`, `AraziModeli.cs`.
- `src/Kst.Acoustics.Core/Terrain/Services/`: `DxfAraziParser.cs`, `AraziImportSonucu.cs`.
- Testler: `Kst.Acoustics.Core.Tests/Program.cs` içinde `M60*` (6 test).
- Not: mevcut kapalı-yüzey `DxfKatPlaniParser`'a DOKUNULMADI; arazi parser ayrı. Varsayılan `"0"`
  katmanı kot kaynağı sayılmaz (yanlış sıfır-kot tuzağı kapatıldı). Unitless arazi varsayılanı 1 birim = 1 m.

### Gerçekleşen kaynak (F2)
- `Terrain/Models/AraziYuzeyi.cs`: `AraziDugum`, `AraziUcgen`, `AraziYuzeyi` (+ `KotSorgula` baricentric).
- `Terrain/Services/AraziYuzeyServisi.cs`: kontur yoğunlaştırma (varsayılan 5 m) + Triangle.NET Delaunay.
- **Bağımlılık:** `Triangle` 0.0.6-Beta3 (TriangleNet) `Core.csproj`'a eklendi. Vertex Z taşımadığından
  Z koordinat-anahtarlı sözlükle geri eşlenir (saf Delaunay Steiner noktası eklemez → köşeler birebir).
- `KotSorgula` şu an O(N) üçgen taraması; ileride spatial index (F9).

### Gerçekleşen kaynak (F4-F5)
- `Terrain/Models/TasarimYuzeyi.cs`: `TasarimBolgeTipi`, `TasarimBolgesi`, `TasarimYuzeyi` (+ `HedefKot`).
- `Terrain/Models/HafriyatSonucu.cs`: yarma/dolgu/net + hücre sayıları + kapsam-dışı.
- `Terrain/Services/HafriyatHesabiServisi.cs`: grid yöntemi (varsayılan 1 m), orta-nokta yaklaşımı.
- Nokta-içinde-poligon ve alan için mevcut `Common/Geometry/PoligonYardimci` kullanıldı.
- Konvansiyon: yarma = mevcut > tasarım (kazı); dolgu = mevcut < tasarım. Net = yarma − dolgu.

### Gerçekleşen kaynak (F7)
- `Terrain/Services/AraziGeometriServisi.cs`: M50 `BinaGeometriServisi` deseni; `.e4arazi.json`
  JSON (WriteIndented + JsonStringEnumConverter), `GuncelSemaSurum=1`, Migrate(0→1).
- `AraziModeli.Tasarim` (TasarimYuzeyi) artifact'a gömüldü → dosya kendine-yeter.
- TIN (`AraziYuzeyi`) ve `HafriyatSonucu` serileştirilmez; yüklemede servislerle yeniden üretilir
  (test M60AraziYuklemeSonrasiHesap bunu kanıtlar).

### Gerçekleşen kaynak (F6 Core)
- `Terrain/Models/HafriyatSonucu.cs`: `HafriyatBolgeSonucu` + `BolgeSonuclari` (bölge bazlı döküm).
- `Terrain/Models/HafriyatRaporu.cs`: `HafriyatKatsayilari` (kabarma/sıkışma %), `HafriyatRaporu` (gevşek/bank/denge).
- `Terrain/Services/HafriyatRaporServisi.cs`: katsayı uygulama + bank denge + Türkçe özet metni (tr-TR).
- `TasarimYuzeyi.HedefBolge(x,y)` eklendi; motor hücreyi içeren bölgeye atar.
- Konvansiyon: gevşek = yarma×(1+kabarma); dolgu için gereken bank = dolgu×(1+sıkışma);
  denge = yarma − gereken bank (pozitif=fazla/depo, negatif=ödünç).

### Gerçekleşen kaynak (App, F3 + F6 rapor)
- `Core/Terrain/Services/Arazi3DGorunumServisi.cs`: TIN üçgenleri + platform poligonları → `ProjectSurface`.
- `App/Terrain/Views/TerrainLauncherWindow.axaml(.cs)`: Terrain modül merkezi (M60 kartı + M61 yakında).
- `App/Terrain/Views/HafriyatWindow.axaml(.cs)`: DXF yükle (Task.Run) → TIN izometrik önizleme →
  hedef kot + kabarma/sıkışma girişi → Hesapla → `RaporText` özet → `.e4arazi.json` kaydet.
  MVP: tasarım bölgesi = arazi sınır kutusu tek platform; ızgara ~150×150 ile otomatik.
- `App/Views/EngineeringHomeWindow`: Terrain disiplin kartı + `OpenTerrain_OnClick` eklendi.
- Uçtan uca gerçek DXF doğrulaması yapıldı (3 kontur + spot → TIN → yarma/dolgu analitikle ±%0,02).

### Gerçekleşen kaynak (F8 köprü)
- `Core/Terrain/Services/AraziTuketimServisi.cs`: `AraziTuketim` (salt-okunur, KotSorgula + KotAraligi) + yükleyici.
- `Core/Electric/CevreAydinlatma/CevreAydinlatmaModels.cs`: `AydinlatmaDiregi.ZeminKotuM` (nullable, geriye-uyumlu).
- M15 App: `CevreAydinlatmaViewModel.AraziYukleVeKotAta` + pencere "Arazi Yükle (.e4arazi)" butonu +
  durum çubuğunda `AraziBilgisi` (rev, kot aralığı, üçgen sayısı). Arazi geometrisi DEĞİŞTİRİLMEZ.

### Gerçekleşen kaynak (F6 renk haritası)
- `Arazi3DGorunumServisi.HafriyatRenkHaritasi(yuzey, tasarim)`: üçgen merkezi diff → [0..1] yoğunluk
  (0.5 nötr, dolgu→mavi, yarma→kırmızı), id'ler `YuzeyleriOlustur` ile hizalı.
- `HafriyatWindow`: Hesapla'da `Onizleme3D.HitIntensityMap` set (mevcut heatmap rendering), DXF yüklemede temizlenir.

### Kalan adımlar (ileri / opsiyonel)
- **İnteraktif tasarım** — sınır kutusu yerine kullanıcı poligonu/çok platform/şev çizimi (canvas etkileşimi).
- **F9** (ileri): TIN-TIN hassas hacim, şev araziye iniş, spatial index, kütle-haul (Bruckner), metraj köprüsü.

> **Mimari:** App katmanı yalnız Core servislerini çağırır; tüm hesap (TIN, hafriyat, rapor) Core'da
> ve testlidir. İçe aktar → TIN → tasarım → hafriyat → rapor → `.e4arazi.json` zinciri 30 testle + gerçek DXF ile doğrulandı.


<!-- ============================================================ -->
# 📄 DOSYA: `terrain/61-bina-temeli-kota-oturtma/codex61.md`
<!-- ============================================================ -->

# M61 Codex — Bina Temeli Kota Oturtma

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Yalnızca güncel kararlar, kapsam,
sistem davranışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

> **Durum:** Geliştiriliyor — F0+F1+F2 Core tamam (7 test). Arazi `AraziTuketim` köprüsü, bina `.e4bina` (M50) → `BinaAyakIzi` adaptörü, oturtma motoru. Kalan: F3 kot düzenleme UI, F4 kademeli kesit, App; mimari M35 adaptörü (ops).
> **Bağlı codex'ler:** [codex60](../60-hafriyat-hesapla/codex60.md) (arazi kaynağı),
> [codex50](../../ortak-moduller/50-bina-geometri/codex50.md) (bina kaynağı),
> [codex30](../../mimari/codex30.md) (mimari bina kütlesi)

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Terrain |
| Modül | M61 — Bina Temeli Kota Oturtma |
| Amaç | Hafriyat sonrası arazi yüzeyine bina temelini oturtmak; bodrum, giriş/çıkış, yangın merdiveni kotlarını ayarlamak |
| Proje uzantısı | `.e4p61` |
| Girdiler (salt-okunur) | `.e4arazi.json` (M60), `.e4bina.json` (M50) veya mimari M35 bina kütlesi |
| Stack / UI dili | C# + Avalonia (net10.0) / Türkçe |
| Çekirdek | `src/Kst.Acoustics.Core/Terrain/` · UI: `src/Kst.Acoustics.App/Terrain/` |

---

## 2. Modül Kapsamı

Aktif kapsam (MVP hedefi):
- Salt-okunur iki girdiyi birleştir: tasarım (hafriyat sonrası) arazi yüzeyi + bina ayak izi/temel
- Bina ayak izini araziye oturt → **±0.00 datum** atama
- Bodrum kotu, giriş/çıkış kotu, yangın merdiveni çıkış kotu ayarı
- Eğimli arazide **kademeli bina** desteği (kat/blok bazlı farklı oturma kotları)
- Otomatik kontroller: bodrum tabanı zemin altında mı, giriş kotu uyumu, gerekli kazı derinliği
- Çıktı: bina-arazi kotlu kesiti + temel altı yarma/dolgu hacmi (M60'a geri besleme verisi)

İleri/eksik alanlar:
- İmar tabii zemin/tesviye kotu denetimi (Planlı Alanlar İmar Yönetmeliği)
- Çok bloklu/kademeli site otomatik kot dağılımı
- Rampa ve erişim eğim kontrolleri (erişilebilirlik)

---

## 3. Mimari Kararlar

- Girdilerin ikisi de **salt-okunur**: arazi M60'ta, bina M50/mimaride düzenlenir; M61 yalnız kot oturtma yapar.
- Geometri değişikliği bu modülde YAPILMAZ; yalnız oturma kotu/datum ve kontrol verisi üretilir.
- Hesap/kontrol motoru `Core/Terrain/`'de; UI yalnız ViewModel/görünüm (maincodex §4.4).
- Revizyon farkı: arazi veya bina güncellenmişse açılışta "güncellenmiş, yeniden oturtulsun mu?" uyarısı.

---

## 4. Veri Modeli (taslak, `Core/Terrain/Models`)

```
TemelOturtmaModeli   ← kök: SemaSurum, AraziRevizyon, BinaRevizyon, SifirKotM (±0.00)
├── BlokOturma       ← BlokId, OturmaKotM, KademeNo
├── KotTanimi        ← Tip(Bodrum/Giris/Cikis/YanginMerdiveni), KotM
└── OturtmaKontrol   ← KaziDerinligiM, ZeminAltiMi, Uyari[]
```

---

## 5. Faz Planı (M60 F7 sonrası başlar)

| Faz | Başlık | Çıktı |
|-----|--------|-------|
| **F0** ✅ | İskelet | `Core/Terrain` altında modeller + servis |
| **F1** ✅ | Girdi köprüleri | Arazi: `AraziTuketim` (M60 köprüsü). Bina: `BinaAyakIziAdaptoru` (M50 `.e4bina` `BinaGeometri` → `BinaAyakIzi`: en alt kat ayak izi + kat sayıları, salt-okunur). Mimari M35 adaptörü ops. | 2 Core testi (tek oda/çok oda bbox + uçtan uca oturtma) ✅ |
| **F2** ✅ | Oturtma motoru (Core) | `TemelOturtmaServisi`: ±0.00 datum (ortalama zemin veya override), bodrum taban/çatı kotu, kazı derinliği, serbest yükseklik, kademe sırası, giriş/çıkış/yangın merdiveni/bodrum kot tanımları, gömülü/kazısız uyarıları — 5 Core testi ✅ |
| **F3** | Kot tanımları (UI) | Bodrum/giriş/çıkış/yangın merdiveni kotu düzenleme + otomatik uyarı (App) |
| **F4** | Kademeli bina | Blok bazlı oturma kotu görseli (eğimli arazi kesiti) |
| **F5** | Çıktı | Bina-arazi kotlu kesit + temel altı yarma/dolgu (M60'a geri besleme) |

## Gerçekleşen kaynak (F0+F1+F2)
- `Core/Terrain/Models/TemelOturtma.cs`: `BinaAyakIzi`, `KotTipi`, `KotTanimi`, `BlokOturma`, `TemelOturtmaModeli`.
- `Core/Terrain/Services/TemelOturtmaServisi.cs`: `OturtBlok` (tek blok) + `Oturt` (çok blok, kademe sırası).
- `Core/Terrain/Services/BinaAyakIziAdaptoru.cs`: M50 `BinaGeometri` → `BinaAyakIzi` (en alt kat ayak izi:
  tek oda→poligon, çok oda→sınır kutusu; KatNo≥0 üst kat, KatNo<0 bodrum; ortalama kat yüksekliği).
- Arazi kotu örnekleme: ayak izi köşeleri + merkezi `AraziTuketim.KotSorgula` ile; kapsam-dışı uyarısı.
- Konvansiyon: ±0.00 = `sifirKotOverride ?? ortalamaZemin`; bodrum taban = ±0.00 − bodrumKat×katY;
  kazı derinliği = max(0, zemin − bodrumTaban); serbest yükseklik = ±0.00 − zemin (negatif → gömülü uyarı).
- Testler: `Core.Tests/Program.cs` `M61*` (helper `M61Blok`/`TuketimDuz`/`TuketimEgik`).

## 6. Açık Riskler / Kararlar

1. Bina kaynağı: M50 `.e4bina.json` mi, mimari M35 kütlesi mi, ikisi de mi? (M60 sonrası netleşecek.)
2. Kademeli bina kesiti çizimi M60'ın kotlu-kesit altyapısını paylaşmalı.
3. M61, M60 üretime geçtikten sonra başlar (arazi yüzeyi hazır olmadan oturtma anlamsız).


<!-- ============================================================ -->
# 📄 DOSYA: `ortak-moduller/50-bina-geometri/codex50.md`
<!-- ============================================================ -->

# CODEX 50 — Bina Geometrisi (Ortak Modül)

> **Durum:** Tamamlandı — Faz 1-5 bitti, M12/M08 köprüleri kurulu (2026-06-13). Aktif/üretimde.
> **Bağlı codex'ler:** [codex8](../../akustik/08-bina-gurultu/codex8.md), [codex12](../../elektrik/12-basit-proje-ciz/codex12.md)
> **Çekirdek:** `src/Kst.Acoustics.Core/Bina/` · **UI:** `src/Kst.Acoustics.App/Bina/`

---

## 1. Amaç

Bina geometrisi işlemlerinin (DXF okuma, oda blokları, açıklıklar, kat yönetimi, oda adlandırma)
**tek merkezi** olmak. Bugüne kadar M08 ve M12 bu işlevleri kendi içlerinde, kısmen kopya kodla
ve çift yönlü Core bağımlılığıyla taşıyordu. M50 ile:

- Her ana başlığın (akustik / elektrik / mekanik / mimari / inşaat) modül girişinde en üstte
  **"Bina Geometrisi Oluştur"** girişi bulunur → M50 penceresi açılır.
- Kullanıcı binayı M50'de hazırlar ve **`.e4bina.json`** dosyası olarak kaydeder.
- Herhangi bir modülde çalışacak kişi işe **"Bina Geometrisi Yükle"** ile başlar.
- **Geometri değişikliği yalnız M50'de yapılır.** Diğer modüllerden oda poligonu, kat,
  açıklık ve oda adı düzenleme komutları kaldırılır; yalnız "M50'de Düzenle" kısayolu kalır.

## 2. Kapsam

### M50'de yapılan işlemler (geometrinin tamamı)
| İşlem | Kaynağı (taşınacağı yer) |
|---|---|
| DXF kat planı içe aktarma (ölçek/birim tespiti, fallback zinciri) | M08 `DxfKatPlaniParser` |
| Flood-fill (boşluk tarama) oda tespiti | `Electric/Ortak/BosluktanOdaTespitServisi` |
| Segment sadeleştirme (5 cm altı eleme, 200k tavan) | M12 `DxfSegmentYardimci` |
| Açıklık tespiti (kapı yayı arc + duvar boşluğu) + manuel düzeltme | M12 `AciklikTespitServisi` + `AciklikYardimci` |
| Açıklık tanımı: kapı/pencere tipi + **genişlik/yükseklik/eşik boyutları** | M08 `BolmeAcikligi` boyut modeli |
| Kat ekle / sil / yukarı-aşağı taşı / yeniden numaralandır | M12 ViewModel |
| Oda çizimi (poligon), vertex/edge düzenleme, oda silme | M12 `OdaCizim` + M08 kopyası (tekleşir) |
| Oda adlandırma (DXF TEXT eşleme + manuel) | her ikisi |
| Kat hizalama (`OriginOffset`) | her ikisi |
| **Döşeme/tavan patlatma (düşey mekan birleşimi)** | M08 `DuseyMekanBirlesimi` + servisi |
| Undo snapshot (lazy Snap deseni) | M12 |
| 2D/3D önizleme (temel prizma + DXF arka plan) | M12 `BasitProjeCizGeometriServisi` |

### M50'de YAPILMAYAN işlemler (modüllerde kalır)
- Malzeme / yapı elemanı ataması (M08: duvar/açıklık `YapiElemani`; M12: armatür vb.)
- Bölme (duvar çifti) üretimi ve komşuluk tespiti — M08 geometriden türetir (`KomsuluklariTespitEt`)
- Akustik sınıflandırma, elektrik elemanları, devreler, kablolama, mahal listesi
- Her türlü hesap ve rapor

## 3. Mimari Kararlar

### 3.1 Veri modeli (`Core/Bina/Models`)
```
BinaGeometri            ← kök: SemaSurum, GeometriRevizyon, Katlar[], DuseyBirlesimler[]
├── BinaKat             ← Id, Ad, KatNo, KatYuksekligiM, OriginOffset, DxfFilePath,
│                         DxfGoster/BloklariGoster (INPC), Odalar[], Acikliklar[],
│                         DxfSegmentler (sadeleşmiş), DxfArclar
├── BinaOda             ← Id, Ad (INPC), Poligon, AlanM2, Merkez,
│                         YukseklikOverrideM? (null = kat yüksekliği)
├── BinaAciklik         ← Id, Bas/Bit segmenti, Tip (Kapı/Pencere),
│                         GenislikM (segmentten), YukseklikM, EsikYuksekligiM,
│                         IptalEdildi, ManuelEklendi, Kaynak
└── DuseyBirlesim       ← M08 DuseyMekanBirlesimi'nden taşınır: üyeler (KatNo+OdaId,
                          TavanAcik/DosemeAcik), geri-alma derin kopyaları
```

### 3.2 `.e4bina` dosya formatı
- JSON, uzantı `.e4bina.json`. `SemaSurum` migrasyon deseni baştan dahil (M12'de kanıtlandı).
- `GeometriRevizyon`: her kayıtta artan damga. Modül projesi yüklediği revizyonu saklar;
  açılışta fark varsa "bina geometrisi güncellenmiş" uyarısı.

### 3.3 Modüllerin tüketimi: kopya + referans birlikte
Modül proje dosyası hem `.e4bina` **yolunu** hem içeriğin **gömülü kopyasını** tutar.
Dosya taşınsa/silinse bile modül projesi açılır; revizyon farkı varsa kullanıcıya
"dosyadan güncelle?" sorulur.

### 3.4 Salt-okunurluk
- Modüllere geometri **read-only arayüz** üzerinden verilir (`IBinaKat`, `IBinaOda`,
  `IBinaAciklik`) — derleme düzeyinde koruma.
- M08/M12 ViewModel'lerindeki `UpdateVertex/DeleteVertex/UpdateEdge/InsertVertexAfter`,
  `OdaEkle*`, `KatEkle/KatSil/KatYukari/KatAsagi`, oda adı düzenleme, açıklık tespit/düzeltme
  komutları M50'ye taşınır, modüllerden silinir.

### 3.5 Id kararlılığı + mutabakat
1. M50'de düzenleme (poligon, ad, kat taşıma) **Id'leri asla değiştirmez**.
2. Yeniden DXF taraması / flood-fill: yeni poligonlar eski odalarla **merkez-örtüşme**
   üzerinden eşleştirilip Id devralır; eşleşmeyenler silinmiş sayılır.
3. Modül güncellenmiş `.e4bina` yüklerken **mutabakat raporu** gösterir
   ("2 oda silinmiş → bağlı 14 eleman askıda; 3 yeni oda eklendi"). Askıdaki domain
   verisinin kaderine kullanıcı karar verir. Mutabakat servisi `Core/Bina`'da ortaktır;
   modüller domain verilerini `IGeometriBagimliVeri` arayüzüyle bildirir.

### 3.6 Açıklıklar (kesinleşen karar, 2026-06-12)
- Kapı/pencere **tanımı ve boyutları** (genişlik/yükseklik/eşik) yalnız M50'de girilir.
- **M08**: açıklığa yalnız malzeme (`YapiElemani`) ataması yapar — `AciklikId → YapiElemani`
  eşlemesi M08 projesinde tutulur.
- **M12**: açıklığa hiçbir müdahale edemez; açıklıkları yalnız no-go bölgesi ve kapı-yanı
  yerleşim referansı olarak okur.

### 3.7 Döşeme/tavan patlatma (kesinleşen karar, 2026-06-12)
- M08'deki düşey mekan birleşimi (patlatma + geri alma + `TavanAcik/DosemeAcik` bayrakları)
  **M50'ye taşınır**, geometrinin parçasıdır; iki modül de ortak kullanır.
- M08 tüketimi mevcut davranışı korur (hacim birleştirme, açık döşeme/tavan yüzey eleme).
- **M12 davranışı (kesinleşen karar, 2026-06-13 — 5.2):**
  1. M12 birleşim kayıtlarını bağlı geometrinin gömülü kopyasından okur
     (`Proje.BinaGeometriKopya.DuseyBirlesimleri`) — kendi patlatma verisi tutmaz.
  2. **Tavan montajlı otomatik yerleşim** (armatür, tavan dedektörü vb.): tavanı açık
     (`TavanAcik=true`) odalarda atlanır + uyarı üretilir — fiziksel tavan yok, eleman
     üst odanın tavanından servis eder. Elle yerleştirme engellenmez (asma kiriş vb.
     istisnalar için).
  3. **Duvar montajlı yerleşim** (priz/anahtar/duvar armatürü): etkilenmez — duvarlar
     yıkıcı olmayan birleşimde korunur.
  4. **Mahal listesi ve devreler**: odalar ayrı mahal/devre birimi olarak kalır
     (Id'ler bağımsız); hacim birleşimi elektrik hesaplarını değiştirmez.
  5. M12'de patlatma kilidi yok (2.7 kararıyla tutarlı: kilit yerine oda-silme kancası).

## 4. Geçiş Planı (Fazlar)

| Faz | İş | Not |
|---|---|---|
| 1 | Core taşıma: `BosluktanOdaTespitServisi`, `DxfSegmentYardimci`, `AciklikTespitServisi`, `AciklikYardimci`, `DxfKatPlaniParser` → `Core/Bina`; ortak modeller + `DxfKatYukleyici` tek yükleme hattı | Eski konumlarda delegasyon bırakılır; davranış değişmez |
| 2 | M50 modülü: bağımsız pencere + `.e4bina` kaydet/aç; editör kodu ağırlıkla M12'den taşınır (en olgun gerçekleme); patlatma M08'den taşınır | Undo M12 lazy-Snap deseni |
| 3 | M12 geçişi: "Bina Geometrisi Yükle" akışı; geometri düzenleme sökülür; eski `.e4p12` açılışta gömülü geometriyi otomatik `.e4bina`'ya dışa aktarır (`SemaSurum` migrasyonu) | Veri kaybı yok |
| 4 | M08 geçişi: `BpcKatToM08Kat` silinir → `BinaKat → Mekan/Bolme` türetme adapteri; çapraz bağımlılık tamamen kalkar | `KomsuluklariTespitEt` geometri-yükleme olayına bağlanır |
| 5 | Yeni modüller (M13/M15/M21+) doğrudan "geometri yükle" API'si ile başlar | Kendi editörünü yazmak yasak |

## 5. Riskler / Dikkat Noktaları
- **Dosya uyumluluğu:** mevcut `.e4p12` ve M08 proje JSON'ları kırılmamalı — Faz 3/4
  migrasyonları zorunlu.
- **NormalizeOrigin farkı:** M08 kat başına normalize eder, M12 CAD ortak çerçevesini korur.
  `DxfKatYukleyici`'de seçenek olarak kalır; M50 varsayılanı M12 davranışı (ortak çerçeve)
  olmalı, çünkü çok katlı hizalama esas senaryo.
- **Performans:** oda/açıklık tespiti TAM segment setinde koşar, saklama/render sadeleşmiş
  sette (M12'deki ayrım korunur — 20-40 MB DXF / ~1M segment gerçeği).
- **Patlatma kilidi:** M08'de patlatma `ProjeKilitli`'yi tetikliyor; bu kilit mantığının
  M50'deki karşılığı (geometri kilidi mi, modül kilidi mi) Faz 2'de netleşecek.

## 6. İş Programı (İnce Fazlı)

> Her adım tek başına tamamlanabilir ve bitiminde **derleme yeşil + mevcut davranış değişmemiş**
> olmalıdır. Oturum limitinde herhangi bir adım sınırında beklenebilir. Bir adıma başlarken
> "Durum" sütunu güncellenir.

### Faz 1 — Core Taşıma (davranış değişikliği yok)
| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 1.1 | `Core/Bina/Models` oluştur: `BinaGeometri`, `BinaKat`, `BinaOda`, `BinaAciklik`, `DuseyBirlesim` + read-only arayüzler (`IBinaKat` vb.). Yalnız YENİ dosyalar — hiçbir mevcut kod değişmez | Modeller derlenir, birim testleri (alan/merkez/serileştirme) geçer | ✅ Tamamlandı |
| 1.2 | `BosluktanOdaTespitServisi` + `DxfSegmentYardimci`'yı `Electric/Ortak` → `Core/Bina/Services`'a taşı; eski konumda delegasyon bırak | M12 mevcut testleri/davranışı aynen geçer | ✅ Tamamlandı |
| 1.3 | `AciklikTespitServisi` + `AciklikYardimci`'yı `Core/Bina/Services`'a taşı; `BpcAciklik` yerine `BinaAciklik` ile çalışır hale getir; eski konumda dönüştürücü delegasyon | M12 açıklık tespiti aynı sonuçları verir | ✅ Tamamlandı |
| 1.4 | `DxfKatPlaniParser`'ı `Core/Bina/Services`'a taşı; `Electric.*` using'leri kalkar; çıktısı `BinaKat` olur; eski `Parse` imzası delegasyonla korunur | M08 + M12 kat yükleme davranışı değişmez | ✅ Tamamlandı |
| 1.5 | `DxfKatYukleyici` tek yükleme hattı: parser + flood-fill + açıklık tespiti + sadeleştirme, `Ayarlar { NormalizeOrigin, SegmentSadelestir, AciklikTespit }` | Aynı DXF'lerde M12 `KatYukle` ile birebir aynı sonuç (karşılaştırma testi) | ✅ Tamamlandı |

### Faz 2 — M50 Modülü
| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 2.1 | `.e4bina.json` formatı: serileştirme, `SemaSurum`, `GeometriRevizyon`, kaydet/aç servisi | Kaydet→aç round-trip testi geçer | ✅ Tamamlandı |
| 2.2 | M50 pencere iskeleti (`App/Bina`): boş pencere, kat listesi paneli, durum çubuğu + ana başlık girişlerine "Bina Geometrisi Oluştur" butonu | Pencere tüm başlıklardan açılır | ✅ Tamamlandı |
| 2.3 | DXF kat yükleme + 2D viewport: DXF arka plan çizgileri + oda blokları + oda seçimi (render kodu M12'den uyarlanır) | Örnek DXF'ler yüklenip görüntülenir | ✅ Tamamlandı |
| 2.4 | Kat yönetimi UI: ekle/sil/yukarı-aşağı/yeniden numaralandır, kat yüksekliği, `OriginOffset` hizalama, DxfGoster/BloklariGoster | M12'deki kat yöneticisi işlevleriyle eşdeğer | ✅ Tamamlandı |
| 2.5 | Oda düzenleme: oda çizim modu, vertex/edge sürükleme, köşe ekle/sil, oda silme, oda adlandırma (DXF TEXT + manuel) | M12 `OdaCizim` işlevleriyle eşdeğer | ✅ Tamamlandı |
| 2.6 | Açıklık yönetimi: otomatik tespit (filtre parametreli), manuel ekle/iptal, kapı/pencere tipi + **genişlik/yükseklik/eşik girişi** | Açıklıklar tespit edilir, boyutlar girilip kaydedilir | ✅ Tamamlandı |
| 2.7 | Döşeme/tavan patlatma: `DuseyBirlesim` M08'den taşınır — patlat/geri al UI + Id kararlılığı; kilit mantığı kararı burada verilir | M08'deki patlatma senaryoları M50'de çalışır | ✅ Tamamlandı |
| 2.8 | Undo snapshot (M12 lazy-Snap deseni) + 3D önizleme + tam kaydet/aç entegrasyonu | M50 uçtan uca: DXF→düzenle→patlat→kaydet→aç | ✅ Tamamlandı |

### Faz 3 — M12 Geçişi
| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 3.1 | M12'ye "Bina Geometrisi Yükle": `.e4bina` seç, yol + gömülü kopya proje dosyasına yazılır, revizyon damgası saklanır | Geometri yüklenir, elemanlar `OdaId` ile bağlanabilir | ✅ Tamamlandı |
| 3.2 | `.e4p12` migrasyonu: eski proje açılışında gömülü geometri otomatik `.e4bina`'ya dışa aktarılır (`SemaSurum` artar) | Eski projeler veri kaybı olmadan açılır | ✅ Tamamlandı |
| 3.3 | M12'den geometri düzenleme sökümü: vertex/oda/kat/açıklık komutları kaldırılır, "M50'de Düzenle" kısayolu eklenir; açıklığa müdahale tamamen kapanır | M12'de geometriyi değiştiren hiçbir yol kalmaz | ✅ Tamamlandı |
| 3.4 | Mutabakat raporu: `IGeometriBagimliVeri` arayüzü + M12 uygulaması (elemanlar/devreler/mahal); güncellenmiş `.e4bina` yüklemede askıda-veri diyaloğu | Oda silinmiş senaryoda rapor doğru çıkar | ✅ Tamamlandı |
| 3.5 | M12 regresyon: mevcut örnek projelerle yerleşim/kablolama/mahal listesi doğrulaması | Sonuçlar geçişten önceyle aynı | ✅ Tamamlandı |

### Faz 4 — M08 Geçişi
| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 4.1 | `BinaKat → Mekan/Bolme` adapteri: geometri yüklendiğinde/güncellendiğinde `KomsuluklariTespitEt` koşar; `Mekan` akustik alanlara inceltilir | Bölme üretimi eski sonuçlarla birebir | ✅ Tamamlandı |
| 4.2 | Açıklık malzeme ataması: `BolmeAcikligi` boyutları M50'den okunur; M08 yalnız `AciklikId → YapiElemani` eşlemesi tutar | Boyutlar M08'de salt-okunur görünür, malzeme atanır | ✅ Tamamlandı |
| 4.3 | M08 proje migrasyonu + "Bina Geometrisi Yükle" akışı (yol + kopya + revizyon) | Eski M08 projeleri kayıpsız açılır | ✅ Tamamlandı |
| 4.4 | M08'den geometri düzenleme sökümü; `BpcKatToM08Kat` silinir; patlatma M50 modeline bağlanır; çapraz bağımlılık sıfır | `BuildingAcoustics ↔ Electric` arasında using kalmaz | ✅ Tamamlandı |
| 4.5 | M08 golden test regresyonu: DnT,w / L'nT,w / cephe hesapları geçiş öncesiyle aynı | Tüm golden testler geçer | ✅ Tamamlandı |

### Faz 5 — Kapanış
| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 5.1 | Faz 1'de bırakılan delegasyon katmanlarının temizliği (artık çağıran kalmadıysa) | Ölü kod yok, derleme yeşil | ✅ Tamamlandı |
| 5.2 | M12 patlatma davranışı tanımı (§3.7 açık konu) — karar + uygulama | Karar günlüğüne işlenir | ✅ Tamamlandı |
| 5.3 | Belge güncellemeleri: codex8/codex12 ilgili bölümler M50'ye yönlendirilir, kullanım kılavuzu | Belgeler tutarlı | ✅ Tamamlandı |

## 7. Kullanım Kılavuzu (özet)

### M50'de geometri hazırlama
1. Herhangi bir ana başlık girişinden **⬡ Bina Geometrisi** ile M50'yi aç.
2. **DXF Kat Planı Yükle** ile katları sırayla ekle (odalar + açıklıklar otomatik taranır)
   veya boş kat ekleyip **✏ Oda Çiz** ile elle çiz.
3. Düzenleme: köşe sürükle · kenara çift tık = köşe ekle · köşeye sağ tık = köşe sil ·
   araç çubuğundan oda adı/silme · AÇIKLIK çubuğundan tara / + Kapı / + Pencere /
   tip-boyut-eşik düzenleme · ⬆⬇ Tavan/Döşeme Patlat · ↶↷ undo (Cmd/Ctrl+Z).
4. **🧊 3D Önizleme** ile kontrol et, **Kaydet** ile `.e4bina.json` üret
   (her kayıt `GeometriRevizyon`'u artırır).

### Modüllerde kullanma (M12 / M08)
- Araç çubuğundaki **"Bina Geo." / "Bina Geometrisi Yükle"** ile `.e4bina` seç:
  yol + revizyon damgası + gömülü kopya projeye yazılır; kat/oda Id'leri korunur,
  modül verileri (eleman/devre/mahal, malzeme atamaları) dokunulmaz.
- Yüklemeden önce **mutabakat raporu**: yeni geometride karşılığı olmayan oda bağları
  listelenir; "Yine de Yükle" veya "Vazgeç".
- Proje açılışında bağlı `.e4bina` daha yeni revizyondaysa durum çubuğunda uyarı çıkar;
  **"M50'de Düzenle"** kısayolu geometriyi M50'de açar (modül penceresi kapanmaz).
- Eski projeler (`.e4p12`/`.e4p08`): ilk açılışta gömülü geometri otomatik `.e4bina`'ya
  dışa aktarılır ve bağlanır; kalıcılaşması için projeyi kaydetmek yeterli.

## 8. Karar Günlüğü
| Tarih | Karar |
|---|---|
| 2026-06-12 | M50 ortak modül kararı; kod 50, ortak-moduller bandı açıldı |
| 2026-06-12 | Kopya + referans birlikte (modül projesi yol + gömülü kopya tutar) |
| 2026-06-12 | `BinaOda.YukseklikOverrideM` nullable — oda bazlı yükseklik desteklenir |
| 2026-06-12 | Açıklık tanımı + boyutları yalnız M50; M08 yalnız malzeme atar; M12 müdahale edemez |
| 2026-06-12 | Döşeme/tavan patlatma M50'ye taşınır; M12 patlatma davranışı SONRA tanımlanacak |
| 2026-06-13 | §3.7 kapandı: M12'de tavanı açık odada tavan montajlı otomatik yerleşim atlanır; duvar montaj + mahal/devre birimleri değişmez; kayıt kaynağı gömülü M50 kopyası (5.2) |


<!-- ============================================================ -->
# 📄 DOSYA: `insaat/_index.md`
<!-- ============================================================ -->

# İnşaat — Ana Başlık 5

**Modül aralığı:** 41–49  
**Durum:** Boş — modüller oluşacak

## Modüller

| # | Klasör | Modül Adı | Durum |
|---|--------|-----------|-------|
| 41–49 | — | _planlanıyor_ | — |
