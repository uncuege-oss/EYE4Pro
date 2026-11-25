# M70 Codex — Park Tasarımı (Sert Zemin / Hardscape)

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir;
yalnızca güncel kararlar, kapsam, sistem davranışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

> **Durum:** F0-F6 TAMAM = **MVP bitti** (Core hattı + App park penceresi). İçe altlık → dışlama →
> yerleşim → oran → kaydet/paylaş + 2D plan/rapor UI. `.e4park.json` hazır → M71 başlayabilir.
> 121 Core testi geçer (24'ü M70); App 0 hata derlenir. Sıradaki: F7 (M71/M15 köprü), F8 (interaktif).
> Desen: Terrain M60 (paylaşılan salt-okunur artifact + Core-first + UI'siz analitik test).
> **Bağlı codex'ler:** [codex60](../../terrain/60-hafriyat-hesapla/codex60.md) (arazi kaynağı / desen),
> [codex50](../../ortak-moduller/50-bina-geometri/codex50.md) (bina kaynağı),
> [codex71](../71-bitkilendirme-peyzaj/codex71.md) (tüketici)

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Landscape |
| Modül | M70 — Park Tasarımı |
| Amaç | Araziye (kotlara) ve varsa binaya göre park sert zemin yerleşimi: yürüyüş yolları, döşeme alanları, oturma alanları, banklar, süs havuzları |
| Proje uzantısı | `.e4p70` |
| Girdiler (salt-okunur) | `.e4arazi.json` (M60 — arazi yüzeyi/kotlar), `.e4bina.json` (M50 — varsa bina ayak izi), opsiyonel DXF park sınırı |
| Paylaşılan çıktı | `.e4park.json` (sert zemin yerleşimi — M71 ve ileride M15 tüketir) |
| Standart ref. | Planlı Alanlar İmar Yönetmeliği (yeşil alan oranı, yapı yaklaşma), erişilebilirlik (TS 9111), EN 1176/1177 (oyun alanı) |
| Stack / UI dili | C# + Avalonia (net10.0) / Türkçe |
| Çekirdek | `src/Kst.Acoustics.Core/Landscape/` · UI: `src/Kst.Acoustics.App/Landscape/` |

---

## 2. Modül Kapsamı

Aktif kapsam (MVP hedefi):
- Park sınırı tanımı: `.e4arazi.json` arazi sınırından **veya** DXF poligonundan park alanı
- Arazi altlığı: M60 yüzeyini salt-okunur yükle → kot/eğim sorgusu (yerleşim öğelerine zemin kotu)
- Bina dışlama: `.e4bina.json` ayak izi + yapı yaklaşma (setback) tamponu → park yerleşiminden **çıkarılır**
- Kullanılabilir park alanı = park sınırı − bina(lar) − setback − (varsa kısıtlı bölgeler)
- Sert zemin öğeleri (yerleştirilebilir kütüphane):
  - **Yürüyüş yolu / patika** (genişlik, güzergâh — çizgi/poligon)
  - **Döşeme / meydan alanı** (poligon, kaplama tipi)
  - **Oturma alanı** (poligon)
  - **Bank** (nokta + yön; oturma alanına veya yol kenarına)
  - **Süs havuzu / su öğesi** (poligon + kenar/derinlik)
  - (öneri) çöp kutusu, pergola, aydınlatma direği yer tutucusu
- Alan/oran kontrolü: yeşil alan oranı, sert zemin oranı, kişi başına alan (imar ön kontrolü)
- Erişilebilirlik ön kontrolü: yol min. genişlik, eğim (arazi kotundan)
- `.e4p70` kaydet/aç + `.e4park.json` paylaşılan artifact üretimi

İleri/eksik alanlar:
- Otomatik/parametrik yerleşim (alana göre öneri üretimi)
- Süs havuzu hidrolik/hacim hesabı, dolaşım
- Maliyet/metraj köprüsü (`Kst.Acoustics.Metraj`) → M74
- M15 park aydınlatma köprüsü (armatür yerleşimi) → M73
- Çocuk oyun alanı EN 1176/1177 detayı (M05 köprüsü)
- 3D görselleştirme (M35/M60 render altyapısı yeniden kullanılır)

---

## 3. Mimari Kararlar

- **Arazi salt-okunur:** Park, araziyi M60'tan alır; geometriyi DEĞİŞTİRMEZ (maincodex §4.7).
  M60 yoksa: düz altlık (tek kot) + DXF/elle çizilen park sınırı ile çalışılabilir.
- **Bina salt-okunur:** Arazi içinde bina varsa `.e4bina.json` ayak izi yüklenir; park yerleşimi bina +
  setback alanını boş bırakır. Bina geometrisi M50'de düzenlenir (maincodex §4.5).
- **Bitki ayrımı:** Ağaç/çalı/bitki yerleşimi bu modülde DEĞİL, **M71 Bitkilendirme**'dedir. M70 yalnız
  sert zemin (hardscape) üretir. M71, M70'in `.e4park.json` çıktısını altlık alır.
- **Hesap/yerleşim motoru Core'da**, UI yalnız ViewModel/görünüm (maincodex §4.4).
- **Paylaşılan artifact:** `.e4park.json` M50/M60 deseniyle — `SemaSurum` migrasyonu + `GeometriRevizyon`
  artan damga. M70 üreticidir; M71/M15 salt-okunur yükler.
- **Revizyon farkı:** Arazi/bina güncellenmişse açılışta "altlık güncellendi, yerleşim doğrulansın mı?" uyarısı.

---

## 4. Veri Modeli (taslak, `Core/Landscape/Models`)

```
ParkModeli              ← kök: SemaSurum, GeometriRevizyon, Birim,
│                          AraziRevizyon?, BinaRevizyon?, ParkSiniri(Poligon),
│                          BinaDislamalari[], Ogeler[], AlanOzeti
├── ParkSiniri          ← Poligon(2D) + (arazi varsa kot sorgusu M60'tan)
├── BinaDislama         ← BinaAyakIzi(Poligon), SetbackM → etkin dışlama poligonu
├── ParkOgesi (soyut)   ← Id, Tip, Konum/Poligon, ZeminKotuM? (araziden), Kaplama/Malzeme
│   ├── YuruyusYolu     ← Guzergah(Çizgi/Poligon), GenislikM, KaplamaTipi
│   ├── DosemeAlani     ← Poligon, KaplamaTipi
│   ├── OturmaAlani     ← Poligon
│   ├── Bank            ← Nokta, YonDeg, Model
│   └── SusHavuzu       ← Poligon, KenarDetay, DerinlikM?
└── AlanOzeti           ← ToplamParkM2, SertZeminM2, YesilAlanM2, YesilOran, SertZeminOran, Uyari[]
```

`ZeminKotuM` arazi yüklendiğinde M60 `AraziTuketim.KotSorgula` ile doldurulur (nullable, geriye-uyumlu).

---

## 5. `.e4park.json` Paylaşılan Artifact

- M50/M60 deseni: JSON, `SemaSurum` migrasyonu + `GeometriRevizyon` artan damga.
- M70 üreticidir; **M71 (bitkilendirme) ve ileride M15 (aydınlatma) salt-okunur yükler.**
- Sert zemin geometrisi yalnız M70'te düzenlenir.
- Tüketici hem yolu hem gömülü kopyayı tutar; revizyon farkında "parktan güncelle?" sorulur.
- Türetilen veriler (etkin dışlama poligonu, kullanılabilir alan) serileştirilmeyip yüklemede yeniden üretilebilir.

---

## 6. Açık Riskler / Kararlar

1. **Poligon boolean + offset → KARAR: Clipper2 (2026-06-14, onaylandı).**
   Park sınırı − (bina + setback) için difference, çok binada union, **setback için offset (InflatePaths)**
   gerekir. Mevcut `PoligonYardimci` yalnız `IsPointInside/Alan/SegmentMesafesi/SegmentProje` sağlar —
   boolean ve offset YOK. Setback offset'ini elle sağlam yazmak (içbükey köşe, kendiyle kesişen tampon,
   daralan boğaz) yüksek riskli; bu yüzden **Clipper2 NuGet** eklenir (Triangle.NET kararıyla tutarlı).
   - **`ClipperAdaptor`** (`Core/Common/Geometry/`) tek sorumlu: metre↔integer **ölçek faktörü** (ör. ×1000)
     dönüşümü Clipper'a giriş/çıkışta yalnız burada yapılır (birim duyarlılığı tuzağı izole edilir —
     krş. [[m01-panel-birim-duyarliligi]] dersi). Dışarıya `Point2D` poligon alıp `Point2D` döndürür.
   - Sunduğu API: `Fark(park, dislama[])`, `Birlestir(poligon[])`, `Tampon(poligon, mesafeM)` (offset),
     delikli sonuç → poligon halkaları listesi.
   - Lisans: Boost (serbest).
2. **Arazi opsiyonel mi zorunlu mu?** KARAR: opsiyonel — M60 yoksa düz altlık + DXF/elle sınır.
3. **`.e4park.json` paylaşılan artifact** (M50/M60 deseni) — KARAR: onaylandı (2026-06-14).
4. 3D render altyapısı (M35/M60 `ProjectSurface`/`GeometryPreviewControl`) yeniden kullanımı; kod kopyası riski.
5. Otomatik/parametrik yerleşim ileri faz; MVP elle yerleştirme + alan/oran kontrolü.

---

## 7. Faz Planı (programlama)

Desen: Core-first, UI'siz analitik test (M60 gibi). Her faz `Core.Tests` ile kapanır.

| Faz | Başlık | Çıktı / Tanım | Test (UI'siz) |
|-----|--------|---------------|---------------|
| **F0** ✅ | İskelet & ürün ailesi | `Core/Landscape/Models` + `Services` klasörleri; enum/model/salt-okunur arayüz | Core 0 hata/0 uyarı; 101 mevcut test geçer ✅ |
| **F1** ✅ | Veri modeli + altlık tüketimi | `ParkAltlikServisi`: arazi (`AraziTuketim`) + bina (`.e4bina`→`BinaAyakIziAdaptoru`) salt-okunur yükle; park sınırı arazi düğümlerinin konveks kabuğundan; eskime durumu | 4 Core testi: konveks kabuk sınır, kot sorgu, bina dışlama+setback, revizyon/eskime ✅ |
| **F2** ✅ | Bina dışlama + kullanılabilir alan | **Clipper2 2.0.0 + `ClipperAdaptor`** (metre↔integer ×1000); `Tampon`(setback offset) → `Birlestir`(çok bina) → `Fark`(park − bina+setback) → delikli kullanılabilir alan + m². `ParkAlanServisi.KullanilabilirAlan` | 4 Core testi: offset 36, difference 96, union 28, uçtan uca 400−36=364 ✅ |
| **F3** ✅ | Sert zemin öğe yerleşimi | `ParkYerlesimServisi`: ekle/sil/taşı (kararlı Id), `ZeminKotunuAta` (araziden merkez kotu), `Dogrula` (kullanılabilir alan içi/dışı/bina+setback üstü) + `ParkAlanServisi.KullanilabilirIcinde` (delikli bölge nokta testi) | 4 Core testi: Id/sil, kot atama (15/14/null), içi/dışı/bina üstü doğrulama, öteleme ✅ |
| **F4** ✅ | Alan/oran + erişilebilirlik kontrolü | `ParkOranServisi.Hesapla` → `AlanOzeti` (sert/yeşil m² + oran), `ParkKurallari` eşikleri (min yol genişlik 1.5 m / max eğim %5 / min yeşil oran); yol eğimi araziden | 4 Core testi: oran 0.25/0.75, binalı 364→328, genişlik uyarısı, eğim uyarısı (dik var/düz yok) ✅ |
| **F5** ✅ | Kaydet/aç + `.e4park.json` | `ParkGeometriServisi` (Kaydet `.e4p70`/Ac/YeniOlustur/Migrate + `ArtifactYaz` `.e4park.json`); polimorfik `ParkOgesi` (`$tip` ayracı); revizyon proje kaydında artar, artifact yazımında artmaz | 4 Core testi: round-trip (5 öğe tipi + değerler), revizyon artışı, dosya-yok, artifact revizyon korur ✅ |
| **F6** ✅ | 2D görselleştirme + rapor | App: `ParkPreview` (2D plan: sınır + kullanılabilir halkalar + öğeler) + `ParkTasarimWindow` (arazi/bina yükle → kullanılabilir alan + alan/oran raporu → .e4p70 kaydet / .e4park paylaş) + `LandscapeLauncherWindow` + ana ekran kartı | App 0 hata derlenir (görsel headless doğrulanmaz); rapor mantığı F4 testli |
| **F7** | Tüketici köprüsü | M71 "Park Yükle" girişine zemin; (ileride) M15 park aydınlatma köprüsü | köprü testi |
| **F8** | İleri | Parametrik yerleşim önerisi, süs havuzu hidrolik, metraj köprüsü, oyun alanı EN 1176/1177 | — |

**Önerilen MVP kesimi:** F0–F6 (altlık → dışlama → yerleşim → kontrol → kaydet/paylaş → görsel/rapor).
F7 M71/M15 entegrasyonunu açar; F8 ileri faz.

**M71 (Bitkilendirme)** M70 F5 (yani `.e4park.json` üretimi) tamamlandıktan sonra ayrı faz dizisiyle
başlar — bkz. [codex71](../71-bitkilendirme-peyzaj/codex71.md).

### Gerçekleşen kaynak (F0)
- `src/Kst.Acoustics.Core/Landscape/Models/`:
  - `ParkEnums.cs` — `ParkOgeTipi` (YuruyusYolu/DosemeAlani/OturmaAlani/Bank/SusHavuzu), `KaplamaTipi`, `AltlikDurumu`.
  - `ParkOgesi.cs` — soyut `ParkOgesi : IParkOgesi` (Id/Tip/Ad/ZeminKotuM?) + `YuruyusYolu`/`DosemeAlani`/`OturmaAlani`/`Bank`/`SusHavuzu`.
  - `ParkModeli.cs` — kök `ParkModeli : IParkGeometri` (SemaSurum/GeometriRevizyon, altlık yolu+revizyon, `ParkSiniri`, `BinaDislama[]`, `Ogeler[]`, `SonrakiOgeId`) + `AlanOzeti`.
  - `IParkGeometri.cs` — `.e4park.json` salt-okunur tüketici arayüzü (`IAraziGeometri` deseni) + `IParkOgesi`.
- `src/Kst.Acoustics.Core/Landscape/Services/_PlaceholderF0.cs` — namespace + F1-F7 servis yol haritası notu (ilgili faz açılınca silinir).
- Not: Bitki/ağaç bilinçli olarak YOK (M71). Clipper2 paketi henüz EKLENMEDİ (F2'de eklenecek). `Point2D` = `Common/Geometry` readonly record struct yeniden kullanıldı.

### Gerçekleşen kaynak (F1)
- `Common/Geometry/PoligonYardimci.cs`: `KonveksKabuk` (Andrew monotone chain, CCW; <3 nokta → girdiyi döndürür) eklendi — park sınırını arazi TIN düğümlerinden türetir.
- `Landscape/Services/ParkAltlikServisi.cs`:
  - `AraziYukle` → `AraziTuketimServisi.Yukle` (M60 tüketici); modele yol+revizyon işler, canlı `AraziTuketim` döndürür (kot sorgusu için).
  - `BinaYukle` → `BinaGeometriServisi.Ac` + mevcut `BinaAyakIziAdaptoru.Donustur` (kod kopyası yok); `BinaDislama` (ayak izi + setback) üretip ekler.
  - `ParkSiniriAraziden` (konveks kabuk) + `ParkSiniriAyarla` (DXF/elle poligon).
  - `AraziDurumu`/`BinaDurumu` → `AltlikDurumu` (Yok/Bağlı/Eskimiş) eskime kontrolü.
- Altlık geometrileri DEĞİŞTİRİLMEZ; modelde yalnız yol + revizyon saklanır.
- Bağımlılık notu: Landscape→`Terrain.Services.BinaAyakIziAdaptoru` çağrısı (bina→ayak izi mantığı paylaşıldı). Adaptör ileride `Bina` namespace'ine taşınabilir (refactor adayı).
- Testler: `Program.cs` `M70*` (4 test): konveks kabuk sınır (alan 100), kot sorgu (z=10+y → 15), bina dışlama (alan 12 + setback 3 + ad), revizyon/eskime durumu.

### Gerçekleşen kaynak (F2)
- **Bağımlılık:** `Clipper2` 2.0.0 (Clipper2Lib) `Core.csproj`'a eklendi.
- `Common/Geometry/ClipperAdaptor.cs`: metre↔integer ölçek (`Olcek=1000`, mm hassasiyeti) TEK noktada izole.
  - `Tampon(poligon, mesafeM)` → `InflatePaths` (Miter köşe, EndType.Polygon); offset öncesi CCW yönelimi (`YoneAyarli`) garanti edilir → dışa şişme.
  - `Birlestir(poligonlar)` → `Clipper.Union` (NonZero); `Fark(ana, cikarilanlar)` → `Clipper.Difference`.
  - `NetAlan` = işaretli alanlar toplamının |·| (dış +, delik − yönelimli → dolu alan); `IsaretliAlan` (Shoelace, abs'siz) public.
- `Landscape/Services/ParkAlanServisi.cs`: `KullanilabilirAlanSonucu` (Halkalar/ToplamPark/Dışlanan/Kullanılabilir) + `KullanilabilirAlan(park)`: her bina ayak izi⊕setback → `Birlestir` → park'tan `Fark`. Park dışına taşan setback `Fark` ile otomatik kırpılır → DışlananM2 yalnız park içini sayar.
- Testler: `M70*` F2 (4 test): offset 4×4+1→36, difference 10×10−2×2→96, union iki örtüşen 4×4→28, uçtan uca 20×20 park − (4×4+setback1) → 400−36=364.

### Gerçekleşen kaynak (F3)
- `Landscape/Services/ParkYerlesimServisi.cs`:
  - `OgeEkle` (kararlı Id = `SonrakiOgeId++`), `OgeSil`, `OgeTasi` (tip bazlı geometri öteleme).
  - `ZeminKotunuAta`/`ZeminKotlariniAta` → öğe merkezinden `AraziTuketim.KotSorgula` (arazi yoksa/yüzey dışıysa null).
  - `Dogrula` → `YerlesimDogrulama` (Uyarilar); kontrol noktaları kullanılabilir alanda mı + arazi merkez kotu çözülüyor mu.
- `ParkAlanServisi.KullanilabilirIcinde(nokta, halkalar)`: dış halka (işaretli alan ≥0) içinde VE delik (CW) içinde değil → bina+setback üstü ve park dışı tek testle yakalanır.
- Bank/poligon/yol tipleri `switch` ile ele alınır; `Point2D` readonly struct → yeniden atama ile öteleme.
- Testler: `M70*` F3 (4 test): Id/sil, kot atama (bank 15 / döşeme 14 / arazisiz null), doğrulama (park içi geçerli, bina üstü+park dışı geçersiz), öteleme (alan korunur).

### Gerçekleşen kaynak (F4)
- `Landscape/Services/ParkOranServisi.cs`: `ParkKurallari` (MinYolGenisligiM=1.5 / MaxYolEgimi=0.05 / MinYesilOran=0) + `Hesapla(park, arazi?, kurallar?)` → `AlanOzeti`.
  - Sert zemin = döşeme/oturma/süs havuzu poligon alanı + yol (güzergâh uzunluğu × genişlik); **bank mobilya, alan saymaz**.
  - Yeşil = max(0, kullanılabilir − sert); oranlar toplam parka göre.
  - Uyarılar: yeşil oran eşiği (kapalı varsayılan), yol genişlik < min, yol boyuna eğim (araziden segment kot farkı / yatay) > max.
- Not: öğe örtüşmeleri tekilleştirilmez (basit toplam); doğru tekilleştirme F8 (Clipper union) adayı.
- Testler: `M70*` F4 (4 test): oran 0.25/0.75, binalı 364→328, dar yol genişlik uyarısı, dik yol eğim uyarısı (düz yolda uyarı yok).

### Gerçekleşen kaynak (F5)
- `Landscape/Models/ParkOgesi.cs`: `[JsonPolymorphic(TypeDiscriminatorPropertyName="$tip")]` + 5 `[JsonDerivedType]` (yol/doseme/oturma/bank/havuz); `Tip` `[JsonIgnore]` (ayraçla zaten yazılır).
- `Landscape/Services/ParkGeometriServisi.cs` (M50/M60 deseni):
  - `Kaydet(.e4p70)` → `GeometriRevizyon++`; `Ac` → deserialize + `Migrate(0→1)`; `YeniOlustur` (rev=0).
  - `ArtifactYaz(.e4park.json)` → dışa aktarma, revizyonu ARTIRMAZ (tüketici M71/M15 bunu okur).
  - `JsonStringEnumConverter` + WriteIndented; `GuncelSemaSurum=1`.
- Testler: `M70*` F5 (4 test): round-trip (5 öğe tipi + bank/yol/havuz değerleri + setback + SonrakiOgeId), revizyon 0→1→2, dosya-yok FileNotFoundException, artifact revizyon korur.

### Gerçekleşen kaynak (F6, App)
- `App/Landscape/Views/ParkPreview.cs`: 2D plan kontrolü (fit-to-view, Y yukarı; `CevreAydinlatmaPreview` deseni). Kullanılabilir halkalar (dış=yeşil, delik=bina+setback mavi; `ClipperAdaptor.IsaretliAlan` ile ayrım) + park sınırı çerçevesi + öğeler (yol kalın çizgi, döşeme/oturma/havuz dolgu poligon, bank nokta).
- `App/Landscape/Views/ParkTasarimWindow.axaml(.cs)`: Arazi Yükle (.e4arazi → konveks kabuk sınır + öğelere kot) · Bina Yükle (.e4bina + setback) · Hesapla/Yenile (`ParkAlanServisi`+`ParkOranServisi` → 2D + Türkçe rapor) · Park Aç/Kaydet (.e4p70) · Paylaş (.e4park.json).
- `App/Landscape/Views/LandscapeLauncherWindow.axaml(.cs)`: modül merkezi (M70 kartı + M71 "Yakında").
- `App/Views/EngineeringHomeWindow`: Landscape disiplin kartı + `OpenLandscape_OnClick`.
- App katmanı yalnız Core servislerini çağırır (maincodex §4.4). MVP: interaktif öğe çizimi yok (F8); yüklenen .e4p70 öğeleri çizilir. Görsel çıktı headless ortamda doğrulanmadı.

---

## 8. Yeni Oturum Notu

1. `maincodex.md` → `landscape/_index.md` → bu dosya okunur.
2. Kaynak: `src/Kst.Acoustics.Core/Landscape/` (henüz yok — F0'da oluşturulur).
3. Bina salt-okunurluğu §4.5, arazi salt-okunurluğu §4.7 maincodex'te bağlayıcıdır.

## 5 Kasım 2026 — sıralı modül denetimi

Kayıt/göç güvenliği, saat yönündeki dış halka, delik üstünü örten döşeme ve yol genişliği taşması düzeltildi. Yerleşimde tam ayak izi kontrolü kullanılıyor; alan özeti union kapsamı değişmedi (A14–A16, A20–A22).

Ayrıntılı hata listesi ve doğrulama: depo kökündeki `modul-denetimi-2026-11-05.md`.
