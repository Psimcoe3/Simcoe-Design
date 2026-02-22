using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ElectricalComponentSandbox.Converters;

namespace ElectricalComponentSandbox.Tests.Converters;

public class VisibilityConverterTests
{
    [Fact]
    public void NullToVisibilityConverter_ConvertBack_ReturnsDoNothing()
    {
        var converter = new NullToVisibilityConverter();

        var result = converter.ConvertBack(Visibility.Visible, typeof(object), string.Empty, CultureInfo.InvariantCulture);

        Assert.Same(Binding.DoNothing, result);
    }

    [Fact]
    public void NotNullToVisibilityConverter_ConvertBack_ReturnsDoNothing()
    {
        var converter = new NotNullToVisibilityConverter();

        var result = converter.ConvertBack(Visibility.Visible, typeof(object), string.Empty, CultureInfo.InvariantCulture);

        Assert.Same(Binding.DoNothing, result);
    }
}
