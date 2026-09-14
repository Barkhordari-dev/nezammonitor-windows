using System.Windows;
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
    }
}
