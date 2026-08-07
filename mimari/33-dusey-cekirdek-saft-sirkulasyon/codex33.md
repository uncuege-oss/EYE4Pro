# CODEX 33 — Düşey Çekirdek, Şaft ve Sirkülasyon

> **Durum:** MVP tamamlandı (2026-06-13)
> **Ana takip:** [../codex30.md](../codex30.md)
> **Önceki modüller:** M31 ihtiyaç programı, M32 konsept plan, M34 yönetmelik ön kontrol MVP tamamlandı.
> **Amaç:** M32 plan taslağına merdiven, yangın merdiveni, asansör, rampa, şaft,
> çekirdek grubu ve kaçış/sirkülasyon ön altyapısını eklemek.
> **M50 kararı:** M50 entegrasyonu bu modülde arkaya alınır. İlk MVP `.e4mimari.json`
> ve `PlanTaslagi` üzerinde çalışır; M50 adapteri Faz 9/M39 tarafında ele alınır.

---

## 0. Kullanım Protokolü

1. Yeni oturumda önce `maincodex.md`, sonra `mimari/codex30.md`, sonra bu dosya okunur.
2. Aktif adım bu dosyadaki küçük faz tablosundan ve `codex30.md` Faz 5 tablosundan bulunur.
3. Kod yazmadan önce ilgili Core/App klasörleri incelenir.
4. Her işlemden sonra hem bu dosya hem `mimari/codex30.md` güncellenir.
5. Bir adım tamamlandığında durulur ve kullanıcıdan sonraki adıma geçiş onayı istenir.

---

## 1. Hedef

Bu modül, mimari taslak planın sadece mahal bloklarından oluşmasını aşarak binanın düşey
organizasyonunu temsil eder. İlk hedef, ruhsat veya kesin uygunluk iddiası taşıyan tam bir
yangın/kaçış hesabı değil; tasarım sırasında mimarı erkenden uyaran, katlar arası sürekliliği
koruyan ve ileride 3D BIM modele taşınabilecek kararlı bir çekirdek verisi üretmektir.

M33 tamamlandığında kullanıcı şunları yapabilmelidir:

- Plana merdiven, yangın merdiveni, asansör, rampa ve şaft taslakları ekleyebilmek.
- Bu elemanları çekirdek grubu olarak aynı aks/zone altında toplayabilmek.
- Katlar arasında aynı aksta devam etmeyen çekirdek ve şaftları görebilmek.
- WC/banyo/ıslak hacimlerin şaft ilişkisini ön kontrol olarak değerlendirebilmek.
- Mahal, koridor ve merdiven bağlantılarından ilk kaçış grafını oluşturabilmek.
- M32 plan ekranında çekirdekleri ayrı katman/renk ile görebilmek.

---

## 2. Kapsam

### MVP Kapsamında

- `CekirdekTipi` enum'u: Merdiven, Yangın merdiveni, asansör, rampa, şaft, çekirdek grubu.
- `CekirdekElemani`, `CekirdekGrubu`, `SaftTaslagi` veri modelleri.
- Kat aralığı, aks kodu, konum, boyut, net genişlik ve kapasite alanları.
- M32 `PlanTaslagi` ile ilişki: kat, mahal bloğu ve koridor bağlantıları.
- M34 ön kontrol entegrasyonu: süreklilik, bağlantısız çekirdek, ıslak hacim/şaft yakınlığı.
- Kaçış grafı v1: mahal, koridor ve merdiven düğümleri ile basit kenarlar.
- `.e4mimari.json` içinde çekirdek/şaft bilgisinin korunması.
- M32 konsept plan canvas'ında çekirdeklerin görünmesi.

### MVP Dışında

- Kesin yangın kaçış mesafesi ve ruhsat uygunluğu hesabı.
- Tam IFC/M50 nesne eşlemesi.
- Detaylı merdiven basamak/rıht/geometri üretimi.
- Asansör makine dairesi, kuyu detayı ve marka/katalog hesabı.
- Tam 3D yapı elemanı üretimi; bu M35'e bırakılır.

---

## 3. Veri Modeli Taslağı

```text
PlanTaslagi
├── KatTaslagi
│   ├── MahalBlogu
│   ├── KoridorTaslagi
│   ├── CekirdekElemani
│   ├── CekirdekGrubu
│   └── SaftTaslagi
└── KacisGrafi
```

### CekirdekElemani

Önerilen alanlar:

- `Id`, `Ad`, `Tip`
- `KatNo`, `BaslangicKatNo`, `BitisKatNo`
- `X`, `Y`, `GenislikM`, `DerinlikM`
- `NetGenislikM`, `KapasiteKisi`
- `AksKodu`, `CekirdekGrubuId`
- `BagliKoridorIdleri`, `BagliMahalIdleri`
- `Kilitli`, `Notlar`

### CekirdekGrubu

Önerilen alanlar:

- `Id`, `Ad`, `AksKodu`
- `BaslangicKatNo`, `BitisKatNo`
- `MerkezX`, `MerkezY`
- `ElemanIdleri`
- `Notlar`

### SaftTaslagi

Önerilen alanlar:

- `Id`, `Ad`, `TesisatTuru`
- `BaslangicKatNo`, `BitisKatNo`
- `X`, `Y`, `GenislikM`, `DerinlikM`
- `AksKodu`
- `BagliMahalIdleri`
- `Notlar`

### KacisGrafi

Önerilen alanlar:

- `Dugumler`: Mahal, koridor, merdiven, kapı/bağlantı placeholder.
- `Kenarlar`: İki düğüm arası bağlantı, mesafe ve kat bilgisi.
- `UyariKodlari`: Bağlantısız düğüm, merdivene ulaşamayan mahal, koridorsuz kaçış aksı.

---

## 4. Kod Konumu

Planlanan Core yerleşimi:

```text
src/Kst.Acoustics.Core/Architecture/Circulation/
├── CekirdekTipi.cs
├── CekirdekElemani.cs
├── CekirdekGrubu.cs
├── SaftTaslagi.cs
├── KacisGrafi.cs
├── CekirdekYerlesimServisi.cs
└── SirkulasyonKontrolServisi.cs
```

Planlanan UI bağlantısı:

```text
src/Kst.Acoustics.App/Architecture/KonseptPlan/
├── ViewModels/KonseptPlanViewModel.cs
└── Views/KonseptPlanCanvas.cs
```

---

## 5. M32 ve M34 Bağlantısı

M32, `PlanTaslagi`, `KatTaslagi`, `MahalBlogu` ve `KoridorTaslagi` üretir. M33 bu taslağın üstüne
düşey çekirdek ve şaft verisini ekler. M34 ise bu veriyi ön kontrol olarak değerlendirir.

İlk M34 uyarıları:

- Çok katlı planda düşey çekirdek yok.
- Merdiven veya şaft katlar arasında aynı aks koduyla devam etmiyor.
- Koridor hiçbir merdivene bağlanmıyor.
- Islak hacimlerin yakınında şaft yok.
- Kaçış grafında merdivene ulaşamayan mahal var.

Uyarı dili daima ön kontrol niteliğinde kalır; "kesin uygundur" veya ruhsat garantisi verilmez.

---

## 6. Küçük Faz Listesi

### M33.1 — Codex Açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.1.1 | Klasör oluştur | `mimari/33-dusey-cekirdek-saft-sirkulasyon/` var | ✅ Tamamlandı |
| 33.1.2 | `codex33.md` oluştur | Kapsam, model taslağı ve küçük faz listesi yazıldı | ✅ Tamamlandı |
| 33.1.3 | Ana takipleri güncelle | `maincodex.md`, `mimari/_index.md`, `codex30.md` M33 durumunu gösterir | ✅ Tamamlandı |

### M33.2 — Çekirdek Modelleri

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.2.1 | `Architecture/Circulation` klasörü | Core altında sirkülasyon klasörü açılır | ✅ Tamamlandı |
| 33.2.2 | Çekirdek enum/model | `CekirdekTipi`, `CekirdekElemani` eklenir | ✅ Tamamlandı |
| 33.2.3 | Grup ve şaft modelleri | `CekirdekGrubu`, `SaftTaslagi` eklenir | ✅ Tamamlandı |
| 33.2.4 | Plan/proje bağlantısı | Modeller `.e4mimari.json` içinde korunur | ✅ Tamamlandı |
| 33.2.5 | Model testleri | Varsayılan değerler ve serileştirme test edilir | ✅ Tamamlandı |

### M33.3 — Katlar Arası Süreklilik

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.3.1 | Aks eşleştirme | Aynı aks kodundaki elemanlar kat aralığına göre gruplanır | ✅ Tamamlandı |
| 33.3.2 | Süreklilik servisi | Eksik kat, aks kayması ve kopuk çekirdek uyarısı üretir | ✅ Tamamlandı |
| 33.3.3 | Test | Aynı aksta devam etmeyen eleman için uyarı testi geçer | ✅ Tamamlandı |

### M33.4 — Islak Hacim ve Şaft İlişkisi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.4.1 | Islak mahal tespiti | WC, banyo, mutfak vb. mahal tipleri sınıflanır | ✅ Tamamlandı |
| 33.4.2 | Yakınlık kontrolü | Şaft mesafe/tolerans parametreleriyle ön kontrol yapılır | ✅ Tamamlandı |
| 33.4.3 | Test | Şaftsız ıslak hacim uyarısı testi geçer | ✅ Tamamlandı |

### M33.5 — Kaçış Grafı v1

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.5.1 | Graf modelleri | Düğüm ve kenar modelleri eklenir | ✅ Tamamlandı |
| 33.5.2 | Plan graf üretimi | Mahal-koridor-merdiven bağlantıları üretilir | ✅ Tamamlandı |
| 33.5.3 | Bağlantı uyarısı | Merdivene ulaşamayan mahal uyarısı üretilir | ✅ Tamamlandı |

### M33.6 — UI Gösterimi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 33.6.1 | Canvas katmanı | Çekirdekler M32 plan canvas'ında ayrı renkte görünür | ✅ Tamamlandı |
| 33.6.2 | Sağ panel özeti | Seçili çekirdek/şaft bilgileri listelenir | ✅ Tamamlandı |
| 33.6.3 | Uyarı seçimi | M34 uyarısı tıklanınca ilgili çekirdek/şaft seçilir | ✅ Tamamlandı |
| 33.6.4 | App build | UI derlemesi hatasız geçer | ✅ Tamamlandı |

---

## 7. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M33 MVP tamamlandı; M35 başlatıldı |
| Sonraki Adım | M35 takibi için `mimari/35-3d-bim-model/codex35.md` kullanılacak |
| Kod Durumu | M33 çekirdek/şaft/sirkülasyon MVP'si tamamlandı; M50 entegrasyonu arkaya alınmış durumda |
| Test Durumu | Core test runner 35/35 geçti; Core test build 0 uyarı/0 hata; App build 0 hata, mevcut farklı modüllerden 6 uyarı |

---

## 8. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-13 | M33 başlatıldı; ilk MVP'nin M50'ye bağlanmadan `.e4mimari.json` ve `PlanTaslagi` üzerinde ilerlemesine karar verildi |
| 2026-06-13 | Modül kapsamı merdiven, yangın merdiveni, asansör, rampa, şaft, çekirdek grubu ve kaçış grafı v1 olarak belirlendi |
| 2026-06-13 | Uyarı dili M34 çizgisine uygun şekilde ön kontrol olarak kalacak; kesin uygunluk veya ruhsat garantisi verilmeyecek |
| 2026-06-13 | Faz 5.2 tamamlandı: `CekirdekTipi`, `SaftTesisatTuru`, `CekirdekElemani`, `CekirdekGrubu`, `SaftTaslagi` eklendi; plan/kat taslağı ve `.e4mimari.json` kayıt akışı çekirdek/şaft verisini koruyor |
| 2026-06-13 | Faz 5.3 tamamlandı: `SirkulasyonKontrolServisi` aks kodu, kat aralığı, kopuk süreklilik ve aks kayması uyarıları üretiyor; M34 plan kontrolü bu uyarıları kaynak profilli sonuçlara taşıyor |
| 2026-06-13 | Faz 5.4 tamamlandı: WC, banyo, mutfak ve benzeri ıslak hacim blokları için aynı kattaki şaft yokluğu ve şaft uzaklığı ön kontrol uyarıları eklendi; M34 plan kontrolüne bağlandı |
| 2026-06-13 | Faz 5.5 tamamlandı: `KacisGrafi`, `KacisDugumu`, `KacisKenari` modelleri ve mahal-koridor-merdiven graf üretimi eklendi; merdivene ulaşamayan mahal M34 uyarısı olarak raporlanıyor |
| 2026-06-13 | Faz 5.6 tamamlandı: M32 konsept plan canvas'ında çekirdek/şaft katmanı, sağ panel özeti ve M34 uyarısından çekirdek/şaft seçimi eklendi; M33 MVP kapandı |
| 2026-06-13 | M35 açıldı; sonraki 3D BIM model geliştirme takibi `codex35.md` üzerinden yürütülecek |
