# M04 Codex — Ses Bariyeri ve Çevresel Gürültü

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları, kritik sınırlamalar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M04 — Ses Bariyeri ve Çevresel Gürültü |
| Amaç | Bina cephesinde bariyer öncesi/sonrası dış gürültü seviyelerini hesaplamak ve görselleştirmek |
| Referans yaklaşım | ISO 9613-2 eğilimleri, Maekawa, Kurze-Anderson |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Tamamlandı; gelişmiş modelleme ve bakım sürüyor |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Dikdörtgen prizma bina modeli
- Bina ön cephesinde otomatik alıcı grid
- Noktasal, çok noktalı, çizgisel ve trafik kaynakları
- Tek veya çoklu düz bariyer senaryosu
- Bariyer yüksekliği, uzunluğu, kalınlığı, mesafesi, X kaydırması ve etkinlik durumu
- Bariyer malzeme kütüphanesi ve manuel/Excel malzeme girişi
- 8 oktav bant hesabı: `63, 125, 250, 500, 1000, 2000, 4000, 8000 Hz`
- Bariyersiz, bariyerli ve insertion loss sonuçları
- `dB(A)` geniş bant sonuç
- Cephe renk haritası, alıcı sonuç tablosu ve frekans tablosu
- Orbit kamera, pan ve zoom destekli yazılım tabanlı 3B/izometrik önizleme
- Basit PDF raporu ve Excel/CSV benzeri malzeme veri akışları

Kapsam dışı / ileri faz:
- Resmi sertifikasyon veya ÇED hesabı
- Tam CNOSSOS/FHWA/NMPB resmi trafik modeli
- BEM/FEM veya tam dalga çözücü
- Topografya, meteoroloji sınıfları, rüzgar ve sıcaklık gradyanı
- Çok cepheli kentsel kanyon için tam ray tracing
- Eski tek-bariyerli `.kstnb` dosyaları için otomatik migration

---

## 3. Koordinat ve Geometri Kuralları

- `X`: bina cephesi boyunca yatay eksen
- `Y`: bina önünden kaynak yönüne mesafe ekseni
- `Z`: yükseklik ekseni
- Bina ön cephesi `Y = 0` düzlemindedir.
- Bariyer bina önünde negatif `Y` tarafındadır.
- Gürültü kaynakları bariyerin daha önünde, negatif `Y` tarafında yer alır.
- Alıcılar bina ön cephesinde küçük cephe ofsetiyle konumlanır.
- Alıcı grid tek satır/sütun durumunda merkezlenmelidir.
- Kaynak, bariyer ve bina sıralaması doğrulanmalı; hatalı geometri uyarı üretmelidir.

---

## 4. Hesap Kuralları

Temel seviye yaklaşımı:
- `Lp(f) = Lw(f) + Dc(f) - A(f)`
- `A(f) = Adiv + Aatm + Agr + Abar + Amisc`

Aktif bileşenler:
- Geometrik yayılım
- Atmosferik absorpsiyon
- Basitleştirilmiş zemin etkisi / ayar bağlı düzeltme
- Bariyer kırınımı
- Bariyer malzeme iletim yolu
- Sınırlı absorptif yüz etkisi
- Sınırlı cephe-bariyer yansıma cezası

Genel kurallar:
- Spektrum tüm oktav bantlarda hesapta kullanılmalıdır.
- `OverallLw`, kaynak spektrumunu enerji bazlı hedef toplam seviyeye normalize eder.
- Çoklu kaynaklar aritmetik değil enerji toplamıyla birleşir.
- A-ağırlıklama bant sonuçlarından enerji toplamıyla üretilir.
- Bariyer zayıflatması negatif değer üretmemelidir.
- Sayısal sonuçlarda `NaN`/`Infinity` güvenli şekilde engellenmelidir.

---

## 5. Bariyer Kırınım ve İletim Kuralları

- Kırınım hesabı üst, sol ve sağ kenar yollarını değerlendirir.
- Yol farkı:
  `delta = d1 + d2 - d0`
- Fresnel sayısı:
  `N = 2 * delta / lambda`
- Maekawa ve Kurze-Anderson yardımcı formülleri ortak `NoiseBarrierDiffraction` alanında tutulur.
- Sonlu uzunluk etkisi yan kenar kaçaklarıyla hesaba katılır.
- Bariyer malzemesinin `TL/R(f)` değeri düşükse toplam performans malzeme iletim yolu tarafından sınırlanır.
- Diffracted ve transmitted enerji yolları enerji bazlı birleştirilir.
- Absorptif yüzler tam yansıma modeli değildir; kırınım/kaçak enerjisi ve yansıma cezası üzerinde sınırlı katkı olarak modellenir.

---

## 6. Çoklu Bariyer Kuralları

- Projede `List<BarrierModel> Barriers` kullanılır; tekil `Barrier` artık aktif model değildir.
- Her bariyerin adı, etkinlik durumu ve malzemesi vardır.
- Son bariyer silinemez.
- Her aktif bariyer bağımsız hesaplanır.
- Alıcı/frekans bazında birincil bariyer, en yüksek `Abar` veren bariyerdir.
- Çoklu bariyer kombinasyonu güncel olarak `Math.Max` ile yapılır.
- Bu yaklaşım paralel/seri bariyerlerde tutucu mühendislik önizlemesidir; ISO 9613-2 toplam-delta seri kombinasyonu ayrı geliştirme konusudur.
- Önizleme şimdilik örnek kırınım yolunu kaynağa en yakın bariyer üzerinden çizer.

---

## 7. Trafik ve Çizgisel Kaynak Kuralları

- Çizgisel kaynaklar segmentlere bölünür; her segment katkısı enerji toplamıyla birleştirilir.
- Trafik kaynakları debi, ortalama hız ve ağır taşıt oranından oktav spektrumu üretir.
- Trafik için seçilebilir önizleme profilleri:
  - CNOSSOS-EU benzeri
  - FHWA TNM benzeri
  - NMPB Routes benzeri
- Bu profiller resmi standart tabloları değildir; davranış trendi veren mühendislik önizleme modelleridir.
- Trafik kaynağı seçiliyken manuel `OverallLw` alanı kullanıcı beklentisini bozmasın diye gizlenir; motor trafik parametrelerini esas alır.
- Varsayılan trafik seviye ölçeği line-source motoruyla uyumlu olacak şekilde kalibre edilmiştir.

---

## 8. Malzeme Kütüphanesi

Bariyer malzemesi alanları:
- Kimlik, ad, kategori
- Kalınlık
- Yüzey kütlesi
- `Rw` / genel `TL`
- 8 oktav `TL` bantları
- 8 oktav absorpsiyon bantları
- `NRC`
- Kaynak
- Güven / confidence
- Notlar

Kurallar:
- Yerleşik malzemelerde kaynak ve güven alanları dolu olmalıdır.
- Kütüphane okuma/yazma hataları sessiz yutulmamalı; `LastError` veya durum mesajıyla görünür olmalıdır.
- Excel şablonu 8 oktav `TL` ve `Alpha` değerleriyle round-trip yapabilmelidir.
- Manuel malzeme eklemede tek `TL/Rw` değeri tüm bantlara uygulanabilir; ayrıntılı spektrum Excel ile girilebilir.
- Kullanıcı `.dwg` gibi yanlış çıktı uzantısı verirse sahte format üretilmemelidir.

---

## 9. UI İş Akışı

- Ana ekran sol/orta/sağ düzenindedir:
  - Sol/sağ paneller: senaryo, kaynak, bariyer ve malzeme girişleri
  - Orta: geniş önizleme/3B sahne
  - Alt/yan: sonuç tabloları, seçili alıcı ve frekans detayları
- Kaynak tipine göre ilgisiz UI alanları gizlenmelidir.
- `NoiseSource.Type` değişimi `IsLineLike`, `IsTraffic`, `ShowManualLw` görünürlüklerini tetiklemelidir.
- Numeric ve seçim değişiklikleri binding modele yazıldıktan sonra hesaplanmalıdır.
- Aynı `Project` nesnesinin iç değeri değiştiğinde önizleme `SimulationRevision` ile yeniden çizilmelidir.
- Geri düğmesi uygulamayı kapatmamalı; güvenli şekilde `LauncherWindow`'a dönmelidir.
- Görünür tüm metinler Türkçe olmalıdır.

---

## 10. Görselleştirme Kuralları

- Önizleme harici 3B paket gerektirmez; Avalonia çizim primitifleri ve yazılım projeksiyonu kullanır.
- Orbit, pan ve zoom desteklenir.
- Trafik kaynağı nokta gibi değil, kalın yol bandı ve merkez/kenar çizgileriyle çizilir.
- Çoklu bariyerler farklı renk paletiyle çizilir; devre dışı bariyerler gri tonlamada gösterilir.
- Cephe alıcı hücreleri sonuç seviyesine göre renklendirilir.
- Kaynak-alıcı görüş çizgisi ve örnek üst kenar kırınım yolu çizilebilir.
- Önizleme tam fiziksel ray tracing değildir; hesap geometrisini okunur kılan mühendislik görselidir.

---

## 11. Raporlama

Rapor içeriği:
- Proje adı
- Bina ölçüleri
- Bariyer listesi, boyutlar ve malzemeler
- Kaynak tipi ve kaynak parametreleri
- Alıcı grid bilgisi
- Hesap ayarları
- Frekans bandı sonuçları
- Bariyersiz/bariyerli karşılaştırma
- Ortalama, minimum, maksimum ve kritik alıcı sonuçları
- Model sınırlamaları ve güvenilirlik uyarıları

Mevcut PDF:
- Ek bağımlılık gerektirmeyen minimal PDF üreticisidir.
- Grafik ve cephe haritası içeren gelişmiş rapor şablonu ayrı geliştirme konusudur.

---

## 12. Kritik Dikkat Notları

- Kısa bariyerde yan kaçaklar insertion loss'u düşürmelidir.
- Bariyer yükseldikçe genel zayıflama artma eğilimi göstermelidir.
- Görüş çizgisi kapanmıyorsa insertion loss sıfıra yakın kalmalıdır.
- Düşük `TL` malzeme, kırınım hesabı iyi görünse bile toplam performansı sınırlamalıdır.
- Trafik modeli resmi CNOSSOS/FHWA/NMPB değildir; raporda önizleme modeli olduğu belirtilmelidir.
- Absorpsiyon katsayıları tam cephe yansıma/akustik ışın modeli yerine sınırlı ceza/bonus olarak kullanılır.
- Eski tek-bariyerli proje dosyaları kırıcı model değişikliği nedeniyle otomatik açılmayabilir.

---

## 13. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/NoiseBarrier/`
- `src/Kst.Acoustics.Core/NoiseBarrier/NoiseBarrierEngine.cs`
- `src/Kst.Acoustics.Core/NoiseBarrier/NoiseBarrierDiffraction.cs`
- `src/Kst.Acoustics.Core/NoiseBarrier/NoiseBarrierProject.cs`
- `src/Kst.Acoustics.Core/NoiseBarrier/BarrierModel.cs`
- `src/Kst.Acoustics.Core/NoiseBarrier/NoiseBarrierLibraryService.cs`

App:
- `src/Kst.Acoustics.App/Views/NoiseBarrierView.axaml`
- `src/Kst.Acoustics.App/ViewModels/NoiseBarrierViewModel.cs`
- `src/Kst.Acoustics.App/Controls/NoiseBarrierPreviewControl.cs`
- `src/Kst.Acoustics.App/Views/NoiseBarrierLibraryWindow.axaml`

Test:
- `src/Kst.Acoustics.Tests/`

---

## 14. Korunması Gereken Regresyon Alanları

- Maekawa referans noktaları
- Kurze-Anderson referans noktaları
- Bariyer yükseldikçe insertion loss artışı
- Kısa bariyerde yan kaçak performans düşüşü
- Düşük `TL` malzemenin performansı sınırlaması
- Görüş çizgisi kapanmıyorsa insertion loss'un sıfıra yakın kalması
- Çizgisel kaynak hesabının sonlu ve kararlı kalması
- Trafik parametrelerinden gerçekçi kaynak seviyesi üretimi
- Trafik model ailelerinin farklı sonuç üretmesi
- Absorptif bariyerin yansıma cezasını düşürmesi
- Excel malzeme şablonu round-trip
- Çoklu bariyerde ikinci yüksek bariyerin sonucu artırması
- Devre dışı bariyerin hesaba katılmaması

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
- M04 regresyonları dahil mevcut test paketi geçmeli

---

## 16. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Hesap değişikliği için önce `Core/NoiseBarrier` dosyalarını incele.
4. UI değişikliği için `NoiseBarrierView`, `NoiseBarrierViewModel` ve `NoiseBarrierPreviewControl` dosyalarını kontrol et.
5. Bariyer formülü veya trafik modeli değişirse regresyon testi ekle.
6. Malzeme kütüphanesi değişirse Excel round-trip davranışını kontrol et.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.
