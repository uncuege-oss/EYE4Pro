using System.Globalization;

namespace Kst.Acoustics.Core.Terrain.Services;

public static class AraziMetrajCsv
{
    public static void Yaz(TextWriter writer, AraziKarsilastirmasi metraj, string ilk, string son, CancellationToken token)
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        string Sayi(double value) => value.ToString("0.#########", culture);
        static string Etiket(string s)
        {
            if (s.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        writer.WriteLine("İlk hâl;" + Etiket(ilk));
        writer.WriteLine("Son hâl;" + Etiket(son));
        writer.WriteLine("Kot farkı;İlk kot eksi son kot (m)");
        writer.WriteLine("Kalem;Alan (m²);Hacim (m³)");
        writer.WriteLine($"Yarma;{Sayi(metraj.YarmaAlaniM2)};{Sayi(metraj.YarmaM3)}");
        writer.WriteLine($"Dolgu;{Sayi(metraj.DolguAlaniM2)};{Sayi(metraj.DolguM3)}");
        writer.WriteLine($"Eşit kot;{Sayi(metraj.EsitAlanM2)};0");
        writer.WriteLine($"Ortak alan;{Sayi(metraj.OrtakAlanM2)};");
        writer.WriteLine($"Net (yarma − dolgu);;{Sayi(metraj.NetM3)}");
        writer.WriteLine($"Yalnız ilk hâlde;{Sayi(Math.Max(0,metraj.IlkAlanM2-metraj.OrtakAlanM2))};");
        writer.WriteLine($"Yalnız son hâlde;{Sayi(Math.Max(0,metraj.SonAlanM2-metraj.OrtakAlanM2))};");
        writer.WriteLine();
        writer.WriteLine("Parça;Tür;Merkez X (m);Merkez Y (m);İlk ortalama kot (m);Son ortalama kot (m);Fark (m);Alan (m²);Yarma (m³);Dolgu (m³)");
        for (int i = 0; i < metraj.Parcalar.Count; i++)
        {
            if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
            var p = metraj.Parcalar[i];
            double d = p.OrtalamaFark;
            string tur = d > 0 ? "Yarma" : d < 0 ? "Dolgu" : "Eşit";
            writer.WriteLine(string.Join(";", i+1, tur, Sayi((p.A.X+p.B.X+p.C.X)/3), Sayi((p.A.Y+p.B.Y+p.C.Y)/3),
                Sayi((p.A.IlkKot+p.B.IlkKot+p.C.IlkKot)/3), Sayi((p.A.SonKot+p.B.SonKot+p.C.SonKot)/3),
                Sayi(d), Sayi(p.AlanM2), Sayi(d>0 ? p.HacimM3 : 0), Sayi(d<0 ? p.HacimM3 : 0)));
        }
    }
}
