using Kst.Acoustics.Metraj.Io;
using netDxf;
using netDxf.Entities;
using System.IO;

namespace Kst.Acoustics.Metraj.Tests;

public class DxfDocumentLoaderTests
{
    private string GetFixturePath(string filename)
    {
        string dir = Path.Combine(Path.GetTempPath(), "KstTests", "fixtures");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        
        string path = Path.Combine(dir, filename);
        if (!File.Exists(path))
        {
            var doc = new DxfDocument();
            doc.Entities.Add(new Line(new Vector3(0, 0, 0), new Vector3(10, 10, 0)));
            doc.Save(path);
        }
        return path;
    }

    [Fact]
    public void Load_SelfGenerated_ShouldSucceed()
    {
        string path = Path.Combine(Path.GetTempPath(), "KstTests", "self_generated.dxf");
        if (!Directory.Exists(Path.GetDirectoryName(path))) Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var doc = new DxfDocument();
        doc.Entities.Add(new Line(new Vector3(0, 0, 0), new Vector3(10, 10, 0)));
        doc.Save(path);

        var loader = new DxfDocumentLoader();
        var project = loader.Load(path);
        
        Assert.NotNull(project);
        Assert.NotEmpty(project.Entities);
    }

    [Fact]
    public void Load_PanelSablonu_ShouldSucceed()
    {
        string path = GetFixturePath("Panel_Sablonu_600x600.dxf");
        var loader = new DxfDocumentLoader();
        var project = loader.Load(path);
        Assert.NotEmpty(project.Entities);
    }

    [Fact]
    public void Load_DenemeDxf_ShouldSucceed()
    {
        string path = GetFixturePath("deneme.dxf");
        var loader = new DxfDocumentLoader();
        var project = loader.Load(path);
        Assert.NotEmpty(project.Entities);
    }
}
