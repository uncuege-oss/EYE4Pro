using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Kst.Acoustics.Metraj.Io;
using Kst.Acoustics.Metraj.Filters;
using Kst.Acoustics.Metraj.Counting;
using Kst.Acoustics.Metraj.Metrics;
using Kst.Acoustics.Metraj.Reporting;
using Kst.Acoustics.Metraj.Rules;
using Kst.Acoustics.Metraj.Template;
using Kst.Acoustics.Metraj.Models;

namespace Kst.Acoustics.Metraj.App;

public class MainViewModel : INotifyPropertyChanged
{
    private string _projectPath = string.Empty;
    private double _lengthTolerance = 2.0;
    private double _angleTolerance = 5.0;
    private MetrajUnit _selectedUnit = MetrajUnit.Millimeters;
    private string _statusText = "Hazır";
    private double _progressValue = 0;
    private string _resultFilePath = string.Empty;
    private bool _isBusy;
    private CancellationTokenSource? _cts;

    private readonly MetrajPointComparer _pointComparer = new MetrajPointComparer(0.01);

    public ObservableCollection<string> TemplatePaths { get; } = new();
    public List<MetrajUnit> Units { get; } = Enum.GetValues<MetrajUnit>().ToList();

    public string ProjectPath
    {
        get => _projectPath;
        set 
        {
            if (SetProperty(ref _projectPath, value))
            {
                LoadProjectSettings();
            }
        }
    }

    private void LoadProjectSettings()
    {
        if (string.IsNullOrEmpty(ProjectPath)) return;
        var ruleSet = RuleSetStore.Load(ProjectPath);
        LengthTolerance = ruleSet.LengthTolerance;
        AngleTolerance = ruleSet.AngleTolerance;
        SelectedUnit = ruleSet.TargetUnit;
        OnlyTemplates = ruleSet.OnlyTemplates;
        TemplatePaths.Clear();
        foreach (var p in ruleSet.TemplatePaths) TemplatePaths.Add(p);
    }

    public double LengthTolerance
    {
        get => _lengthTolerance;
        set => SetProperty(ref _lengthTolerance, value);
    }

    public double AngleTolerance
    {
        get => _angleTolerance;
        set => SetProperty(ref _angleTolerance, value);
    }

    public MetrajUnit SelectedUnit
    {
        get => _selectedUnit;
        set => SetProperty(ref _selectedUnit, value);
    }

    private bool _onlyTemplates;
    public bool OnlyTemplates
    {
        get => _onlyTemplates;
        set => SetProperty(ref _onlyTemplates, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public string ResultFilePath
    {
        get => _resultFilePath;
        set { SetProperty(ref _resultFilePath, value); OnPropertyChanged(nameof(HasResult)); }
    }

    public bool HasResult => !string.IsNullOrEmpty(ResultFilePath);

    public bool IsBusy
    {
        get => _isBusy;
        set { SetProperty(ref _isBusy, value); OnPropertyChanged(nameof(CanRun)); OnPropertyChanged(nameof(CanCancel)); }
    }

    public bool CanRun => !IsBusy;
    public bool CanCancel => IsBusy;

    public ICommand RemoveTemplateCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenResultCommand { get; }

    public MainViewModel()
    {
        RemoveTemplateCommand = new RelayCommand<string>(RemoveTemplate);
        RunCommand = new RelayCommand(async () => await RunMetrajAsync());
        CancelCommand = new RelayCommand(() => { _cts?.Cancel(); StatusText = "İptal ediliyor..."; });
        OpenResultCommand = new RelayCommand(OpenResult);
    }

    private void RemoveTemplate(string? path) 
    { 
        if (path != null) TemplatePaths.Remove(path); 
    }

    private void OpenResult()
    {
        if (string.IsNullOrEmpty(ResultFilePath)) return;
        try
        {
            var p = new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(ResultFilePath) { UseShellExecute = true } };
            p.Start();
        }
        catch (Exception ex)
        {
            StatusText = $"Dosya açılamadı: {ex.Message}";
        }
    }

    private async Task RunMetrajAsync()
    {
        if (string.IsNullOrEmpty(ProjectPath))
        {
            StatusText = "Hata: Proje dosyası seçilmedi.";
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsBusy = true;
        ProgressValue = 0;
        ResultFilePath = string.Empty;

        try
        {
            StatusText = "Hazırlanıyor...";
            await Task.Delay(50, ct);

            // 1. Dönüştürme
            string workingDxf = ProjectPath;
            if (Path.GetExtension(ProjectPath).Equals(".dwg", StringComparison.OrdinalIgnoreCase))
            {
                StatusText = "DWG dosyası dönüştürülüyor...";
                var config = Kst.Acoustics.Metraj.Config.ConfigStore.Load();

                if (string.IsNullOrEmpty(config.OdaConverterPath))
                {
                    StatusText = "Hata: ODA File Converter yolu ayarlanmamış. Ayarlar → ODA yolunu belirtin, veya doğrudan DXF dosyası kullanın.";
                    IsBusy = false;
                    return;
                }

                var converter = new DwgToDxfConverter(config.OdaConverterPath);

                if (!converter.IsConverterAvailable())
                {
                    StatusText = $"Hata: ODA File Converter bulunamadı: {converter.ConverterPath}. Ayarlardan doğru yolu seçin.";
                    IsBusy = false;
                    return;
                }

                workingDxf = await converter.ConvertAsync(ProjectPath);
            }
            ProgressValue = 20;

            // 2. Yükleme
            StatusText = "Proje dosyası yükleniyor...";
            var loader = new DxfDocumentLoader();
            var project = await Task.Run(() => loader.Load(workingDxf), ct);
            ProgressValue = 40;

            // 3. Şablonlar ve Kurallar
            StatusText = "Kurallar ve şablonlar işleniyor...";
            var ruleSet = RuleSetStore.Load(ProjectPath);
            
            // Push current UI values to ruleSet for processing and saving
            ruleSet.LengthTolerance = LengthTolerance;
            ruleSet.AngleTolerance = AngleTolerance;
            ruleSet.TargetUnit = SelectedUnit;
            ruleSet.OnlyTemplates = OnlyTemplates;
            ruleSet.TemplatePaths = TemplatePaths.ToList();
            
            // Save updated settings back to project
            RuleSetStore.Save(ProjectPath, ruleSet);

            IMetrajFilter? masterFilter = null;
            if (ruleSet.Rules.Count > 0)
            {
                var comb = new FilterCombinator(ruleSet.RulesCombinator);
                foreach (var rule in ruleSet.Rules)
                {
                    var ruleComb = new FilterCombinator(rule.Combinator);
                    if (!string.IsNullOrEmpty(rule.LayerPattern))
                        ruleComb.Add(new LayerFilter(rule.LayerPattern, true));
                    if (rule.ColorAci.HasValue)
                        ruleComb.Add(new ColorFilter(rule.ColorAci.Value));
                    if (rule.ColorR.HasValue)
                        ruleComb.Add(new ColorFilter(rule.ColorR.Value, rule.ColorG ?? 0, rule.ColorB ?? 0));
                    if (!string.IsNullOrEmpty(rule.TextPattern))
                        ruleComb.Add(new TextFilter(rule.TextPattern, true));
                    
                    comb.Add(ruleComb);
                }
                masterFilter = comb;
            }

            var lib = new TemplateLibrary();
            var allTemplatePaths = TemplatePaths.Union(ruleSet.TemplatePaths).Distinct().ToList();
            var multipartInfo = new Dictionary<string, int>();

            foreach (var tPath in allTemplatePaths)
            {
                if (!File.Exists(tPath)) continue;
                StatusText = $"Şablon yükleniyor: {Path.GetFileName(tPath)}...";
                var tProject = await Task.Run(() => loader.Load(tPath), ct);
                string fileName = Path.GetFileNameWithoutExtension(tPath);
                
                var texts = tProject.Entities.Where(e => e is MetrajText or MetrajMText).ToList();
                var geometryOnly = tProject.Entities.Where(e => e is not MetrajText and not MetrajMText).ToList();
                var islands = FindConnectedComponents(geometryOnly);

                var namedGroups = new Dictionary<string, List<List<MetrajPoint>>>();
                foreach (var island in islands)
                {
                    if (island.Count < 2) continue;
                    
                    var minX = island.Min(p => p.X);
                    var maxX = island.Max(p => p.X);
                    var minY = island.Min(p => p.Y);
                    var maxY = island.Max(p => p.Y);
                    var islandCenter = new MetrajPoint((minX + maxX) / 2, (minY + maxY) / 2, 0);
                    
                    string tName = fileName;
                    double minDist = double.MaxValue;
                    double diagonal = Math.Sqrt(Math.Pow(maxX - minX, 2) + Math.Pow(maxY - minY, 2));
                    
                    foreach (var txt in texts)
                    {
                        var tPos = txt.InsertionPoint ?? new MetrajPoint(0,0,0);
                        double d = Distance(islandCenter, tPos);
                        if (d < minDist) { minDist = d; tName = txt switch { MetrajText mt => mt.Value, MetrajMText mm => mm.Value, _ => fileName }; }
                    }
                    
                    if (minDist > Math.Max(1000, diagonal * 3)) tName = fileName;

                    if (!namedGroups.TryGetValue(tName, out var groupList)) { groupList = new List<List<MetrajPoint>>(); namedGroups[tName] = groupList; }
                    groupList.Add(island);
                }

                foreach (var group in namedGroups)
                {
                    string tName = group.Key;
                    var groupIslands = group.Value;
                    if (groupIslands.Count == 1)
                    {
                        lib.AddTemplate(tName, groupIslands[0], ruleSet.LengthTolerance, ruleSet.AngleTolerance);
                    }
                    else
                    {
                        for (int i = 0; i < groupIslands.Count; i++)
                        {
                            lib.AddTemplate($"{tName}:::Part:::{i+1}", groupIslands[i], ruleSet.LengthTolerance, ruleSet.AngleTolerance);
                        }
                    }
                }
            }
            ProgressValue = 60;

            // 4. Sayım ve Metraj
            StatusText = "Sayım ve metraj hesaplanıyor...";
            var matcher = new TemplateMatcher(lib);
            var blockCounter = new BlockCounter();
            var lengthCalc = new LengthCalculator();

            var filteredEntities = masterFilter != null ? project.Entities.Where(masterFilter.IsMatch).ToList() : project.Entities;
            var blockCounts = blockCounter.Count(project);
            var totalCounts = new Dictionary<BlockCountKey, int>();

            // A) Match against Block Definitions first (High precision, high speed)
            var blockNameToMatches = new Dictionary<string, List<TemplateMatchResult>>();
            foreach (var blockDef in project.BlockDefinitions)
            {
                ct.ThrowIfCancellationRequested();
                var blockMatches = matcher.Match(blockDef.Value);
                if (blockMatches.Count > 0)
                {
                    blockNameToMatches[blockDef.Key] = blockMatches;
                }
            }

            // B) Count matched blocks
            foreach (var kv in blockCounts)
            {
                var blockKey = kv.Key;
                if (blockNameToMatches.TryGetValue(blockKey.BlockName, out var matchesInBlock))
                {
                    // This block contains matches!
                    foreach (var match in matchesInBlock)
                    {
                        var key = new BlockCountKey(match.TemplateName, blockKey.LayerName, blockKey.Color, blockKey.AttributesHash);
                        int incrementalCount = kv.Value * match.Count;
                        if (totalCounts.ContainsKey(key)) totalCounts[key] += incrementalCount;
                        else totalCounts[key] = incrementalCount;
                    }
                }
                else if (!OnlyTemplates)
                {
                    // Regular block, only count if not in OnlyTemplates mode
                    var dummyInsert = new MetrajInsert(blockKey.LayerName, blockKey.Color, blockKey.BlockName, new MetrajPoint(0,0,0));
                    if (masterFilter == null || masterFilter.IsMatch(dummyInsert))
                    {
                        if (totalCounts.ContainsKey(blockKey)) totalCounts[blockKey] += kv.Value;
                        else totalCounts[blockKey] = kv.Value;
                    }
                }
            }

            // C) Match against Loose Geometry (Low noise)
            var matchableEntities = filteredEntities.Where(e => e is not MetrajInsert && !e.IsFromBlock).ToList();
            var rawMatches = await Task.Run(() => matcher.Match(
                matchableEntities,
                ct,
                msg => Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = msg)), ct);

            // Diagnostic: Update status with raw match summary
            var rawSummary = rawMatches.GroupBy(m => m.TemplateName)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();
            if (rawSummary.Count > 0)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = "İşleniyor: " + string.Join(", ", rawSummary));

            multipartInfo.Clear();
            foreach (var t in lib.GetSortedTemplates())
            {
                if (t.Name.Contains(":::Part:::"))
                {
                    string baseName = t.Name.Substring(0, t.Name.IndexOf(":::Part:::"));
                    if (!multipartInfo.ContainsKey(baseName)) multipartInfo[baseName] = 0;
                    multipartInfo[baseName]++;
                }
            }

            // Multipart spatial grouping
            var multipartGroups = new Dictionary<string, List<TemplateMatchResult>>();
            var standaloneMatches = new List<TemplateMatchResult>();

            foreach (var match in rawMatches)
            {
                if (match.TemplateName.Contains(":::Part:::"))
                {
                    string baseName = match.TemplateName.Substring(0, match.TemplateName.IndexOf(":::Part:::"));
                    if (!multipartGroups.TryGetValue(baseName, out var list)) { list = new List<TemplateMatchResult>(); multipartGroups[baseName] = list; }
                    list.Add(match);
                }
                else
                {
                    standaloneMatches.Add(match);
                }
            }

            foreach (var match in standaloneMatches)
            {
                var key = new BlockCountKey(match.TemplateName, match.LayerName, new MetrajColor(7), "");
                if (totalCounts.ContainsKey(key)) totalCounts[key] += match.Count;
                else totalCounts[key] = match.Count;
            }

            var reportData = new MetrajReportData
            {
                ProjectName = project.Name,
                RuleSet = ruleSet,
                ExportDate = DateTime.Now
            };

            foreach (var group in multipartGroups)
            {
                string baseName = group.Key;
                int requiredParts = multipartInfo[baseName];
                
                // radius = 2.0x diagonal to be safe
                double clusterRadius = lib.GetMaxBoundingDiagonal(baseName) * 2.0;
                if (clusterRadius < 1.0) clusterRadius = 100.0;
                
                var clusters = ClusterMatchesByProximity(group.Value, clusterRadius);
                
                int totalFullTemplates = 0;
                foreach (var cluster in clusters)
                {
                    var distinctPartsPresent = cluster.Select(m => m.TemplateName).Distinct().Count();
                    
                    if (distinctPartsPresent == requiredParts)
                    {
                        // FULL MATCH FOUND
                        var partFreqs = cluster.GroupBy(m => m.TemplateName).Select(g => g.Count());
                        totalFullTemplates += partFreqs.Min();
                    }
                    // Else: IGNORE partial matches to avoid false positives (like squares without circles)
                }

                if (totalFullTemplates > 0)
                {
                    var key = new BlockCountKey(baseName, group.Value[0].LayerName, new MetrajColor(7), "");
                    if (totalCounts.ContainsKey(key)) totalCounts[key] += totalFullTemplates;
                    else totalCounts[key] = totalFullTemplates;
                }
            }

            // Layer bazlı metraj
            var layerLengths = lengthCalc.CalculateByLayer(filteredEntities);
            foreach (var layer in layerLengths)
            {
                int count = totalCounts.Where(c => c.Key.LayerName == layer.Key).Sum(c => c.Value);
                reportData.LayerResults.Add(new MetrajReportRow 
                { 
                    Name = layer.Key, 
                    Count = count, 
                    Length = UnitConverter.Convert(layer.Value, project.SourceUnit, SelectedUnit),
                    Unit = SelectedUnit.ToString()
                });
            }

            // Renk bazlı metraj (Fix A4: count = entity count for that color)
            var colorLengths = lengthCalc.CalculateByColor(filteredEntities);
            var colorEntityCounts = filteredEntities
                .GroupBy(e => {
                    var c = e.EffectiveColor ?? e.Color;
                    return c.R.HasValue ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : c.Aci.ToString();
                })
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var color in colorLengths)
            {
                int entityCount = colorEntityCounts.GetValueOrDefault(color.Key, 0);

                reportData.ColorResults.Add(new MetrajReportRow
                {
                    Name = color.Key,
                    Count = entityCount,
                    Length = UnitConverter.Convert(color.Value, project.SourceUnit, SelectedUnit),
                    Unit = SelectedUnit.ToString()
                });
            }

            // Şablon sonuçları
            foreach (var kv in totalCounts)
            {
                if (reportData.TemplateResults.ContainsKey(kv.Key.BlockName)) reportData.TemplateResults[kv.Key.BlockName] += kv.Value;
                else reportData.TemplateResults[kv.Key.BlockName] = kv.Value;
            }
            ProgressValue = 80;

            // 5. Rapor Çıktısı
            StatusText = "Excel raporu oluşturuluyor...";
            string xlsxPath = Path.Combine(Path.GetDirectoryName(ProjectPath)!, Path.GetFileNameWithoutExtension(ProjectPath) + "_Metraj.xlsx");
            var exporter = new XlsxExporter();
            await Task.Run(() => exporter.Export(xlsxPath, reportData));

            // Fix A3: Status message for partial matches
            if (reportData.PartialMatches.Count > 0)
                StatusText = $"Tamamlandı. (⚠ {reportData.PartialMatches.Count} eksik eşleşme — rapordaki 'Eksik Eşleşmeler' sayfasına bakın)";
            else
                StatusText = "Tamamlandı.";
            
            ResultFilePath = xlsxPath;
            ProgressValue = 100;
        }
        catch (OperationCanceledException)
        {
            StatusText = "İşlem iptal edildi.";
            ProgressValue = 0;
        }
        catch (Exception ex)
        {
            StatusText = $"Hata: {ex.Message}";
            System.Console.Error.WriteLine($"[METRAJ ERROR] {ex}");
            ProgressValue = 0;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
        }
    }

    private List<List<TemplateMatchResult>> ClusterMatchesByProximity(List<TemplateMatchResult> matches, double radius)
    {
        if (matches.Count == 0) return new List<List<TemplateMatchResult>>();
        if (radius < 1e-9) radius = 100.0;

        // Grid-based spatial hashing for O(N) clustering instead of O(N²)
        double cellSize = radius;
        var grid = new Dictionary<(int, int), List<int>>();
        
        for (int i = 0; i < matches.Count; i++)
        {
            int gx = (int)Math.Floor(matches[i].Center.X / cellSize);
            int gy = (int)Math.Floor(matches[i].Center.Y / cellSize);
            var key = (gx, gy);
            if (!grid.ContainsKey(key)) grid[key] = new List<int>();
            grid[key].Add(i);
        }

        // Union-Find for fast clustering
        var parent = new int[matches.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;

        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        foreach (var kvp in grid)
        {
            var (gx, gy) = kvp.Key;
            var indices = kvp.Value;
            
            // Check this cell and 8 neighbors
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var neighborKey = (gx + dx, gy + dy);
                if (!grid.TryGetValue(neighborKey, out var neighborIndices)) continue;
                
                foreach (var i in indices)
                foreach (var j in neighborIndices)
                {
                    if (i >= j) continue;
                    if (Distance(matches[i].Center, matches[j].Center) < radius)
                        Union(i, j);
                }
            }
        }

        var clusters = new Dictionary<int, List<TemplateMatchResult>>();
        for (int i = 0; i < matches.Count; i++)
        {
            int root = Find(i);
            if (!clusters.ContainsKey(root)) clusters[root] = new List<TemplateMatchResult>();
            clusters[root].Add(matches[i]);
        }

        return clusters.Values.ToList();
    }

    private double Distance(MetrajPoint p1, MetrajPoint p2)
    {
        return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2) + Math.Pow(p1.Z - p2.Z, 2));
    }

    private List<List<MetrajPoint>> FindConnectedComponents(List<MetrajEntity> entities)
    {
        var segments = new List<(MetrajPoint P1, MetrajPoint P2)>();
        foreach (var e in entities)
        {
            if (e is MetrajLine l) segments.Add((l.Start, l.End));
            else if (e is MetrajPolyline p)
            {
                for (int i = 0; i < p.Vertices.Count - 1; i++) segments.Add((p.Vertices[i], p.Vertices[i+1]));
                if (p.IsClosed && p.Vertices.Count > 1) segments.Add((p.Vertices.Last(), p.Vertices.First()));
            }
            else if (e is MetrajCircle c)
            {
                // Approximate circle with 8 points for connectivity
                var pts = new List<MetrajPoint>();
                for (int i = 0; i < 8; i++)
                {
                    double ang = i * Math.PI / 4;
                    pts.Add(new MetrajPoint(c.Center.X + c.Radius * Math.Cos(ang), c.Center.Y + c.Radius * Math.Sin(ang), c.Center.Z));
                }
                for (int i = 0; i < 8; i++) segments.Add((pts[i], pts[(i + 1) % 8]));
            }
            else if (e is MetrajArc arc)
            {
                // Approximate arc
                int div = 4;
                double sweep = arc.EndAngle - arc.StartAngle;
                if (sweep < 0) sweep += 360;
                var pts = new List<MetrajPoint>();
                for (int i = 0; i <= div; i++)
                {
                    double ang = (arc.StartAngle + (sweep * i / div)) * Math.PI / 180.0;
                    pts.Add(new MetrajPoint(arc.Center.X + arc.Radius * Math.Cos(ang), arc.Center.Y + arc.Radius * Math.Sin(ang), arc.Center.Z));
                }
                for (int i = 0; i < div; i++) segments.Add((pts[i], pts[i+1]));
            }
        }

        if (segments.Count == 0) return new List<List<MetrajPoint>>();

        var pointToSegments = new Dictionary<MetrajPoint, List<int>>(_pointComparer);
        for (int i = 0; i < segments.Count; i++)
        {
            if (!pointToSegments.TryGetValue(segments[i].P1, out var list1)) { list1 = new List<int>(); pointToSegments[segments[i].P1] = list1; }
            list1.Add(i);
            if (!pointToSegments.TryGetValue(segments[i].P2, out var list2)) { list2 = new List<int>(); pointToSegments[segments[i].P2] = list2; }
            list2.Add(i);
        }

        var islands = new List<List<MetrajPoint>>();
        var visited = new bool[segments.Count];

        for (int i = 0; i < segments.Count; i++)
        {
            if (visited[i]) continue;

            var islandSegments = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                int currIdx = queue.Dequeue();
                islandSegments.Add(currIdx);
                var s = segments[currIdx];

                foreach (var p in new[] { s.P1, s.P2 })
                {
                    if (pointToSegments.TryGetValue(p, out var neighbors))
                    {
                        foreach (var nIdx in neighbors)
                        {
                            if (!visited[nIdx])
                            {
                                visited[nIdx] = true;
                                queue.Enqueue(nIdx);
                            }
                        }
                    }
                }
            }
            
            islands.Add(ExtractOrderedPath(islandSegments.Select(idx => segments[idx]).ToList()));
        }

        return islands;
    }

    /// <summary>
    /// Extract an ordered path from a set of segments forming a connected component.
    /// For simple chains/loops this produces a clean path.
    /// For T-junctions or figure-eights (N3), we use Euler-path heuristic:
    /// start from a vertex with odd degree if any, else lowest-degree vertex.
    /// </summary>
    private List<MetrajPoint> ExtractOrderedPath(List<(MetrajPoint P1, MetrajPoint P2)> island)
    {
        if (island.Count == 0) return new List<MetrajPoint>();

        var adjacency = new Dictionary<MetrajPoint, List<(MetrajPoint Other, int SegIdx)>>(_pointComparer);
        for (int i = 0; i < island.Count; i++)
        {
            var s = island[i];
            if (!adjacency.TryGetValue(s.P1, out var l1)) { l1 = new List<(MetrajPoint, int)>(); adjacency[s.P1] = l1; }
            l1.Add((s.P2, i));
            if (!adjacency.TryGetValue(s.P2, out var l2)) { l2 = new List<(MetrajPoint, int)>(); adjacency[s.P2] = l2; }
            l2.Add((s.P1, i));
        }

        // Choose start: prefer odd-degree vertex (Euler path start), else lowest degree
        var start = adjacency.Where(kv => kv.Value.Count % 2 == 1).Select(kv => kv.Key).FirstOrDefault();
        if (start == null)
            start = adjacency.OrderBy(kv => kv.Value.Count).First().Key;

        // Hierholzer-like traversal for Euler path on edges
        var usedEdges = new bool[island.Count];
        var path = new List<MetrajPoint>();
        var stack = new Stack<MetrajPoint>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Peek();
            if (!adjacency.ContainsKey(current))
            {
                path.Add(stack.Pop());
                continue;
            }
            
            var nextEdge = adjacency[current].FirstOrDefault(e => !usedEdges[e.SegIdx]);
            if (nextEdge.Other == null)
            {
                path.Add(stack.Pop());
            }
            else
            {
                usedEdges[nextEdge.SegIdx] = true;
                stack.Push(nextEdge.Other);
            }
        }

        path.Reverse();
        return path;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    public RelayCommand(Action<T> execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute((T)parameter!);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
