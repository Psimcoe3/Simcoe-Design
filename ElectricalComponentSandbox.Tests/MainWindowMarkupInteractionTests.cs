namespace ElectricalComponentSandbox.Tests;

public class MainWindowMarkupInteractionTests
{
    [Fact]
    public void GetMarkupHandleOverlayMode_PrefersDirectGeometryOverVerticesAndResize()
    {
        var mode = MainWindow.GetMarkupHandleOverlayMode(
            canEditArcAngles: false,
            canEditRadius: true,
            canEditVertices: true,
            canResize: true);

        Assert.Equal(MarkupHandleOverlayMode.DirectGeometry, mode);
    }

    [Fact]
    public void GetMarkupHandleOverlayMode_PrefersVerticesOverResizeWhenNoDirectGeometry()
    {
        var mode = MainWindow.GetMarkupHandleOverlayMode(
            canEditArcAngles: false,
            canEditRadius: false,
            canEditVertices: true,
            canResize: true);

        Assert.Equal(MarkupHandleOverlayMode.Vertices, mode);
    }

    [Fact]
    public void GetMarkupHandleOverlayMode_UsesResizeWhenItIsTheOnlyAvailableMode()
    {
        var mode = MainWindow.GetMarkupHandleOverlayMode(
            canEditArcAngles: false,
            canEditRadius: false,
            canEditVertices: false,
            canResize: true);

        Assert.Equal(MarkupHandleOverlayMode.Resize, mode);
    }

    [Fact]
    public void GetMarkupHandleOverlayMode_ReturnsNoneWhenNoHandlesAreAvailable()
    {
        var mode = MainWindow.GetMarkupHandleOverlayMode(
            canEditArcAngles: false,
            canEditRadius: false,
            canEditVertices: false,
            canResize: false);

        Assert.Equal(MarkupHandleOverlayMode.None, mode);
    }
}