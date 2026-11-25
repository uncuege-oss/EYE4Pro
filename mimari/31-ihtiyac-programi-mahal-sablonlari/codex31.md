# CODEX 31 — İhtiyaç Programı ve Mahal Şablonları

> **Durum:** Başlatıldı (2026-06-12)
> **Bağlı ana takip:** [../codex30.md](../codex30.md)
> **Üst hedef:** EYE4PRO mimari BIM akışında bina işlevi, kapasite, mahal şablonları ve
> ihtiyaç programını üretmek.
> **M50 durumu:** M50 entegrasyonu bu modülde yapılmaz. M31 yalnız `.e4mimari.json` ve
> `Core/Architecture` domain modeli üzerinde çalışır.

---

## 0. Kullanım Protokolü

M31 üzerinde çalışırken sıra:

1. `maincodex.md` oku.
2. `mimari/codex30.md` oku.
3. Bu dosyayı oku.
4. Aktif adımı uygula.
5. Her tamamlanan işlemden sonra hem bu dosyayı hem gerekiyorsa `mimari/codex30.md` dosyasını güncelle.
6. Kullanıcı onayı olmadan bir sonraki adıma geçme.

---

## 1. Amaç

Kullanıcı yeni bir mimari BIM projesine başladığında önce "ne tasarlanıyor?" sorusuna cevap verir.
M31 bu cevaptan hareketle ihtiyaç programını üretir:

- Bina işlevi seçimi.
- Proje tipi ve kapasite bilgileri.
- Mahal şablonlarının bina işlevine göre filtrelenmesi.
- Mahal gereksinimlerinin otomatik üretilmesi.
- Net alan, sirkülasyon alanı, teknik alan ve brüt alan hedeflerinin hesaplanması.
- Kullanıcının mahal adet/alan/hacim değerlerini düzenleyebilmesi.
- `.e4mimari.json` içinde kalıcı saklama.
- İlk rapor çıktısı.

M31 çizim yapmaz. Konsept plan üretimi M32'ye bırakılır.

---

## 2. Mevcut Altyapı

Faz 1'de hazır olan sınıflar:

```text
src/Kst.Acoustics.Core/Architecture/
├── Models/
│   ├── BinaIslevi.cs
│   ├── MahalTipi.cs
│   ├── KullanimSinifi.cs
│   ├── MimariProje.cs
│   ├── IhtiyacProgrami.cs
│   ├── MahalGereksinimi.cs
│   └── MahalSablonu.cs
├── Services/
│   └── MimariProjeServisi.cs
└── Rules/
    ├── KontrolSeviyesi.cs
    ├── MimariKontrolSonucu.cs
    └── MimariProjeDogrulamaServisi.cs
```

Core-only test runner:

```text
src/Kst.Acoustics.Core.Tests/
```

---

## 3. Önerilen Kod Konumu

```text
src/Kst.Acoustics.Core/Architecture/Templates/
├── BinaIsleviTanimlari.cs
├── VarsayilanMahalSablonlari.cs
└── MahalSablonuFiltreServisi.cs

src/Kst.Acoustics.Core/Architecture/Services/
├── IhtiyacProgramiUretimServisi.cs
└── AlanOzetiServisi.cs

src/Kst.Acoustics.App/Architecture/IhtiyacProgrami/
├── ViewModels/
│   └── IhtiyacProgramiViewModel.cs
└── Views/
    └── IhtiyacProgramiWindow.axaml
```

UI fazına gelene kadar Core tarafı bağımsız ilerler.

---

## 4. Veri Kararları

### 4.1 Bina işlevi tanımı

`BinaIslevi` enum tek başına yeterli değildir; UI ve şablon üretimi için insan okunur tanım gerekir:

```text
BinaIsleviTanim
├── Islev
├── Ad
├── Aciklama
├── VarsayilanSirkulasyonOrani
├── VarsayilanTeknikAlanOrani
├── VarsayilanKatYuksekligiM
└── KapasiteEtiketi
```

Örnek kapasite etiketleri: kişi sayısı, daire sayısı, oda sayısı, yatak sayısı, öğrenci sayısı.

### 4.2 Mahal şablonu

Faz 1'deki `MahalSablonu` kullanılacak. M31, varsayılan şablon kütüphanesini doldurur.

İlk sürümde zorunlu minimum şablon aileleri:

- Konut / villa
- Ofis
- Eğitim
- Sağlık
- Konaklama
- Kültür / konferans
- Ticari / restoran
- Endüstri / depo

### 4.3 İhtiyaç programı üretimi

Üretim algoritması ilk sürümde deterministik ve açıklanabilir olacak:

1. Bina işlevine uygun şablonları seç.
2. Kullanıcı kapasitesi varsa kişi başına alanı uygula.
3. Minimum/ideal/hedef alanları hesapla.
4. Zorunlu mahalleri otomatik ekle.
5. Opsiyonel mahalleri işaretli ama pasif olarak sunmaya hazırla.
6. Net alan toplamını hesapla.
7. Sirkülasyon ve teknik alan oranlarını uygula.

---

## 5. M31 Küçük Faz Listesi

### M31.1 — Codex ve klasör açılışı

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.1.1 | `mimari/31-ihtiyac-programi-mahal-sablonlari/` klasörü oluştur | Klasör var | ✅ Tamamlandı |
| 31.1.2 | `codex31.md` oluştur | Bu dosyada amaç, kapsam ve küçük fazlar var | ✅ Tamamlandı |
| 31.1.3 | `mimari/_index.md` bağlantısını güncelle | M31 codex linki çalışır | ✅ Tamamlandı |
| 31.1.4 | `codex30.md` 2.1 durumunu güncelle | 2.1 tamamlandı, 2.2 onay bekliyor | ✅ Tamamlandı |

### M31.2 — Varsayılan bina işlevleri

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.2.1 | `BinaIsleviTanim` modelini ekle | Ad/açıklama/oran/yükseklik/kapasite etiketi alanları var | ✅ Tamamlandı |
| 31.2.2 | `BinaIsleviTanimlari` katalog servisini ekle | Enum değerleri için Türkçe tanımlar döner | ✅ Tamamlandı |
| 31.2.3 | Core test: tüm tanımlar benzersiz | Bina işlevi kodları tekrarsız | ✅ Tamamlandı |
| 31.2.4 | Core test runner'a bağla | `Kst.Acoustics.Core.Tests` geçer | ✅ Tamamlandı |

### M31.3 — Mahal şablon kütüphanesi

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.3.1 | `VarsayilanMahalSablonlari` servisini ekle | İlk sekiz bina işlevi için şablonlar var | ✅ Tamamlandı |
| 31.3.2 | Konut/villa şablonları | Salon, oda, mutfak, banyo, wc, antre, koridor, depo vb. var | ✅ Tamamlandı |
| 31.3.3 | Ofis şablonları | Açık ofis, toplantı, yönetici, resepsiyon, arşiv, server, wc vb. var | ✅ Tamamlandı |
| 31.3.4 | Eğitim/sağlık/kültür/ticari/depo şablonları | Her işlev için temel mahal seti var | ✅ Tamamlandı |
| 31.3.5 | Core test: şablonlar doğrulanır | Negatif alan/yükseklik yok, tipler tanımlı | ✅ Tamamlandı |

### M31.4 — İhtiyaç programı üretici

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.4.1 | `IhtiyacProgramiUretimServisi` ekle | Bina işlevi + kapasiteden program üretir | ✅ Tamamlandı |
| 31.4.2 | Kapasiteye bağlı alan hesabı | `KisiBasinaAlanM2` olan şablonlar kapasiteyle ölçeklenir | ✅ Tamamlandı |
| 31.4.3 | Varsayılan oran aktarımı | Sirkülasyon/teknik alan/kat yüksekliği bina işlevinden gelir | ✅ Tamamlandı |
| 31.4.4 | Core test: ofis programı | Ofis işlevi açık ofis/toplantı/wc/teknik gibi mahaller üretir | ✅ Tamamlandı |
| 31.4.5 | Core test: konferans programı | Konferans salonu/foye/wc/teknik mahaller üretir | ✅ Tamamlandı |

### M31.5 — Alan özeti

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.5.1 | `AlanOzeti` modeli ekle | Net, sirkülasyon, teknik, brüt, hedef fark alanları var | ✅ Tamamlandı |
| 31.5.2 | `AlanOzetiServisi` ekle | `IhtiyacProgrami` üstünden özet çıkarır | ✅ Tamamlandı |
| 31.5.3 | Kullanım sınıfı kırılımı | Ana kullanım, sirkülasyon, teknik vb. gruplanır | ✅ Tamamlandı |
| 31.5.4 | Core test: alan toplamları | Net/brüt hesabı beklenen değerleri verir | ✅ Tamamlandı |

### M31.6 — UI iskeleti

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.6.1 | App klasör iskeleti | `App/Architecture/IhtiyacProgrami` oluşur | ✅ Tamamlandı |
| 31.6.2 | ViewModel iskeleti | Bina işlevi, kapasite, mahal listesi ve alan özeti property'leri var | ✅ Tamamlandı |
| 31.6.3 | Pencere iskeleti | Türkçe etiketli basit Avalonia pencere açılır | ✅ Tamamlandı |
| 31.6.4 | Ana menü/giriş bağlantısı | Mimari başlık altında M31 açılır | ✅ Tamamlandı |
| 31.6.5 | UI build | `dotnet build` App tarafındaki mevcut hatalar giderildiyse geçer; değilse durum not edilir | ✅ Tamamlandı |

### M31.7 — Düzenleme ve kaydet/aç

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.7.1 | Mahal satırı düzenleme | Adet/alan/yükseklik/kapasite değişince özet güncellenir | ✅ Tamamlandı |
| 31.7.2 | Yeni/kopyala/sil mahal | Kullanıcı programı elle revize edebilir | ✅ Tamamlandı |
| 31.7.3 | `.e4mimari.json` kaydet/aç UI | `MimariProjeServisi` UI'dan kullanılır | ✅ Tamamlandı |
| 31.7.4 | Doğrulama paneli | `MimariProjeDogrulamaServisi` sonuçları gösterilir | ✅ Tamamlandı |

### M31.8 — İlk rapor

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.8.1 | HTML rapor servisi | Proje bilgisi, mahal listesi ve alan özeti HTML çıkar | ✅ Tamamlandı |
| 31.8.2 | CSV dışa aktarım | Mahal listesi CSV olarak kaydedilir | ✅ Tamamlandı |
| 31.8.3 | Core test: rapor boş değil | HTML/CSV temel içerikleri üretir | ✅ Tamamlandı |

### M31.9 — Regresyon ve kapanış

| Adım | İş | Bitti Sayılır | Durum |
|------|----|---------------|-------|
| 31.9.1 | Core test runner | `Kst.Acoustics.Core.Tests` geçer | ✅ Tamamlandı |
| 31.9.2 | Ana codex güncellemesi | `codex30.md`, `codex31.md`, `_index.md` tutarlı | ✅ Tamamlandı |
| 31.9.3 | M31 MVP kapanış | Sonraki faza geçiş için kullanıcı onayı beklenir | ✅ Tamamlandı |

---

## 6. Aktif Durum

| Alan | Değer |
|------|-------|
| Aktif Adım | M31 MVP tamamlandı; sonraki faz için kullanıcı onayı bekleniyor |
| Sonraki Adım | Ana plan sırasına göre Faz 3.1 yönetmelik/kural altyapısı açılışı veya kullanıcı tercihiyle M32 konsept plan geçişi |
| Kod Durumu | M31 Core servisleri, UI iskeleti, canlı mahal düzenleme, `.e4mimari.json` kaydet/aç, doğrulama paneli, HTML rapor ve CSV dışa aktarım eklendi |
| Test Durumu | `Kst.Acoustics.Core.Tests` 16/16 geçti; Core test build 0 uyarı, 0 hata; App build 0 hata ve mevcut Electric XAML dosyalarından 4 uyarı verdi; App `--no-incremental` build 0 hata ve mevcut 5 uyarı verdi |

---

## 7. Karar Günlüğü

| Tarih | Karar |
|-------|-------|
| 2026-06-12 | M31 ilk programlanacak mimari BIM modülü olarak açıldı |
| 2026-06-12 | M31 çizim/yerleşim yapmayacak; konsept plan M32'ye bırakıldı |
| 2026-06-12 | M31 ilk etapta Core ağırlıklı ilerleyecek; UI iskeleti şablon ve üretim servisleri hazırlandıktan sonra yapılacak |
| 2026-06-12 | M31.1 açılış adımları tamamlandı; sıradaki iş M31.2 varsayılan bina işlevleri |
| 2026-06-12 | M31.2 tamamlandı: varsayılan bina işlevi tanım kataloğu eklendi; enum kapsama/benzersizlik/geçerlilik testleri geçti |
| 2026-06-12 | M31.3 tamamlandı: varsayılan mahal şablon kataloğu eklendi; kod benzersizliği, zorunlu işlev aileleri ve değer geçerliliği testleri geçti |
| 2026-06-12 | M31.4 tamamlandı: `IhtiyacProgramiUretimServisi` eklendi; ofis ve konferans ihtiyaç programı testleriyle Core test runner 13/13 geçti |
| 2026-06-12 | M31.5 tamamlandı: `AlanOzeti` ve `AlanOzetiServisi` eklendi; net/brüt alan, hedef farkı ve kullanım sınıfı kırılımı testleriyle Core test runner 15/15 geçti |
| 2026-06-12 | M31.6 tamamlandı: M31 ViewModel/pencere iskeleti ve `ArchitectureLauncherWindow` eklendi; ana ekrandaki mimari kartı M31'e bağlandı; App build 0 hata verdi |
| 2026-06-12 | M31.7.1-31.7.2 tamamlandı: mahal satırında ad/adet/alan/yükseklik düzenleme, yeni/kopyala/sil ve canlı alan özeti yenileme eklendi; kaydet/aç 2.8'e bırakıldı |
| 2026-06-12 | M31.7.3-31.7.4 tamamlandı: `.e4mimari.json` aç/kaydet/farklı kaydet UI akışı ve `MimariProjeDogrulamaServisi` tabanlı ön kontrol paneli eklendi |
| 2026-06-12 | M31.8 tamamlandı: `IhtiyacProgramiRaporServisi` ile HTML ihtiyaç programı raporu ve CSV mahal listesi üretildi; M31 penceresine rapor/dışa aktarım düğmeleri eklendi |
| 2026-06-12 | M31.9 tamamlandı: Core test/build, App build, Türkçe UI komut metni ve takip dosyası tutarlılığı kontrol edildi; M31 MVP kapandı |

## 5 Kasım 2026 — sıralı modül denetimi

İhtiyaç programı, mahal ve şablon alan/yükseklik/oranlarında sonlu olmayan değerler geçerli sayılmıyor (A17).

Ayrıntılı hata listesi ve doğrulama: depo kökündeki `modul-denetimi-2026-11-05.md`.
