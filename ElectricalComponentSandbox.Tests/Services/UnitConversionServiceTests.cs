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
        Assert.Equal("3'-6\"", UnitConversionService.FormatFeetInches(3.5));
    }

    [Fact]
    public void FormatFeetInches_WithFraction_DefaultsToSixteenths()
    {
        var value = 2.0 + (3.625 / 12.0); // 2'-3 5/8"
        Assert.Equal("2'-3 5/8\"", UnitConversionService.FormatFeetInches(value));
    }

    [Fact]
    public void FormatFeetInches_WithCustomIncrement()
    {
        var value = 1.0 + (7.0 / 12.0); // 1'-7"
        Assert.Equal("1'-7\"", UnitConversionService.FormatFeetInches(value, 8));
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
    public void ParseFeetInches_FeetInchesFractionFormat()
    {
        var result = UnitConversionService.ParseFeetInches("3'-6 1/2\"");
        Assert.Equal(3.5416666666666665, result, 8);
    }

    [Fact]
    public void TryParseLength_InchesOnlyFractionFormat()
    {
        var ok = UnitConversionService.TryParseLength("6 1/2\"", out var result);
        Assert.True(ok);
        Assert.Equal(0.5416666666666666, result, 8);
    }

    [Fact]
    public void DefaultSystem_IsImperial()
    {
        var service = new UnitConversionService();
        Assert.Equal(UnitSystem.Imperial, service.CurrentSystem);
    }
}
