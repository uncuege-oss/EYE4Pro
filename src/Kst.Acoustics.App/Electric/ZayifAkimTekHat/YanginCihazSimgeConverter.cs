using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

namespace Kst.Acoustics.App.Electric.ZayifAkimTekHat;

/// <summary>Yangın loop zincirinde her cihaz türünü ayrı bir şematik simgeyle gösterir.</summary>
public sealed class YanginCihazSimgeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ElvYanginCihazTipi.DumanDedektoru => "◉",
        ElvYanginCihazTipi.IsiDedektoru => "●",
        ElvYanginCihazTipi.KombineDedektor => "◎",
        ElvYanginCihazTipi.AsmaTavanDedektoru => "⊙",
        ElvYanginCihazTipi.YanginButonu => "▣",
        ElvYanginCihazTipi.Siren => "◖))",
        ElvYanginCihazTipi.Flasor => "✦",
        _ => "?"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
