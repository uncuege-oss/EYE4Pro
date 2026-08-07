using Kst.Acoustics.Metraj.Counting;
using Kst.Acoustics.Metraj.Models;
using netDxf;
using netDxf.Entities;
using netDxf.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Kst.Acoustics.Metraj.Tests;

public class CountingTests
{
    [Fact]
    public void BlockCounter_NestedBlocks_ShouldCountCorrectly()
    {
        var doc = new DxfDocument();
        var blockB = new Block("BlockB");
        blockB.Entities.Add(new Line(Vector3.Zero, Vector3.UnitX));
        doc.Blocks.Add(blockB);
        var blockA = new Block("BlockA");
        blockA.Entities.Add(new Insert(blockB, Vector3.Zero));
        doc.Blocks.Add(blockA);
        doc.Entities.Add(new Insert(blockA, new Vector3(0, 0, 0)));
        doc.Entities.Add(new Insert(blockA, new Vector3(10, 10, 0)));

        var loader = new Kst.Acoustics.Metraj.Io.DxfDocumentLoader();
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dxf");
        doc.Save(tempFile);
        var project = loader.Load(tempFile);
        File.Delete(tempFile);

        var counter = new BlockCounter();
        var results = counter.Count(project);

        var keyA = results.Keys.FirstOrDefault(k => k.BlockName == "BlockA");
        var keyB = results.Keys.FirstOrDefault(k => k.BlockName == "BlockB");

        Assert.NotNull(keyA);
        Assert.NotNull(keyB);
        Assert.Equal(2, results[keyA]);
        Assert.Equal(2, results[keyB]);
    }

    [Fact]
    public void BlockCounter_WithAttributes_ShouldGroupCorrectly()
    {
        var doc = new DxfDocument();
        var block = new Block("Device");
        var attrDef = new AttributeDefinition("TYPE");
        block.AttributeDefinitions.Add(attrDef);
        doc.Blocks.Add(block);

        var ins1 = new Insert(block, Vector3.Zero);
        foreach (var att in ins1.Attributes) { if (att.Tag == "TYPE") att.Value = "A"; }
        var ins2 = new Insert(block, new Vector3(10,0,0));
        foreach (var att in ins2.Attributes) { if (att.Tag == "TYPE") att.Value = "A"; }
        var ins3 = new Insert(block, new Vector3(20,0,0));
        foreach (var att in ins3.Attributes) { if (att.Tag == "TYPE") att.Value = "B"; }

        doc.Entities.Add(ins1);
        doc.Entities.Add(ins2);
        doc.Entities.Add(ins3);

        var loader = new Kst.Acoustics.Metraj.Io.DxfDocumentLoader();
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dxf");
        doc.Save(tempFile);
        var project = loader.Load(tempFile);
        File.Delete(tempFile);

        var counter = new BlockCounter();
        var results = counter.Count(project);

        var keyA = results.Keys.FirstOrDefault(k => k.BlockName == "Device" && k.AttributesHash == "TYPE=A");
        var keyB = results.Keys.FirstOrDefault(k => k.BlockName == "Device" && k.AttributesHash == "TYPE=B");

        Assert.NotNull(keyA);
        Assert.NotNull(keyB);
        Assert.Equal(2, results[keyA]);
        Assert.Equal(1, results[keyB]);
    }

    [Fact]
    public void CombinedCounting_ShouldMergeResultsCorrectly()
    {
        var color = new MetrajColor(7, 255, 255, 255);
        var blockResults = new Dictionary<BlockCountKey, int>
        {
            [new BlockCountKey("Chair", "0", color, "")] = 5
        };

        var explodedResults = new Dictionary<BlockCountKey, int>
        {
            [new BlockCountKey("Chair", "0", color, "")] = 3,
            [new BlockCountKey("Table", "0", color, "")] = 1
        };

        var combined = new Dictionary<BlockCountKey, int>(blockResults);
        foreach (var kv in explodedResults)
        {
            if (combined.ContainsKey(kv.Key)) combined[kv.Key] += kv.Value;
            else combined[kv.Key] = kv.Value;
        }

        Assert.Equal(8, combined[new BlockCountKey("Chair", "0", color, "")]);
        Assert.Equal(1, combined[new BlockCountKey("Table", "0", color, "")]);
    }
}
