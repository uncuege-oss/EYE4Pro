using System.Globalization;
using System.Text.RegularExpressions;
using netDxf;
using netDxf.Entities;
using netDxf.Units;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// Açık konturlar, 3B ölçü noktaları ve kot etiketleri için araziye özel CAD okuyucusu.
/// Geometrik X/Y/Z çizim biriminden metreye çevrilir; yazılı kotlar metre kabul edilir.
public class DxfAraziParser
{
    public double UnitlessScaleMeters { get; set; } = 1.0;
    public double? MetrePerBirim { get; set; }
    public ISet<string>? Katmanlar { get; set; }
    public bool MetinKotlariniOku { get; set; } = true;
    public double EtiketEslesmeToleransiM { get; set; } = 2.0;

    private static readonly Regex KotRegex = new(
        @"^(?:(?:KOT(?:U)?|Z|EL|ELEV(?:ATION)?|RL|H)\s*[:=]?\s*)?([+±−-]?\d+(?:[.,]\d+)?)\s*(?:m)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex KatmanKotRegex = new(
        @"(?:KONTUR|CONTOUR|ELEV(?:ATION)?|KOT|ESYUKSELTI|EŞYÜKSELTİ)[_\s:=]*([+−-]?\d+(?:[.,]\d+)?)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex AraziKatmanRegex = new(
        @"KONTUR|CONTOUR|KOT|ELEV|TOPO|ARAZI|ARAZİ|ZEMIN|ZEMİN|SPOT|NOKTA|SURVEY|ESYUKSELTI|EŞYÜKSELTİ|EĞRI|EGRI|EĞRİ",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private sealed class Etiket(Point2D pos, double deger, string metin, string katman, bool bagimsiz)
    {
        public Point2D Pos { get; } = pos;
        public double Deger { get; } = deger;
        public string Metin { get; } = metin;
        public string Katman { get; } = katman;
        public bool Bagimsiz { get; } = bagimsiz;
        public bool Kullanildi { get; set; }
    }

    public AraziImportSonucu Parse(string filePath)
    {
        var dxf = DxfDocument.Load(filePath) ?? throw new InvalidDataException("DXF dosyası yüklenemedi.");
        var sonuc = Parse(dxf);
        sonuc.Arazi.DosyaYolu = Path.GetFullPath(filePath);
        if (sonuc.Arazi.Kaynak is not null) sonuc.Arazi.Kaynak.DosyaYolu = Path.GetFullPath(filePath);
        sonuc.Arazi.AraziAdi = Path.GetFileNameWithoutExtension(filePath);
        return sonuc;
    }

    public AraziImportSonucu Parse(DxfDocument dxf)
    {
        ArgumentNullException.ThrowIfNull(dxf);
        double? dosyaOlcegi = BirimOlcegi(dxf.DrawingVariables.InsUnits);
        double scale = MetrePerBirim ?? dosyaOlcegi ?? UnitlessScaleMeters;
        if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(MetrePerBirim), "Birim ölçeği pozitif ve sonlu olmalıdır.");
        if (!double.IsFinite(EtiketEslesmeToleransiM) || EtiketEslesmeToleransiM <= 0)
            throw new ArgumentOutOfRangeException(nameof(EtiketEslesmeToleransiM));
        var sonuc = new AraziImportSonucu
        {
            KaynakBirim = dxf.DrawingVariables.InsUnits.ToString(), UygulananOlcek = scale,
            BirimOnayiGerekiyor = MetrePerBirim is null && dosyaOlcegi is null
        };
        if (sonuc.BirimOnayiGerekiyor)
            sonuc.Uyarilar.Add($"Çizim birimi tanımsız/desteklenmiyor. Önizlemede 1 birim = {scale.ToString(CultureInfo.InvariantCulture)} m; yüzey oluşturmadan önce birimi seçin.");

        var tum = ElemanlariAc(dxf.Entities.All, sonuc).ToList();
        sonuc.Katmanlar = tum.GroupBy(e => e.Layer.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AraziImportKatmani
            {
                Ad = g.Key, ElemanSayisi = g.Count(), NoktaSayisi = g.Count(e => e is netDxf.Entities.Point),
                KonturSayisi = g.Count(e => e is Polyline2D or Polyline3D),
                KotMetniSayisi = g.Count(e => MetniAl(e) is string s && KotCoz(s, out _, out _)),
                AraziKatmaniOnerisi = AraziKatmanRegex.IsMatch(g.Key)
            }).OrderByDescending(k => k.AraziKatmaniOnerisi).ThenBy(k => k.Ad).ToList();
        // Kontur katmanları bulunan karma disiplinli projelerde yapı/tesisat kotlarını önerme.
        var konturKatmanlari = sonuc.Katmanlar.Where(k => Regex.IsMatch(k.Ad,
            @"KONTUR|CONTOUR|ESYUKSELTI|EŞYÜKSELTİ|EĞRI|EGRI|EĞRİ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToList();
        if (konturKatmanlari.Any(k => k.KonturSayisi > 0))
            foreach (var k in sonuc.Katmanlar) k.AraziKatmaniOnerisi = k.KonturSayisi > 0 && konturKatmanlari.Contains(k);
        var yuzeyKatmanlari = tum.OfType<Face3D>().Select(f => f.Layer.Name)
            .Where(ad => Regex.IsMatch(ad, @"Y[ÜU]ZEY|SURFACE|(?:^|[_ -])TIN(?:$|[_ -])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (yuzeyKatmanlari.Length == 1)
            foreach (var k in sonuc.Katmanlar) k.AraziKatmaniOnerisi = string.Equals(k.Ad, yuzeyKatmanlari[0], StringComparison.OrdinalIgnoreCase);
        var ents = tum.Where(e => Katmanlar is null || Katmanlar.Contains(e.Layer.Name)).ToList();
        sonuc.Arazi.Kaynak = new AraziKaynakBilgisi
        {
            Birim = sonuc.KaynakBirim, MetrePerBirim = scale,
            Katmanlar = ents.Select(e => e.Layer.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList(),
            MetinKotlariOkundu = MetinKotlariniOku
        };
        var etiketler = new List<Etiket>();
        if (MetinKotlariniOku)
            foreach (var e in ents)
            {
                if (MetniAl(e) is not string s || !KotCoz(s, out double z, out bool acikKot)) continue;
                var p = e is Text t ? t.Position : ((MText)e).Position;
                if (!Sonlu(p.X * scale, p.Y * scale, z)) continue;
                // Düz sayı yalnız eşleştirmede kullanılır. Bağımsız sayı için kot/katman bağlamı gerekir.
                bool bagimsiz = acikKot || AraziKatmanRegex.IsMatch(e.Layer.Name) || Katmanlar is not null;
                etiketler.Add(new(new Point2D(p.X * scale, p.Y * scale), z, s, e.Layer.Name, bagimsiz));
            }

        var etiketIndeksi = new EtiketIndeksi(etiketler, EtiketEslesmeToleransiM);
        foreach (var pt in ents.OfType<netDxf.Entities.Point>())
        {
            double x = pt.Position.X * scale, y = pt.Position.Y * scale, z = pt.Position.Z * scale;
            if (!Sonlu(x, y, z)) { sonuc.Uyarilar.Add("Sonlu olmayan bir POINT atlandı."); continue; }
            var etiket = YakinEtiket([new(x, y)], false, etiketIndeksi);
            if (Math.Abs(z) > 1e-9)
            {
                NoktaEkle(sonuc, x, y, z, pt.Layer.Name, AraziKotKaynagi.VertexZ);
                if (etiket is not null)
                {
                    etiket.Kullanildi = true;
                    if (Math.Abs(etiket.Deger - z) > .05)
                        sonuc.Uyarilar.Add($"{pt.Layer.Name}: POINT Z={z:0.###} m ile yakın etiket ({etiket.Metin}) farklı; geometrik Z kullanıldı.");
                }
            }
            else if (etiket is not null)
            {
                etiket.Kullanildi = true;
                NoktaEkle(sonuc, x, y, etiket.Deger, pt.Layer.Name, AraziKotKaynagi.MetinEtiketi, etiket.Metin);
            }
            else if (AraziKatmanRegex.IsMatch(pt.Layer.Name) || Katmanlar is not null)
                NoktaEkle(sonuc, x, y, 0, pt.Layer.Name, AraziKotKaynagi.VertexZ);
            else sonuc.AtlananKotsuzNoktaSayisi++;
        }

        foreach (var e in ents)
        {
            if (e is Polyline3D p3 && p3.Vertexes.Count >= 2)
            {
                var vs = p3.Vertexes.Select(v => new Vector3(v.X * scale, v.Y * scale, v.Z * scale)).ToList();
                if (vs.Any(v => !Sonlu(v.X, v.Y, v.Z))) continue;
                if (vs.Max(v => v.Z) - vs.Min(v => v.Z) > 1e-6)
                {
                    // Eğimli 3B çizgiyi tek kot ortalamasına düzleştirmek topoğrafyayı bozardı.
                    foreach (var v in vs) NoktaEkle(sonuc, v.X, v.Y, v.Z, e.Layer.Name, AraziKotKaynagi.VertexZ, kaynak: AraziVeriKaynagi.Kontur);
                }
                else KonturEkle(sonuc, vs.Select(v => new Point2D(v.X, v.Y)).ToList(), p3.IsClosed,
                    vs[0].Z, AraziKotKaynagi.VertexZ, e.Layer.Name, etiketIndeksi);
            }
            else if (e is Polyline2D p2 && p2.Vertexes.Count >= 2)
            {
                if (Math.Abs(p2.Normal.X) > 1e-8 || Math.Abs(p2.Normal.Y) > 1e-8 || p2.Normal.Z < 0)
                { sonuc.Uyarilar.Add($"{e.Layer.Name}: yatay olmayan 2B polyline atlandı."); continue; }
                var pts = p2.PolygonalVertexes(24).Select(v => new Point2D(v.X * scale, v.Y * scale)).ToList();
                if (pts.Any(p => !Sonlu(p.X, p.Y, p2.Elevation * scale))) continue;
                KonturEkle(sonuc, pts, p2.IsClosed, p2.Elevation * scale, AraziKotKaynagi.DuzlemKotu, e.Layer.Name, etiketIndeksi);
            }
        }
        foreach (var t in etiketler.Where(t => !t.Kullanildi && t.Bagimsiz))
            NoktaEkle(sonuc, t.Pos.X, t.Pos.Y, t.Deger, t.Katman, AraziKotKaynagi.MetinEtiketi, t.Metin);

        var faces = ents.OfType<Face3D>().ToList();
        if (faces.Count > 0)
        {
            bool tekYuzey = sonuc.Arazi.Noktalar.Count == 0 && sonuc.Arazi.Konturlar.Count == 0 &&
                faces.All(f => f.Layer.Name.Equals(faces[0].Layer.Name, StringComparison.OrdinalIgnoreCase));
            if (tekYuzey) KaynakYuzleriEkle(sonuc, faces, scale);
            else
            {
                sonuc.Uyarilar.Add("Karma kot kaynakları seçildi. Özgün yüz sınırlarını korumak için yalnız 3DFACE yüzey katmanını seçin.");
                foreach (var face in faces)
                    foreach (var v in new[] { face.FirstVertex, face.SecondVertex, face.ThirdVertex, face.FourthVertex }.Distinct())
                        if (Sonlu(v.X * scale, v.Y * scale, v.Z * scale))
                            NoktaEkle(sonuc, v.X * scale, v.Y * scale, v.Z * scale, face.Layer.Name, AraziKotKaynagi.VertexZ, kaynak: AraziVeriKaynagi.Yuzey);
            }
        }

        sonuc.KotsuzKonturSayisi = sonuc.Arazi.Konturlar.Count(k => k.KotKaynagi == AraziKotKaynagi.Bilinmiyor);
        if (sonuc.KotsuzKonturSayisi > 0) sonuc.Uyarilar.Add($"{sonuc.KotsuzKonturSayisi} kotsuz kontur yüzeye alınmayacak.");
        if (sonuc.AtlananKotsuzNoktaSayisi > 0) sonuc.Uyarilar.Add($"{sonuc.AtlananKotsuzNoktaSayisi} Z=0 nokta kot etiketi/katman bağlamı bulunmadığı için atlandı. Sıfır kotları doğrulamak için ilgili katmanı seçin.");
        int bagimsizSayisi = etiketler.Count(t => !t.Kullanildi && t.Bagimsiz);
        if (bagimsizSayisi > 0) sonuc.Uyarilar.Add($"{bagimsizSayisi} kot, TEXT/MTEXT yerleşim konumundan okundu; yazı konumu ölçü noktasıyla aynı olmayabilir.");
        if (etiketler.Any(t => !t.Kullanildi && !t.Bagimsiz))
            sonuc.Uyarilar.Add("Eşleşmeyen düz sayılar kot kabul edilmedi. Kotları yalnız sayıyla yazılmış katmanı açıkça seçerek içe aktarabilirsiniz.");
        if (sonuc.Arazi.Noktalar.Count == 0 && sonuc.Arazi.Konturlar.All(k => k.KotKaynagi == AraziKotKaynagi.Bilinmiyor))
            sonuc.Uyarilar.Add("Okunabilir arazi kotu bulunamadı; kot katmanlarını ve çizim birimini kontrol edin.");
        sonuc.Uyarilar = sonuc.Uyarilar.Distinct().Take(100).ToList();
        return sonuc;
    }

    private static void KaynakYuzleriEkle(AraziImportSonucu sonuc, List<Face3D> faces, double scale)
    {
        var indisler = new Dictionary<AraziDugum, int>();
        var ucgenler = new List<AraziUcgen>(faces.Count);
        int atlanan = 0;
        int Indis(AraziDugum n)
        {
            if (indisler.TryGetValue(n, out int i)) return i;
            i = sonuc.Arazi.Noktalar.Count;
            indisler.Add(n, i);
            NoktaEkle(sonuc, n.X, n.Y, n.Z, faces[0].Layer.Name, AraziKotKaynagi.VertexZ, kaynak: AraziVeriKaynagi.Yuzey);
            return i;
        }
        AraziDugum Metre(Vector3 p) => new(p.X * scale, p.Y * scale, p.Z * scale);
        void Ucgen(AraziDugum a, AraziDugum b, AraziDugum c)
        {
            if (!Sonlu(a.X,a.Y,a.Z) || !Sonlu(b.X,b.Y,b.Z) || !Sonlu(c.X,c.Y,c.Z))
                throw new InvalidDataException("CAD yüzeyinde geçersiz koordinat var.");
            if (Math.Abs((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X)) < 1e-9) { atlanan++; return; }
            ucgenler.Add(new(Indis(a), Indis(b), Indis(c)));
        }
        foreach (var f in faces)
        {
            var a = Metre(f.FirstVertex); var c = Metre(f.ThirdVertex);
            Ucgen(a, Metre(f.SecondVertex), c);
            if (f.FourthVertex != f.ThirdVertex) Ucgen(a, c, Metre(f.FourthVertex));
        }
        if (atlanan > 0) sonuc.Uyarilar.Add($"{atlanan} düşey veya sıfır alanlı CAD üçgeni plan yüzeyine alınmadı.");
        sonuc.Arazi.KaynakUcgenleri = ucgenler;
    }

    private void KonturEkle(AraziImportSonucu sonuc, List<Point2D> pts, bool kapali, double z,
        AraziKotKaynagi geometrikKaynak, string katman, EtiketIndeksi etiketler)
    {
        var k = new AraziKonturu { Noktalar = pts, Kapali = kapali, KatmanAdi = katman };
        if (Math.Abs(z) > 1e-9) { k.KotM = z; k.KotKaynagi = geometrikKaynak; }
        else
        {
            var m = KatmanKotRegex.Match(katman);
            if (m.Success && SayiCoz(m.Groups[1].Value, out double layerZ))
            { k.KotM = layerZ; k.KotKaynagi = AraziKotKaynagi.KatmanAdi; }
            else if (YakinEtiket(pts, kapali, etiketler) is Etiket t)
            { t.Kullanildi = true; k.KotM = t.Deger; k.KotKaynagi = AraziKotKaynagi.MetinEtiketi; }
            else if (geometrikKaynak == AraziKotKaynagi.VertexZ && Katmanlar is not null)
            { k.KotM = 0; k.KotKaynagi = AraziKotKaynagi.VertexZ; }
        }
        // Mevcut konturun yazı etiketlerini ikinci bir ölçü noktası gibi üretme.
        if (k.KotKaynagi != AraziKotKaynagi.Bilinmiyor)
            foreach (var t in etiketler.Adaylar(pts).Where(t => !t.Kullanildi))
            {
                if (KonturaMesafeKare(t.Pos, pts, kapali) > EtiketEslesmeToleransiM * EtiketEslesmeToleransiM) continue;
                bool ayniKaynak = string.Equals(t.Katman, katman, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t.Katman, katman + "_MKOT", StringComparison.OrdinalIgnoreCase);
                if (Math.Abs(t.Deger-k.KotM) < .05) t.Kullanildi = true;
                else if (ayniKaynak)
                {
                    t.Kullanildi = true;
                    sonuc.Uyarilar.Add($"{katman}: kontur kotu {k.KotM:0.###} m ile yakın etiket ({t.Metin}) farklı; kontur kotu kullanıldı.");
                }
            }
        sonuc.Arazi.Konturlar.Add(k);
    }

    private Etiket? YakinEtiket(List<Point2D> pts, bool kapali, EtiketIndeksi etiketler)
    {
        Etiket? best = null;
        double mesafe = EtiketEslesmeToleransiM * EtiketEslesmeToleransiM;
        foreach (var t in etiketler.Adaylar(pts).Where(t => !t.Kullanildi))
        {
            double d = KonturaMesafeKare(t.Pos, pts, kapali);
            if (d < mesafe) { mesafe = d; best = t; }
        }
        return best;
    }

    private static double KonturaMesafeKare(Point2D p, List<Point2D> pts, bool kapali)
    {
        double d = pts.Count == 1 ? MesafeKare(p, pts[0]) : double.MaxValue;
        for (int i = 0; i < (kapali ? pts.Count : pts.Count - 1); i++)
            d = Math.Min(d, SegmentMesafeKare(p, pts[i], pts[(i + 1) % pts.Count]));
        return d;
    }

    /// X sıralı arama, her nokta için bütün etiketleri tekrar taramayı önler.
    private sealed class EtiketIndeksi(List<Etiket> etiketler, double tolerans)
    {
        private readonly Etiket[] _sirali = etiketler.OrderBy(t => t.Pos.X).ToArray();
        public IEnumerable<Etiket> Adaylar(List<Point2D> pts)
        {
            if (pts.Count == 0) yield break;
            double minX=pts.Min(p=>p.X)-tolerans, maxX=pts.Max(p=>p.X)+tolerans;
            double minY=pts.Min(p=>p.Y)-tolerans, maxY=pts.Max(p=>p.Y)+tolerans;
            int lo=0, hi=_sirali.Length;
            while(lo<hi) {int mid=lo+(hi-lo)/2;if(_sirali[mid].Pos.X<minX)lo=mid+1;else hi=mid;}
            for(int i=lo;i<_sirali.Length && _sirali[i].Pos.X<=maxX;i++)
                if(_sirali[i].Pos.Y>=minY && _sirali[i].Pos.Y<=maxY)yield return _sirali[i];
        }
    }

    private static void NoktaEkle(AraziImportSonucu s, double x, double y, double z, string katman,
        AraziKotKaynagi kotKaynak, string? etiket = null, AraziVeriKaynagi kaynak = AraziVeriKaynagi.SpotKot)
    {
        s.Arazi.Noktalar.Add(new(x, y, z, kaynak) { KotKaynagi = kotKaynak, KatmanAdi = katman, KaynakEtiketi = etiket });
        if (kotKaynak == AraziKotKaynagi.MetinEtiketi) s.MetindenKotSayisi++; else s.GeometrikKotSayisi++;
    }

    private static IEnumerable<EntityObject> ElemanlariAc(IEnumerable<EntityObject> entities, AraziImportSonucu sonuc, int depth = 0)
    {
        foreach (var e in entities)
        {
            if (e.Owner?.Record?.Layout?.IsPaperSpace == true) continue;
            if (e is not Insert insert) { yield return e; continue; }
            if (depth >= 8) { sonuc.Uyarilar.Add("İç içe blok sınırına ulaşıldı; bazı bloklar atlandı."); continue; }
            List<EntityObject> parts;
            try { parts = insert.Explode(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            { sonuc.Uyarilar.Add($"{insert.Block.Name} bloğu çözülemedi: {ex.Message}"); continue; }
            foreach (var part in parts)
                if (part.Layer.Name == "0") part.Layer = insert.Layer;
            foreach (var part in ElemanlariAc(parts, sonuc, depth + 1)) yield return part;
        }
    }

    private static string? MetniAl(EntityObject e) => e switch { Text t => t.Value, MText t => t.PlainText(), _ => null };
    private static bool KotCoz(string raw, out double value, out bool acikKot)
    {
        string s = raw.Trim();
        var m = KotRegex.Match(s);
        acikKot = m.Success && (m.Groups[1].Index > 0 || "+-±−".Contains(m.Groups[1].Value[0]));
        value = 0;
        return m.Success && SayiCoz(m.Groups[1].Value, out value);
    }
    private static bool SayiCoz(string s, out double v) => double.TryParse(s.Replace(',', '.').Replace('−', '-').Replace("±", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && double.IsFinite(v);
    private static bool Sonlu(double x, double y, double z) => double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z);
    private static double MesafeKare(Point2D a, Point2D b) => (a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y);
    private static double SegmentMesafeKare(Point2D p, Point2D a, Point2D b)
    {
        double l = MesafeKare(a, b);
        if (l == 0) return MesafeKare(p, a);
        double t = Math.Clamp(((p.X-a.X)*(b.X-a.X)+(p.Y-a.Y)*(b.Y-a.Y))/l, 0, 1);
        return MesafeKare(p, new(a.X+t*(b.X-a.X), a.Y+t*(b.Y-a.Y)));
    }
    private static double? BirimOlcegi(DrawingUnits units) => units switch
    {
        DrawingUnits.Millimeters => .001, DrawingUnits.Centimeters => .01,
        DrawingUnits.Decimeters => .1, DrawingUnits.Meters => 1, DrawingUnits.Kilometers => 1000,
        DrawingUnits.Inches => .0254, DrawingUnits.Feet => .3048, DrawingUnits.Yards => .9144,
        DrawingUnits.Miles => 1609.344, DrawingUnits.USSurveyFeet => 1200.0 / 3937.0, _ => null
    };
}
