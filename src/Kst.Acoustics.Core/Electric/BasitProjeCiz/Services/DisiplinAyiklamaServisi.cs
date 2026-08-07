using System.Linq;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// Bir kattaki/projedeki disiplin içeriğinin sayım özeti.
public readonly record struct DisiplinSayim(int KuvvetliEleman, int ZayifEleman,
    int KuvvetliKablo, int ZayifKablo, int KuvvetliDevre, int ZayifDevre)
{
    public bool KuvvetliVar => KuvvetliEleman + KuvvetliKablo + KuvvetliDevre > 0;
    public bool ZayifVar    => ZayifEleman + ZayifKablo + ZayifDevre > 0;
}

/// Disiplin ayıklamanın sonucu — durum mesajı için özet sayılar.
public readonly record struct DisiplinAyiklamaSonuc(int SilinenEleman, int SilinenKablo,
    int SilinenDevre, int SilinenMahalSatiri);

/// M12/M16 ayrımının veri motoru: bir projeyi tek disipline indirger.
/// Geometri (katlar, odalar, açıklıklar, DXF arka planı, bina geometri bağı)
/// her iki disiplinde de aynen korunur; yalnız elektrik içeriği ayıklanır.
public static class DisiplinAyiklamaServisi
{
    /// Projedeki disiplin içeriğini sayar (uyarı/karar diyalogları için).
    public static DisiplinSayim Say(BasitProjeCizProje proje)
    {
        int kE = 0, zE = 0, kK = 0, zK = 0, kD = 0, zD = 0;
        foreach (var kat in proje.Katlar)
        {
            foreach (var el in kat.Elemanlar)
            {
                if (BpcSistemKatalogu.Disiplini(el.Tip) == BpcDisiplin.ZayifAkim) zE++; else kE++;
            }
            foreach (var kablo in kat.Kablolar)
            {
                if (BpcSistemKatalogu.Disiplini(kablo) == BpcDisiplin.ZayifAkim) zK++; else kK++;
            }
            kD += kat.Devreler.Count + kat.VavienGruplari.Count + kat.PrizDevreleri.Count
                + kat.MekanikDevreleri.Count + kat.BuatPanoBaglantilari.Count;
            zD += kat.SistemDevreleri.Count;
        }
        return new DisiplinSayim(kE, zE, kK, zK, kD, zD);
    }

    /// Projeyi verilen disipline indirger: diğer disiplinin elemanları, kabloları,
    /// devreleri ve mahal satırları silinir. ModulDisiplini damgalanır.
    public static DisiplinAyiklamaSonuc Ayikla(BasitProjeCizProje proje, BpcDisiplin hedef)
    {
        int silinenEleman = 0, silinenKablo = 0, silinenDevre = 0, silinenMahal = 0;

        foreach (var kat in proje.Katlar)
        {
            // Elemanlar — eleman TİPİ üzerinden sınıflanır (PaftaTipi varsayılan
            // değerde kalmış eski kayıtlarda bile doğru sonuç verir).
            var silinecekler = kat.Elemanlar
                .Where(e => BpcSistemKatalogu.Disiplini(e.Tip) != hedef).ToList();
            foreach (var e in silinecekler) kat.Elemanlar.Remove(e);
            silinenEleman += silinecekler.Count;

            // Kablolar
            silinenKablo += kat.Kablolar.RemoveAll(k => BpcSistemKatalogu.Disiplini(k) != hedef);

            // Devre yapıları — disipline göre toptan temizlenir.
            if (hedef == BpcDisiplin.ZayifAkim)
            {
                silinenDevre += kat.Devreler.Count + kat.VavienGruplari.Count
                    + kat.PrizDevreleri.Count + kat.MekanikDevreleri.Count
                    + kat.BuatPanoBaglantilari.Count + kat.KabloTavalari.Count
                    + kat.PanoBeslemeHatlari.Count;
                kat.Devreler.Clear();
                kat.VavienGruplari.Clear();
                kat.PrizDevreleri.Clear();
                kat.MekanikDevreleri.Clear();
                kat.BuatPanoBaglantilari.Clear();
                kat.KabloTavalari.Clear();
                kat.PanoBeslemeHatlari.Clear();
                kat.Topraklama = new BpcTopraklama();
            }
            else
            {
                silinenDevre += kat.SistemDevreleri.Count;
                kat.SistemDevreleri.Clear();
            }

            // Mahal listesi satırları — satırın malzeme tipi üzerinden.
            silinenMahal += kat.MahalListesi
                .RemoveAll(s => BpcSistemKatalogu.Disiplini(s.Tip) != hedef);

            // Diğer disiplinden referans kalmış devre bağlantılarını da arındır:
            // hedefte kalan devrelerde, silinen elemanlara işaret eden Id'ler ayıklanır.
            var kalanIds = kat.Elemanlar.Select(e => e.Id).ToHashSet();
            if (hedef == BpcDisiplin.ZayifAkim)
            {
                foreach (var d in kat.SistemDevreleri)
                {
                    d.CihazIds.RemoveAll(id => !kalanIds.Contains(id));
                    if (d.MerkezId is { } m && !kalanIds.Contains(m)) d.MerkezId = null;
                    if (d.AraToplayiciId is { } a && !kalanIds.Contains(a)) d.AraToplayiciId = null;
                }
            }
            else
            {
                foreach (var d in kat.Devreler)
                    d.ArmaturElemanIds.RemoveAll(id => !kalanIds.Contains(id));
            }
        }

        proje.ModulDisiplini = hedef;
        return new DisiplinAyiklamaSonuc(silinenEleman, silinenKablo, silinenDevre, silinenMahal);
    }
}
