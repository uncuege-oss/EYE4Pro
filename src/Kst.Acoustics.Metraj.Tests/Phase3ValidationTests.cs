using Kst.Acoustics.Metraj.Counting;
using Kst.Acoustics.Metraj.Template;
using Kst.Acoustics.Metraj.Models;
using Kst.Acoustics.Metraj.Io;
using netDxf;
using netDxf.Entities;
using netDxf.Blocks;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Metraj.Tests;

public class Phase3ValidationTests
{
    private (DxfDocument Doc, List<MetrajPoint> TemplatePoints) CreateValidationScenario(int state)
    {
        var doc = new DxfDocument();
        
        var templatePoints = new List<MetrajPoint>
        {
            new MetrajPoint(0,0,0), new MetrajPoint(1,0,0), new MetrajPoint(1,1,0), new MetrajPoint(0,1,0), new MetrajPoint(0,0,0)
        };

        var block = new Block("Square");
        for (int i = 0; i < templatePoints.Count - 1; i++)
        {
            block.Entities.Add(new Line(
                new Vector3(templatePoints[i].X, templatePoints[i].Y, 0),
                new Vector3(templatePoints[i + 1].X, templatePoints[i + 1].Y, 0)));
        }
        doc.Blocks.Add(block);

        int totalExpected = 10;

        if (state == 1) // Orijinal bloklu
        {
            for (int i = 0; i < totalExpected; i++)
                doc.Entities.Add(new Insert(block, new Vector3(i * 10, 0, 0)));
        }
        else if (state == 2) // Yarı bloklu yarı patlatılmış
        {
            for (int i = 0; i < 5; i++)
                doc.Entities.Add(new Insert(block, new Vector3(i * 10, 0, 0)));
            
            for (int i = 5; i < totalExpected; i++)
                AddExplodedSquare(doc, i * 10, 0);
        }
        else if (state == 3) // Tamamen patlatılmış
        {
            for (int i = 0; i < totalExpected; i++)
                AddExplodedSquare(doc, i * 10, 0);
        }

        return (doc, templatePoints);
    }

    private void AddExplodedSquare(DxfDocument doc, double offsetX, double offsetY)
    {
        doc.Entities.Add(new Line(new Vector3(offsetX, offsetY, 0), new Vector3(offsetX + 1, offsetY, 0)));
        doc.Entities.Add(new Line(new Vector3(offsetX + 1, offsetY, 0), new Vector3(offsetX + 1, offsetY + 1, 0)));
        doc.Entities.Add(new Line(new Vector3(offsetX + 1, offsetY + 1, 0), new Vector3(offsetX, offsetY + 1, 0)));
        doc.Entities.Add(new Line(new Vector3(offsetX, offsetY + 1, 0), new Vector3(offsetX, offsetY, 0)));
    }

    [Fact]
    public void Phase3_FinalValidation_StatesShouldMatch()
    {
        var counts = new List<int>();
        string tempDir = Path.Combine(Path.GetTempPath(), "KstMetrajTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            for (int state = 1; state <= 3; state++)
            {
                var scenario = CreateValidationScenario(state);
                var doc = scenario.Doc;
                
                var lib = new TemplateLibrary();
                lib.AddTemplate("Square", scenario.TemplatePoints);
                
                var loader = new DxfDocumentLoader();
                string tmpPath = Path.Combine(tempDir, $"state_{state}.dxf");
                doc.Save(tmpPath);
                
                var project = loader.Load(tmpPath);
                var entities = project.Entities;

                var blockCounter = new BlockCounter();
                var matcher = new TemplateMatcher(lib);
                var explodedCounter = new ExplodedCounter(matcher);

                var blockResults = blockCounter.Count(project);
                var explodedResults = explodedCounter.Count(entities);

                var combined = new Dictionary<BlockCountKey, int>(blockResults);
                foreach (var kv in explodedResults)
                {
                    var key = kv.Key;
                    if (combined.ContainsKey(key)) combined[key] += kv.Value;
                    else combined[key] = kv.Value;
                }

                int total = combined.Values.Sum();
                counts.Add(total);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        Assert.Equal(10, counts[0]);
        Assert.Equal(10, counts[1]);
        Assert.Equal(10, counts[2]);
    }
}
