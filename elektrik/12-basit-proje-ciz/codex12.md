# M12 Codex — Basit Proje Çiz

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, iş akışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M12 — Basit Proje Çiz (Kuvvetli Akım) |
| Amaç | DXF mimari plan üzerinden oda tespiti, **kuvvetli akım** elemanı yerleşimi, kablolama, pano/linye özeti ve DXF çıktı |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Proje uzantısı | `.e4p12` |
| Durum | Geliştiriliyor |

> **M16 ayrışması (2026-06-13):** Zayıf akım proje çizimi **M16 Zayıf Akım Proje Çiz**
> modülüne taşındı — bkz. `elektrik/16-zayif-akim-proje-ciz/codex16.md`. M12 ve M16
> **ortak motoru** paylaşır (`BasitProjeCizWindow`/`BasitProjeCizViewModel`, kurucuya
> `BpcDisiplin modulModu`); kod fork edilmedi. M12 yalnız kuvvetli akımda çalışır,
> M16 yalnız zayıf akımda (`.e4p16`). Disiplin sınıflaması `BpcSistemKatalogu.Disiplini*`;
> ayıklama `DisiplinAyiklamaServisi`. M12 eski karma `.e4p12` açarsa zayıf akımı
> ayıklayıp uyarır (geometri korunur). Aşağıdaki zayıf akım bölümleri **ortak motor**
> referansıdır; M12 UI'ında zayıf akım gizlidir.

---

## 2. Modül Kapsamı

> **M50 yönlendirmesi (2026-06-13):** Bina geometrisi (DXF kat yükleme, kat/oda/açıklık
> düzenleme, kat hizalama, döşeme/tavan patlatma) **M50 Bina Geometrisi** modülüne taşındı —
> bkz. `ortak-moduller/50-bina-geometri/codex50.md`. M12 geometriyi `.e4bina` dosyasından
> salt-okunur tüketir ("Bina Geometrisi Yükle"); eski `.e4p12` projeleri açılışta otomatik
> migrate edilir. Aşağıdaki geometri bölümleri tarihsel referanstır.

Aktif kapsam:
- Bina geometrisi tüketimi: `.e4bina` yükleme + mutabakat raporu (düzenleme M50'de)
- Mahal listesi ve otomatik eleman yerleştirme (patlatılmış/tavanı açık odada tavan
  montajlı otomatik yerleşim atlanır — codex50 §3.7)
- Manuel eleman ekleme, seçme, çoklu seçme, taşıma, silme
- Aydınlatma, priz, pano, buat, klima ve zayıf akım elemanları
- Aydınlatma ve pano/priz kablolama akışları
- Kablo tavası, besleme hattı ve topraklama çizimleri
- DXF proje çıktısı ve DXF şablon üretimi
- M11 Tek Hat Şeması'na `.e4p11` aktarım
- Undo/redo ve pafta/sistem görünüm yönetimi (elemanlar + devreler + kablolar + vavien grupları)

Model sınırları:
- Armatür otomatik yerleşiminde güncel karar yalnız iki yoldur: grid dizilim ve manuel ekleme.
- Eski koridor ekseni, oda merkezi, alan bazlı adet ve özel lineer koridor algoritmaları aktif tasarım kararı değildir.
- Zayıf akım elemanları DXF çıktısında anlık blok/katman olarak üretilir; Tek Hat aktarımına dahil edilmez.
- Omurga kablolama yalnız daisy chain'dir; paralel omurga kablolama iptal edilmiştir.

---

## 3. Veri ve Koordinat Kuralları

- **IsDirty (2026-07-18, maincodex §4.2):** Kaydedilmemiş değişiklik `IsDirty` ile izlenir;
  pencere başlığında `*` gösterilir. Pencere kapatma / Yeni Proje / Proje Aç / Ana Menü
  kirli projede "Kaydet / Kaydetmeden Devam Et / İptal" sorar (`OnayDiyaloguWindow`, M08 deseni).
  `ElemanSnapshotAl` merkezî `Kirlet()` noktasıdır; mahal listesi düzenlemeleri pencereden
  (INPC aboneliği + açık jestler) kirletir. Kaydet/Aç/Yeni `TemizIsaretle()` çağırır.
- Eleman ve oda poligonları lokal koordinatta saklanır: `el.X`, `el.Y`, `oda.Poligon`.
- Render sırasında `OriginOffset` eklenerek dünya koordinatına geçilir.
- Viewport tıklaması dünya koordinatı döndürür; işlem yaparken lokal koordinata çevrilmelidir:
  `localX = worldX - originX`, `localY = worldY - originY`.
- Eski `.e4p12` projeleri eksik alanlar için güvenli varsayılanlarla açılmalıdır.
- Kat planı içe aktarma, oda silme, eleman taşıma/silme gibi işlemler undo snapshot almalıdır.
- Kablo çekme (mahal ve kat otomatik) işlemleri de undo snapshot almalıdır; snapshot elemanlar, devreler, kablolar, vavien grupları, priz/mekanik/sistem devreleri, buat-pano bağlantıları ve (2026-07-18'den itibaren) **mahal listesi, kablo tavaları, pano besleme hatları ve topraklamayı** içerir. `CloneEleman` tüm alanları (dahil `TaliPanoHedefPanoId`) kopyalamalıdır.
- Kilitli projede silme/düzenleme sessizce yutulmamalı; kullanıcıya durum mesajı verilmelidir.

---

## 4. DXF Import ve Oda Tespiti *(M50'ye taşındı — tarihsel referans)*

- DXF mimari plan kat bazlı yüklenir.
- ARC entity'leri kapı tespitinde kullanılabilir; INSERT içi ARC/segment verisi aynı koordinat sistemine normalize edilir.
- Açıklık tespiti iki kaynaktan gelir:
  - Kapı yayı / arc tabanlı tespit
  - Duvar boşluğu / gap analizi
- Manuel eklenen veya iptal edilen açıklıklar yeniden taramada korunur.
- Otomatik açıklıklar oda yüzeyi dışında kalıyorsa temizlenir; manuel ve iptal kayıtları etkilenmez.
- Açıklık tespit parametreleri kullanıcı tarafından ayarlanabilir:
  - Minimum açıklık genişliği
  - Söğe arama ucu
  - Minimum söğe boyu
  - Maksimum karşı duvar mesafesi
- Presetler: Sıkı, Dengeli, Gevşek.
- Sağ tık context menüsü açıklık üzerinde iptal, sil ve tüm gap açıklıklarını temizle işlemlerini destekler.

---

## 5. Yerleşim Kuralları

- Armatürler için güncel otomatik yerleşim grid tabanlıdır.
- Armatür kütüphanesi yüklenirken/kaydedilirken armatür tanımları `GridDizilim` değerine normalize edilir.
- Manuel ekleme her zaman desteklenir.
- Priz, anahtar, pano, buat ve klima gibi duvar snap'i gereken elemanlar duvar yakınına yerleşir.
- Anahtar/komütatör yerleşiminde kapı açıklığına yakın adaylar tercih edilir.
- Anahtar çoğaltma: Bir odada armatür sayısı anahtarın kapasitesini aştığında sistem otomatik olarak fiziksel kopya anahtar üretir.
  - Kapasite limitleri: Normal anahtar/Vavien = 9, Komütatör/KomütatörVavien = 18, Üçlü Komütatör = 27, Darbe Anahtarı = sınırsız.
  - Darbe anahtarında minimum 2 adet üretilir.
  - Kopyalar **en yakın duvara paralel** olarak 0.15 m aralıklarla dizilir (`EnYakinDuvarVektoru` algoritması).
  - Her kopya bağımsız bir `BasitProjeEleman` nesnesidir ve kendi devrelerine sahiptir.
- Mahal listesinden otomatik yerleştirmede sembol tanımı eksikse uygun varsayılan tanım fallback olarak kullanılmalıdır.
- **İki ayrı sensör kavramı vardır** (2026-06-17):
  1. **Armatür-başına sensör** (oda-özel paneldeki "Sensör" checkbox, yalnız `ElemanArmaturMi` satırlarında): her armatüre ayrı sensör üretir, armatür duvar anahtarı yerine doğrudan buata bağlanır (`SensorKontrollu = true`).
  2. **Bağımsız sensör** (mahal listesi satırı, `E4P_SENSOR` / `BpcElemanTipi.Sensör`): tavan montajlı, grid/adet ile yerleşen bağımsız sembol. `ElemanArmaturMi` false döndüğü için anahtara bağlanmaz. `Sensör` tipi `MahalListesiWindow.KuvvetliTipler` dizisinde **olmak zorundadır**; yoksa kalem ekle dropdown'ında görünür ama satır tabloda filtrelenir → "sensör eklenmiyor / listeden kayboluyor" hatası oluşur.
- **KuvvetliTipler genel kuralı (2026-07-18):** Kütüphanede sembolü olan (`TipBlokAdindan`'ın dönebildiği) HER kuvvetli akım tipi `KuvvetliTipler` dizisinde olmalıdır. 3'lü Komütatör / Komütatör Vavien / Deviatör / Kapaklı-Sıvaüstü UPS / Glop-Çıplak-Bant-WallWasher armatür tipleri bu sebeple eklendi; yeni sembol tipi eklerken bu dizi güncellenmelidir (aksi halde "ekledim, kayboldu" hatası).
- **Mahal hücre etkileşimi (2026-07-18):** İlk tık hücreyi yalnız SEÇER (özellik paneli açılır; kilitli projede salt-okunur inceleme serbest). Zaten seçili hücreye ikinci tık aktif/pasif değiştirir. Pasifleşen hücrenin `Adet` değeri SIFIRLANMAZ — soluk gösterilir, yeniden aktive edilince geri gelir.
- **"Açıklık Yanı" semantiği (2026-07-18):** Adaylar TÜM kapılara göre en-yakın-kapı mesafesiyle sıralanır ve kapıya en yakın boş adaylardan başlanarak seçilir (`AdaylardenSecDagit` eşit-aralık dağıtımı bu modda kullanılmaz — prizleri kapıdan uzağa savuruyordu).
- **Aydınlatma buatı sayımı (2026-07-18):** `OtomatikAydinlatmaBuatiTamamla` mevcut buatları `PaftaTipi == Aydinlatma` filtresiyle sayar; priz buatları sayıma girmez (girerse ikinci yerleştirmede aydınlatma buatı eksik kalır).
- **Mahal satır toplam sütunu (2026-07-18):** Tabloda sol blokta "Toplam" sütunu satır başına aktif oda sayısı + toplam adet + (genel güç girildiyse) toplam W gösterir.
- **SadecaEksikleriEkle senkronu (2026-07-18):** VM varsayılanı işaretsiz (tam yenileme, §5 kararı). MahalListesiWindow checkbox'ı VM ile iki yönlü senkrondur; ana penceredeki "Mahalden Otomatik Yerleştir" butonu aynı değeri kullanır ve tooltip'i aktif modu gösterir. Tam yenileme kablo/devre sildiyse `UyariGoster` paneliyle bildirilir + bayat rota bandı yanar.
- **Elle taşınan otomatik eleman (2026-07-18):** `ManuelElemanKonumGuncelle`/`ManuelCokElemanKonumGuncelle` taşınan elemanın `OtomatikYerlesimKaynak` alanını temizler → tam yenileme kullanıcının taşıdığı elemanı silmez/eski konuma döndürmez.
- Genel mahal yerleşim modu grid veya duvar aralıklı ise tek tek hücre aktif etme gerekmeden satırlar efektif aktif sayılabilir.
- `Sadece eksikleri ekle` açıkken mevcut otomatik elemanlar korunur; kapalıysa (tam yenilemede) eski otomatik elemanlar silinir ancak manuel yerleştirilmiş elemanlar korunur. **Varsayılan: işaretsiz** (tam yenileme = sil + mahal listesine göre yeniden yerleştir).
- **Mahal listesi düzenlemeleri anında yerleştirme TETİKLEMEZ** (2026-06-17). Hücre tıklama, adet, grid/duvar aralığı, montaj/yerleşim tipi, güç vb. değişiklikler yalnız mahal listesi durumunu ve tablo görselini günceller (`GorselleriGuncelle`/`TabloKur`). Asıl yerleşim **yalnız "🗺 Yerleştir" butonuyla** yapılır. Sebep: her tıklamada `MahalSatiriYenidenYerlestir` çağrısı elemanları silip yeniden ürettiği için mevcut armatürler kayboluyordu. Eski otomatik-tetikleme yolları (`TetikleYenidenYerlestir`, `AdetDegisti`/`GridAlanLostFocus`/`AyarCombo`/`AyarCheckbox`/`GelenGucWDegisti` içindeki yerleştirme çağrıları) kaldırıldı. `MahalSatiriYenidenYerlestir` VM'de durur ama artık çağrılmaz (tek-satır yeniden yerleştirme için rezerv).
- Kilitli projede mahal listesinden otomatik yerleştirme yapılmaz.
- Duvar aralığı küçüldükçe yerleşen eleman sayısı artar; duvar aralığı ve otomatik kaynak işareti yeni elemanlara yazılır.
- **Küçük oda güvencesi** (2026-06-17): Grid / Duvar aralıklı yerleşimde `IzgaraAdaylariUret`/`DuvarAdaylariUret` 0 aday döndürürse (oda ızgara adımı + `ArmatürDuvarMin` payı için fazla küçük) ve o blok odada hiç yoksa, `OdayaYerlestir` en az 1 eleman üretir: oda merkezi poligon içindeyse merkeze, değilse `IcMekanAdaylar` ile bulunan iç noktaya. Bu güvence yalnız `!isAdet` (Grid/Duvar aralıklı) dalındadır; Adet/Açıklık Yanı/Homojen dalları zaten `FallbackNoktalar` kullanır.
- **Anahtar kapı yanı + buat çakışma** (2026-06-17):
  - `AnahtarYerlesim` kapı adayı bulamayıp `DuvarAdaylar`'a düşerse, oda kapısı varken adayları **kapıya yakınlığa göre** seçer (eski `AdaylardenSecDagit` ortadan seçtiği için anahtar ters duvara düşebiliyordu).
  - Aydınlatma ve priz buatları artık duvardan `BuatDuvarOfset` (0.45 m) içeri çekilir (eski `DuvarGiris` 0.12 m, anahtar/priz ile aynı yere düşüyordu) ve `CakismayiGider` ile mevcut elemanlarla `ElemanMinAralik` çakışması varsa oda içine/teğete kaydırılır. Komütatör/buat üst üste binmesini çözer.

---

## 6. Seçim ve Düzenleme

- Elemanlar ve odalar tekli veya Cmd/Ctrl ile çoklu seçilebilir.
- Çoklu eleman sürükleme grup halinde taşır.
- Salt tıklama elemanı kaydırmamalı ve undo stack'i kirletmemelidir.
- Delete seçili elemanları, seçili odaları veya seçili açıklığı uygun öncelikle silmelidir.
- Eleman tıklanınca oda seçimi temizlenir; oda tıklanınca eleman seçimi temizlenir.
- `Esc` aktif modları iptal eder.
- Üst toolbar'da dosya aç/kaydet/farklı kaydet ikonları, geri al/ileri al ve panel açma düğmeleri bulunur.
- Klavye kısayolları:
  - `Cmd/Ctrl+O`
  - `Cmd/Ctrl+S`
  - `Cmd/Ctrl+Shift+S`
  - `Cmd/Ctrl+Z`
  - `Cmd/Ctrl+Shift+Z`
  - `Cmd/Ctrl+Y`

---

## 7. Kablolama Kuralları

- Kablo tipleri:
  - Güç kablosu
  - Güç kablosu 2. devre
  - Vavien seyahat kablosu
  - Darbe/sensör kontrol kablosu
  - Pano, mekanik, zayıf akım, yangın ve topraklama hatları
- Aydınlatma kablolamada üç ana akış vardır:
  - Manuel mod
  - Mahal modu
  - Tüm kat modu
- Mahal bazlı otomatik kablolama, odadaki boş armatürleri ilgili anahtara daisy-chain yöntemiyle bağlar.
- Kat bazlı otomatik kablolama:
  - Tek anahtarlı odada tüm boş armatürleri o anahtara bağlar.
  - Çok anahtarlı odada armatürleri en yakın anahtarlara dağıtır.
- Anahtar kapasite dağılımı:
  - Armatürler geometrik sıralanır, ardından odadaki anahtarlar sırayla doldurulur.
  - Her anahtar kapasitesi kadar armatür alır (ör. komütatör → 18), kapasitesi dolunca sıradaki (kopyalanmış) anahtara geçilir.
  - Her anahtarın kendi `MaxDevreNo` değerine göre devreler oluşturulur ve armatürler devrelere eşit bölünür.
- Kablo rota algoritması (DaisyChain):
  - Omurga yalnız daisy chain'dir; paralel omurga iptal edilmiştir.
  - `GeometrikSirala`: Orthogonal (grid) hizalama ödüllüdür — X veya Y ekseninde hizalı armatürler 20× daha çekici sayılır.
  - Anahtardan ilk armatüre doğrudan çapraz çizgi yerine L (dirsek) şeklinde ortogonal rota çizilir.
  - Armatürler zincir halinde sırayla bağlanır.
- "Boştakileri Göster" açıkken bağlı olmayan armatür, buat ve anahtarlar 2D görünümde vurgulanır.
- Kablo silme işlemleri seçili hat veya tüm hatlar seviyesinde desteklenir.
- **Kapasite muhasebesi (2026-07-18):** Otomatik aydınlatma kablolamada (kat/mahal/manuel akışların üçü de) anahtara atama yapılırken `AnahtarMevcutYuk` düşülür — önceki grubun/çalıştırmanın doldurduğu anahtar atlanıp sıradaki boş anahtara geçilir. Belgeli istisna: hiç boş anahtar kalmadıysa leftover fallback'i son anahtara yine atar (buatsız/anahtarsız armatür kalmaması kuralı önceliklidir).
- **Priz çift devre koruması (2026-07-18):** `KatPrizleriniOtomatikKablola` zaten bir priz devresine atanmış prizleri yeniden dağıtmaz (`atananPrizIds` — aydınlatmadaki `atananArmaturIds` karşılığı). ≤7 sınırı buatın mevcut devre doluluğunu da sayar.
- **Priz zincir sırası (2026-07-18):** `PrizRotaHesapla` zinciri buattan başlayan `GeometrikSirala` ile sıralar (devre üyeliği değişmez, yalnız çizim sırası) — §17'deki bekleyen kapandı.
- **Boş rota = kablo yok + uyarı (2026-07-18):** `BuatPanoRotalariniHesapla` izinli rota bulunamayan bağlantı için boş rotalı kablo ÜRETMEZ; bağlantıyı korur ve uyarı listesi döner. VM uyarıları `UyariGoster` panelinde "Çizilemeyen kablolar" olarak listeler.
- **Bayat rota bandı (2026-07-18):** Eleman taşınınca (`kat.Kablolar` doluysa) `RotalarBayat` yanar; durum çubuğunda sarı "⚠ Rotalar güncel değil + Kabloları Yenile" bandı görünür. Rota hesaplayan komutlar bandı söndürür.

---

## 8. Pano, Linye ve Tek Hat Aktarımı

- M12 pano/linye özetleri M11 Tek Hat Şeması'na `.e4p11` olarak aktarılabilir.
- Aktarıma yalnız kuvvetli akım A/P/M linyeleri girer:
  - Aydınlatma
  - Priz
  - Klima / mekanik
- Zayıf akım ve yangın sistemleri Tek Hat aktarımına dahil edilmez.
- Ana pano belirleme:
  - Besleme hatlarındaki ortak `AnaPanoId`
  - Yoksa tek pano varsa o pano
  - Çok pano + besleme yoksa hata
- Tali panolarda `UstPanoAd` korunmalıdır.
- **Tali pano → kat panosu besleme** (2026-06-17, kural §14): `TaliPanolariAnaPanoyaBagla` (VM) tali panoyu şu önceliğe göre bir KAT panosuna bağlar: 1) `TaliPanoHedefPanoId` (kullanıcı seçimi), 2) tali odasının atandığı kat panosu (`BagliOdaIds`), 3) en yakın kat panosu/ana. Besleme rotası artık `BasitProjeCizKabloServisi.PanoBeslemeRota` ile **geçiş odaları + duvar boyunca** çizilir (eski düz çapraz çizgi kaldırıldı). Metot başında kat-içi (her iki ucu bu kattaki pano) otomatik besleme hatları temizlenir → hedef değişikliği yansır, çift hat oluşmaz; katlar arası kablo tavası beslemeleri korunur.
- **Pano içeriğinde besleme yükü** (2026-06-17): `BasitProjeCizPanoServisi.PanoOzetleri` artık üst panoya gelen her besleme hattını "BK" linyesi (`OzelEtiket = "← <alt pano adı>"`) olarak listeler; yük = alt panonun (kendi + altındakiler, cevrim korumalı) toplam gücü. Üst panonun `ToplamGucW` ve giriş sigortası bu beslemeleri kapsar. "Çizimde var listede yok" hatasını çözer.
- **Buat/pano ve tali pano besleme geçiş sınırı** (2026-06-17): `BuatPanoRota` artık izinli geçiş mahalleri dışında tüm odalara fallback yapmaz. Rota yalnız kaynak/hedef odaları + `GecisSecili` odaların duvar ağı üzerinde kurulur; aynı oda içinde bile düz çapraz yerine duvar kenarı tercih edilir. İzinli oda zinciri yoksa yasak odadan geçerek düz/duvar fallback çizilmez. Aynı motoru kullanan `PanoBeslemeRota` için de geçerlidir; tali pano → kat panosu kablosu yalnız izin verilen odalardan ve duvar kenarından gider. Kaynak/hedef oda tespiti `OdaId`, güncel `GecisSecili` bayrakları ve duvar yakınlığıyla yapılır; duvar/sınır üstündeki buat-pano elemanları kaybolmamalıdır. VM tarafında `TaliPanolariAnaPanoyaBagla` rota bulunamazsa artık düz çizgi fallback üretmez.
- **Küçük açıklık/kapı boşluğu köprüsü** (2026-06-17): İzinli geçiş odaları arasında oda poligonları küçük boşlukla ayrılmışsa (kapı/açıklık, CAD toleransı) `DuvarAgindaRota` 1.20 m'ye kadar kısa duvar köprüleri kurar. `SegmentIzinliOdalardaMi` uzun segmentlerde sabit 12 örnek yerine uzunluğa göre sık örnekleme kullanır; böylece küçük boşlukları atlayıp doğrudan çapraz/düz rota kabul etmez. Bu, 1B03 gibi panolarda yalnız en yakındaki buatın gelmesi ve diğer buat-pano hatlarının DXF'te kaybolması sorununu hedefler.
- Pano kablo mesafesi besleme rotasının gerçek segment uzunluğundan hesaplanır; besleme yoksa uyarılı varsayılan mesafe kullanılabilir.
- `.e4p11` JSON ayarları M11 ile uyumlu olmalıdır.
- M11 yükleme sonrası `KokPano` ad ile `TumPanolar` listesine yeniden bağlanmalıdır.
- Jeneratör serileştirme alias çakışmaları tekrar oluşturulmamalıdır.

---

## 9. DXF Çıktı Kuralları

- DXF çıktı eleman ve kabloları E4P katmanlarına ayırır.
- Aynı blok adı/tip ve çok yakın koordinattaki üst üste elemanlar 5 cm toleransla dedup edilir.
- Kullanıcı şablon dışa aktarımında `.dwg` adı verirse sahte DWG yazılmaz; çıktı `.dxf` uzantısına çevrilir ve kullanıcı bilgilendirilir.
- Dosya adları geçersiz karakterlerden temizlenmelidir.
- Çıktı başarısı veya hatası `DurumMesaji` ile görünür olmalıdır.

---

## 10. DXF Katman Haritası

Temel eleman katmanları:
- `E4P-REFERENCE` — DXF arka plan çizgileri
- `E4P-LIGHTING` — armatürler
- `E4P-SWITCH` — anahtar vb.
- `E4P-PANEL` — kat panosu
- `E4P-SENSOR` — sensör
- `E4P-BUAT` — buat
- `E4P-MECHANICAL` — klima iç/dış ünite
- `E4P-YANGIN` — yangın elemanları
- `E4P-DATA`, `E4P-TV`, `E4P-TELEFON`, `E4P-CCTV`, `E4P-ANONS`, `E4P-DIAFON`
- `E4P-ANNOTATION` — oda poligonu ve genel yazılar

Temel kablo katmanları:
- `E4P-KABLO-GUC`
- `E4P-KABLO-GUC2`
- `E4P-KABLO-SEYAHAT`
- `E4P-KABLO-KONTROL`
- `E4P-KABLO-PANO`
- `E4P-KABLO-PANO-PRIZ`
- `E4P-KABLO-MEKANIK`
- `E4P-KABLO-TAVASI`
- `E4P-KABLO-BESLEME`
- `E4P-KABLO-DATA`, `E4P-KABLO-TV`, `E4P-KABLO-TELEFON`
- `E4P-KABLO-CCTV`, `E4P-KABLO-ANONS`, `E4P-KABLO-DIAFON`
- `E4P-KABLO-YANGIN`
- `E4P-TOPRAKLAMA`, `E4P-TOPRAKLAMA-BAKIR`

---

## 11. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Electric/BasitProjeCiz/`
- `src/Kst.Acoustics.Core/Electric/BasitProjeCiz/Services/`
- `src/Kst.Acoustics.Core/Electric/BasitProjeCiz/Models/`
- `src/Kst.Acoustics.Core/Electric/Models/`

App:
- `src/Kst.Acoustics.App/Electric/BasitProjeCiz/Views/BasitProjeCizWindow.axaml`
- `src/Kst.Acoustics.App/Electric/BasitProjeCiz/Views/BasitProjeCizWindow.axaml.cs`
- `src/Kst.Acoustics.App/Electric/BasitProjeCiz/ViewModels/BasitProjeCizViewModel*.cs`
- `src/Kst.Acoustics.App/Controls/GeometryPreviewControl.cs`

Test:
- `src/Kst.Acoustics.Tests/`

---

## 12. Korunması Gereken Regresyon Alanları

- Açıklık tespiti manuel/iptal kayıtlarını korur.
- Açıklık yüzey dışı otomatik sonuçları temizler.
- Sağ tık açıklık menüsü doğru açıklığı seçer.
- Mahal listesi tanımsız armatürü varsayılan sembolle yerleştirir.
- Armatür yerleşimi eski enum değerlerinde bile grid kullanır.
- Eleman salt tıklama konumu değiştirmez ve undo snapshot oluşturmaz.
- Çoklu eleman/oda seçimi ve silme çalışır.
- Otomatik aydınlatma kablolama boş armatürleri bağlar.
- Tek Hat aktarımı `.e4p11` round-trip sonrası panoları, linyeleri, üst pano bağını ve kablo boyunu korur.
- DXF çıktı üst üste blokları dedup eder.
- `.dwg` adı verilen şablon çıktısı `.dxf` olarak güvenli yazılır.
- Mahal listesi tam yenilemesi (sadece eksikler kapalıyken) manuel elemanları korur, eski otomatik elemanları siler.
- Kilitli projede mahal listesinden otomatik yerleştirme yapılmaz.
- Duvar aralığı değişimi yerleşen eleman sayısını etkiler, duvar aralığı ve otomatik kaynak işareti yeni elemana yazılır.
- Geri Al (Undo), kablo çekme işleminde yalnız kabloları/devreleri geri alır; armatür pozisyonları korunur.
- Anahtar çoğaltma kopyaları duvara paralel dizilir ve armatürler kapasite bazlı eşit dağıtılır.
- Kablo rotası anahtardan ilk armatüre L (dirsek) çizer; çapraz (spagetti) hat çizmez.
- Kayıt atomik olmalı (geçici dosya + Replace/Move) ve hata durumunda çökmeden `DurumMesaji`'na yazmalı.
- Otomatik aydınlatma kablolamada kapasite aşımı fallback'i de armatür devresine buat atamalı (buatsız devre kalmamalı).
- Priz buatı dağıtımı dengeli + her buat ≤7 priz (sınır manuel buat eksikliğinde de aşılmamalı).
- Pano giriş sigortası: panoda trifaze linye varsa giriş de trifaze (400 V) hesaplanmalı.
- Tek Hat aktarımında pano hiyerarşisi cevrim içerse bile özyineleme çökmemeli (ziyaret seti).
- Mahal listesi tablosundaki hiçbir düzenleme (hücre tıklama, adet, grid, montaj, güç) anında eleman silip yerleştirmemeli; yerleşim yalnız "🗺 Yerleştir" butonuyla yapılmalı (mevcut armatürler tıklamada kaybolmamalı).
- Buat→pano ve tali pano→kat panosu rotası yalnız `GecisSecili` + kaynak/hedef odalarından geçmeli; izin verilmeyen odaya fallback etmemeli ve duvar kenarı rotasını korumalı.
- Kablo tavası beslemesi tali panoyu doğrudan ana panoya bağlamamalı (tali → kat → ana). Tali bir panonun hem tali→kat hem tali→ana hattı varsa TekHat ebeveyn olarak kat panosunu seçmeli ve kablo boyunu çift saymamalı.
- (2026-07-18 paketi) İkinci priz kablolama çift devre üretmemeli; aydınlatma buat sayımı priz buatını saymamalı; kapasite muhasebesi dolu anahtarı atlamalı; "Açıklık Yanı" prizleri kapı yanına toplanmalı; undo tali pano hedefini ve mahal satırını korumalı; IsDirty mutasyonda kirlenip kayıtta temizlenmeli; rota bulunamayan buat-pano bağlantısı kablo üretmeyip uyarı dönmeli (bağlantı korunur).

---

## 13. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Windows tek dosya yayınlama komutu:

```bash
dotnet publish src/Kst.Acoustics.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o publish/win-x64
```

Son bilinen genel hedef:
- Build: 0 hata
- M12 regresyonları dahil mevcut test paketi geçmeli

---

## 14. Tali Pano Mantığı

**Tali pano yalnız kendi odasına hizmet eder** (2026-06-17).

- `BuatIcinHedefPano`: buatın odası tali pano içeriyorsa direkt o tali panoya yönlendirilir; başka odalar tali panoyu seçemez (bu mantık değiştirilmemeli).
- Tali panonun kendisi bir **kat panosuna** bağlanır. Bağlantı önceliği:
  1. `BasitProjeEleman.TaliPanoHedefPanoId` — kullanıcının açıkça seçtiği kat panosu Id'si.
  2. null ise otomatik çözümleme: tali panonun `OdaId`'si hangi kat panosunun `BagliOdaIds`'inde varsa o pano.
  3. İkisi de yoksa "Atanmadı" durumu — sağ panelde kullanıcıya ComboBox gösterilir.
- **Sağ panel (tali pano seçiliyken)**:
  - "Servis ettiği oda" → `TaliPanoOdaAdi` (salt okunur)
  - "Bağlı kat panosu" → `TaliPanoKatPanoSecenekleri` ComboBox + "Seçimi Temizle" butonu
  - Otomatik çözüm varken metin gösterilir (`TaliPanoOtomatikCozumMetni`)
  - Bağlı odalar listesi (`GosterSeciliElemanPanoOdalari`) tali pano için gizlidir.
- **Yeni model alanı**: `BasitProjeEleman.TaliPanoHedefPanoId` (`string?`) — tali panoya özgü, diğer pano tipleri için anlamsız.

> **Çözüldü (2026-07-03, A1)**: İki besleme üreticisi çakışması giderildi. Kablo tavası
> beslemesi (`BeslemeHatlariniHesapla`) artık yalnız KAT/ANA panolarını besler; tali panolar
> hedef döngüsünden `PanoTaliMi` ile dışlanır (riser'dan değil kat panosundan beslenir).
> Tali → kat → ana bağı `TaliPanolariAnaPanoyaBagla` ile kurulur. TekHat tarafında
> `ParentHaritasi(kaynak, anaPanoId)` deterministik: bir tali birden çok besleme hattı
> taşırsa ARA (kat) panosunu doğrudan ana panoya tercih eder; `BeslemeUzunlugu` yalnız
> seçilen ebeveyne giden hattı toplar (çift kablo boyu engellenir). Regresyon:
> `TekHatAktarimTests.CiftBeslemeHattindaKatPanosuEbeveynSecilir`.

---

## 15. KurallarWindow Bakımı

`Views/KurallarWindow.axaml` — Sol alt "Kurallar" butonuyla açılan salt-okunur bilgi penceresi.
Kodda aktif olan bağlantı, yerleşim ve elektrik kurallarını kullanıcıya gösterir.

**Her yeni kural eklendiğinde `KurallarWindow.axaml` güncellenmeli.** Kontrol edilecekler:

- `BasitProjeCizKabloServisi.cs` — `ElemanPrizDirektPanoMu`, `PrizBasinaBuat`, `ElemanPrizMi`, `ElemanArmaturMi`
- `BasitProjeCizOtomatikYerlestirServisi.cs` — `AnahtarBasinaBuat`, `PrizBasinaBuat`, `DuvarGiris`, `AciklikKoruma`, `ElemanMinAralik`, `ArmatürDuvarMin`
- `BasitProjeCizKabloServisi.BuatIcinHedefPano` — Pano seçim hiyerarşisi (Tali → Kat → Ana)
- `LinyeHesapServisi.cs` — `StandartSigortaA`, `SigortaBoyutlandir`, `KesmeKapasitesiTayin`
- `BasitProjeCizPanoServisi.cs` — `GerilimV` (230), `HatGerilimV` (400), `CosF` (0.95)

---

## 16. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. İş Core davranışıysa önce `Core/Electric/BasitProjeCiz` servis/model dosyalarını incele.
4. UI davranışıysa `BasitProjeCizWindow` ve `BasitProjeCizViewModel` partial dosyalarını kontrol et.
5. Viewport etkileşimi gerekiyorsa `GeometryPreviewControl.cs` içindeki callback/hit-test akışını incele.
6. Tek Hat aktarımı değişiyorsa M11 yükleme/serileştirme uyumunu kontrol et.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.
8. Risk varsa ilgili regresyon testini ekle veya mevcut testleri çalıştır.

---

## 17. İyileştirme Tavsiyeleri (bekleyen — denetim 2026-06-15)

Aşağıdaki 6 hata düzeltildi (build 0 hata, 309 test geçer):
1. **Kayıt güvenliği** — `ProjeyiKaydet` artık atomik (`.tmp` + Replace/Move) + try/catch, `bool` döner.
2. **Kablolama buat boşluğu** — kapasite aşımı fallback blokları (mahal + kat akışı) buat atıyor.
3. **Pano hiyerarşi cevrimi** — `HesaplaToplamGuc` ziyaret seti ile StackOverflow korumalı.
4. **Priz buat sınırı** — dağıtım dengeli + her buat ≤7 priz garanti.
5. **Trifaze pano girişi** — giriş sigortası trifaze linye varsa 400 V hesaplanır.
6. **Yutan catch** — kütüphane okuma hatası artık `Debug.WriteLine` ile iz bırakır.

Bekleyen tavsiyeler (henüz yapılmadı, "daha sonra bakılacak"):
- **VM bölme**: `BasitProjeCizViewModel.cs` ~4.6k satır → §4.3 partial kuralına aykırı.
  Kayıt/yükleme/migrasyon, pano-besleme, etiket yerleşimi ayrı partial'lara çıkarılmalı.
- **Rota motoru ayrımı**: `BasitProjeCizKabloServisi.cs` (~2.1k satır) devre yönetimi + geometrik
  rota motorunu birlikte taşıyor. Rota motoru (`DuvarBoyuncaRota`, `DuvarAgindaRota`, Dijkstra,
  `RotaTemizle`…) ayrı `KabloRotaMotoru` sınıfına alınmalı (test edilebilirlik).
- **O(N²) eleman araması** (kısmen çözüldü, 2026-07-03, C3): `KabloServisi.ElemanHaritasi` +
  opsiyonel `elemanById` parametresi eklendi; `TumPrizRotalariniHesapla` ve
  `TumSistemRotalariniHesapla` kat başına bir kez sözlük kurup `PrizRotaHesapla`/`SistemRotaHesapla`'ya
  geçiriyor (geri uyumlu; parametre yoksa eski O(N) tarama). **Kalan**: aydınlatma
  (`RotaHesapla`/`KatAydinlatmasiniOtomatikKablola`) ve pano/mekanik yolları hâlâ tarıyor.
- **Duvar mesafe taraması**: `EnYakinDuvarMesafesi`/`SegmentDuvaraYakinMi` her örnek noktada tüm
  kenarları tarıyor; kenarlar bir kez toplanıp yeniden kullanılmalı.
- ~~**Priz daisy-chain sırası**: prizler `PrizElemanIds` ekleme sırasıyla bağlanıyor; armatürdeki
  gibi `GeometrikSirala` uygulanmalı (daha kısa rota).~~ **Çözüldü (2026-07-18)**:
  `PrizRotaHesapla` zinciri buattan `GeometrikSirala` ile sıralıyor.
- **Proximity hash**: `Point2DProximityComparer.GetHashCode` bilinçli olarak sabit 0 döner —
  yakınlık-tabanlı eşitlik geçişsiz olduğundan grid-hash HashSet değişmezini bozar (2026-07-03
  denendi, geri alındı; değişmez kodda belgeli). Büyük N gerekirse 3×3 komşu tarayan ayrı
  uzamsal grid yazılmalı; comparer'ın hash'i değiştirilmemeli.
- ~~**Defansif geometri**: `PolyCentroid` / `BasitProjeOda.Merkez` boş poligonda bölme koruması.~~
  **Çözüldü (2026-07-03, A2/A3)**: Ortak `PoligonYardimci.Centroid` eklendi (alan-ağırlıklı,
  dejenere→köşe ortalaması, boş→(0,0)). `BasitProjeOda.Merkez`, KabloServisi `PolyCentroid`
  ve OtomatikYerlestir `PoligonMerkez` buna bağlandı — köşe ortalaması yanlılığı + NaN riski
  kapandı. Regresyon: `OdaMerkeziAlanAgirlikliCentroiddir`.
- **Test boşlukları**: pano hiyerarşi cevrimi, kapasite aşımı buat ataması, trifaze pano girişi
  için regresyon testleri eklenmeli.

## 18. 2026-07-18 Güvenilirlik + UX Paketi (tamamlandı)

Kapsamlı mahal listesi + otomatik kablolama denetimi sonrası tek pakette düzeltildi
(609 test geçer; 8 yeni regresyon eklendi):

1. **IsDirty + kapanış onayı** — maincodex §4.2 borcu kapandı (bkz. §3 ilk madde).
2. **KuvvetliTipler eksikleri** — 3'lü Komütatör/Komütatör Vavien/Deviatör/UPS türevleri/armatür türevleri eklendi (bkz. §5 genel kural).
3. **Hücre tıklama** — seç → ikinci tıkta toggle; pasifte Adet korunur (soluk gösterim).
4. **CloneEleman** — `TaliPanoHedefPanoId` undo kopyasına eklendi.
5. **Aydınlatma buat sayımı** — `PaftaTipi == Aydinlatma` filtresi.
6. **Priz çift devre** — `atananPrizIds` koruması + ≤7 sınırında mevcut doluluk.
7. **Undo kapsamı** — mahal listesi, kablo tavaları, pano besleme hatları, topraklama snapshot'a girdi; MahalListesiWindow bayat seçim temizliği.
8. **Açıklık Yanı** — kapıya-en-yakın seçim, tüm kapılar dikkate alınır.
9. **SadecaEksikleriEkle** — VM↔pencere iki yönlü senkron; ana buton tooltip'i modu gösterir; tam yenilemede silinen kablo uyarısı + yerleştirme uyarıları `UyariGoster` panelinde.
10. **Boş rota** — kablo üretilmez, uyarı listesi döner ve panelde gösterilir.
11. **Kapasite muhasebesi** — `AnahtarMevcutYuk` üç akışta da düşülür.
12. **Bayat rota bandı** — taşıma sonrası sarı bant + "Kabloları Yenile" butonu.
13. **Taşınan otomatik eleman** — kaynak temizlenir, tam yenileme korur.
14. **Priz zincir sırası** — `GeometrikSirala` (bkz. §7).
15. **Satır toplam sütunu** — mahal tablosunda oda sayısı + adet + W.
16. **Kilitli projede salt-okunur inceleme** — hücre seçimi serbest, yazma engelli.
17. **`BpcMahalSatiri` INPC** — genel ayarlar eşitlik korumalı bildirim yapar (IsDirty bağlanması).

Pakette bilinçli YAPILMAYANLAR (bekleyen, §17'ye ek):
- Mahal listesi oda-tipi şablonları (yatak odası/ofis hazır setleri).
- Yerleştirme öncesi diff ön-onay modalı (şu an: sonrasında uyarı paneli).
- ~~Duvar graf önbelleği~~ → §19/K12 ile çözüldü (2026-07-19).
- Anahtarsız odada "kapı yanına anahtar ekle" hızlı eylemi (şu an yalnız uyarı).

## 19. 2026-07-19 Kablo Modu UX Paketi (K1-K12, tamamlandı)

"Kablo çekim sistemi kullanışsız" şikayeti üzerine 12 maddelik yeniden tasarım
(611 test geçer; 2 yeni regresyon: canlı rota + graf önbelleği):

1. **Canlı rota (K1)** — her atama tıklaması (armatür/priz/buat/vavien) aktif devrenin
   rotasını ANINDA çizer (`AktifDevreRotasiniCiz`/`AktifPrizRotasiniCiz`). Eski
   "Hesapla'ya basın" mesajının işaret ettiği buton hiç yoktu (bağlanmamış
   `KabloRotaHesaplaCommand`) — akıştaki kopukluğun kökü buydu.
2. **Esc/Enter/sağ tık (K2)** — Esc: yarım hattı iptal (`KabloModuHattiIptal`), ikinci
   Esc modu kapatır; Enter ve boş zemine sağ tık: hattı bitir (`KabloModuHattiBitir`).
   Vavien "ikinci anahtar" beklemesi de Esc ile bozulur (K9).
3. **Mod vurgusu + adım göstergesi (K3)** — `KabloAltMod`
   ("Manuel|Mahal|BuatPano|PrizManuel|PrizMahal") panel butonlarını `Classes.kmAktif`
   ile vurgular; `KabloAdimGosterge` "① Anahtar ✓ → ② Armatür (3) → ③ Buat" bandı.
   Panelde "✔ Hattı Bitir (Enter)" / "✖ İptal (Esc)" butonları.
4. **İkinci tık = çıkar (K4)** — atanmış armatüre/prize tekrar tıklamak devreden
   çıkarır (yanlış tıklama tek tıkla onarılır).
5. **Hover ön-vurgusu (K5)** — `GeometryPreviewControl.HoverPointPosition` (turkuaz
   halka): 1.5 m toleransla hangi elemanın yakalanacağı tıklamadan önce görünür.
6. **Bant seçim (K6)** — Shift+sol sürükleme (`BandSecimAktifMi`/`OnBandSelected`,
   kesikli yeşil dikdörtgen): hat açıkken banda giren boş armatürler/prizler topluca
   atanır (`KabloModuBandSecim`). Pan davranışı değişmedi.
7. **Aday vurgusu + "⚡ Odadakileri Ata" (K7)** — anahtar seçilince odadaki boş
   armatürler kesikli yeşil halkayla vurgulanır (`CandidatePointPositions`); tek tuş
   `OdadakiArmaturleriAta` mevcut `MahaldekiBosArmaturleriAnahtaraBagla` motorunu kullanır.
8. **Kablo çizgi yakalama (K8)** — seçim toleransı 0.5→0.8 m; seçimde durum mesajı.
9. **Komütatör devre çipleri (K9)** — 1/2 butonları aktif seçimi vurgular.
10. **Akıllı tıklama (K10)** — durum 0'da buata tıklamak Buat→Pano akışını otomatik
    başlatır (bu akışın UI'da düğmesi bile yoktu — komut bağlanmamıştı; "Buat → Pano"
    butonu da eklendi); durum 1'de panoya tıklamak devrenin buatını o panoya bağlar.
11. **Devre listesi paneli (K11)** — kablo panelinde "Devre Listesi" expander'ı:
    aydınlatma devreleri (anahtar, armatür sayısı, buat, uzunluk), priz hatları ve
    buat→pano linyeleri; satıra tıkla → canvas vurgusu, ✕ → hat silme
    (`DevreOzetleri`, `DevreSatiriSec/Sil`). İnce çizgi avcılığına alternatif.
12. **Duvar graf önbelleği (K12)** — `DuvarAgindaRota` taban grafı (düğümler + yakın
    düğüm birleştirme + O(E²) kısa açıklık köprüleri) oda-kümesi anahtarıyla
    (`GrafAnahtari`: oda Id + köşe sayısı + koordinat hash) statik önbelleğe alınır
    (kapasite 8, taşınca temizlenir); sorgu başına yalnız start/end düğümleri kopya
    graf üzerine eklenir. Canlı çizimin büyük katlarda akıcı kalmasının önkoşulu.
    Geometri değişince anahtar değişir → bayat graf kullanılmaz (regresyonlu).

## 20. 2026-07-19 (devam) — Mahal Listesi Üretkenlik Paketi (M1-M7, tamamlandı)

"Mahal listesi sistemi kullanışsız" denetiminin sonucu — 4 fazlı plan (Faz 1-4 tümü
onaylandı; aynı sembolden ikinci satır BİLİNÇLİ OLARAK desteklenmedi, bkz. madde 0).
625 test geçer (14 yeni regresyon: `MahalListesiServisiTests` + 1 VM testi).

**0. Kapsam kararı (öncesinde soruldu, cevaplandı):** Aynı malzemeden (örn. armatür)
farklı grid aralığıyla ikinci satır UI tarafından önerilmiyor; şablon/kopyalama/öneri
"kalem kimliği" olarak `ArmatürTanimId` (yoksa `Tip`) kullanır. 2026-08-07
güvenilirlik paketinde veri güvenliği için her satıra kararlı `Id`, otomatik elemanlara
`OtomatikYerlesimGrupIds` eklendi. Böylece eski/elle oluşturulmuş yinelenen satırlar
birbirinin elemanını silmez ve ortak buat/sensör bağı korunur (bkz. §21).

**Yeni dosya:** `Core/Electric/BasitProjeCiz/Services/MahalListesiServisi.cs` — kattan
kopyalama, akıllı doldurma ve şablon mantığının tek kaynağı.

**Ortak sözleşme (tüm işlemler için):** kullanıcının ELLE aktif ettiği (`Aktif=true`)
hücre asla ezilmez; işlemler yalnız pasif/boş hücreleri doldurur veya yeni satır ekler.
Oda eşleme her yerde AD bazlıdır (`OdaAdiNormalize`: küçük harf + Türkçe karakter
sadeleştirme + rakam/noktalama atma) — katlar arası Id farklı olduğu için.

1. **Kattan Kopyala (M1)** — üst barda "⧉ Kattan Kopyala" → kat seçim diyaloğu →
   `MahalListesiServisi.KattanKopyala`. Kaynak/hedef arası oda adı eşleşir; eşleşmeyen
   oda pasif kalır (rapor: `KopyalamaSonucu.Ozet`).
2. **Akıllı Doldur (M2)** — "✨ Akıllı Doldur" → oda adı → tipik kalem seti
   (`VarsayilanOneriler`: Yatak Odası/Salon/Mutfak/Banyo-WC/Hol-Koridor/Ofis/Depo/Balkon,
   anahtar-kelime eşleşmeli, en uzun anahtar kazanır). Sembol çözümü kütüphaneden;
   sembolü olmayan kalem sessizce atlanır. Disipline uymayan kalemler filtrelenir
   (M12 kuvvetli, M16 zayıf — `BpcSistemKatalogu.Disiplini`). Tanınmayan odalar
   sonuç raporunda listelenir.
3. **Mahal Şablonu (M3)** — "💾 Şablon Kaydet" / "📂 Şablondan Yükle". Dosya:
   `%AppData%/Eye4Pro/Electric/BasitProjeCiz/mahal-sablonlari.json` (atomik yazım,
   M12 §9 deseniyle aynı .tmp+Replace/Move). Şablon oda ADI saklar (Id değil);
   `SablonDosyasi.OdaTipiOnerileri` doluysa Akıllı Doldur'un öneri tablosunu
   GENİŞLETİR (kullanıcı kendi oda-tipi standardını ekleyebilir; gömülü tablo yerine
   geçmez, tanım şu an yalnız kullanıcı listesini kullanır — birleştirme yapılmadı,
   ileride madde).
4. **Matris ergonomisi (M4)**:
   - Oda sütunu başlığına **çift tık** = o odadaki tüm (dahil) kalemleri topluca aç/kapat.
   - Malzeme adı hücresine **çift tık** = o kalemi tüm (dahil) odalarda topluca aç/kapat.
   - **Sürükle-boya**: hücreye tıklayıp (aktif/pasif değiştikten sonra) basılı sürükleme
     ile geçilen hücreler aynı yeni duruma boyanır (`_boyaAktif`/`_boyaDegeri`,
     `PointerEntered`; pencere genelinde `PointerReleased` boyayı bitirir + tabloyu
     tazeler).
   - **Klavye gezinme**: ok tuşları hücre gezdirir (satır/oda listesi filtrelenmiş
     sıradan), Space aktif/pasif, rakam tuşu Adet modunda direkt adet yazar (ve
     aktifleştirir), Delete/Backspace pasifleştirir (Adet korunur, §5 kuralı).
   - **Oda arama + sıralama**: üst bar "Oda ara" metin kutusu (`OdaAdiNormalize`
     ile filtreler) + "Sırala" combo (Tespit sırası / Ada göre / Alana göre —
     azalan). `GosterilecekOdalar` tek kaynak; `TabloKur`/`GorselleriGuncelle`/
     klavye gezinme hepsi buradan okur.
5. **Yerleşim ön-tahmini (M5)** — Grid/Duvar aralıklı hücrelerde artık yalnız "✓"
   değil **`~N`** (tahmini adet) gösterilir: `MahalListesiServisi.YerlesimTahmini`
   yerleştirmenin kullandığı AYNI aday üretici motoru (`IzgaraAdaylariUret`/
   `DuvarAdaylariUret`) çalıştırır — tahmin ile gerçek sonuç arasında motor farkı
   yok. Toplam sütunu artık bu tahmini kullanır. Alt bar "Tahmin: N eleman · M oda ·
   ~W kurulu güç" (`KatOzetiGuncelle`/`KatTahminiGucW`) — pano yükü sürprizi
   yerleştirmeden ÖNCE görünür.
   **Çift yönlü vurgu**: canvas'ta oda seçilince (`SeciliOda` değişince) mahal
   penceresi ilgili sütunu turkuaz çerçeveyle işaretler ve yatay kaydırmayı oraya
   götürür (`_canvasVurguOdaId`, `MahalListesi_VmSeciliOdaChanged`).
6. **Tüm Katlara Yerleştir (M6a)** — "🏢 Tüm Katlar" → onay diyaloğu (mod + etkilenen
   katlar listesi) → `BasitProjeCizViewModel.TumKatlaraYerlestir` mahal listesi olan
   HER katta `MahalListesindenOtomatikYerlestir`'i çalıştırır, kat kat rapor döner,
   işlem sonunda önceki seçili kata geri döner (bağlam korunur).
7. **Dock panel yerine tek-örnek pencere (M6b — KAPSAM DEĞİŞİKLİĞİ)**: plandaki
   "mahal listesini ana pencereye gömülü panel yapma" fikri UYGULANMADI — mevcut
   ana pencere (1500+ satır XAML, çok sayıda floating panel) içine mahal listesinin
   ~1000 satırlık tablo/etkileşim mantığını gömmek yüksek regresyon riski taşıyordu.
   Bunun yerine düşük riskli, eşdeğer-fayda bir çözüm uygulandı: `MahalListesi_OnClick`
   artık pencere zaten açıksa YENİDEN OLUŞTURMAZ, `Activate()` ile öne getirir
   (`BasitProjeCizWindow._mahalListesiWindow`). M5'teki çift yönlü vurgu + pencerenin
   zaten non-modal (`Show(this)`) olması, bağlam kopukluğu sorununun büyük kısmını
   dock panel kadar risksiz şekilde çözer. Gömülü panele geçiş hâlâ mümkün bir
   gelecek işi (bekleyenlere eklendi).

**Bekleyen (bu paketten sonra, düşük öncelik):**
- Kullanıcı öneri tablosunu gömülü tabloyla BİRLEŞTİRME (şu an ya/ya da).
- Tam gömülü dock panel (M6b'de risk gerekçesiyle ertelendi).
- Şablonlar arası fark görüntüleme / şablon düzenleme UI'ı (şu an yalnız kaydet/uygula).

## 21. 2026-08-07 — M12 Güvenilirlik ve Hesap Doğruluğu Paketi

- Sağ özellik panelindeki pano/eleman değişiklikleri kilit kontrolü + tek undo snapshot
  + `IsDirty` + pano/filtre yenileme hattından geçer; kilitli panel UI'da da devre dışıdır.
- Eleman Sil düğmesi snapshot alan komutu kullanır. Pano silme tüm katlardaki besleme,
  hedef pano, buat-pano ve mekanik referanslarını temizler; çok katlı işlem tek undo'dur.
- M12→M50 migrasyonu `.e4bina` içine kaynak artifact kimliği yazar: aynı kaynak tekrar
  açılırsa mevcut dosya bağlanır, farklı kaynakta benzersiz güvenli dosya oluşturulur.
  Bağlı yol/revizyon ile `BinaGeometriKopya` daima aynı dosyanın içeriğidir.
- M50 güncellemesinde elemanı olmasa da devre, kablo, tava, pano besleme, topraklama
  veya mahal verisi taşıyan eski kat korunur; kat-seviyesi kayıtlar mutabakatta görünür.
- Doğrudan panolu prizler `BpcPrizDevre.BuatId = priz.Id` ile pano gücü/sigortasına girer.
- Tek Hat kökü ilk hat sırasından değil pano rolü + besleme grafiği kökünden seçilir;
  M12'nin `BK` özet linyeleri M11'e normal yük olarak aktarılmaz, alt pano yükü tek sayılır.
- `Tamamını Sil` yalnız aktif paftayı temizler. Son devre silinince kullanılmayan
  buat-pano bağlantısı/kablosu da kaldırılır; çizilemeyen rota başarı mesajı üretmez.
- 10 yeni M12 güvenilirlik regresyonu eklendi. Tam koşucu: **709 geçti**;
  Core koşucusu: **121 geçti**; çözüm derlemesi: 0 hata / 0 uyarı.
