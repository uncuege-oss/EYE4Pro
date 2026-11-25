using System.Globalization;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Projects;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;

var work = Path.Combine(Path.GetTempPath(), "bina-mimari-e2e-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);
int passed = 0, failed = 0;
try
{
    Run("Fiziksel duvar iki mahal oluşturur; yinelenen etiket üçüncü mahal olmaz", PhysicalPartition);
    Run("Açık plandaki farklı kullanım etiketleri duvarsız bölme oluşturmaz", OpenPlan);
    Run("Kapalı katmandaki bölme üretime ve yeniden açılan projeye sızmaz", HiddenPartition);
    Run("Ardışık kat kotları ve net oda yüksekliği 3D'de ayrılır", StoreyAndNetHeight);
    Run("Çelişkili yerel kotlar doğrulanmış kat kotu sayılmaz", AmbiguousLevels);
    Run("Yerel oda taban kotu ve birleşik mahal adları 3D/kayıt boyunca korunur", LocalRoomLevel);
    Run("Farklı blokların kot farkı kat yüksekliği üretmez", DifferentBlocks);
    Run("Çıplak yerel kot, net yükseklik olsa da oda döşemesi sayılmaz", () => ImportedLocalLevel(false));
    Run("Açık TABAN KOTU ve net yükseklik birlikte yerel 3D kotu belirler", () => ImportedLocalLevel(true));

    bool syntheticOnly = args.Contains("--synthetic", StringComparer.Ordinal);
    string? source = syntheticOnly ? null : args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
        ?? Environment.GetEnvironmentVariable("EYE4PRO_BINA_MIMARI_ORNEK");
    if (source is null && !syntheticOnly)
    {
        var local = Path.GetFullPath("scratch/bina-import/source.dxf");
        if (File.Exists(local)) source = local;
    }
    if (source is not null && File.Exists(source)) Run("Yerel mimari örnek: geometri, kayıt/aç ve 3D tutarlılığı", () => SourceSample(source));
    else Console.WriteLine("SKIP Kaynak örnek yok veya --synthetic seçildi. Dosya yolu argümanı veya EYE4PRO_BINA_MIMARI_ORNEK verilebilir.");

    Console.WriteLine($"{passed}/{passed + failed} senaryo geçti; {failed} hata.");
    return failed == 0 ? 0 : 1;
}
finally { Directory.Delete(work, true); }

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}

void PhysicalPartition()
{
    var doc = Drawing();
    RoomBox(doc, 0, "DUVAR");
    Line(doc, 5, 0, 5, 8, "BOLME");
    Text(doc, 2, 3, "OKUMA SALONU"); Text(doc, 2.03, 3.02, "OKUMA SALONU");
    Text(doc, 7, 3, "TOPLANTI ODASI"); Text(doc, 2, -1, "ZEMİN KAT PLANI 1/50");
    var result = Import(doc, "partition");
    Check(result.Katlar.Count == 1, "Tek kat beklenir.");
    var kat = result.Katlar[0];
    Check(kat.Odalar.Count == 2, $"Duvarla ayrılan iki mahal beklenir; {kat.Odalar.Count} bulundu.");
    Check(kat.Odalar.Count(o => o.Ad == "OKUMA SALONU") == 1, "Yinelenen etiket oda sayısını artırdı.");
    Check(kat.Odalar.Any(o => o.Ad == "TOPLANTI ODASI"), "Bölmenin diğer tarafındaki isim kayboldu.");
    Check(kat.Odalar.All(o => o.Poligon.Max(p => p.X) <= 5.25 || o.Poligon.Min(p => p.X) >= 4.75), "Oda sınırı tam duvarı aştı.");
    AssertGeometry(result.Katlar); RoundTrip(result.Katlar, "partition");
}

void OpenPlan()
{
    var doc = Drawing(); RoomBox(doc, 0, "DUVAR");
    Text(doc, 2, -1, "ZEMİN KAT PLANI 1/50");
    Text(doc, 2, 3, "DANIŞMA"); Text(doc, 7, 3, "GİRİŞ HOLÜ");
    Text(doc, 2.03, 3.02, "DANIŞMA");
    var result = Import(doc, "open-plan");
    Check(result.Katlar.Single().Odalar.Count == 1, "Duvar kanıtı olmadan kullanım etiketleri ayrı odalara çevrildi.");
    Check(result.Notlar.Any(n => n.Contains("aynı sınır", StringComparison.OrdinalIgnoreCase)), "Çok etiketli açık plan gözden geçirme notu eksik.");
    AssertGeometry(result.Katlar); RoundTrip(result.Katlar, "open-plan");
}

void HiddenPartition()
{
    var doc = Drawing(); RoomBox(doc, 0, "DUVAR"); Line(doc, 5, 0, 5, 8, "KAPALI_BOLME");
    Text(doc, 2, -1, "ZEMİN KAT PLANI 1/50"); Text(doc, 2, 3, "OFİS"); Text(doc, 7, 3, "TOPLANTI ODASI");
    var file = Path.Combine(work, "hidden.dxf"); doc.Save(file);
    var drawing = new DxfKatPlaniParser().KatmanliOku(file);
    var selected = drawing.Katmanlar.Where(l => l.Ad != "KAPALI_BOLME").Select(l => l.Ad).ToArray();
    var snapshot = drawing.HamKatOlustur(selected);
    Check(snapshot.Odalar.Count == 0, "Katman önizlemesi üretim yaptı.");
    var result = new DxfKatYukleyici().OtomatikYukle(snapshot, file);
    Check(snapshot.Odalar.Count == 0, "Üretim kaynak snapshot'ı değiştirdi.");
    Check(result.Katlar.Single().Odalar.Count == 1, "Kapalı katman üretimde bölme olarak kullanıldı.");
    var restored = RoundTrip(result.Katlar, "hidden");
    var kat = restored.Katlar.Single();
    Check(kat.KaynakKatmanlari is not null && !kat.KaynakKatmanlari.Contains("KAPALI_BOLME"), "Katman seçimi kayıtta kayboldu.");
    Check(kat.KayitliTespitSegmentleri is not null && !kat.KayitliTespitSegmentleri.Any(s =>
        Math.Abs(s.A.X - 5) < .02 && Math.Abs(s.B.X - 5) < .02 && Math.Abs(s.A.Y - s.B.Y) > 7), "Kapalı bölme yeniden tarama verisine sızdı.");
}

void StoreyAndNetHeight()
{
    var doc = Drawing();
    foreach (var (x, name, z) in new[] { (0d, "ZEMİN KAT PLANI 1/50", 0d), (40d, "1. KAT PLANI 1/50", 4d), (80d, "2. KAT PLANI 1/50", 8d) })
    {
        RoomBox(doc, x, "DUVAR"); Text(doc, x + 2, -1, name); Text(doc, x + 2, 3, "OKUMA SALONU");
        Text(doc, x + 3, 5, $"+{z.ToString("0.00", CultureInfo.InvariantCulture)}");
    }
    Text(doc, 2, 6, "h=2.80 m"); Text(doc, 82, 6, "KAT YÜKSEKLİĞİ=3.20 m");
    var result = Import(doc, "levels");
    Check(result.Katlar.Count == 3, "Üç kat ayrılmalı.");
    var bottom = result.Katlar.Single(k => k.Ad == "Zemin Kat");
    var middle = result.Katlar.Single(k => k.Ad == "1. Kat");
    var top = result.Katlar.Single(k => k.Ad == "2. Kat");
    Near(bottom.TabanKotuM ?? double.NaN, 0, "Zemin kotu"); Near(middle.TabanKotuM ?? double.NaN, 4, "1. kat kotu"); Near(top.TabanKotuM ?? double.NaN, 8, "2. kat kotu");
    Near(bottom.KatYuksekligiM, 4, "Zemin katlar arası yükseklik"); Near(middle.KatYuksekligiM, 4, "1. kat yüksekliği"); Near(top.KatYuksekligiM, 3.2, "2. kat yüksekliği");
    Near(bottom.Odalar.Single().YukseklikOverrideM ?? double.NaN, 2.8, "Net yükseklik");
    var restored = RoundTrip(result.Katlar, "levels");
    AssertNoFloorOverlap(restored.Katlar);
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(restored);
    var bottomRoom = restored.Katlar.Single(k => k.Ad == "Zemin Kat").Odalar.Single();
    Near(surfaces.Single(s => s.OwnerId == bottomRoom.Id && s.Category == "Tavan").Vertices[0].Z, 2.8, "Net oda tavan kotu");
    var middleRoom = restored.Katlar.Single(k => k.Ad == "1. Kat").Odalar.Single();
    Near(surfaces.Single(s => s.OwnerId == middleRoom.Id && s.Category == "Doseme").Vertices[0].Z, 4, "Üst döşeme net yükseklikle karıştırılmamalı");
}

void AmbiguousLevels()
{
    var doc = Drawing(); RoomBox(doc, 0, "DUVAR"); Text(doc, 2, -1, "ZEMİN KAT PLANI 1/50");
    Text(doc, 2, 3, "OKUMA SALONU"); Text(doc, 2, 6, "+0.00"); Text(doc, 7, 6, "+1.00"); Text(doc, 7, 5, "+919.25");
    var result = Import(doc, "ambiguous");
    var kat = result.Katlar.Single();
    Check(kat.TabanKotuM is null, "Çelişkili kademeli döşeme kotları kesin taban kotu sayıldı.");
    Check(result.Notlar.Any(n => n.Contains("çelişkili", StringComparison.OrdinalIgnoreCase)), "Kot çelişkisi raporlanmadı.");
    Check(result.Notlar.Any(n => n.Contains("geçici", StringComparison.OrdinalIgnoreCase)), "Eksik kat yüksekliği geçici olarak belirtilmedi.");
    RoundTrip(result.Katlar, "ambiguous");
}

void LocalRoomLevel()
{
    var kat = new BinaKat { Ad = "Zemin Kat", KatNo = 1, TabanKotuM = 0, KatYuksekligiM = 4 };
    var normal = new BinaOda { Ad = "GİRİŞ HOLÜ", AlanM2 = 80,
        Poligon = [new(0,0), new(10,0), new(10,8), new(0,8)], KaynakMahalAdlari = ["GİRİŞ HOLÜ", "DANIŞMA"] };
    var raised = new BinaOda { Ad = "OKUMA SALONU", AlanM2 = 80,
        Poligon = [new(20,0), new(30,0), new(30,8), new(20,8)], TabanKotuOverrideM = 1, YukseklikOverrideM = 3,
        KaynakMahalAdlari = ["OKUMA SALONU"] };
    kat.Odalar.Add(normal); kat.Odalar.Add(raised);
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(1,0), Bit = new(2,0), GenislikM = 1, YukseklikM = 2.1 });
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(21,0), Bit = new(22,0), GenislikM = 1, YukseklikM = 2.1 });
    var restored = RoundTrip([kat], "local-room-level");
    var savedNormal = restored.Katlar.Single().Odalar.Single(o => o.Id == normal.Id);
    var savedRaised = restored.Katlar.Single().Odalar.Single(o => o.Id == raised.Id);
    Check(savedNormal.TabanKotuOverrideM is null, "Normal oda kat kotunu devralmayı kaybetti.");
    Near(savedRaised.TabanKotuOverrideM ?? double.NaN, 1, "Yerel oda kotu");
    Check(savedNormal.KaynakMahalAdlari.SequenceEqual(normal.KaynakMahalAdlari), "Açık alanın farklı kaynak adları kayboldu.");
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(restored);
    Near(surfaces.Single(s => s.OwnerId == raised.Id && s.Category == "Doseme").Vertices[0].Z, 1, "Yükseltilmiş oda döşemesi");
    Near(surfaces.Single(s => s.OwnerId == raised.Id && s.Category == "Tavan").Vertices[0].Z, 4, "Yükseltilmiş odanın net yüksekliği");
    Near(surfaces.Single(s => s.OwnerId == normal.Id && s.Category == "Doseme").Vertices[0].Z, 0, "Normal oda döşemesi");
    Near(surfaces.Where(s => s.OwnerId == raised.Id && s.Category == "Kapi").SelectMany(s => s.Vertices).Min(p => p.Z), 1, "Yükseltilmiş oda kapı tabanı");
    var openingLevels = Bina3DGorunumServisi.Acikliklar3D(restored).Select(a => a.Bas.Z).Order().ToArray();
    Check(openingLevels.Length == 2, "Kapı çizgileri kayboldu/yinelendi.");
    Near(openingLevels[0], 0, "Normal açıklık çizgisi"); Near(openingLevels[1], 1, "Yükseltilmiş açıklık çizgisi");
}

void DifferentBlocks()
{
    var doc = Drawing();
    RoomBox(doc, 0, "DUVAR"); Text(doc, 2, -1, "A BLOK ZEMİN KAT PLANI 1/50");
    Text(doc, 2, 3, "OFİS"); Text(doc, 3, 5, "+0.00");
    RoomBox(doc, 40, "DUVAR"); Text(doc, 42, -1, "B BLOK 1. KAT PLANI 1/50");
    Text(doc, 42, 3, "OKUMA SALONU"); Text(doc, 43, 5, "+4.00");
    var result = Import(doc, "different-blocks");
    Check(result.Katlar.Count == 2, "İki ayrı blok planı kayboldu.");
    Check(result.Katlar.All(k => Math.Abs(k.KatYuksekligiM - 3) < 1e-6), "A ve B blok kotları yanlışlıkla tek binanın ardışık katları sayıldı.");
    Check(result.Katlar.All(k => k.IceriAktarimNotlari.Any(n => n.Contains("geçici", StringComparison.OrdinalIgnoreCase))), "Blokların eksik yüksekliği geçici olarak belirtilmedi.");
    Check(result.Notlar.Any(n => n.Contains("blok", StringComparison.OrdinalIgnoreCase) && n.Contains("eşleş", StringComparison.OrdinalIgnoreCase)), "Blok eşleşmesi eksikliği raporlanmadı.");
    RoundTrip(result.Katlar, "different-blocks");
}

void ImportedLocalLevel(bool explicitFloor)
{
    var doc = Drawing(); RoomBox(doc, 0, "DUVAR"); Line(doc, 5, 0, 5, 8, "BOLME");
    Text(doc, 2, -1, "ZEMİN KAT PLANI 1/50"); Text(doc, 2, 3, "HOL"); Text(doc, 7, 3, "OKUMA SALONU");
    // Katın ana kotunu tekil yerel kottan ayıran bağımsız referanslar.
    Text(doc, 1, 1, "+0.00"); Text(doc, 2, 1, "+0.00"); Text(doc, 3, 1, "+0.00");
    Text(doc, 1, 6, "KAT YÜKSEKLİĞİ=4.00 m");
    Text(doc, 7, 5, explicitFloor ? "TABAN KOTU +1.00" : "+1.00"); Text(doc, 7, 6, "h=2.80 m");
    string name = explicitFloor ? "explicit-local-floor" : "bare-local-level";
    var result = Import(doc, name);
    var kat = result.Katlar.Single(); Near(kat.TabanKotuM ?? double.NaN, 0, "Ana kat referansı");
    var room = kat.Odalar.Single(o => o.Ad == "OKUMA SALONU");
    Near(room.YukseklikOverrideM ?? double.NaN, 2.8, "İçe aktarılan net oda yüksekliği");
    if (explicitFloor) Near(room.TabanKotuOverrideM ?? double.NaN, 1, "Açık döşeme kotu");
    else
    {
        Check(room.TabanKotuOverrideM is null, "Çıplak +1.00 merdiven/yerel kot adayı kesin döşeme kotuna çevrildi.");
        Check(result.Notlar.Any(n => n.Contains("yerel kot adayı", StringComparison.OrdinalIgnoreCase) && n.Contains("uygulanmadı", StringComparison.OrdinalIgnoreCase)), "Uygulanmayan yerel kot raporda görünmüyor.");
    }
    var restored = RoundTrip(result.Katlar, name);
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(restored);
    double floor = explicitFloor ? 1 : 0;
    Near(surfaces.Single(s => s.OwnerId == room.Id && s.Category == "Doseme").Vertices[0].Z, floor, "İçe aktarma/kayıt sonrası oda tabanı");
    Near(surfaces.Single(s => s.OwnerId == room.Id && s.Category == "Tavan").Vertices[0].Z, floor + 2.8, "İçe aktarma/kayıt sonrası oda tavanı");
}

void SourceSample(string source)
{
    using var cad = MimariCadDosyasi.AcAsync(source).GetAwaiter().GetResult();
    var drawing = new DxfKatPlaniParser().KatmanliOku(cad.DxfYolu);
    bool beylerbeyi = Path.GetFileName(source).Contains("beylerbeyi", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFullPath(source).Equals(Path.GetFullPath("scratch/bina-import/source.dxf"), StringComparison.OrdinalIgnoreCase);
    string Norm(string value) => value.Replace('ı','i').ToUpperInvariant().Replace('İ','I').Replace('Ş','S').Replace('Ö','O').Replace('Ü','U').Replace('Ç','C').Replace('Ğ','G');
    var sourceLayers = drawing.Katmanlar.Where(k => beylerbeyi
        ? new[] { "0", "ALCIPAN", "CAM", "DUVAR", "KAPI", "KOLON", "KOT", "PLAN_YAZI", "SIVA", "YAZI", "YAZI TEF" }.Contains(Norm(k.Ad))
        : k.VarsayilanAcik).Select(k => k.Ad);
    var snapshot = drawing.HamKatOlustur(sourceLayers);
    var result = new DxfKatYukleyici().OtomatikYukle(snapshot, source);
    Check(result.Katlar.Count > 0, "Örnekte kat ayrılamadı.");
    AssertGeometry(result.Katlar);
    if (beylerbeyi)
    {
        // Bu örnekte kat yüksekliği kesit/plan kot zincirinden doğrulanır.
        // Mahal etiket adetlerinden fiziksel oda sayısı türetilmez.
        var bottom = result.Katlar.Single(k => k.Ad == "Zemin Kat");
        var middle = result.Katlar.Single(k => k.Ad == "1. Kat");
        var roof = result.Katlar.Single(k => k.Ad.Contains("Çatı", StringComparison.OrdinalIgnoreCase));
        Near(bottom.TabanKotuM ?? double.NaN, 0, "Beylerbeyi zemin kotu");
        Near(bottom.KatYuksekligiM, 4, "Beylerbeyi zemin kat yüksekliği");
        Near(middle.TabanKotuM ?? double.NaN, 4, "Beylerbeyi 1. kat kotu");
        Near(middle.KatYuksekligiM, 4, "Beylerbeyi 1. kat yüksekliği");
        Near(roof.TabanKotuM ?? double.NaN, 8, "Beylerbeyi çatı taban kotu");
        bool HasLabel(BinaOda room, string name) => Norm(room.Ad) == Norm(name) || room.KaynakMahalAdlari.Any(n => Norm(n) == Norm(name));
        var upperCondolence = middle.Odalar.Single(o => HasLabel(o, "BAYAN TAZİYE OTURMA ALANI"));
        Check(upperCondolence.AlanM2 is > 80 and < 140 && !HasLabel(upperCondolence, "HOL"), "Üst kat çift kanatlı kapısı HOL ile taziye alanını ayıramadı.");
        Check(middle.Odalar.Any(o => o.Id != upperCondolence.Id && HasLabel(o, "HOL") && o.AlanM2 is > 20 and < 60), "Üst kat fiziksel HOL sınırı kayboldu.");
        var lowerCondolence = bottom.Odalar.Single(o => HasLabel(o, "BAY TAZİYE OTURMA ALANI"));
        Check(lowerCondolence.AlanM2 is > 90 and < 130 && !HasLabel(lowerCondolence, "HOL"), "Zemin çift kanatlı kapısı HOL ile taziye alanını ayıramadı.");
        Check(middle.Odalar.Any(o => HasLabel(o, "GİRİŞ HOLÜ") && o.AlanM2 is > 120 and < 170), "Gerçek açık giriş holü etiket sayısına göre keyfi bölündü.");
    }
    var restored = RoundTrip(result.Katlar, "source");
    AssertNoFloorOverlap(restored.Katlar);
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(restored);
    foreach (var kat in restored.Katlar)
    {
        if (!kat.BloklariGoster)
            Check(!surfaces.Any(s => kat.Odalar.Any(o => o.Id == s.OwnerId)), "Gizli çatı adayından 3D hacim üretildi.");
        int knownNames = kat.Odalar.Count(o => MimariOdaEtiketServisi.ContainsRoomName(o.Ad));
        Console.WriteLine($"  ÖRNEK {kat.Ad}: {kat.Odalar.Count} geometrik aday, {knownNames} tanınan mahal adı; kot={kat.TabanKotuM?.ToString("0.###", CultureInfo.InvariantCulture) ?? "belirsiz"}; h={kat.KatYuksekligiM:0.###}m; 3D={kat.BloklariGoster}.");
    }
    Console.WriteLine($"  ÖRNEK {surfaces.Count} 3D yüzey; {result.Notlar.Count} kontrol/bilgi notu. Etiket ve aday adetleri doğrulanmış fiziksel oda sayısı değildir.");
}

DxfKatYukleyici.OtomatikSonuc Import(DxfDocument doc, string name)
{
    string path = Path.Combine(work, name + ".dxf"); doc.Save(path);
    var drawing = new DxfKatPlaniParser().KatmanliOku(path);
    return new DxfKatYukleyici().OtomatikYukle(drawing.HamKatOlustur(drawing.Katmanlar.Select(k => k.Ad)), path);
}

BinaGeometri RoundTrip(IReadOnlyList<BinaKat> floors, string name)
{
    var geometry = BinaGeometriServisi.YeniOlustur("Mimari uçtan uca doğrulama");
    foreach (var kat in floors) geometry.Katlar.Add(kat);
    var before = Fingerprint(Bina3DGorunumServisi.YuzeyleriOlustur(geometry));
    var path = Path.Combine(work, name + ".e4bina.json"); BinaGeometriServisi.Kaydet(geometry, path);
    var restored = BinaGeometriServisi.Ac(path);
    Check(restored.Katlar.Count == floors.Count, "Kayıt/aç kat sayısı değişti.");
    for (int i = 0; i < floors.Count; i++)
    {
        var a = floors[i]; var b = restored.Katlar[i];
        Check(a.Id == b.Id && a.Ad == b.Ad && a.TabanKotuM == b.TabanKotuM && a.BloklariGoster == b.BloklariGoster, "Kat kimliği/kotu/görünürlüğü kayıtta değişti.");
        Near(b.KatYuksekligiM, a.KatYuksekligiM, "Kayıt sonrası kat yüksekliği");
        Check(a.Odalar.Count == b.Odalar.Count && a.Acikliklar.Count == b.Acikliklar.Count, "Oda/açıklık sayısı kayıtta değişti.");
        Check(a.IceriAktarimNotlari.SequenceEqual(b.IceriAktarimNotlari), "Kontrol notları kayıtta değişti.");
    }
    Check(before.SequenceEqual(Fingerprint(Bina3DGorunumServisi.YuzeyleriOlustur(restored))), "Kayıt/aç 3D geometriyi veya yükseklikleri değiştirdi.");
    return restored;
}

static void AssertGeometry(IReadOnlyList<BinaKat> floors)
{
    Check(floors.Select(k => k.Id).Distinct().Count() == floors.Count, "Kat kimlikleri tekil değil.");
    var rooms = floors.SelectMany(k => k.Odalar).ToList();
    Check(rooms.Count > 0 && rooms.Select(o => o.Id).Distinct().Count() == rooms.Count, "Oda kimlikleri tekil değil veya oda yok.");
    foreach (var room in rooms)
    {
        Check(room.Poligon.Count >= 3 && room.Poligon.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y)), "Geçersiz oda poligonu.");
        Check(double.IsFinite(room.AlanM2) && room.AlanM2 > 0, "Geçersiz mahal alanı.");
        Near(PoligonYardimci.Alan(room.Poligon), room.AlanM2, "Poligon ve kayıt alanı", .01);
    }
    foreach (var floor in floors) Check(double.IsFinite(floor.KatYuksekligiM) && floor.KatYuksekligiM > 0, "Geçersiz kat yüksekliği.");
}

static void AssertNoFloorOverlap(IEnumerable<BinaKat> floors)
{
    var ordered = floors.Reverse().ToList();
    double next = 0;
    for (int i = 0; i < ordered.Count; i++)
    {
        var kat = ordered[i]; double level = kat.TabanKotuM ?? next;
        Check(double.IsFinite(level), "3D taban kotu sonlu değil.");
        Check(i == 0 || level >= next - .01, $"Katlar düşeyde çakışıyor: {kat.Ad}, taban {level:0.###}, alt kat üstü {next:0.###}.");
        next = level + kat.KatYuksekligiM;
    }
}

static string[] Fingerprint(IEnumerable<ProjectSurface> surfaces) => surfaces.Select(s => s.Category + "|" + s.OwnerId + "|" + s.Name + "|" +
    string.Join(";", s.Vertices.Select(p => $"{p.X.ToString("R", CultureInfo.InvariantCulture)},{p.Y.ToString("R", CultureInfo.InvariantCulture)},{p.Z.ToString("R", CultureInfo.InvariantCulture)}"))).Order(StringComparer.Ordinal).ToArray();
static DxfDocument Drawing() { var doc = new DxfDocument(); doc.DrawingVariables.InsUnits = DrawingUnits.Meters; return doc; }
static void RoomBox(DxfDocument doc, double x, string layer) => doc.Entities.Add(new Polyline2D(new[] { new Vector2(x, 0), new Vector2(x + 10, 0), new Vector2(x + 10, 8), new Vector2(x, 8) }, true) { Layer = new Layer(layer) });
static void Line(DxfDocument doc, double x1, double y1, double x2, double y2, string layer) => doc.Entities.Add(new Line(new Vector2(x1, y1), new Vector2(x2, y2)) { Layer = new Layer(layer) });
static void Text(DxfDocument doc, double x, double y, string value) => doc.Entities.Add(new Text(value, new Vector2(x, y), .25) { Layer = new Layer("YAZI") });
static void Near(double actual, double expected, string message, double tolerance = 1e-6) => Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance, $"{message}: beklenen {expected:0.###}, bulunan {actual:0.###}.");
static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
