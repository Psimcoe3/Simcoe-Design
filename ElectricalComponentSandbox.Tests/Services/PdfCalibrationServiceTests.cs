using System.Windows;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class PdfCalibrationServiceTests
{
    [Fact]
    public void Calibrate_ValidInput_ReturnsCorrectScale()
    {
        var service = new PdfCalibrationService();
        var p1 = new Point(0, 0);
        var p2 = new Point(100, 0);

        var result = service.Calibrate(p1, p2, 10.0);

        Assert.True(result.IsValid);
        Assert.Equal(10.0, result.PixelsPerUnit);
        Assert.Equal(100.0, result.PixelDistance);
        Assert.Equal(10.0, result.RealDistance);
    }

    [Fact]
    public void Calibrate_DiagonalDistance_CalculatesCorrectly()
    {
        var service = new PdfCalibrationService();
        var p1 = new Point(0, 0);
        var p2 = new Point(30, 40); // 3-4-5 triangle, distance = 50

        var result = service.Calibrate(p1, p2, 5.0);

        Assert.True(result.IsValid);
        Assert.Equal(10.0, result.PixelsPerUnit);
    }

    [Fact]
    public void Calibrate_ZeroDistance_ReturnsInvalid()
    {
        var service = new PdfCalibrationService();
        var p1 = new Point(0, 0);
        var p2 = new Point(100, 0);

        var result = service.Calibrate(p1, p2, 0);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Calibrate_NegativeDistance_ReturnsInvalid()
    {
        var service = new PdfCalibrationService();
        var p1 = new Point(0, 0);
        var p2 = new Point(100, 0);

        var result = service.Calibrate(p1, p2, -5);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Calibrate_SamePoints_ReturnsInvalid()
    {
        var service = new PdfCalibrationService();
        var p1 = new Point(50, 50);
        var p2 = new Point(50, 50);

        var result = service.Calibrate(p1, p2, 10);

        Assert.False(result.IsValid);
    }
}
