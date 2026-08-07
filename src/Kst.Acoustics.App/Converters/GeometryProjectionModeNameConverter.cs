using System.Globalization;
using Avalonia.Data.Converters;
using Kst.Acoustics.App.Controls;

namespace Kst.Acoustics.App.Converters;

/// <summary>
/// Geometri projeksiyon enum değerlerini kullanıcıya gösterilen Türkçe adlara çevirir.
/// Enum değerleri model ve çizim katmanında sabit kalırken arayüz teknik adları sızdırmaz.
/// </summary>
public sealed class GeometryProjectionModeNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        GeometryProjectionMode.Top2D => "Üst 2B",
        GeometryProjectionMode.Front2D => "Ön 2B",
        GeometryProjectionMode.Isometric3D => "İzometrik 3B",
        _ => "Bilinmeyen görünüm"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Projeksiyon adı yalnız görüntüleme amacıyla kullanılır.");
}
