using System.Collections.Generic;

namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// Bina kat konfigürasyonu — pano yerleşim şemasında kat çizgilerini oluşturur.
/// </summary>
public class BinaKonfigurasyonu
{
    public int BodrumSayisi { get; set; } = 0;
    public bool ZeminVar { get; set; } = true;
    public int NormalKatSayisi { get; set; } = 3;
    public int CatiKati { get; set; } = 0; // 0 veya 1

    /// <summary>
    /// Kat etiketlerini en üstten en alta sıralar:
    /// Çatı → N…1 → Zemin → -1…-N
    /// </summary>
    public List<string> KatEtiketleri()
    {
        var liste = new List<string>();
        if (CatiKati > 0) liste.Add("Çatı");
        for (int i = NormalKatSayisi; i >= 1; i--) liste.Add(i.ToString());
        liste.Add("Z");
        for (int i = 1; i <= BodrumSayisi; i++) liste.Add($"-{i}");
        return liste;
    }
}
