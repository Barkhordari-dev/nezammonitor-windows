using System.Windows;
using System.Windows.Threading;
using NezamMonitor.App.Services;

namespace NezamMonitor.App;
public partial class App : Application
{
    private static NavigationService? _nav;
    public static NavigationService Navigation => _nav ?? throw new InvalidOperationException("Navigation not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _nav = new NavigationService();

        // Global exception handlers
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[CRASH] Dispatcher: {e.Exception}");
        e.Handled = true; // Prevent app crash
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            System.Diagnostics.Debug.WriteLine($"[CRASH] AppDomain: {ex}");
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[CRASH] TaskScheduler: {e.Exception}");
        e.SetObserved(); // Prevent app crash
    }
}
