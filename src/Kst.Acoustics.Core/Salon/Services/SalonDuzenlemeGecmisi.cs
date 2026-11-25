using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// Document content history. File identity/revision never travel backwards with edits.
public sealed class SalonDuzenlemeGecmisi
{
    readonly List<string> undo = new(), redo = new();
    string saved = "";
    public void Degisecek(SalonGeometri s) { undo.Add(Icerik(s)); if (undo.Count > 100) undo.RemoveAt(0); redo.Clear(); }
    public void Sifirla(SalonGeometri s, bool temiz = true) { undo.Clear(); redo.Clear(); saved = temiz ? Icerik(s) : ""; }
    public void Kaydedildi(SalonGeometri s) => saved = Icerik(s);
    public bool Kirli(SalonGeometri s) => Icerik(s) != saved;
    public SalonGeometri? Geri(SalonGeometri s) => Restore(s, undo, redo);
    public SalonGeometri? Ileri(SalonGeometri s) => Restore(s, redo, undo);
    public SalonGeometri? Vazgec(SalonGeometri s) { var restored = Restore(s, undo, new()); redo.Clear(); return restored; }
    static string Icerik(SalonGeometri s)
    {
        // Do not mutate the bound document while taking an image.
        var copy = SalonGeometriServisi.KopyadanYukle(SalonGeometriServisi.KopyaJson(s));
        copy.DosyaYolu = null; copy.GeometriRevizyon = 0;
        return SalonGeometriServisi.KopyaJson(copy);
    }
    static SalonGeometri? Restore(SalonGeometri s, List<string> from, List<string> to)
    {
        if (from.Count == 0) return null; var json = from[^1]; from.RemoveAt(from.Count - 1); to.Add(Icerik(s));
        var restored = SalonGeometriServisi.KopyadanYukle(json); restored.DosyaYolu = s.DosyaYolu; restored.GeometriRevizyon = s.GeometriRevizyon; return restored;
    }
}
