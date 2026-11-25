# M21 Codex — Kanal Hesabı

> **5 Kasım 2026 kod denetimi:** Mekanik başlatıcı mevcut; `Core/Mechanical`, M21 hesap motoru, proje modeli ve `.e4p21` kayıt akışı kaynak kodda bulunmuyor. Aşağıdaki aktif kapsam maddeleri hedef kapsamdır; uygulanmış özellik olarak değerlendirilmemelidir. Ayrıntı: [modül denetimi](../../modul-denetimi-2026-11-05.md).

Bu dosya yeni oturum başlangıcında hızlı referans için tutulur. Ayrıntılı kronolojik günlük değildir; yalnızca güncel kararlar, kapsam, sistem davranışları, kritik teknik notlar ve önemli kaynak alanları yazılır.

---

## 1. Kimlik

| Alan | Değer |
|------|-------|
| Program | EYE4PRO Mechanics |
| Modül | M21 — Kanal Hesabı |
| Amaç | HVAC hava kanalı boyutlandırma, basınç kaybı ve hat özeti |
| Proje uzantısı | `.e4p21` |
| Standart referansları | ASHRAE Fundamentals, SMACNA HVAC Systems Duct Design, TS EN 16798, TS EN 1505, TS EN 1506 |
| Stack | C# + Avalonia (`net10.0`) |
| UI dili | Türkçe |
| Durum | Başlatıldı; ürün ailesi ve modül merkezi aktif |

---

## 2. Modül Kapsamı

Aktif kapsam:
- Dikdörtgen ve dairesel kanal kesiti hesabı
- Debi, hız, alan ve eşdeğer çap hesapları
- Hat bazlı kanal segment listesi
- Sürtünme basınç kaybı ve yerel kayıp katsayısı girişi
- Toplam basınç kaybı özeti
- `.e4p21` proje kaydet/aç akışı
- DXF veya tablo çıktısı için veri modeli hazırlığı

İleri/eksik alanlar:
- Fan çalışma noktası ve cihaz seçimi
- Kanal fitting kataloğu
- Kat planı üstünden kanal rotası
- Otomatik metraj ve keşif bağlantısı
- Rapor/PDF çıktı

---

## 3. Genel Model Kuralları

Temel modeller:
- `MekanikKanalProje`
- `KanalHatti`
- `KanalSegmenti`
- `KanalKesiti`
- `KanalBasincKaybiSonucu`

Kurallar:
- İş mantığı `Kst.Acoustics.Core/Mechanical/` altında tutulur.
- UI katmanı yalnızca ViewModel ve görünüm davranışını içerir; hesap motoru Core içinde kalır.
- Tüm görünür metinler Türkçe olmalıdır.
- Debi birimi varsayılan olarak `m3/h`, hesap içi SI karşılığı `m3/s` olmalıdır.
- Uzunluk, genişlik, yükseklik ve çap metrik birimlerde tutulmalıdır.
- Negatif veya sıfır fiziksel değerler validasyon hatası üretmelidir.

---

## 4. Hesap Kuralları

- Kesit alanı dikdörtgen kanalda `genişlik x yükseklik`, dairesel kanalda `pi x çap^2 / 4` ile hesaplanır.
- Hız `debi / alan` üzerinden hesaplanır.
- Eşdeğer çap hesap yöntemi açıkça isimlendirilmelidir.
- Basınç kaybı segment bazında hesaplanmalı, hat toplamı segment toplamlarından türetilmelidir.
- Yerel kayıplar ayrı `K` katsayısı veya katalog öğesi olarak izlenmelidir.
- Büyük hat listelerinde segment aramaları `Dictionary` tabanlı yapılmalıdır.

---

## 5. UI İş Akışı

- Complete Engineering ana ekranından EYE4PRO Mechanics açılır.
- M21 Kanal Hesabı kartı ilk aktif mekanik modül olarak gösterilir.
- İlk uygulama ekranında proje bilgisi, hat listesi, segment tablosu ve sonuç özeti bulunmalıdır.
- Kullanıcı debi ve kesit bilgilerini düzenlediğinde sonuçlar yenilenmelidir.
- Hata ve uyarılar Türkçe, ölçü birimleri açık ve tutarlı gösterilmelidir.

---

## 6. Kritik Dikkat Notları

- Mekanik modüller elektrik M12 içindeki “Mekanik Paftası” ile karıştırılmamalıdır; M12 yalnız elektrik besleme/linye tarafıdır.
- Kanal hesabı HVAC hava tarafı modülüdür.
- İlk sürümde sade ve doğrulanabilir hesap motoru önceliklidir; çizim ve otomatik rota sonraki fazdır.
- AXAML içinde `&` karakteri `&amp;` olarak kaçırılmalıdır.
- Pencere büyüdükçe içerik alanı büyümeli; sabit yükseklikler kullanılmamalıdır.
