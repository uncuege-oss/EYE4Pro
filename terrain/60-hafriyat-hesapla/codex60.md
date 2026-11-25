# M60 Codex — Hafriyat Hesapla (Yarma/Dolgu)

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir;
yalnızca güncel kararlar, kapsam, sistem davranışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

> **Durum:** F0-F8 TAMAM (planlı MVP fazlarının tümü). Terrain başlatıcı + M60 Hafriyat penceresi (DXF→TIN→hedef kot→hesap→**yarma/dolgu renk haritası**→rapor→.e4arazi) + M15 "Arazi Yükle" köprüsü çalışıyor. 32 Core testi geçer; App 0 hata derlenir; gerçek DXF ile uçtan uca doğrulandı. Kalan (ileri/opsiyonel): interaktif platform/poligon/şev çizimi, F9, M61.
> **Bağlı codex'ler:** [codex50](../../ortak-moduller/50-bina-geometri/codex50.md) (desen referansı),
> [codex61](../61-bina-temeli-kota-oturtma/codex61.md) (tüketici)

## 5 Kasım 2026 — DWG kot okuma ve mevcut arazi modeli

- M60 artık DWG/DXF açar. `MimariCadDosyasi` üzerinden ODA/AutoCAD ile geçici DXF dönüşümü kullanılır; kaynak DWG değiştirilmez. M01 kapalı yüzey import kuralı değişmedi.
- İçe aktarma ekranı birim, katman ve kot yazısı seçimini gösterir. Tanımsız birim açık seçim ister. Kontur katmanları bulunan karma çizimlerde yalnız konturlar önerilir; bina/terasman kotları kendiliğinden mevcut zemine eklenmez.
- `DxfAraziParser`: POINT, 2B/3B polyline, 3DFACE ve blok içeriği; TEXT/MTEXT kotları, nokta-yazı ve kontur-yazı eşleştirmesi. Değişken Z'li 3B polyline düzleştirilmez. Kot yazıları metre kabul edilir. Bağımsız yazı konumları için uyarı gösterilir.
- `AraziModeli.Kaynak`: özgün CAD yolu, çizim birimi, ölçek, seçilen katmanlar, metin seçeneği kayıt/aç boyunca korunur. Geometrik nokta ve konturlarda kaynak katman tutulur.
- TIN üretimi sonlu koordinat, aynı XY'de çelişen kot ve örnekleme sınırı kontrolü yapar. `AraziKotIndeksi` hafriyat hücrelerini ve plan imlecini değişmez BVH üzerinden sorgular.
- Kot Planı: kot etiketleri, renkli yüzey, yakınlaştırma/kaydırma, platform sınırı çiz/tamamla/iptal, tüm ölçüm kapsamı. 3B görünüm ayrı sekmedir. Varsayılan dışbükey sınır parsel sınırı değildir.
- `AraziTasarimServisi`: alan ağırlıklı ortalama kot önerisi ve geçerli basit poligonla tek platform tasarımı. Hedef kot önerisi, hesapla uygulanana kadar kayıtlı tasarım değildir. Mevcut arazi boş tasarım bölgeleriyle kaydedilebilir. Değişen parametreler eski hacim raporunu geçersiz kılar.
- `.e4arazi.json` aç/kaydet ve TIN düğümlerini CSV dışa aktarma eklendi. Çok bölgeli/şevli kayıtlar tek platform editöründe sessizce daraltılmaz.
- Adrenapark doğrulaması: `021026ADRENAPARK (3).DWG`, 7 `EĞRI_*` geometri katmanı, **6.074 kontur, 77.515 düğüm, 155.000 üçgen, 969–1015 m**. Tüm kotlar geometrik Elevation; çözülemeyen kontur yok. `*_MKOT`, bina, tesisat ve terasman katmanları dışarıda.
- Kullanıcı tercihiyle **yalnız mevcut arazi** üretildi; hedef kot/platform/hafriyat sonucu atanmadı. Kayıt, kontur köşesi CSV'si ve okuma raporu: `ornekler/adrenapark/`. 497.838,681 m², ölçüm noktalarının dışbükey yüzey izdüşümüdür; parsel veya kazı alanı değildir.
- Doğrulama: Core runner 121/121; TerrainImport.Regression 16/16; TerrainDesign.Regression 19/19. Gerçek DXF kontur koordinatları ve kotları kayıtlı modelle birebir; JSON dönüşümü sonrası aynı TIN ve boş tasarım korunur.
- Sınırlar: kontur kırık çizgileri kısıtlı üçgenleme kenarları olarak zorlanmıyor; kontur noktalarından Delaunay yüzey üretiliyor. İçbükey sınırlar/boşluklar otomatik algılanmıyor. Şev, çok platform ve TIN-TIN hassas hacim ileri kapsamdır.

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
- `AraziKotIndeksi` BVH ile kot sorgusu ve TIN boyunca kesin parça profili sağlar; tasarım, plan ve hafriyat bu indeksi kullanır.

### Gerçekleşen kaynak (F4-F5)
- `Terrain/Models/TasarimYuzeyi.cs`: `TasarimBolgeTipi`, `TasarimBolgesi`, `TasarimYuzeyi` (+ `HedefKot`).
- `Terrain/Models/HafriyatSonucu.cs`: yarma/dolgu/net + hücre sayıları + kapsam-dışı.
- `Terrain/Services/HafriyatHesabiServisi.cs`: grid yöntemi (varsayılan 1 m), orta-nokta yaklaşımı.
- Nokta-içinde-poligon ve alan için mevcut `Common/Geometry/PoligonYardimci` kullanıldı.
- Konvansiyon: yarma = mevcut > tasarım (kazı); dolgu = mevcut < tasarım. Net = yarma − dolgu.

### Gerçekleşen kaynak (F7)
- `Terrain/Services/AraziGeometriServisi.cs`: M50 `BinaGeometriServisi` deseni; `.e4arazi.json`
  JSON (WriteIndented + JsonStringEnumConverter), `GuncelSemaSurum=2`; şev veya kenar istisnası bulunan tasarım sürüm 2 olarak kaydedilir. Mevcut sürüm 1 arazi dosyaları okunur.
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
  Çok bölgeli tasarım: plan üzerinde poligon veya iki köşeyle dikdörtgen; her bölgeye ayrı hedef kot ve kenar davranışı. Hesap ızgarası kullanıcı tarafından ayarlanabilir.
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
- **F9** (ileri): TIN-TIN hassas hacim, kütle-haul (Bruckner), metraj köprüsü, eğimli platform düzlemleri.

> **Mimari:** App katmanı yalnız Core servislerini çağırır; tüm hesap (TIN, hafriyat, rapor) Core'da
> ve testlidir. İçe aktar → TIN → tasarım → hafriyat → rapor → `.e4arazi.json` zinciri 30 testle + gerçek DXF ile doğrulandı.

## 5 Kasım 2026 — sıralı modül denetimi

Arazi kaydı atomik/yedekli ve hata durumunda geri alınabilir; gelecek şemalar reddediliyor. Hafriyat gridinde sonlu veri, hassasiyet ve 2 milyon hücre sınırı var; tarama tamsayı indeksli (A14–A16, A18).

Ayrıntılı hata listesi ve doğrulama: depo kökündeki `modul-denetimi-2026-11-05.md`.

## 5 Kasım 2026 — çok bölgeli kot ve kenar tasarımı

- Akış: **+ Bölge Çiz / + Dikdörtgen → Tamamla → hedef kot → Dik kesim / Şev → Bölgeyi Uygula ve Önizle**. Yeni bölgenin başlangıç kotu köşelerdeki mevcut kotların ortalamasıdır; kullanıcı değiştirir veya **Araziden Kot Al** ile seçer. Mevcut araziye kendiliğinden tasarım bölgesi eklenmez.
- Aynı platformun kenarları farklı davranabilir. Plandaki numaralı kenara tıklayın veya **Tek kenara özel ayar** listesini kullanın. Kenar varsayılanı devralabilir, dik kesim ya da ayrı yatay/düşey oranlı şev olabilir. Turuncu düz çizgi dik kesim; yeşil kesikli çizgi şevdir.
- Köşe sürükleme, bölge seçimi/silme, şev önceliği sıralama, 60 adımlık geri al/ileri al. Bekleyen form değişiklikleri uygulanmadan başka bölgeye geçiş veya kayıt yapılmaz. Poligon kesişmeleri ve ölçülen yüzey dışına taşma kontrol edilir; bitişik sınırlar kabul edilir.
- `AraziTesviyeServisi`: platform içi hedef kot; kenardan dışa H:1V şev; TIN parça profili üzerinde ilk arazi kesişiminde durma. Azami mesafe, ölçüm kapsamı ve şev çakışması uyarıları. Platform içleri öncelikli; şev çakışmasında listedeki üst bölge öncelikli.
- `AraziTasarimOnizlemeServisi`: platform altındaki mevcut TIN kırpılır; düz platform ve dik sınır yüzleri oluşturulur. Şev çevresi ekranda belirtilen aralıkla örneklenir (varsayılan 2 m; yoğun tasarımda otomatik büyüyebilir). Önizleme hassas hacim modeli değildir. Mevcut / Tasarım / Yarma-dolgu görünümü hem planda hem 3B görünümde kullanılabilir.
- `HafriyatHesabiServisi`: platform ve dış şev alanlarını birlikte değerlendirir; bölge bazlı yarma/dolgu, şev hücreleri ve kontrol notları raporlanır. Hacim hesabı seçilen ızgara aralığına bağlı orta nokta yaklaşımıdır.
- Tasarım kaynak konturlardan ayrı `.e4arazi.json` alanında saklanır; türetilmiş önizleme/hacim yeniden üretilir. Adrenapark mevcut arazi örneği tasarım bölgesi olmadan korunmuştur.
- Doğrulama: Core 121/121, çok bölgeli tesviye 16/16, 3B önizleme 12/12. Gerçek Adrenapark verisinde iki 40×40 m bölge (farklı kot, karma kenar) için 2 m önizleme: 311.752 yüz, yaklaşık 5,6 saniye. App derlemesi 0 uyarı / 0 hata.
- Ekran kontrolü: Adrenapark üzerinde iki dikdörtgen, bağımsız kotlar, bir dik/üç şevli kenar, 3B tasarım yüzeyi, iki bölgenin hafriyat raporu ve üç adım geri alma doğrulandı. Deneme bölgeleri kaydedilmeden geri alındı. Ekranı aşan merkezlenmiş pencere açılışta otomatik tam ekrana geçer; modüller arası taşınan pencere yerleşimi korunur.

## 6 Kasım 2026 — 3B gezinme performansı

M60, mevcut TIN'i ve tasarım yüzeylerini ortak köşelerle bellekte tutar. `Arazi3DControl` görünür yüzeyi piksel başına derinlik karşılaştırmasıyla arka planda çizer; arayüze tek görüntü aktarır. Sabit kamera son kareyi kullanır. Sürükleme sırasında ara kamera konumları kuyruğa birikmez; bırakıldığında görüntü normal çözünürlüğe döner. Kot sorguları ve hacim hesabı tam TIN'i kullanır.

`tests/TerrainRender.Regression` kapsama, örtüşme sırası, dik yüzler, yarma/dolgu renkleri, büyük koordinatlar ve ekran dışı kırpmayı kontrol eder. Adrenapark dosya yolu verildiğinde eski ve yeni çiziciyi aynı açılar ve 1140×470 pikselde karşılaştırır. Bu bilgisayarda Release ölçümü: ilk kare 1635,2 → 53,6 ms; yedi dönüş açısının ortancası 656,7 → 16,7 ms (155.000 üçgen). Şev ve dik kenarlı 336.330 üçgenlik deneme tasarımında yeni çizici 24,0 ms ortanca süre verdi. Süreler çizim ve görüntü aktarımı içindir; dosya okuma ve TIN üretimi dahil değildir.
