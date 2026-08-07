using System.Globalization;
using System.Text;

namespace Kst.Acoustics.Core.PanelDesign;

public static class PanelTemplateDxfBuilder
{
    public static string Build600x600MillimeterTemplate()
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        AppendClosedPolyline(sb, [(0, 0), (600, 0), (600, 600), (0, 600)]);

        for (var y = 100; y <= 500; y += 100)
        {
            for (var x = 100; x <= 500; x += 100)
                AppendCircle(sb, x, y, 12);
        }

        AppendFooter(sb);
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("HEADER");
        sb.AppendLine("9");
        sb.AppendLine("$ACADVER");
        sb.AppendLine("1");
        sb.AppendLine("AC1009");
        sb.AppendLine("9");
        sb.AppendLine("$INSUNITS");
        sb.AppendLine("70");
        sb.AppendLine("4");
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("TABLES");
        sb.AppendLine("0");
        sb.AppendLine("TABLE");
        sb.AppendLine("2");
        sb.AppendLine("LAYER");
        sb.AppendLine("70");
        sb.AppendLine("2");
        AppendLayer(sb, "PANEL", 7);
        AppendLayer(sb, "DELIK", 3);
        sb.AppendLine("0");
        sb.AppendLine("ENDTAB");
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("ENTITIES");
    }

    private static void AppendLayer(StringBuilder sb, string name, int color)
    {
        sb.AppendLine("0");
        sb.AppendLine("LAYER");
        sb.AppendLine("2");
        sb.AppendLine(name);
        sb.AppendLine("70");
        sb.AppendLine("0");
        sb.AppendLine("62");
        sb.AppendLine(color.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("6");
        sb.AppendLine("CONTINUOUS");
    }

    private static void AppendFooter(StringBuilder sb)
    {
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("0");
        sb.AppendLine("EOF");
    }

    private static void AppendClosedPolyline(StringBuilder sb, IReadOnlyList<(double X, double Y)> points)
    {
        sb.AppendLine("0");
        sb.AppendLine("POLYLINE");
        sb.AppendLine("8");
        sb.AppendLine("PANEL");
        sb.AppendLine("66");
        sb.AppendLine("1");
        sb.AppendLine("70");
        sb.AppendLine("1");

        foreach (var p in points)
        {
            sb.AppendLine("0");
            sb.AppendLine("VERTEX");
            sb.AppendLine("8");
            sb.AppendLine("PANEL");
            sb.AppendLine("10");
            sb.AppendLine(p.X.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("20");
            sb.AppendLine(p.Y.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("30");
            sb.AppendLine("0");
        }

        sb.AppendLine("0");
        sb.AppendLine("SEQEND");
    }

    private static void AppendCircle(StringBuilder sb, double x, double y, double r)
    {
        sb.AppendLine("0");
        sb.AppendLine("CIRCLE");
        sb.AppendLine("8");
        sb.AppendLine("DELIK");
        sb.AppendLine("10");
        sb.AppendLine(x.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("20");
        sb.AppendLine(y.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("30");
        sb.AppendLine("0");
        sb.AppendLine("40");
        sb.AppendLine(r.ToString(CultureInfo.InvariantCulture));
    }
}
