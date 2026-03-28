using System.Windows.Input;

namespace ElectricalComponentSandbox.Tests;

public class MainWindowShortcutTests
{
    [Fact]
    public void IsEditSelectedMarkupGeometryShortcut_MatchesCtrlShiftGOnly()
    {
        Assert.True(MainWindow.IsEditSelectedMarkupGeometryShortcut(Key.G, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.False(MainWindow.IsEditSelectedMarkupGeometryShortcut(Key.G, ModifierKeys.Control));
        Assert.False(MainWindow.IsEditSelectedMarkupGeometryShortcut(Key.F2, ModifierKeys.Control | ModifierKeys.Shift));
    }

    [Fact]
    public void IsEditSelectedStructuredMarkupTextShortcut_MatchesF2OnlyWithoutModifiers()
    {
        Assert.True(MainWindow.IsEditSelectedStructuredMarkupTextShortcut(Key.F2, ModifierKeys.None));
        Assert.False(MainWindow.IsEditSelectedStructuredMarkupTextShortcut(Key.F2, ModifierKeys.Control));
        Assert.False(MainWindow.IsEditSelectedStructuredMarkupTextShortcut(Key.G, ModifierKeys.None));
    }

    [Fact]
    public void IsDeleteSelectedMarkupOrComponentShortcut_MatchesDeleteOrBackOnlyWithoutModifiers()
    {
        Assert.True(MainWindow.IsDeleteSelectedMarkupOrComponentShortcut(Key.Delete, ModifierKeys.None));
        Assert.True(MainWindow.IsDeleteSelectedMarkupOrComponentShortcut(Key.Back, ModifierKeys.None));
        Assert.False(MainWindow.IsDeleteSelectedMarkupOrComponentShortcut(Key.Delete, ModifierKeys.Control));
        Assert.False(MainWindow.IsDeleteSelectedMarkupOrComponentShortcut(Key.Escape, ModifierKeys.None));
    }

    [Fact]
    public void IsCancelActiveInteractionShortcut_MatchesEscapeOnlyWithoutModifiers()
    {
        Assert.True(MainWindow.IsCancelActiveInteractionShortcut(Key.Escape, ModifierKeys.None));
        Assert.False(MainWindow.IsCancelActiveInteractionShortcut(Key.Escape, ModifierKeys.Shift));
        Assert.False(MainWindow.IsCancelActiveInteractionShortcut(Key.Delete, ModifierKeys.None));
    }
}