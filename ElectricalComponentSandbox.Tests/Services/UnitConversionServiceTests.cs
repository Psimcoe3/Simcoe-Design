using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class UnitConversionServiceTests
{
    [Fact]
    public void InchesToFeet_ConvertsCorrectly()
    {
        Assert.Equal(1.0, UnitConversionService.InchesToFeet(12.0));
        Assert.Equal(0.5, UnitConversionService.InchesToFeet(6.0));
        Assert.Equal(2.5, UnitConversionService.InchesToFeet(30.0));
    }

    [Fact]
    public void FeetToInches_ConvertsCorrectly()
    {
        Assert.Equal(12.0, UnitConversionService.FeetToInches(1.0));
        Assert.Equal(6.0, UnitConversionService.FeetToInches(0.5));
        Assert.Equal(30.0, UnitConversionService.FeetToInches(2.5));
    }

    [Fact]
    public void FormatFeetInches_WholeNumber()
    {
        Assert.Equal("3'-0\"", UnitConversionService.FormatFeetInches(3.0));
    }

    [Fact]
    public void FormatFeetInches_WithInches()
    {
        Assert.Equal("3'-6.0\"", UnitConversionService.FormatFeetInches(3.5));
    }

    [Fact]
    public void FormatFeetInches_Zero()
    {
        Assert.Equal("0'-0\"", UnitConversionService.FormatFeetInches(0.0));
    }

    [Fact]
    public void ParseFeetInches_DecimalValue()
    {
        Assert.Equal(3.5, UnitConversionService.ParseFeetInches("3.5"));
    }

    [Fact]
    public void ParseFeetInches_EmptyString()
    {
        Assert.Equal(0.0, UnitConversionService.ParseFeetInches(""));
    }

    [Fact]
    public void ParseFeetInches_FeetInchesFormat()
    {
        var result = UnitConversionService.ParseFeetInches("3' 6\"");
        Assert.Equal(3.5, result);
    }

    [Fact]
    public void DefaultSystem_IsImperial()
    {
        var service = new UnitConversionService();
        Assert.Equal(UnitSystem.Imperial, service.CurrentSystem);
    }
}
