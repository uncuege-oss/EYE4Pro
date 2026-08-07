using System;
using System.Linq;
using System.Collections.Generic;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

class Program
{
    static void Main()
    {
        var kat = new BasitProjeKat();
        
        var buat = new BasitProjeEleman { Id = "buat1", Tip = BpcElemanTipi.Buat, X = 10, Y = 10 };
        var priz1 = new BasitProjeEleman { Id = "priz1", Tip = BpcElemanTipi.Priz, X = 0, Y = 0 };
        var priz2 = new BasitProjeEleman { Id = "priz2", Tip = BpcElemanTipi.Priz, X = 5, Y = 0 };
        
        kat.Elemanlar.Add(buat);
        kat.Elemanlar.Add(priz1);
        kat.Elemanlar.Add(priz2);
        
        var devre1 = new BpcPrizDevre { BuatId = "buat1", Id = "d1" };
        devre1.PrizElemanIds.Add("priz1");
        kat.PrizDevreleri.Add(devre1);
        BasitProjeCizKabloServisi.TumPrizRotalariniHesapla(kat, 0, 0);
        
        Console.WriteLine($"Kablo sayısı (Devre 1 sonrası): {kat.Kablolar.Count}");
        
        // Simulating second click
        // First PrizKabloModuTiklama -> EnsurePrizDevre is called on "priz2"
        var devre2 = new BpcPrizDevre { BuatId = "buat1", Id = "d2" };
        devre2.PrizElemanIds.Add("priz2");
        kat.PrizDevreleri.Add(devre2);
        
        BasitProjeCizKabloServisi.TumPrizRotalariniHesapla(kat, 0, 0);
        
        Console.WriteLine($"Kablo sayısı (TumPrizRotalariniHesapla sonrası): {kat.Kablolar.Count}");
        var dev1Kablolar = kat.Kablolar.Count(k => k.DevreId == devre1.Id);
        var dev2Kablolar = kat.Kablolar.Count(k => k.DevreId == devre2.Id);
        Console.WriteLine($"Devre 1 kablo sayısı: {dev1Kablolar}, Devre 2 kablo sayısı: {dev2Kablolar}");
    }
}
