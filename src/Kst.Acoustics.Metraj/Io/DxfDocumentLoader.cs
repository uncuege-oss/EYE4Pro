using netDxf;
using netDxf.Entities;
using Kst.Acoustics.Metraj.Models;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Kst.Acoustics.Metraj.Io;

public class DxfDocumentLoader
{
    public MetrajProject Load(string dxfPath)
    {
        if (!File.Exists(dxfPath)) throw new FileNotFoundException("DXF dosyası bulunamadı.", dxfPath);
        DxfDocument doc;
        try 
        {
            using (var fs = new FileStream(dxfPath, FileMode.Open, FileAccess.Read))
            {
                byte[] header = new byte[22];
                fs.ReadExactly(header, 0, 22);
                string headerStr = System.Text.Encoding.ASCII.GetString(header);
                if (headerStr.StartsWith("AutoCAD Binary DXF"))
                {
                    throw new Exception("Seçilen dosya 'Binary DXF' formatında. Lütfen CAD programında 'ASCII DXF' (Metin formatı) olarak kaydedip tekrar deneyin.");
                }

                fs.Position = 0; // Reset for netDxf
                doc = DxfDocument.Load(fs);
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Binary DXF")) throw;
            
            string version = "Bilinmiyor";
            try 
            {
                using var reader = new StreamReader(dxfPath);
                string? prevLine = null;
                for(int i=0; i<100; i++)
                {
                    string? line = reader.ReadLine()?.Trim();
                    if (line == "$ACADVER" && prevLine == "9") 
                    { 
                        reader.ReadLine(); 
                        version = reader.ReadLine()?.Trim() ?? "Bilinmiyor"; 
                        break; 
                    }
                    prevLine = line;
                }
            } catch {}
            throw new Exception($"DXF yükleme hatası: {ex.Message} (Sürüm: {version})");
        }

        if (doc == null)
        {
            throw new Exception($"DXF dosyası okunamadı (doc=null). Sürüm uyumsuzluğu veya bozuk dosya olabilir.");
        }

        var project = new MetrajProject
        {
            Name = Path.GetFileName(dxfPath),
            SourceUnit = (MetrajSourceUnit)(int)doc.DrawingVariables.InsUnits,
            Entities = LoadFromDocument(doc)
        };

        var layerColors = doc.Layers.ToDictionary(l => l.Name, l => ToMetrajColor(l.Color));

        // Load all block definitions (untransformed geometry)
        foreach (var block in doc.Blocks)
        {
            // Skip layout blocks (model space, paper space)
            if (block.Name.StartsWith("*", StringComparison.Ordinal)) continue;
            
            var blockEntities = LoadFromDocumentInternal(block.Entities, layerColors, new HashSet<string>(), 0, Matrix4.Identity, true);
            if (blockEntities.Count > 0)
            {
                project.BlockDefinitions[block.Name] = blockEntities;
            }
        }

        // Flatten all inserts for BlockCounter
        foreach (var ins in doc.Entities.Inserts)
        {
            FlattenInsertRecursive(ins, project.AllInserts, layerColors);
        }

        return project;
    }

    private void FlattenInsertRecursive(Insert ins, List<MetrajInsert> target, Dictionary<string, MetrajColor> layerColors)
    {
        var attributes = new Dictionary<string, string>();
        foreach (var a in ins.Attributes) attributes[a.Tag] = a.Value;
        var mi = new MetrajInsert(
            ins.Layer.Name,
            ToMetrajColor(ins.Color),
            ins.Block.Name,
            ToMetrajPoint(ins.Position),
            attributes
        );
        
        if (mi.Color.IsByLayer && layerColors.TryGetValue(mi.Layer, out var lc))
            mi.EffectiveColor = lc;
        else if (mi.Color.IsByBlock)
            mi.EffectiveColor = new MetrajColor(7);

        target.Add(mi);

        foreach (var nested in ins.Block.Entities.OfType<Insert>())
        {
            FlattenInsertRecursive(nested, target, layerColors);
        }
    }

    public List<MetrajEntity> LoadFromDocument(DxfDocument doc)
    {
        var layerColors = doc.Layers.ToDictionary(l => l.Name, l => ToMetrajColor(l.Color));
        return LoadFromDocumentInternal(doc.Entities.All, layerColors, new HashSet<string>(), 0, Matrix4.Identity, false);
    }

    private List<MetrajEntity> LoadFromDocumentInternal(IEnumerable<EntityObject> docEntities, Dictionary<string, MetrajColor> layerColors, HashSet<string> visitedBlocks, int depth, Matrix4 transform, bool fromBlock)
    {
        if (depth > 16) return new List<MetrajEntity>();
        var entities = new List<MetrajEntity>();
        bool isIdentity = transform.IsIdentity;

        MetrajPoint T(Vector3 v)
        {
            if (isIdentity) return ToMetrajPoint(v);
            var v4 = transform * new Vector4(v.X, v.Y, v.Z, 1.0);
            return new MetrajPoint(v4.X, v4.Y, v4.Z);
        }

        void ResolveColor(MetrajEntity me)
        {
            me.IsFromBlock = fromBlock;
            if (me.Color.IsByLayer && layerColors.TryGetValue(me.Layer, out var lc))
                me.EffectiveColor = lc;
            else if (me.Color.IsByBlock)
                me.EffectiveColor = new MetrajColor(7);
        }

        // LINE
        foreach (var line in docEntities.OfType<Line>())
        {
            var me = new MetrajLine(line.Layer.Name, ToMetrajColor(line.Color), T(line.StartPoint), T(line.EndPoint));
            ResolveColor(me);
            entities.Add(me);
        }

        // LWPOLYLINE
        foreach (var lw in docEntities.OfType<Polyline2D>())
        {
            var me = new MetrajPolyline(lw.Layer.Name, ToMetrajColor(lw.Color), lw.Vertexes.Select(v => T(new Vector3(v.Position.X, v.Position.Y, 0))).ToList(), lw.IsClosed);
            ResolveColor(me);
            entities.Add(me);
        }

        // POLYLINE (3D)
        foreach (var poly in docEntities.OfType<Polyline3D>())
        {
            var me = new MetrajPolyline(poly.Layer.Name, ToMetrajColor(poly.Color), poly.Vertexes.Select(v => T(v)).ToList(), poly.IsClosed);
            ResolveColor(me);
            entities.Add(me);
        }

        // ARC
        foreach (var arc in docEntities.OfType<Arc>())
        {
            var me = new MetrajArc(arc.Layer.Name, ToMetrajColor(arc.Color), T(arc.Center), arc.Radius, arc.StartAngle, arc.EndAngle);
            ResolveColor(me);
            entities.Add(me);
        }

        // CIRCLE
        foreach (var circle in docEntities.OfType<Circle>())
        {
            var me = new MetrajCircle(circle.Layer.Name, ToMetrajColor(circle.Color), T(circle.Center), circle.Radius);
            ResolveColor(me);
            entities.Add(me);
        }

        // ELLIPSE
        foreach (var ell in docEntities.OfType<Ellipse>())
        {
            double rotRad = ell.Rotation * Math.PI / 180.0;
            double mx = ell.MajorAxis * Math.Cos(rotRad);
            double my = ell.MajorAxis * Math.Sin(rotRad);
            var me = new MetrajEllipse(ell.Layer.Name, ToMetrajColor(ell.Color), T(ell.Center), mx, my, 0, ell.MajorAxis > 0 ? ell.MinorAxis / ell.MajorAxis : 1.0, ell.StartAngle, ell.EndAngle);
            ResolveColor(me);
            entities.Add(me);
        }

        // SPLINE
        foreach (var spline in docEntities.OfType<Spline>())
        {
            var me = new MetrajSpline(spline.Layer.Name, ToMetrajColor(spline.Color), spline.ControlPoints.Select(p => T(p)).ToList());
            ResolveColor(me);
            entities.Add(me);
        }

        // INSERT (Manual Explode to avoid netDxf.Explode() crash)
        foreach (var ins in docEntities.OfType<Insert>())
        {
            var attributes = new Dictionary<string, string>();
            foreach (var a in ins.Attributes) attributes[a.Tag] = a.Value;
            var me = new MetrajInsert(ins.Layer.Name, ToMetrajColor(ins.Color), ins.Block.Name, T(ins.Position), attributes);
            ResolveColor(me);
            entities.Add(me);

            if (!visitedBlocks.Contains(ins.Block.Name))
            {
                var subVisited = new HashSet<string>(visitedBlocks) { ins.Block.Name };

                var localTransform = Matrix4.Translation(ins.Position.X, ins.Position.Y, ins.Position.Z) *
                                     Matrix4.RotationZ(ins.Rotation * Math.PI / 180.0) *
                                     Matrix4.Scale(ins.Scale.X, ins.Scale.Y, ins.Scale.Z);

                var nextTransform = transform * localTransform;
                // Entities from inside a block are marked IsFromBlock=true so template
                // matching doesn't double-count them alongside BlockCounter results.
                entities.AddRange(LoadFromDocumentInternal(ins.Block.Entities, layerColors, subVisited, depth + 1, nextTransform, true));
            }
        }

        // TEXT
        foreach (var txt in docEntities.OfType<Text>())
        {
            var me = new MetrajText(txt.Layer.Name, ToMetrajColor(txt.Color), txt.Value, T(txt.Position));
            ResolveColor(me);
            entities.Add(me);
        }

        // MTEXT
        foreach (var mtxt in docEntities.OfType<MText>())
        {
            var me = new MetrajMText(mtxt.Layer.Name, ToMetrajColor(mtxt.Color), mtxt.Value, T(mtxt.Position));
            ResolveColor(me);
            entities.Add(me);
        }

        return entities;
    }

    private MetrajColor ToMetrajColor(AciColor color) => new MetrajColor(color.Index, color.R, color.G, color.B);
    private MetrajPoint ToMetrajPoint(Vector3 v) => new MetrajPoint(v.X, v.Y, v.Z);
}
