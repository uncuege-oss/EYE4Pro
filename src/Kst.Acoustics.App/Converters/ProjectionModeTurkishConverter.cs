using System.Globalization;
using Avalonia.Data.Converters;
using Kst.Acoustics.App.Controls;

namespace Kst.Acoustics.App.Converters;

public sealed class ProjectionModeTurkishConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        GeometryProjectionMode.Top2D => "Üst 2B",
        GeometryProjectionMode.Front2D => "Ön 2B",
        GeometryProjectionMode.Isometric3D => "İzometrik 3B",
        _ => "Görünüm"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
