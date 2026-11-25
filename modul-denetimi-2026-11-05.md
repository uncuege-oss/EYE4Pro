# EYE4Pro — sıralı modül denetimi ve düzeltmeler

Tarih: 5 Kasım 2026. Kapsam: M01'den başlayarak depoda tanımlı modüllerin kod, hesap/veri akışı, dosya işlemleri ve mevcut otomatik testlerinin incelenmesi. Kullanıcı isteği üzerine bulunan uygulanabilir hatalar doğrudan düzeltildi.

## Sonuç

**22 işlevsel hata grubu düzeltildi.** Ayrıca bir Windows test uyumsuzluğu ve modül envanterindeki durum bilgileri düzeltildi. Ortak kök nedenden birden fazla modül etkileniyorsa tek hata grubu olarak sayıldı.

28 kod içeren modül/çalışma alanı incelendi. M21, M38 ve M71 için çalıştırılabilir modül kodu bulunmadı; bunlar geliştirme eksikleridir. Numaralandırmadaki tanımlanmamış boşluklar çalışan modül olarak sayılmadı.

Bu inceleme mevcut testler ve kaynak kod üzerinde yapıldı. Her pencerenin her komutunun canlı kullanıcı arayüzüyle denenmesi, gerçek mikrofon/ses kartıyla ölçüm, harici CAD programlarında bütün dışa aktarımların açılması ve standartların bağımsız mühendislik doğrulaması kapsamda değildir. “Yeni bulgu yok” ifadesi, modülün olası bütün hatalardan arındığı anlamına gelmez.

Başlangıçta çalışma ağacında bulunan elektrik, keşif, salon ve arayüz değişiklikleri korundu. Önceki M51 yeniden tasarımındaki düzeltmeler bu raporun 22 hata grubuna dahil değildir.

## Düzeltilen hata listesi

| No | Modül | Öncelik | Hata ve yeniden üretim | Uygulanan düzeltme / doğrulama |
|---|---|---|---|---|
| A01 | M01 | Yüksek | 0–1 aralığında 0,6 adım seçilince `Round` nedeniyle 1,2 konumunda alan dışı alıcı oluşuyordu. | `ReceiverGrid.AxisPointCount` ile sınır içinde örnekleme; UI ve proje doğrulaması aynı hesapta birleşti. 0,3/0,1 kayan nokta uç durumu korundu. Eski yuvarlama ile kaydedilmiş haritalar okunabilir. |
| A02 | M02 | Yüksek | Üçte bir oktav emilim verisindeki NaN/Infinity karşılaştırmaları başarısız olduğu için veri yüksek `αw`/A sınıfı gibi değerlendirilebiliyordu. | Negatif ve sonlu olmayan katsayılar açık hata ile reddediliyor. Geçersiz veri testi eklendi. |
| A03 | M03 | Orta | Dışa aktarılan, tırnak/noktalı virgül/satır sonu içeren malzeme adı yeniden CSV'ye alındığında kayıt kayboluyordu. | CSV mantıksal kayıtlar üzerinden ayrıştırılıyor; tırnak içindeki satır sonları korunuyor; kapanmayan tırnak hata veriyor. Gidiş-dönüş testi eklendi. |
| A04 | M03, M07 | Yüksek | Duvar ve ölçüm dosyalarının mevcut hedefe doğrudan yazılması, yarım kalan kayıtta sağlam dosyanın kaybına yol açabilirdi. | Ortak atomik kayıt servisi ve önceki sürümü saklayan `.bak` kullanılıyor. Her iki modülde ikinci kayıttan sonra yedek içeriği ve yeni dosya geri okuması doğrulandı. |
| A05 | M04 | Düşük | Kaynak listesi dolu fakat bütün kaynaklar kapalıyken “Etkin kaynak bulunmuyor” uyarısı çıkmıyordu. | Liste adedi yerine etkin kaynak varlığı denetleniyor; kapalı kaynak testi eklendi. |
| A06 | M05 | Yüksek | Özel malzemenin sönümleme değeri NaN olduğunda darbe hesabı `IsValid=true` kalabiliyordu. | Malzemenin modül, sönümleme, yoğunluk ve emilim değerlerinin sonluluğu kontrol ediliyor; geçersiz sonuç açık durum mesajıyla dönüyor. |
| A07 | M06 | Yüksek | Çok büyük oda boyutları/çok küçük ses hızı, tamsayı dönüşümü ve sınırsız üçlü döngü nedeniyle hesabı kilitleyebiliyordu. | İndekslere dönüşmeden hacim ve aday sayısı kontrolü; 2 milyon aday sınırında uyarılı boş sonuç. Aşırı büyük girişte eski test çalışması durdurulmak zorunda kaldı; düzeltme sonrası aynı giriş hızla reddediliyor. |
| A08 | M07 | Yüksek | Farklı uzunlukta ölçüm matrisi satırları sessizce kırpılıyor veya sıfırlarla dolduruluyor; null satır genel çökme üretiyordu. | Dikdörtgen olmayan/null satırlı matris `JsonException` ile reddediliyor. Geçerli matrisin gidiş-dönüşü ve üç bozuk biçim test edildi. |
| A09 | M11 | Yüksek | 900 A linye için üst sınır nedeniyle 630 A koruma seçiliyordu. | Otomatik linye koruma listesi uygulamadaki büyük akım cihaz aralığına genişletildi; 900 A→1000 A ve 6000 A→6300 A testleri eklendi. Bu değişiklik fiziksel tesisat onayı değildir; katalog üst sınırı 6300 A'dır. |
| A10 | M12, M16 | Orta | Yalnız mahal satırı, kablo tavası, besleme hattı veya topraklama içeren karşı disiplin verisi sayımda görünmüyor; açılışta disiplin ayıklaması atlanabiliyordu. | Ek içerik sayımı eklendi. Geometri korunarak mevcut ayıklama akışının tetiklenmesi sağlandı; yalnız topraklama içeren proje testi eklendi. |
| A11 | M13 | Yüksek | 48 V sistemde dört adet 12 V/7 Ah akü iki paralel çift sanılıp 14 Ah gösteriliyordu. | Seri akü sayısı nominal gerilimden hesaplanıyor; yalnız tamamlanmış seri diziler kapasiteye katılıyor; eksik dizi uyarısı gerilime uygun. Aynı örnek artık 7 Ah. |
| A12 | M15 | Yüksek | NaN/Infinity direk aralığı sahte tek direk üretiyor; aşırı küçük aralık çok büyük yerleşim döngüsü doğurabiliyordu. | Aralık/koordinat/ofset sonluluk kontrolü ve 100 bin yerleşim aralığı sınırı. Geçersiz yerleşim boş sonuç döndürüyor. |
| A13 | M15 | Yüksek | Negatif yayılma direnci RCD kontrolünden “uygun” geçiyor; negatif özdirenç hesapta kabul ediliyordu. | Fiziksel girişlerin pozitif/sonlu olması ve logaritmik modelin pozitif direnç alanında kalması denetleniyor. Negatif/sonsuz direnç uygunluk veremiyor. Formül ve mevzuat eşikleri değiştirilmedi. |
| A14 | M30, M50, M60, M70 | Yüksek | Gelecek şema sürümündeki dosyalar destekleniyormuş gibi açılıyor; sonraki kayıtta bilinmeyen alanların kaybı mümkün oluyordu. | Desteklenenden yeni şemalar yüklemede ve proje kaydında açık hata ile reddediliyor; eski şema göçleri korunuyor. Dört servis için test var. |
| A15 | M30, M60, M70 | Yüksek | Başarısız kayıtta mimari dosya yolu/zamanı ve arazi/park revizyonu bellekte değişmiş kalıyordu. | Kayıt metaverisi hata durumunda geri alınıyor; revizyon artışı taşmaya karşı denetleniyor. Dosya yerine klasöre kayıt denemesiyle doğrulandı. |
| A16 | M30, M50, M51, M60, M70 | Yüksek | Bazı servisler doğrudan dosyaya yazıyor; M50/M51 sabit `.tmp` adı nedeniyle aynı adlı dosyaya çarpabiliyordu. | Benzersiz geçici dosya, atomik değiştirme, önceki sağlam sürüm yedeği ve hata temizliği ortak servise bağlandı. Beş modülde ikinci kayıt/yedek ve önceden var olan `.tmp` içeriğinin korunması test edildi. M51'in hedef klasörün var olmasını isteyen mevcut davranışı korundu. |
| A17 | M31/M34 | Yüksek | NaN kat yüksekliği, alan veya oranlar `<=0` karşılaştırmalarından kaçıp temel doğrulamada geçerli kalabiliyordu. | Program, mahal ve şablonların sayısal doğrulamasına sonluluk kontrolleri ve anlaşılır hata metinleri eklendi. Geçerli proje→NaN yükseklik karşılaştırma testi eklendi. |
| A18 | M60 | Yüksek | NaN/Infinity/aşırı küçük grid adımı boş veya anlamsız hafriyat sonucu verebiliyor; kayan noktalı artış çok büyük koordinatta ilerlemeyebiliyordu. | Sonlu/temsil edilebilir hücre alanı, bölge koordinatı/hedef kotu, hücre sayısı ve koordinat hassasiyeti kontrolü; tamsayı indeksli tarama ve 2 milyon hücre sınırı. Mevcut analitik yarma/dolgu testleri korundu. |
| A19 | M61 | Yüksek | Bina listesi yeniden sıralanınca kesit kotu başka bloğun ayak iziyle eşleşiyordu. | Liste indeksi yerine kararlı `BlokId` eşlemesi. Ters sıradaki iki ayak iziyle kesit testi eklendi. |
| A20 | M70 | Orta | Bina dışlaması olmayan park saat yönünde çizildiğinde dış sınır delik sanılıyor; içerideki noktalar reddediliyordu. | Dönen kullanılabilir dış halka yönü normalize edildi; kaynak park geometrisi değiştirilmiyor. Saat yönü testi eklendi. |
| A21 | M70 | Yüksek | Köşeleri kullanılabilir alanda kalan döşeme, ortadaki bina dışlama alanını tamamen örtse de geçerli sayılıyordu. | Köşe kontrolüne ek tam poligon fark alanı kontrolü. Deliği örten ve deliğin yanında kalan iki durum test edildi. |
| A22 | M70 | Orta | Yürüyüş yolunun yalnız ekseni kontrol ediliyor; yol genişliğinin park dışına taşması kaçırılıyordu. | Yol ekseninden gerçek genişlikte ayak izi üretilip kullanılabilir alanla karşılaştırılıyor. Sınır kenarındaki taşan ve içteki geçerli yol test edildi; boş/geçersiz alan uyarısı eklendi. |

## Test ve belge düzeltmeleri

| No | Kapsam | Düzeltme |
|---|---|---|
| T01 | M08 test altyapısı | Klasörü dosya gibi yazma denemesinde yalnız `IOException` bekleniyordu. Windows'un verdiği `UnauthorizedAccessException` da beklenen dosya erişim hatası olarak kabul edildi. Üretim kodunda yazma hatasını yutma davranışı eklenmedi. |
| D01 | Modül envanteri | M15'in “planlama” etiketi mevcut Core/ilk UI durumuyla düzeltildi. M21 belgesindeki hedef kapsamın uygulanmış gibi okunmasını önleyen durum notu eklendi. |

## Modüllerin sırayla kontrol kapsamı

| Modül | İncelenen alan | Bu turdaki sonuç |
|---|---|---|
| M01 | Proje kaydı/yedek/göç, birim ölçekleme, simülasyon ön kontrolü, alıcı ızgarası, mevcut ışın izleme testleri | A01 |
| M02 | TMM bant/NRC/αw hesap akışı ve veri sınırları | A02 |
| M03 | Duvar/kütüphane dosyaları, CSV gidiş-dönüş, mevcut yalıtım hesap testleri | A03, A04 |
| M04 | Kaynak-bariyer-alıcı hesabı, etkinlik ve uyarılar, mevcut kırınım testleri | A05 |
| M05 | Darbe dinamiği, malzeme/katman doğrulaması, HIC ve ses emici yüzey testleri | A06 |
| M06 | Oda modu indeksleri, girişler, hesap maliyeti; mevcut oran/difüzör testleri | A07 |
| M07 | Ölçüm oturumu kaydı ve sayısal matrisler; mevcut DSP/oturum testleri | A04, A08 |
| M08 | Kütüphane yazma hataları ve mevcut hardening/hesap/rapor regresyonları | T01; bu kapsamda yeni üretim kodu bulgusu yok |
| M11 | Linye/pano, mevcut kablo/UPS/OG/busbar regresyonları | A09 |
| M12 | Disiplin ayıklama, proje yükleme; mevcut kablolama, undo ve aktarım testleri | A10 |
| M13 | SVG/şema akışı, analog anons güç/akü; mevcut ELV testleri | A11 |
| M14 | Keşif JSON/parmak izi, mevcut eşleştirme, miktar ve toplu poz testleri | Bu kapsamda yeni bulgu yok; başlangıçtaki düzeltmeler korundu |
| M15 | Direk yerleşimi, topraklama, mevcut fotometri/aydınlatma/tek hat gidiş-dönüş testleri | A12, A13, D01 |
| M16 | M12 ortak motoru ve karşı disiplin temizliği | A10 |
| M21 | Başlatıcı ve belge/kaynak envanteri | Hesap motoru, model ve kayıt akışı depoda yok; D01 |
| M30 | Ortak mimari proje kaydet/aç/göç | A14, A15, A16 |
| M31 | İhtiyaç programı/alan özeti/mahal-şablon doğrulaması | A17; mevcut üretim testleri geçti |
| M32 | Plan üretimi/kat dağıtımı ve mevcut yerleşim/koridor testleri | Bu kapsamda yeni bulgu yok |
| M33 | Çekirdek/şaft sürekliliği ve mevcut kaçış grafı testleri | Bu kapsamda yeni bulgu yok |
| M34 | Temel sayısal veri doğrulaması ve mevcut ön kontrol testleri | A17 |
| M35 | 3D kat/mahal/duvar üretim yolu, mevcut alan/hacim/ihracat testleri | Bu kapsamda yeni bulgu yok |
| M36 | Yapı elemanı performans özeti/finisaj ve mevcut malzeme testleri | Bu kapsamda yeni bulgu yok |
| M37 | CSV/HTML/metin biçimlendirme ve mevcut rapor testleri | Bu kapsamda yeni bulgu yok |
| M38 | Mimari ana plandaki durum ve kaynak envanteri | Planlı; uygulama kodu/modül codex'i yok |
| M39 | Revizyon karşılaştırma/koordinasyon ve mevcut M50 aktarım testleri | Bu kapsamda yeni bulgu yok |
| M50 | Bina proje kaydı/göç ve mevcut geometri/kat/açıklık regresyonları | A14, A16 |
| M51 | Önceki yeniden tasarımın testleri, geometri kaydı ve ortak poligon değişikliğinin etkisi | A16; önceki tasarım değişiklikleri yeniden sayılmadı |
| M60 | Arazi kaydı/göç, grid hafriyat ve mevcut TIN/analitik hacim testleri | A14, A15, A16, A18 |
| M61 | Temel oturtma, kesit eşleme ve mevcut kot/adapter testleri | A19 |
| M70 | Park kaydı, halka yönü, dışlama ve tam alan/şerit yerleşimi; mevcut alan/oran testleri | A14, A15, A16, A20–A22 |
| M71 | Belge ve kaynak envanteri | Planlı; bitkilendirme uygulama kodu yok |
| Metraj | Ayrı uygulamanın mevcut 38 xUnit testi | Tamamı geçti; bu kapsamda yeni bulgu yok |

## Doğrulama

Başlangıç: çözüm derlemesi 0 uyarı/0 hata; genel koşucu 759 başarılı/1 başarısız. Tek başlangıç başarısızlığı T01 idi. Mevcut testlerde görünmeyen sınır durumları için **24 yeni regresyon senaryosu** eklendi.

| Kontrol | Sonuç |
|---|---|
| Tüm çözüm derlemesi | 0 uyarı, 0 hata |
| Genel özel test koşucusu | 784/784 başarılı |
| Core mimari/arazi/park koşucusu | 121/121 başarılı |
| Ana proje xUnit testleri | 72/72 başarılı, 0 atlanan |
| Metraj xUnit testleri | 38/38 başarılı, 0 atlanan |
| M51 özel geometri/editör koşucusu | 82/82 başarılı |

Paketler arasında örtüşen testler vardır; bu sayılar benzersiz test toplamı olarak toplanmamalıdır.

Yeni testler `ModuleAuditRegressionTests.cs` ve `ModuleAuditGeometryTests.cs` içinde. Yalnız denetim regresyonlarını çalıştırmak için:

```powershell
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj --artifacts-path scratch/module-audit/artifacts -p:NuGetAudit=false -p:UsedAvaloniaProducts= -- --module-audit
```

Tam test/derleme günlükleri ve TRX dosyaları `scratch/module-audit` altında. Başlangıç çalışma ağacının yaması ve durum listesi de aynı klasörde saklandı. Derleme çıktıları ayrı klasörde üretildi.

## Kalan geliştirme kapsamı ve sınırlar

- **M21, M38, M71:** çalıştırılabilir modül kodu bulunmadığından çalışma zamanı denetimi yapılamadı. Bunlar otomatik hata düzeltmesinden ayrı yeni modül geliştirmeleridir.
- **M15:** Core ve ilk UI akışı mevcut; codex'te belirtilen ayrıntılı çizim/zoom/pan ve özellik panelleri ürün geliştirme kapsamındadır.
- **M70:** `ParkOranServisi` mevcut tasarım gereği örtüşen sert zemin alanlarını toplamaktadır; alan birleşimi codex'te ileri fazdır. Bu turdaki tam ayak izi doğrulaması yerleşimin geçerliliğini düzeltir; alan raporundaki bu model sınırını değiştirmez.
- **M51:** önceki raporda kayıtlı isteğe bağlı resim/DXF altlık ve eşzamanlı çoklu görünüm geliştirmeleri ayrı kapsam olarak durur.
- Otomatik doğrulama tamamlandı. Canlı kullanıcı arayüzü ve gerçek proje dosyalarıyla kabul kontrolü ayrıca yapılabilir; burada yapılmış gibi raporlanmadı.
