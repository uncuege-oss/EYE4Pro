using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Rules;

/// <summary>
/// M34 on kontrol motorunda calistirilacak tek kural tanimi.
/// </summary>
public class Kural
{
    public string Kod { get; set; } = "";
    public string Baslik { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public string Kapsam { get; set; } = "";
    public string KaynakKodu { get; set; } = "";
    public string CalismaTuru { get; set; } = "Veri";
    public KontrolSeviyesi VarsayilanSeviye { get; set; } = KontrolSeviyesi.Uyari;
    public bool Aktif { get; set; } = true;
    public List<string> GerekliVeriler { get; set; } = new();
}
