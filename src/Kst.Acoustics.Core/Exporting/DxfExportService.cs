using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Exporting;

public sealed class DxfExportService
{
    /// <summary>
    /// Metre tabanlı modeller için geriye uyumlu kısa yol.
    /// Birimi metre olmayan projeler açıkça birim alan overload'u kullanmalıdır.
    /// </summary>
    public void Export(string path, IReadOnlyList<ProjectSurface> surfaces)
        => Export(path, surfaces, SurfaceImportUnit.Meters);

    public void Export(
        string path,
        IReadOnlyList<ProjectSurface> surfaces,
        SurfaceImportUnit unit)
    {
        var sb = new StringBuilder();

        // Birim bilgisini açıkça yaz. $INSUNITS olmadığında M01 ve birçok CAD aracı
        // güvenli bir varsayım yapamaz; M01 kullanıcı onayına ve mm önerisine düşer.
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("HEADER");
        sb.AppendLine("9");
        sb.AppendLine("$INSUNITS");
        sb.AppendLine("70");
        sb.AppendLine(ToDxfInsUnits(unit).ToString(System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");

        // DXF entities
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("ENTITIES");

        foreach (var surface in surfaces)
        {
            if (surface.Vertices.Count < 3) continue;

            // 3-4 köşe doğrudan 3DFACE; daha fazlası kulak-kesme (ear clipping) ile
            // üçgenlenir. Naif fan konkav poligonlarda (L plan, keyhole ile kırpılmış
            // M51 zeminleri) poligon dışına taşan hatalı üçgenler üretir.
            var vertices = surface.Vertices;
            if (vertices.Count == 3 || vertices.Count == 4)
            {
                Write3dFace(sb, surface.Category, vertices);
            }
            else
            {
                foreach (var (p0, p1, p2) in Simulation.RayTracingService.TriangulatePolygon(vertices))
                    Write3dFace(sb, surface.Category, new[] { p0, p1, p2 });
            }
        }

        // DXF Footer
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("0");
        sb.AppendLine("EOF");

        // UTF-8 (BOM'suz): Türkçe katman/kategori adları (Korniş, Duvar-Sahne…) bozulmamalı.
        // DXF R2007+ UTF-8 okur; BOM bazı parser'ları şaşırtmasın diye eklenmez.
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static int ToDxfInsUnits(SurfaceImportUnit unit) => unit switch
    {
        SurfaceImportUnit.Inches => 1,
        SurfaceImportUnit.Feet => 2,
        SurfaceImportUnit.Millimeters => 4,
        SurfaceImportUnit.Centimeters => 5,
        SurfaceImportUnit.Meters => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Desteklenmeyen DXF birimi.")
    };

    private static void Write3dFace(StringBuilder sb, string layer, IReadOnlyList<Geometry.Point3D> v)
    {
        sb.AppendLine("0");
        sb.AppendLine("3DFACE");
        sb.AppendLine("8"); // Layer
        sb.AppendLine(string.IsNullOrWhiteSpace(layer) ? "0" : layer);

        // Point 1
        sb.AppendLine("10"); sb.AppendLine(v[0].X.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine("20"); sb.AppendLine(v[0].Y.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine("30"); sb.AppendLine(v[0].Z.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));

        // Point 2
        sb.AppendLine("11"); sb.AppendLine(v[1].X.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine("21"); sb.AppendLine(v[1].Y.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine("31"); sb.AppendLine(v[1].Z.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));

        // Point 3
        sb.AppendLine("12"); sb.AppendLine(v[2].X.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine("22"); sb.AppendLine(v[2].Y.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine("32"); sb.AppendLine(v[2].Z.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));

        // Point 4 (if only 3 points, repeat the 3rd point)
        var p4 = v.Count > 3 ? v[3] : v[2];
        sb.AppendLine("13"); sb.AppendLine(p4.X.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine("23"); sb.AppendLine(p4.Y.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine("33"); sb.AppendLine(p4.Z.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
    }
}
