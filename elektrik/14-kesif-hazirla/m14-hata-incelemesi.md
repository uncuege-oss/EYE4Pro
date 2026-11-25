# M14 — Keşif Hazırla hata incelemesi

Tarih: 15 Ekim 2026. Aşağıdaki bulgular ilk inceleme anındaki kodu anlatır; satır bağlantıları o sürüme aittir.

## Düzeltme durumu — 15 Ekim 2026

**12 bulgunun tamamı düzeltildi.** İçe aktarma sorunu ayrıca gerçek kullanıcı dosyasıyla doğrulandı.

| Alan | Son davranış |
|---|---|
| M14-01 | Kayıt sonrası yalnız diske yazılan sürüm temiz sayılır; kayıt sırasında düzenleme kirli kalır. Eşzamanlı kayıt/belge geçişi engellenir. |
| M14-02–04 | Referans hedefi kalıcıdır; başlık/boş seçim satır kaybettirmez. Katkılar yerinde güncellenir, elle düzenlenmiş satır değerleri korunur. |
| M14-05 | Çözülemeyen öğrenilmiş pozlar sorunlu kalem olarak gösterilir; yeni sıfır bedel üretilmez, mevcut bedel korunur. |
| M14-06 | Teknik anma değerleri zorunlu filtredir; 50 kVAr → 5 kVAr ve 160 A → 80 A gibi uyumsuz atamalar reddedilir. |
| M14-07 | Ana ekran ve özel poz penceresi kendi seçimlerini siler. |
| M14-08–09 | Türkçe büyük başlıklar okunur; aday listesi kütüphane ekleme/silme/aktiflik değişimlerini izler. |
| M14-10–12 | İcmal kırılımları tablo kimliğiyle ayrılır; çok satırlı quoted CSV korunur; hedef poz değişince kontrol onayı kaldırılır. |

Kullanıcının `birim fiyat 2026.xlsx` dosyasında başlık BF sayfasının **C7:G7** aralığında. Eski okuyucu ilk kullanılan satırı başlık sanıyor, ayrıca `İmalatın Cinsi` ve `Birim Fiyatı` eş adlarını tanımıyordu. Yeni okuyucu başlık satırını tarar, gerçek kolon numaralarını ve iş grubu kategorilerini korur.

Gerçek dosya sonucu: **14.838 kaynak satırı, 14.837 benzersiz kütüphane/atama adayı**. `35.505.2100` iki özdeş kayıt içeriyor (203,84); son değer kazanır kuralıyla tekilleşiyor. Kaynaktaki 1.360 sıfır fiyat korunuyor. İlk poz `35.710.1101` için 1.107.437,49 fiyatı ve kategori doğrulandı. Kaynak fiyat toplamı bağımsız XML kontrolüyle kuruş hassasiyetinde uzlaştı. İçe aktarılan kütüphanenin proje JSON kaydet/aç kontrolü geçti. Kaynak Excel değiştirilmedi.

Yeni `KesifGuvenilirlikTests.cs` içinde **18 regresyon**, mevcut sınıfta 34 test: **M14 52/52 başarılı**. Gerçek App derlemesi ve Tests derlemesi 0 hata/0 uyarı. Canlı GUI testi yapılmadı.

Genel test koşucusu **744/745** başarılı. M14 dışındaki `MaterialLibrarySaveFailureIsReportedToCaller` Windows'ta dizini dosya olarak yazarken `IOException` yerine `UnauthorizedAccessException` alıyor. Bu M08 testi M14 kapsamı dışında bırakıldı; genel paketin tamamı başarılı olarak raporlanmıyor. Mevcut Library proje ayarında koşucu `dotnet build src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj --no-restore -p:OutputType=Exe -p:UsedAvaloniaProducts=` ile derlenip DLL üzerinden çalıştırıldı.

---

12 bulgu: 7 yüksek öncelikli (P1), 5 orta öncelikli (P2). P1 veri kaybına, yanlış keşif bedeline veya yanlış kaydın silinmesine yol açan; P2 içe aktarma, liste tutarlılığı ve kontrol göstergelerini bozan hataları ifade eder.

## Doğrulama ve sınırlar

- Core ve M14 ViewModel kaynaklarını kullanan geçici, arayüzsüz koşucuda mevcut `KesifHazirlaTests` sınıfının **34/34 testi geçti**. Testlerde varsayılan eşleşme veritabanı yolları geçici yollara yönlendirildi; kullanıcının gerçek öğrenme veritabanı kullanılmadı.
- 11 bulgu doğrudan servis/ViewModel çağrılarıyla yeniden üretildi. Kütüphane yenileme bulgusu iki ayrı senaryoyla doğrulandı.
- Kayıt bulgusunda gerçek asenkron kayıt servisi çalıştırıldı; pencerenin kayıt sonrası parmak izi işlemi koşucuda modellendi. Bu, uçtan uca GUI testi değildir.
- App derlemesi başarılı: **0 hata, 0 uyarı**. Komut: `dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj --no-restore --nologo -v quiet -p:UsedAvaloniaProducts=`. İlk normal deneme Avalonia telemetrisinin sandbox dışındaki log dosyasına erişememesiyle durdu; ikinci komutta yalnız `AvaloniaStats` hedefi atlandı.
- Canlı GUI etkileşim testi ve tüm modüllerin genel test paketi çalıştırılmadı. Mevcut testlerin geçmesi aşağıdaki senaryoları kapsadıkları anlamına gelmez.

## Hata listesi

### M14-01 — P1: Kayıt sırasında yapılan değişiklikler kaydedilmiş sayılıyor

Kaynak: [KesifHazirlaWindow.axaml.cs:255](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaWindow.axaml.cs#L255), [KesifJsonServisi.cs:27](../../src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifJsonServisi.cs#L27).

`KaydetAsync` proje içeriğini beklemeye geçmeden JSON'a dönüştürüyor. Dosya yazımı sürerken arayüz düzenlemeye açık. Yazım tamamlandığında `KayitDurumunuTemizle` diske giden içeriğin yerine o andaki güncel projenin parmak izini alıyor. Böylece son düzenleme diske yazılmadığı halde çıkışta kaydetme uyarısı gösterilmeyebilir. Kayıt sürerken Yeni/Aç işlemleri de engellenmiyor.

Yeniden üretim: Büyük bir projede kaydı başlat; yazım bitmeden proje adını değiştir. Koşucuda `yielded=True`, bellek adı `edit-during-save`, disk adı `before-save` oldu. Pencerenin mevcut kayıt sonrası işlemi modellendiğinde değişiklik denetimi `False` döndü.

Düzeltme yönü: Kayıt anındaki proje kimliğini ve içerik parmak izini yakala; yalnız kaydedilen sürümü temiz kabul et. Kayıt tamamlanana kadar belge değiştirme ve eşzamanlı kayıt akışlarını denetle.

### M14-02 — P1: Başlık tablosu seçiliyken eşleşme güncellemek otomatik satırları siliyor

Kaynak: [KesifHazirlaViewModel.cs:822](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L822).

`OtomatikSatirlariUret` önce referansın katkılarını tüm tablolardan temizliyor; sonra seçili tablonun null/başlık olup olmadığını kontrol ediyor. Hedef uygun değilse yeni satır üretmeden dönüyor. Sonraki durum mesajları bu uyarıyı da örtebiliyor.

Yeniden üretim: Yaprak tabloya 10 m eşlenmiş metraj yükle. Başka bir üst tabloya alt tablo ekle; üst tabloyu seç. Tanımlı eşleşmeyi aynı pozla güncelle. İlk yapraktaki satır sayısı **1 → 0** oldu.

Düzeltme yönü: Hedefi herhangi bir silme işleminden önce doğrula; yeniden işleme işlemini mevcut tablo dağılımını koruyacak şekilde uygula.

### M14-03 — P1: Tek eşleşme değişikliği bütün referansları seçili tabloya taşıyor

Kaynak: [KesifHazirlaViewModel.cs:930](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L930), [KesifHazirlaViewModel.cs:824](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L824).

Eşleşme ekleme/güncelleme/silme tüm referansları yeniden işliyor. Her referansın önceki tablo katkıları kaldırılıyor ve tamamı o anda seçili tabloya yazılıyor. İlk aktarımda seçili tabloya yazma kuralı, daha önce başka tablolara dağıtılan metrajın ilgisiz bir eşleştirme sırasında taşınmasını gerektirmiyor.

Yeniden üretim: A tablosuna 10 m referans ekle ve eşleştir. B tablosuna farklı kimlikli 20 m referans ekle ve eşleştir. Sonuç **A=0, B=30**; beklenen **A=10, B=20**.

Düzeltme yönü: Referansın hedef tablo kimliğini koru veya etkilenen katkıları bulundukları tabloda güncelle.

### M14-04 — P1: İlgisiz eşleştirme, elle değiştirilmiş birim fiyatı kaybettiriyor

Kaynak: [KesifHazirlaViewModel.cs:854](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L854), [KesifHazirlaViewModel.cs:888](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L888).

Tamamen otomatik satır yeniden işlemede siliniyor ve güncel kütüphane fiyatıyla yeniden oluşturuluyor. Satırın elle düzenlenen fiyatı ve orijinal fiyat bilgisi korunmuyor. Bu, kütüphane fiyat değişikliklerinin mevcut keşif satırlarını değiştirmemesi kuralını da bozuyor.

Yeniden üretim: Kütüphane fiyatı 100 olan otomatik satırın fiyatını 777 yap. Başka bir metraj kimliğine eşleşme ekle. Mevcut satırın fiyatı **777 → 100** oldu.

Düzeltme yönü: Mevcut satırların elle düzenlenmiş fiyatını ve diğer kullanıcı düzenlemelerini koruyarak katkı güncelle.

### M14-05 — P1: Kütüphanede bulunmayan öğrenilmiş poz sessizce sıfır fiyatla keşfe giriyor

Kaynak: [KesifHazirlaViewModel.cs:844](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L844), [KesifHazirlaViewModel.cs:862](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L862), [KesifHazirlaViewModel.cs:964](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L964).

Öğrenilmiş eşleşmenin poz numarası aktif kütüphanede bulunmasa da otomatik satır oluşturuluyor; fiyat `0` oluyor. Eşlenmemiş listesi yalnız veritabanında kayıt bulunmasına baktığından bu kalemi sorunlu göstermiyor. Farklı fiyat listeleri kullanılan projelerde de aynı durum oluşabilir.

Yeniden üretim: 100 fiyatlı pozu eşleştir; fiyat listesini pasife al; başka bir eşleme ekleyerek yeniden işlemeyi tetikle. Sonuç **fiyat=0, eşlenmemiş=0**.

Düzeltme yönü: Eşleşmenin mevcut ve kullanılabilir bir poza çözüldüğünü doğrula. Çözülemeyen kalemleri açıkça bildir; mevcut satırın bedelini sıfırlama.

### M14-06 — P1: Otomatik eşleştirme uyumsuz teknik kapasiteyi kabul ediyor

Kaynak: [KesifOtomatikPozEslemeServisi.cs:65](../../src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifOtomatikPozEslemeServisi.cs#L65), [KesifOtomatikPozEslemeServisi.cs:181](../../src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifOtomatikPozEslemeServisi.cs#L181).

Kondansatörde tür denetleniyor fakat kVAr uyumu zorunlu koşul değil. Sayısal uyumsuzluğun puan cezası metin/grup benzerliğiyle aşılabiliyor. Seçilen yanlış poz global veritabanına da öğreniliyor.

Yeniden üretim: `Kompanzasyon / Kondansatör / 50 kVAr kondansatör / adet` kalemine tek aday olarak `5 kVAr kondansatör` ver. Servis adayı **24,727 puanla kabul etti**; eşleşme eşiği 20.

Düzeltme yönü: kVAr, kVA, kW, Hz, kutup ve benzeri kritik özellikler için alan bazlı zorunlu uyumluluk kuralları uygula. Puanlama yalnız teknik olarak uygun adaylar arasında seçim yapmalı.

### M14-07 — P1: Özel poz penceresinden silme başka pozu siliyor

Kaynak: [KesifHazirlaViewModel.cs:552](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L552), [OzelPozlarWindow.axaml:70](../../src/Kst.Acoustics.App/Electric/KesifHazirla/OzelPozlarWindow.axaml#L70).

Özel poz penceresi `SeciliOzelPoz` ile seçim tutuyor. Aynı silme komutu ana ekrandaki özel poz modu aktifken `SeciliPoz` değerini kullanıyor. Pencere zaten bu moddan açıldığı için iki seçim ayrıştığında farklı kayıt siliniyor.

Yeniden üretim: A ve B özel pozlarını oluştur. Ana ekran seçimini A, özel poz penceresi seçimini B yap; Poz Sil komutunu çalıştır. **A silindi, B kaldı**.

Düzeltme yönü: Silinecek pozu komut parametresiyle açıkça geçir veya iki pencereye ayrı silme komutu ver.

### M14-08 — P2: Büyük harfli Türkçe Excel/CSV başlıkları okunmuyor

Kaynak: [KesifPozImportServisi.cs:204](../../src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifPozImportServisi.cs#L204).

`ToLowerInvariant` ve küçük Türkçe harf değişimleri, büyük `İ` karakterini beklenen ASCII `i` anahtarına dönüştürmüyor. Aynı normalizasyon metraj başlığı taramasında da kullanılıyor.

Yeniden üretim: `POZ NO;İMALAT ADI;BİRİM;BİRİM FİYAT` başlığı altında `P1;Kablo;m;123` aktar. Sonuç **imalat adı boş, birim adet, fiyat 0** oldu.

Düzeltme yönü: Türkçe büyük/küçük harfleri kapsayan normalizasyon uygula; gerekli başlıklar eksikse sessiz varsayılanlar yerine hata bildir.

### M14-09 — P2: Poz Atama aday listesi kütüphaneyle senkron kalmıyor

Kaynak: [KesifHazirlaViewModel.cs:279](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L279), [KesifHazirlaViewModel.cs:530](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L530), [KesifHazirlaViewModel.cs:550](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L550).

BF listesi içe aktarma ve özel poz ekleme/silme ana ekran filtresini yeniliyor; `FiltrelenmisAtamaPozlari` güncellenmiyor. Poz Atama penceresi açıkken yeni pozlar görünmüyor, silinen pozlar seçilebilir kalabiliyor. Pencereyi yeniden açmak listeyi yenilediği için sorun gözden kaçabiliyor.

Yeniden üretim: Yeni ViewModel'e tek pozlu liste aktar: **kütüphane=1, atama adayları=0**. Ayrı senaryoda adayları yenile, özel pozu sil: silinen nesne atama adaylarında **kalmaya devam etti**.

Düzeltme yönü: Tüm kütüphane değişikliklerinde tek bir ortak yenileme hattını kullan.

### M14-10 — P2: Aynı adlı tabloların icmal kırılımları birleşiyor

Kaynak: [KesifHesapServisi.cs:11](../../src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifHesapServisi.cs#L11), [KesifHesapServisi.cs:35](../../src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifHesapServisi.cs#L35).

Kırılımlar tablo kimliği yerine `Ad` üzerinden gruplanıyor. Aynı ad farklı dallarda veya yeniden adlandırmayla kullanılabiliyor. Konsolide toplam doğru kalsa da tablo bazındaki miktar dağılımı kayboluyor.

Yeniden üretim: İki ayrı `Kat` tablosunda aynı pozdan 10 m ve 20 m oluştur. İcmalde beklenen iki kırılım yerine **tek kırılım** oluştu.

Düzeltme yönü: Tablo Id ile grupla; görünürde gerektiğinde tam hiyerarşi yolunu göster.

### M14-11 — P2: Çok satırlı quoted CSV alanı kayıtları ve fiyatları bozuyor

Kaynak: [KesifPozImportServisi.cs:66](../../src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifPozImportServisi.cs#L66), [KesifPozImportServisi.cs:73](../../src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifPozImportServisi.cs#L73).

Dosya önce fiziksel satırlara bölünüyor; tırnak durumu her satırda sıfırlanıyor. Tırnak içindeki satır sonu yeni poz kaydı sanılıyor.

Yeniden üretim: `P1;"İlk satır\nİkinci satır";m;123` içeren tek kaydı aktar. Sonuç **2 kayıt**, ilk kaydın fiyatı **0** oldu; beklenen tek kayıt ve 123 fiyat.

Düzeltme yönü: Tırnak durumunu satırlar arasında koruyan kayıt okuyucu veya uygun CSV ayrıştırıcısı kullan.

### M14-12 — P2: Poz değişince eski “Kontrol edildi” onayı korunuyor

Kaynak: [KesifHazirlaViewModel.cs:794](../../src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs#L794), [KesifPozEslemeServisi.cs:69](../../src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifPozEslemeServisi.cs#L69).

Eşleşmenin hedef pozu değiştirilirken `KontrolEdildi` sıfırlanmıyor. Kullanıcı önceki atamayı onaylamış olsa bile yeni atama yeşil/kontrol edilmiş olarak kalıyor.

Yeniden üretim: P1 eşleşmesini kontrol edilmiş yap; hedefini P2 olarak güncelle. Bayrak **True kaldı**.

Düzeltme yönü: Eşleşme kimliği/hedefi değiştiğinde kontrol bayrağını kaldır; aynı hedefin tekrar kaydında koruyabilirsin.

## Önerilen düzeltme sırası

1. Kayıt sürümü/parmak izi ve referans yeniden işleme: M14-01–05.
2. Teknik eşleşme güvenilirliği ve silme hedefi: M14-06–07.
3. Türkçe başlıklar, aday listesi senkronu, icmal kimliği, CSV kayıt okuma ve kontrol onayı: M14-08–12.

Her düzeltme için yukarıdaki tetikleyiciyi kapsayan regresyon eklenmeli; özellikle birden fazla tablo/referans, elle düzenlenmiş otomatik satırlar ve kayıt sırasında düzenleme mevcut 34 testin dışında kalıyor.
