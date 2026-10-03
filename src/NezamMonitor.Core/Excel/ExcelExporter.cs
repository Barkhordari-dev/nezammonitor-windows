using ClosedXML.Excel;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Excel;
/// <summary>
/// Generates professional RTL Persian Excel workbooks from case data.
/// </summary>
public static class ExcelExporter
{
    private static void ApplyHeader(IXLCell cell, string value)
    {
        cell.Value = value;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Font.FontName = "B Nazanin";
        cell.Style.Font.FontSize = 11;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4F72");
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cell.Style.Alignment.WrapText = true;
    }

    private static void ApplyData(IXLCell cell, object? value = null)
    {
        if (value != null) cell.Value = value.ToString();
        cell.Style.Font.FontName = "B Nazanin";
        cell.Style.Font.FontSize = 10;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    public static void GenerateFullWorkbook(IReadOnlyList<Case> cases, string filePath)
    {
        using var wb = new XLWorkbook();
        var ws1 = wb.Worksheets.Add("پرونده‌ها"); ws1.RightToLeft = true;
        var ws2 = wb.Worksheets.Add("ناظرین"); ws2.RightToLeft = true;
        var ws3 = wb.Worksheets.Add("حق‌الزحمه"); ws3.RightToLeft = true;
        var ws4 = wb.Worksheets.Add("گزارش‌ها"); ws4.RightToLeft = true;
        var ws5 = wb.Worksheets.Add("پیگیری‌ها"); ws5.RightToLeft = true;

        WriteCases(ws1, cases);
        WriteEngineers(ws2, cases);
        WriteFees(ws3, cases);
        WriteReports(ws4, cases);
        WriteFollowUp(ws5, cases);
        wb.SaveAs(filePath);
    }

    public static void GenerateChangesWorkbook(IReadOnlyList<Core.Diff.FieldChange> changes, string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("تغییرات"); ws.RightToLeft = true;
        string[] h = { "تاریخ", "شماره پرونده", "نام مالک", "نوع تغییر", "فیلد", "مقدار قبلی", "مقدار جدید" };
        for (int c = 0; c < h.Length; c++) ApplyHeader(ws.Cell(1, c + 1), h[c]);
        for (int r = 0; r < changes.Count; r++)
        {
            var ch = changes[r];
            ApplyData(ws.Cell(r + 2, 1), DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            ApplyData(ws.Cell(r + 2, 2), ch.CaseNumber);
            ApplyData(ws.Cell(r + 2, 3), ch.Owner);
            ApplyData(ws.Cell(r + 2, 4), ch.Type.ToString());
            ApplyData(ws.Cell(r + 2, 5), ch.Field);
            ApplyData(ws.Cell(r + 2, 6), ch.OldValue);
            ApplyData(ws.Cell(r + 2, 7), ch.NewValue);
        }
        for (int c = 1; c <= 7; c++) ws.Column(c).Width = 20;
        ws.RangeUsed()?.SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        wb.SaveAs(filePath);
    }

    private static void WriteCases(IXLWorksheet ws, IReadOnlyList<Case> cases)
    {
        string[] h = { "ردیف", "سریال", "پرونده", "مالک", "همراه", "نام پدر", "کد ملی", "تلفن مالک", "آدرس مالک", "کد پستی", "زادگاه",
            "نوع کاربری", "گروه ساختمانی", "کد نوسازی",
            "شماره دستور نقشه", "نوع دستور نقشه", "تاریخ دستور نقشه",
            "نوع سازه", "عنوان بلوک", "تعداد بلوک", "طبقات", "واحد",
            "صادرکننده", "پروانه", "تاریخ پروانه", "تاریخ ترخیص",
            "مساحت", "پاراف", "آدرس", "محدوده طرح", "دفتر", "تاریخ کسر", "مسئولیت" };
        for (int c = 0; c < h.Length; c++) ApplyHeader(ws.Cell(1, c + 1), h[c]);
        for (int r = 0; r < cases.Count; r++)
        {
            var c = cases[r]; var s = c.Specification;
            ApplyData(ws.Cell(r + 2, 1), r + 1);
            ApplyData(ws.Cell(r + 2, 2), c.Serial);
            ApplyData(ws.Cell(r + 2, 3), c.CaseNumber);
            ApplyData(ws.Cell(r + 2, 4), c.Owner);
            ApplyData(ws.Cell(r + 2, 5), c.OwnerMobile);
            ApplyData(ws.Cell(r + 2, 6), c.OwnerFather);
            ApplyData(ws.Cell(r + 2, 7), c.OwnerNationalCode);
            ApplyData(ws.Cell(r + 2, 8), c.OwnerTel);
            ApplyData(ws.Cell(r + 2, 9), c.OwnerAddress);
            ApplyData(ws.Cell(r + 2, 10), c.OwnerZip);
            ApplyData(ws.Cell(r + 2, 11), c.OwnerBirthLoc);
            ApplyData(ws.Cell(r + 2, 12), s?.UsageType);
            ApplyData(ws.Cell(r + 2, 13), s?.BuildingGroup);
            ApplyData(ws.Cell(r + 2, 14), s?.RenovationCode);
            ApplyData(ws.Cell(r + 2, 15), s?.PlanInstructionNo);
            ApplyData(ws.Cell(r + 2, 16), s?.PlanInstructionType);
            ApplyData(ws.Cell(r + 2, 17), s?.PlanInstructionDate);
            ApplyData(ws.Cell(r + 2, 18), s?.StructureType);
            ApplyData(ws.Cell(r + 2, 19), s?.BlockTitle);
            ApplyData(ws.Cell(r + 2, 20), s?.BlockCount);
            ApplyData(ws.Cell(r + 2, 21), s?.Floors);
            ApplyData(ws.Cell(r + 2, 22), s?.Units);
            ApplyData(ws.Cell(r + 2, 23), s?.Issuer);
            ApplyData(ws.Cell(r + 2, 24), s?.PermitNumber);
            ApplyData(ws.Cell(r + 2, 25), s?.PermitDate);
            ApplyData(ws.Cell(r + 2, 26), s?.ReleaseDate);
            ApplyData(ws.Cell(r + 2, 27), s?.LandArea);
            ApplyData(ws.Cell(r + 2, 28), s?.ParafArea);
            ApplyData(ws.Cell(r + 2, 29), s?.Address);
            ApplyData(ws.Cell(r + 2, 30), s?.PlanZone);
            ApplyData(ws.Cell(r + 2, 31), c.Office);
            ApplyData(ws.Cell(r + 2, 32), c.CapacityDate);
            ApplyData(ws.Cell(r + 2, 33), c.Responsibility);
        }
        for (int c = 1; c <= h.Length; c++) ws.Column(c).Width = 16;
        ws.RangeUsed()?.SetAutoFilter(); ws.SheetView.FreezeRows(1);
    }

    private static void WriteEngineers(IXLWorksheet ws, IReadOnlyList<Case> cases)
    {
        string[] h = { "مالک", "پرونده", "سریال", "رشته", "ناظر", "نقش" };
        for (int c = 0; c < h.Length; c++) ApplyHeader(ws.Cell(1, c + 1), h[c]);
        int row = 2;
        foreach (var c in cases)
        {
            int sr = row;
            foreach (var e in c.Engineers)
            { ApplyData(ws.Cell(row, 1), c.Owner); ApplyData(ws.Cell(row, 2), c.CaseNumber); ApplyData(ws.Cell(row, 3), c.Serial); ApplyData(ws.Cell(row, 4), e.Discipline); ApplyData(ws.Cell(row, 5), e.Name); ApplyData(ws.Cell(row, 6), e.Role); row++; }
            if (row - 1 > sr) { ws.Range(sr, 1, row - 1, 1).Merge(); ws.Range(sr, 2, row - 1, 2).Merge(); ws.Range(sr, 3, row - 1, 3).Merge(); }
        }
        for (int c = 1; c <= 6; c++) ws.Column(c).Width = 20; ws.SheetView.FreezeRows(1);
    }

    private static void WriteFees(IXLWorksheet ws, IReadOnlyList<Case> cases)
    {
        string[] h = { "مالک", "پرونده", "سریال", "رشته", "نوع خدمت", "مرحله", "تاریخ شروع", "تاریخ پایان", "مبلغ", "وضعیت پرداخت", "وضعیت تایید", "توضیحات" };
        for (int c = 0; c < h.Length; c++) ApplyHeader(ws.Cell(1, c + 1), h[c]);
        int row = 2;
        foreach (var c in cases)
        {
            int sr = row;
            foreach (var f in c.Fees)
            { ApplyData(ws.Cell(row, 1), c.Owner); ApplyData(ws.Cell(row, 2), c.CaseNumber); ApplyData(ws.Cell(row, 3), c.Serial); ApplyData(ws.Cell(row, 4), f.Discipline); ApplyData(ws.Cell(row, 5), f.ServiceType); ApplyData(ws.Cell(row, 6), f.Stage); ApplyData(ws.Cell(row, 7), f.StartDate); ApplyData(ws.Cell(row, 8), f.EndDate); ApplyData(ws.Cell(row, 9), f.Amount); ApplyData(ws.Cell(row, 10), f.PayStatus); ApplyData(ws.Cell(row, 11), f.ConfirmStatus); ApplyData(ws.Cell(row, 12), f.Description); row++; }
            if (row - 1 > sr) { ws.Range(sr, 1, row - 1, 1).Merge(); ws.Range(sr, 2, row - 1, 2).Merge(); ws.Range(sr, 3, row - 1, 3).Merge(); }
        }
        for (int c = 1; c <= 12; c++) ws.Column(c).Width = 16; ws.SheetView.FreezeRows(1);
    }

    private static void WriteReports(IXLWorksheet ws, IReadOnlyList<Case> cases)
    {
        string[] h = { "مالک", "پرونده", "سریال", "نوع گزارش", "مرحله", "ناظر", "مسئولیت", "تاریخ" };
        for (int c = 0; c < h.Length; c++) ApplyHeader(ws.Cell(1, c + 1), h[c]);
        int row = 2;
        foreach (var c in cases)
        {
            int sr = row;
            foreach (var r in c.Reports)
            { ApplyData(ws.Cell(row, 1), c.Owner); ApplyData(ws.Cell(row, 2), c.CaseNumber); ApplyData(ws.Cell(row, 3), c.Serial); ApplyData(ws.Cell(row, 4), r.ReportType); ApplyData(ws.Cell(row, 5), r.Stage); ApplyData(ws.Cell(row, 6), r.Engineer); ApplyData(ws.Cell(row, 7), r.Discipline); ApplyData(ws.Cell(row, 8), r.VisitDate); row++; }
            if (row - 1 > sr) { ws.Range(sr, 1, row - 1, 1).Merge(); ws.Range(sr, 2, row - 1, 2).Merge(); ws.Range(sr, 3, row - 1, 3).Merge(); }
        }
        for (int c = 1; c <= 8; c++) ws.Column(c).Width = 22; ws.SheetView.FreezeRows(1);
    }

    /// <summary>
        /// نوشتن صفحه پیگیری‌ها در اکسل
        /// </summary>
        private static void WriteFollowUp(IXLWorksheet ws, IReadOnlyList<Case> cases)
        {
            string[] h = { "ردیف", "شماره پرونده", "نام مالک", "آدرس", "همراه مالک", "تعداد گزارش", "توضیحات" };
            for (int c = 0; c < h.Length; c++) ApplyHeader(ws.Cell(1, c + 1), h[c]);
        
            // دریافت تعداد گزارش‌ها بر اساس شماره پرونده
            var reportCounts = new Dictionary<string, int>();
            // دریافت ویرایش‌های ذخیره شده
            var savedEdits = new Dictionary<string, string>();
            try
            {
                var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "nezam_monitor.db");
                if (System.IO.File.Exists(dbPath))
                {
                    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
                    conn.Open();
                
                    // دریافت تعداد گزارش‌ها
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT CaseNumber, COUNT(*) FROM GeneratedReports GROUP BY CaseNumber";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        reportCounts[reader.GetString(0)] = reader.GetInt32(1);
                    }
                
                    // دریافت توضیحات ذخیره شده
                    using var cmdEdit = conn.CreateCommand();
                    cmdEdit.CommandText = "SELECT CaseNumber, Description FROM FollowUpEdits WHERE Description != ''";
                    using var readerEdit = cmdEdit.ExecuteReader();
                    while (readerEdit.Read())
                    {
                        savedEdits[readerEdit.GetString(0)] = readerEdit.GetString(1);
                    }
                }
            }
            catch { }

            for (int r = 0; r < cases.Count; r++)
            {
                var c = cases[r];
                var reportCount = reportCounts.TryGetValue(c.CaseNumber, out var cnt) ? cnt : 0;
                var description = savedEdits.TryGetValue(c.CaseNumber, out var desc) ? desc : "";
            
                ApplyData(ws.Cell(r + 2, 1), r + 1);
                ApplyData(ws.Cell(r + 2, 2), c.CaseNumber);
                ApplyData(ws.Cell(r + 2, 3), c.Owner);
                ApplyData(ws.Cell(r + 2, 4), c.Specification?.Address ?? "");
                ApplyData(ws.Cell(r + 2, 5), c.OwnerMobile);
                ApplyData(ws.Cell(r + 2, 6), reportCount);
                ApplyData(ws.Cell(r + 2, 7), description);
            }
        
            for (int c = 1; c <= h.Length; c++) ws.Column(c).Width = 20;
            ws.RangeUsed()?.SetAutoFilter();
            ws.SheetView.FreezeRows(1);
        }
}
