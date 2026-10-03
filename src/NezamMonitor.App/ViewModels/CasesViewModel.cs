using System.Collections.ObjectModel;
using System.Windows.Input;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.App.ViewModels;

public sealed class CasesViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _searchText = "";
    private string _statusMessage = "در حال بارگذاری...";
    private string _detailTitle = "";
    private string _detailEngineers = "";
    private string _detailFees = "";
    private string _detailReports = "";
    private string _detailSpecs = "";
    private CaseDisplayItem? _selectedCase;

    public ObservableCollection<CaseDisplayItem> Cases { get; } = new();
    public string SearchText { get => _searchText; set { SetProperty(ref _searchText, value); FilterCases(); } }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string DetailTitle { get => _detailTitle; set => SetProperty(ref _detailTitle, value); }
    public string DetailEngineers { get => _detailEngineers; set => SetProperty(ref _detailEngineers, value); }
    public string DetailFees { get => _detailFees; set => SetProperty(ref _detailFees, value); }
    public string DetailReports { get => _detailReports; set => SetProperty(ref _detailReports, value); }
    public string DetailSpecs { get => _detailSpecs; set => SetProperty(ref _detailSpecs, value); }
    public CaseDisplayItem? SelectedCase { get => _selectedCase; set => SetProperty(ref _selectedCase, value); }
    public bool HasDetail => SelectedCase != null;
    public ICommand RefreshCommand { get; }
    public ICommand CloseDetailCommand { get; }

    private List<CaseDisplayItem> _allCases = new();

    public CasesViewModel(NezamDatabase db)
    {
        _db = db;
        RefreshCommand = new RelayCommand(LoadCases);
        CloseDetailCommand = new RelayCommand(CloseDetail);
        LoadCases();
    }

    private void LoadCases()
    {
        try
        {
            Cases.Clear();
            var snapshotId = _db.GetActiveSnapshotId();
            if (snapshotId == 0)
            {
                StatusMessage = "هنوز داده‌ای ذخیره نشده است.";
                return;
            }
            var rawCases = _db.LoadCases(snapshotId);
            _allCases = rawCases.Select((c, i) => new CaseDisplayItem(c) { RowNumber = i + 1 }).ToList();
            foreach (var item in _allCases) Cases.Add(item);
            StatusMessage = $"{Cases.Count} پرونده بارگذاری شد";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
        }
    }

    public void ShowCaseDetails(CaseDisplayItem item)
    {
        SelectedCase = item;
        DetailTitle = $"{item.CaseNumber} | {item.Owner}";

        // Engineers
        var engineers = item.RawCase.Engineers;
        if (engineers.Count > 0)
        {
            var lines = engineers.Select(e =>
            {
                var role = string.IsNullOrEmpty(e.Role) ? "" : $" ({e.Role})";
                return $"  {e.Discipline}: {e.Name}{role}";
            });
            DetailEngineers = string.Join("\n", lines);
        }
        else
            DetailEngineers = "  (ندارد)";

        // Fees — نگاشت نوع خدمت مثل منو حق‌الزحمه (به‌جای حروف n/b/ho/t/pn)
        var fees = item.RawCase.Fees;
        if (fees.Count > 0)
        {
            var stage0 = fees.Where(f => f.Stage?.Trim() == "0").ToList();
            var amountMap = new Dictionary<Fee, long>();
            foreach (var f in stage0) amountMap[f] = ParseFeeAmount(f.Amount);
            var ordered0 = stage0.OrderByDescending(f => amountMap[f]).ToList();
            string[] order0 = { "حسن انجام کار", "نظارت سهم سازمان", "مالیات نظارت سهم سازمان", "بیمه" };
            var serviceForFee = new Dictionary<Fee, string>();
            for (int i = 0; i < ordered0.Count; i++)
            {
                string svc = i < order0.Length ? order0[i] : order0[^1];
                serviceForFee[ordered0[i]] = svc;
            }
            var lines = fees.Select(f =>
            {
                string svc;
                if (f.Stage?.Trim() != "0")
                    svc = "نظارت";
                else if (!serviceForFee.TryGetValue(f, out svc!))
                    svc = "بیمه";
                var pay = string.IsNullOrEmpty(f.PayStatus) || f.PayStatus == "_" ? "" : $" [{f.PayStatus}]";
                return $"  {svc} مرحله {f.Stage}: {f.Amount}{pay}";
            });
            DetailFees = string.Join("\n", lines);
        }
        else
            DetailFees = "  (ندارد)";

        // Reports
        var reports = item.RawCase.Reports;
        if (reports.Count > 0)
        {
            var lines = reports.Take(15).Select(r =>
                $"  {r.ReportType} مرحله {r.Stage} | {r.Engineer} | {r.VisitDate}");
            var text = string.Join("\n", lines);
            if (reports.Count > 15)
                text += $"\n  ... و {reports.Count - 15} گزارش دیگر";
            DetailReports = text;
        }
        else
            DetailReports = "  (ندارد)";

        // Specs
        var s = item.RawCase.Specification;
        if (s != null)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(s.UsageType)) lines.Add($"  نوع کاربری: {s.UsageType}");
            if (!string.IsNullOrEmpty(s.BuildingGroup)) lines.Add($"  گروه ساختمانی: {s.BuildingGroup}");
            if (!string.IsNullOrEmpty(s.RenovationCode)) lines.Add($"  کد نوسازی: {s.RenovationCode}");
            if (!string.IsNullOrEmpty(s.PlanInstructionNo)) lines.Add($"  شماره دستور نقشه: {s.PlanInstructionNo}");
            if (!string.IsNullOrEmpty(s.PlanInstructionType)) lines.Add($"  نوع دستور نقشه: {s.PlanInstructionType}");
            if (!string.IsNullOrEmpty(s.PlanInstructionDate)) lines.Add($"  تاریخ دستور نقشه: {s.PlanInstructionDate}");
            if (!string.IsNullOrEmpty(s.LandArea)) lines.Add($"  مساحت زمین: {s.LandArea}");
            if (!string.IsNullOrEmpty(s.ParafArea)) lines.Add($"  متراژ پاراف: {s.ParafArea}");
            if (!string.IsNullOrEmpty(s.CapacityArea)) lines.Add($"  متراژ کسر ظرفیت: {s.CapacityArea}");
            if (!string.IsNullOrEmpty(s.StructureType)) lines.Add($"  نوع سازه: {s.StructureType}");
            if (!string.IsNullOrEmpty(s.BlockTitle)) lines.Add($"  عنوان بلوک: {s.BlockTitle}");
            if (!string.IsNullOrEmpty(s.BlockCount)) lines.Add($"  تعداد بلوک: {s.BlockCount}");
            if (!string.IsNullOrEmpty(s.Floors)) lines.Add($"  طبقات: {s.Floors}");
            if (!string.IsNullOrEmpty(s.Units)) lines.Add($"  واحدها: {s.Units}");
            if (!string.IsNullOrEmpty(s.Issuer)) lines.Add($"  صادرکننده: {s.Issuer}");
            if (!string.IsNullOrEmpty(s.PermitNumber)) lines.Add($"  شماره پروانه: {s.PermitNumber}");
            if (!string.IsNullOrEmpty(s.PermitDate)) lines.Add($"  تاریخ صدور: {s.PermitDate}");
            if (!string.IsNullOrEmpty(s.ReleaseDate)) lines.Add($"  تاریخ ترخیص: {s.ReleaseDate}");
            if (!string.IsNullOrEmpty(s.PlanZone)) lines.Add($"  محدوده طرح: {s.PlanZone}");
            if (!string.IsNullOrEmpty(s.Address)) lines.Add($"  آدرس: {s.Address}");
            DetailSpecs = lines.Count > 0 ? string.Join("\n", lines) : "  (بدون فیلد)";
        }
        else
            DetailSpecs = "  (ندارد)";

        OnPropertyChanged(nameof(HasDetail));
    }

    private void CloseDetail()
    {
        SelectedCase = null;
        DetailTitle = "";
        DetailEngineers = "";
        DetailFees = "";
        DetailReports = "";
        DetailSpecs = "";
        OnPropertyChanged(nameof(HasDetail));
    }

    private static long ParseFeeAmount(string amount)
    {
        if (string.IsNullOrWhiteSpace(amount)) return 0;
        var cleaned = amount.Replace("ریال", "").Replace(",", "").Replace(" ", "").Trim();
        cleaned = cleaned.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4").Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9").Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4").Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9");
        return long.TryParse(cleaned, out var r) ? r : 0;
    }

    private void FilterCases()
    {
        Cases.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allCases
            : _allCases.Where(c =>
                c.CaseNumber.Contains(SearchText) ||
                c.Owner.Contains(SearchText) ||
                c.Serial.Contains(SearchText) ||
                c.Id.ToString().Contains(SearchText)).ToList();
        foreach (var c in filtered) Cases.Add(c);
        StatusMessage = $"{Cases.Count} پرونده";
    }
}

/// <summary>Flattened display item that exposes Case + Specification fields for DataGrid binding.</summary>
public sealed class CaseDisplayItem : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void On(string? n) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; On(nameof(IsSelected)); } }
    public Case RawCase { get; }
    public int RowNumber { get; set; }

    // Case fields
    public long Id => RawCase.Id;
    public string CaseNumber => RawCase.CaseNumber;
    public string Serial => RawCase.Serial;
    public string Owner => RawCase.Owner;
    public string OwnerMobile => RawCase.OwnerMobile;
    public string Responsibility => RawCase.Responsibility;
    public string CapacityDate => RawCase.CapacityDate;
    public string Office => RawCase.Office;
    public string OwnerFather => RawCase.OwnerFather;
    public string OwnerNationalCode => RawCase.OwnerNationalCode;
    public string OwnerAddress => RawCase.OwnerAddress;
    public string OwnerZip => RawCase.OwnerZip;
    public string OwnerTel => RawCase.OwnerTel;
    public string OwnerBirthLoc => RawCase.OwnerBirthLoc;

    // Specification fields (flattened)
    public string BuildingGroup => RawCase.Specification?.BuildingGroup ?? "";
    public string RenovationCode => RawCase.Specification?.RenovationCode ?? "";
    public string PlanInstructionNo => RawCase.Specification?.PlanInstructionNo ?? "";
    public string PlanInstructionType => RawCase.Specification?.PlanInstructionType ?? "";
    public string PlanInstructionDate => RawCase.Specification?.PlanInstructionDate ?? "";
    public string LandArea => RawCase.Specification?.LandArea ?? "";
    public string ParafArea => RawCase.Specification?.ParafArea ?? "";
    public string CapacityArea => RawCase.Specification?.CapacityArea ?? "";
    public string StructureType => RawCase.Specification?.StructureType ?? "";
    public string BlockTitle => RawCase.Specification?.BlockTitle ?? "";
    public string BlockCount => RawCase.Specification?.BlockCount ?? "";
    public string Floors => RawCase.Specification?.Floors ?? "";
    public string Units => RawCase.Specification?.Units ?? "";
    public string Issuer => RawCase.Specification?.Issuer ?? "";
    public string PermitNumber => RawCase.Specification?.PermitNumber ?? "";
    public string PermitDate => RawCase.Specification?.PermitDate ?? "";
    public string ReleaseDate => RawCase.Specification?.ReleaseDate ?? "";
    public string PlanZone => RawCase.Specification?.PlanZone ?? "";
    public string Address => RawCase.Specification?.Address ?? "";
    public string UsageType => RawCase.Specification?.UsageType ?? "";

    public CaseDisplayItem(Case c) => RawCase = c;
}
