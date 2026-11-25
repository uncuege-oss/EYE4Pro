# M51 — Salon Tasarla: inceleme ve yeniden tasarım önerisi

Tarih: 5 Kasım 2026. İlk incelemenin bulguları aşağıda tarihsel kayıt olarak korunmuştur. Kullanıcının “hepsini düzelt ve yap” talebi üzerine düzeltmeler ve ortak nesne modeline dayanan yeni editör uygulandı.

## Uygulama sonucu — 5 Kasım 2026

- **H01–H03:** Yeni/Aç/Şablon/Kapat ortak kayıt korumasını kullanıyor. Kayıt koleksiyonları değiştirmiyor. İçerik geçmişi dosya yolu/revizyondan ayrıldı; kaydedilmiş içeriğe geri/yinele ile dönülünce kirli işareti temizleniyor.
- **H04–H07:** Her kademe kendi kırpılmış alanıyla hesaplanıyor. Çakışmalar raporlanıp aktarımı durduruyor. Düz ve yerel eğimli tavanda çarpışma kontrolü var. Katıların yan/alt yüzeyleri, kabuğa bitişik yüzeyler ve tavan kot kırılımları ortak yüzey üretiminden geliyor.
- **H08–H09:** Sahne, seyirci, balkon, çukur, tavan ve sınırlar katman kimliği taşıyan ortak nesneler. Görünümde gizleme, “Hesaba / çıktıya dahil” seçeneğinden ayrı. Kategori adları çıktıya taşınıyor.
- **H10–H12:** Belge değişiminde bekleyen çizim/offset/sürükleme temizleniyor. İşaretli açı/mesafe ve sayısal kopyalama çalışıyor. Sıfır yükselme kabul ediliyor; geçersiz değerler alanda gösteriliyor.
- **H13–H14:** Eski uzun form ve araç şeridi kaldırıldı. Sarılan araç şeridi, nesne listesi, seçime bağlı özellikler, Plan/Kesit/3D sekmeleri ve canlı önizleme eklendi. Yakalama durumları alt şeritte görünür.
- **Serbest geometri:** Tek sınırda düz/yay kenarlar; kenar uzunluğu ve yay açısı; köşe ekleme/silme, pah/yuvarlama; çoklu alan birleştirme/çıkarma ve nesneye ait iç boşluklar. Daireler düzenlenebilir dört gerçek yay kenarına dönüşüyor. Kapı/pencerelerin kenar bağları taşıma, dönüş, kopya ve kenar bölmede korunuyor.
- **Salon bileşenleri:** Serbest çokgen sahne/balkon/seyirci; düz, basamak ve rampa; düz veya yaylı koltuk sıraları; koridorlara/sınıra kırpılan gerçek yerleşim; “Sahneye yönelt”; balkona bağlı seyirciyi birlikte taşıma/kopyalama. Boşluklu balkon ve çukur yüzeylerinde iç bölümleme çizgileri fazladan akustik yüzey oluşturmuyor.
- **Kesit:** İstenen doğrultuda kesit; tavan noktasını sürükleme veya sayısal mesafe/kot girme; bağımsız X/Y eğimli tavan bölgeleri. Farklı kesitlerden üretilen tavan bölgeleri birlikte korunuyor.
- **Uyumluluk:** Şema 2, eski dosyayı açarken ortak modele dönüşüm; aynı doğrulanmış modelden hacim, 3D, DXF ve M01.

### Kullanım

1. **Salon sınırı** ile noktaları koyun. **A** düz/yay değiştirir; yayda bitiş ve kavis noktası seçilir. **Enter** kapatır, **Backspace** son noktayı geri alır.
2. **Bölge** ile sahne, seyirci, balkon, çukur, boşluk, koridor veya tavan çizin. **Şekil** menüsünden dikdörtgen/daire seçilebilir.
3. **Seç / Düzenle** ile nesneyi seçin. Köşeleri sürükleyin; **Shift+kenar** paralel taşır, kenara çift tık köşe ekler. **Shift+tık** çoklu seçimdir. Ölçü/kot/yay açısı sağ paneldedir.
4. Seyircide zemin/sıra aralıklarını belirleyin; gerekiyorsa **Sahneye yönelt** ve **Yaylı koltuk sıraları** kullanın. Balkon üstü seyirciyi sağ panelden balkona bağlayın.
5. **Kapı / Pencere** ile duvara açıklık yerleştirin. **Kesit çiz** ile iki nokta seçin; kesitte turuncu tavan tutamaklarını düzenleyin. **Kontrol** listesindeki soruna tıklayınca nesne seçilip yakınlaştırılır.
6. **Ctrl+S** kaydeder. **DXF aktar** dışa aktarır; `.e4salon.json` dosyası M01’in **Salon Yükle** akışında açılır.

Trim/Uzat/Paralel kopya gelişmiş menüdedir. Trim, ortak modelin kapalı bölgelerini açık çizgilere parçalamaz; kapalı bölgelerde **Son seçileni çıkar** kullanılır. Görsel/DXF altlık ve yan yana çoklu görünüm, ilk önerideki isteğe bağlı genişletmelerdir; mevcut sürümde Plan/Kesit/3D sekmeleri kullanılır.

### Doğrulama ve örnek

- **Son sonuç: 82 başarılı, 0 başarısız. Derleme: 0 hata, 0 uyarı.**
- Kalıcı test çalıştırıcısı: `tests/SalonEditor.Regression/SalonEditor.Regression.csproj`. Eski 49 geometri testi, 24 yeni model/yüzey testi ve 9 editör/aktarım kontrolü.
- Derleme: `dotnet build tests/SalonEditor.Regression/SalonEditor.Regression.csproj --artifacts-path scratch/salon-audit/final-artifacts -p:NuGetAudit=false -p:UsedAvaloniaProducts= -v:q`.
- Test: `dotnet run --project tests/SalonEditor.Regression/SalonEditor.Regression.csproj --artifacts-path scratch/salon-audit/final-artifacts -p:NuGetAudit=false -p:UsedAvaloniaProducts=`.
- Canlı Windows kontrolü: yeni dosya açılışı, çizilen L planın 3D’si, örnek dosyayı açma, yaylı koltuk sıraları, 3D ve iki noktayla serbest kesit. 980 ve 1366 mantıksal piksel pencere yerleşimi otomatik kontrolde doğrulandı; ayrı yüksek DPI donanım testi yapılmadı.
- [Örnek salon](ornekler/serbest-L-salon.e4salon.json): eğri arka duvarlı L plan, sahne, koridor, yaylı ana seyirci, yan balkon, bağlı balkon seyircisi, yerel eğimli tavan ve kapı. Canlı sonuç: **423,16 m² · 3247,22 m³ · 264 koltuk · 0 uyarı**.

---

Aşağıdaki bölümler ilk inceleme anındaki davranışı ve gerekçeleri anlatır; güncel uygulama durumunu yukarıdaki bölüm açıklar.

## Sonuç

Mevcut çizim ve geometri servisleri yararlı bir başlangıç. Ancak arayüz genel amaçlı CAD araçlarına dayanırken salonun esas bileşenleri ayrı sayısal formlar üzerinden yönetiliyor. Serbest salon sınırı çizilebilmesi, serbest sahne/seyirci/balkon tasarlanabildiği anlamına gelmiyor. Öncelik yeni komut eklemekten önce veri güvenliği, ortak nesne düzenleme ve güvenilir geometri olmalı.

## İnceleme kapsamı ve kanıt

- Pencere, canvas, ViewModel, dosyalama, şablonlar, 2D–3D dönüşümü, hacim, doğrulama, seyirci/balkon ve DXF akışları kaynak koddan incelendi.
- Mevcut `SalonGeometriTests` sınıfındaki **49 test çalıştırıldı: 49 başarılı, 0 başarısız**. Bu, tüm uygulama test paketinin sonucu değildir.
- Uygulama ayrı çıktı klasörüne derlendi: **0 hata, 0 uyarı**. İlk denemede Avalonia telemetri görevi kullanıcı profilindeki günlük dosyasına yazamadı; yalnız bu derleme için `-p:UsedAvaloniaProducts=` verilerek telemetri görevi atlandı. Kaynak/paket ayarı değiştirilmedi. Komut: `dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj --artifacts-path scratch/salon-audit/artifacts -p:NuGetAudit=false -p:UsedAvaloniaProducts= -v:q`. Günlük: [app-build.txt](../../scratch/salon-audit/app-build.txt).
- Ayrı denetim programında sekiz senaryo çalıştırıldı. Sayısal sonuçlar aşağıda belirtildi. Bunlar mevcut davranışı gözleyen kontrollerdir; beklenen hataları başarı sayan regresyon testleri değildir.
- Canlı pencereyle fare/klavye testi yapılmadı. Arayüz yerleşimi ve olay akışı bulguları kaynak incelemesidir; farklı ekran boyutlarında görsel doğrulama ayrıca gereklidir.
- Tekrar çalıştırma: `dotnet run --project scratch/salon-audit/SalonAudit.csproj --artifacts-path scratch/salon-audit/artifacts -p:NuGetAudit=false`.
- Kontrol programı: [Program.cs](../../scratch/salon-audit/Program.cs). Sonuç: [results.txt](../../scratch/salon-audit/results.txt).

## Hata listesi

P1: yeniden tasarımın ilk aşamasında giderilmeli. P2: temel kullanım akışıyla birlikte giderilmeli. “Çalıştırıldı” bulguları denetim programında; “Kaynak” bulguları belirtilen kod akışında doğrulandı.

| No | Öncelik / kanıt | Hata ve yeniden üretim | Etki / gerekli düzeltme |
|---|---|---|---|
| H01 | P1 / Kaynak | Çizim yapıp **Yeni** seçildiğinde model ve geri alma geçmişi doğrudan sıfırlanıyor. **Aç** da mevcut kaydedilmemiş çalışmayı sormadan değiştiriyor. | Çalışma kaybı. Yeni/Aç/Şablon/Kapat için ortak Kaydet–Vazgeç–İptal akışı kullanılmalı. |
| H02 | P1 / Çalıştırıldı | `Kaydet → Normalize`, bütün koleksiyonları yeni koleksiyonlarla değiştiriyor; ViewModel ilgili liste değişikliklerini bildirmiyor. Kayıttan sonra seyirci ekleme kontrolünde eski bağlı listede **0**, modelde **1** öğe kaldı. | Kayıt sonrası listeler modelden kopuyor. Koleksiyon kimliğini koruyarak normalleştirme veya bütün bağları açıkça yenileme gerekli. |
| H03 | P1 / Çalıştırıldı | Geri alma görüntüsü dosya yolunu ve revizyonu da içeriyor. A dosyasından alınan görüntü, B’ye farklı kaydetmeden sonra yüklenince hedef **B/rev2 → A/rev1** oluyor. | Sonraki kayıt yanlış dosyaya yönelebilir; revizyon gerileyebilir. Belge içeriği geçmişi ile kayıt kimliği ayrılmalı. |
| H04 | P1 / Çalıştırıldı | Kabuğa kırpılan seyirci alanında toplam dolu hacim, tek bir plan alanı oranıyla çarpılıyor. 10×5 m kabuk, 10 m yükseklik; 10×10 m iki kademeli alanın yalnız 2 m yüksek arka yarısı içeride: **beklenen 400 m³, sonuç 450 m³**. | Her kademenin içeride kalan alanı kendi yüksekliğiyle hesaplanmalı. Düzensiz salonlarda doğrudan hesap hatası. |
| H05 | P1 / Çalıştırıldı | Aynı iki kademeli seyirci alanı iki kez eklenince aynı fiziksel yer için hacim **900 → 800 m³** oluyor; doğrulama **0 uyarı** veriyor. | Çakışan yüzey, iki kez hacim düşümü ve iki kez kapasite. Nesne çakışmasını saptama ve açık bir birleşim/engelleme kuralı gerekli. |
| H06 | P1 / Çalıştırıldı | 3 m düz tavan altında 4 m yüksek sahne için `Dogrula` **0 uyarı** veriyor. Platform kotu kontrolü yalnız profilli tavanda çalışıyor. | Düz tavanda da kesişim kontrol edilmeli; profilli tavanda küresel minimum yerine ilgili konumdaki tavan yüksekliği kullanılmalı. |
| H07 | P1 / Çalıştırıldı | Ortada duran iki kademeli seyirci katısı yalnız **iki döşeme + bir rıht** üretiyor; yan ve arka kapanış yok. Hacim servisi ise burayı dolu katı kabul ediyor. | Yüzey modeli ile hacim modeli uyuşmuyor. Açıkta kalan yan/arka yüzeyler ve yükseltilmiş alanın alt yüzeyi, taşıma biçimine göre üretilmeli. |
| H08 | P2 / Çalıştırıldı + Kaynak | Parametrik sahnenin katmanı `CustomStage` yapılıp gizlendiğinde **5 Sahne/Sahne-Yan yüzeyi** yine üretiliyor; yeni kategoriye geçen yüzey **0**. Seyirci/balkon kategorileri de servislerde sabit. | Katman adları ve görünürlük serbest çizimlerle parametrik nesnelerde tutarsız. Nesnelerin açık kategori/katman bağı olmalı. |
| H09 | P1 / Kaynak | Katman görünürlüğü doğrudan `Gorunur` alanına bağlı; değişiklikte snapshot/IsDirty izleme yok. Buna karşılık görünürlük serbest çizimlerin 3D/DXF çıktısına dahil edilmesini değiştiriyor. | Çıktıyı değiştiren işlem kaydedilmemiş değişiklik olarak izlenmiyor. Görünümde gizleme ve hesaba/çıktıya dahil etme ayrı seçenekler olmalı. |
| H10 | P2 / Kaynak | `Salon` değişince canvas sadece seçimi temizliyor; devam eden çizim noktaları ve offset hedefi temizlenmiyor. Çizimin ilk noktasından sonra Yeni/Aç yapılınca eski araç durumu taşınabilir. | Yeni belgeye eski başlangıç noktası/işlem hedefi sızması. Belge değişiminde tam araç durumu sıfırlanmalı. |
| H11 | P2 / Kaynak | Sayısal girişte `KeyToChar` eksi tuşunu kabul etmiyor. Döndürmede `-30`, çizgide `5<-30` yazılamıyor. Ayrıca sayıyla taşı/döndür akışı kopyalamayı sabit `false` geçiriyor. | Hassas ve hızlı çizim aksıyor. İşaretli açı, mesafe, koordinat ve kopya modu aynı giriş sistemiyle desteklenmeli. |
| H12 | P2 / Kaynak | Kademe yüksekliği ve sahne yüksekliği pozitif sayı okuyucusundan geçiyor; **0** girilince önceki/varsayılan değer kullanılıyor. | Düz seyirci alanı veya sıfır yükseltili sahne tanımlamak beklenmedik sonuç veriyor. Alan bazlı sınırlar ve görünür doğrulama mesajı gerekli. |
| H13 | P2 / Kaynak | Üst araç çubukları sarmalanmayan yatay `StackPanel`; pencere 980 px’e kadar küçülebiliyor, sol panel 300 px. Çok sayıdaki komut için taşma düzeni yok. | Dar pencerede komutların kesilme/örtüşme riski. Taşma menüsü, az sayıda ana araç ve seçime göre açılan işlemler gerekli. |
| H14 | P2 / Kaynak | 3D önizleme yüzeyleri yalnız pencere yüklenirken oluşturuluyor. Açık 3D penceresi sonradan yapılan çizim değişikliklerine abone değil. | 2D ile açık 3D önizleme farklı durumları gösterebilir. Canlı güncelleme veya açık bir “Güncel değil / Yenile” durumu gerekli. |

Kaynak eşleştirmesi:

- H01/H03/H09/H12: `src/Kst.Acoustics.App/Salon/Views/SalonTasarlaWindow.axaml.cs` — `Yeni_OnClick`, `DosyaAc`, `SnapshotAl`, `SalonuGeriYukle`, `SahnePozitif`, seyirci işlemleri. H09 için ayrıca AXAML katman görünürlük bağı.
- H02: `src/Kst.Acoustics.Core/Salon/Services/SalonGeometriServisi.cs` — `Kaydet`, `Normalize`; `SalonTasarlaViewModel` liste özellikleri ve bildirimleri.
- H04/H05: `SalonHacimServisi.Hesapla`, `SalonDogrulamaServisi.Dogrula`.
- H06: `SalonDogrulamaServisi.Dogrula` tavan/platform kontrolü.
- H07/H08: `SalonSeyirciServisi.Yuzeyler`, `SalonBalkonServisi.Yuzeyler`, `Salon3DGorunumServisi.YuzeyleriOlustur`.
- H10/H11: `SalonCizimCanvas.Salon`, `KeyToChar`, `SayiyiUygula`.
- H13: `SalonTasarlaWindow.axaml` üst dosya/araç şeritleri.
- H14: `Salon3DOnizlemeWindow.Yukle` ve pencere kurucusu.

## Hata olmayan, fakat hedef kullanımı engelleyen eksikler

1. **Düz çizgi + yay birleşik salon sınırı yok.** Kapalı polyline düz kenarlardan oluşuyor; yay ayrı öğe. Dört çizgiyle kapalı bir dikdörtgen denetiminde yalnız dört duvar üretildi, zemin/tavan üretilmedi. Çizgileri/yayları bir salon sınırına birleştiren kullanıcı işlemi yok.
2. **Sahne, seyirci ve balkon ortak seçim/dönüşüm sisteminde değil.** Canvas seçim, taşıma ve döndürme işlemleri `CizimOgeleri` üzerinde çalışıyor. Parametrik nesneler ayrı listeler ve merkez X/Y kutuları üzerinden düzenleniyor.
3. **Seyirci ve balkon planları dikdörtgen ve eksenlere bağlı.** Serbest çokgen, dönüş açısı, yay biçimli sıra, yan kanat, U biçimli balkon ve sahneye yönelen bloklar için veri modeli yetersiz.
4. **Tavan yalnız Y yönünde profil.** X boyunca değişen, yerel yükselen veya farklı yönlerde eğilen tavan bölgeleri temsil edilemiyor. Serbest planın kesiti de serbest olmalı.
5. **Kapasite yalnız alan tahmini.** `genişlik × derinlik / 0,5`; gerçek koltuk, koridor veya dış sınıra kırpılma hesaba katılmıyor. Denetimde 50 m²’si içeride kalan 100 m² alan için yine 200 koltuk bildirildi. Bu değer gerçek yerleşim kapasitesi olarak kullanılmamalı.
6. **Yardımcı çizgi kavramı eksik.** Her açık çizim öğesi 3D duvar üretiyor. Ölçü referansı/yardımcı çizgi açıkça hesap dışı olmalı.
7. **Form yükü yüksek.** Katman, sahne, çukur, seyirci, balkon, açıklık ve tavan aynı uzun sol panelde. Seçilen nesneye ait özellikler doğrudan gösterilmiyor.

## Önerilen kullanım yapısı

Ana akış: **Salon sınırını çiz → bölge ekle → kot/eğim ver → kapı/pencere yerleştir → kontrol et ve aktar.** Her aşamada önceki geometri düzenlenebilir kalır; zorunlu bir sihirbaz sırasına bağlanmaz.

### 1. Tek bir “Salon sınırı çiz” aracı

- Aynı işlem içinde düz kenar ve yay arasında geçiş; kenar uzunluğu, açı, yarıçap ve koordinat girişi.
- İlk noktaya yakında kapatma önerisi; son kenarı geri al; açık uçları işaretle; “Sınırı tamamla” işlemi.
- Kenarı tutup paralel taşıma, köşeyi sürükleme, kenara nokta ekleme, köşeyi yuvarlama/pah, girinti/çıkıntı ekleme.
- Birden çok alanı birleştirme ve alan çıkarma: L/U plan, asimetrik cep, kolon/şaft boşluğu ve yerel çıkıntı.
- Çakışma ve kendini kesme geri bildirimi çizim sırasında; sorunlu kenara yakın açıklama.
- İç sınırın neyi tanımladığı açık: varsayılan akustik iç yüzey. Duvar kalınlığı ayrı özellik olarak eklenebilir.

### 2. “Bölge ekle”: sahne, seyirci, balkon, çukur

- Kullanıcı alanı çizer veya kapalı bir çizimi seçip işlevini atar. Dikdörtgen, daire, oval ve yay dilimi hızlı başlangıç seçenekleridir.
- Aynı seç/taşı/döndür/kopyala/tutamak mantığı bütün nesnelere uygulanır.
- Seyirci alanında bakış yönü çizim üstündeki okla belirlenir. Sıralar düz veya yaylı; bölge sınırına ve koridorlara kırpılır.
- Basamak/rampa/düz zemin seçenekleri; kademe veya sıra aralığı, yükselme, başlangıç kotu; daha sonra gerçek koltuk yerleşimi.
- Balkon üstü seyirci balkona bağlı olur; balkon taşınınca birlikte gider. U biçimli ve kavisli balkon serbest sınır kullanır.
- Kapı/pencere duvar kenarına kimlik ve kenar üzerindeki mesafeyle bağlanır. Duvar hareketi/dönüşümüyle taşınır; bölme/birleştirme bağları açıkça günceller.

### 3. Plan, kesit ve 3D birlikte çalışmalı

- Ana görünüm plan; **Plan / Kesit / 3D** sekmeleri ve isteğe bağlı yan yana görünüm.
- Kullanıcı kesit çizgisini istediği yönde çizer; tavan, sahne ve seyirci kotunu bu kesitte düzenler.
- Tavan için düz/eğimli/kırıklı başlangıçları; gerektiğinde bölgelere ayırıp bağımsız kot/eğim verme.
- Kontrol paneli soruna tıklayınca ilgili nesneyi seçer ve görünümü oraya götürür. Kaynak kod terimleri yerine “Sınır açık”, “İki alan üst üste”, “Balkon tavanı aşıyor” gibi açıklamalar kullanılır.

### 4. Sade arayüz

- Üstte: **Dosya, Geri al, Yinele, Plan/Kesit/3D, Kontrol, Aktar**.
- Çizim araçları: **Seç/Düzenle, Salon sınırı, Bölge, Kapı/Pencere, Ölçü**.
- Sağda seçilen nesnenin özellikleri; seçim yokken salonun genel ölçüleri ve kotları.
- Altta kısa işlem yönergesi, ölçü girişi ve yakalama ayarları. Sayısal ölçü, ekrandaki kenara/nesneye bağlı görünür.
- Alan listesi sade bir nesne ağacı olur; katman ayrıntıları gelişmiş bölümde kalır.

## Komutları sadeleştirme kararı önerisi

| Mevcut komut/alan | Öneri |
|---|---|
| Çizgi, Polyline, Yay | Ana salon sınırı aracı içinde birleştir. Yardımcı çizgiyi ayrı, hesap dışı seçenek yap. |
| Dikdörtgen, Daire | Sınır/Bölge aracında hızlı şekil seçenekleri olarak tut. |
| Taşı | Seçip sürüklemenin doğal davranışı yap; hassas baz/hedef taşımasını bağlam menüsünde koru. |
| Döndür, Aynala, Kopyala | Seçime ait küçük işlem çubuğunda tut. Asimetrik plan ve tekrarlı localar için gerekli. |
| Sil aracı | Sürekli araç olmaktan çıkar; Delete ve bağlam menüsüne taşı. |
| Trim, Uzat, Offset | Ana şeritten çıkar; gelişmiş düzenleme altında koru. Sınır düzenleme içinde otomatik birleştirme/uzatma ve kenarı mesafeyle taşıma sun. |
| Snap, Grid, Orto, Polar | Alt şeritte “Yakalama” grubuna topla; kısayolları koru, adım/açı kullanıcı tarafından ayarlanabilsin. |
| Sığdır, Görünüm sıfırla | Tek görünüm menüsünde birleştir; Sığdır hızlı erişimde kalsın. |
| Katman ekle, renk kodu, katmana ata | Gelişmiş nesne/kategori yönetimine taşı; normal kullanıcı alanın işlevini seçsin. |
| Faset sayısı | Gelişmiş çıktı kalitesine taşı. Normal akışta eğriyi gerçek eğri olarak göster. |
| Sahne/Seyirci/Balkon için ayrı Ekle/Güncelle/Sil formları | Ortak nesne oluşturma ve seçilen nesnenin özellik paneliyle değiştir. |
| Tavan noktası Y/Kot listesi | Kesitte tutamak ve kot girişiyle değiştir; sayısal tablo gelişmiş seçenek olsun. |
| Yeni/Aç/Farklı Kaydet/DXF | Dosya ve Aktar menülerinde grupla; Kaydet kısayolunu koru. |

## Teknik uygulama sırası

1. **Güvenilir temel:** H01–H09; kayıt ve geri alma sınırları, koleksiyon bağları, çakışma kontrolleri, yüzey/hacim tutarlılığı. Eski dosyalar için geriye uyum testleri.
2. **Serbest geometri:** düz/yay kenarlı ortak sınır modeli; dış sınır + iç boşluklar; kalıcı kenar kimlikleri; ortak nesne seçimi/dönüşümü. Eski şekilleri bu modele dönüştüren şema migrasyonu. Hassas eğriler modelde korunur, 3D/çıktı için toleransa göre parçalanır.
3. **Yeni günlük kullanım:** sade araç şeridi, bağlamsal özellikler, kenar/köşe tutamakları, hızlı sayısal giriş ve canlı 3D.
4. **Salon bileşenleri:** serbest sahne/balkon, yönlendirilebilir seyirci blokları, koridorlar, kesitte kot/eğim, gerçek koltuk sayımı.
5. **Aktarım ve ileri özellikler:** aynı doğrulanmış geometriden 3D/hacim/DXF/M01 çıktısı; isteğe bağlı DXF veya ölçekli görsel altlık üzerinden çizim. Altlık ana salon geometrisinden ayrı tutulur.

## Kabul senaryoları

- Girintili L plan; düz ve yaylı kenarı birlikte olan asimetrik plan; U biçimli balkon; sahneye farklı açılarda bakan üç seyirci bloğu oluşturulabilmeli.
- Kenar ölçüsü değişince bağlı kapı yerini korumalı; balkon hareketinde bağlı seyirci birlikte gitmeli.
- Kaydet sonrası ekle/sil/güncelle listede anında görünmeli; Farklı Kaydet + Geri Al hedef dosyayı değiştirmemeli.
- Üst üste gelen bölgeler sessizce iki kez hesaplanmamalı; her kademe kırpıldıktan sonra hacme kendi kotuyla katılmalı.
- 2D, kesit, 3D ve çıktı aynı belge durumunu göstermeli; bozuk sınır kullanıcıya konumuyla açıklanmalı.
- 980 px, 1366 px ve yüksek DPI görünümde temel araçlar erişilebilir olmalı.
- Önerilen kullanılabilirlik hedefi: uygulamayı bilen bir kullanıcı ölçülü L planı, sahne ve bir seyirci bloğunu yaklaşık 5 dakika içinde hazırlayabilmeli. Bu bir test hedefidir; mevcut sürümde ölçülmüş süre değildir.
