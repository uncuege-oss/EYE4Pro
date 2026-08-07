using Kst.Acoustics.Metraj.Template;
using Kst.Acoustics.Metraj.Models;

namespace Kst.Acoustics.Metraj.Tests;

public class TemplateTests
{
    private List<MetrajPoint> CreateLShape(double scale = 1.0, double rotationDeg = 0, bool mirror = false)
    {
        // L-Shape: (0,0) -> (10,0) -> (10,5)
        var pts = new List<MetrajPoint>
        {
            new MetrajPoint(0, 0, 0),
            new MetrajPoint(10 * scale, 0, 0),
            new MetrajPoint(10 * scale, 5 * scale, 0)
        };

        if (mirror)
        {
            pts = pts.Select(p => new MetrajPoint(p.X, -p.Y, p.Z)).ToList();
        }

        if (rotationDeg != 0)
        {
            double rad = rotationDeg * Math.PI / 180.0;
            pts = pts.Select(p => new MetrajPoint(
                p.X * Math.Cos(rad) - p.Y * Math.Sin(rad),
                p.X * Math.Sin(rad) + p.Y * Math.Cos(rad),
                p.Z
            )).ToList();
        }

        return pts;
    }

    [Fact]
    public void GeometricSignature_InvarianceTests_ShouldWork()
    {
        var original = new GeometricSignature(CreateLShape());
        var rotated = new GeometricSignature(CreateLShape(rotationDeg: 45));
        var scaled = new GeometricSignature(CreateLShape(scale: 2.5));
        var mirrored = new GeometricSignature(CreateLShape(mirror: true));
        var rotatedAndScaled = new GeometricSignature(CreateLShape(scale: 0.5, rotationDeg: 120));
        
        var different = new GeometricSignature(new List<MetrajPoint> { 
            new MetrajPoint(0,0,0), new MetrajPoint(10,0,0), new MetrajPoint(10,10,0) 
        });

        // Invariance Checks
        Assert.True(GeometricSignature.IsMatch(original, rotated, 1.0, 1.0, false), "Rotation invariance failed");
        Assert.True(GeometricSignature.IsMatch(original, scaled, 1.0, 1.0, false), "Scale invariance failed");
        Assert.True(GeometricSignature.IsMatch(original, mirrored, 1.0, 1.0, false), "Mirror invariance failed");
        Assert.True(GeometricSignature.IsMatch(original, rotatedAndScaled, 1.0, 1.0, false), "Combined invariance failed");
        
        // Difference Check
        Assert.False(GeometricSignature.IsMatch(original, different, 1.0, 1.0, false), "False positive match");
    }

    [Fact]
    public void TemplateLibrary_Sorting_ShouldWork()
    {
        var lib = new TemplateLibrary();
        
        // Simple line (1 segment)
        lib.AddTemplate("Single", new[] { new MetrajPoint(0,0,0), new MetrajPoint(1,0,0) });
        
        // L-Shape (2 segments)
        lib.AddTemplate("L-Shape", new[] { new MetrajPoint(0,0,0), new MetrajPoint(1,0,0), new MetrajPoint(1,1,0) });
        
        // U-Shape (3 segments)
        lib.AddTemplate("U-Shape", new[] { new MetrajPoint(0,0,0), new MetrajPoint(1,0,0), new MetrajPoint(1,1,0), new MetrajPoint(0,1,0) });

        var sorted = lib.GetSortedTemplates().ToList();

        Assert.Equal("U-Shape", sorted[0].Name);
        Assert.Equal("L-Shape", sorted[1].Name);
        Assert.Equal("Single", sorted[2].Name);
    }

    [Fact]
    public void TemplateMatcher_ExplodedEntities_ShouldMatchCorrectly()
    {
        // Setup Library
        var lib = new TemplateLibrary();
        // L-Shape: (0,0) -> (10,0) -> (10,5)
        var lPoints = new[] { new MetrajPoint(0,0,0), new MetrajPoint(10,0,0), new MetrajPoint(10,5,0) };
        lib.AddTemplate("L-Shape", lPoints);

        // Setup Project Entities (2 Exploded L-Shapes)
        var entities = new List<MetrajEntity>();
        
        // 1. Orijinal L (Çizgiler halinde patlatılmış)
        entities.Add(new MetrajLine("0", new MetrajColor(256), new MetrajPoint(0,0,0), new MetrajPoint(10,0,0)));
        entities.Add(new MetrajLine("0", new MetrajColor(256), new MetrajPoint(10,0,0), new MetrajPoint(10,5,0)));

        // 2. Döndürülmüş ve Ötelenmiş L
        entities.Add(new MetrajLine("0", new MetrajColor(256), new MetrajPoint(100,100,0), new MetrajPoint(100,110,0)));
        entities.Add(new MetrajLine("0", new MetrajColor(256), new MetrajPoint(100,110,0), new MetrajPoint(95,110,0)));

        // Match
        var matcher = new TemplateMatcher(lib);
        var matches = matcher.Match(entities);

        var lMatchCount = matches.Where(m => m.TemplateName == "L-Shape").Sum(m => m.Count);
        Assert.Equal(2, lMatchCount);
    }

    [Fact]
    public void TemplateMatcher_ComplexPolyline_ShouldMatch()
    {
        var lib = new TemplateLibrary();
        // Square: (0,0)-(10,0)-(10,10)-(0,10)-(0,0)
        var points = new List<MetrajPoint> { new(0,0,0), new(10,0,0), new(10,10,0), new(0,10,0), new(0,0,0) };
        lib.AddTemplate("Square", points);

        var entities = new List<MetrajEntity>
        {
            new MetrajLine("0", new MetrajColor(256), new(50,50,0), new(60,50,0)),
            new MetrajLine("0", new MetrajColor(256), new(60,50,0), new(60,60,0)),
            new MetrajLine("0", new MetrajColor(256), new(60,60,0), new(50,60,0)),
            new MetrajLine("0", new MetrajColor(256), new(50,60,0), new(50,50,0))
        };

        var matcher = new TemplateMatcher(lib);
        var matches = matcher.Match(entities);

        Assert.Equal(1, matches.Where(m => m.TemplateName == "Square").Sum(m => m.Count));
    }
}
