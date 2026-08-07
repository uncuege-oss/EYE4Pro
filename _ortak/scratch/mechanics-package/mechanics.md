# EYE4PRO Mechanics Geliştirme Kuralları

Bu paket EYE4PRO Mechanics modül ailesini bağımsız geliştirmek için hazırlanmıştır. Amaç, geliştirilen mekanik modüllerin mevcut EYE4PRO sistemine sonradan güvenli biçimde entegre edilebilmesidir.

---

## 1. Genel Kural

Bu çalışma paketi içinde yalnız mekanik modül ailesi geliştirilecektir.

Akustik ve elektrik modülleri mevcut ürünün çalışan alanlarıdır. Bu alanlarda zorunlu olmadıkça değişiklik yapılmamalıdır.

---

## 2. Çalışılacak Ana Alanlar

Mekanik iş mantığı:

```text
src/Kst.Acoustics.Core/Mechanical/
```

Mekanik UI ekranları:

```text
src/Kst.Acoustics.App/Mechanical/
```

Mekanik modül merkezi:

```text
src/Kst.Acoustics.App/Views/MechanicsLauncherWindow.axaml
src/Kst.Acoustics.App/Views/MechanicsLauncherWindow.axaml.cs
```

Mekanik dokümantasyon:

```text
mekanik/
mekanik/_index.md
mekanik/21-kanal-hesabi/codex21.md
```

Testler:

```text
src/Kst.Acoustics.Tests/
```

Test dosyaları mekanik adıyla ayrılmalıdır. Örnek:

```text
src/Kst.Acoustics.Tests/MechanicalKanalHesabiTests.cs
```

---

## 3. Dokunulmaması Gereken Alanlar

Aşağıdaki alanlar yalnız entegrasyon zorunluysa değiştirilmelidir:

```text
src/Kst.Acoustics.Core/Acoustic...
src/Kst.Acoustics.Core/BuildingAcoustics/
src/Kst.Acoustics.Core/Electric/
src/Kst.Acoustics.App/Electric/
src/Kst.Acoustics.App/BuildingAcoustics/
akustik/
elektrik/
```

Ana ekran bağlantısı zaten hazırlanmıştır. Bu nedenle aşağıdaki dosyalara normalde tekrar dokunulmasına gerek yoktur:

```text
src/Kst.Acoustics.App/Views/EngineeringHomeWindow.axaml
src/Kst.Acoustics.App/Views/EngineeringHomeWindow.axaml.cs
```

---

## 4. Teknik Kurallar

- Stack: C# + Avalonia, `net10.0`.
- Tüm görünür UI metinleri Türkçe olmalıdır.
- Türkçe karakterler doğru yazılmalıdır: `ö`, `ü`, `ç`, `ş`, `ı`, `İ`, `ğ`.
- İş mantığı Core katmanında kalmalıdır.
- UI katmanı Core'a bağımlı olabilir; Core katmanı UI'a bağımlı olamaz.
- Büyük dosya okuma veya uzun hesaplama işlemleri UI thread'ini kilitlememelidir.
- Hesap motorları test edilebilir, UI'dan bağımsız sınıflar olarak yazılmalıdır.
- JSON proje dosyalarında eksik alanlar geriye dönük uyumluluk için güvenli deserialize edilmelidir.
- AXAML içinde `&` karakteri `&amp;` olarak kaçırılmalıdır.
- `StackPanel` doğrudan `Padding` almaz; gerekiyorsa `Border` wrapper kullanılmalıdır.
- Pencere büyüdükçe içerik alanı büyümelidir; sabit büyük `Height` değerleri kullanılmamalıdır.

---

## 5. M21 Kanal Hesabı İlk Kapsam

İlk mekanik modül:

```text
M21 — Kanal Hesabı
```

Beklenen temel özellikler:

- Dikdörtgen kanal hesabı
- Dairesel kanal hesabı
- Debi, hız, alan ve eşdeğer çap hesabı
- Kanal segment listesi
- Segment bazlı basınç kaybı
- Yerel kayıp katsayısı girişi
- Hat toplam basınç kaybı
- `.e4p21` proje kaydet/aç
- Birim testleri

Önerilen Core model adları:

```text
MekanikKanalProje
KanalHatti
KanalSegmenti
KanalKesiti
KanalBasincKaybiSonucu
```

---

## 6. Dosya ve Namespace Düzeni

Önerilen Core klasör yapısı:

```text
src/Kst.Acoustics.Core/Mechanical/
src/Kst.Acoustics.Core/Mechanical/KanalHesabi/
src/Kst.Acoustics.Core/Mechanical/KanalHesabi/Models/
src/Kst.Acoustics.Core/Mechanical/KanalHesabi/Services/
```

Önerilen App klasör yapısı:

```text
src/Kst.Acoustics.App/Mechanical/
src/Kst.Acoustics.App/Mechanical/KanalHesabi/
src/Kst.Acoustics.App/Mechanical/KanalHesabi/Views/
src/Kst.Acoustics.App/Mechanical/KanalHesabi/ViewModels/
```

Namespace örnekleri:

```csharp
Kst.Acoustics.Core.Mechanical.KanalHesabi
Kst.Acoustics.App.Mechanical.KanalHesabi
```

---

## 7. Teslim Kuralları

Geliştirme sonunda tüm EYE4PRO klasörü geri verilmemelidir.

Yalnız değişen veya eklenen dosyalar teslim edilmelidir:

```text
mekanik/
src/Kst.Acoustics.Core/Mechanical/
src/Kst.Acoustics.App/Mechanical/
src/Kst.Acoustics.App/Views/MechanicsLauncherWindow.axaml
src/Kst.Acoustics.App/Views/MechanicsLauncherWindow.axaml.cs
src/Kst.Acoustics.Tests/*Mechanical*
```

Eğer zorunlu olarak başka dosya değiştiyse, teslim notunda açıkça yazılmalıdır:

```text
Değişen dosya:
Neden değişti:
Entegrasyon etkisi:
```

---

## 8. Teslimden Önce Çalıştırılacak Komutlar

Teslimden önce aşağıdaki komutlar başarılı olmalıdır:

```bash
dotnet build src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
dotnet test src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
dotnet run --project src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
```

Test veya build uyarısı/hatası varsa teslim notuna yazılmalıdır.

---

## 9. Teslim Notu Formatı

Teslim edilen zip içinde kısa bir not bulunmalıdır:

```text
Geliştirilen modüller:
Eklenen dosyalar:
Değiştirilen dosyalar:
Çalıştırılan testler:
Bilinen eksikler:
Entegrasyon için dikkat:
```

---

## 10. Önemli Hatırlatma

Bu paket geliştirme kolaylığı için tam proje iskeleti içerir. Geri dönüşte mevcut EYE4PRO klasörünün üzerine komple kopyalama yapılmamalıdır. Entegrasyon, yalnız mekanik dosyaları ve açıkça belirtilmiş bağlantı dosyaları üzerinden yapılmalıdır.
