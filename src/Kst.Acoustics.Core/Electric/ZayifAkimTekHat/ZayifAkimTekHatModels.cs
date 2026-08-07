using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public enum ELVBlokRol
{
    Merkez,
    AraToplayici,
    Cihaz
}

public class ELVKatInfo
{
    public int KatNo { get; set; }
    public string Ad { get; set; } = string.Empty;
}

public class ELVBlok
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public BpcElemanTipi ElemanTipi { get; set; }
    public string ElemanAdi { get; set; } = string.Empty;
    public int? Kat { get; set; }
    public string? OdaAdi { get; set; }
    public List<string> BagliCocukBlokIds { get; set; } = new();
    public ELVBlokRol Rol { get; set; }
    public double LayoutX { get; set; }
    public double LayoutY { get; set; }
    public int Miktar { get; set; } = 1;

    public int CihazSayisi
    {
        get => Rol == ELVBlokRol.Cihaz ? Miktar : BagliCocukBlokIds.Count;
    }
}

public class ELVSayfa
{
    public BpcPaftaTipi Sistem { get; set; }
    public string SistemAdi { get; set; } = string.Empty;
    public string KabloRengi { get; set; } = "#000000";
    public List<ELVBlok> Bloklar { get; set; } = new();

    public int ToplamCihazSayisi
    {
        get => Bloklar.Where(b => b.Rol == ELVBlokRol.Cihaz).Sum(b => b.CihazSayisi);
    }
}

public class ZayifAkimTekHatProje
{
    /// <summary>Güncel .e4p13 şema sürümü. Kaydederken bu değere yazılır.</summary>
    public const int GuncelSemaSurum = 3;

    /// <summary>Dosyadaki şema sürümü. Eski dosyalarda alan yoktur → 0 okunur ve
    /// <see cref="ZayifAkimTekHatMigrasyonServisi"/> güncel sürüme taşır.</summary>
    public int SemaSurum { get; set; }

    public string? KaynakDosyaYolu { get; set; }
    public string ProjeAdi { get; set; } = "Yeni Proje";
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
    public Kst.Acoustics.Core.Electric.Models.BinaKonfigurasyonu Bina { get; set; } = new();
    public List<ELVKatInfo> Katlar { get; set; } = new();
    public List<ELVSayfa> Sayfalar { get; set; } = new();
    /// <summary>Yerleşik salt-okunur kataloğa eklenen, projeyle kaydedilen kullanıcı kalemleri.</summary>
    public List<ElvOzelKutuphaneKalemi> OzelKutuphaneKalemleri { get; set; } = new();

    /// <summary>Data sistemi — rack kabin merkezli yeni model (kat rack'leri → ana merkez).</summary>
    public ElvDataSistemi DataSistemi { get; set; } = new();

    /// <summary>Kamera/CCTV sistemi — Data ile aynı rack merkezli model.</summary>
    public ElvKameraSistemi KameraSistemi { get; set; } = new();

    /// <summary>Acil anons sistemi — merkez cihazları + geçişli hoparlör hatları.</summary>
    public ElvAnonsSistemi AnonsSistemi { get; set; } = new();

    /// <summary>TV sistemi — her TV prizi ana santrale, santralden çatı uydu/antenlerine.</summary>
    public ElvTvSistemi TvSistemi { get; set; } = new();

    /// <summary>Yangın algılama sistemi — kat panosu olmadan loop'lar doğrudan santrale.</summary>
    public ElvYanginSistemi YanginSistemi { get; set; } = new();

    /// <summary>Telefon sistemi — her priz ayrı hat ile telefon santraline (TV deseni).</summary>
    public ElvTelefonSistemi TelefonSistemi { get; set; } = new();

    /// <summary>Diafon/kapı zili sistemi — kat üniteleri diafon santraline hat çeker.</summary>
    public ElvDiafonSistemi DiafonSistemi { get; set; } = new();

    public int ToplamCihazSayisi
    {
        get => Sayfalar.Sum(s => s.ToplamCihazSayisi);
    }

    public void KatlariBinadanGuncelle()
    {
        var etiketler = Bina.KatEtiketleri();
        Katlar.Clear();
        foreach (var etiket in etiketler)
        {
            int katNo = 0;
            string ad = etiket;
            if (etiket == "Çatı")
            {
                katNo = Bina.NormalKatSayisi + 1;
                ad = "Çatı Katı";
            }
            else if (etiket == "Z")
            {
                katNo = 0;
                ad = "Zemin Kat";
            }
            else if (etiket.StartsWith("-"))
            {
                if (int.TryParse(etiket, out int parsed))
                {
                    katNo = parsed;
                    ad = $"{Math.Abs(parsed)}. Bodrum";
                }
            }
            else
            {
                if (int.TryParse(etiket, out int parsed))
                {
                    katNo = parsed;
                    ad = $"{parsed}. Kat";
                }
            }
            Katlar.Add(new ELVKatInfo { KatNo = katNo, Ad = ad });
        }
    }
}
