using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;

var cases = new (string Name, Action Check)[]
{
    ("Türkçe ve sayısal kat başlıkları", () =>
    {
        foreach (var (text, order) in new (string, double)[] { ("ZEMİN KAT PLANI", 0), ("A BLOK 1.NORMAL KAT PLANI 1/50", 1), ("2. BODRUM KAT PLANI", -2), ("BODRUM 2", -2), ("ASMA KAT", .5), ("ÇATI KATI", 9999), ("3-5. KATLAR PLANI", 3), ("ZEMİN KAT PLANI (+0.00)", 0) })
        {
            var result = KatEtiketServisi.TahminEt(Cluster(text));
            Check(result.EtiketBulundu && result.SiraDegeri == order, text);
        }
    }),
    ("Kaplama ve çatı notları kat başlığı olmaz", () =>
    {
        var result = KatEtiketServisi.TahminEt(Cluster("ZEMİN KAPLAMASI", "ÇATI İZOLASYONU", "BODRUM DUVARI", "2. KAT TESİSAT ŞAFTI"));
        Check(!result.EtiketBulundu, "Malzeme notu kat olarak yorumlandı.");
    }),
    ("Plan başlığı referans kat yazısından önce gelir", () =>
    {
        var result = KatEtiketServisi.TahminEt(Cluster("ÇATI KATI", "1. KAT PLANI"));
        Check(result.EtiketBulundu && result.SiraDegeri == 1, "Plan başlığı seçilmedi.");
    }),
    ("Çelişen plan başlıkları belirsiz kalır", () =>
    {
        var result = KatEtiketServisi.TahminEt(Cluster("ZEMİN KAT PLANI", "1. KAT PLANI"));
        Check(!result.EtiketBulundu, "Birleşik paftada rastgele kat adı seçildi.");
    }),
    ("Çizim dışındaki başlık tek yakın kümeye atanır", () =>
    {
        var segments = Rect(0).Concat(Rect(40)).ToList();
        var clusters = KatKumelemeServisi.Kumele(segments, [(new(5,-3), "ZEMİN KAT PLANI"), (new(45,-3), "1. KAT PLANI"), (new(5,-2), "SERAMİK KAPLAMA")], []);
        Check(clusters.Count == 2, "İki küme bulunmadı.");
        Check(clusters[0].Metinler.Count == 1 && clusters[1].Metinler.Count == 1, "Dış başlık ataması/filtreleme hatalı.");
        Check(KatEtiketServisi.TahminEt(clusters[0]).SiraDegeri == 0 && KatEtiketServisi.TahminEt(clusters[1]).SiraDegeri == 1, "Başlıklar yanlış çizime atandı.");
    }),
    ("İki çizime eşit uzaklıktaki başlık atanmaz", () =>
    {
        var clusters = KatKumelemeServisi.Kumele(Rect(0).Concat(Rect(40)).ToList(), [(new(25,4), "1. KAT PLANI")], [], new() { DisBaslikMesafesiM = 20 });
        Check(clusters.All(c => c.Metinler.Count == 0), "Belirsiz başlık bir çizime atandı.");
    }),
    ("Kesit içindeki kat etiketleri yeni kat oluşturmaz", () =>
    {
        var plan = Cluster("ZEMİN KAT PLANI");
        foreach (var title in new[] { "A-A KESİTİ", "KUZEY GÖRÜNÜŞÜ 1/50", "VAZİYET PLANI", "PENCERE DETAYLARI" })
        {
            var section = Cluster(title, "ZEMİN KAT", "1. KAT");
            var (plans, other) = KatKumelemeServisi.PlanFiltrele([plan, section]);
            Check(plans.Count == 1 && other.Count == 1 && ReferenceEquals(plans[0], plan), title);
        }
    }),
    ("Etiketsiz mevcut büyük plan adayı korunur", () =>
    {
        var (plans, other) = KatKumelemeServisi.PlanFiltrele([Cluster("SALON")]);
        Check(plans.Count == 1 && other.Count == 0, "Etiketsiz plan uyumluluğu bozuldu.");
    }),
    ("Antetteki kopya başlık ölçekli plan başlığını ezmez", () =>
    {
        var result = KatEtiketServisi.TahminEt(Cluster("ZEMİN KAT PLANI", "1. KAT PLANI 1/50"));
        Check(result.EtiketBulundu && result.SiraDegeri == 1, "Kopya antet başlığı önceliklendirildi.");
        Check(KatEtiketServisi.PlanDisiBasligiMi("ZEMİN KAT TAVAN PLANI 1/50"), "Tavan planı ikinci bina katı sayıldı.");
        Check(KatEtiketServisi.TahminEt(Cluster("ÇATI ALTI VE TERAS PLANI 1/50")).EtiketBulundu, "Çatı/teras başlığı okunmadı.");
    }),
    ("Temas eden pafta çerçeveleri katları birleştirmez", () =>
    {
        var segs = new List<(Point2D A, Point2D B)>();
        foreach (double x in new[] { 0d, 30d })
        {
            segs.AddRange([(new(x,0),new(x,40)),(new(x,40),new(x+30,40)),(new(x+30,40),new(x+30,0)),(new(x+30,0),new(x,0))]);
            segs.AddRange(Rect(x+5).Select(s => (new Point2D(s.A.X,s.A.Y+15),new Point2D(s.B.X,s.B.Y+15))));
        }
        var clusters = KatKumelemeServisi.Kumele(segs, [(new(15,3),"ZEMİN KAT PLANI 1/50"),(new(45,3),"1. KAT PLANI 1/50")], []);
        Check(clusters.Count == 2, $"İki temas eden pafta ayrılmadı: {clusters.Count}");
        Check(clusters.All(c => c.Segmentler.Count == 4), "Pafta çerçevesi oda duvarı olarak tutuldu.");
        Check(clusters.Select(c => KatEtiketServisi.TahminEt(c).SiraDegeri).Order().SequenceEqual(new[] {0d,1d}), "Ayrılan paftalarda başlıklar yanlış.");
    }),
    ("Ölçekli başlık düzeninde antetler ve tavanlar kat sayılmaz", () =>
    {
        var ground = Cluster("ZEMİN KAT PLANI 1/50", "ZEMİN KAT PLANI");
        var first = Cluster("1. KAT PLANI 1/50", "ZEMİN KAT PLANI");
        var roof = Cluster("ÇATI ALTI VE TERAS PLANI 1/50", "ZEMİN KAT PLANI");
        var ceiling = Cluster("ZEMİN KAT TAVAN PLANI 1/50", "1. KAT PLANI 1/50");
        var elevation = Cluster("ZEMİN KAT PLANI", "ZEMİN KAT", "1. KAT");
        var unknown = Cluster();
        var (plans, others) = KatKumelemeServisi.PlanFiltrele([ground,first,roof,ceiling,elevation,unknown]);
        Check(plans.Count == 3 && others.Count == 3 && plans.Contains(ground) && plans.Contains(first) && plans.Contains(roof), "Antet veya tavan planı bina katı olarak seçildi.");
    })
};
int failed = 0;
foreach (var test in cases)
{
    try { test.Check(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
Console.WriteLine($"{cases.Length - failed}/{cases.Length} geçti.");
return failed == 0 ? 0 : 1;

static List<(Point2D A, Point2D B)> Rect(double x) => [(new(x,0),new(x+10,0)),(new(x+10,0),new(x+10,8)),(new(x+10,8),new(x,8)),(new(x,8),new(x,0))];
static KatKumelemeServisi.KatKumesi Cluster(params string[] text) => new(0,0,10,8,Rect(0),text.Select(t => (new Point2D(5,4), t)).ToList(),[]);
static void Check(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
