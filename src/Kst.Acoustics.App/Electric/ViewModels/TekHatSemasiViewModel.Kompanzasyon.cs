using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Kst.Acoustics.Core.Electric.Models;
using Kst.Acoustics.Core.Electric.Calculation;

namespace Kst.Acoustics.App.Electric.ViewModels;

public partial class TekHatSemasiViewModel
{
    // === Kompanzasyon Kademe Yönetimi ===

    /// <summary>
    /// Kompanzasyon sonucunu zorla oluşturur (yoksa). Tetik gücü altında olunsa bile
    /// kullanıcı kademe sayısı / reaktör ayarlayabilsin diye.
    /// </summary>
    private KompanzasyonSonucu KompanzasyonuSagla()
    {
        if (Proje.Kompanzasyon == null)
        {
            Proje.Kompanzasyon = new KompanzasyonSonucu
            {
                MevcutCosF = Proje.CosF,
                HedefCosF = Proje.HedefCosF,
                ToplamAktifGucKW = (Proje.KokPano?.ToplamGucW ?? 0) / 1000.0,
                KondansatorKademeSayisi = 6
            };
        }
        return Proje.Kompanzasyon;
    }

    public void KompanzasyonElleAyarToggle(bool elleAyarla)
    {
        var k = KompanzasyonuSagla();
        k.KademelerElleAyarlandi = elleAyarla;
        TumunuHesapla();
    }

    /// <summary>Kondansatör kademe sayısını 6/12/18 olarak ayarlar.</summary>
    public void KompanzasyonKondansatorKademeSayisi(int kademeSayisi)
    {
        if (!KompanzasyonHesapServisi.StandartKondansatorKademeSayilari.Contains(kademeSayisi))
            return;
        var k = KompanzasyonuSagla();
        k.KondansatorKademeSayisi = kademeSayisi;
        // Kademe sayısı değiştiğinde liste yeniden üretilmeli — elle ayar varsa kapat
        k.KademelerElleAyarlandi = false;
        TumunuHesapla();
    }

    /// <summary>Reaktör (SVC) kademelerini açar/kapatır. Aktifse 3 reaktör kademesi oluşur.</summary>
    public void KompanzasyonReaktorAktifAyarla(bool aktif)
    {
        var k = KompanzasyonuSagla();
        k.ReaktorAktif = aktif;
        k.KademelerElleAyarlandi = false; // yeniden üret
        TumunuHesapla();
    }

    /// <summary>Reaktör akort yüzdesini (5.67 / 7 / 14) ayarlar ve kademeleri yeniden üretir.</summary>
    public void KompanzasyonReaktorAkortAyarla(double yuzde)
    {
        if (yuzde <= 0) return;
        var k = KompanzasyonuSagla();
        k.ReaktorAkortYuzde = yuzde;
        k.KademelerElleAyarlandi = false; // yeni akorda yeniden üret
        TumunuHesapla();
    }

    /// <summary>Tek bir kondansatör kademesinin atamasını değiştirir (elle ayar moduna geçer).</summary>
    public void KompanzasyonKondansatorKademesiniDegistir(KompanzasyonKademe kademe, KondansatorTanim kond)
    {
        if (Proje.Kompanzasyon == null) return;
        Proje.Kompanzasyon.KademelerElleAyarlandi = true;
        KompanzasyonHesapServisi.KondansatorKademesiniGuncelle(kademe, kond);
        TumunuHesapla();
    }

    /// <summary>Tek bir reaktör kademesinin atamasını değiştirir (elle ayar moduna geçer).</summary>
    public void KompanzasyonReaktorKademesiniDegistir(KompanzasyonReaktorKademe kademe, ReaktorTanim reaktor)
    {
        if (Proje.Kompanzasyon == null) return;
        Proje.Kompanzasyon.KademelerElleAyarlandi = true;
        KompanzasyonHesapServisi.ReaktorKademesiniGuncelle(kademe, reaktor);
        TumunuHesapla();
    }

    /// <summary>SVC yük sürücüyü manuel atar (genelde otomatik seçilir; override için).</summary>
    public void KompanzasyonYukSurucuAyarla(SvcSurucuTanim? surucu)
    {
        if (Proje.Kompanzasyon == null) return;
        Proje.Kompanzasyon.SecilenYukSurucu = surucu;
        OnPropertyChanged(nameof(KompanzasyonSonucu));
    }

}
