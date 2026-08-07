# M61 Codex — Bina Temeli Kota Oturtma

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Yalnızca güncel kararlar, kapsam,
sistem davranışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

> **Durum:** F0-F5 TAMAM + iki bina kaynağı. `TemelOturtmaWindow`: arazi + çok bina (.e4bina **veya** .e4mimari) → adapter → oturt → kademe + 3B kutular + blok seçici + kot raporu + ±0.00 override + kot düzenleme + **kotlu kesit**. 9 Core testi; App 0 hata; gerçek dosyalarla doğrulandı. Tüm planlı fazlar + opsiyonel M35 adaptörü kapandı.
> **Bağlı codex'ler:** [codex60](../60-hafriyat-hesapla/codex60.md) (arazi kaynağı),
> [codex50](../../ortak-moduller/50-bina-geometri/codex50.md) (bina kaynağı),
> [codex30](../../mimari/codex30.md) (mimari bina kütlesi)

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Terrain |
| Modül | M61 — Bina Temeli Kota Oturtma |
| Amaç | Hafriyat sonrası arazi yüzeyine bina temelini oturtmak; bodrum, giriş/çıkış, yangın merdiveni kotlarını ayarlamak |
| Proje uzantısı | `.e4p61` |
| Girdiler (salt-okunur) | `.e4arazi.json` (M60), `.e4bina.json` (M50) veya mimari M35 bina kütlesi |
| Stack / UI dili | C# + Avalonia (net10.0) / Türkçe |
| Çekirdek | `src/Kst.Acoustics.Core/Terrain/` · UI: `src/Kst.Acoustics.App/Terrain/` |

---

## 2. Modül Kapsamı

Aktif kapsam (MVP hedefi):
- Salt-okunur iki girdiyi birleştir: tasarım (hafriyat sonrası) arazi yüzeyi + bina ayak izi/temel
- Bina ayak izini araziye oturt → **±0.00 datum** atama
- Bodrum kotu, giriş/çıkış kotu, yangın merdiveni çıkış kotu ayarı
- Eğimli arazide **kademeli bina** desteği (kat/blok bazlı farklı oturma kotları)
- Otomatik kontroller: bodrum tabanı zemin altında mı, giriş kotu uyumu, gerekli kazı derinliği
- Çıktı: bina-arazi kotlu kesiti + temel altı yarma/dolgu hacmi (M60'a geri besleme verisi)

İleri/eksik alanlar:
- İmar tabii zemin/tesviye kotu denetimi (Planlı Alanlar İmar Yönetmeliği)
- Çok bloklu/kademeli site otomatik kot dağılımı
- Rampa ve erişim eğim kontrolleri (erişilebilirlik)

---

## 3. Mimari Kararlar

- Girdilerin ikisi de **salt-okunur**: arazi M60'ta, bina M50/mimaride düzenlenir; M61 yalnız kot oturtma yapar.
- Geometri değişikliği bu modülde YAPILMAZ; yalnız oturma kotu/datum ve kontrol verisi üretilir.
- Hesap/kontrol motoru `Core/Terrain/`'de; UI yalnız ViewModel/görünüm (maincodex §4.4).
- Revizyon farkı: arazi veya bina güncellenmişse açılışta "güncellenmiş, yeniden oturtulsun mu?" uyarısı.

---

## 4. Veri Modeli (taslak, `Core/Terrain/Models`)

```
TemelOturtmaModeli   ← kök: SemaSurum, AraziRevizyon, BinaRevizyon, SifirKotM (±0.00)
├── BlokOturma       ← BlokId, OturmaKotM, KademeNo
├── KotTanimi        ← Tip(Bodrum/Giris/Cikis/YanginMerdiveni), KotM
└── OturtmaKontrol   ← KaziDerinligiM, ZeminAltiMi, Uyari[]
```

---

## 5. Faz Planı (M60 F7 sonrası başlar)

| Faz | Başlık | Çıktı |
|-----|--------|-------|
| **F0** ✅ | İskelet | `Core/Terrain` altında modeller + servis |
| **F1** ✅ | Girdi köprüleri | Arazi: `AraziTuketim`. Bina iki kaynak: `BinaAyakIziAdaptoru.Donustur(BinaGeometri)` (M50 `.e4bina`) **ve** `.Donustur(Bim3DModel)` (mimari M35; en alt kat mahal kutuları sınır kutusu, KatNo≥1 üst/≤0 bodrum). App: "Bina Ekle" + "Mimari Ekle" (.e4mimari → `PlanTaslagi.Model3D` ?? `Model3DUretimServisi.Uret`). | 3 Core testi (M50 tek/çok oda + M35) ✅ |
| **F2** ✅ | Oturtma motoru (Core) | `TemelOturtmaServisi`: ±0.00 datum (ortalama zemin veya override), bodrum taban/çatı kotu, kazı derinliği, serbest yükseklik, kademe sırası, giriş/çıkış/yangın merdiveni/bodrum kot tanımları, gömülü/kazısız uyarıları — 5 Core testi ✅ |
| **F3** ✅ | Kot tanımları (UI) | App `TemelOturtmaWindow`: kot raporu + ±0.00 override + **kot elle düzenleme** (Çıkış/YanginMerdiveni/Bodrum taban → "Kotları Uygula" → kazı derinliği + uyarılar + 3B kutu yeniden) | App ✅ |
| **F4** ✅ | Kademeli bina | App: **çok bina ekle** → `TemelOturtmaServisi.Oturt` kademe sırası (zemin kotuna göre) → her blok ayrı 3B kutu + blok seçici + tüm bloklar raporu. Eğimli arazide kademeli yerleşim | App ✅ (çok-blok yamaç doğrulandı) |
| **F5** ✅ | Çıktı | `KesitServisi` (arazi profili + blok kesit dikdörtgenleri, Core) + App `KesitControl`/`KesitWindow`: üniform ölçekli kotlu kesit, ±0.00/çatı/bodrum etiketleri, tabii zemin dolgusu | 1 Core testi (profil + blok) ✅ |

## Gerçekleşen kaynak (F0+F1+F2)
- `Core/Terrain/Models/TemelOturtma.cs`: `BinaAyakIzi`, `KotTipi`, `KotTanimi`, `BlokOturma`, `TemelOturtmaModeli`.
- `Core/Terrain/Services/TemelOturtmaServisi.cs`: `OturtBlok` (tek blok) + `Oturt` (çok blok, kademe sırası).
- `Core/Terrain/Services/BinaAyakIziAdaptoru.cs`: M50 `BinaGeometri` → `BinaAyakIzi` (en alt kat ayak izi:
  tek oda→poligon, çok oda→sınır kutusu; KatNo≥0 üst kat, KatNo<0 bodrum; ortalama kat yüksekliği).
  Ayrıca mimari M35 `Bim3DModel` → `BinaAyakIzi` aşırı yüklemesi (mahal kutuları sınır kutusu; KatNo≥1 üst/≤0 bodrum).
- `Core/Terrain/Services/Arazi3DGorunumServisi.BlokKutusuYuzeyleri`: ayak izi → 3B kutu (taban+tavan+duvar).
- App: `App/Terrain/Views/TemelOturtmaWindow.axaml(.cs)` + Terrain başlatıcıda M61 kartı (`OpenTemelOturtma_OnClick`).
  Arazi+bina yükle (Task.Run) → adapter → `TemelOturtmaServisi.OturtBlok` → 3B (arazi TIN + temel kutusu) + kot raporu.
  F3: sağ panelde Çıkış/Yangın merd./Bodrum taban düzenleme kutuları + "Kotları Uygula" → `KotAyarla` ile
  `BlokOturma.Kotlar` güncellenir, kazı derinliği + düzenleme uyarıları yeniden, `Yenile()` 3B+rapor tazeler.
  F4: "Bina Ekle" çok bina biriktirir (`_ayaklar`), Oturt `TemelOturtmaServisi.Oturt` ile kademe atar; her blok
  ayrı id aralığında 3B kutu (`20000+i*1000`); `BlokSecici` ComboBox ile düzenlenecek blok seçilir; rapor tüm
  blokları kademe sırasıyla listeler. ±0.00 override yalnız tek bina varken uygulanır (çok blokta her blok kendi zemini).
- F5: `Core/Terrain/Models/KesitModeli.cs` (`KesitBlok`) + `Services/KesitServisi.cs` (blok ortalama Y'sinden geçen
  X-ekseni kesit hattı, arazi profili örnekleme, blok→kesit dikdörtgeni). App: `KesitControl` (DrawingContext üniform
  ölçek, tabii zemin dolgusu, ±0.00 kesik çizgi, çatı/bodrum etiketleri) + `KesitWindow`; "Kotlu Kesit" butonu.
- Arazi kotu örnekleme: ayak izi köşeleri + merkezi `AraziTuketim.KotSorgula` ile; kapsam-dışı uyarısı.
- Konvansiyon: ±0.00 = `sifirKotOverride ?? ortalamaZemin`; bodrum taban = ±0.00 − bodrumKat×katY;
  kazı derinliği = max(0, zemin − bodrumTaban); serbest yükseklik = ±0.00 − zemin (negatif → gömülü uyarı).
- Testler: `Core.Tests/Program.cs` `M61*` (helper `M61Blok`/`TuketimDuz`/`TuketimEgik`).

## 6. Açık Riskler / Kararlar

1. Bina kaynağı: M50 `.e4bina.json` mi, mimari M35 kütlesi mi, ikisi de mi? (M60 sonrası netleşecek.)
2. Kademeli bina kesiti çizimi M60'ın kotlu-kesit altyapısını paylaşmalı.
3. M61, M60 üretime geçtikten sonra başlar (arazi yüzeyi hazır olmadan oturtma anlamsız).
