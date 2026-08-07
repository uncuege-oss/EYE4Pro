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
