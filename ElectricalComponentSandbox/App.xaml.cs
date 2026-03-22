using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Dimensioning;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>Application-wide service provider (DI container)</summary>
    public IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ActionLogService.Instance.Log(LogCategory.Application, "Application starting",
            $"Args: [{string.Join(", ", e.Args)}]");

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        // Build the DI container
        var sc = new ServiceCollection();
        ConfigureServices(sc);
        Services = sc.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services (singletons — one instance for the entire app lifetime)
        services.AddSingleton<ComponentFileService>();
        services.AddSingleton<ProjectFileService>();
        services.AddSingleton<UndoRedoService>();
        services.AddSingleton<UnitConversionService>();
        services.AddSingleton<BomExportService>();
        services.AddSingleton<SnapService>();
        services.AddSingleton<PdfCalibrationService>();
        services.AddSingleton<MarkupRenderService>();
        services.AddSingleton<Dimension2DService>();
        services.AddSingleton<ShadowGeometryTree>();

        // ViewModels
        services.AddSingleton<MainViewModel>();

        // Main window
        services.AddTransient<MainWindow>();
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

