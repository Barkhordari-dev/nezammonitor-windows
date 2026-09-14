using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class UpdateView : UserControl
{
    private readonly UpdateViewModel _vm;
    private readonly DispatcherTimer _uiTimer;

    public UpdateView()
    {
        InitializeComponent();
        _vm = new UpdateViewModel(DatabaseService.Instance);
        DataContext = _vm;

        // Timer updates button states every 500ms
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _uiTimer.Tick += (s, e) => UpdateButtonStates();

        // Start/stop timer based on visibility to prevent leaks & crashes
        Loaded += (_, _) =>
        {
            _uiTimer.Start();
            UpdateButtonStates();
        };
        Unloaded += (_, _) =>
        {
            _uiTimer.Stop();
        };
    }

    private void UpdateButtonStates()
    {
        try
        {
            var ec = ExtractionController.Instance;
            if (ec == null) return;

            // Start: enabled only when NOT busy
            BtnStart.IsEnabled = !ec.IsBusy;

            // Stop: enabled only when busy
            BtnStop.IsEnabled = ec.IsBusy;

            // Progress + Status
            ProgressBar.Value = ec.Progress;
            StatusText.Text = ec.StatusMessage;
            LogText.Text = ec.LogText;
        }
        catch (Exception ex)
        {
            // Log the error but don't crash
            System.Diagnostics.Debug.WriteLine($"UpdateButtonStates error: {ex.Message}");
        }
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        var ec = ExtractionController.Instance;
        // Pass credentials from UI to ExtractionController
        ec.Username = DataContext is ViewModels.UpdateViewModel vm ? vm.Username : "";
        ec.Password = DataContext is ViewModels.UpdateViewModel vm2 ? vm2.Password : "";
        ec.RememberMe = DataContext is ViewModels.UpdateViewModel vm3 ? vm3.RememberMe : false;
        
        if (ec.StartCommand is ViewModels.AsyncRelayCommand asyncCmd)
            await asyncCmd.ExecuteAsync();
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        ExtractionController.Instance.StopCommand.Execute(null);
    }

    private void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        ExtractionController.Instance.LoadLatestCommand.Execute(null);
        UpdateButtonStates();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // Always sync password to ViewModel
        if (PasswordBox.IsLoaded)
        {
            _vm.Password = PasswordBox.Password;

            // Save password when RememberMe is checked
            if (PasswordBox.Password.Length > 0)
            {
                var db = DatabaseService.Instance;
                var rememberMe = db.GetSetting("remember_me");
                if (rememberMe == "true")
                {
                    db.SaveSetting("password", PasswordBox.Password);
                }
            }
        }
    }

    private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "انتخاب پوشه ذخیره فایل‌ها"
        };
        if (dialog.ShowDialog() == true)
        {
            DatabaseService.Instance.SaveSetting("output_path", dialog.FolderName);
            if (Template.FindName("OutputPathBox", this) is TextBox tb)
                tb.Text = dialog.FolderName;
        }
    }
}
