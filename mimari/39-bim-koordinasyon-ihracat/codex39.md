# CODEX 39 — BIM Koordinasyon ve İhracat

> **Durum:** MVP tamamlandı (2026-06-13)
> **Ana takip:** [../codex30.md](../codex30.md)
> **Önceki:** M31-M37 MVP tamamlandı; Faz 9 M50 entegrasyonu (mimari→`.e4bina`, M12/M08 köprüleri) bitti.
> **Amaç:** Mimari tasarımın disiplinler arası koordinasyonunu ve dış formatlara ihracatını
> ele almak: revizyon farkı, askıda veri (mutabakat) raporu ve IFC/glTF/DXF ihracat yönü.

---

## 0. Kullanım Protokolü

1. Yeni oturumda önce `maincodex.md`, sonra `mimari/codex30.md`, sonra bu dosya okunur.
2. Aktif adım bu dosyadaki küçük faz tablosundan ve `codex30.md` Faz 10 tablosundan bulunur.
3. Kod yazmadan önce ilgili Core klasörleri (`Architecture/Exporting`, `Reports`, `Materials`) incelenir.
4. Her işlemden sonra hem bu dosya hem `mimari/codex30.md` güncellenir.
5. Bir adım tamamlandığında durulur ve kullanıcıdan sonraki adıma geçiş onayı istenir.

---

## 1. Hedef

M39, mimari modeli "tek başına" olmaktan çıkarıp koordinasyon ve dış dünya katmanını ekler:

- **Revizyon farkı:** iki mimari tasarım (veya iki `.e4bina`) arasında eklenen/silinen/değişen
  mahal, kat ve alan farkını okunabilir raporlamak.
- **Disiplin mutabakatı:** geometri/atama değiştiğinde askıda kalan veriyi (artık var olmayan
  elemana bağlı malzeme ataması, hedefsiz finisaj vb.) raporlamak.
- **İhracat yönü:** glTF/OBJ (M35 sözleşmesi) ve DXF gibi pratik değişim formatları;
  IFC için kütüphane/format kararı (uzun vade).

İlk hedef tam IFC üretimi değildir; küçük, test edilebilir Core katmanları + net format kararıdır.

---

## 2. Kapsam

### MVP Kapsamında

- `Architecture/Coordination` (veya `Exporting`) altında revizyon farkı ve mutabakat modelleri.
- **Revizyon farkı servisi:** iki `MimariProje`/`BinaGeometri` → fark raporu
  (eklenen/silinen/değişen mahal, kat; alan/hacim deltaları).
- **Mutabakat raporu:** askıda `MalzemeAtamalari` (var olmayan 3D eleman Id'sine bağlı),
  hedefsiz finisaj, geometri-veri tutarsızlıkları.
- IFC/glTF/DXF için **format kararı** ve (varsa) hafif DXF/OBJ ihracat sözleşmesinin netleştirilmesi.
- Boş/eksik veride güvenli; Core test runner kapsamı.

### MVP Dışında

- Tam IFC property set üretimi (kütüphane bağımlılığı; uzun vade).
- Gerçek 3D mesh tessellation/optimizasyon.
- Bulut/sunucu tabanlı model koordinasyonu (BCF, clash detection).
- M50 ortak modeline yazma (mimari yalnız üretir — Faz 9 kararı).

---

## 3. Veri Modeli Taslağı

```text
RevizyonFarki
├── EklenenMahaller[] / SilinenMahaller[] / DegisenMahaller[]   (Id, ad, alan eski→yeni)
├── KatFarklari[]     (eklenen/silinen kat, kat alan deltası)
└── ToplamAlanDeltaM2, ozet sayilari

MutabakatRaporu
├── AskidaAtamalar[]  (MalzemeAtamasi → var olmayan eleman Id)
├── HedefsizFinisaj[] (profil → karşılığı olmayan mahal tipi)
└── ToplamAskidaSayisi
```

Revizyon farkı girdisi iki `MimariProje` (ör. kayıtlı eski vs. güncel) veya iki `BinaGeometri`.
Mutabakat girdisi tek `MimariProje` (kendi içsel tutarlılığı) — M50'nin `IGeometriBagimliVeri`
deseniyle uyumlu kalır.

---

## 4. Kod Konumu

```text
src/Kst.Acoustics.Core/Architecture/Coordination/
├── RevizyonFarki.cs            (fark modelleri)
├── RevizyonFarkiServisi.cs     (iki proje/geometri → fark)
├── MutabakatRaporu.cs          (askıda veri modelleri)
└── MutabakatServisi.cs         (proje → askıda veri raporu)
```

Testler mevcut Core test runner içine eklenir: `src/Kst.Acoustics.Core.Tests/Program.cs`.

---

## 5. M31-M37 / M50 Bağlantısı

- Revizyon farkı, M37 `MahalListesiOlustur` / `MimariRaporUretimServisi` çıktısını veya doğrudan
  3D modeli kaynak alır; Id eşleşmesi M35 `KaynakMahalBloguId` üzerinden yürür.
- Mutabakat, M36 `MalzemeAtamalari` ve `FinisajProfilleri`'ni 3D model elemanlarıyla karşılaştırır.
- M50 tarafındaki `GeometriMutabakat`/`IGeometriBagimliVeri` deseni referans alınır; mimari
  kendi içsel mutabakatını üretir (geometri üreticisi olduğu için).

---

## 6. IFC / Format Yaklaşımı (KARAR — 10.3, 2026-06-13)

Format öncelik kararı:
- **OBJ → ŞİMDİ uygulandı.** Düz metin, bağımlılıksız, evrensel içe aktarım. `MimariObjIhracatServisi`
  mahal hacimleri + duvarları kutu mesh olarak yazar (M35 GLB/OBJ sözleşmesindeki "OBJ yedek" somutlaştı).
- **GLB/glTF → ertelendi.** İkili tampon + JSON; OBJ ihtiyacı karşıladığı için MVP dışı. M35
  `Model3DIhracatSecenekleri` sözleşmesi (GLB birincil) korunur; serileştirici sonra yazılır.
- **DXF → ertelendi.** Kod tabanında ortak `Exporting/DXF` var; mimari 2D plan DXF'i ileride bununla
  hizalanır (ayrı iş).
- **IFC → uzun vade.** Tam IFC üretimi ağır kütüphane (xBIM / IfcOpenShell) gerektirir; bağımlılık ve
  bakım yükü nedeniyle MVP'ye alınmadı. İhtiyaç netleşince kütüphane değerlendirmesi yeniden açılır.

Gerekçe: bağımlılıksızlık ilkesi (Core saf mantık) + "en az iki çalışan format" hedefi OBJ ile
karşılanıyor; ağır formatlar gerçek talep oluşunca ele alınacak.

---

## 7. Küçük Faz Listesi

### M39.1 — Codex Açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 39.1.1 | Klasör + `codex39.md` | Kapsam, model taslağı ve faz listesi yazıldı | ✅ Tamamlandı |
| 39.1.2 | Ana takipleri güncelle | `maincodex.md`, `_index.md`, `codex30.md` M39 durumunu gösterir | ✅ Tamamlandı |

### M39.2 — Revizyon Farkı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 39.2.1 | Fark modeli | Eklenen/silinen/değişen mahal + kat + alan deltası modeli | ✅ Tamamlandı |
| 39.2.2 | Fark servisi | İki proje/geometriden fark üretir (Id eşleşmeli) | ✅ Tamamlandı |
| 39.2.3 | Test | Mahal ekle/sil/alan değiştir senaryoları doğru rapor çıkar | ✅ Tamamlandı |

### M39.3 — IFC / Format Kararı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 39.3.1 | Araştırma + karar | IFC/glTF/OBJ/DXF yönü ve kütüphane kararı belgelenir | ✅ Tamamlandı |
| 39.3.2 | (Opsiyonel) OBJ/DXF | Bağımlılıksız hafif bir ihracat sözleşmesi/iskeleti | ✅ Tamamlandı (OBJ) |

### M39.4 — Disiplin Mutabakatı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 39.4.1 | Mutabakat modeli | Askıda atama / kırık tip referansı modeli | ✅ Tamamlandı |
| 39.4.2 | Mutabakat servisi | Proje içsel tutarlılık raporu üretir | ✅ Tamamlandı |
| 39.4.3 | Test | Askıda atama / kırık tip referansı senaryoları yakalanır | ✅ Tamamlandı |

### M39.5 — Regresyon ve Kapanış

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 39.5.1 | Regresyon | Tüm M39 testleri + Core/test build temiz | ✅ Tamamlandı |

---

## 8. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M39 MVP tamamlandı — Faz 10 (M39) kapandı |
| Sonraki Adım | Mimari başlıkta açık tüm faz/modül MVP'leri bitti; yön kullanıcı kararına bağlı (UI bağlama, GLB/DXF/IFC, M38, M21 köprüsü) |
| Kod Durumu | Revizyon farkı + format kararı/OBJ + disiplin mutabakatı (`MutabakatServisi`) hazır |
| Test Durumu | Core test runner 65/65 geçti; Core/tam çözüm build 0 hata |

---

## 9. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-13 | M39 başlatıldı; kapsam revizyon farkı + disiplin mutabakatı + IFC/format kararı olarak belirlendi |
| 2026-06-13 | IFC tam üretimi uzun vadeye bırakılması öngörülüyor (ağır kütüphane); MVP'de araştırma+karar (10.3) |
| 2026-06-13 | Faz 10.1 tamamlandı: M39 klasörü ve `codex39.md` açıldı; ana takip bağlantıları güncellendi; sıradaki adım 39.2 revizyon farkı |
| 2026-06-13 | Faz 10.2 tamamlandı: `RevizyonFarki`/`RevizyonMahalGirisi`/`RevizyonMahalDegisikligi` modelleri + `RevizyonFarkiServisi` (iki `MimariProje` veya iki `BinaGeometri` → kararlı oda Id eşleşmeli fark) eklendi; eklenen/silinen/değişen mahal, eklenen/silinen kat ve toplam alan deltası üretiliyor. Core test runner 63/63 geçti |
| 2026-06-13 | Faz 10.3 tamamlandı: Format kararı belgelendi (OBJ şimdi, GLB/DXF ertelendi, IFC uzun vade — §6). `MimariObjIhracatServisi` eklendi: bağımlılıksız Wavefront OBJ; mahal hacimleri + duvarlar kutu mesh (8 vertex/6 yüz), grup adlı, geçerli indeksli. Core test runner 64/64 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 10.4 tamamlandı + M39.5 regresyon, **M39 MVP kapandı**: `MutabakatRaporu`/`MutabakatBulgusu` + `MutabakatServisi.Denetle` eklendi — askıda atama (var olmayan 3D eleman) ve kırık tip referansı (atama/finisaj → katalogda olmayan yapı elemanı tipi) yakalanıyor. §3'teki "hedefsiz finisaj" kırık tip referansına çevrildi (varsayılan katalog yanlış-pozitifini önlemek için). Core test runner 65/65 geçti, tam çözüm build 0 hata |
