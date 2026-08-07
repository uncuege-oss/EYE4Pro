# M11 Codex — Tek Hat Şeması

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M11 — Tek Hat Şeması |
| Amaç | Pano, linye, kablo, busbar, kompanzasyon ve jeneratör içeren tek hat şeması üretmek |
| Standart referansları | IEC 60617, IEC 60364, TS HD 60364 |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Proje uzantısı | `.e4p11` |
| Durum | Geliştiriliyor |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Ana pano, tali pano, aydınlatma panosu, kompanzasyon panosu, transfer panosu, kontrol panosu ve **yangın panosu** (`PanoTipi.YanginPanosu`: ana şalter öncesinden beslenir, ana pano yüküne eklenmez)
- Linye girişi ve düzenleme
- Seçili panoya Excel şablonundan toplu linye aktarımı
- Monofaze/trifaze yük ayrımı
- Faz dağılımı
- Giriş/çıkış sigortası seçimi
- Kaçak akım rölesi seçimi
- 100A üstü kaçak akım korumasında troid röle + troid akım trafosu otomatik seçimi
- Kablo kesiti, gerilim düşümü ve ısı kaybı hesabı (hem pano-pano hem **linye seviyesinde**)
- Kısa devre akımı (Ik3) — IEC 60909 basitleştirilmiş, kaynaktan aşağı azalan; kesme kapasitesi Ik'ya göre
- DXF dışa aktarma (IEC 60617 tarzı yukarıdan-aşağı tek hat şeması)
- Pano listelerini Excel'e dışa aktarma (tek `.xlsx`, pano başına ayrı sayfa)
- Dikey busbar sistemi ve çıkış kutuları
- Aydınlatma panosu için astronomik zaman rölesi ve kontaktör seçimi
- Kompanzasyon hesabı, kondansatör kademeleri, SVC reaktör kademeleri ve harmonik filtre
- Jeneratör, transfer panosu ve ATS/MTS şalteri
- UPS (KGK) ile beslenen kritik pano ve akü grubu boyutlandırma (IEEE 485)
- **Orta gerilim (OG) katmanı**: ana pano öncesi şebeke girişi, OG hücreleri (ölçü, bara bağlama, giriş/çıkış, trafo koruma), trafo (yağlı/hermetik/kuru tip), beton köşk (bina altı / dış mekan / harici yapı) ve trafo AG→ana pano bağlantısı (kablo / Bar-24 / hücre); köşk içi fiziksel yerleşim çizimi
- Bina kat konfigürasyonu ve kat bantlı şema çizimi
- Kütüphane düzenleme penceresi
- M12 Basit Proje Çiz'den `.e4p11` aktarım uyumu

---

## 3. Temel Tasarım Kararları

- Aydınlatma varsayılan birim güç: `40 W`
- Priz varsayılan birim güç: `300 W`
- Islak hacim priz varsayılan güç: `300 W`
- Tali pano KAR: `30 mA`
- Ana pano KAR: `300 mA`
- 100A ve altı giriş akımlarında klasik 4P kaçak akım rölesi seçilir; **100A üstünde klasik KAR yerine troid kaçak akım rölesi** seçilir.
- KAR kullanımı pano bazında kapatılabilir (`Pano.KacakAkimRolesiAktif`, varsayılan `true`). Hesap Özeti içindeki KAR anahtarı değişikliği anında yeniden hesaplar; kapalı panoda KAR/troid akım trafosu ve bunların modül ihtiyacı kaldırılır. Tercih `.e4p11`, Excel ve DXF davranışında korunur.
- Troid KAR seçilirse rölenin `AkimTrafosuSekonderA` ve `MinAkimTrafosuIcCapMm` ihtiyacına göre `AkimTrafosuKutuphanesi.json` içinden en küçük yeterli troid akım trafosu atanır (`Pano.KacakAkimAkimTrafosu`).
- Kompanzasyon tetik: toplam güç `> 15 kW`
- Kompanzasyon yalnız ana pano düzeyinde değerlendirilir.
- Hedef cosφ: `0.99`
- Varsayılan kablo tipi: `NHXMH`
- Şemada otomatik atanacak bakır kabloların minimum kesiti `2,5 mm²`dir; hesap sonucu daha küçük olsa bile `KabloHesapServisi` kesiti `2,5 mm²`ye yükseltir.
- Selektivite proje ayarıdır; aktifse üst pano çıkış sigortası bir üst standart değere çıkar.
- **Kontrol (doğrulama) modu:** Selektivite yanındaki "✓ Kontrol" butonu modu aç/kapatır (`_kontrolModu`). Açıkken her pano kanvasta eksikliğine göre renklenir: **kırmızı** = pano içinde en az bir linyenin açıklaması boş; **sarı** = kablo mesafesi hâlâ varsayılan (`Pano.KabloMesafeKullaniciAyarladi=false`; "Ata" ile ayarlanınca kalkar); **yeşil** = eksik yok. Önem sırası kırmızı > sarı > yeşil. Fareyle pano üzerine gelince (tıklamadan) eksiklik listesi **tooltip** olarak çıkar (`KontrolTooltip`/`PanoEksiklikListesi`). **Boş alana sol tık** → moddan çıkar, şema normale döner. Pano sağ-tık/seçim mod açıkken de çalışır. Buton durumu kendi görünümünde gösterir ("✓ Kontrol (Açık)" + yeşil); açıkken kısa renk açıklaması (legend) görünür.
- **Tooltip teması:** kanvas tooltip'leri koyu zeminlidir (Window `ToolTip` stili `#0C1A0C` + açık yazı); varsayılan açık zeminde açık yazılar okunmuyordu.
- **Busbar etiketi tooltip'i:** busbar pill etiketine fareyle gelince (tıklamadan) **toplam yük (kW/kVA) + toplam akım (A) + nominal akım & doluluk % + ΔU + çıkış sayısı/uzunluk** özeti çıkar (`BusbarTooltip`).
- Pano giriş/çıkış sigortası standart listesi yüksek ana pano yükleri için 3200A'a kadar çıkar
  (`... 630/800/1000/1250/1600/2000/2500/3200`); ana pano yalnız alt panolardan yük alsa bile
  giriş sigortası özet satırında güncel gösterilmelidir.
- İzin verilen gerilim düşümü **iki ayrı proje ayarıdır**: **segment** sınırı (`IzinVerilenGerilimDususYuzde`, vars. `%3`) her kablo/busbar segmentini boyutlandırır; **toplam (kümülatif)** sınırı (`IzinVerilenToplamGerilimDususYuzde`, vars. `%5`) kaynaktan uca birikimli ΔU için uyarı eşiğidir. Toolbar'da "Segment (%)" + "Toplam (%)" kutuları.
- **Kümülatif gerilim düşümü (A1, `GerilimDususKontrolServisi`):** tüm segment ΔU'ları (kablo + busbar + OG) hesaplandıktan sonra kaynaktan (trafo/ana pano) her panonun barasına ve her linyenin ucuna kadar birikimli ΔU toplanır (`Pano/Linye.KumulatifGerilimDususYuzde`). Busbar beslemede zincire çıkış kutusuna kadarki busbar ΔU'su (`BusbarCikisKutusu.GerilimDususYuzde`) + kutu→pano kablosu eklenir. Toplam sınırı aşanlar `GerilimDususuAsiyor` ile işaretlenir → Hesap Özeti uyarı paneli + Kontrol modu (kırmızı) + linye tablosunda `ΣΔU (%)` kolonu (⚠). `TumunuHesapla` zincirinin sonunda çağrılır.
- **Kablo kesiti yeterlilik uyarısı (A2):** otomatik boyutlandırılan beslemelerde (pano besleme, linye, busbar çıkışı) paralel kol sayısı gerçekçi tavanla sınırlıdır (`KabloHesapServisi.VarsayilanAzamiParalelKol = 12`; ötesi pratikte busbar ile çözülür). Bu tavanda bile termal/segment-ΔU karşılanmazsa `Pano/Linye.KabloKesitiYeterli=false` (`ParalelKabloSonucu.YeterliMi`'den) → Hesap Özeti uyarısı + Kontrol modu (kırmızı). OG/jeneratör gibi açık kol sınırı geçiren yollar etkilenmez.
- Reaktör akort yüzdesi (p) proje/kompanzasyon ayarıdır; varsayılan `%7` (189 Hz). Seçenekler: 5.67/7/14.
- Eşzamanlılık (diversite) faktörü pano ayarıdır (`Pano.EszamanlilikFaktoru`, 0–1, varsayılan `1.0`); alt panonun üst panoya yansıyan talebini belirler.
- **Talep akımı (`Pano.TalepAkimA = GirisAkimA × ks`):** diversite faktörü yalnız üst panoya yansıyan yükü değil, **panonun kendi besleme tarafını** da küçültür. Giriş sigortası, üst panodaki çıkış sigortası, besleme kablosu (gerilim düşümü/ısı kaybı) ve KAR seçimi **talep akımına** göre boyutlanır; `GirisAkimA` (bağlı akım) yalnız gösterim/bara akımı içindir. ks değişince besleme sigortaları ve kablo otomatik revize olur.
- **Diversite faktörü kanvasta görünür:** üst panosu olan her panonun sağ üst köşesinde `ks {değer}` rozeti (ks=1.0 yeşil, <1.0 turuncu); tooltip bağlı akım → talep akımını gösterir.
- **Giriş şalteri (`Pano.GirisSalteriA`, yalnız ana pano):** trafo varsa **trafo AG çıkış akımına** göre (`max(TrafoAgAkimA, GirisAkimA)`, `OrtaGerilimHesapServisi`), trafo yoksa ana pano akımına göre (`PanoHesapServisi`) otomatik seçilir. Kanvasta trafo→ana pano hattında (OG bloğu) ve trafosuz durumda ana pano girişinde kesici sembolü + "Giriş Şalteri {A}A" etiketiyle; DXF'te de aynı.
- **Yangın panosu (`PanoTipi.YanginPanosu`):** ana şalter **öncesinden** beslenir (tesiste elektrik kesilse de beslemede kalır), bu yüzden **ana pano yük toplamına katılmaz** (`PanoHesapServisi` alt pano güç toplamı `YanginPanosu`'nu dışlar). Üst çubuktaki `Pano Ekle` tip listesinde ve `PanoDuzenleDialog`'da seçilebilir. Kanvasta koyu kırmızı kutu + "🔥 Ana şalter öncesi" rozeti, DXF'te "YANGIN — ana şalter öncesi" notu, Excel'de ayrı etiket.
- Ana pano adı her zaman `Ana Pano` olmalıdır.
- Projede yalnız bir ana pano olabilir; ikinci ana pano UI ve ViewModel seviyesinde engellenmelidir.
- OG sistemi opsiyonel ve tekildir (tek trafo → tek ana pano); kısa devre kaynağı OG varsa trafo+şebekeden, yoksa eski `TrafoGucKVA`/`SebekeBaslangicIkKA`'dan türetilir (geriye dönük uyumluluk).
- OG trafo boyutlandırma emniyet faktörü varsayılan `1.0`; köşk tipi varsayılan dış mekan beton köşk.
- Tüm görünür metinler Türkçe olmalıdır.

---

## 4. Pano ve Linye Kuralları

Pano tipleri:
- Ana pano
- Tali pano
- Aydınlatma panosu
- Kompanzasyon panosu
- Transfer panosu
- Kontrol panosu
- UPS panosu (kritik pano)
- Yangın panosu (ana şalter öncesi)

Linye kuralları:
- Yük tipi kütüphaneden gelir.
- Linye no, yük tipi, faz tipi, sorti sayısı, birim güç, talep faktörü, kablo mesafesi, sigorta cinsi ve açıklama düzenlenebilir.
- Excel linye aktarımı seçili panoya ekler; `Linye No` opsiyoneldir ve pano içinde yük tipine göre yeniden sıralanır (`A1/A2`, `P1/P2` vb.).
- Sigorta amperi ve akım hesap servisleriyle yeniden hesaplanır.
- Monofaze/trifaze ayrımı korunur.
- Faz dağılımı greedy dengeli dağıtım mantığıyla yapılır.
- `NumberHelper` hem nokta hem virgül ondalık ayracını kabul etmelidir.
- **Linye kontaktörü:** her linyede `KontaktorluMu` bayrağı var; linye tablosunun "Kontaktör" sütunundaki tik ile (her pano tipinde) işaretlenir. İşaretliyse `PanoHesapServisi` kontaktörü linye akımına göre otomatik seçer (`KontaktorSec`), işaretsizse temizler. Sütun hücresinde tik + seçilen kontaktör anma akımı (örn. "25A") gösterilir.
- **Linye darbe kesici (darbe akım anahtarı):** `DarbeKesiciMi` bayrağı + "Darbe Kesici" tik sütunu (kontaktörle aynı mantık). İşaretliyse `DarbeKesiciSec` ile akıma göre seçilir (`DarbeKesiciTanim`, `DarbeKesiciKutuphanesi.json`, varsayılan 16–63A). **Kural:** darbe kesicili ve linye akımı **16A üstü** ise impuls rölesi tek başına yetmediğinden **kontaktör de otomatik eklenir**.
- **Faz ataması gösterimi:** `Linye.FazAtamasiGoster` — trifaze linye üç fazı da kullandığından tabloda "RST", monofaze linye atandığı tek faza göre "R"/"S"/"T" gösterir (tablo bu hesaplanan alana bağlanır).

Aydınlatma panosu:
- Girişinde KAR + sigorta + astronomik zaman rölesi bulunur.
- Sayaçlı/sayaçsız olabilir.
- Aydınlatma linyelerinde kontaktör `KontaktorluMu` bayrağından bağımsız olarak otomatik seçilir (mevcut davranış korunur).

---

## 5. Busbar ve Kablo Kuralları

Busbar:
- Busbar dikey çizilir.
- Busbar uzunluğu kullanıcıdan sabit alınmaz; bağlı panoların pozisyonlarına göre otomatik hesaplanır.
- Çıkış kutuları pozisyona göre sıralanır.
- Her çıkış kutusunda sigorta bulunur.
- Çıkış kutusundan panoya kısa kablo mesafesi girilebilir.
- Busbar segmentlerinde akım, gerilim düşümü ve ısı kaybı hesaplanır.

Kablo:
- Otomatik kablo kesiti alt sınırı `2,5 mm²`dir; bu kural linye, pano besleme, busbar çıkış kablosu ve aynı merkezi servisi kullanan diğer elektrik hesaplarına uygulanır.
- Linye kablosu linyenin kendi yük akımına göre boyutlandırılır (`KabloHesapServisi.LinyeKabloHesapla`); kesit/gerilim düşümü/ısı kaybı `Linye` üzerine yazılır. `PanoHesapServisi.PanoHesapla` her linye için çağırır.
- Kablo hesabı üst pano çıkış sigortasına göre yapılır.
- Busbar bağlı panoda çıkış kutusu sigortası esas alınır.
- Gerilim düşümü gerçek akım ve mesafeye göre hesaplanır; **reaktans dahildir**: `ΔU = k·I·(R·cosφ + X·sinφ)` (k = 2 mono / √3 tri). Kablo birim reaktansı `KabloReaktansOhmPerM ≈ 0.08 mΩ/m`. Kesit seçimi de reaktansı içerir. Tek nokta: `KabloHesapServisi.GerilimDususV`.
- Busbar gerilim düşümü de reaktans içerir (`BusbarTanim.ReaktansMOhmPerM`).
- Isı kaybı `P = n * I^2 * R` (n = iletken sayısı) yaklaşımıyla hesaplanır.
- Direkt kablolu alt pano bağlantıları pano merkezinden geçmemeli; ayrı kablo kanallarıyla çizilmelidir.
- **Kablo kesiti tüm besleme etiketlerinde gösterilir:** kanvasta pano→pano, busbar çıkışı ve ayrı kolon hatlarının etiketleri `{sigorta}A • {mesafe}m • {kesit} mm²` formatındadır (ortak `KesitEk` yardımcısı; kesit boşsa eklenmez). Jeneratör hatlarında da kesit gösterilir (bkz. §7).

Kısa devre (Ik3):
- `KisaDevreHesapServisi` IEC 60909 basitleştirilmiş üç-faz kısa devre hesabı yapar. Zincir boyunca **R ve X ayrı toplanır**; düğüm empedansı `Z = √(R² + X²)`. Kaynak (trafo/şebeke) **saf reaktans** kabul edilir (X/R yüksek → kaynak direnci ihmal); kablo/busbar reaktansı gerilim düşümüyle aynı birim değerlerden gelir (`KabloHesapServisi.KabloReaktansOhmPerM`, `BusbarTanim.ReaktansMOhmPerM`). **Not:** trafo bakır kaybından (`YukKaybiW`) kaynak direnci türetmek ileride bir doğruluk iyileştirmesi olabilir.
- Kaynak empedansı (büyüklük): trafo varsa `Z = uk% · V_LL² / S`; yoksa `SebekeBaslangicIkKA`'dan `Z = V_LL/(√3·Ik)`. Bu büyüklük kaynak X'i olarak kullanılır; kaynak-bara Ik'sı (kablo yokken) değişmez.
- Ik kaynaktan aşağı doğru her kablo/busbar segmentinin R ve X'i eklenerek azalır; her panonun `KisaDevreAkimiKA` alanına yazılır. Reaktans kuadratür birleştiği için aşağı yöndeki Ik, eski yalnız-R aritmetik toplamından biraz yüksektir (daha doğru, kesme kapasitesi seçiminde daha emniyetli).
- **Koruma cihazı + kesme kapasitesi (`KorumaCihaziServisi`):** anma akımı **≤63A → otomatik sigorta (MCB)**, kesme kapasitesi seçenekleri **6/10 kA**; **>63A → TMŞ/kompakt şalter (MCCB)**, kesme **36/50/100 kA**. Eşik `SigortaUstSinirA = 63`. Kesme kapasitesi bara Ik'sının üstündeki ilk seçenektir (cihaz tipine göre kümeden; kütüphane verilirse değerler kütüphaneden türetilir). `KisaDevreHesapServisi` linyelere bunu yazar; `Linye.KorumaCihaziTipi` ("Sigorta"/"TMŞ") tabloda "Cihaz" sütununda gösterilir.
- **Cihaz tipi + kesme kapasitesi elle seçimi (linye tablosu):** "Cihaz" ve "kA" sütunları artık **açılır menü**dür.
  - `Linye.TmsZorla`: ≤63A bir linyeyi kullanıcı **isteğe bağlı TMŞ'ye** çevirebilir ("Cihaz" menüsü). >63A linyeler zaten zorunlu TMŞ → menü kilitli (`SigortaDegistirilebilir=false`). Cihaz değişince kesme kapasitesi yeniden otomatik seçilir (`KesmeKapasitesiManuel=false`).
  - `Linye.KesmeKapasitesiManuel`: "kA" menüsünden elle seçim yapılırsa motor üzerine yazmaz; kullanıcı değeri korunur. Cihaz tipiyle uyumsuz kalırsa (ör. sigortaya dönüşte 50kA) motor `GecerliKesme` ile otomatik değere düzeltir. Seçenekler `Linye.KesmeKapasiteSecenekleri` (Sigorta 6/10, TMŞ 36/50/100).
- **Çıkış sigortaları (alt panolara) tablosu** da düzenlenebilir: yatay kaydırma (`HorizontalScrollBarVisibility`), "Cihaz" (Sigorta/TMŞ) ve "kA" açılır menüleri. Alanlar `Pano.CikisTmsZorla` / `CikisKesmeKapasitesi` / `CikisKesmeKapasitesiManuel`; cihaz akımı `CikisKorumaAkimA` (busbar→çıkış kutusu sig., değilse `UstPanoCikisSigortaA`). Kesme kapasitesi **üst pano barasının Ik'sına** göre `KisaDevreHesapServisi`'de hesaplanır (cihaz bu baraya oturur).
- Proje alanları: `TrafoGucKVA` (0=şebeke), `TrafoUkYuzde` (vars. 4), `SebekeBaslangicIkKA` (vars. 10).

DXF çıktısı:
- `TekHatSemasiDxfServisi.DxfCiktiAl(proje, hedefDosya)` — UI Canvas'ından bağımsız. **Kat-bazlı yerleşim**: panolar kat bantlarına oturur (Çatı en üstte → Zemin → bodrum en altta), ana pano alt katta kaldığından şema **aşağıdan yukarı** okunur (kanvasla aynı). Kat ayrım çizgileri + "Kat: X" etiketleri çizilir. Parent→child bağlantıları **yön bağımsızdır** (çocuk üst veya alt katta olabilir); kat sırası `KatSiraAnahtari` ile fiziksel (Bina'dan bağımsız sağlam).
- netDxf ile katmanlar: `M11-PANO / M11-KABLO / M11-SEMBOL / M11-METIN / M11-BUSBAR / M11-JEN`.
- IEC 60617 tarzı semboller: giriş şalteri, sigorta dikdörtgeni, busbar, jeneratör dairesi (G), transfer (ATS).
- Her pano kutusunda ad/kW/giriş sigortası/Ik; her linyede no/cihaz/faz/kesit/kesme kapasitesi etiketi. **Linye etiketi tüm linye özelliklerini yansıtır:** cihaz tipi (sigorta "16A/B" ↔ "TMŞ"), faz `FazAtamasiGoster` ("RST"/"R"/"S"/"T"), kontaktör (`K{A}A`) ve darbe kesici (`DK{A}A`) işaretleri. Alt panoya giden **çıkış etiketleri** de cihaz tipi (TMŞ) + kesme kapasitesini gösterir (`CikisEtiket`, kablo/busbar kutusu).
- UI: üst çubukta "DXF Dışa Aktar" butonu → `StorageProvider` SaveFilePicker (`*.dxf`).

Excel pano raporu:
- `TekHatSemasiExcelServisi.RaporOlustur(proje, hedefDosya)` tek çalışma kitabında her pano için ayrı sayfa üretir.
- Sayfa adı pano adından gelir; Excel'in 31 karakter sınırı, yasak karakterleri ve çakışan adlar otomatik yönetilir.
- Her sayfada pano giriş bilgileri ile linye/alt pano çıkışları 14 sütunlu, filtrelenebilir tabloda yer alır: pano ve üst pano adı, giriş kablosu/mesafesi/sigortası/KAR; linye no veya alt pano/yük; çıkış sigortası/tipi/kesme kapasitesi; çıkış kablosu/mesafesi/açıklaması.
- Busbar bağlı panolarda busbar ve çıkış kutusu bilgisi; kontrol panolarında buton ataması, damar sayısı ve sinyal kablosu metrajı rapora yansır.
- UI: üst çubukta "Excel Dışa Aktar" butonu → `StorageProvider` SaveFilePicker (`*.xlsx`).

---

## 5b. Orta Gerilim / Trafo / Köşk

Model:
- `ElektrikProje.OrtaGerilim: OrtaGerilimSistemi?` — opsiyonel, tekil (`Jenerator`/`Ups` deseni). `Core/Electric/Models/OrtaGerilim.cs`.
- Tek trafo → tek ana pano. **Geliştirme notu:** çoklu trafo + bara bağlama (çift bara/çift seksiyon, birden fazla AG ana pano) ileride `OrtaGerilimSistemi` içinde liste + bus coupler bölümlemesiyle eklenecek; bara bağlama hücresi kütüphanede tanımlı ama şu an tek seksiyon çizilir.
- Hesap derinliği: ekipman seçimi + şema + kısa devre (Ik) katkısı. **OG koruma koordinasyonu (röle/selektivite) kapsam dışı.**

Enum'lar: `TrafoTipi {Yagli, Hermetik, KuruTip}`, `KoskTipi {BinaAlti, DisMekanBetonKosk, HariciYapi}`, `OgHucreTipi {Giris, Olcu, BaraBaglama, Cikis, TrafoKoruma}`, `OgHucreKorumaTipi {YukAyirici, YukAyiriciSigortali, Kesicili}`, `TrafoCikisBaglantiTipi {Kablo, Bar24, Hucre}`.

Hesap (`OrtaGerilimHesapServisi`):
- Trafo seçimi: `UygunTrafoSec` — yük(kVA)×emniyet ≥ GucKVA olan en küçük standart trafo.
- Trafo akımları: AG = S·1000/(√3·400), OG = S·1000/(√3·V_OG).
- Kaynak empedansı (AG): `Zq + Zt`. Zt = uk%·V_AG²/S; şebeke Zq AG'ye indirgenmiş = V_AG²/Skq (Skq = SebekeKisaDevreMVA·1e6 ya da √3·V_OG·Ik).
- **Kısa devre entegrasyonu:** `KisaDevreHesapServisi.KaynakEmpedansiOhm(proje, kutuphane)` — OG trafosu çözülürse kaynak Zq+Zt'den, yoksa eski `TrafoGucKVA`/`SebekeBaslangicIkKA`'dan. `TumunuHesapla` OG'yi kısa devreden **önce** hesaplar.
- Hücre anma akımı kontrolü: OG bara akımına göre `AnmaAkimYeterliMi`.
- **OG XLPE kablo segmentleri:** `SebekeHucreXlpeKablosu` ve `HucreTrafoXlpeKablosu` ayrı `OgXlpeKablo` nesneleridir. Dialogda her segment için iletken (Alüminyum=`NA2XSY`, Bakır=`N2XSY`), faz kesiti ve metraj girilir. Etiket `N2XSY XLPE Cu • 3x(1x70 mm²) • 18m` biçimindedir; kanvasta ilgili hat üzerinde, sağ OG özetinde ve DXF'te görünür. Değerler proje JSON'unda korunur.
- Çıkış bağlantısı: Kablo → `KabloHesapServisi.ParalelKabloHesapla`; `ParalelKolSayisi` kullanıcı minimumudur, gerçek sonuç ayrı `HesaplananParalelKolSayisi` alanına her hesapta yeniden yazılır. Böylece trafo gücü değişince eski otomatik kol sayısı yeni hesabı kilitlemez. 240 mm²/564 A termal sınırı veya gerilim düşümü gerektirirse kol sayısı artırılır. Örneğin 2000 kVA/400 V trafo AG çıkışı 25 m için `6x(4x240) mm²`, 630 kVA için `2x(4x240) mm²` olur. Paralel kol direnci kısa devre hesabında da dikkate alınır. Bar-24 → seçili `BusbarTanim` R/X; Hücre → etiketli.

Köşk fiziksel yerleşim (`KoskYerlesimServisi`): saf geometri (mm); hücreler ön duvar boyunca genişlikleri kadar yan yana, trafo arka bölmede; köşk zarfı verilmemişse dizilimden otomatik. Hem köşk yerleşim penceresi (`KoskYerlesimWindow`) hem DXF köşk bloğu kullanır.

Çizim:
- Canvas: `DrawOrtaGerilim(og, cx, anaCy, kabloKanalX, ogTopY)` — şebeke → OG bara + hücreler → trafo (iki daire) → AG bağlantı → ana pano. Renk: turuncu (#C8702E). Blok dikey konumu `ogTopY` ile belirlenir.
- **Trafo katı (`OrtaGerilimSistemi.TrafoKat`):** dialogda "Trafo Katı" combosundan seçilir (boş = en üstte / geleneksel OG girişi). Boşken OG bandı (`OgBandYuksekligi` 220px) en üste konur (`_ogTopY=0`); doluyken o katın **üstüne** ayrı bant eklenir ve OG bloğu oraya çizilir → trafo o kat seviyesinde görünür, panolarla çakışmaz. Köşk rozetinde "… • Kat: X". DXF köşk etiketinde "Kat: X".
- DXF: `M11-OG`, `M11-TRAFO`, `M11-HUCRE` katmanları; OG zinciri ana panonun üstüne + köşk planı ayrı blok (kütüphane verilirse). `DxfCiktiAl(proje, hedef, kutuphane?)`.

UI: toolbar `🔌 OG/Trafo` → `OrtaGerilimDialog` (köşk tipi, şebeke, trafo, hücre listesi sırala/ekle/sil, çıkış bağlantısı, köşk yerleşim butonu, canlı özet). Sağ panel `OrtaGerilimPanel`.

Serileştirme: `OrtaGerilim` doğal serileşir (string referanslar). `ProjeyiYukle` AnaPanoAd tutarlılığını kontrol eder; `PanoyuYenidenAdlandir` `OrtaGerilim.AnaPanoAd`'ı da kaskadlar.

---

## 6. Kompanzasyon Kuralları

Kompanzasyon:
- Toplam güç tetik sınırını aşarsa otomatik hesaplanır.
- Kullanıcı kompanzasyon panosu eklediyse tetik gücü altında bile `Proje.Kompanzasyon` sonucu silinmemelidir.
- Elle ayarlanan kademeler hesap sırasında korunmalıdır.

Kondansatör kademeleri:
- Kondansatör kademe sayısı `6`, `12` veya `18` olabilir.
- Kondansatör ve reaktör aynı kademe üzerinde olamaz.
- Kondansatör kademeleri ayrı listede tutulur.

Reaktör/SVC:
- Reaktör ya kapalıdır ya da sabit `3` reaktör kademesi vardır.
- SVC yük sürücü, 3 reaktörün toplam kVAr değerinden büyük/uygun tek sürücü olarak seçilir.
- SVC sürücü manuel seçimi yeterliyse otomatik hesap tarafından ezilmemelidir.

Kompanzasyon panosu:
- Linye girilmez.
- Röle, NH bıçaklı sigorta, kondansatör, reaktör, SVC sürücü ve harmonik filtre otomatik seçilebilir.
- Kullanıcı override açarsa seçilen bileşenler korunur.
- Harmonik filtreli kompanzasyon panosu desteklenir.
- **kVAr tabanlı boyutlandırma (`KompanzasyonPanoBoyutlandir`):** kompanzasyon panosuna yük bağlanmaz; aktif gücü `0` kalır (ana pano yükünü şişirmez), ancak besleme sigortası/kablosu **0 kW'a düşmemelidir**. Banka akımı toplam kVAr'dan türetilir: `I = kVAr×1000/(√3·V) × 1.43` (IEC 60831 harmonik+tolerans faktörü `KondansatorAkimFaktoru = 1.43`). Giriş sigortası, KAR ve besleme kablosu bu akıma göre hesaplanır. `TumunuHesapla` her kompanzasyon panosu için `KompanzasyonPanoBoyutlandir` + `KompanzasyonOtomatikSec` + `PanoKabloHesapla` çağırır.
- **DXF/kanvas çıktısı:** kompanzasyon panosu DXF'te kondansatör kademeleri (IEC 60617 iki paralel plaka sembolü `KondansatorSembol`), reaktör kademeleri (`ReaktorSembol`) ve kademe çıkış sigortalarıyla çizilir (`KompanzasyonCikislariCiz`). Kanvas etiketi kW yerine **kVAr** gösterir.

---

## 7. Jeneratör Kuralları

- **Besleme kapsamı** (`JeneratorGrubu.BeslemeKapsami`): `TesisGeneli` (hedef = ana pano, tüm tesisi yedekler) veya `LokalBolge` (hedef = bir tali pano, yalnız o bölgeyi besler). Kapsam hedef pano tipinden tutarlı türetilir: ana pano → tesis geneli, tali/aydınlatma/UPS panosu → lokal bölge. Transfer panosu hedef olamaz.
- Hedef pano `JeneratorGrubu.AnaPanoAd` alanında tutulur (ad korundu; anlamı "beslenen/hedef pano"). Jeneratör hesabı hedef panonun yüküne göre boyutlandırılır (`Hesapla(grup, hedefPano)`); tesis genelinde ana pano toplamı, lokalde yalnız o tali pano alt ağacı.
- Bir projede birden çok grup birlikte bulunabilir (ör. bir tesis geneli standby + bir lokal acil); her grup kendi hedef panosu + senkron/transfer panosuyla çizilir. Aynı hedefin grubu güncellenir. UI: toolbar "⚡ Jeneratör" (ana pano) + pano sağ-tık "⚡ Jeneratör Ekle (bu bölge/tesis geneli)". Dialog kapsam + hedef pano seçtirir; şemada senkron kutusu üstünde "TESİS GENELİ / LOKAL BÖLGE → <pano>" rozeti.
- Proje düzeyinde `JeneratorGruplari` bulunur; eski tekil `Jenerator` alanı geriye dönük uyumluluk için korunur ve açılışta tek üniteli gruba taşınır.
- Bir jeneratör grubunda aynı anda birden fazla jeneratör ünitesi olabilir; üniteler farklı güçlerde seçilebilir ve senkron panosu üzerinden birleştirilir.
- **Jeneratör güç kablosu mesafeleri:** her ünitenin senkron panoya mesafesi ayrı tutulur (`Jenerator.SenkronPanoKabloMesafeM`); senkron panodan transfer/ara/hedef panoya ortak hat mesafesi grup seviyesindedir (`JeneratorGrubu.SenkronHedefPanoKabloMesafeM`). Dialogdaki jeneratör tablosunda ünite mesafeleri ve ayrı ortak hat alanı düzenlenir. Değerler JSON, kanvas, DXF ve Excel pano raporuna yansır.
- **Jeneratör iç/dış mekân seçimi:** dialogdaki her jeneratör ünitesi satırında `Dışarıda` tiki bulunur (`Jenerator.DisMekandaMi`, varsayılan `false` = içeride). Seçim `.e4p11` JSON'unda ünite bazında korunur. Yalnız dış mekândaki üniteler için Excel M13/M14 metrajına `Jeneratör Aksesuarı / Jeneratör kabini` satırı eklenir; içerideki jeneratör kabin adedini artırmaz.
- **Jeneratör katı (`JeneratorGrubu.Kat`):** dialogda "Jeneratör Katı" combosundan seçilir (boş = hedef pano katı). Doluysa jeneratör/senkron bloğu o **kat bandına** çizilir; ana panodan ortogonal kablo ile bağlanır. Senkron rozetinde "… • Kat: X" gösterilir. DXF'te jeneratör bloğu kat bandına yerleşir (o katta pano varsa) + "Kat: X" etiketi.
- Jeneratör modları:
  - Prime / PRP
  - Standby / ESP
- Emniyet faktörü varsayılan `1.25`.
- Jeneratör gücü hedef pano yüküne göre otomatik seçilebilir; grup hesabında toplam kW/kVA kapasitesi ve ünite bazlı yük payı hesaplanır.
- Senkron/transfer panosu otomatik oluşturulur (`PanoTipi.TransferPanosu`) ve normal kök pano olarak ayrıca çizilmez.
- Senkron/transfer şalteri ATS/MTS olarak kütüphaneden seçilir veya grup toplam akımına göre otomatik atanır.
- **Transfer şalteri manuel/otomatik (`TransferSalterManuel`, hem `Jenerator` hem `JeneratorGrubu`):** kullanıcı şalteri elle seçtiyse `true` → motor üzerine yazmaz; aksi halde her hesapta ana pano akımına göre yeniden seçilir (yük büyüdükçe şalter büyür, "donmaz"). Grup için seçilen şalter tüm ünitelere senkronlanır. `JeneratorEkleDialog` flag'i set eder; "(otomatik tayin)" / seçili şalter ayrımını gösterir.
- **Ortak kablo kesiti formatı:** M11'de monofaze kablo `2xS mm²`, trifaze kablo `4xS mm²`, paralel trifaze besleme `Nx(4xS) mm²` olarak saklanır ve kanvas/DXF/Excel'de aynı metin gösterilir. Pano, linye, busbar çıkış, jeneratör ve trafo kabloları aynı `KabloEtiketiOlustur` / `ParalelKabloHesapla` yolunu kullanır.
- **Jeneratör kablo kesitleri:** her ünite → senkron pano kablosu ünitenin anma akımına göre (`Jenerator.SenkronKabloKesiti`), senkron pano → hedef/ana pano ortak kablosu grup toplam anma akımına göre (`JeneratorGrubu.SenkronHedefKabloKesiti`) `KabloHesapServisi.ParalelKabloHesapla` ile hesaplanır (trifaze, jeneratör gerilimi). Tek 240 mm² kablo için güç besleme kriteri 564 A'dır; akım veya gerilim düşümü tek kabloyla karşılanamıyorsa paralel kol sayısı otomatik artırılır ve sonuç `3x(4x240) mm²` biçiminde gösterilir. `Jenerator.AnmaAkimA` / `JeneratorGrubu.ToplamAnmaAkimA` alanları kesit hesabının dayanağıdır. Kanvasta ünite hattında mesafe+kesit **iki satır** halinde (tek satır jeneratör dairesinin altında kalıyordu); senkron→ana pano hattında `{mesafe} m • {kesit}`. Aynı kablo etiketleri DXF ve Excel çıktısına da yazılır. Jeneratör dairesi (`jenCx = senkronCx + 230`) kesit etiketine yer açacak şekilde senkron barasından uzakta konumlanır.
- Jeneratör anma akımı:
  `kVA * 1000 / (sqrt(3) * V)`
- Transfer panosu normal kök pano olarak ayrıca çizilmemeli; jeneratör görseli içinde tek kaynak olarak temsil edilmelidir.
- Jeneratör bloğu hedef pano alt ağacının sağ sınırından sonraya yerleştirilmeli; kompanzasyon/alt panolarla çakışmamalıdır.

---

## 7b. UPS (KGK) Kuralları

- **Topoloji (proje geneli, `ElektrikProje.UpsTopolojisi`): `Lokal` veya `Merkezi`.** Toolbar'da seçilir (varsayılan `Lokal`), `.e4p11`'e yazılır. Mod değişince mevcut UPS yapısına **dokunulmaz**; yalnız hangi sağ-tık aksiyonlarının göründüğü değişir.
  - **Lokal:** kritik panoların önüne ayrı UPS; yalnız o pano + alt panoları beslenir. Pano sağ-tıkta "🔋 Kritik Panoya Çevir/Çıkar" ve "UPS ile Beslenen Kritik Pano Ekle" görünür; linyede "Kritik Panoya Taşı" **gizli**.
  - **Merkezi:** ana panodan ayrı kolon → tek **merkezi UPS** (`Ups.Merkezi = true`, kritik panosu = "UPS Dağıtım" panosu) → dağıtım altına her kata istenildiği kadar **kat kritik panosu** (`UpsPanosu`). Toolbar "Merkezi UPS Kur/Kaldır"; dağıtım panosu sağ-tıkta "Kat Kritik Panosu Ekle" (kat = toolbar kat seçimi). Merkezi UPS, dağıtım panosunun toplam yüküne (tüm kat kritik panoları dahil) göre mevcut motorla boyutlanır. Lokal aksiyonlar gizlenir.
  - **Linye taşıma (Merkezi'ye özel):** linye satırına sağ-tık → "🔋 Kritik Panoya Taşı" alt menüsü, yalnız linyenin panosuyla **aynı kattaki** kritik panoları (dağıtım hariç) listeler; seçince linye o panoya taşınır (`LinyeyiKritikPanoyaTasi`). Kritik pano içinde standart linye ekleme devam eder. Lokal modda bu menü çıkmaz.
- **Model: ayrı kritik alt pano.** UPS bir cihaz (`Ups`) olarak modellenir; besleyen panonun altına `PanoTipi.UpsPanosu` tipli bir **kritik pano** bağlar. Kritik devreler (acil aydınlatma, sunucu, güvenlik) bu panoya linye/alt pano olarak taşınır. Aşağı-akım hesabı (kablo, KAR, faz, Ik, modül) kritik panoya mevcut motorla uygulanır.
- Lokal modda iki giriş yolu: (a) `UpsEkle` besleyen panonun altına **yeni boş** kritik pano yaratır; (b) `KritikPanoyaCevir` **mevcut** bir tali/aydınlatma panosunu (yükleri+alt panoları korunarak) `UpsPanosu`'na çevirir, besleyen = panonun üst panosudur. Ana/kök/transfer pano çevrilemez.
- Projede **birden fazla UPS** olabilir (`ElektrikProje.Upsler: List<Ups>`). **Bir besleyen pano birden fazla bağımsız UPS kritik panosu besleyebilir** (lokal modda aynı panoya ikinci kritik pano/UPS eklenebilir). UPS artık besleyen panoya göre değil **kritik panoya göre** çözülür (`KritikPanoUps(pano)` / `UpsKritikPanoBul(ups)`); `UpsBul(besleyenPano)` "tekil" anlamı taşımaz. Sağ-tıkta kritik panoya (teal) tıklayınca O UPS düzenlenir, normal panoya tıklayınca **yeni** kritik pano eklenir.
- **Rozete tıklayarak düzenleme:** kanvastaki UPS rozetine (mavi "🔋 UPS …" kutucuğu) **sol tık** doğrudan `UpsEkleDialog`'u o UPS için açar (`UpsEkleDialog(vm, besleyenPano, Ups? duzenlenecek)` 3-parametreli ctor).
- UPS gücü kritik panonun yüküne göre otomatik seçilebilir: `GerekliKVA = kritikKVA × emniyet`; seçilen UPS hem kVA hem kW kriterini sağlamalı. Emniyet faktörü varsayılan `1.25`.
- Giriş akımı: `(P_kritik / η + akü şarj payı) / (√3·V·cosφ_giriş)` — çıkış akımından büyük olmalı.
- **Akü boyutlandırma (IEEE 485 sabit-güç):**
  - Seri blok = `round(DcBaraGerilimV / BlokGerilimV)`.
  - Blok başına gerekli güç = `inverterGirişW / seriBlok`; bir blok yetmezse paralel string eklenir: `ceil(blokBasinaGerekli / blokKapasiteW)`.
  - Akü tipi `AkuTanim.DesarjTablosu` (W/blok @ dakika) üzerinden interpolasyonla değerlendirilir.
  - `AkuYeterliMi`: deşarj sonu gerilimi DC alt sınırını (`seri × DesarjSonuGerilimV ≥ MinDcBaraGerilimV`) sağlamalı ve tablo dolu olmalı.
- UPS kaldırılınca kritik pano **silinmez**; `TaliPano`'ya dönüştürülür (yükler korunur).
- Çizim: kritik pano teal renk (`#1A4A5A`); üstünde UPS+akü rozeti (kanvas) / `M11-UPS` katmanında UPS kutusu + akü sembolü (DXF).
- **Merkezi UPS "ayrı kolon" vurgusu:** kanvasta ana pano → dağıtım besleme hattı teal + kalın, etiket "⚡ UPS Kolonu (Ayrı)", rozet "MERKEZİ UPS". DXF'te aynı dal `M11-UPS` katmanında çizilir ve "UPS Kolonu (Ayrı)" etiketi taşır.

---

## 8. Bina ve Şema Çizimi

- Bina konfigürasyonu bodrum, zemin, normal kat ve çatı bilgisinden oluşur.
- Kat etiketleri üstten alta sıralanır.
- Pano katı kullanıcı tarafından seçilir.
- İlk ana pano eklerken bina konfigürasyon diyaloğu otomatik açılmamalı; üst bardaki mevcut kat seçimi korunmalıdır.
- Şemada kat bantları yatay çizgilerle ayrılır.
- Aynı kattaki direkt alt panolar parent üzerine çizilmemeli; ayrı yatay sütunlara veya uygun istifleme düzenine yerleşmelidir.
- Busbar bağlantıları dikey busbar hattı + çıkış kutusu + yatay dal olarak gösterilir.
- Kablo etiketleri üst üste binmeyecek şekilde kanal/offset mantığıyla çizilmelidir.

---

## 9. Kütüphane Kuralları

Kaynak ağacında temel JSON dosyaları:
- `YukTipiKutuphanesi.json`
- `KacakAkimRolesiKutuphanesi.json`
- `KondansatorKutuphanesi.json`

Runtime'da kullanıcı veri konumunda üretilebilen kütüphaneler:
- Trafo (`TrafoKutuphanesi.json`) — standart kVA × tip (yağlı/hermetik/kuru), uk%, kayıplar, fiziksel ölçüler
- OG Hücre (`OgHucreKutuphanesi.json`) — giriş/ölçü/bara bağlama/çıkış/trafo koruma, koruma tipi, anma, ölçüler
- Busbar
- Astronomik zaman rölesi
- Kontaktör (genişletilmiş: AC-3 9–225A 3P + 4P aydınlatma serisi)
- Darbe kesici (darbe akım anahtarı) — `DarbeKesiciKutuphanesi.json`, varsayılan 16–63A
- Troid akım trafosu — `AkimTrafosuKutuphanesi.json`, troid KAR için primer/sekonder/iç çap/sınıf bilgisi
- Otomatik sigorta / MCB — `SigortaKutuphanesi.json`, ≤63A, kesme 6/10 kA
- TMŞ / kompakt şalter (MCCB) — `TmsKutuphanesi.json`, >63A, kesme 36/50/100 kA
- NH bıçaklı sigorta
- Kompanzasyon rölesi
- Reaktör
- SVC sürücü
- Harmonik filtre
- Motor
- Alternatör
- Jeneratör
- Transfer şalteri

Kütüphane penceresi:
- Yük, KAR, kondansatör, busbar, AZR, kontaktör, kompanzasyon bileşenleri, jeneratör bileşenleri ve harmonik filtreleri düzenleyebilmelidir.
- Varsayılan fallback verileri `ElektrikKutuphaneServisi` içinde korunmalıdır.

---

## 10. Serileştirme ve Uyumluluk

- Proje dosyası `.e4p11` JSON formatındadır.
- JSON ayarları M12 aktarımıyla uyumlu olmalıdır.
- `PropertyNameCaseInsensitive = true` kullanılırken alias property çakışmalarına dikkat edilmelidir.
- `Jenerator.GereklikVAMin` eski yazım alias'ı kaldırılmıştır; case-insensitive ayar eski alanı `GerekliKVAMin` ile eşleştirebilir.
- `AstronomikZamanRolesiTanim.BesgelimGerilimV` alias'ı çakışmadığı için geriye dönük uyumluluk amacıyla korunabilir.
- M12'den gelen `.e4p11` yüklenince `KokPano`, `TumPanolar` listesine ad ile yeniden bağlanmalıdır.
- `Linye`'deki hesaplanan/salt-get property'ler (`ToplamGucW`, `KorumaCihaziTipi`, `SigortaDegistirilebilir`, `KesmeKapasiteSecenekleri`, `FazAtamasiGoster`, `KumulatifGerilimDususGoster`) `[JsonIgnore]`'ludur (Pano ile tutarlı; `.e4p11` şişmesini önler, yükleyince yeniden türetilir).

---

## 11. UI İş Akışı

- Electric modül merkezi üzerinden Tek Hat Şeması açılır.
- Tek Hat penceresi üç ana panelden oluşur:
  - Sol: pano ağacı, kat/bina/busbar/pano komutları
  - Orta: linye formu, tablo, faz dağılımı, şema
  - Sağ: hesap özeti, kompanzasyon, jeneratör, çıkış sigortaları ve uyarılar
- Kompanzasyon panosu seçiliyken linye formu gizlenir, kompanzasyon paneli görünür.
- Alt pano çıkış sigortaları ana/üst pano ekranında görünür olmalıdır.
- Sağ tık pano düzenleme ile ad, kat, tip ve kompanzasyon override ayarları değiştirilebilir.
- Kaydet/Aç `StorageProvider` ile yapılır.
- İlk kaydetmede Farklı Kaydet diyaloğu açılır.
- `Yeni`, `Aç`, `Modüller` ve pencere kapatma işlemleri yalnız gerçek proje değişikliği varsa
  `Kaydet / Kaydetmeden Devam / İptal` onayı gösterir. `Ctrl/Cmd+S` kaydeder,
  `Ctrl/Cmd+Shift+S` Farklı Kaydet açar.
- `.e4p11` kaydı aynı klasörde geçici dosyaya yazılıp atomik değiştirilir; önceki sağlam sürüm
  `<dosya>.e4p11.bak` olarak korunur (`GuvenliDosyaYazmaServisi`).
- Üst proje komutları, hesap ayarları ve pano komutları ayrı/taşabilen satırlardadır; 1340 px ve
  pencerenin asgari genişliğinde hiçbir komut sağdan kesilmemelidir.
- Görünür tüm metinlerde Türkçe karakterler doğru kullanılmalıdır.

---

## 12. Kritik Dikkat Notları

- Ana pano tekil kuralı bozulmamalıdır.
- Kompanzasyon kademe sayısı kullanıcı seçimi hesap sırasında tekrar 6'ya düşmemelidir.
- Kondansatör ve reaktör aynı liste/kademe gibi modellenmemelidir.
- Kompanzasyon panosunda SVC/reaktör seçimi `KompanzasyonSonucu.ReaktorAktif`'e bağlıdır; harmonik filtre ayrı bağımsız bir bileşendir, SVC gereğini belirlemez.
- Ana pano eklendiğinde, ondan önce oluşturulup üst panosu olmayan (öksüz) panolar otomatik olarak ana panonun altına bağlanır; ağaç dışında pano kalmamalıdır.
- Jeneratör ve kompanzasyon aynı anda varken şema çakışmamalıdır.
- Transfer panosu çift çizilmemelidir.
- Busbar uzunluğu pozisyonlardan yeniden düzenlenmelidir.
- Runtime üretilen kütüphane JSON dosyaları repo dosyası gibi varsayılmamalıdır.
- JSON alias eklerken case-insensitive çakışma riski test edilmelidir.

---

## 13. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Electric/Models/`
- `src/Kst.Acoustics.Core/Electric/Calculation/` (`KisaDevreHesapServisi.cs`, `TekHatSemasiDxfServisi.cs`, `UpsHesapServisi.cs`, `AkuHesapServisi.cs` dahil)
- `src/Kst.Acoustics.Core/Electric/Models/Ups.cs`; `KutuphaneTanimlari.cs` içinde `UpsTanim`, `AkuTanim`, `AkuDesarjNoktasi`, `TrafoTanim`, `OgHucreTanim`
- `src/Kst.Acoustics.Core/Electric/Models/OrtaGerilim.cs` (`OrtaGerilimSistemi`, `OgHucre`, `TrafoCikisBaglanti`, OG enum'ları)
- `src/Kst.Acoustics.Core/Electric/Calculation/OrtaGerilimHesapServisi.cs`, `KoskYerlesimServisi.cs`
- `src/Kst.Acoustics.Core/Electric/Data/`
- `src/Kst.Acoustics.Core/Electric/ElektrikKutuphaneServisi.cs`

App:
- `src/Kst.Acoustics.App/Electric/Views/TekHatSemasiWindow.axaml`
- `src/Kst.Acoustics.App/Electric/Views/TekHatSemasiWindow.axaml.cs` — **partial class** (C2): ana dosya yalnız alanlar + ctor; geri kalanı sorumluluk dosyalarında: `.SemaCizimi.cs` (kanvas çizimi), `.Islemler.cs` (pano/linye/kablo/kontrol modu işlemleri), `.UiGuncelle.cs` (tablo/hesap özeti güncelleme), `.Ayarlar.cs` (proje ayarları/UPS topolojisi/kütüphane/busbar/proje işlemleri/kompanzasyon/jeneratör)
- `src/Kst.Acoustics.App/Electric/ViewModels/TekHatSemasiViewModel.cs` — **partial class** (C2): ana dosya spine (alanlar, koleksiyonlar, ctor, PanoOzet, `TumunuHesapla`, `ProjeyiYukle`); geri kalanı: `.PanoBusbar.cs`, `.Kompanzasyon.cs`, `.Jenerator.cs`, `.OrtaGerilim.cs`, `.Ups.cs`
- `src/Kst.Acoustics.App/Electric/Views/KutuphaneWindow.axaml`
- `src/Kst.Acoustics.App/Electric/Views/PanoDuzenleDialog.axaml`
- `src/Kst.Acoustics.App/Electric/Views/JeneratorEkleDialog.axaml`
- `src/Kst.Acoustics.App/Electric/Views/UpsEkleDialog.axaml`
- `src/Kst.Acoustics.App/Electric/Views/OrtaGerilimDialog.axaml`, `KoskYerlesimWindow.axaml`
- `src/Kst.Acoustics.App/Electric/Views/BusbarEkleDialog.axaml`
- `src/Kst.Acoustics.App/Electric/Views/PanoBusbarBaglaDialog.axaml`
- `src/Kst.Acoustics.App/Electric/Views/LinyeDuzenleDialog.axaml`

Test:
- `src/Kst.Acoustics.Tests/` — M11 hesap motoru izole testleri: `ElektrikHesapTests.cs` (kablo kesiti, faz dağılımı, linye kablo, kompanzasyon Q/kademe, kısa devre Ik3, jeneratör, selektivite, DXF smoke). Aktarım testleri: `TekHatAktarimTests.cs`.

---

## 14. Korunması Gereken Regresyon Alanları

- Linye akım ve sigorta hesabı
- Excel linye şablonu üretimi ve seçili panoya aktarım
- Faz dağılımı ve dengesizlik oranı
- Kablo kesiti, gerilim düşümü ve ısı kaybı (pano + linye)
- Kısa devre Ik3 (trafo/şebeke kaynak empedansı, aşağı azalma, kesme kapasitesi seçimi)
- DXF tek hat çıktısı (geçerli netDxf belgesi + katmanlar)
- DXF kat-bazlı yerleşim: aşağıdan yukarı (zemin altta, üst kat üstte) + kat ayrım çizgileri/etiketleri; yön bağımsız parent→child bağlantısı
- Kablo/busbar reaktansı (gerilim düşümü R+X)
- Kısa devre reaktansı (B): zincir Z = √(R²+X²); kaynak saf reaktans, kablo/busbar R+X. Kaynak-bara Ik (kablo yok) ve OG `Zq+Zt` korunur; aşağı yönde kuadratür birleşim
- Kümülatif gerilim düşümü (A1): segment ΔU'ları zincir boyunca additif toplanır (kaynak→pano→linye, busbar dahil); toplam sınırı aşan pano/linye `GerilimDususuAsiyor` ile işaretlenir
- Kablo kesiti yeterliliği (A2): otomatik beslemede azami paralel kol tavanı (12) aşılırsa/segment ΔU karşılanmazsa `KabloKesitiYeterli=false`
- Hesap sırası (A3): `TumunuHesapla` kompanzasyon panosu boyutlandırma+kablosunu **kısa devreden önce** kesinleştirir; kompanzasyon panosunun Ik'sı gerçek kablosuyla bulunur (eski sırada varsayılan 2,5 mm² ile çıkıyordu)
- Jeneratör lokal grup (A4): hedef tali panosu bulunamayan **lokal** grup atlanır (ana panoya/tüm tesise boyutlanmaz); yalnız **tesis geneli** grup ana panoya düşebilir (`BeslemeKapsami`)
- Linye sigorta aralığı (C3): büyük trifaze linyeler 250A'da kalmaz, üst standartlara (315/400/500/630A) çıkar
- Reaktör akort yüzdesi (5.67/7/14) reaktör seçimi
- Eşzamanlılık (diversite) faktörü alt pano güç toplamı
- Pano toplam güç, KAR ve sayaç tayini
- Ana pano giriş sigortası: doğrudan linye olmasa bile alt pano yükleriyle hesaplanır; yüksek akımda 630A üstü standartlara çıkmalı
- KAR seçimi: 100A altı klasik 4P röle, 100A üstü troid röle; troid röleye uygun akım trafosu atanması ve eski kütüphaneye troid/AT varsayılanlarının eklenmesi
- Busbar segment hesabı
- Aydınlatma panosu AZR/kontaktör seçimi
- Linye `KontaktorluMu` bayrağı: işaretli linyeye (her pano tipinde) akıma göre kontaktör otomatik seçilir; işaret kalkınca temizlenir
- Linye `DarbeKesiciMi` bayrağı: işaretli linyeye akıma göre darbe kesici seçilir; akım 16A üstüyse kontaktör de otomatik eklenir
- Kontrol panosu (`PanoTipi.KontrolPanosu`): güç hesabına/üst pano çıkış sigortasına katılmaz; kat seçimiyle bağımsız pano olarak eklenir. İçine kontrol butonu eklenirken kat → pano → darbe kesicili linye seçilir; `Hepsini Ekle` seçili kaynak panodaki bütün uygun darbe kesicileri tek seferde atar. Bir linye yalnız tek kontrol panosuna atanabilir. Sinyal kabloları canvas'ta çizilmez; tabloda kaynak pano bazında kablo tipi, kesit ve metraj girilir. Aynı kaynak panodan birden fazla darbe kesici seçilirse tek kablo satırında damar sayısı artar. DXF'te kontrol panosu → kaynak pano bağlantısı `M11-KONTROL` katmanında çizilir ve `{tip} • {damar}x{kesit} mm² • L={metraj}m` etiketi gösterilir
- Pano gövdesi elektriksel pano tipinden bağımsız seçilir: varsayılan `Duvar Tipi`, alternatif `Dikili Tip`. Modül alanı `modül sayısı x 0,01 m²`, klemens alanı `klemens sayısı x 0,005 m²` olarak hesaplanır ve gerekli alan bu ikisinin toplamıdır. Linye klemensleri monofaze için 3, trifaze için 5 adettir. Gerekli alan `1,00 m²`yi aşarsa duvar tipi pano otomatik olarak `Dikili Tip`e çevrilir ve ilk derinliği 90 cm olur. Standart ön yüz ölçüsü `0,10 x 0,20 m` ile `1,00 x 1,00 m` arasındaki katalogdan otomatik atanır; otomatik seçim kapatılırsa kullanıcı ölçüsü korunur. Dikili pano derinliği 40/50/60/70/80/90 cm seçeneklidir ve varsayılan 90 cm'dir.
- **Pano metraj kimliği (`PanoBoyutlandirmaServisi.MetrajEtiketi` / `MetrajAnahtari`):** ekran, Excel ve DXF metrajı ortak tekil etiket kullanır. **Duvar tipi** seçilen **standart ön-yüz alanı**na yuvarlanır (ör. gerekli 0,21/0,26/0,29 m² → "Duvar Tipi 0,30 m²"); ham gerekli alan öne çıkmaz. **Dikili tip** yalnız **derinlikle** gösterilir ("Dikili Tip 90 cm derinlik"), m² yazılmaz. Metrajda (Excel M13 + DXF sağdaki "PANO METRAJ ÖZETİ" bloğu) aynı standart alana/derinliğe oturan farklı panolar **tek satırda** adet olarak toplanır; ayrı "toplam m²" satırı yoktur.
- **Excel M13 → M14 toplu aktarım ekipmanları:** kompanzasyon reaktörü ve SVC yük sürücüsü ile jeneratör transfer şalteri ayrı metraj satırlarıdır. Grup düzeyi transfer şalter adı boş eski projelerde ünite üzerindeki ad kullanılır; bir jeneratör grubunda şalter yalnız bir kez sayılır. Jeneratörün senkron/transfer panosu genel `Pano` metrajına ikinci kez katılmaz; `Jeneratör Sistemi / Senkron / Transfer panosu` satırında tek kez yer alır. Metraj anahtarlarındaki gereksiz boşluklar normalize edilerek aynı malzemenin biçim farkıyla yinelenmesi önlenir.
- **Excel M13 → M14 geniş ekipman metrajı:** seçili/modelde bulunan KAR ve troid AT, bireysel KAR, kontaktör, darbe akım anahtarı, AZR, sayaç, harmonik filtre, UPS+akü+bypass, OG hücresi+trafo+köşk+AG bağlantı ekipmanı; ayrıca jeneratör egzoz/yakıt tankı/marş aküsü aktarılır. Türetilen aksesuarlar: pano klemensi, PE barası/bağlantı seti, faz lambası, multimetre, yardımcı röle; AG pabuç-rakor-başlık seti, OG başlık takımı, kablo mufları; busbar askı/sonlandırma/ek modülü. Muf varsayımı AG için 100 m, OG için 500 m tambur boyudur; busbar askısı 1,5 m, ek modülü 3 m aralıkla hesaplanır. Bu satırlar M14 tarafından ilk sayfadaki standart altı kolonlu metraj tablosundan okunur.
- UPS anma gücü hesap sırasında `Ups.SeciliUpsGucKVA` alanına yazılır; kanvas, hesap özeti ve DXF aynı seçilen standart gücü gösterir (ör. yaklaşık 23 kVA ihtiyaç için seçilen `30 kVA`). UPS kanvas rozeti standart düğme tıklamasıyla açılır ve canvas pointer yakalama kaybında pan durumu sıfırlanır.
- Excel raporunun ilk sayfası `M13 Metraj Özeti`dir. **Tüm kablolar tek "Kablo" bölümünde, kablo tipi (kesit+tip) başına TEK satırda toplanır** (`KabloSatirlariniBirlestir`): linye, pano besleme, busbar kutu-pano, trafo-ana pano, jeneratör-senkron ve senkron-hedef aynı tip ise (örn. "NHXMH 4x240 mm²") tek satırda birleşir; kategori güç/sinyal/OG XLPE'dir. Paralel kablolarda satın alma metrajı `mesafe x kol`'dur; güzergâh detayları satır notuna düşer. Üst kartlar: Güç / Sinyal-OG / Tüm Kablolar. **Koruma cihazları da cihaz tipi (tip+amper+kesme) başına TEK satırda toplanır** (`KorumaCihaziSatirlariniBirlestir`): aynı TMŞ/sigorta giriş/çıkış/linye/busbar/kademe gibi farklı güzergâhlarda geçse de adetleri tek satırda; kategori TMŞ / otomatik sigorta (MCB) / NH bıçaklı, güzergâhlar nota düşer. Busbar metreleri ve amperaja göre çıkış kutuları toplulaştırılır. **Panolar metraj kimliğine göre** (duvar→standart ön-yüz alanı 0,30 m², dikili→derinlik 90 cm) tek satırda; kondansatör/reaktör güç bazında, röleler kademe bazında, jeneratör/senkron pano/transfer şalteri ayrı toplulaştırılır.
- Faz ataması tabloda trifaze için "RST", monofaze için tek faz (`FazAtamasiGoster`)
- Koruma cihazı: ≤63A sigorta (MCB 6/10kA) / >63A TMŞ (36/50/100kA); kesme kapasitesi bara Ik'sına göre; `KorumaCihaziTipi` gösterimi; Sigorta+TMŞ kütüphaneleri round-trip
- Koruma cihazı elle override: ≤63A linye `TmsZorla` ile TMŞ'ye çevrilir, kesme kapasitesi TMŞ setine geçer; manuel kesme (`KesmeKapasitesiManuel`) korunur, cihaz tipiyle uyumsuz kalırsa motor düzeltir
- Çıkış koruma override: alt panoya giden cihaz `CikisTmsZorla` ile TMŞ; `CikisKesmeKapasitesi` üst pano Ik'sına göre; manuel çıkış kesmesi korunur
- DXF: linye etiketinde TMŞ/DK işaretleri + faz "RST" yansır (`Dxf_LinyeKorumaVeFazYansir`)
- Kompanzasyon tetik ve cosφ hedefi
- Kompanzasyon 6/12/18 kademe seçimi
- Reaktör 3 kademe ve SVC sürücü kapasitesi
- Harmonik filtre seçimi
- Jeneratör PRP/ESP ve transfer şalteri seçimi; transfer şalter kütüphanesi ATS için 100–6300A aralığını kapsar ve eski kütüphane dosyalarına yüksek akım ATS varsayılanları eklenir
- Jeneratör besleme kapsamı: tesis geneli (ana pano) + lokal bölge (tali pano) birlikte; lokal grup yalnız hedef pano yüküne boyutlanır; `BeslemeKapsami` round-trip; hedef pano silinince grup temizlenir
- DXF jeneratör görünürlüğü: `M11-JEN` katmanında senkron pano + jeneratör geometrisi/metni yazılır; hedef pano adı eski/boş kalırsa hesap servisindeki ana pano fallback'iyle çizim atlanmaz
- UPS kVA/kW seçimi ve giriş>çıkış akımı; akü seri blok = DC bara / blok gerilimi; otonomi→paralel string
- UPS kritik panoya çevirme: mevcut pano `UpsPanosu`'na döner (linye/alt pano korunur), besleyen=üst pano, geri alınca `TaliPano`; ana/transfer pano çevrilemez; **aynı besleyen panoya ikinci UPS kritik panosu eklenebilir** (kabul edilir)
- Diversite besleme revizyonu: `TalepAkimA = GirisAkimA × ks`; ks düşünce giriş/çıkış sigortası, KAR ve besleme kablosu küçülür
- Yangın panosu (`PanoTipi.YanginPanosu`): ana pano yük toplamına eklenmez; kanvas/DXF özel gösterim
- Giriş şalteri (`Pano.GirisSalteriA`): trafo varsa AG akımına, yoksa ana pano akımına göre otomatik atanır
- Transfer şalteri otomatik güncelleme: `TransferSalterManuel=false` iken yük büyüyünce yeniden seçilir, manuel seçim korunur
- Kompanzasyon kVAr boyutlandırma: aktif güç 0 kalır, besleme banka akımından (kVAr×1.43) hesaplanır; DXF'e kondansatör/reaktör kademeleri yazılır
- Jeneratör kablo kesiti: ünite→senkron ve senkron→hedef kesitleri anma akımına göre hesaplanır; mesafe 0 ise kesit boş
- UPS topolojisi (Lokal/Merkezi) `.e4p11` round-trip; Merkezi kurulum (dağıtım panosu + `Ups.Merkezi`), kat kritik panosu dağıtım altında, merkezi UPS dağıtım toplam yüküne boyutlanır; linye kritik panoya taşıma aynı kat hedefleriyle
- OG trafo seçimi (yük×emniyet), trafo AG/OG akımı, kaynak Zq+Zt → Ik3 (OG yokken eski davranış korunur)
- OG hücre anma akımı kontrolü, çıkış bağlantısı (kablo/Bar-24) boyutlandırma
- Köşk yerleşim geometrisi (çakışmasız dizilim), OG DXF katmanları (M11-OG/TRAFO/HUCRE)
- OG `.e4p11` round-trip; yeniden adlandırma `OrtaGerilim.AnaPanoAd` kaskadı
- Ana pano tekil kuralı
- Pano silme (`TekHatSemasiViewModel.PanoSil`): alt ağaç + busbar (besleyen çıkış kutusu ve panodan çıkan busbarlar) + jeneratör grubu/tekil + UPS + OG referansları temizlenir; dangling referans kalmamalı
- `.e4p11` JSON round-trip
- Kaydedilmemiş değişiklik koruması: temiz projede gereksiz onay çıkmamalı; değişiklikten sonra
  Yeni/Aç/Modüller/X akışları veri kaybına izin vermemeli; başarılı kayıt sonrası proje temiz sayılmalı
- M12 Tek Hat aktarımı sonrası KokPano re-link
- Jeneratör serileştirme alias çakışmasının tekrar oluşmaması

---

## 15. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M11/M12 aktarım regresyonları dahil mevcut test paketi geçmeli
- 2026-08-07 güvenilirlik/UX doğrulaması: çözüm build **0 hata, 0 uyarı**; özel regresyon paketi
  **668/668** geçti. M11'e değişiklik parmak izi, Yeni/Aç/Modüller/X kayıt onayı, atomik `.bak`
  kayıt, kaydetme kısayolları ve taşmayan iki satırlı üst/pano araç çubukları eklendi.

---

## 16. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Hesap değişikliği için önce `Core/Electric/Calculation` dosyalarını incele.
4. Model/serileştirme değişikliği için `Core/Electric/Models` dosyalarını ve JSON uyumluluğunu kontrol et.
5. UI değişikliği için `TekHatSemasiWindow` ve `TekHatSemasiViewModel` dosyalarını incele.
6. Kütüphane değişikliği için `ElektrikKutuphaneServisi` ve `KutuphaneWindow` alanlarını kontrol et.
7. M12 aktarımını etkileyen değişikliklerde `.e4p11` round-trip testini koru.
8. Görünür UI metinlerinde Türkçe karakter kontrolü yap.
