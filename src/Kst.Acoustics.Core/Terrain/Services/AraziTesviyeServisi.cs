using System.Collections.Concurrent;
using Clipper2Lib;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

public enum TesviyeTur { Platform, Sev }
public sealed record TesviyeNoktasi(double MevcutKotM, double HedefKotM, TasarimBolgesi Bolge, TesviyeTur Tur);
public readonly record struct TesviyeSiniri(double MinX, double MinY, double MaxX, double MaxY);

/// <summary>
/// Bölge içlerinde yatay platform; dışarıda en yakın kenara ait 1D:nY şevi.
/// Şev her dışa doğru ışında mevcut TIN ile ilk kesişimde biter; sonraki tepelerde yeniden başlamaz.
/// Platformlar şevlerden önceliklidir. Birden çok şev kesişirse listedeki ilk bölge kullanılır ve uyarılır.
/// </summary>
public sealed class AraziTesviyeServisi
{
    private readonly AraziKotIndeksi _kot;
    private readonly BolgeVerisi[] _bolgeler;
    private readonly ConcurrentDictionary<IsinAnahtari, IsinSonucu> _isinlar = new();
    private readonly ConcurrentDictionary<string, byte> _uyarilar = new(StringComparer.Ordinal);
    public IReadOnlyList<string> Uyarilar => _uyarilar.Keys.OrderBy(s => s, StringComparer.CurrentCulture).ToArray();
    public TesviyeSiniri Sinir { get; }
    public IReadOnlyList<TasarimBolgesi> Bolgeler { get; }
    public AraziTesviyeServisi(AraziYuzeyi mevcut, TasarimYuzeyi tasarim)
        : this(new AraziKotIndeksi(mevcut), tasarim) { }

    public AraziTesviyeServisi(AraziKotIndeksi indeks, TasarimYuzeyi tasarim)
    {
        _kot = indeks ?? throw new ArgumentNullException(nameof(indeks));
        ArgumentNullException.ThrowIfNull(tasarim);
        if(tasarim.Bolgeler.Count>200 || tasarim.Bolgeler.Sum(b=>b.Poligon.Count)>4000)
            throw new ArgumentException("Tasarım en fazla 200 bölge ve toplam 4000 köşe içerebilir.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<BolgeVerisi>();
        foreach(var b in tasarim.Bolgeler)
        {
            if(b.Tip is not (TasarimBolgeTipi.Platform or TasarimBolgeTipi.Kademe))
                throw new ArgumentException($"{b.Ad}: bağımsız şev bölgesi desteklenmiyor; platformun kenarlarını şev olarak ayarlayın.");
            AraziTasarimServisi.SiniriDogrula(b.Poligon);
            if(!double.IsFinite(b.HedefKotM) || !double.IsFinite(b.YatayDuseyOrani) || b.YatayDuseyOrani<=0 || b.YatayDuseyOrani>1000 ||
                !double.IsFinite(b.SevAzamiMesafeM) || b.SevAzamiMesafeM<=0 || b.SevAzamiMesafeM>10000 || !Enum.IsDefined(b.KenarGecisi))
                throw new ArgumentException($"{b.Ad}: kot, şev oranı veya azami mesafe geçersiz (oran 0–1000; mesafe 0–10000 m).");
            if(b.KenarAyarlari.Any(k=>k.KenarIndex<0||k.KenarIndex>=b.Poligon.Count||!Enum.IsDefined(k.Tip)||
                k.YatayDuseyOrani is double r&&(!double.IsFinite(r)||r<=0||r>1000)) ||
                b.KenarAyarlari.Select(k=>k.KenarIndex).Distinct().Count()!=b.KenarAyarlari.Count)
                throw new ArgumentException($"{b.Ad}: kenar ayarları geçersiz veya yineleniyor.");
            if(string.IsNullOrWhiteSpace(b.Id)||!ids.Add(b.Id)) throw new ArgumentException("Bölge kimlikleri boş veya yineleniyor.");
            var clone = Kopyala(b);
            bool sev=Enumerable.Range(0,b.Poligon.Count).Any(i=>b.KenarTipi(i)==TasarimKenarTipi.Sev);
            var box=new TesviyeSiniri(b.Poligon.Min(p=>p.X),b.Poligon.Min(p=>p.Y),b.Poligon.Max(p=>p.X),b.Poligon.Max(p=>p.Y));
            double pay=sev?b.SevAzamiMesafeM:0;
            // TIN'in tüm kot aralığını geçen şev mutlaka daha önce zemine değmiştir;
            // bu güvenli üst sınır düz arazide boş 50 m tampon taramasını önler.
            if(sev && _kot.MinKotM is double minZ && _kot.MaxKotM is double maxZ)
            {
                double maxRatio=Enumerable.Range(0,b.Poligon.Count).Where(i=>b.KenarTipi(i)==TasarimKenarTipi.Sev).Max(b.KenarOrani);
                pay=Math.Min(pay,Math.Max(Math.Abs(minZ-b.HedefKotM),Math.Abs(maxZ-b.HedefKotM))*maxRatio);
            }
            list.Add(new(clone,box,pay));
        }
        _bolgeler=list.ToArray(); Bolgeler=_bolgeler.Select(b=>b.Bolge).ToArray();
        for(int i=0;i<_bolgeler.Length;i++) for(int j=i+1;j<_bolgeler.Length;j++)
            if(KutuKesisir(_bolgeler[i].Kutu,_bolgeler[j].Kutu) && PozitifKesisim(_bolgeler[i].Bolge.Poligon,_bolgeler[j].Bolge.Poligon))
                throw new ArgumentException($"'{_bolgeler[i].Bolge.Ad}' ve '{_bolgeler[j].Bolge.Ad}' platformları üst üste geliyor. Sınırları ayırın; ortak kenar kullanılabilir.");
        Sinir=_bolgeler.Length==0?new(0,0,0,0):new(_bolgeler.Min(b=>b.Kutu.MinX-b.Pay),_bolgeler.Min(b=>b.Kutu.MinY-b.Pay),
            _bolgeler.Max(b=>b.Kutu.MaxX+b.Pay),_bolgeler.Max(b=>b.Kutu.MaxY+b.Pay));
        // Uçların ön kontrolü: her şev kenarında en fazla 5 dış normal ışın.
        foreach(var b in _bolgeler) KenarlariKontrolEt(b);
    }

    public double? KotSorgula(double x,double y)=>Sorgula(x,y)?.HedefKotM??_kot.KotSorgula(x,y);
    public TasarimBolgesi? PlatformSorgula(double x,double y)
    {
        foreach(var b in _bolgeler)
            if(Kutuda(x,y,b.Kutu,0)&&IcindeVeyaSinirda(new(x,y),b.Bolge.Poligon)) return b.Bolge;
        return null;
    }
    public TesviyeNoktasi? Sorgula(double x,double y)
    {
        if(!double.IsFinite(x)||!double.IsFinite(y)||_bolgeler.Length==0||!Kutuda(x,y,Sinir,0)) return null;
        double? mevcut=_kot.KotSorgula(x,y);
        if(mevcut is null) return null;
        var platform=PlatformSorgula(x,y);
        if(platform is not null) return new(mevcut.Value,platform.HedefKotM,platform,TesviyeTur.Platform);
        TesviyeNoktasi? first=null;
        foreach(var b in _bolgeler)
        {
            if(b.Pay<=0||!Kutuda(x,y,b.Kutu,b.Pay))continue;
            var (q,edge,d)=EnYakinKenar(new(x,y),b.Bolge);
            if(d<1e-8 || d>b.Pay+1e-8 || b.Bolge.KenarTipi(edge)!=TasarimKenarTipi.Sev)continue;
            var ray=IsinBul(b,edge,q,(x-q.X)/d,(y-q.Y)/d);
            if(d>ray.Mesafe+1e-7||ray.Mesafe<=1e-8)continue;
            double hedef=b.Bolge.HedefKotM+ray.Yon*d/b.Bolge.KenarOrani(edge);
            if(Math.Abs(hedef-mevcut.Value)<1e-8)continue;
            var sample=new TesviyeNoktasi(mevcut.Value,hedef,b.Bolge,TesviyeTur.Sev);
            if(first is null)first=sample;
            else Uyar($"'{first.Bolge.Ad}' ve '{b.Bolge.Ad}' şevleri örtüşüyor; listedeki ilk bölge ('{first.Bolge.Ad}') öncelikli.");
        }
        return first;
    }

    private IsinSonucu IsinBul(BolgeVerisi b,int edge,Point2D p,double ux,double uy)
    {
        var key=new IsinAnahtari(b.Bolge.Id,edge,p.X,p.Y,ux,uy);
        if(_isinlar.TryGetValue(key,out var cached))return cached;
        var ray=IsiniHesapla(b,edge,p,ux,uy);
        if(_isinlar.Count<100000)_isinlar.TryAdd(key,ray);
        return ray;
    }
    private IsinSonucu IsiniHesapla(BolgeVerisi b,int edge,Point2D p,double ux,double uy)
    {
        double? bas=_kot.KotSorgula(p.X,p.Y);
        if(bas is null){AcikUyari(b,edge,"kenar başlangıcı arazi kapsamı dışında");return new(0,0);}
        double diff=bas.Value-b.Bolge.HedefKotM;
        if(Math.Abs(diff)<1e-8)return new(0,0);
        int yon=diff>0?1:-1;
        double mesafe=b.Bolge.SevAzamiMesafeM, oran=b.Bolge.KenarOrani(edge), covered=0;
        var profile=_kot.ProfilParcalari(p.X,p.Y,p.X+ux*mesafe,p.Y+uy*mesafe);
        foreach(var part in profile)
        {
            if(part.BasT>covered+1e-7/mesafe)break;
            if(part.BitT<=covered+1e-12)continue;
            double start=Math.Max(part.BasT,covered), end=part.BitT;
            double zstart=part.BasKotM+(part.BitKotM-part.BasKotM)*(start-part.BasT)/(part.BitT-part.BasT);
            double fstart=yon*(zstart-b.Bolge.HedefKotM)-start*mesafe/oran;
            double fend=yon*(part.BitKotM-b.Bolge.HedefKotM)-end*mesafe/oran;
            if(fstart<=1e-8)return new(start*mesafe,yon);
            if(fend<=0)
            {
                double t=start+(end-start)*fstart/(fstart-fend);
                return new(Math.Clamp(t,0,1)*mesafe,yon);
            }
            covered=Math.Max(covered,end);
        }
        AcikUyari(b,edge,covered<1-1e-8?"arazi kapsamı bitiyor":"azami şev mesafesine ulaşılıyor");
        return new(covered*mesafe,yon);
    }
    private void KenarlariKontrolEt(BolgeVerisi b)
    {
        var pts=b.Bolge.Poligon;
        double area=0;for(int i=1;i+1<pts.Count;i++)area+=(pts[i].X-pts[0].X)*(pts[i+1].Y-pts[0].Y)-(pts[i].Y-pts[0].Y)*(pts[i+1].X-pts[0].X);
        double orientation=area>0?1:-1;
        for(int edge=0;edge<pts.Count;edge++)
        {
            if(b.Bolge.KenarTipi(edge)!=TasarimKenarTipi.Sev)continue;
            var a=pts[edge];var z=pts[(edge+1)%pts.Count];double dx=z.X-a.X,dy=z.Y-a.Y,length=double.Hypot(dx,dy);
            foreach(double t in new[]{.001,.25,.5,.75,.999})
                IsinBul(b,edge,new(a.X+dx*t,a.Y+dy*t),orientation*dy/length,-orientation*dx/length);
        }
    }
    private void AcikUyari(BolgeVerisi b,int edge,string neden)=>Uyar($"{b.Bolge.Ad}, kenar {edge+1}: şev zemine bağlanmadan {neden}. Bu şev açık uçlu; mesafeyi/sınırı kontrol edin.");
    private void Uyar(string text)=>_uyarilar.TryAdd(text,0);
    public static bool IcindeVeyaSinirda(Point2D p,IReadOnlyList<Point2D> polygon)
    {
        for(int i=0;i<polygon.Count;i++)
            if(Uzaklik(p,polygon[i],polygon[(i+1)%polygon.Count]).Mesafe<1e-8)return true;
        return PoligonYardimci.IsPointInside(p,polygon);
    }
    private static (Point2D Nokta,int Kenar,double Mesafe) EnYakinKenar(Point2D p,TasarimBolgesi b)
    {
        Point2D nearest=default;int edge=-1;double best=double.PositiveInfinity;
        for(int i=0;i<b.Poligon.Count;i++)
        {
            var (q,d)=Uzaklik(p,b.Poligon[i],b.Poligon[(i+1)%b.Poligon.Count]);
            if(d<best-1e-8||Math.Abs(d-best)<=1e-8&&b.KenarTipi(i)==TasarimKenarTipi.DikKesim)
                {best=d;nearest=q;edge=i;}
        }
        return(nearest,edge,best);
    }
    private static (Point2D Nokta,double Mesafe) Uzaklik(Point2D p,Point2D a,Point2D b)
    {
        double dx=b.X-a.X,dy=b.Y-a.Y,l=dx*dx+dy*dy;
        double t=Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/l,0,1);
        var q=new Point2D(a.X+t*dx,a.Y+t*dy);return(q,double.Hypot(p.X-q.X,p.Y-q.Y));
    }
    private static bool Kutuda(double x,double y,TesviyeSiniri b,double pay)=>x>=b.MinX-pay-1e-8&&x<=b.MaxX+pay+1e-8&&y>=b.MinY-pay-1e-8&&y<=b.MaxY+pay+1e-8;
    private static bool KutuKesisir(TesviyeSiniri a,TesviyeSiniri b)=>a.MinX<b.MaxX&&a.MaxX>b.MinX&&a.MinY<b.MaxY&&a.MaxY>b.MinY;
    private static bool PozitifKesisim(IReadOnlyList<Point2D> a,IReadOnlyList<Point2D> b)
    {
        var origin=a[0]; const double scale=1e6;
        Path64 Convert(IReadOnlyList<Point2D> poly)=>new(poly.Select(p=>new Point64(checked((long)Math.Round((p.X-origin.X)*scale)),checked((long)Math.Round((p.Y-origin.Y)*scale)))));
        var intersection=Clipper.Intersect(new Paths64{Convert(a)},new Paths64{Convert(b)},FillRule.NonZero);
        return Math.Abs(Clipper.Area(intersection))/(scale*scale)>1e-8;
    }
    public static TasarimBolgesi Kopyala(TasarimBolgesi b)=>new()
    {
        Id=b.Id,Ad=b.Ad,Tip=b.Tip,HedefKotM=b.HedefKotM,SevEgimi=b.SevEgimi,Poligon=b.Poligon.ToList(),
        KenarGecisi=b.KenarGecisi,YatayDuseyOrani=b.YatayDuseyOrani,SevAzamiMesafeM=b.SevAzamiMesafeM,
        KenarAyarlari=b.KenarAyarlari.Select(k=>new TasarimKenarAyari{KenarIndex=k.KenarIndex,Tip=k.Tip,YatayDuseyOrani=k.YatayDuseyOrani}).ToList()
    };
    private sealed record BolgeVerisi(TasarimBolgesi Bolge,TesviyeSiniri Kutu,double Pay);
    private readonly record struct IsinAnahtari(string Id,int Kenar,double X,double Y,double Ux,double Uy);
    private readonly record struct IsinSonucu(double Mesafe,int Yon);
}
