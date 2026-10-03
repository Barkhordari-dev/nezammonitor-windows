using System.Globalization;
using System.Windows.Input;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;

    private int _totalCases;
    private int _totalEngineers;
    private int _totalFees;
    private int _totalReports;
    private string _connectionStatus = "غیر متصل";
    private string _lastUpdate = "هنوز انجام نشده";

    private int _casesWithSpecs;
    private int _casesWithoutSpecs;
    private int _casesWithEngineers;
    private int _casesWithoutEngineers;
    private int _casesWithFees;
    private int _casesWithoutFees;
    private int _casesWithReports;
    private int _casesWithoutReports;


    public int TotalCases { get => _totalCases; set => SetProperty(ref _totalCases, value); }
    public int TotalEngineers { get => _totalEngineers; set => SetProperty(ref _totalEngineers, value); }
    public int TotalFees { get => _totalFees; set => SetProperty(ref _totalFees, value); }
    public int TotalReports { get => _totalReports; set => SetProperty(ref _totalReports, value); }
    public string ConnectionStatus { get => _connectionStatus; set => SetProperty(ref _connectionStatus, value); }
    public string LastUpdate { get => _lastUpdate; set => SetProperty(ref _lastUpdate, value); }

    public int CasesWithSpecs { get => _casesWithSpecs; set => SetProperty(ref _casesWithSpecs, value); }
    public int CasesWithoutSpecs { get => _casesWithoutSpecs; set => SetProperty(ref _casesWithoutSpecs, value); }
    public int CasesWithEngineers { get => _casesWithEngineers; set => SetProperty(ref _casesWithEngineers, value); }
    public int CasesWithoutEngineers { get => _casesWithoutEngineers; set => SetProperty(ref _casesWithoutEngineers, value); }
    public int CasesWithFees { get => _casesWithFees; set => SetProperty(ref _casesWithFees, value); }
    public int CasesWithoutFees { get => _casesWithoutFees; set => SetProperty(ref _casesWithoutFees, value); }
    public int CasesWithReports { get => _casesWithReports; set => SetProperty(ref _casesWithReports, value); }
    public int CasesWithoutReports { get => _casesWithoutReports; set => SetProperty(ref _casesWithoutReports, value); }

    public ICommand RefreshCommand { get; }

    public DashboardViewModel(NezamDatabase db)
    {
        _db = db;
        RefreshCommand = new RelayCommand(() => LoadDashboard());
        _ = LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        await Task.Delay(150);
        LoadDashboard();
    }

    private void LoadDashboard()
    {
        try
        {
            var snapshotId = _db.GetActiveSnapshotId();
            if (snapshotId == 0)
            {
                ConnectionStatus = "\u063a\u064a\u0631 \u0645\u062a\u0635\u0644";
                LastUpdate = "\u0647\u0646\u0648\u0632 \u0627\u0646\u062c\u0627\u0645 \u0646\u0634\u062f\u0647";
                return;
            }

            var cases = _db.LoadCases(snapshotId);

            TotalCases = cases.Count;
            TotalEngineers = cases.Sum(c => c.Engineers.Count);
            TotalFees = cases.Sum(c => c.Fees.Count);
            TotalReports = cases.Sum(c => c.Reports.Count);
            ConnectionStatus = "متصل";
            LastUpdate = cases.Count + " \u067e\u0631\u0648\u0646\u062f\u0647 \u0641\u0639\u0627\u0644";

            CasesWithSpecs = cases.Count(c => c.Specification != null);
            CasesWithoutSpecs = TotalCases - CasesWithSpecs;
            CasesWithEngineers = cases.Count(c => c.Engineers.Count > 0);
            CasesWithoutEngineers = TotalCases - CasesWithEngineers;
            CasesWithFees = cases.Count(c => c.Fees.Count > 0);
            CasesWithoutFees = TotalCases - CasesWithFees;
            CasesWithReports = cases.Count(c => c.Reports.Count > 0);
            CasesWithoutReports = TotalCases - CasesWithReports;
        }
        catch
        {
            ConnectionStatus = "خطا در بارگذاری";
        }
    }
}
