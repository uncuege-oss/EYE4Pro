using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

namespace Kst.Acoustics.App.Electric.ZayifAkimTekHat;

/// <summary>
/// Data sistemi tek hat şeması: ana merkez (solda) ↔ kat rack kabinleri (sağda).
/// Her rack kutusu data/wireless sayısını ve içindeki cihazları gösterir;
/// rack→merkez bağlantısı kablo tipi ve metrajıyla etiketlenir.
/// </summary>
public sealed class ElvDataSemaControl : Control
{
    private readonly List<(Rect Alan, ElvRackKabin? Rack, ElvYanginKatLoopGrubu? YanginGrubu, bool AnaMerkez)> secimAlanlari = [];
    private readonly List<(Rect Alan, ElvAnonsZonu Zon)> analogSecimAlanlari = [];
    private readonly List<(Rect Alan, ElvCctvIzlemeNoktasi Nokta)> izlemeSecimAlanlari = [];

    public event EventHandler<SemaSecimEventArgs>? SecimIstendi;

    public static readonly StyledProperty<ElvDataSistemi?> DataSistemiProperty =
        AvaloniaProperty.Register<ElvDataSemaControl, ElvDataSistemi?>(nameof(DataSistemi));

    public static readonly StyledProperty<ElvKameraSistemi?> KameraSistemiProperty =
        AvaloniaProperty.Register<ElvDataSemaControl, ElvKameraSistemi?>(nameof(KameraSistemi));

    public static readonly StyledProperty<ElvAnonsSistemi?> AnonsSistemiProperty =
        AvaloniaProperty.Register<ElvDataSemaControl, ElvAnonsSistemi?>(nameof(AnonsSistemi));

    public static readonly StyledProperty<ElvTvSistemi?> TvSistemiProperty =
        AvaloniaProperty.Register<ElvDataSemaControl, ElvTvSistemi?>(nameof(TvSistemi));

    public static readonly StyledProperty<ElvYanginSistemi?> YanginSistemiProperty =
        AvaloniaProperty.Register<ElvDataSemaControl, ElvYanginSistemi?>(nameof(YanginSistemi));

    public static readonly StyledProperty<ElvTelefonSistemi?> TelefonSistemiProperty =
        AvaloniaProperty.Register<ElvDataSemaControl, ElvTelefonSistemi?>(nameof(TelefonSistemi));

    public static readonly StyledProperty<ElvDiafonSistemi?> DiafonSistemiProperty =
        AvaloniaProperty.Register<ElvDataSemaControl, ElvDiafonSistemi?>(nameof(DiafonSistemi));

    public static readonly StyledProperty<string> SistemTuruProperty =
        AvaloniaProperty.Register<ElvDataSemaControl, string>(nameof(SistemTuru), "Data");

    public static readonly StyledProperty<List<ELVKatInfo>> KatlarProperty =
        AvaloniaProperty.Register<ElvDataSemaControl, List<ELVKatInfo>>(nameof(Katlar), new List<ELVKatInfo>());

    public static readonly StyledProperty<int> RevizyonProperty =
        AvaloniaProperty.Register<ElvDataSemaControl, int>(nameof(Revizyon));

    public ElvDataSistemi? DataSistemi
    {
        get => GetValue(DataSistemiProperty);
        set => SetValue(DataSistemiProperty, value);
    }

    public ElvKameraSistemi? KameraSistemi
    {
        get => GetValue(KameraSistemiProperty);
        set => SetValue(KameraSistemiProperty, value);
    }

    public ElvAnonsSistemi? AnonsSistemi
    {
        get => GetValue(AnonsSistemiProperty);
        set => SetValue(AnonsSistemiProperty, value);
    }

    public ElvTvSistemi? TvSistemi
    {
        get => GetValue(TvSistemiProperty);
        set => SetValue(TvSistemiProperty, value);
    }

    public ElvYanginSistemi? YanginSistemi
    {
        get => GetValue(YanginSistemiProperty);
        set => SetValue(YanginSistemiProperty, value);
    }

    public ElvTelefonSistemi? TelefonSistemi
    {
        get => GetValue(TelefonSistemiProperty);
        set => SetValue(TelefonSistemiProperty, value);
    }

    public ElvDiafonSistemi? DiafonSistemi
    {
        get => GetValue(DiafonSistemiProperty);
        set => SetValue(DiafonSistemiProperty, value);
    }

    public string SistemTuru
    {
        get => GetValue(SistemTuruProperty);
        set => SetValue(SistemTuruProperty, value);
    }

    public List<ELVKatInfo> Katlar
    {
        get => GetValue(KatlarProperty);
        set => SetValue(KatlarProperty, value);
    }

    public int Revizyon
    {
        get => GetValue(RevizyonProperty);
        set => SetValue(RevizyonProperty, value);
    }

    static ElvDataSemaControl()
    {
        AffectsRender<ElvDataSemaControl>(DataSistemiProperty, KameraSistemiProperty, AnonsSistemiProperty, TvSistemiProperty, YanginSistemiProperty, TelefonSistemiProperty, DiafonSistemiProperty, SistemTuruProperty, KatlarProperty, RevizyonProperty);
        AffectsMeasure<ElvDataSemaControl>(DataSistemiProperty, KameraSistemiProperty, AnonsSistemiProperty, TvSistemiProperty, YanginSistemiProperty, TelefonSistemiProperty, DiafonSistemiProperty, SistemTuruProperty, KatlarProperty, RevizyonProperty);
    }

    // ── Yerleşim sabitleri ──
    private const double MerkezW = 200, MerkezX = 24;
    private const double RackW = 240, RackX = 430;
    private const double RackBaseH = 46, SatirH = 16, RackBosluk = 18;
    private const double FloorTopMargin = 50, FloorHeight = 120;

    private double RackYuksekligi(ElvRackKabin r)
        => RackBaseH + (Math.Max(1, r.Cihazlar.Count) + RackUcHatlari(r).Count) * SatirH;

    private static double IzlemeNoktasiYuksekligi(ElvCctvIzlemeNoktasi n)
        => RackBaseH + Math.Max(1, n.Cihazlar.Count) * SatirH;

    private IReadOnlyList<ElvHatMetraj> RackUcHatlari(ElvRackKabin r)
        => SistemTuru switch
        {
            "Kamera" => r.KameraHatlari,
            "Anons" when AnonsSistemi?.Teknoloji == "Dijital (PoE)" => r.DijitalAnonsUclari,
            "Telefon" => r.TelefonHatlari,
            "Diafon" => r.DiafonUniteHatlari,
            "Data" => r.DataHatlari.Concat(r.WirelessHatlari)
                .Concat(r.IpTelefonHatlari).Concat(r.YaziciHatlari)
                .Concat(r.ErisimKontrolHatlari).Concat(r.DiafonHatlari).ToList(),
            _ => []
        };

    protected override Size MeasureOverride(Size availableSize)
    {
        if (SistemTuru == "Tv" && TvSistemi?.Teknoloji == TvDagitimTeknolojisi.DirektLnb)
            return new Size(980, Math.Max(380, 90 + TvSistemi.DirektLnb.Hatlar.Count * 54));
        if (SistemTuru == "Anons" && AnonsSistemi?.Teknoloji == "Analog")
        {
            double h = 80 + AnonsSistemi.AnalogZonlar.Sum(z => Math.Max(120,
                72 + (z.HatA.HoparlorGruplari.Count + (z.HatB?.HoparlorGruplari.Count ?? 0)) * 18));
            return new Size(1180, Math.Max(380, h));
        }
        if (SistemTuru == "Yangin")
        {
            var yanginKatlar = Katlar.Count > 0 ? Katlar : AktifKatlar([]);
            var yukseklikler = YanginKatYukseklikleriniHesapla(yanginKatlar);
            double toplam = FloorTopMargin + yukseklikler.Values.Sum() + 20;
            return new Size(RackX + RackW + 40, Math.Max(360, toplam));
        }

        var rackler = AktifRackler();
        var merkez = AktifMerkez();
        if (merkez is null)
            return new Size(680, 360);

        var katlar = AktifKatlar(rackler);
        var katYukseklikleri = KatYukseklikleriniHesapla(katlar, rackler);
        double merkezH = MerkezBoyu(merkez);
        double katYuksekligi = FloorTopMargin + katlar.Sum(k => katYukseklikleri[k.KatNo]) + 20;
        double genislik = SistemTuru is "Data" or "Kamera" or "Telefon" or "Diafon"
                           || SistemTuru == "Anons" && AnonsSistemi?.Teknoloji == "Dijital (PoE)"
            ? RackX + RackW + 360
            : RackX + RackW + 40;
        return new Size(genislik, Math.Max(katYuksekligi, merkezH + FloorTopMargin + 40));
    }

    private static double MerkezBoyu(ElvMerkez m) => RackBaseH + Math.Max(1, m.Cihazlar.Count) * SatirH + 10;

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        secimAlanlari.Clear();
        analogSecimAlanlari.Clear();
        izlemeSecimAlanlari.Clear();
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#08140C")), new Rect(Bounds.Size));

        if (SistemTuru == "Anons" && AnonsSistemi?.Teknoloji == "Analog")
        {
            DrawAnalogAnonsSistemi(ctx, AnonsSistemi);
            return;
        }

        if (SistemTuru == "Tv" && TvSistemi?.Teknoloji == TvDagitimTeknolojisi.DirektLnb)
        {
            DrawDirektLnbSistemi(ctx, TvSistemi);
            return;
        }

        if (SistemTuru == "Yangin")
        {
            DrawYanginSistemi(ctx);
            return;
        }

        var merkez = AktifMerkez();
        var rackler = AktifRackler();
        var katlar = AktifKatlar(rackler);
        if (merkez is null)
        {
            DrawText(ctx, "Veri yok", new Point(24, 24), 14, "#5E8C6E");
            return;
        }

        var katYukseklikleri = KatYukseklikleriniHesapla(katlar, rackler);
        var katUstleri = KatUstleriniHesapla(katlar, katYukseklikleri);
        DrawKatBantlari(ctx, katlar, Bounds.Width, katYukseklikleri, katUstleri);

        if (rackler.Count == 0)
            DrawText(ctx, "Rack kabin yok - 'Rack Ekle' veya M12'den içe aktarın.", new Point(24, 24), 13, "#7AB890");

        // ── Rack kutuları (sağda, kat sırasına göre) ──
        var rackMerkezleri = new List<Point>();
        var rackAnchor = new Dictionary<string, Point>();
        var katKullanilanYukseklik = new Dictionary<int, double>();
        double altSinir = FloorTopMargin;
        foreach (var r in rackler)
        {
            double h = RackYuksekligi(r);
            int katNo = katUstleri.ContainsKey(r.KatNo) ? r.KatNo : katlar[0].KatNo;
            double kullanilan = katKullanilanYukseklik.TryGetValue(katNo, out var mevcut) ? mevcut : 0;
            double y = katUstleri[katNo] + 28 + kullanilan;
            katKullanilanYukseklik[katNo] = kullanilan + h + RackBosluk;
            altSinir = Math.Max(altSinir, y + h);
            var rect = new Rect(RackX, y, RackW, h);
            secimAlanlari.Add((rect, r, null, false));
            DrawKutu(ctx, rect, "#0E2614", "#2E7D46");

            DrawText(ctx, r.Ad, new Point(RackX + 10, y + 7), 12.5, "#CFF0D8", bold: true);
            DrawText(ctx, $"{r.KatAdi}", new Point(RackX + RackW - 96, y + 8), 10.5, "#7FB893");
            DrawText(ctx, RackOzet(r), new Point(RackX + 10, y + 24), 11, "#A8D8B8");

            double cy = y + RackBaseH;
            if (r.Cihazlar.Count == 0)
            {
                DrawText(ctx, "— cihaz tanımlı değil —", new Point(RackX + 16, cy), 10.5, "#5E8C6E", italic: true);
                cy += SatirH;
            }
            else
            {
                foreach (var c in r.Cihazlar)
                {
                    DrawText(ctx, $"• {c.Ad}  ×{c.Adet}", new Point(RackX + 16, cy), 10.5, "#9CCFAC");
                    cy += SatirH;
                }
            }

            var ucHatlari = RackUcHatlari(r);
            if (ucHatlari.Count > 0)
            {
                string renk = SistemTuru == "Kamera" ? "#75D6F0" : SistemTuru == "Data" ? "#7AC7FF" : "#69B7E8";
                var ucPen = new Pen(new SolidColorBrush(Color.Parse(renk)), 1.4);
                foreach (var uc in ucHatlari)
                {
                    double hatY = cy + 6;
                    ctx.DrawLine(ucPen, new Point(RackX + RackW, hatY), new Point(RackX + RackW + 28, hatY));
                    DrawText(ctx, $"{uc.Ad} — {uc.KabloTipi} {uc.Metraj:0.#} m",
                        new Point(RackX + RackW + 34, cy), 9.5, renk);
                    cy += SatirH;
                }
            }

            var anchor = new Point(RackX, y + h / 2);
            rackMerkezleri.Add(anchor);
            rackAnchor[r.Id] = anchor;
        }

        // ── Ana merkez kutusu (solda, dikey ortalı) ──
        double merkezH = MerkezBoyu(merkez);
        double merkezY = MerkezY(merkez, katlar, merkezH, katYukseklikleri, katUstleri);
        var merkezRect = new Rect(MerkezX, merkezY, MerkezW, merkezH);
        secimAlanlari.Add((merkezRect, null, null, true));
        DrawKutu(ctx, merkezRect, "#13361C", "#3CA85A");
        DrawText(ctx, merkez.Ad, new Point(MerkezX + 10, merkezY + 7), 13, "#DDF6E4", bold: true);
        double my = merkezY + RackBaseH;
        if (merkez.Cihazlar.Count == 0)
            DrawText(ctx, "- cihaz tanımlı değil -", new Point(MerkezX + 14, my), 10.5, "#5E8C6E", italic: true);
        else
            foreach (var c in merkez.Cihazlar)
            {
                DrawText(ctx, $"• {c.Ad}  ×{c.Adet}", new Point(MerkezX + 14, my), 10.5, "#AEE0BE");
                my += SatirH;
            }

        if (SistemTuru == "Tv" && TvSistemi is not null
            && TvSistemi.Teknoloji == TvDagitimTeknolojisi.MerkeziKoaksiyel)
            DrawTvCatiUydu(ctx, TvSistemi, merkezRect);

        // ── Bağlantı kabloları — her rack üst düğümüne bağlanır (Ana Merkez veya başka rack) ──
        var penMerkez = new Pen(new SolidColorBrush(Color.Parse("#4CAF50")), 1.8);
        var penRack = new Pen(new SolidColorBrush(Color.Parse("#F0A83C")), 2.6); // rack→rack: turuncu, kalın
        var rackNoktaBrush = new SolidColorBrush(Color.Parse("#F0A83C"));
        double busX = RackX - 34; // rack→rack bağlantıları için sol taraf bus hattı (kutudan uzak)
        var dogrudanRackIndexleri = rackler
            .Select((r, index) => (r, index))
            .Where(x => x.r.UstRackId is null || !rackAnchor.ContainsKey(x.r.UstRackId))
            .Select((x, sira) => (x.index, sira))
            .ToDictionary(x => x.index, x => x.sira);
        int dogrudanAdet = dogrudanRackIndexleri.Count;
        for (int i = 0; i < rackMerkezleri.Count; i++)
        {
            var rp = rackMerkezleri[i];
            var r = rackler[i];

            // Üst rack varsa ona, yoksa Ana Merkez'e bağla.
            if (r.UstRackId != null && rackAnchor.TryGetValue(r.UstRackId, out var pp))
            {
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(rp, false);
                    g.LineTo(new Point(busX, rp.Y));
                    g.LineTo(new Point(busX, pp.Y));
                    g.LineTo(pp);
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, penRack, geo);
                // Uç noktalara belirgin işaret (bağlantıyı görünür kıl).
                ctx.DrawEllipse(rackNoktaBrush, null, rp, 3.5, 3.5);
                ctx.DrawEllipse(rackNoktaBrush, null, pp, 3.5, 3.5);
                double lblY = (rp.Y + pp.Y) / 2;
                DrawText(ctx, $"→ {r.MerkezeKabloTipi}  {r.MerkezeKabloMetraj:0.#} m", new Point(busX - 2, lblY - 7), 9, "#F6CE86");
            }
            else
            {
                int sira = dogrudanRackIndexleri[i];
                double cikisY = merkezY + 12 + (sira + 1) * Math.Max(8, merkezH - 24) / (dogrudanAdet + 1);
                var merkezCikis = new Point(MerkezX + MerkezW, cikisY);
                // Her doğrudan hat için ayrı düşey koridor: kablolar üst üste binmez.
                double midX = MerkezX + MerkezW + 28
                              + (sira + 1) * (RackX - (MerkezX + MerkezW) - 56) / (dogrudanAdet + 1);
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(merkezCikis, false);
                    g.LineTo(new Point(midX, merkezCikis.Y));
                    g.LineTo(new Point(midX, rp.Y));
                    g.LineTo(rp);
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, penMerkez, geo);
                DrawText(ctx, $"{r.MerkezeKabloTipi}  {r.MerkezeKabloMetraj:0.#} m", new Point(midX + 4, rp.Y - 15), 9.5, "#B8E0C0");
            }
        }

        if (SistemTuru == "Kamera" && KameraSistemi is not null)
            DrawCctvIzlemeNoktalari(ctx, KameraSistemi, merkezRect, rackAnchor, katUstleri);
    }

    private void DrawDirektLnbSistemi(DrawingContext ctx, ElvTvSistemi tv)
    {
        var kaynak = new Rect(28, 55, 240, 105);
        DrawKutu(ctx, kaynak, "#30270B", "#D6B336");
        DrawText(ctx, tv.DirektLnb.CanakAdi, new Point(kaynak.X + 12, kaynak.Y + 10), 13, "#FFF0B5", bold: true);
        DrawText(ctx, $"{tv.DirektLnb.UyduPozisyonu} • {tv.DirektLnb.LnbTipi}", new Point(kaynak.X + 12, kaynak.Y + 34), 11, "#E9D578");
        DrawText(ctx, $"Çıkış: {tv.DirektLnb.GerekliCikis}/{tv.DirektLnb.LnbCikisSayisi * Math.Max(1, tv.DirektLnb.LnbSayisi)}",
            new Point(kaynak.X + 12, kaynak.Y + 58), 11, "#E9D578");
        secimAlanlari.Add((kaynak, null, null, true));

        var pen = new Pen(new SolidColorBrush(Color.Parse("#D8B842")), 1.7);
        double y = 48;
        for (int i = 0; i < tv.DirektLnb.Hatlar.Count; i++)
        {
            var hat = tv.DirektLnb.Hatlar[i];
            var rect = new Rect(560, y, 300, 40);
            DrawKutu(ctx, rect, "#26200D", "#B89B32");
            DrawText(ctx, hat.Ad, new Point(rect.X + 10, rect.Y + 6), 11, "#FFF1BF", bold: true);
            DrawText(ctx, $"LNB {hat.LnbCikisNo} • {hat.KabloTipi} • {hat.Metraj:0.#} m",
                new Point(rect.X + 10, rect.Y + 23), 9.5, "#D8C778");
            double cikisY = kaynak.Y + 12 + (i + 1) * (kaynak.Height - 24) / (tv.DirektLnb.Hatlar.Count + 1);
            var hedef = new Point(rect.Left, rect.Center.Y);
            double midX = 330 + i * 8;
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(new Point(kaynak.Right, cikisY), false);
                g.LineTo(new Point(midX, cikisY));
                g.LineTo(new Point(midX, hedef.Y));
                g.LineTo(hedef);
                g.EndFigure(false);
            }
            ctx.DrawGeometry(null, pen, geo);
            y += 54;
        }
    }

    private void DrawCctvIzlemeNoktalari(
        DrawingContext ctx,
        ElvKameraSistemi sistem,
        Rect merkezRect,
        IReadOnlyDictionary<string, Point> rackAnchor,
        IReadOnlyDictionary<int, double> katUstleri)
    {
        const double izlemeX = RackX + RackW + 150;
        const double izlemeW = 230;
        var pen = new Pen(new SolidColorBrush(Color.Parse("#4DB6E8")), 1.8);
        int toplam = Math.Max(1, sistem.IzlemeNoktalari.Count);
        var katKullanilanYukseklik = new Dictionary<int, double>();

        for (int i = 0; i < sistem.IzlemeNoktalari.Count; i++)
        {
            var nokta = sistem.IzlemeNoktalari[i];
            int katNo = katUstleri.ContainsKey(nokta.KatNo) ? nokta.KatNo : katUstleri.Keys.First();
            double kullanilan = katKullanilanYukseklik.TryGetValue(katNo, out var mevcut) ? mevcut : 0;
            double h = IzlemeNoktasiYuksekligi(nokta);
            double y = katUstleri[katNo] + 28 + kullanilan;
            katKullanilanYukseklik[katNo] = kullanilan + h + RackBosluk;
            var rect = new Rect(izlemeX, y, izlemeW, h);
            izlemeSecimAlanlari.Add((rect, nokta));
            DrawKutu(ctx, rect, "#102636", "#3A9BC8");
            DrawText(ctx, nokta.Ad, new Point(rect.X + 10, rect.Y + 7), 12.5, "#D5F2FF", bold: true);
            DrawText(ctx, nokta.KatAdi, new Point(rect.X + 10, rect.Y + 24), 10, "#75BEDD");
            double cy = rect.Y + RackBaseH;
            if (nokta.Cihazlar.Count == 0)
                DrawText(ctx, "— cihaz tanımlı değil —", new Point(rect.X + 14, cy), 10, "#6A9FB5", italic: true);
            else
                foreach (var cihaz in nokta.Cihazlar)
                {
                    DrawText(ctx, $"• {cihaz.Ad} ×{cihaz.Adet}", new Point(rect.X + 14, cy), 10, "#A8DCEC");
                    cy += SatirH;
                }

            Point kaynak;
            if (nokta.BagliRackId is not null && rackAnchor.TryGetValue(nokta.BagliRackId, out var rackSol))
                kaynak = new Point(RackX + RackW, rackSol.Y);
            else
            {
                double cikisY = merkezRect.Y + 10 + (i + 1) * Math.Max(8, merkezRect.Height - 20) / (toplam + 1);
                kaynak = new Point(merkezRect.Right, cikisY);
            }

            var hedef = new Point(rect.Left, rect.Center.Y);
            double midX = kaynak.X + 24 + (i + 1) * Math.Max(20, hedef.X - kaynak.X - 48) / (toplam + 1);
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(kaynak, false);
                g.LineTo(new Point(midX, kaynak.Y));
                g.LineTo(new Point(midX, hedef.Y));
                g.LineTo(hedef);
                g.EndFigure(false);
            }
            ctx.DrawGeometry(null, pen, geo);
            DrawText(ctx, $"{nokta.BaglantiKabloTipi}  {nokta.BaglantiKabloMetraj:0.#} m",
                new Point(midX + 4, hedef.Y - 15), 9, "#8ED8F5");
        }
    }

    private void DrawYanginSistemi(DrawingContext ctx)
    {
        var yangin = YanginSistemi;
        var katlar = Katlar.Count > 0 ? Katlar : AktifKatlar([]);
        var katYukseklikleri = YanginKatYukseklikleriniHesapla(katlar);
        var katUstleri = KatUstleriniHesapla(katlar, katYukseklikleri);
        DrawKatBantlari(ctx, katlar, Bounds.Width, katYukseklikleri, katUstleri);

        if (yangin is null)
        {
            DrawText(ctx, "Yangın algılama verisi yok", new Point(24, 24), 14, "#A06060");
            return;
        }

        double merkezH = MerkezBoyu(yangin.AnaMerkez);
        double merkezY = MerkezY(yangin.AnaMerkez, katlar, merkezH, katYukseklikleri, katUstleri);
        var merkezRect = new Rect(MerkezX, merkezY, MerkezW, merkezH);
        secimAlanlari.Add((merkezRect, null, null, true));
        DrawKutu(ctx, merkezRect, "#351414", "#C85050");
        DrawText(ctx, yangin.AnaMerkez.Ad, new Point(MerkezX + 10, merkezY + 7), 13, "#FFE0E0", bold: true);

        double my = merkezY + RackBaseH;
        if (yangin.AnaMerkez.Cihazlar.Count == 0)
            DrawText(ctx, "- cihaz tanımlı değil -", new Point(MerkezX + 14, my), 10.5, "#9E6060", italic: true);
        else
            foreach (var c in yangin.AnaMerkez.Cihazlar)
            {
                DrawText(ctx, $"• {c.Ad}  ×{c.Adet}", new Point(MerkezX + 14, my), 10.5, "#F0B0B0");
                my += SatirH;
            }

        if (yangin.KatLoopGruplari.Count == 0)
        {
            DrawText(ctx, "Loop yok - 'Loop Grubu Ekle' veya M12'den içe aktarın.", new Point(24, 24), 13, "#B87878");
            return;
        }

        var katSira = new Dictionary<int, int>();
        var pen = new Pen(new SolidColorBrush(Color.Parse("#E05050")), 1.8);
        int toplamLoop = Math.Max(1, yangin.ToplamLoop);
        int loopIndex = 0;

        foreach (var grup in yangin.KatLoopGruplari)
        {
            // Kat listesinde olmayan gruplar ilk kat bandına düşer (yükseklik hesabıyla aynı kural).
            int katNo = katUstleri.ContainsKey(grup.KatNo) ? grup.KatNo : katlar[0].KatNo;
            int sira = katSira.TryGetValue(katNo, out var mevcut) ? mevcut : 0;
            foreach (var loop in grup.Looplar)
            {
                double y = katUstleri[katNo] + 24 + sira * 50;
                var rect = new Rect(RackX, y, RackW, 38);
                secimAlanlari.Add((rect, null, grup, false));
                DrawKutu(ctx, rect, "#2A1010", "#C85050");
                DrawText(ctx, $"{grup.KatAdi} - {loop.Ad}", new Point(rect.X + 10, rect.Y + 6), 11.5, "#FFD8D8", bold: true);
                DrawText(ctx, $"Cihaz: {loop.CihazSayisi}  {loop.KabloTipi}  {loop.Metraj:0.#} m", new Point(rect.X + 10, rect.Y + 23), 9.5, "#E8A0A0");

                var hedef = new Point(rect.X, rect.Y + rect.Height / 2);
                // Her loop santralden ayrı Y noktasından çıkar ve ayrı bir düşey X koridoru
                // kullanır. Böylece paralel hatlar aynı çizgi üzerinde üst üste binmez.
                double cikisY = merkezY + 10 + (loopIndex + 1) * Math.Max(8, merkezH - 20) / (toplamLoop + 1);
                var merkezCikis = new Point(MerkezX + MerkezW, cikisY);
                double midX = MerkezX + MerkezW + 24
                              + (loopIndex + 1) * (hedef.X - (MerkezX + MerkezW) - 48) / (toplamLoop + 1);
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(merkezCikis, false);
                    g.LineTo(new Point(midX, merkezCikis.Y));
                    g.LineTo(new Point(midX, hedef.Y));
                    g.LineTo(hedef);
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, pen, geo);
                sira++;
                loopIndex++;
            }
            katSira[katNo] = sira;
        }
    }

    /// <summary>Yangın şemasında kat bandı yükseklikleri: kattaki loop sayısına göre büyür.</summary>
    private Dictionary<int, double> YanginKatYukseklikleriniHesapla(IReadOnlyList<ELVKatInfo> katlar)
    {
        var loopSayilari = new Dictionary<int, int>();
        if (YanginSistemi is not null && katlar.Count > 0)
        {
            var katNolar = katlar.Select(k => k.KatNo).ToHashSet();
            foreach (var grup in YanginSistemi.KatLoopGruplari)
            {
                // Kat listesinde olmayan gruplar çizimde ilk kata düştüğünden yükseklik de oraya eklenir.
                int katNo = katNolar.Contains(grup.KatNo) ? grup.KatNo : katlar[0].KatNo;
                loopSayilari[katNo] = loopSayilari.TryGetValue(katNo, out var mevcut)
                    ? mevcut + grup.Looplar.Count
                    : grup.Looplar.Count;
            }
        }

        return katlar.ToDictionary(k => k.KatNo, k =>
        {
            int loops = loopSayilari.TryGetValue(k.KatNo, out var adet) ? adet : 0;
            return loops == 0 ? FloorHeight : Math.Max(FloorHeight, 24 + loops * 50 + 14);
        });
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var nokta = e.GetPosition(this);
        for (int i = izlemeSecimAlanlari.Count - 1; i >= 0; i--)
        {
            if (!izlemeSecimAlanlari[i].Alan.Contains(nokta)) continue;
            SecimIstendi?.Invoke(this, new SemaSecimEventArgs(null, null, false, null, izlemeSecimAlanlari[i].Nokta));
            e.Handled = true;
            return;
        }
        for (int i = analogSecimAlanlari.Count - 1; i >= 0; i--)
        {
            if (!analogSecimAlanlari[i].Alan.Contains(nokta)) continue;
            SecimIstendi?.Invoke(this, new SemaSecimEventArgs(null, null, false, analogSecimAlanlari[i].Zon));
            e.Handled = true;
            return;
        }
        for (int i = secimAlanlari.Count - 1; i >= 0; i--)
        {
            var secim = secimAlanlari[i];
            if (!secim.Alan.Contains(nokta)) continue;

            SecimIstendi?.Invoke(this, new SemaSecimEventArgs(secim.Rack, secim.YanginGrubu, secim.AnaMerkez, null));
            e.Handled = true;
            return;
        }
    }

    private void DrawAnalogAnonsSistemi(DrawingContext ctx, ElvAnonsSistemi sistem)
    {
        var merkezRect = new Rect(24, 36, 190, Math.Max(70, 48 + sistem.AnaMerkez.Cihazlar.Count * 16));
        DrawKutu(ctx, merkezRect, "#3A2410", "#D28A32");
        DrawText(ctx, "ANALOG ACİL ANONS", new Point(merkezRect.X + 10, merkezRect.Y + 8), 13, "#FFE2B8", bold: true);
        DrawText(ctx, "EN 54-16 Ana Merkez", new Point(merkezRect.X + 10, merkezRect.Y + 27), 10.5, "#D8B078");
        secimAlanlari.Add((merkezRect, null, null, true));

        double ampY = 36;
        foreach (var amp in sistem.AnalogAmplifikatorler)
        {
            var rect = new Rect(255, ampY, 190, 52);
            DrawKutu(ctx, rect, amp.Yedek ? "#30233A" : "#332810", amp.Yedek ? "#B58CD8" : "#D2A33A");
            DrawText(ctx, amp.Ad, new Point(rect.X + 8, rect.Y + 7), 10.5, "#FFF0C8", bold: true);
            DrawText(ctx, $"{amp.NominalGucW:0.#} W • {amp.KanalSayisi} kanal{(amp.Yedek ? " • YEDEK" : "")}",
                new Point(rect.X + 8, rect.Y + 27), 9.5, "#D8BE88");
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#B98232")), 1.5),
                new Point(merkezRect.Right, merkezRect.Center.Y), new Point(rect.Left, rect.Center.Y));
            ampY += 62;
        }

        double y = 36;
        foreach (var zon in sistem.AnalogZonlar.OrderByDescending(z => z.KatNo))
        {
            int grupSayisi = zon.HatA.HoparlorGruplari.Count + (zon.HatB?.HoparlorGruplari.Count ?? 0);
            double h = Math.Max(110, 66 + grupSayisi * 18);
            var zonRect = new Rect(490, y, 210, h);
            analogSecimAlanlari.Add((zonRect, zon));
            DrawKutu(ctx, zonRect, "#2D2010", "#E2A33A");
            DrawText(ctx, zon.Ad, new Point(zonRect.X + 10, zonRect.Y + 7), 12, "#FFE0A8", bold: true);
            DrawText(ctx, $"{zon.KatAdi} • {zon.ToplamHoparlor} hop. • {zon.ToplamGucW:0.#} W",
                new Point(zonRect.X + 10, zonRect.Y + 25), 9.5, "#D8B878");

            DrawAnalogHat(ctx, zon, zon.HatA, zonRect, 740, zonRect.Y + 48, "#63C878");
            if (zon.HatB is not null)
                DrawAnalogHat(ctx, zon, zon.HatB, zonRect, 740,
                    zonRect.Y + 48 + Math.Max(1, zon.HatA.HoparlorGruplari.Count) * 18 + 8, "#69AEE8");
            y += h + 20;
        }
    }

    private static void DrawAnalogHat(DrawingContext ctx, ElvAnonsZonu zon, ElvAnalogHoparlorHatti hat,
        Rect zonRect, double x, double y, string renk)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse(renk)), 1.6);
        ctx.DrawLine(pen, new Point(zonRect.Right, y + 8), new Point(x, y + 8));
        DrawText(ctx, $"Hat {hat.Kol} • 100 V • {hat.ToplamGucW:0.#} W • {hat.KabloTipi} {hat.Metraj:0.#} m",
            new Point(x + 8, y), 10, renk, bold: true);
        double gy = y + 18;
        foreach (var grup in hat.HoparlorGruplari)
        {
            DrawText(ctx, $"{grup.Adet}x {grup.HoparlorTipi} @ {grup.TapGucuW:0.##} W",
                new Point(x + 22, gy), 9.5, "#D8E8D8");
            gy += 18;
        }
    }

    private static void DrawTvCatiUydu(DrawingContext ctx, ElvTvSistemi tv, Rect merkezRect)
    {
        var catiRect = new Rect(RackX, 14, RackW, 42);
        DrawKutu(ctx, catiRect, "#1B2C24", "#C9A646");
        DrawText(ctx, $"Çatı Uydu: {Math.Max(1, tv.CatiUyduSayisi)}", new Point(catiRect.X + 10, catiRect.Y + 7), 12, "#FFF4C0", bold: true);
        DrawText(ctx, $"RG-11/U  {tv.ToplamCatiHatMetraj:0.#} m", new Point(catiRect.X + 10, catiRect.Y + 25), 10.5, "#E8D58A");

        var baslangic = new Point(merkezRect.X + merkezRect.Width / 2, merkezRect.Y);
        var bitis = new Point(catiRect.X + catiRect.Width / 2, catiRect.Bottom);
        var pen = new Pen(new SolidColorBrush(Color.Parse("#D7B84A")), 1.8);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(baslangic, false);
            g.LineTo(new Point(baslangic.X, bitis.Y - 18));
            g.LineTo(new Point(bitis.X, bitis.Y - 18));
            g.LineTo(bitis);
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, geo);
    }

    private IReadOnlyList<ElvRackKabin> AktifRackler()
        => SistemTuru switch
        {
            "Kamera" => KameraSistemi?.RackKabinler ?? [],
            "Anons" => AnonsSistemi?.RackKabinler ?? [],
            "Tv" => TvSistemi?.Teknoloji == TvDagitimTeknolojisi.IpTv
                ? TvSistemi.IpTv.RackKabinler : TvSistemi?.RackKabinler ?? [],
            "Telefon" => TelefonSistemi?.RackKabinler ?? [],
            "Diafon" => DiafonSistemi?.RackKabinler ?? [],
            _ => DataSistemi?.RackKabinler ?? []
        };

    private ElvMerkez? AktifMerkez()
        => SistemTuru switch
        {
            "Kamera" => KameraSistemi?.AnaMerkez,
            "Anons" => AnonsSistemi?.AnaMerkez,
            "Tv" => TvSistemi?.Teknoloji == TvDagitimTeknolojisi.IpTv
                ? TvSistemi.IpTv.AnaMerkez : TvSistemi?.AnaMerkez,
            "Telefon" => TelefonSistemi?.AnaMerkez,
            "Diafon" => DiafonSistemi?.AnaMerkez,
            _ => DataSistemi?.AnaMerkez
        };

    private string RackOzet(ElvRackKabin r)
        => SistemTuru switch
        {
            "Kamera" => $"Dahili: {r.DahiliKameraSayisi}   Harici: {r.HariciKameraSayisi}   PTZ: {r.PtzKameraSayisi}",
            "Anons" => AnonsSistemi?.Teknoloji == "Dijital (PoE)"
                ? $"Dijital (PoE)   Ayrı uç: {r.ToplamDijitalAnonsUcu}"
                : $"Analog   Hat: {r.AnonsHatSayisi}   Hoparlör: {r.ToplamAnonsHoparlor}",
            "Tv" => TvSistemi?.Teknoloji == TvDagitimTeknolojisi.IpTv
                ? $"IP-TV uç: {r.TvPrizSayisi}" : $"TV priz: {r.TvPrizSayisi}",
            "Telefon" => $"Telefon priz: {r.TelefonPrizSayisi}",
            "Diafon" => $"Diafon ünite: {r.DiafonUniteSayisi}",
            _ => DataRackOzet(r)
        };

    // Data rack özeti: temel hatlar + varsa ek uç birimler (IP telefon, yazıcı, erişim, diafon).
    private static string DataRackOzet(ElvRackKabin r)
    {
        var sb = new System.Text.StringBuilder($"Data: {r.DataHatSayisi}   WiFi: {r.WirelessSayisi}");
        if (r.IpTelefonSayisi > 0) sb.Append($"   Tel: {r.IpTelefonSayisi}");
        if (r.YaziciSayisi > 0) sb.Append($"   Yzc: {r.YaziciSayisi}");
        if (r.ErisimKontrolSayisi > 0) sb.Append($"   Erş: {r.ErisimKontrolSayisi}");
        if (r.DiafonSayisi > 0) sb.Append($"   Dfn: {r.DiafonSayisi}");
        return sb.ToString();
    }

    private static double MerkezY(ElvMerkez merkez, IReadOnlyList<ELVKatInfo> katlar, double merkezH,
        IReadOnlyDictionary<int, double> katYukseklikleri, IReadOnlyDictionary<int, double> katUstleri)
    {
        int hedefKat = merkez.KatNo ?? 0;
        if (!katUstleri.ContainsKey(hedefKat))
            hedefKat = katUstleri.ContainsKey(0) ? 0 : katlar[0].KatNo;
        double katH = katYukseklikleri[hedefKat];
        return katUstleri[hedefKat] + Math.Max(10, (katH - merkezH) / 2);
    }

    private Dictionary<int, double> KatYukseklikleriniHesapla(
        IReadOnlyList<ELVKatInfo> katlar, IReadOnlyList<ElvRackKabin> rackler)
    {
        var gruplar = rackler.GroupBy(r => r.KatNo).ToDictionary(g => g.Key, g => g.ToList());
        var izlemeGruplari = new Dictionary<int, List<ElvCctvIzlemeNoktasi>>();
        if (SistemTuru == "Kamera" && KameraSistemi is not null && katlar.Count > 0)
        {
            var katNolar = katlar.Select(k => k.KatNo).ToHashSet();
            foreach (var nokta in KameraSistemi.IzlemeNoktalari)
            {
                int katNo = katNolar.Contains(nokta.KatNo) ? nokta.KatNo : katlar[0].KatNo;
                if (!izlemeGruplari.TryGetValue(katNo, out var liste))
                    izlemeGruplari[katNo] = liste = [];
                liste.Add(nokta);
            }
        }
        return katlar.ToDictionary(k => k.KatNo, k =>
        {
            double rackIcerik = gruplar.TryGetValue(k.KatNo, out var grup) && grup.Count > 0
                ? grup.Sum(RackYuksekligi) + Math.Max(0, grup.Count - 1) * RackBosluk
                : 0;
            double izlemeIcerik = izlemeGruplari.TryGetValue(k.KatNo, out var noktalar) && noktalar.Count > 0
                ? noktalar.Sum(IzlemeNoktasiYuksekligi) + Math.Max(0, noktalar.Count - 1) * RackBosluk
                : 0;
            return Math.Max(FloorHeight, Math.Max(rackIcerik, izlemeIcerik) + 56);
        });
    }

    private static Dictionary<int, double> KatUstleriniHesapla(
        IReadOnlyList<ELVKatInfo> katlar, IReadOnlyDictionary<int, double> katYukseklikleri)
    {
        var sonuc = new Dictionary<int, double>();
        double y = FloorTopMargin;
        foreach (var kat in katlar)
        {
            sonuc[kat.KatNo] = y;
            y += katYukseklikleri[kat.KatNo];
        }
        return sonuc;
    }

    private List<ELVKatInfo> AktifKatlar(IReadOnlyList<ElvRackKabin> rackler)
    {
        if (Katlar.Count > 0)
            return Katlar;

        var katlar = rackler
            .Select(r => new ELVKatInfo { KatNo = r.KatNo, Ad = string.IsNullOrWhiteSpace(r.KatAdi) ? $"Kat {r.KatNo}" : r.KatAdi })
            .GroupBy(k => k.KatNo)
            .Select(g => g.First())
            .OrderByDescending(k => k.KatNo)
            .ToList();

        if (katlar.Count == 0)
            katlar.Add(new ELVKatInfo { KatNo = 0, Ad = "Zemin Kat" });

        return katlar;
    }

    private static void DrawKatBantlari(DrawingContext ctx, IReadOnlyList<ELVKatInfo> katlar, double width,
        IReadOnlyDictionary<int, double> katYukseklikleri, IReadOnlyDictionary<int, double> katUstleri)
    {
        var dashPen = new Pen(new SolidColorBrush(Color.Parse("#385E40")), 1, new DashStyle(new double[] { 4, 3 }, 0));
        for (int i = 0; i < katlar.Count; i++)
        {
            var kat = katlar[i];
            double yStart = katUstleri[kat.KatNo];
            double h = katYukseklikleri[kat.KatNo];
            double yEnd = yStart + h;
            if (i % 2 == 1)
                ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0D2213")), new Rect(0, yStart, width, h));
            ctx.DrawLine(dashPen, new Point(0, yEnd), new Point(width, yEnd));

            var badgeRect = new Rect(10, yStart + (h - 26) / 2, 76, 26);
            ctx.FillRectangle(new SolidColorBrush(Color.Parse("#1F3D24")), badgeRect);
            ctx.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#4E8555")), 1.5), badgeRect);
            DrawText(ctx, kat.Ad, new Point(badgeRect.X + 8, badgeRect.Y + 6), 11, "#FFFFFF", bold: true);
        }
    }

    private static void DrawKutu(DrawingContext ctx, Rect rect, string fill, string border)
    {
        ctx.DrawRectangle(
            new SolidColorBrush(Color.Parse(fill)),
            new Pen(new SolidColorBrush(Color.Parse(border)), 1.6),
            rect, 6, 6);
    }

    private static void DrawText(DrawingContext ctx, string text, Point at, double size, string color,
        bool bold = false, bool italic = false)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default,
                italic ? FontStyle.Italic : FontStyle.Normal,
                bold ? FontWeight.SemiBold : FontWeight.Normal),
            size,
            new SolidColorBrush(Color.Parse(color)));
        ctx.DrawText(ft, at);
    }
}

public sealed class SemaSecimEventArgs(
    ElvRackKabin? rack,
    ElvYanginKatLoopGrubu? yanginGrubu,
    bool anaMerkez,
    ElvAnonsZonu? analogZon,
    ElvCctvIzlemeNoktasi? izlemeNoktasi = null) : EventArgs
{
    public ElvRackKabin? Rack { get; } = rack;
    public ElvYanginKatLoopGrubu? YanginGrubu { get; } = yanginGrubu;
    public bool AnaMerkez { get; } = anaMerkez;
    public ElvAnonsZonu? AnalogZon { get; } = analogZon;
    public ElvCctvIzlemeNoktasi? IzlemeNoktasi { get; } = izlemeNoktasi;
}
