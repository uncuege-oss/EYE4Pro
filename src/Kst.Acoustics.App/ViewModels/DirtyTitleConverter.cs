using System.Globalization;
using Avalonia.Data.Converters;

namespace Kst.Acoustics.App.ViewModels;

public sealed class DirtyTitleConverter : IValueConverter
{
    private const string BaseTitle = "Acoustics Platform - İçe Aktarma Önizleme";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var baseTitle = parameter as string;
        if (string.IsNullOrWhiteSpace(baseTitle)) baseTitle = BaseTitle;
        if (value is true)
        {
            return $"{baseTitle} *";
        }

        return baseTitle;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
