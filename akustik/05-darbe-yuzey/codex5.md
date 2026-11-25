# M05 Codex — Darbe Emici ve Ses Emici Yüzey

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları, kritik sınırlamalar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M05 — Darbe Emici Yüzey |
| Ek mod | Ses Emici İnşaat Yüzeyi |
| Amaç | Katmanlı yüzeylerde düşme darbesi güvenliği ve ses emicilik ön tasarımı |
| Standart referansları | EN 1177, ASTM F1292 |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Tamamlandı; güvenilirlik ve raporlama bakımı sürüyor |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Katmanlı darbe emici yüzey tasarımı
- Half-sine darbe ivme-zaman sinyali üretimi
- HIC hesabı için sliding-window integral
- `Gmax`, `HIC`, pulse duration ve CFH hesabı
- Kafa kütlesi ve temas alanı parametreleri
- Katman kalınlığına göre seri-yay eşdeğer sertlik ve sönümleme
- EN 1177 / ASTM F1292 tarzı compliance taraması
- Sertifika tipi Markdown raporu
- JSON proje kaydet/aç
- Özel darbe malzemeleri
- Ayrı sekmede ses emici inşaat yüzeyi tasarımı
- Ses emici yüzey için oktav bant `α`, `αw`, `NRC`, `SAA`, kullanım önerisi ve rapor

Model sınırları:
- Sonuçlar simülasyon/ön hesap niteliğindedir; saha veya laboratuvar testinin yerine geçmez.
- Sıcaklık etkili EN 1177 prosedürü henüz yoktur; malzeme veritabanında sıcaklık bağımlı veri bulunmaz.
- Ses emici yüzey hesabı resmi laboratuvar ölçümü değildir.
- Ses emici tarafta tam empedans transfer matrisi henüz yoktur; Maa + poröz absorber mühendislik modeli kullanılır.

---

## 3. Darbe Hesap Kuralları

- HIC hesabı `g` birimindeki ivme-zaman serisi üzerinden yapılır.
- Darbe sinyali half-sine dalga formu ve damping parametreleriyle üretilir.
- HIC, standart prensibe uygun sliding-window integral ile hesaplanır.
- CFH hesabı hem `HIC <= 1000` hem `Gmax <= 200g` limitlerini birlikte kontrol eder.
- Sertifika tipi CFH bin arama ile yaklaşık 5 mm hassasiyetle bulunur.
- Compliance taraması varsayılan olarak `0.5, 1.0, 1.5, 2.0, 2.5, 3.0 m` yüksekliklerini değerlendirir.
- Geçersiz girişler (`NaN`, `Infinity`, negatif/sıfır kalınlık, geçersiz kafa kütlesi veya temas alanı) sessiz yutulmamalı; kullanıcıya `StatusMessage` ile bildirilmelidir.

---

## 4. Darbe Malzeme ve Katman Kuralları

- Katman hesabında kalınlık oranlarına göre ağırlıklı seri-yay yaklaşımı kullanılır.
- Eşdeğer E-modülü ve sönümleme kalınlık ağırlığıyla türetilir.
- Özel malzemeler proje dosyasına gömülür ve açılışta geri yüklenir.
- Proje dosyasında kafa kütlesi ve temas alanı da saklanmalıdır.
- Katman listesi ve 3D viewport seçimleri senkron kalmalıdır.
- Katman satırlarında malzeme/fonksiyon etiketleri doğru güncellenmelidir.

---

## 5. Ses Emici Yüzey Kuralları

Aktif hesap çıktıları:
- Oktav bant emilim katsayıları
- `αw`
- `NRC`
- `SAA`
- Kullanım önerisi
- Uyarılar

Model notları:
- Perfore/rezonatör davranışı Maa formülasyonuyla yaklaşık hesaplanır.
- Rezonans:
  `f0 = (c / 2π) * sqrt(sigma / (t_eff * D))`
- Uç düzeltme:
  `t_eff = t + 0.85 * d`
- Poröz arka katman varsa tepe daha geniş ve yüksek; yoksa daha dar ve düşük kabul edilir.
- Poröz absorber hesabı kalınlık asimptotu ve `λ/4 = t_porous + t_air` düşük frekans etkisini içerir.
- Eski empirik `airGapFactor` ve `LowMidWeight` yaklaşımı aktif karar değildir.

Başlangıç malzemeleri:
- Taş yünü
- Cam yünü
- PET
- Melamin köpük
- Akustik kumaş
- Perfore alçıpan
- Slotlu ahşap
- Hava boşluğu
- Rijit arka yüzey

---

## 6. UI İş Akışı

- Pencere iki ana sekmeye ayrılır:
  - Darbe Emici Yüzey
  - Ses Emici İnşaat Yüzeyi
- Darbe sekmesinde kafa kütlesi, temas alanı, katman listesi, sonuçlar, kaydet/aç ve rapor komutları bulunur.
- Ses emici sekmede katman ekle/sil/sırala, hava boşluğu ekle, özel malzeme ekle ve rapor alma komutları bulunur.
- Darbe sonuçları kullanıcıya tahmini olarak etiketlenmelidir.
- Hatalar ve geçersiz hesaplar kullanıcıya görünür durum mesajı üretmelidir.
- Eski `Watermark` yerine Avalonia uyumlu `PlaceholderText` kullanılmalıdır.

---

## 7. Raporlama

Darbe raporu:
- Parametreler
- Katman tablosu
- `Gmax`, `HIC`, CFH ve pulse duration
- ASCII ivme-zaman grafiği
- EN 1177 / ASTM F1292 tarama tablosu
- Her deneme için karar: `GEÇTİ`, `HIC aştı`, `Gmax aştı` vb.
- Sertifika tipi CFH
- Model sınırlamaları

Ses emici raporu:
- Katman özeti
- Oktav bant `α` tablosu
- `αw`, `NRC`, `SAA`
- 40 sütunluk ASCII spektrum çubuk grafiği
- Kullanım önerileri ve uyarılar

---

## 8. Kritik Dikkat Notları

- HIC ölçeği `g` biriminde kalmalıdır; SI ivme ile karışmamalıdır.
- Drop yüksekliği etkisi yaklaşık `sqrt(h)` ölçeğiyle doğrulanmalıdır.
- Kafa kütlesi etkisi yaklaşık `1/sqrt(m)` eğilimi göstermelidir.
- Temas alanı etkisi yaklaşık `sqrt(A)` eğilimiyle kontrol edilmelidir.
- Daha sert katman daha yüksek darbe ivmesi üretmelidir.
- Half-sine profil non-negatif ve deterministik olmalıdır.
- CFH her zaman HIC ve Gmax limitlerinden daha kısıtlayıcı olanı esas almalıdır.
- Ses emici yüzeyde hava boşluğu düşük frekans emilimini artırabilmelidir.
- Perfore panel rezonansının beklenen bantta tepe üretmesi korunmalıdır.

---

## 9. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/ImpactSurface/`
- `src/Kst.Acoustics.Core/ImpactSurface/ImpactCalculationService.cs`
- `src/Kst.Acoustics.Core/ImpactSurface/ImpactCalculationResult.cs`
- `src/Kst.Acoustics.Core/ImpactSurface/StandardComplianceReport.cs`
- `src/Kst.Acoustics.Core/ImpactSurface/ImpactReportBuilder.cs`
- `src/Kst.Acoustics.Core/AbsorptiveSurface/`
- `src/Kst.Acoustics.Core/AbsorptiveSurface/AbsorptiveSurfaceCalculationService.cs`
- `src/Kst.Acoustics.Core/AbsorptiveSurface/AbsorptiveReportBuilder.cs`

App:
- `src/Kst.Acoustics.App/ViewModels/ImpactSurfaceViewModel.cs`
- `src/Kst.Acoustics.App/ViewModels/ImpactLayerRowViewModel.cs`
- `src/Kst.Acoustics.App/ViewModels/AbsorptiveLayerRowViewModel.cs`
- `src/Kst.Acoustics.App/Views/ImpactSurfaceWindow.axaml`

Test:
- `src/Kst.Acoustics.Tests/ImpactValidationTests.cs`
- `src/Kst.Acoustics.Tests/`

---

## 10. Korunması Gereken Regresyon Alanları

- HIC hesap ölçeği
- Drop yüksekliği, kafa kütlesi, temas alanı ve sertlik monotonluğu
- Tek katman analitik half-sine sonucu
- Determinizm
- Golden EVA fixture sonucu
- Non-negatif half-sine profili
- Seri-yay tutarlılığı
- Geçersiz giriş doğrulaması
- Proje JSON round-trip ve özel malzeme geri yükleme
- Ses emici yüzey kalınlık/NRC davranışı
- Perfore panel Maa rezonans tepesi
- Hava boşluğunun 125 Hz civarı emilimi artırması

---

## 11. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M05 regresyonları dahil mevcut test paketi geçmeli

---

## 12. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Darbe hesabı için önce `Core/ImpactSurface` dosyalarını incele.
4. Ses emici yüzey için önce `Core/AbsorptiveSurface` dosyalarını incele.
5. UI değişikliği için `ImpactSurfaceViewModel` ve `ImpactSurfaceWindow` dosyalarını kontrol et.
6. Fizik/formülasyon değişirse regresyon testi ekle veya mevcut M05 testlerini çalıştır.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.

## 5 Kasım 2026 — sıralı modül denetimi

Sonlu olmayan özel malzeme özellikleri geçersiz sonuç olarak bildiriliyor (A06).

Ayrıntılı hata listesi ve doğrulama: depo kökündeki `modul-denetimi-2026-11-05.md`.
