using System.Windows;
using System.Windows.Controls;
using NezamMonitor.Core.Models;

namespace NezamMonitor.App.Views;

public partial class FeeReceiptDialog : Window
{
    private readonly List<string> _paidDates;
    private readonly List<string> _confirmedDates;
    private readonly HashSet<string> _selected;
    private bool _isAdvance;

    public FeeReceipt ResultReceipt { get; private set; } = new();

    // overload قدیمی
    public FeeReceiptDialog(FeeReceipt? edit, List<string> allEndDates)
        : this(edit, allEndDates, new List<string>()) { }

    public FeeReceiptDialog(FeeReceipt? edit, List<string> paidDates, List<string> confirmedDates)
    {
        InitializeComponent();
        _paidDates = paidDates.Distinct().OrderBy(s => s).ToList();
        _confirmedDates = confirmedDates.Distinct().OrderBy(s => s).ToList();
        _selected = new HashSet<string>((edit?.RelatedEndDates ?? new()).Select(s => s.Trim()));
        _isAdvance = edit?.IsAdvance ?? false;

        if (edit != null)
        {
            DateBox.Text = edit.ReceiptDate;
            AmountBox.Text = edit.Amount.ToString("N0");
            ChangesBox.Text = edit.Changes ?? "";
            ResultReceipt.Id = edit.Id;
        }
        AdvanceCheck.IsChecked = _isAdvance;
        AdvanceHint.Visibility = _isAdvance ? Visibility.Visible : Visibility.Collapsed;
        UpdateHeader();
        BuildChecks();
    }

    private void UpdateHeader()
    {
        RelatedHeader.Text = _isAdvance
            ? "تاریخ‌های مرتبط (ستون آغاز — فقط تایید شده):"
            : "تاریخ‌های مرتبط (ستون آغاز — فقط پرداخت شده):";
    }

    private void BuildChecks()
    {
        EndDatePanel.Children.Clear();
        var list = _isAdvance ? _confirmedDates : _paidDates;
        if (list.Count == 0)
        {
            var msg = _isAdvance ? "هیچ تاریخ شروعی با وضعیت تایید شده یافت نشد." : "هیچ تاریخ شروعی با وضعیت پرداخت شده یافت نشد.";
            EndDatePanel.Children.Add(new TextBlock { Text = msg, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(4) });
            return;
        }
        foreach (var d in list)
        {
            var cb = new CheckBox { Content = d, IsChecked = _selected.Contains(d.Trim()), Margin = new Thickness(4, 3, 4, 3), FontFamily = new System.Windows.Media.FontFamily("B Nazanin"), FontSize = 14 };
            cb.Checked += (_, _) => _selected.Add(d.Trim());
            cb.Unchecked += (_, _) => _selected.Remove(d.Trim());
            EndDatePanel.Children.Add(cb);
        }
    }

    private void AdvanceCheck_Changed(object sender, RoutedEventArgs e)
    {
        _isAdvance = AdvanceCheck.IsChecked == true;
        AdvanceHint.Visibility = _isAdvance ? Visibility.Visible : Visibility.Collapsed;
        UpdateHeader();
        // تاریخ‌های تیک‌خورده قبلی نگه داشته می‌شن؛ فقط لیست نمایش عوض میشه
        BuildChecks();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var date = DateBox.Text.Trim();
        var amtRaw = AmountBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(date)) { MessageBox.Show("تاریخ واریز را وارد کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        long amt = ParseAmount(amtRaw);
        if (amt <= 0) { MessageBox.Show("مبلغ واریز معتبر نیست.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (_selected.Count == 0) { MessageBox.Show("حداقل یک تاریخ مرتبط انتخاب کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        ResultReceipt.ReceiptDate = date;
        ResultReceipt.Amount = amt;
        ResultReceipt.IsAdvance = _isAdvance;
        ResultReceipt.RelatedEndDatesRaw = string.Join(";", _selected);
        DialogResult = true;
        Close();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private static long ParseAmount(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var c = s.Replace("ریال", "").Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", "").Trim();
        c = c.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4").Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9")
             .Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4").Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9");
        return long.TryParse(c, out var x) ? x : 0;
    }
}
