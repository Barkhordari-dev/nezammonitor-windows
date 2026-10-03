using System.Windows;
using System.Windows.Controls;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContext = new DashboardViewModel(DatabaseService.Instance);
    }

    private void GoToUpdate_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateTo("update");
    }

    private void GoToFollowUp_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateTo("followup");
    }

    private void GoToExcel_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateTo("excel");
    }

    private void GoToCases_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateTo("cases");
    }
}
