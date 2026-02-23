using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
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
        var instance = RunInSta(() => Activator.CreateInstance(hostType, nonPublic: true));
        Assert.NotNull(instance);
    }

    private static Type GetConduitVisualHostType()
    {
        var hostType = typeof(MainWindow).GetNestedType("ConduitVisualHost", BindingFlags.NonPublic);
        return hostType ?? throw new InvalidOperationException("ConduitVisualHost nested type was not found.");
    }

    private static T RunInSta<T>(Func<T> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        T? result = default;
        Exception? capturedException = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (capturedException != null)
            ExceptionDispatchInfo.Capture(capturedException).Throw();

        return result!;
    }
}
