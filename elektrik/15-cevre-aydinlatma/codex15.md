# M15 Codex — Çevre Aydınlatma Projesi Hazırla

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kapsam, kararlar, iş akışı, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M15 — Çevre Aydınlatma Projesi Hazırla |
| Amaç | Park/çevre düzenleme DXF altlığı üzerinden dış aydınlatma projesi hazırlamak |
| Proje uzantısı | `.e4p15` |
| Tek hat çıktısı | `.e4p11` |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Core boru hattı tamamlandı; ilk App/UI dilimleri var; çizim/zoom/pan ve detay panelleri eksik |

---

## 2. Modül Amacı ve Akış

Temel kullanıcı akışı:

```text
Park DXF yükle
  -> yüzeyleri belirle: yeşil alan / yol / spor alanı / bina
  -> armatür + direk bilgilerini gir veya LDT/IES fotometri dosyasından oku
  -> yüzeyden/güzergâhtan direk yerleşimi üret
  -> aydınlatma hesabı yap
  -> panoya kablo çek ve gerilim düşümüne göre kesit kontrol et
  -> TT topraklama şeridi + kazık hesabı yap
  -> Tek Hat'a Aktar (.e4p11)
  -> proje kaydet/aç (.e4p15)
```

Mevcut çizimsiz UI akışı:

```text
DXF Yükle
  -> yüzey kategorisini elle düzelt
  -> Seçili Yüzeyden Güzergâh
  -> Otomatik Direk Yerleştir
  -> harita önizleme
  -> Tek Hat'a Aktar (.e4p11) veya Kaydet (.e4p15)
```

---

## 3. Kapsam

Aktif kapsam:
- DXF kapalı poligonlarından yüzey üretimi
- DXF arka plan çizgilerinin çıkarılması ve sadeleştirilmesi
- Katman adından yüzey kategorisi tahmini
- Yüzey kategorisinin UI'da elle düzeltilmesi
- LDT ve IES fotometri okuma
- Fotometriden armatür tanımı üretme
- Manuel armatür/direk tanımı
- Yüzey çevresinden veya açık güzergâhtan direk yerleşimi
- Lümen metodu ile ortalama aydınlık ve armatür/adım önerisi
- Devre kablo hesabı ve gerilim düşümü için M11 `KabloHesapServisi` kullanımı
- TT topraklama şerit/kazık direnç hesabı
- M11 tek hat aktarımı (`.e4p11`)
- M15 proje JSON kaydet/aç (`.e4p15`)
- Harita önizlemede arka plan, yüzey ve direk render

Terrain entegrasyonu (köprü ✅ — 2026-06-14):
- "Arazi Yükle (.e4arazi)" butonu → `CevreAydinlatmaViewModel.AraziYukleVeKotAta`: M60 `.e4arazi.json`
  `AraziTuketimServisi` ile salt-okunur yüklenir, TIN kurulur, mevcut direklere `ZeminKotuM` (dip kotu) atanır.
  Durum çubuğunda `AraziBilgisi` (rev, kot aralığı, üçgen). Arazi geometrisi değiştirilmez.
- `AydinlatmaDiregi.ZeminKotuM` (nullable) eklendi. Bkz. [codex60](../../terrain/60-hafriyat-hesapla/codex60.md).

Eksik veya sonraki iş:
- Terrain: yerleştirme sonrası otomatik kot atama, 3D yerleşim, gerilim düşümünde gerçek (eğimli) hat uzunluğu.
- Serbest güzergâh çizimi
- Manuel poligon çizimi
- Zoom/pan
- LDT/IES dosya seçimi için tam kütüphane UI'ı
- Aydınlatma, kablo ve topraklama detay panelleri
- DXF çıktı ve rapor
- Nokta-nokta izolüks hesabı
- Gerçek üretici LDT/IES dosyalarıyla saha doğrulaması

Kapsam dışı:
- Hakediş
- Pursantaj
- Online fiyat/veri güncellemesi
- M11 içinde topraklama çizimini tamamen çözmek

---

## 4. Ana Kararlar

- Yüzey işaretleme iki kademelidir: katmandan otomatik tahmin + kullanıcı düzeltmesi.
- Park DXF birimi belirsizse `1 birim = 1 m` kabul edilir.
- Büyük DXF için segment sadeleştirme zorunludur; çok kısa parçalar elenir.
- Aydınlatma hesabı önce Kademe A lümen metodu ile yapılır.
- Kademe B nokta-nokta hesap ve izolüks sonraya bırakılmıştır.
- LDT ve IES düz ASCII olarak proje içinde, dış bağımlılık eklemeden okunur.
- Fotometrik dosya yoksa manuel armatür girişi fallback kalır.
- Kablo hesabı M11 `KabloHesapServisi` ile yapılır.
- Dış aydınlatmada varsayılan gerilim düşümü hedefi `%5` kabul edilmiştir.
- Topraklama sistemi TT'dir.
- TT uygunluk kontrolü `R_A * I_Delta_n <= 50 V` kuralıyla yapılır.
- Tek hat şeması M15 içinde çizilmez; M11'e `.e4p11` aktarımı yapılır.
- M11 aktarımı her seferinde yeni proje üretir; M11 tarafındaki elle yapılan ekler yeniden aktarımda korunmaz.
- M15 için M12'nin karmaşık 3D preview'i kopyalanmadı; M15'e özel basit 2D preview kullanılır.

---

## 5. Veri Modeli Özeti

Proje kökü:
- `CevreAydinlatmaProje`
- Yüzeyler
- Armatür kütüphanesi
- Direk kütüphanesi
- Güzergâhlar
- Yerleşmiş direkler
- Arka plan çizgileri
- Topraklama

Yüzey:
- `CevreYuzey`
- `YuzeyKategori`: `Diger`, `YesilAlan`, `Yol`, `SporAlani`, `Bina`
- Poligon, kaynak katman, alan

Armatür:
- `CevreArmaturTanim`
- Güç W, akı lm, marka/model, IP, optik, renk sıcaklığı
- `FotometriKaynak`: `Ldt`, `Ies`, `Manuel`
- Fotometri dosya yolu opsiyoneldir

Direk:
- `CevreDirekTanim`
- Boy, malzeme, konsol bilgileri, varsayılan armatür

Güzergâh:
- `AydinlatmaGuzergahi`
- Açık rota veya kapalı poligon çevresi
- Direk aralığı, kenar ofseti, varsayılan direk/armatür

Yerleşmiş direk:
- `AydinlatmaDiregi`
- Konum, güzergâh, direk tanımı, armatür tanımı

Topraklama:
- `TopraklamaTasarimi`
- TT/TN sistem tipi, şerit güzergâhı, kazıklar, direnç bilgileri

Kalıcılık:
- `.e4p15` JSON olarak kaydedilir.
- `Point2D` round-trip testle doğrulanmıştır.
- `.e4p11` aktarımı M11 JSON ayarlarıyla yapılır.

---

## 6. Hesap Kuralları

Lümen metodu:

```text
E_ort = (N * Phi * MF * UF) / A
```

Gerekli armatür sayısı:

```text
N = ceil(HedefLux * A / (Phi * MF * UF))
```

Önerilen direk aralığı:

```text
Aralik = Phi * MF * UF / (HedefLux * SeritGenisligi)
```

Fotometri:
- LDT/EULUMDAT ve IES/LM-63 desteklenir.
- Güç W elektrik yükü için kullanılır.
- Akı lm lümen hesabı için kullanılır.
- Işık şiddeti dağılımı nokta-nokta hesap için saklanabilir.
- `cd/1000 lm` değerleri gerçek kandela hesabında akı ile ölçeklenir.

Kablo:
- Her güzergâh/devre panodan çıkan radyal linye gibi ele alınır.
- Direk yükleri downstream kümülatif akımla hesaplanır.
- Eşdeğer uzunluk:

```text
L_eq = Sum(I_segment * L_segment) / I_toplam
```

- Kesit ve gerilim düşümü M11 `KabloHesapServisi` ile hesaplanır.
- Monofaze 230 V, trifaze 400 V kabul edilir.

Topraklama:
- Kazık, şerit ve paralel toplam direnç tahmini yapılır.
- Paralel kazık hesabı iyimserdir; etkileşim/kullanım faktörü ihmal edilmiştir.
- TT uygunluk:

```text
R_A * I_Delta_n <= 50 V
```

---

## 7. M11 ve M12 ile İlişki

M12'den gelen ortaklar:
- `Electric/Ortak/YerlesimGeometri.cs`
- `Electric/Ortak/DxfSegmentYardimci.cs`
- `Electric/Ortak/BosluktanOdaTespitServisi.cs`

M11'den kullanılanlar:
- `KabloHesapServisi`
- `ElektrikProje`
- `PanoTipi.AydinlatmaPanosu`
- `.e4p11` JSON aktarım deseni

M15 -> M11 aktarım kuralları:
- Tek kök pano: `AydinlatmaPanosu`
- Her güzergâh bir `Linye`
- `SortiSayisi` direk sayısıdır
- Yük armatür güçlerinden hesaplanır
- Kablo boyu gerçek güzergâh uzunluğudur
- Sigorta, kesit, faz dağılımı ve kompanzasyon M11 tarafında yeniden hesaplanır
- Direksiz güzergâh atlanır ve uyarı üretir
- Armatür bulunamazsa yük 0 W olur ve uyarı üretir

---

## 8. UI Durumu

Var:
- Electric launcher içinde Çevre Aydınlatma kartı
- `CevreAydinlatmaWindow`
- `CevreAydinlatmaViewModel`
- DXF açma
- `.e4p15` aç/kaydet
- `.e4p11` tek hat aktarımı kaydetme
- Yüzey listesi
- Kategori ComboBox ile elle düzeltme
- Seçili yüzeyden güzergâh üretme
- Otomatik direk yerleştirme
- 2D önizleme: arka plan çizgileri, yüzeyler, direkler

Eksik:
- Pointer ile serbest güzergâh çizimi
- Pointer ile manuel poligon çizimi
- Zoom/pan
- Armatür/direk kütüphane ekranı
- LDT/IES dosya seçme UI'ı
- Hesap/kablo/topraklama detay panelleri

Görsel doğrulama notu:
- Headless ortamda buton, diyalog ve render görsel olarak tam denenmemiştir.
- VM/Core akışları testlerle doğrulanmıştır.

---

## 9. Kritik Dikkat Notları

- Yüzey kategorisi otomatik tahmindir; kullanıcı düzeltmesi her zaman mümkün kalmalıdır.
- Türkçe katman adları aksan ve büyük/küçük harften bağımsız normalize edilmelidir.
- Açık polyline yüzey değildir; kapalı poligon yüzeye çevrilir.
- DXF arka plan çizgileri yüzeylerin altında soluk çizilmelidir.
- Unitless DXF için metre varsayımı korunmalıdır.
- M15 preview basit 2D kontroldür; M12 3D preview'e bağlı değildir.
- LDT/IES gerçek üretici dosyalarıyla farklı `Isym`, `TILT` ve açı tabloları kontrol edilmelidir.
- Direk yerleşiminde köşe kümelenmesi olmamalıdır; arc-length tabanlı yürüyüş korunmalıdır.
- Kapalı poligon çevresinde dikiş noktası tekrarı ayıklanmalıdır.
- M11 aktarımında enum/JSON ayarları M11 ile uyumlu kalmalıdır.
- Topraklama sonucu mühendislik tahminidir; zemin özgül direnci ve uygulama şartları kullanıcı tarafından doğrulanmalıdır.

---

## 10. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/CevreAydinlatmaModels.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/CevreYuzeyImportServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/YuzeyKategoriEsleyici.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/ArmaturTanimFabrikasi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/AydinlatmaHesapServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/DirekYerlesimServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/KabloGuzergahServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/TopraklamaServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/CevreAydinlatmaTekHatAktarimServisi.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/CevreAydinlatmaJsonServisi.cs`

Fotometri:
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/Fotometri/FotometrikVeri.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/Fotometri/LdtParser.cs`
- `src/Kst.Acoustics.Core/Electric/CevreAydinlatma/Fotometri/IesParser.cs`

Ortak:
- `src/Kst.Acoustics.Core/Electric/Ortak/YerlesimGeometri.cs`
- `src/Kst.Acoustics.Core/Electric/Ortak/DxfSegmentYardimci.cs`
- `src/Kst.Acoustics.Core/Electric/Ortak/BosluktanOdaTespitServisi.cs`

App:
- `src/Kst.Acoustics.App/Electric/CevreAydinlatma/CevreAydinlatmaWindow.axaml`
- `src/Kst.Acoustics.App/Electric/CevreAydinlatma/CevreAydinlatmaWindow.axaml.cs`
- `src/Kst.Acoustics.App/Electric/CevreAydinlatma/CevreAydinlatmaViewModel.cs`
- `src/Kst.Acoustics.App/Electric/CevreAydinlatma/CevreAydinlatmaPreview.cs`
- `src/Kst.Acoustics.App/Views/ElectricLauncherWindow.axaml`
- `src/Kst.Acoustics.App/Views/ElectricLauncherWindow.axaml.cs`

Test:
- `src/Kst.Acoustics.Tests/CevreAydinlatmaTests.cs`
- `src/Kst.Acoustics.Tests/Program.cs`

---

## 11. Korunması Gereken Regresyon Alanları

- LDT parser armatür adı, güç, akı ve tablo değerlerini okur.
- IES parser güç, akı ve kandela normalizasyonunu doğru yapar.
- Boş fotometri hata verir.
- Ortak yerleşim geometrisi kenar ve ızgara adaylarını üretir.
- DXF segment sadeleştirme kısa parçaları eler.
- Katman adı yüzey kategorisine doğru eşleşir.
- Kapalı poligonlar yüzeye çevrilir; açık polyline atlanır.
- Armatür fotometriden üretilir.
- Açık güzergâha eşit aralıkla direk yerleşir.
- Kapalı poligon çevresine direk yerleşir.
- Lümen metodu sınır değerlerde hata verir ve normal değerleri doğru hesaplar.
- Devre kablo hesabı M11 ile tutarlı kalır.
- TT topraklama hesabı RCD eşiğini uygular.
- Tek hat aktarımı pano ve linyeleri üretir.
- `.e4p11` round-trip yük ve linye bilgilerini korur.
- ViewModel çizimsiz akışı çalışır.
- `.e4p15` round-trip yüzey, armatür, güzergâh, direk ve topraklamayı korur.
- Arka plan çizgileri çıkarılır ve sadeleştirilir.
- Seçili yüzeyden güzergâh ve direk üretimi çalışır.

---

## 12. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.Core/Kst.Acoustics.Core.csproj
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj --no-incremental
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen hedef:
- Core build: 0 hata
- App build: 0 hata
- Test paketi: M15 regresyonları dahil geçmeli

---

## 13. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Core değişikliği için `Electric/CevreAydinlatma` ve gerekirse `Electric/Ortak` alanlarını incele.
4. UI değişikliği için `CevreAydinlatmaWindow`, `CevreAydinlatmaViewModel` ve `CevreAydinlatmaPreview` dosyalarını incele.
5. M11 aktarımı değişiyorsa M11 model/JSON beklentilerini kontrol et.
6. DXF veya yüzey değişikliği yapılıyorsa unitless metre varsayımını ve kapalı poligon davranışını koru.
7. Fotometri değişikliği yapılıyorsa LDT/IES testlerini genişlet.
8. Son olarak M15 testlerini ve ilgili App build'i çalıştır.

## 5 Kasım 2026 — sıralı modül denetimi

Geçersiz/aşırı yoğun direk aralığı reddediliyor; negatif/sonsuz direnç uygunluk veremiyor (A12–A13).

Ayrıntılı hata listesi ve doğrulama: depo kökündeki `modul-denetimi-2026-11-05.md`.
