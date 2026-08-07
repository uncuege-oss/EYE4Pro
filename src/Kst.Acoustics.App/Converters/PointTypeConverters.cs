using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Kst.Acoustics.Core.Measurement.Data;

namespace Kst.Acoustics.App.Converters;

public sealed class PointTypeMicrophoneConverter : IValueConverter
{
    public static readonly PointTypeMicrophoneConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PointType t && t == PointType.Microphone;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class PointTypeLoudspeakerConverter : IValueConverter
{
    public static readonly PointTypeLoudspeakerConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PointType t && t == PointType.Loudspeaker;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
