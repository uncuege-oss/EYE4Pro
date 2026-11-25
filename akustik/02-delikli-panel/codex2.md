# M02 Codex — Delikli Panel Tasarımı

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M02 — Delikli Panel Tasarımı |
| Amaç | Çok katmanlı panel emilim hesabı ve materyal kütüphanesine aktarım |
| Yöntem | Transfer Matrix Method (TMM) |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Sınırsız/çok katmanlı akustik panel hesabı
- Delikli, mikro-delikli, yivli, hava boşluğu, gözenekli malzeme ve kumaş katmanları
- Normal geliş ve rastgele geliş emilim hesapları
- NRC, `αw`, soğurma sınıfı ve Helmholtz rezonans özeti
- Parametrik delik düzeni ve canlı grafik
- Özel DXF panel deseni içe aktarma
- Panel kesiti ve performans raporu dışa aktarımı
- Tasarlanan panelin `MaterialDatabase` sistemine kaydedilmesi

Kapsam dışı / model sınırı:
- Özel DXF desenleri tam dalga/FEA olarak çözülmez.
- Özel açıklıklar porozite, hidrolik çap dağılımı ve paralel akustik admitans yaklaşımına indirgenir.
- Çok dar boğazlı slot, voronoi veya aşırı heterojen açıklık dağılımlarında sonuç mühendislik yaklaşımıdır; kalite uyarıları dikkate alınmalıdır.

---

## 3. Akustik Hesap Kuralları

- Ana hesap motoru `System.Numerics.Complex` tabanlı TMM'dir.
- Gözenekli katman modelleri:
  - Delany-Bazley-Macdonald
  - Miki
- Delikli panel empedansı Maa tipi viskoz tüp direnci, kenar direnci ve Fok açık alan düzeltmesiyle hesaplanır.
- Delik düzenleri:
  - Düz
  - Şaşırtmalı
- Delik düzenine göre Fok düzeltmesi ayrı katsayılarla uygulanır.
- NRC yalnızca 250, 500, 1000 ve 2000 Hz değerlerinin ortalamasından hesaplanır ve 0.05'e yuvarlanır.
- `αw` ISO 11654 referans eğrisiyle hesaplanır; NRC yerine geçmez.
- Oda akustiği materyal kaydı için oktav bant değerleri rastgele geliş tahmininden türetilir.
- Grafik normal/rastgele geliş seçimine göre gösterilir.

---

## 4. Katman ve Parametre Kuralları

- Katmanlar sırayla panel yüzünden arka sınıra doğru değerlendirilir.
- Her katmanda `Hesaba kat` anahtarı vardır; kapalı katman hesap, grafik, rapor ve materyal kaydından çıkarılır.
- SoundTex tipi kumaş ince dirençli levha olarak modellenir.
- Parametre sınırları güvenli tutulmalıdır:
  - Boşluk oranı `0 < p <= 0.99`
  - Hava boşluğu kalınlığı en az 1 mm
  - Negatif kalınlık, çap veya akış direnci kabul edilmez
- Kullanıcı manuel delik çapı/boşluk/aks aralığı değiştirirse özel DXF açıklık dağılımı temizlenir.
- UI parametre değişiklikleri debounce ile hesaplanmalıdır; sürükleme sırasında UI donmamalıdır.

---

## 5. DXF Panel Import Kuralları

- Özel desen importu DXF üzerinden yapılır.
- Parser kapalı geometriyi analiz eder; açık rehber çizgiler alan hesabına dahil edilmez.
- Desteklenen/çizilebilen varlıklar:
  - `CIRCLE`
  - `LWPOLYLINE`
  - `POLYLINE`
  - `3DFACE`
  - `ARC`
  - `ELLIPSE`
  - `SPLINE`
- En büyük kapalı şekil panel sınırı kabul edilir.
- Daha küçük kapalı şekiller açıklık/delik olarak değerlendirilir.
- İç içe poligonlar panel/delik/ada hiyerarşisiyle hesaplanır.
- LWPOLYLINE bulge/yay bilgisi tessellation ile alan hesabına katılır.
- `$INSUNITS` algılanamazsa kullanıcıdan birim onayı alınır.
- Özel açıklıklar için her açıklığın alanı, çevresi, hidrolik çapı, daireselliği ve aspect ratio değeri üretilir.
- DXF kalite analizi uyarıları rapora ve UI'a yansıtılır.

---

## 6. Rapor ve Entegrasyon

- Panel sonuçları `MaterialDatabase` sistemine kaydedilebilir.
- Materyal kaydında oktav bant soğurma değerleri ve tahmini saçılma katsayısı üretilir.
- Rapor formatları:
  - `.txt`
  - `.md`
  - `.html`
- Raporlarda hesap güveni, varsayımlar, katman listesi, geometri özeti ve emilim tablosu bulunmalıdır.
- DXF şablonu AutoCAD R12 ASCII DXF formatında üretilir; kullanıcı `.dwg` adı verirse çıktı `.dxf` uzantısına çevrilir.

---

## 7. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/PanelDesign/` — TMM, katman modelleri, panel DXF analizi
- `src/Kst.Acoustics.Core/Projects/MaterialDatabase.cs` — materyal kütüphanesi entegrasyonu

App:
- `src/Kst.Acoustics.App/PanelDesignWindow*` — panel tasarım penceresi
- `src/Kst.Acoustics.App/ViewModels/PanelDesignViewModel*` — panel tasarım ViewModel dosyaları
- `src/Kst.Acoustics.App/Controls/AbsorptionPlotControl.cs` — emilim grafiği
- `src/Kst.Acoustics.App/Controls/PanelPreviewControl.cs` — panel/desen önizleme

Test:
- `src/Kst.Acoustics.Tests/` — panel hesap, DXF parser ve regresyon testleri

---

## 8. Güncel Özellik Notları

- Parametrik panel tasarımında delik çapı, aks aralığı, boşluk oranı ve delik düzeni karşılıklı güncellenir.
- Panel önizlemesi düz ve şaşırtmalı delik dizilimini gösterebilir.
- Özel DXF importu hidrolik çap dağılımıyla hesaplanır; tek eşdeğer çap yaklaşımı yalnızca uygun durumlarda referans davranış olarak kalır.
- Kalite paketi yüksek/düşük porozite, küçük hidrolik çap, geniş açıklık dağılımı, yüksek slot aspect ratio, düşük dairesellik, belirsiz birim ve açık rehber entity durumlarını işaretler.
- Helmholtz benchmark ve DXF parser davranışları regresyon testleriyle korunmalıdır.

---

## 9. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- Genel regresyon: `dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj`
- Son doğrulama notu: 2026-06-22 oturumunda `394 tests passed`

---

## 10. 2026-06-22 Hesap Motoru Denetimi

Bu oturumda M02 hesap motoru ve DXF akışı detaylı incelendi. Aşağıdaki bulgular kapatıldı ve regresyon testleri eklendi.

Kapatılan ana bulgular:
- `3DFACE` DXF panel sınırı okunmuyordu; `10/20`, `11/21`, `12/22`, `13/23` noktaları desteklendi.
- Bilinmeyen veya farklı `$INSUNITS` kodları sessizce milimetre gibi davranabiliyordu; bilinen birimler genişletildi, bilinmeyen kod kullanıcı onayına bırakıldı.
- Panel alanı tüm kapalı şekillerden türetilebiliyordu; artık en büyük dış kapalı şekil panel sınırı, yalnız onun içindeki kapalı şekiller açıklık kabul edilir.
- Boşluk oranı sınırları Core/UI/parser arasında tutarsızdı; ortak sınır `0.0001..0.99` olarak `PerforatedLayer` üzerinden toplandı.
- Helmholtz özeti eski `t + 0.85d` formülünü kullanıyordu; artık TMM'deki Fok düzeltmesi, şekil katsayısı ve özel açıklık hidrolik çap modeliyle aynı boyun varsayımından tahmin edilir.
- `PorousLayer.LastFrequencyOutOfRange` mutable state üretiyordu; geçerlilik aralığı saf sorgu fonksiyonuna taşındı.
- Bozuk DXF sayıları parser ve önizlemede exception riski yaratıyordu; parser kalite uyarısı üretir, preview bozuk/finite olmayan sayıları güvenli atlar veya `0` kabul eder.

Kapatılan tavsiye/refaktör maddeleri:
- `αw` hesabı üçüncü oktav verisiyle deterministik test edilebilir hale getirildi.
- Rastgele geliş entegrasyonu yüzey empedansından doğrudan çağrılabilir hale getirildi ve `Zs/Z0 = 1` kapalı formuna karşı testlendi.
- DXF kalite metrikleri import onay modalında, panel ekranında ve `.txt/.md/.html` raporlarda karar maddeleri olarak görünür hale getirildi.
- DXF importundan gelen `CustomOpenings` ve önizleme geometrisi, kullanıcı parametrik delik alanlarını değiştirdiğinde temizlenir.
- Tüm katmanlar hesap dışıysa rapor frekans tablosu üretmez; açıkça "Hesapta katman yok" mesajı verir.

Bu denetim sonunda eklenen/korunan önemli regresyonlar:
- 3DFACE boundary okuma
- bilinmeyen ve farklı `INSUNITS` davranışı
- en büyük panel sınırı hiyerarşisi
- yüksek boşluk oranı korunumu
- Helmholtz/TMM boyun modeli uyumu
- gözenekli katman geçerlilik sorgusunun stateless olması
- malformed DXF numeric parser/preview dayanıklılığı
- `αw`, rastgele geliş ve DXF kalite raporu davranışları
- DXF import state temizliği
- boş hesaplı rapor frekans tablosu güvenliği

---

## 11. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Panel hesabı için önce `Core/PanelDesign` dosyalarını incele.
4. UI değişikliği gerekiyorsa `PanelDesignWindow` ve ilgili ViewModel dosyalarını kontrol et.
5. Formülasyon değişikliği yapıldıysa regresyon testi ekle veya mevcut panel testlerini çalıştır.
6. Görünür UI metinlerinde Türkçe karakter kontrolü yap.

## 5 Kasım 2026 — sıralı modül denetimi

NaN/Infinity/negatif emilim verisinden sahte alpha-w sınıfı üretilmesi engellendi (A02).

Ayrıntılı hata listesi ve doğrulama: depo kökündeki `modul-denetimi-2026-11-05.md`.
