using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Geometry;

public static class PanelYerlesimServisi
{
    // Üretilebilecek maksimum panel sayısı (güvenlik valfi — birim hatasında kilitlenmeyi önler)
    public const int MaksimumPanelSayisi = 2000;

    public class SalonBoyutlari
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }

        public double Derinlik => MaxY - MinY;
        public double Genislik => MaxX - MinX;
        public double Yukseklik => MaxZ - MinZ;

        // Sahne önünün MinY mi MaxY mi olduğunu belirler
        public bool SahneOnuMinY { get; set; } = true;

        // "1 metre kaç proje birimi" (mm→1000, cm→100, m→1, inch→1/0.0254). UI'dan gelen metre
        // değerlerinin (PanelEniM, SarkmaCikmaM) proje birimine çevrilmesinde kullanılır.
        public double MetreCarpan { get; set; } = 1.0;
    }

    public static double GetMetreCarpan(SurfaceImportUnit unit) => unit switch
    {
        SurfaceImportUnit.Millimeters => 1000.0,
        SurfaceImportUnit.Centimeters => 100.0,
        SurfaceImportUnit.Inches => 1.0 / 0.0254,
        SurfaceImportUnit.Feet => 1.0 / 0.3048,
        _ => 1.0
    };

    public static SalonBoyutlari BoyutlariHesapla(
        IReadOnlyList<ProjectSurface> surfaces,
        string? sahneOnuYuzeyId,
        SurfaceImportUnit unit)
    {
        var bounds = BoyutlariHesapla(surfaces, sahneOnuYuzeyId);
        bounds.MetreCarpan = GetMetreCarpan(unit);
        return bounds;
    }

    public static SalonBoyutlari BoyutlariHesapla(IReadOnlyList<ProjectSurface> surfaces, string? sahneOnuYuzeyId)
    {
        if (surfaces == null || surfaces.Count == 0)
            return new SalonBoyutlari();

        var allPoints = surfaces.SelectMany(s => s.Vertices).ToList();
        var bounds = new SalonBoyutlari
        {
            MinX = allPoints.Min(p => p.X),
            MaxX = allPoints.Max(p => p.X),
            MinY = allPoints.Min(p => p.Y),
            MaxY = allPoints.Max(p => p.Y),
            MinZ = allPoints.Min(p => p.Z),
            MaxZ = allPoints.Max(p => p.Z)
        };

        if (!string.IsNullOrEmpty(sahneOnuYuzeyId))
        {
            var frontSurface = surfaces.FirstOrDefault(s => s.Name == sahneOnuYuzeyId || s.Id.ToString() == sahneOnuYuzeyId);
            if (frontSurface != null)
            {
                var centroid = SurfaceTransformService.ComputeCentroid(frontSurface.Vertices);
                var distToMinY = Math.Abs(centroid.Y - bounds.MinY);
                var distToMaxY = Math.Abs(centroid.Y - bounds.MaxY);
                bounds.SahneOnuMinY = distToMinY < distToMaxY;
            }
        }

        return bounds;
    }

    public static List<AcousticPanel> PanelleriUret(AcousticPanelKonfigurasyon konfig, SalonBoyutlari boyutlar)
        => PanelleriUret(konfig, Array.Empty<ProjectSurface>(), boyutlar);

    public static List<AcousticPanel> PanelleriUret(
        AcousticPanelKonfigurasyon konfig,
        IReadOnlyList<ProjectSurface> surfaces,
        SalonBoyutlari boyutlar)
    {
        var paneller = new List<AcousticPanel>();
        var dilimler = PanelDilimleriniHesapla(konfig, boyutlar).ToList();
        if (dilimler.Count == 0) return paneller;

        bool tavanAktif = konfig.AktifTipler.Contains(PanelTipi.Tavan);
        bool solAktif = konfig.AktifTipler.Contains(PanelTipi.DuvarSol);
        bool sagAktif = konfig.AktifTipler.Contains(PanelTipi.DuvarSag);

        var hedefler = surfaces.Count > 0
            ? PanelHedefDetector.Bul(surfaces, konfig)
            : new PanelHedefSec(null, null, null);

        var odaMerkezi = new Point3D(
            0.5 * (boyutlar.MinX + boyutlar.MaxX),
            0.5 * (boyutlar.MinY + boyutlar.MaxY),
            0.5 * (boyutlar.MinZ + boyutlar.MaxZ));

        // Sarkma metre → proje birimi
        var sarkma = konfig.SarkmaCikmaM * boyutlar.MetreCarpan;

        foreach (var dilim in dilimler)
        {
            if (tavanAktif)
            {
                var panel = TavanPaneliUret(dilim, hedefler.Tavan, sarkma, konfig.SarkmaCikmaM, boyutlar, odaMerkezi);
                if (panel is not null) paneller.Add(panel);
            }

            if (solAktif)
            {
                var panel = DuvarPaneliUret(PanelTipi.DuvarSol, dilim, hedefler.SolDuvar, sarkma, konfig.SarkmaCikmaM, boyutlar, odaMerkezi);
                if (panel is not null) paneller.Add(panel);
            }

            if (sagAktif)
            {
                var panel = DuvarPaneliUret(PanelTipi.DuvarSag, dilim, hedefler.SagDuvar, sarkma, konfig.SarkmaCikmaM, boyutlar, odaMerkezi);
                if (panel is not null) paneller.Add(panel);
            }

            // Güvenlik: çok fazla panel üretilmek üzereyse durdur
            if (paneller.Count >= MaksimumPanelSayisi)
                break;
        }

        return paneller;
    }

    private static AcousticPanel? TavanPaneliUret(
        PanelDilimi dilim, HedefYuzey? hedef, double sarkma, double sarkmaMetre,
        SalonBoyutlari boyutlar, Point3D odaMerkezi)
    {
        const double epsilon = 0.01;

        if (hedef is null)
        {
            // Fallback: yatay tavan varsay, bbox X aralığı
            var zPanel = boyutlar.MaxZ - sarkma;
            var xSol = boyutlar.MinX + epsilon;
            var xSag = boyutlar.MaxX - epsilon;
            return new AcousticPanel
            {
                Tip = PanelTipi.Tavan,
                DilimNo = dilim.DilimNo,
                PanelEniM = dilim.PanelEniM,
                SarkmaCikmaM = sarkmaMetre,
                Vertices = new List<Point3D>
                {
                    new(xSol, dilim.BasY, zPanel),
                    new(xSag, dilim.BasY, zPanel),
                    new(xSag, dilim.BitY, zPanel),
                    new(xSol, dilim.BitY, zPanel)
                }
            };
        }

        var verts = hedef.Surface.Vertices;
        var yMin = Math.Min(dilim.BasY, dilim.BitY);
        var yMax = Math.Max(dilim.BasY, dilim.BitY);

        // Tavan yüzeyinin kendi Y aralığı dışında dilim yoksa atla
        var surfaceYMin = verts.Min(v => v.Y);
        var surfaceYMax = verts.Max(v => v.Y);
        var yShiritMin = Math.Max(yMin, surfaceYMin);
        var yShiritMax = Math.Min(yMax, surfaceYMax);
        if (yShiritMax - yShiritMin < 1e-6) return null;

        // Y şeridinde tavan poligonunun X aralığı
        if (!PoligonYSheridiXAraligi(verts, yShiritMin, yShiritMax, out var xSheritMin, out var xSheritMax))
            return null;

        xSheritMin += epsilon;
        xSheritMax -= epsilon;
        if (xSheritMax - xSheritMin < 1e-6) return null;

        // Z'yi tavan düzleminden çöz: nz*Z = D - nx*X - ny*Y → Z = (D - nx*X - ny*Y) / nz
        var n = hedef.Normal;
        var d = hedef.PlaneD;
        if (Math.Abs(n.Z) < 1e-6) return null;

        double Z(double x, double y) => (d - n.X * x - n.Y * y) / n.Z;

        var corner1 = new Point3D(xSheritMin, yShiritMin, Z(xSheritMin, yShiritMin));
        var corner2 = new Point3D(xSheritMax, yShiritMin, Z(xSheritMax, yShiritMin));
        var corner3 = new Point3D(xSheritMax, yShiritMax, Z(xSheritMax, yShiritMax));
        var corner4 = new Point3D(xSheritMin, yShiritMax, Z(xSheritMin, yShiritMax));

        var offset = OdaIciOffset(n, hedef.Centroid, odaMerkezi, sarkma);

        return new AcousticPanel
        {
            Tip = PanelTipi.Tavan,
            DilimNo = dilim.DilimNo,
            PanelEniM = dilim.PanelEniM,
            SarkmaCikmaM = sarkmaMetre,
            Vertices = new List<Point3D>
            {
                Offset(corner1, offset),
                Offset(corner2, offset),
                Offset(corner3, offset),
                Offset(corner4, offset)
            }
        };
    }

    private static AcousticPanel? DuvarPaneliUret(
        PanelTipi tip, PanelDilimi dilim, HedefYuzey? hedef, double sarkma, double sarkmaMetre,
        SalonBoyutlari boyutlar, Point3D odaMerkezi)
    {
        const double epsilon = 0.01;

        if (hedef is null)
        {
            // Fallback: AABB hizalı dikey duvar
            var xSabit = tip == PanelTipi.DuvarSol ? boyutlar.MinX + sarkma : boyutlar.MaxX - sarkma;
            return new AcousticPanel
            {
                Tip = tip,
                DilimNo = dilim.DilimNo,
                PanelEniM = dilim.PanelEniM,
                SarkmaCikmaM = sarkmaMetre,
                Vertices = new List<Point3D>
                {
                    new(xSabit, dilim.BasY, boyutlar.MinZ),
                    new(xSabit, dilim.BitY, boyutlar.MinZ),
                    new(xSabit, dilim.BitY, boyutlar.MaxZ),
                    new(xSabit, dilim.BasY, boyutlar.MaxZ)
                }
            };
        }

        var verts = hedef.Surface.Vertices;
        var yMin = Math.Min(dilim.BasY, dilim.BitY);
        var yMax = Math.Max(dilim.BasY, dilim.BitY);

        // Duvar Y aralığına clip et
        var surfaceYMin = verts.Min(v => v.Y);
        var surfaceYMax = verts.Max(v => v.Y);
        var yShiritMin = Math.Max(yMin, surfaceYMin);
        var yShiritMax = Math.Min(yMax, surfaceYMax);
        if (yShiritMax - yShiritMin < 1e-6) return null;

        // Z aralığı: duvarın kendi Z'leri (eğimli tavanla birleşen duvarlarda üst sınır Y'ye bağlı olabilir,
        // basit ilk versiyon: duvarın bbox Z'sini kullan, hafif epsilon)
        var zMin = verts.Min(v => v.Z) + epsilon;
        var zMax = verts.Max(v => v.Z) - epsilon;
        if (zMax - zMin < 1e-6) return null;

        var n = hedef.Normal;
        var d = hedef.PlaneD;

        // Düzlemden X'i çöz: nx*X = D - ny*Y - nz*Z → X = (D - ny*Y - nz*Z) / nx
        if (Math.Abs(n.X) < 1e-6)
        {
            // X bileşeni yok (saf ön/arka duvar olur, sol/sağ duvar değil) — Y'den çözmek gerekir.
            // Bu durumda hedef yanlış sınıflandırılmış demektir; emniyetli fallback'e dön.
            var xSabit = tip == PanelTipi.DuvarSol ? boyutlar.MinX + sarkma : boyutlar.MaxX - sarkma;
            return new AcousticPanel
            {
                Tip = tip,
                DilimNo = dilim.DilimNo,
                PanelEniM = dilim.PanelEniM,
                SarkmaCikmaM = sarkmaMetre,
                Vertices = new List<Point3D>
                {
                    new(xSabit, yShiritMin, zMin),
                    new(xSabit, yShiritMax, zMin),
                    new(xSabit, yShiritMax, zMax),
                    new(xSabit, yShiritMin, zMax)
                }
            };
        }

        double X(double y, double z) => (d - n.Y * y - n.Z * z) / n.X;

        var corner1 = new Point3D(X(yShiritMin, zMin), yShiritMin, zMin);
        var corner2 = new Point3D(X(yShiritMax, zMin), yShiritMax, zMin);
        var corner3 = new Point3D(X(yShiritMax, zMax), yShiritMax, zMax);
        var corner4 = new Point3D(X(yShiritMin, zMax), yShiritMin, zMax);

        var offset = OdaIciOffset(n, hedef.Centroid, odaMerkezi, sarkma);

        return new AcousticPanel
        {
            Tip = tip,
            DilimNo = dilim.DilimNo,
            PanelEniM = dilim.PanelEniM,
            SarkmaCikmaM = sarkmaMetre,
            Vertices = new List<Point3D>
            {
                Offset(corner1, offset),
                Offset(corner2, offset),
                Offset(corner3, offset),
                Offset(corner4, offset)
            }
        };
    }

    private static Vector3D OdaIciOffset(Vector3D normal, Point3D yuzeyCentroid, Point3D odaMerkezi, double sarkma)
    {
        // İçeri yönünü oda merkezine bakarak belirle: normal "dışa" bakıyorsa -normal, "içe" bakıyorsa +normal kullan
        var icSart = (odaMerkezi.X - yuzeyCentroid.X) * (-normal.X)
                     + (odaMerkezi.Y - yuzeyCentroid.Y) * (-normal.Y)
                     + (odaMerkezi.Z - yuzeyCentroid.Z) * (-normal.Z);
        var dir = icSart >= 0 ? new Vector3D(-normal.X, -normal.Y, -normal.Z) : normal;
        return new Vector3D(dir.X * sarkma, dir.Y * sarkma, dir.Z * sarkma);
    }

    private static Point3D Offset(Point3D p, Vector3D v) => new(p.X + v.X, p.Y + v.Y, p.Z + v.Z);

    // Poligonun Y∈[yMin,yMax] şeridindeki X aralığını döner. Y şeridi içinde kalan vertice'ler ve
    // şerit sınırlarıyla kesişen edge'lerin X'leri toplanır.
    private static bool PoligonYSheridiXAraligi(
        IReadOnlyList<Point3D> verts, double yMin, double yMax,
        out double xMin, out double xMax)
    {
        xMin = double.PositiveInfinity;
        xMax = double.NegativeInfinity;

        int n = verts.Count;
        for (int i = 0; i < n; i++)
        {
            var p = verts[i];
            if (p.Y >= yMin - 1e-9 && p.Y <= yMax + 1e-9)
            {
                if (p.X < xMin) xMin = p.X;
                if (p.X > xMax) xMax = p.X;
            }

            var q = verts[(i + 1) % n];
            CheckIntersection(p, q, yMin, ref xMin, ref xMax);
            CheckIntersection(p, q, yMax, ref xMin, ref xMax);
        }

        return xMax > xMin;
    }

    private static void CheckIntersection(Point3D p, Point3D q, double y, ref double xMin, ref double xMax)
    {
        var dy = q.Y - p.Y;
        if (Math.Abs(dy) < 1e-9) return;
        var t = (y - p.Y) / dy;
        if (t < -1e-9 || t > 1 + 1e-9) return;
        var x = p.X + t * (q.X - p.X);
        if (x < xMin) xMin = x;
        if (x > xMax) xMax = x;
    }

    private static IEnumerable<PanelDilimi> PanelDilimleriniHesapla(
        AcousticPanelKonfigurasyon konfig,
        SalonBoyutlari boyutlar)
    {
        var derinlik = boyutlar.Derinlik;
        if (derinlik <= 0) yield break;

        double yBaslangic = boyutlar.SahneOnuMinY ? boyutlar.MinY : boyutlar.MaxY;
        int yon = boyutlar.SahneOnuMinY ? 1 : -1;
        var k = boyutlar.MetreCarpan;

        if (konfig.PanelEniModu == PanelEniModu.Sirali)
        {
            var enler = konfig.PanelEnleriM.Where(e => e > 0).Select(e => e * k).ToArray();
            if (enler.Length == 0) yield break;

            double ilerleme = 0;
            int dilimNo = 1;
            while (ilerleme < derinlik - 0.001 * k)
            {
                var panelEni = enler[Math.Min(dilimNo - 1, enler.Length - 1)];
                if (ilerleme + panelEni > derinlik + 0.001 * k)
                    yield break;

                var basY = yBaslangic + (ilerleme * yon);
                var bitY = basY + (panelEni * yon);
                // dilim.PanelEniM metre cinsinden saklanır (panel objesi UI ile uyumlu kalsın)
                yield return new PanelDilimi(dilimNo, panelEni / k, basY, bitY);

                ilerleme += panelEni;
                dilimNo++;

                if (dilimNo > MaksimumPanelSayisi) yield break;
            }

            yield break;
        }

        if (konfig.PanelEniM <= 0) yield break;

        var panelEniBirim = konfig.PanelEniM * k;
        int tamDilimSayisi = (int)Math.Ceiling(derinlik / panelEniBirim);
        if (tamDilimSayisi > MaksimumPanelSayisi) yield break; // birim hatası ya da çok küçük panel

        for (int i = 0; i < tamDilimSayisi; i++)
        {
            double basY = yBaslangic + (i * panelEniBirim * yon);
            double bitY = basY + (panelEniBirim * yon);

            if (boyutlar.SahneOnuMinY && bitY > boyutlar.MaxY) bitY = boyutlar.MaxY;
            if (!boyutlar.SahneOnuMinY && bitY < boyutlar.MinY) bitY = boyutlar.MinY;

            var gercekEnBirim = Math.Abs(bitY - basY);
            if (gercekEnBirim < 0.001 * k) continue;

            yield return new PanelDilimi(i + 1, gercekEnBirim / k, basY, bitY);
        }
    }

    private readonly record struct PanelDilimi(int DilimNo, double PanelEniM, double BasY, double BitY);

    public static AcousticPanel PanelDondur(AcousticPanel panel, SalonBoyutlari? boyutlar = null)
    {
        if (Math.Abs(panel.AciDerece) < 0.001)
        {
            if (boyutlar is not null)
                panel.Vertices = PaneliSinirlarIcindeTut(panel.Tip, panel.Vertices, boyutlar);
            return panel;
        }

        var centroid = SurfaceTransformService.ComputeCentroid(panel.Vertices);
        var rad = panel.AciDerece * Math.PI / 180.0;
        
        // Tavan paneli -> X ekseni etrafında döner
        // Duvar paneli -> Z ekseni etrafında döner
        var k = panel.Tip == PanelTipi.Tavan 
            ? new Vector3D(1, 0, 0) 
            : new Vector3D(0, 0, 1);

        var newVertices = panel.Vertices
            .Select(v => RotatePoint(v, centroid, k, rad))
            .ToList();

        panel.Vertices = boyutlar is null
            ? newVertices
            : PaneliSinirlarIcindeTut(panel.Tip, newVertices, boyutlar);
        return panel;
    }

    private static List<Point3D> PaneliSinirlarIcindeTut(
        PanelTipi tip,
        IReadOnlyList<Point3D> vertices,
        SalonBoyutlari boyutlar)
    {
        if (vertices.Count == 0)
            return vertices.ToList();

        double dx = 0;
        double dz = 0;

        if (tip == PanelTipi.Tavan)
        {
            var maxZ = vertices.Max(v => v.Z);
            if (maxZ > boyutlar.MaxZ)
                dz -= maxZ - boyutlar.MaxZ;
        }
        else if (tip == PanelTipi.DuvarSol)
        {
            var minX = vertices.Min(v => v.X);
            if (minX < boyutlar.MinX)
                dx += boyutlar.MinX - minX;
        }
        else if (tip == PanelTipi.DuvarSag)
        {
            var maxX = vertices.Max(v => v.X);
            if (maxX > boyutlar.MaxX)
                dx -= maxX - boyutlar.MaxX;
        }

        if (Math.Abs(dx) < 1e-9 && Math.Abs(dz) < 1e-9)
            return vertices.ToList();

        return vertices
            .Select(v => new Point3D(v.X + dx, v.Y, v.Z + dz))
            .ToList();
    }

    private static Point3D RotatePoint(Point3D p, Point3D origin, Vector3D k, double rad)
    {
        var vx = p.X - origin.X;
        var vy = p.Y - origin.Y;
        var vz = p.Z - origin.Z;

        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var dot = k.X*vx + k.Y*vy + k.Z*vz;

        var crossX = k.Y*vz - k.Z*vy;
        var crossY = k.Z*vx - k.X*vz;
        var crossZ = k.X*vy - k.Y*vx;

        var rx = vx*cos + crossX*sin + k.X*dot*(1-cos);
        var ry = vy*cos + crossY*sin + k.Y*dot*(1-cos);
        var rz = vz*cos + crossZ*sin + k.Z*dot*(1-cos);

        return new Point3D(origin.X + rx, origin.Y + ry, origin.Z + rz);
    }

    public static List<ProjectSurface> PanelleriYuzeyeAktar(
        IEnumerable<AcousticPanel> paneller,
        int baslangicId,
        SalonBoyutlari? boyutlar = null)
    {
        var result = new List<ProjectSurface>();
        int id = baslangicId;

        foreach (var p in paneller)
        {
            var surfacePanel = new AcousticPanel
            {
                Id = p.Id,
                Tip = p.Tip,
                DilimNo = p.DilimNo,
                PanelEniM = p.PanelEniM,
                SarkmaCikmaM = p.SarkmaCikmaM,
                AciDerece = p.AciDerece,
                MaterialId = p.MaterialId,
                Vertices = p.Vertices.ToList()
            };
            var rotPanel = PanelDondur(surfacePanel, boyutlar);
            
            string layerName = p.Tip switch
            {
                PanelTipi.Tavan => "TAVAN-PANEL",
                PanelTipi.DuvarSol => "DUVAR-PANEL-SOL",
                PanelTipi.DuvarSag => "DUVAR-PANEL-SAG",
                _ => "PANEL"
            };

            string name = $"{layerName}-{p.DilimNo}";
            
            result.Add(new ProjectSurface(id++, name, layerName, rotPanel.Vertices));
        }

        return result;
    }
}
