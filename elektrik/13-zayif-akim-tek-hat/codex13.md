# M13 Codex — Zayıf Akım Tek Hat Şeması

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, sistem davranışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M13 — Zayıf Akım Tek Hat Şeması |
| Amaç | ELV sistem merkezleri, rack/hat topolojileri, cihaz özetleri ve tek hat çıktıları |
| Proje uzantısı | `.e4p13` |
| Ana veri kaynağı | M12 `.e4p12` |
| Standart referansları | IEC 60364-8, TS HD 60364-8, IEC 61076 |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Geliştiriliyor; Data/Kamera/Anons/TV/Yangın/Telefon/Diafon akışları kullanılabilir |

---

## 2. Modül Kapsamı

Aktif kapsam:
- M12 Basit Proje Çiz dosyasından zayıf akım sistemlerini içe aktarma
- Data sistemi rack merkezli tek hat
- Kamera / CCTV sistemi rack merkezli tek hat
- Acil anons sistemi hat bazlı tek hat (+ tam analog 100 V dönüşümü)
- TV sistemi ana santral + çatı uydu/anten hatları
- Yangın algılama sistemi loop bazlı tek hat
- **Telefon sistemi**: her priz ayrı hat → Telefon Santrali (TV deseni, yıldız topoloji)
- **Diafon sistemi**: kat üniteleri (daire diafonu + kapı zili) → Diafon Santrali
- Sistem bazlı şematik canvas
- Kat bazlı düzen ve kat şeritleri
- Cihaz konsolidasyonu ve adet gösterimi
- Tekil hat metrajları
- `.e4p13` proje kaydet/aç + **SemaSurum şema sürümü/migrasyon** + **IsDirty (`*`) takibi**
- Aktif sisteme göre **DXF / SVG / PNG** çıktı (hepsi kaydetme diyaloğuyla)
- Data/Kamera **port-kanal kapasite denetimi** (switch/patch panel/NVR)
- Analog anons **EN 54-4 akü ön boyutlandırması** + mesaj senaryoları
- Yangın looplarında **tür bazlı cihaz zinciri editörü** + cihazlar arası manuel kablo segmenti metrajı
- Sol altta ayrı pencerede açılan **cihaz/kablo kütüphanesi**; yerleşikler salt okunur,
  kullanıcı kalemleri alt başlık bazında ekle/sil/düzenle ve `.e4p13` ile saklanır

İleri/eksik alanlar:
- Data özel import/DXF testlerinin daha ayrıntılı genişletilmesi
- Mesaj senaryolarına zon bazlı seçmeli atama (şimdilik "tüm zonları ata" var)

---

## 3. Genel Model Kuralları

Temel modeller:
- `ELVBlokRol`: Merkez, AraToplayici, Cihaz
- `ELVBlok`
- `ELVSayfa`
- `ELVKatInfo`
- `ZayifAkimTekHatProje`

Kurallar:
- M12 `BpcSistemKatalogu` ve `BpcSistemDevre` yapıları yeniden kullanılır.
- Katlar M12'den alınır ve büyükten küçüğe sıralanır.
- **Devresiz cihaz kuralı:** katta sistem devresi olsa bile hiçbir devreye atanmamış
  cihazlar (DataPriz/Kamera/Hoparlör/TvPriz/yangın cihazı) aktarımda kaybolmaz;
  "(Devresiz)" adlı ayrı rack/hat/loop altında toplanır. TV'de kattaki tüm prizler
  devre üyeliğinden bağımsız sayılır (her priz zaten ayrı hattır).
- Cihazlar kat ve tip bazında konsolide edilir; tekil blok yerine `Miktar` alanı kullanılır.
- Ana merkeze bağlı olmayan üst kat toplayıcı/cihazları otomatik olarak merkez bloğuna bağlanmalıdır.
- Layout kat bantları üzerinden yapılır.
- DXF çıktısında Y ekseni CAD görünümü için ters çevrilir.

---

## 4. Data Sistemi

Model:
- `ElvDataSistemi`
- `ElvMerkez`
- `ElvRackKabin`
- `ElvCihaz`
- `ElvHatMetraj`

Kurallar:
- Varsayılan data/uç birim kablosu **"Cat6 U/UTP"**dir (katalogla aynı ad; eski "Cat6 UTP"
  yazımı kaldırıldı, metraj özetinde tek grupta toplanır).
- Her kat için rack kabin oluşturulabilir.
- M12 Data devreleri rack kabine dönüştürülür.
- `DataPriz` elemanları `DataHatSayisi` olarak sayılır.
- Devresiz Data prizleri kat bazında tek rack altında toplanır.
- Wireless sayısı M12'den otomatik türetilmez; kullanıcı manuel girer.
- Data ve Wireless hatları tekil metraj satırları olarak tutulur.
- Varsayılan hat metraj listesi hat sayısıyla senkron kalmalıdır.
- Data DXF çıktısı ana merkez solda, kat rack kabinleri sağda olacak şekilde üretilir.

---

## 5. Kamera / CCTV Sistemi

Model:
- `ElvKameraSistemi`
- Rack kabinlerde `KameraSayisi`
- `KameraHatlari`

Kurallar:
- M12 `Cctv` devrelerindeki `Kamera` elemanları kat rack'lerine aktarılır.
- Devresiz kameralar kat bazında tek CCTV rack altında toplanır.
- Kamera hatları tekil metraj satırları olarak tutulur.
- Data ile aynı rack merkezli şema mantığını kullanır.
- CCTV cihaz kataloğu NVR, PoE switch, patch panel, medya dönüştürücü, kayıt sunucusu, UPS vb. içerir.
- Aktif sistem Kamera ise DXF çıktı `KameraDxfCiktiAl` yoluna gider.

CCTV izleme noktaları (2026-07-02):
- `ElvCctvIzlemeNoktasi` kamera sisteminde birinci sınıf düğümdür; bir projede birden fazla olabilir.
- Her izleme noktası ana CCTV merkeze (`BagliRackId = null`) veya herhangi bir CCTV rack kabinine bağlanabilir.
- Ad, kat, bağlantı kablo tipi/metrajı ve kendi `ElvCihaz` listesi `.e4p13` içinde saklanır.
- Sol panelde rack listesinin altında ayrı **İzleme Noktaları** listesi ve ekle/sil komutları vardır.
- Sağ panelden ad, kat, bağlı kabin, kablo ve cihazlar düzenlenir. İzleme noktası seçiliyken
  `ElvCihazKatalogu.CctvIzlemeNoktasiCihazlari` kullanılır: operatör/VMS iş istasyonu,
  joystick, KVM, decoder, video wall kontrol/matrix, 22–55 inç monitörler, video wall panelleri,
  ses birimleri, UPS/PDU, masaüstü switch ve medya dönüştürücü.
- Canvas'ta izleme noktaları rack'lerin sağında mavi düğüm olarak çizilir ve tıklanarak seçilir.
- İzleme noktası canvas Y konumu `KatNo` ile ilgili kat bandından hesaplanır; kat değişikliği
  `SemaRevizyon` ile yeniden ölçüm/çizim tetikler. Aynı kattaki noktalar kendi kolonunda alt alta
  dizilir ve kat bandı yüksekliği rack/izleme kolonlarının büyük olanına göre büyür.
- Kamera DXF/SVG çıktıları izleme düğümlerini, cihazlarını, bağlantı kablosunu ve metrajını içerir.
- Bağlı rack silinirse izleme noktası silinen rack'in üst bağlantısına aktarılır; kopuk referans bırakılmaz.

---

## 6. Acil Anons Sistemi

Model:
- `ElvAnonsSistemi`
- `ElvAnonsHatti`

Kurallar:
- Anons hoparlörleri geçişli hat mantığıyla modellenir.
- M12 `Anons` devreleri ayrı anons hattı kabul edilir.
- Her hattın `HoparlorSayisi`, kablo tipi ve metrajı ayrı düzenlenir.
- Devresiz hoparlörler kat bazında tek anons hattı altında toplanır.
- Ana merkez `Acil Anons Merkezi` olarak gösterilir.
- Cihaz kataloğu amfi, yedek amfi, mikser, acil mikrofon, bölge seçici, hat denetim modülü gibi öğeler içerir.
- Aktif sistem Acil Anons ise DXF çıktı `AnonsDxfCiktiAl` yoluna gider.

Analog sistem dönüşümü (Faz 1 tamamlandı):
- Eski `ElvAnonsHatti` / rack modeli geriye uyumluluk için korunur.
- Yeni analog 100 V domain modeli `ElvAnalogAnonsModels.cs` içindedir.
- Temel modeller: `ElvAnonsZonu`, `ElvAnalogHoparlorHatti` (A/B),
  `ElvAnalogHoparlorGrubu`, `ElvAnonsAmplifikatoru`, `ElvAnonsMesajSenaryosu`,
  `ElvAnonsGucKaynagi`.
- `ElvAnonsSistemi` yeni modeli `AnalogZonlar`, `AnalogAmplifikatorler`,
  `AnalogMesajSenaryolari`, `AnalogGucKaynagi` alanlarıyla taşır.
- Faz 1 yalnız domain + JSON uyumluluğudur; UI/canvas ve hesap motoru sonraki fazlardadır.
- Analog dönüşüm Faz 2 tamamlandı: `ElvAnalogHoparlorKatalogu.cs` içinde tavan,
  duvar/kabin, horn, projektör, kolon, line-array ve dış ortam 100 V şablonları bulunur.
- Her katalog kalemi izin verilen güç tap listesini, ortam sınıfını ve EN 54-24 ürün seçimi
  gereğini taşır. `ElvAnalogHoparlorServisi` katalogdan grup oluşturur; geçersiz tap seçimini
  reddedip önceki geçerli değeri korur.
- Faz 2 Core/katalog katmanıdır; UI seçicileri Faz 7 ile birlikte ele alınacaktır.
- Analog dönüşüm Faz 3 tamamlandı: `ElvAnalogAnonsTopolojiServisi` tek hat ve A/B çift
  hat oluşturur, katalog hoparlörlerini adet farkı en fazla bir olacak şekilde A/B'ye dağıtır.
- Hat B doluyken veri aktarımı açıkça istenmeden tek hatta dönüş reddedilir; aktarımda
  hoparlör grupları ve metraj Hat A'ya taşınır.
- Topoloji doğrulaması hat kolu, zorunlu Hat B ve A/B arasında yinelenen grup kimliklerini denetler.
- Analog dönüşüm Faz 4 tamamlandı: `ElvAnalogAnonsGucServisi` tap güçlerinden zon/hat
  yüklerini, varsayılan %20 rezervli amplifikatör gereksinimini ve kanal kullanımını hesaplar.
- Hat bazında amplifikatör ataması yapılır; zon amplifikatörü geriye uyumlu varsayılan atamadır.
- Aşırı yük, kanal aşımı, eksik/geçersiz atama ve normal işletme hattının yedek amplifikatöre
  atanması sorun olarak raporlanır.
- N+1 kontrolünde toplam yedek kapasitesi, en yüksek rezervli aktif amplifikatör yükünü karşılamalıdır.
- Analog dönüşüm Faz 5 tamamlandı: hatlarda denetim yöntemi (EOL, pilot ton, empedans),
  dal/EOL sayısı, güzergâh, kısa devre izolatörü ve hat durumu tutulur.
- `ElvAnalogAnonsHatDenetimServisi`; denetimsiz aktif hat, dal başına eksik EOL,
  geçersiz pilot ton, açık/kısa devre/toprak kaçağı ve FE180/NHXH/JE-H(St)H dışı kabloyu raporlar.
- A/B çift hat aynı güzergâh kodunu kullanamaz; bağımsız güzergâh zorunludur.
- Bilinmeyen hat durumu ve büyük hatta izolatör eksikliği uyarı; işlevsel kusurlar hata seviyesidir.
- Analog dönüşüm Faz 6 tamamlandı: `ElvAnalogAnonsAnaMerkezKatalogu` kontrol,
  amplifikatör, mikrofon, mesaj/ses, hat denetimi, yangın entegrasyonu, güç/akü ve rack
  kategorilerinde yapılandırılmış merkez cihazlarını taşır.
- Kontrol/mikrofon/hat denetimi/yangın I/O temel EN 54-16; güç/şarj ünitesi EN 54-4
  kalemi olarak işaretlenir. Amplifikatör katalog kalemleri güç ve kanal bilgisini taşır.
- `ElvCihaz.KatalogKodu` geriye uyumlu opsiyonel alandır; katalogdan cihaz ve ana/yedek
  amplifikatör üretilebilir.
- Analog dönüşüm Faz 7 tamamlandı: Analog modda sol panel eski rack listesi yerine tahliye
  zonlarını gösterir; kat seçimiyle varsayılan A/B zon eklenir.
- Sağ panelde A/B seçimi, katalog hoparlörü + adet + tap ile dengeli dağıtım, Hat A/B grup
  tabloları, katalogdan ana/yedek amplifikatör ekleme ve güç özeti bulunur.
- Analog canvas ana merkez, amplifikatörler, zonlar, 100 V Hat A/B ve hoparlör gruplarını
  ayrı renklerle çizer; zon kutusu canvas üzerinden seçilebilir.
- Aynı katalog kodu + tap + mahal notuyla tekrar eklenen hoparlör ayrı satır açmaz; mevcut
  A/B grup adetleri artırılır. Hat A ve Hat B grupları UI'dan seçilip silinebilir.
- Her analog hoparlör fiziksel zincirde bir `ElvAnalogKabloSegmenti` üretir. Panel/önceki
  hoparlör → sonraki hoparlör aralığı için metraj ayrı girilir; hat toplam metrajı segmentlerden hesaplanır.
- Analog dönüşüm Faz 8 tamamlandı: `ElvAnalogAnonsDogrulamaServisi` topoloji, EN 54-24,
  hoparlör tap gücü, segment sayısı, amplifikatör kapasitesi/N+1 ve hat denetimini tek raporda birleştirir.
- Sağ panelde **Analog Sistemi Doğrula** komutu hata ve uyarıları kaynaklarıyla listeler.
- Analog DXF çıktısı ana merkez, amplifikatör, zon, Hat A/B, hoparlör grubu ve hoparlörler arası
  segment metrajlarını ayrı `ZA-ANALOG-*` katmanlarında üretir.
- Analog dönüşümün 8 fazı tamamlandı; son M13 regresyon sonucu 41/41 başarılıdır.
- UI tamamlama (2026-07-02): Zon panelinde **Hat A/B amplifikatör atama** ComboBox'ları
  (`SeciliHatAAmplifikatorId`/`SeciliHatBAmplifikatorId`, boş "" = atanmamış/null; yalnız
  yedek olmayan amplifikatörler listelenir). Ana merkez panelinde **Seçili Amplifikatörü
  Sil** komutu; silinen amplifikatörün zon/hat atamaları otomatik null'a çekilir.
- Zon panelinde Hat A/B için **Hat Denetimi expander'ları**: denetim yöntemi (Yok/EOL/
  Pilot Ton/Empedans/EOL+Empedans), dal sayısı, EOL modül adedi, pilot ton frekansı,
  güzergâh kodu, kısa devre izolatörü. Faz 4-5 motorları artık UI'dan beslenebilir.
- **EN 54-4 akü ön boyutlandırması** (`ElvAnalogAnonsAkuServisi`): gerekli Ah =
  (bekleme akımı × 24 sa + alarm akımı × 30 dk) × 1.25; alarm akımı rezervli amplifikatör
  yükünden %65 verimle türetilir. 24 V sistemde 12 V aküler seri çift; adet tek ise uyarı.
  Merkez panelinde güç kaynağı alanları + akü özeti; sonuçlar birleşik doğrulamaya
  **uyarı** seviyesinde eklenir (kesin hesap üretici verisiyle).
- **Mesaj senaryoları UI**: merkez panelinde senaryo grid'i (ad/öncelik/mesaj/yangın tetik),
  ekle/sil ve "Seçili Senaryoya Tüm Zonları Ata" komutu.

---

## 6b. Telefon ve Diafon Sistemleri

Model:
- `ElvTelefonSistemi` (AnaMerkez "Telefon Santrali") — rack başına `TelefonPrizSayisi` + `TelefonHatlari`
- `ElvDiafonSistemi` (AnaMerkez "Diafon Santrali") — rack başına `DiafonUniteSayisi` + `DiafonUniteHatlari`

Kurallar:
- Telefon TV desenini izler: her priz devre üyeliğinden bağımsız ayrı hat ile santrale iner.
- Varsayılan telefon kablosu `J-Y(St)Y 2x2x0.6`, merkez omurgası `J-Y(St)Y 10x2x0.6`.
- Diafon üniteleri = M12 `Diafon` + `KapiZili` elemanları; `DiafonSantrali` merkez cihazıdır, sayılmaz.
- Varsayılan diafon kablosu `J-Y(St)Y 3x2x0.8`, merkez hattı `J-Y(St)Y 5x2x0.8`.
- Data sistemindeki `DiafonSayisi`/`DiafonHatlari` **IP tabanlı ek uçlardır**; analog diafon
  sistemi `DiafonUnite*` alanlarını kullanır — ikisi karıştırılmamalıdır.
- DXF katmanları `ZA-TELEFON-*` / `ZA-DIAFON-*`; SVG karşılıkları da üretilir.

---

## 6c. Kapasite Denetimi (Data/Kamera)

- `ElvKapasiteDenetimServisi` cihaz adlarındaki "N Port"/"N Kanal" kalıbını okur (Adet ile çarpar).
- Data: rack switch portları ve patch panel portları toplam uç sayısını (data+wifi+ek uçlar+
  dijital anons) karşılamalı; port bilgisi olan switch yoksa uyarı verir.
- Kamera: rack PoE/switch portu ≥ kamera sayısı; ana merkez NVR kanalları ≥ toplam kamera.
- Cihaz girilmemiş rack için konuşmaz (gürültü üretmez). UI: sağ panel "Kapasite Denetimi".

---

## 7. TV Sistemi

Model:
- `ElvTvSistemi`
- `TvHatlari`
- `ElvTvCatiHatti`

Kurallar:
- Her TV prizi ayrı koaksiyel hat satırıdır.
- TV hatlarında Cat kablo kullanılmamalıdır.
- Varsayılan TV priz hattı kablosu: `RG-6/U Koaksiyel`.
- Ana merkez `TV Ana Santral` olarak gösterilir.
- Ana santralden çatıdaki uydu/antenlere çıkan hatlar ayrıca tutulur.
- Çatıda her zaman en az 1 uydu/anten hattı bulunmalıdır.
- Varsayılan çatı hattı kablosu: `RG-11/U Koaksiyel`.
- M12 `Tv` devrelerindeki `TvPriz` elemanları aktarılır; devresiz TV prizleri de kat bazında eklenir.
- Aktif sistem TV ise DXF çıktı `TvDxfCiktiAl` yoluna gider.

### 7.1 TV Üç Teknoloji Dönüşümü (2026-07-02 — MVP uygulandı)

TV modülü üç ayrı dağıtım teknolojisi taşır. Bunlar yalnız kablo tipi değişen tek bir
`ElvTvSistemi` görünümü değildir; çalışma prensibi, topoloji, kapasite ve doğrulama kuralları farklıdır:

1. **Direkt Çok Çıkışlı LNB → Priz** (`DirektLnb`)
2. **Merkezi TV Santrali / Multiswitch → Koaksiyel Dağıtım** (`MerkeziKoaksiyel`)
3. **IP-TV / SAT&gt;IP → Ethernet (Cat/Fiber) Dağıtım** (`IpTv`)

Ortak kararlar:
- `TvDagitimTeknolojisi` enum'u bu üç değeri taşır; görünür Türkçe adlar UI katmanındadır.
- M12'den gelen TV prizleri önce teknolojiden bağımsız **ortak uç envanterine** (`ElvTvUcu`) alınır.
  Kat, mahal, uç adı ve kaynak M12 eleman kimliği burada korunur.
- Seçili teknoloji ortak uç envanterini kendi fiziksel topolojisine bağlar. Teknoloji değişiminde
  TV uçları kaybolmaz; teknolojiye özgü cihaz, hat ve metrajlar için kullanıcı onayıyla dönüşüm yapılır.
- Aynı projede aynı anda yalnız bir aktif TV teknolojisi vardır. Üç topolojinin verisi ayrı tutulur;
  teknoloji değiştirmek diğer topolojiyi sessizce silmez.
- Mevcut `.e4p13` TV verisi migrasyonda **Merkezi Koaksiyel** kabul edilir. `RackKabinler`,
  `TvHatlari` ve `CatiHatlari` bir geçiş sürümü boyunca okunur; yeni domain'e taşındıktan sonra
  legacy alanlar yalnız geriye uyumluluk için kalır.

#### 7.1.1 Sistem A — Direkt Çok Çıkışlı LNB

Çalışma prensibi:
- Uydu çanağı üzerindeki **Single/Twin/Quad/Octo gibi bağımsız kullanıcı çıkışlı LNB**'den her
  alıcı/tuner için ayrı RG-6 koaksiyel hat doğrudan TV prizi veya STB'ye gider.
- Birden fazla uydu/LNB varsa her kullanıcı hattında opsiyonel DiSEqC anahtarı bulunabilir;
  alıcı tek koaksiyel hat üzerinden seçilen uyduyu kumanda eder.
- Klasik universal LNB kullanıcı çıkışı pasif splitter ile birden fazla bağımsız tunere bölünmez;
  her tuner 13/18 V ve 0/22 kHz seçimini kendi hattında yapar.
- **Quattro LNB**, dört sabit polarite/bant (VL/VH/HL/HH) çıkışı verdiği için bu topolojide
  doğrudan prize bağlanmaz; Merkezi Koaksiyel/multiswitch topolojisinin kaynağıdır.
- Çift tuner/PVR uçları iki LNB çıkışı ve iki koaksiyel hat tüketir. SCR/dCSS tek-kablo çözümü
  ilk MVP'de Direkt LNB ile karıştırılmaz; ileri seçenek olarak ayrı kullanıcı bandı kapasitesiyle eklenir.

Planlanan modeller:
- `ElvDirektLnbSistemi`: çanaklar, LNB'ler, DiSEqC anahtarları, uç hatları.
- `ElvTvLnb`: uydu pozisyonu, LNB tipi, toplam/boş çıkış, bağlı çanak, desteklenen uydu.
- `ElvDirektTvHatti`: TV ucu, LNB/DiSEqC bağlantısı, çıkış no, tuner no, RG-6 tipi ve metraj.
- `ElvTvDiseqcAnahtari`: giriş sayısı, bağlı LNB'ler ve kullanıcı hattı.

Canvas:
`Çanak → LNB(ler) → [DiSEqC] → tekil RG-6 hat → TV prizi/STB`.
Rack veya TV santrali çizilmez. Her uç hattı ayrı gösterilir.

Doğrulamalar:
- LNB kullanıcı çıkış kapasitesi ≥ toplam tuner sayısı.
- Aynı fiziksel LNB çıkışı iki bağımsız hatta atanamaz.
- Quattro LNB doğrudan kullanıcı hattında hata verir.
- Çok uydulu uçta DiSEqC giriş/uydu eşlemesi eksikse hata verir.
- Hat metrajı ve kablo tipi zorunludur; Cat kablo reddedilir.

#### 7.1.2 Sistem B — Merkezi TV Santrali / Koaksiyel Dağıtım

Çalışma prensibi iki merkezi koaksiyel çözümü aynı ana topolojide destekler:
- **Multiswitch IF dağıtımı:** Quattro/WideBand LNB'nin uydu bant/polarite girişleri merkezi veya
  kaskat multiswitch'e gelir; her kullanıcı çıkışı ayrı koaksiyel hatla prize gider. Multiswitch
  yıldız veya kaskat çalışabilir.
- **İşlemeli headend/santral:** DVB-S/S2, DVB-T/T2 veya harici kaynaklar santralde alınır;
  transmodülatör/encoder/remultiplexer ile DVB-T/C/QAM benzeri ortak RF paketine çevrilir;
  yükselteç, splitter/tap ve kolon hatlarıyla TV prizlerine dağıtılır.

Planlanan modeller:
- `ElvMerkeziTvSistemi`: kaynaklar, ana santral, dağıtım düğümleri, uçlar.
- `ElvTvSantralTipi`: `MultiswitchIf` veya `IslemeliHeadend`.
- `ElvTvKaynak`: Uydu, karasal, kablo, HDMI/AV veya harici IP kaynak bilgisi.
- `ElvTvSantralCihazi`: multiswitch, transmodülatör, modülatör, encoder, PSU ve amplifikatör.
- `ElvTvDagitimDugumu`: ana/kaskat multiswitch, kat dağıtıcı, splitter, tap, yükselteç;
  üst düğüm kimliğiyle ağaç topolojisi.
- `ElvKoaksiyelTvHatti`: başlangıç/bitiş düğümü, RG-6/RG-11, metraj, kayıp ve seviye alanları.

Canvas:
`Çatı kaynakları → Ana santral/multiswitch → [kaskat/kat dağıtıcı] → prizler`.
Mevcut rack tabanlı TV görünümü doğrudan bu yeni düğüm ağının yerine kullanılmaz.

Doğrulamalar:
- Multiswitch giriş uyumu: uydu sayısı ve her uydu için gerekli polarite/bant girişleri.
- Kullanıcı çıkışı ≥ bağlı tuner/uç sayısı; kaskat bağlantıda giriş/çıkış döngüsü yasaktır.
- Headend kanal/program kapasitesi ve modül/PSU kapasitesi.
- 75 Ω sonlandırma, Cat kablo reddi ve bağlantısız dağıtım düğümü denetimi.
- Faz 2'de RF seviye hesabı: kablo frekans kaybı + splitter/tap kaybı + cihaz kazancı;
  her priz için hedef/minimum/maksimum dBµV raporu.

#### 7.1.3 Sistem C — IP-TV / SAT&gt;IP (Data Benzeri Yapı)

Çalışma prensibi:
- Uydu/karasal/HDMI/harici akış kaynakları **IP-TV headend/gateway** tarafından IP akışına çevrilir.
  SAT&gt;IP seçeneğinde dönüşüm alım noktasındaki SAT&gt;IP sunucusunda yapılır.
- Akışlar IPTV VLAN'ında core switch'e, oradan kat/alan erişim switch'lerine ve her Smart TV,
  IPTV STB veya yazılım istemcisine ayrı Cat hattıyla gider.
- Canlı lineer TV varsayılan olarak multicast taşınır. Uçlar IGMP ile kanala katılır/ayrılır;
  switch'lerde IGMP snooping, VLAN'da multicast router veya IGMP querier bulunur. Unicast/OTT
  akışlar ayrıca desteklenir fakat kapasite hesabı farklı yapılır.

Data sistemine benzerlik ve sınır:
- `ElvIpTvRack` ana merkez → kat rack → alt rack hiyerarşisini, `UstRackId`, kat şeritleri,
  ayrı bağlantı koridorları ve tekil uç hatları desenini Data'dan **ortak servis** olarak kullanır.
- IP-TV kendi `ElvIpTvSistemi` domain'ine sahiptir; Data `ElvRackKabin` listesiyle aynı mutable
  listeyi paylaşmaz. Böylece Data ve TV değişiklikleri birbirini bozmaz.
- `IpTvAltyapiModu`: `AyrikIpTvRack` veya `DataRackPaylasimli`. İlk MVP ayrık rack'tir.
- Paylaşımlı modda `FizikselDataRackId` ile Data rack'ine referans verilir; geometri/topoloji
  salt-okunur alınır, IPTV switch portu/uplink bant genişliği **rezervasyon** olarak hesaplanır.
  Data rack cihazları IPTV tarafından değiştirilmez.
- “Data Rack'lerinden Oluştur” komutu Data topolojisini snapshot olarak IP-TV rack'lerine kopyalar;
  sonrasında kullanıcı açıkça yeniden eşitlemedikçe bağımsızdır.

Planlanan modeller:
- `ElvIpTvSistemi`: ana merkez, rack'ler, kanal/akış kataloğu, VLAN ve multicast ayarları.
- `ElvIpTvAnaMerkez`: SAT&gt;IP/IPTV gateway, receiver/tuner, encoder/transcoder, middleware,
  CAS/DRM, EPG, origin/stream sunucusu, core switch, storage ve UPS cihazları.
- `ElvIpTvRack`: Id/ad/kat, `UstRackId`, opsiyonel `FizikselDataRackId`, erişim switch'leri,
  uplink tipi/metrajı/kapasitesi ve uç hatları.
- `ElvIpTvUcHatti`: ortak TV ucu, uç tipi (`SmartTv`, `Stb`, `YazilimIstemcisi`), switch portu,
  Cat6/Cat6A/fiber, metraj, VLAN, PoE gereği ve yedek hat bilgisi.
- `ElvIpTvAkisi`: kanal adı, kaynak, multicast grup IP'si/portu veya unicast URL kimliği,
  codec, çözünürlük, ortalama/tepe bitrate ve şifreleme bilgisi.
- `ElvIpTvAgAyarlari`: IPTV VLAN ID, IGMP sürümü, snooping, querier/router, fast-leave,
  QoS/DSCP, multicast/unicast çalışma modu ve kapasite rezervi.

IP-TV kapasite ve doğrulama motoru:
- Erişim switch kullanılabilir portu ≥ IPTV uç sayısı + kullanıcı rezervi.
- Bakır hat tasarım eşiği varsayılan 90 m permanent-link / 100 m channel; eşikler ayarlanabilir.
- Multicast modunda rack uplink ihtiyacı, o daldaki eşzamanlı **benzersiz kanal** akışlarının
  bitrate toplamı + protokol/rezerv yüzdesidir; uç sayısı × tüm kanal bitrate'i körlemesine yapılmaz.
- Worst-case ön kontrol: en yüksek bitrate'li `min(eşzamanlı benzersiz kanal, uç sayısı)` akışın toplamı.
- Unicast modunda eşzamanlı uçların akış bitrate toplamı kullanılır.
- Core/headend çıkışı, rack uplink'i ve switch backplane kapasitesi ayrı ayrı denetlenir.
- Multicast varsa IGMP snooping zorunlu; L2 alanda multicast router yoksa querier zorunlu.
- VLAN tanımsızlığı, yinelenen multicast grup/port, geçersiz grup adresi, eksik gateway,
  bağlantısız rack/uç, port çakışması ve uygunsuz Cat/koaksiyel kablo hata üretir.
- PoE yalnız gerçekten PoE beslenen STB/gateway uçlarında hesaba katılır; Smart TV/STB için
  varsayılan `PoeGerekli=false`'dır.
- İleri faz: çift core, N+1 headend/gateway, yedek uplink ve servis sürekliliği senaryosu.

IP-TV UI:
- Üstte teknoloji seçimi: Direkt LNB / Merkezi Koaksiyel / IP-TV.
- Sol: IP-TV Ana Merkez + rack ağacı; her rack'in kat, uç, port ve uplink özeti.
- Orta: Data'ya benzer kat bantlı canvas; ana merkez solda, rack'ler ortada, Smart TV/STB
  uçları sağda; rack→rack ve rack→uç kabloları farklı renk/katmanda.
- Sağ: seçili merkez/rack/uç cihazları, üst bağlantı, fiziksel Data rack referansı, switch portu,
  Cat/fiber hattı, metraj ve kapasite özeti.
- Ayrı “Kanal ve Akışlar” panelinde multicast grup/port, codec ve bitrate düzenlenir.
- Kütüphane alt başlıkları ileride `TV — Direkt LNB`, `TV — Merkezi Koaksiyel` ve
  `TV — IP-TV` olarak ayrılır; her biri yalnız kendi cihaz ve kablolarını gösterir.

#### 7.1.4 Import, Çıktı ve Teknoloji Değişimi

M12 import:
- M12 `TvPriz` elemanları ortak `ElvTvUcu` envanterine bir kez alınır.
- Direkt LNB: her tuner için radial koaksiyel hat oluşturulur, LNB çıkış ataması bekler.
- Merkezi Koaksiyel: uçlar kat dağıtım düğümüne/santrale bağlanır, varsayılan RG-6 atanır.
- IP-TV: kat başına varsayılan IP-TV rack ve her uç için Cat6A hattı oluşturulur; switch portu
  otomatik sıra alır. Data rack kopyası ancak kullanıcı komutuyla yapılır.

Teknoloji değişimi:
- Ortak TV uçları korunur.
- Hedef topolojide eksik düğümler varsayılan olarak üretilir; kaynak topoloji silinmez.
- Koaksiyel metraj Cat metrajına veya tersi otomatik çevrilmez; kullanıcıya açık dönüşüm özeti
  gösterilir ve yeni hatlar “metraj bekliyor” işareti alır.

Çıktılar:
- Direkt: `ZA-TV-DIREKT-*`; çanak/LNB/DiSEqC, tekil tuner hatları ve metrajlar.
- Merkezi: `ZA-TV-MERKEZI-*`; kaynak, santral/multiswitch, dağıtım düğümü, RF hatları.
- IP-TV: `ZA-TV-IP-*`; headend/core, rack/uplink, uç Cat hatları, VLAN/port ve bitrate özeti.
- DXF ve SVG aynı layout servisini kullanır; PNG aktif canvas görüntüsüdür.
- Metraj özeti teknolojiye göre koaksiyel, Cat/fiber ve cihaz adetlerini ayrı gruplar.

#### 7.1.5 Uygulama Fazları

2026-07-02 uygulama durumu: F0-F2, F5-F6 ve F8-F9 tamamlandı. F3'ün mevcut merkezi
koaksiyel modeli teknoloji seçicisine bağlandı. F4'teki ayrıntılı RF dBµV motoru ile F7'deki
Data rack'ine canlı fiziksel referans ileri geliştirme olarak bırakıldı.

| Faz | İçerik | Kabul ölçütü |
|---|---|---|
| TV-F0 | `TvDagitimTeknolojisi`, ortak `ElvTvUcu`, üç topoloji kabuğu, JSON v3 migrasyonu | Eski TV projesi Merkezi Koaksiyel olarak kayıpsız açılır |
| TV-F1 | Direkt LNB domain + import dönüşümü + çıkış/tuner doğrulaması | Quad/Octo kapasitesi, Quattro reddi, DiSEqC topolojisi testleri |
| TV-F2 | Direkt LNB UI/canvas + DXF/SVG + kütüphane | Her priz/tuner radial hat ve metrajla görünür |
| TV-F3 | Merkezi Koaksiyel santral/multiswitch/dağıtım düğümü domain'i | Yıldız/kaskat ağaç ve kapasite doğrulaması |
| TV-F4 | Merkezi UI/canvas/çıktı; temel RF kayıp/seviye motoru | Uç bazlı dBµV ve bağlantı raporu |
| TV-F5 | IP-TV domain, ana merkez, rack hiyerarşisi, uç Cat hatları | Data benzeri topoloji ve port ataması |
| TV-F6 | IPTV VLAN/IGMP/akış kataloğu + bant genişliği motoru | Multicast/unicast kapasite testleri ve sorun raporu |
| TV-F7 | IP-TV UI/canvas + Data rack snapshot/paylaşımlı referans | Data'yı değiştirmeden fiziksel rack eşleme |
| TV-F8 | Üç teknoloji DXF/SVG/PNG, metraj ve kütüphane ayrımı | Aktif teknolojiye özel doğru katman/özet |
| TV-F9 | Teknoloji dönüşüm diyaloğu, regresyon, belge | TV uçları hiçbir geçişte kaybolmaz |

#### 7.1.6 Teknik Dayanaklar

- SAT&gt;IP, yayın sinyalini alım noktasındaki sunucuda IP'ye çevirip IP istemcilerine dağıtan
  mimaridir; CENELEC EN 50585 ile ilişkilidir: https://www.ses.com/press-release/satip-alliance-continues-drive-future-satellite-content-distribution-multiscreen
- DVB-IPTV çekirdek uygulama kılavuzu ETSI TS 102 034'e eşlik eder:
  https://dvb.org/?standard=guidelines-for-the-implementation-of-dvb-ip-phase-1-specifications-part-1-core-iptv-functions
- IGMP, multicast grup üyeliğini; IGMP snooping ise akışın yalnız ilgili switch portlarına
  iletilmesini sağlar. L2 multicast alanda router yoksa querier gerekir:
  https://www.cisco.com/c/en/us/td/docs/switches/lan/c9000/multicast/multicast-configuration-guide/igmp.html
- Multiswitch yıldız/kaskat çalışabilir; bir uydu için karasal + dört bant/polarite girişi kullanır:
  https://www.televes.com/uk/tv-distribution/multiswitches/nevoswitch-series.html
- Quattro LNB'nin dört çıkışı sabit VL/VH/HL/HH bant/polariteleridir:
  https://www.televes.com/en/lnb-quattro-quattro-lnb-offset-4-outputs-hh-vh-hl-vl.html
- İşlemeli headend DVB-S/S2 girişini IP, DVB-T veya DVB-C/QAM çıkışlarına dönüştürebilir:
  https://www.televes.com/en/tv-distribution/processing-headends/t-0x-series/transmodulators-satellite-input-smatv.html

---

## 8. Yangın Algılama Sistemi

Model:
- `ElvYanginSistemi`
- `ElvYanginKatLoopGrubu`
- `ElvYanginLoop`

Kurallar:
- Yangın sistemi rack/pano mantığı kullanmaz.
- Her kat/hata bir veya daha fazla loop olabilir.
- Her loop doğrudan ana yangın algılama santraline gider.
- Varsayılan kablo: `J-Y(St)Y 1x2x0.8 Yangın Kablosu`.
- Cat kablo kullanılmamalıdır.
- M12 `Yangin` devreleri ayrı loop kabul edilir.
- Devre yoksa kattaki yangın cihazları tek loop altında toplanır.
- Duman, ısı, kombine, asma tavan dedektörü, buton, siren ve flaşör cihaz sayısına dahil edilir.
- DXF çıktısında yangın santrali solda, looplar sağda çizilir; rack çizilmez.

---

## 9. Layout ve Görselleştirme Kuralları

Yangın loop cihaz zinciri (2026-07-02):
- Yangın canvas'ının altında, seçili loop için yatay cihaz zinciri editörü açılır.
- Palet tipleri: duman, ısı, kombine ve asma tavan dedektörü; yangın ihbar butonu; siren; flaşör.
- Palet simgesine basmak cihazı seçili loop'un sonuna ekler; seçili cihaz silinebilir.
- Her cihazın `OncekiCihazaMesafeMetre` değeri, ilk cihazda loop başlangıcı→cihaz; diğerlerinde
  önceki cihaz→cihaz kablo segmentidir. Loop `Metraj` değeri segment toplamından otomatik hesaplanır.
- Segment metrajı spinner içermeyen düz TextBox ile girilir; yazım sırasında genel `Yenile()`
  çağrılmaz, böylece odak/seçili loop ve alt editör kapanmaz.
- Zincir cihaz kartları tipe özgü simge kullanır; cihaz seçimi `ItemsControl` içindeki karttan yapılır
  (ListBox seçili öğesinin geniş yeşil arka planı kullanılmaz).
- `ElvYanginLoop.Cihazlar` fiziksel zinciri taşır; `CihazSayisi` zincirden hesaplanır.
- Eski yalnız-adet/metraj `.e4p13` loop'u ilk yeni cihaz eklemesinde `Bilinmeyen` tipli düğümlere
  dönüştürülür; eski adet ve toplam metraj kaybolmaz.
- M12 importu cihaz türünü koruyarak `ElvYanginLoopCihazi` üretir.

- Kat bazlı layout kullanılır.
- Yangın şemasında kat bandı yüksekliği kattaki loop sayısına göre büyür
  (`YanginKatYukseklikleriniHesapla`); `MeasureOverride` ile çizim aynı sözlüğü kullanır,
  çok katlı/çok loop'lu projede kesilme veya banda taşma olmaz.
- Yangın santrali→loop kablolarında her loop ayrı santral Y çıkışı ve ayrı düşey X koridoru
  kullanır; hatlar üst üste binmez. Aynı offset geometrisi canvas, DXF ve SVG'de korunur.
- Blok X konumları role göre hizalanır:
  - Merkez: sol kolon
  - Ara toplayıcı/rack: orta kolon
  - Cihaz/hat grupları: sağ kolonlar
- Kat şeritleri alternatif arka planla gösterilir.
- Cihaz bloklarında miktar öneki gösterilir: örn. `5x Data Prizi`.
- Şematik alan `ScrollViewer` içinde olmalı; çok kat/çok cihaz taşmasında kaydırma çıkmalıdır.
- Data/Kamera/Anons/TV/Yangın aktif sisteme göre aynı ekran dinamik çalışmalıdır.
- `SemaRevizyon` benzeri sinyal ile düzenleme sonrası çizim yenilenmelidir.

---

## 10. DXF Çıktı Kuralları

- DXF üretiminde `netDxf` kullanılır.
- DXF etiketlerinde de Türkçe karakterler doğru yazılır ("Ayrı uç", "Hoparlör", "Erş");
  netDxf `Text` unicode destekler, ASCII'ye düşülmez.
- `DxfCiktiAl(proje, ...)` (ELVSayfa tabanlı) **legacy yoldur**; UI yalnız sistem-özel
  metodları çağırır, legacy yol testlerde yaşar. Sayfalar yan yana X-ofsetiyle çizilir,
  yerleşimsiz bloklar önce `OtomatikYerlestir`'den geçirilir.
- `Polyline2D` ve `Polyline2DVertex` sınıfları kullanılmalıdır; `LwPolyline` kullanılmamalıdır.
- `AciColor.White` yoktur; uygun durumda `AciColor.Default` veya açık renk eşlemesi kullanılmalıdır.
- Her sayfa/sistem için bloklar, bağlantılar, kat çizgileri ve etiketler çizilir.
- Kat çizgileri `ZA-KAT-CIZGI` gibi ayrı katmanda tutulur.
- Cihaz adetleri DXF etiketlerine yansıtılır.
- Aktif sistem neyse DXF çıktısı ilgili özel metoda yönlenmelidir:
  - Data
  - Kamera
  - Anons
  - TV
  - Yangın

---

## 11. UI İş Akışı

- Electric modül merkezinden M13 açılır.
- Üst barda sistem seçimi bulunur: Data, Kamera, Acil Anons, TV, Yangın, Telefon, Diafon.
- `M12'den İçe Aktar` ile `.e4p12` seçilir.
- `Kaydet` ile `.e4p13` kaydedilir; başlıkta kaydedilmemiş değişiklik `*` ile gösterilir.
- Açılışta `ZayifAkimTekHatMigrasyonServisi` şema sürümünü denetler/taşır; daha yeni
  sürümden gelen dosya açılmaz.
- `DXF / SVG / PNG Çıktı` aktif sisteme göre üretir; üçü de kaydetme diyaloğu açar.
  PNG canvas'ın 2x ölçekli birebir görüntüsüdür; SVG Core'daki `ZayifAkimTekHatSvgServisi`
  ile DXF yerleşimini aynalar.
- Sol panel aktif sisteme göre merkez/rack/loop grup listesini gösterir.
- Orta panel aktif sistem şemasını çizer.
- Sağ panel seçili rack/merkez/loop için cihaz ve hat düzenleme alanıdır.
- Görünür tüm metinlerde Türkçe karakterler doğru kullanılmalıdır.

### 11.1 M13 Cihaz ve Kablo Kütüphanesi (2026-07-02)
- Sol panelin en altında **Kütüphane** butonu bulunur; `ZayifAkimKutuphaneWindow` ayrı modal açılır.
- Alt başlıklar: Data, Kamera/CCTV, CCTV İzleme Noktası, Acil Anons Analog, Acil Anons
  Dijital (PoE), TV, Yangın Algılama, Telefon ve Diafon.
- Her alt başlıkta cihazlar ve kullanılan kablolar yan yana listelenir.
- `ElvCihazKatalogu` kaynaklı yerleşik satırlar **salt okunur** ve silinemez.
- Kullanıcı satırları eklenebilir, adı doğrudan düzenlenebilir ve silinebilir.
- Kullanıcı kalemleri `ElvOzelKutuphaneKalemi` olarak `ZayifAkimTekHatProje.OzelKutuphaneKalemleri`
  içinde saklanır; proje şeması bu nedenle **v2**'dir. Kayıtlar alt başlık kodu + cihaz/kablo türü taşır.
- `ElvKutuphaneServisi` yerleşik ve kullanıcı kayıtlarını tekilleştirip aktif `CihazKatalogu` /
  `KabloKatalogu` listelerine birleştirir. Data ana merkez/rack ve dijital anons bağlam ayrımı korunur.

---

## 12. Kritik Dikkat Notları

- Data `WirelessSayisi` M12'den otomatik alınmıyor; manuel alan olduğu unutulmamalıdır.
- Yangın sistemi rack mantığına sokulmamalıdır.
- TV sistemi Cat kabloya düşmemelidir; koaksiyel kablo mantığı korunmalıdır.
- Çatı uydu hattı sayısı minimum 1 olmalıdır.
- Hat sayısı değişince tekil hat metraj listeleri senkronlanmalıdır.
- Konsolidasyon sonrası testlerde blok sayısı yerine `Miktar` kontrol edilmelidir.
- DXF Y ekseni tersleme korunmalıdır; CAD'de şema yukarıdan aşağı doğru görünmelidir.
- AXAML'de `&` karakteri `&amp;` olarak kaçırılmalıdır.
- `StackPanel` doğrudan `Padding` almaz; gerektiğinde `Border` wrapper kullanılmalıdır.
- ViewModel'de `Proje` gibi ObservableProperty atamaları `Task.Run` içinde yapılmaz;
  arka planda yüklenir, atama UI thread'inde yapılır (PropertyChanged UI'da ateşlenmeli).
- Import'ta devresiz cihazlar "(Devresiz)" rack/hat/loop'una gider; bu davranış
  `ImportServisi_DevreVarken_DevresizCihazlarKaybolmaz` testiyle korunur.

---

## 13. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ZayifAkimTekHatModels.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ZayifAkimTekHatImportServisi.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ZayifAkimTekHatLayoutServisi.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ZayifAkimTekHatDxfServisi.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ZayifAkimTekHatSvgServisi.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ZayifAkimTekHatMigrasyonServisi.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ElvKapasiteDenetimServisi.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ElvAnalogAnonsAkuServisi.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ElvDataModels.cs`
- `src/Kst.Acoustics.Core/Electric/ZayifAkimTekHat/ElvCihazKatalogu.cs`

App:
- `src/Kst.Acoustics.App/Electric/ZayifAkimTekHat/ZayifAkimTekHatWindow.axaml`
- `src/Kst.Acoustics.App/Electric/ZayifAkimTekHat/ZayifAkimTekHatWindow.axaml.cs`
- `src/Kst.Acoustics.App/Electric/ZayifAkimTekHat/ZayifAkimTekHatViewModel.cs`
- `src/Kst.Acoustics.App/Electric/ZayifAkimTekHat/ELVSematikControl.cs`
- `src/Kst.Acoustics.App/Electric/ZayifAkimTekHat/ElvDataSemaControl.cs`

Test:
- `src/Kst.Acoustics.Tests/ZayifAkimTekHatTests.cs`

---

## 14. Korunması Gereken Regresyon Alanları

- M12 import temel blok yapısı
- Kat bilgisinin aktarılması ve sıralanması
- Cihaz konsolidasyonu ve `Miktar`
- Layout role göre X ve kat bandına göre Y yerleşimi
- L-şekilli bağlantı hesabı
- DXF dosya üretimi
- DXF Y ekseni çevrimi
- Data rack import ve metraj listesi
- Kamera rack import ve kamera hatları
- Anons hat grubu ve hoparlör sayıları
- TV koaksiyel hatları ve çatı uydu hattı minimumu
- Yangın loop grupları ve doğrudan santral bağlantısı
- Devresiz cihazların "(Devresiz)" rack/hat/loop olarak aktarımı
- Analog amplifikatör hat ataması ve silmede atama temizliği
- Telefon priz hatları ve diafon ünite hatları importu (santral cihazı sayılmaz)
- Şema sürümü migrasyonu (Cat6 UTP → Cat6 U/UTP) ve yeni sürüm reddi
- Kapasite denetimi port/kanal ayrıştırma
- EN 54-4 akü hesap eşiği

---

## 15. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet test src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj --filter "FullyQualifiedName~ZayifAkimTekHat"
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M13 regresyonları geçmeli (son sonuç: 50/50)

---

## 16. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. M12 import değişikliği için `ZayifAkimTekHatImportServisi` dosyasını incele.
4. Sistem modeli değişikliği için `ZayifAkimTekHatModels` ve `ElvDataModels` dosyalarını kontrol et.
5. Layout/çizim değişikliği için layout servisi, `ELVSematikControl` ve `ElvDataSemaControl` dosyalarını incele.
6. DXF değişikliği için `ZayifAkimTekHatDxfServisi` ve ilgili testleri kontrol et.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.
