using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        DataContext = new ReportsViewModel(Services.DatabaseService.Instance);
    }

    private void ReportsGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(dg, hit);
        if (cell == null) return;

        var item = cell.DataContext as ReportItem;
        if (item == null) return;

        var menu = new ContextMenu();
        var colHeader = cell.Column.Header.ToString() ?? "";

        string? cellValue = colHeader switch
        {
            "پرونده" => item.CaseNumber, "مالک" => item.Owner, "نوع" => item.ReportType,
            "مرحله" => item.Stage, "ناظر" => item.Engineer, "رشته" => item.Discipline, _ => null
        };
        string? columnName = cellValue != null ? colHeader switch
        {
            "پرونده" => "CaseNumber", "مالک" => "Owner", "نوع" => "ReportType",
            "مرحله" => "Stage", "ناظر" => "Engineer", "رشته" => "Discipline", _ => ""
        } : null;

        if (!string.IsNullOrWhiteSpace(cellValue) && !string.IsNullOrEmpty(columnName))
        {
            var filterItem = new MenuItem { Header = $"🔍 فیلتر بر اساس «{cellValue}»" };
            filterItem.Click += (_, _) => (DataContext as ReportsViewModel)?.FilterByValueCommand.Execute($"{columnName}:{cellValue}");
            menu.Items.Add(filterItem);

            var removeItem = new MenuItem { Header = $"❌ لغو فیلتر {colHeader}" };
            removeItem.Click += (_, _) => (DataContext as ReportsViewModel)?.RemoveFilter(columnName);
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