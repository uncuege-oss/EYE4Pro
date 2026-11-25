using Avalonia.Controls;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;
using netDxf;

namespace Kst.Acoustics.App.Terrain.Views;

internal sealed record YuklenenArazi(string Yol, AraziModeli Model, AraziYuzeyi Yuzey, IReadOnlyList<string> Uyarilar);

internal static class AraziProjeOkuyucu
{
    internal static async Task<YuklenenArazi?> AcAsync(Window sahip, string path, CancellationToken token)
    {
        string uzanti = Path.GetExtension(path).ToLowerInvariant();
        if (uzanti is not (".dwg" or ".dxf"))
        {
            return await Task.Run(() => {
                var model = AraziGeometriServisi.Ac(path);
                return new YuklenenArazi(path, model, new AraziYuzeyServisi().Olustur(model), []);
            }, token);
        }
        using var cad = await MimariCadDosyasi.AcAsync(path, token);
        var doc = await Task.Run(() => DxfDocument.Load(cad.DxfYolu) ?? throw new InvalidDataException("CAD dosyası okunamadı."), token);
        token.ThrowIfCancellationRequested();
        var dialog = new AraziCadImportWindow(doc, path);
        var sonuc = await dialog.ShowDialog<AraziImportSonucu?>(sahip);
        if (sonuc == null) return null;
        var yuzey = dialog.Yuzey ?? await Task.Run(() => new AraziYuzeyServisi().Olustur(sonuc.Arazi), token);
        return new(path, sonuc.Arazi, yuzey, sonuc.Uyarilar.ToArray());
    }
}
