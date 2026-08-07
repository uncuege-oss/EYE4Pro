# M71 Codex — Bitkilendirme & Peyzaj (Yumuşak Zemin / Softscape)

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Yalnızca güncel kararlar, kapsam,
sistem davranışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

> **Durum:** PLANLANDI — kod yazılmadı. M70 `.e4park.json` üretimi (F5) tamamlandıktan sonra başlar.
> Desen: Terrain M61 (salt-okunur altlık tüketici) + Core-first + UI'siz analitik test.
> **Bağlı codex'ler:** [codex70](../70-park-tasarimi/codex70.md) (park sert zemin altlığı),
> [codex60](../../terrain/60-hafriyat-hesapla/codex60.md) (arazi kotu)

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Landscape |
| Modül | M71 — Bitkilendirme & Peyzaj |
| Amaç | Park yeşil alanlarına ağaç, çalı, çiçek ve çim yerleşimi; bitki kütüphanesi; bitki listesi/metrajı |
| Proje uzantısı | `.e4p71` |
| Girdiler (salt-okunur) | `.e4park.json` (M70 — park sert zemin + yeşil alan), `.e4arazi.json` (M60 — zemin kotu) |
| Çıktı | `.e4peyzaj.json` (bitki yerleşimi) + bitki listesi raporu |
| Standart ref. | Mekânsal Planlar Yapım Yönetmeliği, bitki dikim aralıkları/tepe taç çapı ön kabulleri, iklim/bölge uygunluğu |
| Stack / UI dili | C# + Avalonia (net10.0) / Türkçe |
| Çekirdek | `src/Kst.Acoustics.Core/Landscape/` · UI: `src/Kst.Acoustics.App/Landscape/` |

---

## 2. Modül Kapsamı

Aktif kapsam (MVP hedefi):
- M70 park altlığını salt-okunur yükle: yeşil alan (sert zemin DIŞI) = ekilebilir alan
- **Bitki kütüphanesi:** ağaç, çalı, yer örtücü, çiçek, çim — tür, tepe taç çapı, dikim aralığı,
  boy, ışık/su isteği, yaprak dökme durumu, bölge/iklim uygunluğu
- Bitki yerleşimi:
  - **Tekil ağaç** (nokta + tür; taç çapı dairesi)
  - **Ağaç sırası / alee** (çizgi + aralık)
  - **Çalı/bitki grubu** (poligon + tür + yoğunluk)
  - **Çim/yer örtücü alanı** (poligon)
- Çakışma/aralık kontrolü: ağaç tepe çakışması, bina/yola min. mesafe, sert zemine taşma
- Zemin kotu: araziden (M60) her bitkiye dikim kotu
- Bitki listesi/metrajı: tür × adet, alan (m²), çim m²
- `.e4p71` kaydet/aç + `.e4peyzaj.json`

İleri/eksik alanlar:
- Mevsimsel/zaman içinde büyüme görselleştirmesi
- Gölge/güneşlenme analizi (mimari M38 cephe/günışığı köprüsü)
- Sulama ihtiyacı → M72 sulama köprüsü
- Otomatik tür önerisi (iklim/toprak/ışık)
- Maliyet/metraj köprüsü (`Kst.Acoustics.Metraj`) → M74
- 3D bitki sembolleri / gerçekçi render

---

## 3. Mimari Kararlar

- **Park salt-okunur:** M70 `.e4park.json` altlık alınır; sert zemin geometrisi M71'de DEĞİŞTİRİLMEZ.
  Bitki yalnız yeşil alana (sert zemin dışı) yerleştirilir.
- **Arazi salt-okunur:** Zemin kotu M60'tan okunur (maincodex §4.7).
- **Yerleşim/kontrol motoru Core'da**, UI yalnız ViewModel/görünüm (maincodex §4.4).
- **Revizyon farkı:** Park (M70) güncellenmişse açılışta "park altlığı değişti, yerleşim doğrulansın mı?" uyarısı —
  özellikle yeşil alan küçüldüyse sınır dışı kalan bitkiler işaretlenir.
- Bitki kütüphanesi başta gömülü statik liste; ileride düzenlenebilir/dış dosya.

---

## 4. Veri Modeli (taslak, `Core/Landscape/Models`)

```
PeyzajModeli           ← kök: SemaSurum, GeometriRevizyon, ParkRevizyon?, AraziRevizyon?,
│                          Yerlesimler[], BitkiListesi
├── BitkiTuru          ← Id, Ad(Latince/Türkçe), Kategori(Agac/Cali/YerOrtucu/Cicek/Cim),
│                         TacCapiM, DikimAraligiM, BoyM, IsikIstegi, YaprakDokme, BolgeUygunluk
├── BitkiYerlesimi (soyut) ← Id, TurId, ZeminKotuM? (araziden)
│   ├── TekilAgac      ← Nokta
│   ├── AgacSirasi     ← Çizgi, AralikM
│   ├── BitkiGrubu     ← Poligon, YogunlukAdetM2
│   └── CimAlani       ← Poligon
└── BitkiListesi       ← (Tür → Adet, Alan m²) döküm + Uyari[] (çakışma, sınır dışı)
```

---

## 5. Faz Planı (programlama)

Desen: Core-first, UI'siz analitik test. Her faz `Core.Tests` ile kapanır.

| Faz | Başlık | Çıktı / Tanım | Test (UI'siz) |
|-----|--------|---------------|---------------|
| **F0** | İskelet | `Core/Landscape` model/servis iskeleti (M70 ile ortak klasör) | derlenir |
| **F1** | Bitki kütüphanesi + park altlık | `BitkiTuru` + gömülü kütüphane; `.e4park.json` salt-okunur yükle → yeşil alan | yeşil alan çıkarımı testi |
| **F2** | Bitki yerleşimi + kot | Tekil/sıra/grup/çim yerleşimi + araziden kot; sınır-içi kontrolü | yerleşim + kot atama testleri |
| **F3** | Çakışma/aralık kontrolü | Tepe taç çakışması, min. mesafe (bina/yol), sert zemine taşma | çakışma analitik testleri |
| **F4** | Bitki listesi/metraj + rapor | `BitkiListesi` (tür×adet, m²) + Türkçe özet | döküm testleri |
| **F5** | Kaydet/aç + `.e4peyzaj.json` | `PeyzajGeometriServisi` (round-trip + revizyon) | round-trip + revizyon testleri |
| **F6** | Görselleştirme | App: park altlığı + bitki sembolleri (taç çapı) çizimi + liste raporu | App derlenir |
| **F7** | İleri | Büyüme/mevsim, gölge analizi (M38), sulama (M72), metraj (M74), tür önerisi | — |

**Önerilen MVP kesimi:** F0–F6. M70 `.e4park.json` (F5) hazır olmadan M71 başlamaz.

---

## 6. Yeni Oturum Notu

1. `maincodex.md` → `landscape/_index.md` → [codex70](../70-park-tasarimi/codex70.md) → bu dosya okunur.
2. M70 sert zemin altlığı (`.e4park.json`) hazır olmadan M71 geliştirilmez.
3. Park ve arazi salt-okunurdur; M71 yalnız bitki katmanı üretir.
