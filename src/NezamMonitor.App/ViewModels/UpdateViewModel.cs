using System.ComponentModel;
using System.Windows.Input;
using NezamMonitor.Core.Data;

namespace NezamMonitor.App.ViewModels;

public sealed class UpdateViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _username = "";
    private string _password = "";
    private bool _rememberMe;

    public string Username { get => _username; set {
        if (_username == value) return;
        SetProperty(ref _username, value);
        try { _db.SaveSetting("username", value); } catch { }
    } }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public bool RememberMe { get => _rememberMe; set => SetProperty(ref _rememberMe, value); }

    public UpdateViewModel(NezamDatabase db)
    {
        _db = db;
        try
        {
            var settings = _db.LoadSettings();
            if (settings.TryGetValue("username", out var u)) _username = u;
            if (settings.TryGetValue("password", out var p)) _password = p;
        }
        catch { }
    }
}
