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
| Durum | Geliştiriliyor; ana DSP/UI çekirdeği mevcut, 2026-07-18 20-bulgu düzeltme paketi tamamlandı |

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
- Gerçek kayıt/oynatma Windows (NAudio WASAPI) ve macOS (CoreAudio AudioQueue, saf P/Invoke) üzerinde desteklenir; Linux'ta desteklenmez.
- macOS'ta CoreAudio kuyruğu açılamazsa oynatma `afplay` fallback'ine düşer.
- macOS'ta `AudioQueueStop(immediate)` dolmamış son buffer'ı (~100 ms) teslim etmez; sweep sonrası 500 ms bekleme bu kaybı telafi eder.
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
- IR işlenmeden önce peak alignment yapılmalıdır — `ImpulseResponseWindow.AlignAndWindow` (Processing)
  peak'i bulur, ~5ms pre-roll ile kırpar (varsayılan 3 sn analiz penceresi), yalnız kuyruğa (son %15)
  raised-cosine söndürme uygular. Eski "tüm tampona Tukey" yaklaşımı peak tamponun başına yakınsa
  direkt sesi -30dB'den fazla zayıflatıyor, tam hizalıysa tamamen siliyordu — artık kullanılmıyor.
- Schroeder EDC hesaplarında gürültü tabanı ve kuyruk kırpma dikkate alınmalıdır.
- FFT dekonvolüsyonda circular aliasing oluşmaması için FFT boyutu `N + M - 1` tabanlı seçilmelidir.
- Biquad/IIR hesaplarında `NaN` üretimi engellenmelidir.
- `AcousticParameterExtractor` hesaplanamayan (yetersiz nokta, düz/artan eğim, enerji=0) parametre için
  `0` değil `double.NaN` döner; `0.00 s` gibi geçerli-görünen ama yanlış bir sonuç göstermek yerine UI/PDF
  "—" gösterir. UI/servis katmanında "değer yok" kontrolü **yalnız `double.IsNaN`** ile yapılır,
  `== 0` sentinel'i kullanılmaz (0 dB/0 s gerçek bir ölçüm sonucu olabilir).

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
- Windows: NAudio WASAPI capture/playback yolu
- macOS: **gerçek capture/playback CoreAudio AudioQueue ile desteklenir** — saf P/Invoke
  (`MacCoreAudioNative.cs`), harici bağımlılık yok. Cihaz listeleme CoreAudio HAL üzerinden
  (`MacAudioDeviceEnumerator`, Id = cihaz UID); kayıt `MacAudioCapture` (3×100 ms buffer,
  callback AudioQueue iç thread'inde), oynatma `MacAudioPlayback` (tüm sinyal baştan kuyruğa,
  IsRunning listener ile Task tamamlanır, süre+3 sn güvenlik ağı). Oynatmada afplay fallback korunur.
- macOS mikrofon izni: `AudioQueueStart` ilk çağrıda TCC iznini tetikler; bundle için `Info.plist` korunmalıdır
- Audio capture örnek listesi thread-safe olmalıdır (Windows/macOS callback'leri ortak `OnSamples` + lock yolundan geçer)

---

## 10. Kritik Dikkat Notları

- Log sweep dekonvolüsyonda circular aliasing tekrar oluşmamalıdır.
- Octave band filtre katsayıları Audio EQ Cookbook düzeltmeleriyle uyumlu kalmalıdır. `OctaveBandFilter`
  artık iki özdeş biquad'ı art arda uygular (4. derece); tek kademe komşu oktavdan aşırı sızıyordu,
  kademeli yapı merkez kazancını (0 dB) koruyarak komşu bant zayıflamasını ikiye katlar.
  RTA `HannCoherentGain` (0.5) ile pencere kazancını telafi eder (eskiden ~6dB düşük raporluyordu).
- Parametre hesabı peak-aligned IR üzerinden yapılmalıdır (bkz. §3, `ImpulseResponseWindow`).
- Schroeder kuyruğu gürültü tabanı nedeniyle yapay uzamamalıdır.
- THD/IMD sonuçları uygun test sinyali olmadan kesin gibi gösterilmemelidir. THD artık UI'dan da
  tetiklenebilir (`StartThdTestCommand`, tek sabit ton) — yalnız IMD değil.
- RTA canlı akışında cross-thread UI update hataları engellenmelidir.
- macOS CoreAudio P/Invoke delegate'leri statik alanda köklenmelidir (GC toplarsa native callback çöker).
- macOS'ta cihaz Id'si CoreAudio UID'dir; `default_in`/`default_out` sistem varsayılanı demektir, UID olarak kuyruğa verilmez.
- Ölçüm/simülasyon karşılaştırmasında toleransların birimi ve anlamı görünür olmalıdır.
- `FrequencyResponseAnalyzer` çıkışını `fft.Length`'e bölmez: `ir`, dekonvolüsyonda zaten `H(f)=Y(f)/X(f)`
  olarak normalize edilmiştir; Matlab FFT/IFFT çifti `Forward(Inverse(X))=X` sağladığından ek 1/N
  bölmesi ~100 dB'lik yapay zayıflatma yaratıyordu (EQ üretici de bu yüzden anlamsız +160dB kazançlı
  filtre üretiyordu — `EqGenerator` artık kazancı ±12dB ile sınırlar).
- `MeasurementRepository`, `float[,]` (Spectrogram) için özel `Float2DArrayJsonConverter` kullanır ve
  `IncludeFields=true` ayarlıdır (`RoomDimensions` bir `ValueTuple`, alanları property değil field'dır —
  STJ varsayılanında hem spectrogram'lı oturum kaydı `NotSupportedException` ile patlıyor hem de
  oda boyutları sessizce `(0,0,0)`'a düşüyordu).
- CSV (`ExportService`) ve PDF (`MeasurementReportPdf`) sayısal biçimlendirme `InvariantCulture`
  kullanır; tr-TR gibi virgül-ondalıklı kültürlerde biçimlendirme CSV alan ayracını bozuyordu.
- Pencere kapanırken (Geri/OS kapat) `vm.Dispose()` çağrılır ve `IsDirty` kirliyse kapanış onayı
  sorulur (maincodex §4.2 deseni; M08/M50/M51'deki gibi). Aksi halde RTA/kayıt açık kalıyor,
  kaydedilmemiş oturum sorulmadan kayboluyordu.

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
- `src/Kst.Acoustics.App/ViewModels/MeasurementViewModel.cs` (ana akış + capture/analiz burada)
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
- macOS CoreAudio cihaz listeleme, AudioQueue kayıt/oynatma (gerçek donanımda elle doğrulanır; birim testi yok)
- Ölçüm-simülasyon karşılaştırma toleransları
- PDF rapor üretimi (çok sayfalı — 60 satır/sayfa; eskiden yalnız ilk 2 ölçüm ve tek sayfa taşardı)
- IR peak alignment + kuyruk penceresi (`ImpulseResponseWindow`)
- FR mutlak seviyesi (fft.Length'e bölünmemeli)
- Oturum JSON round-trip: `RoomDimensions` (tuple) + `SpectrogramData.MagnitudeDb` (`float[,]`)
- NaN sentinel: başarısız RT/C50/D50/Ts hesabı `0` değil `NaN` döner, `== 0` sentinel kontrolü kullanılmaz

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
