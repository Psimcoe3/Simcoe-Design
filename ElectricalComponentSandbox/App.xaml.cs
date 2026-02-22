using System.Windows;
using System.Windows.Threading;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ActionLogService.Instance.Log(LogCategory.Application, "Application starting",
            $"Args: [{string.Join(", ", e.Args)}]");

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.Application, "Application exiting",
            $"Exit code: {e.ApplicationExitCode}");
        ActionLogService.Instance.EndSession();
        ActionLogService.Instance.Dispose();

        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ActionLogService.Instance.LogError(LogCategory.Error, "Unhandled UI thread exception", e.Exception);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            ActionLogService.Instance.LogError(LogCategory.Error, "Unhandled domain exception", ex);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ActionLogService.Instance.LogError(LogCategory.Error, "Unobserved task exception", e.Exception);
    }
}

