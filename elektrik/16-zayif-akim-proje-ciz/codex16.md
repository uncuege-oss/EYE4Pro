# M16 Codex — Zayıf Akım Proje Çiz

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. M16, M12 ile **ortak motoru** paylaşan ikinci kabuktur; yalnız zayıf akım disiplininde çalışır. Ayrıntılı kronolojik günlük değildir.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Electric |
| Modül | M16 — Zayıf Akım Proje Çiz |
| Amaç | DXF/M50 mimari plan üzerinden zayıf akım eleman yerleşimi, kablolama, merkez özeti ve DXF çıktı |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Proje uzantısı | `.e4p16` |
| Durum | Geliştiriliyor |

---

## 2. Mimari Karar — Ortak Motor, İki Kabuk

> **Ayrışma kararı (2026-06-13):** M12'deki kuvvetli + zayıf akım tek pencerede
> büyüyünce ikiye ayrıldı. M12 = yalnız kuvvetli akım, M16 = yalnız zayıf akım.
> **Kod fork edilmedi.** Core (`BasitProjeCiz`) modelleri/servisleri tektir;
> aynı `BasitProjeCizWindow` + `BasitProjeCizViewModel`, kurucuya verilen
> `BpcDisiplin modulModu` ile modüle sabitlenir.

- M12 girişi: `new BasitProjeCizWindow()` → `BpcDisiplin.KuvvetliAkim`.
- M16 girişi: `new BasitProjeCizWindow(BpcDisiplin.ZayifAkim)` (Electric launcher "Zayıf Akım Proje Tasarımı" kartı).
- `BasitProjeCizViewModel.ModulModu` açılışta bir kez `ModulModuAyarla` ile sabitlenir;
  **çalışma zamanında disiplin değiştirilemez** (`OnAktifDisiplinChanged` sapmayı geri çevirir).
- `ModulKuvvetliMi` / `ModulZayifMi` özellikleri UI görünürlüğünü sürer (sistem seçici,
  Tek Hat/M13 aktarım butonları, mahal listesi disiplin sekmeleri).
- Çapraz görünürlük **yok**: M16 kuvvetli akım elemanlarını göstermez/çizmez.

---

## 3. Disiplin Ayrımının Tek Kaynağı

- `BpcSistemKatalogu` registry'si her sistemin `Disiplin` alanını taşır.
- Eleman/kablo → disiplin sınıflaması: `BpcSistemKatalogu.Disiplini(BpcElemanTipi)`,
  `Disiplini(BpcKablo)`, `Disiplini(BpcPaftaTipi)`. Zayıf akım eleman tipleri katalogdan türetilir;
  listede olmayan her tip kuvvetli akıma düşer (Buat, kablo tavası, topraklama ortak → M12).
- `DisiplinAyiklamaServisi` (Core):
  - `Say(proje)` → kuvvetli/zayıf eleman, kablo, devre sayıları.
  - `Ayikla(proje, hedef)` → diğer disiplinin eleman/kablo/devre/mahal satırlarını siler,
    **geometriyi (kat/oda/açıklık/DXF/M50 bağı) korur**, `ModulDisiplini` damgalar,
    kalan devrelerdeki silinmiş eleman referanslarını arındırır.

---

## 4. Dosya Formatı ve Geçiş

- M16 projeleri `.e4p16` uzantısıyla kaydedilir; `BasitProjeCizProje.ModulDisiplini = ZayifAkim`.
- M16 açılış diyaloğu hem `.e4p16` hem eski `.e4p12` dosyalarını listeler.
- **İçe aktarma:** `ProjeyiYukle` içinde `DisiplinMutabakati` çalışır — yüklenen projede
  modüle ait olmayan içerik varsa `DisiplinAyiklamaServisi.Ayikla` ile ayıklanır ve
  durum çubuğunda uyarı + "'.e4p16' olarak kaydedin" önerisi gösterilir.
- M12 eski karma `.e4p12` açarsa zayıf akımı ayıklayıp uyarır (veri proje seviyesinde temiz kalır).
- DXF çıktısı kat içeriğini gezdiği için ayrım otomatiktir: M16 projesi yalnız zayıf akım
  elemanı taşıdığından çıktıda yalnız zayıf akım katmanları (E4P-DATA/TV/CCTV/...) üretilir.

---

## 5. Kapsam (M12'den miras, zayıf akımla sınırlı)

- Bina geometrisi tüketimi: `.e4bina` salt-okunur yükleme + mutabakat (düzenleme M50'de).
- Mahal listesi + otomatik/manuel eleman yerleştirme (zayıf akım sembolleri).
- Zayıf akım sistemleri: Data, TV, Telefon, CCTV, Acil Anons, Diafon, Yangın Algılama.
- Kablolama topolojileri: BuatDaisy (Data/TV/Telefon), YildizMerkez (CCTV/Anons/Diafon), Loop (Yangın).
- DXF proje çıktısı.
- **M16 → M13 Zayıf Akım Tek Hat aktarımı (tamamlandı):** "📡 M13'e Aktar" butonu
  `ZayifAkimTekHatImportServisi.Donustur(proje)` ile **bellek içi** dönüşüm yapar
  (dosya round-trip yok → serileştirme uyumsuzluğu yok), sonucu M13'ün native
  `.e4p13` formatına (CamelCase + IgnoreCycles) yazar. Kullanıcı M13'te "Proje Aç"
  ile açar. Zayıf akım sistemi yoksa "aktarılacak sistem yok" uyarısı verilir.

---

## 6. Önemli Kaynak Alanları

- Core ortak: `src/Kst.Acoustics.Core/Electric/BasitProjeCiz/` (Models + Services)
- Disiplin motoru: `Services/DisiplinAyiklamaServisi.cs`, `Models/BasitProjeCizModels.cs` (`BpcSistemKatalogu.Disiplini*`)
- UI ortak: `src/Kst.Acoustics.App/Electric/BasitProjeCiz/Views/BasitProjeCizWindow.axaml(.cs)`,
  `ViewModels/BasitProjeCizViewModel*.cs` (`ModulModu`, `ModulModuAyarla`, `DisiplinMutabakati`)
- Giriş: `src/Kst.Acoustics.App/Views/ElectricLauncherWindow.axaml(.cs)` (`OpenZayifAkimProjeCiz_OnClick`)
- Test: `src/Kst.Acoustics.Tests/DisiplinAyiklamaTests.cs`

---

## 7. Korunması Gereken Regresyon Alanları

- M12 penceresi yalnız kuvvetli, M16 yalnız zayıf akım sistemlerini listeler (kabuk sabit).
- Çalışma zamanında disiplin değiştirilemez (sapma `ModulModu`'na geri çevrilir).
- `DisiplinAyiklamaServisi.Ayikla` geometriyi korur, yalnız elektrik içeriğini ayıklar.
- M16 eski `.e4p12` açınca kuvvetli akımı ayıklar, zayıf akımı + mahal satırlarını korur.
- Kaydetme `ModulDisiplini`'ni damgalar; M16 → `.e4p16`, M12 → `.e4p12`.
- Ayıklama, kalan devrelerdeki silinmiş eleman referanslarını temizler.

---

## 8. Doğrulama Komutları

```bash
dotnet build src/Kst.Acoustics.slnx
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
```

Son bilinen durum: build 0 hata; custom runner 309 test geçer (7 disiplin/aktarım testi dahil),
xUnit M13 import testleri 8/8 geçer.

## 5 Kasım 2026 — sıralı modül denetimi

Yalnız karşı disiplinin mahal veya topraklama verisini içeren projeler de ayıklama akışına giriyor (A10).

Ayrıntılı hata listesi ve doğrulama: depo kökündeki `modul-denetimi-2026-11-05.md`.
