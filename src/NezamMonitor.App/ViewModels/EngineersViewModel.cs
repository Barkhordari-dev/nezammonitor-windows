using System.Collections.ObjectModel;
using System.Windows.Input;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.App.ViewModels;

public sealed class EngineersViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _searchText = "";
    private string _statusMessage = "";
    private string _filterDiscipline = "همه";
    private string _filterOwner = "همه";
    private string _filterCaseNumber = "همه";
    private string _filterEngineer = "همه";

    public ObservableCollection<EngineerItem> Engineers { get; } = new();
    public string SearchText { get => _searchText; set { SetProperty(ref _searchText, value); ApplyFilters(); } }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string FilterDiscipline { get => _filterDiscipline; set { SetProperty(ref _filterDiscipline, value); ApplyFilters(); } }
    public string FilterOwner { get => _filterOwner; set { SetProperty(ref _filterOwner, value); ApplyFilters(); } }
    public string FilterCaseNumber { get => _filterCaseNumber; set { SetProperty(ref _filterCaseNumber, value); ApplyFilters(); } }
    public string FilterEngineer { get => _filterEngineer; set { SetProperty(ref _filterEngineer, value); ApplyFilters(); } }

    // Dynamic dropdown values from actual data
    public List<string> DisciplineFilters { get; set; } = new() { "همه" };
    public List<string> OwnerFilters { get; set; } = new() { "همه" };
    public List<string> CaseNumberFilters { get; set; } = new() { "همه" };
    public List<string> EngineerFilters { get; set; } = new() { "همه" };

    private int _totalCount;
    private int _filteredCount;
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
    public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }
    public string CountDisplay => FilteredCount == TotalCount
        ? $"{TotalCount} مورد"
        : $"{FilteredCount} از {TotalCount} مورد";

    public bool HasActiveFilters => FilterDiscipline != "همه" || FilterOwner != "همه" ||
                                     FilterCaseNumber != "همه" || FilterEngineer != "همه" ||
                                     !string.IsNullOrWhiteSpace(SearchText);

    public ICommand RefreshCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand FilterByValueCommand { get; }
    public ICommand ImportFromExcelCommand { get; }
    public ICommand ExportVCardCommand { get; }

    private List<EngineerItem> _all = new();

    public EngineersViewModel(NezamDatabase db)
    {
        _db = db;
        RefreshCommand = new RelayCommand(Load);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        FilterByValueCommand = new RelayCommand<string>(FilterByValue);
        ImportFromExcelCommand = new RelayCommand(ImportFromExcel);
        ExportVCardCommand = new RelayCommand(ExportVCard);
        Load();
    }

    private void Load()
    {
        Engineers.Clear();
        var snapshotId = _db.GetActiveSnapshotId();
        if (snapshotId == 0) { StatusMessage = "داده‌ای موجود نیست"; return; }
        var cases = _db.LoadCases(snapshotId);
        _all.Clear();
        foreach (var c in cases)
            foreach (var e in c.Engineers)
                _all.Add(new EngineerItem
                {
                    CaseNumber = c.CaseNumber,
                    Owner = c.Owner,
                    Serial = c.Serial,
                    Discipline = e.Discipline,
                    Name = e.Name,
                    Phone = e.Phone,
                    DesignLevel = e.DesignLevel,
                    SupervisionLevel = e.SupervisionLevel,
                    ExecutionLevel = e.ExecutionLevel
                });

        // Build dynamic filter lists from actual data
        DisciplineFilters = FilterHelper.ExtractDistinctValues(_all, e => e.Discipline);
        OwnerFilters = FilterHelper.ExtractDistinctValues(_all, e => e.Owner);
        CaseNumberFilters = FilterHelper.ExtractDistinctValues(_all, e => e.CaseNumber);
        EngineerFilters = FilterHelper.ExtractDistinctValues(_all, e => e.Name);
        OnPropertyChanged(nameof(DisciplineFilters));
        OnPropertyChanged(nameof(OwnerFilters));
        OnPropertyChanged(nameof(CaseNumberFilters));
        OnPropertyChanged(nameof(EngineerFilters));

        TotalCount = _all.Count;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = FilterHelper.ApplyFilters(_all,
            (e => e.Discipline, FilterDiscipline),
            (e => e.Owner, FilterOwner),
            (e => e.CaseNumber, FilterCaseNumber),
            (e => e.Name, FilterEngineer));

        // General search on top of column filters
        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(e =>
                FilterHelper.Matches(e.Name, SearchText) ||
                FilterHelper.Matches(e.CaseNumber, SearchText) ||
                FilterHelper.Matches(e.Owner, SearchText) ||
                FilterHelper.Matches(e.Discipline, SearchText));

        Engineers.Clear();
        foreach (var item in filtered) Engineers.Add(item);
        FilteredCount = Engineers.Count;
        OnPropertyChanged(nameof(CountDisplay));
        OnPropertyChanged(nameof(HasActiveFilters));
        StatusMessage = CountDisplay;
    }

    private void ClearFilters()
    {
        SearchText = "";
        FilterDiscipline = "همه";
        FilterOwner = "همه";
        FilterCaseNumber = "همه";
        FilterEngineer = "همه";
    }

    private void ImportFromExcel()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "فایل اکسل|*.xlsx;*.xls",
            Title = "انتخاب فایل اطلاعات مهندسین"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            StatusMessage = "در حال خواندن فایل اکسل...";
            
            // خواندن فایل اکسل با ClosedXML
            using var workbook = new ClosedXML.Excel.XLWorkbook(dialog.FileName);
            var worksheet = workbook.Worksheet(1); // شیت اول
            
            // بررسی خالی نبودن شیت
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                StatusMessage = "❌ فایل اکسل خالی است";
                return;
            }
            
            // خواندن همه ردیف‌ها در حافظه
            var records = new List<(string FirstName, string LastName, string FullName, string Phone, string Discipline, string DesignLevel, string SupervisionLevel, string ExecutionLevel)>();
            foreach (var row in usedRange.RowsUsed().Skip(1))
            {
                var firstName = row.Cell(2).GetString().Trim();
                var lastName = row.Cell(3).GetString().Trim();
                var phone = row.Cell(4).GetString().Trim();
                var discipline = row.Cell(5).GetString().Trim();
                var designLevel = row.Cell(6).GetString().Trim();
                var supervisionLevel = row.Cell(7).GetString().Trim();
                var executionLevel = row.Cell(8).GetString().Trim();
                
                var fullName = $"{firstName} {lastName}".Trim();
                if (string.IsNullOrEmpty(fullName)) continue;
                
                records.Add((firstName, lastName, fullName, phone, discipline, designLevel, supervisionLevel, executionLevel));
            }
            
            StatusMessage = $"در حال ذخیره {records.Count} رکورد...";
            
            // پاک کردن و درج دسته‌ای (خیلی سریع)
            _db.ClearEngineerRegistry();
            _db.BulkInsertEngineerRegistry(records);
            
            // تطبیق مهندسین
            int matched = _db.MatchEngineersFromRegistry();
            
            StatusMessage = $"✅ {records.Count} مهندس از اکسل خوانده شد | {matched} مهندس با دیتابیس تطبیق یافت";
            
            // بارگذاری مجدد
            Load();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ خطا: {ex.Message}";
        }
    }

    private void ExportVCard()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "ذخیره فایل مخاطبین مهندسین",
                Filter = "فایل vCard|*.vcf",
                DefaultExt = ".vcf",
                FileName = $"engineers_contacts_{DateTime.Now:yyyyMMdd}.vcf"
            };

            if (dialog.ShowDialog() != true) return;

            StatusMessage = "در حال ایجاد فایل مخاطبین...";

            var exporter = new NezamMonitor.Core.Export.VCardExporter(_db);
            var result = exporter.Export(dialog.FileName);

            if (result.Success)
            {
                StatusMessage = $"✅ {result.EngineerCount} مهندس — فایل ذخیره شد: {result.OutputPath}";
            }
            else
            {
                StatusMessage = "❌ " + string.Join(" | ", result.Errors);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ خطا: {ex.Message}";
        }
    }

    /// <summary>
    /// Filter by a specific cell value. The parameter format is "Column:Value".
    /// </summary>
    private void FilterByValue(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return;
        var parts = parameter.Split(':', 2);
        if (parts.Length != 2) return;
        var column = parts[0];
        var value = parts[1];

        switch (column)
        {
            case "Discipline": FilterDiscipline = value; break;
            case "Owner": FilterOwner = value; break;
            case "CaseNumber": FilterCaseNumber = value; break;
            case "Engineer": FilterEngineer = value; break;
        }
    }

    /// <summary>
    /// Remove filter for a specific column only.
    /// </summary>
    public void RemoveFilter(string column)
    {
        switch (column)
        {
            case "Discipline": FilterDiscipline = "همه"; break;
            case "Owner": FilterOwner = "همه"; break;
            case "CaseNumber": FilterCaseNumber = "همه"; break;
            case "Engineer": FilterEngineer = "همه"; break;
        }
    }
}

public sealed class EngineerItem
{
    public string CaseNumber { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Serial { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string DesignLevel { get; set; } = "";
    public string SupervisionLevel { get; set; } = "";
    public string ExecutionLevel { get; set; } = "";
}
