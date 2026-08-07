using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Architecture.Materials;
using Kst.Acoustics.Core.Architecture.Layout;

namespace Kst.Acoustics.Core.Architecture.Models;

/// <summary>
/// Mimari BIM tasarim kararlarinin kok proje modeli. `.e4mimari.json` olarak saklanir.
/// </summary>
public class MimariProje
{
    public int SemaSurum { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjeAdi { get; set; } = "Yeni Mimari Proje";
    public string? DosyaYolu { get; set; }
    public DateTime OlusturmaZamani { get; set; } = DateTime.Now;
    public DateTime GuncellemeZamani { get; set; } = DateTime.Now;

    public IhtiyacProgrami IhtiyacProgrami { get; set; } = new();
    public PlanTaslagi? PlanTaslagi { get; set; }
    public List<MahalSablonu> MahalSablonlari { get; set; } = new();
    public List<MimariMalzeme> Malzemeler { get; set; } = new();
    public List<YapiElemaniTipi> YapiElemaniTipleri { get; set; } = new();
    public List<MahalFinisajProfili> FinisajProfilleri { get; set; } = new();
    public List<MalzemeAtamasi> MalzemeAtamalari { get; set; } = new();

    /// <summary>
    /// Ileride M50 entegrasyonu geldiginde baglanacak `.e4bina.json` dosya yolu.
    /// Faz 9'a kadar bos kalabilir.
    /// </summary>
    public string? BinaGeometriYolu { get; set; }
}
