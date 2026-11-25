using Kst.Acoustics.Core.Bina.Services;

namespace Kst.Acoustics.Core.Terrain.Services;

public sealed class AraziImportSecenekleri
{
    public double? MetrePerBirim { get; set; }
    /// Null tüm katmanlar; boş küme hiçbir katman anlamına gelir.
    public ISet<string>? Katmanlar { get; set; }
    public bool MetinKotlariniOku { get; set; } = true;
    public double EtiketEslesmeToleransiM { get; set; } = 2.0;
}

/// DWG dönüşümü ve DXF kot okuma. Kaynak CAD dosyası değiştirilmez.
public sealed class CadAraziImportServisi
{
    public async Task<AraziImportSonucu> OkuAsync(string path, AraziImportSecenekleri? secenekler = null,
        CancellationToken cancellationToken = default)
    {
        using var cad = await MimariCadDosyasi.AcAsync(path, cancellationToken);
        secenekler ??= new();
        var parser = new DxfAraziParser
        {
            MetrePerBirim = secenekler.MetrePerBirim, Katmanlar = secenekler.Katmanlar,
            MetinKotlariniOku = secenekler.MetinKotlariniOku,
            EtiketEslesmeToleransiM = secenekler.EtiketEslesmeToleransiM
        };
        var sonuc = await Task.Run(() => parser.Parse(cad.DxfYolu), cancellationToken);
        sonuc.Arazi.DosyaYolu = Path.GetFullPath(path);
        if (sonuc.Arazi.Kaynak is not null) sonuc.Arazi.Kaynak.DosyaYolu = Path.GetFullPath(path);
        sonuc.Arazi.AraziAdi = Path.GetFileNameWithoutExtension(path);
        return sonuc;
    }
}
