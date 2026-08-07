# M06 Codex — Oda Modları Hesabı

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, hesap varsayımları, kritik sınırlamalar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Acoustics |
| Modül | M06 — Oda Modları Hesabı |
| Amaç | Dikdörtgen odalarda düşük frekans modlarını, basınç dağılımını ve difüzör ön tasarımını hesaplamak |
| Modül kodu | OM |
| UI rengi | `#C49BE4` |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Tamamlandı; DXF/CSV çıktıları ileri faz |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Dikdörtgen oda için axial, tangential ve oblique mod frekansları
- 0-500 Hz arası düşük frekans oda modu analizi
- Sıcaklığa bağlı ses hızı
- Schroeder geçiş frekansı
- Oda oran analizi ve Bolt bölgesi yaklaşımı
- Seçili mod için 3D basınç haritası
- Orbit kamera, wireframe oda ve yüzey basınç örnekleri
- QRD 1D difüzör ön tasarımı
- Skyline 2D difüzör ön tasarımı
- Düşük frekansta difüzör uygunluk uyarıları

İleri faz:
- DXF çıktı
- CSV çıktı
- Daha kapsamlı modal yoğunluk grafiği

---

## 3. Fizik Kuralları

Mod frekansı:

```text
f(nx, ny, nz) = (c / 2) * sqrt((nx/Lx)^2 + (ny/Ly)^2 + (nz/Lz)^2)
```

Ses hızı:

```text
c = 331.3 * sqrt(1 + T / 273.15)
```

Mod tipleri:
- Axial: yalnız 1 indeks > 0
- Tangential: tam 2 indeks > 0
- Oblique: 3 indeks > 0

Schroeder geçiş frekansı:

```text
fs = 2000 * sqrt(RT60 / V)
```

Kurallar:
- RT60 girilmezse hacme bağlı mühendislik varsayımıyla etkin RT60 ve Schroeder frekansı tahmin edilir.
- Hatalı oda boyutu, sıcaklık, maksimum frekans veya RT60 çökme yerine uyarılı boş sonuç üretmelidir.
- Modlar özellikle Schroeder frekansı altında anlamlıdır; kullanıcıya bu sınır görünür olmalıdır.

---

## 4. Basınç Haritası Kuralları

Seçili mod için basınç alanı:

```text
p(x, y, z) = cos(nx*pi*x/Lx) * cos(ny*pi*y/Ly) * cos(nz*pi*z/Lz)
```

Kurallar:
- Değer aralığı normalize olarak `-1 .. +1` kalmalıdır.
- `+1` sıkışma, `-1` seyrelme, `0` nötr bölge olarak gösterilir.
- Oda wireframe kutu olarak çizilir.
- Basınç örnekleri oda yüzeylerinde gösterilir.
- Tabloda mod seçimi değişince basınç haritası aynı modla güncellenir.
- Kamera orbit, pan/zoom ve sağ tık döndürme mantığıyla çalışır.

---

## 5. Difüzör Tasarımı Kuralları

QRD 1D derinlik dizisi:

```text
s(n) = n^2 mod N
d(n) = s(n) * lambda / (2N)
```

Skyline 2D hücre dizisi:

```text
s(x,y) = (x^2 + y^2) mod N
d(x,y) = s(x,y) * lambda / (2N)
```

Kurallar:
- `N` asal sıra değeridir; desteklenen tipik değerler `7, 11, 13, 17`.
- Hedef frekans seçili moda veya kullanıcı girdisine bağlı olabilir.
- Maksimum derinlik sınırı uygulanmalı; aşan kuyular kırpılmalı ve uyarı üretilmelidir.
- Schroeder frekansının belirgin altında kalan hedeflerde difüzör yerine bas tuzağı veya ayarlı yutucu önerisi verilmelidir.
- Skyline görünümü `N x N` kare hücreden oluşur ve izometrik 3B önizleme ile gösterilir.

---

## 6. UI İş Akışı

- Ana pencere üç ana bölgeden oluşur:
  - Giriş paneli
  - Mod sonuç tablosu
  - 3D basınç/difüzör görsel alanı
- Giriş değişiklikleri hesaplamayı otomatik yeniler.
- Uyarı ve durum mesajları kullanıcıya görünür olmalıdır.
- Difüzör tasarımı ayrı sekmede veya aynı pencere içinde net ayrılmış bir bölümde gösterilir.
- Görünür tüm metinler Türkçe olmalıdır.

---

## 7. Kritik Dikkat Notları

- İlk axial mod frekansı oda boyutuyla tutarlı olmalıdır.
- Basınç örnekleri normalize sınır dışına taşmamalıdır.
- Geçersiz girişler hesap motorunda güvenli ele alınmalıdır.
- RT60 tahmini kullanıcı girdisinin yerine sessizce geçmemeli; tahmin olduğu belirtilmelidir.
- Düşük modal frekanslarda QRD difüzör önerisi yanıltıcı olabilir; uygunluk uyarısı korunmalıdır.
- QRD/Skyline derinlik dizileri asal sıra ve maksimum derinlik sınırına göre regresyonla korunmalıdır.

---

## 8. Önemli Kaynak Alanları

Core:
- `src/Kst.Acoustics.Core/RoomModes/RoomMode.cs`
- `src/Kst.Acoustics.Core/RoomModes/RoomModeType.cs`
- `src/Kst.Acoustics.Core/RoomModes/RoomModeInput.cs`
- `src/Kst.Acoustics.Core/RoomModes/RoomModeResult.cs`
- `src/Kst.Acoustics.Core/RoomModes/RoomModeCalculator.cs`
- `src/Kst.Acoustics.Core/RoomModes/RoomRatioAnalyzer.cs`
- `src/Kst.Acoustics.Core/RoomModes/PressureFieldSampler.cs`
- `src/Kst.Acoustics.Core/RoomModes/Diffusers/`

App:
- `src/Kst.Acoustics.App/ViewModels/RoomModesViewModel.cs`
- `src/Kst.Acoustics.App/Views/RoomModesWindow.axaml`
- `src/Kst.Acoustics.App/Views/RoomModesWindow.axaml.cs`
- `src/Kst.Acoustics.App/Controls/RoomModePressureControl.cs`
- `src/Kst.Acoustics.App/Controls/QrdDiffuserPreviewControl.cs`

Test:
- `src/Kst.Acoustics.Tests/`

---

## 9. Korunması Gereken Regresyon Alanları

- İlk axial mod frekansı
- Geçersiz giriş uyarıları
- RT60 tahmini
- Basınç örneklerinin normalize kalması
- QRD kalıntı dizisi
- Skyline hücre dizisi
- Düşük frekans difüzör uygunluk uyarısı
- Maksimum derinlik kırpması

---

## 10. Doğrulama Komutları

Sık kullanılan kontroller:

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen genel hedef:
- Build: 0 hata
- M06 regresyonları dahil mevcut test paketi geçmeli

---

## 11. Oturum Başlangıç Akışı

1. `maincodex.md` oku.
2. Bu dosyayı oku.
3. Mod hesabı için önce `Core/RoomModes` dosyalarını incele.
4. Difüzör değişikliği için `Core/RoomModes/Diffusers` dosyalarını incele.
5. UI değişikliği için `RoomModesViewModel`, `RoomModesWindow` ve ilgili kontrol dosyalarını kontrol et.
6. Formülasyon/görsel örnekleme değişirse regresyon testi ekle veya mevcut M06 testlerini çalıştır.
7. Görünür UI metinlerinde Türkçe karakter kontrolü yap.
