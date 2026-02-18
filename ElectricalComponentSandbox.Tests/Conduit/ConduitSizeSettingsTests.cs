using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Tests.Conduit;

/// <summary>
/// Tests for ConduitSizeSettings min-length validation and size lookup.
/// </summary>
public class ConduitSizeSettingsTests
{
    [Fact]
    public void DefaultEMT_HasExpectedSizes()
    {
        var settings = ConduitSizeSettings.CreateDefaultEMT();
        Assert.True(settings.Sizes.Count >= 9);
    }

    [Fact]
    public void GetSize_ValidTradeSize_ReturnsSizeInfo()
    {
        var settings = ConduitSizeSettings.CreateDefaultEMT();
        var size = settings.GetSize("1/2");
        Assert.NotNull(size);
        Assert.Equal(0.5, size!.NominalDiameter);
    }

    [Fact]
    public void GetSize_InvalidTradeSize_ReturnsNull()
    {
        var settings = ConduitSizeSettings.CreateDefaultEMT();
        Assert.Null(settings.GetSize("99"));
    }

    [Theory]
    [InlineData(0.1, true)]
    [InlineData(0.09, false)]
    [InlineData(10.0, true)]
    [InlineData(0.0, false)]
    [InlineData(-1.0, false)]
    public void IsValidLength_EnforcesMinimum(double lengthInches, bool expected)
    {
        var settings = ConduitSizeSettings.CreateDefaultEMT();
        Assert.Equal(expected, settings.IsValidLength(lengthInches));
    }

    [Fact]
    public void Iterator_EnumeratesAllSizes()
    {
        var settings = ConduitSizeSettings.CreateDefaultEMT();
        int count = 0;
        var enumerator = settings.GetEnumerator();
        while (enumerator.MoveNext()) count++;
        Assert.Equal(settings.Sizes.Count, count);
    }

    [Fact]
    public void AddSize_IncreasesSizeCount()
    {
        var settings = new ConduitSizeSettings();
        int before = settings.Sizes.Count;
        settings.AddSize(new ConduitSize { TradeSize = "test", NominalDiameter = 1.0 });
        Assert.Equal(before + 1, settings.Sizes.Count);
    }

    [Fact]
    public void RemoveSize_DecreasesSizeCount()
    {
        var settings = ConduitSizeSettings.CreateDefaultEMT();
        int before = settings.Sizes.Count;
        settings.RemoveSize("1/2");
        Assert.Equal(before - 1, settings.Sizes.Count);
    }
}
