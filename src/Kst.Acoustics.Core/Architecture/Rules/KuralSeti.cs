using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Rules;

/// <summary>
/// Birlikte calistirilacak mimari on kontrol kurallarini gruplar.
/// </summary>
public class KuralSeti
{
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public string KaynakKodu { get; set; } = "";
    public bool Aktif { get; set; } = true;
    public List<Kural> Kurallar { get; set; } = new();

    public int ToplamKuralSayisi => Kurallar.Count;

    public int AktifKuralSayisi
    {
        get
        {
            var toplam = 0;
            foreach (var kural in Kurallar)
            {
                if (kural.Aktif)
                    toplam++;
            }

            return toplam;
        }
    }
}
