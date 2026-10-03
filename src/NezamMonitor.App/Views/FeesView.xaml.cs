using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class FeesView : UserControl
{
    public FeesView()
    {
        InitializeComponent();
        DataContext = new FeesViewModel(Services.DatabaseService.Instance);
        UpdateTabUI(0);
    }

    private void TabFee_Click(object sender, RoutedEventArgs e) => UpdateTabUI(0);
    private void TabReceipt_Click(object sender, RoutedEventArgs e) => UpdateTabUI(1);

    private void UpdateTabUI(int tab)
    {
        if (DataContext is FeesViewModel vm) vm.ActiveTab = tab;
        PanelFees.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanelReceipts.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
        BtnTabFee.Background = tab == 0 ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3498DB")) : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ECF0F1"));
        BtnTabFee.Foreground = tab == 0 ? System.Windows.Media.Brushes.White : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2C3E50"));
        BtnTabReceipt.Background = tab == 1 ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3498DB")) : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ECF0F1"));
        BtnTabReceipt.Foreground = tab == 1 ? System.Windows.Media.Brushes.White : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2C3E50"));
    }

    private void ReceiptsGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ReceiptsGrid.SelectedItem is FeeReceiptRow row && DataContext is FeesViewModel vm)
            vm.EditReceiptCommand.Execute(row);
    }
    // تک‌کلیک: انتخاب چک‌باکس ردیف را toggle می‌کند
    private void ReceiptsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(ReceiptsGrid, hit);
        if (cell == null) return;
        if (cell.Column is DataGridCheckBoxColumn) return; // خود چک‌باکس خودش هندل می‌کند
        if (cell.DataContext is FeeReceiptRow row)
        {
            row.IsSelected = !row.IsSelected;
            e.Handled = false;
        }
    }
    private void FeesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(FeesGrid, hit);
        if (cell == null) return;
        if (cell.Column is DataGridCheckBoxColumn) return;
        if (cell.DataContext is FeeItem item)
        {
            item.IsSelected = !item.IsSelected;
            e.Handled = false;
        }
    }
    private void ReceiptEdit_Click(object sender, RoutedEventArgs e)
    {
        if (ReceiptsGrid.SelectedItem is FeeReceiptRow row && DataContext is FeesViewModel vm)
            vm.EditReceiptCommand.Execute(row);
        else if (DataContext is FeesViewModel vm2)
            vm2.ReceiptStatus = "ردیفی انتخاب نشده";
    }
    private void ReceiptDelete_Click(object sender, RoutedEventArgs e)
    {
        if (ReceiptsGrid.SelectedItem is FeeReceiptRow row && DataContext is FeesViewModel vm)
            vm.DeleteReceiptCommand.Execute(row);
        else if (DataContext is FeesViewModel vm2)
            vm2.ReceiptStatus = "ردیفی انتخاب نشده";
    }

    private void FeesGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(dg, hit);
        if (cell == null) return;

        var item = cell.DataContext as FeeItem;
        if (item == null) return;

        var menu = new ContextMenu();
        var colHeader = cell.Column.Header.ToString() ?? "";

        string? cellValue = colHeader switch
        {
            "پرونده" => item.CaseNumber, "مالک" => item.Owner, "رشته" => item.Discipline,
            "مرحله" => item.Stage, "وضعیت" => item.PayStatus, "نوع خدمت" => item.ServiceType, _ => null
        };
        string? columnName = cellValue != null ? colHeader switch
        {
            "پرونده" => "CaseNumber", "مالک" => "Owner", "رشته" => "Discipline",
            "مرحله" => "Stage", "وضعیت" => "Status", "نوع خدمت" => "ServiceType", _ => ""
        } : null;

        if (!string.IsNullOrWhiteSpace(cellValue) && !string.IsNullOrEmpty(columnName))
        {
            var filterItem = new MenuItem { Header = $"🔍 فیلتر بر اساس «{cellValue}»" };
            filterItem.Click += (_, _) => (DataContext as FeesViewModel)?.FilterByValueCommand.Execute($"{columnName}:{cellValue}");
            menu.Items.Add(filterItem);

            var removeItem = new MenuItem { Header = $"❌ لغو فیلتر {colHeader}" };
            removeItem.Click += (_, _) => (DataContext as FeesViewModel)?.RemoveFilter(columnName);
            menu.Items.Add(removeItem);

            menu.Items.Add(new Separator());
            var copyItem = new MenuItem { Header = $"📋 کپی «{cellValue}»" };
            copyItem.Click += (_, _) => Clipboard.SetText(cellValue);
            menu.Items.Add(copyItem);
        }

        if (menu.HasItems) menu.IsOpen = true;
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
