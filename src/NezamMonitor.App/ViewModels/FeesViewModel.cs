using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Excel;
using NezamMonitor.Core.Models;
using NezamMonitor.App.Views;

namespace NezamMonitor.App.ViewModels;

public sealed class FeesViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _searchText = "";
    private string _statusMessage = "";
    private string _filterStatus = "همه";
    private string _filterDiscipline = "همه";
    private string _filterStage = "همه";
    private string _filterOwner = "همه";
    private string _filterCaseNumber = "همه";
    private string _filterServiceType = "همه";
    private string _filterAmountFrom = "";
    private string _filterAmountTo = "";

    // Summary
    private string _totalAmount = "۰";
    private string _paidAmount = "۰";
    private string _confirmedAmount = "۰";
    private string _pendingAmount = "۰";
    private int _totalCount;
    private int _paidCount;
    private int _confirmedCount;
    private int _pendingCount;

    public ObservableCollection<FeeItem> Fees { get; } = new();
    public string SearchText { get => _searchText; set { SetProperty(ref _searchText, value); ApplyFilters(); } }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string FilterStatus { get => _filterStatus; set { SetProperty(ref _filterStatus, value); ApplyFilters(); } }
    public string FilterDiscipline { get => _filterDiscipline; set { SetProperty(ref _filterDiscipline, value); ApplyFilters(); } }
    public string FilterStage { get => _filterStage; set { SetProperty(ref _filterStage, value); ApplyFilters(); } }
    public string FilterOwner { get => _filterOwner; set { SetProperty(ref _filterOwner, value); ApplyFilters(); } }
    public string FilterCaseNumber { get => _filterCaseNumber; set { SetProperty(ref _filterCaseNumber, value); ApplyFilters(); } }
    public string FilterServiceType { get => _filterServiceType; set { SetProperty(ref _filterServiceType, value); ApplyFilters(); } }
    public string FilterAmountFrom { get => _filterAmountFrom; set { SetProperty(ref _filterAmountFrom, value); ApplyFilters(); } }
    public string FilterAmountTo { get => _filterAmountTo; set { SetProperty(ref _filterAmountTo, value); ApplyFilters(); } }

    public string TotalAmount { get => _totalAmount; set => SetProperty(ref _totalAmount, value); }
    public string PaidAmount { get => _paidAmount; set => SetProperty(ref _paidAmount, value); }
    public string ConfirmedAmount { get => _confirmedAmount; set => SetProperty(ref _confirmedAmount, value); }
    public string PendingAmount { get => _pendingAmount; set => SetProperty(ref _pendingAmount, value); }
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
    public int PaidCount { get => _paidCount; set => SetProperty(ref _paidCount, value); }
    public int ConfirmedCount { get => _confirmedCount; set => SetProperty(ref _confirmedCount, value); }
    public int PendingCount { get => _pendingCount; set => SetProperty(ref _pendingCount, value); }

    private int _filteredCount;
    public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }
    public string CountDisplay => FilteredCount == TotalCount
        ? $"{TotalCount} مورد"
        : $"{FilteredCount} از {TotalCount} مورد";

    public bool HasActiveFilters => FilterStatus != "همه" || FilterDiscipline != "همه" || FilterStage != "همه" ||
                                     FilterOwner != "همه" || FilterCaseNumber != "همه" || FilterServiceType != "همه" ||
                                     !string.IsNullOrWhiteSpace(FilterAmountFrom) || !string.IsNullOrWhiteSpace(FilterAmountTo) ||
                                     !string.IsNullOrWhiteSpace(SearchText);

    // Dynamic filter lists
    public List<string> StatusFilters { get; set; } = new() { "همه" };
    public List<string> DisciplineFilters { get; set; } = new() { "همه" };
    public List<string> StageFilters { get; set; } = new() { "همه" };
    public List<string> OwnerFilters { get; set; } = new() { "همه" };
    public List<string> CaseNumberFilters { get; set; } = new() { "همه" };
    public List<string> ServiceTypeFilters { get; set; } = new() { "همه" };

    public ICommand RefreshCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand FilterByValueCommand { get; }

    private string _selectedAmount = "۰ ریال";
    private int _selectedCount;
    public string SelectedAmount { get => _selectedAmount; set => SetProperty(ref _selectedAmount, value); }
    public int SelectedCount { get => _selectedCount; set => SetProperty(ref _selectedCount, value); }

    private List<FeeItem> _all = new();

    // ── دریافتی ──
    private int _activeTab = 0;
    public int ActiveTab { get => _activeTab; set => SetProperty(ref _activeTab, value); }
    public System.Collections.ObjectModel.ObservableCollection<FeeReceiptRow> Receipts { get; } = new();
    public System.Windows.Input.ICommand AddReceiptCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand EditReceiptCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand DeleteReceiptCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand RefreshReceiptsCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand ExportReceiptsCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand ImportReceiptsCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand UpdateReceiptsCommand { get; private set; } = null!;
    private string _receiptSearch = "";
    public string ReceiptSearch { get => _receiptSearch; set { SetProperty(ref _receiptSearch, value); ApplyReceiptFilter(); } }
    private string _receiptStatus = "";
    public string ReceiptStatus { get => _receiptStatus; set => SetProperty(ref _receiptStatus, value); }
    private List<FeeReceiptRow> _allReceiptsVm = new();
    private List<string> _distinctStartDates = new();
    private List<string> _distinctStartPaid = new();
    private List<string> _distinctStartConfirmed = new();
    // alias برای سازگاری
    private List<string> _distinctEndDates { get => _distinctStartPaid; set => _distinctStartPaid = value; }
    private string _receiptTotalAmount = "۰ ریال";
    private string _receiptTotalFeeSum = "۰ ریال";
    private string _receiptTotalDiff = "۰ ریال";
    public string ReceiptTotalAmount { get => _receiptTotalAmount; set => SetProperty(ref _receiptTotalAmount, value); }
    public string ReceiptTotalFeeSum { get => _receiptTotalFeeSum; set => SetProperty(ref _receiptTotalFeeSum, value); }
    public string ReceiptTotalDiff { get => _receiptTotalDiff; set => SetProperty(ref _receiptTotalDiff, value); }
    // جمع انتخاب‌شده دریافتی
    private int _receiptSelectedCount;
    public int ReceiptSelectedCount { get => _receiptSelectedCount; set => SetProperty(ref _receiptSelectedCount, value); }
    private string _receiptSelectedAmount = "۰ ریال";
    public string ReceiptSelectedAmount { get => _receiptSelectedAmount; set => SetProperty(ref _receiptSelectedAmount, value); }
    private string _receiptSelectedFeeSum = "۰ ریال";
    public string ReceiptSelectedFeeSum { get => _receiptSelectedFeeSum; set => SetProperty(ref _receiptSelectedFeeSum, value); }
    private string _receiptSelectedDiff = "۰ ریال";
    public string ReceiptSelectedDiff { get => _receiptSelectedDiff; set => SetProperty(ref _receiptSelectedDiff, value); }

    public FeesViewModel(NezamDatabase db)
    {
        _db = db;
        RefreshCommand = new RelayCommand(Load);
        SelectAllCommand = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        FilterByValueCommand = new RelayCommand<string>(FilterByValue);
        AddReceiptCommand = new RelayCommand(AddReceipt);
        EditReceiptCommand = new RelayCommand<FeeReceiptRow>(EditReceipt);
        DeleteReceiptCommand = new RelayCommand<FeeReceiptRow>(DeleteReceipt);
        RefreshReceiptsCommand = new RelayCommand(LoadReceipts);
        ExportReceiptsCommand = new RelayCommand(ExportReceipts);
        ImportReceiptsCommand = new RelayCommand(ImportReceipts);
        UpdateReceiptsCommand = new RelayCommand(UpdateReceipts);
        Load();
        LoadReceipts();
    }

    // نگاشت نوع خدمت بر اساس قواعد کاربر
    private static string MapServiceForStage(string stage)
    {
        if (stage?.Trim() != "0") return "نظارت";
        return ""; // برای مرحله 0 بعداً بر اساس مبلغ پر می‌شود
    }

    private void Load()
    {
        Fees.Clear();
        var snapshotId = _db.GetActiveSnapshotId();
        if (snapshotId == 0) { StatusMessage = "داده‌ای موجود نیست"; return; }
        var cases = _db.LoadCases(snapshotId);
        _all.Clear();
        foreach (var c in cases)
        {
            // مرحله 0 این پرونده — مرتب‌سازی نزولی مبلغ برای تخصیص نوع خدمت
            var stage0 = c.Fees.Where(f => f.Stage?.Trim() == "0").ToList();
            var amountMap = new Dictionary<Fee, long>();
            foreach (var f in stage0) amountMap[f] = ParseAmount(f.Amount);
            var ordered0 = stage0.OrderByDescending(f => amountMap[f]).ToList();
            string[] order0 = { "حسن انجام کار", "نظارت سهم سازمان", "مالیات نظارت سهم سازمان", "بیمه" };
            var serviceForFee = new Dictionary<Fee, string>();
            for (int i = 0; i < ordered0.Count; i++)
            {
                string svc = i < order0.Length ? order0[i] : order0[^1];
                serviceForFee[ordered0[i]] = svc;
            }

            foreach (var f in c.Fees)
            {
                string svc;
                if (f.Stage?.Trim() != "0")
                    svc = "نظارت";
                else if (!serviceForFee.TryGetValue(f, out svc!))
                    svc = "بیمه"; // fallback
                // اگر ServiceType از DB پر بود و مرحله !=0 بود، همان نظارت را نگه می‌داریم
                // برای مرحله 0 حتماً نگاشت مرتب‌شده را اعمال می‌کنیم

                var item = new FeeItem
                {
                    CaseNumber = c.CaseNumber, Owner = c.Owner,
                    Discipline = f.Discipline, ServiceType = svc, AmountType = f.AmountType,
                    Stage = f.Stage ?? "", Amount = f.Amount, PayStatus = f.PayStatus,
                    ConfirmStatus = f.ConfirmStatus, StartDate = f.StartDate, EndDate = f.EndDate,
                    Description = f.Description
                };
                item.OnSelectedChanged = UpdateSelectedSummary;
                _all.Add(item);
            }
        }

        // Build dynamic filter lists
        StatusFilters = FilterHelper.ExtractDistinctValues(_all, f => f.PayStatus);
        DisciplineFilters = FilterHelper.ExtractDistinctValues(_all, f => f.Discipline);
        StageFilters = FilterHelper.ExtractDistinctValues(_all, f => f.Stage);
        OwnerFilters = FilterHelper.ExtractDistinctValues(_all, f => f.Owner);
        CaseNumberFilters = FilterHelper.ExtractDistinctValues(_all, f => f.CaseNumber);
        ServiceTypeFilters = FilterHelper.ExtractDistinctValues(_all, f => f.ServiceType);
        OnPropertyChanged(nameof(StatusFilters));
        OnPropertyChanged(nameof(DisciplineFilters));
        OnPropertyChanged(nameof(StageFilters));
        OnPropertyChanged(nameof(OwnerFilters));
        OnPropertyChanged(nameof(CaseNumberFilters));
        OnPropertyChanged(nameof(ServiceTypeFilters));

        TotalCount = _all.Count;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = FilterHelper.ApplyFilters(_all,
            (f => f.PayStatus, FilterStatus),
            (f => f.Discipline, FilterDiscipline),
            (f => f.Stage, FilterStage),
            (f => f.Owner, FilterOwner),
            (f => f.CaseNumber, FilterCaseNumber),
            (f => f.ServiceType, FilterServiceType));

        // Amount range
        if (!string.IsNullOrWhiteSpace(FilterAmountFrom) && long.TryParse(NormalizeDigits(FilterAmountFrom).Replace(",", ""), out var fromAmt))
            filtered = filtered.Where(f => ParseAmount(f.Amount) >= fromAmt);
        if (!string.IsNullOrWhiteSpace(FilterAmountTo) && long.TryParse(NormalizeDigits(FilterAmountTo).Replace(",", ""), out var toAmt))
            filtered = filtered.Where(f => ParseAmount(f.Amount) <= toAmt);

        // General search
        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(f =>
                FilterHelper.Matches(f.Owner, SearchText) ||
                FilterHelper.Matches(f.CaseNumber, SearchText) ||
                FilterHelper.Matches(f.Discipline, SearchText) ||
                FilterHelper.Matches(f.ServiceType, SearchText));

        Fees.Clear();
        foreach (var item in filtered) Fees.Add(item);
        FilteredCount = Fees.Count;
        OnPropertyChanged(nameof(CountDisplay));
        OnPropertyChanged(nameof(HasActiveFilters));
        CalculateSummary(filtered.ToList());
        StatusMessage = CountDisplay;
    }

    private void SelectAll() { foreach (var item in Fees) item.IsSelected = true; UpdateSelectedSummary(); }
    private void DeselectAll() { foreach (var item in Fees) item.IsSelected = false; UpdateSelectedSummary(); }

    private void ClearFilters()
    {
        SearchText = ""; FilterStatus = "همه"; FilterDiscipline = "همه"; FilterStage = "همه";
        FilterOwner = "همه"; FilterCaseNumber = "همه"; FilterServiceType = "همه";
        FilterAmountFrom = ""; FilterAmountTo = "";
    }

    private void FilterByValue(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return;
        var parts = parameter.Split(':', 2);
        if (parts.Length != 2) return;
        switch (parts[0])
        {
            case "Discipline": FilterDiscipline = parts[1]; break;
            case "Owner": FilterOwner = parts[1]; break;
            case "CaseNumber": FilterCaseNumber = parts[1]; break;
            case "Stage": FilterStage = parts[1]; break;
            case "Status": FilterStatus = parts[1]; break;
            case "ServiceType": FilterServiceType = parts[1]; break;
        }
    }

    public void RemoveFilter(string column)
    {
        switch (column)
        {
            case "Discipline": FilterDiscipline = "همه"; break;
            case "Owner": FilterOwner = "همه"; break;
            case "CaseNumber": FilterCaseNumber = "همه"; break;
            case "Stage": FilterStage = "همه"; break;
            case "Status": FilterStatus = "همه"; break;
            case "ServiceType": FilterServiceType = "همه"; break;
        }
    }

    public void UpdateSelectedSummary()
    {
        var selected = Fees.Where(f => f.IsSelected).ToList();
        SelectedCount = selected.Count;
        long total = 0;
        foreach (var item in selected) total += ParseAmount(item.Amount);
        SelectedAmount = FormatAmount(total);
    }

    private void CalculateSummary(List<FeeItem> items)
    {
        long total = 0, paid = 0, confirmed = 0, pending = 0;
        int totalN = 0, paidN = 0, confirmedN = 0, pendingN = 0;
        foreach (var item in items)
        {
            var amount = ParseAmount(item.Amount);
            total += amount; totalN++;
            var ps = item.PayStatus?.Trim() ?? "";
            // پرداخت شده: دقیقاً پرداخت شده
            if (ps == "پرداخت شده") { paid += amount; paidN++; }
            // تایید شده: PayStatus تایید شده
            if (ps == "تایید شده") { confirmed += amount; confirmedN++; }
            // در انتظار: پرداخت نشده + نوع خدمت نظارت یا حسن انجام کار
            if (ps == "پرداخت نشده" && (item.ServiceType == "نظارت" || item.ServiceType == "حسن انجام کار"))
            { pending += amount; pendingN++; }
        }
        TotalAmount = FormatAmount(total); PaidAmount = FormatAmount(paid);
        ConfirmedAmount = FormatAmount(confirmed); PendingAmount = FormatAmount(pending);
        TotalCount = totalN; PaidCount = paidN; ConfirmedCount = confirmedN; PendingCount = pendingN;
    }

    private static long ParseAmount(string amount)
    {
        if (string.IsNullOrWhiteSpace(amount)) return 0;
        var cleaned = amount.Replace("ریال", "").Replace(",", "").Replace(" ", "").Trim();
        cleaned = NormalizeDigits(cleaned);
        return long.TryParse(cleaned, out var result) ? result : 0;
    }

    private static string NormalizeDigits(string input) =>
        input.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4")
             .Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9")
             .Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4")
             .Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9");

    private static string FormatAmount(long amount) => amount.ToString("N0") + " ریال";

    // ═════ دریافتی ═════
    private long CalcFeeSum(FeeReceipt rc)
    {
        var map = _db.GetFeeSumByStartDateForReceipt(rc.IsAdvance);
        long sum = 0;
        foreach (var d in rc.RelatedEndDates) if (map.TryGetValue(d.Trim(), out var v)) sum += v;
        return sum;
    }
    private void RebuildEndDates()
    {
        _distinctStartPaid = _all.Where(x => x.PayStatus == "پرداخت شده" && !string.IsNullOrWhiteSpace(x.StartDate))
                                 .Select(x => x.StartDate.Trim()).Distinct().OrderBy(s => s).ToList()!;
        _distinctStartConfirmed = _all.Where(x => x.PayStatus == "تایید شده" && !string.IsNullOrWhiteSpace(x.StartDate))
                                      .Select(x => x.StartDate.Trim()).Distinct().OrderBy(s => s).ToList()!;
        _distinctStartDates = _distinctStartPaid;
    }
    private void LoadReceipts()
    {
        var list = _db.GetFeeReceipts();
        var paidMap = _db.GetFeeSumByStartDatePaid();
        var confMap = _db.GetFeeSumByStartDateConfirmed();
        _allReceiptsVm = list.Select(m =>
        {
            var map = m.IsAdvance ? confMap : paidMap;
            return new FeeReceiptRow(m, map);
        }).ToList();
        foreach (var rr in _allReceiptsVm) rr.OnSelectedChanged = UpdateReceiptSelectedSummary;
        ApplyReceiptFilter();
        UpdateReceiptSelectedSummary();
        ReceiptStatus = $"{_allReceiptsVm.Count} دریافتی";
    }
    private void ApplyReceiptFilter()
    {
        IEnumerable<FeeReceiptRow> q = _allReceiptsVm;
        if (!string.IsNullOrWhiteSpace(ReceiptSearch))
            q = q.Where(r => r.ReceiptDate.Contains(ReceiptSearch) || r.RelatedDisplay.Contains(ReceiptSearch) || r.Changes.Contains(ReceiptSearch));
        Receipts.Clear();
        foreach (var r in q) Receipts.Add(r);
        CalcReceiptSummary();
    }
    private void CalcReceiptSummary()
    {
        long totAmt = 0, totFee = 0;
        foreach (var r in Receipts) { totAmt += r.Amount; totFee += r.FeeSum; }
        ReceiptTotalAmount = totAmt.ToString("N0") + " ریال";
        ReceiptTotalFeeSum = totFee.ToString("N0") + " ریال";
        ReceiptTotalDiff = (totAmt - totFee).ToString("N0") + " ریال";
        UpdateReceiptSelectedSummary();
    }
    public void UpdateReceiptSelectedSummary()
    {
        var sel = Receipts.Where(r => r.IsSelected).ToList();
        ReceiptSelectedCount = sel.Count;
        long amt = 0, fee = 0;
        foreach (var r in sel) { amt += r.Amount; fee += r.FeeSum; }
        ReceiptSelectedAmount = amt.ToString("N0") + " ریال";
        ReceiptSelectedFeeSum = fee.ToString("N0") + " ریال";
        ReceiptSelectedDiff = (amt - fee).ToString("N0") + " ریال";
    }
    private void AddReceipt()
    {
        RebuildEndDates();
        var dlg = new FeeReceiptDialog(null, _distinctStartPaid, _distinctStartConfirmed) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true) return;
        var rc = dlg.ResultReceipt;
        rc.FeeSum = CalcFeeSum(rc);
        rc.IsRecentlyUpdated = false; rc.Changes = "";
        _db.InsertFeeReceipt(rc);
        LoadReceipts(); ReceiptStatus = rc.IsAdvance ? "دریافتی علی‌الحساب اضافه شد ✓" : "دریافتی جدید اضافه شد ✓";
    }
    private void EditReceipt(FeeReceiptRow? row)
    {
        if (row == null) return;
        RebuildEndDates();
        var src = new FeeReceipt { Id = row.Id, ReceiptDate = row.ReceiptDate, Amount = row.Amount, IsAdvance = row.IsAdvance, RelatedEndDatesRaw = row.RelatedRaw, FeeSum = row.FeeSum, Changes = row.Changes, IsRecentlyUpdated = row.IsRecentlyUpdated };
        var dlg = new FeeReceiptDialog(src, _distinctStartPaid, _distinctStartConfirmed) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true) return;
        var rc = dlg.ResultReceipt;
        rc.FeeSum = CalcFeeSum(rc); rc.Changes = ""; rc.IsRecentlyUpdated = false;
        _db.UpdateFeeReceipt(rc);
        LoadReceipts(); ReceiptStatus = "ویرایش ذخیره شد ✓";
    }
    private void DeleteReceipt(FeeReceiptRow? row)
    {
        if (row == null) return;
        if (MessageBox.Show($"حذف دریافتی {row.ReceiptDate} مبلغ {row.AmountDisplay} ؟", "تایید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _db.DeleteFeeReceipt(row.Id);
        LoadReceipts(); ReceiptStatus = "حذف شد ✓";
    }
    private void ExportReceipts()
    {
        if (_allReceiptsVm.Count == 0) { ReceiptStatus = "دریافتی‌ای برای خروجی وجود ندارد"; return; }
        var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = $"دریافتی_{DateTime.Now:yyyy-MM-dd}.xlsx" };
        if (dlg.ShowDialog() != true) return;
        var paidMap = _db.GetFeeSumByStartDatePaid();
        var confMap = _db.GetFeeSumByStartDateConfirmed();
        var list = _db.GetFeeReceipts();
        FeeReceiptExcelHelper.Export(list, paidMap, confMap, dlg.FileName);
        ReceiptStatus = $"خروجی ذخیره شد: {dlg.FileName}";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); } catch { }
    }
    private void ImportReceipts()
    {
        var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var imported = FeeReceiptExcelHelper.Import(dlg.FileName);
            if (imported.Count == 0) { ReceiptStatus = "فایل خالی است"; return; }
            var paidMap = _db.GetFeeSumByStartDatePaid();
            var confMap = _db.GetFeeSumByStartDateConfirmed();
            foreach (var rc in imported)
            {
                var map = rc.IsAdvance ? confMap : paidMap;
                rc.FeeSum = 0;
                foreach (var d in rc.RelatedEndDates) if (map.TryGetValue(d.Trim(), out var v)) rc.FeeSum += v;
                rc.IsRecentlyUpdated = true; rc.Changes = "وارد شده از اکسل";
                _db.InsertFeeReceipt(rc);
            }
            LoadReceipts(); ReceiptStatus = $"{imported.Count} ردیف از اکسل وارد شد ✓ (سبز)";
        }
        catch (Exception ex) { ReceiptStatus = $"خطا در ورود اکسل: {ex.Message}"; }
    }
    private void UpdateReceipts()
    {
        Load();
        var paidMap = _db.GetFeeSumByStartDatePaid();
        var confMap = _db.GetFeeSumByStartDateConfirmed();
        var all = _db.GetFeeReceipts();
        int changed = 0;
        foreach (var rc in all)
        {
            long oldSum = rc.FeeSum;
            var map = rc.IsAdvance ? confMap : paidMap;
            long newSum = 0;
            foreach (var d in rc.RelatedEndDates) if (map.TryGetValue(d.Trim(), out var v)) newSum += v;
            if (oldSum != newSum)
            {
                var diff = newSum - oldSum;
                string diffStr = diff > 0 ? $"+{diff:N0}" : $"{diff:N0}";
                rc.Changes = $"جمع {oldSum:N0} ← {newSum:N0} ({diffStr}) ریال";
                rc.FeeSum = newSum; rc.IsRecentlyUpdated = true;
                _db.UpdateFeeReceipt(rc); changed++;
            }
            else if (rc.IsRecentlyUpdated && string.IsNullOrWhiteSpace(rc.Changes))
            {
                rc.IsRecentlyUpdated = false; _db.UpdateFeeReceipt(rc);
            }
        }
        LoadReceipts();
        ReceiptStatus = changed > 0 ? $"بروزرسانی: {changed} ردیف تغییر کرد و سبز شد ✓" : "بروزرسانی: بدون تغییر ✓";
    }
}

public sealed class FeeItem : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public Action? OnSelectedChanged;
    public string CaseNumber { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string ServiceType { get; set; } = "";
    public string AmountType { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Amount { get; set; } = "";
    /// <summary>مبلغ با جداکننده هزارگان (سه‌رقمی با ,) برای نمایش — مثل 23,158,293 ریال</summary>
    public string AmountDisplay => FormatAmountDisplay(Amount);
    /// <summary>مقدار عددی برای مرتب‌سازی ستون مبلغ</summary>
    public long AmountValue => ParseAmountForDisplay(Amount);
    public string PayStatus { get; set; } = "";
    public string ConfirmStatus { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Description { get; set; } = "";
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected))); OnSelectedChanged?.Invoke(); } }

    private static string FormatAmountDisplay(string amount)
    {
        if (string.IsNullOrWhiteSpace(amount)) return amount ?? "";
        var trimmed = amount.Trim();
        bool hasRial = trimmed.Contains("ریال");
        // حذف ریال، کاماهای قبلی (لاتین/فارسی)، فاصله
        var num = trimmed.Replace("ریال", "").Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", "").Trim();
        num = num.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4")
                 .Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9")
                 .Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4")
                 .Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9");
        if (long.TryParse(num, out var v))
            return v.ToString("N0") + (hasRial ? " ریال" : "");
        return amount;
    }

    private static long ParseAmountForDisplay(string amount)
    {
        if (string.IsNullOrWhiteSpace(amount)) return 0;
        var cleaned = amount.Replace("ریال", "").Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", "").Trim();
        cleaned = cleaned.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4")
                         .Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9")
                         .Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4")
                         .Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9");
        if (long.TryParse(cleaned, out var r)) return r;
        return 0;
    }
}

public sealed class FeeReceiptRow : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void On(string? n = null) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
    public Action? OnSelectedChanged;
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; On(nameof(IsSelected)); OnSelectedChanged?.Invoke(); } }
    public long Id { get; }
    private bool _isAdvance;
    public bool IsAdvance { get => _isAdvance; set { _isAdvance = value; On(); On(nameof(TypeDisplay)); } }
    public string TypeDisplay => _isAdvance ? "علی‌الحساب" : "عادی";
    private string _receiptDate;
    public string ReceiptDate { get => _receiptDate; set { _receiptDate = value; On(); } }
    private long _amount;
    public long Amount { get => _amount; set { _amount = value; On(); On(nameof(AmountDisplay)); On(nameof(Difference)); On(nameof(DifferenceDisplay)); On(nameof(IsNegative)); } }
    public string AmountDisplay => _amount.ToString("N0") + " ریال";
    private string _relatedRaw;
    public string RelatedRaw { get => _relatedRaw; set { _relatedRaw = value; On(); On(nameof(RelatedDisplay)); } }
    public string RelatedDisplay => string.IsNullOrWhiteSpace(_relatedRaw) ? "—" : string.Join(" ؛ ", _relatedRaw.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
    public List<string> RelatedDates => string.IsNullOrWhiteSpace(_relatedRaw) ? new() : _relatedRaw.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
    private long _feeSum;
    public long FeeSum { get => _feeSum; set { _feeSum = value; On(); On(nameof(FeeSumDisplay)); On(nameof(Difference)); On(nameof(DifferenceDisplay)); On(nameof(IsNegative)); } }
    public string FeeSumDisplay => _feeSum.ToString("N0") + " ریال";
    public long Difference => _amount - _feeSum;
    public string DifferenceDisplay => Difference.ToString("N0") + " ریال";
    public bool IsNegative => Difference < 0;
    private string _changes = "";
    public string Changes { get => _changes; set { _changes = value; On(); } }
    private bool _isRecentlyUpdated;
    public bool IsRecentlyUpdated { get => _isRecentlyUpdated; set { _isRecentlyUpdated = value; On(); } }
    public FeeReceiptRow(NezamMonitor.Core.Models.FeeReceipt m, Dictionary<string, long> map)
    {
        Id = m.Id;
        _isAdvance = m.IsAdvance;
        _receiptDate = m.ReceiptDate;
        _amount = m.Amount;
        _relatedRaw = m.RelatedEndDatesRaw;
        long sum = 0;
        foreach (var d in RelatedDates) if (map.TryGetValue(d.Trim(), out var v)) sum += v;
        _feeSum = sum;
        _changes = m.Changes ?? "";
        _isRecentlyUpdated = m.IsRecentlyUpdated;
    }
}
