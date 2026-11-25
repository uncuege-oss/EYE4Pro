using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// TIN üzerinde bir XY doğru parçasının doğrusal kot profili; T parametresi [0,1].
public readonly record struct AraziProfilParcasi(double BasT, double BitT, double BasKotM, double BitKotM, int UcgenSirasi);

/// TIN üçgenlerinin değişmez kopyası üzerinde sınırlayıcı kutu ağacı.
/// Tek sefer hazırlanır; paralel kot sorguları güvenlidir. Kaynak yüzey değişirse yeniden oluşturulur.
public sealed class AraziKotIndeksi
{
    private const double Epsilon = 1e-9;
    private const int YaprakKapasitesi = 8;
    private readonly Ucgen[] _ucgenler;
    private readonly Dugum[] _dugumler;
    public double? MinKotM { get; }
    public double? MaxKotM { get; }

    public AraziKotIndeksi(AraziYuzeyi yuzey)
    {
        ArgumentNullException.ThrowIfNull(yuzey);
        var ucgenler = new List<Ucgen>(yuzey.Ucgenler.Count);
        for (int i = 0; i < yuzey.Ucgenler.Count; i++)
        {
            var t = yuzey.Ucgenler[i];
            if (t.A < 0 || t.B < 0 || t.C < 0 || t.A >= yuzey.Dugumler.Count ||
                t.B >= yuzey.Dugumler.Count || t.C >= yuzey.Dugumler.Count)
                throw new ArgumentException("Arazi yüzeyinde geçersiz üçgen bağlantısı var.");
            var a = yuzey.Dugumler[t.A]; var b = yuzey.Dugumler[t.B]; var c = yuzey.Dugumler[t.C];
            if (!Sonlu(a) || !Sonlu(b) || !Sonlu(c))
                throw new ArgumentException("Kot indeksi için arazi koordinatları sonlu olmalıdır.");
            double payda = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (!double.IsFinite(payda)) throw new ArgumentException("Arazi koordinat aralığı kot hesabı için geçersiz.");
            if (Math.Abs(payda) < Epsilon) continue;
            double minX = Math.Min(a.X, Math.Min(b.X, c.X)), maxX = Math.Max(a.X, Math.Max(b.X, c.X));
            double minY = Math.Min(a.Y, Math.Min(b.Y, c.Y)), maxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
            // Barycentric -epsilon kabulü üçgen kutusunun az dışına taşabilir.
            double pay = Math.Max(maxX - minX, maxY - minY) * 2 * Epsilon + 1e-12;
            ucgenler.Add(new(a, b, c, payda, i,
                minX - pay, minY - pay, maxX + pay, maxY + pay,
                a.X / 3 + b.X / 3 + c.X / 3, a.Y / 3 + b.Y / 3 + c.Y / 3));
        }
        _ucgenler = ucgenler.ToArray();
        if (_ucgenler.Length == 0) { _dugumler = []; return; }
        MinKotM=_ucgenler.Min(t=>Math.Min(t.A.Z,Math.Min(t.B.Z,t.C.Z)));
        MaxKotM=_ucgenler.Max(t=>Math.Max(t.A.Z,Math.Max(t.B.Z,t.C.Z)));
        var dugumler = new List<Dugum>();
        AgacOlustur(0, _ucgenler.Length, dugumler);
        _dugumler = dugumler.ToArray();
    }

    // Return original TIN indices for the exact triangle-overlap calculation.
    internal void KutuAdaylari(double minX, double minY, double maxX, double maxY, List<int> sonuc)
    {
        sonuc.Clear();
        if (_dugumler.Length == 0) return;
        Span<int> stack = stackalloc int[64];
        int count = 1;
        stack[0] = 0;
        while (count > 0)
        {
            var node = _dugumler[stack[--count]];
            if (node.MaxX < minX || node.MinX > maxX || node.MaxY < minY || node.MinY > maxY) continue;
            if (node.Sol >= 0)
            {
                stack[count++] = node.Sol;
                stack[count++] = node.Sag;
                continue;
            }
            for (int i = node.Bas; i < node.Bas + node.Adet; i++)
            {
                var t = _ucgenler[i];
                if (t.MaxX >= minX && t.MinX <= maxX && t.MaxY >= minY && t.MinY <= maxY)
                    sonuc.Add(t.Sira);
            }
        }
    }

    public double? KotSorgula(double x, double y)
    {
        if (_dugumler.Length == 0 || !double.IsFinite(x) || !double.IsFinite(y)) return null;
        // Ağaç her adımda ikiye bölünür; int boyutlu dizide derinlik 32'yi aşmaz.
        Span<int> yigin = stackalloc int[64];
        int adet = 1, ilkSira = int.MaxValue;
        yigin[0] = 0;
        double? sonuc = null;
        while (adet > 0)
        {
            var d = _dugumler[yigin[--adet]];
            if (d.IlkSira >= ilkSira || x < d.MinX || x > d.MaxX || y < d.MinY || y > d.MaxY) continue;
            if (d.Sol >= 0)
            {
                yigin[adet++] = d.Sag;
                yigin[adet++] = d.Sol;
                continue;
            }
            for (int i = d.Bas; i < d.Bas + d.Adet; i++)
            {
                var t = _ucgenler[i];
                if (t.Sira >= ilkSira || x < t.MinX || x > t.MaxX || y < t.MinY || y > t.MaxY) continue;
                double wa = ((t.B.Y - t.C.Y) * (x - t.C.X) + (t.C.X - t.B.X) * (y - t.C.Y)) / t.Payda;
                double wb = ((t.C.Y - t.A.Y) * (x - t.C.X) + (t.A.X - t.C.X) * (y - t.C.Y)) / t.Payda;
                double wc = 1 - wa - wb;
                if (wa < -Epsilon || wb < -Epsilon || wc < -Epsilon) continue;
                sonuc = wa * t.A.Z + wb * t.B.Z + wc * t.C.Z;
                ilkSira = t.Sira;
            }
        }
        return sonuc;
    }

    /// Doğru parçasını kesen TIN üçgenlerini BVH ile bulur; ilk zemin kesişimini adım hatası olmadan hesaplamak içindir.
    public IReadOnlyList<AraziProfilParcasi> ProfilParcalari(double x0, double y0, double x1, double y1)
    {
        if (!double.IsFinite(x0) || !double.IsFinite(y0) || !double.IsFinite(x1) || !double.IsFinite(y1))
            throw new ArgumentException("Profil koordinatları sonlu olmalıdır.");
        var sonuc = new List<AraziProfilParcasi>();
        if (_dugumler.Length == 0) return sonuc;
        double dx = x1 - x0, dy = y1 - y0;
        Span<int> stack = stackalloc int[64]; int count = 1; stack[0] = 0;
        while (count > 0)
        {
            var node = _dugumler[stack[--count]];
            if (!KutuKesisir(node.MinX, node.MinY, node.MaxX, node.MaxY)) continue;
            if (node.Sol >= 0) { stack[count++] = node.Sol; stack[count++] = node.Sag; continue; }
            for (int i = node.Bas; i < node.Bas + node.Adet; i++)
            {
                var tri = _ucgenler[i];
                if (!KutuKesisir(tri.MinX, tri.MinY, tri.MaxX, tri.MaxY)) continue;
                double a0 = ((tri.B.Y - tri.C.Y) * (x0 - tri.C.X) + (tri.C.X - tri.B.X) * (y0 - tri.C.Y)) / tri.Payda;
                double b0 = ((tri.C.Y - tri.A.Y) * (x0 - tri.C.X) + (tri.A.X - tri.C.X) * (y0 - tri.C.Y)) / tri.Payda;
                double da = ((tri.B.Y - tri.C.Y) * dx + (tri.C.X - tri.B.X) * dy) / tri.Payda;
                double db = ((tri.C.Y - tri.A.Y) * dx + (tri.A.X - tri.C.X) * dy) / tri.Payda;
                double lo = 0, hi = 1;
                if (!Kirp(a0, da, ref lo, ref hi) || !Kirp(b0, db, ref lo, ref hi) || !Kirp(1-a0-b0, -da-db, ref lo, ref hi) || hi-lo < 1e-12) continue;
                double z0 = a0*tri.A.Z+b0*tri.B.Z+(1-a0-b0)*tri.C.Z;
                double dz = da*tri.A.Z+db*tri.B.Z+(-da-db)*tri.C.Z;
                sonuc.Add(new(lo, hi, z0+lo*dz, z0+hi*dz, tri.Sira));
            }
        }
        return sonuc.OrderBy(p => p.BasT).ThenBy(p => p.UcgenSirasi).ToArray();

        bool KutuKesisir(double minX, double minY, double maxX, double maxY)
        {
            double lo=0, hi=1;
            return Kirp(x0-minX,dx,ref lo,ref hi) && Kirp(maxX-x0,-dx,ref lo,ref hi) &&
                Kirp(y0-minY,dy,ref lo,ref hi) && Kirp(maxY-y0,-dy,ref lo,ref hi);
        }
        static bool Kirp(double start,double delta,ref double lo,ref double hi)
        {
            if (Math.Abs(delta)<1e-20) return start>=-1e-12;
            double t=-start/delta;
            if(delta>0) lo=Math.Max(lo,t); else hi=Math.Min(hi,t);
            return hi>=lo && hi>=0 && lo<=1;
        }
    }

    private int AgacOlustur(int bas, int adet, List<Dugum> dugumler)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        int ilkSira = int.MaxValue;
        for (int i = bas; i < bas + adet; i++)
        {
            var t = _ucgenler[i];
            minX = Math.Min(minX, t.MinX); minY = Math.Min(minY, t.MinY);
            maxX = Math.Max(maxX, t.MaxX); maxY = Math.Max(maxY, t.MaxY);
            ilkSira = Math.Min(ilkSira, t.Sira);
        }
        int index = dugumler.Count;
        dugumler.Add(default);
        int sol = -1, sag = -1;
        if (adet > YaprakKapasitesi)
        {
            Array.Sort(_ucgenler, bas, adet, maxX - minX >= maxY - minY ? XSiralama : YSiralama);
            int yari = adet / 2;
            sol = AgacOlustur(bas, yari, dugumler);
            sag = AgacOlustur(bas + yari, adet - yari, dugumler);
        }
        dugumler[index] = new(minX, minY, maxX, maxY, bas, adet, sol, sag, ilkSira);
        return index;
    }

    private static bool Sonlu(AraziDugum p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
    private static readonly IComparer<Ucgen> XSiralama = Comparer<Ucgen>.Create((a, b) => a.MerkezX.CompareTo(b.MerkezX));
    private static readonly IComparer<Ucgen> YSiralama = Comparer<Ucgen>.Create((a, b) => a.MerkezY.CompareTo(b.MerkezY));
    private readonly record struct Ucgen(AraziDugum A, AraziDugum B, AraziDugum C, double Payda, int Sira,
        double MinX, double MinY, double MaxX, double MaxY, double MerkezX, double MerkezY);
    private readonly record struct Dugum(double MinX, double MinY, double MaxX, double MaxY,
        int Bas, int Adet, int Sol, int Sag, int IlkSira);
}
