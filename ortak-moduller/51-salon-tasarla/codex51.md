# CODEX 51 — Salon Tasarla (Ortak Modül)

> **Durum:** Faz 0-24 tamamlandı. MVP/P0/P1/P2 üzerine güvenli belge yaşam döngüsü, şema v2,
> model bütünlüğü, gerçek koltuk-koridor yerleşimi, görüş hattı/C-değeri, canlı kesit, akustik ön
> kontrol, yansıtıcı bulutlar, kalite haritası, DXF/PDF/görsel altlık, kalıcı ölçü-kısıt, CAD+,
> hızlı tasarım, komut paleti, otomatik kayıt/sürüm geçmişi ve kalıcı editör ayarları eklendi.
> Core + App + Tests **0 hata / 0 uyarı** derlenir; tam çalıştırıcıda **791/791 test geçer**.
> **Bağlı codex'ler:** [codex.md](../../akustik/01-proje-geometri/codex.md) (M01 — tüketici), [codex50](../50-bina-geometri/codex50.md) (desen kaynağı)
> **Çekirdek:** `src/Kst.Acoustics.Core/Salon/` · **UI:** `src/Kst.Acoustics.App/Salon/`

---

## 1. Amaç

Bir salon/oditoryum akustiğini hesaplamak için M01 (Proje Yönetimi & Geometri Import) bir
geometriye ihtiyaç duyar; bugün bu geometri yalnızca dışarıdan **DXF import** ile gelir.
Kullanıcının elinde hazır DXF yoksa salonu sıfırdan çizebileceği araç yoktur. **M51 "Salon
Tasarla"**, SketchUp benzeri basit bir editörle salonun **zeminini, sahnesini, duvarlarını,
tavanını** ve **kademeli seyirci alanını** tasarlayıp **M01'e referans/girdi** üretir.

- Her ana başlık girişinde (akustik launcher) **"🎭 Salon Tasarla"** ile M51 penceresi açılır.
- Kullanıcı salonu M51'de hazırlar ve **`.e4salon.json`** dosyası olarak kaydeder.
- M01'de **"Salon Yükle"** ile bu dosya (veya M51'in ürettiği DXF) tüketilir.
- Çıktı M50/M60/M70 deseniyle **paylaşılan salt-okunur artifact**'tır; ileride elektrik (sahne
  aydınlatma) ve mekanik (salon HVAC) gibi disiplinler de aynı altlığı tüketebilir.

## 2. Kapsam

### M51'de yapılan işlemler
| İşlem | Not |
|---|---|
| Serbest 2D eskiz: **line / polyline / circle / rectangle** | metre tabanlı |
| Düzenleme: **trim** (segment kesişim/bölme), **del** (öğe silme), vertex/edge sürükleme | |
| Akış: zemin çiz → sahne (2D) → çizgilerden duvar → tavanla kapat | kullanıcı sırası |
| **Kademeli (raked) seyirci alanı**: N parça + her parçaya eğim | Terrain `TasarimYuzeyi` deseni |
| Opsiyonel **balkon / loca** (platform + yükseklik + konsol) | |
| **Hazır şablonlar**: salon tipi (dikdörtgen/at-nalı…) + sahne tipi (dairesel/oval/dikdörtgen) + konum (salon içi/önü) + ölçü → otomatik geometri | sadece ölçü girilerek ilerler |
| **Katman atama** (duvar/yüzey → katman); şablonlar katmanı otomatik atar | `Ad` = M01 kategorisi |
| Çıktı: **`.e4salon.json`** (native, M01 köprüsü) + opsiyonel **DXF** | her ikisi birinci sınıf |
| **Gerçek koltuk yerleşimi** | koltuk/sıra aksı, yan-orta koridorlar, erişilebilir cepler |
| **Görüş ve akustik ön kontrol** | C-değeri ısı haritası, hacim/koltuk, 6 bant kaba RT60 |
| **Canlı boyuna/enine kesit + yansıtıcı bulutlar** | sahne/seyirci/balkon/tavan/reflektör birlikte |
| **Kalıcı ölçü, geometrik kısıt ve CAD+** | fillet/pah, dizi, hizala-dağıt, grup, pano |
| **Referans altlık** | birim-duyarlı DXF önbelleği; PDF/görsel yol+dönüşüm+kalibrasyon |
| **Güvenli çalışma** | otomatik kurtarma, sürüm geçmişi, komut paleti, ayar kalıcılığı |

### M51'de YAPILMAYAN işlemler (M01'de kalır)
- Malzeme ataması (M01 kategori→malzeme), kaynak/alıcı yerleştirme, ışın izleme, RT60/STI hesabı.
- Akustik sınıflandırma, ayrıntılı absorber/difüzör panel hesabı ve nihai rapor.

## 3. Mimari Kararlar

### 3.1 Veri modeli (`Core/Salon/Models`)
```
SalonGeometri          ← kök: SemaSurum, GeometriRevizyon, SalonAdi, SalonYuksekligiM,
│                         2B/parametrik geometri + ölçü/kısıt/altlık/yansıtıcı koleksiyonları
├── SalonKatman        ← Id, Ad (M01 kategorisi), Tip, RenkHex, Gorunur, Kilitli (INPC)
├── SalonCizimOgesi    ← Id, Tip (Cizgi/Polyline/Dikdortgen/Daire), Noktalar[], YariCapM,
│                         Kapali, KatmanId, GrupId, BlokAdi
├── SahneGeometri, SeyirciSekciyonu, BalkonGeometri, OrkestraCukuru, TavanProfili
└── SalonOlculendirme, SalonGeometrikKisit, SalonReferansAltlik, SalonYansiticiPanel
```
Salt-okunur arayüzler: `ISalonGeometri`, `ISalonKatman`, `ISalonCizimOgesi` (tüketici koruması).

### 3.2 `.e4salon` dosya formatı
- JSON, uzantı `.e4salon.json`. `SemaSurum` migrasyon deseni baştan dahil (M50/M12'de kanıtlı).
- `GeometriRevizyon`: her kayıtta artan damga; M01 yüklediği revizyonu saklar, fark varsa uyarır.
- Servis: `SalonGeometriServisi` (`Kaydet`/`Ac`/`YeniOlustur`/`Migrate`, `GuncelSemaSurum=2`).
  `YeniOlustur` 12 anlamsal katman tohumlar: Zemin, Sahne, Duvar-Sahne, Duvar-Seyirci,
  Tavan, Seyirci, Korniş, Balkon, Orkestra, Kapı, Pencere, Yansıtıcı. v1→v2 migrasyonu yeni
  koleksiyonları ve anlamsal katmanları kayıp olmadan tamamlar; gelecek şema reddedilir.

### 3.3 Eğriler fasetlenir
M01 düz çokgen yüzeyler (3DFACE / kapalı polyline) tüketir. Daire/oval 2D'de merkez+yarıçap
saklanır; **3D üretim/DXF ihracatı anında** N kenarlı poligona fasetlenir
(`SalonCizimOgesi.DaireyiFasetle`). Maincodex §4.1 "kavisli öğelerin çokgen yaklaşımı" ile uyumlu.

### 3.4 Kategori = akustik malzeme anahtarı
Üretilen her `ProjectSurface.Category`, katmanın `Ad` alanından gelir ("Zemin", "Duvar-Sahne",
"Duvar-Seyirci", "Tavan", "Sahne", "Seyirci", "Korniş", "Balkon"). M01 bunları `GroupBy(Category)`
ile gruplar ve `CategoryMaterials` üzerinden malzeme atar.

### 3.5 Dedicated canvas (yeniden kullanım)
M51 kendi `SalonCizimCanvas`'ını yazar (M50 `BinaGeometriCanvas`'tan uyarlanır) — M50 düzenleme
iç yapısına bağlanmaz, ama kanıtlanmış pan/zoom/snap/hit-test/StreamGeometry render kodunu ödünç
alır. 2D→3D üretimi M50 `Bina3DGorunumServisi`, DXF ihracatı `DxfExportService` desenini izler.

### 3.7 Tavan profili, kapasite ve tam arayüz sözleşmesi (P2, 2026-07-02)
- **Tavan kesit profili:** `SalonGeometri.TavanProfili` — derinlik (Y) → kot kontrol noktaları
  (`TavanKirilimNoktasi`; Terrain `TasarimYuzeyi` deseninin tavan karşılığı). Boş/1 nokta = düz
  tavan (`SalonYuksekligiM`); ≥2 nokta = **eğik/parçalı tavan**: kabuk, kırılım Y'lerinde bantlara
  bölünür, her bantta kot y'ye doğrusal bağlı olduğundan panel **düzlemseldir** (ışın izleme/DXF
  geçerli). Uçların dışında uç kotu (clamp). `SalonTavanServisi.Kot(salon, y)` tek doğruluk kaynağı.
- **Duvar üstleri profili izler:** kabuk çevre duvarları ve açık-çizgi duvarların üst kenarı
  `Kot(y)`'yi izler (kenar boyunca kırılım Y'leri ara köşe olur; duvar düşey düzlemde → düzlemsel).
- **Hacim entegrasyonu:** profilli tavanda V = ∫z(y)dA — bant bazlı `a·A + b·∫y dA`
  (`SalonPoligonServisi.IsaretliYMomenti`, delikli halkalarda işaretler doğal düşer).
- **Koltuk kapasitesi:** `SalonSeyirciServisi.KoltukKapasitesi` — brüt 0,5 m²/koltuk varsayılanı;
  seyirci ekle/güncelle durum mesajında ve ✔ Kontrol raporunda (m³/koltuk ile birlikte) gösterilir.
- **Tam arayüz sözleşmesi:** `ISalonGeometri` artık `ISahneGeometri`, `ISeyirciSekciyonu`,
  `IBalkonGeometri`, `ITavanKirilimNoktasi` görünümlerini de sunar — tüketici modüller (M01,
  ileride elektrik/mekanik) somut sınıfa inmeden tüm geometriyi salt-okunur okur.
- **Güncelle akışı:** seyirci/balkon listeden seçilince değerleri panele dolar; "Seçiliyi Güncelle"
  ile düzenlenir (sil+yeniden ekle bitti). Sahne ve seyirciye **taban kotu** alanı eklendi —
  "balkon üstü seyirci" senaryosu artık UI'dan kurulabilir.
- UI: sol panelde "TAVAN PROFİLİ (kesit)" bölümü (nokta ekle/sil/düz tavana dön); canvas'ta
  kırılım Y'lerinde kesikli mor çizgi + kot etiketi.

### 3.10 Profesyonel çizim paketi (Faz 14-17, 2026-07-03)
Editör, AutoCAD/SketchUp çekirdek araç setine çıkarıldı. Faz 14-17'de kapsam dışı bırakılan
kalıcı ölçü, blok/grup, fillet, array ve katman kilidi Faz 22'de tamamlandı; metin, hatch ve
spline hâlâ bilinçli kapsam dışıdır:
- **Çoklu seçim:** Shift+tık ekle/çıkar; boş alanda sürükleme = pencere seçimi (soldan sağa mavi
  = tamamen içindekiler, sağdan sola yeşil kesikli = değen her şey — AutoCAD window/crossing).
  Sil/katman atama/dönüşümler tüm seçime uygulanır; vertex tutamakları birincil öğede.
- **Dönüşümler** (`SalonDonusumServisi`, Core + testli): **Taşı** (baz+hedef; Ctrl=kopya),
  **Aynala** (2 nokta eksen; Ctrl=kopya — simetrik salon akışı), **Döndür** (merkez+referans+hedef
  veya derece yazıp Enter; Ctrl=kopya). Yay açıları dönüşümlerde düzeltilir (aynalamada CCW için
  uç takası). Canlı hayalet önizleme.
- **Yay** (`SalonCizimTipi.Yay`): merkez+yarıçap+CCW açı çifti; çizim başlangıç→bitiş→kavis
  (3 nokta çevrel çember, `UcNoktadanYay`; doğrusalsa çizgiye düşer). `Segmentler` fasetlediği
  için 3D duvar/DXF/trim-kesici/snap kendiliğinden çalışır; trim host'u olamaz (daire gibi).
- **Offset:** kapalı öğede `ClipperAdaptor.Tampon` (içe/dışa taraf = tık), daire/yayda r±d,
  açık çizgide `SalonPoligonServisi.AcikOffset` (miter, 4× sınırlı); mesafe klavyeden yazılabilir.
- **Uzat** (`SalonTrimServisi.Uzat`): çizgi/açık polyline ucu ilk ileri kesişime uzar (trim'in tersi).
- **Hassasiyet:** dinamik girişte `uzunluk<açı` (ör. `5<30`); **F10 polar** 15° yumuşak kilit
  (orto öncelikli); **dik** ve **merkez** snap'leri (öncelik: uç>kesişim>merkez>dik>orta>kenar);
  **F3** nesne snap aç/kapa; **F9** 0,5 m grid kilidi (nesne snap > grid > serbest).
- **Bilgi:** Ölçüm aracı (2 nokta mesafe/ΔX/ΔY/açı; kapalı öğeye tık = alan+çevre); seçimde
  tip/uzunluk/alan özeti; imleç etiketi uzunluk+açı gösterir.
- **Verimlilik:** tek harf kısayolları V/L/P/R/C/A/T/E/M/O/N/D/X (tooltip'lerde); araç şeridi
  çizim | düzenleme | modlar gruplarına ayrıldı. Vertex ekleme = seçili öğenin kenarına çift tık
  (dikdörtgen kapalı polyline'a dönüşür); vertex silme = tutamağa tıkla + Delete.

### 3.11 Güvenilirlik ve gelişmiş tasarım paketi (Faz 18-24, 2026-08-10)
- **Belge güvenliği:** Yeni/Aç/Şablon/Hızlı Tasarım/Sürüm Geri Yükle akışları aynı kaydet–at–iptal
  kapısından geçer. Uygulanmamış panel taslakları ayrıca izlenir; seçim değişimi taslağı sessizce
  ezemez. Undo/redo tam belgeyi, dosya yolu ve kayıt revizyonunu doğru korur. Kaydet/Aç/Kopyadan
  Yükle giriş-çıkışlarında `SalonModelButunlukServisi` bozuk/sonsuz/çift kimlikli modeli engeller.
- **3B/hacim doğruluğu:** sıfır hacimde yanlış bbox uydurma, kısmi seyirci düşümü, düşey balkon
  kesişimi, zemin/tavan normal yönü, coplanar bölge örtüşmesi, açık kabuk kabulü, sahne kabuk
  kırpması, köşe/çoklu-duvar açıklık bağları ve 32'den fazla açıklık keyhole limiti düzeltildi.
  Dairesel sahnede etkin derinlik kullanılır. Yüzey kategorileri anlamsal katman adından türetilir.
- **M01 güvenli yenileme:** geometri güncellenirken malzemeler, kaynak/alıcılar, oda tipi,
  simülasyon ayarları ve tarihsel senaryolar derin kopyayla korunur; etkin senaryo sıfırlanır,
  sonuç eskimiş işaretlenir, panel yerleşimleri temizlenir. Kaynak yol/revizyon kaybolmaz.
- **Gerçek oturma ve görüş:** `SalonKoltukYerlesimServisi` koltuk/sıra aksını, yan/orta
  koridorları ve 0,90 m erişilebilir cepleri fiziksel olarak yerleştirir. `SalonGorusHattiServisi`
  her koltuk için sahne görüş ışını, C-değeri ve 0..1 ısı puanı üretir; UI'da koltuk ayarları ve
  görüş haritası vardır.
- **Kesit/akustik/yansıtıcı:** `SalonKesitServisi` canlı boyuna/enine kesitte kabuk, sahne,
  seyirci, balkon, çukur ve reflektörü birlikte verir. `SalonAkustikOnKontrolServisi` gerçek hacim,
  gerçek koltuk, yüzey oranları ve 125–4000 Hz kaba Sabine RT60 hedefini raporlar. Yansıtıcı panel
  dizisi hedefe yönlendirilir, tavan altına indirilir ve M01/3B/DXF kategorisine katılır.
- **CAD+/parametrik çizim:** kalıcı bağlı ölçüler canvas'ta çizilir; yatay/dikey/uzunluk ve öğeler
  arası kısıtlar geçici kopyada atomik çözülür. CAD+ penceresi fillet/pah, doğrusal-dairesel dizi,
  hizala/eşit dağıt, grup/çöz, ofsetli pano yapıştırma, ölçü ve kısıt işlemlerini tek undo adımında
  uygular. Katman kilidi ve grup/blok metadata'sı dosyada kalıcıdır.
- **Altlık/kalite/verimlilik:** DXF `$INSUNITS` metre ölçeğine çevrilip segment önbelleğiyle
  çizilir; PDF/görsel yol, saydamlık, ölçek/dönüşüm/ofset ve iki noktalı kalibrasyonla saklanır.
  Kalite haritası sızıntı, ters normal, çift/örtüşen yüzey, bozuk öğe ve açıklık bağını odak
  noktasıyla gösterir. Hızlı Tasarım hedef koltuktan salon+koltuk+görüş+RT+reflektör üretir.
  Ctrl+K komut paleti, kullanıcı ayarları ve ana belge metadata'sını değiştirmeyen otomatik
  kurtarma/adlandırılmış sürüm geçmişi kolay ve hızlı kullanımı tamamlar.
- **DXF birimleri:** M51, M01 ve şablon ihracatları HEADER/`$INSUNITS` yazar; metre/mm fiziksel
  boyutları round-trip korunur.

### 3.9 Duvar açıklıkları — kapı/pencere (2026-07-02)
- **Konum-tabanlı bağ:** `DuvarAcikligi` (tip Kapı/Pencere, genişlik×yükseklik + alt kot)
  duvara Id ile değil **plan konumuyla** bağlanır: merkez noktası hangi duvar segmentinin
  üzerindeyse (`AciklikToleransM` = 0,15 m) 3D'de o duvardan kesilir — trim/yeniden çizim
  sonrasında bağ kopmaz (M12 yerleşim deseni). Kabuk çevre duvarları ve açık-çizgi duvarların
  ikisi de destekler.
- **Delik + dolgu:** duvar (s,z) düzleminde poligonlaştırılır (s = kenar boyunca mesafe;
  tavan profili üst kenarı korunur), Clipper fark + keyhole ile delik açılır; delik açıklığın
  kategorisiyle ("Kapı"/"Pencere" — katman adından türetilir) **yüzey olarak doldurulur**.
  Kabuk sızdırmaz kalır (LeakTest geçer), toplam alan korunur, M01'de kapı/pencereye ayrı
  malzeme atanır. Varsayılan katman seti 11'e çıktı (Kapı + Pencere).
- **UI:** araç çubuğunda **🚪 Açıklık** — paneldeki ölçülerle duvar çizgisine tıklayarak
  yerleştirme; AÇIKLIKLAR panelinde liste + Seçiliyi Güncelle/Sil; canvas'ta duvar boyunca
  kalın renkli işaret (K/P + genişlik etiketi). Doğrulama: duvara bağlanmayan açıklık uyarısı.
- Hacim etkilenmez (açıklık dolgusu aynı düzlemde).

### 3.8 Kabuğa kırpma, orkestra çukuru ve faset ayarı (P2 devam, 2026-07-02)
- **Seyirci/balkon kabuğa kırpılır:** basamak/döşeme/soffit panelleri Clipper2 kesişimiyle,
  düşey yüzeyler (korniş/saçak/parapet) `YatayParcalar`/`DikeyParcalar` ince-şerit tekniğiyle
  dış hat içinde kalan aralıklara bölünür — dışarı taşan yerleşim kabuk-dışı hayalet yüzey
  üretmez (Dogrula uyarısı yine verilir). Sahne eteği kırpılmaz (eksenel olmayan kenarlar;
  şablon sahneyi zaten içeride tutar).
- **Orkestra çukuru:** `OrkestraCukuru` (Gen×Der plan + çukur derinliği; salon başına tek) —
  zeminde açıklık açar, çukur tabanı (−d) + 4 çevre duvarı üretir (kategori "Orkestra",
  varsayılan katman setine eklendi → 9 katman). Hacme +alan×d eklenir. Doğrulama: dış hat
  taşması + sahne footprint çakışması. UI: SAHNE panelinin altında; canvas'ta kesikli
  turkuaz footprint.
- **Faset ayarı:** `SalonGeometri.FasetKenarSayisi` (varsayılan 48; servisler 12–128'e
  kelepçeler, `Salon3DGorunumServisi.Faset`). SALON panelinde alan; 3D/DXF/hacim/doğrulama
  aynı değeri kullanır.
- **Sahne kulesi ayrı özellik değildir:** "salon önü" sahne cebi + tavan profiline y<0
  bölgesinde yüksek kot noktaları eklenerek fly-tower hacmi modellenir (profil clamp
  davranışı cep üzerinde de geçerlidir).

### 3.6 Kabuk kuralı, kırpma ve hacim (P0 güvenilirlik paketi, 2026-07-02)
- **Kabuk yalnız Zemin'den:** kapalı öğe yalnız **Zemin tipli katmandaysa** (veya katmanı
  çözülemiyorsa) kabuk (zemin+tavan+çevre duvar) üretir; birden çok kabuk poligonu **union**'lanır
  (çift duvar/tavan yok). Tavan tipli kapalı öğe z=h'de, diğer katmanlardaki kapalı öğe z=0'da
  tek **bölge** yüzeyidir (kategori = katman adı) — eski "her kapalı öğe kabuk üretir" kuralı
  çift tavan + salon içi tam yükseklik hayalet duvar üretiyordu.
- **Çakışık yüzey kırpma (Clipper2):** zemine oturan (taban kotu ≈ 0) sahne/seyirci footprint'leri
  ve z=0 bölge öğeleri kabuk zemininden **fark** alınarak çıkarılır; Tavan bölgeleri kabuk
  tavanından çıkarılır. Delikli sonuçlar `SalonPoligonServisi.BasitPoligonlaraAyir` ile **keyhole**
  tekniğinde basit poligonlara açılır (şerit ±2 mm — ClipperAdaptor 1 mm ızgarasında daha darı
  sıfıra yuvarlanır, dikkat). Böylece Sabine alanı çift sayılmaz, ışın izlemede çakışık yüzey kalmaz.
- **Gerçek hacim:** `SalonHacimServisi` — V = kabuk plan alanı × yükseklik − sahne platform −
  seyirci teras katısı − balkon plağı (kabuk içi oranla). Bbox değil. `SalonImporter` bunu
  `RoomVolume` olarak taşır; kapalı zemin yoksa bbox'a geri düşer.
- **Kategoriler katmandan:** kabuk duvarı `DuvarSeyirci` tipli katmanın adından, tavan `Tavan`
  tipli katmanın adından türetilir (yeniden adlandırma M01 kategorisine akar). Varsayılan katman
  seti 8'e çıktı (Korniş + Balkon eklendi) — 3D çıktı kategorileri katman listesiyle tutarlı.
- **Şablon yerleşim garantileri:** seyirci alanı plan hattına sığdırılır (`YariGenislik` —
  yelpaze ön daralması / at nalı arka yayı, 1 m koridor payı); "salon önü" sahnede dış hatta
  **sahne cebi** (proscenium) açılır — sahne artık kapalı hacmin dışında duvar arkasında kalmaz.
- **Doğrulama:** `SalonDogrulamaServisi.Dogrula` (kabuk var mı, sahne/seyirci/balkon taşması,
  gizli katman öğesi) + `KabukKapalilikRaporu` (LeakTestService ışın sızıntı testi). M51 toolbar
  **"✔ Kontrol"** butonu ikisini de çalıştırır; M01 "Salon Yükle" uyarı sayısını durumda gösterir.
- **Revizyon uyarısı:** `AcousticsProject.KaynakGeometriRevizyon` eklendi; M01 proje açarken
  kaynak `.e4salon.json`'ın güncel revizyonuyla karşılaştırır, fark varsa durum çubuğunda
  "salon geometrisi güncellenmiş" uyarısı verir (§3.2'de baştan beri planlanan davranış).

## 4. İş Programı (İnce Fazlı)

| Faz | İş | Bitti Sayılır | Durum |
|---|----|---------------|-------|
| 0 | Başlık aç + domain (`SalonGeometri`/`SalonKatman`/`SalonCizimOgesi` + arayüzler) + `SalonGeometriServisi` (.e4salon.json) + pencere iskeleti + launcher girişi + round-trip testleri | Core+App derlenir; 4 round-trip/migrasyon/faset testi geçer | ✅ Tamamlandı |
| 1 | **MVP**: `SalonCizimCanvas` (pan/zoom/snap/hit-test) + araç modları line/polyline/circle/rectangle + **trim** (`SalonTrimServisi`) + **del** + katman seçimi | Serbest çizim uçtan uca; .e4salon kaydet/aç | ✅ Tamamlandı |
| 2 | 2D→3D: `Salon3DGorunumServisi` zemin/duvar/tavan `ProjectSurface`'leri + 3D önizleme (`Salon3DOnizlemeWindow`, GeometryPreviewControl) | Kapalı öğe→zemin+tavan+çevre duvar; açık çizgi→duvar; izometrik önizleme | ✅ Tamamlandı |
| 3 | `SahneGeometri`: tip (dairesel/oval/dikdörtgen, fasetli) + konum (içi/önü) + ölçü + yükseklik; sol panel + canvas footprint + 3D platform (üst yüzey + çevre etek) | Parametrik sahne girilir, 2D'de görünür, 3D'ye yükseltilmiş platform olarak katılır | ✅ Tamamlandı |
| 4 | Kademeli seyirci: `SeyirciSekciyonu` + `SalonSeyirciServisi` (N parça + kademe yük. → basamak döşeme "Seyirci" + düşey korniş "Korniş"); sol panel çoklu alan + canvas footprint/kademe çizgileri | Eğimli (raked) seyirci yüzeyleri üretilir, 2D'de görünür, 3D'ye katılır | ✅ Tamamlandı |
| 5 | `BalkonGeometri` + `SalonBalkonServisi` (yükseltilmiş platform: döşeme + alt soffit + çevre saçak + parapet); Balkon/Loca tipi; sol panel çoklu + canvas footprint | Balkon/loca eklenir, 2D'de görünür, 3D'ye katılır | ✅ Tamamlandı |
| 6 | Hazır şablonlar: `SalonSablonu` + `SalonParametrikServisi` (salon tipi Dikdörtgen/AtNalı/Yelpaze + sahne tipi/konum + ölçü → dış hat (Zemin) + sahne + seyirci); `SalonSablonWindow` sihirbazı + toolbar "📐 Şablon" | Ölçü girerek tam salon üretilir; katmanlar otomatik atanır | ✅ Tamamlandı |
| 7 | Katman yönetimi: katman ekle/ad/renk/tür düzenle/sil + "seçili öğeyi katmana ata"; açık çizgi duvar kategorisi = katman **adı** (M01'e doğrudan akar) | Katmanlar M01 kategorisi olarak çıkar; düzenlenebilir | ✅ Tamamlandı |
| 8 | Çıktı/entegrasyon: `SalonImporter` + M01 "🎭 Salon Yükle" (native, `SurfaceImportFormat.Salon`, RoomVolume + kategoriler) + `SalonDxfExportServisi` (M51 "DXF Dışa Aktar") | M01'de .e4salon native yüklenir; DXF dışa aktarılır (M01 DXF yolundan da okunur) | ✅ Tamamlandı |
| 9 | **P0 güvenilirlik paketi** (§3.6): gerçek hacim (`SalonHacimServisi`) + kabuk kuralı katman tipine bağlandı + Clipper2 zemin/tavan kırpma (keyhole) + kategori→katman türetme + 8 varsayılan katman + şablon sığdırma/sahne cebi + `SalonDogrulamaServisi` + "✔ Kontrol" + M01 revizyon uyarısı (`KaynakGeometriRevizyon`) | Sabine V gerçek plan geometrisinden; çakışık/hayalet yüzey yok; şablon seyircisi dış hatta sığar; 8 yeni test | ✅ Tamamlandı |
| 14-17 | **Profesyonel çizim paketi** (§3.10): çoklu seçim + pencere/crossing seçimi + Taşı/Kopyala + Aynala + Döndür (`SalonDonusumServisi`) + vertex ekle/sil + **Yay** (`SalonCizimTipi.Yay`, 3-nokta) + **Offset** (Tampon / r±d / `AcikOffset`) + **Uzat** (`SalonTrimServisi.Uzat`) + `uzunluk<açı` girişi + polar F10 + dik/merkez snap + F3/F9 + Ölçüm aracı + seçim bilgisi + tek harf kısayolları + araç şeridi düzeni | Editör CAD çekirdek setinde; tüm dönüşümler undo'lu; 5 yeni Core testi | ✅ Tamamlandı |
| 13 | **Duvar açıklıkları** (§3.9): `DuvarAcikligi` kapı/pencere — konum-tabanlı duvar bağı (0,15 m tolerans), (s,z) düzleminde Clipper fark + keyhole ile delik, kendi kategorisiyle dolgu (kabuk sızdırmaz, alan korunur); 🚪 Açıklık aracı + AÇIKLIKLAR paneli + canvas işaretleri; "Kapı"/"Pencere" katmanları (varsayılan 11); duvara bağlanmayan açıklık uyarısı | Kapı/pencere M01'de ayrı malzeme kategorisi olarak akar; 3 yeni test | ✅ Tamamlandı |
| 12 | **P2 devam** (§3.8): seyirci/balkon 3D yüzeyleri kabuğa **kırpılır** (panel kesişimi + korniş/parapet/saçak şerit kırpma) + **orkestra çukuru** (zemin açıklığı + taban + duvarlar, "Orkestra" kategorisi/katmanı, hacim +) + **faset ayarı** (`FasetKenarSayisi` 12–128, UI alanı) + sahne kulesi reçetesi (cep + tavan profili) | Taşan yerleşim hayalet yüzey üretmez; çukur 2D/3D/hacim/doğrulamada; 4 yeni test | ✅ Tamamlandı |
| 11 | **P2 akustik özellik paketi** (§3.7): eğik/parçalı **tavan kesit profili** (`TavanKirilimNoktasi` + `SalonTavanServisi`; bantlı düzlemsel paneller, duvar üstleri profili izler, hacim ∫z(y)dA) + **koltuk kapasitesi** (0,5 m²/koltuk + m³/koltuk Kontrol'de) + `ISalonGeometri` tam sözleşme (ISahne/ISeyirci/IBalkon/ITavanKirilim) + seyirci/balkon **Seçiliyi Güncelle** akışı + sahne/seyirci **taban kotu** alanları (balkon üstü seyirci UI'dan kurulabilir) + canvas kırılım çizgileri | Profilli tavan 2D/3D/DXF/M01/hacimde tutarlı; 7 yeni test | ✅ Tamamlandı |
| 10 | **P1 editör güvenilirliği**: undo/redo (JSON snapshot, Ctrl+Z / Ctrl+Shift+Z / Ctrl+Y + ↶↷ toolbar, canvas `OnDegisecek` + tüm panel mutasyonları, limit 50) + `IsDirty` (başlıkta `*`, temizken kapanış onayı sorulmaz, Ctrl+S) + atomik kaydet (tmp+move, hata → revizyon geri alınır) + vertex sürüklemede self-snap dışlama + trim kapalılık kaybı uyarısı + DXF ihracatı kulak-kesme üçgenleme (`RayTracingService.TriangulatePolygon` public — konkav/keyhole zeminler) + Sığdır sahne/seyirci/balkon kapsar | Ctrl+Z tüm mutasyonları geri alır; temiz pencere sormadan kapanır; kırık kayıt dosyası oluşmaz; 3 yeni test | ✅ Tamamlandı |
| 18 | **Denetim/güvenilirlik kapanışı:** koleksiyon kimliği, v2 migrasyon, model bütünlük kapısı, hacim/normal/kırpma/açıklık/keyhole/kategori düzeltmeleri; güvenli UI belge/taslak/klavye/pointer yaşam döngüsü | Core+App 0 hata/uyarı; eski ve yeni M51 regresyonları geçer | ✅ Tamamlandı |
| 19 | **M01 ve DXF entegrasyonu:** kayıpsız geometri yenileme, stale sonuç/senaryo kuralı, kaynak yol+revizyon; tüm ihracatlarda `$INSUNITS` | M01 çalışması korunur; metre/mm round-trip | ✅ Tamamlandı |
| 20 | **Oturma + görüş:** gerçek koltuk/koridor/erişilebilir yer üretimi, C-değeri ve ısı haritası; seyirci panelinde fiziksel ayarlar | Her koltuk yerleşir ve analizde eşleşir | ✅ Tamamlandı |
| 21 | **Canlı kesit + akustik ön kontrol + yansıtıcı:** birleşik Analiz penceresi | 6 RT bandı, kalite, kesit ve panel dizisi tek akışta | ✅ Tamamlandı |
| 22 | **Parametrik/CAD+:** kalıcı ölçü ve atomik kısıt; fillet/pah/dizi/hizala/dağıt/grup/pano; katman kilidi ve blok metadata | Tek undo adımı; round-trip korunur | ✅ Tamamlandı |
| 23 | **Altlık + kalite haritası:** DXF/PDF/görsel referans, dönüşüm/kalibrasyon; odaklanabilir model sağlık bulguları | DXF altlık canvas'ta; hatalar M01 öncesi bulunur | ✅ Tamamlandı |
| 24 | **Hızlı kullanım:** hedef koltuktan tek tık tasarım, Ctrl+K komut paleti, otomatik kurtarma/sürüm geçmişi, editör ayar kalıcılığı | Ana dosya metadata'sı değişmeden kurtarma; 791/791 test | ✅ Tamamlandı |

## 5. Riskler / Dikkat Noktaları
- **Kapalı hacim:** ✔ Kontrol butonu `LeakTestService` sızıntı testini çalıştırır (§3.6);
  kullanıcı M01'e göndermeden kabuk kapalılığını görebilir. Serbest çizimde kapanma hâlâ
  kullanıcı sorumluluğunda.
- **Trim sağlamlığı:** segment-segment kesişim/bölme sayısal kenar durumları (paralel, uç-uca).
  Trim kapalı şekli çizgilere ayırır → kapalılık (kabuk) kaybolur; kullanıcı artık durum
  çubuğunda uyarılır ve Ctrl+Z ile geri alabilir (Faz 10).
- **Faset çözünürlüğü:** daire/oval kenar sayısı akustik hassasiyet ↔ yüzey sayısı dengesi.
- **Keyhole şeridi:** kırpma delik açarsa zemin ±2 mm'lik kesikle bölünür; DXF'te çok ince
  slit görünebilir (akustik etkisi ihmal). ClipperAdaptor 1 mm ızgarası altına inilmemeli.
- **Davranış değişikliği (Faz 9):** Zemin dışı katmandaki kapalı öğe artık kabuk üretmez
  (bölge yüzeyi olur). Eski dosyada dış hattı yanlış katmana çizen kullanıcı kabuk kaybını
  ✔ Kontrol uyarısıyla görür.
- **Akustik ön kontrol sınırı:** RT60 tahmini varsayılan kategori yutumlarıyla erken tasarım
  geri bildirimidir; nihai malzeme, kaynak/alıcı ve ışın izleme hesabı M01'de yapılır.
- **PDF/görsel altlık:** dosya yolu ve kalibrasyonu kalıcıdır; DXF segmentleri ayrıca önbelleklenir.
  Kaynak PDF/görsel taşınırsa yeniden bağlamak gerekir.

## 6. Kullanım Kılavuzu (özet)
1. Akustik launcher → **🎭 Salon Tasarla**.
2. En hızlı yol: **✨ Hızlı Tasarım** → hedef koltuk + kullanım + plan tipi. İnce ayar için bir
   katman seç (Zemin) → zemini çiz; Sahne katmanına geç → sahneyi çiz; çizgilerden duvar üret;
   tavanla kapat; seyirci alanına kademe + eğim ver; istersen balkon/loca ekle.
3. Alternatif: **Hazır Şablon** veya **🗺 Altlık**; seyirci panelinde gerçek koltuk/koridor
   ölçülerini belirle. Seçili öğelerde **🛠 CAD+** ile ölçü, kısıt, fillet, dizi ve hizalama kullan.
4. **📊 Analiz** → koltuk/görüş ısı haritası, kesit, kalite, RT60 ve yansıtıcı önerisini kontrol et.
5. **Kaydet** → `.e4salon.json`; **🕘 Sürümler** ile anlık sürüm/kurtarma, Ctrl+K ile komut ara.
6. İstersen **DXF Dışa Aktar**; M01'de **Salon Yükle** ile geometriyi al, malzeme atayıp hesapla.

## 7. Karar Günlüğü
| Tarih | Karar |
|---|---|
| 2026-08-10 | **Faz 18-24 denetim ve gelişmiş tasarım paketi:** şema v2/12 anlamsal katman ve model bütünlük kapısı; güvenli belge+taslak+undo yaşam döngüsü; 3B/hacim/açıklık/kırpma/normal düzeltmeleri; kayıpsız M01 yenileme ve birim damgalı DXF; gerçek koltuk/koridor/erişilebilir yer + C-değeri ısı haritası; canlı kesit, 6 bant akustik ön kontrol ve hedefe yönlü yansıtıcı; kalıcı ölçü/kısıt ve CAD+; DXF/PDF/görsel altlık, kalite haritası, hızlı tasarım, Ctrl+K, otomatik kayıt/sürüm geçmişi, kalıcı ayarlar. Core/App/Tests 0 hata/0 uyarı; 791/791 test. |
| 2026-06-23 | M51 "Salon Tasarla" ortak modülü açıldı (50-59 bandı); M01'e girdi üreten salon/oditoryum editörü |
| 2026-06-23 | M01 çıktısı: native `.e4salon.json` köprüsü **ve** tam DXF — ikisi de birinci sınıf (kullanıcı kararı) |
| 2026-06-23 | İlk teslim (MVP): serbest 2D çizim motoru (zemin→sahne→duvar→tavan + line/polyline/circle/rectangle + trim/del) |
| 2026-06-23 | Eğriler (daire/oval) 3D üretim/DXF anında çokgene fasetlenir; katman `Ad`'ı M01 kategorisi olur |
| 2026-06-24 | Baştan-aşağı kontrol: `DxfExportService` `Encoding.ASCII`→**UTF-8 (BOM'suz)** (Türkçe "Korniş"/katman adları bozuluyordu — tüm DXF tüketicileri yararlanır); çift-tık ile polyline bitirme bağlandı; tam-model (sahne/seyirci/balkon) round-trip testi eklendi. 435+121 test geçer |
| 2026-07-03 | **Profesyonel çizim paketi (Faz 14-17, §3.10):** kullanıcı isteğiyle editör CAD çekirdek setine çıkarıldı — çoklu seçim (Shift + AutoCAD window/crossing bandı), Taşı/Kopyala/Aynala/Döndür (Ctrl=kopya, hayalet önizleme, `SalonDonusumServisi` Core'da testli), yay çizimi (3-nokta; `Segmentler` fasetlediğinden 3D/DXF/trim/snap bedava), offset, uzat, `uzunluk<açı` dinamik girişi, polar/dik/merkez snap, ölçüm ve tek harf kısayolları. O tarihte kapsam dışı kalan kalıcı ölçü/kısıt, blok/grup, fillet ve array Faz 22'de tamamlandı. |
| 2026-07-02 | **Duvar açıklıkları (Faz 13, §3.9):** kapı/pencere tanımı eklendi — konum-tabanlı duvar bağı (Id yok; trim'e dayanıklı), duvar (s,z) düzleminde Clipper fark + keyhole delik, delik açıklık kategorisiyle doldurulur (kabuk sızdırmaz, toplam alan korunur, M01'de ayrı malzeme); 🚪 canvas aracı ile duvara tıklayarak yerleştirme; varsayılan katman 9→11 (Kapı, Pencere). 528 test geçer |
| 2026-07-02 | **P2 devam (Faz 12, §3.8):** seyirci basamak/korniş ve balkon döşeme/saçak/parapet kabuğa kırpılır (`KabugaKirp` + `YatayParcalar`/`DikeyParcalar`); orkestra çukuru eklendi (zemin açıklığı + taban + 4 duvar, "Orkestra" kategorisi, varsayılan katman 8→9, hacim +alan×derinlik, sahne çakışma uyarısı); faset kenar sayısı kullanıcı ayarı oldu (12–128 kelepçeli, tüm servisler tek değeri kullanır); sahne kulesi = cep + tavan profili reçetesi belgelendi. 525 test geçer |
| 2026-07-02 | **P2 akustik özellik paketi (Faz 11, §3.7):** tavan kesit profili (Y→kot kırılımları; bantlı düzlemsel eğik paneller, duvar üstleri profili izler, hacim ∫z(y)dA momentle); koltuk kapasitesi (0,5 m²/koltuk, Kontrol'de m³/koltuk); `ISalonGeometri` tam sözleşme; seyirci/balkon Güncelle akışı + taban kotu alanları; canvas kırılım çizgileri. 521 test geçer |
| 2026-07-02 | **P1 editör güvenilirliği (Faz 10):** undo/redo (değişiklik-öncesi JSON snapshot deseni — canvas `OnDegisecek` callback'i + pencere yığınları; Ctrl+Z/Ctrl+Shift+Z/Ctrl+Y/Ctrl+S); `IsDirty` + başlıkta `*` + temizken sormadan kapanma (maincodex §4.2 uyumu); atomik kaydet (tmp+move, başarısız yazımda revizyon geri alınır); vertex sürüklemede öğenin kendi noktaları snap adayı olmaktan çıkarıldı (self-snap kilidi çözüldü); trim kapalı şekli bozarken açık uyarı; `DxfExportService` naif fan → `RayTracingService.TriangulatePolygon` (kulak-kesme, public yapıldı — P0 keyhole zeminleri konkav olabildiği için kritikleşmişti); Sığdır sahne/seyirci/balkon footprint'lerini kapsar. 514 test geçer |
| 2026-07-02 | **P0 güvenilirlik paketi (Faz 9, §3.6):** (1) RoomVolume bbox→gerçek plan hacmi (`SalonHacimServisi`; bbox M01'in daha iyi tahmincisini de bastırıyordu); (2) kabuk kuralı katman tipine bağlandı — çift tavan/hayalet iç duvar kapandı; (3) zemine oturan sahne/seyirci/bölge footprint'leri Clipper2 ile zeminden kırpılır (keyhole ±2 mm; 1 mm ızgara altı şerit sıfıra yuvarlanıyor — öğrenildi); (4) kabuk duvar/tavan kategorileri katman adından türetilir, varsayılan katman 6→8 (Korniş+Balkon); (5) şablon seyircisi plan hattına sığdırılır (yelpaze/at nalı taşması düzeltildi), "salon önü" sahneye dış hatta **sahne cebi** açılır; (6) `SalonDogrulamaServisi` + M51 "✔ Kontrol" (geometri uyarıları + LeakTest sızıntı raporu + hacim); (7) `AcousticsProject.KaynakGeometriRevizyon` + M01 proje açılışında salon revizyon uyarısı. 511 test geçer |
