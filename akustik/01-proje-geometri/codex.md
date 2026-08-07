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

### 8.1 Hesaplama Motoru (`Simulation/RayTracingService` + `AcousticParameterCalculator`)

Bilinçli tasarım kararları (gelecekte "hata" diye değiştirme — önce ürün kararı):
- **Direkt ses stokastik yakalanır** (deterministik ISM yalnız bounce ≥ 1). `RayTracingValidationTests.FreeFieldDirectSoundFollowsInverseSquare` bu istatistiği doğrular. Deterministik direkt yol = mimari değişiklik (2026-06-22 stokastik bırakıldı).
- Sonuçlar **birim-değişmezdir** (ses hızı da proje biriminde): metre/mm/inch aynı RT/C80/STI verir.

2026-06-22 hesaplama motoru incelemesi düzeltmeleri:
- **Reverb kuyruğu** ışın başına değil, kaynak başına 2.5 ms histogramda toplanıp yayılır (`EmitReverbTail`) → geliş sayısı ~%99.5 azalır; RT/C80/STI korunur.
- **Yönlülük** analitik pattern'leri güç-normalize (cardioid/figure8 ×3, supercardioid ×3.732) → toplam güç omni ile aynı.
- **Emici yüzey** katman birleştirme union değil "baskın + azalan artık katkı"; hava boşluğu λ/4 bonusu panel varken uygulanmaz.
- **Birim ölçeği**: geometri epsilonları, kuyruk `recFactor` ve G referansı (`SimulationParameters.UnitScale`) metreye çevrilir → mm/inch projelerde RT/G doğru.
- **Izgara haritası** tüm kaynakları toplar (eskiden yalnız ilk kaynak).
- Ayrıntı: hafıza `m01-hesaplama-motoru-incelemesi`.

### 8.2 2026-07-15 güvenilirlik incelemesi düzeltmeleri (Faz 1-3 tamamlandı)

Kapsamlı bir M01 incelemesi sonrası 12 madde düzeltildi (13. madde bu doğrulama):

**Faz 1 — Sonuç doğruluğu:**
- `BuildSimulationProject` artık güncel `SourcePoints`/`ReceiverPoints`'i simülasyona aktarır (eskiden `_project.Sources/Receivers` — UI'da eklenen/silinen nokta simülasyona ulaşmıyordu).
- Birim dönüşümü yüzey/kaynak/alıcı/panel/ızgara koordinatlarını birlikte dönüştürür; fiziksel SI oda hacmi m³ olarak sabit kalır. Yalnız gerçek fiziksel özel ölçek hacmi `factor³` ile değiştirir.
- DXF hacmi artık `RoomVolumeCalculator` ile hesaplanır: kabuk kapalıysa (watertight — her kenar tam 2 yüzey tarafından paylaşılıyor) gerçek imzalı hacim, değilse açıkça işaretli bounding-box tahmini. `ProjectWindowViewModel.RoomVolumeSourceText` kaynağı UI'da gösterir.
- Kaydedilmemiş değişiklik koruması: `UnsavedChangesGate` + `UnsavedChangesDialog` (Kaydet/Kaydetmeden Çık/İptal) — proje açma, yeni içe aktarma, ana ekrana dönme, pencere kapatma (`OnClosing`) hepsi aynı akıştan geçer.
- `IsDirty` artık kategori malzemesi, elle girilen oda hacmi, simülasyon tamamlanması ve `SelectedRoomType` (proje yüklüyken) değişikliklerini de yakalar.

**Faz 2 — Dosya güvenilirliği:**
- `AcousticsProjectMigrator`/`AcousticsProjectValidator` (Core/Projects): ileri şema sürümü reddedilir, yinelenen yüzey/kaynak/alıcı ID'si, üçten az köşeli yüzey, NaN/Infinity koordinat doğrulama hatası fırlatır. `ProjectFileService.LoadAsync` bunu otomatik uygular.
- Kaydetme hata yönetimi: `ProjectFileService.SaveAsync` hedefle aynı dizinde benzersiz `.tmp` üretir, diske flush eder, snapshot'ı yeniden yükleyip doğrular ve atomik `File.Replace`/rename ile terfi ettirir. Hata halinde geçici dosya temizlenir; ViewModel'ler Türkçe mesaj gösterir ve `IsDirty` durumunu korur.
- `ProjectWindow` proje açma artık kurtarma durumunu da döndüren `LoadWithStatusAsync` kullanır + çift tıklama koruması.
- Proje açma hatası kullanıcıya sade Türkçe mesaj gösterir; stack trace yalnız konsol/debug log'a gider.

**Faz 3 — DXF profesyonelleşme:**
- Coplanar yüzey birleştirme (`DxfSurfaceParser.TraceBoundaryPolygons`) artık birim-duyarlı tolerans kullanır (~1mm gerçek dünya, eskiden sabit 3 ondalık — mm projesinde 1 mikron). Sınır grafiği adjacency list'e taşındı; T-junction'da (bir köşeden birden fazla sınır kenarı çıktığında) doğru devam kenarı düzlemsel açı kuralıyla (`PickNextBoundaryEdge`) seçilir.
- İçe aktarma denetim raporu: `MainWindowViewModel.ImportWarnings`/`ImportWarningsSummary` DXF parser uyarılarını (ör. bulge→çizgi dönüşümü) kalıcı bir panelde gösterir.
- Yüzeye snap offset'i (`GetSelectedSurfaceSnapPoint`) artık ~0.1m fiziksel hedefi proje birimine çevirir (eskiden sabit 0.1 "birim" — mm projesinde 0.1mm'ye denk gelirdi).

Kapsam dışı bırakılanlar (bilinçli, gelecekte ele alınabilir): DXF adjacency list mid-edge T-junction'ı (bir yüzeyin kenarı başka bir yüzeyin köşesinden geçen ama köşesi olmayan durum) çözmez, yalnız gerçek ortak köşe branching'ini çözer.

### 8.3 2026-08-07 bütüncül güvenilirlik ve kullanılabilirlik paketi

**Belge yaşam döngüsü ve dosya güvenliği:**
- Ana pencere ve proje penceresi aynı `UnsavedChangesGate` sonucunu kullanır: Kaydet, Kaydetmeden Çık ve İptal birbirinden ayrıdır; kapanış/yeni/aç/import/geri akışlarında çift tetikleme koruması vardır.
- Kaydetme revizyon duyarlıdır: kayıt sürerken yeni değişiklik yapılırsa disk snapshot'ı başarılı olsa da belge yanlışlıkla temiz sayılmaz. Proje penceresi ana pencereye yalnız diske gerçekten yazılan snapshot'ı yayınlar.
- Bozuk primary dosya varsa `Load`/`LoadAsync` sessizce yedeğe düşmez. Kullanıcı akışları `LoadWithStatusAsync` ile sağlam `.bak` kurtarmasını açıkça bildirir; oturum uyarısı proje JSON'una karışmaz ve kurtarılan belge onarım kaydı yapılana kadar kirli kalır.
- Kurtarma sonrası kayıt sağlam `.bak` dosyasını bozuk primary ile ezmez. Bu davranış yalnız recovery path'ine açıkça bağlanır; normal overwrite/Save As mevcut hedef geçersiz veya daha yeni şemalı olsa bile önce onu `.bak` olarak korur.
- Şema sürümü 2; hacim kaynağı (`VolumeOrigin`), kalıcı import uyarıları, simülasyon ayarları ve yeniden açılabilir sonuç özeti saklanır. Geçersiz/null koleksiyonlar, duplicate ID, sonlu olmayan koordinat/ayar/sonuç verisi kayıt ve açmada reddedilir.

**Geometri, birim ve nokta güvenliği:**
- DXF parser yalnız `ENTITIES` bölümünü işler; desteklenmeyen `INSERT` için görünür uyarı verir. Birimsiz DXF'te fiziksel tolerans gerektiren yüzey birleştirme kullanıcı birimi doğrulanana kadar ertelenir.
- Delikler NaN ayırıcılarla temsil edilmez. Kapalı kabuk hacmi bağlantılı bileşenleri ve karışık winding'i yönlendirerek hesaplanır; açık kabuk açıkça bounding-box tahmini olarak işaretlenir.
- Tüm düzlem/snap toleransları fiziksel metre sözleşmesine bağlanır. Snap sonucu oda içinde tutulur; nokta metinleri `"R"` biçimiyle hassasiyet kaybetmeden round-trip olur ve NaN/Infinity düzenlemeleri kayıt/simülasyonu engeller.
- Birim/özel ölçek dönüşümü aday proje üzerinde atomik hazırlanır. Yüzey, nokta, panel, ızgara veya hacimde taşma/alt-taşma olursa canlı belge, birim ve `IsDirty` aynen kalır; aşırı ölçek katsayıları reddedilir.

**Simülasyon ve panel tutarlılığı:**
- Simülasyon immutable çalışma snapshot'ı ve iptal token'ı kullanır; ayarlar, grid maliyeti, oda hacmi, nokta geçerliliği ve fiziksel receiver yarıçapı koşudan önce doğrulanır. Eşzamanlı düzenleme sonucu yanlış belgeye uygulanmaz.
- Sonuç girdileri geometri, materyal, hoparlör, kaynak/alıcı, panel ve grid ayarlarını içeren parmak iziyle bağlanır. Girdi değişince sonuç klonlanarak eski işaretlenir; parent baseline veya paylaşılan sonuç nesnesi mutasyona uğramaz.
- Ham arrival listeleri dosyaya şişirilmez; `StoredArrivalCount` ve `ComputedParameters` saklanır. Yeniden açılan rapor/karşılaştırma çalışır, ham IR gerektiren auralizasyon ise nedenini açıklayarak devre dışıdır.
- Panel yerleşimi başlangıçtaki proje yüzeyleri yerine güncel `_workingSurfaces` host geometrisini kullanır; dönüşüm/silme sonrası panel üretimi ve senkronizasyonu güncel yüzeylere bağlıdır. Birim dönüşümü panel koordinatlarını dönüştürür, metre cinsinden fiziksel panel ölçülerini korur.

**Kullanılabilirlik:**
- Ana ve proje pencereleri dar ekranlarda kaydırılabilir/esnek yerleşime geçti; açık tema kontrastı, Türkçe görünüm adları, kalıcı doğrulama/import uyarı bantları ve işlem durumları görünür hale getirildi.
- Sıfır veya bilinmeyen hacim artık sessizce `100 m³` yapılmaz; kullanıcıya kaynağı gösterilir ve simülasyon öncesi gerçek değer istenir.
- Ölçüm karşılaştırması ve ImportPreview yedekten kurtarmayı görünür biçimde taşır; eski veya parmak izi uyuşmayan simülasyon sonucu ölçümle karşılaştırılmaz.

---

## 9. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen durum (2026-08-07):
- Release build: Core + App + Tests 0 hata / 0 uyarı.
- Standart M01 test keşfi: 17/17 geçer.
- Genel özel koşucu: 699/699 geçer; M01/M51 ve diğer modüllerde regresyon yoktur.
- ImportPreview Release build: 0 hata / 0 uyarı.

---

## 10. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. İş yapılacak alana göre ilgili Core/App dosyalarını incele.
4. Değişiklikleri mümkün olduğunca dar kapsamlı tut.
5. Görünür UI metinlerinde Türkçe karakter kontrolü yap.
6. Uygunsa build ve ilgili testleri çalıştır.
