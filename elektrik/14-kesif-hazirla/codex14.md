# M14 Codex — Keşif Hazırla

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap kuralları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M14 — Keşif Hazırla |
| Amaç | Poz kütüphanesi ve çoklu keşif tabloları ile elektrik keşfi hazırlamak |
| Proje uzantısı | `.e4p14` |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Faz 2 tamamlandı: M11/M13 metraj excel'i → otomatik keşif + öğrenen/otomatik Poz Atama + Referanslar |

---

## 2. Modül Kapsamı

Faz 1 aktif kapsam:
- Poz kütüphanesi yönetimi
- Birim fiyat listeleri yönetimi
- Excel/CSV/TSV poz içe aktarma
- Özel poz yönetimi
- Birden fazla keşif tablosu oluşturma
- Kütüphaneden poz seçip tabloya satır ekleme
- Miktar, birim fiyat, satır toplamı ve tablo toplamı
- Ana icmal ve tablo kırılımı
- Aynı poz/birim için tablo bazlı kırılım
- Farklı birim fiyat uyarısı
- JSON proje kaydet/aç
- Excel keşif raporu dışa aktarma: ilk sayfa konsolide genel icmal, ikinci sayfa tablo adı/toplam özeti,
  sonraki sayfalar her keşif tablosunun satır ayrıntıları

Faz 2 (tamamlandı) — Otomatik metraj aktarımı + öğrenen eşleşme:
- M11'in ürettiği `..._pano_listeleri.xlsx`'in ilk sayfası ("M13 Metraj Özeti") metraj referansı olarak yüklenir.
  Başlık 1. satırda DEĞİL (M13'te 7. satırda); başlık satırı içerik taranarak bulunur (`KesifMetrajImportServisi`).
- Kolon yapısı: `Ana Grup | Alt Grup | Teknik Özellik | Birim | Miktar | Açıklama`. Üst başlık/özet satırları atlanır.
- **Poz Atama** penceresi (toolbar, "Ana İcmal" yanında): tanımsız metraj kalemleri ile poz kütüphanesi
  pozları manuel eşleştirilir. Eşleşme **global öğrenme veritabanına** (AppData) yazılır → sonraki projede
  aynı metraj otomatik tanınır. Tanımlı eşleşmeler görülebilir/güncellenebilir/silinebilir.
- Poz Atama'daki **Tümünü Otomatik Ata**, birim + alan türü + model + kesit + amper/kA/kVAr/Hz vb.
  teknik değerleri puanlayarak en uygun aktif/fiyatlı pozu öğrenir; güvenli aday bulamadıklarını eşlenmemiş
  bırakır. Sonuçlar mevcut "Eşleşmeyi Güncelle" akışıyla kullanıcı tarafından düzeltilebilir.
- **Referanslar** penceresi (toolbar): metraj excel'lerini yönetir — Excel Yükle / Excel Sil / Excel Şablonu.
  Bir excel silinince ondan üretilen otomatik satırlar kaldırılır; öğrenilen eşleşmeler KORUNUR.
- Eşleşmesi olan kalemler **seçili (aktif) keşif tablosuna** `KaynakTipi=Otomatik`, `KaynakModul`,
  `KaynakReferansId` etiketiyle yazılır (idempotent: referans yeniden işlenince eski otomatik satırlar temizlenir).
- Eşleşme anahtarı (tam kimlik): **Ana Grup + Alt Grup + Teknik Özellik + Birim** (`KesifEslemeAnahtar.Uret`).

Faz 2 kararları (kullanıcı onayı):
- Anahtar = tam kimlik (Ana+Alt+Teknik+Birim). Otomatik satırlar = seçili tabloya. Bir kalem = tek poz.
  Referans saklama = projede yalnız ayrıştırılmış metraj + dosya adı (orijinal excel gömülmez).

Faz 2.1 (tamamlandı) — Hiyerarşik tablolar + Proje Bilgileri:
- Keşif tablolarına **alt tablo** eklenebilir; **en fazla 3 seviye** (tablo → alt tablo → alt-alt tablo,
  `KesifTabloAgaci.MaksimumDerinlik`). Model: `KesifTablosu.AltTablolar` (özyinelemeli ObservableCollection).
- **Üst tablo yalnız başlıktır** (kullanıcı kararı): alt tablosu olan tabloya doğrudan satır eklenemez;
  satırı olan tabloya da alt tablo eklenemez. Satırlar yalnız **yaprak** tablolarda. (`YaprakMi`/`AltTabloVarMi`)
- Toplamlar özyinelemeli: `KesifTablosu.Toplam = kendi satırları + alt tablo toplamları`; `GenelToplam`
  en üst tabloların toplamı. İcmal/birleştirme/otomatik temizleme `KesifTabloAgaci.TumTablolar` ile tüm
  ağacı dolaşır. Otomatik metraj satırları yalnız yaprak seçili tabloya yazılır.
- UI: "Keşif Tabloları" ListBox → **TreeView** (ağaç). Toolbar'a "Alt Tablo Ekle" butonu. `TabloSil`
  ebeveynden (alt tabloysa) ya da en üst listeden siler. Seçim TreeView `SelectionChanged` ile `SeciliTablo`'ya bağlanır.
- **Proje Bilgileri** paneli: tablo ağacının sağındaki künye formu (İşveren, Yüklenici, İl/İlçe, Ada/Parsel,
  Sözleşme No, Tarih) → `KesifProje.Bilgiler` (`KesifProjeBilgileri`). Excel "Genel İcmal" alt başlığına künye yazılır.
- **Excel raporu hiyerarşik**: detay sayfası yalnız yaprak (satırlı) tablolar için; "Tablo Özeti" ağacı
  girintili gösterir (üst tablolar kalın + alt toplam, yaprak toplam detay sayfasına bağlı); Genel Toplam =
  `proje.GenelToplam` (girintili liste alt toplamları tekrarladığından SUM kullanılmaz).

Faz 2 sonrası açık kalan (ileride):
- Bir kaleme birden çok poz (malzeme + işçilik + montaj) eşleme.
- M12/M15 vb. farklı yapıdaki metraj excel'leri için kolon eşleme genelleştirmesi.
- Keşif için DXF cetvel çıktısı (şu an yalnız Excel raporu var).

Kapsam dışı:
- Kamu poz veritabanı entegrasyonu
- Online fiyat güncelleme
- Hakediş
- Yaklaşık maliyet kapakları
- Pursantaj
- İhale dosyası üretimi

---

## 3. Veri Modeli Kuralları

Poz:
- `PozNo`
- `BirimAdi`
- `Birim`
- `BirimFiyat`
- `Kategori`
- `Not`
- `AktifMi`
- Özel pozlarda `SonGuncellemeTarihi`

Keşif projesi:
- Proje adı
- Birim fiyat listeleri
- Manuel/özel pozlar
- Keşif tabloları
- Oluşturma/güncelleme tarihleri

Keşif tablosu:
- Ad
- Satırlar
- Not
- Toplam computed değerdir

Keşif satırı:
- Poz değerlerinin kopyasını saklar.
- Miktar ve birim fiyat düzenlenebilir.
- Satır toplamı computed değerdir.
- `KaynakTipi` ileride manuel/otomatik ayrımı için korunur.
- Aynı tabloda aynı `PozNo + Birim` ikinci satır olarak bulunamaz; miktarlar tek satırda toplanır.
  Eski projeler açılış/hesap yenilemede otomatik temizlenir. Farklı fiyatlı eski satırlar birleşirken
  toplam tutarı koruyan ağırlıklı ortalama birim fiyat kullanılır.
- Birden çok otomatik referans aynı poza gelirse tek satırdaki `OtomatikKatkilar` referans bazlı miktarı
  korur; referans silme yalnız kendi katkısını düşürür. Manuel+otomatik birleşim `KaynakTipi=Karma` olur.

Kalıcılık:
- `PozKutuphane` doğrudan JSON'a yazılmamalıdır; aktif listeler + manuel pozlardan yeniden oluşturulur.
- Liste pozları ve manuel pozlar kaydet/aç round-trip sonrası tekilleşmiş biçimde korunmalıdır.
- Computed toplam alanları JSON'a yazılmamalıdır.

---

## 4. Hesap Kuralları

Satır toplamı:

```text
Satır Toplamı = Miktar * Birim Fiyat
```

Tablo toplamı:

```text
Tablo Toplamı = tablodaki satır toplamları
```

Genel toplam:

```text
Genel Toplam = tüm keşif tablolarının toplamı
```

Ana icmal:
- Keşifte kullanılan bütün poz/birimler gösterilir; yalnız tekrar edenler değil.
- Tek tabloda kullanılan pozlar da kaynak tablo ve miktar bilgisiyle görünür.
- Birden çok tabloda geçen pozlar tek konsolide satırda toplanır.
- Kırılımda her tablo ayrı miktar ile gösterilir.
- Aynı `PozNo` farklı birim fiyatla kullanılmışsa `BirimFiyatFarkiVar` uyarısı verilmelidir.
- Toplam tutar, satır bazlı gerçek tutarların toplamından hesaplanır.
- `PozNo` boş gelen satırlar yalnız birime göre birleştirilmez; imalat adı da gruplama anahtarına girer.

---

## 5. Birim Fiyat Listesi Kuralları

- Birim fiyat listeleri ayrı liste olarak saklanır.
- `BF Listesi Ekle` doğrudan Excel/CSV/TSV dosya seçicisini açar; boş birim fiyat listesi oluşturma akışı yoktur.
- Yeni liste yüklendiğinde önceki listeler otomatik pasife alınır.
- Pasif liste silinmez; kullanıcı tekrar aktif yapabilir.
- Birden fazla liste aynı anda aktif olabilir.
- Poz kütüphanesi aktif listelerin pozları + manuel pozlardan oluşur.
- Manuel pozlar liste aktif/pasif değişimlerinden etkilenmez.
- Liste içinde aynı `PozNo` varsa tekilleştirme yapılır; son değer kazanır.
- Varsayılan `birim_fiyat_042026.xlsx` projeden kaldırılmıştır; kullanıcı kendi listesini ekler.

---

## 6. İçe Aktarma Kuralları

Desteklenen formatlar:
- Excel
- CSV
- TSV

Başlık eşleştirme:
- Türkçe karakterlerden bağımsız normalize edilir.
- Desteklenen temel başlıklar:
  - Poz No: `Poz No`, `PozNo`
  - Birim adı: `Birim Adı`, `İmalat Adı`, `Tanım`, `Tanımı`, `Poz Adı`, `Açıklama`
  - Birim: `Birim`
  - Birim fiyat: `Birim Fiyat`, `Fiyat`
  - Ek: `Kategori`, `Not`

Excel okuma:
- Başlık hücrelerinin gerçek kolon numarası kullanılmalıdır; boş başlık hücreleri kolon kaydırmamalıdır.
- Sayısal hücreler kültürden bağımsız doğrudan okunmalıdır.
- Para/sayı parse işlemi hem `1.107.437,49` hem `1234.56` gibi formatları doğru okumalıdır.
- Küsuratsız binlik formatlar (`1.107.437`, `1,107,437`) ondalık sanılmamalıdır.
- Aynı `PozNo` tekrar içe aktarılırsa mevcut poz güncellenir.
- CSV okuma quoted alanları ve noktalı virgül ayracını desteklemelidir.

---

## 7. UI İş Akışı

Ana pencere:
- Üst: dosya/komut çubuğu ve `Ana İcmal` butonu
- `Kaydet`, açılmış veya daha önce kaydedilmiş mevcut `.e4p14` yoluna doğrudan yazar; yalnız yeni ve
  henüz yolu olmayan projede dosya seçtirir. `Farklı Kaydet` her zaman yeni konum/ad seçtirir.
- Farklı Kaydet önerisi uzantısız `kesif` adıdır; dosya seçici `.e4p14` uzantısını yalnız bir kez ekler.
- `Yeni`, `Aç`, `Geri` ve pencere kapatma işlemleri yalnız gerçek proje değişikliği varsa
  `Kaydet / Kaydetmeden Devam / İptal` onayı gösterir. `Ctrl/Cmd+S` ve
  `Ctrl/Cmd+Shift+S` kısayolları desteklenir.
- Proje JSON'u UI thread'inde üretilir, aynı klasörde geçici dosyaya yazılır ve atomik olarak hedefe
  geçirilir; önceki sağlam sürüm `<dosya>.e4p14.bak` olarak korunur.
- Sol/orta bölünmüş alan:
  - Üst: keşif tabloları ve seçili tablo satırları
  - Alt: birim fiyat pozları veya özel pozlar
- GridSplitter ile üst/alt alan yeniden boyutlandırılabilir.
- Keşif tablosu ad listesi ile seçili tablonun satır grid'i arasında ikinci yatay GridSplitter bulunur;
  sabit yükseklik yoktur, liste kullanıcı tarafından yukarı/aşağı sürüklenerek büyütülüp küçültülebilir.
- Ana icmal ayrı `KesifIcmalWindow` penceresinde açılır.
- Ana İcmal, Poz Atama ve Referanslar pencereleri M14'e sahipli (`Show(this)`) açılır; alt pencere
  kapanınca ana M14 penceresi görünür ve etkin kalır.
- Sağ alt: `Excel Çıktısı` butonu; `.xlsx` kayıt konumu seçtirir.

Poz alanı:
- Birim fiyat listeleri sürekli tablo olarak görünmez; flyout ile yönetilir.
- Arama alanı poz no ve birim adı üzerinde canlı filtreleme yapar.
- Birim fiyat modu ve özel poz modu ayrı tutulur.
- Kütüphane grid'i salt okunurdur.
- Pozu tabloya ekleme çift tık ile yapılabilir.
- Keşif satırı çift tık ile silinebilir; miktar kolonu bu davranıştan hariçtir.

Poz Atama:
- Sol arama atanacak poz no/ad listesini; sağ arama eşlenmemiş metraj kalemleri ile tanımlı
  eşleşmeleri birbirinden bağımsız ve canlı filtreler.
- Metraj araması ana/alt grup, teknik özellik, birim ve açıklamada; eşleşme araması bunlara ek
  olarak poz no ve poz adında çalışır. Başlıklar `filtrelenmiş/toplam` kayıt sayısını gösterir.

Özel pozlar:
- `OzelPozlarWindow` ayrı penceredir.
- Özel poz ekleme/silme/düzenleme burada yapılır.
- Özel poz güncelleme tarihi değişiklikte otomatik güncellenir.

Tablo adı:
- Ana ekrandaki büyük isim TextBox kaldırılmıştır.
- Tablo adı çift tıkla açılan `RenameTableWindow` üzerinden değiştirilir.
- İsim değişince icmal kırılımları güncellenmelidir.

---

## 8. Görünüm ve Format Kuralları

- Para alanları uygun yerlerde `N2 ₺` formatıyla gösterilir.
- Düzenlenebilir `BirimFiyat` kolonunda para simgesi/string format kullanılmamalıdır; parse sorunları doğurur.
- DataGrid yazı boyutları kompakt olmalıdır.
- M14 içindeki tüm DataGrid sütun başlıkları `↕` ile sıralanabilir olduğunu gösterir; metin alanları
  alfabetik, miktar/fiyat/tutar alanları sayısal, tarih alanları kronolojik sıralanır. Tıklama yönü
  artan/azalan olarak değiştirir.
- Seçili keşif tablosu ListBox satırında okunabilir kontrast kullanılmalıdır.
- Seçili tablo arka planı lacivert, yazı beyaz olmalıdır.
- `DataGridColumn` binding'leri görsel ağaç dışında olduğundan görünürlük kod-arkasında yönetilebilir.
- Görünür tüm metinler Türkçe olmalıdır.

---

## 9. Kritik Dikkat Notları

- Satırlar poz kütüphanesinden bağımsız kopya saklar; kütüphane fiyatı değişince eski satırlar otomatik değişmemelidir.
- `PozKutuphane` JSON'da iki kez yazılmamalıdır.
- Manuel pozlar `ManuelPozlar` üzerinden kalıcı tutulmalıdır; `PozKutuphane` çalışma zamanı birleşik listedir.
- Özel poz eklenirken benzersiz `PozNo` üretilmelidir; aynı varsayılan poz no icmalde yanlış birleşmeye yol açar.
- Çift tık satır silme, kolon başlık metnine bağlı kalmamalıdır; kolon referansı kullanılmalıdır.
- Pencere ctor sırası ve kolon referansları dikkatli kurulmalıdır.
- Aktif/pasif liste değişince filtrelenmiş poz kütüphanesi yenilenmelidir.
- Son özel poz silindikten sonra `ManuelPozlar` içinde eski kopya bırakılmamalıdır; aksi halde kütüphane yenilemede poz geri döner.

---

## 10. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifModels.cs`
  (Faz 2: `KesifMetrajSatiri`, `KesifReferansDosyasi`, `KesifPozEsleme`, `KesifEslemeAnahtar`,
   `KesifProje.ReferansDosyalari`, `KesifSatir.KaynakReferansId`)
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifBirimFiyatListesiServisi.cs`
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifHesapServisi.cs`
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifPozImportServisi.cs`
  (Faz 2: `NormalizeHeader` + `ParseDecimal` `internal` yapıldı — metraj ayrıştırıcı paylaşır)
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifJsonServisi.cs`
  (`KaydetAsync`: ObservableCollection grafiğini UI thread'inde JSON'a dönüştürür, yalnız dosya yazımını asenkron yapar;
  `GuvenliDosyaYazmaServisi` ile atomik hedef değiştirme + `.bak` yedeği)
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifExcelExportServisi.cs`
  (Genel İcmal + Tablo Özeti + tablo başına ayrıntı sayfası; Excel formüllü tutarlar)
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifSatirBirlestirmeServisi.cs`
  (tablo içinde PozNo+Birim tekilleştirme, miktar toplama, ağırlıklı fiyat, referans katkısı koruma)
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifMetrajImportServisi.cs` (Faz 2 — M13 özeti ayrıştırıcı + `SablonOlustur`)
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifOtomatikPozEslemeServisi.cs`
  (otomatik eşleştirme: alan/birim/model/kesit/anma değeri filtreleri + puanlama)
- `src/Kst.Acoustics.Core/Electric/KesifHazirla/KesifPozEslemeServisi.cs` (Faz 2 — global öğrenme DB,
   `%AppData%/Eye4Pro/Electric/KesifHazirla/PozEslemeVeritabani.json`; test için özel yol ctor'u)

App:
- `src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaViewModel.cs`
  (Faz 2: `_eslemeServisi` + ikinci ctor (test/özel DB yolu); `ReferansDosyalari`, `PozEslemeleri`,
   `EslenmemisKalemler`, `AtanacakPoz`; `ReferansEkle/ReferansSil`, `EslemeAtaVeUygula/EslemeGuncelle/EslemeSil`,
   `OtomatikSatirlariUret/Temizle`, `TumReferanslariYenidenIsle`, `EslemeleriYenile`, `EslenmemisKalemleriYenile`)
- `src/Kst.Acoustics.App/Electric/KesifHazirla/KesifHazirlaWindow.axaml(.cs)`
  (Faz 2: toolbar "Poz Atama" + "Referanslar" butonları + handler'lar; tüm DataGrid'lere `CanUserResizeColumns`)
- `src/Kst.Acoustics.App/Electric/KesifHazirla/PozAtamaWindow.axaml(.cs)` (Faz 2 — YENİ)
  (atanacak pozlar ve metraj/eşleşme tabloları için iki bağımsız canlı arama; ana ekran poz filtresinden bağımsızdır)
- `src/Kst.Acoustics.App/Electric/KesifHazirla/ReferanslarWindow.axaml(.cs)` (Faz 2 — YENİ)
- `src/Kst.Acoustics.App/Electric/KesifHazirla/KesifIcmalWindow.axaml(.cs)`
- `src/Kst.Acoustics.App/Electric/KesifHazirla/OzelPozlarWindow.axaml(.cs)`
- `src/Kst.Acoustics.App/Electric/KesifHazirla/RenameTableWindow.axaml`

Test:
- `src/Kst.Acoustics.Tests/KesifHazirlaTests.cs`

---

## 11. Korunması Gereken Regresyon Alanları

- Ana icmal bütün pozları gösterir.
- Tek tabloda kullanılan pozların kırılımı gösterilir.
- Aynı poz farklı birim fiyat uyarısı verir.
- Türkçe başlıklar import edilir.
- Birim fiyat listeleri aktif/pasif yönetilir.
- Özel poz tarih güncellemesi çalışır.
- Poz kütüphanesi arama/filtreleme çalışır.
- Tablo isim değişikliği icmale yansır.
- JSON round-trip manuel ve liste pozlarını tekil korur.
- Excel başlık-kolon kayması olmaz.
- Para/sayı parse kültürden bağımsız çalışır.

---

## 12. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M14 regresyonları dahil mevcut test paketi geçmeli
- 2026-06-22 doğrulama: App build 0 hata; mevcut test paketi 365/365 geçti.
- 2026-06-30 doğrulama (Faz 2): Core/App/Tests build 0 hata; test paketi **487/487** geçti
  (6 yeni Faz 2 testi: eşleşme anahtarı normalize, öğrenme DB round-trip, metraj şablon + 7. satır başlık,
   otomatik satır üretimi/referans silme eşleşmeyi korur, referans+KaynakReferansId JSON round-trip).
- 2026-06-30 düzeltme: Kaydet işlemindeki Avalonia thread sahipliği hatası giderildi; Poz Atama penceresine
  poz no/ad canlı araması eklendi. Asenkron kayıt ve Poz Atama filtresi için 2 regresyon testi eklendi;
  App build 0 hata, test paketi **489/489** geçti.
- 2026-06-30 otomatik poz atama: "Tümünü Otomatik Ata" eklendi. `Downloads/kesif.e4p14` içindeki gerçek
  14.838 poz / 113 metraj kalemiyle ayarlandı; 92 güvenli aday bulundu, teknik karşılığı bulunmayan 21 kalem
  manuel düzeltmeye bırakıldı. 3 regresyon testi eklendi; test paketi **492/492** geçti.
- 2026-06-30 Excel çıktısı: ana pencerenin sağ altına `Excel Çıktısı` eklendi. Çalışma kitabı sırası
  `Genel İcmal` → `Tablo Özeti` → her keşif tablosu; ayrıntı tutarları ve özet toplamları Excel formülleriyle
  bağlıdır. Sayfa sırası/içerik/formül regresyonu ve görsel QA tamamlandı; test paketi **493/493** geçti.
- 2026-06-30 aynı poz tekilleştirme: aynı tabloda `PozNo+Birim` tekrarına izin verilmez; ikinci manuel ekleme
  miktarı artırır, mevcut projedeki tekrarlar açılışta toplanır. Çoklu otomatik referans katkıları JSON'da
  ayrı korunur ve referans silme yalnız ilgili miktarı düşürür. 3 regresyon eklendi; **496/496** test geçti.
- 2026-06-30 Kaydet/Farklı Kaydet ayrımı: `Kaydet` açık dosya yolunu hatırlayıp doğrudan üzerine yazar;
  `Farklı Kaydet` ayrı buton olarak her zaman dosya seçiciyi açar. App build 0 hata.
- 2026-06-30 Excel toplam farkı incelemesi: diskteki `.e4p14` ve `.xlsx` tablo bazında karşılaştırıldı;
  ikisi de **77.599.975,74 ₺**. Açık kalan Excel penceresi önceki 105.479.706,10 ₺ sürümünü (silinmiş
  `Özel İmalatlar`: 27.879.730,36 ₺) bellekte gösteriyordu. Açık Excel dosyasının üzerine dışa aktarma
  artık `~$` kilit dosyasından engellenip kapatma/farklı ad uyarısı verir; **497/497** test geçti.
- 2026-06-30 BF listesi UX: boş liste oluşturan eski `BF Listesi Ekle` komutu kaldırıldı; Excel içe aktarma
  butonu `BF Listesi Ekle` olarak yeniden adlandırıldı ve doğrudan dosya seçiciyi açıyor. App build 0 hata.
- 2026-06-30 DataGrid sıralama: M14'teki bütün tablo sütunlarına görünür `↕` işareti ve açık
  `SortMemberPath` eklendi; fiyat/miktar/tutar decimal olarak, tarih DateTime olarak sıralanır. App build 0 hata.
- 2026-06-30 Ana İcmal seçim düzeltmesi: sıralanmış DataGrid'de satır seçimi açık `TwoWay` bağ ve
  `SelectionChanged` ile ViewModel'e aktarılır; Tablo Kırılımı artık tıklanan pozu izler. **498/498** test geçti.
- 2026-06-30 keşif tablosu listesi yeniden boyutlandırma: sabit 78 px yükseklik kaldırıldı; tablo adları
  listesi ile satır grid'i arasına yatay GridSplitter eklendi. App build 0 hata.
- 2026-06-30 hiyerarşik tablolar + Proje Bilgileri (Faz 2.1): keşif tablolarına alt tablo (en çok 3 seviye,
  `KesifTablosu.AltTablolar`); üst tablo yalnız başlık (satır yalnız yaprakta); özyinelemeli toplam/icmal/
  birleştirme (`KesifTabloAgaci`). UI: tablo listesi TreeView'a çevrildi, "Alt Tablo Ekle" butonu, sağ tarafa
  Proje Bilgileri künyesi (`KesifProje.Bilgiler`). Excel raporu hiyerarşik (yaprak detay sayfası + girintili
  Tablo Özeti + Genel İcmal künye alt başlığı). 3 regresyon eklendi; test paketi **501/501** geçti.
- 2026-06-30 sütun/silme/biçim UX: (1) DataGrid sütun genişlikleri kullanıcı bazında hatırlanır
  (`KesifKolonGenislikDeposu`, AppData `KolonGenislikleri.json`; pencere açılışında uygulanır, kapanışta
  kaydedilir — satırlar + poz kütüphanesi grid'leri). (2) Birim Fiyat artık ₺ ile ve sağa yaslı gösterilir;
  düzenleme ham decimal TextBox'ta (gösterim/düzenleme ayrı şablon — HATA-2 parse sorunu önlenir). Tutar ve
  poz Fiyat sağa yaslı. (3) "Satır Sil"/"Tablo Sil" butonları kaldırıldı; silme **Del** tuşuyla yapılır
  (tablo ağacı + satır grid) ve her silmeden önce **Tamam/İptal onayı** sorulur (`OnayWindow`). Çift tık
  silme de onaya bağlandı. Test paketi 501/501 geçti, App build 0 hata.
- 2026-06-30 silme Backspace: Del'e ek olarak Backspace de seçili tablo/satırı (onaylı) siler.
- 2026-06-30 referans kaynağı görselleştirme: keşif satır grid'inde referanstan gelen satırlar farklı renkte
  (`KaynakTipiRenkConverter`: Otomatik=amber, Karma=teal, Manuel=varsayılan; yazı + hafif arka plan tonu,
  `DataGridRow` stilinde). Fare balonunda (tooltip) referans dosya adı + miktar gösterilir
  (`KesifSatir.ReferansBilgisi` geçici alanı, VM `ReferansBilgileriniYenile` ile `HesaplariYenile` içinde dolar;
  `OtomatikKatkilar` → `ReferansDosyalari` adıyla eşlenir, karma satırda manuel pay da listelenir). Serialize edilmez.
- 2026-06-30 Poz Atama kontrol tiki: "Tanımlı Eşleşmeler" gridine "Kontrol" onay kutusu eklendi
  (`KesifPozEsleme.KontrolEdildi`, global DB'de kalıcı). Kontrol edilen atama yeşil (`KontrolEdildiRenkConverter`),
  edilmeyen varsayılan renkte — gözden kaçan atamalar ayırt edilir. Tik değişince DB'ye yazılır
  (`EslemeKontrolDurumuKaydet`, kontrolsüz sayısı durum çubuğunda). 1 round-trip testi; 502/502 test geçti.
- 2026-06-30 elle değişen birim fiyat rengi: satır oluşurken `KesifSatir.OrijinalBirimFiyat` (poz/otomatik
  kaynak fiyatı) saklanır; güncel `BirimFiyat` bundan farklıysa `BirimFiyatElleDegisti` true olur ve keşif
  grid'inde birim fiyat turuncu (`ElleDegisenRenkConverter`) gösterilir. Eski projelerde yüklemede orijinal
  fiyat güncel değere baz alınır (değişmemiş sayılır). 1 tespit+round-trip testi; 503/503 test geçti.
- 2026-06-30 çıkış onayı + çift monitör: pencere kapatılırken (X) veya "Geri/Modüller" butonunda 3 seçenekli
  onay (`CikisOnayWindow`: Kaydet ve Çık / Kaydetmeden Çık / İptal). `Window_Closing` içinde `e.Cancel=true` +
  asenkron diyalog; `_cikisOnaylandi` bayrağıyla ikinci kapanışta gerçek kapanır. "Kaydet ve Çık" mevcut yola
  yazar (yoksa Farklı Kaydet); kayıt iptal edilirse çıkılmaz. Launcher (`ElectricLauncherWindow`) artık
  `Screens.ScreenFromWindow` + `Scaling` ile **mevcut pencerenin bulunduğu ekranda** açılır (eskiden birincil
  monitöre ortalanıyordu). App build 0 hata.
- 2026-08-07 güvenilirlik/UX düzeltmesi: kayıtta atomik değiştirme + `.bak`, gerçek değişiklik parmak izi,
  Yeni/Aç/Geri/X için koşullu kayıt onayı, kaydetme kısayolları, tek `.e4p14` uzantısı, iki taraflı Poz Atama
  araması ve taşmayan iki satırlı üst komut/proje bilgisi düzeni eklendi. Atomik yedek ve arama regresyonları
  genişletildi; çözüm build **0 hata, 0 uyarı**, özel test paketi **668/668** geçti.

---

## 13. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Veri veya hesap değişikliği için `Core/Electric/KesifHazirla` dosyalarını incele.
4. UI değişikliği için `KesifHazirlaWindow`, `KesifHazirlaViewModel`, `KesifIcmalWindow` ve `OzelPozlarWindow` alanlarını kontrol et.
5. Import değişikliği yapılıyorsa Excel/CSV parse regresyonlarını koru.
6. JSON model değişikliği yapılıyorsa round-trip testini çalıştır.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.
