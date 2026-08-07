namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// UPS montaj topolojisi — proje genelinde tek seçim.
/// </summary>
public enum UpsTopolojisi
{
    /// <summary>Lokal: kritik panoların önüne ayrı UPS; yalnız o pano + alt panoları beslenir.</summary>
    Lokal,
    /// <summary>Merkezi: ana panodan ayrı kolon → merkezi UPS → dağıtım panosu → kat kritik panoları.</summary>
    Merkezi
}

/// <summary>
/// Projedeki UPS (KGK) örneği — kütüphaneden seçilen <see cref="UpsTanim"/> ve
/// <see cref="AkuTanim"/>'a ada göre referans verir. Besleyen panonun altına
/// bağlanan kritik panoyu (PanoTipi.UpsPanosu) besler. Projede birden fazla
/// UPS bulunabilir (<see cref="ElektrikProje.Upsler"/>).
/// </summary>
public class Ups
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>Merkezi topoloji UPS'i mi? (true ise KritikPanoAd, merkezi UPS dağıtım panosudur;
    /// kat kritik panoları bu dağıtımın altında yer alır.)</summary>
    public bool Merkezi { get; set; }

    /// <summary>UPS girişini besleyen pano adı (normal kaynak).</summary>
    public string BesleyenPanoAd { get; set; } = "";

    /// <summary>UPS çıkışının beslediği kritik pano adı (PanoTipi.UpsPanosu).</summary>
    public string KritikPanoAd { get; set; } = "";

    /// <summary>Kütüphaneden seçilen UPS tipi adı.</summary>
    public string UpsTanimAd { get; set; } = "";

    /// <summary>Kütüphaneden seçilen akü tipi adı.</summary>
    public string AkuTanimAd { get; set; } = "";

    /// <summary>İstenen akü otonomi süresi (dakika) — 5/10/15/30 vb.</summary>
    public int OtonomiDk { get; set; } = 10;

    /// <summary>Tasarım emniyet/büyüme faktörü (örn. 1.25 = %25 pay).</summary>
    public double EmniyetFaktoru { get; set; } = 1.25;

    /// <summary>Giriş akımı hesabında akü şarj payı (%) — şarj sırasında ek giriş yükü.</summary>
    public double SarjPayiYuzde { get; set; } = 15;

    // === Hesaplanan değerler ===
    /// <summary>Kritik panodan gelen aktif yük (kW).</summary>
    public double KritikYukKW { get; set; }

    /// <summary>Kritik yükün görünür gücü (kVA) — yük cosφ'sine göre.</summary>
    public double KritikYukKVA { get; set; }

    /// <summary>Emniyet faktörlü gerekli UPS gücü (kVA min.).</summary>
    public double GerekliKVA { get; set; }

    /// <summary>Seçilen kütüphane UPS'inin anma gücü (kVA). Canvas, DXF ve raporlar aynı
    /// değeri kullanabilsin diye hesap sırasında UPS kaydına da yazılır.</summary>
    public double SeciliUpsGucKVA { get; set; }

    /// <summary>Seçilen UPS yükü karşılıyor mu (kVA ve kW).</summary>
    public bool YukYeterliMi { get; set; }

    /// <summary>UPS çıkış akımı (A).</summary>
    public double CikisAkimA { get; set; }

    /// <summary>UPS giriş akımı (A) — çıkış/η + akü şarj payı dahil.</summary>
    public double GirisAkimA { get; set; }

    // === Akü grubu (IEEE 485 sabit-güç) ===
    /// <summary>İnverter giriş gücü (W) — P_kritik / (η_inv).</summary>
    public double InverterGirisGucW { get; set; }

    /// <summary>Seri blok sayısı — DC bara gerilimi / blok gerilimi.</summary>
    public int SeriBlokSayisi { get; set; }

    /// <summary>Otonomi için blok başına gerekli güç (W/blok).</summary>
    public double BlokBasinaGerekliGucW { get; set; }

    /// <summary>Paralel string (kol) sayısı — bir string yetmezse artar.</summary>
    public int ParalelStringSayisi { get; set; }

    /// <summary>Toplam blok sayısı = seri × paralel.</summary>
    public int ToplamBlokSayisi { get; set; }

    /// <summary>Toplam akü kapasitesi (Ah) — paralel string × blok Ah.</summary>
    public double ToplamAh { get; set; }

    /// <summary>Toplam akü enerjisi (kWh) — nominal.</summary>
    public double ToplamEnerjiKWh { get; set; }

    /// <summary>Akü grubu yeterli mi (deşarj sonu gerilimi DC alt sınırı sağlıyor mu).</summary>
    public bool AkuYeterliMi { get; set; }
}
