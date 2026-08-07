using System;

namespace Kst.Acoustics.Core.Architecture.Model3D;

public enum Model3DIhracatFormati
{
    Glb,
    Gltf,
    Obj
}

/// <summary>
/// M35 ilk 3D dis aktarim sozlesmesini tasir. Export motoru sonraki adimda bu ayarlari kullanir.
/// </summary>
public class Model3DIhracatSecenekleri
{
    public Model3DIhracatFormati Format { get; set; } = Model3DIhracatFormati.Glb;
    public string DosyaAdiOnEki { get; set; } = "eye4pro-mimari-3d";
    public string Birim { get; set; } = "m";
    public string KoordinatSistemi { get; set; } = "glTF sağ el koordinat sistemi; +Y yukarı; birim metre";
    public bool MahalleriAyriMeshOlarakYaz { get; set; } = true;
    public bool CekirdekVeSaftlariAyriMeshOlarakYaz { get; set; } = true;
    public bool RenkMalzemeleriniYaz { get; set; } = true;
    public bool ExtrasMetadataYaz { get; set; } = true;

    public string Uzanti => Format switch
    {
        Model3DIhracatFormati.Glb => ".glb",
        Model3DIhracatFormati.Gltf => ".gltf",
        Model3DIhracatFormati.Obj => ".obj",
        _ => ".glb"
    };

    public string MimeType => Format switch
    {
        Model3DIhracatFormati.Glb => "model/gltf-binary",
        Model3DIhracatFormati.Gltf => "model/gltf+json",
        Model3DIhracatFormati.Obj => "model/obj",
        _ => "model/gltf-binary"
    };

    public string DosyaAdiOner(string? projeAdi)
    {
        var kok = string.IsNullOrWhiteSpace(projeAdi)
            ? DosyaAdiOnEki
            : projeAdi.Trim();

        foreach (var karakter in System.IO.Path.GetInvalidFileNameChars())
        {
            kok = kok.Replace(karakter, '-');
        }

        if (!kok.EndsWith(Uzanti, StringComparison.OrdinalIgnoreCase))
            kok += Uzanti;

        return kok;
    }
}
