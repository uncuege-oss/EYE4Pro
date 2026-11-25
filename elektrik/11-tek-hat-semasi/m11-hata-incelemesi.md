# M11 hata incelemesi ve düzeltme kaydı — 7 Eylül 2026

Kapsam: M11 hesap motoru, ViewModel, pano düzenleme ve proje akışları, Excel aktarımı ve DXF çıktısı. `maincodex.md` ve `codex11.md` esas alındı. Aşağıdaki 15 bulgunun tamamı üretim kodunda düzeltildi ve regresyon testleri eklendi. İnceleme kaynak kod takibi ve bağımsız çalıştırılan hesap/aktarım senaryolarıyla yapıldı; canlı masaüstü tıklama testi yapılmadı.

P1: yanlış boyutlandırma, veri kaybı veya uygulamanın kilitlenmesi. P2: işlev/çıktı tutarsızlığı ve kullanıcı seçiminin kaybı.

## Doğrulama

- `dotnet build src/Kst.Acoustics.slnx --no-restore -v minimal -p:UsedAvaloniaProducts=`: **0 hata, 0 uyarı**. Son parametre, sandbox içinde Avalonia telemetrisinin kullanıcı profiline günlük yazma hatasını önlemek için kullanıldı.
- Güncel özel test koşucusu: eklenen **13/13 M11 regresyonu geçti**. Tam pakette yalnız M11 dışındaki malzeme kütüphanesi erişim testi başarısız.
- Boş Excel aktarımı, linye kopyalama, busbar döngüsü, pano adı ve tür korumaları, monofaze/yüksek yük, busbar kompanzasyonu ve diversitesi, UPS seçimi/girişi, OG önizleme, DXF UPS topolojisi ve Excel ondalık ayracı regresyonlarla doğrulandı.

## Bulgular

### 1. [P1] Dengesiz monofaze yükte pano beslemesi düşük seçiliyor

Tek 10 kW monofaze linyede R fazı **51,151 A** iken pano giriş akımı **16,981 A**, giriş sigortası **20 A**, besleme kablosu **4x2,5 mm²** ve yeterlilik `true` çıkıyor. Faz dağılımı hesaplanmasına rağmen besleme akımı toplam görünür gücün dengeli üç faza bölünmesiyle bulunuyor. Gerçek en yüklü faz besleme boyutlandırmasına yansımıyor.

Kaynak: [PanoHesapServisi.cs:65](/C:/dev/EYE4Pro/src/Kst.Acoustics.Core/Electric/Calculation/PanoHesapServisi.cs:65). Doğrulama: çalıştırılan ViewModel senaryosu.

### 2. [P1] UPS giriş kayıpları ve şarj yükü besleme hesabına eklenmiyor

20 kW kritik yük, proje cosφ=0,99 ve UPS-30 için UPS giriş akımı **34,93 A** hesaplanıyor. Buna rağmen ana pano **29,16 A**, ana ve çıkış sigortaları **32 A** kalıyor; aynı standart seçim fonksiyonu UPS giriş akımı için **40 A** seçiyor. UPS hesabı panolar, OG ve jeneratör hesaplandıktan sonra çalışıyor; UPS giriş gücü üst panolara geri aktarılmıyor.

Kaynak: [TekHatSemasiViewModel.cs:486](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.cs:486), [UpsHesapServisi.cs:61](/C:/dev/EYE4Pro/src/Kst.Acoustics.Core/Electric/Calculation/UpsHesapServisi.cs:61). Doğrulama: çalıştırılan ViewModel senaryosu.

### 3. [P1] Busbara bağlı kompanzasyon panosunun yükü busbarda sıfır kalıyor

300 kW ana yükte kompanzasyon **142,5 kVAr / 294,124 A** çıkarken onu besleyen busbar **0 A** ve **160 A tipinde** kalıyor. `TumunuHesapla` busbarı, kompanzasyon panosunun banka akımını kesinleştirmeden önce hesaplıyor. Sonrasında busbar yeniden hesaplanmıyor; gerilim düşümü de eksik kalıyor.

Kaynak: [TekHatSemasiViewModel.cs:433](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.cs:433), [TekHatSemasiViewModel.cs:467](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.cs:467). Doğrulama: çalıştırılan ViewModel senaryosu.

### 4. [P1] Sigorta kapasitesini aşan yükte kabloya yanlış yeterlilik veriliyor

4 MW trifaze alt pano talebi **6792,356 A** iken sigorta listesi **3200 A** tavanında kalıyor. Kablo bu sınırlanmış değerle **6x(4x240) mm²** seçiliyor ve `KabloKesitiYeterli=true` yazılıyor. Kodun kendi 564 A/kol kapasitesine göre toplam **3384 A**, gerçek talebin çok altında. Yeterlilik gerçek yük akımıyla tekrar denetlenmiyor.

Kaynak: [KabloHesapServisi.cs:254](/C:/dev/EYE4Pro/src/Kst.Acoustics.Core/Electric/Calculation/KabloHesapServisi.cs:254), [KabloHesapServisi.cs:276](/C:/dev/EYE4Pro/src/Kst.Acoustics.Core/Electric/Calculation/KabloHesapServisi.cs:276). Doğrulama: çalıştırılan ViewModel senaryosu.

### 5. [P1] Pano kendi busbarına bağlanabiliyor; hesap döngüye giriyor

Bir tali panodan busbar oluşturup aynı panoyu bu busbara bağlamak mümkün. Seçici tüm busbarları sunuyor; ViewModel kendini/alt ağacını üst pano yapmayı denetlemiyor. Denemede `pano.UstPano == pano` ve `pano.AltPanolar.Contains(pano)` oluştu. UI'nin ardından çağırdığı hesapta `DerinlikBul` sonsuz döngüye girer. Doğrulamada uygulamayı kilitlememek için döngü oluştuktan sonra hesap çağrılmadı.

Kaynak: [PanoBusbarBaglaDialog.axaml.cs:36](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/Views/PanoBusbarBaglaDialog.axaml.cs:36), [TekHatSemasiViewModel.PanoBusbar.cs:155](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.PanoBusbar.cs:155), [TekHatSemasiViewModel.cs:667](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.cs:667).

### 6. [P1] Excel aktarımı mevcut linyeleri ve kontrol atamalarını siliyor

Aktarım eklemek yerine önce seçili panonun bütün linyelerini ve ilgili kontrol referanslarını temizliyor. Geçerli satır içermeyen bir aktarımda bile silme gerçekleşiyor; ön onay yok. Denemede mevcut linye sayısı **1 → 0**, aktarılan satır sayısı **0** oldu. Bu davranış `codex11.md` içindeki seçili panoya ekleme sözleşmesine de aykırı.

Kaynak: [TekHatSemasiViewModel.PanoBusbar.cs:421](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.PanoBusbar.cs:421), [TekHatSemasiWindow.Islemler.cs:639](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/Views/TekHatSemasiWindow.Islemler.cs:639). Doğrulama: çalıştırılan ViewModel senaryosu.

### 7. [P1] Aynı adlı panolar kaydedilip açılınca bağlantı değişiyor

Pano ekleme ve yeniden adlandırma benzersiz ad kontrolü yapmıyor; açılış ise üst panoyu ada göre ilk eşleşmeden buluyor. Z ve 1. katta iki ayrı `A` panosu oluşturup `B` panosunu 1. kattaki A'ya bağladığım senaryoda, kayıt/açılış sonrası B **Z katındaki A'ya** bağlandı. Proje topolojisi sessizce değişiyor.

Kaynak: [TekHatSemasiViewModel.PanoBusbar.cs:17](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.PanoBusbar.cs:17), [TekHatSemasiViewModel.cs:543](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.cs:543). Doğrulama: JSON kayıt/açılış senaryosu.

### 8. [P1] Excel'de metin olarak girilmiş ondalık sayılar yanlış okunuyor

İngilizce kültürde metin hücrelerindeki `300,5 W`, `0,5` talep ve `10,5 m`; **3005 W**, **1** talep ve **105 m** oluyor. Türkçe kültürde noktalı karşılıkları aynı hatayı üretiyor. `TryGetValue<double>` karşı ayracı binlik ayracı kabul edip başarılı döndüğü için nokta/virgül uyumlu `NumberHelper` hiç çalışmıyor. Talep faktörü önce 5 okunup sonrasında geçersiz diye 1'e dönüyor.

Kaynak: [TekHatLinyeExcelServisi.cs:178](/C:/dev/EYE4Pro/src/Kst.Acoustics.Core/Electric/Calculation/TekHatLinyeExcelServisi.cs:178). Doğrulama: iki kültürde oluşturulup tekrar içeri aktarılan `.xlsx` dosyaları. Sayısal Excel hücreleriyle sınırlı bir hata değil; özellikle metin hücreleri etkileniyor.

### 9. [P2] OG penceresinde İptal, ana pano değişikliklerini geri almıyor

Önizleme hesabı geçici OG nesnesiyle birlikte canlı ana pano nesnesini kullanıyor. Denemede projeye OG eklenmemişken yalnız önizleme hesabı ana pano kablosunu **3 m NHXMH 4x16 → 60 m NYY 4x70**, giriş şalterini **32 → 160 A** değiştirdi. İptal yalnız pencereyi kapatıyor; çağıran akış da önceki durumu geri yüklemiyor.

Kaynak: [OrtaGerilimDialog.axaml.cs:255](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/Views/OrtaGerilimDialog.axaml.cs:255), [OrtaGerilimDialog.axaml.cs:341](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/Views/OrtaGerilimDialog.axaml.cs:341). Doğrulama: önizlemenin çağırdığı servis çalıştırıldı; iptal akışı kaynak koddan izlendi.

### 10. [P2] Pano düzenleme ikinci ana panoya izin veriyor

Yeni pano ekleme ana pano tekilliğini kontrol etse de düzenleme diyaloğu tipi doğrudan yazıyor. Ana pano varken tali panoyu `Ana Pano` yapmak mümkün; ana pano adı da serbestçe değiştiriliyor. Kök pano referansı bu dönüşümde düzeltilmediğinden ana pano üzerinden çalışan hesap ve çizim yolları farklı panoları esas alabilir.

Kaynak: [PanoDuzenleDialog.axaml.cs:144](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/Views/PanoDuzenleDialog.axaml.cs:144). Doğrulama: UI ve ViewModel çağrı zinciri.

### 11. [P2] UPS panosunu düzenlemek tipini sessizce Tali Pano yapıyor

UPS panosunda `Panoyu Düzenle` açıldığında tip eşlemesinde `UpsPanosu` bulunmadığı için `Tali Pano` seçiliyor. Yalnız ad/kat değiştirip Kaydet demek bile tipi değiştiriyor; projedeki UPS kaydı ise kalıyor. Modelde UPS ile pano tipi birbirinden kopuyor.

Kaynak: [PanoDuzenleDialog.axaml.cs:34](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/Views/PanoDuzenleDialog.axaml.cs:34), [PanoDuzenleDialog.axaml.cs:150](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/Views/PanoDuzenleDialog.axaml.cs:150). Doğrulama: diyaloğun açılış ve kaydetme kodu.

### 12. [P2] Otomatik UPS seçimi ilk boş yükte seçilen cihazda kalıyor

Merkezi UPS boş dağıtım panosuyla kurulunca **UPS-6** seçiliyor. Sonradan 20 kW yük eklenip ihtiyaç **25,25 kVA** olduğunda yeniden seçim yapılmıyor; UPS-6 kalıp yetersiz işaretleniyor. Otomatik seçim sonucu `UpsTanimAd` alanına yazıldığı için sonraki hesapta manuel seçim gibi korunuyor; otomatik/manuel ayrımı yok.

Kaynak: [UpsHesapServisi.cs:40](/C:/dev/EYE4Pro/src/Kst.Acoustics.Core/Electric/Calculation/UpsHesapServisi.cs:40). Doğrulama: merkezi UPS oluşturma ve sonradan yük ekleme senaryosu.

### 13. [P2] Tekrarla Ekle, linye ekipman ve koruma tercihlerini kaybediyor

Kopyalamada `KontaktorluMu`, `DarbeKesiciMi`, `TmsZorla`, `KesmeKapasitesiManuel` ve manuel kesme kapasitesi taşınmıyor. Denemede kaynakta tüm bayraklar açık ve **100 kA** seçiliyken kopyada tümü kapalı ve **10 kA** oluştu. Kopyalanan devre kaynakla aynı işlevi taşımıyor.

Kaynak: [TekHatSemasiViewModel.PanoBusbar.cs:392](/C:/dev/EYE4Pro/src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.PanoBusbar.cs:392). Doğrulama: çalıştırılan kopyalama senaryosu.

### 14. [P2] DXF'te UPS, besleme hattının dışında çiziliyor

Ana pano Z, kritik pano 1. katta olan senaryoda normal ana→kritik pano kablosu doğrudan çiziliyor. UPS bloğu kritik panonun diğer tarafına ekleniyor ve yalnız kritik panoya bağlanıyor; UPS girişine üst panodan bağlantı yok. Üretilen DXF çizgi uçları bunu doğruluyor: kritik alt kenarı `(75,264)` normal kabloyla besleniyor, kritik üstünden `(75,290)` UPS altına `(75,308)` ayrı dal var; UPS üstünde `(75,334)` besleme bulunmuyor.

Kaynak: [TekHatSemasiDxfServisi.cs:148](/C:/dev/EYE4Pro/src/Kst.Acoustics.Core/Electric/Calculation/TekHatSemasiDxfServisi.cs:148), [TekHatSemasiDxfServisi.cs:822](/C:/dev/EYE4Pro/src/Kst.Acoustics.Core/Electric/Calculation/TekHatSemasiDxfServisi.cs:822). Doğrulama: DXF üretimi ve netDxf ile çizgi geometrisinin okunması.

### 15. [P2] Busbar hesabı eşzamanlılık faktörünü uygulamıyor

200 kW trifaze alt pano ve `ks=0,5` için üst panoya yansıyan talep **169,809 A** iken busbar **339,618 A** üzerinden seçiliyor. Hesap, talep akımı yerine bağlı akımı kullanıyor; segment gerilim düşümü ve ısı kaybı da aynı tutarsızlığı sürdürüyor.

Kaynak: [BusbarHesapServisi.cs:23](/C:/dev/EYE4Pro/src/Kst.Acoustics.Core/Electric/Calculation/BusbarHesapServisi.cs:23), [BusbarHesapServisi.cs:66](/C:/dev/EYE4Pro/src/Kst.Acoustics.Core/Electric/Calculation/BusbarHesapServisi.cs:66). Doğrulama: hesap servisi senaryosu.

## Uygulanan düzeltme sırası

Önce 1–8 kapsamındaki hesap, veri kaybı ve döngü sorunları; ardından 9–13 kapsamındaki iptal, düzenleme ve seçim tutarlılığı; son olarak 14–15 kapsamındaki DXF topolojisi ve busbar talep hesabı düzeltildi. Her hata senaryosu regresyon kapsamına alındı.


## Düzeltme durumu (2026-09-07)

15 bulgunun tamamı düzeltildi:

1. Monofaze pano beslemesi en yüklü gerçek faza göre boyutlandırılıyor.
2. UPS giriş kaybı ve şarj yükü üst besleme zincirine yansıtılıyor.
3. Busbar, kompanzasyon banka akımı kesinleştikten sonra hesaplanıyor.
4. Sigorta listesinin tavanını aşan gerçek talep kablo seçiminde korunuyor.
5. Kendi veya alt ağacındaki busbara bağlantı işlem öncesinde reddediliyor.
6. Excel aktarımı mevcut linyeleri ve kontrol referanslarını koruyarak ekliyor.
7. Pano adları büyük/küçük harf duyarsız benzersiz tutuluyor.
8. Metin Excel hücreleri ortak kültür bağımsız sayı ayrıştırıcıyla okunuyor.
9. OG önizlemesi canlı ana pano bağlantısını değiştirmiyor.
10. Ana pano adı/türü korunuyor ve ikinci ana pano reddediliyor.
11. UPS ve transfer pano türleri doğru gösteriliyor ve genel düzenlemede korunuyor.
12. Otomatik UPS seçimi yük değiştikçe yeniden yapılıyor; manuel seçim ayrı tutuluyor.
13. Linye kopyası ekipman ve manuel koruma tercihlerini taşıyor.
14. DXF beslemesi üst pano → UPS girişi → kritik pano sırasıyla çiziliyor.
15. Busbar hesabı diversiteli talep akımını kullanıyor.

Doğrulama: çözüm derlemesi 0 hata/0 uyarı; eklenen 13 M11 regresyonunun 13'ü de geçti. Tam özel test koşucusunda yalnız M11 dışındaki mevcut `Building acoustics hardening: material library save failure reaches caller` testi `Access to the path is denied` hatası verdi.