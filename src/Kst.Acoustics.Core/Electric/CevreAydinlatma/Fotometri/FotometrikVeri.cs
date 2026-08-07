namespace Kst.Acoustics.Core.Electric.CevreAydinlatma.Fotometri;

public enum FotometriKaynak
{
    Ldt,
    Ies,
    Manuel
}

/// <summary>
/// Bir armatürün fotometrik verisi (LDT/EULUMDAT veya IES/LM-63 dosyasından ya da manuel).
/// Işık şiddeti tablosu cd/1000 lm biriminde, [C düzlemi indeksi][gama açısı indeksi] sırasıyla
/// saklanır. M15 Faz 0 fizibilite kapsamı: elektrik yükü (güç) + aydınlatma hesabı (akı + dağılım).
/// </summary>
public sealed class FotometrikVeri
{
    public FotometriKaynak Kaynak { get; set; }
    public string ArmaturAdi { get; set; } = "";

    /// <summary>Sistem gücü, balast/sürücü dahil (W).</summary>
    public double ToplamGucW { get; set; }

    /// <summary>Toplam ışık akısı (lm).</summary>
    public double ToplamAkiLm { get; set; }

    public int LambaSayisi { get; set; }

    /// <summary>Simetri göstergesi (LDT Isym; IES için 0 kabul edilir).</summary>
    public int Simetri { get; set; }

    /// <summary>C düzlemi (azimut) açıları, derece.</summary>
    public double[] CDuzlemAcilari { get; set; } = [];

    /// <summary>Gama (eğim) açıları, derece.</summary>
    public double[] GamaAcilari { get; set; } = [];

    /// <summary>Işık şiddeti, cd/1000 lm; [C düzlemi indeksi][gama indeksi].</summary>
    public double[][] IsikSiddeti_cd_klm { get; set; } = [];

    /// <summary>En yüksek ışık şiddeti, gerçek kandela (cd) cinsinden.</summary>
    public double KandelaMaks()
    {
        double maks = 0;
        foreach (var duzlem in IsikSiddeti_cd_klm)
            foreach (var v in duzlem)
                if (v > maks)
                    maks = v;
        return maks * ToplamAkiLm / 1000.0;
    }
}
