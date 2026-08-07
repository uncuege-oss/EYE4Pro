using System;
using netDxf;
using netDxf.Entities;

class Program
{
    static void Main()
    {
        var doc = new DxfDocument();
        var block = new netDxf.Blocks.Block("test");
        block.Entities.Add(new Line(Vector3.Zero, Vector3.UnitX));
        doc.Blocks.Add(block);
        
        var ins = new Insert(block);
        var exploded = ins.Explode();
        Console.WriteLine($"Exploded count: {exploded.Count}");
    }
}
