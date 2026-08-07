using System;
using Kst.Acoustics.Core.Importing;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Kullanım: dotnet run -- <dxf-dosyası>");
            return 64;
        }

        try
        {
            var parser = new DxfSurfaceParser();
            var doc = parser.ParseFile(Path.GetFullPath(args[0]));
            Console.WriteLine($"Loaded {doc.Surfaces.Count} surfaces.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex}");
            return 1;
        }
    }
}
