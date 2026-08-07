using System.Globalization;
using Avalonia.Data.Converters;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.Converters;

public sealed class DirectivityPatternTurkishConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DirectivityPattern.Omnidirectional => "Tüm yönlü",
        DirectivityPattern.Cardioid => "Kardiyoid",
        DirectivityPattern.SuperCardioid => "Süper kardiyoid",
        DirectivityPattern.Figure8 => "Sekiz biçimli",
        _ => "Tüm yönlü"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
