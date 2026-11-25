using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;

int passed = 0;
void Check(bool value, string title)
{
    if (!value) throw new Exception(title);
    Console.WriteLine("PASS " + title); passed++;
}
double Distance(Point2D a, Point2D b) => Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2));

// Two real wall runs, two open leaves and two CAD swing arcs. Both arc
// endpoints belong to source linework, so endpoint-to-line distance alone
// cannot select the closed leaf. The meeting point is slightly off axis,
// as in the Beylerbeyi drawing.
BinaKat DoubleDoor(double rotation = 0, bool withArcs = true)
{
    double r = Math.Sqrt(1.01), a = Math.Atan2(1,.1)*180/Math.PI;
    Point2D Rotate(Point2D p) => new(p.X*Math.Cos(rotation)-p.Y*Math.Sin(rotation),
        p.X*Math.Sin(rotation)+p.Y*Math.Cos(rotation));
    var kat = new BinaKat { KaynakKatmanlari = ["DUVAR", "KAPI"] };
    void Line(Point2D p,Point2D q)=>kat.HamTespitSegmentler.Add((Rotate(p),Rotate(q)));
    Line(new(0,0),new(8,0)); Line(new(8,0),new(8,6));
    Line(new(8,6),new(0,6)); Line(new(0,6),new(0,0));
    Line(new(4,0),new(4,2)); Line(new(4,4),new(4,6));
    Line(new(4,2),new(4+r,2)); Line(new(4,4),new(4+r,4));
    if (withArcs)
    {
        foreach (var (center,start,end) in new[] { (new Point2D(4,2),0d,a), (new Point2D(4,4),360-a,360d) })
        {
            kat.DxfArclar.Add(new BinaDxfArc { Merkez=Rotate(center),R=r,
                BasAciDeg=start+rotation*180/Math.PI,BitAciDeg=end+rotation*180/Math.PI });
            Point2D At(double deg)=>new(center.X+r*Math.Cos(deg*Math.PI/180),center.Y+r*Math.Sin(deg*Math.PI/180));
            for(int i=0;i<16;i++) Line(At(start+(end-start)*i/16),At(start+(end-start)*(i+1)/16));
        }
    }
    kat.HamSegmentler = kat.HamTespitSegmentler.ToList();
    kat.HamMetinler.Add((Rotate(new(2,3)),"HOL"));
    kat.HamMetinler.Add((Rotate(new(6,3)),"OTURMA ALANI"));
    return kat;
}

var floor=DoubleDoor();
var arcs=floor.DxfArclar.Select(a=>(a.Merkez,a.R,a.BasAciDeg,a.BitAciDeg)).ToList();
var openings=AciklikTespitServisi.Bul(floor.HamTespitSegmentler,arcs,[]).Where(a=>a.Kaynak=="arc").ToList();
Check(openings.Count==2 && openings.All(a=>Distance(a.Bit,new(4.1,3))<1e-8),
    "Opposing CAD arcs select their shared closed endpoint, despite open leaf lines");
Check(DxfKatPlaniParser.OdalariBosluklardanYenidenTara(floor)==2,
    "Double leaf doorway separates two rooms through the production parser");
Check(floor.Odalar.All(o=>o.AlanM2>22 && o.AlanM2<26),
    "Room closure keeps the two approximately 24 square metre footprints");
Check(floor.Odalar.Any(o=>o.Ad=="HOL") && floor.Odalar.Any(o=>o.Ad=="OTURMA ALANI"),
    "Room names remain associated with their respective closed polygons");
var reverse=AciklikTespitServisi.Bul(floor.HamTespitSegmentler,arcs.AsEnumerable().Reverse().ToList(),[]);
Check(reverse.Count(a=>a.Kaynak=="arc" && Distance(a.Bit,new(4.1,3))<1e-8)==2,
    "Closed direction is independent of arc iteration order");

var rotated=DoubleDoor(27*Math.PI/180);
Check(DxfKatPlaniParser.OdalariBosluklardanYenidenTara(rotated)==2,
    "Oblique double leaf doorway also separates rooms");
var openPlan=DoubleDoor(withArcs:false);
Check(DxfKatPlaniParser.OdalariBosluklardanYenidenTara(openPlan)==1,
    "Two labels without a physical door boundary stay one connected room");
Check(openPlan.Odalar.Single().AlanM2>44,
    "An open hall is not split by invented label partitions");

var ambiguous=arcs.Append(arcs[0]).ToList();
var ambiguousFound=AciklikTespitServisi.Bul(floor.HamTespitSegmentler,ambiguous,[]);
Check(ambiguousFound.Any(o=>Distance(o.Bit,new(4.1,3))>.5),
    "Duplicate candidate arcs do not force an arbitrary pair assignment");

var anchoredWall = new List<(Point2D A,Point2D B)> { (new(-2,0),new(0,0)) };
var unanchoredPair = new List<(Point2D Merkez,double R,double BasDeg,double BitDeg)> {
    (new(0,0),1,0,90), (new(0,2),1,270,360)
};
var anchored=AciklikTespitServisi.Bul(anchoredWall,unanchoredPair,[]);
Check(anchored.Count==1 && Distance(anchored[0].Bit,new(1,0))<1e-8,
    "A floating arc cannot redirect the anchored door's closed endpoint");

var unequal = new List<(Point2D Merkez,double R,double BasDeg,double BitDeg)> {
    (new(0,0),.6,0,90), (new(1.6,0),1,90,180)
};
var baseWall = new List<(Point2D A,Point2D B)> { (new(-2,0),new(3,0)) };
var unequalFound=AciklikTespitServisi.Bul(baseWall,unequal,[]);
Check(unequalFound.Count==2 && unequalFound.All(a=>Distance(a.Bit,new(.6,0))<1e-8),
    "Unequal double leaves meet at their shared physical endpoint");

Console.WriteLine($"{passed}/{passed} passed");
