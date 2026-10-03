using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ClosedXML.Excel;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class CasesView : UserControl
{
    private CasesViewModel _vm;
    private bool _isLoaded = false;
    private bool _suppressColumnSave = false;

    public CasesView()
    {
        InitializeComponent();
        _vm = new CasesViewModel(DatabaseService.Instance);
        DataContext = _vm;

        Loaded += (_, _) =>
        {
            var savedRatio = DatabaseService.Instance.GetSetting("cases_splitter_ratio");
            if (!string.IsNullOrEmpty(savedRatio))
            {
                var parts = savedRatio.Split(':');
                if (parts.Length == 2 && double.TryParse(parts[0], out double t) && double.TryParse(parts[1], out double d) && t > 0 && d > 0)
                {
                    TableRow.Height = new GridLength(t, GridUnitType.Star);
                    DetailRow.Height = new GridLength(d, GridUnitType.Star);
                }
            }
            if (!_isLoaded)
            {
                _isLoaded = true;
                RestoreColumnOrder();
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                var sv = FindChild<ScrollViewer>(CasesGrid);
                if (sv != null) sv.ScrollToEnd();
            });
        };
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CasesViewModel.Cases))
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
                {
                    var sv = FindChild<ScrollViewer>(CasesGrid);
                    if (sv != null) sv.ScrollToEnd();
                });
        };
        CasesGrid.ColumnReordered += (_, _) => { if (_suppressColumnSave) return; SaveColumnOrder(); };
    }

    private void SaveColumnOrder()
    {
        var order = string.Join(",", CasesGrid.Columns.Select(c => c.Header?.ToString() ?? ""));
        DatabaseService.Instance.SaveSetting("cases_column_order", order);
    }

    private void RestoreColumnOrder()
    {
        var savedOrder = DatabaseService.Instance.GetSetting("cases_column_order");
        if (string.IsNullOrEmpty(savedOrder)) return;
        var orderHeaders = savedOrder.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var columns = CasesGrid.Columns.ToList();
        _suppressColumnSave = true;
        try
        {
            int displayIndex = 0;
            foreach (var header in orderHeaders)
            {
                var col = columns.FirstOrDefault(c => c.Header?.ToString() == header);
                if (col != null) { col.DisplayIndex = displayIndex; displayIndex++; }
            }
        }
        finally { _suppressColumnSave = false; }
    }

    private void ResetColumns_Click(object sender, RoutedEventArgs e)
    {
        // چینش پیش‌فرض دقیقاً مطابق ترتیب XAML
        string[] defaultHeaders = new[]
        {
            "☐", "ردیف", "شماره پرونده", "شماره سریال", "نام مالک", "همراه مالک",
            "نام پدر", "کد ملی", "تلفن مالک", "آدرس مالک", "کد پستی", "زادگاه",
            "نوع کاربری", "گروه ساختمانی", "کد نوسازی", "شماره دستور نقشه",
            "نوع دستور نقشه", "تاریخ دستور نقشه", "مساحت زمین", "متراژ پاراف",
            "متراژ کسر ظرفیت", "نوع سازه", "عنوان بلوک", "تعداد بلوک",
            "طبقات", "واحدها", "صادرکننده پروانه", "شماره پروانه",
            "تاریخ صدور پروانه", "تاریخ ترخیص", "محدوده طرح", "آدرس",
            "دفتر", "تاریخ کسر ظرفیت", "مسئولیت"
        };
        _suppressColumnSave = true;
        try
        {
            var columns = CasesGrid.Columns.ToList();
            for (int i = 0; i < defaultHeaders.Length; i++)
            {
                var col = columns.FirstOrDefault(c => c.Header?.ToString() == defaultHeaders[i]);
                if (col != null) col.DisplayIndex = i;
            }
            // ستون‌های ناشناخته (در صورت افزوده شدن در آینده) انتهای جدول
            int next = defaultHeaders.Length;
            foreach (var col in columns)
            {
                var h = col.Header?.ToString() ?? "";
                if (!defaultHeaders.Contains(h))
                    col.DisplayIndex = next++;
            }
        }
        finally { _suppressColumnSave = false; }
        try
        {
            DatabaseService.Instance.SaveSetting("cases_column_order", "");
            _vm.StatusMessage = "چینش ستون‌ها به حالت پیش‌فرض برگشت ✓";
        }
        catch { }
    }

    private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        var tableHeight = TableRow.Height.Value;
        var detailHeight = DetailRow.Height.Value;
        if (tableHeight > 0 && detailHeight > 0)
        {
            var ratio = $"{tableHeight:F1}:{detailHeight:F1}";
            DatabaseService.Instance.SaveSetting("cases_splitter_ratio", ratio);
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private void CasesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CasesGrid.SelectedItem is CaseDisplayItem selectedItem)
            _vm.ShowCaseDetails(selectedItem);
    }

    // تک‌کلیک روی ردیف = تیک چک‌باکس
    private void CasesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(CasesGrid, hit);
        if (cell == null) return;
        if (cell.Column is DataGridCheckBoxColumn) return;
        if (cell.DataContext is CaseDisplayItem item)
        {
            item.IsSelected = !item.IsSelected;
        }
    }

    // راست‌کلیک: کپی سلول / کپی ردیف / کپی انتخاب‌شده
    private void CasesGrid_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(CasesGrid, hit);
        if (cell == null) return;
        var colHeader = cell.Column.Header?.ToString() ?? "";
        string cellText = GetCellText(cell);
        var item = cell.DataContext as CaseDisplayItem;

        var menu = new ContextMenu { FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("B Nazanin"), FontSize = 14 };

        if (!string.IsNullOrEmpty(cellText))
        {
            var copyCell = new MenuItem { Header = $"📋 کپی سلول «{Truncate(cellText, 32)}»" };
            copyCell.Click += (_, _) => { try { Clipboard.SetText(cellText); } catch { } };
            menu.Items.Add(copyCell);
        }
        if (item != null)
        {
            var copyRow = new MenuItem { Header = $"📄 کپی ردیف {item.CaseNumber}" };
            copyRow.Click += (_, _) => CopyRowsToClipboard(new[] { item });
            menu.Items.Add(copyRow);
        }
        var sel = GetSelectedOrCurrent(item);
        if (sel.Count > 0)
        {
            var copySel = new MenuItem { Header = $"📋 کپی {sel.Count} ردیف انتخاب‌شده" };
            copySel.Click += (_, _) => CopyRowsToClipboard(sel);
            menu.Items.Add(copySel);

            var exportSel = new MenuItem { Header = $"📤 خروجی اکسل {sel.Count} ردیف" };
            exportSel.Click += (_, _) => ExportRowsToExcel(sel);
            menu.Items.Add(exportSel);
        }
        menu.Items.Add(new Separator());
        var copyAll = new MenuItem { Header = "📋 کپی همه (فیلتر فعلی)" };
        copyAll.Click += (_, _) => CopyRowsToClipboard(_vm.Cases.Where(c => true).ToList());
        menu.Items.Add(copyAll);

        menu.IsOpen = true;
        e.Handled = true;
    }

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        var sel = _vm.Cases.Where(c => c.IsSelected).ToList();
        if (sel.Count == 0) sel = _vm.Cases.ToList();
        CopyRowsToClipboard(sel);
    }

    private void ExportSelected_Click(object sender, RoutedEventArgs e)
    {
        var sel = _vm.Cases.Where(c => c.IsSelected).ToList();
        if (sel.Count == 0) sel = _vm.Cases.ToList();
        ExportRowsToExcel(sel);
    }

    private List<CaseDisplayItem> GetSelectedOrCurrent(CaseDisplayItem? current)
    {
        var sel = _vm.Cases.Where(c => c.IsSelected).ToList();
        if (sel.Count > 0) return sel;
        if (current != null) return new List<CaseDisplayItem> { current };
        return new List<CaseDisplayItem>();
    }

    private static string GetCellText(DataGridCell cell)
    {
        if (cell.Content is TextBlock tb) return tb.Text ?? "";
        // fallback: walk visual tree
        var txt = FindChild<TextBlock>(cell);
        if (txt != null) return txt.Text ?? "";
        return cell.DataContext?.ToString() ?? "";
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";

    private void CopyRowsToClipboard(IReadOnlyList<CaseDisplayItem> rows)
    {
        if (rows.Count == 0) { MessageBox.Show("ردیفی انتخاب نشده.", "کپی", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var headers = CasesGrid.Columns.Where(c => c.Header?.ToString() != "☐" && c.Visibility == Visibility.Visible)
                        .Select(c => c.Header?.ToString() ?? "").ToList();
        var lines = new List<string> { string.Join("\t", headers) };
        foreach (var r in rows)
        {
            var vals = new List<string>();
            foreach (var col in CasesGrid.Columns)
            {
                if (col.Header?.ToString() == "☐" || col.Visibility != Visibility.Visible) continue;
                vals.Add(GetValueForColumn(r, col));
            }
            lines.Add(string.Join("\t", vals));
        }
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
            _vm.StatusMessage = $"{rows.Count} ردیف کپی شد ✓";
        }
        catch (Exception ex) { MessageBox.Show($"خطا در کپی: {ex.Message}"); }
    }

    private static string GetValueForColumn(CaseDisplayItem r, DataGridColumn col)
    {
        var h = col.Header?.ToString() ?? "";
        return h switch
        {
            "ردیف" => r.RowNumber.ToString(),
            "شماره پرونده" => r.CaseNumber,
            "شماره سریال" => r.Serial,
            "نام مالک" => r.Owner,
            "همراه مالک" => r.OwnerMobile,
            "نام پدر" => r.OwnerFather,
            "کد ملی" => r.OwnerNationalCode,
            "تلفن مالک" => r.OwnerTel,
            "آدرس مالک" => r.OwnerAddress,
            "کد پستی" => r.OwnerZip,
            "زادگاه" => r.OwnerBirthLoc,
            "نوع کاربری" => r.UsageType,
            "گروه ساختمانی" => r.BuildingGroup,
            "کد نوسازی" => r.RenovationCode,
            "شماره دستور نقشه" => r.PlanInstructionNo,
            "نوع دستور نقشه" => r.PlanInstructionType,
            "تاریخ دستور نقشه" => r.PlanInstructionDate,
            "مساحت زمین" => r.LandArea,
            "متراژ پاراف" => r.ParafArea,
            "متراژ کسر ظرفیت" => r.CapacityArea,
            "نوع سازه" => r.StructureType,
            "عنوان بلوک" => r.BlockTitle,
            "تعداد بلوک" => r.BlockCount,
            "طبقات" => r.Floors,
            "واحدها" => r.Units,
            "صادرکننده پروانه" => r.Issuer,
            "شماره پروانه" => r.PermitNumber,
            "تاریخ صدور پروانه" => r.PermitDate,
            "تاریخ ترخیص" => r.ReleaseDate,
            "محدوده طرح" => r.PlanZone,
            "آدرس" => r.Address,
            "دفتر" => r.Office,
            "تاریخ کسر ظرفیت" => r.CapacityDate,
            "مسئولیت" => r.Responsibility,
            _ => ""
        };
    }

    private void ExportRowsToExcel(IReadOnlyList<CaseDisplayItem> rows)
    {
        if (rows.Count == 0) { MessageBox.Show("ردیفی برای خروجی وجود ندارد."); return; }
        var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = $"پرونده‌ها_{DateTime.Now:yyyy-MM-dd}.xlsx" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("پرونده‌ها");
            ws.RightToLeft = true;
            ws.Style.Font.FontName = "B Nazanin";
            ws.Style.Font.FontSize = 14;

            var cols = CasesGrid.Columns.Where(c => c.Header?.ToString() != "☐" && c.Visibility == Visibility.Visible).ToList();
            // header
            for (int i = 0; i < cols.Count; i++)
            {
                var c = ws.Cell(1, i + 1);
                c.Value = cols[i].Header?.ToString() ?? "";
                c.Style.Font.Bold = true;
                c.Style.Font.FontColor = XLColor.White;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B3147");
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                c.Style.Border.OutsideBorderColor = XLColor.FromHtml("#2C3E50");
            }
            ws.Row(1).Height = 26;

            for (int r = 0; r < rows.Count; r++)
            {
                int row = r + 2;
                bool odd = r % 2 == 0;
                var bg = odd ? XLColor.FromHtml("#F8F9FA") : XLColor.White;
                for (int ci = 0; ci < cols.Count; ci++)
                {
                    var cell = ws.Cell(row, ci + 1);
                    cell.Value = GetValueForColumn(rows[r], cols[ci]);
                    cell.Style.Font.FontName = "B Nazanin";
                    cell.Style.Font.FontSize = 14;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D5D8DC");
                    cell.Style.Fill.BackgroundColor = bg;
                }
                ws.Row(row).Height = 20;
            }
            ws.Columns(1, cols.Count).AdjustToContents();
            for (int ci = 1; ci <= cols.Count; ci++) ws.Column(ci).Width = Math.Max(ws.Column(ci).Width, 12);
            ws.RangeUsed()!.SetAutoFilter();
            ws.SheetView.FreezeRows(1);
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
            wb.SaveAs(dlg.FileName);
            _vm.StatusMessage = $"خروجی اکسل ذخیره شد: {dlg.FileName}";
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); } catch { }
        }
        catch (Exception ex) { MessageBox.Show($"خطا در خروجی اکسل: {ex.Message}"); }
    }

    private static DataGridCell? FindDataGridCell(DataGrid dg, DependencyObject? hit)
    {
        while (hit != null && hit != dg)
        {
            if (hit is DataGridCell cell) return cell;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }
}
