using netDxf;
using Kst.Acoustics.Core.Bina.Services;
if (args.Length == 0) { TestCases.Run(); return; }
if (args.Contains("--dwg")) {
 string converted;
 using(var cad = await MimariCadDosyasi.AcAsync(args[0])) {
  converted = cad.DxfYolu;
  if(!File.Exists(converted)) throw new Exception("Conversion output missing");
  Console.WriteLine($"Converted {new FileInfo(converted).Length} bytes");
  var loaded = new DxfKatYukleyici().OtomatikYukle(converted, args[0]);
  Console.WriteLine($"Imported {loaded.Katlar.Count} floors / {loaded.Katlar.Sum(k=>k.Odalar.Count)} rooms");
 }
 if(File.Exists(converted)) throw new Exception("Temporary DXF not cleaned");
 Console.WriteLine("DWG lifecycle passed"); return;
}
if (args.Contains("--inventory")) {
 var doc = DxfDocument.Load(args[0]);
 Console.WriteLine($"Units: {doc.DrawingVariables.InsUnits}, Entities: {doc.Entities.All.Count()}");
 Console.WriteLine("ARCS " + string.Join(", ", doc.Entities.Arcs.GroupBy(a=>Math.Round(a.Radius)).OrderByDescending(g=>g.Count()).Take(20).Select(g=>$"{g.Key}:{g.Count()}"))); foreach(var g in doc.Entities.All.GroupBy(e=>e.Layer.Name).OrderByDescending(g=>g.Count())) Console.WriteLine($"Layer {g.Key}: {g.Count()}");
 foreach(var t in doc.Entities.Texts.Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t.Value,"KAT|PLAN|KES|GÖR|GOR|[Hh]=|[Kk][Oo][Tt]|[0-9]/[0-9]|^[+±-][0-9]"))) Console.WriteLine($"TEXT {t.Position.X:0.##},{t.Position.Y:0.##}: {t.Value}");
 return;
}
var svc = new DxfKatYukleyici();
if(args.Contains("--auto")) {
 var resultAuto = svc.OtomatikYukle(args[0]);
 var geo = BinaGeometriServisi.YeniOlustur("Beylerbeyi otomatik içe aktarma");
 foreach(var k in resultAuto.Katlar) {
  geo.Katlar.Add(k);
  Console.WriteLine($"FLOOR {k.Ad}: {k.Odalar.Count} rooms, {k.Acikliklar.Count} openings, height={k.KatYuksekligiM}, level={k.TabanKotuM}");
  foreach(var room in k.Odalar) Console.WriteLine($"ROOM {room.Ad}: {room.AlanM2:0.##}");
 }
 foreach(var n in resultAuto.Notlar) Console.WriteLine(n);
 BinaGeometriServisi.Kaydet(geo, "scratch/bina-import/result.e4bina.json");
 Console.WriteLine($"3D surfaces: {Bina3DGorunumServisi.YuzeyleriOlustur(geo).Count}");
 return;
}
var result = svc.CokluKatTespitEt(args[0]);
Console.WriteLine($"Candidates: {result?.Adaylar.Count}, {svc.UnitWarning}");
if(result is not null) foreach(var a in result.Adaylar) Console.WriteLine($"{a.OnerilenAd} included={a.OneriliDahil} bbox={a.Kume.MinX:0.##},{a.Kume.MinY:0.##} {a.Kume.GenislikM:0.##}x{a.Kume.DerinlikM:0.##} segs={a.Kume.Segmentler.Count} texts={a.Kume.Metinler.Count}");


if(result is not null) foreach(var a in result.Adaylar.Where(a=>a.OneriliDahil)) {
 Console.WriteLine("LEVELS " + a.OnerilenAd);
 foreach(var t in a.Kume.Metinler.Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t.Value,@"^[+±-]\d|KAT.*YÜK"))) Console.WriteLine(t.Value);
}
