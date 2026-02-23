using System.Reflection;
using System.Windows;

namespace ElectricalComponentSandbox.Tests;

public class ConduitVisualHostTests
{
    [Fact]
    public void ConduitVisualHost_NestedType_ExistsAndIsFrameworkElement()
    {
        var hostType = GetConduitVisualHostType();
        Assert.NotNull(hostType);
        Assert.True(typeof(FrameworkElement).IsAssignableFrom(hostType));
    }

    [Fact]
    public void ConduitVisualHost_PrivateConstructor_DoesNotThrow()
    {
        var hostType = GetConduitVisualHostType();
        var instance = Activator.CreateInstance(hostType, nonPublic: true);
        Assert.NotNull(instance);
    }

    private static Type GetConduitVisualHostType()
    {
        var hostType = typeof(MainWindow).GetNestedType("ConduitVisualHost", BindingFlags.NonPublic);
        return hostType ?? throw new InvalidOperationException("ConduitVisualHost nested type was not found.");
    }
}
