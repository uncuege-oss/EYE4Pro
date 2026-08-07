# CODEX 37 — Mimari Pafta, Rapor ve Çizelgeler

> **Durum:** MVP tamamlandı (2026-06-13)
> **Ana takip:** [../codex30.md](../codex30.md)
> **Önceki modüller:** M31 ihtiyaç programı, M32 konsept plan, M33 düşey çekirdek/şaft,
> M34 yönetmelik ön kontrol, M35 3D BIM model ve M36 detay/malzeme MVP tamamlandı.
> **Amaç:** M31–M36 boyunca üretilen mimari kararları (mahal programı, plan, 3D model, malzeme/atama,
> disiplin verisi, ön kontrol uyarıları) okunabilir tablo, çizelge ve raporlara dökmek.
> **M50 kararı:** M37 de M50'ye bağlanmadan ilerler; çıktılar `.e4mimari.json` ve mevcut domain
> modeli üzerinden üretilir. M50/IFC koordinasyonu M39/Faz 9-10 tarafındadır.

---

## 0. Kullanım Protokolü

1. Yeni oturumda önce `maincodex.md`, sonra `mimari/codex30.md`, sonra bu dosya okunur.
2. Aktif adım bu dosyadaki küçük faz tablosundan ve `codex30.md` Faz 8 tablosundan bulunur.
3. Kod yazmadan önce ilgili Core/App klasörleri incelenir.
4. Her işlemden sonra hem bu dosya hem `mimari/codex30.md` güncellenir.
5. Bir adım tamamlandığında durulur ve kullanıcıdan sonraki adıma geçiş onayı istenir.

---

## 1. Hedef

M37, mimari modeli "çıktı alınabilir" hale getirir. Hedef tam pafta/antet düzeni veya CAD ölçekli
çizim üretimi değildir; hedef, mimarın ve diğer disiplinlerin kullanabileceği güvenilir **tablo ve
çizelgeleri** tek merkezde üretmektir.

M37 tamamlandığında kullanıcı şunları alabilmelidir:

- Kat / mahal no / ad / alan / hacim / işlev içeren **mahal listesi**.
- Net/brüt, sirkülasyon ve teknik alan kırılımını içeren **alan tablosu**.
- Taslak açıklık verisinden **kapı-pencere çizelgesi v1**.
- Mahal bazında zemin/duvar/tavan **finisaj çizelgesi**.
- Kaynak ve kural koduyla etiketli **ön kontrol raporu**.
- En az iki dosya formatında (HTML + CSV; XLSX stretch) dışa aktarım.

---

## 2. Kapsam

### MVP Kapsamında

- `Architecture/Reports` altında rapor/çizelge domain modelleri ve üretici servisler.
- Mahal listesi tablosu (3D mahaller + ihtiyaç programı işlev/kullanım sınıfı).
- Alan tablosu (`AlanOzetiServisi` çıktısının raporlanabilir hali).
- Kapı-pencere çizelgesi v1 (`Model3D` açıklıkları + atanan tip/malzeme).
- Finisaj çizelgesi (mahal finisaj önerisi / atama → zemin/duvar/tavan).
- Ön kontrol raporu (`YonetmelikOnKontrolServisi` + M36 finisaj uyarıları, kaynak/kural koduyla).
- Disiplin özeti raporu (`DisiplinVeriOzetiServisi` çıktısının tablolaştırılması).
- HTML ve CSV üreticiler; ortak bir rapor model → format katmanı.
- Boş/eksik veride güvenli çalışan üreticiler.

### MVP Dışında

- Ölçekli mimari pafta ve antet/çizim düzeni.
- 2D plan/kesit/görünüş çizimi.
- PDF üretimi (ileride değerlendirilir).
- Tam XLSX biçim/şablon (varsa basit XLSX stretch hedefidir).
- IFC/BIM ihracat (M39).
- M50 ortak modeline yazma.

---

## 3. Veri Modeli / Çıktı Taslağı

```text
RaporModeli (tek üretim için)
├── MahalListesiTablosu  -> satır: Kat, No, Ad, MahalTipi, KullanimSinifi, AlanM2, HacimM3
├── AlanTablosu          -> net/brüt/sirkülasyon/teknik + kullanım sınıfı kırılımı
├── KapiPencereCizelgesi -> satır: Kat, Tip(Kapı/Pencere), Ölçü, Adet, YapiElemaniTipi, Malzeme
├── FinisajCizelgesi     -> satır: Kat, Mahal, Zemin, Duvar, Tavan, Not
├── OnKontrolRaporu      -> satır: Seviye, KuralKodu, Kaynak, Mesaj, EtkilenenEleman
└── DisiplinOzetiTablosu -> akustik/enerji/elektrik/metraj okunabilir kırılımları
```

Üretim girdisi `MimariProje`'dir. Servisler önce yapısal **rapor modeli** üretir, sonra biçim
katmanı (HTML/CSV) bu modeli render eder. Böylece yeni format eklemek model üretimini bozmaz.

---

## 4. Kod Konumu

Planlanan Core yerleşimi:

```text
src/Kst.Acoustics.Core/Architecture/Reports/
├── IhtiyacProgramiRaporServisi.cs   (mevcut — M31)
├── MimariRaporModeli.cs             (tablo/satır modelleri)
├── MimariRaporUretimServisi.cs      (proje -> rapor modeli)
├── MimariRaporHtmlBuilder.cs        (model -> HTML)
└── MimariRaporCsvBuilder.cs         (model -> CSV)
```

Testler mevcut Core test runner içine eklenir: `src/Kst.Acoustics.Core.Tests/Program.cs`.
UI tarafı (rapor önizleme/dışa aktarım butonları) gerekiyorsa M37 sonunda değerlendirilir; çekirdek
hedef Core üreticilerdir.

---

## 5. M31–M36 Bağlantısı

- M31 → ihtiyaç programı, mahal tipi/kullanım sınıfı, alan/hacim hedefleri ve `AlanOzeti`.
- M32/M33 → plan taslağı, kat/blok, çekirdek/şaft (mahal listesi kat bilgisi).
- M35 → 3D mahaller, açıklıklar, döşeme/tavan/duvar (alan/hacim ve kapı-pencere ölçüleri).
- M36 → malzeme, yapı elemanı tipi, atama, finisaj profili ve `DisiplinVeriOzeti`.
- M34 → ön kontrol uyarıları (kaynak/kural kodlu rapor satırları).

M37 yalnız okur ve raporlar; mevcut Id'leri ve geometriyi değiştirmez.

---

## 6. Dil ve Standart Yaklaşımı

M37, M34 "ön kontrol" dilini korur: raporlarda kesin uygunluk beyanı yer almaz. Ön kontrol raporu
uyarıları kaynak profili (PAIY, BYKHY, OTOPARK, TBDY, BEPY, erişilebilirlik, M34-VERI) ve kural
koduyla etiketler. Tüm görünür metin Türkçe ve doğru Türkçe karakterlerle yazılır.

---

## 7. Küçük Faz Listesi

### M37.1 — Codex Açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.1.1 | Klasör oluştur | `mimari/37-pafta-rapor-cizelgeler/` var | ✅ Tamamlandı |
| 37.1.2 | `codex37.md` oluştur | Kapsam, çıktı taslağı ve küçük faz listesi yazıldı | ✅ Tamamlandı |
| 37.1.3 | Ana takipleri güncelle | `maincodex.md`, `mimari/_index.md`, `codex30.md` M37 durumunu gösterir | ✅ Tamamlandı |

### M37.2 — Mahal Listesi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.2.1 | Rapor modeli | Mahal listesi tablo/satır modeli eklenir | ✅ Tamamlandı |
| 37.2.2 | Üretim | Kat, no, ad, tip, kullanım sınıfı, alan, hacim satırları üretilir | ✅ Tamamlandı |
| 37.2.3 | Test | Boş/dolu projede mahal listesi tutarlı üretilir | ✅ Tamamlandı |

### M37.3 — Alan Tablosu

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.3.1 | Alan tablosu | Net/brüt/sirkülasyon/teknik ve kullanım sınıfı kırılımı tablolaşır | ✅ Tamamlandı |
| 37.3.2 | Test | `AlanOzeti` ile tutarlı tablo üretilir | ✅ Tamamlandı |

### M37.4 — Kapı-Pencere Çizelgesi v1

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.4.1 | Çizelge | Açıklık tipi, ölçü, adet, yapı elemanı tipi ve malzeme satırları çıkar | ✅ Tamamlandı |
| 37.4.2 | Test | Açıklık + atama verisinden çizelge tutarlı üretilir | ✅ Tamamlandı |

### M37.5 — Finisaj Çizelgesi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.5.1 | Çizelge | Mahal bazında zemin/duvar/tavan finisajı tablolaşır | ✅ Tamamlandı |
| 37.5.2 | Test | Finisaj öneri/atama verisinden çizelge üretilir | ✅ Tamamlandı |

### M37.6 — Ön Kontrol Raporu

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.6.1 | Rapor | M34 + M36 uyarıları seviye/kaynak/kural koduyla raporlanır | ✅ Tamamlandı |
| 37.6.2 | Test | Sorunlu projede uyarı satırları kaynak koduyla çıkar | ✅ Tamamlandı |

### M37.7 — Çıktı Formatları

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 37.7.1 | HTML builder | Tüm tablolar tek HTML raporda render edilir | ✅ Tamamlandı |
| 37.7.2 | CSV builder | Her tablo CSV olarak dışa aktarılır | ✅ Tamamlandı |
| 37.7.3 | Regresyon | En az iki format çalışır; Core/test build temiz | ✅ Tamamlandı |
| 37.7.4 | XLSX (stretch) | Bağımlılıksız OOXML; her tablo bir sayfa | ✅ Tamamlandı |

---

## 8. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M37 MVP tamamlandı — Faz 9/10 (M50 entegrasyonu / BIM ihracat) için onay bekleniyor |
| Sonraki Adım | Mimari başlıkta açık MVP modülleri bitti; sıradaki yön kullanıcı kararına bağlı (M50 ertelendi) |
| Kod Durumu | M37 MVP kapandı: mahal listesi, alan tablosu, kapı-pencere ve finisaj çizelgeleri, ön kontrol raporu + tümünü toplayan `TumRaporuOlustur` ve `MimariRaporHtmlBuilder`/`MimariRaporCsvBuilder` hazır |
| Test Durumu | Core test runner 58/58 geçti; Core build 0 uyarı/0 hata; tam çözüm build 0 hata |
| Çıktı Formatları | HTML + CSV + XLSX (üçü de çalışır; XLSX bağımlılıksız OOXML) |

---

## 9. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-13 | M37 başlatıldı; M50'ye bağlanmadan `.e4mimari.json` ve mevcut domain modeli üzerinden çıktı üretimine karar verildi |
| 2026-06-13 | M37 kapsamı mahal listesi, alan tablosu, kapı-pencere çizelgesi v1, finisaj çizelgesi, ön kontrol raporu ve disiplin özeti tablosu olarak belirlendi |
| 2026-06-13 | Rapor üretimi iki katmanlı tasarlanacak: yapısal rapor modeli → biçim (HTML/CSV) builder'ları; XLSX stretch hedefi |
| 2026-06-13 | Faz 8.1 tamamlandı: M37 klasörü ve `codex37.md` açıldı; ana takip bağlantıları güncellendi; sıradaki adım 37.2 mahal listesi |
| 2026-06-13 | Faz 8.2 tamamlandı: `MimariRaporModeli` (`MahalListesiSatiri`/`MahalListesiTablosu`) ve `MimariRaporUretimServisi.MahalListesiOlustur` eklendi; 3D model mahallerinden gerçek alan/hacim + kat ve plan bloğundan kullanım sınıfı, model yoksa ihtiyaç programından hedef alan/hacim üretiliyor; null/boş güvenli. Core test runner 52/52 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.3 tamamlandı: `AlanTablosu` + `AlanKirilimSatiri` modelleri ve `AlanTablosuOlustur` eklendi; `AlanOzetiServisi` çıktısı (net/brüt/sirkülasyon/teknik, hedef farkı, kullanım sınıfı kırılımı) raporlanabilir tabloya çevriliyor, null/boş güvenli. Core test runner 53/53 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.4 tamamlandı: `KapiPencereSatiri`/`KapiPencereCizelgesi` ve `KapiPencereCizelgesiOlustur` eklendi; 3D açıklıklar tip+ölçü+yapı elemanı tipine göre gruplanıp pozisyon (K1/P1...), adet, ölçü (cm "G/Y"), tip ve malzeme adlarıyla üretiliyor; atanmamış açıklık "Atanmadı" görünüyor. Ortak `TipSozlugu`/`MalzemeSozlugu` yardımcıları eklendi. Core test runner 54/54 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.5 tamamlandı: `FinisajCizelgesiSatiri`/`FinisajCizelgesi` ve `FinisajCizelgesiOlustur` eklendi; mahal listesi satırları M36 finisaj önerisiyle (`MahalFinisajOneriServisi.Oner`) eşleştirilip her mahal için zemin/duvar/tavan tip adı, ıslak hacim bayrağı ve uyarı notu üretiliyor. Core test runner 55/55 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.6 tamamlandı: `OnKontrolSatiri`/`OnKontrolRaporu` ve `OnKontrolRaporuOlustur` eklendi; `IlkVeriKontrolleriniCalistir` (M34 veri + plan + M36 finisaj) çıktısı seviye/kaynak/kural kodu, mesaj, çözüm önerisi ve etkilenen eleman satırlarına çevriliyor; seviye sayıları ve kritik bayrağı üretiliyor. Core test runner 56/56 geçti, Core build 0 uyarı/0 hata |
| 2026-06-13 | Faz 8.7 tamamlandı, M37 MVP kapandı: `MimariRaporu` + `TumRaporuOlustur`, paylaşılan `MimariRaporMetin` (görünüm/format/CSV yardımcıları), `MimariRaporHtmlBuilder` (tek belge, tüm bölümler, ön kontrol dili) ve `MimariRaporCsvBuilder` (tablo başına CSV) eklendi. Boş projede de güvenli. Core test runner 57/57 geçti, Core build 0 uyarı/0 hata, tam çözüm build 0 hata |
| 2026-06-13 | XLSX stretch (37.7.4) tamamlandı: `XlsxYazici` (bağımlılıksız, System.IO.Compression ile minimal OOXML — Content_Types/rels/workbook/worksheet, sayısal+inlineStr hücreler) ve `MimariRaporXlsxBuilder` (her tablo bir sayfa) eklendi; ZipArchive ile yapısal doğrulama testi geçti. Üç format (HTML+CSV+XLSX) çalışıyor. Core test runner 58/58 geçti |
