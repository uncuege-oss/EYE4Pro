# Landscape — Ana Başlık 7

**Ürün:** EYE4PRO Landscape (park, peyzaj, kentsel açık alan tasarımı)
**Modül aralığı:** 70–79
**Durum:** Planlandı — M70 ilk modül (kod yazılmadı)

## Modüller

| # | Klasör | Modül Adı | Durum |
|---|--------|-----------|-------|
| 70 | [70-park-tasarimi](70-park-tasarimi/codex70.md) | Park Tasarımı (Sert Zemin / Hardscape) | F0-F6 tamam (MVP: Core + App park penceresi) |
| 71 | [71-bitkilendirme-peyzaj](71-bitkilendirme-peyzaj/codex71.md) | Bitkilendirme & Peyzaj (Yumuşak Zemin / Softscape) | Planlandı |
| 72 | — | Sulama & Yağmursuyu Hasadı | _öneri_ |
| 73 | — | Park Aydınlatma Entegrasyonu (M15 köprü) | _öneri_ |
| 74 | — | Peyzaj Metraj & Keşif | _öneri_ |
| 75–79 | — | _rezerve_ | — |

## Altlık Zinciri (kim neyi tüketir / üretir)

Landscape başlığı, M50/M60 **paylaşılan salt-okunur artifact** desenini sürdürür.

| Modül | Tüketir (salt-okunur) | Üretir |
|---|---|---|
| **M70 Park Tasarımı** | `.e4arazi.json` (M60 — arazi yüzeyi/kotlar), `.e4bina.json` (M50 — varsa bina ayak izi) | **`.e4park.json`** (park sert zemin yerleşimi) |
| **M71 Bitkilendirme** | `.e4park.json` (M70), `.e4arazi.json` (M60) | `.e4peyzaj.json` (bitki yerleşimi) |

- **Arazi:** Park, araziyi M60'tan alır. Arazi geometrisi M70'te **değiştirilmez**; yalnız kot/yüzey
  okunur (eğim, kademe, sınır). M60 yoksa düz altlık + DXF park sınırı ile çalışılabilir.
- **Bina:** Arazi içinde bina varsa `.e4bina.json` ayak izi salt-okunur yüklenir; park yerleşimi
  bina oturum alanı + yapı yaklaşma (setback) mesafesini **boş bırakır / dışlar**. Bina geometrisi
  M50'de düzenlenir (maincodex §4.5).
- **Bitki ayrımı:** Kullanıcının istediği gibi ağaç/çalı/bitki yerleşimi **ayrı modül (M71)**'dedir;
  M70 yalnız sert zemin (yollar, döşeme, oturma alanı, bank, süs havuzu) ile ilgilenir.

## Tüketici Modüller (cross-başlık entegrasyon)

`.e4park.json` da disiplinler arası altlık olabilir:

| Tüketici | Başlık | Kullanım |
|---|---|---|
| M71 Bitkilendirme | Landscape | Park sert zemin boşluklarına bitki/ağaç yerleşimi |
| **M15 Çevre/Park Aydınlatma** | Elektrik | Yürüyüş yolu/oturma alanı sınırlarına armatür yerleşimi (ileride köprü) |
| M05 Darbe Emici Yüzey | Akustik | Park içi çocuk oyun alanı varsa EN 1177 düşme yüzeyi köprüsü (ileride) |
| Mekanik | Mekanik | Süs havuzu / sulama dış tesisat güzergâhı (ileride) |

## Standartlar

Planlı Alanlar İmar Yönetmeliği (yeşil alan / aktif yeşil alan oranı, yapı yaklaşma sınırı),
Mekânsal Planlar Yapım Yönetmeliği, erişilebilirlik (TS 9111 / TS 12576 — engelli erişimi, rampa/eğim),
çocuk oyun alanı güvenliği (EN 1176/EN 1177 — M05 köprüsü), Otopark Yönetmeliği (park içi otopark/erişim),
süs havuzu hidrolik/güvenlik ön kabulleri.

## Kaynak Kod

`../src/Kst.Acoustics.Core/Landscape/` — park/peyzaj iş mantığı (yerleşim, alan, motor)
`../src/Kst.Acoustics.App/Landscape/` — UI katmanı (Avalonia)
