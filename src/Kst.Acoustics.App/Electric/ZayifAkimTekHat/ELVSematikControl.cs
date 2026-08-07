using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;
using System.Globalization;

namespace Kst.Acoustics.App.Electric.ZayifAkimTekHat;

public class ELVSematikControl : Control
{
    public static readonly StyledProperty<List<ELVBlok>> BlokProperty =
        AvaloniaProperty.Register<ELVSematikControl, List<ELVBlok>>(nameof(Bloklar), new List<ELVBlok>());

    public static readonly StyledProperty<string> KabloRengiProperty =
        AvaloniaProperty.Register<ELVSematikControl, string>(nameof(KabloRengi), "#4ED0E0");

    public static readonly StyledProperty<string> SistemAdiProperty =
        AvaloniaProperty.Register<ELVSematikControl, string>(nameof(SistemAdi), "Sistem");

    public static readonly StyledProperty<List<ELVKatInfo>> KatlarProperty =
        AvaloniaProperty.Register<ELVSematikControl, List<ELVKatInfo>>(nameof(Katlar), new List<ELVKatInfo>());

    static ELVSematikControl()
    {
        AffectsRender<ELVSematikControl>(BlokProperty, KabloRengiProperty, SistemAdiProperty, KatlarProperty);
    }

    public List<ELVBlok> Bloklar
    {
        get => GetValue(BlokProperty);
        set => SetValue(BlokProperty, value);
    }

    public string KabloRengi
    {
        get => GetValue(KabloRengiProperty);
        set => SetValue(KabloRengiProperty, value);
    }

    public string SistemAdi
    {
        get => GetValue(SistemAdiProperty);
        set => SetValue(SistemAdiProperty, value);
    }

    public List<ELVKatInfo> Katlar
    {
        get => GetValue(KatlarProperty);
        set => SetValue(KatlarProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double maxW = 800; // minimum width
        double maxH = 600; // minimum height

        var katList = Katlar;
        if (katList == null || katList.Count == 0)
        {
            var inferredKats = Bloklar
                .Where(b => b.Kat.HasValue)
                .Select(b => b.Kat ?? 0)
                .Distinct()
                .ToList();
            int katCount = inferredKats.Count > 0 ? inferredKats.Count : 1;
            maxH = 50 + katCount * 120 + 20;
        }
        else
        {
            maxH = 50 + katList.Count * 120 + 20;
        }

        if (Bloklar.Count > 0)
        {
            var (blokW, _) = ZayifAkimTekHatLayoutServisi.BlokBoyutu();
            double maxX = Bloklar.Max(b => b.LayoutX) + blokW + 40;
            if (maxX > maxW)
            {
                maxW = maxX;
            }
        }

        return new Size(maxW, maxH);
    }

    public override void Render(DrawingContext ctx)
    {
        // Arka plan
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0C1610")), new Rect(Bounds.Size));

        // Katlar listesi
        var katlarList = Katlar;
        if (katlarList == null || katlarList.Count == 0)
        {
            var inferredKats = Bloklar
                .Where(b => b.Kat.HasValue)
                .Select(b => b.Kat ?? 0)
                .Distinct()
                .OrderByDescending(k => k)
                .ToList();
            if (inferredKats.Count == 0)
            {
                inferredKats.Add(0);
            }
            katlarList = inferredKats.Select(k => new ELVKatInfo { KatNo = k, Ad = $"Kat {k}" }).ToList();
        }

        // Kat şeritleri ve çizgilerini çiz
        double floorTopMargin = 50;
        double floorHeight = 120;
        var dashPen = new Pen(new SolidColorBrush(Color.Parse("#385E40")), 1, new DashStyle(new double[] { 4, 3 }, 0));

        for (int floorIdx = 0; floorIdx < katlarList.Count; floorIdx++)
        {
            var katInfo = katlarList[floorIdx];
            double yStart = floorTopMargin + floorIdx * floorHeight;
            double yEnd = yStart + floorHeight;

            // Alternatif arka plan
            if (floorIdx % 2 == 1)
            {
                ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0D2213")), new Rect(0, yStart, Bounds.Width, floorHeight));
            }

            // Kesikli çizgi
            ctx.DrawLine(dashPen, new Point(0, yEnd), new Point(Bounds.Width, yEnd));

            // Kat etiketi
            double badgeW = 60;
            double badgeH = 26;
            double badgeX = 10;
            double badgeY = yStart + (floorHeight - badgeH) / 2;
            var badgeRect = new Rect(badgeX, badgeY, badgeW, badgeH);
            ctx.FillRectangle(new SolidColorBrush(Color.Parse("#1F3D24")), badgeRect);
            ctx.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#4E8555")), 1.5), badgeRect);

            var tfBadge = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
            var badgeText = new FormattedText(
                katInfo.Ad,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                tfBadge,
                11,
                new SolidColorBrush(Color.Parse("#FFFFFF")));
            var badgeTextPoint = new Point(badgeX + (badgeW - badgeText.Width) / 2, badgeY + (badgeH - badgeText.Height) / 2);
            ctx.DrawText(badgeText, badgeTextPoint);
        }

        if (Bloklar.Count == 0)
        {
            // Boş durum
            var tf = new Typeface("Segoe UI");
            var text = new FormattedText(
                "Sistem verisi yok",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                tf,
                14,
                new SolidColorBrush(Color.Parse("#A2C498")));
            ctx.DrawText(text, new Point(Bounds.Width / 2 - 60, Bounds.Height / 2 - 20));
            return;
        }

        // Sistem başlığı
        DrawSistemBasligi(ctx);

        // Bağlantıları çiz (önce arka planda)
        DrawBaglantilar(ctx);

        // Blokları çiz
        foreach (var blok in Bloklar)
        {
            DrawBlok(ctx, blok);
        }
    }

    private void DrawSistemBasligi(DrawingContext ctx)
    {
        var kabloColor = Color.Parse(KabloRengi);
        var barHeight = 36;

        // Dark background for header
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#080F0A")), new Rect(0, 0, Bounds.Width, barHeight));

        // Draw a horizontal bottom border line using the cable color
        ctx.DrawLine(new Pen(new SolidColorBrush(kabloColor), 2), new Point(0, barHeight - 1), new Point(Bounds.Width, barHeight - 1));

        // Draw a left indicator block using the cable color
        var indicatorRect = new Rect(12, 8, 6, 20);
        ctx.FillRectangle(new SolidColorBrush(kabloColor), indicatorRect);

        // System Name Text (clean white/light text, high contrast and extremely readable)
        var tf = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var text = new FormattedText(
            SistemAdi,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            tf,
            13,
            new SolidColorBrush(Color.Parse("#E0F0D0")));
        
        // Draw the text next to the indicator block
        ctx.DrawText(text, new Point(26, 8 + (20 - text.Height) / 2));
    }

    private void DrawBaglantilar(DrawingContext ctx)
    {
        var kabloColor = Color.Parse(KabloRengi);
        var pen = new Pen(new SolidColorBrush(kabloColor), 2);
        var (blokW, blokH) = ZayifAkimTekHatLayoutServisi.BlokBoyutu();

        // Ebeveyn-çocuk bağlantılarını çiz
        foreach (var ebeveyn in Bloklar.Where(b => b.BagliCocukBlokIds.Count > 0))
        {
            foreach (var cocukId in ebeveyn.BagliCocukBlokIds)
            {
                var cocuk = Bloklar.FirstOrDefault(b => b.Id == cocukId);
                if (cocuk == null)
                    continue;

                // L-şekilli çizgi
                var x1 = ebeveyn.LayoutX + blokW / 2;
                var y1 = ebeveyn.LayoutY + blokH;
                var x2 = cocuk.LayoutX + blokW / 2;
                var y2 = cocuk.LayoutY;

                var noktalar = ZayifAkimTekHatLayoutServisi.BaglantiBicimi(x1, y1, x2, y2);
                for (int i = 0; i < noktalar.Count - 1; i++)
                {
                    var p1 = new Point(noktalar[i].X, noktalar[i].Y);
                    var p2 = new Point(noktalar[i + 1].X, noktalar[i + 1].Y);
                    ctx.DrawLine(pen, p1, p2);
                }
            }
        }
    }

    private void DrawBlok(DrawingContext ctx, ELVBlok blok)
    {
        var (blokW, blokH) = ZayifAkimTekHatLayoutServisi.BlokBoyutu();
        var x = blok.LayoutX;
        var y = blok.LayoutY;

        // Blok arka planı rol'e göre renk
        var bgColor = blok.Rol switch
        {
            ELVBlokRol.Merkez => Color.Parse("#1A4D15"),
            ELVBlokRol.AraToplayici => Color.Parse("#236E2B"),
            ELVBlokRol.Cihaz => Color.Parse("#134F1B"),
            _ => Color.Parse("#102B16")
        };

        var rect = new Rect(x, y, blokW, blokH);
        ctx.FillRectangle(new SolidColorBrush(bgColor), rect);
        ctx.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#60B349")), 1.5), rect);

        // Metin belirle
        string displayLabel = blok.Rol switch
        {
            ELVBlokRol.Merkez => $"M:{blok.ElemanAdi}",
            ELVBlokRol.AraToplayici => $"A:{blok.ElemanAdi}",
            ELVBlokRol.Cihaz => $"{blok.Miktar}x {blok.ElemanAdi}",
            _ => blok.ElemanAdi
        };

        var tf = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var text = new FormattedText(
            displayLabel,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            tf,
            11,
            new SolidColorBrush(Color.Parse("#FFFFFF")));

        var textPoint = new Point(x + 8, y + (blokH - text.Height) / 2);
        ctx.DrawText(text, textPoint);
    }
}
