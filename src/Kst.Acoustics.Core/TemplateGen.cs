using System;
using System.Collections.Generic;
using System.IO;
using Kst.Acoustics.Core.Exporting;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Geometry;

namespace TemplateGen;

class Program
{
    static void Main(string[] args)
    {
        var outputPath = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(Directory.GetCurrentDirectory(), "Panel_Sablonu_600x600.dxf");
        var exporter = new DxfExportService();
        var surfaces = new List<ProjectSurface>();

        // Create a 600x600 square surface
        var vertices = new List<Point3D>
        {
            new Point3D(0, 0, 0),
            new Point3D(600, 0, 0),
            new Point3D(600, 600, 0),
            new Point3D(0, 600, 0)
        };

        surfaces.Add(new ProjectSurface(1, "Panel Frame", "PANEL", vertices));

        exporter.Export(outputPath, surfaces, SurfaceImportUnit.Millimeters);
        Console.WriteLine($"Template generated successfully: {outputPath}");
    }
}
