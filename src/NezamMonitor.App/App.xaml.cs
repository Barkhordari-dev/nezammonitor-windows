using System.Windows;
using System.Windows.Threading;

namespace NezamMonitor.App;
public partial class App : Application
{
    private static Services.NavigationService? _nav;
    public static Services.NavigationService Navigation => _nav ?? throw new InvalidOperationException("Navigation not initialized.");

    public App()
    {
        InitializeComponent();

        // مقدار پیش‌فرض RTL برای تمام Resource های جهت‌دهی
        Resources["MainWindowFlowDirection"] = FlowDirection.RightToLeft;
        Resources["TextFlowDirection"] = FlowDirection.RightToLeft;
        Resources["TableFlowDirection"] = FlowDirection.RightToLeft;
        Resources["MenuFlowDirection"] = FlowDirection.RightToLeft;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _nav = new Services.NavigationService();

        // Global exception handlers
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[CRASH] Dispatcher: {e.Exception}");
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            System.Diagnostics.Debug.WriteLine($"[CRASH] AppDomain: {ex}");
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[CRASH] TaskScheduler: {e.Exception}");
        e.SetObserved();
    }
}
