# CODEX 50 — Bina Geometrisi (Ortak Modül)

> **Durum:** Tamamlandı — Faz 1-5 bitti, M12/M08 köprüleri kurulu (2026-06-13). §9 Çoklu Kat Tek
> DXF (2026-07-17) tamamlandı. Aktif/üretimde.
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
   veya boş kat ekleyip **✏ Oda Çiz** ile elle çiz. Seçilen DXF'te yan yana birden fazla
   plan tespit edilirse (§9) **Çoklu Kat Dağıtımı** diyaloğu açılır: her plan kümesi için
   önerilen ad/sıra/yükseklik düzenlenip dahil/hariç işaretlenir; onaylanınca katlar tek
   seferde ▲ üst → ▼ alt sırasıyla listeye eklenir.
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
| 2026-06-16 | 2D viewport'a **referans (hayalet) kat** katmanı eklendi: sol panelde combo'dan ikinci kat seçilir, turuncu kesikli hayalet olarak aktif katın altına çizilir (DXF + oda hatları, iki katın OriginOffset farkı uygulanır). Aktif katın ofsetini ayarlayarak kat kayması gözle hizalanır. `BinaGeometriCanvas.ReferansKat` + VM `EtkinReferansKat`; etkileşim yok, salt görsel altlık |
| 2026-06-16 | `DxfKatPlaniParser` zayıf oda taraması durumunda (çok az oda / çok küçük toplam alan) `$INSUNITS` doğru görünse bile 10x/100x alternatif ölçekleri dener; annotasyon katmanları (`YAZI`, `DIM`, vb.) bu alternatif ölçek değerlendirmesinde duvar segmenti sayılmaz. Yeşilova `Bodrum.dxf` gibi başlığı mm olup koordinatı cm davranan dosyalarda M50 oda blokları üretir ve kullanıcıya ölçek düzeltme uyarısı gösterir. |
| 2026-07-04 | **Güvenilirlik düzeltmeleri (kod incelemesi sonrası) — SemaSurum 1→2.** (Hata 1) Düşey birleşim üyeleri artık kararlı `BinaKat.Id`'ye (`BinaBirlesimUyesi.KatId`) bağlı; kat sıralama/silme ile `KatNo` değişse bile `BinaDuseyBirlesimServisi` doğru katı bulur. `KatNolariYenile` yeniden numaralandırmada üye `KatNo` + `Üst/AltKatNo`'yu tazeler (M08 köprüsü `KatNo` okur). Eski `.e4bina` dosyaları yüklemede `KatIdleriTamamla` ile backfill edilir. (Hata 2) `VM.KatSil` silinen katın odalarına bağlı birleşimleri `OdaSilindi` ile kaldırır (askıda birleşim / hayalet açık tavan-döşeme kapandı). (Hata 3) Otomatik tespit edilen açıklıklar tip varsayılan yüksekliğini alır (`BinaAciklik.VarsayilanYukseklik`: kapı 2.1 / pencere 1.5 m) — daha önce 0 kalıyordu; yeniden tarama artık kullanıcının girdiği yükseklik/eşiği de korur. 540 test geçer (5 yeni M50 testi). |
| 2026-07-04 | **Orta öncelikli iyileştirmeler (kod incelemesi devamı).** (§4.2 uyumu) M50'ye **IsDirty** eklendi: başlıkta `*`, temizken kapanışta soru sorulmaz; tüm mutasyon yolları (undo snapshot, kat komutları, ofset, DXF yükleme, oda/açıklık/kat özellik düzenlemeleri) kirletir, Yeni/Aç/Kaydet temizler. **Atomik kaydet**: `BinaGeometriServisi.Kaydet` `.tmp`'ye yazıp üzerine taşır (yarıda kalan yazım `.e4bina`'yı bozmaz). **DXF oda tespiti tutarlılığı**: birincil geçiş de annotation-hariç segment setini kullanır (alternatif-ölçek geçişleriyle aynı; `detectionSegments` artık kullanılıyor). **Canvas layout önbelleği**: `BuildLayout` sonucu yapısal imza (köşe/segment/açıklık sayıları + ofsetler) ile önbelleğe alındı — büyük planlarda her fare hareketinde tam tarama önlendi + köşe sürüklerken görünüm sabit. 541 test geçer (+1 IsDirty testi). |
| 2026-07-17 | **Güvenilirlik paketi 2 (tam kod incelemesi — 13 bulgu düzeltildi).** (H1) Seçili kata DXF yeniden yükleme artık §3.5'e uygun: yeni `OdaEslestirmeServisi` merkez-örtüşme ile eski oda Id'lerini devreder, eşleşmeyen odaların düşey birleşimleri temizlenir; açıklıklar `Bul(mevcut)` ile yenilenir (manuel/iptal + kullanıcı boyutları korunur). (H2) Yeni/Aç artık IsDirty'de kaydet-kaydetme-iptal sorar. (O1) Kat listesine "▲ Üst / ▼ Alt (Taban)" etiketleri — liste sırası = bina yüksekliği konvansiyonu görünür kılındı. (O2) M12→M50 köprüsü açıklıklara tip varsayılan yüksekliği atar; M08 fallback'i M50 varsayılanıyla hizalandı (pencere 1.2→1.5). (O3) `AciklikTara` kayıtlı dosyada ham segmentleri DXF'ten yeniden okur (tam-set ilkesi). (O4) `Kaydet` revizyonu yalnız başarılı yazım sonrası kalıcılaştırır. (D1) Başarısız patlatma/geri-al boş undo adımı + sahte IsDirty üretmez. (D2) Undo/redo silinmiş kata ait adımı yutmaz, atlar. (D3) İki ayrı aktif birleşim patlatmayla buluşursa tek birleşimde toplanır. (D4) `AyniBirlesimdeAcik` TavanAcik/DosemeAcik bayraklarını da denetler. (D5) Mimari adapter `YeniOlustur` kullanır (güncel SemaSurum damgası). (D6) `EstimateScaleFromTexts` MTEXT'i de okur. (D7) Kat Id formatı tekleşti. **Not (düzeltme):** bu paket "50 M50 testi" ile doğrulandı denmişti ama
o komut (`dotnet test`) yalnız `ZayifAkimTekHatTests`'teki 50 xUnit `[Fact]`'ini çalıştırıyor —
proje asıl test bataryası `dotnet run --project Kst.Acoustics.Tests` ile çalışan özel dizi
tabanlı koşucudur (Program.cs). Bu paket dahil tüm mevcut testler o koşucuda da doğrulandı
(bkz. bir sonraki satır — 597 test). |
| 2026-07-17 | **Çoklu kat tek DXF tamamlandı (§9, Faz 9.1-9.5).** Yan yana çizilmiş kat planlarını
kümeleyip katlara dağıtan özellik uçtan uca çalışır: `KatKumelemeServisi` (grid+BFS kümeleme),
`KatEtiketServisi` (ad/sıra tahmini), `DxfKatYukleyici.CokluKatTespitEt/CokluKatUret`,
`CokluKatDagitimWindow` UI, `DxfYukle_OnClick` entegrasyonu. Gerçek test koşucusu
`dotnet run --project Kst.Acoustics.Tests`: **597 test geçer** (12 yeni §9 testi dahil);
Core.Tests (`dotnet run --project Kst.Acoustics.Core.Tests`): **121 test geçer**. |

## 9. Çoklu Kat Tek DXF (Yan Yana Planlar) — Tamamlandı (2026-07-17)

> **Amaç:** Tüm kat planları TEK DXF'te model space'te yan yana çizilmişse (yaygın CAD
> pratiği), M50 bunları otomatik ayırıp katlara dağıtır. Bugünkü akış kat başına ayrı
> DXF istiyor; bu özellik tek dosyadan N kat üretir.
>
> **Kod:** `Core/Bina/Services/KatKumelemeServisi.cs`, `KatEtiketServisi.cs`,
> `DxfKatYukleyici.CokluKatTespitEt/CokluKatUret` · `App/Bina/Views/CokluKatDagitimWindow`
> · Testler: `Kst.Acoustics.Tests/CokluKatTests.cs` (12 test). 597 test geçer.

### 9.1 Akış (hedef davranış)

1. Kullanıcı **DXF Kat Planı Yükle** ile dosyayı seçer (ayrı buton yok — otomatik tespit).
2. Parser tam ham segment/metin/arc setini çıkarır (mevcut `ExtractRaw*` hattı, tek geçiş).
3. `KatKumelemeServisi` segmentleri mekânsal olarak kümeler; **≥ 2 plan kümesi** bulunursa
   "N kat planı tespit edildi — katlara dağıtılsın mı?" diyaloğu açılır. Tek küme → mevcut
   tek-kat akışı aynen devam eder (geriye dönük davranış değişmez).
4. Diyalogda her küme bir satır: **tahmini kat adı** (etiketten), **sıra**, **kat yüksekliği**,
   **dahil/hariç** işareti (antet/lejant/görünüş kümeleri hariç önerilir). Kullanıcı düzenler,
   onaylar.
5. Her dahil küme kendi yerel çerçevesine normalize edilir (bbox-min), mevcut oda tespiti +
   açıklık tespiti + sadeleştirme hattından geçer ve `BinaKat` olarak listeye eklenir —
   **M50 konvansiyonuyla: en üst kat listede en üstte, taban en altta** (§7 / "▲ Üst" etiketi).
6. Kaba hizalama otomatik (bbox merkez eşleme); ince ayar mevcut `OriginOffset` +
   hayalet referans kat akışıyla yapılır.

### 9.2 Teknik tasarım (gerçekleşen)

- **Kümeleme (`KatKumelemeServisi`):** 1 m hücreli doluluk ızgarası → `KumeBoslukEsigiM`
  (varsayılan 5 m) kadar dilate → 8-komşuluk bağlı bileşen (BFS). ≤1 bileşen bulunursa
  tek-küme fallback döner (çağıran normal akışa düşer). 1M segmentte O(n) — performans
  riski yok.
- **Plan-değil filtresi (`PlanFiltrele`):** küme bbox alanı `MinPlanAlaniM2` (varsayılan
  20 m²) altındaysa "plan değil" (antet, lejant, görünüş) sayılır; dağıtım diyaloğunda
  listelenir ama **dahil** kutusu varsayılan işaretsiz gelir (kullanıcı isterse işaretler).
- **Ad/sıra tahmini (`KatEtiketServisi.TahminEt`):** öncelik sırasıyla tip-kat aralığı
  (`N-M. KATLAR`) → asma kat → çatı → numaralı bodrum → düz bodrum → zemin → `N. KAT`.
  Sıra: bodrum negatif, zemin 0, asma 0.5, normal kat pozitif tam sayı, çatı 9999. Eşleşme
  yoksa `EtiketBulundu=false` + küme X-konumu geçici sıra değeri — kullanıcı diyalogda düzeltir.
- **Ölçek tek geçiş:** `CokluKatTespitEt`, `DxfKatPlaniParser.Parse(path, normalizeOrigin:false)`'ı
  TÜM dosya için bir kez çağırır (kanıtlanmış alternatif-ölçek/zayıf-tespit mantığı aynen
  kullanılır); dönen `HamSegmentler/HamMetinler/DxfArclar` dünya koordinatında kümelenir.
  **Bilinen ödün:** dosya tek-planlıysa (`kumeler.Count<=1`) bu ilk `Parse` çağrısı boşa gider,
  çağıran normal `Yukle()` ile dosyayı ikinci kez açar — büyük tek-plan DXF'lerde küçük bir
  ekstra gecikme (arka planda `Task.Run`, UI bloklanmaz). v2'de tek geçişe indirilebilir.
- **Kat üretimi (`CokluKatUret`):** her onaylı küme kendi `BinaKat`'ına dönüşür — mevcut,
  test edilmiş yapı taşları yeniden kullanılır: `DxfKatPlaniParser.OdalariBosluklardanYenidenTara`
  (flood-fill oda tespiti) → `AciklikTespitServisi.Bul` + `AciklikYardimci` (açıklık) →
  `DxfKatPlaniParser.NormalizeOrigin` (kümenin kendi bbox-min'ine, tek-kat akışıyla AYNI
  yöntem) → `DxfSegmentYardimci.Sadelestir` (görüntü segmentleri). Kat üretimi sırasında
  DXF'in polyline/line/text-etiket fallback zincirleri (yalnız tek-kat `Parse` yolunda)
  ÇALIŞMAZ — v1'de per-küme tespit yalnız flood-fill kullanır; bilinçli kapsam daraltması
  (bkz. §9.4).
- **Id kararlılığı:** yeni katlar yeni Id alır (ilk yükleme, `DxfYukle_OnClick`). Var olan
  bir kata küme atama (v2, "yeniden dağıt") `OdaEslestirmeServisi` merkez-örtüşme devrini
  kullanabilir (§3.5) — henüz uygulanmadı.

### 9.3 İş Programı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 9.1 | `KatKumelemeServisi`: grid + union-find kümeleme, eşik parametreleri, plan-değil filtresi | Sentetik segment setlerinde (2-4 yan yana plan + antet) doğru küme sayısı; testler | ✅ Tamamlandı |
| 9.2 | `KatEtiketServisi`: ad regex'leri + sıra türetimi (bodrum→taban, çatı→üst) | Etiketli/etiketsiz senaryolarda doğru ad ve M50 liste sırası; testler | ✅ Tamamlandı |
| 9.3 | `DxfKatYukleyici.CokluKatTespitEt` (tek geçiş ölçek + kümeleme + tahmin) + `CokluKatUret` (küme→BinaKat, bbox-min normalize) | Sentetik iki-planlı DXF'te doğru oda/alan/segment üretimi (karşılaştırma testi) | ✅ Tamamlandı |
| 9.4 | UI dağıtım diyaloğu (`CokluKatDagitimWindow`): küme listesi (ad/sıra/yükseklik/dahil-hariç), onay → `DxfYukle_OnClick`'te katların sıraya göre eklenmesi, IsDirty | Örnek çoklu-plan DXF'i uçtan uca katlara dağıtılır | ✅ Tamamlandı |
| 9.5 | Kaba hizalama: bbox-min normalize (bbox-merkez yerine — tek-kat akışıyla tutarlı, paylaşılan grid orijinli gerçek planlarda daha isabetli); belgeler + §7 güncellemesi | Katlar makul hizada açılır; belgeler tutarlı | ✅ Tamamlandı |

### 9.4 Riskler / açık konular (kapsam dışı bırakılanlar — v2)
- **Bitişik planlar** (küçük boşluk): eşik parametrik (`KumeBoslukEsigiM`); yanlış birleşirse
  kullanıcı dosyayı ayırır — diyalogda "kümeyi böl/birleştir" arayüzü yok.
- **Tip kat** ("3-8. KATLAR TİP"): tek kat olarak gelir (adında "(Tip)" işaretiyle); aynı
  planı N kata çoğaltma (kopyalama + Id üretimi) yapılmıyor.
- **Per-küme oda tespiti yalnız flood-fill:** tek-kat `Parse`'ın polyline/line/text-etiket
  fallback zincirleri (duvarlar kapalı poligon değilse) küme bazında ÇALIŞMAZ; flood-fill 0
  oda bulursa o kat boş odayla gelir, kullanıcı M50'de elle çizer.
- **Tek-plan dosyada çift `Parse`:** performans notu — bkz. §9.2 "Ölçek tek geçiş".
- **Hizalama:** bbox-min normalize simetrik olmayan / farklı köşe referanslı planlarda
  kayabilir; kesin hizalama kullanıcıda (mevcut hayalet referans kat + `OriginOffset` akışı,
  §2.8/§7 — yeni kod eklenmedi, olduğu gibi kullanılıyor).
- **Etiket çeşitliliği:** regex seti gerçek dosyalarla genişletilecek; tahmin her zaman
  diyalogda düzeltilebilir olduğundan yanlış tahmin veri bozmaz.

## 5 Kasım 2026 — sıralı modül denetimi

Gelecek şemalar reddediliyor; sabit .tmp yerine benzersiz geçici dosya ve .bak yedeği kullanılıyor (A14, A16).

Ayrıntılı hata listesi ve doğrulama: depo kökündeki `modul-denetimi-2026-11-05.md`.

## 10. DWG/DXF otomatik mimari içe aktarma — 2026-11-05

**Durum: otomatik akış eklendi; yalnız küçük düzeltme gerektiren genel mimari rekonstrüksiyon hedefi henüz tamamlanmadı.**

- M50 sol panelinde `Mimari Proje Yükle…`: DWG/DXF seçimi, kat ayırma, oda/etiket/açıklık çıkarma, kalıcı içe aktarma raporu. Mevcut bina listesine yeni katlar eklenir.
- `MimariCadDosyasi`: ODA File Converter veya kurulu AutoCAD Core Console ile izole çalışma kopyası; kaynak DWG değiştirilmez. Geçici çıktı okuma bitene kadar yaşar, sonunda temizlenir. Motor için `EYE4PRO_CAD_CONVERTER` kullanılabilir. CAD motoru yoksa açık hata verir.
- Ham pafta önce bölünür; bütün belge tek oda taramasına verilmez. Birimsiz çizimde yeterli kapı yayı kanıtı varsa ölçek önerilir ve kontrol notu kaydedilir.
- Temas eden pafta çerçeveleri ayrılır. Ölçekli ana plan başlıkları antet tekrarlarına tercih edilir; kesit, görünüş, vaziyet ve tavan paftaları elenir.
- Oda adları semantik olarak sıralanır; malzeme notları, kapı kodları ve ölçüler mahal adı olmaz. İç içe blok metinleri ve blok attribute değerleri okunur.
- Açık kat yüksekliği yazıları ve doğrulanabilir ardışık kat kot farkları kullanılır. `h=` oda net yüksekliğidir. Çelişkili/eksik yükseklikte 3 m **geçici** değer raporlanır; kesin ölçü sayılmaz.
- Yakındaki kapı/pencere ölçüleri geometrik genişlikle doğrulanır. Çift kanat ve iki duvar yüzündeki aynı açıklık eşleştirmesi desteklenir; belirsiz yerleşim uydurulmaz.
- 3D duvarlar kapı/pencere dikdörtgenlerini keser; denizlik ve açıklık yüksekliği uygulanır. `TabanKotuM` düzenlenebilir. Açık sıfır denizlik ile eski 0=varsayılan davranışı ayrılır.
- Şema 3: mutlak kat kotu, açık sıfır eşik ve rapor notları. Eski dosyalar göç eder; eski uygulamanın yeni alanları sessiz kaybetmemesi için yeni sürüm damgası kullanılır.

### Doğrulama ve mevcut sınırlar

Kullanıcının Beylerbeyi mimari DWG dosyası kurulu AutoCAD 2027 ile uçtan uca açıldı. Son otomatik analiz:

- Zemin kat ve 1. kat: 32 + 29 **oda adayı**, 34 + 25 açıklık; bunlar doğrulanmış mahal adetleri değildir.
- Çatı altı/teras: 31 geometrik alan adayı; kapalı mahal kanıtı bulunmadığı için oda blokları gizlendi, kaynak çizim korundu.
- Zemin/1. kat/çatı için sırasıyla 4/5/2 açıklık kaydına yazılı ölçü eşleşti. Kalanlar varsayılan ölçü/kontrol notu taşır.
- Zemin ve 1. katın kot yazıları kademeli döşeme/merdiven kotlarıyla karışıyor; güvenilir kat yüksekliği çözülemedi. Kesit geometrisinden katlar arası ilişki çıkarımı, aks tabanlı hizalama, eksik açıklıkların blok/duvar semantiğiyle çıkarımı ve oda sınırlarının iyileştirilmesi sonraki geliştirme gereksinimleridir.
- Tam çatı kabuğu/BIM rekonstrüksiyonu yapılmıyor. Çatı taraması otomatik olarak oda hacmi sayılmıyor.

Derleme: `dotnet build src/Kst.Acoustics.App --no-restore -p:NuGetAudit=false -p:UsedAvaloniaProducts=` — 0 hata/uyarı.

Regresyon çalıştırıcıları (`dotnet run --project tests/<ad>.Regression -p:NuGetAudit=false -p:UsedAvaloniaProducts=`):
`BinaImport` 9, `Bina3D` 9, `BinaKat` 11, `BinaKalite` 6, `BinaMetin` 56, `BinaExisting` 42; toplam 133 kontrol geçti.

Yerel inceleme çıktıları: `scratch/bina-import/automatic.txt`, `scratch/bina-import/result.e4bina.json`, `scratch/bina-import/dwg-smoke.txt`. Kaynak DWG ve dönüştürülmüş çizim test verisidir; dağıtım paketine/depo kalıcı örneklerine dahil edilmemelidir.

## 11. Katman seçimi → Üret akışı — 2026-11-05

Kullanıcının talebiyle dosya seçimi artık hemen oda/kat üretmez. Ana `Mimari Proje Yükle…` ve seçili kata `DWG/DXF Planı Yükle…` girişleri önce katman önizlemesini açar.

1. DWG gerekiyorsa geçici çalışma kopyasından DXF'e çevrilir; kaynak dosya değiştirilmez.
2. `KatmanliOku` katmanları, çizgileri, yazıları ve yayları belleğe alır. CAD'deki kapalı/donmuş katmanlar başlangıçta kapalıdır.
3. Kullanıcı katman araması, kutular, tümünü aç/kapat veya dosyadaki duruma dönüş ile çizimi sadeleştirir. Yakınlaştırma, kaydırma ve ekrana sığdırma vardır. Oda adları/ölçüler için yazı katmanları gerektiği ekranda belirtilir.
4. **Üret** yalnız seçilmiş verinin anlık kopyasını işler. Önizleme güncellenirken, bütün katmanlar kapalıyken veya kullanılabilir tespit çizgisi yokken kapalıdır. **Vazgeç** mevcut modeli değiştirmez.
5. Kaynak `0` katmanlı blok nesneleri insert katmanını devralır. Ana blok kapalıysa açık alt katmanlar da üretime sızmaz; görünür alt katmanları ayrı kapatmak mümkündür.
6. `KaynakKatmanlari` ve yerel `KayitliTespitSegmentleri` `.e4bina` içinde saklanır. Kayıttan sonra **Tara**, bütün DWG/DXF'i filtresiz yeniden okuyup kapalı katmanları geri getirmez.

Dosyalar: `MimariKatmanliCizim`, `DxfKatPlaniParser.Katmanlar`, `MimariKatmanSecimWindow`, `MimariKatmanOnizlemeCanvas`. Önizleme sade CAD çizgi/metin gösterimidir; tam AutoCAD pafta çizicisi değildir.

Doğrulama: `BinaKatman.Regression` 22/22, `BinaImport.Regression` 9/9, `BinaExisting.Regression` 44/44; toplam 75 kontrol geçti. Uygulama derlemesi 0 hata/0 uyarı. Örnek Beylerbeyi dosyası önizlemede 52 katman olarak açıldı. Küçük ekranda pencere çalışma alanına sığdırılıyor. Katman seçimi §10'da belgelenen mimari anlamlandırma sınırlarını tek başına ortadan kaldırmaz.

## 12. Katman ve yeniden tarama doğruluğu — 2026-11-05

- Tefriş/yazı gibi tespit dışı katmanların yayları önizlemede kalır; kapı tespitine ve oda sınırlarının sanal kapatılmasına aktarılmaz. Böylece görünür dekoratif yaylar kapı kanıtı sayılmaz.
- **Tara**, aynı geometride tekrar bulunan otomatik açıklığın kimliğini, tipini, yüksekliğini, denizliğini ve ölçü kaynağını korur. İki uç için 2 cm tolerans ve iki yönde tekil eşleşme gerekir; belirsiz eşleşmeye bilgi devredilmez. Yeni uçlar ve genişlik güncel geometriden kalır. Silinmiş otomatik kayıtlar geri eklenmez.
- Komşu oda duvarındaki yansıma oluşturulduktan sonra da bu eşleştirme uygulanır. Her iki yüzün ayrı kimliği ve ölçüleri bellek içi taramada ve kayıt/aç sonrasında korunur.
- Seçili kata dosyayı **yeniden yükleme**, eşleştirmeden sonra yeni çizimin ölçü etiketlerini uygular. Kaynaktaki güncel ölçü önceki otomatik açıklığın düzenlenmiş ölçüsünü değiştirebilir; bu işlem yalnız **Tara** ile aynı değildir.
- Noktasız küçük `ı` açıkça normalleştirilir: `sığınak`, `danışma`, `çalışma odası`, `çatı katı` ve `kapı` tanınır; `sıva` mahal adı sayılmaz. Katman araması `siva/sıva`, `olcu/ölçü` farklarını tolere eder; CAD katman kimlikleri değiştirilmez.
- Örnek projede görülen `MDFLAM ALTI DOLAP`, `AKRİLİK KAP.` ve gömme dolap notları mahal adı olmaz. `MDF Deposu`, `Dolap Odası` ve `Seramik Atölyesi` gibi kullanım amacı içeren gerçek mahal adları korunur.

Son doğrulama: `BinaKatman` 25, `BinaMetin` 67, `BinaAciklikYenile` 19, `BinaExisting` 47, `BinaImport` 9, `BinaKat` 11, `BinaKalite` 6, `Bina3D` 9; **193 kontrol geçti**. Uygulama derlemesi 0 hata/0 uyarı. Kayıtlar: `scratch/bina-import/final-*.txt`.

Örnek dosyada 52 katman açıkken tespit girdisindeki yay sayısı 2214'ten 133'e indi. Bu, dekoratif yayların elendiğini gösterir; kalan kapı/pencerelerin tamamının doğrulandığı anlamına gelmez. Oda sınırı ve güvenilir kat yüksekliği için §10'daki açık konular sürer.

12 mimari katmanla üretilen yerel inceleme modeli: `scratch/bina-layer-audit/beylerbeyi-katmanli-inceleme.e4bina.json`. Üç katın katman seçimi, tespit çizgileri, oda/açıklık adetleri ve açıklık kimlikleri kaydet/aç ile doğrulandı. Zemin 21, 1. kat 13, çatı 2 alan adayıdır; bunlar doğrulanmış mahal adetleri değildir. Çatı blokları gizlidir. Tam karşılaştırma `scratch/bina-layer-audit/report.txt` dosyasındadır; bu yerel kullanıcı verisi dağıtıma dahil edilmez.

## 13. Kapı sınırları ve kesitten kat yüksekliği — 2026-11-05

### Çift kanat kapı ve mahal sınırları

Örnek projede açık kapı kanatları da tespit çizgilerine dahildi. Tek yayın ucunu en yakın çizgiye göre seçmek, kapalı geçiş doğrultusu yerine açık kanadı kapı sayabiliyordu. `AciklikTespitServisi` artık karşılıklı menteşe/yarıçapları ve ortak uçları uyuşan iki yayı tekil eşleştirir; bu durumda kapalı uçları kullanır. Kopya/belirsiz yaylar eşleşme oluşturmaz. Eşit olmayan kanat, hafif eksen sapması ve döndürülmüş çift kapı desteklenir. Fiziksel kapı kanıtı olmayan açıklığa duvar eklenmez.

13 mimari katmanla örnek sonuçları:

- 1. kat: 154,5 m² birleşik sınır, yaklaşık 36,5 m² hol + 117,7 m² taziye oturma alanına ayrıldı; oda adayı 13 → 14.
- Zemin: 134,6 m² birleşik sınır, yaklaşık 115,5 m² taziye oturma alanı + 19,0 m² hole ayrıldı; oda adayı 21 → 22.
- Giriş/merdiven/kafeterya gibi açık bağlantılı kullanım alanları rastgele bölünmez. Bir bağlı hacimdeki kaynak adları `BinaOda.KaynakMahalAdlari` içinde kalıcıdır; seçili oda panelinde görünür.

### Kesit kanıtı ve kotlar

`MimariKatYukseklikServisi.Cozumle`, plan kotlarını kesit/görünüşteki tekrarlı kotlarla eşleştirir. Metin konumlarının Y−kot referansı en fazla 12 cm aralıkta kalmalıdır; üst üste CAD kopyaları bağımsız tekrar sayılmaz. Açık plan kotu + düşey koordinat uyumu gerekir. Kesit yoksa veya ölçek/kat/blok ilişkisi belirsizse değer üretilmez. Başka kesitte doğrulanan ara kat, seyrek etiketli kesit üzerinden atlanamaz. İsimli blokların kesit eşleşmesi kurulmadan yükleyicinin kot-farkı yolu da katlar arasında ölçü taşımaz.

Örnekte **zemin ana tabanı 0,00 m, 1. kat ana tabanı 4,00 m, çatı tabanı 8,00 m; iki kat aralığı 4,00 m**. Bunlar net oda yüksekliği değil, ana kat referanslarıdır. C-C kesitindeki +0/+4/+8 etiketlerinin düşey farkı yaklaşık 4,000/3,995 m; B-B ve A-A uygun seviyelerde ek kanıt sağlar. Kanıt metinleri ve koordinatları içe aktarma raporunda saklanır.

Önemli katman ayrıntısı: B-B/C-C kotları **YAZI TEF** katmanındadır. Önceki 12 katmanlı maskeye bu katman eklenmelidir. Katman önizlemesi kot/yükseklik yazısı sayılarını tooltip ile gösterir ve ölçü yazısı taşıyan kapalı katmanları bildirir. Gizli katmanlar üretime gizlice dahil edilmez. Alt kot kanıtı kapalıyken görünür +1 m yerel kot bütün katın tabanı yapılmaz.

### Yerel kot ve model uyumluluğu

Şema **4**: `BinaOda.TabanKotuOverrideM` mutlak oda taban kotudur; null ise kat tabanı geçerlidir. 3D döşeme/duvar/tavan ve açıklık çizgileri bu kotu kullanır. Oda eşleştirme, geri al/ileri al ve kaydet/aç alanı korur. Seçili oda panelinde taban kotu ve yükseklik düzenlenebilir; boş değer kat değerine döner, geçersiz giriş önceki değeri korur.

`YerelKotlariOku` oda içindeki çelişkisiz kotları aday olarak raporlar. Bir `+3.17` yazısı tek başına döşeme kanıtı değildir. Ana tabandan farklı değer, yalnız açık TABAN/DÖŞEME KOTU etiketi **ve** bağımsız net yükseklik bulunduğunda hacme otomatik uygulanır. Örneğin +1/+4,5 gibi kademeler ve +3,17 merdiven olasılığı, üst/net ölçü kesinleşmeden bütün hacme yayılmaz. Çatı tabanı çözüldü; çatı kabuğu/net yüksekliği üretilmedi ve çatı oda blokları gizli kalır.

Yeni yerel çıktı: `scratch/bina-layer-audit/beylerbeyi-kot-ve-oda-duzeltmesi.e4bina.json`. Görsel inceleme: `scratch/bina-evidence/after-1.png`, `after-2.png`; kaynakla karşılaştırma: `before-1.png`, `before-2.png`. Kaynak DWG/DXF değişmedi.

Doğrulama: katman 33, metin 67, açıklık yenileme 19, mevcut bina/VM 47, içe aktarma 9, kat 11, kalite 6, 3D 9, oda sınırı 11, yükseklik 25; **237 regresyon kontrolü**. Ayrıca **10 uçtan uca senaryo**, gerçek Beylerbeyi kaynağı dahil, kayıt/aç ve 3D tutarlılığını doğrular. Testler `tests/Bina*.Regression`; son kayıtlar `scratch/bina-import/oda-kot-*.txt`. Uygulama derlemesi 0 hata/0 uyarı. İzole güncel çalıştırılabilir: `scratch/bina-final-build/bin/Kst.Acoustics.App/debug/Kst.Acoustics.App.exe`.

## 14. DWG okurken kapanma düzeltmesi — 2026-11-24

`berecik.dwg` dönüşümü tamamlandıktan sonra `sta4cad App2` APPID kaydının kendisine bağlı XData'sı, netDxf `Insert.Explode → XData.Clone → ApplicationRegistry.Clone` zincirinde sonsuz özyinelemeye neden oluyordu. Windows hata kodu `0xc00000fd`; bu yığın taşması normal `catch` ile yakalanamaz.

`DxfKatPlaniParser` içindeki dört dosya okuma girişi ortak `GeometriBelgesiniOku` yöntemini kullanır. Bina geometrisi hesabında kullanılmayan APPID düzeyi XData yalnız bellekte temizlenir; nesne XData'sı, çizgi/metin, katmanlar ve kaynak CAD dosyası korunur.

Doğrulama: `BinaKatman.Regression` **49/49** geçti; yeni 16 kontrol kendine referans ve iki APPID arasındaki döngüleri, blok koordinatlarını, oda alanını, dört okuma yolunu ve kaynak dosyanın değişmediğini kapsar. Gerçek DWG dönüşüm/okuma testi 17 katman, 99.343 çizgi parçası ve 111 metinle tamamlandı. Varsayılan katman seçiminde otomatik kat ayırma güvenilir plan bulamadı (0 kat); bu sonuç kapanma düzeltmesinden ayrı bir anlamlandırma sınırıdır. Ekran üzerinden test, bilgisayar kullanımı aracının uygulama erişimine izin vermemesi nedeniyle yapılamadı. Kayıtlar: `scratch/berecik-repro/`.

## 15. Rapordan kaynak çizimi inceleme — 2026-11-24

İçe aktarma raporundaki **Çizimi incele** eskiden yalnız raporu kapatıyordu. Kat üretilemeyince geride boş bina ekranı kalıyordu. Artık bu düğme `MimariCizimIncelemeWindow` içinde seçili katmanların çizgi ve yazılarını gösterir; yakınlaştırma, kaydırma, ekrana sığdırma ve rapora dönüş desteklenir. Raporu kapatmak için ayrı **Kapat** düğmesi vardır.

Son içe aktarmanın çizimi ve raporu oturum belleğinde saklanır. Sıfır kat üretilse veya geçici DXF silinse bile **İçe Aktarma Raporu** yeniden açıldığında aynı kaynak çizim incelenebilir. Yeni bina veya başka bina dosyası açıldığında bu önbellek temizlenir. Kaydedilmiş binalarda ham veri yoksa seçili katın kayıtlı DXF çizgileri kullanılır. İncelenecek veri yoksa düğme devre dışıdır ve nedenini açıklayan metin görünür.

`BinaDrawingReview.Regression`: **24/24 kontrol** geçti. Avalonia Headless + Skia ile düğme olayları, gerçek önizleme penceresi, tekrar açma/kapama, kaynak dosyasız/sıfır odalı görünüm ve kaydedilmiş kat çizgileri doğrulandı. Gerçek `berecik` çizimi 17 katman, 99.343 çizgi parçası ve 111 yazıyla render edildi; görsel `outputs/drawing-review-tests/berecik.png`. Bu değişiklik otomatik kat ayırma kurallarını değiştirmez.

## 16. Windows katman önizlemesi donması — 2026-11-24

`berecik.dwg` içindeki 99.343 ayrık çizginin tek `StreamGeometry` içinde çizilmesi Windows'ta yerel Skia çizimini kilitliyordu. Gerçek Windows regresyonunda `READY` sonrasında arayüz durdu; yığın örneğinde render iş parçacığı `SKCanvas.DrawPath`, UI iş parçacığı `MediaContext.SyncWaitCompositorBatch` içinde bekliyordu. Headless bitmap testi bu yolu kapsamadığından önceki kontrolde görülmedi.

`MimariKatmanOnizlemeCanvas` şimdi çizgileri en fazla 256 parçalık geometri gruplarında tutar ve yalnız görünümle kesişen grupları çizer. Çizgi/metin verisi eksiltilmez; katman seçimi ve rapor önizlemesi aynı kontrolü kullanır.

Doğrulama: `BinaDrawingReview.Regression` **24/24**. Ek `--native <DXF>` modu üretim teması ve gerçek Windows compositor ile katman penceresini açar, 16 zoom olayı, üç tümünü kapat/aç çevrimi, yeniden boyutlandırma ve ekrana sığdırmayı çalıştırır; dispatcher nabzını ölçer. Düzeltme sonrasında test tamamlandı: 50 nabız, en büyük aralık **469 ms**. Donma öncesi yığın ve sonraki sonuçlar `outputs/native-preview-before.stack.txt`, `outputs/native-preview-after.log`; görüntü `outputs/drawing-review-tests/berecik.png`.
