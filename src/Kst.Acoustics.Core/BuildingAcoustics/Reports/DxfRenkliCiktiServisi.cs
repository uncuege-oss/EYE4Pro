using System;
using System.Collections.Generic;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Reports;

/// <summary>
/// UX — Renkli DXF çıktısı: her bölmenin kanonik rapor sonucuna göre çizilir + orta noktasına
/// kullanılan büyüklük, değer, limit, marj ve durum etiketi yerleştirilir. Kullanıcı bunu CAD'de açıp yönetmelik
/// uyumunu görselleştirir.
/// </summary>
public class DxfRenkliCiktiServisi
{
    private readonly BinaAkustikRaporServisi _rapor = new();

    /// <param name="haricTutulanBolmeIdleri">Kullanıcının "Rapora Dahil" işaretini kaldırdığı
    /// bölmeler — geometri yine çizilir (gerçek duvardır) ama renkli uyum değerlendirmesi
    /// yerine nötr gri "Hariç Tutuldu" katmanına konur.</param>
    public void DxfRenkliRaporOlustur(BinaProje proje, string filePath,
        ISet<Guid>? haricTutulanBolmeIdleri = null)
    {
        var kanonikSatirlar = _rapor.TabloVerisiOlustur(proje)
            .GroupBy(s => s.SonucId)
            .ToDictionary(g => g.Key, g => g.First());
        var dxf = new DxfDocument();
        var lyrDuvar = new Layer("DUVARLAR");
        var lyrUygun = new Layer("UYGUN")   { Color = AciColor.Green };
        var lyrSinir = new Layer("SINIR")   { Color = new AciColor(40) };  // amber
        var lyrKaldi = new Layer("UYGUNSUZ"){ Color = AciColor.Red };
        var lyrEksik = new Layer("VERI_EKSIK") { Color = AciColor.Yellow };
        var lyrHaric = new Layer("HARIC_TUTULDU") { Color = new AciColor(9) }; // nötr gri
        var lyrEtiket = new Layer("ETIKETLER") { Color = AciColor.Cyan };
        dxf.Layers.Add(lyrDuvar); dxf.Layers.Add(lyrUygun); dxf.Layers.Add(lyrSinir);
        dxf.Layers.Add(lyrKaldi); dxf.Layers.Add(lyrEksik); dxf.Layers.Add(lyrHaric);
        dxf.Layers.Add(lyrEtiket);

        double zStack = 0;
        foreach (var kat in proje.Katlar.OrderBy(k => k.KatNo))
        {
            var off = kat.OriginOffset;
            // Her kat farklı Z seviyesinde — CAD'de katları üst üste değil yanyana koymak için
            // X yönünde de kaydır.
            double xOffsetKat = zStack;

            foreach (var mekan in kat.Mekanlar)
            {
                // Hesap dışı mekanlar uygun/uygunsuz paftasına hiç dahil edilmez. Böylece
                // balkon, açık teras ve şaftlar yanlışlıkla kırmızı/yeşil karar taşımaz.
                if (HesapDisiMekan(mekan))
                    continue;

                foreach (var bolme in mekan.Bolmeler)
                {
                    if (!bolme.HasGeometry || HesapDisiMekan(bolme.AliciMekan)) continue;
                    var sonucId = bolme.IsDisCephe
                        ? RaporSonucIdleri.DisCephe(mekan.Id)
                        : bolme.Id;
                    var haricTutuldu = haricTutulanBolmeIdleri?.Contains(sonucId) == true ||
                                       haricTutulanBolmeIdleri?.Contains(bolme.Id) == true;
                    var satir = kanonikSatirlar.TryGetValue(sonucId, out var kanonik)
                        ? kanonik
                        : _rapor.BolmeSatiriOlustur(proje, kat, mekan, bolme);
                    var (lyr, etiket) = haricTutuldu
                        ? (lyrHaric, "Hariç Tutuldu (Rapor Dışı)")
                        : SecLayerVeEtiket(satir, lyrUygun, lyrSinir, lyrKaldi, lyrEksik);
                    var a = new Vector2(bolme.Baslangic.X + off.X + xOffsetKat, bolme.Baslangic.Y + off.Y);
                    var b = new Vector2(bolme.Bitis.X + off.X + xOffsetKat, bolme.Bitis.Y + off.Y);
                    var line = new Line(a, b) { Layer = lyr };
                    dxf.Entities.Add(line);

                    // Etiketi bolme orta noktasına yerleştir, kısa metin
                    if (!string.IsNullOrEmpty(etiket))
                    {
                        var mid = new Vector2((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                        var txt = new Text(etiket, new Vector3(mid.X, mid.Y, 0), 0.3) { Layer = lyrEtiket };
                        dxf.Entities.Add(txt);
                    }
                }

                // Mekan adı etiketi (centroid) — P1 düzeltmesi: netDxf'in Text entity'si tek
                // satırlıktır; değerin içine gömülü "\n" DXF'in satır-tabanlı ASCII yapısını
                // bozup dosyayı okunamaz hale getiriyordu (netDxf'in kendi Load'ı sessizce null
                // döndürüyordu). İki ayrı tek-satır Text ile aynı görsel sonuç, bozulmadan.
                if (mekan.Poligon.Count >= 3)
                {
                    double cx = mekan.Poligon.Average(p => p.X) + off.X + xOffsetKat;
                    double cy = mekan.Poligon.Average(p => p.Y) + off.Y;
                    dxf.Entities.Add(new Text(mekan.Ad, new Vector3(cx, cy + 0.3, 0), 0.5) { Layer = lyrEtiket });
                    dxf.Entities.Add(new Text($"Kat {kat.KatNo}", new Vector3(cx, cy - 0.3, 0), 0.5) { Layer = lyrEtiket });
                }
            }

            zStack += 100; // sonraki kat 100 m kaydırılarak yan yana yerleşsin
        }

        // Açıklama bandı sol alt — aynı gömülü "\n" nedeniyle iki ayrı Text'e bölündü.
        dxf.Entities.Add(new Text("EYE4PRO Akustik — Renkli DXF Raporu",
            new Vector3(0, -2.5, 0), 0.4) { Layer = lyrEtiket });
        dxf.Entities.Add(new Text(
            "Yeşil: Uygun (+marj) | Amber: Sınır | Kırmızı: Limit Aşan | Sarı: Veri Eksik | Gri: Hariç Tutuldu",
            new Vector3(0, -3.5, 0), 0.4) { Layer = lyrEtiket });

        dxf.Save(filePath);
    }

    /// P1 düzeltmesi (2026-07-14) — eskiden her çağrıda AYNI isimli ("UYGUN" vb.) YENİ Layer
    /// nesneleri üretiliyordu; netDxf tablo yazımında bu, aynı adlı yinelenen katman
    /// kayıtlarına yol açıp üretilen DXF'in bazı okuyucularda (ör. netDxf'in kendi
    /// DxfDocument.Load'ı) bozuk/okunamaz sayılmasına neden oluyordu. Artık üst metotta bir
    /// kez oluşturulan paylaşılan Layer nesneleri buraya parametre olarak geçiliyor.
    private static (Layer, string) SecLayerVeEtiket(RaporSatiri satir,
        Layer lyrUygun, Layer lyrSinir, Layer lyrKaldi, Layer lyrEksik)
    {
        if (satir.Durum == nameof(UyumDurumu.VeriEksik) || double.IsNaN(satir.MarjDb))
            return (lyrEksik, $"{satir.SonucTuruEtiketi}: Veri Eksik");

        var buyukluk = satir.SonucTuru switch
        {
            AkustikRaporSonucTuru.DarbeSesi => "L'nT,w",
            AkustikRaporSonucTuru.DisCephe => "DnT,A,tr",
            _ => "DnT,A",
        };
        var yon = satir.DahaDusukDahaIyi ? "<=" : ">=";
        var durum = satir.Durum == nameof(UyumDurumu.Gecti) ? "UYGUN" : "UYGUNSUZ";
        var etiket = $"{buyukluk} {satir.Deger:F0} / {yon}{satir.LimitDeger:F0} / M {satir.MarjDb:+0;-0;0} / {durum}";

        Layer lyr = satir.Durum == nameof(UyumDurumu.Gecti)
            ? (satir.MarjDb >= 1 ? lyrUygun : lyrSinir)
            : (satir.MarjDb >= -1 ? lyrSinir : lyrKaldi);
        return (lyr, etiket);
    }

    private static bool HesapDisiMekan(Mekan? mekan) =>
        mekan != null &&
        (
            mekan.Sinif == MekanSinifi.HesapDisi ||
            EK2SiniflandirmaServisi.HesapDisiMi(mekan.KullanimTipi) ||
            MekanKullanimiHelper.Parse(mekan.KullanimTipi) == MekanKullanimiTipi.HesapDisi
        );
}
