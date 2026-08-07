using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Kst.Acoustics.App.Converters;

public class BoolToGridLengthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked)
        {
            if (isChecked)
            {
                if (parameter is string paramStr)
                {
                    paramStr = paramStr.Trim();
                    if (paramStr.EndsWith('*'))
                    {
                        var valStr = paramStr.Substring(0, paramStr.Length - 1);
                        if (string.IsNullOrEmpty(valStr))
                        {
                            return new GridLength(1, GridUnitType.Star);
                        }
                        if (double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double starVal))
                        {
                            return new GridLength(starVal, GridUnitType.Star);
                        }
                    }
                    else if (double.TryParse(paramStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double pixelVal))
                    {
                        return new GridLength(pixelVal, GridUnitType.Pixel);
                    }
                }
                return new GridLength(1, GridUnitType.Star);
            }
            else
            {
                return GridLength.Auto;
            }
        }
        return GridLength.Auto;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
