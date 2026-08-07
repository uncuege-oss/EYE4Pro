# Terrain — Ana Başlık 6

**Ürün:** EYE4PRO Terrain (arazi, topografya, hafriyat)
**Modül aralığı:** 60–69
**Durum:** Planlandı — M60 ilk modül

## Modüller

| # | Klasör | Modül Adı | Durum |
|---|--------|-----------|-------|
| 60 | [60-hafriyat-hesapla](60-hafriyat-hesapla/codex60.md) | Hafriyat Hesapla (Yarma/Dolgu) | MVP tamam (F0-F8 + App + renk haritası + M15 köprü) |
| 61 | [61-bina-temeli-kota-oturtma](61-bina-temeli-kota-oturtma/codex61.md) | Bina Temeli Kota Oturtma | Tamam (F0-F5 + M50/M35 bina kaynakları) |
| 62 | — | Şev & İstinat Duvarı | _öneri_ |
| 63 | — | Yol & Saha Profili | _öneri_ |
| 64 | — | Saha Drenajı & Yağmursuyu | _öneri_ |
| 65 | — | Kazı Güvenliği & Şantiye (İksa) | _öneri_ |
| 66–69 | — | _rezerve_ | — |

## Paylaşılan Arazi Geometrisi

- Arazi yüzeyi M50 bina geometrisi deseniyle **paylaşılan salt-okunur artifact**'tır: **`.e4arazi.json`**.
- Üretici M60'tır; tüketiciler bu dosyayı salt-okunur yükler ("Arazi Yükle").
- `SemaSurum` migrasyonu + `GeometriRevizyon` damgası + mutabakat raporu baştan dahildir.

## Tüketici Modüller (cross-başlık entegrasyon)

`.e4arazi.json` disiplinler arası ortak altlıktır. Bilinen tüketiciler:

| Tüketici | Başlık | Kullanım |
|---|---|---|
| M61 Bina Temeli Kota Oturtma | Terrain | Hafriyat sonrası kotlara temel oturtma |
| Mimari M30-M39 | Mimari | Tabii zemin/tesviye kotu, kademeli bina kesiti |
| **M15 Çevre/Park Aydınlatma** ✅ | Elektrik | "Arazi Yükle" köprüsü kuruldu — direklere `ZeminKotuM` atanır; sonraki: 3D yerleşim, gerçek hat uzunluğu |
| Mekanik (M21+) | Mekanik | İleride: dış tesisat güzergâh kotu, eğim |
| Akustik (M04 ses bariyeri vb.) | Akustik | İleride: arazi topografyası difraksiyon/yansıma altlığı |

- Tüketici, arazi **geometrisini değiştirmez**; yalnız kot/yüzey verisini okur (M50 salt-okunurluk deseni).
- Tüketici hem `.e4arazi.json` yolunu hem gömülü kopyasını saklar; revizyon farkında "araziden güncelle?" sorulur.

## Standartlar

Average-end-area / prizmatik hacim yöntemleri, Karayolları Teknik Şartnamesi (kazı/dolgu, kabarma katsayıları),
İSG kazı/iksa, TBDY (geoteknik ön kontrol), Planlı Alanlar İmar Yönetmeliği (tabii zemin/tesviye kotu).

## Kaynak Kod

`../src/Kst.Acoustics.Core/Terrain/` — arazi iş mantığı (DTM/TIN, hafriyat motoru)
`../src/Kst.Acoustics.App/Terrain/` — UI katmanı
