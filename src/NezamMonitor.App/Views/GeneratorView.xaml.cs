using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class GeneratorView : UserControl
{
    public GeneratorView()
    {
        InitializeComponent();
        DataContext = new GeneratorViewModel(DatabaseService.Instance);
    }

    private void StageFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int stage))
        {
            if (DataContext is GeneratorViewModel vm)
                vm.SelectedStage = stage;
        }
    }

    private void FilterStage_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && DataContext is GeneratorViewModel vm)
            vm.FilterStage = cb.SelectedIndex;
    }

    private void FilterStatus_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && DataContext is GeneratorViewModel vm)
            vm.FilterStatus = cb.SelectedIndex;
    }

    private void CaseGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid dg && dg.SelectedItem is CaseReportItem item)
        {
            var menu = new ContextMenu();

            // تولید گزارش این پرونده
            var genItem = new MenuItem { Header = "📝 تولید گزارش", Tag = item };
            genItem.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                {
                    item.IsSelected = true;
                    vm.GenerateCommand.Execute(null);
                    item.IsSelected = false;
                }
            };
            menu.Items.Add(genItem);

            // باز کردن پوشه پرونده
            var openFolder = new MenuItem { Header = "📂 باز کردن پوشه پرونده", Tag = item };
            openFolder.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                    vm.OpenFolderForCase(item);
            };
            menu.Items.Add(openFolder);

            // مشاهده گزارش‌های این پرونده
            var viewReports = new MenuItem { Header = "📚 مشاهده گزارش‌های این پرونده", Tag = item };
            viewReports.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                {
                    vm.HistorySearchText = item.CaseNumber;
                    vm.ActiveTab = 1;
                    vm.LoadHistory();
                }
            };
            menu.Items.Add(viewReports);

            menu.Items.Add(new Separator());

            // کپی شماره پرونده
            var copyCase = new MenuItem { Header = "📋 کپی شماره پرونده", Tag = item };
            copyCase.Click += (s, args) =>
            {
                Clipboard.SetText(item.CaseNumber);
                if (DataContext is GeneratorViewModel vm)
                    vm.StatusMessage = $"شماره پرونده کپی شد: {item.CaseNumber}";
            };
            menu.Items.Add(copyCase);

            // کپی نام مالک
            var copyOwner = new MenuItem { Header = "📋 کپی نام مالک", Tag = item };
            copyOwner.Click += (s, args) =>
            {
                Clipboard.SetText(item.Owner);
                if (DataContext is GeneratorViewModel vm)
                    vm.StatusMessage = $"نام مالک کپی شد: {item.Owner}";
            };
            menu.Items.Add(copyOwner);

            menu.IsOpen = true;
        }
    }

    private void HistoryGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid dg)
        {
            // انتخاب ردیف راست‌کلیک‌شده اگه قبلاً انتخاب نشده
            var hit = dg.InputHitTest(e.GetPosition(dg));
            if (hit is DependencyObject depObj)
            {
                // پیدا کردن DataGridRow از طریق visual tree
                var current = depObj;
                while (current != null)
                {
                    if (current is DataGridRow row)
                    {
                        if (row.Item is ReportHistoryItem item && !dg.SelectedItems.Contains(item))
                            dg.SelectedItem = item;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }
    }

    // ─── Helper: دریافت آیتم‌های انتخاب‌شده ───
    private List<ReportHistoryItem> GetSelectedHistoryItems()
    {
        var selected = HistoryGrid.SelectedItems;
        if (selected == null || selected.Count == 0)
        {
            if (HistoryGrid.SelectedItem is ReportHistoryItem single)
                return new List<ReportHistoryItem> { single };
            return new List<ReportHistoryItem>();
        }
        return selected.Cast<ReportHistoryItem>().ToList();
    }

    // ─── Event Handlers for XAML ContextMenu ───

    private void HistoryMenu_OpenReport(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.OpenReport(items[0]);
    }

    private void HistoryMenu_OpenFolder(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.OpenFolder(items[0]);
    }

    private void HistoryMenu_Regenerate(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.RegenerateWithConfirmation(items[0]);
    }

    private void HistoryMenu_DeleteSelected(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.DeleteSelectedHistory(items);
    }

    private void HistoryMenu_DeleteWithFile(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.DeleteSelectedHistoryWithFile(items);
    }

    // ─── Event Handlers for XAML Toolbar Buttons ───

    private void History_SelectAll(object sender, RoutedEventArgs e)
    {
        HistoryGrid.SelectAll();
        if (DataContext is GeneratorViewModel vm)
            vm.StatusMessage = $"همه {HistoryGrid.SelectedItems.Count} ردیف انتخاب شد";
    }

    private void History_DeleteSelected(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0)
        {
            if (DataContext is GeneratorViewModel vm0) vm0.StatusMessage = "موردی انتخاب نشده — ابتدا ردیف‌ها را انتخاب کنید";
            return;
        }
        if (DataContext is GeneratorViewModel vm) vm.DeleteSelectedHistory(items);
    }

    private void History_DeleteSelectedWithFile(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0)
        {
            if (DataContext is GeneratorViewModel vm0) vm0.StatusMessage = "موردی انتخاب نشده — ابتدا ردیف‌ها را انتخاب کنید";
            return;
        }
        if (DataContext is GeneratorViewModel vm) vm.DeleteSelectedHistoryWithFile(items);
    }

    private void HistoryMenu_CopyPath(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        Clipboard.SetText(items[0].OutputPath);
        if (DataContext is GeneratorViewModel vm)
            vm.StatusMessage = $"مسیر کپی شد: {items[0].OutputPath}";
    }
}
