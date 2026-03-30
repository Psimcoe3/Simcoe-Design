namespace ElectricalComponentSandbox.Tests;

internal static class WpfStaTestSynchronization
{
    internal static object MainWindowLock { get; } = new();
}