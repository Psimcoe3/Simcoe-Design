using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ElectricalComponentSandbox.Converters;

public sealed class ThreadDepthToMarginConverter : IValueConverter
{
    public double IndentStep { get; set; } = 18.0;

    public double BaseLeft { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var depth = value switch
        {
            int intDepth => intDepth,
            long longDepth => (int)longDepth,
            short shortDepth => shortDepth,
            byte byteDepth => byteDepth,
            _ => 0
        };

        return new Thickness(BaseLeft + Math.Max(0, depth) * IndentStep, 0, 0, 6);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}