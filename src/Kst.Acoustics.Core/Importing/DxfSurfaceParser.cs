using System.Globalization;
using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.Importing;

public sealed class DxfSurfaceParser : ISurfaceImporter
{
    public SurfaceImportFormat Format => SurfaceImportFormat.Dxf;

    public bool TryParse(string text, out SurfaceImportDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        List<DxfPair> pairs;
        try
        {
            pairs = ParsePairs(text);
        }
        catch (Exception)
        {
            return false;
        }

        if (!TryGetSectionRange(pairs, "ENTITIES", out var entitiesStart, out var entitiesEnd)
            || !HasEntity(pairs, "EOF"))
        {
            return false;
        }

        var (unit, detected, code) = ParseUnits(pairs);
        var warnings = new List<string>();

        // BLOCKS içindeki tanımlar, ENTITIES içinde bir INSERT ile gerçekten
        // örneklenmedikçe model geometrisi değildir. Yalnız ENTITIES aralığını
        // ayrıştırmak hem hayalet yüzeyleri önler hem de INSERT dönüşümünü uygulamadan
        // block tanımını yanlış yerde kullanma riskini kaldırır.
        var entityPairs = pairs.Skip(entitiesStart).Take(entitiesEnd - entitiesStart).ToArray();
        var containsInsert = HasEntity(entityPairs, "INSERT");
        if (containsInsert)
        {
            warnings.Add("DXF INSERT/blok referansı içeriyor; blok dönüşümleri desteklenmediği için INSERT öğeleri içe aktarılmadı.");
        }

        var surfaces = ParseEntities(entityPairs, warnings);
        if (surfaces.Count == 0)
        {
            if (containsInsert)
            {
                throw new InvalidDataException(
                    "DXF yalnızca INSERT/blok referansları içeriyor. Blokları CAD uygulamasında patlatıp (explode) dosyayı yeniden içe aktarın.");
            }

            throw new InvalidDataException("DXF içinde desteklenen kapalı yüzey bulunamadı. Desteklenen geometri: 3DFACE, kapalı POLYLINE ve kapalı LWPOLYLINE.");
        }

        // $INSUNITS yoksa gerçek-dünya toleransı henüz bilinmez. Bu aşamada yapılan
        // birleştirme, metre çizilmiş bir dosyayı milimetre sanıp farklı köşeleri
        // kaynaştırabilir. Birim kullanıcı tarafından kesinleştirilene kadar ham
        // yüzeyleri koru; ProjectFactory kesinleşmiş belgeyi FinalizeUnit ile tamamlar.
        if (detected)
        {
            surfaces = MergeCoplanarFaces(
                surfaces,
                Kst.Acoustics.Core.Projects.ProjectScalingService.GetMetersPerUnit(unit),
                warnings);
        }

        document = new SurfaceImportDocument(unit, surfaces, detected, code, warnings.Count > 0 ? warnings : null);
        return true;
    }

    /// <summary>
    /// Birimi sonradan kullanıcı tarafından belirlenen DXF belgesinde birim-duyarlı
    /// topoloji birleştirmesini tamamlar. Metot tekrarlı çağrılmaya uygundur.
    /// </summary>
    public static SurfaceImportDocument FinalizeUnit(SurfaceImportDocument document, SurfaceImportUnit unit)
    {
        ArgumentNullException.ThrowIfNull(document);

        var warnings = document.Warnings?.ToList() ?? [];
        var surfaces = MergeCoplanarFaces(
            document.Surfaces.ToList(),
            Kst.Acoustics.Core.Projects.ProjectScalingService.GetMetersPerUnit(unit),
            warnings);

        return document with
        {
            Unit = unit,
            UnitDetected = true,
            Surfaces = surfaces,
            Warnings = warnings.Count == 0
                ? null
                : warnings.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private static List<DxfPair> ParsePairs(string text)
    {
        var lines = SurfaceImportText.SplitLines(text);
        if (lines.Count % 2 != 0 && !string.IsNullOrWhiteSpace(lines[^1]))
        {
            throw new InvalidDataException($"DXF satır {lines.Count}: eşleşmeyen grup kodu/değeri var.");
        }

        var pairs = new List<DxfPair>(lines.Count / 2);

        for (var i = 0; i < lines.Count - 1; i += 2)
        {
            pairs.Add(new DxfPair(lines[i].Trim(), lines[i + 1].Trim(), i + 1));
        }

        return pairs;
    }

    private static bool TryGetSectionRange(
        IReadOnlyList<DxfPair> pairs,
        string sectionName,
        out int contentStart,
        out int contentEnd)
    {
        contentStart = 0;
        contentEnd = 0;

        for (var i = 0; i < pairs.Count - 1; i++)
        {
            if (pairs[i].Code == "0"
                && pairs[i].Value.Equals("SECTION", StringComparison.OrdinalIgnoreCase)
                && pairs[i + 1].Code == "2"
                && pairs[i + 1].Value.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
            {
                contentStart = i + 2;
                for (var j = contentStart; j < pairs.Count; j++)
                {
                    if (pairs[j].Code == "0"
                        && pairs[j].Value.Equals("ENDSEC", StringComparison.OrdinalIgnoreCase))
                    {
                        contentEnd = j;
                        return true;
                    }
                }

                return false;
            }
        }

        return false;
    }

    private static bool HasEntity(IReadOnlyList<DxfPair> pairs, string entityName)
    {
        return pairs.Any(pair => pair.Code == "0" && pair.Value.Equals(entityName, StringComparison.OrdinalIgnoreCase));
    }

    private static (SurfaceImportUnit Unit, bool Detected, int Code) ParseUnits(IReadOnlyList<DxfPair> pairs)
    {
        for (var i = 0; i < pairs.Count - 1; i++)
        {
            if (pairs[i].Code != "9" || !string.Equals(pairs[i].Value, "$INSUNITS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (pairs[i + 1].Code != "70" || !int.TryParse(pairs[i + 1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unitCode))
            {
                break;
            }

            return unitCode switch
            {
                1 => (SurfaceImportUnit.Inches, true, unitCode),
                2 => (SurfaceImportUnit.Feet, true, unitCode),
                4 => (SurfaceImportUnit.Millimeters, true, unitCode),
                5 => (SurfaceImportUnit.Centimeters, true, unitCode),
                6 => (SurfaceImportUnit.Meters, true, unitCode),
                0 => (SurfaceImportUnit.Millimeters, false, unitCode),
                _ => (SurfaceImportUnit.Millimeters, false, unitCode)
            };
        }

        return (SurfaceImportUnit.Millimeters, false, 0);
    }

    private static List<SurfaceDefinition> ParseEntities(IReadOnlyList<DxfPair> pairs, List<string> warnings)
    {
        var surfaces = new List<SurfaceDefinition>();
        var nextId = 1;

        for (var index = 0; index < pairs.Count; index++)
        {
            var pair = pairs[index];
            if (pair.Code != "0")
            {
                continue;
            }

            if (pair.Value.Equals("3DFACE", StringComparison.OrdinalIgnoreCase))
            {
                var entityEnd = FindNextEntityStart(pairs, index + 1);
                var surface = Parse3dFace(pairs, index + 1, entityEnd, nextId);
                if (surface is not null)
                {
                    surfaces.Add(surface);
                    nextId++;
                }

                index = entityEnd - 1;
                continue;
            }

            if (pair.Value.Equals("LWPOLYLINE", StringComparison.OrdinalIgnoreCase))
            {
                var entityEnd = FindNextEntityStart(pairs, index + 1);
                var surface = ParseLightweightPolyline(pairs, index + 1, entityEnd, nextId, warnings);
                if (surface is not null)
                {
                    surfaces.Add(surface);
                    nextId++;
                }

                index = entityEnd - 1;
                continue;
            }

            if (pair.Value.Equals("POLYLINE", StringComparison.OrdinalIgnoreCase))
            {
                if (IsPolyFaceMesh(pairs, index + 1))
                {
                    var meshSurfaces = ParsePolyFaceMesh(pairs, ref index, ref nextId);
                    surfaces.AddRange(meshSurfaces);
                }
                else
                {
                    var surface = ParsePolyline(pairs, ref index, nextId);
                    if (surface is not null)
                    {
                        surfaces.Add(surface);
                        nextId++;
                    }
                }
                continue;
            }
        }

        return surfaces;
    }

    // --- Coplanar face merging ---

    private static List<SurfaceDefinition> MergeCoplanarFaces(
        List<SurfaceDefinition> faces,
        double metersPerUnit,
        List<string> warnings)
    {
        if (faces.Count <= 1)
        {
            return faces;
        }

        var groups = new Dictionary<string, List<SurfaceDefinition>>(StringComparer.Ordinal);
        var ungrouped = new List<SurfaceDefinition>();

        foreach (var face in faces)
        {
            var key = ComputeCoplanarKey(face, metersPerUnit);
            if (key is null)
            {
                ungrouped.Add(face);
                continue;
            }

            if (!groups.TryGetValue(key, out var g))
            {
                groups[key] = g = [];
            }

            g.Add(face);
        }

        var result = new List<SurfaceDefinition>(ungrouped);

        foreach (var group in groups.Values)
        {
            if (group.Count == 1)
            {
                result.Add(group[0]);
                continue;
            }

            var merged = TraceBoundaryPolygons(group, metersPerUnit, out var containsHoles);
            if (containsHoles)
            {
                // Eski uygulama dış kontur ile delikleri NaN köşe ayıracıyla tek yüzeye
                // sıkıştırıyordu. NaN geçerli JSON sayısı değildir ve alan/dönüşüm/grid
                // hesaplarını zehirler. Delikli birleşimde kaynak basit yüzleri korumak,
                // deliği doldurmadan ve kalıcı modele sentinel sokmadan aynı ağı temsil eder.
                warnings.Add("Delikli eş-düzlemsel bölge güvenli basit yüzeyler olarak korundu; tek çokgene birleştirilmedi.");
                result.AddRange(group);
            }
            else
            {
                result.AddRange(merged.Count > 0 ? merged : group);
            }
        }

        // Renumber IDs sequentially
        for (var i = 0; i < result.Count; i++)
        {
            result[i] = result[i] with { Id = i + 1, Name = $"DXF Surface {i + 1}" };
        }

        return result;
    }

    private static string? ComputeCoplanarKey(SurfaceDefinition face, double metersPerUnit)
    {
        var v = face.Vertices;

        for (var i = 0; i < v.Count - 2; i++)
        {
            if (double.IsNaN(v[i].X) || double.IsNaN(v[i + 1].X) || double.IsNaN(v[i + 2].X))
            {
                continue;
            }

            var ex = v[i + 1].X - v[i].X;
            var ey = v[i + 1].Y - v[i].Y;
            var ez = v[i + 1].Z - v[i].Z;
            var fx = v[i + 2].X - v[i].X;
            var fy = v[i + 2].Y - v[i].Y;
            var fz = v[i + 2].Z - v[i].Z;

            var nx = (ey * fz) - (ez * fy);
            var ny = (ez * fx) - (ex * fz);
            var nz = (ex * fy) - (ey * fx);
            var len = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));

            if (len < 1e-10)
            {
                continue;
            }

            nx /= len; ny /= len; nz /= len;

            // Canonicalize: ensure first significant component is positive so
            // coplanar faces with opposite winding share the same key.
            if (nx < -1e-6
                || (Math.Abs(nx) <= 1e-6 && ny < -1e-6)
                || (Math.Abs(nx) <= 1e-6 && Math.Abs(ny) <= 1e-6 && nz < 0))
            {
                nx = -nx; ny = -ny; nz = -nz;
            }

            var d = (nx * v[i].X) + (ny * v[i].Y) + (nz * v[i].Z);

            // Use integer keys to avoid -0.0 vs 0.0 string mismatch.
            const double NormalQuantization = 10_000.0;
            const double PlaneToleranceMeters = 0.001;
            var knx = (long)Math.Round(nx * NormalQuantization);
            var kny = (long)Math.Round(ny * NormalQuantization);
            var knz = (long)Math.Round(nz * NormalQuantization);
            var kd  = (long)Math.Round((d * metersPerUnit) / PlaneToleranceMeters);

            return $"{face.Category}|{knx},{kny},{knz}|{kd}";
        }

        return null;
    }

    private static List<SurfaceDefinition> TraceBoundaryPolygons(
        List<SurfaceDefinition> group,
        double metersPerUnit,
        out bool containsHoles)
    {
        containsHoles = false;
        // Köşe eşleştirme toleransı gerçek dünyada ~1mm sabit kalacak şekilde proje
        // birimine göre ayarlanır (scale = metre/birim). Sabit 3 ondalık, mm projesinde
        // 1 mikron (çok sıkı — kaynaşması gereken köşeleri ayırır), feet projesinde ise
        // ~0.3mm (çok gevşek) olurdu.
        const double ToleranceMeters = 0.001;
        var toleranceUnits = ToleranceMeters / metersPerUnit;

        string VKey(Point3D p) =>
            FormattableString.Invariant(
                $"{Math.Round(p.X / toleranceUnits)},{Math.Round(p.Y / toleranceUnits)},{Math.Round(p.Z / toleranceUnits)}");

        // Build directed-edge counts and a vertex lookup.
        var dirCount = new Dictionary<(string, string), int>();
        var ptByKey  = new Dictionary<string, Point3D>(StringComparer.Ordinal);

        var groupNormal = ComputePolygonNormal(group[0].Vertices.Where(IsFinite).ToList());

        foreach (var face in group)
        {
            var verts = face.Vertices.Where(IsFinite).ToList();
            var faceNormal = ComputePolygonNormal(verts);
            if (GeometryMath.Dot(groupNormal, faceNormal) < 0)
            {
                verts.Reverse();
            }

            var n = verts.Count;
            for (var i = 0; i < n; i++)
            {
                var a  = verts[i];
                var b  = verts[(i + 1) % n];
                var ak = VKey(a);
                var bk = VKey(b);
                ptByKey.TryAdd(ak, a);
                ptByKey.TryAdd(bk, b);
                var ekey = (ak, bk);
                dirCount.TryGetValue(ekey, out var c);
                dirCount[ekey] = c + 1;
            }
        }

        // Sınır yönlü kenarları: A→B var ama B→A yoksa sınırdadır. Bir köşeden birden
        // fazla sınır kenarı çıkabilir (T-junction — üç veya daha çok yüzeyin ortak bir
        // köşesi); tekil hedef yerine adjacency list kullanılır, aksi halde
        // Dictionary<string,string> bir kenarı sessizce ezerdi.
        var boundary = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var ((from, to), _) in dirCount)
        {
            if (!dirCount.ContainsKey((to, from)))
            {
                if (!boundary.TryGetValue(from, out var list))
                    boundary[from] = list = [];
                list.Add(to);
            }
        }

        if (boundary.Count == 0)
        {
            return [];
        }

        // T-junction'da doğru devam kenarını seçmek için düzlemsel 2D izdüşüm gerekir
        // (grup zaten eş-düzlemsel — ComputeCoplanarKey ile gruplanmıştı).
        var groupAxis   = DominantAxis(groupNormal);
        var pos2D = ptByKey.ToDictionary(kv => kv.Key, kv => ProjectTo2D(kv.Value, groupAxis), StringComparer.Ordinal);

        // Trace closed loops from boundary edges. Kenar bazlı ziyaret kullanılır (köşe
        // bazlı değil) çünkü T-junction'ın paylaşılan köşesi birden fazla halkada yer alır.
        var visitedEdges = new HashSet<(string From, string To)>();
        var polygons = new List<SurfaceDefinition>();
        var category = group[0].Category;
        var loops = new List<List<Point3D>>();

        foreach (var startFrom in boundary.Keys)
        {
            foreach (var startTo in boundary[startFrom])
            {
                if (visitedEdges.Contains((startFrom, startTo)))
                {
                    continue;
                }

                var chain = new List<string> { startFrom };
                visitedEdges.Add((startFrom, startTo));
                var prev = startFrom;
                var cur  = startTo;

                while (cur != startFrom)
                {
                    chain.Add(cur);
                    if (!boundary.TryGetValue(cur, out var options))
                    {
                        break; // çıkmaz sokak — zincir kapanmadı
                    }

                    var next = PickNextBoundaryEdge(prev, cur, options, visitedEdges, pos2D);
                    if (next is null)
                    {
                        break; // bu köşeden ziyaret edilmemiş kenar kalmadı
                    }

                    visitedEdges.Add((cur, next));
                    prev = cur;
                    cur  = next;
                }

                if (cur != startFrom || chain.Count < 3)
                {
                    continue; // döngü kapanmadı
                }

                var pts = chain.Select(k => ptByKey[k]).ToList();
                RemoveCollinear(pts, toleranceUnits);

                if (pts.Count >= 3)
                {
                    loops.Add(pts);
                }
            }
        }

        if (loops.Count == 0)
        {
            return polygons;
        }

        // Alanlarına göre büyükten küçüğe sırala
        loops.Sort((a, b) => ComputeCoplanarArea(b).CompareTo(ComputeCoplanarArea(a)));

        var normal = ComputePolygonNormal(loops[0]);
        var axis = DominantAxis(normal);
        var loops2D = loops.Select(l => l.Select(p => ProjectTo2D(p, axis)).ToList()).ToList();

        var parents = new int[loops.Count];
        var depths = new int[loops.Count];

        for (int i = 0; i < loops.Count; i++)
        {
            parents[i] = -1;
            depths[i] = 0;
            for (int j = i - 1; j >= 0; j--)
            {
                if (IsPointInPolygon2D(loops2D[i][0], loops2D[j]))
                {
                    parents[i] = j;
                    depths[i] = depths[j] + 1;
                    break; // En küçük kapsayan ebeveyn
                }
            }
        }

        containsHoles = depths.Any(depth => depth % 2 == 1);
        if (containsHoles)
        {
            return [];
        }

        // Delik içermeyen bağımsız dış konturları üret. Delikli durumda yukarıda ham
        // basit yüzlere dönüldüğü için kalıcı modelde NaN ayıracı oluşmaz.
        for (int i = 0; i < loops.Count; i++)
        {
            if (depths[i] == 0)
            {
                polygons.Add(new SurfaceDefinition(0, string.Empty, category, loops[i]));
            }
        }

        return polygons;
    }

    /// <summary>
    /// T-junction'da (cur'den birden fazla sınır kenarı çıktığında) gelen kenarın yönünden
    /// en küçük SOLA (CCW) dönüşü yapan seçeneği döner. CCW yönlü bir kapalı poligonun her
    /// köşesinde iç kısım her zaman soldadır; bu yüzden en küçük sol dönüş her zaman AYNI
    /// poligonun devamıdır — sağa (büyük CCW açı) dönmek başka bir yüzeyin sınırına
    /// sıçramak anlamına gelir. Düzlemsel altbölümlerde yüz sınırı izlemenin standart
    /// kuralıdır. Tek seçenek varsa (T-junction yoksa) doğrudan onu döner.
    /// </summary>
    private static string? PickNextBoundaryEdge(
        string prev, string cur, List<string> candidates,
        HashSet<(string From, string To)> visitedEdges,
        Dictionary<string, (double x, double y)> pos2D)
    {
        var unvisited = candidates.Where(c => !visitedEdges.Contains((cur, c))).ToList();
        if (unvisited.Count == 0) return null;
        if (unvisited.Count == 1) return unvisited[0];

        var (px, py) = pos2D[prev];
        var (cx, cy) = pos2D[cur];
        var inAngle = Math.Atan2(cy - py, cx - px);

        string? best = null;
        var bestTurn = double.MaxValue;
        foreach (var candidate in unvisited)
        {
            var (nx, ny) = pos2D[candidate];
            var outAngle = Math.Atan2(ny - cy, nx - cx);
            // [0, 2π) aralığına sar: gelen yönden ne kadar sola (CCW) dönüldüğü.
            var turn = ((outAngle - inAngle) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
            if (turn < bestTurn)
            {
                bestTurn = turn;
                best = candidate;
            }
        }
        return best;
    }

    private static Vector3D ComputePolygonNormal(List<Point3D> verts)
    {
        double nx = 0, ny = 0, nz = 0;
        for (int i = 0; i < verts.Count; i++)
        {
            var cur = verts[i];
            var next = verts[(i + 1) % verts.Count];
            nx += (cur.Y - next.Y) * (cur.Z + next.Z);
            ny += (cur.Z - next.Z) * (cur.X + next.X);
            nz += (cur.X - next.X) * (cur.Y + next.Y);
        }
        var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return len < 1e-12 ? new Vector3D(0, 0, 1) : new Vector3D(nx / len, ny / len, nz / len);
    }

    private static int DominantAxis(Vector3D n)
    {
        var ax = Math.Abs(n.X);
        var ay = Math.Abs(n.Y);
        var az = Math.Abs(n.Z);
        if (ax >= ay && ax >= az) return 0;
        return ay >= az ? 1 : 2;
    }

    private static (double x, double y) ProjectTo2D(Point3D p, int droppedAxis) => droppedAxis switch
    {
        0 => (p.Y, p.Z),
        1 => (p.X, p.Z),
        _ => (p.X, p.Y)
    };

    private static bool IsPointInPolygon2D((double x, double y) p, List<(double x, double y)> poly)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
            {
                c = !c;
            }
        }
        return c;
    }

    private static double ComputeCoplanarArea(List<Point3D> poly)
    {
        double ax = 0, ay = 0, az = 0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            ax += (poly[j].Y - poly[i].Y) * (poly[j].Z + poly[i].Z);
            ay += (poly[j].Z - poly[i].Z) * (poly[j].X + poly[i].X);
            az += (poly[j].X - poly[i].X) * (poly[j].Y + poly[i].Y);
        }
        return 0.5 * Math.Sqrt((ax * ax) + (ay * ay) + (az * az));
    }

    private static void RemoveCollinear(List<Point3D> pts, double toleranceUnits)
    {
        for (var i = pts.Count - 1; i >= 0 && pts.Count > 3; i--)
        {
            var prev = (i - 1 + pts.Count) % pts.Count;
            var next = (i + 1) % pts.Count;

            var ax = pts[i].X - pts[prev].X;
            var ay = pts[i].Y - pts[prev].Y;
            var az = pts[i].Z - pts[prev].Z;
            var bx = pts[next].X - pts[i].X;
            var by = pts[next].Y - pts[i].Y;
            var bz = pts[next].Z - pts[i].Z;

            var cx = (ay * bz) - (az * by);
            var cy = (az * bx) - (ax * bz);
            var cz = (ax * by) - (ay * bx);

            var crossLength = Math.Sqrt((cx * cx) + (cy * cy) + (cz * cz));
            var chordLength = Math.Sqrt(
                Math.Pow(pts[next].X - pts[prev].X, 2)
                + Math.Pow(pts[next].Y - pts[prev].Y, 2)
                + Math.Pow(pts[next].Z - pts[prev].Z, 2));

            // Üçüncü köşenin komşular arasındaki doğruya fiziksel uzaklığı.
            if (chordLength > 1e-12 && crossLength / chordLength <= toleranceUnits)
            {
                pts.RemoveAt(i);
            }
        }
    }

    private static bool IsFinite(Point3D point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static SurfaceDefinition? Parse3dFace(IReadOnlyList<DxfPair> pairs, int start, int end, int id)
    {
        var layer = "0";
        var coordinates = new Dictionary<int, PointAccumulator>();

        for (var i = start; i < end; i++)
        {
            var pair = pairs[i];
            switch (pair.Code)
            {
                case "8":
                    layer = pair.Value;
                    break;
                case "10":
                case "20":
                case "30":
                case "11":
                case "21":
                case "31":
                case "12":
                case "22":
                case "32":
                case "13":
                case "23":
                case "33":
                    ParseFaceCoordinate(pair, coordinates);
                    break;
            }
        }

        var vertices = coordinates.OrderBy(x => x.Key)
            .Select(x => x.Value.ToPoint())
            .Where(x => x is not null)
            .Cast<Point3D>()
            .ToList();

        NormalizeVertices(vertices);
        if (vertices.Count < 3)
        {
            return null;
        }

        return new SurfaceDefinition(id, $"DXF Surface {id}", layer, vertices);
    }

    private static SurfaceDefinition? ParseLightweightPolyline(IReadOnlyList<DxfPair> pairs, int start, int end, int id, List<string> warnings)
    {
        var layer = "0";
        var isClosed = false;
        var elevation = 0.0;
        double? pendingX = null;
        var vertices = new List<Point3D>();

        for (var i = start; i < end; i++)
        {
            var pair = pairs[i];
            switch (pair.Code)
            {
                case "8":
                    layer = pair.Value;
                    break;
                case "38":
                    elevation = ParseDouble(pair);
                    break;
                case "70":
                    isClosed = (ParseInt(pair) & 1) == 1;
                    break;
                case "10":
                    pendingX = ParseDouble(pair);
                    break;
                case "20":
                    if (pendingX is not null)
                    {
                        vertices.Add(new Point3D(pendingX.Value, ParseDouble(pair), elevation));
                        pendingX = null;
                    }
                    break;
                case "42":
                    var bulge = ParseDouble(pair);
                    if (Math.Abs(bulge) > 1e-5 && !warnings.Contains("LWPOLYLINE yay (bulge) içeriyor; yaylar düz çizgiye dönüştürüldü."))
                    {
                        warnings.Add("LWPOLYLINE yay (bulge) içeriyor; yaylar düz çizgiye dönüştürüldü.");
                    }
                    break;
            }
        }

        if (!isClosed)
        {
            return null;
        }

        NormalizeVertices(vertices);
        if (vertices.Count < 3)
        {
            return null;
        }

        return new SurfaceDefinition(id, $"DXF Surface {id}", layer, vertices);
    }

    private static bool IsPolyFaceMesh(IReadOnlyList<DxfPair> pairs, int start)
    {
        var headerEnd = FindChildOrNextEntityStart(pairs, start);
        for (var i = start; i < headerEnd; i++)
        {
            if (pairs[i].Code == "70")
            {
                return (ParseInt(pairs[i]) & 64) == 64;
            }
        }
        return false;
    }

    private static List<SurfaceDefinition> ParsePolyFaceMesh(IReadOnlyList<DxfPair> pairs, ref int index, ref int id)
    {
        var surfaces = new List<SurfaceDefinition>();
        var headerStart = index + 1;
        var headerEnd = FindChildOrNextEntityStart(pairs, headerStart);
        var layer = "0";

        for (var i = headerStart; i < headerEnd; i++)
        {
            if (pairs[i].Code == "8") layer = pairs[i].Value;
        }

        var vertices = new List<Point3D>();
        var cursor = headerEnd;

        while (cursor < pairs.Count)
        {
            if (pairs[cursor].Code != "0")
            {
                cursor++;
                continue;
            }

            if (pairs[cursor].Value.Equals("VERTEX", StringComparison.OrdinalIgnoreCase))
            {
                var vertexEnd = FindNextEntityStart(pairs, cursor + 1);
                
                bool isFace = false;
                for (var i = cursor + 1; i < vertexEnd; i++)
                {
                    if (pairs[i].Code == "70")
                    {
                        var flags = ParseInt(pairs[i]);
                        if (flags == 128)
                        {
                            isFace = true;
                        }
                        break;
                    }
                }

                if (!isFace)
                {
                    var point = ParseVertex(pairs, cursor + 1, vertexEnd);
                    if (point is not null) vertices.Add(point.Value);
                }
                else
                {
                    var faceIndices = new List<int>();
                    for (var i = cursor + 1; i < vertexEnd; i++)
                    {
                        var code = pairs[i].Code;
                        if (code is "71" or "72" or "73" or "74")
                        {
                            var idx = Math.Abs(ParseInt(pairs[i]));
                            if (idx > 0 && idx <= vertices.Count)
                            {
                                faceIndices.Add(idx - 1);
                            }
                        }
                    }

                    if (faceIndices.Count >= 3)
                    {
                        var faceVertices = faceIndices.Select(idx => vertices[idx]).ToList();
                        NormalizeVertices(faceVertices);
                        if (faceVertices.Count >= 3)
                        {
                            surfaces.Add(new SurfaceDefinition(id, $"DXF Mesh Face {id}", layer, faceVertices));
                            id++;
                        }
                    }
                }

                cursor = vertexEnd;
                continue;
            }

            if (pairs[cursor].Value.Equals("SEQEND", StringComparison.OrdinalIgnoreCase))
            {
                index = cursor;
                break;
            }

            index = cursor - 1;
            break;
        }

        return surfaces;
    }

    private static SurfaceDefinition? ParsePolyline(IReadOnlyList<DxfPair> pairs, ref int index, int id)
    {
        var headerStart = index + 1;
        var headerEnd = FindChildOrNextEntityStart(pairs, headerStart);
        var layer = "0";
        var isClosed = false;

        for (var i = headerStart; i < headerEnd; i++)
        {
            var pair = pairs[i];
            switch (pair.Code)
            {
                case "8":
                    layer = pair.Value;
                    break;
                case "70":
                    isClosed = (ParseInt(pair) & 1) == 1;
                    break;
            }
        }

        var vertices = new List<Point3D>();
        var cursor = headerEnd;

        while (cursor < pairs.Count)
        {
            if (pairs[cursor].Code != "0")
            {
                cursor++;
                continue;
            }

            if (pairs[cursor].Value.Equals("VERTEX", StringComparison.OrdinalIgnoreCase))
            {
                var vertexEnd = FindNextEntityStart(pairs, cursor + 1);
                var point = ParseVertex(pairs, cursor + 1, vertexEnd);
                if (point is not null)
                {
                    vertices.Add(point.Value);
                }

                cursor = vertexEnd;
                continue;
            }

            if (pairs[cursor].Value.Equals("SEQEND", StringComparison.OrdinalIgnoreCase))
            {
                index = cursor;
                break;
            }

            index = cursor - 1;
            break;
        }

        if (!isClosed)
        {
            return null;
        }

        NormalizeVertices(vertices);
        if (vertices.Count < 3)
        {
            return null;
        }

        return new SurfaceDefinition(id, $"DXF Surface {id}", layer, vertices);
    }

    private static Point3D? ParseVertex(IReadOnlyList<DxfPair> pairs, int start, int end)
    {
        double? x = null;
        double? y = null;
        double? z = null;

        for (var i = start; i < end; i++)
        {
            var pair = pairs[i];
            switch (pair.Code)
            {
                case "10":
                    x = ParseDouble(pair);
                    break;
                case "20":
                    y = ParseDouble(pair);
                    break;
                case "30":
                    z = ParseDouble(pair);
                    break;
            }
        }

        if (x is null || y is null)
        {
            return null;
        }

        return new Point3D(x.Value, y.Value, z ?? 0.0);
    }

    private static void ParseFaceCoordinate(DxfPair pair, IDictionary<int, PointAccumulator> coordinates)
    {
        var vertexIndex = pair.Code[1] - '0';
        if (!coordinates.TryGetValue(vertexIndex, out var accumulator))
        {
            accumulator = new PointAccumulator();
            coordinates[vertexIndex] = accumulator;
        }

        switch (pair.Code[0])
        {
            case '1':
                accumulator.X = ParseDouble(pair);
                break;
            case '2':
                accumulator.Y = ParseDouble(pair);
                break;
            case '3':
                accumulator.Z = ParseDouble(pair);
                break;
        }
    }

    private static void NormalizeVertices(List<Point3D> vertices)
    {
        for (var i = vertices.Count - 1; i > 0; i--)
        {
            if (vertices[i] == vertices[i - 1])
            {
                vertices.RemoveAt(i);
            }
        }

        if (vertices.Count > 1 && vertices[0] == vertices[^1])
        {
            vertices.RemoveAt(vertices.Count - 1);
        }
    }

    private static int FindNextEntityStart(IReadOnlyList<DxfPair> pairs, int start)
    {
        for (var i = start; i < pairs.Count; i++)
        {
            if (pairs[i].Code == "0")
            {
                return i;
            }
        }

        return pairs.Count;
    }

    private static int FindChildOrNextEntityStart(IReadOnlyList<DxfPair> pairs, int start)
    {
        for (var i = start; i < pairs.Count; i++)
        {
            if (pairs[i].Code == "0" &&
                (pairs[i].Value.Equals("VERTEX", StringComparison.OrdinalIgnoreCase)
                || pairs[i].Value.Equals("SEQEND", StringComparison.OrdinalIgnoreCase)
                || pairs[i].Value.Equals("POLYLINE", StringComparison.OrdinalIgnoreCase)
                || pairs[i].Value.Equals("3DFACE", StringComparison.OrdinalIgnoreCase)
                || pairs[i].Value.Equals("LWPOLYLINE", StringComparison.OrdinalIgnoreCase)))
            {
                return i;
            }
        }

        return pairs.Count;
    }

    private static int ParseInt(DxfPair pair)
    {
        if (int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new SurfaceImportParseException($"Sayı değeri bekleniyordu: {pair.Value}", pair.LineNumber);
    }

    private static double ParseDouble(DxfPair pair)
    {
        if (double.TryParse(pair.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new SurfaceImportParseException($"Ondalık sayı bekleniyordu: {pair.Value}", pair.LineNumber);
    }

    private sealed record DxfPair(string Code, string Value, int LineNumber);

    private sealed class PointAccumulator
    {
        public double? X { get; set; }

        public double? Y { get; set; }

        public double? Z { get; set; }

        public Point3D? ToPoint()
        {
            if (X is null || Y is null || Z is null)
            {
                return null;
            }

            return new Point3D(X.Value, Y.Value, Z.Value);
        }
    }
}
