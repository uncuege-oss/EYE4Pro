# Mimari BIM Modül Planlama Raporu

> Tarih: 2026-06-12
> Bağlam: EYE4PRO ana modül yapısı, M50 Bina Geometrisi ortak modülü ve mimari ana başlığı 31-39 aralığı.
> Hedef: EYE4PRO içinde DXF içe aktarmaya ek olarak, binayı sıfırdan tasarlayabilen BIM yönlü mimari üretim hattı kurmak.

---

## 1. Yeni Hedef Tanımı

EYE4PRO mimari başlığı yalnız mevcut DXF dosyasını okuyup mahal/alan raporu çıkaran bir yardımcı
olmamalıdır. Hedef, kullanıcının binayı tasarım kararından başlayarak program içinde kurabilmesidir:

1. Bina işlevi seçilir.
2. İhtiyaç programı otomatik önerilir.
3. Mahaller, alanlar, hacimler, kapasite ve ilişki kuralları tanımlanır.
4. Koridor, merdiven, asansör, wc, şaft ve teknik hacimler kurallara göre yerleştirilir.
5. Basit 2D plan ve 3D kütle/BIM model oluşturulur.
6. Yönetmelik ön kontrolleri yapılır.
7. Detay, malzeme, kapı-pencere, cephe ve pafta çalışmaları ayrı modüllerde geliştirilir.
8. Sonunda mimarın ihtiyacı olan çizelge, rapor, metraj ve dışa aktarımlar hazırlanır.

Bu hedefte M50 sadece "DXF yükleme aracı" olarak kalmamalı; EYE4PRO'nun ortak bina modeli
çekirdeği haline gelmelidir. Mimari modüller ise bu çekirdeği akıllı tasarım, kural kontrolü,
detaylandırma ve çıktı üretimi için kullanmalıdır.

---

## 2. Stratejik Mimari Karar

### 2.1 DXF akışı korunmalı

Mevcut akış değerli ve korunmalıdır:

```text
DXF -> M50 Bina Geometrisi -> Akustik / Elektrik / Mekanik / Mimari / Keşif
```

Bu akış dışarıdan gelen projelerde hızlı başlangıç sağlar.

### 2.2 Sıfırdan BIM akışı eklenmeli

Yeni hedef için ikinci akış eklenmelidir:

```text
Mimari İhtiyaç Programı
-> Parametrik Mahal Üretimi
-> Kurallı Plan Yerleşimi
-> M50 Ortak Bina Modeli
-> 3D BIM Model
-> Akustik / Elektrik / Mekanik / Keşif / Rapor
```

Bu ikinci akışta kullanıcı binayı EYE4PRO içinde kurar. DXF artık zorunlu başlangıç değil,
yalnız alternatif veri girişidir.

### 2.3 Dosya stratejisi

Mevcut `.e4bina.json` yapısı genişletilmelidir; fakat diğer modülleri kırmamak için iki katmanlı
yaklaşım önerilir:

| Dosya | Amaç |
|---|---|
| `.e4bina.json` | Ortak bina modeli: katlar, mahaller, açıklıklar, çekirdekler, şaftlar, merdivenler, temel 3D hacimler |
| `.e4mimari.json` | Mimari tasarım kararları: ihtiyaç programı, mahal şablonları, kurallar, malzemeler, detay kararları, rapor ayarları |

Akustik, elektrik ve mekanik modüller öncelikle `.e4bina.json` okur. Mimari modül daha zengin
tasarım bilgisini `.e4mimari.json` içinde tutar.

---

## 3. Bir BIM Mimari Modülünün Çözmesi Gereken İşler

### 3.1 Bina işlevi ve proje tipi kararı

Kullanıcı işe "hangi bina tasarlanıyor?" sorusuyla başlamalıdır. Örnek bina işlevleri:

- Konut / apartman
- Villa / müstakil yapı
- Ofis
- Eğitim yapısı
- Sağlık yapısı
- Otel / konaklama
- Kültür merkezi / konferans salonu
- Ticari yapı
- Endüstri / depo
- Karma kullanım

Bu seçim daha sonra mahal listesi, koridor genişliği, wc sayısı, merdiven ihtiyacı, yangın kaçış
yaklaşımı, otopark, erişilebilirlik ve mekanik/elektrik ön kabullerini etkiler.

### 3.2 İhtiyaç programı üretimi

Program kullanıcıdan kapasite ve kullanım bilgilerini almalıdır:

- Kullanıcı/kişi sayısı
- Daire/oda/ofis/derslik/yatak sayısı
- Kat adedi ve yaklaşık kat yüksekliği
- Hedef toplam alan
- Servis alanı oranı
- Sirkülasyon oranı
- Teknik hacim ihtiyacı
- Islak hacim ihtiyacı
- Otopark/servis/yangın/erişilebilirlik özel koşulları

Sistem bu bilgilerle önerilen mahal programı üretir.

### 3.3 Mahal şablon kütüphanesi

Her mahal tipi için bir şablon olmalıdır. Şablon yalnız isim değil, tasarım bilgisi taşımalıdır:

```text
MahalSablonu
├── MahalTipi
├── VarsayilanAd
├── MinimumAlanM2
├── IdealAlanM2
├── MinimumNetYukseklikM
├── KisiKapasitesi
├── EnBoyOraniAraligi
├── GerekliKomşuluklar
├── UzakDurulacakMahaller
├── GerekliAciklikTipleri
├── HavalandirmaAydinlatmaIhtiyaci
├── AkustikOncelik
├── ElektrikOncelik
├── MekanikOncelik
└── VarsayilanMalzemeSeti
```

Örnek mahal tipleri:

- Salon, yatak odası, çocuk odası, mutfak, banyo, wc, antre, hol, depo.
- Açık ofis, yönetici odası, toplantı odası, resepsiyon, arşiv, server odası.
- Derslik, laboratuvar, atölye, öğretmen odası, kütüphane, konferans salonu.
- Muayene, bekleme, danışma, steril alan, personel alanı.
- Restoran, mutfak, çamaşırhane, teknik oda, yükleme alanı.
- Merdiven, asansör, yangın merdiveni, rampa, şaft, elektrik odası, mekanik oda.

### 3.4 Mahal ilişki matrisi

BIM tasarımın akıllı olabilmesi için mahaller arası ilişkiler tanımlanmalıdır:

| İlişki | Örnek |
|---|---|
| Yakın olmalı | mutfak-servis, konferans salonu-foye, yatak odası-banyo |
| Aynı aksta/üst üste olmalı | wc, ıslak hacim, tesisat şaftı, merdiven, asansör |
| Ayrı tutulmalı | gürültülü teknik hacim-yatak odası, servis alanı-temsil alanı |
| Dış cephe almalı | yatak odası, ofis, derslik |
| İç bölgede kalabilir | depo, şaft, teknik hacim |
| Ana dolaşıma bağlı olmalı | merdiven, asansör, yangın çıkışı, wc |

Bu ilişki matrisi ileride otomatik plan yerleşiminin ana kural seti olur.

### 3.5 Koridor ve sirkülasyon üretimi

Kullanıcı "koridor genişliği" gibi değerleri elle girebilmelidir; ama sistem bina işlevine ve
kapasiteye göre öneri de üretmelidir.

Gerekli kabiliyetler:

- Kullanım tipine göre koridor genişliği önerisi.
- Kat bazlı ana koridor / tali koridor ayrımı.
- Engelli erişimi için minimum geçiş uyarıları.
- Kaçış yolu olarak kullanılan koridorların yangın ön kontrolüne bağlanması.
- Koridorun mahal alanı mı sirkülasyon alanı mı sayılacağının belirlenmesi.

Koridorlar yalnız çizgi değil, mahal ve dolaşım grafı olarak tutulmalıdır. Böylece yangın kaçış,
erişilebilirlik ve elektrik/mekanik yerleşim analizleri aynı veriden beslenir.

### 3.6 Düşey çekirdek ve aks sürekliliği

Merdiven, asansör, wc ve şaftlar BIM modelinde özel eleman olmalıdır. Bunlar sıradan oda gibi
tutulursa yönetmelik ve koordinasyon kontrolleri zayıf kalır.

Gerekli kurallar:

- Merdivenler katlar arasında aynı aksta devam etmeli.
- Asansör kuyuları aynı aksta devam etmeli.
- Tesisat şaftları üst üste gelmeli.
- Wc/ıslak hacimlerin mümkün olduğunca aynı düşey hatta toplanması önerilmeli.
- Katlar arasında aks kaçıklığı varsa kullanıcıya uyarı verilmeli.
- Şaftın geçtiği katlar ve hizmet ettiği mahaller listelenmeli.

Bu yapı M12 elektrik, M21 mekanik ve M14 keşif için büyük değer üretir.

### 3.7 Basit yapı ve 3D aktarım

İlk BIM hedefi karmaşık serbest modelleme değil, güvenilir bir parametrik yapı üretimi olmalıdır:

- Katlar
- Döşemeler
- Duvarlar
- Tavanlar
- Açıklıklar
- Merdiven/asansör/şaft çekirdekleri
- Mahallerin 3D hacimleri
- Basit çatı/kütle
- Cephe açıklıkları

İlk 3D model "görsel ve koordinasyon amaçlı" olmalıdır. Sonraki fazlarda IFC/glTF/OBJ dışa
aktarımı düşünülebilir.

### 3.8 Detay ve malzeme çalışması

Detay çalışması ayrı bir mimari modül olmalıdır. Bu modül şu işleri yapar:

- Duvar tipi seçimi.
- Döşeme/tavan tipi seçimi.
- Zemin, duvar, tavan finisajı.
- Kapı/pencere/doğrama tipleri.
- Islak hacim malzeme setleri.
- Yangın dayanımı, akustik performans, ısı geçirgenliği gibi teknik özellikler.
- Mahal tipine göre varsayılan malzeme önerisi.

Malzeme kütüphanesi yalnız mimari görünüş değil, diğer disiplinlerin kullanacağı teknik veri de
taşımalıdır:

```text
MalzemeYapiElemani
├── Kod
├── Ad
├── Kategori
├── KullanimYeri
├── YanginSinifi
├── YanginDayanimDakika
├── AkustikRw
├── SesYutmaNrc
├── IsilIletkenlik
├── UDegeri
├── KaymazlikSinifi
├── IslakHacimUygun
├── DisMekanUygun
├── BakimNotu
└── BirimMetrajTipi
```

---

## 4. Yönetmelik ve Standart Ön Kontrol Kapsamı

Bu bölümde amaç, programın resmi ruhsat onayı verdiğini iddia etmek değildir. Amaç, mimara
tasarım sırasında erken uyarı veren parametrik bir ön kontrol sistemi kurmaktır.

### 4.1 Öncelikli yönetmelik/kural başlıkları

Programın mimari BIM tarafında takip etmesi gereken ana başlıklar:

| Başlık | Programda kullanılacağı yer |
|---|---|
| Planlı Alanlar İmar Yönetmeliği | bina yüksekliği, kat, çekme mesafesi, kullanım, emsal/alan, yapı yaklaşımı |
| Binaların Yangından Korunması Hakkında Yönetmelik | kaçış yolları, merdiven, yangın kompartımanı, kapı, koridor, duman kontrolü ön uyarıları |
| Otopark Yönetmeliği | kullanım tipine göre otopark ihtiyacı ön hesabı |
| Türkiye Bina Deprem Yönetmeliği | taşıyıcı sistemle koordinasyon, düzensizlik/kat bilgisi, mimari eleman uyarıları |
| Erişilebilirlik mevzuatı ve TS 9111 | engelli erişimi, kapı/koridor/rampa/wc ön kontrolleri |
| Binalarda Enerji Performansı Yönetmeliği | kabuk, cephe, enerji kimlik belgesi için ön veri |
| Binalarda Su Yalıtımı Yönetmeliği | ıslak hacim, bodrum, çatı, teras detay uyarıları |
| Sığınak Yönetmeliği | yapı tipine göre sığınak ihtiyacı kontrolü |
| Asansör Yönetmeliği ve ilgili TS EN 81 standartları | asansör alanı, erişilebilirlik ve düşey çekirdek kararları |
| Yapı Malzemeleri Yönetmeliği | malzeme performans beyanı ve teknik sınıflar |
| Gürültüye karşı koruma mevzuatı | akustik modül ile mahal tipi ve yapı elemanı koordinasyonu |

### 4.2 Resmi kaynak takip listesi

Uygulamada değerler hard-code edilmemeli; kural setleri güncellenebilir olmalıdır. İlk kaynak
takip listesi:

- Planlı Alanlar İmar Yönetmeliği, Resmî Gazete 03.07.2017 / 30113:
  https://www.resmigazete.gov.tr/eskiler/2017/07/20170703-8.htm
- Binaların Yangından Korunması Hakkında Yönetmelik, Resmî Gazete 19.12.2007 / 26735:
  https://www.resmigazete.gov.tr/eskiler/2007/12/20071219-2.htm
- Otopark Yönetmeliği, Resmî Gazete 22.02.2018 / 30340:
  https://www.resmigazete.gov.tr/eskiler/2018/02/20180222-7.htm
- Türkiye Bina Deprem Yönetmeliği, Resmî Gazete 18.03.2018 / 30364 mükerrer:
  https://www.resmigazete.gov.tr/eskiler/2018/03/20180318M1-2.htm

Not: Resmî Gazete içerikleri program geliştirme aşamasında madde madde doğrulanmalı ve uygulama
içindeki kural değerleri sürüm bilgisiyle saklanmalıdır. Yerel belediye koşulları ve güncel mevzuat
değişiklikleri kullanıcı tarafından seçilebilir profil olarak yönetilmelidir.

### 4.3 Kural motoru yaklaşımı

Kural motoru şu yapıda olmalıdır:

```text
KuralSeti
├── Kod
├── Ad
├── Kaynak
├── SurumTarihi
├── ProjeTipleri
├── Kurallar[]
└── KullaniciParametreleri

Kural
├── Kod
├── Aciklama
├── Seviye: Bilgi/Uyari/Kritik
├── GirdiTipleri
├── Kosul
├── SonucMesaji
└── CozumOnerisi
```

Kural sonuçları "uygun / uyarı / kritik / kontrol edilemedi" olarak ayrılmalıdır. "Kontrol edilemedi"
sonucu özellikle önemlidir; çünkü bazı mevzuat maddeleri yalnız geometriden değil, proje notu,
yerel imar durumu veya statik/mekanik veriyle değerlendirilebilir.

---

## 5. Önerilen Mimari Modül Haritası

Mimari ana başlık 31-39 aralığı BIM hedefiyle şöyle kurgulanmalıdır:

| # | Modül | Amaç | Öncelik |
|---|---|---|---|
| 31 | İhtiyaç Programı ve Mahal Şablonları | bina işlevi, kapasite, mahal listesi, alan/hacim hedefleri | 1 |
| 32 | Konsept Plan ve Parametrik Yerleşim | mahalleri plana yerleştirme, koridor/çekirdek üretimi, ilişki matrisi | 1 |
| 33 | Düşey Çekirdek, Şaft ve Sirkülasyon | merdiven, asansör, wc, şaft aks sürekliliği ve kaçış grafı | 1 |
| 34 | Yönetmelik Ön Kontrol Motoru | imar, yangın, erişilebilirlik, otopark, enerji, sığınak ön kontrolleri | 1 |
| 35 | 3D BIM Model ve Görselleştirme | kat/duvar/döşeme/açıklık hacimleri, 3D önizleme, dışa aktarım | 2 |
| 36 | Detay, Malzeme ve Yapı Elemanı | duvar/döşeme/tavan/kapı/pencere tipleri, standart uyumlu malzeme kütüphanesi | 2 |
| 37 | Mimari Pafta, Rapor ve Çizelgeler | mahal listesi, alan tablosu, kapı-pencere, finisaj, yönetmelik raporu | 2 |
| 38 | Cephe, Günışığı ve Kabuk Analizi | cephe açıklıkları, yönlenme, kabuk kararları, enerji/akustik ön veri | 3 |
| 39 | BIM Koordinasyon ve İhracat | elektrik/akustik/mekanik/keşif veri mutabakatı, IFC/glTF/DXF çıktı hedefleri | 3 |

Bu yapı, mimariyi EYE4PRO'nun diğer disiplinlerine veri veren ana tasarım katmanı yapar.

---

## 6. M31 - İhtiyaç Programı ve Mahal Şablonları

### 6.1 Amaç

Kullanıcının binanın işlevini seçip ihtiyaç listesini hızlıca oluşturmasını sağlamak.

### 6.2 Kullanıcı akışı

1. Yeni mimari proje açılır.
2. Bina işlevi seçilir.
3. Kapasite bilgileri girilir.
4. Sistem mahal ihtiyaç listesini önerir.
5. Kullanıcı metrekare, hacim, adet ve öncelikleri düzenler.
6. Sistem net/brüt alan, sirkülasyon ve teknik hacim hedeflerini hesaplar.
7. Program M32'ye aktarılır.

### 6.3 Üretilecek veri

```text
IhtiyacProgrami
├── BinaIslevi
├── ProjeTipi
├── Kapasite
├── HedefToplamAlan
├── MahalGereksinimleri[]
├── SirkulasyonOrani
├── TeknikAlanOrani
└── Notlar
```

---

## 7. M32 - Konsept Plan ve Parametrik Yerleşim

### 7.1 Amaç

M31'den gelen mahal listesini plan şemasına dönüştürmek.

### 7.2 Temel kabiliyetler

- Mahal bloklarını otomatik veya yarı otomatik yerleştirme.
- Koridorları dolaşım grafı olarak üretme.
- Dış cephe isteyen mahalleri cepheye yerleştirme.
- Islak hacimleri ve şaftları çekirdeğe bağlama.
- Mahaller arası yakınlık/uzaklık kurallarını kontrol etme.
- Kullanıcı sürükle-bırak düzenleme yaptığında kuralları tekrar çalıştırma.
- M50'ye `BinaOda`, `BinaKat`, `BinaAciklik`, `BinaCekirdek` olarak aktarma.

### 7.3 Önemli karar

M32 serbest çizim programı gibi başlamamalıdır. İlk sürümde blok tabanlı çalışmalı:

```text
Mahal gereksinimi -> dikdörtgen/çokgen blok -> koridor bağlantısı -> kat planı -> M50 geometri
```

Bu yaklaşım hızlı ürün verir ve kurallı tasarımı mümkün kılar.

---

## 8. M33 - Düşey Çekirdek, Şaft ve Sirkülasyon

### 8.1 Amaç

Binanın katlar arası çalışan çekirdek sistemini yönetmek.

### 8.2 Kapsam

- Ana merdiven.
- Yangın merdiveni.
- Asansör.
- Rampa.
- Tesisat şaftı.
- Elektrik şaftı.
- Mekanik şaft.
- Wc/ıslak hacim düşey hizası.
- Kaçış güzergahı grafı.

### 8.3 Kritik kontroller

- Merdiven ve asansör tüm gerekli katlarda devam ediyor mu?
- Şaftlar aynı aks üzerinde mi?
- Wc/ıslak hacimler şaftla ilişkili mi?
- Kaçış yolu kesintiye uğruyor mu?
- Kapı/koridor/merdiven ilişkisi yangın ön kontrolünde uyarı veriyor mu?

---

## 9. M34 - Yönetmelik Ön Kontrol Motoru

### 9.1 Amaç

Tasarım sürecinde mimara erken uyarı vermek.

### 9.2 İlk sürüm kontrolleri

- Adsız veya sınıflandırılmamış mahal.
- Minimum alan/ölçü hedefinden sapan mahal.
- Kapısız mahal.
- Dış cephe gerektirip cephe almayan mahal.
- Aynı aksta devam etmeyen merdiven/asansör/şaft.
- Kapasiteye göre eksik wc veya servis mahali.
- Koridor genişliği kullanıcı kural setine göre yetersiz.
- Erişilebilir wc/rampa/asansör gerekliliği için ön uyarı.
- Yangın kaçış güzergahı oluşturulamıyor uyarısı.
- Otopark/sığınak/teknik alan için "kontrol gerekli" uyarısı.

### 9.3 Dil

Uygulama "kesin uygundur" dememelidir. Doğru dil:

- "Ön kontrol uygun görünüyor."
- "Kullanıcı kural setine göre uyarı var."
- "Bu kontrol için ek veri gerekiyor."
- "Güncel yerel imar koşullarıyla doğrulayın."

---

## 10. M35 - 3D BIM Model ve Görselleştirme

### 10.1 Amaç

2D planı basit ama tutarlı 3D modele dönüştürmek.

### 10.2 İlk 3D kapsam

- Kat döşemeleri.
- Duvar prizması.
- Mahal hacimleri.
- Kapı/pencere boşlukları.
- Merdiven/asansör/şaft hacimleri.
- Çatı/kütle basit gösterimi.
- Kat kesiti ve mahal yüksekliği.

### 10.3 Dışa aktarım hedefleri

İlk sürümde görsel ve koordinasyon yeterlidir. Sonra:

- glTF: hızlı 3D önizleme ve paylaşım.
- OBJ: basit geometri dışa aktarımı.
- DXF: 2D plan çıktısı.
- IFC: uzun vadeli BIM veri değişimi hedefi.

---

## 11. M36 - Detay, Malzeme ve Yapı Elemanı

### 11.1 Amaç

Mimari elemanları standartlara ve disiplinler arası kullanıma uygun teknik nesnelere dönüştürmek.

### 11.2 Kapsam

- Duvar tipleri.
- Döşeme tipleri.
- Tavan tipleri.
- Kapı/pencere/doğrama tipleri.
- Islak hacim detay setleri.
- Cephe kaplama ve yalıtım setleri.
- Yangın, akustik, ısı ve su yalıtımı parametreleri.

### 11.3 Diğer modüllere katkı

- Akustik: duvar/kapı/pencere Rw ve ses yutma bilgisi.
- Elektrik: mahal tipi ve tavan/duvar malzemesine göre yerleşim varsayımları.
- Mekanik: hacim, kullanım tipi, ısı kaybı/kazancı için ön veri.
- Keşif: malzeme metrajları ve poz başlangıçları.

---

## 12. M37 - Pafta, Rapor ve Çizelgeler

Mimarın ihtiyaç duyacağı çıktı ailesi:

- İhtiyaç programı raporu.
- Mahal listesi.
- Net/brüt alan tablosu.
- Kat bazlı alan özeti.
- Kapı-pencere çizelgesi.
- Mahal finisaj çizelgesi.
- Malzeme/yapı elemanı listesi.
- Yönetmelik ön kontrol raporu.
- Revizyon fark raporu.
- 2D plan çıktısı.
- 3D görsel/koordinasyon çıktısı.
- CSV/XLSX/HTML/PDF dışa aktarım.

---

## 13. M50 ile Yeni İlişki

M50'nin rolü genişletilmelidir.

### 13.1 M50'de kalacak/geçecek temel işler

- DXF içe aktarma.
- Sıfırdan oluşturulan mimari planı bina modeline alma.
- Kat, oda, açıklık, duvar, döşeme, tavan, merdiven, asansör, şaft temel geometrisi.
- Id kararlılığı.
- Revizyon numarası.
- `.e4bina.json` kaydet/aç.
- Modüllere salt-okunur bina modeli sağlama.

### 13.2 Mimari modüllerde kalacak işler

- İhtiyaç programı.
- Mahal şablonları.
- Tasarım kural setleri.
- Parametrik yerleşim.
- Yönetmelik ön kontrolü.
- Detay/malzeme.
- 3D görselleştirme ve rapor.

Bu ayrım sayesinde M50 ortak çekirdek olur; mimari ise bu çekirdeğin akıllı tasarım katmanı olur.

---

## 14. Önerilen Core Veri Modeli Genişletmeleri

Mevcut `Core/Bina` modeli BIM hedefi için genişletilmelidir.

```text
BinaModeli
├── SemaSurum
├── GeometriRevizyon
├── ProjeBilgisi
├── ParselBilgisi
├── BinaIslevi
├── Katlar[]
├── Mahaller[]
├── Duvarlar[]
├── Dosemeler[]
├── Tavanlar[]
├── Acikliklar[]
├── Merdivenler[]
├── Asansorler[]
├── Saftlar[]
├── Cekirdekler[]
├── MalzemeAtamalari[]
└── KuralKontrolSonuclari[]
```

Yeni mimari domain için:

```text
MimariTasarim
├── IhtiyacProgrami
├── MahalSablonlari[]
├── IliskiMatrisi
├── YerlesimKurallari
├── YonetmelikProfili
├── MalzemeKutuphaneBaglantilari
├── PaftaAyarları
└── RaporAyarları
```

---

## 15. Uygulama Fazları

### Faz 1 - BIM programlama temeli

- `mimari/31-ihtiyac-programi-mahal-sablonlari/codex31.md` açılır.
- Bina işlevi ve kapasite ekranı yapılır.
- Mahal şablon kütüphanesi oluşturulur.
- İhtiyaç programı raporu üretilir.

### Faz 2 - Parametrik plan üretimi

- M32 konsept plan editörü başlar.
- Mahal blokları, koridorlar ve çekirdekler 2D olarak yerleştirilir.
- M50'ye aktarılabilen `BinaModeli` üretilir.

### Faz 3 - Çekirdek ve yönetmelik ön kontrolü

- M33 düşey çekirdek/şaft modeli eklenir.
- M34 kural motoru başlar.
- Merdiven, koridor, wc, şaft, erişilebilirlik ve yangın ön uyarıları eklenir.

### Faz 4 - 3D BIM model

- M35 ile duvar/döşeme/açıklık hacimleri üretilir.
- 3D önizleme ve basit dışa aktarım yapılır.

### Faz 5 - Detay ve malzeme

- M36 malzeme ve yapı elemanı kütüphanesi kurulur.
- Yangın, akustik, ısı, su yalıtımı ve metraj parametreleri bağlanır.

### Faz 6 - Rapor ve disiplin koordinasyonu

- M37 rapor/pafta çıktıları tamamlanır.
- M39 ile akustik, elektrik, mekanik ve keşif modüllerine veri aktarımı standartlaştırılır.

---

## 16. Net Öneri

Önceki rapordaki "M31 - Mahal Programı ve Alan Kontrolü" önerisi genişletilmelidir.
Yeni ilk modül şu olmalıdır:

```text
mimari/31-ihtiyac-programi-mahal-sablonlari/codex31.md
```

İlk MVP kapsamı:

- Bina işlevi seçimi.
- Kapasite ve hedef alan girişi.
- Mahal ihtiyaç listesi üretimi.
- Mahal şablon kütüphanesi.
- Alan/hacim hedefleri.
- Sirkülasyon/teknik alan oranı.
- Yönetmelik profili seçimi.
- İhtiyaç programı raporu.

İkinci adım hemen M32 olmalıdır:

```text
mimari/32-konsept-plan-parametrik-yerlesim/codex32.md
```

Bu modül mahalleri blok plan haline getirir, koridorları ve çekirdeği üretir, sonra M50 ortak
bina modeline aktarır.

Bu yol izlenirse EYE4PRO, yalnız DXF tüketen bir disiplin koordinasyon aracı olmaktan çıkar;
binayı programdan tasarlayan, yönetmelik ön kontrolü yapan, 3D BIM modele dönüştüren ve diğer
disiplinlere aynı veri omurgasını dağıtan bir mimari BIM platformuna evrilir.

