using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using NezamMonitor.Core;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;
using NezamMonitor.Core.Reports;

namespace NezamMonitor.App.ViewModels;

public sealed class GeneratorViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _templatePath = "";
    private string _outputPath = "";
    private string _statusMessage = "مرحله و تمپلیت را انتخاب کنید";
    private int _progress;
    private string _progressText = "";
    private int _selectedStage = 1;
    private string _selectedTemplateName = "";
    private string _selectedTemplatePath = "";
    private string _reportDate = "";
    private bool _isHistoryExpanded = false;
    private string _historySearchText = "";
    private int _historyFilterStage = 0;
    private int _historyFilterFormat = 0;
    private string _previewText = "";
    private bool _useLtr = false;
    private int _activeTab = 0;

    public string TemplatePath { get => _templatePath; set => SetProperty(ref _templatePath, value); }
    public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public int Progress { get => _progress; set => SetProperty(ref _progress, value); }
    public string ProgressText { get => _progressText; set => SetProperty(ref _progressText, value); }

    public int SelectedStage
    {
        get => _selectedStage;
        set
        {
            if (SetProperty(ref _selectedStage, value))
                AutoSelectTemplate();
        }
    }

    public string SelectedTemplateName
    {
        get => _selectedTemplateName;
        set => SetProperty(ref _selectedTemplateName, value);
    }

    public string SelectedTemplatePath
    {
        get => _selectedTemplatePath;
        set => SetProperty(ref _selectedTemplatePath, value);
    }

    public string ReportDate
    {
        get => _reportDate;
        set => SetProperty(ref _reportDate, value);
    }

    public bool IsHistoryExpanded
    {
        get => _isHistoryExpanded;
        set => SetProperty(ref _isHistoryExpanded, value);
    }

    public string HistorySearchText
    {
        get => _historySearchText;
        set
        {
            if (SetProperty(ref _historySearchText, value))
                LoadHistory();
        }
    }

    public int HistoryFilterStage
    {
        get => _historyFilterStage;
        set
        {
            if (SetProperty(ref _historyFilterStage, value))
                LoadHistory();
        }
    }

    public int HistoryFilterFormat
    {
        get => _historyFilterFormat;
        set
        {
            if (SetProperty(ref _historyFilterFormat, value))
                LoadHistory();
        }
    }

    public string PreviewText { get => _previewText; set => SetProperty(ref _previewText, value); }
    public bool UseLtr { get => _useLtr; set => SetProperty(ref _useLtr, value); }
    public int ActiveTab { get => _activeTab; set => SetProperty(ref _activeTab, value); }

    public ObservableCollection<CaseReportItem> ReportStatus { get; } = new();
    public ObservableCollection<TemplateInfo> AvailableTemplates { get; } = new();
    public ObservableCollection<string> TemplateList
    {
        get
        {
            var list = new ObservableCollection<string>(AvailableTemplates.Select(t => t.FileName));
            return list;
        }
    }

    private int _filterStage = 0;
    private int _filterStatus = 0;
    private List<CaseReportItem> _allReportItems = new();

    // کش مقایسه اسکن‌ها از دیتابیس
    private Dictionary<string, bool> _previousScanState = new();
    private bool _hasLoadedFromDb = false;

    // زمان‌بندی
    private int _intervalGroupA = 4; // ماه - گروه الف
    private int _intervalGroupB = 3; // ماه - گروه ب

    public int IntervalGroupA
    {
        get => _intervalGroupA;
        set
        {
            if (SetProperty(ref _intervalGroupA, value))
            {
                _db.SaveSetting("report_interval_group_a", value.ToString());
                ApplyReportFilters();
            }
        }
    }

    public int IntervalGroupB
    {
        get => _intervalGroupB;
        set
        {
            if (SetProperty(ref _intervalGroupB, value))
            {
                _db.SaveSetting("report_interval_group_b", value.ToString());
                ApplyReportFilters();
            }
        }
    }

    // لیست ماه‌ها برای ComboBox
    public List<int> MonthOptions { get; } = Enumerable.Range(1, 12).ToList();

    public int FilterStage
    {
        get => _filterStage;
        set
        {
            if (SetProperty(ref _filterStage, value))
                ApplyReportFilters();
        }
    }

    public int FilterStatus
    {
        get => _filterStatus;
        set
        {
            if (SetProperty(ref _filterStatus, value))
                ApplyReportFilters();
        }
    }

    public string CountDisplay => $"تعداد: {ReportStatus.Count(n => n.IsSelected)} از {ReportStatus.Count}";

    public ICommand BrowseTemplateCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand GenerateCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand ToggleHistoryCommand { get; }
    public ICommand RefreshHistoryCommand { get; }
    public ICommand OpenReportCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand RegenerateCommand { get; }
    public ICommand DeleteReportCommand { get; }
    public ICommand SwitchToGenerateCommand { get; }
    public ICommand SwitchToHistoryCommand { get; }
    public ICommand DeleteLogCommand { get; }
    public ICommand SyncOutputCommand { get; }

    public bool HasActiveFilters => !string.IsNullOrEmpty(HistorySearchText) || HistoryFilterStage > 0 || HistoryFilterFormat > 0;

    public GeneratorViewModel(NezamDatabase db)
    {
        _db = db;
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        _templatePath = Path.Combine(exeDir, "templates");
        _outputPath = Path.Combine(exeDir, "outputs");
        _reportDate = PersianDateHelper.ToPersianDateDigits(DateTime.Now);

        // بارگذاری تنظیمات زمان‌بندی
        var savedIntervalA = _db.GetSetting("report_interval_group_a");
        var savedIntervalB = _db.GetSetting("report_interval_group_b");
        if (int.TryParse(savedIntervalA, out int a) && a >= 1 && a <= 12) _intervalGroupA = a;
        if (int.TryParse(savedIntervalB, out int b) && b >= 1 && b <= 12) _intervalGroupB = b;

        BrowseTemplateCommand = new RelayCommand(BrowseTemplate);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        ScanCommand = new RelayCommand(Scan);
        SelectAllCommand = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);
        GenerateCommand = new RelayCommand(Generate);
        PreviewCommand = new RelayCommand(Preview);
        ToggleHistoryCommand = new RelayCommand(() => IsHistoryExpanded = !IsHistoryExpanded);
        RefreshHistoryCommand = new RelayCommand(LoadHistory);
        OpenReportCommand = new RelayCommand<ReportHistoryItem>(OpenReport);
        OpenFolderCommand = new RelayCommand<ReportHistoryItem>(OpenFolder);
        RegenerateCommand = new RelayCommand<ReportHistoryItem>(RegenerateWithConfirmation);
        DeleteReportCommand = new RelayCommand<ReportHistoryItem>(DeleteReportWithConfirmation);
        SwitchToGenerateCommand = new RelayCommand(() => ActiveTab = 0);
        SwitchToHistoryCommand = new RelayCommand(() => { ActiveTab = 1; LoadHistory(); });
        DeleteLogCommand = new RelayCommand<ReportHistoryItem>(DeleteLog);
        SyncOutputCommand = new RelayCommand(SyncOutputFolder);

        LoadTemplates();
        LoadHistory();
        LoadPreviousScanResults();
    }

    private void LoadTemplates()
    {
        AvailableTemplates.Clear();
        var generator = new StageReportGenerator(_templatePath);
        foreach (var t in generator.ListTemplates())
            AvailableTemplates.Add(t);
        AutoSelectTemplate();
    }

    private void AutoSelectTemplate()
    {
        var match = AvailableTemplates.FirstOrDefault(t => t.Stage == SelectedStage);
        if (match != null)
        {
            SelectedTemplateName = match.FileName;
            SelectedTemplatePath = match.Path;
        }
        else
        {
            SelectedTemplateName = "";
            SelectedTemplatePath = "";
        }
    }

    private void BrowseTemplate()
    {
        var dialog = new OpenFileDialog
        {
            Title = "انتخاب تمپلیت",
            Filter = "فایل Word|*.docx",
            InitialDirectory = _templatePath
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedTemplatePath = dialog.FileName;
        }
    }

    private void BrowseOutput()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "انتخاب پوشه خروجی",
            InitialDirectory = _outputPath
        };
        if (dialog.ShowDialog() == true)
        {
            _outputPath = dialog.FolderName;
        }
    }

    /// <summary>
    /// بارگذاری نتایج اسکن قبلی از دیتابیس (فقط برای نمایش اولیه)
    /// </summary>
    private void LoadPreviousScanResults()
    {
        try
        {
            var saved = _db.LoadLastScanResults();
            if (saved.Count == 0) return; // هنوز اسکنی انجام نشده

            // --- Build row-number map ONCE (O(N), not O(N²)) ---
            var cases = _db.LoadCases(_db.GetActiveSnapshotId());
            var rowMap = new Dictionary<string, int>(cases.Count);
            for (int i = 0; i < cases.Count; i++)
                rowMap[cases[i].CaseNumber] = i + 1; // 1-based, matches CasesView order

            _allReportItems.Clear();
            ReportStatus.Clear();

            foreach (var s in saved)
            {
                var shamsi = !string.IsNullOrWhiteSpace(s.GeneratedAtShamsi) ? s.GeneratedAtShamsi : PersianDateHelper.GregoryStringToShamsi(s.GeneratedAt);
                var item = new CaseReportItem
                {
                    CaseNumber = s.CaseNumber,
                    RowNumber = rowMap.TryGetValue(s.CaseNumber, out var rn) ? rn : 0,
                    Stage = s.Stage,
                    Owner = s.Owner,
                    FullAddress = s.FullAddress,
                    PermitNumber = s.PermitNumber,
                    Companion = s.Companion,
                    StageName = s.StageName,
                    Status = s.Status,
                    GeneratedAt = s.GeneratedAt,
                    GeneratedAtShamsi = shamsi,
                    PermitDate = s.PermitDate,
                    Deadline = s.Deadline,
                    DaysRemaining = s.DaysRemaining,
                    DaysRemainingSort = CalculateDaysRemainingSort(s.Deadline),
                    DeadlineStatus = s.DeadlineStatus,
                    BuildingGroup = s.BuildingGroup,
                    IsChanged = false, // در لود اولیه تغییری نشون داده نمیشه
                };
                _allReportItems.Add(item);
            }
            ApplyReportFilters();
            StatusMessage = $"نتایج اسکن قبلی بارگذاری شد ({_allReportItems.Count} مورد). اسکن مجدد برای بروزرسانی.";
        }
        catch { /* silently ignore - scan will show fresh results */ }
    }

    /// <summary>
    /// اسکن پرونده‌ها و نمایش وضعیت واقعی گزارش‌ها
    /// </summary>
    private void Scan()
    {
        try
        {
            _allReportItems.Clear();
            ReportStatus.Clear();
            Directory.CreateDirectory(_outputPath);
            var cases = _db.LoadCases(_db.GetActiveSnapshotId());
            var stages = new[] { 1, 2, 3 };

            // ─── بارگذاری کش از DB (فقط یک بار) ───
            if (!_hasLoadedFromDb)
            {
                _previousScanState = _db.LoadScanCache();
                _hasLoadedFromDb = true;
            }

            // دریافت تمام گزارش‌های تولید شده از دیتابیس
            var existingReports = _db.GetGeneratedReports();
            var existingReportSet = new HashSet<string>(
                existingReports.Select(r => $"{r.CaseNumber}|{r.Stage}"));
            // دیکشنری تاریخ تولید
            var generatedDates = new Dictionary<string, string>();
            foreach (var r in existingReports)
            {
                var key = $"{r.CaseNumber}|{r.Stage}";
                if (!generatedDates.ContainsKey(key) && !string.IsNullOrEmpty(r.CreatedAt))
                    generatedDates[key] = r.CreatedAt;
            }

            // همچنین اسکن فایل‌های واقعی در پوشه outputs
            // ساختار: outputs/01 - نام مالک - شماره-سال/گزارش مرحله X - تاریخ.docx
            var actualFiles = new HashSet<string>(); // "CaseNumber|Stage"
            if (Directory.Exists(_outputPath))
            {
                foreach (var dir in Directory.GetDirectories(_outputPath))
                {
                    var dirName = new DirectoryInfo(dir).Name;
                    // استخراج شماره پرونده از نام پوشه: "01 - نام مالک - 514-1404"
                    var dashParts = dirName.Split(" - ", 3);
                    var casePart = dashParts.Length >= 3 ? dashParts[2].Trim() : "";
                    if (string.IsNullOrEmpty(casePart)) continue;

                    // تبدیل "514-1404" به "۱۴۰۴/۵۱۴" (فرمت دیتابیس)
                    var caseNumParts = casePart.Split("-");
                    if (caseNumParts.Length == 2)
                    {
                        var dbCaseNumber = $"{caseNumParts[1]}/{caseNumParts[0]}";
                        // هم فرمت لاتین هم فارسی رو چک کن
                        var persianCase = DigitNormalizer.ToPersianDigits(dbCaseNumber);

                        foreach (var file in Directory.GetFiles(dir, "*.docx"))
                        {
                            var fileName = Path.GetFileNameWithoutExtension(file);
                            foreach (var stage in stages)
                            {
                                var stageWord = StageReportRules.StageName(stage);
                                if (fileName.Contains($"مرحله {stageWord}"))
                                {
                                    actualFiles.Add($"{dbCaseNumber}|{stage}");
                                    actualFiles.Add($"{persianCase}|{stage}");
                                }
                            }
                        }
                    }
                }
            }

            // ─── کش جدید برای ذخیره وضعیت فعلی ───
            var currentScanState = new Dictionary<string, bool>();
            bool isFirstScan = _previousScanState.Count == 0;

            int current = 0;
            foreach (var c in cases)
            {
                current++;
                Progress = (int)((double)current / cases.Count * 100);
                ProgressText = $"{current}/{cases.Count}";

                // تعیین تعداد مراحل بر اساس حق‌الزحمه
                var feeStages = c.Fees.Where(f => f.Stage != "0" && f.Stage != "۰")
                                       .Select(f => f.Stage)
                                       .Distinct()
                                       .Count();
                var maxStage = feeStages > 0 ? feeStages : 3; // پیش‌فرض ۳ مرحله

                // گروه ساختمانی
                var buildingGroup = c.Specification?.BuildingGroup ?? "";
                var isGroupB = buildingGroup.Contains("ب");

                // تاریخ صدور پروانه
                var permitDate = c.Specification?.PermitDate ?? "";
                var interval = isGroupB ? IntervalGroupB : IntervalGroupA;

                foreach (var stage in stages)
                {
                    // فقط مراحلی که نیاز به گزارش دارن
                    if (stage > maxStage) continue;

                    var reportKey = $"{c.CaseNumber}|{stage}";
                    var hasReport = actualFiles.Contains(reportKey);

                    // ─── مقایسه با اسکن قبلی ───
                    bool isChanged = false;
                    if (!isFirstScan)
                    {
                        if (_previousScanState.TryGetValue(reportKey, out bool prevHasReport))
                        {
                            isChanged = hasReport != prevHasReport; // تغییر وضعیت
                        }
                        else
                        {
                            isChanged = true; // آیتم جدید (در اسکن قبلی نبود)
                        }
                    }

                    // ذخیره وضعیت فعلی در کش جدید
                    currentScanState[reportKey] = hasReport;

                    // محاسبه مهلت
                    var deadline = CalculateDeadline(permitDate, stage, interval);
                    var daysRemaining = CalculateDaysRemaining(deadline);
                    var daysRemainingSort = CalculateDaysRemainingSort(deadline);
                    var deadlineStatus = GetDeadlineStatus(hasReport, daysRemaining);

                    // تاریخ تولید
                    var generatedAt = "";
                    if (hasReport && generatedDates.TryGetValue(reportKey, out var gDate))
                        generatedAt = gDate;

                    var item = new CaseReportItem
                    {
                        CaseNumber = c.CaseNumber,
                        PermitNumber = c.Specification?.PermitNumber ?? "",
                        RowNumber = GetRowNumber(c.CaseNumber),
                        Owner = c.Owner,
                        FullAddress = c.Specification?.Address ?? "",
                        Companion = c.OwnerMobile,
                        Stage = stage,
                        StageName = StageReportRules.StageName(stage),
                        Status = hasReport ? "✅ تولید شده" : "⏳ در انتظار",
                        GeneratedAt = generatedAt,
                        GeneratedAtShamsi = PersianDateHelper.GregoryStringToShamsi(generatedAt),
                        PermitDate = NormalizePermitDate(c.Specification?.PermitDate),
                        Deadline = deadline,
                        DaysRemaining = daysRemaining,
                        DaysRemainingSort = daysRemainingSort,
                        DeadlineStatus = deadlineStatus,
                        BuildingGroup = buildingGroup,
                        IsChanged = isChanged,
                    };
                    _allReportItems.Add(item);
                }
            }

            // ─── آپدیت کش (حافظه + DB) ───
            _previousScanState = currentScanState;
            _db.SaveScanCache(currentScanState);

            // ─── ذخیره نتایج در LastScanResults (فقط آخرین اسکن) ───
            var scanResults = _allReportItems.Select(item => new LastScanResultItem
            {
                CaseNumber = item.CaseNumber,
                Stage = item.Stage,
                Owner = item.Owner,
                FullAddress = item.FullAddress,
                PermitNumber = item.PermitNumber,
                Companion = item.Companion,
                StageName = item.StageName,
                Status = item.Status,
                GeneratedAt = item.GeneratedAt,
                GeneratedAtShamsi = item.GeneratedAtShamsi,
                PermitDate = item.PermitDate,
                Deadline = item.Deadline,
                DaysRemaining = item.DaysRemaining,
                DeadlineStatus = item.DeadlineStatus,
                BuildingGroup = item.BuildingGroup,
                HasReport = item.Status.Contains("تولید شده"),
            }).ToList();
            _db.SaveLastScanResults(scanResults);

            Progress = 100;
            ApplyReportFilters();

            if (isFirstScan)
                StatusMessage = $"اسکن انجام شد: {_allReportItems.Count} مورد یافت شد";
            else
            {
                int changedCount = _allReportItems.Count(i => i.IsChanged);
                StatusMessage = changedCount > 0
                    ? $"اسکن انجام شد: {changedCount} تغییر نسبت به اسکن قبلی"
                    : "اسکن انجام شد: بدون تغییر";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
            Progress = 0;
        }
    }

    /// <summary>
    /// اعمال فیلترها روی لیست گزارش‌ها
    /// </summary>
    private void ApplyReportFilters()
    {
        var filtered = _allReportItems.AsEnumerable();

        // فیلتر مرحله: 0=همه, 1=اول, 2=دوم, 3=سوم
        if (FilterStage > 0)
            filtered = filtered.Where(r => r.Stage == FilterStage);

        // فیلتر وضعیت: 0=همه, 1=در انتظار, 2=تولید شده
        if (FilterStatus == 1)
            filtered = filtered.Where(r => r.Status.Contains("در انتظار"));
        else if (FilterStatus == 2)
            filtered = filtered.Where(r => r.Status.Contains("تولید شده"));

        ReportStatus.Clear();
        foreach (var item in filtered)
            ReportStatus.Add(item);

        OnPropertyChanged(nameof(CountDisplay));
    }

    /// <summary>
    /// نرمال‌سازی تاریخ صدور: ارقام فارسی→لاتین، پد ماه/روز، اصلاح ماه 0→1 (داده کثیف مثل 1405/0/02)
    /// </summary>
    private static string NormalizePermitDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var ascii = DigitNormalizer.ToAsciiDigits(input.Trim());
        var parts = ascii.Split('/');
        if (parts.Length != 3) return ascii.Trim();
        if (!int.TryParse(parts[0].Trim(), out int y)) return ascii.Trim();
        if (!int.TryParse(parts[1].Trim(), out int m)) return ascii.Trim();
        if (!int.TryParse(parts[2].Trim(), out int d)) return ascii.Trim();
        if (m < 1) m = 1; if (m > 12) m = 12;
        if (d < 1) d = 1; if (d > 31) d = 31;
        return $"{y}/{m:D2}/{d:D2}";
    }

    private string CalculateDeadline(string permitDate, int stage, int intervalMonths)
    {
        var norm = NormalizePermitDate(permitDate);
        if (string.IsNullOrWhiteSpace(norm)) return "";

        try
        {
            var parts = norm.Split('/');
            if (parts.Length != 3) return "";

            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);

            // تبدیل شمسی به میلادی با PersianCalendar
            var pc = new PersianCalendar();
            var gregorianDate = pc.ToDateTime(year, month, day, 0, 0, 0, 0);

            // اضافه کردن ماه‌ها
            var deadlineGregorian = gregorianDate.AddMonths(intervalMonths * stage);

            // برگشت به شمسی
            int rY = pc.GetYear(deadlineGregorian);
            int rM = pc.GetMonth(deadlineGregorian);
            int rD = pc.GetDayOfMonth(deadlineGregorian);

            return $"{rY}/{rM:D2}/{rD:D2}";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// محاسبه تعداد روزهای مانده تا مهلت (شمسی)
    /// </summary>
    private string CalculateDaysRemaining(string deadline)
    {
        if (string.IsNullOrWhiteSpace(deadline)) return "";

        try
        {
            var parts = deadline.Split('/');
            if (parts.Length != 3) return "";

            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);

            var pc = new PersianCalendar();
            var deadlineGregorian = pc.ToDateTime(year, month, day, 0, 0, 0, 0);
            var today = DateTime.Today;
            var diff = (deadlineGregorian - today).Days;

            if (diff < 0) return $"🔴 {Math.Abs(diff)} روز گذشته";
            if (diff == 0) return "⚠️ امروز";
            if (diff <= 7) return $"⚠️ {diff} روز";
            return $"📅 {diff} روز";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// مقدار عددی مانده برای مرتب‌سازی: منفی=گذشته، ۰=امروز، مثبت=آینده، int.MaxValue=بدون مهلت (آخر جدول)
    /// </summary>
    private int CalculateDaysRemainingSort(string deadline)
    {
        if (string.IsNullOrWhiteSpace(deadline)) return int.MaxValue;
        try
        {
            var parts = deadline.Split('/');
            if (parts.Length != 3) return int.MaxValue;
            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);
            var pc = new PersianCalendar();
            var deadlineGregorian = pc.ToDateTime(year, month, day, 0, 0, 0, 0);
            return (deadlineGregorian - DateTime.Today).Days;
        }
        catch { return int.MaxValue; }
    }

    /// <summary>
    /// تعیین وضعیت مهلت
    /// </summary>
    private string GetDeadlineStatus(bool hasReport, string daysRemaining)
    {
        if (hasReport) return "✅";
        if (string.IsNullOrEmpty(daysRemaining)) return "";
        if (daysRemaining.Contains("گذشته") || daysRemaining.Contains("امروز")) return "🔴";
        if (daysRemaining.Contains("روز") && !daysRemaining.Contains("📅"))
        {
            // استخراج عدد روز
            var numStr = new string(daysRemaining.Where(char.IsDigit).ToArray());
            if (int.TryParse(numStr, out int days) && days <= 7) return "⚠️";
        }
        return "📅";
    }

    private int GetRowNumber(string caseNumber)
    {
        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        var idx = cases.FindIndex(c => c.CaseNumber == caseNumber);
        return idx >= 0 ? idx + 1 : -1; // -1 = پرونده یافت نشد
    }

    private void SelectAll()
    {
        foreach (var item in ReportStatus)
            item.IsSelected = true;
    }

    private void DeselectAll()
    {
        foreach (var item in ReportStatus)
            item.IsSelected = false;
    }

    private void Preview()
    {
        var selected = ReportStatus.Where(r => r.IsSelected && !r.Status.Contains("تولید شده")).ToList();
        if (selected.Count == 0)
        {
            PreviewText = "موردی انتخاب نشده";
            return;
        }

        var ltrSuffix = UseLtr ? " - LTR" : "";
        var lines = new List<string> { $"گزارش‌های آماده تولید ({selected.Count} مورد):", "" };

        foreach (var item in selected)
        {
            var folderName = StageReportRules.GenerateFolderName(item.RowNumber, new Case { CaseNumber = item.CaseNumber, Owner = item.Owner });
            lines.Add($"- {item.Owner} ({item.StageName}), {item.CaseNumber} -> {folderName}{ltrSuffix}/گزارش مرحله {item.Stage}{ltrSuffix}.docx");
        }

        PreviewText = string.Join("\r\n", lines);
    }

    /// <summary>
    /// تولید گزارش‌های انتخاب شده
    /// </summary>
    private void Generate()
    {
        try
        {
            Directory.CreateDirectory(_outputPath);
            var cases = _db.LoadCases(_db.GetActiveSnapshotId());
            var generator = new StageReportGenerator(_templatePath);

            int generated = 0, failed = 0, skipped = 0;
            var selected = ReportStatus.Where(r => r.IsSelected).ToList();
            int total = selected.Count;

            if (total == 0)
            {
                StatusMessage = "هیچ موردی انتخاب نشده";
                return;
            }

            int current = 0;
            foreach (var item in selected)
            {
                current++;
                Progress = (int)((double)current / total * 100);
                ProgressText = $"{current}/{total}";

                var c = cases.FirstOrDefault(x => x.CaseNumber == item.CaseNumber);
                if (c == null) { failed++; item.Status = "❌ پرونده یافت نشد"; continue; }

                var rowNumber = GetRowNumber(c.CaseNumber);
                if (rowNumber <= 0)
                {
                    failed++;
                    item.Status = $"❌ پرونده {c.CaseNumber} در لیست پرونده‌ها یافت نشد (ردیف نامعتبر)";
                    continue;
                }

                var templateForStage = generator.FindTemplateForStage(item.Stage);
                if (templateForStage == null)
                {
                    failed++;
                    item.Status = $"❌ تمپلیت مرحله {item.StageName} یافت نشد";
                    continue;
                }

                var genResult = generator.GenerateReport(c, item.Stage, rowNumber, _outputPath, DateTime.Now, templateForStage, UseLtr);

                if (genResult.Success && genResult.OutputPath != null && File.Exists(genResult.OutputPath) && new FileInfo(genResult.OutputPath).Length > 0)
                {
                    var numFormat = UseLtr ? "LTR" : "Persian";
                    var fileHash = NezamDatabase.ComputeFileHash(genResult.OutputPath);
                    _db.SaveGeneratedReport(c.CaseNumber, item.Stage, Path.GetFileNameWithoutExtension(templateForStage), genResult.OutputPath, c.Owner, numFormat, fileHash);
                    generated++;
                    item.Status = "✅ تولید شده";
                    var now2 = DateTime.Now;
                    item.GeneratedAt = now2.ToString("yyyy/MM/dd HH:mm");
                    item.GeneratedAtShamsi = PersianDateHelper.ToShamsiDateTime(now2);
                    item.IsSelected = false;
                }
                else if (genResult.Success)
                {
                    failed++;
                    item.Status = "❌ فایل ایجاد نشد";
                }
                else
                {
                    failed++;
                    item.Status = $"❌ {genResult.ErrorMessage}";
                }
            }

            StatusMessage = $"تکمیل: {generated} تولید شد | {failed} خطا | {skipped} لغو شد | مسیر: {_outputPath}";
            Progress = 100;
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
        }
    }

    /// <summary>
    /// بارگذاری تاریخچه گزارش‌های تولید شده
    /// </summary>
    public void LoadHistory()
    {
        try
        {
            ReportHistory.Clear();
            var reports = _db.GetGeneratedReports();
            int row = 1;
            foreach (var r in reports)
            {
                if (HistoryFilterStage > 0 && r.Stage != HistoryFilterStage) continue;
                if (HistoryFilterFormat > 0)
                {
                    var fmtFilter = HistoryFilterFormat == 1 ? "Persian" : (HistoryFilterFormat == 2 ? "LTR" : "English");
                    if (r.NumberFormat != fmtFilter) continue;
                }
                if (!string.IsNullOrEmpty(HistorySearchText))
                {
                    var normSearch = FilterHelper.Normalize(HistorySearchText);
                    var normStored = FilterHelper.Normalize($"{r.OwnerName} {r.CaseNumber} {r.NumberFormat}");
                    if (!normStored.Contains(normSearch, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var fileExists = !string.IsNullOrEmpty(r.OutputPath) && File.Exists(r.OutputPath);
                var folderPath = Path.GetDirectoryName(r.OutputPath) ?? "";
                var displayTime = FormatTimestamp(r.CreatedAt);

                string status;
                if (!fileExists)
                    status = "⚠️ فایل موجود نیست";
                else if (!string.IsNullOrEmpty(r.FileHash))
                {
                    var currentHash = NezamDatabase.ComputeFileHash(r.OutputPath);
                    status = currentHash == r.FileHash ? "✅ موجود" : "🔄 فایل تغییر کرده (بازنویسی شده)";
                }
                else
                    status = "✅ موجود";

                ReportHistory.Add(new ReportHistoryItem
                {
                    RowId = row++,
                    CaseNumber = r.CaseNumber,
                    OwnerName = r.OwnerName,
                    Owner = r.OwnerName,
                    Stage = r.Stage,
                    StageName = StageReportRules.StageName(r.Stage),
                    ReportType = StageReportRules.StageName(r.Stage),
                    ReportDate = displayTime,
                    NumberFormat = r.NumberFormat,
                    OutputPath = r.OutputPath,
                    Status = status,
                    FileExists = fileExists,
                    FolderPath = folderPath
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در بارگذاری تاریخچه: {ex.Message}";
        }
    }

    private string FormatTimestamp(string? createdAt)
    {
        if (string.IsNullOrEmpty(createdAt)) return "نامشخص";
        if (createdAt.Contains('-') && createdAt.Length >= 10)
            return createdAt.Split('T')[0].Replace('-', '/');
        return createdAt;
    }

    public ObservableCollection<ReportHistoryItem> ReportHistory { get; } = new();

    /// <summary>
    /// باز کردن فایل گزارش
    /// </summary>
    public void OpenReport(ReportHistoryItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.OutputPath) || !File.Exists(item.OutputPath))
        {
            StatusMessage = "فایل یافت نشد";
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = item.OutputPath,
            UseShellExecute = true
        });
        StatusMessage = $"باز شد: {item.FileName}";
    }

    /// <summary>
    /// باز کردن پوشه گزارش
    /// </summary>
    public void OpenFolder(ReportHistoryItem? item)
    {
        if (item == null)
        {
            StatusMessage = "موردی انتخاب نشده";
            return;
        }

        var folderPath = item.FolderPath;
        if (string.IsNullOrEmpty(folderPath) && !string.IsNullOrEmpty(item.OutputPath))
        {
            folderPath = Path.GetDirectoryName(item.OutputPath);
        }

        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            StatusMessage = $"پوشه یافت نشد: {folderPath ?? "نامشخص"}";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });
        StatusMessage = $"باز شد: {folderPath}";
    }

    /// <summary>
    /// باز کردن پوشه پرونده (از طریق context menu میز کار)
    /// </summary>
    public void OpenFolderForCase(CaseReportItem item)
    {
        if (item == null) return;

        // جستجوی پوشه بر اساس شماره پرونده در outputs/
        if (Directory.Exists(_outputPath))
        {
            // CaseNumber مثل "1404/514" → در نام پوشه "514-1404"
            var caseParts = item.CaseNumber.Split('/');
            var searchPattern = caseParts.Length == 2 ? $"{caseParts[1]}-{caseParts[0]}" : item.CaseNumber;

            foreach (var dir in Directory.GetDirectories(_outputPath))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.Contains(searchPattern))
                {
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                    StatusMessage = $"باز شد: {dir}";
                    return;
                }
            }
        }

        StatusMessage = $"پوشه‌ای برای پرونده {item.CaseNumber} یافت نشد";
    }

    /// <summary>
    /// حذف فایل و لاگ با تایید کاربر
    /// </summary>
    public void DeleteReportWithConfirmation(ReportHistoryItem? item)
    {
        if (item == null) return;
        if (!item.FileExists && string.IsNullOrEmpty(item.OutputPath)) return;

        var confirmMsg = $"آیا از حذف فایل و لاگ این گزارش اطمینان دارید?\n\nمالک: {item.OwnerName}\nمرحله: {item.StageName}\nفایل: {item.FileName}\n\nاین عملیات غیرقابل بازگشت است.";
        var confirm = MessageBox.Show(confirmMsg, "تایید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "لغو حذف";
            return;
        }

        try
        {
            if (item.FileExists && !string.IsNullOrEmpty(item.OutputPath))
                File.Delete(item.OutputPath);

            _db.DeleteGeneratedReportByPath(item.OutputPath);

            if (!string.IsNullOrEmpty(item.FolderPath) && Directory.Exists(item.FolderPath))
            {
                if (!Directory.EnumerateFileSystemEntries(item.FolderPath).Any())
                    Directory.Delete(item.FolderPath);
                else
                    StatusMessage = $"فایل و لاگ حذف شد: {item.FileName} (پوشه خالی نیست)";
            }
            else
            {
                StatusMessage = $"فایل و لاگ حذف شد: {item.FileName}";
            }
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در حذف: {ex.Message}";
        }
    }

    /// <summary>
    /// حذف فقط لاگ (بدون حذف فایل)
    /// </summary>
    public void DeleteLog(ReportHistoryItem? item)
    {
        if (item == null) { StatusMessage = "موردی انتخاب نشده"; return; }
        try
        {
            _db.DeleteGeneratedReportByPath(item.OutputPath);
            StatusMessage = $"لاگ حذف شد: {item.FileName} (فایل روی دیسک حذف نشد)";
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در حذف لاگ: {ex.Message}";
        }
    }

    /// <summary>
    /// حذف لاگ‌های انتخاب‌شده (بدون حذف فایل) — پشتیبانی از انتخاب چند ردیفی
    /// </summary>
    public void DeleteSelectedHistory(List<ReportHistoryItem> items)
    {
        if (items == null || items.Count == 0) { StatusMessage = "موردی انتخاب نشده"; return; }

        var confirmMsg = items.Count == 1
            ? $"آیا از حذف لاگ این گزارش اطمینان دارید?\n\nمالک: {items[0].OwnerName}\nمرحله: {items[0].StageName}\nفایل: {items[0].FileName}"
            : $"آیا از حذف {items.Count} لاگ انتخاب‌شده اطمینان دارید?\n\nفایل‌ها روی دیسک حذف نخواهند شد.";

        var confirm = MessageBox.Show(confirmMsg, "تایید حذف لاگ", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) { StatusMessage = "لغو حذف"; return; }

        int deleted = 0;
        foreach (var item in items)
        {
            try
            {
                _db.DeleteGeneratedReportByPath(item.OutputPath);
                deleted++;
            }
            catch { }
        }
        StatusMessage = $"{deleted} لاگ حذف شد (فایل‌ها روی دیسک حذف نشدند)";
        LoadHistory();
    }

    /// <summary>
    /// حذف فایل و لاگ‌های انتخاب‌شده — پشتیبانی از انتخاب چند ردیفی
    /// </summary>
    public void DeleteSelectedHistoryWithFile(List<ReportHistoryItem> items)
    {
        if (items == null || items.Count == 0) { StatusMessage = "موردی انتخاب نشده"; return; }

        var fileList = string.Join("\n", items.Take(10).Select(i => $"• {i.OwnerName} — {i.StageName}"));
        if (items.Count > 10) fileList += $"\n... و {items.Count - 10} مورد دیگر";

        var confirmMsg = items.Count == 1
            ? $"آیا از حذف فایل و لاگ این گزارش اطمینان دارید?\n\nمالک: {items[0].OwnerName}\nمرحله: {items[0].StageName}\nفایل: {items[0].FileName}\n\nاین عملیات غیرقابل بازگشت است."
            : $"آیا از حذف {items.Count} فایل و لاگ انتخاب‌شده اطمینان دارید?\n\n{fileList}\n\nاین عملیات غیرقابل بازگشت است.";

        var confirm = MessageBox.Show(confirmMsg, "تایید حذف فایل و لاگ", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) { StatusMessage = "لغو حذف"; return; }

        int deleted = 0, errors = 0;
        foreach (var item in items)
        {
            try
            {
                // حذف فایل از دیسک
                if (item.FileExists && !string.IsNullOrEmpty(item.OutputPath) && File.Exists(item.OutputPath))
                    File.Delete(item.OutputPath);

                // حذف لاگ از دیتابیس
                _db.DeleteGeneratedReportByPath(item.OutputPath);
                deleted++;

                // حذف پوشه خالی
                if (!string.IsNullOrEmpty(item.FolderPath) && Directory.Exists(item.FolderPath))
                {
                    if (!Directory.EnumerateFileSystemEntries(item.FolderPath).Any())
                        Directory.Delete(item.FolderPath);
                }
            }
            catch { errors++; }
        }

        StatusMessage = errors == 0
            ? $"{deleted} فایل و لاگ حذف شد"
            : $"{deleted} حذف شد، {errors} خطا";
        LoadHistory();
    }

    /// <summary>
    /// تولید مجدد با تایید کاربر
    /// </summary>
    public void RegenerateWithConfirmation(ReportHistoryItem? item)
    {
        if (item == null) { StatusMessage = "موردی انتخاب نشده"; return; }

        var confirmMsg = $"این گزارش قبلاً تولید شده است.\n\nآیا می‌خواهید آن را مجدداً تولید کنید?\n\nمالک: {item.OwnerName}\nمرحله: {item.StageName}";
        var confirm = MessageBox.Show(confirmMsg, "تولید مجدد", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "لغو تولید مجدد";
            return;
        }

        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        var c = cases.FirstOrDefault(x => x.CaseNumber == item.CaseNumber);
        if (c == null) { StatusMessage = "پرونده یافت نشد"; return; }

        var idx = cases.IndexOf(c);
        var isLtr = item.NumberFormat == "LTR";
        var generator = new StageReportGenerator(Path.GetDirectoryName(SelectedTemplatePath) ?? _templatePath);
        var genResult = generator.GenerateReport(c, item.Stage, idx + 1, _outputPath, DateTime.Now, null, isLtr);

        if (genResult.Success && genResult.OutputPath != null && File.Exists(genResult.OutputPath))
        {
            _db.DeleteGeneratedReport(c.CaseNumber, item.Stage);
            var numFormat = isLtr ? "LTR" : "Persian";
            var fileHash = NezamDatabase.ComputeFileHash(genResult.OutputPath);
            _db.SaveGeneratedReport(c.CaseNumber, item.Stage, SelectedTemplateName, genResult.OutputPath, c.Owner, numFormat, fileHash);
            StatusMessage = $"تولید مجدد: {item.CaseNumber} مرحله {item.StageName} ✅";
            LoadHistory();
        }
        else
        {
            StatusMessage = $"خطا: {genResult.ErrorMessage}";
        }
    }

    public void SyncOutputFolder()
    {
        try
        {
            var result = _db.SyncOutputFolder(_outputPath);
            StatusMessage = $"sync: added={result.added}, removed={result.removed}, existing={result.existing}";
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در سینک: {ex.Message}";
        }
    }
}

/// <summary>
/// آیتم وضعیت تولید گزارش (در جدول اسکن)
/// </summary>
public class CaseReportItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _status = "";
    private string _generatedAt = "";
    private bool _isChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string CaseNumber { get; set; } = "";
    public string PermitNumber { get; set; } = "";
    public int RowNumber { get; set; }
    public string Owner { get; set; } = "";
    public string FullAddress { get; set; } = "";
    public string Companion { get; set; } = "";
    public int Stage { get; set; }
    public string StageName { get; set; } = "";

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string GeneratedAt
    {
        get => _generatedAt;
        set { _generatedAt = value; OnPropertyChanged(); }
    }
    public string GeneratedAtShamsi { get; set; } = "";

    public bool IsChanged
    {
        get => _isChanged;
        set { _isChanged = value; OnPropertyChanged(); }
    }

    public string PermitDate { get; set; } = "";
    public string Deadline { get; set; } = "";
    public string DaysRemaining { get; set; } = "";
    /// <summary>مقدار عددی برای مرتب‌سازی ستون «مانده»: منفی=گذشته، ۰=امروز، مثبت=آینده، int.MaxValue=بدون مهلت</summary>
    public int DaysRemainingSort { get; set; } = int.MaxValue;
    public string DeadlineStatus { get; set; } = "";
    public string BuildingGroup { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// آیتم تاریخچه گزارش‌های تولید شده
/// </summary>
public class ReportHistoryItem : INotifyPropertyChanged
{
    public int RowId { get; set; }
    public string CaseNumber { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string Owner { get; set; } = "";
    public int Stage { get; set; }
    public string StageName { get; set; } = "";
    public string ReportType { get; set; } = "";
    public string ReportDate { get; set; } = "";
    public string NumberFormat { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string FileName => Path.GetFileName(OutputPath) ?? "";
    public string Status { get; set; } = "";
    public bool FileExists { get; set; }
    public string FolderPath { get; set; } = "";
    public string FolderName { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
