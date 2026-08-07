# EYE4Pro – Zayıf Akım Sistemleri Geliştirme Planı

> Hedef: Mevcut BasitProjeCiz modülüne (aynı proje, aynı DXF/kat/oda altyapısı)
> zayıf akım sistemlerini eklemek. Sistemler: Data, TV, Telefon, Yangın Algılama,
> CCTV, Acil Anons.

## Onaylanan kararlar

1. **Aynı modül / aynı proje** — ayrı modül yok. DXF arka plan, katlar, odalar,
   duvar-snap ve `DuvarBoyuncaRota` rota motoru, kaydet/aç paylaşılıyor.
2. **İki seviyeli navigasyon** — üstte disiplin seçici (⚡ Kuvvetli Akım / 🔌 Zayıf
   Akım), altında o disiplinin paftaları.
3. **Her sistem kendi merkezine sonlanır** — Data→rack, TV→splitter, Telefon→MDF,
   Yangın→santral, CCTV→NVR, Anons→amfi. Kat panosuna bağlanmaz.
4. **Önce basit, sonra derinleştir** — tüm sistemler önce "cihaz + merkez + kablo".
   Yangın loop/zone, CCTV IP/coax, anons zone matrisi sonraki fazlarda.
5. **Önce refaktör** — sabit `BpcPaftaTipi` enum + dağınık switch'ler veri-güdümlü
   bir sistem kataloğuna taşınacak; sonra 6 sistem eklenecek. Mevcut 56 test korunur.

---

## Disiplin / Sistem haritası

| Disiplin | Sistem | Prefix | Topoloji | Merkez | Cihazlar |
|----------|--------|--------|----------|--------|----------|
| Kuvvetli | Aydınlatma | A | Anahtarlı devre | Kat Panosu | armatür, anahtar… |
| Kuvvetli | Priz | P | Buat→Pano | Kat Panosu | priz tipleri |
| Kuvvetli | Mekanik | M | Cihaz→Pano (yıldız) | Kat Panosu | klima iç/dış |
| Zayıf | Data | D | Cihaz→(kat kutusu)→merkez | Data Rack | data prizi |
| Zayıf | TV | TV | Cihaz→splitter→merkez | TV Merkezi | TV prizi |
| Zayıf | Telefon | T | Cihaz→MDF | Telefon Dağıtım | telefon prizi |
| Zayıf | CCTV | C | Cihaz→merkez (yıldız) | NVR | kamera (dome/bullet) |
| Zayıf | Acil Anons | S | Cihaz→merkez (zone) | Anons Amfi | hoparlör |
| Zayıf | Yangın Algılama | Y | Loop / zone | Yangın Santrali | dedektör, buton, siren |

> Not: Data/TV/Telefon mevcut **Priz** desenine (cihaz→ara toplayıcı→merkez),
> CCTV/Anons mevcut **Mekanik** desenine (cihaz→merkez yıldız) çok yakın.
> Yangın en karmaşık olan; ayrı faz.

---

## Durum

- ✅ **Faz 0** tamamlandı — registry, iki seviyeli navigasyon, genel merkez/devre modeli.
- ✅ **Faz 1** tamamlandı — Data / TV / Telefon (cihaz → ara toplayıcı → merkez).
- ✅ **Faz 2** tamamlandı — CCTV / Acil Anons (yıldız).
- ✅ **Faz 3** tamamlandı — Yangın Algılama (loop, basit seviye). Zone/sebep-sonuç sonraki iterasyon.

---

## Faz 0 — Veri-güdümlü sistem altyapısı (refaktör) ✅

**Amaç:** Davranışı değiştirmeden mevcut 3 paftayı registry'ye taşımak.

### Core modeller
- `enum BpcDisiplin { KuvvetliAkim, ZayifAkim }`
- `enum BpcSistemTopoloji { AnahtarliDevre, BuatDaisy, YildizMerkez, Loop }`
- `record BpcSistemTanim(string Anahtar, string Ad, BpcDisiplin Disiplin,
   string LinyePrefix, string KabloRengi, BpcSistemTopoloji Topoloji,
   BpcElemanTipi MerkezTipi, IReadOnlyList<BpcElemanTipi> CihazTipleri,
   IReadOnlyList<BpcElemanTipi> AraToplayiciTipleri)`
- `static class BpcSistemKatalogu` — tüm `BpcSistemTanim`'ları tutar, `Anahtar`
  ile lookup verir. (Aydınlatma/Priz/Mekanik buraya tanımlanır.)

### Genelleştirmeler
- **Merkez (hub):** `KatPanosu` artık "merkez" ailesinin bir üyesi. Render'da
  `ElemanMerkezMi(tip)` her merkez tipini her paftada gösterir (bugün KatPanosu
  için yapılan özel kontrol genelleşir).
- **Genel ELV devresi:** yeni sistemler için tek model
  `BpcSistemDevre { Id, SistemAnahtar, MerkezId?, AraToplayiciId?, CihazIds[] }`.
  Mevcut `BpcDevre` (anahtar/vavien/komütatör mantığı kendine özgü) korunur;
  `BpcPrizDevre`/`BpcMekanikDevre` ileride bu modele birleştirilebilir (opsiyonel).
- **Kablo rengi veri-güdümlü:** `BpcKablo`'ya `SistemAnahtar?` alanı; renk
  registry'den okunur. Mevcut `BpcKabloTipi` renkleri korunur (geriye uyum).

### ViewModel dispatch sadeleştirme
Şu switch'ler registry lookup'a iner:
- `KabloAktifPaftadaMi` → kablonun sistemi == aktif sistem mi
- `ElemanEtiket`, `ElemanDuvarSnapMi` → registry/eleman tablosu
- `ManualType*` (code-behind) → aktif sistemin `CihazTipleri` listesi
- `Guncelle3DGorunum` filtresi → `el.SistemAnahtar == aktifSistem || ElemanMerkezMi`

### UI
- Üst bar iki seviyeli: disiplin butonları + aktif disiplinin pafta sekmeleri
  (registry'den üretilir).
- Sağ panel manuel eleman combosu registry'den dinamik kurulur (her sisteme ayrı
  combo yerine tek dinamik liste).

### Doğrulama
- Mevcut 56 test yeşil kalmalı. Gerekirse `BpcPaftaTipi` enum'u registry
  `Anahtar` string'lerine eşleyen geçiş katmanı.

---

## Faz 1 — Data / TV / Telefon (priz benzeri) ✅

**Cihaz → (kat dağıtım kutusu) → merkez** akışı. Priz kablo modunun genel
ELV sürümü.

### Eklenecekler
- Eleman tipleri: `DataPriz`, `TvPriz`, `TelefonPriz`, merkezler `DataRack`,
  `TvMerkezi`, `TelefonDagitim`, ara toplayıcı `KatDagitimKutusu`.
- Semboller (`BasitProjeCizKutuphaneServisi`): RJ45 üçgeni (data), TV prizi,
  telefon prizi, rack/dağıtım dikdörtgenleri.
- Kablo modu: cihaz seç → (ara toplayıcı seç) → merkez seç. `BpcSistemDevre`
  oluşturulur, `DuvarBoyuncaRota` ile çizilir.
- Linye numaralandırma: D1/D2…, TV1/TV2…, T1/T2…
- Merkez içeriği menüsü: linye başına cihaz (port) sayısı + kablo metrajı.
  (Sigorta/güç hesabı yok — zayıf akım.)

---

## Faz 2 — CCTV / Acil Anons (yıldız) ✅

**Cihaz → merkez doğrudan.** Mekanik kablo modunun genel sürümü.

### Eklenecekler
- Eleman tipleri: `Kamera` (dome/bullet ayrımı sembolde), `Hoparlor`,
  merkezler `CctvKayit` (NVR), `AnonsAmfi`.
- Semboller: kamera (dome=daire+yön, bullet=dikdörtgen+yön), hoparlör.
- Kablo modu: cihaz → merkez (yıldız), `BpcSistemDevre` (AraToplayiciId yok).
- Linye: C1/C2… (kanal), S1/S2… (anons hattı).
- Merkez içeriği: kamera/kanal sayısı, hoparlör/zone sayısı.

---

## Faz 3 — Yangın Algılama (loop / zone) — en kapsamlı ✅ (basit seviye)

> Ayrı faz; kendine özgü modeli olabilir.

### Kapsam (kademeli)
1. Basit: dedektör/buton/siren → santral, radyal hat (Faz 1 deseniyle).
2. Derinleştirme: loop topolojisi (santral → cihaz1 → cihaz2 → … → santral),
   zone tanımı, cihaz adresleme.
3. İleri: sebep-sonuç (cause & effect) matrisi, raporlama.

### Eklenecekler
- Eleman tipleri: `DumanDedektor`, `IsiDedektor`, `YanginButonu`, `Siren`,
  `Flasor`, merkez `YanginSantrali`.
- Semboller: standart yangın sembolleri.
- Loop rota motoru (mevcut daisy chain'in kapalı-loop varyantı).

---

## Açık implementasyon kararları (uygulama sırasında netleşecek)

- `BpcPrizDevre`/`BpcMekanikDevre` Faz 0'da `BpcSistemDevre`'ye birleştirilsin mi,
  yoksa yeni sistemler için mi kullanılsın? (Önce yeni sistemler, sonra opsiyonel migrasyon.)
- Kablo tipi: per-sistem enum değeri mi, yoksa tek `ZayifAkimKablosu` + `SistemAnahtar` mı?
  (Öneri: `SistemAnahtar` + registry rengi — daha az enum şişmesi.)
- DXF çıktı katman adları: her sistem için `E4P-<SISTEM>` katmanı.
- Kütüphane penceresi: zayıf akım cihazları için ayrı sekme(ler).

---

## Önerilen sıra

Faz 0 (refaktör + testler) → Faz 1 (Data/TV/Telefon) → Faz 2 (CCTV/Anons) →
Faz 3 (Yangın). Her faz sonunda build + test + uygulamada manuel doğrulama.
