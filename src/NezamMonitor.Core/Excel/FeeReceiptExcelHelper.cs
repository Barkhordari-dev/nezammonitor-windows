using ClosedXML.Excel;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Excel;

/// <summary>اکسل دریافتی — B Nazanin 14، راست‌چین، استایل مالی</summary>
public static class FeeReceiptExcelHelper
{
    private static void H(IXLCell c, string v)
    {
        c.Value = v;
        c.Style.Font.Bold = true;
        c.Style.Font.FontName = "B Nazanin";
        c.Style.Font.FontSize = 14;
        c.Style.Font.FontColor = XLColor.White;
        c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B3147");
        c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        c.Style.Border.OutsideBorderColor = XLColor.FromHtml("#2C3E50");
    }
    private static void D(IXLCell c, object? v, bool neg = false)
    {
        if (v != null) c.Value = v.ToString() ?? "";
        c.Style.Font.FontName = "B Nazanin";
        c.Style.Font.FontSize = 14;
        c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        c.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D5D8DC");
        if (neg) { c.Style.Font.FontColor = XLColor.FromHtml("#C0392B"); c.Style.Font.Bold = true; }
    }

    // سازگاری قدیمی: یک map
    public static void Export(IReadOnlyList<FeeReceipt> list, Dictionary<string, long> feeSumByEnd, string path)
    {
        // تفکیک map برای علی‌الحساب — اگر فقط یک map داده شده، برای هر دو استفاده می‌کنیم
        Export(list, feeSumByEnd, feeSumByEnd, path);
    }
    public static void Export(IReadOnlyList<FeeReceipt> list, Dictionary<string, long> paidMap, Dictionary<string, long> confirmedMap, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("دریافتی");
        ws.RightToLeft = true;
        ws.Style.Font.FontName = "B Nazanin";
        ws.Style.Font.FontSize = 14;

        string[] headers = { "ردیف", "تاریخ واریز", "مبلغ واریز (ریال)", "نوع واریز", "تاریخ‌های مرتبط (آغاز)", "جمع حق‌الزحمه مرتبط (ریال)", "اختلاف واریزی با حق‌الزحمه (ریال)", "تغییرات", "توضیح" };
        for (int i = 0; i < headers.Length; i++) H(ws.Cell(1, i + 1), headers[i]);
        ws.Row(1).Height = 26;

        for (int i = 0; i < list.Count; i++)
        {
            var r = list[i];
            int row = i + 2;
            bool odd = i % 2 == 0;
            var bg = odd ? XLColor.FromHtml("#F8F9FA") : XLColor.White;
            var map = r.IsAdvance ? confirmedMap : paidMap;
            long feeSum = CalcFeeSum(r, map);
            long diff = r.Amount - feeSum;
            bool neg = diff < 0;

            D(ws.Cell(row, 1), (i + 1).ToString());
            D(ws.Cell(row, 2), r.ReceiptDate);
            D(ws.Cell(row, 3), r.Amount.ToString("N0"));
            D(ws.Cell(row, 4), r.IsAdvance ? "علی‌الحساب" : "عادی");
            if (r.IsAdvance) ws.Cell(row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9E7");
            D(ws.Cell(row, 5), string.Join(" ؛ ", r.RelatedEndDates));
            D(ws.Cell(row, 6), feeSum.ToString("N0"));
            D(ws.Cell(row, 7), diff.ToString("N0"), neg);
            D(ws.Cell(row, 8), r.Changes ?? "");
            D(ws.Cell(row, 9), "");
            ws.Row(row).Height = 22;
            for (int c = 1; c <= 9; c++) ws.Cell(row, c).Style.Fill.BackgroundColor = bg;
            if (r.IsAdvance) ws.Cell(row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9E7");
            if (r.IsRecentlyUpdated)
                for (int c = 1; c <= 9; c++) ws.Cell(row, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#D5F5E3");
            if (neg) ws.Cell(row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#FDEDEC");
        }
        ws.Columns(1, 9).AdjustToContents();
        for (int c = 1; c <= 9; c++) ws.Column(c).Width = Math.Max(ws.Column(c).Width, 16);
        ws.Column(5).Width = Math.Max(ws.Column(5).Width, 28);
        ws.Column(8).Width = Math.Max(ws.Column(8).Width, 24);
        ws.RangeUsed()!.SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
        wb.SaveAs(path);
    }

    public static List<FeeReceipt> Import(string path)
    {
        var list = new List<FeeReceipt>();
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        // تشخیص هدر: اگر ستون 4 = نوع واریز، فرمت جدید 9 ستونه است
        var h4 = ws.Cell(1, 4).GetString().Trim();
        bool hasTypeCol = h4.Contains("نوع");
        int colRelated = hasTypeCol ? 5 : 4;
        int colChanges = hasTypeCol ? 8 : 7;
        int colType = 4;
        int last = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= last; row++)
        {
            var date = ws.Cell(row, 2).GetString().Trim();
            var amountRaw = ws.Cell(row, 3).GetString().Trim();
            var relatedRaw = ws.Cell(row, colRelated).GetString().Trim();
            var changes = ws.Cell(row, colChanges).GetString().Trim();
            if (string.IsNullOrWhiteSpace(date) && string.IsNullOrWhiteSpace(amountRaw)) continue;
            long amt = ParseAmount(amountRaw);
            var related = relatedRaw.Split(new[] { '؛', ';', '،', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            bool isAdvance = false;
            if (hasTypeCol)
            {
                var t = ws.Cell(row, colType).GetString().Trim();
                isAdvance = t.Contains("علی") || t.Contains("الحساب");
            }
            list.Add(new FeeReceipt
            {
                ReceiptDate = date,
                Amount = amt,
                IsAdvance = isAdvance,
                RelatedEndDatesRaw = string.Join(";", related),
                Changes = changes
            });
        }
        return list;
    }

    private static long CalcFeeSum(FeeReceipt r, Dictionary<string, long> map)
    {
        long sum = 0;
        foreach (var d in r.RelatedEndDates)
            if (map.TryGetValue(d.Trim(), out var v)) sum += v;
        return sum;
    }

    private static long ParseAmount(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var c = s.Replace("ریال", "").Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", "").Trim();
        c = c.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4").Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9")
             .Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4").Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9");
        return long.TryParse(c, out var x) ? x : 0;
    }
}
