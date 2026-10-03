using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class FollowUpView : UserControl
{
    private FollowUpItem? _rightClickedItem;
    private string _rightClickedColumnHeader = "";

    public FollowUpView()
    {
        InitializeComponent();
        DataContext = new FollowUpViewModel(DatabaseService.Instance);
    }

    /// <summary>
    /// ذخیره خودکار هنگام پایان ویرایش سلول
    /// </summary>
    private void FollowUpGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit && e.Row.Item is FollowUpItem item)
        {
            // ذخیره را عقب بینداز تا binding مقدار جدید را آپدیت کند
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                NezamMonitor.App.Services.DatabaseService.Instance.SaveFollowUpEdit(
                    item.OriginalCaseNumber,
                    item.Description,
                    item.CaseNumber != item.OriginalCaseNumber ? item.CaseNumber : "",
                    item.Owner != item.OriginalOwner ? item.Owner : "",
                    item.Address != item.OriginalAddress ? item.Address : "",
                    item.OwnerMobile != item.OriginalOwnerMobile ? item.OwnerMobile : ""
                );
            }));
        }
    }

    /// <summary>
    /// راست کلیک روی جدول - شناسایی سلول دقیق و نمایش منوی زمینه
    /// </summary>
    private void FollowUpGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var cell = FindDataGridCell(e.OriginalSource as DependencyObject);
        if (cell == null || cell.DataContext is not FollowUpItem item) return;

        _rightClickedItem = item;
        _rightClickedColumnHeader = GetColumnHeader(cell);

        if (string.IsNullOrEmpty(_rightClickedColumnHeader)) return;

        var menu = new ContextMenu();

        var value = GetCellValue(item, _rightClickedColumnHeader);
        if (!string.IsNullOrEmpty(value))
        {
            var copyCell = new MenuItem { Header = $"📋 کپی «{_rightClickedColumnHeader}»" };
            copyCell.Click += (s, args) => Clipboard.SetText(value);
            menu.Items.Add(copyCell);
        }

        var copyRow = new MenuItem { Header = "📋 کپی کل ردیف" };
        copyRow.Click += (s, args) =>
        {
            var rowText = $"{item.RowId}\t{item.CaseNumber}\t{item.Owner}\t{item.Address}\t{item.OwnerMobile}\t{item.ReportCount}\t{item.Description}";
            Clipboard.SetText(rowText);
        };
        menu.Items.Add(copyRow);

        var propertyName = GetPropertyName(_rightClickedColumnHeader);
        if (!string.IsNullOrEmpty(propertyName))
        {
            menu.Items.Add(new Separator());
            var refreshCase = new MenuItem { Header = "🔄 بروزرسانی از پرونده‌ها" };
            refreshCase.Click += (s, args) =>
            {
                if (DataContext is FollowUpViewModel vm)
                    vm.RefreshCellFromCases(item, propertyName);
            };
            menu.Items.Add(refreshCase);
        }

        menu.IsOpen = true;
    }

    private static DataGridCell? FindDataGridCell(DependencyObject? source)
    {
        while (source != null && source is not DataGridCell)
            source = VisualTreeHelper.GetParent(source);
        return source as DataGridCell;
    }

    private static string GetColumnHeader(DataGridCell cell)
    {
        if (cell.Column is DataGridBoundColumn boundColumn && boundColumn.Binding is System.Windows.Data.Binding binding)
        {
            return binding.Path.Path switch
            {
                nameof(FollowUpItem.CaseNumber) => "شماره پرونده",
                nameof(FollowUpItem.Owner) => "نام مالک",
                nameof(FollowUpItem.Address) => "آدرس",
                nameof(FollowUpItem.OwnerMobile) => "همراه مالک",
                nameof(FollowUpItem.ReportCount) => "تعداد گزارش",
                nameof(FollowUpItem.Description) => "توضیحات",
                nameof(FollowUpItem.RowId) => "ردیف",
                _ => ""
            };
        }
        return "";
    }

    private static string GetPropertyName(string header)
    {
        return header switch
        {
            "شماره پرونده" => nameof(FollowUpItem.CaseNumber),
            "نام مالک" => nameof(FollowUpItem.Owner),
            "آدرس" => nameof(FollowUpItem.Address),
            "همراه مالک" => nameof(FollowUpItem.OwnerMobile),
            _ => ""
        };
    }

    private static string GetCellValue(FollowUpItem item, string header)
    {
        return header switch
        {
            "ردیف" => item.RowId.ToString(),
            "شماره پرونده" => item.CaseNumber,
            "نام مالک" => item.Owner,
            "آدرس" => item.Address,
            "همراه مالک" => item.OwnerMobile,
            "تعداد گزارش" => item.ReportCount.ToString(),
            "توضیحات" => item.Description,
            _ => ""
        };
    }

    private void ExportVCard_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FollowUpViewModel vm) return;

        var selectedItems = FollowUpGrid.SelectedItems;
        var selectedCases = new HashSet<string>();

        if (selectedItems != null && selectedItems.Count > 0)
        {
            // Export only selected rows
            foreach (var item in selectedItems)
            {
                if (item is FollowUpItem fi)
                    selectedCases.Add(fi.OriginalCaseNumber);
            }
        }
        // If nothing selected, export all
        vm.ExportVCard(selectedCases.Count > 0 ? selectedCases : null);
    }
}
