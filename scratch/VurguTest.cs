using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main()
    {
        var aktifKablolar = new List<Kablo>
        {
            new Kablo { DevreId = "d1", Renk = "red" },
            new Kablo { DevreId = "d2", Renk = "blue" }
        };
        
        var vurguSet = new HashSet<string> { "d2" };
        
        var normalSegs = aktifKablolar.Where(k => !vurguSet.Contains(k.DevreId)).ToList();
        var vurguSegs = aktifKablolar.Where(k => vurguSet.Contains(k.DevreId)).ToList();
        
        Console.WriteLine($"Normal: {normalSegs.Count}, Vurgu: {vurguSegs.Count}");
    }
}
class Kablo { public string DevreId; public string Renk; }
