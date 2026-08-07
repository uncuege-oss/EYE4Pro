# Faz 7 — Publish Öncesi Manuel Doğrulama Kontrol Listesi

## Otomatik Testler (CI-uyumlu)

```bash
dotnet test Kst.Acoustics.Metraj.Tests/Kst.Acoustics.Metraj.Tests.csproj
```

| Test | Neyi doğrular |
|------|---------------|
| `CountingTests` (3) | Block sayımı, attribute gruplama, combined counting |
| `MetricsTests` (5) | Birim dönüşümü, uzunluk hesabı (line/arc/circle/polyline/ellipse) |
| `TemplateTests` (4) | İmza invaryansları, library sıralaması, exploded/polyline eşleştirme |
| `DxfDocumentLoaderTests` (3) | Entity yükleme, renk çözümleme, birim okuma |
| `DwgToDxfConverterTests` (3) | API stabilitesi, missing file, default path formatı |
| `RuleTests` (3) | RuleSet JSON, RGB serileştirme, RuleSetStore kaydet/yükle |
| `Phase3ValidationTests` (1) | 3-durumlu (blok/karma/patlatılmış) sayım tutarlılığı |
| `EndToEndPipelineTests` (1) | DXF → Load → Filter → Count → Match → XLSX tam zincir |
| `ManualSmokeTest` (1) | ODA Converter ile gerçek DWG (ortam değişkeni gerektirir) |

**Toplam: 38 test**

---

## Manuel Doğrulama (Publish Öncesi Zorunlu)

### 1. ODA Converter End-to-End

```bash
# macOS
METRAJ_TEST_DWG=/path/to/real/project.dwg dotnet test --filter ManualSmokeTest

# Windows
set METRAJ_TEST_DWG=C:\path\to\real\project.dwg
dotnet test --filter ManualSmokeTest
```

**Kontrol noktaları:**
- [ ] DXF dosyası üretildi (boyut > 0)
- [ ] Entity sayısı > 0
- [ ] XLSX raporu üretildi (boyut > 1 KB)

### 2. Tam UI Akışı

- [ ] Uygulamayı başlat
- [ ] İlk açılışta ODA yolu diyaloğu çıkıyor mu?
- [ ] Geçerli ODA yolu girildikten sonra kaydediliyor mu?
- [ ] Sonraki açılışlarda diyalog tekrar çıkmıyor mu?
- [ ] Şablon DXF/DWG ekleme/çıkarma çalışıyor mu?
- [ ] Proje DWG/DXF seçimi çalışıyor mu?
- [ ] Birim seçimi (mm/cm/m) UI'da değiştirilebiliyor mu?
- [ ] Tolerans değerleri ayarlanabiliyor mu?
- [ ] Kural Düzenleyici açılıyor mu? (Proje seçilmeden uyarı veriyor mu?)
- [ ] "Çalıştır" butonu ilerleme gösteriyor mu?
- [ ] XLSX raporu üretiliyor ve "Aç" butonu çalışıyor mu?
- [ ] Hata durumunda kullanıcıya anlaşılır mesaj gösteriliyor mu?

### 3. XLSX Rapor Doğrulaması

- [ ] **By-Layer** sayfası: Katman adları, Adet ve Toplam Uzunluk sütunları dolu
- [ ] **By-Color** sayfası: Renk (ACI/RGB), Entity Sayısı, Uzunluk dolu
- [ ] **By-Template** sayfası: Şablon adları ve tespit adetleri doğru
- [ ] **Eksik Eşleşmeler** sayfası: Varsa konum ve parça bilgisi görünüyor

### 4. Platform Publish

```bash
# macOS ARM64
dotnet publish -r osx-arm64 -c Release --self-contained -o dist/macos-arm64

# macOS x64
dotnet publish -r osx-x64 -c Release --self-contained -o dist/macos-x64

# Windows x64
dotnet publish -r win-x64 -c Release --self-contained -o dist/win-x64
```

- [ ] macOS ARM64 binary çalışıyor
- [ ] macOS x64 binary çalışıyor
- [ ] Windows x64 EXE çalışıyor
- [ ] Her platformda ODA Converter ile DWG dönüşümü test edildi

---

## Bilinen Limitler

| Durum | Açıklama |
|-------|----------|
| T-kavşaklı şablonlar | Hierholzer path'i tek Euler yolu üretir; GeometricSignature 4 varyant dener, çoğu durumda yeterli |
| Cluster radius | Otomatik (diagonal × 0.75); nadir durumlarda manuel override gerekebilir |
| ODA Converter | Harici bağımlılık; sürüm değişikliklerinde argüman formatı kontrol edilmeli |
